using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 — 동결 원장의 <b>파일 쓰기</b>. 어느 스레드에서 불러도 된다.
    ///
    /// ============================================================================
    /// 채널 둘, 잠금 둘 — 메인 스레드는 워치독의 디스크 쓰기를 기다리지 않는다 (verify-change (5))
    /// ============================================================================
    /// 1차 구현은 잠금 하나를 두 스레드가 나눠 썼고 <c>IsActive</c> 게터까지 그 잠금을 잡았다. 워치독은
    /// 토폴로지 사건(t0) 뒤 60초 동안 1초마다 그 잠금 안에서 <c>Flush(true)</c>를 한다 — 즉 <b>바로 그 사건
    /// 구간에서</b> 메인 스레드가 SetResolution 직전 한 줄을 쓰려다 워치독의 디스크 대기를 기다릴 수 있었다.
    /// 계측이 현상의 타이밍을 바꾸면 증거가 오염된다. 그래서:
    /// <list type="bullet">
    ///   <item><see cref="IsActive"/>는 <b>잠금 없는</b> volatile 읽기다.</item>
    ///   <item><see cref="FreezeForensicsChannel.Main"/>(메인 스레드 사건)과
    ///     <see cref="FreezeForensicsChannel.Watchdog"/>(워치독 줄)은 <b>파일도 잠금도 따로</b>다. 한 채널의
    ///     flush가 다른 채널을 막지 않는다. 두 파일 모두 UTC 시각이 붙어 있어 나란히 놓으면 합쳐 읽힌다.</item>
    ///   <item>채널 사이의 공유 상태는 "사건 창이 언제 닫히는가" 하나뿐이고 원자적 교환으로만 다룬다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 메인 스레드의 동기 flush는 <b>유지한다</b> — 트레이드오프
    /// ============================================================================
    /// 이 원장의 목적은 "하드 프리즈 직전 줄이 디스크에 남는다"이다. 비동기 큐로 넘기면 메인은 기다리지
    /// 않지만 <b>SetResolution을 부르는 순간 그 줄이 아직 디스크에 없을 수 있어</b> 목적을 잃는다. 대신
    /// (1) 사건 창 안의 줄만 동기로 쓰고(사건 한 번에 약 5~8줄), (2) <b>각 줄의 쓰기+flush 소요 시간을
    /// 다음 줄에 <c>직전기록=</c>으로 적는다</b> — 계측이 현상을 얼마나 건드렸는지가 원장 안에 숫자로 남는다.
    /// 남는 한계: 서로 다른 파일이라도 같은 볼륨의 저널 커밋이 직렬화될 수 있다(미측정).
    ///
    /// ============================================================================
    /// 사건 밖의 적합 기록은 디스크에 쓰지 않는다 (verify-change (4))
    /// ============================================================================
    /// 기동 첫 적합(SetResolution·창 크기·위치)이 매 실행 파일을 만들어 12슬롯 이력을 약 12회 실행 만에
    /// 밀어냈다. 이제 규칙(<see cref="FreezeForensicsPolicy.ClassifyRetention"/>)이 사건 창 밖의 적합 기록을
    /// <b>메모리 링(<see cref="FreezeForensicsPolicy.ContextBufferLines"/>줄)에만</b> 두고, 사건이 열리면 그때
    /// "※맥락" 표시와 함께 먼저 내려 쓴다. 평소 실행은 파일을 하나도 만들지 않는다.
    ///
    /// <para><b>파일을 지우지 않는다.</b> 슬롯 파일을 링으로 돌며 가장 오래된 슬롯을 <b>덮어쓴다</b>
    /// (<c>FileMode.Create</c>) — 프로덕션 코드 삭제 0건 불변식(원칙 3), <see cref="ReservedBarRestoreLedger"/>와
    /// 같은 관례. <b>어떤 실패도 예외로 새어 나가지 않는다</b> — IO가 실패한 채널은 이번 세션 동안 조용히 끈다.</para>
    /// </summary>
    public static class FreezeForensicsLog
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly object LifeGate = new object();

        private static volatile bool s_active;
        private static volatile int s_pid;
        private static volatile string s_directory;
        private static volatile string s_sessionHeader;
        private static long s_incidentUntilTicks = long.MinValue;

        private static volatile int s_maxLines = FreezeForensicsPolicy.MaxLinesPerFile;
        private static long s_maxBytes = FreezeForensicsPolicy.MaxBytesPerFile;
        private static volatile int s_slotCount = FreezeForensicsPolicy.SlotCount;
        private static volatile int s_maxSlotsPerSession = FreezeForensicsPolicy.MaxSlotsPerSession;

        private static readonly Channel MainChannel = new Channel(FreezeForensicsChannel.Main);
        private static readonly Channel WatchdogChannel = new Channel(FreezeForensicsChannel.Watchdog);

        /// <summary>원장이 켜져 있는가. <b>잠금 없이</b> 읽는다 — 메인 스레드가 이 한 줄 때문에 기다리는 일이 없다.</summary>
        public static bool IsActive => s_active;

        /// <summary>원장 폴더(활성 전이면 null). 사용자에게 알려줄 경로다.</summary>
        public static string DirectoryPath => s_directory;

        /// <summary>그 채널이 지금 쓰는 파일(아직 기록이 없으면 null).</summary>
        public static string CurrentFilePath(FreezeForensicsChannel channel) => Get(channel).CurrentPath;

        /// <summary>그 채널이 IO 실패로 꺼졌는가.</summary>
        public static bool IsChannelBroken(FreezeForensicsChannel channel) => Get(channel).Broken;

        /// <summary>사건 창이 지금 열려 있는가(단조 시계 기준, 잠금 없음).</summary>
        public static bool IsIncidentWindowOpen => Stopwatch.GetTimestamp() < Interlocked.Read(ref s_incidentUntilTicks);

        /// <summary>
        /// 활성화. <b>메인 스레드에서</b> 폴더 경로를 받아 둔다(<c>Application.persistentDataPath</c>는 Unity API다).
        /// 파일은 만들지 않는다.
        /// </summary>
        public static void Activate(string directory, string sessionHeaderDetail)
        {
            lock (LifeGate)
            {
                s_active = false;
                MainChannel.Reset();
                WatchdogChannel.Reset();
                s_directory = string.IsNullOrEmpty(directory) ? null : directory;
                s_sessionHeader = sessionHeaderDetail ?? string.Empty;
                Interlocked.Exchange(ref s_incidentUntilTicks, long.MinValue);
                if (s_pid == 0)
                {
                    try { using (var self = Process.GetCurrentProcess()) s_pid = self.Id; }
                    catch (Exception) { /* pid는 사람이 읽는 보조 정보 — 없으면 "?"로 찍힌다. */ }
                }
                s_active = s_directory != null;
            }
        }

        /// <summary>사건 창을 지금부터 <see cref="FreezeForensicsPolicy.IncidentWindowSeconds"/> 뒤까지 연다(이미 더 길면 유지).</summary>
        public static void OpenIncidentWindow()
        {
            long until = Stopwatch.GetTimestamp() + (long)(FreezeForensicsPolicy.IncidentWindowSeconds * Stopwatch.Frequency);
            while (true)
            {
                long current = Interlocked.Read(ref s_incidentUntilTicks);
                if (current >= until) return;
                if (Interlocked.CompareExchange(ref s_incidentUntilTicks, until, current) == current) return;
            }
        }

        /// <summary>한 줄을 규칙대로 처리한다(즉시 디스크 또는 맥락 링). 비활성이면 아무것도 하지 않는다.</summary>
        public static void Write(FreezeForensicsChannel channel, in FreezeForensicsRecord record)
        {
            if (!s_active) return;
            if (FreezeForensicsPolicy.OpensIncident(record.Kind)) OpenIncidentWindow();
            ForensicsRetention retention = FreezeForensicsPolicy.ClassifyRetention(record.Kind, IsIncidentWindowOpen);
            Get(channel).Write(record, retention);
        }

        private static Channel Get(FreezeForensicsChannel channel)
            => channel == FreezeForensicsChannel.Watchdog ? WatchdogChannel : MainChannel;

        // ------------------------------------------------------------------------------------

        private sealed class Channel
        {
            internal readonly object Gate = new object();
            private readonly FreezeForensicsChannel _kind;
            private readonly Queue<string> _context = new Queue<string>(FreezeForensicsPolicy.ContextBufferLines);
            private readonly List<int> _sessionSlots = new List<int>(FreezeForensicsPolicy.MaxSlotsPerSession);
            private volatile string _currentPath;
            private volatile bool _broken;
            private int _currentSlot = -1;
            private int _lines;
            private long _bytes;
            private int _contextDropped;
            private double _lastWriteMs = -1.0;

            internal Channel(FreezeForensicsChannel kind) { _kind = kind; }

            internal string CurrentPath => _currentPath;
            internal bool Broken => _broken;

            internal void Reset()
            {
                lock (Gate)
                {
                    _context.Clear();
                    _sessionSlots.Clear();
                    _currentPath = null;
                    _broken = false;
                    _currentSlot = -1;
                    _lines = 0;
                    _bytes = 0;
                    _contextDropped = 0;
                    _lastWriteMs = -1.0;
                }
            }

            internal void Write(in FreezeForensicsRecord record, ForensicsRetention retention)
            {
                // ★ R-7 — 줄마다 pid(단일 인스턴스 잠금이 없어 두 인스턴스가 같은 폴더에 쓸 수 있다).
                string line = FreezeForensicsPolicy.FormatLine(record, s_pid);
                lock (Gate)
                {
                    if (_broken || s_directory == null) return;

                    if (retention == ForensicsRetention.ContextOnly)
                    {
                        if (_context.Count >= FreezeForensicsPolicy.ContextBufferLines)
                        {
                            _context.Dequeue();
                            _contextDropped++;
                        }
                        _context.Enqueue(line);
                        return;
                    }

                    try
                    {
                        long start = Stopwatch.GetTimestamp();
                        var pending = new List<string>(_context.Count + 2);
                        if (_context.Count > 0)
                        {
                            if (_contextDropped > 0)
                            {
                                pending.Add(FreezeForensicsPolicy.FormatLine(new FreezeForensicsRecord(
                                    FreezeForensicsEvent.SessionHeader, record.UtcTime, -1.0, -1, -1,
                                    "※맥락 링이 넘쳐 앞선 " + _contextDropped + "줄은 버려졌다"), s_pid));
                                _contextDropped = 0;
                            }
                            while (_context.Count > 0)
                            {
                                pending.Add(_context.Dequeue() + " | " + FreezeForensicsPolicy.ContextMarker);
                            }
                        }
                        pending.Add(_lastWriteMs >= 0.0
                            ? line + " | " + FreezeForensicsPolicy.PreviousWriteCostLabel +
                              _lastWriteMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "ms"
                            : line);

                        foreach (string l in pending) AppendLocked(l, record.UtcTime);
                        _lastWriteMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    }
                    catch (Exception)
                    {
                        _broken = true;
                    }
                }
            }

            private void AppendLocked(string line, DateTime utcNow)
            {
                byte[] bytes = Utf8NoBom.GetBytes(line + "\r\n");
                if (_currentPath == null
                    || !FreezeForensicsPolicy.FitsBudget(_lines, _bytes, bytes.Length, s_maxLines, Interlocked.Read(ref s_maxBytes)))
                {
                    OpenNextFileLocked(utcNow);
                }
                using (var fs = new FileStream(_currentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    // ★ Flush(true) — OS 캐시까지 밀어낸다. 이 파일의 존재 이유가 "전원이 나가도 남는 것"이다.
                    fs.Flush(true);
                }
                _lines++;
                _bytes += bytes.Length;
            }

            private void OpenNextFileLocked(DateTime utcNow)
            {
                string directory = s_directory;
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                int slotCount = s_slotCount;
                int slot;
                bool continued = _currentPath != null;
                if (_sessionSlots.Count < s_maxSlotsPerSession)
                {
                    var exists = new bool[slotCount];
                    var written = new DateTime[slotCount];
                    var excluded = new bool[slotCount];
                    for (int i = 0; i < slotCount; i++)
                    {
                        string p = Path.Combine(directory, FreezeForensicsPolicy.SlotFileName(_kind, i));
                        exists[i] = File.Exists(p);
                        written[i] = exists[i] ? File.GetLastWriteTimeUtc(p) : DateTime.MinValue;
                        excluded[i] = _sessionSlots.Contains(i);
                    }
                    // R-7 — 가장 오래된 슬롯을 고르므로 다른 인스턴스가 방금 쓴 슬롯은 고르지 않는다(규칙 문서).
                    slot = FreezeForensicsPolicy.ChooseSlot(exists, written, excluded);
                    if (slot < 0) slot = _sessionSlots.Count > 0 ? _sessionSlots[0] : 0;
                    if (!_sessionSlots.Contains(slot)) _sessionSlots.Add(slot);
                }
                else
                {
                    // 이 세션의 슬롯을 다 썼다 — 자기 슬롯 안에서만 돈다(이전 세션 기록을 보호한다).
                    int at = _sessionSlots.IndexOf(_currentSlot);
                    slot = _sessionSlots[(at + 1) % _sessionSlots.Count];
                }

                string path = Path.Combine(directory, FreezeForensicsPolicy.SlotFileName(_kind, slot));
                string header = s_sessionHeader + " / 채널=" + _kind + (continued
                    ? " / 같은 세션의 앞 파일 " + Path.GetFileName(_currentPath) + "에서 이어짐"
                    : string.Empty);
                var headerRecord = new FreezeForensicsRecord(FreezeForensicsEvent.SessionHeader, utcNow, -1.0, -1, -1, header);
                byte[] headerBytes = Utf8NoBom.GetBytes(FreezeForensicsPolicy.FormatLine(headerRecord, s_pid) + "\r\n");

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.Write(headerBytes, 0, headerBytes.Length);
                    fs.Flush(true);
                }

                _currentPath = path;
                _currentSlot = slot;
                _lines = 1;
                _bytes = headerBytes.Length;
            }
        }

        // ------------------------------------------------------------------------------------
        // 테스트 전용
        // ------------------------------------------------------------------------------------

        /// <summary>테스트 전용 — 상한을 작게 잡아 링/회전을 빨리 재현한다.</summary>
        internal static void ConfigureLimitsForTesting(int maxLines, long maxBytes, int slotCount, int maxSlotsPerSession)
        {
            s_maxLines = maxLines;
            Interlocked.Exchange(ref s_maxBytes, maxBytes);
            s_slotCount = slotCount;
            s_maxSlotsPerSession = maxSlotsPerSession;
        }

        /// <summary>테스트 전용 — 채널 잠금 객체. 한 채널을 붙잡고 다른 채널이 막히지 않는지 재는 데 쓴다.</summary>
        internal static object ChannelGateForTesting(FreezeForensicsChannel channel) => Get(channel).Gate;

        /// <summary>테스트 전용 — 비활성으로 되돌리고 상한을 기본값으로. 파일은 지우지 않는다.</summary>
        internal static void ResetForTesting()
        {
            lock (LifeGate)
            {
                s_active = false;
                s_directory = null;
                s_sessionHeader = null;
                Interlocked.Exchange(ref s_incidentUntilTicks, long.MinValue);
                MainChannel.Reset();
                WatchdogChannel.Reset();
                ConfigureLimitsForTesting(FreezeForensicsPolicy.MaxLinesPerFile, FreezeForensicsPolicy.MaxBytesPerFile,
                    FreezeForensicsPolicy.SlotCount, FreezeForensicsPolicy.MaxSlotsPerSession);
            }
        }
    }

    /// <summary>
    /// ★ 2026-09-14 — 플랫폼 Enforcer가 부르는 <b>메인 스레드 전용</b> 창구(<see cref="FreezeForensicsChannel.Main"/>).
    /// Unity 시계(<c>realtimeSinceStartup</c>/<c>frameCount</c>)를 여기서 붙인다. 판정은
    /// <see cref="FreezeForensicsPolicy"/>에 있고, 호출 지점(두 플랫폼 Enforcer)은 배선만 한다.
    /// </summary>
    public static class FreezeForensics
    {
        /// <summary>원장이 켜져 있는가(잠금 없음). 호출 지점은 이 값이 참일 때만 상세 문자열을 만든다.</summary>
        public static bool IsActive => FreezeForensicsLog.IsActive;

        /// <summary>사각형 없는 한 줄(메인 스레드).</summary>
        public static void Record(FreezeForensicsEvent kind, int monitorCount, string detail)
        {
            if (!FreezeForensicsLog.IsActive) return;
            FreezeForensicsLog.Write(FreezeForensicsChannel.Main, new FreezeForensicsRecord(kind, DateTime.UtcNow,
                Time.realtimeSinceStartupAsDouble, Time.frameCount, monitorCount, detail));
        }

        /// <summary>사각형이 있는 한 줄(메인 스레드). 스왑체인/창 표면을 건드리는 호출 <b>직전</b>에 부른다.</summary>
        public static void Record(FreezeForensicsEvent kind, float x, float y, float width, float height,
            int monitorCount, string detail)
        {
            if (!FreezeForensicsLog.IsActive) return;
            FreezeForensicsLog.Write(FreezeForensicsChannel.Main, new FreezeForensicsRecord(kind, DateTime.UtcNow,
                Time.realtimeSinceStartupAsDouble, Time.frameCount, monitorCount, x, y, width, height, detail));
        }

        /// <summary>
        /// 토폴로지 감시기 관측 한 번을 원장에 반영한다. 사건이 아니면 <b>할당 없이</b> 곧바로 돌아온다
        /// (표본 주기마다 불린다). t0(변화 감지)이면 워치독의 하트비트 창을 연다.
        /// </summary>
        public static void ObserveTopologyTransition(bool wasSettling, bool isSettling, bool fired,
            in DisplayTopologySignature sample, string platformTag)
        {
            TopologyForensicsTransition t = FreezeForensicsPolicy.ClassifyTopologyTransition(wasSettling, isSettling, fired);
            if (t == TopologyForensicsTransition.None) return;
            if (!FreezeForensicsLog.IsActive) return;

            FreezeForensicsEvent kind;
            switch (t)
            {
                case TopologyForensicsTransition.ChangeDetected:
                    // ★ 줄보다 먼저 연다 — 쓰기가 늦어져도 워치독의 시계는 이미 t0에 있다.
                    FreezeWatchdog.MarkEpisodeStart();
                    kind = FreezeForensicsEvent.TopologyChangeDetected;
                    break;
                case TopologyForensicsTransition.Settled:
                    kind = FreezeForensicsEvent.TopologySettled;
                    break;
                default:
                    kind = FreezeForensicsEvent.TopologyReverted;
                    break;
            }

            string detail = platformTag + " " + sample.ToString();
            if (sample.Valid)
            {
                Record(kind, sample.MonitorX, sample.MonitorY, sample.MonitorWidth, sample.MonitorHeight,
                    sample.MonitorCount, detail);
            }
            else
            {
                Record(kind, -1, detail);
            }
        }

        /// <summary>투명 재적용 직전. 창 사각형을 바꾸는 경우만 적는다(<see cref="FreezeForensicsPolicy.ShouldRecordTransparencyReassign"/>).</summary>
        public static void RecordTransparencyReassign(bool causesWindowResize, int monitorCount, string detail)
        {
            if (!FreezeForensicsPolicy.ShouldRecordTransparencyReassign(causesWindowResize)) return;
            Record(FreezeForensicsEvent.TransparencyReassign, monitorCount, detail);
        }
    }
}
