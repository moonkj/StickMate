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
    /// ★ 테스트가 적어 둔 「결함 서술」이 조용히 늙지 않게 한다 (code-inspection R2, 2026-09-02 신설)
    /// ============================================================================
    /// R1이 잡은 것은 <b>거짓 주석</b>이었다 — 틀린 말. 이 파일이 잡는 것은 그 반대다:
    /// <b>맞는 말인데 아무도 안 읽는 문장.</b>
    ///
    /// <para>발단(리더 실측): <c>AppearanceShapeBudgetTests</c>의 실패 메시지 안에
    /// <i>"CharacterFxRenderer는 ItemCatalog.ResolveWornPalette를 부르지 않아 보조색 자체가 없습니다"</i>가
    /// 정확히 적혀 있었고, 그 문장이 서술하는 결함은 <b>지금도 참</b>이며, 테스트는 <b>초록</b>이었다.
    /// 러너는 아무 신호도 내지 않았다. 결함을 아는 사람이 그 문장을 썼다는 것은
    /// <b>한 번은 발견됐고 그 다음에 잊혔다</b>는 뜻이다.</para>
    ///
    /// <para>이 저장소는 이미 그 처방을 갖고 있다 — <c>Assert.Ignore</c>(사유 포함)로 남겨 러너에
    /// "건너뜀"으로 계속 보이게 하는 관례(CLAUDE.md). 그런데 위 문장은 Ignore도 아니고
    /// <b>통과하는 테스트의 실패 메시지 안</b>에 숨어 있었다. 그 자리에는 만료 장치가 없다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 하는 일 — 두 개의 대장
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>주장 대장</b>(<see cref="주장_대장_테스트가_적어_둔_프로덕션_갭이_아직_그대로다"/>) —
    ///    "프로덕션에 이것이 없다"는 <b>현재형</b> 문장을 골라, 그 부재를 <b>기계가 매 실행 다시 잰다</b>.
    ///    갭이 닫히면 <b>실패</b>해서 문장을 지우게 한다. 안 닫힌 동안은 초록이지만 대장이 러너 로그에
    ///    전문을 찍어 목록이 눈에 남는다.</item>
    ///  <item><b>Ignore 명부</b>(<see cref="Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다"/>) —
    ///    <c>Assert</c>.<c>Ignore</c>를 쓰는 테스트를 전수해 <b>역방향 장치</b>(갭이 닫히면 스스로
    ///    빨개지는가)를 명부로 잠근다. 장치가 <b>없는</b> 항목 수는 늘 수 없다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 왜 정규식 <c>\b</c>를 한 번도 쓰지 않는가 (R1의 자기 고백을 반복하지 않기 위해)
    /// ============================================================================
    /// .NET의 <c>\b</c>는 <b>유니코드 낱말 경계</b>라 한글도 낱말 문자로 센다. R1의 첫 판이 정확히
    /// 이 함정에 걸려 참조 534건 중 <b>245건(46%)</b>을 놓쳤고, 놓친 것 안에 진짜 위반이 있었다.
    /// 이 파일은 한글 문장을 훑으므로 같은 함정에 다시 걸릴 수 있다. 그래서
    /// <b>정규식을 아예 쓰지 않고</b> <see cref="ContainsIdentifier"/>가 앞뒤 한 글자만 보고
    /// <c>[A-Za-z0-9_]</c>가 아니면 경계로 인정한다(한글이 붙어도 매치가 사라지지 않는다).
    ///
    /// <para><b>애셋 탐지도 같은 함정이 있다</b>: <c>.asset</c>은 스크립트를 <b>GUID</b>로 참조하므로
    /// 타입 이름을 <c>grep</c>하면 <b>탐지력이 애초에 0</b>이다(TEAM.md 4절 사고 #4의 형태).
    /// 그래서 <c>.cs.meta</c>에서 GUID를 읽어서 찾고, <see cref="양성대조_스캐너들이_있는_것을_실제로_찾는다"/>가
    /// <c>AccessoryDefSO</c> GUID로 <b>42개</b>를 실제로 찾아 보이며 그 능력을 매 실행 증명한다.</para>
    ///
    /// ============================================================================
    /// ★ 명부 길이의 상한 — 명부가 길어지면 감사가 아니라 부채 목록이다
    /// ============================================================================
    /// 주장 대장은 <see cref="MaxClaims"/>(8)을 넘을 수 없다. 넘으려는 라운드는 항목을 더할 것이
    /// 아니라 <b>리더에게 보고</b>해야 한다 — 8건을 넘는 "알고도 안 고친 프로덕션 갭"은 개별 항목의
    /// 문제가 아니라 배정의 문제다.
    ///
    /// <para><b>이 파일은 프로덕션 타입을 하나도 참조하지 않는다.</b> 전부 소스 텍스트 스캔이다 —
    /// 활성 빌드 타깃 반대편 파일은 타입이 존재하지 않아 리플렉션으로는 영원히 못 보기 때문이다
    /// (CLAUDE.md 활성 빌드 타깃 규칙).</para>
    /// </summary>
    public sealed class TestClaimExpiryAuditTests
    {
        private const string LogPrefix = "[주장만료]";

        /// <summary>주장 대장의 상한. 이 숫자의 근거는 클래스 문서 마지막 절.</summary>
        private const int MaxClaims = 8;

        /// <summary>역방향 장치가 <b>없는</b> Ignore의 상한. 늘어나는 방향으로는 열리지 않는다.</summary>
        private const int MaxIgnoresWithoutRatchet = 1;

        private static string ScriptsRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string TestsRoot => Path.Combine(ScriptsRoot, "Tests");

        // ====================================================================
        // 소스 도구 — 정규식 없음(한글 안전), 주석 제거 있음(주석 속 언급은 배선이 아니다)
        // ====================================================================

        /// <summary>
        /// C# 주석(<c>//</c>, <c>/* */</c>)만 지우고 <b>문자열/문자 리터럴은 보존</b>한다.
        /// <para>왜 필요한가: <c>FootholdPoller.cs</c>는 <c>FootholdScanPolicy</c>를 <b>주석에서만</b>
        /// 언급한다. 주석을 안 지우면 "배선됐다"로 잘못 읽고 이 대장이 통째로 거짓 실패한다.</para>
        /// </summary>
        internal static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            int i = 0, n = src.Length;
            bool inString = false, verbatim = false;
            char quote = '"';

            while (i < n)
            {
                char c = src[i];

                if (!inString)
                {
                    if (c == '/' && i + 1 < n && src[i + 1] == '/')
                    {
                        while (i < n && src[i] != '\n') i++;
                        continue;
                    }
                    if (c == '/' && i + 1 < n && src[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                        i = Math.Min(i + 2, n);
                        continue;
                    }
                    if (c == '"' || c == '\'')
                    {
                        inString = true;
                        quote = c;
                        verbatim = c == '"' && i > 0 && src[i - 1] == '@';
                        sb.Append(c);
                        i++;
                        continue;
                    }
                    sb.Append(c);
                    i++;
                    continue;
                }

                if (!verbatim && c == '\\' && i + 1 < n)
                {
                    sb.Append(c).Append(src[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == quote)
                {
                    if (verbatim && i + 1 < n && src[i + 1] == quote)
                    {
                        sb.Append(c).Append(c);
                        i += 2;
                        continue;
                    }
                    inString = false;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// <paramref name="needle"/>이 <b>식별자 하나로</b> 들어 있는가.
        /// <para>★ 정규식 <c>\b</c>를 쓰지 않는다 — .NET의 <c>\b</c>는 한글을 낱말 문자로 세어
        /// <c>StickConfig.cs가</c> 같은 조사 결합에서 경계가 서지 않는다(R1이 46% 미탐했던 그 함정).
        /// 여기서는 앞뒤 한 글자가 <c>[A-Za-z0-9_]</c>인지만 본다. 한글·괄호·점은 전부 경계다.</para>
        /// <para>이 방향(부분일치 배제)이 필요한 이유: <c>ResolveInk</c>를 그냥 찾으면
        /// <c>ResolveInkColor</c>가 걸려 "배선됐다"는 거짓 판정이 난다.</para>
        /// </summary>
        internal static bool ContainsIdentifier(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;

            int at = 0;
            while (true)
            {
                at = haystack.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0) return false;

                bool leftOk = at == 0 || !IsIdentifierChar(haystack[at - 1]);
                int end = at + needle.Length;
                bool rightOk = end >= haystack.Length || !IsIdentifierChar(haystack[end]);
                if (leftOk && rightOk) return true;

                at = end;
            }
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>프로덕션 <c>.cs</c> 전부(<c>Tests/</c> 제외). 파일명을 하드코딩하지 않는다 —
        /// 파일을 쪼개면 눈이 머는 감사를 이 저장소가 이미 두 번 만들었다.</summary>
        private static string[] ProductionFiles()
        {
            string[] all = Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
            var keep = new List<string>(all.Length);
            string testsMark = Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar;
            foreach (string p in all)
            {
                if (p.IndexOf(testsMark, StringComparison.Ordinal) >= 0) continue;
                keep.Add(p);
            }
            keep.Sort(StringComparer.Ordinal);
            return keep.ToArray();
        }

        /// <summary>
        /// 한 클래스가 <b>쪼개져도</b> 따라가는 파일 묶음. <paramref name="stem"/>으로 시작하는
        /// 프로덕션 파일 전부를 돌려준다.
        /// <para>★ 이 저장소는 <b>파일명으로 소스를 찾는 감사가 파일 분할에 눈이 먼 사고를 두 번</b>
        /// 겪었다. 지금 이 트리에도 <c>CharacterInfoWindow.Cards.cs</c>처럼 부분 클래스로 쪼개진
        /// 예가 이미 있다. 그래서 "그 파일 하나"가 아니라 "그 이름으로 시작하는 전부"를 본다.</para>
        /// </summary>
        private static List<string> ProductionFilesWithStem(string stem)
        {
            var found = new List<string>();
            foreach (string p in ProductionFiles())
            {
                if (Path.GetFileName(p).StartsWith(stem, StringComparison.Ordinal)) found.Add(p);
            }
            return found;
        }

        /// <summary>주석을 지운 프로덕션 소스에서 <paramref name="ident"/>를 식별자로 쓰는 파일 목록.
        /// <paramref name="exceptBaseNames"/>는 선언처(자기 자신)를 뺀다.</summary>
        private static List<string> ProductionUsers(string ident, params string[] exceptBaseNames)
        {
            var found = new List<string>();
            foreach (string p in ProductionFiles())
            {
                string baseName = Path.GetFileName(p);
                bool skip = false;
                for (int i = 0; i < exceptBaseNames.Length; i++)
                {
                    if (string.Equals(baseName, exceptBaseNames[i], StringComparison.Ordinal)) { skip = true; break; }
                }
                if (skip) continue;

                if (ContainsIdentifier(StripComments(File.ReadAllText(p)), ident)) found.Add(baseName);
            }
            return found;
        }

        /// <summary><c>.cs.meta</c>의 GUID를 그 이름으로 참조하는 <c>.asset</c> 개수.
        /// <para>★ 타입 이름으로 <c>.asset</c>을 찾으면 <b>영원히 0건</b>이다 — 애셋은 스크립트를
        /// GUID로만 가리킨다. 이 함수의 탐지력은 <see cref="양성대조_스캐너들이_있는_것을_실제로_찾는다"/>가
        /// 매 실행 증명한다.</para></summary>
        private static int AssetsReferencingScript(string relativeScriptPath, out string guid)
        {
            guid = null;
            string meta = Path.Combine(ScriptsRoot, relativeScriptPath) + ".meta";
            if (!File.Exists(meta)) return -1;

            foreach (string line in File.ReadAllLines(meta))
            {
                if (!line.StartsWith("guid:", StringComparison.Ordinal)) continue;
                guid = line.Substring("guid:".Length).Trim();
                break;
            }
            if (string.IsNullOrEmpty(guid)) return -1;

            int count = 0;
            foreach (string p in Directory.GetFiles(Application.dataPath, "*.asset", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(p).IndexOf(guid, StringComparison.Ordinal) >= 0) count++;
            }
            return count;
        }

        private static string FindTestFile(string fileName)
        {
            string[] hits = Directory.GetFiles(TestsRoot, fileName, SearchOption.AllDirectories);
            return hits.Length == 1 ? hits[0] : null;
        }

        // ====================================================================
        // 1. 주장 대장
        // ====================================================================

        /// <summary>테스트 파일이 적어 둔 <b>현재형 부재 주장</b> 하나.</summary>
        private struct Claim
        {
            /// <summary>보고서와 대조하기 위한 번호(R2 배정표와 같은 이름).</summary>
            public string Id;

            /// <summary>그 문장이 있는 테스트 파일(경로 아닌 <b>파일명</b> — 쪼개고 옮겨도 찾는다).</summary>
            public string TestFile;

            /// <summary>그 파일에 <b>반드시 그대로 있어야</b> 하는 문장 조각. 사라지면 실패한다
            /// (문장이 없어졌는데 대장만 남으면 대장이 거짓말을 하게 된다).</summary>
            public string Anchor;

            /// <summary>갭이 <b>닫혔으면</b> 그 근거 문장을, 아직 열려 있으면 <c>null</c>을 돌려준다.</summary>
            public Func<string> ClosedBecause;

            /// <summary>사람에게 남기는 한 줄 — 무엇이 없고, 고치면 무슨 일이 일어나는가.</summary>
            public string Note;
        }

        private static Claim[] Ledger()
        {
            return new[]
            {
                new Claim
                {
                    Id = "R2-1",
                    TestFile = "AppearanceShapeBudgetTests.cs",
                    Anchor = "CharacterFxRenderer는 ItemCatalog.ResolveWornPalette를 부르지 않아",
                    Note = "FX 월드 도형에 보조색이 구조적으로 없다(렌더러 4종 중 이것만). " +
                           "카드에는 있는 색이 착용하면 사라진다.",
                    ClosedBecause = () =>
                    {
                        List<string> parts = ProductionFilesWithStem("CharacterFxRenderer");
                        if (parts.Count == 0)
                            return "CharacterFxRenderer로 시작하는 프로덕션 파일이 하나도 없다 — " +
                                   "이름이 바뀌었다면 이 항목도 함께 고쳐라(그대로 두면 아무것도 안 잰다)";

                        foreach (string p in parts)
                        {
                            if (ContainsIdentifier(StripComments(File.ReadAllText(p)), "ResolveWornPalette"))
                                return $"{Path.GetFileName(p)}가 이제 ResolveWornPalette를 부른다";
                        }
                        return null;
                    },
                },
                new Claim
                {
                    Id = "R2-2",
                    TestFile = "FootholdScanPolicyTests.cs",
                    Anchor = "이 규칙은 지금 제품 코드에 배선돼 있지 않다",
                    Note = "Platform/FootholdScanPolicy.cs 380줄이 프로덕션 호출 0건. " +
                           "테스트만 통과하는 죽은 코드다(배선하지 않기로 한 근거는 그 파일 문서).",
                    ClosedBecause = () =>
                    {
                        List<string> users = ProductionUsers("FootholdScanPolicy", "FootholdScanPolicy.cs");
                        return users.Count > 0
                            ? "FootholdScanPolicy를 부르는 프로덕션 파일이 생겼다: " + string.Join(", ", users)
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-3",
                    TestFile = "StickmanStateIdWireFormatTests.cs",
                    Anchor = "지금은 매니페스트 에셋이 0개라 피해가 없다",
                    Note = "MotionPluginSO/EffectPluginSO는 소비자 0 · 애셋 0. " +
                           "CLAUDE.md 원칙 4(플러그인 구조)가 아직 구조로 서 있지 않다는 뜻이고, " +
                           "첫 팩이 나가는 순간 이 문장의 '피해가 없다'가 거짓이 된다.",
                    ClosedBecause = () =>
                    {
                        int motion = AssetsReferencingScript(Path.Combine("Plugins", "MotionPluginSO.cs"), out _);
                        int effect = AssetsReferencingScript(Path.Combine("Plugins", "EffectPluginSO.cs"), out _);
                        if (motion < 0 || effect < 0)
                        {
                            return "MotionPluginSO/EffectPluginSO의 .cs.meta에서 GUID를 못 읽었다 — " +
                                   "파일이 옮겨졌다면 이 대장의 경로를 함께 고쳐라(못 읽은 채로 두면 " +
                                   "이 항목은 아무것도 재지 않는다)";
                        }
                        if (motion + effect > 0)
                            return $"DLC 매니페스트 애셋이 생겼다(Motion {motion}개 / Effect {effect}개)";

                        List<string> users = ProductionUsers("MotionPluginSO", "MotionPluginSO.cs");
                        users.AddRange(ProductionUsers("EffectPluginSO", "EffectPluginSO.cs"));
                        return users.Count > 0
                            ? "플러그인 매니페스트를 읽는 프로덕션 파일이 생겼다: " + string.Join(", ", users)
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-4",
                    TestFile = "PackPaletteGateTests.cs",
                    Anchor = "팩 12색은 <b>아직 프로덕션에 없다</b>",
                    Note = "게이트가 재는 12색이 프로덕션 상수가 아니라 테스트 안의 동결 대장이다. " +
                           "매니페스트가 생기면 게이트는 대장이 아니라 매니페스트를 재야 한다 — " +
                           "안 바꾸면 게이트가 '자기가 적은 값'을 검사하게 된다.",
                    ClosedBecause = () =>
                    {
                        List<string> users = ProductionUsers("PackPalette");
                        return users.Count > 0
                            ? "프로덕션에 팩 팔레트가 생겼다: " + string.Join(", ", users)
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-5",
                    TestFile = "ItemCatalogAssetParityTests.cs",
                    Anchor = "A단계에서 이 필드를 읽는 코드는",
                    Note = "AccessoryDefSO.hidesHair는 선언만 있고 읽는 프로덕션 코드가 0건. " +
                           "값이 틀려도 화면이 멀쩡하므로 이 테스트가 유일한 파수꾼이다.",
                    ClosedBecause = () =>
                    {
                        List<string> users = ProductionUsers("hidesHair", "AccessoryDefSO.cs");
                        return users.Count > 0
                            ? "hidesHair를 읽는 프로덕션 파일이 생겼다: " + string.Join(", ", users)
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-6",
                    TestFile = "CloseChipAffordanceTests.cs",
                    Anchor = "hover/pressed는 미배선",
                    Note = "UiChrome.ChromeButtonSurfaceHover/Pressed는 선언과 대비 검사만 있고 " +
                           "화면에 나가는 소비자가 0건. 두 색은 지금 아무 데도 안 쓰인다.",
                    ClosedBecause = () =>
                    {
                        List<string> users = ProductionUsers("ChromeButtonSurfaceHover", "UiChrome.cs");
                        users.AddRange(ProductionUsers("ChromeButtonSurfacePressed", "UiChrome.cs"));
                        return users.Count > 0
                            ? "hover/pressed 색을 쓰는 프로덕션 파일이 생겼다: " + string.Join(", ", users)
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-7",
                    TestFile = "TopmostBandOcclusionTests.cs",
                    Anchor = "어떤 Win32 쓰기에도 연결돼 있지 않다",
                    Note = "★ 이 항목은 문장보다 나쁘다. 규칙은 '모의판정'으로만 불리는데 그 호출이 " +
                           "characterInsideBar를 리터럴 false로 넘긴다 — ShouldRaiseWithinBand는 " +
                           "그 값이 false면 무조건 false를 돌려주므로 로그의 '모의판정'은 " +
                           "측정이 아니라 상수다. 배선 여부를 그 로그로 판단하면 안 된다.",
                    ClosedBecause = () =>
                    {
                        int callSites = 0, hardcodedFalse = 0;
                        foreach (string p in ProductionFiles())
                        {
                            string s = StripComments(File.ReadAllText(p));
                            int at = 0;
                            while (true)
                            {
                                at = s.IndexOf("ShouldRaiseWithinBand", at, StringComparison.Ordinal);
                                if (at < 0) break;
                                int after = at + "ShouldRaiseWithinBand".Length;
                                at = after;

                                // 선언(`bool ShouldRaiseWithinBand(`)은 호출이 아니다.
                                int lineStart = s.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
                                string head = s.Substring(lineStart, Math.Max(0, at - lineStart));
                                if (head.IndexOf("static bool", StringComparison.Ordinal) >= 0) continue;

                                callSites++;
                                int len = Math.Min(400, s.Length - after);
                                string tail = s.Substring(after, len);
                                if (tail.IndexOf("characterInsideBar: false", StringComparison.Ordinal) >= 0
                                    || tail.IndexOf("characterInsideBar:false", StringComparison.Ordinal) >= 0)
                                {
                                    hardcodedFalse++;
                                }
                            }
                        }
                        if (callSites == 0)
                            return "ShouldRaiseWithinBand의 프로덕션 호출이 사라졌다 — 규칙이 통째로 죽었다";
                        return hardcodedFalse < callSites
                            ? $"호출 {callSites}곳 중 {callSites - hardcodedFalse}곳이 " +
                              "characterInsideBar에 진짜 값을 넘긴다 — 훅이 생겼다"
                            : null;
                    },
                },
                new Claim
                {
                    Id = "R2-8",
                    TestFile = "InfoGearMeshingTests.cs",
                    Anchor = "작은 기어가 없어졌다",
                    Note = "★ 이 항목은 Ignore 명부와 짝이다. InfoGearMeshingTests의 Ignore 2건은 " +
                           "역방향 장치가 하나도 없어, 두 기어가 되살아나도 영원히 '건너뜀'으로 남는다. " +
                           "여기서 대신 잰다 — 기어 외곽선을 두 번 만들면 그 두 단언을 되살려야 한다.",
                    ClosedBecause = () =>
                    {
                        List<string> parts = ProductionFilesWithStem("InfoGearIconWidget");
                        if (parts.Count == 0)
                            return "InfoGearIconWidget으로 시작하는 프로덕션 파일이 하나도 없다 — " +
                                   "이름이 바뀌었다면 이 항목도 함께 고쳐라";

                        int count = 0;
                        foreach (string p in parts)
                        {
                            string s = StripComments(File.ReadAllText(p));
                            int at = 0;
                            while (true)
                            {
                                at = s.IndexOf("BuildGearOutline", at, StringComparison.Ordinal);
                                if (at < 0) break;
                                count++;
                                at += "BuildGearOutline".Length;
                            }
                        }
                        // 선언 1 + 호출 1 = 2가 "기어 하나"의 형태다.
                        return count > 2
                            ? $"기어 외곽선을 {count - 1}번 만든다 — 기어가 늘었다"
                            : null;
                    },
                },
            };
        }

        /// <summary>
        /// ★ 본론 — 테스트가 적어 둔 <b>부재 주장</b>이 아직 참인가를 매 실행 다시 잰다.
        ///
        /// <para><b>양방향으로 잠근다.</b>
        /// ① 갭이 <b>닫혔는데</b> 문장이 남아 있으면 실패한다(그 문장은 그 순간부터 거짓 주석이다 —
        ///    R1이 잡는 그 형태로 <b>변신</b>한다).
        /// ② 문장이 <b>사라졌는데</b> 대장에 남아 있어도 실패한다(대장이 없는 문장을 지키게 된다).</para>
        /// </summary>
        [Test]
        public void 주장_대장_테스트가_적어_둔_프로덕션_갭이_아직_그대로다()
        {
            Claim[] claims = Ledger();

            // ★ 비공허성 잠금 ① — 대장이 비면 아래 foreach가 아무것도 안 재고 초록이 된다(거짓 통과 #5).
            Assert.IsNotEmpty(claims,
                $"{LogPrefix} 주장 대장이 비었습니다. 정말로 남은 항목이 0건이라면 이 파일을 지우세요 — " +
                "빈 대장은 아무것도 재지 않으면서 러너에 초록 한 줄을 더합니다.");

            // ★ 상한 — 명부가 길어지면 그건 감사가 아니라 부채 목록이다.
            Assert.LessOrEqual(claims.Length, MaxClaims,
                $"{LogPrefix} 주장 대장이 {claims.Length}건으로 상한 {MaxClaims}건을 넘었습니다. " +
                "항목을 더하지 말고 리더에게 보고하세요 — 이 숫자가 넘친다는 것은 개별 갭의 문제가 " +
                "아니라 '알고도 안 고친 것'이 배정되지 않고 쌓인다는 뜻입니다.");

            // ★ 비공허성 잠금 ② — 프로덕션 스캔 대상이 실제로 있는가.
            int prodCount = ProductionFiles().Length;
            Assert.Greater(prodCount, 100,
                $"{LogPrefix} 프로덕션 .cs를 {prodCount}개밖에 읽지 못했습니다({ScriptsRoot}) — " +
                "경로가 바뀌었다면 ScriptsRoot를 고치세요. 그대로 두면 모든 '아직 없다' 판정이 " +
                "'아무것도 안 봤다'가 됩니다(거짓 통과).");

            var expired = new List<string>();
            var lostAnchor = new List<string>();
            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" 살아 있는 주장 ").Append(claims.Length).Append("건\n");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Claim c in claims)
            {
                Assert.IsTrue(seenIds.Add(c.Id), $"{LogPrefix} 대장에 같은 번호({c.Id})가 둘 있습니다.");

                string path = FindTestFile(c.TestFile);
                if (path == null)
                {
                    lostAnchor.Add($"  · {c.Id}: 테스트 파일 {c.TestFile}을 Tests/ 아래에서 유일하게 " +
                        "찾지 못했습니다(0개이거나 2개 이상). 파일을 옮겼다면 대장도 함께 고치세요.");
                    continue;
                }

                string source = File.ReadAllText(path);
                if (source.IndexOf(c.Anchor, StringComparison.Ordinal) < 0)
                {
                    lostAnchor.Add($"  · {c.Id}: {c.TestFile}에서 문장이 사라졌습니다 → \"{c.Anchor}\"\n" +
                        "      갭이 닫혀서 지운 것이라면 이 대장 항목도 함께 지우세요. " +
                        "문구만 다듬은 것이라면 Anchor를 갱신하세요. " +
                        "그냥 두면 대장이 '없는 문장'을 지키는 셈이 됩니다.");
                    continue;
                }

                string closed = c.ClosedBecause();
                report.Append(closed == null ? "  열림  " : "  ★닫힘 ")
                      .Append(c.Id).Append('\t').Append(c.TestFile).Append('\n')
                      .Append("         ").Append(c.Note).Append('\n');

                if (closed != null)
                {
                    expired.Add($"  · {c.Id} ({c.TestFile})\n      {closed}\n" +
                        $"      → 이제 이 문장은 <b>거짓</b>입니다: \"{c.Anchor}\"\n" +
                        "      그 문장을 고치거나 지우고, 이 대장에서도 항목을 빼세요. " +
                        "남겨 두면 다음 사람이 이미 있는 기능을 '없다'고 읽습니다(R1이 잡는 그 형태).");
                }
            }

            Debug.Log(report.ToString());

            Assert.IsEmpty(lostAnchor,
                $"{LogPrefix} 대장이 가리키는 문장을 찾지 못했습니다({lostAnchor.Count}건):\n" +
                string.Join("\n", lostAnchor));

            Assert.IsEmpty(expired,
                $"{LogPrefix} <b>고쳐졌는데 문장이 남았습니다</b>({expired.Count}건). " +
                "이건 축하할 실패입니다 — 갭이 닫혔다는 뜻입니다:\n" + string.Join("\n", expired));
        }

        // ====================================================================
        // 2. Ignore 명부
        // ====================================================================

        /// <summary>역방향 장치의 종류. 이름 자체가 "무엇이 이 항목을 되살릴 것인가"를 말한다.</summary>
        private enum RatchetKind
        {
            /// <summary>같은 메서드가 갭이 닫히면 스스로 빨개진다(Assert.Pass / 상한 래칫 등).</summary>
            자동,

            /// <summary>다른 테스트가 반대 방향으로 잰다. <see cref="IgnoreEntry.Companion"/>에 그 이름.</summary>
            동반,

            /// <summary>아무 장치도 없다. ★ 이 개수는 늘 수 없다.</summary>
            없음,
        }

        private struct IgnoreEntry
        {
            public string File;
            public string Method;
            public RatchetKind Kind;

            /// <summary><see cref="RatchetKind.동반"/>일 때 그 동반 테스트의 <b>메서드 이름</b>.
            /// 실재 여부를 이 감사가 확인한다 — 이름만 적고 안심하는 것을 막는다.</summary>
            public string Companion;

            public string Why;
        }

        /// <summary>
        /// <b>자기 대장을 이미 가진 파일</b>은 여기서 중복해 세지 않는다.
        /// <para><c>PlatformParityAuditTests</c>는 <c>감사_대장_모든_항목이_분류표에_들어_있다</c>가
        /// 접두사·건너뜀·사유 날짜를 기계적으로 강제한다. 그 장치가 <b>사라지면</b> 이 면제도
        /// 사라져야 하므로, 면제의 근거를 <see cref="Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다"/>가
        /// 매 실행 확인한다(이름이 그 파일에 실제로 있는가).</para>
        /// </summary>
        private static readonly (string File, string GovernedBy)[] SelfGoverned =
        {
            ("PlatformParityAuditTests.cs", "감사_대장_모든_항목이_분류표에_들어_있다"),
        };

        private static IgnoreEntry[] IgnoreInventory()
        {
            return new[]
            {
                new IgnoreEntry
                {
                    File = "AppearanceShapeBudgetTests.cs",
                    Method = "PET은_정원과_보조색_규칙을_그대로_지킨다",
                    Kind = RatchetKind.동반,
                    Companion = "아직_미완_커서친구는_머리와_꼬리로_안_쪼개졌다",
                    Why = "커서친구를 머리/꼬리로 쪼개면 동반 테스트가 먼저 빨개진다.",
                },
                new IgnoreEntry
                {
                    File = "AppearanceShapeBudgetTests.cs",
                    Method = "최단_실제_변_검사를_액세서리_30종으로_확장한다",
                    Kind = RatchetKind.자동,
                    Why = "실측 14건을 상한으로 든 래칫 + 0이 되면 Ignore를 지나 초록이 된다.",
                },
                new IgnoreEntry
                {
                    File = "ComicFontFloorOutlineRingTests.cs",
                    Method = "배율1에서는_속공간이_검증_운용점에_못_미친다_보류",
                    Kind = RatchetKind.자동,
                    Why = "속공간이 운용점을 채우면 Assert.Pass로 빠져 '실단언으로 바꾸라'고 말한다.",
                },
                new IgnoreEntry
                {
                    File = "CommentReferenceAuditTests.cs",
                    Method = "이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다",
                    Kind = RatchetKind.자동,
                    Why = "R1의 명부 래칫 — 참조가 고쳐지면 Ignore 앞에서 실패한다.",
                },
                new IgnoreEntry
                {
                    File = "SuspendClickBlockerAuditTests.cs",
                    Method = "차단막_소유자는_등급1_창구를_읽는다",
                    Kind = RatchetKind.동반,
                    Companion = "미배선_목록은_실제로_존재하고_아직_배선되지_않은_파일만_담는다",
                    Why = "배선이 끝났는데 명부만 남으면 동반 테스트가 실패한다(S1 라운드의 역방향 단언).",
                },
                new IgnoreEntry
                {
                    File = "InfoGearMeshingTests.cs",
                    Method = "GearRatioAndCenterDistanceFollowRealGearGeometry",
                    Kind = RatchetKind.동반,
                    Companion = "주장_대장_테스트가_적어_둔_프로덕션_갭이_아직_그대로다",
                    Why = "2026-09-02 R2까지 <b>장치가 없었다</b>(무조건 Ignore). " +
                          "이 파일의 주장 대장 R2-8이 기어 개수를 대신 재어 되살릴 시점을 알린다.",
                },
                new IgnoreEntry
                {
                    File = "InfoGearMeshingTests.cs",
                    Method = "TwoGearsSpinInOppositeDirectionsAtTheToothRatio",
                    Kind = RatchetKind.동반,
                    Companion = "주장_대장_테스트가_적어_둔_프로덕션_갭이_아직_그대로다",
                    Why = "위와 같다.",
                },
                new IgnoreEntry
                {
                    File = "PortraitTextureResolutionTests.cs",
                    Method = "PortraitTextureIsSupersampledAgainstPhysicalPixelsOnRetina",
                    Kind = RatchetKind.없음,
                    Why = "헤드리스(-nographics) 환경 가드다. 닫힐 '갭'이 없고 " +
                          "환경이 바뀌면 저절로 실검사로 돈다 — 래칫을 붙일 대상이 아니다.",
                },
            };
        }

        /// <summary>
        /// ★ <c>Assert</c>.<c>Ignore</c>를 쓰는 테스트를 <b>전수</b>해 명부와 대조한다.
        ///
        /// <para>이 저장소는 "못 고친 갭은 <c>Ignore</c>로 남겨 러너에 계속 보이게 한다"를 관례로
        /// 정했다(CLAUDE.md). 그런데 <b>되살릴 장치가 없는 Ignore</b>는 그 관례의 반대다 —
        /// 갭이 닫혀도 영원히 "건너뜀"으로 남아 러너에서 사실상 사라진다.</para>
        ///
        /// <para>양방향: 명부에 없는 Ignore가 새로 생기면 실패하고(등록 강제),
        /// 명부에 있는데 실물이 없어져도 실패한다(자동 만료).</para>
        ///
        /// <para><b>왜 이 파일 자신은 걸리지 않는가</b>: 찾는 토큰을 소스에 통째로 적지 않고 두 조각으로
        /// 이어 붙인다 — <c>PlatformParityAuditTests</c>가 쓰는 것과 같은 수법이고, 자기 자신을
        /// 예외 목록에 넣는 것보다 정직하다(예외 목록은 언젠가 커진다).</para>
        /// </summary>
        [Test]
        public void Ignore를_쓰는_테스트는_전부_명부에_있고_장치없음이_늘지_않는다()
        {
            string ignoreToken = "Assert" + ".Ignore(";     // ← 이 파일에 문자 그대로 적지 않는다.

            string[] testFiles = Directory.GetFiles(TestsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(testFiles.Length, 100,
                $"{LogPrefix} Tests/ 아래에서 .cs를 {testFiles.Length}개밖에 읽지 못했습니다 — " +
                "스캔이 공허합니다(거짓 초록).");

            // 자기 대장을 가진 파일의 그 장치가 실제로 살아 있는가.
            var governedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string file, string governedBy) in SelfGoverned)
            {
                string p = FindTestFile(file);
                Assert.IsNotNull(p,
                    $"{LogPrefix} 자기 대장 면제 대상 {file}을 찾지 못했습니다 — 면제를 지우거나 경로를 고치세요.");
                Assert.IsTrue(File.ReadAllText(p).IndexOf(governedBy, StringComparison.Ordinal) >= 0,
                    $"{LogPrefix} {file}에서 자기 대장({governedBy})이 사라졌습니다. " +
                    "그 파일의 Ignore들은 지금 아무 규칙도 안 받습니다 — 면제를 거두고 " +
                    "이 명부에 옮겨 담으세요.");
                governedFiles.Add(file);
            }

            // 실물 수집.
            var found = new List<(string File, string Method)>();
            int tokenHitFiles = 0;
            foreach (string path in testFiles)
            {
                string src = File.ReadAllText(path).Replace("\r\n", "\n");
                if (src.IndexOf(ignoreToken, StringComparison.Ordinal) < 0) continue;
                tokenHitFiles++;

                string fileName = Path.GetFileName(path);
                if (governedFiles.Contains(fileName)) continue;

                foreach ((string method, string body) in TestMethods(src))
                {
                    if (body.IndexOf(ignoreToken, StringComparison.Ordinal) >= 0)
                        found.Add((fileName, method));
                }
            }

            // ★ 비공허성 잠금 — 토큰이 한 파일에서도 안 걸리면 조립이 깨진 것이다.
            Assert.Greater(tokenHitFiles, 0,
                $"{LogPrefix} 어떤 테스트 파일에서도 '{ignoreToken}'를 찾지 못했습니다. " +
                "이 저장소는 그 관례를 쓰고 있으므로 0건은 '없다'가 아니라 '스캐너가 눈이 멀었다'입니다.");

            IgnoreEntry[] inventory = IgnoreInventory();
            Assert.IsNotEmpty(inventory, $"{LogPrefix} Ignore 명부가 비었습니다 — 아래 대조가 공허해집니다.");

            var expected = new HashSet<string>(StringComparer.Ordinal);
            int noRatchet = 0;
            var problems = new List<string>();

            foreach (IgnoreEntry e in inventory)
            {
                expected.Add(e.File + "::" + e.Method);
                if (e.Kind == RatchetKind.없음) noRatchet++;

                if (e.Kind != RatchetKind.동반) continue;

                Assert.IsNotEmpty(e.Companion,
                    $"{LogPrefix} {e.File}::{e.Method}가 '동반'인데 동반 테스트 이름이 비었습니다.");

                // ★ 이름이 <b>어딘가에 적혀 있는가</b>가 아니라 <b>메서드로 선언되어 있는가</b>를 본다.
                //   자기 고백: 첫 판은 문자열 포함으로만 봤고, 그러면 <b>이 명부 자신</b>에 적힌
                //   그 이름이 걸려서 검사가 통째로 공허해진다(동반 테스트를 지워도 초록이었다 —
                //   돌연변이 M11에서 실제로 그렇게 통과했다). 선언으로 보면 문자열 리터럴은 안 걸린다.
                bool companionDeclared = false;
                foreach (string path in testFiles)
                {
                    string body = File.ReadAllText(path).Replace("\r\n", "\n");
                    if (body.IndexOf(e.Companion, StringComparison.Ordinal) < 0) continue;

                    foreach (string line in body.Split('\n'))
                    {
                        if (!string.Equals(MethodNameOrNull(line), e.Companion, StringComparison.Ordinal)) continue;
                        companionDeclared = true;
                        break;
                    }
                    if (companionDeclared) break;
                }
                if (!companionDeclared)
                {
                    problems.Add($"  · {e.File}::{e.Method}\n      동반 테스트 '{e.Companion}'가 " +
                        "Tests/ 어디에도 <b>메서드로 선언되어</b> 있지 않습니다 — 이 Ignore를 되살릴 " +
                        "장치가 사실은 없습니다(이름만 남았습니다).");
                }
            }

            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string file, string method) in found) actual.Add(file + "::" + method);

            foreach (string key in actual)
            {
                if (expected.Contains(key)) continue;
                problems.Add($"  · {key}\n      명부에 없는 Ignore가 새로 생겼습니다. " +
                    "명부(IgnoreInventory)에 등록하고 <b>역방향 장치</b>를 함께 적으세요 — " +
                    "갭이 닫혔을 때 이 테스트를 되살릴 방법이 무엇인지. 장치가 없다면 " +
                    "RatchetKind.없음으로 등록해야 하고, 그러면 아래 상한에 걸립니다.");
            }

            foreach (string key in expected)
            {
                if (actual.Contains(key)) continue;
                problems.Add($"  · {key}\n      명부에는 있는데 실물이 없습니다 — Ignore를 지웠다면 " +
                    "명부에서도 지우세요(자동 만료). 메서드 이름만 바꿨다면 명부를 갱신하세요.");
            }

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" Ignore 명부 ").Append(inventory.Length)
                  .Append("건(자기 대장 파일 ").Append(governedFiles.Count).Append("개 제외)\n");
            foreach (IgnoreEntry e in inventory)
            {
                report.Append("  ").Append(e.Kind).Append('\t').Append(e.File).Append("::")
                      .Append(e.Method).Append('\n').Append("      ").Append(e.Why).Append('\n');
            }
            Debug.Log(report.ToString());

            Assert.IsEmpty(problems,
                $"{LogPrefix} Ignore 명부가 실물과 어긋났습니다({problems.Count}건):\n" +
                string.Join("\n", problems));

            Assert.LessOrEqual(noRatchet, MaxIgnoresWithoutRatchet,
                $"{LogPrefix} 역방향 장치가 <b>없는</b> Ignore가 {noRatchet}건입니다(상한 " +
                $"{MaxIgnoresWithoutRatchet}건). 이런 항목은 갭이 닫혀도 영원히 '건너뜀'으로 남아 " +
                "러너에서 사실상 사라집니다 — CLAUDE.md가 Ignore 관례를 만든 이유가 정확히 " +
                "그것을 막기 위해서였습니다. 새 Ignore에는 '무엇이 이것을 되살리는가'를 함께 다세요.");
        }

        /// <summary>테스트 메서드(<c>public void</c> / <c>public IEnumerator</c>)를 이름과 본문으로 자른다.
        /// <para>다음 메서드 선언까지를 본문으로 본다 — 중괄호를 세는 것보다 단순하고, 이 저장소의
        /// 서식(메서드 사이에 빈 줄)에서 충분하다. 스캐너가 실제로 자르는지는 네거티브 컨트롤이 잰다.</para></summary>
        internal static List<(string Method, string Body)> TestMethods(string source)
        {
            var result = new List<(string, string)>();
            string[] lines = source.Split('\n');

            var starts = new List<(int Line, string Name)>();
            for (int i = 0; i < lines.Length; i++)
            {
                string name = MethodNameOrNull(lines[i]);
                if (name != null) starts.Add((i, name));
            }

            for (int k = 0; k < starts.Count; k++)
            {
                int from = starts[k].Line;
                int to = k + 1 < starts.Count ? starts[k + 1].Line : lines.Length;
                var sb = new StringBuilder();
                for (int i = from; i < to; i++) sb.Append(lines[i]).Append('\n');
                result.Add((starts[k].Name, sb.ToString()));
            }
            return result;
        }

        /// <summary>한 줄이 <c>public void X(</c> / <c>public IEnumerator X(</c> 형태면 그 이름.
        /// <para>정규식을 쓰지 않는다(이 파일의 <c>\b</c> 금지 규칙과 같은 이유 — 이름에 한글이 섞인다).</para></summary>
        internal static string MethodNameOrNull(string line)
        {
            string t = line.Trim();
            string[] heads = { "public void ", "public IEnumerator ", "public System.Collections.IEnumerator " };
            foreach (string head in heads)
            {
                if (!t.StartsWith(head, StringComparison.Ordinal)) continue;
                int from = head.Length;
                int paren = t.IndexOf('(', from);
                if (paren <= from) return null;
                string name = t.Substring(from, paren - from).Trim();
                return name.Length == 0 || name.IndexOf(' ') >= 0 ? null : name;
            }
            return null;
        }

        // ====================================================================
        // 3. 대조 — "0건"을 믿기 전에 검사기가 작동함을 먼저 보인다
        // ====================================================================

        /// <summary>
        /// ★ 양성 대조 — 이 파일의 모든 "아직 없다" 판정은 <b>스캐너가 있는 것을 찾는다</b>는 전제 위에 선다.
        /// 그 전제를 매 실행 증명한다. 하나라도 깨지면 위 두 대장의 결과를 전부 폐기해야 한다.
        /// <para>(TEAM.md 4절 사고 #4: <c>strings</c>로 .NET UTF-16 문자열을 찾아 "0건 = 깨끗"으로 읽었는데
        /// 탐지력이 애초에 0이었다.)</para>
        /// </summary>
        [Test]
        public void 양성대조_스캐너들이_있는_것을_실제로_찾는다()
        {
            // ① 식별자 스캔이 실제로 프로덕션에서 흔한 이름을 찾는가.
            List<string> stickConfigUsers = ProductionUsers("StickConfig", "StickConfig.cs");
            Assert.Greater(stickConfigUsers.Count, 10,
                $"{LogPrefix} StickConfig를 쓰는 프로덕션 파일을 {stickConfigUsers.Count}개밖에 " +
                "찾지 못했습니다 — 식별자 스캔이 눈이 멀었습니다. 이 파일의 모든 '0건' 판정이 무효입니다.");

            // ② 없는 이름은 0건이어야 한다(오탐이면 대장이 상시 거짓 실패한다).
            Assert.IsEmpty(ProductionUsers("Zzz명부에없는식별자Zzz"),
                $"{LogPrefix} 존재하지 않는 식별자를 찾아냈습니다 — 스캐너가 아무 문자열이나 " +
                "매치시키고 있습니다.");

            // ③ GUID 기반 애셋 탐지가 실제로 애셋을 찾는가.
            //    ★ 타입 이름으로 .asset을 grep하면 영원히 0건이다 — 그 함정에 안 빠졌음을 여기서 증명한다.
            int defAssets = AssetsReferencingScript(Path.Combine("Core", "AccessoryDefSO.cs"), out string defGuid);
            Assert.IsNotNull(defGuid,
                $"{LogPrefix} AccessoryDefSO.cs.meta에서 GUID를 읽지 못했습니다 — 애셋 탐지가 통째로 " +
                "0건이 되고, R2-3의 '매니페스트 0개' 판정이 무의미해집니다.");
            Assert.Greater(defAssets, 30,
                $"{LogPrefix} AccessoryDefSO(guid {defGuid})를 참조하는 .asset을 {defAssets}개밖에 " +
                "찾지 못했습니다(42개 있어야 합니다) — GUID 탐지가 눈이 멀었습니다.");

            // ④ 타입 이름 grep이 왜 안 되는지도 함께 못 박는다(다음 사람이 되돌리지 않게).
            int byTypeName = 0;
            foreach (string p in Directory.GetFiles(Application.dataPath, "*.asset", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(p).IndexOf("AccessoryDefSO", StringComparison.Ordinal) >= 0) byTypeName++;
            }
            Assert.AreEqual(0, byTypeName,
                $"{LogPrefix} .asset에서 타입 이름 'AccessoryDefSO'가 {byTypeName}개 잡혔습니다. " +
                "이 저장소의 애셋이 타입 이름을 적기 시작했다면 위 GUID 우회의 근거 문단을 갱신하세요 " +
                "(지금은 GUID만 적히므로 타입 이름 grep은 탐지력 0입니다).");

            Debug.Log($"{LogPrefix} 양성 대조 — StickConfig 사용 {stickConfigUsers.Count}파일 / " +
                      $"AccessoryDefSO 애셋 {defAssets}개(guid {defGuid}) / 타입이름 grep {byTypeName}개.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 위 스캐너들이 <b>합법과 위반을 실제로 가르는가.</b>
        /// 대장들은 "문제 없음"을 단언하므로 판정기가 아무것도 안 해도 초록이다. 그래서 판정의
        /// 양쪽 극을 여기에 박제한다.
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_판정기가_합법과_위반을_가른다()
        {
            // ① 주석 제거 — 주석 속 언급을 '배선'으로 세면 대장이 거짓 실패한다(FootholdPoller가 그 형태).
            string src =
                "/// <see cref=\"FootholdScanPolicy\"/> 문서에만 있다\n" +
                "// FootholdScanPolicy 라고 줄 주석에도 쓴다\n" +
                "/* FootholdScanPolicy 블록 주석 */\n" +
                "int x = 1;\n";
            Assert.IsFalse(ContainsIdentifier(StripComments(src), "FootholdScanPolicy"),
                "주석 속 언급을 배선으로 셉니다 — R2-2가 상시 거짓 실패합니다.");
            Assert.IsTrue(ContainsIdentifier(StripComments(src + "FootholdScanPolicy.Decide(in s);\n"),
                    "FootholdScanPolicy"),
                "실제 호출을 놓칩니다 — R2-2는 배선이 들어와도 영원히 초록입니다.");

            // ② 문자열 리터럴은 지우지 않는다(단언 메시지가 통째로 사라지면 다른 감사들이 눈이 먼다).
            Assert.IsTrue(StripComments("var s = \"보존되어야 한다\";").Contains("보존되어야 한다"),
                "문자열 안까지 지웁니다.");
            Assert.IsTrue(StripComments("var s = \"http://example.com\"; int y = 2;").Contains("int y = 2;"),
                "문자열 안의 // 를 주석 시작으로 오인합니다 — URL 하나가 그 줄의 나머지를 삼킵니다.");

            // ③ 식별자 경계 — 부분일치를 배제하는가(ResolveInk vs ResolveInkColor).
            Assert.IsFalse(ContainsIdentifier("StickConfig.ResolveInkColor();", "ResolveInk"),
                "ResolveInkColor를 ResolveInk로 셉니다 — 부분일치가 통과하면 '배선됐다'가 거짓이 됩니다.");
            Assert.IsTrue(ContainsIdentifier("var c = ResolveInk(x);", "ResolveInk"),
                "진짜 호출을 놓칩니다.");

            // ④ ★ 한글 경계 — .NET 정규식 \b가 한글을 낱말 문자로 세어 R1이 46% 미탐한 그 함정.
            //    이 파일은 \b를 쓰지 않으므로 조사가 붙어도 매치가 살아 있어야 한다.
            Assert.IsTrue(ContainsIdentifier("ResolveWornPalette를 부르지 않아", "ResolveWornPalette"),
                "한글 조사가 붙으면 매치가 사라집니다 — 정규식 \\b로 되돌아갔는지 확인하세요. " +
                "그 판은 한글 문장 안의 참조를 절반 가까이 놓쳤습니다(R1 자기 고백).");
            Assert.IsTrue(ContainsIdentifier("(FootholdScanPolicy)에 배선하지", "FootholdScanPolicy"),
                "괄호 경계를 못 넘습니다.");

            // ⑤ 메서드 자르기 — 이름을 실제로 읽고, 아무 줄에서나 만들어 내지 않는가.
            Assert.AreEqual("가나다_라마바", MethodNameOrNull("        public void 가나다_라마바()"),
                "한글 메서드 이름을 못 읽습니다 — 이 저장소 테스트의 대부분이 한글 이름입니다.");
            Assert.AreEqual("Foo", MethodNameOrNull("        public IEnumerator Foo()"),
                "UnityTest(IEnumerator) 메서드를 못 읽습니다.");
            Assert.IsNull(MethodNameOrNull("        private void Helper()"),
                "private 헬퍼까지 테스트로 셉니다.");
            Assert.IsNull(MethodNameOrNull("            // public void 주석 속 선언()"),
                "주석 속 선언을 진짜 메서드로 셉니다.");
            // ★ 이 한 줄이 M11(동반 테스트 소멸)을 잡는 근거다. 이름이 <b>문자열로 적혀 있는 것</b>을
            //   선언으로 세면, 명부 자신에 적힌 이름이 걸려서 동반 검사가 통째로 공허해진다.
            Assert.IsNull(MethodNameOrNull("            Companion = \"미배선_목록은_어쩌고\","),
                "문자열 리터럴에 적힌 이름을 메서드 선언으로 셉니다 — 동반 검사가 자기 명부를 " +
                "읽고 스스로를 안심시키게 됩니다(첫 판이 실제로 그랬습니다).");

            List<(string Method, string Body)> cut = TestMethods(
                "        public void A()\n        {\n            X();\n        }\n" +
                "        public void B()\n        {\n            Y();\n        }\n");
            Assert.AreEqual(2, cut.Count, "메서드 두 개를 못 가릅니다.");
            Assert.IsTrue(cut[0].Body.Contains("X()") && !cut[0].Body.Contains("Y()"),
                "본문이 다음 메서드까지 넘칩니다 — Ignore가 엉뚱한 메서드에 귀속됩니다.");
        }
    }
}
