using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — 동결 원장 <b>파일 쓰기</b>와 <b>워치독 스레드</b>를 실제로 돌려 잠근다.
    /// 시간 조건은 전부 <b>벽시계</b>로 기다린다(CLAUDE.md — 프레임 수 대기 금지).
    /// 임시 폴더는 지우지 않는다(이 저장소의 삭제 0건 관례).
    /// </summary>
    public sealed class FreezeForensicsWriterTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            FreezeWatchdog.ResetForTesting();
            FreezeForensicsLog.ResetForTesting();
            _dir = Path.Combine(Application.temporaryCachePath, "StickMateFreezeForensicsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            FreezeWatchdog.ResetForTesting();
            FreezeForensicsLog.ResetForTesting();
        }

        private static FreezeForensicsRecord Rec(FreezeForensicsEvent kind, string detail)
            => new FreezeForensicsRecord(kind, DateTime.UtcNow, 1.0, 1, 1, detail);

        private static void WriteMain(FreezeForensicsEvent kind, string detail)
            => FreezeForensicsLog.Write(FreezeForensicsChannel.Main, Rec(kind, detail));

        /// <summary>쓰는 쪽이 파일을 잡고 있어도 읽을 수 있게 공유 모드를 넓혀 읽는다.</summary>
        private static string[] ReadLinesShared(string path)
        {
            if (path == null || !File.Exists(path)) return Array.Empty<string>();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        return sr.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }
            return Array.Empty<string>();
        }

        private string[] AllSlotLines()
        {
            var lines = new List<string>();
            foreach (string f in Directory.GetFiles(_dir)) lines.AddRange(ReadLinesShared(f));
            return lines.ToArray();
        }

        private static bool WaitUntil(Func<bool> condition, double seconds)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        // ------------------------------------------------------------------ 원장

        [Test]
        public void 비활성이면_아무것도_쓰지_않는다()
        {
            WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "x");
            Assert.IsFalse(FreezeForensicsLog.IsActive);
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length);
        }

        [Test]
        public void 활성화만으로는_파일이_생기지_않는다()
        {
            FreezeForensicsLog.Activate(_dir, "header");
            Assert.IsTrue(FreezeForensicsLog.IsActive);
            Assert.IsNull(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main));
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length, "사건이 없는 세션은 디스크에 흔적을 남기지 않는다.");
        }

        [Test]
        public void 사건_밖의_적합_기록은_디스크에_쓰지_않는다()
        {
            // ★ verify-change (4) — 기동 첫 적합이 매 실행 파일을 만들어 12슬롯 이력을 밀어내던 것.
            FreezeForensicsLog.Activate(_dir, "h");
            WriteMain(FreezeForensicsEvent.SetResolution, "startup-fit");
            WriteMain(FreezeForensicsEvent.WindowResize, "startup-fit");
            WriteMain(FreezeForensicsEvent.WindowMove, "startup-fit");
            WriteMain(FreezeForensicsEvent.TransparencyReassign, "startup-fit");
            Assert.IsFalse(FreezeForensicsLog.IsIncidentWindowOpen);
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length, "사건이 없는 실행의 기동 적합이 파일을 만들었다.");
        }

        [Test]
        public void 사건이_열리면_맥락이_먼저_내려가고_창_안의_적합은_즉시_쓴다()
        {
            FreezeForensicsLog.Activate(_dir, "session-header-marker");
            WriteMain(FreezeForensicsEvent.SetResolution, "ctx-1");
            WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "t0-marker");

            string path = FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main);
            Assert.AreEqual(FreezeForensicsPolicy.SlotFileName(FreezeForensicsChannel.Main, 0), Path.GetFileName(path));
            string[] lines = ReadLinesShared(path);
            Assert.AreEqual(3, lines.Length, string.Join("\n", lines));
            StringAssert.Contains(nameof(FreezeForensicsEvent.SessionHeader), lines[0]);
            StringAssert.Contains("session-header-marker", lines[0]);
            StringAssert.Contains("ctx-1", lines[1]);
            StringAssert.Contains(FreezeForensicsPolicy.ContextMarker, lines[1]);
            StringAssert.Contains("t0-marker", lines[2]);
            StringAssert.DoesNotContain(FreezeForensicsPolicy.ContextMarker, lines[2]);
            Assert.IsTrue(FreezeForensicsLog.IsIncidentWindowOpen);

            WriteMain(FreezeForensicsEvent.WindowResize, "in-window");
            lines = ReadLinesShared(path);
            Assert.AreEqual(4, lines.Length, "사건 창 안의 적합은 즉시 디스크에 가야 한다(SetResolution 직전 줄의 목적).");
            StringAssert.Contains("in-window", lines[3]);
            StringAssert.DoesNotContain(FreezeForensicsPolicy.ContextMarker, lines[3]);
            StringAssert.Contains(FreezeForensicsPolicy.PreviousWriteCostLabel, lines[3],
                "앞 기록의 쓰기+flush 소요가 다음 줄에 실려야 계측이 현상을 건드린 크기가 원장에 남는다.");
        }

        [Test]
        public void 맥락_링이_넘치면_앞줄을_버리고_그_사실을_남긴다()
        {
            FreezeForensicsLog.Activate(_dir, "h");
            const int overflow = 3;
            for (int i = 0; i < FreezeForensicsPolicy.ContextBufferLines + overflow; i++)
                WriteMain(FreezeForensicsEvent.WindowMove, "ctx-" + i);
            WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "t0");

            string[] lines = ReadLinesShared(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main));
            Assert.AreEqual(1 + 1 + FreezeForensicsPolicy.ContextBufferLines + 1, lines.Length, string.Join("\n", lines));
            StringAssert.Contains(overflow + "줄", lines[1], "버린 줄 수가 남아야 한다.");
            Assert.IsFalse(lines.Any(l => l.EndsWith("| ctx-0 | " + FreezeForensicsPolicy.ContextMarker, StringComparison.Ordinal)),
                "가장 오래된 맥락은 버려졌어야 한다.");
            Assert.IsTrue(lines.Any(l => l.Contains("ctx-" + (FreezeForensicsPolicy.ContextBufferLines + overflow - 1))));
        }

        [Test]
        public void 줄마다_이_프로세스의_pid가_실린다()
        {
            FreezeForensicsLog.Activate(_dir, "h");
            WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "t0");
            int pid;
            using (var self = Process.GetCurrentProcess()) pid = self.Id;
            string[] lines = ReadLinesShared(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main));
            Assert.IsTrue(lines.All(l => l.Contains(" | pid=" + pid + " | ")), string.Join("\n", lines));
        }

        [Test]
        public void 상한을_넘으면_세션_슬롯_안에서만_돌고_이전_세션_파일은_보존한다()
        {
            const int slots = 5;
            FreezeForensicsLog.ConfigureLimitsForTesting(maxLines: 3, maxBytes: 1 << 20, slotCount: slots, maxSlotsPerSession: 2);
            var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < slots; i++)
            {
                string p = Path.Combine(_dir, FreezeForensicsPolicy.SlotFileName(FreezeForensicsChannel.Main, i));
                File.WriteAllText(p, "previous-session-" + i + "\n");
                File.SetLastWriteTimeUtc(p, baseTime.AddHours(i));   // 0번이 가장 오래됐다.
            }

            FreezeForensicsLog.Activate(_dir, "h");
            for (int k = 0; k < 10; k++) WriteMain(FreezeForensicsEvent.TopologySettled, "line-" + k);

            for (int i = 2; i < slots; i++)
            {
                string text = File.ReadAllText(Path.Combine(_dir, FreezeForensicsPolicy.SlotFileName(FreezeForensicsChannel.Main, i)));
                StringAssert.Contains("previous-session-" + i, text, $"슬롯 {i}는 이전 세션 기록이다 — 한 세션이 전부 덮어쓰면 안 된다.");
            }
            for (int i = 0; i < 2; i++)
            {
                string text = File.ReadAllText(Path.Combine(_dir, FreezeForensicsPolicy.SlotFileName(FreezeForensicsChannel.Main, i)));
                StringAssert.DoesNotContain("previous-session-", text, $"가장 오래된 두 슬롯({i})을 이번 세션이 써야 한다.");
            }

            string[] current = ReadLinesShared(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main));
            Assert.IsTrue(current.Any(l => l.Contains("| line-9")), "가장 최근 줄은 항상 남아야 한다.");
            Assert.IsTrue(AllSlotLines().Any(l => l.Contains("이어짐")), "이어지는 파일의 헤더가 앞 파일을 가리켜야 한다.");
        }

        [Test]
        public void 여러_스레드가_동시에_써도_줄이_온전하다()
        {
            FreezeForensicsLog.Activate(_dir, "h");
            const int threads = 4, perThread = 50;
            var workers = new Thread[threads];
            for (int n = 0; n < threads; n++)
            {
                int id = n;
                workers[n] = new Thread(() =>
                {
                    for (int k = 0; k < perThread; k++)
                        FreezeForensicsLog.Write(FreezeForensicsChannel.Main, Rec(FreezeForensicsEvent.Heartbeat, "t" + id + "-" + k));
                });
                workers[n].Start();
            }
            foreach (Thread w in workers) Assert.IsTrue(w.Join(20000));

            string[] lines = ReadLinesShared(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main));
            Assert.AreEqual(threads * perThread + 1, lines.Length);
            var details = lines.Skip(1)
                .Select(l => l.Split(new[] { " | " }, StringSplitOptions.None).First(p => p.StartsWith("t", StringComparison.Ordinal) && p.Contains("-")))
                .ToList();
            Assert.AreEqual(threads * perThread, details.Distinct().Count(), "줄이 섞이거나 잘렸다.");
        }

        [Test]
        public void 쓰기_실패는_예외로_새지_않고_그_채널만_끈다()
        {
            string notADirectory = Path.Combine(_dir, "i-am-a-file");
            File.WriteAllText(notADirectory, "x");
            FreezeForensicsLog.Activate(notADirectory, "h");
            Assert.DoesNotThrow(() => WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "x"));
            Assert.IsTrue(FreezeForensicsLog.IsChannelBroken(FreezeForensicsChannel.Main), "IO가 한 번 실패하면 이번 세션은 그 채널을 조용히 끈다.");
        }

        [Test]
        public void 메인_채널은_워치독_채널의_잠금을_기다리지_않는다()
        {
            // ★ verify-change (5) — 워치독이 사건 구간에 1초마다 잠금 안에서 flush하는 동안 메인 스레드가 그 잠금을
            //   기다리면, 계측이 SetResolution 직전 타이밍을 바꿔 증거를 오염시킨다.
            FreezeForensicsLog.Activate(_dir, "h");
            object watchdogGate = FreezeForensicsLog.ChannelGateForTesting(FreezeForensicsChannel.Watchdog);
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                var holder = new Thread(() =>
                {
                    lock (watchdogGate)
                    {
                        entered.Set();
                        release.Wait(TimeSpan.FromSeconds(8));
                    }
                });
                holder.Start();
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "잠금을 쥐는 스레드가 시작하지 못했다.");

                var sw = Stopwatch.StartNew();
                bool active = FreezeForensicsLog.IsActive;
                double activeMs = sw.Elapsed.TotalMilliseconds;
                WriteMain(FreezeForensicsEvent.TopologyChangeDetected, "main-not-blocked");
                double writeSeconds = sw.Elapsed.TotalSeconds;
                bool landed = ReadLinesShared(FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Main))
                    .Any(l => l.Contains("main-not-blocked"));

                release.Set();
                Assert.IsTrue(holder.Join(TimeSpan.FromSeconds(10)));

                Assert.IsTrue(active);
                Assert.Less(activeMs, 200.0, "IsActive가 잠금을 기다렸다.");
                Assert.Less(writeSeconds, 3.0, "메인 채널 쓰기가 워치독 채널 잠금을 기다렸다.");
                Assert.IsTrue(landed, "워치독 잠금이 잡힌 동안에도 메인 줄은 디스크에 있어야 한다.");
            }
        }

        // ------------------------------------------------------------------ 워치독 스레드

        private void StartFastWatchdog(double stall, double window, double interval)
        {
            FreezeForensicsLog.Activate(_dir, "h");
            FreezeWatchdog.ConfigureTimingForTesting(pollSeconds: 0.02, stallSeconds: stall, clockJumpSeconds: 5.0,
                heartbeatWindowSeconds: window, heartbeatIntervalSeconds: interval);
            FreezeWatchdog.Start();
        }

        [Test]
        public void 메인이_진행하고_사건이_없으면_워치독은_디스크에_쓰지_않는다()
        {
            StartFastWatchdog(stall: 0.3, window: 0.5, interval: 0.1);
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 1.0)
            {
                FreezeWatchdog.PublishMainFrame(sw.Elapsed.TotalSeconds);
                Thread.Sleep(10);
            }
            Assert.IsTrue(FreezeWatchdog.Stop(2000));
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length, "정상 상주 중 디스크 쓰기 0 — 24시간 앱의 전제다.");
        }

        [Test]
        public void 메인이_멈추면_워치독_채널에_정지_줄을_쓰고_재개하면_재개_줄을_쓴다()
        {
            FreezeWatchdog.PublishMainFrame(0.0);
            StartFastWatchdog(stall: 0.3, window: 0.5, interval: 0.1);

            bool stalled = WaitUntil(() => AllSlotLines().Any(l => l.Contains(nameof(FreezeForensicsEvent.MainThreadStall))), 5.0);
            Assert.IsTrue(stalled, "메인 프레임이 멈췄는데 정지 줄이 없다.");
            string watchdogFile = FreezeForensicsLog.CurrentFilePath(FreezeForensicsChannel.Watchdog);
            Assert.IsNotNull(watchdogFile);
            StringAssert.StartsWith(FreezeForensicsPolicy.SlotFileName(FreezeForensicsChannel.Watchdog, 0), Path.GetFileName(watchdogFile));
            string stallLine = AllSlotLines().First(l => l.Contains(nameof(FreezeForensicsEvent.MainThreadStall)));
            StringAssert.Contains("단계=", stallLine);
            StringAssert.Contains("열린구간=", stallLine);
            StringAssert.Contains("기록스레드=워치독", stallLine);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 0.3)
            {
                FreezeWatchdog.PublishMainFrame(sw.Elapsed.TotalSeconds);
                Thread.Sleep(10);
            }
            bool resumed = WaitUntil(() => AllSlotLines().Any(l => l.Contains(nameof(FreezeForensicsEvent.MainThreadResumed))), 5.0);
            Assert.IsTrue(resumed, "메인이 재개했는데 재개 줄이 없다.");

            Assert.IsTrue(FreezeWatchdog.Stop(2000));
            Assert.IsFalse(FreezeWatchdog.IsRunning);
        }

        [Test]
        public void 토폴로지_사건_뒤_창_동안만_하트비트를_쓴다()
        {
            const double window = 0.5, interval = 0.1;
            StartFastWatchdog(stall: 5.0, window: window, interval: interval);
            FreezeWatchdog.MarkEpisodeStart();
            Assert.IsTrue(FreezeForensicsLog.IsIncidentWindowOpen, "t0는 원장의 사건 창도 함께 열어야 한다.");

            var sw = Stopwatch.StartNew();
            int countAtWindowEnd = -1;
            while (sw.Elapsed.TotalSeconds < window + 1.2)
            {
                FreezeWatchdog.PublishMainFrame(sw.Elapsed.TotalSeconds);
                if (countAtWindowEnd < 0 && sw.Elapsed.TotalSeconds > window + 0.4)
                {
                    countAtWindowEnd = AllSlotLines().Count(l => l.Contains(nameof(FreezeForensicsEvent.Heartbeat)));
                }
                Thread.Sleep(10);
            }
            int countLater = AllSlotLines().Count(l => l.Contains(nameof(FreezeForensicsEvent.Heartbeat)));
            Assert.IsTrue(FreezeWatchdog.Stop(2000));

            int max = (int)Math.Ceiling(window / interval) + 2;
            Assert.That(countAtWindowEnd, Is.InRange(2, max), $"창 안 하트비트 {countAtWindowEnd}개");
            Assert.AreEqual(countAtWindowEnd, countLater, "창이 닫힌 뒤에도 하트비트를 쓴다.");
        }

        [Test]
        public void 정지는_여러_번_불러도_안전하고_스레드가_끝난다()
        {
            StartFastWatchdog(stall: 5.0, window: 0.5, interval: 0.1);
            Assert.IsTrue(FreezeWatchdog.IsRunning);
            FreezeWatchdog.Start();   // 두 번째 시작은 새 스레드를 만들지 않는다.
            Assert.IsTrue(FreezeWatchdog.Stop(2000));
            Assert.IsFalse(FreezeWatchdog.IsRunning);
            Assert.IsTrue(FreezeWatchdog.Stop(2000));
        }
    }
}
