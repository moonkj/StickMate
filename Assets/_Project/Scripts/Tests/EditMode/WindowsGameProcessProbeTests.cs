using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #8 — <b>「레지스트리 쓰기 API가 없음을 기계로 잠근다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>Platform/Windows/WindowsGameProcessProbe.cs</c>의 클래스 문서는 <b>「절대 불변 원칙 3
    /// (유저 자산 불변) — 이 파일이 지키는 방식」</b> 절에서 이렇게 단언한다:
    ///
    /// <list type="bullet">
    ///  <item><i>"레지스트리는 <c>KEY_READ</c>로만 연다. 쓰기 계열(RegSetValueEx / RegCreateKeyEx /
    ///    RegDeleteKey / RegDeleteValue)은 <b>선언조차 하지 않는다</b> — 선언이 없으면 실수로도 부를 수
    ///    없다. <c>WindowsGameProcessProbeTests</c>가 이 파일에 그 이름들이 없음을 기계로 잠근다."</i></item>
    ///  <item><i>"프로세스 핸들은 <c>PROCESS_QUERY_LIMITED_INFORMATION</c>만 요청한다."</i></item>
    ///  <item><i>"타 프로세스에 어떤 메시지도 보내지 않고, 어떤 창도 건드리지 않는다."</i></item>
    /// </list>
    ///
    /// <b>그 「기계」는 2026-09-06까지 존재하지 않았다.</b> <c>CommentReferenceAuditTests</c>의
    /// 확장(확장자 없는 인용까지 보게 넓힌 판)이 처음 잡은 8건 중 하나이고, <b>원칙 3에 직접 걸린
    /// 유일한 건</b>이라 가장 먼저 만든다. 이 파일이 그 문장을 참으로 만든다.
    ///
    /// ============================================================================
    /// ★ 이 감사가 <b>새로 잠그는 것</b> — 중복이 아니다
    /// ============================================================================
    /// <see cref="UserAssetImmutabilityAuditTests"/>가 <b>저장소 전체</b>에 대해 레지스트리 쓰기 API
    /// 부재와 쓰기 권한 플래그 부재를 이미 잠근다(그쪽 «레지스트리_쓰기_API가_저장소_어디에도_없다»).
    /// <b>즉 위 첫 문장이 약속한 사실은 실제로 지켜지고 있었다</b> — 다만 <b>그 문장이 이름으로 지목한
    /// 테스트</b>가 없었을 뿐이다. 그래서 이 파일은 저쪽을 베끼지 않고 <b>저쪽이 구조적으로 못 보는
    /// 세 가지</b>를 맡는다:
    ///
    /// <list type="number">
    ///  <item><b>이 파일이 부르는 네이티브 API 전체의 집합 등호.</b> 니들 목록은 <b>적어 둔 이름만</b>
    ///    본다 — 아무도 예상 못 한 새 P/Invoke(예: <c>RegSetKeyValueW</c>, <c>NtSetValueKey</c>,
    ///    <c>SHDeleteKeyW</c>)는 니들 목록에 없으므로 <b>조용히 통과한다</b>. 집합 등호는
    ///    <b>목록에 없는 것이 늘어나도</b> 반드시 빨개진다.</item>
    ///  <item><b>프로세스 핸들 권한</b>. 저쪽은 레지스트리만 본다. <c>PROCESS_VM_READ</c>가 붙는 날
    ///    저쪽은 초록이다.</item>
    ///  <item><b>메시지 전송 · 창 조작 · 원격 메모리/스레드</b>. 위 세 번째 문장은 지금까지
    ///    <b>아무도 재지 않았다</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 왜 리플렉션을 한 줄도 쓰지 않는가 (CLAUDE.md 활성 빌드 타깃 규칙)
    /// ============================================================================
    /// 이 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이다. <b>macOS 타깃에서는 타입이 아예
    /// 존재하지 않는다.</b> 리플렉션 감사는 없는 타입을 셀 수 없고, 그 0건은 «깨끗함»과 글자 하나
    /// 다르지 않다 — 이 저장소가 정본에 적어 둔 사각지대 그 자체다.
    /// 그래서 <b>소스 텍스트만</b> 읽는다. 이 감사는 osx 타깃에서도 win 타깃에서도 <b>같은 것</b>을 잰다.
    ///
    /// ============================================================================
    /// ★ 「없다」를 말하기 전에 「있다」를 먼저 보인다 (CLAUDE.md 부재 단언 규칙)
    /// ============================================================================
    /// 부재 단언은 썩으면 <b>조용히 초록</b>이 된다. 그래서 이 파일의 모든 부재 단언에는 짝이 있다:
    /// <list type="bullet">
    ///  <item>파일을 <b>타입 선언으로</b> 찾는다(경로 하드코딩 금지 — 파일이 옮겨지면 시끄럽게 빨개진다).</item>
    ///  <item>스캐너가 그 파일에서 <b>실제로 7개의 P/Invoke를 찾아낸다</b>는 것을 먼저 단언한다.</item>
    ///  <item>금지 니들이 <b>주석에는 실재한다</b>는 것을 같은 테스트에서 대조한다 — 그 정직한 주석이
    ///    바로 이 감사를 호출한 문장이므로, 그것이 사라지면 이 감사의 전제도 사라진다.</item>
    /// </list>
    /// </summary>
    public sealed class WindowsGameProcessProbeTests
    {
        private const string LogPrefix = "[게임프로세스탐침감사]";

        /// <summary>★ <b>존재 단언용 니들.</b> 이름이 바뀌면 아래 <c>파일을_찾지_못했다</c>가
        /// 시끄럽게 빨개진다(조용한 0건이 되지 않는다). 경로가 아니라 <b>타입 선언</b>으로 찾으므로
        /// 파일을 옮기거나 <c>partial</c>로 쪼개도 따라간다.</summary>
        private const string ProbeTypeName = "WindowsGameProcessProbe";

        // ====================================================================
        // 1. 허용 명부 — 사유를 못 대는 항목은 여기 있으면 안 된다
        // ====================================================================

        /// <summary>
        /// 이 파일이 <b>부를 수 있는 네이티브 함수 전부</b>. 집합 <b>등호</b>로 쓴다 —
        /// 하나라도 늘거나 줄면 빨개진다.
        ///
        /// <para>★ <b>이게 왜 니들 목록보다 강한가.</b> 니들 목록(아래 <see cref="ForbiddenRegistryWriteApis"/>)은
        /// <b>내가 상상한 이름만</b> 본다. 레지스트리에 쓰는 방법은 그 넷 말고도 많다 —
        /// <c>RegSetKeyValueW</c>, <c>SHSetValueW</c>, <c>NtSetValueKey</c>, <c>RegRenameKey</c>…
        /// 그런 이름이 들어오면 니들 목록은 <b>조용히 초록</b>이지만 이 집합 등호는 반드시 빨개진다.</para>
        ///
        /// <para>★ <b>정당하게 늘어나는 경우</b>도 여기서 멈추는 것이 맞다. 조회 전용 API를 새로
        /// 추가했다면 이 표에 <b>사유와 함께</b> 한 줄 적으면 통과한다 — 그 한 줄이 «원칙 3을 다시
        /// 한 번 생각했다»는 기록이 된다.</para>
        /// </summary>
        private static readonly (string Name, string Why)[] AllowedNativeImports =
        {
            ("RegOpenKeyExW",
                "레지스트리 키 열기. samDesired는 KEY_READ 하나뿐임을 아래에서 따로 잠근다."),
            ("RegEnumKeyExW",
                "하위 키 '이름'만 열거한다. 값을 만들거나 지우지 않는다."),
            ("RegQueryValueExW",
                "값 하나(MatchedExeFullPath)를 읽는다. 인용이지 수정이 아니다."),
            ("RegCloseKey",
                "핸들 반납. 레지스트리 내용을 바꾸지 않는다."),
            ("OpenProcess",
                "pid -> 핸들. 요청 권한이 PROCESS_QUERY_LIMITED_INFORMATION 하나뿐임을 아래에서 잠근다."),
            ("QueryFullProcessImageNameW",
                "핸들 -> exe 경로. 대상 프로세스의 메모리를 읽지 않는다(경로는 커널이 준다)."),
            ("CloseHandle",
                "핸들 반납. 대상 프로세스에 아무 영향이 없다."),
        };

        /// <summary>
        /// 선언조차 없어야 하는 레지스트리 <b>쓰기</b> 계열. 위 집합 등호와 <b>겹치는 것이 의도</b>다 —
        /// 두 다리로 서면 한쪽이 눈이 멀어도 다른 쪽이 답을 낸다. 이쪽의 값은 <b>실패 메시지가
        /// 구체적</b>이라는 것이다("집합이 다르다"가 아니라 "레지스트리에 쓰는 API가 생겼다").
        ///
        /// <para>★ 이 이름들은 <b>그 파일의 주석에 실제로 적혀 있다</b>(원문 인용). 그래서 이 스캔은
        /// 반드시 <b>주석을 걷어낸</b> 소스에서 돌아야 한다 — 안 그러면 <b>정직하게 금지를 적어 둔
        /// 그 문장 때문에</b> 감사가 빨개지고, 다음 사람은 사실을 지워서 초록을 만든다.</para>
        /// </summary>
        private static readonly (string Needle, string Why)[] ForbiddenRegistryWriteApis =
        {
            ("RegSetValue", "값 쓰기. 유저·OS 소유의 데이터를 덮어쓴다."),
            ("RegCreateKey", "키 생성. 없던 것을 만드는 것도 수정이다."),
            ("RegDeleteKey", "키 삭제."),
            ("RegDeleteValue", "값 삭제."),
            ("RegSetKeyValue", "한 방에 열고 쓰는 축약판 — 위 넷을 피해 가는 가장 흔한 경로다."),
            ("RegRenameKey", "키 이름 변경."),
            ("SHSetValue", "셸 헬퍼판 쓰기."),
            ("SHDeleteKey", "셸 헬퍼판 삭제."),
        };

        /// <summary>
        /// 클래스 문서 세 번째 문장(<i>"타 프로세스에 어떤 메시지도 보내지 않고, 어떤 창도 건드리지
        /// 않는다"</i>)을 재는 니들. <b>지금까지 아무도 재지 않던 문장</b>이다.
        /// </summary>
        private static readonly (string Needle, string Why)[] ForbiddenReachIntoOthersApis =
        {
            ("SendMessage", "타 창에 동기 메시지. 상대 앱의 상태를 바꿀 수 있다."),
            ("PostMessage", "같은 것의 비동기판."),
            ("SetWindowPos", "남의 창 위치·크기·Z순서 변경 — 원칙 3이 이름으로 금지한 형태다."),
            ("MoveWindow", "같은 것의 축약판."),
            ("ShowWindow", "남의 창 최소화/복원."),
            ("CloseWindow", "남의 창 최소화(이름과 달리 닫기가 아니지만 여전히 조작이다)."),
            ("DestroyWindow", "창 파괴."),
            ("SetForegroundWindow", "포커스 강탈 — 비침해 원칙(원칙 2)에도 걸린다."),
            ("SetWindowLong", "남의 창 스타일 변경."),
            ("TerminateProcess", "프로세스 종료. 유저의 작업을 잃게 만든다."),
            ("ReadProcessMemory", "메모리 읽기 — PROCESS_QUERY_LIMITED_INFORMATION으로는 애초에 불가능하고, "
                                  + "이 이름이 등장한다면 권한도 함께 넓어졌다는 뜻이다."),
            ("WriteProcessMemory", "메모리 쓰기."),
            ("VirtualAllocEx", "남의 프로세스에 메모리 확보 — 코드 주입의 첫 단계."),
            ("CreateRemoteThread", "남의 프로세스에서 스레드 실행."),
            ("SetWindowsHookEx", "전역 훅. 입력을 가로챈다."),
        };

        /// <summary>레지스트리를 여는 권한. <b>집합 등호</b>로 쓴다 — 문서가 «KEY_READ로만 연다»고
        /// 단언하므로 기대 집합의 원소는 하나다.
        /// <para>★ <c>HKEY_CURRENT_USER</c>가 이 검사를 첫날부터 거짓 빨강으로 만들 수 있었다 —
        /// 부분 문자열로 <c>KEY_</c>를 찾으면 <c>H<b>KEY_</b>CURRENT_USER</c>가 걸린다.
        /// 그래서 <see cref="IdentifiersStartingWith"/>는 <b>낱말 전체</b>가 접두로 시작할 때만 센다.
        /// 그 함정은 <see cref="NegativeControl_HKEY_CURRENT_USER는_권한_플래그가_아니다"/>가 박제한다.</para></summary>
        private const string AllowedRegistryAccessRight = "KEY_READ";

        /// <summary>프로세스 핸들 권한. 같은 이유로 집합 등호이며 원소는 하나다.</summary>
        private const string AllowedProcessAccessRight = "PROCESS_QUERY_LIMITED_INFORMATION";

        private const string RegistryRightPrefix = "KEY_";
        private const string ProcessRightPrefix = "PROCESS_";

        // ====================================================================
        // 2. 스캐너 — 순수 함수. 아래 네거티브 컨트롤이 <b>같은 함수</b>에 가짜 소스를 먹인다.
        // ====================================================================

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>
        /// <b>낱말 전체</b>가 <paramref name="prefix"/>로 시작하는 식별자를 전부 모은다(중복 제거).
        /// <para>★ 부분 문자열이 아니다. <c>HKEY_CURRENT_USER</c>는 <c>KEY_</c>로 <b>시작하지 않으므로</b>
        /// 잡히지 않고, <c>KEY_READ</c>는 잡힌다. 이 구분이 이 파일의 생사를 가른다.</para>
        /// </summary>
        internal static SortedSet<string> IdentifiersStartingWith(string stripped, string prefix)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(stripped) || string.IsNullOrEmpty(prefix)) return found;

            int i = 0;
            while (i < stripped.Length)
            {
                if (!IsIdentifierChar(stripped[i])) { i++; continue; }
                int start = i;
                while (i < stripped.Length && IsIdentifierChar(stripped[i])) i++;
                string word = stripped.Substring(start, i - start);
                if (word.Length > prefix.Length && word.StartsWith(prefix, StringComparison.Ordinal))
                    found.Add(word);
            }
            return found;
        }

        /// <summary>
        /// <c>extern</c> 뒤에 오는 <b>메서드 이름</b>을 전부 모은다 —
        /// <c>private static extern int RegOpenKeyExW(</c> → <c>RegOpenKeyExW</c>.
        /// <para>반환형이 <c>int</c>든 <c>IntPtr</c>든 <c>bool</c>이든 상관없이,
        /// <c>extern</c> 이후 <b>바로 뒤에 여는 괄호가 붙은 첫 식별자</b>를 이름으로 본다.</para>
        /// </summary>
        internal static SortedSet<string> ExternMethodNames(string stripped)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(stripped)) return found;

            const string keyword = "extern";
            int from = 0;
            while (true)
            {
                int at = stripped.IndexOf(keyword, from, StringComparison.Ordinal);
                if (at < 0) return found;
                from = at + keyword.Length;

                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(stripped[before])) continue;
                if (from < stripped.Length && IsIdentifierChar(stripped[from])) continue;

                // extern 이후로 식별자를 훑다가 '(' 가 바로 붙은 것을 만나면 그게 이름이다.
                int p = from;
                while (p < stripped.Length && stripped[p] != ';' && stripped[p] != '}')
                {
                    if (!IsIdentifierChar(stripped[p])) { p++; continue; }
                    int start = p;
                    while (p < stripped.Length && IsIdentifierChar(stripped[p])) p++;

                    int q = p;
                    while (q < stripped.Length && (stripped[q] == ' ' || stripped[q] == '\t'
                                                   || stripped[q] == '\r' || stripped[q] == '\n')) q++;
                    if (q < stripped.Length && stripped[q] == '(')
                    {
                        found.Add(stripped.Substring(start, p - start));
                        break;
                    }
                }
            }
        }

        /// <summary>니들 히트를 <c>이름 → 건수</c>로 돌려준다(0건은 넣지 않는다).</summary>
        internal static List<string> NeedleHits(string haystack, (string Needle, string Why)[] needles)
        {
            var hits = new List<string>();
            if (string.IsNullOrEmpty(haystack)) return hits;
            foreach ((string needle, string why) in needles)
            {
                int count = 0, from = 0;
                while (true)
                {
                    int at = haystack.IndexOf(needle, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    count++;
                    from = at + 1;
                }
                if (count > 0) hits.Add($"'{needle}' {count}건 — {why}");
            }
            return hits;
        }

        // ====================================================================
        // 3. 대상 파일 찾기 — 경로가 아니라 <b>타입 선언</b>으로
        // ====================================================================

        private readonly struct Target
        {
            public readonly string Path;
            public readonly string Raw;
            public readonly string Stripped;

            public Target(string path, string raw, string stripped)
            {
                Path = path;
                Raw = raw;
                Stripped = stripped;
            }
        }

        private static Target FindProbeSource()
        {
            string[] all = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(all.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Length}개밖에 읽지 못했습니다 " +
                $"({EntitlementAuditSource.ScriptsRoot}). 이 상태의 '위반 0건'은 측정이 아닙니다.");

            var matches = new List<Target>();
            foreach (string path in all)
            {
                string raw = File.ReadAllText(path);
                string stripped = EntitlementAuditSource.StripComments(raw);
                if (!EntitlementAuditSource.DeclaresType(stripped, ProbeTypeName)) continue;
                matches.Add(new Target(path, raw, stripped));
            }

            Assert.AreEqual(1, matches.Count,
                $"{LogPrefix} '{ProbeTypeName}' 타입을 선언하는 프로덕션 파일이 {matches.Count}개입니다(기대 1). " +
                "0개면 이름이 바뀌었거나 파일이 사라진 것이고, 2개 이상이면 partial로 쪼개진 것입니다. " +
                "어느 쪽이든 <b>여기서 멈추는 것이 맞습니다</b> — 그대로 두면 아래 모든 부재 단언이 " +
                "'검사할 파일이 없어서 위반 0건'이 되어 조용히 초록이 됩니다(CLAUDE.md 부재 단언 규칙). " +
                "이름이 바뀌었다면 WindowsGameProcessProbe.cs 클래스 문서의 원칙 3 절 인용도 함께 낡았습니다.");

            return matches[0];
        }

        private static string Rel(string path)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return path.Length > root.Length ? path.Substring(root.Length + 1) : path;
        }

        // ====================================================================
        // 4. ★ 본론 (1) — 이 파일이 부르는 네이티브 API 전체
        // ====================================================================

        /// <summary>
        /// <b>집합 등호.</b> 「금지 목록에 없다」가 아니라 「허용 목록과 정확히 같다」로 쓴다.
        /// <para>부재 단언이 조용히 썩는 것을 구조적으로 막는 유일한 형태다 — 새 P/Invoke가 들어오면
        /// 그 이름을 내가 미리 알고 있었는지와 <b>무관하게</b> 빨개진다.</para>
        /// </summary>
        [Test]
        public void 이_파일이_부르는_네이티브_API는_허용_목록과_정확히_같다()
        {
            Target target = FindProbeSource();
            SortedSet<string> actual = ExternMethodNames(target.Stripped);

            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach ((string name, string why) in AllowedNativeImports)
            {
                Assert.IsNotEmpty(why, $"{LogPrefix} '{name}'의 사유가 비었습니다 — 근거를 못 대는 " +
                                       "허용 항목은 명부를 장식으로 만듭니다.");
                expected.Add(name);
            }

            // ★ 존재 쪽 먼저: 스캐너가 실제로 무언가를 찾아냈는가.
            Assert.IsNotEmpty(actual,
                $"{LogPrefix} {Rel(target.Path)}에서 extern 선언을 <b>하나도</b> 찾지 못했습니다. " +
                "P/Invoke 표기가 바뀌었거나(예: LibraryImport 소스 제너레이터로 이전) 파서가 죽었습니다. " +
                "이 상태에서 아래 '금지 API 0건'은 «없다»가 아니라 «못 봤다»입니다.");

            var added = new List<string>();
            foreach (string name in actual) if (!expected.Contains(name)) added.Add(name);

            var removed = new List<string>();
            foreach (string name in expected) if (!actual.Contains(name)) removed.Add(name);

            Debug.Log($"{LogPrefix} {Rel(target.Path)} — 네이티브 API {actual.Count}종: " +
                      string.Join(", ", actual));

            Assert.IsEmpty(added,
                $"{LogPrefix} 허용 목록에 <b>없는</b> 네이티브 API가 생겼습니다: {string.Join(", ", added)}\n" +
                "이 파일의 클래스 문서는 «레지스트리는 KEY_READ로만 연다 / 쓰기 계열은 선언조차 하지 " +
                "않는다 / 타 프로세스에 어떤 메시지도 보내지 않고 어떤 창도 건드리지 않는다»라고 " +
                "단언하고 있고, 그 문장이 CLAUDE.md 절대 불변 원칙 3(유저 자산 불변)의 Windows 쪽 " +
                "근거입니다. 조회 전용 API를 정당하게 추가한 것이라면 " +
                $"{nameof(AllowedNativeImports)}에 <b>사유와 함께</b> 한 줄 적으세요 — " +
                "그 한 줄이 «원칙 3을 다시 생각했다»는 기록입니다.");

            Assert.IsEmpty(removed,
                $"{LogPrefix} 허용 목록에 적힌 API가 소스에서 사라졌습니다: {string.Join(", ", removed)}\n" +
                "명부가 낡으면 그만큼 이 검사가 무뎌집니다(다음에 그 이름이 다시 들어와도 «원래 있던 것»으로 " +
                $"통과합니다). {nameof(AllowedNativeImports)}에서 지우세요.");
        }

        // ====================================================================
        // 5. ★ 본론 (2) — 인용 원문 그대로: 「그 이름들이 이 파일에 없다」
        // ====================================================================

        /// <summary>
        /// 인용된 문장을 <b>글자 그대로</b> 잰다. 그리고 <b>같은 테스트 안에서</b> 존재 대조를 붙인다:
        /// 같은 이름들이 <b>주석에는 실재</b>하는가.
        ///
        /// <para>★ 왜 그 대조가 필요한가. 이 스캔은 주석을 걷어낸 소스에서 돈다. 만약 스트리퍼가
        /// 고장 나 <b>전부</b>를 지워 버리면 «위반 0건»이 나오는데, 그건 «없다»가 아니라 «아무것도
        /// 안 봤다»다. 그리고 이 파일의 경우 <b>금지 이름들이 주석에 실제로 적혀 있다</b>
        /// (그 주석이 바로 이 테스트를 이름으로 부른 문장이다). 그래서 «원본에는 있고 걷어낸 뒤에는
        /// 없다»가 성립해야 비로소 0건이 값을 갖는다.</para>
        /// </summary>
        [Test]
        public void 레지스트리_쓰기_API는_이_파일에_선언조차_없다()
        {
            Target target = FindProbeSource();

            // ── 존재 쪽(대조): 원본 주석에는 금지 이름이 실재한다.
            List<string> inRaw = NeedleHits(target.Raw, ForbiddenRegistryWriteApis);
            Assert.IsNotEmpty(inRaw,
                $"{LogPrefix} {Rel(target.Path)}의 <b>원본</b>에서 금지 이름을 하나도 찾지 못했습니다.\n" +
                "이 파일은 클래스 문서에 «쓰기 계열(RegSetValueEx / RegCreateKeyEx / RegDeleteKey / " +
                "RegDeleteValue)은 선언조차 하지 않는다»고 적어 두었고, 그 문장이 이 테스트를 이름으로 " +
                "부릅니다. 그 문장이 사라졌다면 (ㄱ) 이 감사의 전제가 사라졌고 (ㄴ) 아래 «걷어낸 뒤 0건»이 " +
                "무엇과 대조되는지 알 수 없습니다. 주석을 고쳤다면 이 대조도 함께 고치세요.");

            // ── 부재 쪽: 주석을 걷어내면 한 건도 남지 않는다.
            List<string> inCode = NeedleHits(target.Stripped, ForbiddenRegistryWriteApis);
            Debug.Log($"{LogPrefix} {Rel(target.Path)} — 원본 주석 히트 {inRaw.Count}종 / 코드 히트 {inCode.Count}종");

            Assert.IsEmpty(inCode,
                $"{LogPrefix} 레지스트리 <b>쓰기</b> 계열 API가 코드에 등장했습니다:\n  " +
                string.Join("\n  ", inCode) + "\n" +
                "CLAUDE.md 절대 불변 원칙 3(유저 자산 불변)은 레지스트리에도 걸립니다 — 게임 바 등록 " +
                "목록(HKCU\\System\\GameConfigStore)은 OS와 사용자의 소유물이고 이 앱은 <b>인용만</b> " +
                "합니다. 승인된 예외는 작업표시줄 자동 숨김 비트 1개뿐이고 그건 레지스트리가 아니라 " +
                "SHAppBarMessage입니다(docs/TASKBAR_REVEAL.md).");
        }

        // ====================================================================
        // 6. ★ 본론 (3) — 권한 두 개. 둘 다 집합 등호로 쓴다
        // ====================================================================

        [Test]
        public void 레지스트리를_여는_권한은_KEY_READ_하나뿐이다()
        {
            Target target = FindProbeSource();
            SortedSet<string> rights = IdentifiersStartingWith(target.Stripped, RegistryRightPrefix);

            Debug.Log($"{LogPrefix} 레지스트리 권한 식별자: {string.Join(", ", rights)}");

            // 존재 쪽 — 하나도 없으면 «권한을 안 쓴다»가 아니라 «스캐너가 눈이 멀었다»일 수 있다.
            Assert.IsNotEmpty(rights,
                $"{LogPrefix} '{RegistryRightPrefix}'로 시작하는 식별자를 하나도 찾지 못했습니다. " +
                "레지스트리를 여는 코드가 통째로 사라졌거나 상수 이름이 바뀌었습니다 — 어느 쪽이든 " +
                "아래 등호는 지금 아무것도 재지 않습니다.");

            var unexpected = new List<string>();
            foreach (string right in rights)
                if (!string.Equals(right, AllowedRegistryAccessRight, StringComparison.Ordinal))
                    unexpected.Add(right);

            Assert.IsEmpty(unexpected,
                $"{LogPrefix} {AllowedRegistryAccessRight} 이외의 레지스트리 권한이 등장했습니다: " +
                string.Join(", ", unexpected) + "\n" +
                "쓰기 함수를 부르지 않더라도 <b>여는 순간</b> 원칙 3의 보장이 «코드를 전부 읽어야 아는 " +
                "것»으로 약해집니다. 클래스 문서의 «레지스트리는 KEY_READ로만 연다»가 곧 이 등호입니다.");

            Assert.IsTrue(rights.Contains(AllowedRegistryAccessRight),
                $"{LogPrefix} '{AllowedRegistryAccessRight}'가 코드에 없습니다. 위 «그 밖에 없다»는 " +
                "이 상태에서 공허합니다(있어야 할 것이 없는데 없는 것만 확인한 꼴입니다).");
        }

        [Test]
        public void 프로세스_핸들_권한은_QUERY_LIMITED_INFORMATION_하나뿐이다()
        {
            Target target = FindProbeSource();
            SortedSet<string> rights = IdentifiersStartingWith(target.Stripped, ProcessRightPrefix);

            Debug.Log($"{LogPrefix} 프로세스 권한 식별자: {string.Join(", ", rights)}");

            Assert.IsTrue(rights.Contains(AllowedProcessAccessRight),
                $"{LogPrefix} '{AllowedProcessAccessRight}'가 코드에 없습니다. 클래스 문서는 " +
                "«프로세스 핸들은 PROCESS_QUERY_LIMITED_INFORMATION만 요청한다»고 단언합니다 — " +
                "그 이름이 사라졌다면 문서가 낡았거나 권한이 다른 방식(숫자 리터럴 등)으로 넘어가고 " +
                "있습니다. 후자라면 이 감사는 그 순간부터 <b>아무것도 못 봅니다</b>.");

            var unexpected = new List<string>();
            foreach (string right in rights)
                if (!string.Equals(right, AllowedProcessAccessRight, StringComparison.Ordinal))
                    unexpected.Add(right);

            Assert.IsEmpty(unexpected,
                $"{LogPrefix} 최소 권한보다 넓은 프로세스 권한이 등장했습니다: " +
                string.Join(", ", unexpected) + "\n" +
                "PROCESS_QUERY_LIMITED_INFORMATION은 메모리 읽기/쓰기·스레드 조작·종료 권한이 " +
                "<b>아예 없는</b> 최소 조회 권한이고 관리자 승격도 필요 없습니다. 여기가 넓어지는 " +
                "순간 «전경 프로세스가 게임인가»라는 질문에 필요한 것보다 많은 힘을 갖게 됩니다.");
        }

        // ====================================================================
        // 7. ★ 본론 (4) — 지금까지 아무도 재지 않던 문장
        // ====================================================================

        [Test]
        public void 타_프로세스와_타_창을_건드리는_API가_하나도_없다()
        {
            Target target = FindProbeSource();
            List<string> hits = NeedleHits(target.Stripped, ForbiddenReachIntoOthersApis);

            Assert.IsEmpty(hits,
                $"{LogPrefix} 타 프로세스·타 창에 손대는 API가 등장했습니다:\n  " +
                string.Join("\n  ", hits) + "\n" +
                "클래스 문서 세 번째 문장(«타 프로세스에 어떤 메시지도 보내지 않고, 어떤 창도 건드리지 " +
                "않는다»)이 깨졌습니다. 이 파일의 역할은 «전경 프로세스가 게임인가»라는 <b>사실 조회</b> " +
                "하나뿐입니다(판정 규칙조차 여기 없고 플랫폼 중립 WindowsGameExecutablePolicy가 갖고 " +
                "있습니다). 창을 만져야 하는 일이라면 그건 이 클래스의 일이 아닙니다.");
        }

        // ====================================================================
        // 8. 양성 대조 — 「0건」은 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        /// <summary>
        /// 스캐너가 <b>실제 파일에서 실제로</b> 무엇을 찾아내는지 보인다. 여기가 초록이어야
        /// 위의 모든 «0건»이 «없다»를 뜻한다.
        /// </summary>
        [Test]
        public void 양성대조_스캐너가_실제_파일에서_조회_API와_권한을_찾아낸다()
        {
            Target target = FindProbeSource();

            SortedSet<string> externs = ExternMethodNames(target.Stripped);
            Assert.GreaterOrEqual(externs.Count, 2,
                $"{LogPrefix} extern 파서가 {externs.Count}개만 찾았습니다 — 파서가 첫 매치에서 " +
                "포기했거나 표기가 바뀌었습니다.");

            Assert.IsTrue(IdentifiersStartingWith(target.Stripped, RegistryRightPrefix).Count > 0,
                $"{LogPrefix} 레지스트리 권한 식별자를 못 찾았습니다.");
            Assert.IsTrue(IdentifiersStartingWith(target.Stripped, ProcessRightPrefix).Count > 0,
                $"{LogPrefix} 프로세스 권한 식별자를 못 찾았습니다.");

            // ★ 원본에는 있고 코드에는 없다 — 스트리퍼가 살아 있다는 증명.
            Assert.Greater(target.Raw.Length, target.Stripped.Length,
                $"{LogPrefix} 주석 제거 전후 길이가 같습니다 — 스트리퍼가 아무것도 안 걷어냈습니다. " +
                "이 파일은 클래스 문서만 30줄이 넘습니다.");
        }

        [Test]
        public void 니들표는_비어_있지_않고_전부_사유를_단다()
        {
            // 거짓 통과 #5 — 명부가 비면 foreach가 아무것도 안 재고 초록이 된다.
            Assert.IsNotEmpty(AllowedNativeImports, $"{LogPrefix} 허용 API 표가 비었습니다.");
            Assert.IsNotEmpty(ForbiddenRegistryWriteApis, $"{LogPrefix} 레지스트리 쓰기 니들표가 비었습니다.");
            Assert.IsNotEmpty(ForbiddenReachIntoOthersApis, $"{LogPrefix} 침범 니들표가 비었습니다.");

            foreach ((string needle, string why) in ForbiddenRegistryWriteApis)
                Assert.IsNotEmpty(why, $"{LogPrefix} '{needle}'의 사유가 비었습니다.");
            foreach ((string needle, string why) in ForbiddenReachIntoOthersApis)
                Assert.IsNotEmpty(why, $"{LogPrefix} '{needle}'의 사유가 비었습니다.");
        }

        // ====================================================================
        // 9. 네거티브 컨트롤 — 가짜 소스를 <b>같은 함수</b>에 흘린다
        // ====================================================================

        /// <summary>실제로 있었던 위반 형태(P/Invoke 선언 한 줄)를 주면 <b>두 다리 모두</b> 잡는가.</summary>
        [Test]
        public void NegativeControl_레지스트리_쓰기_선언이_생기면_집합등호와_니들이_둘_다_잡는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    [DllImport(\"advapi32.dll\", CharSet = CharSet.Unicode)]\n" +
                "    private static extern int RegSetValueExW(IntPtr hKey, string name);\n" +
                "    [DllImport(\"advapi32.dll\")]\n" +
                "    private static extern int RegCloseKey(IntPtr hKey);\n" +
                "}\n";

            string stripped = EntitlementAuditSource.StripComments(fake);

            SortedSet<string> externs = ExternMethodNames(stripped);
            Assert.IsTrue(externs.Contains("RegSetValueExW"),
                $"{LogPrefix} extern 파서가 쓰기 선언을 못 봤습니다 — 집합 등호가 이 형태를 놓칩니다.");
            Assert.IsTrue(externs.Contains("RegCloseKey"),
                $"{LogPrefix} extern 파서가 두 번째 선언을 못 봤습니다 — 첫 매치에서 멈추고 있습니다.");

            Assert.IsNotEmpty(NeedleHits(stripped, ForbiddenRegistryWriteApis),
                $"{LogPrefix} 니들 스캔이 RegSetValueExW를 놓쳤습니다.");
        }

        /// <summary>
        /// ★★ <b>이 저장소를 즉시 거짓 빨강으로 만들 수 있었던 함정 (1).</b>
        /// 대상 파일은 «쓰기 계열은 선언조차 하지 않는다»는 <b>정직한 금지 주석</b>에 그 이름들을
        /// 그대로 적어 두었다. 주석을 세면 이 감사는 <b>첫날부터 빨간 채로</b> 방치되고,
        /// 다음 사람은 사실을 지워 초록을 만든다.
        /// </summary>
        [Test]
        public void NegativeControl_주석에_적힌_금지_이름은_위반이_아니다()
        {
            const string fake =
                "/// <summary>레지스트리는 KEY_READ로만 연다. 쓰기 계열(RegSetValueEx / RegCreateKeyEx /\n" +
                "/// RegDeleteKey / RegDeleteValue)은 선언조차 하지 않는다.</summary>\n" +
                "internal sealed class Fake\n" +
                "{\n" +
                "    // SendMessage 도 쓰지 않는다 — 남의 창을 건드리지 않는다.\n" +
                "    /* RegSetKeyValue 도 마찬가지다. */\n" +
                "    private const uint KEY_READ = 0x20019;\n" +
                "}\n";

            string stripped = EntitlementAuditSource.StripComments(fake);

            Assert.IsEmpty(NeedleHits(stripped, ForbiddenRegistryWriteApis),
                $"{LogPrefix} 주석 속 금지 이름을 위반으로 셌습니다. 그러면 <b>정직하게 금지를 적어 둔</b> " +
                "그 문장 때문에 감사가 빨개지고, 그런 감사는 몇 번 만에 꺼집니다.");
            Assert.IsEmpty(NeedleHits(stripped, ForbiddenReachIntoOthersApis),
                $"{LogPrefix} 주석 속 SendMessage 언급을 위반으로 셌습니다.");

            // 그리고 «주석에는 실재한다»는 대조 쪽은 반대로 반드시 잡혀야 한다.
            Assert.IsNotEmpty(NeedleHits(fake, ForbiddenRegistryWriteApis),
                $"{LogPrefix} 원본에서조차 금지 이름을 못 찾았습니다 — 니들이 낡았습니다. " +
                "이 상태에서는 «원본에는 있고 코드에는 없다»는 대조가 성립하지 않습니다.");
        }

        /// <summary>
        /// ★★ <b>함정 (2).</b> <c>HKEY_CURRENT_USER</c>는 레지스트리 <b>루트 핸들</b>이지
        /// 권한 플래그가 아니다. 부분 문자열로 <c>KEY_</c>를 찾으면 그것이 걸려
        /// «KEY_READ 이외의 권한이 있다»는 <b>거짓 빨강</b>이 난다 — 대상 파일 48행에 실제로 있다.
        /// </summary>
        [Test]
        public void NegativeControl_HKEY_CURRENT_USER는_권한_플래그가_아니다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));\n" +
                "    private const uint KEY_READ = 0x20019;\n" +
                "}\n";

            SortedSet<string> rights =
                IdentifiersStartingWith(EntitlementAuditSource.StripComments(fake), RegistryRightPrefix);

            Assert.IsTrue(rights.Contains(AllowedRegistryAccessRight),
                $"{LogPrefix} KEY_READ를 못 찾았습니다 — 스캐너가 눈이 멀었습니다.");
            Assert.AreEqual(1, rights.Count,
                $"{LogPrefix} 권한 식별자를 {rights.Count}개로 셌습니다({string.Join(", ", rights)}). " +
                "HKEY_CURRENT_USER를 권한 플래그로 오인하면 이 감사는 <b>첫 실행부터</b> 거짓 빨강이 " +
                "되고, 그러면 아무도 이 감사를 켜 두지 않습니다.");
        }

        /// <summary>넓은 권한이 들어오면 <b>두 등호가 모두</b> 잡는가.</summary>
        [Test]
        public void NegativeControl_넓은_권한이_들어오면_등호가_반드시_깨진다()
        {
            const string wideRegistry =
                "internal sealed class Fake { private const uint KEY_ALL_ACCESS = 0xF003F; }\n";
            SortedSet<string> registryRights =
                IdentifiersStartingWith(EntitlementAuditSource.StripComments(wideRegistry), RegistryRightPrefix);
            Assert.IsTrue(registryRights.Contains("KEY_ALL_ACCESS"),
                $"{LogPrefix} KEY_ALL_ACCESS를 못 잡았습니다.");
            Assert.IsFalse(registryRights.Contains(AllowedRegistryAccessRight),
                $"{LogPrefix} 없는 KEY_READ를 있다고 셌습니다.");

            const string wideProcess =
                "internal sealed class Fake\n" +
                "{\n" +
                "    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;\n" +
                "    private const uint PROCESS_VM_READ = 0x0010;\n" +
                "    private static void M() { OpenProcess(PROCESS_ALL_ACCESS | PROCESS_VM_READ, false, 1u); }\n" +
                "}\n";
            SortedSet<string> processRights =
                IdentifiersStartingWith(EntitlementAuditSource.StripComments(wideProcess), ProcessRightPrefix);
            Assert.AreEqual(2, processRights.Count,
                $"{LogPrefix} 넓은 프로세스 권한을 {processRights.Count}개로 셌습니다(기대 2) — " +
                $"{string.Join(", ", processRights)}");
            Assert.IsFalse(processRights.Contains(AllowedProcessAccessRight),
                $"{LogPrefix} 없는 최소 권한을 있다고 셌습니다.");
        }

        /// <summary>세 번째 문장(메시지·창)의 탐지 방향을 증명한다.</summary>
        [Test]
        public void NegativeControl_남의_창을_건드리는_선언을_잡는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    [DllImport(\"user32.dll\")]\n" +
                "    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y,\n" +
                "        int cx, int cy, uint flags);\n" +
                "    private static void Nudge(IntPtr h) { SendMessageW(h, 0x0112, IntPtr.Zero, IntPtr.Zero); }\n" +
                "}\n";

            string stripped = EntitlementAuditSource.StripComments(fake);
            List<string> hits = NeedleHits(stripped, ForbiddenReachIntoOthersApis);

            Assert.GreaterOrEqual(hits.Count, 2,
                $"{LogPrefix} 창 조작 니들이 {hits.Count}종만 잡았습니다(기대 2종 이상: SetWindowPos · " +
                $"SendMessage). 잡힌 것: {string.Join(" / ", hits)}");
        }

        /// <summary>
        /// extern 파서가 <b>선언과 호출</b>을 구분하는지. 구분 못 하면 호출문의 이름까지 «네이티브
        /// 임포트»로 세어 집합 등호가 거짓 빨강을 낸다(대상 파일에는 <c>RegCloseKey(childKey)</c>
        /// 같은 호출이 여러 번 있다).
        /// </summary>
        [Test]
        public void NegativeControl_호출문은_네이티브_선언으로_세지_않는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    [DllImport(\"advapi32.dll\")]\n" +
                "    private static extern int RegCloseKey(IntPtr hKey);\n" +
                "    private static void M(IntPtr k)\n" +
                "    {\n" +
                "        HelperThatIsNotNative(k);\n" +
                "        if (k != IntPtr.Zero) RegCloseKey(k);\n" +
                "    }\n" +
                "}\n";

            SortedSet<string> externs = ExternMethodNames(EntitlementAuditSource.StripComments(fake));

            Assert.IsTrue(externs.Contains("RegCloseKey"),
                $"{LogPrefix} 선언을 못 찾았습니다.");
            Assert.AreEqual(1, externs.Count,
                $"{LogPrefix} extern이 아닌 이름까지 셌습니다({string.Join(", ", externs)}). " +
                "그러면 평범한 헬퍼 호출 하나로 집합 등호가 깨져 거짓 빨강이 납니다.");
        }
    }
}
