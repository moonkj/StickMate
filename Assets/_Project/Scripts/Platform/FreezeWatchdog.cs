using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 — 메인 스레드 정지를 <b>다른 스레드에서</b> 보는 워치독.
    ///
    /// <para><b>왜 별도 스레드인가.</b> 메인 스레드 계측(<see cref="StallAttribution"/>)은 메인이 돌아온 다음
    /// 프레임에만 기록한다. 영구 정지에서는 아무것도 남지 않는다. 이 스레드는 메인의 프레임 카운터만 보고,
    /// 멈추면 그때 한 줄을 쓴다. 그리고 <b>시스템 전체가 멈추면 이 스레드도 멈춘다</b> — 그래서 토폴로지 변화
    /// 직후 하트비트가 <b>어느 초에서 끊겼는가</b>가 곧 증거가 된다.</para>
    ///
    /// <para><b>이 스레드가 절대 하지 않는 것.</b> Unity API 호출(메인 스레드 전용이다), 정상일 때의 디스크 쓰기
    /// (24시간 상주 앱 — SSD 쓰기·전력), 예외 전파(앱을 죽이지 않는다).</para>
    ///
    /// <para><b>메인이 발행하는 값</b>은 프레임 경계 탐침(<c>StallAttributionProbe.cs</c>의
    /// <c>StallFrameBeginProbe</c>/<c>StallFrameEndProbe</c>)이 원자적 쓰기로만 넣는다. 탐침이 없으면
    /// 카운터가 0에 머물고, 판정기는 1 미만에서 무장하지 않으므로 거짓 정지가 나지 않는다.</para>
    /// </summary>
    public static class FreezeWatchdog
    {
        private const string ThreadName = "StickMate.FreezeWatchdog";

        private static readonly object LifeGate = new object();
        private static Thread s_thread;
        private static volatile bool s_stopRequested;
        private static AutoResetEvent s_wake;

        private static long s_mainFrame;
        private static long s_mainRealtimeBits = BitConverter.DoubleToInt64Bits(-1.0);
        private static int s_mainPhase;
        private static long s_episodeStartTicks = -1;

        private static double s_pollSeconds = FreezeForensicsPolicy.WatchdogPollSeconds;
        private static double s_stallSeconds = FreezeForensicsPolicy.StallThresholdSeconds;
        private static double s_clockJumpSeconds = FreezeForensicsPolicy.WatchdogClockJumpSeconds;
        private static double s_heartbeatWindowSeconds = FreezeForensicsPolicy.HeartbeatWindowSeconds;
        private static double s_heartbeatIntervalSeconds = FreezeForensicsPolicy.HeartbeatIntervalSeconds;

        private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;

        /// <summary>스레드가 살아 있는가(진단/테스트).</summary>
        public static bool IsRunning
        {
            get
            {
                lock (LifeGate) return s_thread != null && s_thread.IsAlive;
            }
        }

        /// <summary>메인 스레드가 지금까지 발행한 프레임 수(진단/테스트).</summary>
        public static long PublishedFrames => Interlocked.Read(ref s_mainFrame);

        // ------------------------------------------------------------------------------------
        // 메인 스레드 발행 — 원자적 쓰기만. 할당 0.
        // ------------------------------------------------------------------------------------

        /// <summary>Update 시작(실행 순서 -30000)에서 부른다.</summary>
        public static void PublishMainFrame(double realtimeSinceStartup)
        {
            Interlocked.Increment(ref s_mainFrame);
            Interlocked.Exchange(ref s_mainRealtimeBits, BitConverter.DoubleToInt64Bits(realtimeSinceStartup));
            Volatile.Write(ref s_mainPhase, (int)MainThreadPhase.Update);
        }

        /// <summary>프레임 단계 경계에서 부른다.</summary>
        public static void PublishPhase(MainThreadPhase phase) => Volatile.Write(ref s_mainPhase, (int)phase);

        /// <summary>토폴로지 변화 감지(t0). 하트비트 창을 연다(다시 부르면 창이 새로 시작된다).</summary>
        public static void MarkEpisodeStart() => Interlocked.Exchange(ref s_episodeStartTicks, Stopwatch.GetTimestamp());

        // ------------------------------------------------------------------------------------
        // 수명
        // ------------------------------------------------------------------------------------

        public static void Start()
        {
            lock (LifeGate)
            {
                if (s_thread != null && s_thread.IsAlive) return;
                s_stopRequested = false;
                s_wake = new AutoResetEvent(false);
                s_thread = new Thread(Run) { IsBackground = true, Name = ThreadName };
                s_thread.Start();
            }
        }

        /// <summary>멈추고 합류를 기다린다. 제때 끝났으면 true. 여러 번 불러도 안전하다.</summary>
        public static bool Stop(int joinMilliseconds)
        {
            Thread t;
            lock (LifeGate)
            {
                t = s_thread;
                s_stopRequested = true;
                s_wake?.Set();
            }
            if (t == null) return true;
            bool joined = t.Join(Math.Max(0, joinMilliseconds));
            lock (LifeGate)
            {
                if (s_thread == t) s_thread = null;
            }
            return joined;
        }

        private static void Run()
        {
            var tracker = new FreezeWatchdogTracker();
            double lastHeartbeat = double.NegativeInfinity;
            int consecutiveFailures = 0;

            while (!s_stopRequested)
            {
                try
                {
                    double now = Stopwatch.GetTimestamp() * TicksToSeconds;
                    long frame = Interlocked.Read(ref s_mainFrame);
                    FreezeWatchdogVerdict verdict = tracker.Observe(frame, now, s_stallSeconds, s_clockJumpSeconds);

                    long episodeTicks = Interlocked.Read(ref s_episodeStartTicks);
                    double episodeStart = episodeTicks < 0 ? -1.0 : episodeTicks * TicksToSeconds;

                    if (verdict == FreezeWatchdogVerdict.StallStarted)
                    {
                        WriteLine(FreezeForensicsEvent.MainThreadStall, frame, now, episodeStart,
                            "메인 스레드가 " + Seconds(tracker.StalledSeconds) + "초째 한 프레임도 진행하지 않음");
                    }
                    else if (verdict == FreezeWatchdogVerdict.StallEnded)
                    {
                        WriteLine(FreezeForensicsEvent.MainThreadResumed, frame, now, episodeStart,
                            "메인 스레드 재개 — 정지 " + Seconds(tracker.StalledSeconds) + "초");
                    }

                    if (FreezeForensicsPolicy.ShouldWriteHeartbeat(now, episodeStart, lastHeartbeat,
                            s_heartbeatWindowSeconds, s_heartbeatIntervalSeconds))
                    {
                        lastHeartbeat = now;
                        WriteLine(FreezeForensicsEvent.Heartbeat, frame, now, episodeStart,
                            "워치독 생존, 메인 무진행 " + Seconds(tracker.SecondsSinceAdvance(now)) + "초");
                    }

                    consecutiveFailures = 0;
                }
                catch (Exception)
                {
                    // 진단 장치가 앱을 죽이면 안 된다. 반복해서 실패하면 조용히 끝낸다.
                    if (++consecutiveFailures >= 5) break;
                }

                AutoResetEvent wake = s_wake;
                if (wake == null) break;
                wake.WaitOne(TimeSpan.FromSeconds(s_pollSeconds));
            }
        }

        private static void WriteLine(FreezeForensicsEvent kind, long frame, double now, double episodeStart, string what)
        {
            double mainRealtime = BitConverter.Int64BitsToDouble(Interlocked.Read(ref s_mainRealtimeBits));
            var phase = (MainThreadPhase)Volatile.Read(ref s_mainPhase);
            string detail = what +
                " / 단계=" + FreezeForensicsPolicy.DescribePhase(phase) +
                " / 열린구간=" + DescribeOpenSection(StallAttribution.ProbeOpenSectionForWatchdog()) +
                " / t0+" + (episodeStart >= 0.0 ? Seconds(now - episodeStart) + "초" : "없음") +
                " / 기록스레드=워치독";
            FreezeForensicsLog.Write(new FreezeForensicsRecord(kind, DateTime.UtcNow, mainRealtime, frame, -1, detail));
        }

        private static string Seconds(double s) => s < 0.0 ? "?" : s.ToString("F1", CultureInfo.InvariantCulture);

        /// <summary><see cref="StallAttribution.ProbeOpenSectionForWatchdog"/>의 반환값을 이름으로.</summary>
        internal static string DescribeOpenSection(int id)
        {
            if (id == -1) return "없음";
            if (id == -2) return "깊이초과";
            if (id >= 0 && id < (int)StallSection.Count) return ((StallSection)id).ToString();
            return "알수없음(" + id.ToString(CultureInfo.InvariantCulture) + ")";
        }

        // ------------------------------------------------------------------------------------
        // 테스트 전용
        // ------------------------------------------------------------------------------------

        internal static void ConfigureTimingForTesting(double pollSeconds, double stallSeconds, double clockJumpSeconds,
            double heartbeatWindowSeconds, double heartbeatIntervalSeconds)
        {
            s_pollSeconds = pollSeconds;
            s_stallSeconds = stallSeconds;
            s_clockJumpSeconds = clockJumpSeconds;
            s_heartbeatWindowSeconds = heartbeatWindowSeconds;
            s_heartbeatIntervalSeconds = heartbeatIntervalSeconds;
        }

        internal static void ResetForTesting()
        {
            Stop(2000);
            s_pollSeconds = FreezeForensicsPolicy.WatchdogPollSeconds;
            s_stallSeconds = FreezeForensicsPolicy.StallThresholdSeconds;
            s_clockJumpSeconds = FreezeForensicsPolicy.WatchdogClockJumpSeconds;
            s_heartbeatWindowSeconds = FreezeForensicsPolicy.HeartbeatWindowSeconds;
            s_heartbeatIntervalSeconds = FreezeForensicsPolicy.HeartbeatIntervalSeconds;
            Interlocked.Exchange(ref s_mainFrame, 0);
            Interlocked.Exchange(ref s_mainRealtimeBits, BitConverter.DoubleToInt64Bits(-1.0));
            Volatile.Write(ref s_mainPhase, (int)MainThreadPhase.Unknown);
            Interlocked.Exchange(ref s_episodeStartTicks, -1);
        }
    }

    /// <summary>
    /// ★ 2026-09-14 — 동결 계측 기동. 데스크톱 플레이어(Windows/macOS)에서만 켠다
    /// (<see cref="FreezeForensicsPolicy.ShouldActivate"/>). 두 플랫폼이 <b>같은 코드</b>로 켜진다.
    /// </summary>
    public static class FreezeForensicsBootstrap
    {
        private static bool s_installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            bool desktopPlayer = Application.platform == RuntimePlatform.WindowsPlayer
                || Application.platform == RuntimePlatform.OSXPlayer;
            if (!FreezeForensicsPolicy.ShouldActivate(Application.isEditor, desktopPlayer)) return;
            if (s_installed) return;
            s_installed = true;

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, FreezeForensicsPolicy.DirectoryName);
                int pid = 0;
                try { using (var self = Process.GetCurrentProcess()) pid = self.Id; } catch (Exception) { }

                string header = "StickMate " + Application.version +
                    " / " + Application.platform +
                    " / OS=" + SystemInfo.operatingSystem +
                    " / GPU=" + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")" +
                    " / pid " + pid.ToString(CultureInfo.InvariantCulture) +
                    " / 세션 시작 UTC " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) +
                    " / 이 파일은 멈춤 진단용입니다(개인 정보 없음)";

                FreezeForensicsLog.Activate(directory, header);
                FreezeWatchdog.Start();
                Application.quitting += OnQuitting;

                Debug.Log($"{FreezeForensicsPolicy.LogTag} 활성 — 폴더 {directory} " +
                    $"(파일은 첫 사건 때 생깁니다, 슬롯 {FreezeForensicsPolicy.SlotCount}개 링). " +
                    "앱이나 컴퓨터가 멈추면 이 폴더를 통째로 보내 주세요. " +
                    $"정상 상주 중에는 워치독이 디스크에 쓰지 않습니다(정지 {FreezeForensicsPolicy.StallThresholdSeconds:F0}초 이상 / " +
                    $"화면 구성 변화 후 {FreezeForensicsPolicy.HeartbeatWindowSeconds:F0}초만).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{FreezeForensicsPolicy.LogTag} 켜지 못했습니다(계측 없이 계속 진행): {e.GetType().Name}: {e.Message}");
            }
        }

        private static void OnQuitting()
        {
            // 종료 중에는 프레임이 멈춘다 — 먼저 워치독을 세워 거짓 정지 줄을 막는다.
            FreezeWatchdog.Stop(1000);
        }
    }
}
