using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// E-9 #3 — <b>돈 낸 사람을 잠그지 않는다</b>: C층 소유 판정의 3상태 계약 (security, 2026-09-02)
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-1(3상태) · §E-2(회수 금지) · §E-3(재시도).
    ///
    /// <para><b>이 감사는 무단 사용을 막는 장치가 아니다. 정반대다.</b>
    /// 스팀 클라이언트가 안 떠 있으면 <c>SteamAPI_Init</c>이 실패하고
    /// (Steamworks <c>steam_api.h</c>: <i>"A running Steam client is required"</i>),
    /// 그때 소박한 구현 <c>bool owned = SteamApps.BIsDlcInstalled(id);</c>는 <b>false</b>로 읽힌다.
    /// 즉 <b>정상 결제한 사용자 전원이 잠긴다.</b> 이 앱은 부팅 자동 실행 + 종일 상주가 정상 사용
    /// 형태라(§E-3), 로그인 직후 스팀이 아직 안 뜬 구간이 <b>매일</b> 발생한다.</para>
    ///
    /// <para>정의서 원칙: <b>정당한 유저를 한 명이라도 잠그는 조치는 무단 사용 열 건보다 비싸다.</b>
    /// 이 앱은 동료지 금고가 아니다.</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-03 정정 — <b>축 A가 발동했다. 이 테스트는 더 이상 보류가 아니다</b>
    /// ============================================================================
    /// <c>coder-systems</c> 의 팩 통로 라운드가 <c>Core/PackEntitlement.cs</c> 에
    /// <c>PackEntitlementState { Owned, NotOwned, Unknown }</c> 를 넣었다.
    /// <b>아무도 스위치를 켜지 않았다</b> — 아래 보류 분기의 조건이 거짓이 되어 실검사로 넘어갔다.
    /// 설계대로 동작한 것이고, 그 사실을 여기 기록한다.
    ///
    /// <para>★ 같은 라운드에 <b>V2·V3의 사거리를 C층 파일로 좁혔다</b>(본문 주석에 실측과 근거).
    /// 축 B(<c>스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다</c>)는 <b>그대로 초록</b>이었다 —
    /// 스팀 SDK는 그때 여전히 0줄이고, <c>NullPackEntitlementSource</c> 는 모든 팩에 <c>Unknown</c> 을 답했다.
    ///
    /// <para>★★ 2026-09-05 — 결재-1(리더)로 <c>SteamPackEntitlementSource.cs</c> 1개 파일이
    /// 승인됐고 축 B의 이름과 판정이 그 예외를 반영하도록 개작됐다(위 절 참고).
    /// <c>OfflineFirstNetworkAuditTests</c> 에도 같은 diff에서 화이트리스트 1건이 등록됐다.</para>
    ///
    /// <para>★★★ <b>2026-09-28 사용자 결정(DLC 폐지) — 그 예외가 전부 닫혔다.</b> 승인 근거
    /// 「6팩을 팔 수 없다 = 출시 자체가 없다」가 소멸했고, 같은 커밋에서
    /// ⑴ 어댑터 파일 삭제 → ⑵ <c>OfflineFirstNetworkAuditTests</c> 전송 계열 명부를 빈 배열로 →
    /// ⑶ <b>축 B의 승인 표를 비워 원형(어디서든 하나라도 나오면 빨강)으로 복원</b> →
    /// ⑷ <c>SteamEntitlementAdapterAuditTests</c>(닫힌 세계 감사) 삭제 순서로 처리했다.
    /// <b>순서가 본체다</b> — ⑷를 ⑶보다 먼저 하면 그 사이에
    /// <c>SteamRemoteStorage</c>·<c>SteamInventory</c>·<c>SteamUser</c>를 지키는 것이 하나도 없다.</para>
    ///
    /// <para>★ <b>C층 자체는 여전히 코드에 있다</b>(<c>Core/PackEntitlement.cs</c>의
    /// <c>PackEntitlementState</c>·<c>IPackEntitlementSource</c>·<c>PackEntitlements</c>). 없어진 것은
    /// <b>그 출처를 채우는 스토어 어댑터</b>이고, 그래서 지금 모든 팩 조회는 <c>Unknown</c>이다.
    /// 즉 축 A(3상태 계약)는 계속 실검사로 돌고, 축 B가 다시 「0건」을 잠근다.</para>
    ///
    /// <para><b>남은 Ignore 분기는 지우지 않는다.</b> C층 타입이 사라지면(리팩터링·롤백) 이 파일은
    /// 다시 「검사할 대상이 없음」이 되고, 그때 조용히 초록이 되는 대신 <b>건너뜀으로 러너에 보여야</b> 한다.
    /// <c>TestClaimExpiryAuditTests</c> 의 명부 항목도 같은 이유로 유효하다.</para>
    ///
    /// ============================================================================
    /// (아래는 2026-09-02 원문 — 왜 이 장치가 이렇게 생겼는지가 필요하다)
    /// ============================================================================
    /// 2026-09-02 당시 C층(유료 권한) 코드는 <b>0줄</b>이었다. 검사할 대상이 없었다.
    /// CLAUDE.md 관례: <i>"아직 못 고친 갭은 <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>(사유 포함)로
    /// 남겨 러너에 '건너뜀'으로 계속 보이게 한다 — 잊히지 않게."</i>
    ///
    /// <para><b>역방향 장치 두 개를 서로 독립인 축으로 걸어 둔다</b>(TEAM.md가 기록한 열 번째 거짓 통과
    /// 형태 — "생성기와 검사기가 같은 잘못된 흉내를 공유해 서로를 확인해 주지 못했다"):</para>
    /// <list type="number">
    ///  <item><b>축 A — 타입 이름.</b> 프로덕션에 <c>Entitlement</c>/<c>Ownership</c>/<c>License</c>가
    ///    들어간 타입 선언이 생기면 아래 보류가 <b>스스로 풀려</b> 실검사로 돈다.</item>
    ///  <item><b>축 B — 스토어 SDK 표면.</b>
    ///    <see cref="스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다"/>가 <b>항상 실행</b>되며
    ///    <c>Steamworks</c>/<c>BIsDlcInstalled</c> 같은 식별자가 <b>승인된 어댑터 파일 밖</b>에
    ///    나타나는 순간 <b>빨개진다.</b> 축 A가 눈이 멀어도(예: 타입 이름을 <c>PackAccess</c>로
    ///    지어 버리면) 축 B가 대신 알린다.
    ///    ★ 2026-09-05 결재-1(리더)로 <c>SteamPackEntitlementSource.cs</c> 1개 파일의 승인된
    ///    심볼만 예외가 됐다 — 그 파일 안에서도 승인 안 된 멤버(RestartAppIfNecessary 등)나
    ///    그 파일 밖의 사용은 여전히 이 경보를 울린다(더 엄밀한 재검증은
    ///    ~~<c>SteamEntitlementAdapterAuditTests</c>~~).
    ///    ★★ <b>as-of 2026-09-05~2026-09-28.</b> 2026-09-28 사용자 결정(DLC 폐지)으로 그 예외 파일과
    ///    라인 단위 재검증 감사가 <b>둘 다 삭제됐다</b>. 지금 승인된 자리는 <b>0곳</b>이고, 이 경보는
    ///    「프로덕션 어디에든 하나라도 나오면 빨강」 원형이다 — 재검증 감사가 없어졌으므로
    ///    <b>이 경보가 유일한 문지기</b>다.</item>
    /// </list>
    ///
    /// <para>★ 오늘 <b>보류 분기가 실제로 도는 코드인지</b>는 아래 네거티브 컨트롤들이 증명한다 —
    /// 가짜 C층 소스를 같은 판정 함수에 흘려 위반을 잡는지/정상은 통과시키는지 매 실행 확인한다.
    /// 그렇게 하지 않으면 이 파일은 "언젠가 켜질 코드"라는 이름의 <b>한 번도 실행되지 않은 코드</b>가 된다.</para>
    ///
    /// ============================================================================
    /// ★ 다른 감사와의 충돌을 미리 적어 둔다 (C층 배선 라운드가 반드시 만난다)
    /// ============================================================================
    /// <c>OfflineFirstNetworkAuditTests</c>는 프로덕션 소스에서 <c>Steamworks</c>를 <b>금지 니들</b>로
    /// 잡고 있다(네트워크 0 원칙). 즉 C층을 배선하는 라운드는 그 감사도 함께 빨갛게 만든다.
    /// <b>그 실패는 버그가 아니라 설계된 관문이다</b> — 그 라운드는 화이트리스트 항목을 근거와 함께
    /// 등록해야 하고, 그 과정에서 "스팀 SDK가 네트워크를 여는가"를 사람이 한 번 판단하게 된다.
    /// 여기서 미리 적어 두는 이유는, 그날 그 빨강을 <b>귀찮은 오탐으로 오해해 니들만 지우는</b> 일을
    /// 막기 위해서다.
    ///
    /// <para>이 파일은 리플렉션 0줄 · 정규식 0줄이다(활성 빌드 타깃 사각지대 · 한글 낱말 경계 함정 회피).</para>
    /// </summary>
    public sealed class EntitlementFailOpenAuditTests
    {
        private const string LogPrefix = "[C층-페일오픈]";

        /// <summary>축 A — C층 <b>정책 타입</b>을 알아보는 이름 조각.</summary>
        private static readonly string[] PolicyTypeFragments = { "Entitlement", "Ownership", "License" };

        /// <summary>
        /// 축 B — C층 <b>배관</b>을 알아보는 스토어 SDK 식별자.
        /// 축 A와 <b>일부러</b> 겹치지 않게 골랐다. 두 축이 같은 신호를 보면 함께 눈이 먼다.
        /// </summary>
        private static readonly string[] StoreSdkIdentifiers =
        {
            "Steamworks",        // Steamworks.NET 네임스페이스
            "SteamApps",         // ISteamApps
            "BIsDlcInstalled",   // Valve: "not intended for granting in-game items"
            "BIsSubscribedApp",
            "SteamAPI",          // SteamAPI_Init / SteamAPI_RestartAppIfNecessary
            "StoreContext",      // Windows.Services.Store (MS 스토어 애드온)
            "StoreProduct",
            "StoreAppLicense",
        };

        /// <summary>3상태 중 <b>붕괴하면 안 되는</b> 상태 이름.</summary>
        private const string UnknownStateName = "Unknown";

        /// <summary><c>bool</c> 반환을 금지할 함수 이름 조각(§E-1-a).</summary>
        private static readonly string[] BoolBanFragments = { "Entitle", "Owned", "Dlc", "Ownership" };

        // ====================================================================
        // 탐지 — 두 축, 서로 독립
        // ====================================================================

        private struct Surface
        {
            public string File;
            public string Detail;
        }

        /// <summary>축 A: C층 정책 타입 선언.</summary>
        private static List<Surface> DetectPolicyTypes(IEnumerable<(string File, string Stripped)> sources)
        {
            var found = new List<Surface>();
            foreach ((string file, string stripped) in sources)
            {
                foreach (string fragment in PolicyTypeFragments)
                {
                    foreach (string name in EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, fragment))
                    {
                        found.Add(new Surface { File = file, Detail = name });
                    }
                }
            }
            return found;
        }

        /// <summary>축 B: 스토어 SDK 식별자.</summary>
        private static List<Surface> DetectStoreSdk(IEnumerable<(string File, string Stripped)> sources)
        {
            var found = new List<Surface>();
            foreach ((string file, string stripped) in sources)
            {
                foreach (string id in StoreSdkIdentifiers)
                {
                    if (!EntitlementAuditSource.ContainsIdentifier(stripped, id)) continue;
                    found.Add(new Surface { File = file, Detail = id });
                }
            }
            return found;
        }

        // ====================================================================
        // 판정 — 순수 함수. 네거티브 컨트롤이 가짜 C층 소스를 여기에 흘린다.
        // ====================================================================

        /// <summary>
        /// C층 소스가 §E-1 3상태 계약을 지키는가. 어기는 항목을 사람이 읽을 문장으로 돌려준다.
        /// <para>정적으로 <b>잴 수 없는 것</b>은 여기 없다 — §E-2-a(NotOwned와 Unknown의 UI 문구가
        /// 서로 달라야 한다)는 문자열 내용 판정이라 소스 스캔의 사거리 밖이다. 그건 C층 배선 라운드의
        /// 기능 테스트가 맡는다. <b>못 재는 것을 재는 척하지 않는다.</b></para>
        /// </summary>
        private static List<string> Violations(IEnumerable<(string File, string Stripped)> sources)
        {
            var problems = new List<string>();
            var stateEnums = new List<string>();
            var files = new List<(string File, string Stripped)>(sources);

            // ---- V1: 3상태 열거형이 존재하고 Unknown을 가진다 (§E-1) ----
            foreach ((string file, string stripped) in files)
            {
                foreach (string fragment in PolicyTypeFragments)
                {
                    foreach (string name in EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, fragment))
                    {
                        // 상태는 열거형이어야 한다. 같은 이름 조각을 가진 <b>클래스</b>
                        // (예: PackEntitlements 서비스 타입)는 3상태 계약의 대상이 아니다.
                        if (!DeclaresEnum(stripped, name)) continue;

                        string body = EntitlementAuditSource.TypeBodyOrNull(stripped, name);
                        if (body == null) continue;
                        stateEnums.Add(name);

                        if (!EntitlementAuditSource.ContainsIdentifier(body, UnknownStateName))
                        {
                            problems.Add($"{file}: {name}에 '{UnknownStateName}' 상태가 없습니다.\n" +
                                "      §E-1: 상태는 2개가 아니라 3개다. Unknown이 없으면 조회 실패가 " +
                                "NotOwned로 붕괴하고, 그 붕괴가 곧 '돈 낸 사람 잠그기'입니다.");
                        }
                    }
                }
            }

            // ================================================================
            // ★★ 2026-09-03 coder-systems — V2·V3의 <b>사거리</b>를 C층 파일로 좁혔다
            // ================================================================
            // 이 파일의 원 작성자(security)가 실패 메시지에 적어 둔 절차 그대로다:
            //   "여전히 '건너뜀'이면 PolicyTypeFragments에 실제 타입 이름 조각을 추가하세요."
            // C층 배선 라운드가 실제로 그 자리에 도착했고, 도착해 보니 <b>사거리</b> 쪽이 문제였다.
            //
            // ★ 실측(2026-09-03, 프로덕션 전량): V2를 트리 전체에 걸면 <b>A·B층</b> 선언 8건이 걸린다 —
            //   EquipmentModel.IsItemOwned / ItemCatalogEntry.IsOwned /
            //   LocalClickCaptureGate.IsOwnedBy / IsLocalClickCaptureOwnedBy ×4 /
            //   FocusWatchDirector.ReleaseOwnedLock.
            //   이들은 <b>레벨 해금</b>과 <b>클릭 캡처 소유권</b>이지 <b>유료 권한</b>이 아니다.
            //   §E-1-a가 금지하는 것은 «엔타이틀먼트 조회 결과를 bool로 반환하는 API»이고,
            //   그 조회는 스팀이 안 뜨면 실패한다는 성질 때문에 3상태가 필요한 것이다.
            //   레벨 해금에는 그 성질이 없다(로컬 값이라 실패하지 않는다).
            //
            // ★ 좁히지 않으면 무슨 일이 일어나는가: C층이 들어오는 <b>바로 그 라운드</b>에
            //   무관한 빨강 8건이 함께 뜨고, 그때 사람이 하는 일은 «귀찮은 오탐이니 니들을 지우자»다.
            //   이 파일이 스스로 경고한 그 형태다(클래스 문서의 OfflineFirstNetworkAuditTests 문단).
            //   방치되거나 꺼진 경보는 없는 경보다.
            //
            // ★ 무엇을 잃는가 — 정직하게: C층 <b>상태 타입을 한 번도 언급하지 않는</b> 파일에 숨은
            //   bool 소유 판정은 이제 안 보인다. 그러나 그런 함수는 C층 상태를 볼 수 없으므로
            //   §E-1이 막으려는 «Unknown의 붕괴»를 저지를 수단 자체가 없다.
            //   그리고 <b>V1은 여전히 트리 전체</b>를 본다 — 3상태 열거형이 어디서 생기든 잡힌다.
            //   아래 NegativeControl_사거리는_같은_파일에_갇히지_않는다 가 «선언 파일과 위반 파일이
            //   달라도 잡힌다»를 매 실행 증명한다.
            var cLayer = new List<(string File, string Stripped)>();
            foreach ((string file, string stripped) in files)
            {
                for (int i = 0; i < stateEnums.Count; i++)
                {
                    if (!EntitlementAuditSource.ContainsIdentifier(stripped, stateEnums[i])) continue;
                    cLayer.Add((file, stripped));
                    break;
                }
            }

            // ---- V2: bool 반환 소유 판정 금지 (§E-1-a) ----
            foreach ((string file, string stripped) in cLayer)
            {
                int lineNo = 0;
                foreach (string raw in stripped.Replace("\r\n", "\n").Split('\n'))
                {
                    lineNo++;
                    string line = raw.Trim();
                    if (line.IndexOf('(') < 0) continue;
                    if (line.IndexOf(" bool ", StringComparison.Ordinal) < 0) continue;
                    if (!line.StartsWith("public ", StringComparison.Ordinal)
                        && !line.StartsWith("internal ", StringComparison.Ordinal)
                        && !line.StartsWith("private ", StringComparison.Ordinal)
                        && !line.StartsWith("protected ", StringComparison.Ordinal)) continue;

                    int paren = line.IndexOf('(');
                    int start = paren;
                    while (start > 0 && IsIdentifierChar(line[start - 1])) start--;
                    if (start == paren) continue;
                    string name = line.Substring(start, paren - start);

                    foreach (string fragment in BoolBanFragments)
                    {
                        if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        problems.Add($"{file}:{lineNo}: bool을 돌려주는 소유 판정 '{name}'.\n" +
                            "      §E-1-a: 엔타이틀먼트 조회 결과를 bool로 반환하는 API를 만들지 않는다. " +
                            "bool로 두면 Unknown이 NotOwned로 붕괴하고, 스팀이 아직 안 뜬 아침마다 " +
                            "정상 구매자가 잠깁니다.");
                        break;
                    }
                }
            }

            // ---- V3: 세 갈래를 전부 명시한다 — switch의 default: 금지 (§E-1-b) ----
            foreach ((string file, string stripped) in cLayer)
            {
                int from = 0;
                while (true)
                {
                    int at = stripped.IndexOf(UnknownStateName, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + 1;

                    int before = at - 1;
                    int after = at + UnknownStateName.Length;
                    if (before >= 0 && IsIdentifierChar(stripped[before])) continue;
                    if (after < stripped.Length && IsIdentifierChar(stripped[after])) continue;

                    int wStart = Math.Max(0, at - 700);
                    int wEnd = Math.Min(stripped.Length, after + 700);
                    string window = stripped.Substring(wStart, wEnd - wStart);
                    if (window.IndexOf("switch", StringComparison.Ordinal) < 0) continue;
                    if (window.IndexOf("default:", StringComparison.Ordinal) < 0) continue;

                    problems.Add($"{file}:{EntitlementAuditSource.LineNumberAt(stripped, at)}: " +
                        "상태 switch에 default:가 있습니다.\n" +
                        "      §E-1-b: default를 두지 말고 세 갈래를 전부 명시한다. default는 " +
                        "새 상태가 생겼을 때 <b>조용히</b> 한쪽으로 흡수해 버립니다 — " +
                        "이 저장소가 반복해 당한 '조용한 실패'와 같은 계열입니다.");
                    break;
                }
            }

            if (stateEnums.Count == 0)
            {
                problems.Add("C층 표면은 있는데 3상태 열거형을 하나도 찾지 못했습니다.\n" +
                    "      §E-1: EntitlementState { Owned, NotOwned, Unknown } 형태가 이 계약의 뿌리입니다.");
            }
            return problems;
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary><c>enum &lt;name&gt;</c> 선언이 있는가(낱말 경계 확인 — 정규식 없음).</summary>
        private static bool DeclaresEnum(string stripped, string name)
        {
            string needle = "enum " + name;
            int from = 0;
            while (true)
            {
                int at = stripped.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) return false;
                from = at + 1;

                int before = at - 1;
                int after = at + needle.Length;
                if (before >= 0 && IsIdentifierChar(stripped[before])) continue;
                if (after < stripped.Length && IsIdentifierChar(stripped[after])) continue;
                return true;
            }
        }

        private static List<(string File, string Stripped)> ProductionSources()
        {
            var list = new List<(string, string)>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
                list.Add((Path.GetFileName(path), EntitlementAuditSource.StripComments(File.ReadAllText(path))));
            return list;
        }

        // ====================================================================
        // 1. 본론 — 오늘은 보류, C층이 들어오는 날 스스로 실검사로 돈다
        // ====================================================================

        [Test]
        public void C층_소유판정은_Unknown을_NotOwned로_붕괴시키지_않는다()
        {
            List<(string File, string Stripped)> sources = ProductionSources();
            Assert.GreaterOrEqual(sources.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {sources.Count}개밖에 읽지 못했습니다 — " +
                "이 상태의 '보류'는 판단이 아니라 사고입니다.");

            List<Surface> policy = DetectPolicyTypes(sources);
            if (policy.Count == 0)
            {
                Assert.Ignore(
                    $"{LogPrefix} 유료 권한(C층) 코드가 아직 0줄이라 검사할 대상이 없습니다 " +
                    "(2026-09-02 security, ENTITLEMENT_CONTRACT §E-9 #3).\n" +
                    "★ 무엇이 이 보류를 되살리는가 — 두 축이 걸려 있습니다:\n" +
                    "  ① 축 A(자동): 프로덕션에 Entitlement/Ownership/License가 들어간 타입 선언이 " +
                    "생기는 순간, 이 테스트는 보류를 지나 실검사(§E-1 3상태 · §E-1-a bool 금지 · " +
                    "§E-1-b default 금지)로 돕니다. 아무도 켤 필요가 없습니다.\n" +
                    "  ② 축 B(동반): 같은 파일의 [스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다]가 " +
                    "항상 실행되며, Steamworks/BIsDlcInstalled 같은 식별자가 프로덕션 어디에든 " +
                    "나타나면 빨개집니다(2026-09-28 DLC 폐지로 승인된 자리가 0곳이 되어 원형으로 복원). " +
                    "축 A가 이름을 못 알아봐도 축 B가 대신 알립니다.\n" +
                    "C층 배선 라운드는 이 두 축 중 하나를 반드시 건드리게 됩니다.");
            }

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" C층 표면 ").Append(policy.Count).Append("건\n");
            foreach (Surface s in policy) report.Append("  ").Append(s.File).Append('\t').Append(s.Detail).Append('\n');
            Debug.Log(report.ToString());

            List<string> problems = Violations(sources);
            Assert.IsEmpty(problems,
                $"{LogPrefix} C층 소유 판정이 3상태 계약을 어깁니다({problems.Count}건):\n  · " +
                string.Join("\n  · ", problems) + "\n\n" +
                "이 감사는 무단 사용을 막는 장치가 아닙니다 — <b>돈 낸 사람이 실수로 잠기는 것</b>을 " +
                "막는 장치입니다. 스팀이 아직 안 뜬 상태에서 조회가 실패하면 Unknown이어야 하고, " +
                "Unknown에서는 이미 착용 중인 것을 <b>회수하지 않습니다</b>(§E-2).");
        }

        /// <summary>승인된 예외(결재-1, 2026-09-05) — 파일+식별자 단위. ~~이보다 엄밀한 재검증
        /// (멤버 접근·using 횟수·팩 이름 하드코딩 등)은 <c>SteamEntitlementAdapterAuditTests</c>가
        /// 별도 축으로 다시 잠근다 — 두 검사가 같은 실수를 공유하지 않게 일부러 중복한다.~~
        ///
        /// <para>★★ <b>2026-09-28 사용자 결정(DLC 폐지) — 이 표를 비웠다. 경보가 원형(어디서든 하나라도
        /// 나오면 빨강)으로 돌아왔다.</b> 결재-1의 승인 근거는 「6팩을 팔 수 없다 = 출시 자체가 없다」였고
        /// (<c>docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md</c> :491), 팔 6팩이 없어지면서 소멸했다.
        /// 어댑터 파일과 그 전용 감사(<c>SteamEntitlementAdapterAuditTests</c>)도 같은 커밋에서 지웠고,
        /// <b>순서는 이 표를 먼저 비운 뒤 그 감사를 지우는 것이었다</b> — 뒤집으면 한 라운드 동안
        /// <c>SteamRemoteStorage</c>·<c>SteamInventory</c>·<c>SteamUser</c>를 지키는 것이 하나도 없다.</para>
        ///
        /// <para>★ <b>빈 표는 이 검사를 느슨하게 하지 않고 조인다</b>(면제가 0이므로 모든 히트가 위반이다).
        /// 그래도 「빈 목록을 기대값으로 명시」 규칙(거짓 통과 #5)에 따라 아래 본문이 그 0을 단언하고,
        /// <see cref="NegativeControl_승인_판정기는_표가_비어도_살아_있다"/>가 <b>승인 판정기 자체</b>를
        /// 합성 표로 교정한다 — 표가 비었다는 이유로 판정기가 죽은 코드가 되지 않게.</para></summary>
        private static readonly (string File, string Identifier)[] ApprovedStoreSdkExceptions =
            new (string File, string Identifier)[0];

        /// <summary>히트 하나가 승인 표에 있는가. <b>표를 인자로 받는다</b> — 실제 표가 비어 있어도
        /// 이 판정기를 합성 표로 교정할 수 있게 하기 위한 형태다(그러지 않으면 표가 빈 동안
        /// 판정기가 한 번도 실행되지 않은 코드가 된다).</summary>
        private static bool IsApproved(string file, string identifier,
            (string File, string Identifier)[] table)
        {
            if (table == null) return false;
            foreach ((string approvedFile, string approvedIdentifier) in table)
            {
                if (approvedFile == file && approvedIdentifier == identifier) return true;
            }
            return false;
        }

        /// <summary>
        /// ★ 위 보류의 <b>역방향 장치(축 B)</b>. 항상 실행되며 보류하지 않는다.
        ///
        /// <para>C층 배관(스토어 SDK)이 <b>승인된 어댑터 파일 밖</b>에 나타나거나, 그 파일 안에서도
        /// 승인 안 된 식별자(<c>BIsSubscribedApp</c>·<c>StoreContext</c> 등)가 나타나면 <b>빨개진다.</b>
        /// ★ 2026-09-05 결재-1 전에는 "어디서든 하나라도 나오면 빨강"이었다 — 이제 "승인된 자리 밖에서
        /// 나오면 빨강"으로 좁혔다. 좁힌 것이지 껐다 <b>것은 아니다</b>: 예외 목록을 벗어난 모든 확장은
        /// 여전히 이 경보를 울린다.</para>
        ///
        /// <para>★★ <b>2026-09-28 사용자 결정(DLC 폐지) — 승인 표를 비워 「어디서든 하나라도 나오면 빨강」
        /// 원형으로 돌아왔다.</b> 판정 강도는 2026-09-05 이전과 같다.
        /// <b>메서드 이름은 바꾸지 않았다</b> — 이름이 <c>TestClaimExpiryAuditTests</c>의 <c>Companion</c>과
        /// <c>docs/verify/BASELINE.md</c>의 개명 대장에 인용돼 있어, 이름을 되돌리면 내 범위 밖 파일
        /// 둘이 같은 커밋을 요구한다. 지금 이름은 <b>빈 승인 집합에 대해 공허하게 참</b>이고(히트가 0이면
        /// 「승인된 자리에서만 쓴다」는 자동으로 참이다) <b>검사는 이름이 약속한 것보다 엄격하다</b> —
        /// 위험한 방향은 「이름이 검사보다 많이 약속하는 것」이고 이건 반대 방향이다.
        /// 개명 제안은 리더에게 인계했다(세 파일 동시 수정 필요).</para>
        ///
        /// <para>이 테스트가 사라지면 <c>TestClaimExpiryAuditTests</c>의 Ignore 명부가 먼저 실패한다
        /// (명부가 동반 테스트의 <b>메서드 선언</b> 실재를 매 실행 확인한다). 즉 이 장치를 조용히
        /// 치울 수 있는 경로가 없다.</para>
        /// </summary>
        [Test]
        public void 스토어_SDK는_승인된_어댑터_한_파일에서_승인된_심볼만_쓴다()
        {
            List<(string File, string Stripped)> sources = ProductionSources();
            Assert.GreaterOrEqual(sources.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {sources.Count}개밖에 읽지 못했습니다 — " +
                "이 경보는 지금 아무것도 감시하지 못합니다.");
            Assert.IsNotEmpty(StoreSdkIdentifiers,
                $"{LogPrefix} 감시 식별자 목록이 비었습니다(거짓 통과 #5: 빈 목록은 아무것도 재지 않습니다).");

            // ★ 승인 표의 기대값을 <b>명시</b>한다(거짓 통과 #5의 반대 방향 적용 — 여기서 빈 표는
            //   느슨함이 아니라 엄격함이고, 누군가 조용히 항목을 되살리면 이 줄이 먼저 빨개진다).
            Assert.IsEmpty(ApprovedStoreSdkExceptions,
                $"{LogPrefix} 승인된 스토어 SDK 예외가 {ApprovedStoreSdkExceptions.Length}건 있습니다 " +
                "(기대: 0건 — 2026-09-28 사용자 결정으로 DLC가 폐지되고 결재-1이 닫혔습니다). " +
                "예외를 다시 열려면 사용자 결정이 먼저이고, 그다음이 리더 결재입니다.");

            List<Surface> hits = DetectStoreSdk(sources);
            var violations = new List<Surface>();
            foreach (Surface s in hits)
            {
                if (!IsApproved(s.File, s.Detail, ApprovedStoreSdkExceptions)) violations.Add(s);
            }

            var lines = new List<string>();
            foreach (Surface s in violations) lines.Add($"  · {s.File} → {s.Detail}");

            Assert.IsEmpty(lines,
                $"{LogPrefix} <b>스토어 SDK가 프로덕션에 있습니다</b>({lines.Count}건):\n" +
                string.Join("\n", lines) + "\n\n" +
                "★ 2026-09-28 사용자 결정으로 DLC가 폐지되어 <b>승인된 자리가 0곳</b>입니다 " +
                "(docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md 머리의 예외 폐지 배너). 지금 할 일:\n" +
                "  ① 이 빨강을 오탐으로 보고 니들을 지우지 마세요 — 그게 이 클래스 문서가 이름으로 " +
                "경고하는 형태입니다(니들만 지우고 열면 SteamRemoteStorage·SteamInventory·SteamUser가 " +
                "아무 저항 없이 들어옵니다).\n" +
                "  ② 정말 다시 필요하면 <b>사용자 결정</b>부터입니다(DLC 폐지를 뒤집는 결정). " +
                "그다음 리더 재결재 → 예외 문서 갱신 → ApprovedStoreSdkExceptions에 근거와 함께 추가 " +
                "→ OfflineFirstNetworkAuditTests의 전송 계열 명부도 같은 diff에서 갱신.\n" +
                "  ③ 라인 단위 재검증 감사(SteamEntitlementAdapterAuditTests)는 2026-09-28에 " +
                "함께 삭제됐습니다 — 다시 여는 라운드는 그것도 되살려야 합니다.");
        }

        /// <summary>
        /// ★ <b>승인 판정기 교정</b>. 실제 승인 표가 비어 있어서(2026-09-28 DLC 폐지) 위 본문의
        /// <see cref="IsApproved"/> 호출은 <b>언제나 false</b>를 돈다 — 즉 판정기의 「참」 쪽 분기가
        /// 한 번도 실행되지 않는다. 그 상태를 그대로 두면 나중에 예외를 되살리는 라운드가
        /// <b>한 번도 검증되지 않은 코드</b>에 매출 경계를 맡기게 된다. 합성 표로 양쪽 분기를 매 실행 돈다.
        /// </summary>
        [Test]
        public void NegativeControl_승인_판정기는_표가_비어도_살아_있다()
        {
            var synthetic = new[] { ("합성_어댑터.cs", "Steamworks") };

            Assert.IsTrue(IsApproved("합성_어댑터.cs", "Steamworks", synthetic),
                $"{LogPrefix} 승인 판정기가 정확히 일치하는 항목을 못 알아봅니다 — 예외를 되살리는 날 " +
                "승인된 파일까지 위반으로 잡혀, 그때 사람이 하는 일은 이 검사를 지우는 것입니다.");
            Assert.IsFalse(IsApproved("다른파일.cs", "Steamworks", synthetic),
                $"{LogPrefix} 파일이 달라도 승인으로 셉니다 — 예외가 파일 경계를 넘어 번집니다.");
            Assert.IsFalse(IsApproved("합성_어댑터.cs", "SteamRemoteStorage", synthetic),
                $"{LogPrefix} 식별자가 달라도 승인으로 셉니다 — 파일 통행권이 되어 " +
                "SteamRemoteStorage·SteamInventory가 같은 파일에서 통과합니다.");
            Assert.IsFalse(IsApproved("합성_어댑터.cs", "Steamworks", ApprovedStoreSdkExceptions),
                $"{LogPrefix} 지금의 빈 승인 표가 무언가를 승인합니다 — 표가 비었다는 사실이 " +
                "판정에 반영되지 않고 있습니다.");
            Assert.IsFalse(IsApproved("합성_어댑터.cs", "Steamworks", null),
                $"{LogPrefix} null 표에 대해 승인을 냅니다.");
        }

        // ====================================================================
        // 2. 네거티브 컨트롤 — 오늘 도는 유일한 검증. 보류 분기가 죽은 코드가 되지 않게 한다.
        // ====================================================================

        private static List<(string File, string Stripped)> Fake(string source)
            => new List<(string, string)> { ("Fake.cs", EntitlementAuditSource.StripComments(source)) };

        /// <summary>파일 <b>여럿</b>을 흘린다. 사거리 좁히기(V2·V3의 C층 한정)가
        /// <b>같은 파일 안에만</b> 갇히지 않는다는 것을 재기 위해 필요하다.</summary>
        private static List<(string File, string Stripped)> Fake(params (string Name, string Source)[] parts)
        {
            var list = new List<(string, string)>();
            foreach ((string name, string source) in parts)
            {
                list.Add((name, EntitlementAuditSource.StripComments(source)));
            }
            return list;
        }

        [Test]
        public void NegativeControl_계약을_지키는_C층_샘플은_위반이_0건이다()
        {
            string good =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class PackEntitlements\n" +
                "{\n" +
                "    public static EntitlementState Query(string packId)\n" +
                "    {\n" +
                "        return EntitlementState.Unknown;\n" +
                "    }\n" +
                "    public static string Describe(EntitlementState state)\n" +
                "    {\n" +
                "        switch (state)\n" +
                "        {\n" +
                "            case EntitlementState.Owned: return \"보유\";\n" +
                "            case EntitlementState.NotOwned: return \"구매하면 열립니다\";\n" +
                "            case EntitlementState.Unknown: return \"지금은 확인할 수 없습니다\";\n" +
                "        }\n" +
                "        return \"\";\n" +
                "    }\n" +
                "}\n";
            List<string> problems = Violations(Fake(good));
            Assert.IsEmpty(problems,
                $"{LogPrefix} 계약을 지키는 샘플에 위반이 잡혔습니다 — 이 감사는 C층 배선 라운드를 " +
                "부당하게 빨갛게 만들고, 그러면 몇 번 만에 꺼집니다.\n  · " +
                string.Join("\n  · ", problems));
        }

        [Test]
        public void NegativeControl_Unknown이_없는_2상태를_잡는다()
        {
            string bad = "public enum EntitlementState { Owned, NotOwned }\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} Unknown이 없는 2상태를 통과시켰습니다. 그 붕괴가 이 계약이 막으려는 " +
                "사고 그 자체입니다(스팀 미기동 → false → 정상 구매자 전원 잠김).");
        }

        [Test]
        public void NegativeControl_bool을_돌려주는_소유판정을_잡는다()
        {
            string bad =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class Gate\n" +
                "{\n" +
                "    public static bool IsDlcOwned(string id) => false;\n" +
                "}\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} bool 반환 소유 판정을 통과시켰습니다(§E-1-a).");
        }

        [Test]
        public void NegativeControl_상태_switch의_default를_잡는다()
        {
            string bad =
                "public enum EntitlementState { Owned, NotOwned, Unknown }\n" +
                "public static class Gate\n" +
                "{\n" +
                "    public static string Describe(EntitlementState s)\n" +
                "    {\n" +
                "        switch (s)\n" +
                "        {\n" +
                "            case EntitlementState.Owned: return \"보유\";\n" +
                "            case EntitlementState.Unknown: return \"확인 불가\";\n" +
                "            default: return \"미보유\";\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
            List<string> problems = Violations(Fake(bad));
            Assert.IsNotEmpty(problems,
                $"{LogPrefix} 상태 switch의 default:를 통과시켰습니다(§E-1-b). " +
                "default는 Unknown을 조용히 NotOwned 쪽으로 흡수합니다.");
        }

        [Test]
        public void NegativeControl_축B_스캐너는_스토어_식별자를_실제로_찾아낸다()
        {
            // ★ 양성 대조. 이것이 없으면 위 경보의 "0건"은 능력을 증명하지 못한 0건이다
            //   (이 저장소 실제 사고: strings|grep이 .NET UTF-16 문자열에 대해 탐지력이 애초에 0이었다).
            string fake = "using Steamworks;\n" +
                          "public static class Probe\n" +
                          "{\n" +
                          "    public static void Init() { SteamAPI.Init(); }\n" +
                          "}\n";
            List<Surface> hits = DetectStoreSdk(Fake(fake));
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 축 B 스캐너가 명백한 스토어 SDK 사용을 놓쳤습니다 — " +
                "그러면 위 경보의 '0건'은 '없다'가 아니라 '못 본다'입니다.");
        }

        [Test]
        public void NegativeControl_주석_속_스토어_언급은_경보가_아니다()
        {
            string fake = "public static class Plan\n" +
                          "{\n" +
                          "    /// <summary>훗날 Steamworks로 BIsDlcInstalled를 부를 자리.</summary>\n" +
                          "    public static void TODO() { }\n" +
                          "}\n";
            List<Surface> hits = DetectStoreSdk(Fake(fake));
            Assert.IsEmpty(hits,
                $"{LogPrefix} 주석 속 계획을 배선으로 셌습니다. 이 저장소의 프로덕션·문서 주석에는 " +
                "스팀 이야기가 이미 여러 군데 있어, 주석을 세면 이 경보는 첫날부터 빨간 채로 " +
                "방치됩니다 — 방치된 경보는 없는 경보입니다.");
        }

        [Test]
        public void NegativeControl_축A_스캐너는_C층_타입_선언을_찾아낸다()
        {
            string fake = "public enum PackEntitlementState { Owned, NotOwned, Unknown }\n";
            List<Surface> hits = DetectPolicyTypes(Fake(fake));
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 축 A 스캐너가 C층 타입 선언을 놓쳤습니다 — " +
                "그러면 위 보류는 C층이 들어와도 영원히 '건너뜀'으로 남습니다. " +
                "이 저장소의 Ignore 관례가 정확히 그것을 막으려고 만들어졌습니다.");
        }

        // ====================================================================
        // 3. ★ 사거리 대조 (2026-09-03 coder-systems) — 좁힌 만큼 눈이 멀지 않았는가
        // ====================================================================

        /// <summary>
        /// ★ <b>존재 방향</b>: 상태 열거형을 <b>다른 파일</b>이 선언해도, 그 타입을 쓰는 파일의
        /// 위반은 잡힌다. 사거리를 「선언한 파일」이 아니라 「그 타입을 아는 파일」로 잡은 이유가 이것이다.
        /// </summary>
        [Test]
        public void NegativeControl_사거리는_같은_파일에_갇히지_않는다()
        {
            List<string> problems = Violations(Fake(
                ("State.cs", "public enum PackEntitlementState { Owned, NotOwned, Unknown }\n"),
                ("Gate.cs",
                    "public static class Gate\n" +
                    "{\n" +
                    "    public static bool IsDlcOwned(string id) => Query(id) == PackEntitlementState.Owned;\n" +
                    "    private static PackEntitlementState Query(string id) => PackEntitlementState.Unknown;\n" +
                    "}\n")));

            Assert.IsNotEmpty(problems,
                $"{LogPrefix} 선언 파일과 위반 파일이 다르다는 이유로 위반을 놓쳤습니다. " +
                "그러면 C층을 두 파일로 쪼개는 것만으로 이 감사가 무력해집니다.");
        }

        /// <summary>
        /// ★ <b>부재 방향</b>: C층 상태 타입을 <b>한 글자도 모르는</b> 파일의 <c>bool ...Owned...</c>는
        /// 사거리 밖이다. <b>이건 결함이 아니라 정의다</b> — §E-1-a가 막는 것은
        /// 「엔타이틀먼트 조회 결과」를 <c>bool</c>로 돌려주는 것이고,
        /// 그 타입을 볼 수 없는 함수는 그 결과를 돌려줄 수단 자체가 없다.
        ///
        /// <para>실제로 이 저장소에는 그런 함수가 <b>8건</b> 있다(레벨 해금 2건 · 클릭 캡처 소유권 5건 ·
        /// 포커스 락 1건). 좁히지 않으면 C층이 들어오는 라운드에 그 8건이 함께 빨개지고,
        /// 그때 사람이 하는 일은 니들을 지우는 것이다 — <b>방치되거나 꺼진 경보는 없는 경보다.</b></para>
        ///
        /// <para>★ 이 테스트가 <b>초록이라는 사실 자체</b>가 위험할 수 있으므로
        /// 바로 위 [사거리는_같은_파일에_갇히지_않는다]와 <b>짝</b>이다:
        /// 둘 중 하나만 보면 「좁혔다」와 「꺼 버렸다」를 구분할 수 없다.</para>
        /// </summary>
        [Test]
        public void NegativeControl_C층을_모르는_파일의_bool은_사거리_밖이다()
        {
            List<string> problems = Violations(Fake(
                ("State.cs", "public enum PackEntitlementState { Owned, NotOwned, Unknown }\n"),
                ("LevelUnlock.cs",
                    "public static class LevelUnlock\n" +
                    "{\n" +
                    "    public static bool IsItemOwned(int slot, int index) => index >= 0;\n" +
                    "}\n")));

            Assert.IsEmpty(problems,
                $"{LogPrefix} A·B층(레벨 해금)의 bool 판정이 C층 위반으로 잡혔습니다({problems.Count}건). " +
                "무관한 빨강은 감사를 꺼지게 만듭니다:\n  · " + string.Join("\n  · ", problems));
        }
    }
}
