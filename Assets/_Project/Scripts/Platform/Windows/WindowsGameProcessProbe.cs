#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Platform.Windows
{
    /// <summary>
    /// "전경 프로세스가 게임인가"라는 <b>사실만</b> 조회하는 Windows 전용 계층.
    /// 판정 규칙 자체는 여기 없다 — 플랫폼 중립 <see cref="WindowsGameExecutablePolicy"/>가 갖고 있고
    /// 이 클래스는 그 규칙에 입력값(전경 exe 경로 / 등록된 게임 exe 경로 목록)만 공급한다.
    /// macOS에서 <c>MacWindowService.QueryAppCategory</c>가 문자열 하나를 떠 와서
    /// <c>FullscreenGameCategory.IsGameCategory</c>에 넘기는 구조와 1:1이다.
    ///
    /// ============================================================================
    /// 왜 존재하는가 (2026-09-01, 사용자 신고 "전체화면 엑셀에서 캐릭터가 사라짐")
    /// ============================================================================
    /// CLAUDE.md 절대 불변 원칙 2는 "전체화면 <b>게임</b> 감지 시 자동 숨김"이다. 그런데 Windows는
    /// 2026-09-01까지 기하 판정("전경 창 == 모니터")만 했고, 그래서 전체화면 Excel/PowerPoint/브라우저
    /// 에서도 캐릭터가 사라졌다(macOS는 같은 버그를 8/31에 카테고리 필터로 고쳤는데, 정작 사용자가
    /// 신고한 Windows가 남아 있었다). 근거 선택과 기각한 후보들의 이유는 전부
    /// <see cref="WindowsGameExecutablePolicy"/> 문서에 적어 뒀다.
    ///
    /// ============================================================================
    /// 절대 불변 원칙 3(유저 자산 불변) — 이 파일이 지키는 방식
    /// ============================================================================
    /// · 레지스트리는 <c>KEY_READ</c>로만 연다. 쓰기 계열(RegSetValueEx / RegCreateKeyEx /
    ///   RegDeleteKey / RegDeleteValue)은 <b>선언조차 하지 않는다</b> — 선언이 없으면 실수로도 부를 수
    ///   없다. <c>WindowsGameProcessProbeTests</c>가 이 파일에 그 이름들이 없음을 기계로 잠근다.
    /// · 프로세스 핸들은 <c>PROCESS_QUERY_LIMITED_INFORMATION</c>만 요청한다. 메모리 읽기/쓰기,
    ///   스레드 조작, 종료 권한이 아예 없는 최소 권한이며 관리자 승격도 필요 없다.
    /// · 타 프로세스에 어떤 메시지도 보내지 않고, 어떤 창도 건드리지 않는다.
    ///
    /// ============================================================================
    /// 호출 빈도와 캐시
    /// ============================================================================
    /// 이 조회는 전체화면 폴링(기본 1.5초)에서 <b>기하 조건이 이미 성립한 뒤에만</b> 불린다. 그래도
    /// 전체화면 엑셀을 하루 종일 켜 두면 하루 5만 번이 되므로, pid별 판정을 짧게 캐시하고
    /// (레지스트리 열거는 그 캐시가 만료될 때만) 재조회한다. pid는 재사용되므로 만료를 짧게 둔다.
    ///
    /// ============================================================================
    /// ★ 실패는 캐시하지 않는다 (N-23 · FC-1, 2026-09-26)
    /// ============================================================================
    /// 옛 판은 조회 성공과 <b>실패를 가리지 않고</b> 30초 캐시했다. 그래서 이미 등급 2로 감지된 게임에서
    /// 캐시 만료 순간 조회가 한 번 실패하면 「게임 아님」이 30초 굳어 <b>게임 위에 캐릭터가 다시</b>
    /// 나왔다(약 30초 · 범위 30.0–31.5초 · 원칙 2 조건부 위반). 게다가 판정 캐시와 게임바 목록 캐시가
    /// 실패를 <b>따로</b> 30초 유지했다.
    ///
    /// 지금은 <see cref="GameVerdictRetryPolicy"/>(플랫폼 중립 순수 규칙)에 판정을 맡긴다:
    /// <list type="number">
    ///  <item><b>확정 판정만</b> 캐시한다 — 경로를 읽었고 목록 상태가 「유지」 계열일 때만.</item>
    ///  <item>실패하면 <b>시간 비교 없이 다음 폴링에</b> 한 번 더 묻는다(한 폴링짜리 실패는 전체화면
    ///        판정 디바운서 1.0초가 흡수하므로 등급이 바뀌지 않는다).</item>
    ///  <item>연속 실패 K회면 <b>연속 실패의 첫 시도부터</b> 30초 후퇴하고, 창이 끝나면 계수를 0으로
    ///        되돌린다. 경로 실패 후퇴는 <b>pid별</b>, 목록 실패 후퇴는 <b>전역</b>이다.</item>
    /// </list>
    /// 설계 정본과 기각한 대안(시간 비교 재시도 · 마지막 시도 기준 창 · 전역 경로 후퇴)의 근거는
    /// <c>docs/platform/GAME_DETECTION_FAILURE_CACHE.md</c>의 「4-1. ★ FC-1 확장 설계」에 있다.
    /// </summary>
    internal sealed class WindowsGameProcessProbe
    {
        #region Win32 선언 (전부 조회 전용 — 쓰기 계열은 선언 자체가 없다)

        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

        private const uint KEY_READ = 0x20019;
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_MORE_DATA = 234;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const uint REG_SZ = 1;
        private const uint REG_EXPAND_SZ = 2;

        /// <summary>메모리/스레드/종료 권한이 전혀 없는 최소 조회 권한(Vista+).</summary>
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyExW(IntPtr hKey, string lpSubKey, uint ulOptions,
            uint samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegEnumKeyExW(IntPtr hKey, uint dwIndex, StringBuilder lpName,
            ref uint lpcchName, IntPtr lpReserved, IntPtr lpClass, IntPtr lpcchClass,
            IntPtr lpftLastWriteTime);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueExW(IntPtr hKey, string lpValueName, IntPtr lpReserved,
            out uint lpType, byte[] lpData, ref uint lpcbData);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags,
            StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        #endregion

        /// <summary>게임 바가 관리하는 게임 목록의 위치. 값 하나(MatchedExeFullPath)만 읽는다.</summary>
        private const string GameConfigStoreChildrenKey = @"System\GameConfigStore\Children";

        private const string MatchedExeValueName = "MatchedExeFullPath";

        /// <summary>pid별 판정 유효기간. pid는 재사용될 수 있어 길게 두지 않는다(macOS와 같은 값).</summary>
        private const double VerdictCacheSeconds = 30.0;

        /// <summary>등록 목록 재열거 주기. 게임을 처음 실행하면 게임 바가 이 시점 이후 항목을 만들 수
        /// 있으므로(그 사이에는 "게임 아님" = 안 숨김이라 안전한 방향), 짧게 유지한다.</summary>
        private const double RegistryCacheSeconds = 30.0;

        /// <summary>재사용 버퍼 — 폴링마다 새 리스트를 만들지 않는다(24시간 상주 앱).</summary>
        private readonly List<string> _registeredGameExePaths = new List<string>(32);

        private readonly StringBuilder _nameBuffer = new StringBuilder(256);
        private readonly StringBuilder _exePathBuffer = new StringBuilder(1024);
        private byte[] _valueBuffer = new byte[1024];

        private double _registryCachedAt = double.NegativeInfinity;
        private bool _registryReadSucceeded;
        private string _registryFailureNote;

        /// <summary>직전 목록 조회의 성격. 기본값 <c>Retryable</c>이라 첫 폴링은 반드시 조회한다.</summary>
        private GameListReadOutcome _listOutcome;

        /// <summary>경로 조회(<c>OpenProcess</c> + 경로) 연속 실패 상태 — <b>pid별</b>이다.
        /// pid가 바뀌면 <c>ResetIfScopeChanged</c>가 계수와 창을 버린다.</summary>
        private readonly GameVerdictRetryState _pathRetry = new GameVerdictRetryState();

        /// <summary>게임바 목록 조회 연속 실패 상태 — 목록은 pid와 무관하므로 <b>전역</b>이다
        /// (이 프로브 인스턴스가 앱 전체에 1벌이다).</summary>
        private readonly GameVerdictRetryState _listRetry = new GameVerdictRetryState();

        /// <summary>접힘 요약 줄에 붙이는 태그 — 태그 기준 grep 집계가 깨지지 않게 본문과 같은 값을 쓴다.</summary>
        private const string LogTag = "[전체화면판정]";

        /// <summary>
        /// 예외 경고 접기의 중간 요약 주기. <b>0 = 주기 요약 없음</b>이라
        /// 「연속 실패 구간당 정확히 1줄」이 된다(설계 정본 4-1 항목 4의 문구 그대로).
        /// 접힌 횟수는 사라지지 않는다 — 구간이 끝나면 <see cref="FlushFoldedWarning"/>이 한 줄로 낸다.
        /// </summary>
        private const double WarnFoldHoldSeconds = 0.0;

        /// <summary>
        /// 경로 조회 예외 경고 접기. <b>연속 실패 구간당 1줄</b>로 묶되, 접는 키가
        /// (예외 형 · pid)라서 <b>이유나 대상이 바뀌면 새 줄이 나간다</b> — 묶음이 진단을 죽이지
        /// 않게 하는 자리다. <c>readonly</c>를 붙이지 않는다: 값 타입이라 readonly 필드에서는
        /// 복사본이 갱신되어 접힘 상태가 저장되지 않는다(<see cref="WallClockIntervalGate"/> 선례).
        /// </summary>
        private RepeatedLogFolder _pathWarnFolder;

        /// <summary>게임바 목록 예외 경고 접기. 키는 예외 형이다(대상이 계정 전역이라 pid가 없다).</summary>
        private RepeatedLogFolder _listWarnFolder;

        private uint _cachedPid;
        private bool _cachedPidValid;
        private bool _cachedVerdict;
        private string _cachedExePath;
        private double _cachedVerdictAt = double.NegativeInfinity;

        /// <summary>
        /// 이 pid의 프로세스가 "게임으로 등록된 실행 파일"인가.
        /// </summary>
        /// <param name="diagnostic">사람이 읽는 사유(전체화면 판정 로그에 그대로 붙는다).</param>
        /// <returns>확실히 게임일 때만 true. <b>조회가 어떤 이유로든 실패하면 false</b>
        /// (= 게임 아님 = 숨기지 않음). macOS의 "카테고리 미선언 -> 게임 아님"과 같은 계약이다.
        /// ★ 그 false는 <b>캐시하지 않는다</b> — 다음 폴링에 다시 묻는다(N-23).</returns>
        public bool IsGameProcess(uint pid, out string diagnostic)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            // 경로 실패 후퇴는 pid별이다. 전경이 바뀌면 계수와 창을 버린다 — 전역으로 두면 매번
            // 실패하는 비게임 전체화면 앱의 후퇴가 게임으로 전환한 뒤까지 남아, 게임 위에 최대
            // 약 30초 노출이 새로 생긴다(옛 판에는 없던 노출이다).
            _pathRetry.ResetIfScopeChanged(pid);

            if (_cachedPidValid && pid == _cachedPid && now - _cachedVerdictAt < VerdictCacheSeconds)
            {
                diagnostic = DescribeVerdict(_cachedExePath, _cachedVerdict);
                return _cachedVerdict;
            }

            // 한 폴링의 판정 시도는 «경로 조회 + 목록 갱신» 한 묶음이다. 둘 중 하나라도 후퇴 창
            // 안이면 이 폴링은 아무것도 조회하지 않는다(목록이 후퇴 중인데 경로만 폴링마다 묻는
            // 것을 막는다). || 단축 평가라 경로가 막히면 목록 쪽 문은 열리지도 않는다.
            if (!_pathRetry.TryBeginAttempt(now) || !_listRetry.TryBeginAttempt(now))
            {
                diagnostic = DescribeBackoff();
                return false;
            }

            string exePath = TryGetProcessImagePath(pid, now);
            bool pathResolved = !string.IsNullOrEmpty(exePath);
            if (pathResolved)
            {
                _pathRetry.NoteSuccess();
                FlushFoldedWarning(ref _pathWarnFolder, now);
            }
            else
            {
                _pathRetry.NoteFailure(now);
            }

            GameListReadOutcome listOutcome = RefreshRegisteredGamesIfStale(now);
            if (GameVerdictRetryPolicy.HoldsWithoutRetry(listOutcome))
            {
                _listRetry.NoteSuccess();
                FlushFoldedWarning(ref _listWarnFolder, now);
            }
            else
            {
                _listRetry.NoteFailure(now);
            }

            bool isGame = WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                exePath, _registeredGameExePaths);

            // ★ 확정 판정만 저장한다. 실패에서 나온 false를 저장하면 그 실패가 30초 굳어
            //   감지된 게임 위에서 캐릭터가 약 30초 동안 다시 나온다(N-23의 본체).
            if (GameVerdictRetryPolicy.ShouldCacheVerdict(pathResolved, listOutcome))
            {
                _cachedPid = pid;
                _cachedPidValid = true;
                _cachedVerdict = isGame;
                _cachedExePath = exePath;
                _cachedVerdictAt = now;
            }
            else
            {
                // 옛 확정 판정도 버린다 — 실패한 조회 뒤에 남겨 두면 «언제 잰 값인지» 모르는
                // 판정이 최대 30초 더 살아 있게 된다.
                _cachedPidValid = false;
            }

            diagnostic = DescribeVerdict(exePath, isGame);
            return isGame;
        }

        /// <summary>
        /// 연속 실패 구간이 끝났으니 접혀 있던 반복 횟수를 한 줄로 낸다 — <b>접은 정보를 잃지 않는다</b>
        /// ("로그를 줄인다"가 "눈을 감는다"가 되면 안 된다는 <see cref="RepeatedLogFolder"/>의 선).
        /// </summary>
        private static void FlushFoldedWarning(ref RepeatedLogFolder folder, double now)
        {
            if (folder.TryFlush(now, WarnFoldHoldSeconds, out int folded) && folded > 0)
            {
                Debug.LogWarning(RepeatedLogFolder.Describe(LogTag, folded));
            }
        }

        /// <summary>후퇴 창 안이라 이번 폴링에서 아무것도 조회하지 않았을 때의 사유.</summary>
        private string DescribeBackoff()
        {
            return $"전경 실행 파일=(조회 보류 — 경로 연속 실패 {_pathRetry.ConsecutiveFailures}회), " +
                $"게임바 목록 연속 실패 {_listRetry.ConsecutiveFailures}회, " +
                $"연속 실패 {GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold}회 뒤 " +
                $"{GameVerdictRetryPolicy.BackoffWindowSeconds:F0}초 후퇴 중 -> 게임=False";
        }

        private string DescribeVerdict(string exePath, bool isGame)
        {
            // 폴링(1.5초)마다 한 번 만드는 문자열 — macOS 쪽 사유 문자열과 같은 비용 등급이다.
            string where = _registryReadSucceeded
                ? $"게임바 등록 {_registeredGameExePaths.Count}건"
                : $"게임바 목록 조회 실패({_registryFailureNote})";
            string exe = string.IsNullOrEmpty(exePath) ? "(실행 파일 경로 조회 실패)" : exePath;
            return $"전경 실행 파일={exe}, {where} -> 게임={isGame}";
        }

        /// <summary>pid -> 실행 파일 전체 경로. 실패는 null(= 게임 아님으로 떨어진다).
        /// <paramref name="now"/>는 예외 경고 접기에만 쓴다(조회 자체는 시각을 보지 않는다).</summary>
        private string TryGetProcessImagePath(uint pid, double now)
        {
            if (pid == 0) return null;

            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (handle == IntPtr.Zero) return null;   // 보호된 프로세스 등 — 조회 불가 = 게임 아님.

                _exePathBuffer.Length = 0;
                _exePathBuffer.EnsureCapacity(1024);
                uint size = (uint)_exePathBuffer.Capacity;

                // dwFlags = 0 -> Win32 경로 형식("C:\..."). 레지스트리의 MatchedExeFullPath와 같은 표기다.
                if (!QueryFullProcessImageNameW(handle, 0, _exePathBuffer, ref size)) return null;
                return _exePathBuffer.ToString();
            }
            catch (Exception e)
            {
                // 연속 실패 구간당 1줄. 키로 접으므로 접히는 폴링에서는 보간 문자열을 만들지도 않는다
                // (RepeatedLogFolder가 「완성된 줄로 접으면 게이트 뒤로 밀려 한 번도 실행되지 않는다」는
                //  사고를 겪고 만든 오버로드다). 예외 형이나 pid가 바뀌면 새 줄이 나간다.
                bool emit = _pathWarnFolder.ShouldEmit(e.GetType().Name, (int)pid, 0f, now,
                    WarnFoldHoldSeconds, out int folded);
                if (folded > 0) Debug.LogWarning(RepeatedLogFolder.Describe(LogTag, folded));
                if (emit)
                {
                    Debug.LogWarning($"[전체화면판정] pid {pid}의 실행 파일 경로를 읽지 못했습니다" +
                        $"({e.GetType().Name}) — 게임이 아닌 것으로 간주해 숨기지 않습니다.");
                }
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero) CloseHandle(handle);
            }
        }

        /// <summary>
        /// <c>HKCU\System\GameConfigStore\Children\*\MatchedExeFullPath</c>를 <b>읽기 전용</b>으로 훑어
        /// "게임으로 등록된 실행 파일" 목록을 만든다. 실패하면 목록을 비우고 사유만 남긴다 —
        /// 빈 목록은 곧 "아무것도 게임이 아니다" = 숨기지 않음이라 실패가 안전한 방향으로만 작동한다.
        /// </summary>
        private GameListReadOutcome RefreshRegisteredGamesIfStale(double now)
        {
            // 「유지」 계열(성공 · 키 없음 · 하위 키 상한)만 30초 동안 다시 묻지 않는다.
            // 재시도 대상 실패는 이 문을 그대로 통과해 다음 폴링에 다시 시도된다.
            if (GameVerdictRetryPolicy.HoldsWithoutRetry(_listOutcome)
                && now - _registryCachedAt < RegistryCacheSeconds)
            {
                return _listOutcome;
            }

            _registeredGameExePaths.Clear();
            _registryReadSucceeded = false;
            _registryFailureNote = null;

            GameListReadOutcome outcome = GameListReadOutcome.Retryable;
            IntPtr childrenKey = IntPtr.Zero;
            try
            {
                int rc = RegOpenKeyExW(HKEY_CURRENT_USER, GameConfigStoreChildrenKey, 0, KEY_READ,
                    out childrenKey);
                if (rc != ERROR_SUCCESS || childrenKey == IntPtr.Zero)
                {
                    // 게임 바를 한 번도 쓰지 않은 계정에는 이 키가 아예 없다(정상적인 상황) —
                    // 그때 rc는 2이고 다시 물어도 결과가 같으므로 30초 유지한다. rc가 성공인데
                    // 핸들이 0인 조합은 rc가 2가 아니므로 규칙이 알아서 재시도 대상으로 분류한다.
                    _registryFailureNote = $"키 열기 실패 rc={rc}";
                    outcome = GameVerdictRetryPolicy.ClassifyKeyOpenResult(rc);
                }
                else
                {
                    outcome = EnumerateRegisteredGames(childrenKey);
                }
            }
            catch (Exception e)
            {
                _registeredGameExePaths.Clear();
                _registryFailureNote = e.GetType().Name;
                outcome = GameListReadOutcome.Retryable;

                // 연속 실패 구간당 1줄(키 = 예외 형). 같은 예외가 이어지면 접고, 예외 형이 바뀌면
                // 새 줄이 나간다 — 「같은 이유로 계속 실패」와 「이유가 바뀜」이 로그에서 갈린다.
                bool emitListWarn = _listWarnFolder.ShouldEmit(e.GetType().Name, 0, 0f, now,
                    WarnFoldHoldSeconds, out int foldedListWarn);
                if (foldedListWarn > 0) Debug.LogWarning(RepeatedLogFolder.Describe(LogTag, foldedListWarn));
                if (emitListWarn)
                {
                    Debug.LogWarning("[전체화면판정] 게임바 등록 목록(HKCU\\System\\GameConfigStore)을 " +
                        $"읽지 못했습니다({e.GetType().Name}) — 전체화면 앱을 게임이 아닌 것으로 간주해 " +
                        "숨기지 않습니다.");
                }
            }
            finally
            {
                if (childrenKey != IntPtr.Zero) RegCloseKey(childrenKey);
            }

            _registryReadSucceeded = _registryFailureNote == null;

            // ★ 이 줄은 성공에서도 실패에서도 **무조건** 돈다. 그러니 이 줄이 「실패를 30초 묶지
            //   않는」 이유가 아니다 — 실패를 다시 묻게 하는 것은 위 신선도 문의 **결과 항**
            //   (GameVerdictRetryPolicy.HoldsWithoutRetry(_listOutcome))이다. 재시도 대상 실패는
            //   그 항이 false라 시각이 아무리 새로워도 문을 통과해 다시 조회한다.
            //   ⇒ **결과 항을 지우면서 「시각을 뒤로 옮겼으니 괜찮다」고 읽지 마라.** 그 순간
            //     FC-1이 조용히 원래 버그(실패가 30초 굳는다)로 돌아간다.
            //   시각을 시도 뒤로 옮긴 것은 별개의 이득이다: 옛 판은 시도 **전에** 박아서 조회가
            //   얼마나 걸렸든 30초 창이 시도 시점부터 시작됐다.
            _registryCachedAt = now;
            _listOutcome = outcome;
            return outcome;
        }

        /// <summary>열린 키에서 하위 항목을 훑어 목록을 채운다. 반환값은 그 조회의 성격이다.</summary>
        private GameListReadOutcome EnumerateRegisteredGames(IntPtr childrenKey)
        {
            for (uint index = 0; ; index++)
            {
                _nameBuffer.Length = 0;
                _nameBuffer.EnsureCapacity(256);
                uint nameLength = (uint)_nameBuffer.Capacity;

                int enumRc = RegEnumKeyExW(childrenKey, index, _nameBuffer, ref nameLength,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (enumRc == ERROR_NO_MORE_ITEMS) return GameListReadOutcome.Success;
                if (enumRc != ERROR_SUCCESS)
                {
                    _registryFailureNote = $"하위 키 열거 실패 rc={enumRc} (index {index})";
                    return GameListReadOutcome.Retryable;
                }

                string exePath = TryReadMatchedExePath(childrenKey, _nameBuffer.ToString());
                if (!string.IsNullOrEmpty(exePath)) _registeredGameExePaths.Add(exePath);

                // 게임 바 항목이 비정상적으로 많은 계정에서 폴링이 길어지지 않도록 상한을 둔다.
                // 항목 수가 그대로면 매번 같은 결과라 재시도 대상이 아니다(30초 유지).
                if (index > 4096)
                {
                    _registryFailureNote = "하위 키가 4096개를 넘어 열거를 중단";
                    return GameListReadOutcome.SubkeyLimitReached;
                }
            }
        }

        private string TryReadMatchedExePath(IntPtr parentKey, string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;

            IntPtr childKey = IntPtr.Zero;
            try
            {
                if (RegOpenKeyExW(parentKey, childName, 0, KEY_READ, out childKey) != ERROR_SUCCESS
                    || childKey == IntPtr.Zero)
                {
                    return null;
                }

                uint cb = (uint)_valueBuffer.Length;
                int rc = RegQueryValueExW(childKey, MatchedExeValueName, IntPtr.Zero, out uint type,
                    _valueBuffer, ref cb);

                if (rc == ERROR_MORE_DATA)
                {
                    // 경로가 버퍼보다 길다 — 딱 필요한 만큼 키우고 한 번만 재시도한다.
                    _valueBuffer = new byte[cb];
                    cb = (uint)_valueBuffer.Length;
                    rc = RegQueryValueExW(childKey, MatchedExeValueName, IntPtr.Zero, out type,
                        _valueBuffer, ref cb);
                }

                if (rc != ERROR_SUCCESS) return null;
                if (type != REG_SZ && type != REG_EXPAND_SZ) return null;
                if (cb < 2) return null;

                // REG_SZ는 UTF-16이고 종단 NUL이 바이트 수에 포함될 수도, 안 될 수도 있다.
                // 남은 NUL은 WindowsGameExecutablePolicy.PathEquals가 어차피 잘라 내지만,
                // 로그에 그대로 찍히지 않도록 여기서 한 번 다듬는다.
                string raw = Encoding.Unicode.GetString(_valueBuffer, 0, (int)(cb / 2) * 2);
                int nul = raw.IndexOf('\0');
                if (nul >= 0) raw = raw.Substring(0, nul);
                return raw.Length == 0 ? null : raw;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (childKey != IntPtr.Zero) RegCloseKey(childKey);
            }
        }
    }
}
#endif
