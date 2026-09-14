using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace StickMate.Platform
{
    /// <summary>직전 실행이 어떻게 끝났나(다음 실행의 기동에서 판정).</summary>
    public enum PreviousSessionVerdict
    {
        /// <summary>표지가 없다 — 이 기능이 처음 도는 실행이다. 판단 근거가 없으니 아무것도 하지 않는다.</summary>
        NoMarker = 0,
        /// <summary>직전 실행이 종료 순서(<c>Application.quitting</c> 또는 Windows <c>WM_ENDSESSION</c>)를 끝까지 돌았다.</summary>
        CleanExit,
        /// <summary>표지가 "실행 중"인데 그 프로세스가 없다 — 크래시·강제 종료·전원 차단·PC 전체 정지 중 하나.</summary>
        AbnormalExit,
        /// <summary>표지의 pid가 지금 살아 있는 같은 앱이다 — 동시 실행이지 비정상 종료가 아니다.</summary>
        OtherInstanceAlive,
        /// <summary>표지를 해석할 수 없다 — 추측하지 않는다(복사하지 않는다).</summary>
        Unreadable,
        /// <summary>
        /// ★ 4차 — 표지가 "종료 시작"에서 멈췄다. 종료 순서를 시작했지만 끝내지 못하고 끊겼다(추정 경로: 셸이 먼저 내려가
        /// 작업표시줄 원복의 동기 셸 호출이 돌아오지 않은 채 세션 종료로 강제 종료). <b>세션 중 멈춤이 아니므로 복사하지 않는다</b>
        /// — 이 경로가 매일 밤 반복되는 사용자에게 매일 아침 4MB 복사가 일어나면 안 된다.
        /// </summary>
        ExitStartedNotFinished,
    }

    /// <summary>
    /// ★ 2026-09-14 (3차 D, persona-stress R-6) — 정상 종료 표지와 "비정상 종료 다음 실행의 <c>Player-prev.log</c> 회수" 판정.
    /// 순수 함수(플랫폼 중립) — EditMode가 실행해 잠근다.
    ///
    /// <para><b>왜 필요한가.</b> Unity는 실행할 때마다 <c>Player.log</c>를 <c>Player-prev.log</c>로 밀어낸다. PC가 멈춰
    /// 강제로 전원을 끈 사용자가 앱을 <b>두 번</b> 켜면 멈춘 실행의 로그가 사라진다
    /// (<c>docs/verify/WINDOWS_CHECK_SESSION.md</c> H-3이 "StickMate를 켜지 마세요"라고 적어야 했던 이유).
    /// 그래서 비정상 종료 <b>바로 다음 실행</b>이 그 파일을 우리 폴더로 <b>복사</b>해 둔다.</para>
    ///
    /// <para><b>왜 표지를 종료 순서 안에 두나(R-6).</b> 표지를 <c>Application.quitting</c>에만 찍으면, Unity가 Windows
    /// 로그오프·시스템 종료에서 quitting을 부르지 않는 경우(실기 미확인) 매일 밤 PC를 끄는 사용자가 매일 아침
    /// "비정상 종료"로 오판된다. 그래서 <see cref="AppShutdownSequence"/> 안에 두어 두 입구가 같이 찍는다.</para>
    ///
    /// <para><b>표지 세 상태(4차).</b> 기동 "실행 중"(디스크 동기화) → 종료 순서 맨 앞 "종료 시작"(동기화 <b>없음</b> — 원복 앞에
    /// 기다리는 단계를 두지 않는다) → 종료 순서 맨 끝 "정상 종료"(동기화). 두 표지 사이(원복·워치독 정지·진행 저장)에서 끊기면 "종료 시작"이 남는다
    /// — 어느 단계에서인지는 표지로 가를 수 없다.</para>
    ///
    /// <para><b>한 실행에 한 번 끝낸다(5차).</b> 세션 종료 처리 뒤 앱 종료 요청으로 종료 순서가 한 번 더 돌 때, 이번 실행이 정상 종료 표지를 이미 썼으면
    /// 두 번째 순서는 표지 두 단계를 건너뛴다(<see cref="CleanExitWrittenThisRun"/> → <see cref="AppShutdownSequence.ShouldRunStep"/>; ★ 5-c: 진행 저장 단계도 건너뛴다 —
    /// 표지가 선 뒤에 저장이 돌다 끊기면 표지가 저장보다 앞서 거짓말한다). 4차까지는 두 번째 순서가
    /// "정상 종료"를 "종료 시작"으로 덮어써, 그 틈에 끊기면 판정이 원복 도중 끊김과 구별되지 않았다.
    /// ★ 5-b: 종료 순서는 이 기록을 순서 시작이 아니라 <b>각 표지 단계 직전에</b> 읽는다 — 한 순서가 기다리는 사이 끼어든 순서가 먼저 표지를 쓰면
    /// 재개된 순서가 그 줄(<c>trigger=</c>)을 덮지 않는다. 진행 저장이 도는 중에는 정상 종료 표지를 쓰지 않는다(<see cref="AppShutdownSequence"/> 문서).</para>
    ///
    /// <para><b>원칙 3.</b> 원본(<c>Player-prev.log</c>)은 읽기만 하고, 남의 읽기·쓰기·이름 바꾸기를 막지 않는 공유 모드(<c>FileShare.ReadWrite | FileShare.Delete</c>)로 연다 —
    /// ★ 5차 정정: 이 공유 모드가 실제로 막지 않는지 이 개발 머신(Unity Mono)이 <b>실행으로 확인하는 것은 열기(읽기·쓰기) 축뿐</b>이고, 이름 바꾸기·삭제 축은
    /// 형태 감사 + Windows 실기 항목이다(<c>SessionExitMarkerTests</c> 문서). 쓰는
    /// 곳은 우리 폴더(<c>persistentDataPath/FreezeForensics</c>)의 표지 파일 1개와 복사본 슬롯 <see cref="CopySlotCount"/>개뿐이고,
    /// 슬롯은 덮어쓰기 링이라 삭제·이동이 없다. 사용자에게 알림 UI를 띄우지 않는다(로그 한 줄뿐).</para>
    /// </summary>
    public static class SessionExitMarkerPolicy
    {
        /// <summary>표지 파일 이름(우리 폴더 안).</summary>
        public const string MarkerFileName = "session-exit-marker.txt";
        public const string RunningState = "running";
        /// <summary>종료 순서를 시작했다(4차).</summary>
        public const string ExitStartedState = "exit-started";
        public const string CleanExitState = "clean-exit";

        /// <summary>Unity가 직전 실행 로그에 붙이는 이름(<c>Application.consoleLogPath</c>와 같은 폴더).</summary>
        public const string PreviousPlayerLogFileName = "Player-prev.log";

        /// <summary>복사본 파일 접두사.</summary>
        public const string CopyFilePrefix = "previous-abnormal-player-";

        /// <summary>복사본 슬롯 수(가장 오래된 것을 덮어쓴다).</summary>
        public const int CopySlotCount = 3;

        /// <summary>복사 상한(바이트). 넘으면 <b>끝부분</b>만 복사한다 — 멈춘 순간은 로그의 끝에 있다.</summary>
        public const long MaxCopyBytes = 4L * 1024 * 1024;

        public static string CopySlotFileName(int slot)
            => CopyFilePrefix + slot.ToString("00", CultureInfo.InvariantCulture) + ".log";

        public static string FormatRunning(int pid, DateTime utc)
            => string.Format(CultureInfo.InvariantCulture, "state={0} pid={1} utc={2:o}", RunningState, pid, utc.ToUniversalTime());

        public static string FormatExitStarted(int pid, AppShutdownTrigger trigger, DateTime utc)
            => string.Format(CultureInfo.InvariantCulture, "state={0} pid={1} trigger={2} utc={3:o}", ExitStartedState, pid, trigger, utc.ToUniversalTime());

        public static string FormatCleanExit(int pid, AppShutdownTrigger trigger, DateTime utc)
            => string.Format(CultureInfo.InvariantCulture, "state={0} pid={1} trigger={2} utc={3:o}", CleanExitState, pid, trigger, utc.ToUniversalTime());

        /// <summary>표지 한 줄을 읽는다. <c>state=</c>와 <c>pid=</c>가 둘 다 있어야 성공이다.</summary>
        public static bool TryParse(string text, out string state, out int pid)
        {
            state = null;
            pid = 0;
            if (string.IsNullOrEmpty(text)) return false;
            bool hasPid = false;
            foreach (string token in text.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("state=", StringComparison.Ordinal)) state = token.Substring("state=".Length);
                else if (token.StartsWith("pid=", StringComparison.Ordinal))
                    hasPid = int.TryParse(token.Substring("pid=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
            }
            return !string.IsNullOrEmpty(state) && hasPid;
        }

        /// <param name="markerExists">표지 파일이 있는가.</param>
        /// <param name="markerText">표지 내용(없으면 null).</param>
        /// <param name="currentPid">이번 실행의 pid — 표지의 pid가 우리 자신이면 "다른 인스턴스"가 아니다(pid 재사용).</param>
        /// <param name="isSameAppAlive">그 pid가 지금 살아 있는 같은 앱인가(사실 조회).</param>
        public static PreviousSessionVerdict Classify(bool markerExists, string markerText, int currentPid, Func<int, bool> isSameAppAlive)
        {
            if (!markerExists) return PreviousSessionVerdict.NoMarker;
            if (!TryParse(markerText, out string state, out int pid)) return PreviousSessionVerdict.Unreadable;
            if (state == CleanExitState) return PreviousSessionVerdict.CleanExit;
            if (state == ExitStartedState) return PreviousSessionVerdict.ExitStartedNotFinished;
            if (state != RunningState) return PreviousSessionVerdict.Unreadable;
            if (pid > 0 && pid != currentPid && isSameAppAlive != null && isSameAppAlive(pid)) return PreviousSessionVerdict.OtherInstanceAlive;
            return PreviousSessionVerdict.AbnormalExit;
        }

        public static bool ShouldCopyPreviousPlayerLog(PreviousSessionVerdict verdict) => verdict == PreviousSessionVerdict.AbnormalExit;

        /// <summary>복사를 시작할 원본 위치. 상한을 넘으면 끝에서 상한만큼.</summary>
        public static long CopyStartOffset(long sourceLength, long maxBytes)
        {
            if (sourceLength <= 0 || maxBytes <= 0) return Math.Max(0, sourceLength);
            return sourceLength > maxBytes ? sourceLength - maxBytes : 0;
        }
    }

    /// <summary>
    /// ★ 2026-09-14 (3차 D) — 표지 읽기·쓰기와 <c>Player-prev.log</c> 복사(실행부). 판정은 <see cref="SessionExitMarkerPolicy"/>.
    /// 경로는 전부 인자로 받는다 — Unity 경로 조회는 기동부(<c>FreezeForensicsBootstrap</c>)가 한다(EditMode가 임시 폴더로 실행한다).
    /// </summary>
    public static class SessionExitMarker
    {
        public readonly struct StartupResult
        {
            public readonly PreviousSessionVerdict Verdict;
            /// <summary>복사본 파일 이름(복사하지 않았으면 null). 사용자 경로가 섞이지 않게 이름만 남긴다.</summary>
            public readonly string CopiedFileName;
            public readonly long SourceBytes;
            public readonly long CopiedBytes;
            public readonly bool Truncated;
            /// <summary>복사하지 않았거나 실패한 이유(사람이 읽는 한 줄).</summary>
            public readonly string Note;

            public StartupResult(PreviousSessionVerdict verdict, string copiedFileName, long sourceBytes, long copiedBytes, bool truncated, string note)
            {
                Verdict = verdict;
                CopiedFileName = copiedFileName;
                SourceBytes = sourceBytes;
                CopiedBytes = copiedBytes;
                Truncated = truncated;
                Note = note;
            }
        }

        private static readonly object Gate = new object();
        private static string s_directory;
        private static int s_pid;
        private static bool s_cleanExitWritten;

        /// <summary>
        /// 테스트 전용 — 원본을 연 <b>동안</b> 불린다(원본 경로). 남의 핸들을 막는지 실행으로 재는 탐침 자리.
        /// ★ 5차 정정: 이 러너(Unity Mono)가 그 탐침으로 잴 수 있는 축은 열기(읽기·쓰기)뿐이다 — <c>SessionExitMarkerTests</c> 문서.
        /// </summary>
        internal static Action<string> SourceOpenedForTesting;

        /// <summary>이번 실행의 표지가 켜졌는가(기동이 "실행 중" 표지를 썼다).</summary>
        public static bool IsStarted
        {
            get { lock (Gate) return s_directory != null; }
        }

        /// <summary>
        /// ★ 5차 — 이번 실행(마지막 기동 표지 이후)이 정상 종료 표지를 <b>디스크에 실제로 썼는가</b>. 종료 순서가 두 번 돌 때(세션 종료 처리 →
        /// 앱 종료 요청 → quitting) 두 번째 순서가 표지 두 단계와 ★ 5-c 진행 저장 단계를 건너뛰는 근거다(<see cref="AppShutdownSequence.ShouldRunStep"/>).
        /// 쓰기에 실패했으면 거짓으로 남아 두 번째 순서가 진행 저장과 표지를 다시 돈다. 기동(<see cref="RunStartup(string,int,string,Func{int,bool})"/>)이 거짓으로 되돌린다.
        /// ★ 5-b(verify-change 5차 생존 변이 S1): "쓰기가 돌아온 뒤에만"은 <c>SessionEndShutdownTests.S1_…</c>가 표지 자리에 같은 이름의 폴더를 세워
        /// 쓰기를 <b>실제로 실패</b>시켜 잠근다 — 5차에는 이 기록을 쓰기 앞으로 옮겨도 전량 초록이었다.
        /// </summary>
        public static bool CleanExitWrittenThisRun
        {
            get { lock (Gate) return s_cleanExitWritten; }
        }

        /// <summary>기동: 직전 표지 판정 → (비정상이면) 직전 로그 복사 → 이번 실행의 "실행 중" 표지. 던지지 않는다.</summary>
        public static StartupResult RunStartup(string directory, int pid, string previousPlayerLogPath, Func<int, bool> isSameAppAlive)
            => RunStartup(directory, pid, previousPlayerLogPath, isSameAppAlive, SessionExitMarkerPolicy.MaxCopyBytes, DateTime.UtcNow);

        internal static StartupResult RunStartup(string directory, int pid, string previousPlayerLogPath,
            Func<int, bool> isSameAppAlive, long maxCopyBytes, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(directory))
                return new StartupResult(PreviousSessionVerdict.NoMarker, null, 0, 0, false, "폴더 없음");

            PreviousSessionVerdict verdict = PreviousSessionVerdict.Unreadable;
            string copied = null, note = null;
            long sourceBytes = 0, copiedBytes = 0;
            bool truncated = false;
            try
            {
                Directory.CreateDirectory(directory);
                string markerPath = Path.Combine(directory, SessionExitMarkerPolicy.MarkerFileName);
                bool exists = File.Exists(markerPath);
                string text = exists ? ReadSmallText(markerPath) : null;
                verdict = SessionExitMarkerPolicy.Classify(exists, text, pid, isSameAppAlive);

                if (SessionExitMarkerPolicy.ShouldCopyPreviousPlayerLog(verdict))
                {
                    copied = TryCopyTail(previousPlayerLogPath, directory, maxCopyBytes, verdict, utcNow,
                        out sourceBytes, out copiedBytes, out truncated, out note);
                }
                else if (verdict == PreviousSessionVerdict.ExitStartedNotFinished)
                {
                    note = "직전 실행이 종료를 시작했지만 끝내지 못했습니다(종료 중 끊김 — 셸 선종료 등 추정). 세션 중 멈춤이 아니라 복사하지 않습니다";
                }

                // 복사 <b>뒤에</b> 쓴다 — 복사 도중 죽으면 옛 "실행 중" 표지가 남아 다음 실행이 다시 판정한다.
                WriteMarker(markerPath, SessionExitMarkerPolicy.FormatRunning(pid, utcNow), flushToDisk: true);
                lock (Gate)
                {
                    s_directory = directory;
                    s_pid = pid;
                    s_cleanExitWritten = false;   // 새 "실행 중" 표지 — 이 실행은 아직 정상 종료 표지를 쓰지 않았다(5차).
                }
            }
            catch (Exception e)
            {
                note = (note == null ? "" : note + " / ") + "표지 처리 실패: " + e.GetType().Name;
            }
            return new StartupResult(verdict, copied, sourceBytes, copiedBytes, truncated, note);
        }

        /// <summary>
        /// ★ 4차 — 종료 순서의 <b>맨 앞</b>. 우리 폴더의 한 줄을 OS에 넘기고 끝난다 — <b>디스크 동기화(<c>Flush(true)</c>)를 하지 않는다</b>.
        /// 프로세스가 끊겨도 OS가 받은 쓰기는 남고(잃는 것은 전원 차단뿐), 그래서 작업표시줄 원복 앞에 기다리는 단계가 생기지 않는다.
        /// 기동이 표지를 켜지 않았으면 아무것도 하지 않는다. 던지지 않는다.
        /// </summary>
        public static bool WriteExitStarted(AppShutdownTrigger trigger)
        {
            if (!TryGetStarted(out string directory, out int pid)) return false;
            try
            {
                WriteMarker(Path.Combine(directory, SessionExitMarkerPolicy.MarkerFileName),
                    SessionExitMarkerPolicy.FormatExitStarted(pid, trigger, DateTime.UtcNow), flushToDisk: false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>종료 순서의 마지막 단계(디스크 동기화). 기동이 표지를 켜지 않았으면 아무것도 하지 않는다. 던지지 않는다.</summary>
        public static bool WriteCleanExit(AppShutdownTrigger trigger)
        {
            if (!TryGetStarted(out string directory, out int pid)) return false;
            try
            {
                WriteMarker(Path.Combine(directory, SessionExitMarkerPolicy.MarkerFileName),
                    SessionExitMarkerPolicy.FormatCleanExit(pid, trigger, DateTime.UtcNow), flushToDisk: true);
                lock (Gate)
                {
                    // 쓰기가 돌아온 뒤에만 — 실패했으면 두 번째 종료 순서가 다시 쓴다(5차, AppShutdownSequence.ShouldRunStep).
                    if (string.Equals(s_directory, directory, StringComparison.Ordinal)) s_cleanExitWritten = true;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>그 pid가 지금 살아 있는 같은 이름의 프로세스인가(사실 조회 — 읽기 전용).</summary>
        public static bool IsSameApplicationProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (Process other = Process.GetProcessById(pid))
                using (Process self = Process.GetCurrentProcess())
                {
                    return !other.HasExited
                        && string.Equals(other.ProcessName, self.ProcessName, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void ResetForTesting()
        {
            lock (Gate)
            {
                s_directory = null;
                s_pid = 0;
                s_cleanExitWritten = false;
            }
            SourceOpenedForTesting = null;
        }

        private static bool TryGetStarted(out string directory, out int pid)
        {
            lock (Gate)
            {
                directory = s_directory;
                pid = s_pid;
            }
            return directory != null;
        }

        private static string ReadSmallText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[(int)Math.Min(4096, stream.Length)];
                int total = 0, read;
                while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0) total += read;
                return Encoding.UTF8.GetString(buffer, 0, total);
            }
        }

        /// <summary>
        /// 우리 폴더의 표지 파일을 통째로 다시 쓴다(<c>FileMode.Create</c> = 우리 파일 덮어쓰기).
        /// <paramref name="flushToDisk"/>가 참이면 전원 차단 대비로 OS 캐시까지 민다.
        /// </summary>
        private static void WriteMarker(string markerPath, string line, bool flushToDisk)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
            using (var stream = new FileStream(markerPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                if (flushToDisk) stream.Flush(true);
            }
        }

        private static string TryCopyTail(string sourcePath, string directory, long maxCopyBytes, PreviousSessionVerdict verdict,
            DateTime utcNow, out long sourceBytes, out long copiedBytes, out bool truncated, out string note)
        {
            sourceBytes = 0;
            copiedBytes = 0;
            truncated = false;
            note = null;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                note = "원본 " + SessionExitMarkerPolicy.PreviousPlayerLogFileName + " 없음(-logFile 지정 실행이거나 로그가 꺼져 있다)";
                return null;
            }

            string fullSource = Path.GetFullPath(sourcePath);
            string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (fullSource.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
            {
                note = "원본이 우리 폴더 안에 있다 — 복사하지 않는다";
                return null;
            }

            int slot = ChooseCopySlot(directory);
            if (slot < 0)
            {
                note = "복사본 슬롯을 고르지 못했다";
                return null;
            }
            string fileName = SessionExitMarkerPolicy.CopySlotFileName(slot);
            string destination = Path.Combine(directory, fileName);

            // 원본은 읽기 전용으로 연다. Unity·사용자·다른 프로그램의 읽기·쓰기·이름 바꾸기를 막지 않도록 공유를 전부 연다.
            using (var source = new FileStream(fullSource, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                SourceOpenedForTesting?.Invoke(fullSource);
                sourceBytes = source.Length;
                long start = SessionExitMarkerPolicy.CopyStartOffset(sourceBytes, maxCopyBytes);
                truncated = start > 0;

                using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    string header = string.Format(CultureInfo.InvariantCulture,
                        "※ StickMate가 비정상 종료 다음 실행에서 복사한 직전 실행 로그 — 판정={0}, 원본={1}, 원본 {2}바이트, " +
                        "{3}, 복사 UTC {4:o}. 원본은 그대로 둡니다.\n",
                        verdict, SessionExitMarkerPolicy.PreviousPlayerLogFileName, sourceBytes,
                        truncated
                            ? string.Format(CultureInfo.InvariantCulture, "앞 {0}바이트 생략(끝 {1}바이트만)", start, sourceBytes - start)
                            : "전체",
                        utcNow.ToUniversalTime());
                    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                    target.Write(headerBytes, 0, headerBytes.Length);

                    source.Seek(start, SeekOrigin.Begin);
                    var buffer = new byte[81920];
                    long remaining = sourceBytes - start;
                    while (remaining > 0)
                    {
                        int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read <= 0) break;
                        target.Write(buffer, 0, read);
                        remaining -= read;
                        copiedBytes += read;
                    }
                    target.Flush(true);
                }
            }
            return fileName;
        }

        private static int ChooseCopySlot(string directory)
        {
            int n = SessionExitMarkerPolicy.CopySlotCount;
            var exists = new bool[n];
            var lastWrite = new DateTime[n];
            for (int i = 0; i < n; i++)
            {
                string path = Path.Combine(directory, SessionExitMarkerPolicy.CopySlotFileName(i));
                exists[i] = File.Exists(path);
                lastWrite[i] = exists[i] ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            }
            return FreezeForensicsPolicy.ChooseSlot(exists, lastWrite, null);
        }
    }
}
