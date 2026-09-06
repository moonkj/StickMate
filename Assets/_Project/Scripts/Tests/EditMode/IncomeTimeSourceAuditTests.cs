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
    /// T-11 #2 — <b>재화 코드가 어느 시계를 읽는가</b> (test-engineer, 2026-09-06)
    /// ============================================================================
    /// 규범: <c>docs/security/SECURITY_MODEL.md</c> T-3-a · T-11 #2,
    /// <c>docs/DESIGN_SYSTEMS_STATS.md</c> §23-7 (가).
    ///
    /// ============================================================================
    /// ★★ 왜 이 파일이 <b>2026-09-06에야</b> 생겼는가 — 그 사실 자체가 이 감사의 근거다
    /// ============================================================================
    /// <c>Core/CurrencyModel.cs</c>의 클래스 문서는 <b>2026-09-05부터</b> 이렇게 적고 있었다:
    /// <i>"<c>Tests/EditMode/IncomeTimeSourceAuditTests</c>가 이 문장(1곳, 그리고 그 1곳이
    /// 어디인지)을 <b>매 실행 집합 등호로</b> 확인한다."</i>
    /// <b>그런 파일은 없었다.</b> <c>DESIGN_SYSTEMS_STATS §23-7</c>이 실측으로 그것을 적어 두기까지
    /// 했는데도(<i>"즉 지금은 이 규칙을 깨도 아무도 빨개지지 않는다 — 조용히 초록이 되는 형태다"</i>)
    /// 아무도 만들지 않았다.
    ///
    /// <para>더 나쁜 것은 <b>그 거짓 인용을 잡아야 할 감사도 못 봤다</b>는 점이다 —
    /// <c>CommentReferenceAuditTests</c>의 정규식이 <c>.cs</c>가 붙은 참조만 봤다.
    /// 같은 라운드에 그쪽도 함께 넓혔다(그 파일의 「2026-09-06 확장」 절).</para>
    ///
    /// ============================================================================
    /// 무엇을 재고 무엇을 <b>안</b> 재는가
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>잰다</b>: 소스 텍스트에 <b>시계를 읽는 표현</b>이 어디에 몇 번 나오는가.</item>
    ///  <item><b>안 잰다</b>: 그 값으로 무엇을 계산하는가. 그건
    ///    <c>Tests/EditMode/CurrencyRulesTests.cs</c>·<c>CurrencyDayRolloverTests.cs</c>가
    ///    <b>순수 함수 동작</b>으로 잰다. <b>"코드가 그렇게 생겼나"와 "코드가 그렇게 계산하나"는
    ///    다른 질문이고, 섞으면 둘 다 약해진다</b>(T-15-7 서문).</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 「0건」이 아니라 「정확히 이 한 곳」으로 쓴다 (CLAUDE.md 부재 단언 규칙)
    /// ============================================================================
    /// 부재 단언은 <b>썩으면 조용히 초록</b>이 된다 — 니들이 낡아 아무 데도 안 맞으면
    /// "위반 0건"과 "측정 0건"이 글자 하나 다르지 않다. 그래서 이 파일은
    /// <b>있어야 할 것을 먼저 찾아 보이고</b>(존재 단언), 그 다음에 <b>그 밖에 없다</b>고 말한다.
    /// <see cref="ClockMemberName"/> · <see cref="ModelTypeName"/> 니들은 전부 존재 단언 쪽이라,
    /// 이름이 바뀌면 여기서 <b>시끄럽게</b> 빨개진다.
    ///
    /// <para>리플렉션은 한 줄도 쓰지 않는다 — 활성 빌드 타깃 반대편 파일은 타입이 존재하지 않아
    /// 리플렉션 감사가 구조적으로 눈이 먼다(CLAUDE.md 활성 빌드 타깃 규칙).</para>
    ///
    /// ============================================================================
    /// 짝이 되는 감사
    /// ============================================================================
    /// <see cref="DailyLimitClampAuditTests"/>가 <b>다른 절반</b>을 맡는다: 이 파일은
    /// <b>"어느 시계를 읽는가"</b>를, 저쪽은 <b>"읽은 값을 무엇에 넣는가"</b>(호출부 책임)와
    /// <b>"저장 스키마가 어떻게 생겼는가"</b>를 본다. 겹쳐 보이면 그 경계를 다시 읽어라.
    /// </summary>
    public sealed class IncomeTimeSourceAuditTests
    {
        private const string LogPrefix = "[수급시계감사]";

        // ====================================================================
        // 니들 — 전부 사유를 단다. 사유를 못 대는 토큰은 여기 있으면 안 된다.
        // ====================================================================

        /// <summary>
        /// <b>벽시계</b>. security T-3-a가 수급·정산 판정 경로에서 금지한 것들이다.
        /// <para>★ <c>DateTime</c>을 <b>타입으로</b> 쓰는 것(<c>new DateTime(1970,…)</c>,
        /// <c>DateTime</c> 파라미터)은 시계 읽기가 <b>아니다</b> — <c>CurrencyRules.LocalDayIndex</c>가
        /// 정확히 그 모양이라, 여기서 <c>DateTime</c>만 니들로 잡으면 <b>이 저장소가 즉시 거짓
        /// 빨강</b>이 된다. 그래서 <c>.Now</c>/<c>.UtcNow</c>까지 붙여 «읽기»만 겨눈다.</para>
        /// </summary>
        private static readonly (string Token, string Why)[] WallClockTokens =
        {
            ("DateTime.Now",          "로컬 벽시계. 시계를 앞으로 돌리면 그대로 따라간다."),
            ("DateTime.UtcNow",       "UTC 벽시계. 일자 경계 판정의 유일한 정당한 입력이지만 그것도 한 곳뿐이다."),
            ("DateTimeOffset.Now",    "같은 것의 오프셋 판. 유닉스 초로 세이브에 적히던 옛 설계의 입구다."),
            ("DateTimeOffset.UtcNow", "같은 것의 UTC 판."),
            ("Environment.TickCount", "int32라 ~24.9일에 감긴다(T-12) — 상주 데스크톱에서는 반드시 터진다."),
        };

        /// <summary>
        /// <b>로컬 시간대</b>. 시계 «값»은 아니지만 <b>같은 판정</b>의 입력이라 같은 자리에 있어야 한다
        /// (T-D-4: 오프셋은 첫 실행에 고정하고 다시 안 바꾼다 — 여행자가 하루 경계를 여러 번
        /// 넘기는 것을 막는다). 이게 다른 메서드로 새어 나가면 그 순간 오프셋이 두 곳에서 정해진다.
        /// </summary>
        private static readonly (string Token, string Why)[] LocalTimeZoneTokens =
        {
            ("TimeZoneInfo.Local", "로컬 오프셋. 고정 시점이 두 곳이 되면 시간대를 옮길 때 하루가 두 번 온다."),
        };

        /// <summary>
        /// <b>프레임 델타</b>. T-11 #2의 «부재» 쪽이다. 수급에 프레임 델타를 쓰면
        /// ① 엔진의 프레임 델타 상한 때문에 <b>조용히 적게</b> 쌓이고(T-3-c),
        /// ② 기계가 잠들었다 깬 시간이 통째로 사라진다.
        /// </summary>
        private static readonly (string Token, string Why)[] FrameDeltaTokens =
        {
            ("deltaTime",         "타임스케일에 묶인다. 0으로 만들면 수급이 멈춘다."),
            ("unscaledDeltaTime", "프레임 델타라는 점은 같다 — 잠든 시간을 잃는다."),
            ("fixedDeltaTime",    "물리 스텝. 수급과 아무 관계가 없다."),
            ("smoothDeltaTime",   "평활값이라 합이 실제 경과와 다르다."),
        };

        /// <summary>T-11 #2의 «존재» 쪽. 이 저장소가 쓰기로 한 단조 시계다.</summary>
        private const string MonotonicToken = "realtimeSinceStartupAsDouble";

        // ====================================================================
        // 발견용 니들 — 전부 <b>존재 단언</b>으로 못박는다(썩으면 시끄럽게 빨개진다)
        // ====================================================================

        /// <summary>재화 도메인을 <b>이름으로 찾지 않고 선언으로 찾기</b> 위한 조각.
        /// 파일 명부를 쓰면 파일을 쪼개는 순간 눈이 먼다(이 저장소에서 2건 발생).</summary>
        private const string DomainTypeFragment = "Currency";

        /// <summary>벽시계 한 곳을 가진 타입.</summary>
        private const string ModelTypeName = "CurrencyModel";

        /// <summary>시계를 읽지 <b>않기로</b> 한 순수 규칙 타입.</summary>
        private const string RulesTypeName = "CurrencyRules";

        /// <summary>★ <b>그 한 곳</b>. <c>CurrencyModel</c> 문서가 이름으로 지목한 멤버다.</summary>
        private const string ClockMemberName = "ResolveTodayIndex";

        /// <summary>도메인 파일이 몇 개는 잡혀야 스캔이 공허하지 않은가.
        /// 2026-09-06 실측 3개(<c>CurrencyModel</c>·<c>CurrencyRules</c>·<c>CurrencyDayRolloverTicker</c>).</summary>
        private const int MinDomainFileCount = 3;

        // ====================================================================
        // 스캐너 — 순수 함수. 아래 네거티브 컨트롤이 <b>같은 함수</b>에 가짜 소스를 흘린다.
        // ====================================================================

        private static int CountAny(string stripped, (string Token, string Why)[] tokens)
        {
            int total = 0;
            foreach ((string token, string _) in tokens)
                total += EntitlementAuditSource.CountIdentifier(stripped, token);
            return total;
        }

        /// <summary>이 파일이 «시계 읽기»로 세는 것 전부(벽시계 + 로컬 시간대).</summary>
        internal static int CountEnvironmentTimeReads(string stripped)
            => CountAny(stripped, WallClockTokens) + CountAny(stripped, LocalTimeZoneTokens);

        /// <summary>
        /// 중괄호를 세어 <paramref name="methodName"/> <b>선언</b>의 본문을 잘라 낸다.
        /// 못 찾으면 <c>null</c>(호출자가 그것을 <b>실패</b>로 다룬다 — 조용한 빈 문자열 금지).
        ///
        /// <para>★ <b>호출부와 선언부를 구분한다</b>: <c>ResolveTodayIndex()</c> 뒤에 <c>;</c>가 오면
        /// 호출이고 <c>{</c>가 오면 선언이다. 구분하지 않으면 «호출 한 줄»을 본문으로 잘라
        /// 내부 개수가 0이 되고, 그 0은 «메서드 밖으로 샜다»는 <b>거짓 빨강</b>을 만든다.</para>
        /// </summary>
        internal static string MethodBodyOrNull(string stripped, string methodName)
        {
            if (string.IsNullOrEmpty(stripped) || string.IsNullOrEmpty(methodName)) return null;

            int from = 0;
            while (true)
            {
                int at = stripped.IndexOf(methodName, from, StringComparison.Ordinal);
                if (at < 0) return null;
                from = at + 1;

                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(stripped[before])) continue;

                int p = at + methodName.Length;
                while (p < stripped.Length && (stripped[p] == ' ' || stripped[p] == '\t')) p++;
                if (p >= stripped.Length || stripped[p] != '(') continue;

                int depth = 0;
                int q = p;
                for (; q < stripped.Length; q++)
                {
                    if (stripped[q] == '(') depth++;
                    else if (stripped[q] == ')') { depth--; if (depth == 0) break; }
                }
                if (q >= stripped.Length) continue;

                q++;
                while (q < stripped.Length && char.IsWhiteSpace(stripped[q])) q++;
                if (q >= stripped.Length || stripped[q] != '{') continue;   // 호출부(;)·식 본문(=>)

                int braces = 0;
                for (int r = q; r < stripped.Length; r++)
                {
                    if (stripped[r] == '{') braces++;
                    else if (stripped[r] == '}')
                    {
                        braces--;
                        if (braces == 0) return stripped.Substring(q + 1, r - q - 1);
                    }
                }
                return null;
            }
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        // ====================================================================
        // 트리 읽기
        // ====================================================================

        private static List<(string Path, string Stripped)> AllProduction()
        {
            var all = new List<(string, string)>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
                all.Add((path, EntitlementAuditSource.StripComments(File.ReadAllText(path))));
            return all;
        }

        /// <summary>재화 도메인 — <b>이름이 <see cref="DomainTypeFragment"/>를 품은 타입을
        /// 선언하는</b> 프로덕션 파일. 파일 명부가 아니라 선언으로 찾으므로 파일을 쪼개도 따라간다.</summary>
        private static List<(string Path, string Stripped)> DomainFiles(
            List<(string Path, string Stripped)> all)
        {
            var found = new List<(string, string)>();
            foreach ((string path, string stripped) in all)
            {
                if (EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, DomainTypeFragment).Count == 0)
                    continue;
                found.Add((path, stripped));
            }
            return found;
        }

        private static List<(string Path, string Stripped)> FilesDeclaring(
            List<(string Path, string Stripped)> all, string typeName)
        {
            var found = new List<(string, string)>();
            foreach ((string path, string stripped) in all)
            {
                if (!EntitlementAuditSource.DeclaresType(stripped, typeName)) continue;
                found.Add((path, stripped));
            }
            return found;
        }

        private static string Rel(string path)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return path.Length > root.Length ? path.Substring(root.Length + 1) : path;
        }

        // ====================================================================
        // 1. ★ 본론 — 「정확히 한 곳, 그리고 그 한 곳이 어디인지」
        // ====================================================================

        /// <summary>
        /// <c>CurrencyModel</c> 문서의 ★★절을 그대로 잰다:
        /// <i>"남은 단 하나는 «오늘이 며칠인가»다 … 그 읽기는 <c>ResolveTodayIndex</c> 안에만 있고"</i>.
        ///
        /// <para><b>집합 등호로 쓴다</b>: 파일 전체의 읽기 개수와 <see cref="ClockMemberName"/> 본문
        /// 안의 읽기 개수가 <b>같으면</b>, 본문은 파일의 부분집합이므로 «전부 거기 있다»가 참이다.
        /// 이 등식에는 <b>기대 개수를 베낀 숫자가 없다</b> — 읽기가 정당하게 늘어도(예: 두 번째
        /// 시간대 축) 그것이 같은 메서드 안이면 통과하고, 밖으로 나가면 빨개진다.</para>
        ///
        /// <para>그와 <b>별도로</b> «벽시계 값을 읽는 표현»은 <b>1개</b>여야 한다 — 그게 T-3-a가
        /// 남긴 유일한 예외의 개수이고, 문서가 «정확히 한 곳»이라고 쓴 그 1이다.</para>
        /// </summary>
        [Test]
        public void 재화_모델의_시계_읽기는_전부_ResolveTodayIndex_안에_있다()
        {
            List<(string Path, string Stripped)> all = AllProduction();
            Assert.GreaterOrEqual(all.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Count}개밖에 읽지 못했습니다 " +
                $"({EntitlementAuditSource.ScriptsRoot}). 이 상태의 '위반 0건'은 측정이 아닙니다.");

            List<(string Path, string Stripped)> model = FilesDeclaring(all, ModelTypeName);
            Assert.IsNotEmpty(model,
                $"{LogPrefix} {ModelTypeName}을 선언하는 프로덕션 파일을 찾지 못했습니다. " +
                "타입 이름이 바뀌었거나 파일이 옮겨졌습니다 — 이 감사를 그 자리로 따라가게 고치기 " +
                "전에는 재화 파일의 벽시계 규칙을 아무도 보고 있지 않습니다.");

            int fileReads = 0;
            int memberReads = 0;
            int wallClockReads = 0;
            bool sawMemberDeclaration = false;
            var report = new StringBuilder(LogPrefix).Append(' ').Append(ModelTypeName).Append(" 시계 읽기\n");

            foreach ((string path, string stripped) in model)
            {
                int reads = CountEnvironmentTimeReads(stripped);
                fileReads += reads;
                wallClockReads += CountAny(stripped, WallClockTokens);

                // ★ partial로 쪼개져도 따라간다 — 선언이 없는 조각은 '0건'으로 그냥 합산된다.
                //   «어느 조각에도 선언이 없다»만 아래에서 실패로 다룬다.
                string body = MethodBodyOrNull(stripped, ClockMemberName);
                int inMember = body == null ? 0 : CountEnvironmentTimeReads(body);
                if (body != null) sawMemberDeclaration = true;
                memberReads += inMember;

                report.Append("  ").Append(Rel(path)).Append("\t파일 ").Append(reads)
                      .Append("건 / ").Append(ClockMemberName).Append(" 안 ")
                      .Append(inMember).Append("건\n");
            }
            Debug.Log(report.ToString());

            Assert.IsTrue(sawMemberDeclaration,
                $"{LogPrefix} {ModelTypeName}을 선언한 파일 어디에서도 {ClockMemberName}() <b>선언</b>을 " +
                "잘라 내지 못했습니다. 이름이 바뀌었다면 CurrencyModel의 클래스 문서(★★ 벽시계 절)도 " +
                "함께 낡았습니다 — 여기서 멈추는 것이 맞습니다. 그대로 두면 아래 등호가 " +
                "'0 == 0'으로 조용히 통과합니다.");

            Assert.Greater(memberReads, 0,
                $"{LogPrefix} {ClockMemberName} 안에서 시계 읽기를 하나도 찾지 못했습니다. " +
                "니들(DateTime.UtcNow 등)이 낡았거나 읽기가 다른 곳으로 옮겨졌습니다 — " +
                "어느 쪽이든 아래 등호는 지금 아무것도 재지 않습니다(부재 단언이 조용히 초록이 되는 형태).");

            Assert.AreEqual(fileReads, memberReads,
                $"{LogPrefix} 시계 읽기가 {ClockMemberName} <b>밖으로</b> 새어 나갔습니다 " +
                $"(파일 {fileReads}건 중 {memberReads}건만 그 안에 있습니다).\n" +
                "CurrencyModel의 클래스 문서는 «그 읽기는 ResolveTodayIndex 안에만 있고»라고 " +
                "단언합니다. 새 읽기가 필요하다면 그 문장을 먼저 고치세요 — 그리고 " +
                "DESIGN_SYSTEMS_STATS §23-7 (가)가 재화 코드에 시간대 읽기를 넣는 것을 금지합니다" +
                "(시간대 판정은 대사 쪽 AmbientChatter 계열에만 둡니다).");

            Assert.AreEqual(1, wallClockReads,
                $"{LogPrefix} 벽시계 «값»을 읽는 표현이 {wallClockReads}개입니다. " +
                "security T-3-a가 남긴 예외는 «오늘이 며칠인가» <b>하나</b>뿐이고 " +
                "(오프라인 정산이 폐기되면서 나머지는 0개가 됐습니다, §18-1), " +
                "그 하나가 CurrencyModel 클래스 문서의 «정확히 한 곳»입니다. " +
                "수급량 계산에는 단조 시계 델타만 씁니다 — 시계를 앞으로 돌려도 동전이 안 나오는 " +
                "성질이 이 1에 걸려 있습니다.");
        }

        /// <summary>
        /// <c>CurrencyRules</c> 클래스 문서: <i>"이 클래스의 함수는 전부 <b>인자로 받은 값</b>만 본다.
        /// <c>DateTime.Now</c>/<c>UtcNow</c>도 <c>Time.realtimeSinceStartupAsDouble</c>도 여기서 읽지 않는다"</i>.
        /// <para>이건 <b>부재 단언</b>이라 썩으면 조용히 초록이 된다. 그래서
        /// <see cref="NegativeControl_시계_읽기가_늘면_같은_스캐너가_반드시_잡는다"/>가 <b>같은 함수</b>에
        /// 가짜 소스를 흘려 탐지력을 매 실행 증명한다.</para>
        /// </summary>
        [Test]
        public void 재화_규칙은_시계를_한_번도_읽지_않는다()
        {
            List<(string Path, string Stripped)> rules = FilesDeclaring(AllProduction(), RulesTypeName);
            Assert.IsNotEmpty(rules,
                $"{LogPrefix} {RulesTypeName}을 선언하는 프로덕션 파일을 찾지 못했습니다.");

            var offenders = new List<string>();
            foreach ((string path, string stripped) in rules)
            {
                int reads = CountEnvironmentTimeReads(stripped);
                int mono = EntitlementAuditSource.CountIdentifier(stripped, MonotonicToken);
                if (reads > 0) offenders.Add($"{Rel(path)} — 시계 읽기 {reads}건");
                if (mono > 0) offenders.Add($"{Rel(path)} — 단조 시계 읽기 {mono}건");
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 순수 규칙이 시계를 읽기 시작했습니다:\n  " + string.Join("\n  ", offenders) +
                "\n이 클래스가 시계를 읽는 순간 (ㄱ) 날짜 경계를 인자로 밀어 넣는 EditMode 검증이 " +
                "불가능해지고(T-15-7-b: 이 파일 전체가 실시간 대기 없이 검증되는 근거), " +
                "(ㄴ) 같은 사실이 모델과 규칙 두 곳에서 계산됩니다. 시각은 인자로 받으세요 — " +
                "LocalDayIndex(DateTime utcNow, int offset)가 그 관례입니다.");
        }

        // ====================================================================
        // 2. ★ T-11 #2 — 부재와 존재를 <b>같은 테스트에서</b> 대조한다
        // ====================================================================

        /// <summary>
        /// SECURITY_MODEL T-11 #2가 요구한 형태 그대로다:
        /// <i>"수급·정산 타입에서 <c>Time.deltaTime</c> <b>부재</b> + <c>realtimeSinceStartupAsDouble</c>
        /// <b>존재</b>를 <b>같은 테스트에서</b> 대조"</i>.
        ///
        /// <para>★ <b>왜 한 테스트에 둘을 넣는가.</b> 부재만 재면 «배선이 아직 0줄이라 아무것도
        /// 없는 상태»와 «배선이 있고 올바른 시계를 쓰는 상태»가 <b>똑같이 초록</b>이다. 이 저장소가
        /// 반복해 당한 형태가 정확히 그것이다(실패한 측정과 성공한 측정이 똑같이 생겼다).
        /// 존재 쪽을 붙여야 «재고 있다»가 증명된다.</para>
        ///
        /// <para>★ 부재는 <b>도메인 3파일</b>에만 건다. 배선 파일에는 걸지 <b>않는다</b> —
        /// <c>CharacterProgressionDirector</c>는 <c>Time.unscaledDeltaTime</c>을 <b>주기 저장 타이머</b>에
        /// 정당하게 쓰면서(2건) 재화 롤오버·활쏘기 상금에는 <c>realtimeSinceStartupAsDouble</c>을 넘긴다
        /// (2026-09-06 2차 배선으로 2건 → <b>3건</b>: 롤오버 2 + 활쏘기 1).
        /// <b>한 파일이 두 시계를 다른 일에 쓰는 것이 정상</b>이고, 파일 단위로 금지하면
        /// 정직한 코드가 빨개진다. 인자 수준의 정밀 검사는
        /// <see cref="DailyLimitClampAuditTests"/>가 맡는다.</para>
        /// </summary>
        [Test]
        public void T11대조_수급_도메인에_프레임_델타가_없고_배선에는_단조_시계가_있다()
        {
            List<(string Path, string Stripped)> all = AllProduction();
            List<(string Path, string Stripped)> domain = DomainFiles(all);

            Assert.GreaterOrEqual(domain.Count, MinDomainFileCount,
                $"{LogPrefix} 재화 도메인 파일을 {domain.Count}개밖에 찾지 못했습니다(바닥값 " +
                $"{MinDomainFileCount}, 2026-09-06 실측 3개). '{DomainTypeFragment}'을 품은 타입 " +
                "선언으로 찾습니다 — 이름 규칙이 바뀌었다면 여기서 멈추는 것이 맞습니다.");

            // ── 부재 쪽
            var frameDeltaHits = new List<string>();
            foreach ((string path, string stripped) in domain)
            {
                foreach ((string token, string why) in FrameDeltaTokens)
                {
                    int n = EntitlementAuditSource.CountIdentifier(stripped, token);
                    if (n > 0) frameDeltaHits.Add($"{Rel(path)} — '{token}' {n}건\n      {why}");
                }
            }

            // ── 존재 쪽: 도메인을 «부르는» 프로덕션 파일 중 하나는 단조 시계를 실제로 읽어야 한다.
            var wiring = new List<string>();
            foreach ((string path, string stripped) in all)
            {
                bool isDomain = false;
                foreach ((string domainPath, string _) in domain)
                    if (domainPath == path) { isDomain = true; break; }
                if (isDomain) continue;

                if (!EntitlementAuditSource.ContainsIdentifier(stripped, ModelTypeName)) continue;
                if (EntitlementAuditSource.CountIdentifier(stripped, MonotonicToken) <= 0) continue;
                wiring.Add(Rel(path));
            }

            Debug.Log($"{LogPrefix} 도메인 {domain.Count}파일 / 단조 시계를 읽는 배선 " +
                      $"{wiring.Count}파일: {string.Join(", ", wiring)}");

            Assert.IsEmpty(frameDeltaHits,
                $"{LogPrefix} 재화 도메인이 <b>프레임 델타</b>를 읽기 시작했습니다:\n  " +
                string.Join("\n  ", frameDeltaHits) +
                "\n수급의 시간 입력은 단조 시계 두 시점의 <b>차</b>여야 합니다. 프레임 델타는 " +
                "① 엔진 상한 때문에 조용히 적게 쌓이고(T-3-c: '적게 쌓이는 방향의 공정성 문제') " +
                "② 기계가 잠든 시간을 통째로 잃습니다.");

            Assert.IsNotEmpty(wiring,
                $"{LogPrefix} 재화 도메인을 부르면서 '{MonotonicToken}'을 읽는 프로덕션 파일이 " +
                "하나도 없습니다. 위의 «프레임 델타 0건»은 이 상태에서 <b>아무 뜻도 없습니다</b> — " +
                "«올바른 시계를 쓴다»가 아니라 «시계를 아예 안 쓴다»일 뿐입니다(T-11 #2가 부재와 " +
                "존재를 같은 테스트에 넣으라고 한 이유). 배선이 정말 아직 0줄이라면 이 단언을 " +
                "지우지 말고 Assert.Ignore로 사유와 함께 남기세요.");
        }

        // ====================================================================
        // 3. 양성 대조 — 「0건」이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        [Test]
        public void 양성대조_스캐너가_실제_한_곳을_찾아내고_이름표까지_맞춘다()
        {
            List<(string Path, string Stripped)> model = FilesDeclaring(AllProduction(), ModelTypeName);
            Assert.IsNotEmpty(model, $"{LogPrefix} {ModelTypeName} 선언 파일을 찾지 못했습니다.");

            bool sawWallClock = false;
            bool sawTimeZone = false;
            foreach ((string _, string stripped) in model)
            {
                string body = MethodBodyOrNull(stripped, ClockMemberName);
                if (body == null) continue;                      // partial 조각 — 선언이 없을 수 있다

                if (CountAny(body, WallClockTokens) > 0) sawWallClock = true;
                if (CountAny(body, LocalTimeZoneTokens) > 0) sawTimeZone = true;
            }

            // 존재 단언 — 프로덕션이 바뀌면 여기서 <b>시끄럽게</b> 빨개진다(CLAUDE.md).
            Assert.IsTrue(sawWallClock,
                $"{LogPrefix} {ClockMemberName} 안에서 벽시계 니들이 하나도 안 맞았습니다. " +
                "표현이 바뀌었다면(예: 주입된 시계로 교체) 이 감사도 함께 따라가야 합니다 — " +
                "그때까지 위의 등호는 아무것도 재지 않습니다.");
            Assert.IsTrue(sawTimeZone,
                $"{LogPrefix} {ClockMemberName} 안에서 로컬 시간대 니들이 안 맞았습니다. " +
                "T-D-4(첫 실행에 오프셋 고정)를 실행하는 코드가 그 자리에 있어야 합니다.");
        }

        // ====================================================================
        // 4. 네거티브 컨트롤 — 가짜 소스를 <b>같은 함수</b>에 흘린다
        // ====================================================================

        [Test]
        public void NegativeControl_시계_읽기가_늘면_같은_스캐너가_반드시_잡는다()
        {
            foreach (string line in new[]
                     {
                         "long t = DateTime.Now.Ticks;",
                         "long t = DateTime.UtcNow.Ticks;",
                         "long t = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();",
                         "long t = DateTimeOffset.Now.ToUnixTimeSeconds();",
                         "int t = Environment.TickCount;",
                         "var off = TimeZoneInfo.Local.BaseUtcOffset;",
                     })
            {
                string fake = "public static class Fake\n{\n    static void M()\n    {\n        " + line + "\n    }\n}\n";
                Assert.Greater(CountEnvironmentTimeReads(EntitlementAuditSource.StripComments(fake)), 0,
                    $"{LogPrefix} 스캐너가 시계 읽기를 놓쳤습니다 → {line}\n" +
                    "이 상태에서는 프로덕션의 '한 곳'도 '0건'도 아무 뜻이 없습니다.");
            }
        }

        /// <summary>
        /// ★★ <b>이 저장소를 즉시 거짓 빨강으로 만들 수 있었던 함정.</b>
        /// <c>CurrencyRules</c>는 <c>DateTime</c>을 <b>타입으로</b> 쓴다 —
        /// <c>new DateTime(1970,…)</c>(유닉스 에폭 상수)와 <c>LocalDayIndex(DateTime utcNow, …)</c>
        /// (시각을 <b>인자로 받는</b> 순수 함수) 둘 다. 그게 바로 «시계를 안 읽는다»의 <b>구현 방식</b>인데,
        /// 니들을 <c>DateTime</c>으로 잡으면 <b>그 설계 자체가 위반으로 보인다.</b>
        /// </summary>
        [Test]
        public void NegativeControl_타입으로서의_DateTime은_시계_읽기가_아니다()
        {
            string fake =
                "public static class Fake\n" +
                "{\n" +
                "    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);\n" +
                "    public static int DayIndex(DateTime utcNow, int offsetMinutes)\n" +
                "    {\n" +
                "        DateTime shifted = utcNow.AddMinutes(offsetMinutes);\n" +
                "        return (int)Math.Floor((shifted - Epoch).TotalDays);\n" +
                "    }\n" +
                "}\n";

            Assert.AreEqual(0, CountEnvironmentTimeReads(EntitlementAuditSource.StripComments(fake)),
                $"{LogPrefix} <b>타입으로서의</b> DateTime을 시계 읽기로 셌습니다. " +
                "그렇게 되면 CurrencyRules.LocalDayIndex(시각을 인자로 받는 순수 함수)와 " +
                "유닉스 에폭 상수가 위반으로 잡히고, 이 감사는 첫날부터 빨간 채로 방치됩니다 — " +
                "방치된 감사는 없는 감사입니다.");
        }

        [Test]
        public void NegativeControl_주석_속_시계_언급은_읽기가_아니다()
        {
            // 실제 상황이다: CurrencyRules 클래스 문서가 «DateTime.Now/UtcNow도 여기서 읽지 않는다»고
            // 적고 있고, CurrencyModel 문서도 T-3-a 원문을 인용한다. 주석을 세면 두 파일 다 빨개진다.
            string fake =
                "/// <summary>DateTime.Now / DateTime.UtcNow 를 여기서 읽지 않는다.</summary>\n" +
                "public static class Fake\n" +
                "{\n" +
                "    // TimeZoneInfo.Local 도 안 본다 — 오프셋은 인자로 받는다.\n" +
                "    /* Environment.TickCount 는 24.9일에 감긴다. */\n" +
                "    public static int M(int x) => x;\n" +
                "}\n";

            Assert.AreEqual(0, CountEnvironmentTimeReads(EntitlementAuditSource.StripComments(fake)),
                $"{LogPrefix} 주석 속 언급을 읽기로 셌습니다. 재화 파일들은 «무엇을 읽지 않는지»를 " +
                "주석에 길게 적어 두는 관례라, 주석을 세면 정확히 그 정직한 문서 때문에 빨개집니다.");
        }

        /// <summary>
        /// ★ 등호의 <b>탐지 방향</b>을 증명한다: 읽기가 지목된 메서드 <b>밖으로</b> 나가면 잡히는가.
        /// 이게 없으면 위 <c>AreEqual(fileReads, memberReads)</c>는 «항상 같아서 통과»일 수도 있다.
        /// </summary>
        [Test]
        public void NegativeControl_지목된_메서드_밖으로_새어_나간_읽기를_잡는다()
        {
            const string good =
                "public static class Fake\n" +
                "{\n" +
                "    private static int ResolveTodayIndex()\n" +
                "    {\n" +
                "        DateTime utcNow = DateTime.UtcNow;\n" +
                "        return (int)TimeZoneInfo.Local.GetUtcOffset(utcNow).TotalMinutes;\n" +
                "    }\n" +
                "    public static bool Tick() { return ResolveTodayIndex() > 0; }\n" +
                "}\n";

            string strippedGood = EntitlementAuditSource.StripComments(good);
            string bodyGood = MethodBodyOrNull(strippedGood, ClockMemberName);
            Assert.IsNotNull(bodyGood,
                $"{LogPrefix} 선언을 못 잘랐습니다 — 아래 등호가 전부 무의미해집니다.");
            Assert.AreEqual(CountEnvironmentTimeReads(strippedGood), CountEnvironmentTimeReads(bodyGood),
                $"{LogPrefix} 전부 그 메서드 안에 있는데도 등호가 깨졌습니다. " +
                "MethodBodyOrNull이 <b>호출부</b>를 선언으로 오인했을 가능성이 큽니다 " +
                "(그러면 프로덕션에서 거짓 빨강이 납니다).");

            const string leaked =
                "public static class Fake\n" +
                "{\n" +
                "    private static int ResolveTodayIndex()\n" +
                "    {\n" +
                "        DateTime utcNow = DateTime.UtcNow;\n" +
                "        return utcNow.Day;\n" +
                "    }\n" +
                "    public static bool Tick()\n" +
                "    {\n" +
                "        var hour = DateTime.Now.Hour;            // ← 새어 나간 읽기\n" +
                "        return ResolveTodayIndex() > 0 && hour < 24;\n" +
                "    }\n" +
                "}\n";

            string strippedLeak = EntitlementAuditSource.StripComments(leaked);
            string bodyLeak = MethodBodyOrNull(strippedLeak, ClockMemberName);
            Assert.IsNotNull(bodyLeak);
            Assert.AreNotEqual(CountEnvironmentTimeReads(strippedLeak), CountEnvironmentTimeReads(bodyLeak),
                $"{LogPrefix} 지목된 메서드 <b>밖</b>의 읽기를 못 잡았습니다. " +
                "그러면 «시간대 대사를 재화 코드에 넣지 마라»(§23-7 가)가 아무도 안 보는 규칙이 됩니다.");
        }

        [Test]
        public void NegativeControl_호출부를_선언으로_오인하지_않는다()
        {
            // ★ 호출이 선언보다 <b>먼저</b> 나오는 배치. 순서에 기대는 파서라면 여기서 무너진다.
            const string source =
                "public static class Fake\n" +
                "{\n" +
                "    public static bool Tick() { return ResolveTodayIndex() > 0; }\n" +
                "    private static int ResolveTodayIndex()\n" +
                "    {\n" +
                "        DateTime utcNow = DateTime.UtcNow;\n" +
                "        return utcNow.Day;\n" +
                "    }\n" +
                "}\n";

            string body = MethodBodyOrNull(EntitlementAuditSource.StripComments(source), ClockMemberName);
            Assert.IsNotNull(body,
                $"{LogPrefix} 호출이 먼저 나오면 선언을 못 찾습니다 — 파서가 첫 매치에서 포기했습니다.");
            Assert.Greater(CountEnvironmentTimeReads(body), 0,
                $"{LogPrefix} <b>호출 한 줄</b>을 본문으로 잘랐습니다. 그러면 내부 개수가 0이 되어 " +
                "«메서드 밖으로 샜다»는 거짓 빨강이 납니다.");
        }

        [Test]
        public void 니들표는_비어_있지_않다()
        {
            // 거짓 통과 #5 — 명부가 비면 foreach가 아무것도 안 재고 초록이 된다.
            Assert.IsNotEmpty(WallClockTokens, $"{LogPrefix} 벽시계 토큰표가 비었습니다.");
            Assert.IsNotEmpty(LocalTimeZoneTokens, $"{LogPrefix} 시간대 토큰표가 비었습니다.");
            Assert.IsNotEmpty(FrameDeltaTokens, $"{LogPrefix} 프레임 델타 토큰표가 비었습니다.");

            foreach ((string token, string why) in WallClockTokens)
                Assert.IsNotEmpty(why, $"{LogPrefix} '{token}'의 사유가 비었습니다 — 근거를 못 대는 " +
                                       "토큰은 오탐 확률만 올립니다.");
            foreach ((string token, string why) in FrameDeltaTokens)
                Assert.IsNotEmpty(why, $"{LogPrefix} '{token}'의 사유가 비었습니다.");
        }
    }
}
