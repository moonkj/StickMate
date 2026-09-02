using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>디스크 흔적을 읽은 결과. "없다"와 "못 읽었다"와 "내 것이 아니다"를 <b>절대 뭉치지
    /// 않는다</b> — 뭉치는 순간 복구 실패가 정상 동작처럼 보인다.</summary>
    public enum ReservedBarLedgerState
    {
        /// <summary>흔적 파일이 없다. 정상적인 첫 실행/정상 종료 이후의 모습이다.</summary>
        None = 0,

        /// <summary>파일은 있지만 닫혀 있다(<c>active=false</c>). 갚을 빚이 없다.</summary>
        Closed,

        /// <summary>★ 열린 흔적 — 지난 실행이 원복하지 못하고 죽었다. 이번 실행이 갚아야 한다.</summary>
        Open,

        /// <summary>파일이 깨졌거나 파싱할 수 없다. 없는 것으로 취급하되 <b>경고를 남긴다</b>.</summary>
        Unreadable,

        /// <summary>이 빌드보다 새로운 스키마다. 해석하지 않고 <b>그대로 둔다</b> — 신버전이 갚는다.</summary>
        NewerSchema,

        /// <summary>다른 OS에서 만든 흔적이다(폴더 동기화 등). 우리가 갚을 빚이 아니므로 손대지 않는다.</summary>
        ForeignPlatform,
    }

    /// <summary>흔적 파일의 내용. <c>JsonUtility</c>가 읽을 수 있도록 public 필드만 쓴다.</summary>
    [Serializable]
    public sealed class ReservedBarRestoreTrace
    {
        public int version;

        /// <summary>false면 "이미 갚았다"는 표시. <b>파일을 지우는 대신 이 값을 내린다</b>
        /// (아래 클래스 문서 "왜 파일을 지우지 않는가").</summary>
        public bool active;

        /// <summary>우리가 바꾸기 <b>전</b>의 사용자 설정. 복구의 목표값이다.</summary>
        public bool originalAutoHide;

        /// <summary>흔적을 만든 플랫폼("Windows" 등).</summary>
        public string platform;

        /// <summary>사람이 로그와 대조할 수 있게 남기는 UTC 시각(ISO 8601).</summary>
        public string writtenAtUtc;

        /// <summary>흔적을 만든 프로세스 id. 같은 파일을 두 인스턴스가 물었을 때 사람이 구분한다.</summary>
        public int pid;
    }

    /// <summary>
    /// ★★ 2026-09-02 — <b>크래시에서 살아남는 원복 장치</b>. 이 기능의 안전장치 그 자체다.
    ///
    /// <see cref="ReservedBarRevealPolicy"/> 클래스 문서의 "왜 write-ahead인가"가 이 파일이 존재하는
    /// 이유 전부다: <c>Application.quitting</c>은 SIGTERM/크래시/강제 종료에서 <b>돌지 않고</b>,
    /// 그러면 사용자의 작업표시줄이 <b>영구히</b> 바뀐 채 남는다. 그래서 시스템을 바꾸기 <b>전에</b>
    /// 원래 값을 디스크에 적고, 다음 실행이 그 흔적을 먼저 갚는다.
    ///
    /// ============================================================================
    /// 어디에 쓰는가 — 우리 자신의 샌드박스뿐이다
    /// ============================================================================
    /// <c>Application.persistentDataPath</c> 아래 <b>고정 파일명 하나</b>. OS가 이 앱에 배정한 자리이고
    /// 사용자 자산이 아니다(<c>Core/CharacterSaveStore</c>와 정확히 같은 관례). 경로를 조립하거나
    /// 사용자가 고른 폴더에 쓰는 일은 없다.
    ///
    /// <para><b>PlayerPrefs를 쓰지 않는 이유가 둘 있다.</b>
    /// (1) Windows에서 PlayerPrefs는 <b>레지스트리</b>에 쓴다 — 이 저장소는 레지스트리 쓰기를
    ///     감사로 금지하고 있고(<c>UserAssetImmutabilityAuditTests</c>), 그 금지를 이 기능 때문에
    ///     흐리게 만들 이유가 없다.
    /// (2) PlayerPrefs 쓰기는 버퍼링되고 <c>Save()</c> 시점이 보장되지 않는다 — <b>크래시에서
    ///     사라지는 저장소</b>는 크래시 대비 장치로 쓸 수 없다. 여기서는 <c>FileStream.Flush(true)</c>로
    ///     OS 캐시까지 밀어낸다.</para>
    ///
    /// ============================================================================
    /// ★ 왜 파일을 <b>지우지</b> 않는가 (일부러다)
    /// ============================================================================
    /// 이 저장소의 프로덕션 코드에는 <c>File.Delete(</c>가 <b>한 건도 없고</b>, 감사가 그 0건을
    /// 지키고 있다(원칙 3). 흔적을 닫자고 그 불변식을 깨는 것은 이득에 비해 대가가 너무 크다 —
    /// 삭제 능력이 한 번 열리면 다음 사람이 다른 곳에서 쓴다. 그래서 "닫음"은 <c>active=false</c>를
    /// <b>쓰는</b> 것이다. 파일 하나(200바이트 미만)가 우리 샌드박스에 남을 뿐이고, 그 파일은
    /// 다음 라운드의 진단 자료로도 쓸모가 있다(마지막 원복이 언제 있었는지).
    /// </summary>
    public static class ReservedBarRestoreLedger
    {
        /// <summary>흔적 스키마 버전. 이 값을 올리는 라운드는 구버전 파일을 읽었을 때의 동작을
        /// 반드시 테스트로 함께 잠근다(CLAUDE.md 협업 프로토콜).</summary>
        public const int CurrentVersion = 1;

        private const string FileName = "stickmate_reserved_bar_restore.json";

        // 테스트가 개발자의 실제 파일 대신 임시 폴더를 보게 하는 리디렉션(CharacterSaveStore와 같은 관례).
        // 프로덕션에서는 언제나 null이다.
        private static string s_testingDirectoryOverride;

        private static string Directory_ => s_testingDirectoryOverride ?? Application.persistentDataPath;

        /// <summary>흔적 파일의 절대 경로. 로그/진단/테스트에서만 쓴다.</summary>
        public static string FilePath => Path.Combine(Directory_, FileName);

        /// <summary>테스트 리디렉션이 걸려 있는가. 단언용.</summary>
        public static bool IsRedirectedForTesting => s_testingDirectoryOverride != null;

        /// <summary>테스트 전용 — 흔적 경로를 임시 폴더로 옮긴다(<see cref="Application.temporaryCachePath"/> 아래).</summary>
        public static string RedirectToTemporaryDirectoryForTesting(string label)
        {
            string dir = Path.Combine(Application.temporaryCachePath, "StickMateReservedBar",
                string.IsNullOrEmpty(label) ? "default" : label);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            s_testingDirectoryOverride = dir;
            return dir;
        }

        /// <summary>테스트 전용 — 흔적 경로를 <b>정확히 이 경로</b>로 지정한다.
        /// 존재하지 않거나 쓸 수 없는 경로를 일부러 넣어 "흔적을 못 남기는 상황"을 재현하는 데 쓴다
        /// (그 상황에서 시스템을 바꾸지 않는 것이 이 기능의 핵심 안전장치다).</summary>
        public static void RedirectToPathForTesting(string directory) => s_testingDirectoryOverride = directory;

        /// <summary>테스트 전용 — 리디렉션 해제. 임시 폴더는 지우지 않는다(삭제 능력 0건 유지).</summary>
        public static void ResetForTesting() => s_testingDirectoryOverride = null;

        /// <summary>
        /// 흔적을 읽는다. <b>어떤 실패도 예외로 새어 나가지 않는다</b> — 기동 경로에서 도는 코드가
        /// 던지면 앱이 통째로 안 뜬다.
        /// </summary>
        /// <param name="trace">읽어 낸 내용. <see cref="ReservedBarLedgerState.Open"/>일 때만 의미가 있다.</param>
        /// <param name="platformTag">지금 플랫폼 꼬리표. 다르면 <see cref="ReservedBarLedgerState.ForeignPlatform"/>.</param>
        public static ReservedBarLedgerState Read(string platformTag, out ReservedBarRestoreTrace trace)
        {
            trace = null;
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return ReservedBarLedgerState.None;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return ReservedBarLedgerState.Unreadable;

                var parsed = JsonUtility.FromJson<ReservedBarRestoreTrace>(json);
                if (parsed == null || parsed.version < 1) return ReservedBarLedgerState.Unreadable;

                trace = parsed;
                if (parsed.version > CurrentVersion) return ReservedBarLedgerState.NewerSchema;
                if (!parsed.active) return ReservedBarLedgerState.Closed;

                // 플랫폼 대조는 **열린 흔적에만** 의미가 있다. 닫힌 흔적은 누가 남겼든 갚을 것이 없다.
                if (!string.IsNullOrEmpty(parsed.platform)
                    && !string.Equals(parsed.platform, platformTag, StringComparison.Ordinal))
                {
                    return ReservedBarLedgerState.ForeignPlatform;
                }

                return ReservedBarLedgerState.Open;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[작업표시줄] 원복 흔적 파일을 읽지 못했습니다({FilePath}): {ex.Message}. " +
                    "없는 것으로 취급합니다 — 이번 실행은 시스템 설정을 바꾸지 않습니다.");
                return ReservedBarLedgerState.Unreadable;
            }
        }

        /// <summary>
        /// ★ write-ahead — 시스템을 바꾸기 <b>전에</b> 부른다. <b>false를 돌려주면 호출부는 시스템을
        /// 바꾸면 안 된다</b>(원복 보증이 없는 변경은 하지 않는다).
        /// </summary>
        public static bool Open(bool originalAutoHide, string platformTag)
            => TryWrite(active: true, originalAutoHide: originalAutoHide, platformTag: platformTag);

        /// <summary>흔적을 닫는다(= 갚았다). 파일은 지우지 않고 <c>active=false</c>로 덮어쓴다.</summary>
        public static bool Close(bool originalAutoHide, string platformTag)
            => TryWrite(active: false, originalAutoHide: originalAutoHide, platformTag: platformTag);

        private static bool TryWrite(bool active, bool originalAutoHide, string platformTag)
        {
            try
            {
                string dir = Directory_;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                int pid = 0;
                try
                {
                    using (var self = System.Diagnostics.Process.GetCurrentProcess()) pid = self.Id;
                }
                catch (Exception) { /* pid는 사람이 읽는 보조 정보다. 없어도 복구는 성립한다. */ }

                var trace = new ReservedBarRestoreTrace
                {
                    version = CurrentVersion,
                    active = active,
                    originalAutoHide = originalAutoHide,
                    platform = platformTag,
                    writtenAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    pid = pid,
                };

                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(trace, prettyPrint: true));

                // ★ Flush(true) — OS 캐시까지 밀어낸다. 이 파일의 존재 이유가 "전원이 나가도 남는 것"이라
                //   버퍼에만 있는 흔적은 흔적이 아니다. (원자적 교체까지는 하지 않는다: 이 파일이 반쯤
                //   써진 채 남으면 다음 실행이 Unreadable로 읽고 "시스템을 바꾸지 않는다"로 가므로,
                //   가장 나쁜 결과가 '이번 실행에서 기능이 꺼진다'이지 사용자 설정 파괴가 아니다.)
                using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[작업표시줄] 원복 흔적을 쓰지 못했습니다({FilePath}): {ex.Message}. " +
                    (active
                        ? "원복을 보증할 수 없으므로 이번 실행에서는 자동 숨김을 해제하지 않습니다."
                        : "다음 실행이 같은 흔적을 한 번 더 갚게 됩니다(같은 값을 다시 쓰는 것이라 무해합니다)."));
                return false;
            }
        }
    }
}
