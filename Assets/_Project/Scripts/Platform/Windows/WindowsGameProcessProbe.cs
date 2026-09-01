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
        /// (= 게임 아님 = 숨기지 않음). macOS의 "카테고리 미선언 -> 게임 아님"과 같은 계약이다.</returns>
        public bool IsGameProcess(uint pid, out string diagnostic)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (_cachedPidValid && pid == _cachedPid && now - _cachedVerdictAt < VerdictCacheSeconds)
            {
                diagnostic = DescribeVerdict(_cachedExePath, _cachedVerdict);
                return _cachedVerdict;
            }

            string exePath = TryGetProcessImagePath(pid);
            RefreshRegisteredGamesIfStale(now);

            bool isGame = WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                exePath, _registeredGameExePaths);

            _cachedPid = pid;
            _cachedPidValid = true;
            _cachedVerdict = isGame;
            _cachedExePath = exePath;
            _cachedVerdictAt = now;

            diagnostic = DescribeVerdict(exePath, isGame);
            return isGame;
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

        /// <summary>pid -> 실행 파일 전체 경로. 실패는 null(= 게임 아님으로 떨어진다).</summary>
        private string TryGetProcessImagePath(uint pid)
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
                Debug.LogWarning($"[전체화면판정] pid {pid}의 실행 파일 경로를 읽지 못했습니다" +
                    $"({e.GetType().Name}) — 게임이 아닌 것으로 간주해 숨기지 않습니다.");
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
        private void RefreshRegisteredGamesIfStale(double now)
        {
            if (now - _registryCachedAt < RegistryCacheSeconds) return;
            _registryCachedAt = now;
            _registeredGameExePaths.Clear();
            _registryReadSucceeded = false;
            _registryFailureNote = null;

            IntPtr childrenKey = IntPtr.Zero;
            try
            {
                int rc = RegOpenKeyExW(HKEY_CURRENT_USER, GameConfigStoreChildrenKey, 0, KEY_READ,
                    out childrenKey);
                if (rc != ERROR_SUCCESS || childrenKey == IntPtr.Zero)
                {
                    // 게임 바를 한 번도 쓰지 않은 계정에는 이 키가 아예 없다(정상적인 상황).
                    _registryFailureNote = $"키 열기 실패 rc={rc}";
                    return;
                }

                for (uint index = 0; ; index++)
                {
                    _nameBuffer.Length = 0;
                    _nameBuffer.EnsureCapacity(256);
                    uint nameLength = (uint)_nameBuffer.Capacity;

                    int enumRc = RegEnumKeyExW(childrenKey, index, _nameBuffer, ref nameLength,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    if (enumRc == ERROR_NO_MORE_ITEMS) break;
                    if (enumRc != ERROR_SUCCESS)
                    {
                        _registryFailureNote = $"하위 키 열거 실패 rc={enumRc} (index {index})";
                        break;
                    }

                    string exePath = TryReadMatchedExePath(childrenKey, _nameBuffer.ToString());
                    if (!string.IsNullOrEmpty(exePath)) _registeredGameExePaths.Add(exePath);

                    // 게임 바 항목이 비정상적으로 많은 계정에서 폴링이 길어지지 않도록 상한을 둔다.
                    if (index > 4096)
                    {
                        _registryFailureNote = "하위 키가 4096개를 넘어 열거를 중단";
                        break;
                    }
                }

                _registryReadSucceeded = _registryFailureNote == null;
            }
            catch (Exception e)
            {
                _registeredGameExePaths.Clear();
                _registryFailureNote = e.GetType().Name;
                Debug.LogWarning("[전체화면판정] 게임바 등록 목록(HKCU\\System\\GameConfigStore)을 " +
                    $"읽지 못했습니다({e.GetType().Name}) — 전체화면 앱을 게임이 아닌 것으로 간주해 " +
                    "숨기지 않습니다.");
            }
            finally
            {
                if (childrenKey != IntPtr.Zero) RegCloseKey(childrenKey);
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
