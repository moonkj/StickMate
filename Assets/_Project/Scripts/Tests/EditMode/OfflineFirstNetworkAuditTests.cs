using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <b>오프라인 우선 원칙 정적 감사</b> (docs/ARCHITECTURE.md 5-1-8, 우선순위표 5-4 A-1).
    ///
    /// 배경: 이 앱은 오늘까지 네트워크 API 사용이 <b>0건</b>이다. "권한 0개 요구"는 이 앱의 최대 자산이지만
    /// 그 0건을 지킬 장치가 프로젝트에 하나도 없었다. 자동 업데이트(5-1-2) / 옵트인 크래시 리포트(5-1-9) /
    /// Steam Cloud(5-1-10)가 들어온 <b>뒤</b>에 이 감사를 넣으면 그저 현상 추인이 된다. 그래서 0건인 지금 잠근다.
    ///
    /// 방식은 <see cref="UserAssetImmutabilityAuditTests"/>(원칙 3 감사)의 프레임워크를 그대로 복제했다 —
    /// 리플렉션이 아니라 <c>Assets/_Project/Scripts/</c> 하위 .cs를 파일 시스템에서 직접 읽어 텍스트 스캔하므로,
    /// 아직 존재하지 않는 미래 파일도 디렉터리 전체 탐색 설계 자체로 자동 포함된다(파일명 하드코딩 없음).
    ///
    /// 이 감사가 원칙 3 감사보다 한 걸음 더 나아간 지점 3가지:
    ///  1. <b>주석 인식</b>: 문자열 리터럴을 존중하면서 줄 주석 <c>//</c> · 블록 주석 <c>/* */</c>(여러 줄 포함)을
    ///     제거한 뒤 스캔한다. 문서 주석이 금지 API 이름을 언급하는 것은 위반이 아니지만(이 파일 자체가 그렇다),
    ///     코드 뒤에 붙은 변명 주석이 그 코드를 숨겨주지도 않는다.
    ///  2. <b>파일 단위 화이트리스트</b>: 미래에 정말 필요한 네트워크 기능이 생기면 이 파일 상단 목록에
    ///     (파일 · 이유 · 허용 니들 · 동의 게이트 유무)를 <b>명시적으로 추가해야만</b> 통과한다. 그 diff가 곧
    ///     리뷰 게이트다 — "왜 이 파일이 화이트리스트에 들어갔지?"라는 질문이 코드 리뷰에서 자동으로 발생하는 것,
    ///     그게 이 감사의 진짜 목적이다. <b>전송(Transmission) 계열 화이트리스트는 현재 0건이며 그 사실 자체를
    ///     테스트로 고정한다</b>(<see cref="전송계열_화이트리스트는_현재_비어_있다"/>).
    ///  3. <b>네거티브 컨트롤</b>: 스캔 로직이 무력화된 채 "위반 0건"으로 허위 통과하는 것을 막기 위해,
    ///     일부러 금지 API를 쓰는 가짜 소스 문자열을 <b>실제 프로덕션 스캔과 완전히 동일한 함수</b>
    ///     (<see cref="ScanSource"/>)에 흘려서 정말 잡아내는지 확인한다.
    ///
    /// 알려진 한계(의도적, 보수적 방향): 여러 줄에 걸친 verbatim 문자열(@"...") 내부는 줄 단위로 다시 코드로
    /// 간주된다 → 놓치는(under-report) 방향이 아니라 과탐(over-report) 방향이므로 감사로서 안전하다.
    /// </summary>
    public class OfflineFirstNetworkAuditTests
    {
        private const string LogPrefix = "[오프라인감사]";

        // =====================================================================
        // 0. 금지/감시 니들 표
        // =====================================================================

        /// <summary>니들의 성격. 화이트리스트 정책이 카테고리별로 다르다.</summary>
        private enum NeedleKind
        {
            /// <summary>바이트가 이 기기 밖으로 나가거나 들어올 수 있는 API. 화이트리스트 기본 0건.</summary>
            Transmission,

            /// <summary>전송은 하지 않지만 네트워크 상태를 조회하는 API. 최소 범위로만 허용한다.</summary>
            ReadOnlyStatus,
        }

        private sealed class ForbiddenNeedle
        {
            public string Needle;
            public NeedleKind Kind;
            public string Reason;
        }

        private static readonly List<ForbiddenNeedle> ForbiddenNeedles = new List<ForbiddenNeedle>
        {
            // ---- Unity 계열 ----
            new ForbiddenNeedle
            {
                Needle = "UnityWebRequest",
                Kind = NeedleKind.Transmission,
                Reason = "Unity 표준 HTTP 클라이언트. 자동 업데이트 확인 / 크래시 리포트 전송이 들어올 때 " +
                    "가장 먼저 등장할 API다(ARCHITECTURE 5-1-2, 5-1-9). 옵트인 동의 게이트 없이 추가 불가.",
            },
            new ForbiddenNeedle
            {
                Needle = "UnityEngine.Networking",
                Kind = NeedleKind.Transmission,
                Reason = "UnityWebRequest / DownloadHandler류가 사는 네임스페이스. using 한 줄만으로도 " +
                    "오프라인 우선 원칙의 표면적이 열린다.",
            },
            new ForbiddenNeedle
            {
                Needle = "DownloadHandler",
                Kind = NeedleKind.Transmission,
                Reason = "원격 응답 본문 수신 핸들러 — 수신도 통신이다.",
            },
            new ForbiddenNeedle
            {
                Needle = "UploadHandler",
                Kind = NeedleKind.Transmission,
                Reason = "원격 전송 본문 핸들러. 창 제목/경로/사용자명이 payload에 섞여 나갈 수 있는 경로 " +
                    "(5-1-9의 스크러빙 요구사항이 이것 때문에 존재한다).",
            },
            new ForbiddenNeedle
            {
                Needle = "new WWW(",
                Kind = NeedleKind.Transmission,
                Reason = "구형 Unity HTTP API. deprecated지만 여전히 컴파일된다.",
            },
            new ForbiddenNeedle
            {
                Needle = "Application.OpenURL",
                Kind = NeedleKind.Transmission,
                Reason = "우리 프로세스가 직접 통신하진 않지만 외부 브라우저로 사용자를 내보낸다. " +
                    "5-1-8이 '사용자 명시 액션 외 금지'로 못박은 항목 — 추가하려면 화이트리스트에 올려 " +
                    "'어떤 사용자 클릭에서만 열리는가'를 리뷰에서 답해야 한다.",
            },

            // ---- BCL(System.*) 계열 ----
            new ForbiddenNeedle
            {
                Needle = "System.Net",
                Kind = NeedleKind.Transmission,
                Reason = "System.Net / System.Net.Http / System.Net.Sockets / System.Net.WebSockets를 " +
                    "using 한 줄 단위에서 통째로 잡는 상위 니들.",
            },
            new ForbiddenNeedle
            {
                Needle = "HttpClient",
                Kind = NeedleKind.Transmission,
                Reason = ".NET 표준 HTTP 클라이언트.",
            },
            new ForbiddenNeedle
            {
                Needle = "HttpWebRequest",
                Kind = NeedleKind.Transmission,
                Reason = "구형 .NET HTTP 요청 API.",
            },
            new ForbiddenNeedle
            {
                Needle = "HttpListener",
                Kind = NeedleKind.Transmission,
                Reason = "로컬 HTTP 서버 개설 — 방화벽 권한 요청을 유발한다('권한 0개 요구' 자산 훼손).",
            },
            new ForbiddenNeedle
            {
                Needle = "WebClient",
                Kind = NeedleKind.Transmission,
                Reason = "구형 .NET 다운로드/업로드 클라이언트.",
            },
            new ForbiddenNeedle
            {
                Needle = "WebSocket",
                Kind = NeedleKind.Transmission,
                Reason = "상시 연결 소켓 — 오프라인 우선 원칙과 가장 정면으로 충돌하는 형태.",
            },
            new ForbiddenNeedle
            {
                Needle = "TcpClient",
                Kind = NeedleKind.Transmission,
                Reason = "원시 TCP 연결.",
            },
            new ForbiddenNeedle
            {
                Needle = "TcpListener",
                Kind = NeedleKind.Transmission,
                Reason = "원시 TCP 수신 대기 — HttpListener와 같은 이유로 금지.",
            },
            new ForbiddenNeedle
            {
                Needle = "UdpClient",
                Kind = NeedleKind.Transmission,
                Reason = "원시 UDP 소켓(디스커버리/텔레메트리에 흔히 쓰인다).",
            },
            new ForbiddenNeedle
            {
                Needle = "new Socket(",
                Kind = NeedleKind.Transmission,
                Reason = "최저수준 소켓 생성.",
            },
            new ForbiddenNeedle
            {
                Needle = "SmtpClient",
                Kind = NeedleKind.Transmission,
                Reason = "메일 전송 — 리포트 전송의 우회 경로.",
            },
            new ForbiddenNeedle
            {
                Needle = "FtpWebRequest",
                Kind = NeedleKind.Transmission,
                Reason = "FTP 전송.",
            },
            new ForbiddenNeedle
            {
                Needle = "Dns.",
                Kind = NeedleKind.Transmission,
                Reason = "DNS 조회 자체가 외부로 나가는 질의다(연결 없이도 '어디에 접속하려 했는지'가 샌다).",
            },
            new ForbiddenNeedle
            {
                Needle = "ServicePointManager",
                Kind = NeedleKind.Transmission,
                Reason = "TLS/커넥션 정책 설정 — 네트워크 스택을 쓰기 시작했다는 확실한 신호.",
            },
            new ForbiddenNeedle
            {
                Needle = "new Uri(\"http",
                Kind = NeedleKind.Transmission,
                Reason = "원격 스킴 URI 조립(5-1-8 명시 니들). 로컬 file:// URI나 문서 주석의 URL은 " +
                    "여기에 걸리지 않는다 — 실제 코드에서 http(s) Uri 객체를 만드는 경우만 잡는다.",
            },

            // ---- 서드파티 네트워크 스택(선제 차단) ----
            new ForbiddenNeedle
            {
                Needle = "Steamworks",
                Kind = NeedleKind.Transmission,
                Reason = "Steamworks.NET 의존은 런타임 네트워크 의존을 새로 만든다. 5-1-10이 명시적으로 " +
                    "'Auto-Cloud(코드 0줄) 경로를 권고, ISteamRemoteStorage API 방식은 권고하지 않음'으로 " +
                    "결론 낸 항목이다. 되살리려면 그 결론을 뒤집는 리뷰가 선행되어야 한다.",
            },
            new ForbiddenNeedle
            {
                Needle = "Unity.Netcode",
                Kind = NeedleKind.Transmission,
                Reason = "Netcode for GameObjects — P2P 친구 방문(5-4 C-23)은 장기 로드맵이고 " +
                    "'오프라인 원칙 체계 확립 후'가 전제 조건이다.",
            },

            // ---- 전송은 아니지만 감시하는 상태 조회 ----
            new ForbiddenNeedle
            {
                Needle = "Application.internetReachability",
                Kind = NeedleKind.ReadOnlyStatus,
                Reason = "바이트를 보내지 않는 OS 상태 조회지만, '네트워크를 의식하는 코드'가 늘어나는 " +
                    "입구다. 하드웨어 리액션 1곳으로 범위를 못박는다(아래 화이트리스트 참고).",
            },
            new ForbiddenNeedle
            {
                Needle = "NetworkReachability",
                Kind = NeedleKind.ReadOnlyStatus,
                Reason = "위 항목의 열거형. 같은 이유로 같은 파일에서만 허용.",
            },
        };

        // =====================================================================
        // 1. 화이트리스트 — 여기에 줄을 추가하는 행위 자체가 리뷰 게이트다
        // =====================================================================

        /// <summary>
        /// 파일 단위 예외. <b>새 네트워크 코드를 넣으려는 사람은 반드시 여기에 항목을 추가해야 한다</b> —
        /// 그 diff가 리뷰어에게 "왜 이 파일이?"를 묻게 만드는 장치다.
        ///
        /// 각 항목은 5-1-8이 요구한 3종을 전부 채워야 한다: <b>(파일, 이유, 동의 게이트 존재 여부)</b>.
        /// 추가로 <see cref="AllowedNeedles"/>를 반드시 명시해야 한다 — 파일 하나를 통째로 면제해 주면
        /// 나중에 그 파일에 진짜 위반이 추가돼도 화이트리스트가 함께 숨겨주기 때문이다(원칙 3 감사가
        /// 이미 배운 교훈). <see cref="LineVerifier"/>는 "그 라인이 정말 알려진 안전한 형태인지"까지 본다.
        /// </summary>
        private sealed class NetworkWhitelistEntry
        {
            /// <summary>대상 파일명(대소문자 구분, 경로 없음).</summary>
            public string FileName;

            /// <summary>왜 이 예외가 필요한가 — 리뷰에서 읽힐 문장. 비어 있으면 테스트가 실패한다.</summary>
            public string Reason;

            /// <summary>이 파일에서만 허용할 니들 목록. null/빈 목록은 금지(파일 통째 면제 방지).</summary>
            public string[] AllowedNeedles;

            /// <summary>
            /// 사용자 동의(옵트인)가 전제되어야 하는 기능인가. true면 <see cref="ConsentGateNeedle"/>이
            /// 같은 파일 안에 실제로 존재하는지까지 검사한다 — "동의 받는다"는 말이 코드에 없으면 실패.
            /// </summary>
            public bool RequiresConsentGate;

            /// <summary>동의 플래그를 읽는 코드의 식별 문자열(RequiresConsentGate=true일 때 필수).</summary>
            public string ConsentGateNeedle;

            /// <summary>매치된 코드 라인 원문 하나를 받아 "알려진 안전한 형태인가"를 판정. null이면 무조건 허용.</summary>
            public Func<string, bool> LineVerifier;
        }

        // ★ 전송(Transmission) 계열 화이트리스트는 의도적으로 비어 있다(2026-08-31 기준).
        //    아래 목록의 유일한 항목은 바이트를 하나도 보내지 않는 ReadOnlyStatus 계열이다.
        //    전송 계열 항목이 0건임은 별도 테스트로 고정되어 있다 → 전송계열_화이트리스트는_현재_비어_있다()
        private static readonly List<NetworkWhitelistEntry> Whitelist = new List<NetworkWhitelistEntry>
        {
            new NetworkWhitelistEntry
            {
                FileName = "HardwareReactionDirector.cs",
                Reason = "하드웨어 리액션(UX_FLOW 23절) — 인터넷이 끊기면 캐릭터가 걱정하는 연출. " +
                    "OS가 이미 알고 있는 도달성 상태를 읽기만 하며 소켓을 열지도, 바이트를 보내지도 않는다. " +
                    "따라서 '권한 0개 요구' 자산에 손상이 없다. 다만 네트워크를 의식하는 코드의 유일한 " +
                    "입구이므로 이 1파일 · 이 2니들로 범위를 못박는다.",
                AllowedNeedles = new[] { "Application.internetReachability", "NetworkReachability" },
                RequiresConsentGate = false,
                ConsentGateNeedle = null,
                // 허용 형태는 오직 "도달성 열거형과의 비교"뿐이다. 대입(=)이나 전송 니들 동반은 불허.
                LineVerifier = line =>
                    line.Contains("NetworkReachability")
                    && !line.Contains("UnityWebRequest")
                    && !line.Contains("HttpClient")
                    && !line.Contains("System.Net"),
            },
        };

        // =====================================================================
        // 2. 스캔 엔진 — 프로덕션 스캔과 네거티브 컨트롤이 반드시 공유하는 단 하나의 경로
        // =====================================================================

        private sealed class Violation
        {
            public string FileName;
            public int LineNumber;
            public string Needle;
            public string Reason;
            public string LineText;

            public override string ToString() =>
                $"{FileName}:{LineNumber}: 금지 니들 '{Needle}' — {Reason}\n    라인 원문: {LineText.Trim()}";
        }

        /// <summary>
        /// 소스 한 파일(줄 배열)을 스캔해 위반 목록을 만든다.
        /// <b>프로덕션 감사와 네거티브 컨트롤이 이 함수 하나만 쓴다</b> — 스캔 로직이 조용히 무력화되면
        /// 네거티브 컨트롤 쪽이 먼저 빨갛게 터지도록 만들기 위한 구조다.
        /// </summary>
        private static List<Violation> ScanSource(
            string fileName,
            IReadOnlyList<string> lines,
            IReadOnlyList<ForbiddenNeedle> needles,
            IReadOnlyList<NetworkWhitelistEntry> whitelist,
            IReadOnlyList<string> fullFileTextForConsentCheck = null)
        {
            var result = new List<Violation>();
            bool inBlockComment = false;
            var consentSource = fullFileTextForConsentCheck ?? lines;

            for (int i = 0; i < lines.Count; i++)
            {
                string code = StripComments(lines[i], ref inBlockComment);
                if (code.Trim().Length == 0) continue;

                foreach (var needle in needles)
                {
                    if (!code.Contains(needle.Needle)) continue;
                    if (IsWhitelisted(fileName, needle.Needle, lines[i], whitelist, consentSource)) continue;

                    result.Add(new Violation
                    {
                        FileName = fileName,
                        LineNumber = i + 1,
                        Needle = needle.Needle,
                        Reason = needle.Reason,
                        LineText = lines[i],
                    });
                }
            }

            return result;
        }

        private static bool IsWhitelisted(
            string fileName,
            string needle,
            string rawLine,
            IReadOnlyList<NetworkWhitelistEntry> whitelist,
            IReadOnlyList<string> fileLines)
        {
            foreach (var entry in whitelist)
            {
                if (!string.Equals(entry.FileName, fileName, StringComparison.Ordinal)) continue;
                if (entry.AllowedNeedles == null || !entry.AllowedNeedles.Contains(needle)) continue;
                if (entry.LineVerifier != null && !entry.LineVerifier(rawLine)) continue;

                if (entry.RequiresConsentGate)
                {
                    // "동의를 받는다"는 주장이 같은 파일의 코드로 뒷받침되지 않으면 예외를 인정하지 않는다.
                    bool consentPresent = !string.IsNullOrEmpty(entry.ConsentGateNeedle)
                        && fileLines.Any(l =>
                        {
                            bool dummy = false;
                            return StripComments(l, ref dummy).Contains(entry.ConsentGateNeedle);
                        });
                    if (!consentPresent) continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 한 줄에서 주석을 제거하고 코드 부분만 돌려준다. 문자열 리터럴("...", @"...", '...')을 존중하므로
        /// URL 문자열 안의 <c>//</c>가 줄을 잘라먹지 않고, 반대로 문자열 안에 숨긴 금지 니들도 놓치지 않는다.
        /// <paramref name="inBlockComment"/>는 여러 줄 블록 주석 상태를 호출자가 이어서 들고 있기 위한 것이다.
        /// </summary>
        private static string StripComments(string line, ref bool inBlockComment)
        {
            var sb = new StringBuilder(line.Length);
            bool inString = false, inVerbatim = false, inChar = false;
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];
                char n = i + 1 < line.Length ? line[i + 1] : '\0';

                if (inBlockComment)
                {
                    if (c == '*' && n == '/') { inBlockComment = false; i += 2; }
                    else i++;
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    if (inVerbatim)
                    {
                        if (c == '"' && n == '"') { sb.Append(n); i += 2; continue; }
                        if (c == '"') { inString = false; inVerbatim = false; }
                        i++;
                        continue;
                    }

                    if (c == '\\' && n != '\0') { sb.Append(n); i += 2; continue; }
                    if (c == '"') inString = false;
                    i++;
                    continue;
                }

                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && n != '\0') { sb.Append(n); i += 2; continue; }
                    if (c == '\'') inChar = false;
                    i++;
                    continue;
                }

                if (c == '/' && n == '/') break;                       // 줄 주석 — 이후는 전부 버린다
                if (c == '/' && n == '*') { inBlockComment = true; i += 2; continue; }
                if (c == '@' && n == '"') { inString = true; inVerbatim = true; sb.Append(c).Append(n); i += 2; continue; }
                if (c == '"') { inString = true; sb.Append(c); i++; continue; }
                if (c == '\'') { inChar = true; sb.Append(c); i++; continue; }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        // =====================================================================
        // 3. 스캔 대상 수집 (UserAssetImmutabilityAuditTests와 동일 규약)
        // =====================================================================

        /// <summary>
        /// Assets/_Project/Scripts/ 하위 전체 .cs (Tests 폴더 자신은 제외 — 이 파일이 금지 니들을 문자열
        /// 리터럴로 담고 있어 자기 자신을 스캔하면 100% 오탐이다). 파일명을 하드코딩하지 않으므로
        /// 미래에 추가될 파일도 자동으로 스캔 대상이 된다.
        /// </summary>
        private static List<string> CollectScannedSourceFiles()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string testsRoot = (Path.Combine(scriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');

            return Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        // =====================================================================
        // 4. 본 감사
        // =====================================================================

        [Test]
        public void 프로덕션_소스에_네트워크_API_사용이_0건이다()
        {
            var files = CollectScannedSourceFiles();
            var violations = new List<Violation>();

            foreach (string path in files)
            {
                string[] lines = File.ReadAllLines(path);
                violations.AddRange(ScanSource(Path.GetFileName(path), lines, ForbiddenNeedles, Whitelist));
            }

            Debug.Log($"{LogPrefix} 정적 스캔 — 파일 {files.Count}개 × 니들 {ForbiddenNeedles.Count}종, " +
                $"위반 {violations.Count}건, 화이트리스트 {Whitelist.Count}건" +
                $"(전송계열 {Whitelist.Count(TransmissionWhitelistPredicate)}건).");

            Assert.IsTrue(violations.Count == 0,
                $"{LogPrefix} 네트워크 관련 API 사용이 발견되었습니다. 이 앱은 오늘까지 네트워크 코드 0건이며 " +
                "그것이 '권한 0개 요구'라는 최대 자산의 근거입니다(docs/ARCHITECTURE.md 5-1-8).\n" +
                "정말로 필요한 기능이라면 이 테스트 파일 상단의 Whitelist에 (파일 · 이유 · 허용 니들 · " +
                "동의 게이트 유무)를 명시해 추가하세요 — 그 diff가 리뷰에서 논의되는 것이 이 감사의 목적입니다.\n\n" +
                string.Join("\n\n", violations.Select(v => v.ToString())));
        }

        [Test]
        public void 전송계열_화이트리스트는_현재_비어_있다()
        {
            // 이 앱의 상태를 한 줄로 요약하는 테스트다. 여기가 빨개졌다면 그건 버그가 아니라
            // "우리 앱이 더 이상 완전 오프라인이 아니게 되는 결정"이 내려졌다는 뜻이고,
            // 그 결정은 리더 승인 + 이 테스트의 명시적 갱신을 함께 거쳐야 한다.
            var transmissionEntries = Whitelist.Where(TransmissionWhitelistPredicate).ToList();

            Assert.IsTrue(transmissionEntries.Count == 0,
                $"{LogPrefix} 전송(Transmission) 계열 네트워크 API의 화이트리스트 예외가 생겼습니다. " +
                "자동 업데이트(5-1-2) / 옵트인 크래시 리포트(5-1-9) / Steam Cloud(5-1-10)는 전부 " +
                "'1차 출시 이후 · 조건부'로 미뤄진 항목입니다. 리더 승인 없이 추가할 수 없습니다.\n" +
                string.Join("\n", transmissionEntries.Select(e => $"  - {e.FileName}: {e.Reason}")));
        }

        private static bool TransmissionWhitelistPredicate(NetworkWhitelistEntry entry)
        {
            return entry.AllowedNeedles != null && entry.AllowedNeedles.Any(n =>
                ForbiddenNeedles.Any(f => f.Needle == n && f.Kind == NeedleKind.Transmission));
        }

        // =====================================================================
        // 5. 화이트리스트 자체의 위생 검사
        // =====================================================================

        [Test]
        public void 화이트리스트_항목은_전부_파일_이유_허용니들_동의게이트를_갖춘다()
        {
            var problems = new List<string>();

            foreach (var entry in Whitelist)
            {
                if (string.IsNullOrWhiteSpace(entry.FileName))
                    problems.Add("FileName이 비어 있는 항목이 있습니다.");
                if (string.IsNullOrWhiteSpace(entry.Reason) || entry.Reason.Trim().Length < 20)
                    problems.Add($"{entry.FileName}: Reason이 없거나 너무 짧습니다 — 리뷰어가 읽고 판단할 문장이어야 합니다.");
                if (entry.AllowedNeedles == null || entry.AllowedNeedles.Length == 0)
                    problems.Add($"{entry.FileName}: AllowedNeedles가 비어 있습니다 — 파일 통째 면제는 금지입니다.");
                else
                {
                    foreach (string n in entry.AllowedNeedles)
                    {
                        if (!ForbiddenNeedles.Any(f => f.Needle == n))
                            problems.Add($"{entry.FileName}: AllowedNeedles의 '{n}'이 금지 니들 표에 없습니다 " +
                                "— 오타이거나 죽은 예외입니다.");
                    }
                }

                if (entry.RequiresConsentGate && string.IsNullOrWhiteSpace(entry.ConsentGateNeedle))
                    problems.Add($"{entry.FileName}: 동의가 필요한 항목인데 ConsentGateNeedle이 없습니다 " +
                        "— '옵트인'이라는 주장을 코드로 확인할 방법이 없습니다(5-1-9).");
            }

            Assert.IsTrue(problems.Count == 0,
                $"{LogPrefix} 화이트리스트 항목이 5-1-8의 요구 형식을 충족하지 않습니다:\n" +
                string.Join("\n", problems));
        }

        [Test]
        public void 화이트리스트_항목은_죽은_예외가_아니다()
        {
            // 예외가 실제로는 아무 라인도 가리지 않는데 남아 있으면, 다음 사람이 "이 파일은 원래
            // 네트워크를 쓰는 파일"이라고 오해하고 진짜 위반을 그 밑에 끼워 넣게 된다.
            // 원칙 3 감사가 SetWindowPos 예외를 제거할 때 쓴 것과 같은 논리다.
            var files = CollectScannedSourceFiles();
            var problems = new List<string>();

            foreach (var entry in Whitelist)
            {
                string path = files.FirstOrDefault(p => Path.GetFileName(p) == entry.FileName);
                if (path == null)
                {
                    problems.Add($"{entry.FileName}: 화이트리스트에 있으나 스캔 대상에 그런 파일이 없습니다 " +
                        "(삭제/이름변경됐다면 이 예외도 함께 지우세요).");
                    continue;
                }

                string[] lines = File.ReadAllLines(path);
                bool anyHit = false;
                bool block = false;
                foreach (string line in lines)
                {
                    string code = StripComments(line, ref block);
                    if (entry.AllowedNeedles != null && entry.AllowedNeedles.Any(code.Contains)) { anyHit = true; break; }
                }

                if (!anyHit)
                {
                    problems.Add($"{entry.FileName}: 허용 니들이 이 파일의 실제 코드에 한 번도 등장하지 않습니다 " +
                        "— 죽은 예외이므로 제거하세요(예외가 줄어드는 것은 좋은 신호입니다).");
                }
            }

            Assert.IsTrue(problems.Count == 0,
                $"{LogPrefix} 죽은 화이트리스트 예외가 있습니다:\n" + string.Join("\n", problems));
        }

        // =====================================================================
        // 6. 스캔 커버리지 가드 (허위 통과 방지)
        // =====================================================================

        [Test]
        public void 정적_스캔이_실제로_충분한_수의_소스파일을_찾아낸다()
        {
            // 2026-08-31 기준 Scripts/ 하위(Tests 제외) .cs는 129개다. 경로 계산이 틀려 0개를 스캔한 채
            // "위반 0건"으로 허위 통과하는 사고를 막는 하한 가드 — 파일 수는 늘어나기만 하므로 100으로 잡는다.
            var files = CollectScannedSourceFiles();

            Assert.GreaterOrEqual(files.Count, 100,
                $"{LogPrefix} 스캔 대상 파일 수가 비정상적으로 적습니다({files.Count}) — " +
                "Application.dataPath 기준 경로 계산 오류로 감사가 사실상 아무것도 보지 않고 있을 수 있습니다.");

            Assert.IsTrue(files.Any(p => Path.GetFileName(p) == "IPlatformWindowService.cs"),
                $"{LogPrefix} 알려진 파일(IPlatformWindowService.cs)이 스캔 목록에 없습니다 — 경로 계산 오류 의심.");
            Assert.IsTrue(files.Any(p => Path.GetFileName(p) == "HardwareReactionDirector.cs"),
                $"{LogPrefix} 알려진 파일(HardwareReactionDirector.cs)이 스캔 목록에 없습니다 — 경로 계산 오류 의심.");

            Assert.IsFalse(files.Any(p => p.Replace('\\', '/').Contains("/Tests/")),
                $"{LogPrefix} 테스트 폴더의 .cs가 스캔 대상에 포함되면 안 됩니다 " +
                "(이 파일 자신이 금지 니들 리터럴을 담고 있어 자기 참조 오탐이 납니다).");

            Assert.GreaterOrEqual(ForbiddenNeedles.Count, 15,
                $"{LogPrefix} 금지 니들 표가 비정상적으로 작습니다({ForbiddenNeedles.Count}) — " +
                "누군가 표를 비워 감사를 무력화했을 수 있습니다.");
            Assert.IsTrue(ForbiddenNeedles.Any(n => n.Needle == "UnityWebRequest"),
                $"{LogPrefix} 핵심 니들 'UnityWebRequest'가 표에서 사라졌습니다.");
            Assert.IsTrue(ForbiddenNeedles.Any(n => n.Needle == "System.Net"),
                $"{LogPrefix} 핵심 니들 'System.Net'이 표에서 사라졌습니다.");
        }

        [Test]
        public void 어셈블리_정의가_네트워크_어셈블리를_참조하지_않는다()
        {
            // .cs 텍스트 스캔이 놓치는 선행 신호: 코드보다 asmdef 참조가 먼저 들어오는 경우가 있다.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            var asmdefs = Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories);

            Assert.Greater(asmdefs.Length, 0, $"{LogPrefix} asmdef를 하나도 찾지 못했습니다 — 경로 계산 오류 의심.");

            string[] banned = { "Unity.Networking", "UnityEngine.Networking", "Unity.Netcode", "Mirror", "Steamworks" };
            var problems = new List<string>();

            foreach (string path in asmdefs)
            {
                string text = File.ReadAllText(path);
                foreach (string b in banned)
                {
                    if (text.Contains(b))
                        problems.Add($"{Path.GetFileName(path)}: 네트워크 어셈블리 '{b}' 참조");
                }
            }

            Assert.IsTrue(problems.Count == 0,
                $"{LogPrefix} 어셈블리 정의에 네트워크 어셈블리 참조가 생겼습니다 " +
                "(코드보다 먼저 들어오는 선행 신호입니다):\n" + string.Join("\n", problems));
        }

        // =====================================================================
        // 7. 네거티브 컨트롤 — 스캔 로직이 정말 무언가를 잡아내는가
        // =====================================================================
        // 아래 테스트들은 전부 위 프로덕션 감사와 **동일한** ScanSource()에 가짜 소스를 흘린다.
        // 스캔이 무력화되면(니들 표가 비거나, 주석 제거가 코드까지 먹어치우거나, 화이트리스트가
        // 전부 통과시키거나) 프로덕션 감사는 조용히 초록불로 남지만 이쪽이 먼저 빨개진다.

        private static readonly List<NetworkWhitelistEntry> NoWhitelist = new List<NetworkWhitelistEntry>();

        [Test]
        public void NegativeControl_스캐너는_실제_금지_API_코드를_반드시_잡아낸다()
        {
            var fakeSource = new[]
            {
                "using System.Net.Http;",                                     // System.Net
                "public class FakeUpdater {",
                "    public void Check() {",
                "        var req = UnityWebRequest.Get(\"https://example.com/version\");",  // UnityWebRequest
                "        var client = new HttpClient();",                     // HttpClient
                "        var tcp = new TcpClient(\"example.com\", 80);",      // TcpClient
                "        Application.OpenURL(\"https://example.com\");",      // Application.OpenURL
                "        var uri = new Uri(\"https://example.com\");",        // new Uri(\"http
                "    }",
                "}",
            };

            var violations = ScanSource("FakeUpdater.cs", fakeSource, ForbiddenNeedles, NoWhitelist);
            var caught = new HashSet<string>(violations.Select(v => v.Needle));

            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 가짜 소스 {fakeSource.Length}줄에서 " +
                $"위반 {violations.Count}건 검출, 니들 종류: {string.Join(", ", caught.OrderBy(s => s))}.");

            foreach (string expected in new[]
                { "System.Net", "UnityWebRequest", "HttpClient", "TcpClient", "Application.OpenURL", "new Uri(\"http" })
            {
                Assert.IsTrue(caught.Contains(expected),
                    $"{LogPrefix} 스캔 로직이 명백한 위반 '{expected}'을 놓쳤습니다 — 감사가 무력화된 상태이며, " +
                    "프로덕션 스캔의 '위반 0건'은 이 시점부터 아무것도 증명하지 못합니다.");
            }

            // 줄 번호도 실제 위치를 가리켜야 리포트가 쓸모 있다.
            Assert.IsTrue(violations.Any(v => v.Needle == "UnityWebRequest" && v.LineNumber == 4),
                $"{LogPrefix} 위반 줄 번호가 실제 위치(4)를 가리키지 않습니다.");
        }

        [Test]
        public void NegativeControl_주석_안의_금지_API는_위반으로_보지_않는다()
        {
            var fakeSource = new[]
            {
                "// var client = new HttpClient();  // 예전에 이렇게 하려다 접었다",
                "/// <summary>UnityWebRequest는 이 프로젝트에서 금지다(5-1-8).</summary>",
                "/*",
                "   using System.Net.Sockets;",
                "   var tcp = new TcpClient();",
                "*/",
                "public class CleanFile { }",
            };

            var violations = ScanSource("CleanFile.cs", fakeSource, ForbiddenNeedles, NoWhitelist);

            Assert.IsTrue(violations.Count == 0,
                $"{LogPrefix} 주석 줄을 위반으로 오탐했습니다 — 문서가 금지 API를 언급하는 것까지 막으면 " +
                "이 감사는 곧 무시되거나 삭제됩니다(오탐은 감사의 죽음이다).\n" +
                string.Join("\n", violations.Select(v => v.ToString())));
        }

        [Test]
        public void NegativeControl_코드_뒤에_붙은_변명_주석은_그_코드를_숨겨주지_못한다()
        {
            var fakeSource = new[]
            {
                "var client = new HttpClient(); // 이건 그냥 임시 코드라서 괜찮아요",
                "string note = \"// UnityWebRequest\"; // 문자열 안에 숨긴 니들",
                "string url = \"https://example.com/path\"; // URL 문자열 자체는 위반이 아니다",
            };

            var violations = ScanSource("Sneaky.cs", fakeSource, ForbiddenNeedles, NoWhitelist);

            Assert.IsTrue(violations.Any(v => v.Needle == "HttpClient" && v.LineNumber == 1),
                $"{LogPrefix} 뒤에 주석이 붙었다는 이유로 실제 코드를 놓쳤습니다 — 주석 제거 로직이 " +
                "라인 전체를 주석으로 오판하고 있습니다.");

            Assert.IsTrue(violations.Any(v => v.Needle == "UnityWebRequest" && v.LineNumber == 2),
                $"{LogPrefix} 문자열 리터럴 안의 니들을 놓쳤습니다 — 주석 제거가 문자열을 존중하지 않아 " +
                "'//' 이후를 잘라버렸다는 뜻입니다.");

            Assert.IsFalse(violations.Any(v => v.LineNumber == 3),
                $"{LogPrefix} 단순 URL 문자열을 위반으로 오탐했습니다(new Uri(\"http 니들은 실제 Uri 조립만 " +
                "잡아야 합니다).");
        }

        [Test]
        public void NegativeControl_화이트리스트는_명시된_파일과_니들에만_적용된다()
        {
            var fakeSource = new[]
            {
                "var client = new HttpClient();",
            };

            var scoped = new List<NetworkWhitelistEntry>
            {
                new NetworkWhitelistEntry
                {
                    FileName = "Allowed.cs",
                    Reason = "네거티브 컨트롤용 가짜 예외 — 화이트리스트가 파일/니들 범위를 지키는지 확인한다.",
                    AllowedNeedles = new[] { "HttpClient" },
                    RequiresConsentGate = false,
                },
            };

            Assert.IsTrue(ScanSource("Allowed.cs", fakeSource, ForbiddenNeedles, scoped).Count == 0,
                $"{LogPrefix} 화이트리스트에 명시된 파일인데도 위반으로 잡혔습니다 — 예외 메커니즘이 죽어 있으면 " +
                "정당한 기능 추가가 불가능해지고 결국 감사 전체가 삭제됩니다.");

            Assert.IsTrue(ScanSource("Other.cs", fakeSource, ForbiddenNeedles, scoped).Count > 0,
                $"{LogPrefix} 화이트리스트에 없는 파일까지 통과시켰습니다 — 파일 범위가 무시되고 있습니다.");

            var otherNeedle = new[] { "var req = UnityWebRequest.Get(\"https://x\");" };
            Assert.IsTrue(ScanSource("Allowed.cs", otherNeedle, ForbiddenNeedles, scoped).Count > 0,
                $"{LogPrefix} 허용 니들(HttpClient) 외의 니들까지 통과시켰습니다 — 파일 통째 면제로 " +
                "동작하고 있다는 뜻이며, 그 파일에 추가되는 미래의 진짜 위반을 전부 숨기게 됩니다.");
        }

        [Test]
        public void NegativeControl_동의_게이트가_없으면_화이트리스트가_적용되지_않는다()
        {
            // 5-1-9(옵트인 크래시 리포트)가 실제로 들어올 때의 예행연습이다.
            // "옵트인입니다"라는 주석만 달고 동의 플래그를 읽는 코드가 없으면 예외는 인정되지 않는다.
            var withoutConsent = new[]
            {
                "public void Send() {",
                "    var client = new HttpClient(); // 옵트인이라고 주장만 함",
                "}",
            };

            var withConsent = new[]
            {
                "public void Send() {",
                "    if (!settings.crashReportOptIn) return;",
                "    var client = new HttpClient();",
                "}",
            };

            var gated = new List<NetworkWhitelistEntry>
            {
                new NetworkWhitelistEntry
                {
                    FileName = "CrashReporter.cs",
                    Reason = "네거티브 컨트롤용 가짜 예외 — 동의 게이트 검증이 실제로 동작하는지 확인한다.",
                    AllowedNeedles = new[] { "HttpClient" },
                    RequiresConsentGate = true,
                    ConsentGateNeedle = "crashReportOptIn",
                },
            };

            Assert.IsTrue(ScanSource("CrashReporter.cs", withoutConsent, ForbiddenNeedles, gated).Count > 0,
                $"{LogPrefix} 동의 플래그를 읽는 코드가 없는데도 화이트리스트가 통과시켰습니다 — " +
                "'옵트인 + 명시 고지 필수'(기획서 2·3절, 5-1-9)가 코드로 강제되지 않고 있습니다.");

            Assert.IsTrue(ScanSource("CrashReporter.cs", withConsent, ForbiddenNeedles, gated).Count == 0,
                $"{LogPrefix} 동의 플래그를 읽는 코드가 있는데도 위반으로 잡혔습니다 — 게이트 검사가 " +
                "항상 실패하는 방향으로 고장 나 있습니다.");
        }

        [Test]
        public void NegativeControl_라인_검증자는_같은_파일의_다른_위반을_숨기지_않는다()
        {
            // 실제 Whitelist의 HardwareReactionDirector 항목이 갖는 성질을 그대로 검증한다:
            // "도달성 비교" 라인은 통과하지만, 같은 파일에 전송 코드가 추가되면 그건 잡혀야 한다.
            var source = new[]
            {
                "bool downNow = Application.internetReachability == NetworkReachability.NotReachable;",
                "var client = new HttpClient();",
                "Application.internetReachability.ToString(); // 비교가 아닌 다른 사용",
            };

            var violations = ScanSource("HardwareReactionDirector.cs", source, ForbiddenNeedles, Whitelist);

            Assert.IsFalse(violations.Any(v => v.LineNumber == 1),
                $"{LogPrefix} 실제 화이트리스트가 커버해야 할 도달성 비교 라인을 위반으로 잡았습니다.");
            Assert.IsTrue(violations.Any(v => v.LineNumber == 2 && v.Needle == "HttpClient"),
                $"{LogPrefix} 화이트리스트된 파일이라는 이유로 같은 파일의 전송 코드까지 숨겼습니다 — " +
                "파일 통째 면제와 다를 바 없는 상태입니다.");
            Assert.IsTrue(violations.Any(v => v.LineNumber == 3),
                $"{LogPrefix} LineVerifier가 '도달성 열거형과의 비교'라는 알려진 안전 형태를 벗어난 사용을 " +
                "그대로 통과시켰습니다.");
        }
    }
}
