using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 — 동결 원장의 <b>파일 쓰기</b>. 어느 스레드에서 불러도 된다(내부 잠금).
    ///
    /// <para><b>한 줄 = 열기 → 덧붙이기 → <c>Flush(true)</c> → 닫기.</b> 핸들을 세션 내내 쥐고 있지 않는다:
    /// (1) 사용자가 앱을 켠 채로 파일을 복사해 보낼 수 있어야 하고, (2) 전원이 나가도 이미 쓴 줄은 디스크에
    /// 있어야 한다. 줄 수가 드물어서(사건 때만) 비용은 무시할 수 있다.</para>
    ///
    /// <para><b>파일을 지우지 않는다.</b> 슬롯 파일 <see cref="FreezeForensicsPolicy.SlotCount"/>개를 링으로 돌며
    /// 가장 오래된 슬롯을 <b>덮어쓴다</b>(<c>FileMode.Create</c>). 프로덕션 코드의 삭제 0건 불변식(원칙 3)을
    /// 그대로 지킨다 — <see cref="ReservedBarRestoreLedger"/>와 같은 관례다.</para>
    ///
    /// <para><b>파일은 첫 사건 때 만든다.</b> 활성화만으로는 아무것도 쓰지 않는다.</para>
    ///
    /// <para><b>어떤 실패도 예외로 새어 나가지 않는다.</b> 한 번 IO가 실패하면 이 세션에서는 조용히 끈다 —
    /// 진단 장치가 앱을 죽이면 사고다.</para>
    /// </summary>
    public static class FreezeForensicsLog
    {
        private static readonly object Gate = new object();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static string s_directory;
        private static string s_sessionHeader;
        private static bool s_broken;

        private static string s_currentPath;
        private static int s_currentSlot = -1;
        private static int s_lines;
        private static long s_bytes;
        private static readonly List<int> s_sessionSlots = new List<int>(FreezeForensicsPolicy.MaxSlotsPerSession);

        private static int s_maxLines = FreezeForensicsPolicy.MaxLinesPerFile;
        private static long s_maxBytes = FreezeForensicsPolicy.MaxBytesPerFile;
        private static int s_slotCount = FreezeForensicsPolicy.SlotCount;
        private static int s_maxSlotsPerSession = FreezeForensicsPolicy.MaxSlotsPerSession;

        /// <summary>활성이고 아직 IO가 망가지지 않았는가.</summary>
        public static bool IsActive
        {
            get { lock (Gate) return s_directory != null && !s_broken; }
        }

        /// <summary>원장 폴더(활성 전이면 null). 사용자에게 알려줄 경로다.</summary>
        public static string DirectoryPath
        {
            get { lock (Gate) return s_directory; }
        }

        /// <summary>지금 쓰는 파일(아직 사건이 없었으면 null).</summary>
        public static string CurrentFilePath
        {
            get { lock (Gate) return s_currentPath; }
        }

        /// <summary>
        /// 활성화. <b>메인 스레드에서</b> 폴더 경로를 받아 둔다(<c>Application.persistentDataPath</c>는 Unity API다).
        /// 파일은 만들지 않는다.
        /// </summary>
        public static void Activate(string directory, string sessionHeaderDetail)
        {
            lock (Gate)
            {
                s_directory = string.IsNullOrEmpty(directory) ? null : directory;
                s_sessionHeader = sessionHeaderDetail ?? string.Empty;
                s_broken = false;
                s_currentPath = null;
                s_currentSlot = -1;
                s_lines = 0;
                s_bytes = 0;
                s_sessionSlots.Clear();
            }
        }

        /// <summary>한 줄을 쓰고 디스크까지 밀어낸다. 비활성이면 아무것도 하지 않는다.</summary>
        public static void Write(in FreezeForensicsRecord record)
        {
            string line = FreezeForensicsPolicy.FormatLine(record) + "\r\n";
            byte[] bytes = Utf8NoBom.GetBytes(line);

            lock (Gate)
            {
                if (s_directory == null || s_broken) return;
                try
                {
                    if (s_currentPath == null
                        || !FreezeForensicsPolicy.FitsBudget(s_lines, s_bytes, bytes.Length, s_maxLines, s_maxBytes))
                    {
                        OpenNextFileLocked(record.UtcTime);
                    }
                    AppendAndFlush(s_currentPath, bytes);
                    s_lines++;
                    s_bytes += bytes.Length;
                }
                catch (Exception)
                {
                    s_broken = true;
                }
            }
        }

        private static void OpenNextFileLocked(DateTime utcNow)
        {
            if (!Directory.Exists(s_directory)) Directory.CreateDirectory(s_directory);

            int slot;
            bool continued = s_currentPath != null;
            if (s_sessionSlots.Count < s_maxSlotsPerSession)
            {
                var exists = new bool[s_slotCount];
                var written = new DateTime[s_slotCount];
                var excluded = new bool[s_slotCount];
                for (int i = 0; i < s_slotCount; i++)
                {
                    string p = Path.Combine(s_directory, FreezeForensicsPolicy.SlotFileName(i));
                    exists[i] = File.Exists(p);
                    written[i] = exists[i] ? File.GetLastWriteTimeUtc(p) : DateTime.MinValue;
                    excluded[i] = s_sessionSlots.Contains(i);
                }
                slot = FreezeForensicsPolicy.ChooseSlot(exists, written, excluded);
                if (slot < 0) slot = s_sessionSlots.Count > 0 ? s_sessionSlots[0] : 0;
                if (!s_sessionSlots.Contains(slot)) s_sessionSlots.Add(slot);
            }
            else
            {
                // 이 세션의 슬롯을 다 썼다 — 자기 슬롯 안에서만 돈다(이전 세션 기록을 보호한다).
                int at = s_sessionSlots.IndexOf(s_currentSlot);
                slot = s_sessionSlots[(at + 1) % s_sessionSlots.Count];
            }

            string path = Path.Combine(s_directory, FreezeForensicsPolicy.SlotFileName(slot));
            string header = s_sessionHeader + (continued
                ? " / 같은 세션의 앞 파일 " + Path.GetFileName(s_currentPath) + "에서 이어짐"
                : string.Empty);
            var headerRecord = new FreezeForensicsRecord(FreezeForensicsEvent.SessionHeader, utcNow, -1.0, -1, -1, header);
            byte[] headerBytes = Utf8NoBom.GetBytes(FreezeForensicsPolicy.FormatLine(headerRecord) + "\r\n");

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Write(headerBytes, 0, headerBytes.Length);
                fs.Flush(true);
            }

            s_currentPath = path;
            s_currentSlot = slot;
            s_lines = 1;
            s_bytes = headerBytes.Length;
        }

        private static void AppendAndFlush(string path, byte[] bytes)
        {
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Write(bytes, 0, bytes.Length);
                // ★ Flush(true) — OS 캐시까지 밀어낸다. 이 파일의 존재 이유가 "전원이 나가도 남는 것"이다.
                fs.Flush(true);
            }
        }

        /// <summary>테스트 전용 — 상한을 작게 잡아 링/회전을 빨리 재현한다.</summary>
        internal static void ConfigureLimitsForTesting(int maxLines, long maxBytes, int slotCount, int maxSlotsPerSession)
        {
            lock (Gate)
            {
                s_maxLines = maxLines;
                s_maxBytes = maxBytes;
                s_slotCount = slotCount;
                s_maxSlotsPerSession = maxSlotsPerSession;
            }
        }

        /// <summary>테스트 전용 — 비활성으로 되돌리고 상한을 기본값으로. 파일은 지우지 않는다.</summary>
        internal static void ResetForTesting()
        {
            lock (Gate)
            {
                s_directory = null;
                s_sessionHeader = null;
                s_broken = false;
                s_currentPath = null;
                s_currentSlot = -1;
                s_lines = 0;
                s_bytes = 0;
                s_sessionSlots.Clear();
                s_maxLines = FreezeForensicsPolicy.MaxLinesPerFile;
                s_maxBytes = FreezeForensicsPolicy.MaxBytesPerFile;
                s_slotCount = FreezeForensicsPolicy.SlotCount;
                s_maxSlotsPerSession = FreezeForensicsPolicy.MaxSlotsPerSession;
            }
        }
    }

    /// <summary>
    /// ★ 2026-09-14 — 플랫폼 Enforcer가 부르는 <b>메인 스레드 전용</b> 창구.
    /// Unity 시계(<c>realtimeSinceStartup</c>/<c>frameCount</c>)를 여기서 붙인다. 판정은
    /// <see cref="FreezeForensicsPolicy"/>에 있고, 호출 지점(두 플랫폼 Enforcer)은 배선만 한다.
    /// </summary>
    public static class FreezeForensics
    {
        /// <summary>원장이 켜져 있는가. 호출 지점은 이 값이 참일 때만 상세 문자열을 만든다(할당 절약).</summary>
        public static bool IsActive => FreezeForensicsLog.IsActive;

        /// <summary>사각형 없는 한 줄(메인 스레드).</summary>
        public static void Record(FreezeForensicsEvent kind, int monitorCount, string detail)
        {
            if (!FreezeForensicsLog.IsActive) return;
            FreezeForensicsLog.Write(new FreezeForensicsRecord(kind, DateTime.UtcNow,
                Time.realtimeSinceStartupAsDouble, Time.frameCount, monitorCount, detail));
        }

        /// <summary>사각형이 있는 한 줄(메인 스레드). 스왑체인/창 표면을 건드리는 호출 <b>직전</b>에 부른다.</summary>
        public static void Record(FreezeForensicsEvent kind, float x, float y, float width, float height,
            int monitorCount, string detail)
        {
            if (!FreezeForensicsLog.IsActive) return;
            FreezeForensicsLog.Write(new FreezeForensicsRecord(kind, DateTime.UtcNow,
                Time.realtimeSinceStartupAsDouble, Time.frameCount, monitorCount, x, y, width, height, detail));
        }

        /// <summary>
        /// 토폴로지 감시기 관측 한 번을 원장에 반영한다. 사건이 아니면 <b>할당 없이</b> 곧바로 돌아온다
        /// (0.25초마다 불린다). t0(변화 감지)이면 워치독의 하트비트 창을 연다.
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
