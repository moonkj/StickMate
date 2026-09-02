using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"주석이 지목한 파일은 실제로 존재한다."</b>
    ///
    /// ============================================================================
    /// 왜 있는가 (2026-09-02, code-inspection 1라운드)
    /// ============================================================================
    /// 이 저장소가 반복해서 당한 형태는 <b>거짓 주석</b>이다. 버그는 언젠가 재현되지만
    /// 주석은 아무도 실행하지 않으므로 <b>버그보다 오래 산다</b> — 다음 사람이 그걸 읽고
    /// 판단한다. 실제로 하루에 5건이 나왔고 <b>전부 사람이 우연히 발견</b>했다.
    ///
    /// 그중 <b>기계로 검증 가능한 부분집합</b>이 하나 있다: 주석이 <b>다른 파일을 지목</b>하는
    /// 경우다. 그 파일이 없으면 그 문장은 무조건 거짓이고, 판정에 사람의 판단이 끼지 않는다.
    /// 이 테스트는 그 부분집합만 잠근다. "이 함수는 X를 한다" 같은 의미 주장은 못 잰다 —
    /// <b>못 재는 것을 재는 척하지 않는다.</b>
    ///
    /// <para>이 검사가 특히 값싼 이유: 파일을 <b>쪼개거나 이름을 바꾸는</b> 순간 그 파일을
    /// 가리키던 주석이 전부 거짓이 된다. 이 저장소는 2026-09-02에 <c>CharacterInfoWindow</c>를
    /// 7개 partial로 쪼갰고, 앞으로도 쪼갤 것이다.</para>
    ///
    /// ============================================================================
    /// 이 테스트가 처음 잡은 것 (전부 실측)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><c>Platform/VisibleTopEdgeSolver.cs</c>가 "<c>Tests/EditMode/VisibleTopEdgeSolverTests.cs</c>가
    ///     그 실측이다"라고 적었는데 그런 파일은 없다(실제 이름은 <c>VisibleTopEdgeOcclusionTests.cs</c>).</item>
    ///   <item><c>States/IdleState.cs</c> · <c>States/WalkState.cs</c>가 <c>States/IPlannedDwellSource.cs</c>를
    ///     지목하는데 그런 파일은 없다(그 인터페이스는 <c>States/IMovementIntentSource.cs</c> 안에 있다).</item>
    ///   <item><c>Core/CharacterSaveStore.cs</c>의 <c>Tests/*/GlobalTestIsolation.cs</c>는 어떤 파일에도
    ///     맞지 않는다(실제는 <c>GlobalEditModeTestIsolation.cs</c> / <c>GlobalPlayModeTestIsolation.cs</c>).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 설계 규칙 3가지 (전부 이 저장소가 당한 사고에서 나왔다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>파일명 명부로 소스를 찾지 않는다.</b> 디렉터리를 통째로 걷는다 —
    ///     명부 방식은 파일을 쪼개는 순간 눈이 먼다(이 저장소에서 2건 발생).</item>
    ///   <item><b>모든 "0건"에 양성 대조를 붙인다.</b> 아래 <c>양성대조_*</c> 테스트가
    ///     스캐너에 <b>있는 것을 실제로 찾는지</b> 먼저 증명한다. 최소 수집량 가드도 함께 둔다 —
    ///     스캐너가 아무것도 못 읽고 초록불이 되는 것이 이 저장소의 사고 #4·#5였다.</item>
    ///   <item><b>면제에는 사유를 적는다.</b> 그리고 면제가 <b>고쳐졌으면 실패</b>한다 —
    ///     명부가 조용히 늙는 것을 막는다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게 적는다)</b>: 이 검사는 "그 파일이 있는가"만 본다.
    /// 그 파일이 주석이 말하는 <b>내용</b>을 담고 있는지는 못 잰다.
    /// 다만 <see cref="줄번호_참조는_썩지_않았는지_앵커로_확인한다"/>만은 예외로,
    /// 지목한 <b>줄</b>이 여전히 같은 문구인지까지 확인한다.</para>
    /// </summary>
    public sealed class CommentReferenceAuditTests
    {
        // ==================================================================
        // 경로
        // ==================================================================

        /// <summary><c>&lt;repo&gt;/Assets</c>. Unity가 주는 유일한 안정적 기준점이다.</summary>
        private static string AssetsRoot => Application.dataPath;

        /// <summary><c>&lt;repo&gt;</c>. <c>docs/</c>·<c>design/</c>·<c>Tools/</c> 참조를 확인할 때 쓴다.</summary>
        private static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>테스트 어셈블리 자신은 검사 대상이 아니다(테스트 주석은 프로덕션 독자가 읽지 않는다).
        /// 경로 조각으로 판정한다 — 파일명 명부가 아니다.</summary>
        private const string TestsPathFragment = "/Scripts/Tests/";

        // ==================================================================
        // 면제 — 사유 없이 넣지 마라
        // ==================================================================

        /// <summary>
        /// <b>저장소 밖 파일</b>. 우리가 만들지 않았고 우리 트리에 없다(UPM 패키지 / Unity 엔진 소스).
        /// 지목 자체는 정당하므로 면제하되, <b>이 목록에 없는 외부 파일은 전부 위반</b>이다 —
        /// 그래야 오타 난 우리 파일 이름이 "외부겠지"로 빠져나가지 못한다.
        /// </summary>
        private static readonly Dictionary<string, string> ExternalOwned = new Dictionary<string, string>
        {
            ["UniWinCore.cs"] = "UPM 패키지 com.kirurobo.uniwinc의 저수준 래퍼. Library/PackageCache에만 있다(gitignore).",
            ["UniWindowController.cs"] = "같은 패키지의 상위 컴포넌트.",
            ["UniWindowControllerEditor.cs"] = "같은 패키지의 에디터 검증기.",
            ["OnDemandRendering.bindings.cs"] = "Unity 엔진 소스(Runtime/Export/Graphics/). 배포본에 없다.",
        };

        /// <summary>
        /// <b>과거형으로 적힌 파일</b>. "지금 있다"가 아니라 "예전에 있었다"를 서술하므로 거짓이 아니다.
        /// 파일명만으로는 시제를 못 읽으므로 여기에 사유와 함께 적는다.
        /// </summary>
        private static readonly Dictionary<string, string> HistoricalOnly = new Dictionary<string, string>
        {
            ["WindowsFramePacing.cs"] = "Platform/FramePacing.cs 클래스 문서가 '통합 전에는 따로 있었다'로 명시한 과거 파일.",
            ["MacFramePacing.cs"] = "같은 문단의 macOS 짝.",
        };

        /// <summary>
        /// ★ <b>이미 깨져 있는 참조</b> — 이 라운드가 발견했고 <b>수정은 원 담당자에게 배정된다</b>
        /// (code-inspection은 프로덕션 <c>.cs</c>를 고치지 않는다).
        ///
        /// <para>키는 <b>깨진 참조 문자열</b>, 값은 사유 + 실제로 무엇을 가리켰어야 하는가다.
        /// 고쳐지면 <see cref="이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다"/>가 <b>실패</b>해서
        /// 명부를 지우라고 말한다 — 명부가 조용히 늙지 않게 하는 장치다.</para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownBroken = new Dictionary<string, string>
        {
            ["Tests/EditMode/VisibleTopEdgeSolverTests.cs"] =
                "Platform/VisibleTopEdgeSolver.cs — 실제 파일명은 Tests/EditMode/VisibleTopEdgeOcclusionTests.cs다. " +
                "'그 실측이다'라고 단언하므로, 이 줄을 읽고 실측을 찾으러 간 사람은 빈손으로 돌아온다.",

            ["States/IPlannedDwellSource.cs"] =
                "States/IdleState.cs · States/WalkState.cs 두 곳. IPlannedDwellSource는 파일이 아니라 " +
                "States/IMovementIntentSource.cs 안에 선언된 인터페이스다.",

            ["Tests/*/GlobalTestIsolation.cs"] =
                "Core/CharacterSaveStore.cs — 와일드카드가 어떤 파일에도 맞지 않는다. 실제는 " +
                "Tests/EditMode/GlobalEditModeTestIsolation.cs / Tests/PlayMode/GlobalPlayModeTestIsolation.cs.",
        };

        /// <summary>
        /// ★ <b>줄 번호 참조</b> — <c>Foo.cs:123</c>. 줄 번호는 위에 한 줄만 끼어들어도 조용히 썩는다.
        /// 새로 만들지 마라(절 이름이나 <c>nameof</c>로 걸어라). 이미 있는 것은 여기 <b>앵커 문구</b>와
        /// 함께 적어 두고, 그 줄이 여전히 같은 문구인지 매 실행 확인한다.
        ///
        /// <para>앵커는 프로덕션 <b>상수의 복사</b>가 아니다(CLAUDE.md의 하드코딩 금지와 무관하다) —
        /// "이 줄이 그 줄인가"를 재기 위한 <b>참조 무결성 표식</b>이며, 값이 아니라 위치를 잰다.</para>
        /// </summary>
        private static readonly (string RefText, string TargetRelative, int Line, string Anchor)[] KnownLineRefs =
        {
            ("InfoGearIconWidget.cs:51", "_Project/Scripts/Interaction/InfoGearIconWidget.cs", 51,
                "hitTestType=Raycast"),
            ("CharacterFxRenderer.cs:304", "_Project/Scripts/Interaction/CharacterFxRenderer.cs", 304,
                "item <= FxNone"),
        };

        // ==================================================================
        // 스캐너 (순수 함수 — 아래 양성 대조가 직접 먹인다)
        // ==================================================================

        /// <summary>
        /// ★ <b>끝을 <c>\b</c>로 막지 않는다.</b> .NET의 <c>\b</c>는 유니코드 단어 경계라
        /// 한글도 단어 문자로 센다 — <c>StickConfig.cs가</c>처럼 조사가 붙은 참조에서
        /// <c>s</c>와 <c>가</c> 사이에 경계가 서지 않아 <b>매치 자체가 사라진다</b>.
        /// 오프라인 예행에서 실측했다: <c>\b</c>판은 534건 중 <b>245건(46%)을 놓쳤고</b>,
        /// 놓친 것 안에 이 라운드가 손으로 찾은 진짜 위반이 들어 있었다
        /// (<c>...VisibleTopEdgeSolverTests.cs<b>가</b> 그 실측이다</c>).
        /// 그래서 "다음 글자가 ASCII 낱말 문자가 아니다"로 명시한다 — <c>.csproj</c>는 그대로 걸러진다.
        /// </summary>
        private static readonly Regex CsRefRegex =
            new Regex(@"[A-Za-z0-9_./*-]*[A-Za-z0-9_]\.cs(?![A-Za-z0-9_])", RegexOptions.Compiled);

        private static readonly Regex RepoPathRefRegex =
            new Regex(@"(?:docs|design|Tools)/[A-Za-z0-9_./-]*[A-Za-z0-9_]\.(?:md|py|sh|json|txt)(?![A-Za-z0-9_])",
                RegexOptions.Compiled);

        private static readonly Regex LineRefRegex =
            new Regex(@"[A-Za-z0-9_]+\.cs:[0-9]+", RegexOptions.Compiled);

        /// <summary>그 줄에서 <b>주석 부분만</b> 잘라낸다. 주석이 아니면 <c>null</c>.
        /// <c>https://</c> 같은 URL이 <c>//</c>로 오인되지 않게 <c>://</c> 앞은 주석 시작으로 보지 않는다.</summary>
        internal static string CommentPart(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                return trimmed;
            }

            int at = 0;
            while ((at = line.IndexOf("//", at, StringComparison.Ordinal)) >= 0)
            {
                if (at > 0 && line[at - 1] == ':') { at += 2; continue; }   // "https://"
                return line.Substring(at);
            }
            return null;
        }

        /// <summary>주석 한 줄에서 <c>*.cs</c> 참조를 전부 뽑는다.</summary>
        internal static List<string> ExtractCsRefs(string commentLine)
        {
            var found = new List<string>();
            foreach (Match m in CsRefRegex.Matches(commentLine))
            {
                // "A.cs(설명)/B.cs" 처럼 이어 적힌 자리에서 앞 조각의 닫는 괄호 때문에 '/'로 시작하는
                // 조각이 나온다. 그건 경로가 아니라 잘린 자국이므로 떼어 낸다.
                found.Add(m.Value.TrimStart('/'));
            }
            return found;
        }

        private static IEnumerable<string> ProductionSources()
        {
            foreach (string path in Directory.GetFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains(TestsPathFragment)) continue;
                yield return normalized;
            }
        }

        /// <summary>Assets 아래 모든 <c>.cs</c>를 <b>파일명 -> 경로들</b>로 색인한다(테스트 포함 —
        /// 프로덕션 주석이 테스트 파일을 지목하는 것은 정상이다).</summary>
        private static Dictionary<string, List<string>> IndexAllSources()
        {
            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                string name = Path.GetFileName(normalized);
                if (!index.TryGetValue(name, out List<string> list)) index[name] = list = new List<string>();
                list.Add(normalized);
            }
            return index;
        }

        internal static bool Resolves(string reference, Dictionary<string, List<string>> index)
        {
            reference = reference.TrimStart('/');
            string name = reference.Substring(reference.LastIndexOf('/') + 1);
            if (!index.TryGetValue(name, out List<string> candidates)) return false;
            if (reference.IndexOf('/') < 0) return true;

            foreach (string candidate in candidates)
            {
                if (candidate.EndsWith("/" + reference, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private sealed class Violation
        {
            public string File;
            public int Line;
            public string Reference;
            public string Text;
            public override string ToString() =>
                $"{File}:{Line}  ->  '{Reference}'\n      {Text}";
        }

        private static List<Violation> CollectCsRefViolations(out int totalRefs)
        {
            Dictionary<string, List<string>> index = IndexAllSources();
            var violations = new List<Violation>();
            totalRefs = 0;

            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (string reference in ExtractCsRefs(comment))
                    {
                        totalRefs++;
                        string name = reference.Substring(reference.LastIndexOf('/') + 1);
                        if (ExternalOwned.ContainsKey(name)) continue;
                        if (HistoricalOnly.ContainsKey(name)) continue;
                        if (Resolves(reference, index)) continue;

                        violations.Add(new Violation
                        {
                            File = path.Substring(RepoRoot.Length + 1),
                            Line = i + 1,
                            Reference = reference,
                            Text = lines[i].Trim(),
                        });
                    }
                }
            }
            return violations;
        }

        // ==================================================================
        // 1) 소스 파일 참조
        // ==================================================================

        [Test]
        public void 주석이_지목한_소스_파일이_새로_사라지지_않는다()
        {
            List<Violation> violations = CollectCsRefViolations(out int totalRefs);

            // ★ 스캐너가 눈이 멀면 "0건"이 곧 초록불이 된다 — 먼저 그게 아님을 보인다.
            Assert.Greater(totalRefs, 300,
                $"주석에서 뽑은 .cs 참조가 {totalRefs}건뿐이다 — 스캐너가 트리를 잘못 보고 있다. " +
                "이 저장소는 프로덕션 주석에만 500건 이상을 갖고 있었다(2026-09-02 실측 534건).");

            var unexpected = new List<Violation>();
            foreach (Violation v in violations)
            {
                if (!KnownBroken.ContainsKey(v.Reference)) unexpected.Add(v);
            }

            Assert.IsEmpty(unexpected,
                "주석이 지목한 소스 파일이 존재하지 않는다. 파일을 쪼개거나 이름을 바꿨다면 " +
                "그 파일을 가리키던 주석도 함께 고쳐라 — 고칠 수 없다면 " +
                $"{nameof(ExternalOwned)}/{nameof(HistoricalOnly)}/{nameof(KnownBroken)}에 " +
                "사유와 함께 적어라.\n  " + string.Join("\n  ", unexpected));
        }

        [Test]
        public void 이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다()
        {
            List<Violation> violations = CollectCsRefViolations(out _);
            var stillBroken = new HashSet<string>(StringComparer.Ordinal);
            foreach (Violation v in violations) stillBroken.Add(v.Reference);

            var fixedAlready = new List<string>();
            foreach (KeyValuePair<string, string> entry in KnownBroken)
            {
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 면제 사유가 비어 있다.");
                if (!stillBroken.Contains(entry.Key)) fixedAlready.Add(entry.Key);
            }

            Assert.IsEmpty(fixedAlready,
                $"아래 참조는 이미 고쳐졌다 — {nameof(KnownBroken)}에서 지워라. " +
                "남겨 두면 다음에 같은 자리가 다시 깨져도 이 감사가 침묵한다.\n  "
                + string.Join("\n  ", fixedAlready));

            if (KnownBroken.Count > 0)
            {
                var lines = new List<string>();
                foreach (KeyValuePair<string, string> e in KnownBroken) lines.Add($"{e.Key} — {e.Value}");
                Assert.Ignore(
                    $"아직 안 고친 거짓 참조 {KnownBroken.Count}건(수정은 원 담당자에게 배정). " +
                    "러너에 '건너뜀'으로 계속 보이게 남긴다 — 잊히지 않게.\n  "
                    + string.Join("\n  ", lines));
            }
        }

        // ==================================================================
        // 2) 문서 · 도구 경로 참조
        // ==================================================================

        [Test]
        public void 주석이_지목한_문서와_도구_경로가_실제로_있다()
        {
            var missing = new List<string>();
            int total = 0;

            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (Match m in RepoPathRefRegex.Matches(comment))
                    {
                        total++;
                        if (File.Exists(Path.Combine(RepoRoot, m.Value))) continue;
                        missing.Add($"{path.Substring(RepoRoot.Length + 1)}:{i + 1}  ->  '{m.Value}'");
                    }
                }
            }

            Assert.Greater(total, 150,
                $"문서·도구 경로 참조가 {total}건뿐이다 — 스캐너 고장(2026-09-02 실측 258건).");
            Assert.IsEmpty(missing,
                "주석이 지목한 문서/도구 파일이 없다. 문서를 옮기거나 이름을 바꿨다면 그것을 " +
                "가리키던 주석도 함께 고쳐라.\n  " + string.Join("\n  ", missing));
        }

        // ==================================================================
        // 3) 줄 번호 참조 — 썩는다
        // ==================================================================

        [Test]
        public void 줄번호_참조를_새로_만들지_않는다()
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string refText, _, _, _) in KnownLineRefs) known.Add(refText);
            foreach (string external in ExternalOwned.Keys) known.Add(external);   // 패키지 줄 참조는 nameof로 못 건다

            var unexpected = new List<string>();
            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (Match m in LineRefRegex.Matches(comment))
                    {
                        string fileName = m.Value.Substring(0, m.Value.IndexOf(".cs:", StringComparison.Ordinal) + 3);
                        if (known.Contains(m.Value) || known.Contains(fileName)) continue;
                        unexpected.Add($"{path.Substring(RepoRoot.Length + 1)}:{i + 1}  ->  '{m.Value}'\n      {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(unexpected,
                "주석에 새 줄 번호 참조가 생겼다. 줄 번호는 위에 한 줄만 끼어들어도 조용히 썩고, " +
                "아무 테스트도 그것을 잡지 않는다. 대신 <b>절 이름</b>(예: \"클릭 판정\" 절)이나 " +
                $"<c>nameof</c>로 걸어라. 그래도 필요하면 {nameof(KnownLineRefs)}에 앵커 문구와 함께 적어라.\n  "
                + string.Join("\n  ", unexpected));
        }

        [Test]
        public void 줄번호_참조는_썩지_않았는지_앵커로_확인한다()
        {
            Assert.IsNotEmpty(KnownLineRefs,
                "명부가 비었다 — 아래 foreach가 아무것도 재지 않고 초록불이 된다(사고 #5). " +
                "정말 줄 번호 참조가 하나도 없다면 이 단언을 지우지 말고 '0건이 기대값'이라고 고쳐 적어라.");

            var rotten = new List<string>();
            foreach ((string refText, string relative, int line, string anchor) in KnownLineRefs)
            {
                string target = Path.Combine(AssetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(target), $"{refText}가 가리키는 {relative}가 없다.");

                string[] lines = File.ReadAllLines(target);
                if (line < 1 || line > lines.Length)
                {
                    rotten.Add($"{refText}: 대상 파일이 {lines.Length}줄뿐이다.");
                    continue;
                }
                if (lines[line - 1].IndexOf(anchor, StringComparison.Ordinal) < 0)
                {
                    rotten.Add($"{refText}: {line}번 줄이 더 이상 '{anchor}'를 담고 있지 않다.\n" +
                               $"      지금 그 줄: {lines[line - 1].Trim()}");
                }
            }

            Assert.IsEmpty(rotten,
                "줄 번호 참조가 썩었다. 그 사이에 줄이 끼어들었다는 뜻이다 — 주석을 읽는 사람은 " +
                "엉뚱한 줄을 보게 된다. 줄 번호를 고치지 말고 <b>절 이름/nameof</b>로 바꿔라(다시 썩는다).\n  "
                + string.Join("\n  ", rotten));
        }

        // ==================================================================
        // 4) 양성 대조 — "0건"이 진짜 0인지 먼저 증명한다
        // ==================================================================

        [Test]
        public void 양성대조_스캐너가_깨진_참조를_실제로_잡는다()
        {
            Dictionary<string, List<string>> index = IndexAllSources();

            Assert.IsTrue(Resolves("Core/ItemCatalog.cs", index), "있는 파일을 못 찾으면 이 감사 전체가 무의미하다.");
            Assert.IsTrue(Resolves("ItemCatalog.cs", index), "디렉터리 없는 참조도 풀려야 한다.");
            Assert.IsTrue(Resolves("Tests/EditMode/EquipmentMigrationTests.cs", index),
                "프로덕션 주석이 테스트 파일을 지목하는 것은 정상이다 — 색인이 테스트를 빠뜨리면 거짓 위반이 쏟아진다.");

            Assert.IsFalse(Resolves("Core/NoSuchFileAtAll.cs", index), "없는 파일을 있다고 하면 탐지력이 0이다.");
            Assert.IsFalse(Resolves("Tests/EditMode/VisibleTopEdgeSolverTests.cs", index),
                "실제로 깨져 있는 참조다 — 이게 통과로 나오면 이 감사는 아무것도 안 잡는다.");
            Assert.IsFalse(Resolves("States/IPlannedDwellSource.cs", index),
                "IPlannedDwellSource는 파일이 아니라 IMovementIntentSource.cs 안의 인터페이스다.");
            Assert.IsFalse(Resolves("Tests/*/GlobalTestIsolation.cs", index), "와일드카드는 어떤 파일에도 맞지 않는다.");

            // ★ 경로가 틀린 경우 — 파일명은 맞지만 위치가 다르면 잡아야 한다.
            Assert.IsFalse(Resolves("Platform/ItemCatalog.cs", index),
                "파일명만 보고 통과시키면 파일을 옮겨도 감사가 침묵한다.");
        }

        [Test]
        public void 양성대조_주석_추출이_코드와_URL을_구분한다()
        {
            Assert.IsNull(CommentPart("            int a = 1;"), "코드 줄을 주석으로 보면 소음이 쏟아진다.");
            Assert.IsNotNull(CommentPart("        /// Core/ItemCatalog.cs 참고"));
            Assert.IsNotNull(CommentPart("        // Core/ItemCatalog.cs 참고"));
            Assert.IsNotNull(CommentPart("            int a = 1;   // Core/ItemCatalog.cs 참고"));
            Assert.IsNull(CommentPart("            var url = \"https://example.com/a.cs\";"),
                "URL의 //를 주석 시작으로 보면 문자열 안의 경로까지 검사 대상이 된다.");

            List<string> refs = ExtractCsRefs("/// Core/A.cs 와 Interaction/B.cs, 그리고 C.cs");
            Assert.AreEqual(3, refs.Count, "한 줄에 여러 참조가 있으면 전부 잡아야 한다.");
            CollectionAssert.AreEqual(new[] { "Core/A.cs", "Interaction/B.cs", "C.cs" }, refs);

            Assert.IsEmpty(ExtractCsRefs("/// 여기에는 소스 참조가 없다"),
                "아무 줄에서나 참조를 만들어 내면 거짓 위반이 쏟아진다.");

            // ★★ 이 저장소의 사고 #4 그 자체 — "탐지력이 애초에 0"인 검사.
            //    이 파일의 첫 판은 정규식 끝을 \b로 막았고, .NET의 \b는 한글을 낱말 문자로 세기 때문에
            //    조사가 붙은 참조를 통째로 놓쳤다(534건 중 245건). 이 단언이 그 판을 되살리지 못하게 막는다.
            CollectionAssert.AreEqual(new[] { "Core/StickConfig.cs" },
                ExtractCsRefs("/// 값의 원본은 Core/StickConfig.cs가 갖고 있다"),
                "조사(가/를/이/의…)가 붙은 참조를 놓치면 이 감사의 '0건'은 전부 무효다.");
            CollectionAssert.AreEqual(new[] { "Core/StickConfig.cs" },
                ExtractCsRefs("/// Core/StickConfig.cs를 보라"));

            Assert.IsEmpty(ExtractCsRefs("/// StickMate.csproj 는 소스가 아니다"),
                ".csproj까지 소스 참조로 세면 소음이 된다.");

            CollectionAssert.AreEqual(new[] { "States/DragThrowState.cs", "RodeoCursorState.cs" },
                ExtractCsRefs("/// States/DragThrowState.cs(던진 속도)/RodeoCursorState.cs(흔들기)도"),
                "앞 조각의 닫는 괄호가 남긴 '/'를 떼지 않으면 멀쩡한 파일이 거짓 위반으로 뜬다.");
        }
    }
}
