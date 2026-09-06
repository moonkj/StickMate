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
    /// T-15-7 (A) — <b>상한은 필드가 아니라 함수다 / 로드값은 클램프를 지난다</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// 규범: <c>docs/security/SECURITY_MODEL.md</c> T-14-5-b · T-14-9 #3 · T-15-7 (A) · T-D-9.
    ///
    /// ============================================================================
    /// ★★ 이 파일도 <b>인용이 먼저 있고 파일이 나중에 생겼다</b>
    /// ============================================================================
    /// <c>Core/CurrencyRules.cs</c>가 <b>두 곳</b>에서 이 테스트를 근거로 인용하고 있었다:
    /// <list type="number">
    ///   <item>클래스 문서 §T-D-9: <i>"세이브 스키마에 <c>dailyCap</c>/<c>todayLimit</c>류 필드를
    ///     만들지 마라 — <c>Tests/EditMode/DailyLimitClampAuditTests</c>가 <b>그 부재</b>를
    ///     매 실행 확인한다."</i></item>
    ///   <item><c>IdleTick</c> 문서: <i>"벽시계 델타를 넣으면 T-3-a가 깨진다 — 이 함수는 그것을 알 수
    ///     없으므로 <b>호출부의 책임</b>이고, <c>Tests/EditMode/DailyLimitClampAuditTests</c>가
    ///     <b>소스 스캔</b>으로 그 책임을 잠근다."</i></item>
    /// </list>
    /// <b>그 파일은 없었다.</b> 두 문장 모두 현재형 단언이라, 읽는 사람은 잠금이 있다고 믿는다.
    /// 그래서 이 파일이 잠그는 것은 정확히 <b>그 두 문장 + T-15-7 (A) 표의 세 줄</b>이다.
    ///
    /// ============================================================================
    /// 네 가지를 잰다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>부재</b> — 직렬화 스키마에 <c>dailyCap</c>/<c>todayLimit</c>/<c>windowCap</c> 계열
    ///     필드가 없다(T-D-9: 상한을 파일에 적으면 그 숫자가 위조 대상이 되고, 재계산된 숫자는
    ///     위조 대상이 아니다).</item>
    ///   <item><b>존재</b> — 로드 경로가 일일 래칫 세 값을 클램프에 통과시킨다(T-14-5-b).
    ///     <b>부재만 재면 «측정 0건»과 «위반 0건»이 똑같이 생긴다</b>(CLAUDE.md).</item>
    ///   <item><b>호출부 책임</b> — 단조 시계를 받기로 한 진입점에 벽시계·프레임 델타를 넘기지 않는다.</item>
    ///   <item><b>네거티브 컨트롤</b>(T-15-7 A 3행, <b>필수</b>) — 가짜 <c>public int dailyCapCoins;</c>를
    ///     <b>같은 스캔 함수</b>에 흘려 실제로 잡히는지 매 실행 증명한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 호출부 검사는 <b>인자 표현식까지</b>다 — 그 너머로 넓히지 마라
    /// ============================================================================
    /// 「인자로 넘긴 변수가 <b>어디서 왔는지</b>」까지 따라가고 싶어질 텐데, 실측하면 그 순간
    /// <b>의도된 설계가 빨개진다</b>: <c>Interaction/FocusWatchDirector</c>의 취소 지급은
    /// <c>명목 − 잔여</c>이고 그 <c>잔여</c>는 <c>Time.deltaTime</c>으로 깎이는 <b>카운트다운</b>이다.
    /// 그건 «수급 델타»가 아니라 «남은 시간»이고, 지급이 <b>분 단위로 내림</b>되므로 프레임 오차가
    /// 결과를 바꾸지 않는다(§22-12). 그리고 <c>FocusCompletionCoins</c>/<c>FocusCancelCoins</c>는
    /// <b>시각을 받는 함수가 아니라 길이를 받는 함수</b>라 이 검사의 사거리 밖이다.
    /// <para><b>«코드가 그렇게 생겼나»로 잴 수 있는 것은 여기까지다.</b> «코드가 그렇게 계산하나»는
    /// <c>Tests/EditMode/CurrencyRulesTests.cs</c>·<c>CurrencyDayRolloverTests.cs</c>가 순수 함수
    /// 동작으로 잰다 — 섞으면 둘 다 약해진다(T-15-7 서문).</para>
    ///
    /// ============================================================================
    /// 짝이 되는 감사
    /// ============================================================================
    /// <see cref="IncomeTimeSourceAuditTests"/>가 <b>"어느 시계를 읽는가"</b>를 맡는다.
    /// 이 파일은 <b>"읽은 값을 어디에 넣는가"</b>와 <b>"저장 스키마가 어떻게 생겼는가"</b>다.
    /// </summary>
    public sealed class DailyLimitClampAuditTests
    {
        private const string LogPrefix = "[일일상한감사]";

        // ====================================================================
        // 1군 — 부재: 「상한은 필드가 아니라 함수다」(T-D-9)
        // ====================================================================

        /// <summary>
        /// 금지 <b>낱말 쌍</b>과 사유. <c>EntitlementNotInSaveAuditTests</c>의 필드명 스캐너를
        /// <b>토큰만 바꿔 재사용</b>한다(T-15-7 A 2행).
        ///
        /// <para>★ <b>왜 낱말 하나가 아니라 「인접한 두 낱말」인가.</b> 저쪽은 <c>dlc</c>·<c>pack</c>처럼
        /// 낱말 하나로 충분했지만 여기는 아니다 — 실재하는 정직한 필드가
        /// <c>todayGrantedCoins</c>(<c>today</c>) · <c>idleWindowUsedSeconds</c>(<c>window</c>)이고,
        /// 낱말 하나로 잡으면 <b>그 둘이 첫날에 빨개진다</b>. 인접 쌍으로 좁히면
        /// <c>dailyCapCoins</c>·<c>todayLimit</c>·<c>windowCapSeconds</c>는 잡히고 그 둘은 안 잡힌다
        /// (아래 양방향 대조가 매 실행 증명한다).</para>
        /// </summary>
        private static readonly (string First, string Second, string Why)[] ForbiddenFieldPairs =
        {
            ("daily", "cap",
                "오늘의 상한. T-D-9 — 파일에 적으면 그 숫자가 위조 대상이 되고, 재계산된 숫자는 아니다. " +
                "그리고 회복제 개수가 이미 저장되므로 상한은 그것의 함수로 언제든 복원된다."),
            ("daily", "limit",
                "같은 것의 다른 이름. 이름을 바꿔 우회하는 것을 막는다."),
            ("today", "limit",
                "T-D-9가 명시적으로 지목한 이름(dailyCap/todayLimit 계열)."),
            ("today", "cap",
                "같은 계열."),
            ("window", "cap",
                "8시간 창의 상한. T-15가 요구한 세 번째 이름 — 창 상한을 저장하면 " +
                "그 값을 늘려 창을 무한대로 만들 수 있다(창의 존재 이유가 사라진다)."),
        };

        /// <summary>
        /// 스캔이 공허해지는 것을 막는 <b>바닥값</b>. 2026-09-06 실측은 프로덕션 전체
        /// <c>[Serializable]</c> public 필드 <b>93개</b>다. 60 아래로 떨어졌다면 그건
        /// "필드가 줄었다"가 아니라 <b>파서가 눈이 멀었다</b>로 읽어야 한다.
        /// <para>정확한 개수를 등호로 걸지 <b>않는</b> 이유: 필드는 정상적으로 늘어난다.
        /// 등호는 기능 추가마다 빨개져서 몇 번 만에 꺼지고, 꺼진 감사는 없는 감사다.</para>
        /// </summary>
        private const int MinSerializedFieldCount = 60;

        /// <summary>
        /// ★ <b>일부러 비어 있다.</b> 정당한 이유로 위 쌍에 걸리는 필드가 생기면 근거와 함께 등록한다.
        /// 비어 있다는 사실 자체를 <see cref="면제표는_오늘_비어_있다"/>가 단언한다 —
        /// 이 저장소의 거짓 통과 #5(명부가 비어 <c>foreach</c>가 아무것도 안 재고 초록)를 막는 장치다.
        /// </summary>
        private static readonly (string Field, string Why)[] FieldExemptions = { };

        // ====================================================================
        // 2군 — 존재: 로드 경로의 클램프 (T-14-5-b)
        // ====================================================================

        /// <summary>재화 모델 타입. <b>존재 단언</b>으로 쓴다 — 이름이 바뀌면 시끄럽게 빨개진다.</summary>
        private const string ModelTypeName = "CurrencyModel";

        /// <summary>로드 경로. <c>CurrencyModel</c>이 저장 파일에서 값을 되살리는 유일한 진입점이다.</summary>
        private const string LoadMethodName = "RestoreFromSave";

        /// <summary>순수 규칙 타입(클램프 함수들이 사는 곳).</summary>
        private const string RulesTypeName = "CurrencyRules";

        /// <summary>클램프 함수 이름의 접두. <c>ClampPotionsUsed</c>·<c>ClampGrantedCoins</c>… .</summary>
        private const string ClampPrefix = "Clamp";

        /// <summary>정규화 함수(클램프와 같은 역할을 하는 다른 이름 — 배열/목록 쪽).</summary>
        private const string NormalizePrefix = "Normalize";

        /// <summary>
        /// ★ <b>반드시 클램프를 지나야 하는 일일 래칫 세 값</b>(T-15-7 A 1행).
        /// 문서는 <c>windowSecondsUsedToday</c>라고 적었지만 실제 구현 이름은
        /// <c>IdleWindowUsedSeconds</c>다 — <b>구현 쪽 이름을 쓴다</b>(문서가 아니라 코드를 잰다).
        ///
        /// <para>전부 <b>존재 단언</b>이다: 로드 경로에서 이 이름의 <b>대입문</b>을 찾지 못하면
        /// 그 자리에서 실패한다. 이름이 바뀌면 조용히 초록이 되는 대신 시끄럽게 빨개진다(CLAUDE.md).</para>
        /// </summary>
        private static readonly (string Member, string Why)[] MustClampOnLoad =
        {
            ("PotionsUsedToday",
                "I-12′의 유일한 방어선. 세이브에 9999를 써 넣으면 상한이 그대로 밀려 올라가고 " +
                "위조 순이득이 무한이 된다."),
            ("TodayGrantedCoins",
                "오늘 지급분. 음수를 써 넣으면 오늘 상한이 그만큼 넓어진다."),
            ("IdleWindowUsedSeconds",
                "8시간 창. ★ NaN을 0이 아니라 상한으로 보내는 유일한 클램프라(T-15-1-c) " +
                "이게 빠지면 손상된 파일이 창을 리셋하는 무료 우회가 된다."),
        };

        /// <summary>
        /// 로드 경로에서 클램프를 <b>안 지나도 되는</b> 저장 값과 그 사유.
        /// <para>★ 이 명부는 <b>썩지 않는다</b> — <see cref="로드경로_면제항목이_아직_로드경로에_있다"/>가
        /// 각 항목이 여전히 로드 경로에 실재하는지 확인한다. 없어졌으면 빨개져서 지우라고 말한다.</para>
        /// </summary>
        private static readonly (string Member, string Why)[] LoadClampExemptions =
        {
            ("SeedGranted",           "bool — 범위 밖 값이 존재하지 않는다."),
            ("TodoCoinPaidToday",     "bool — 같음."),
            ("DayBoundaryOffsetSaved","bool — 같음(동반 불리언). 짝인 분(minutes) 값은 클램프를 지난다."),
            ("PurchasedItemIds",      "문자열 목록 — 수치 범위가 없다. 빈 값·중복은 바로 아래 루프가 거른다."),
            ("StatTierReached",       "배열 — 원소가 루프 안에서 ClampStatTier를 지난다. " +
                                      "읽는 표현식 자체에는 클램프가 없어 여기 적는다."),
            ("DayIndex",              "인라인 삼항(< 0 ? 0 :)으로 하한만 건다. 상한이 없는 것이 의도다 — " +
                                      "래칫이라 전진만 하고, 큰 값은 «미래로 밀렸다»일 뿐 이득이 아니다."),
            ("ItemGraceBaselines",    "U-2 자리. v10에서는 읽은 그대로 다시 쓰기만 한다(§20-4) — " +
                                      "무손실 왕복이 목적이라 여기서 다듬으면 그 목적이 깨진다."),
        };

        // ====================================================================
        // 3군 — 호출부 책임 (CurrencyRules.IdleTick 문서)
        // ====================================================================

        /// <summary>
        /// <b>단조 시계에서 온 값만</b> 받기로 문서에 적힌 진입점들.
        /// <para><c>PayFocusCompletionCoins</c>/<c>PayFocusCancelCoins</c>는 <b>없다</b> —
        /// 그건 시각이 아니라 <b>세션 길이</b>를 받는 함수다(클래스 문서 「사거리」 절).</para>
        /// </summary>
        private static readonly (string Name, string Why)[] MonotonicEntryPoints =
        {
            ("IdleTick",             "인자 deltaSeconds는 단조 시계 델타여야 한다(문서 원문)."),
            ("TickIdleIncome",       "위 함수를 그대로 통과시키는 모델 쪽 진입점."),
            ("TickDayRollover",      "nowMonotonic — 리필 최소 간격(T-14-3-a)을 재는 시계."),
            ("TryAwardArcheryCoins", "nowMonotonic — 상금 쿨다운. 벽시계 유닉스 초를 쓰던 옛 설계는 " +
                                     "시계를 600초 되감으면 즉시 재지급됐다(§20-3)."),
            ("TickIfDue",            "주기 게이트. 이름이 트리에서 유일하도록 지은 것이 계약의 일부다."),
            ("CheckNow",             "주기를 무시하는 즉시 판정. 같은 시계를 받는다."),
        };

        /// <summary>호출부 인자에 나오면 안 되는 것들.</summary>
        private static readonly (string Token, string Why)[] ForbiddenArgumentTokens =
        {
            ("DateTime.Now",          "벽시계. 시계를 앞으로 돌리면 그대로 따라간다."),
            ("DateTime.UtcNow",       "벽시계(UTC). 일자 «판정» 밖에서는 쓸 자리가 없다."),
            ("DateTimeOffset.Now",    "같은 것."),
            ("DateTimeOffset.UtcNow", "같은 것."),
            ("Environment.TickCount", "int32라 ~24.9일에 감긴다 — 상주 데스크톱에서 반드시 터진다."),
            ("Time.time",             "타임스케일에 묶인 게임 시각. 0으로 만들면 시간이 멈춘다."),
            ("Time.unscaledTime",     "float라 장시간 상주에서 정밀도가 무너진다(그래서 …AsDouble이다)."),
            ("deltaTime",             "프레임 델타. 엔진 상한 때문에 조용히 적게 쌓인다(T-3-c)."),
            ("unscaledDeltaTime",     "프레임 델타라는 점은 같다 — 기계가 잠든 시간을 잃는다."),
            ("fixedDeltaTime",        "물리 스텝. 수급과 아무 관계가 없다."),
            ("smoothDeltaTime",       "평활값이라 합이 실제 경과와 다르다."),
        };

        /// <summary>호출부가 몇 개는 잡혀야 스캔이 공허하지 않은가.
        /// 2026-09-06 실측 5건(<c>IdleTick</c> 1 · <c>TickDayRollover</c> 1 · <c>CheckNow</c> 2 ·
        /// <c>TickIfDue</c> 1). <c>TickIdleIncome</c>·<c>TryAwardArcheryCoins</c>는 아직 0건이다.
        /// <para>★ 2026-09-06(2차 배선) 정정 — <c>TryAwardArcheryCoins</c>가 <b>1건이 됐다</b>
        /// (<c>Interaction/CharacterProgressionDirector.cs</c>의 정중앙 명중 훅). 그래서 실측은 6건이다.
        /// 바닥값은 <b>일부러 올리지 않는다</b> — 이 상수의 목적은 «스캐너가 죽었는가»를 잡는 것이지
        /// 배선 개수를 못박는 것이 아니고, 배선 실재는 전용 테스트
        /// (<c>Tests/EditMode/CurrencySeedAndArcheryWiringTests</c>)가 잰다.
        /// ★★ 그 뒤 같은 날 3차 배선으로 <c>TickIdleIncome</c>도 <b>1건이 됐다</b>
        /// (<c>Interaction/CharacterProgressionDirector.Update</c>). ⇒ 실측 7건.
        /// 바닥값은 여전히 올리지 않는다 — 이 상수는 «배선 개수»가 아니라 «스캐너 생존»을 잰다.
        /// 유휴 배선의 계약은 아래 <see cref="유휴_수급_배선이_단조_델타와_집중세션_분기를_지킨다"/>가 잠근다.</para></summary>
        private const int MinProductionCallSites = 4;

        // ====================================================================
        // 스캐너 — 전부 순수 함수. 네거티브 컨트롤이 <b>같은 함수</b>에 가짜 소스를 흘린다.
        // ====================================================================

        private struct FieldScan
        {
            public List<string> TypeNames;
            public List<string> FieldNames;
            public List<string> Violations;
        }

        /// <summary>
        /// 주석 제거본에서 <c>[Serializable]</c> 스키마를 발견하고 필드 <b>이름</b>을 금지 쌍과 대조한다.
        /// <b>값·문자열 리터럴은 보지 않는다</b> — 값은 위반이 아니다.
        /// </summary>
        private static FieldScan ScanFields(string strippedSource)
        {
            var scan = new FieldScan
            {
                TypeNames = EntitlementAuditSource.SerializableTypeNames(strippedSource),
                FieldNames = new List<string>(),
                Violations = new List<string>(),
            };

            var exempt = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string field, string why) in FieldExemptions) exempt[field] = why;

            foreach (string typeName in scan.TypeNames)
            {
                string body = EntitlementAuditSource.TypeBodyOrNull(strippedSource, typeName);
                if (body == null)
                {
                    scan.Violations.Add($"직렬화 타입 {typeName}의 본문을 잘라 내지 못했습니다 — " +
                        "파서가 이 타입을 통째로 못 보고 있습니다(위반 0건이 아니라 측정 0건입니다).");
                    continue;
                }

                foreach (string field in EntitlementAuditSource.PublicFieldNames(body))
                {
                    scan.FieldNames.Add(field);
                    if (exempt.ContainsKey(field)) continue;

                    List<string> segments = EntitlementAuditSource.CamelSegments(field);
                    foreach ((string first, string second, string why) in ForbiddenFieldPairs)
                    {
                        if (!HasAdjacentSegments(segments, first, second)) continue;
                        scan.Violations.Add($"{typeName}.{field} — 금지 쌍 '{first}'+'{second}'\n      {why}");
                        break;
                    }
                }
            }
            return scan;
        }

        /// <summary>낱말 조각 목록에 <paramref name="first"/> 바로 뒤에 <paramref name="second"/>가
        /// 오는 자리가 있는가(접두 비교). <c>dailyCapCoins</c> → <c>daily</c>·<c>cap</c>·<c>coins</c>.</summary>
        internal static bool HasAdjacentSegments(List<string> segments, string first, string second)
        {
            if (segments == null) return false;
            for (int i = 0; i + 1 < segments.Count; i++)
            {
                if (!segments[i].StartsWith(first, StringComparison.Ordinal)) continue;
                if (!segments[i + 1].StartsWith(second, StringComparison.Ordinal)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// <paramref name="methodName"/> <b>선언</b>의 괄호 안(파라미터 목록)을 돌려준다.
        /// 못 찾으면 <c>null</c>. 파라미터 <b>이름</b>을 니들로 베끼지 않기 위한 것이다 —
        /// 베껴 두면 이름이 바뀌는 날 «<c>state.</c>가 0건»이 되어 조용히 초록이 된다.
        /// </summary>
        internal static string DeclaredParameterListOrNull(string stripped, string methodName)
        {
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

                int after = q + 1;
                while (after < stripped.Length && char.IsWhiteSpace(stripped[after])) after++;
                if (after >= stripped.Length || stripped[after] != '{') continue;   // 호출부는 건너뛴다

                return stripped.Substring(p + 1, q - p - 1);
            }
        }

        /// <summary>파라미터 목록에서 <b>첫 파라미터의 이름</b>. <c>"CurrencySaveState state"</c> → <c>state</c>.</summary>
        internal static string FirstParameterNameOrNull(string parameterList)
        {
            if (string.IsNullOrEmpty(parameterList)) return null;
            string first = parameterList.Split(',')[0].Trim();
            if (first.Length == 0) return null;
            string[] parts = first.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[parts.Length - 1] : null;
        }

        /// <summary>
        /// <paramref name="index"/>를 품은 <b>문장</b>을 잘라 낸다 —
        /// 직전 <c>;</c>·<c>{</c>·<c>}</c> 다음부터 다음 <c>;</c>까지.
        /// <para>★ <b>줄 단위로 자르지 않는 이유</b>: 실제 로드 경로의
        /// <c>DayBoundaryOffsetMinutes = … ? CurrencyRules.Clamp…(…) : 0;</c>이 <b>세 줄</b>에 걸쳐 있다.
        /// 줄로 자르면 그 클램프를 못 보고 <b>거짓 빨강</b>이 난다.</para>
        /// </summary>
        internal static string EnclosingStatement(string body, int index)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            if (index < 0) index = 0;
            if (index >= body.Length) index = body.Length - 1;

            int start = 0;
            for (int i = index; i >= 0; i--)
            {
                char c = body[i];
                if (c == ';' || c == '{' || c == '}') { start = i + 1; break; }
            }

            int end = body.IndexOf(';', index);
            if (end < 0) end = body.Length - 1;
            return body.Substring(start, end - start + 1);
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>
        /// 로드 경로에서 «저장 파일 값을 읽는 문장»을 전부 모은다.
        /// 키는 읽은 <b>멤버 이름</b>, 값은 그 문장 전문.
        /// </summary>
        internal static Dictionary<string, string> ReadStatementsByMember(string loadBody, string parameterName)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(loadBody) || string.IsNullOrEmpty(parameterName)) return result;

            string needle = parameterName + ".";
            int from = 0;
            while (true)
            {
                int at = loadBody.IndexOf(needle, from, StringComparison.Ordinal);
                if (at < 0) return result;
                from = at + needle.Length;

                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(loadBody[before])) continue;

                int p = at + needle.Length;
                int start = p;
                while (p < loadBody.Length && IsIdentifierChar(loadBody[p])) p++;
                if (p == start) continue;

                string member = loadBody.Substring(start, p - start);
                string statement = EnclosingStatement(loadBody, at);

                // 같은 멤버가 여러 문장에서 읽히면(널 검사 + 인덱싱 등) 클램프를 <b>담은</b> 쪽을 남긴다.
                if (result.TryGetValue(member, out string existing) && IsSanitized(existing)) continue;
                result[member] = statement;
            }
        }

        /// <summary>그 문장이 값을 다듬고 있는가(클램프 또는 정규화).</summary>
        internal static bool IsSanitized(string statement)
            => statement != null
               && (statement.IndexOf(ClampPrefix, StringComparison.Ordinal) >= 0
                   || statement.IndexOf(NormalizePrefix, StringComparison.Ordinal) >= 0);

        /// <summary>
        /// <paramref name="entryName"/>의 <b>호출부</b>를 전부 찾아 인자 목록 원문을 돌려준다.
        /// <b>선언부는 뺀다</b> — 닫는 괄호 뒤가 <c>{</c>거나 <c>=&gt;</c>면 선언이다.
        /// </summary>
        internal static List<string> CallArguments(string stripped, string entryName)
        {
            var args = new List<string>();
            if (string.IsNullOrEmpty(stripped)) return args;

            int from = 0;
            while (true)
            {
                int at = stripped.IndexOf(entryName, from, StringComparison.Ordinal);
                if (at < 0) return args;
                from = at + 1;

                int before = at - 1;
                if (before >= 0 && IsIdentifierChar(stripped[before])) continue;

                int p = at + entryName.Length;
                if (p >= stripped.Length || stripped[p] != '(') continue;

                int depth = 0;
                int q = p;
                for (; q < stripped.Length; q++)
                {
                    if (stripped[q] == '(') depth++;
                    else if (stripped[q] == ')') { depth--; if (depth == 0) break; }
                }
                if (q >= stripped.Length) continue;

                int after = q + 1;
                while (after < stripped.Length && char.IsWhiteSpace(stripped[after])) after++;
                if (after < stripped.Length && stripped[after] == '{') continue;          // 선언(블록 본문)
                if (after + 1 < stripped.Length
                    && stripped[after] == '=' && stripped[after + 1] == '>') continue;    // 선언(식 본문)

                args.Add(stripped.Substring(p + 1, q - p - 1));
            }
        }

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

        private static string Rel(string path)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return path.Length > root.Length ? path.Substring(root.Length + 1) : path;
        }

        /// <summary>로드 경로 본문 + 그 파라미터 이름. 못 찾으면 <c>Assert</c>로 즉시 실패한다.</summary>
        private static (string Body, string ParameterName, string Path) LoadPath()
        {
            foreach ((string path, string stripped) in AllProduction())
            {
                if (!EntitlementAuditSource.DeclaresType(stripped, ModelTypeName)) continue;

                string body = IncomeTimeSourceAuditTests.MethodBodyOrNull(stripped, LoadMethodName);
                if (body == null) continue;

                string parameters = DeclaredParameterListOrNull(stripped, LoadMethodName);
                string parameterName = FirstParameterNameOrNull(parameters);
                if (parameterName == null) continue;

                return (body, parameterName, path);
            }
            return (null, null, null);
        }

        // ====================================================================
        // 1. 부재 — 상한은 필드가 아니라 함수다
        // ====================================================================

        [Test]
        public void 직렬화_스키마에_상한_필드가_하나도_없다()
        {
            List<(string Path, string Stripped)> all = AllProduction();
            Assert.GreaterOrEqual(all.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Count}개밖에 읽지 못했습니다 " +
                $"({EntitlementAuditSource.ScriptsRoot}). 이 상태의 '위반 0건'은 측정이 아닙니다.");

            Assert.IsNotEmpty(ForbiddenFieldPairs,
                $"{LogPrefix} 금지 쌍 표가 비었습니다 — 아래 대조가 통째로 공허해집니다(거짓 통과 #5).");

            var violations = new List<string>();
            var fields = new List<string>();
            var report = new StringBuilder(LogPrefix).Append(" 직렬화 스키마 스캔\n");

            foreach ((string path, string stripped) in all)
            {
                FieldScan scan = ScanFields(stripped);
                if (scan.TypeNames.Count == 0) continue;

                fields.AddRange(scan.FieldNames);
                foreach (string v in scan.Violations) violations.Add($"  · {Path.GetFileName(path)} :: {v}");

                report.Append("  ").Append(Path.GetFileName(path)).Append('\t')
                      .Append(scan.TypeNames.Count).Append("타입 / ")
                      .Append(scan.FieldNames.Count).Append("필드\n");
            }
            report.Append("  합계 직렬화 필드 ").Append(fields.Count).Append("개\n");
            Debug.Log(report.ToString());

            Assert.GreaterOrEqual(fields.Count, MinSerializedFieldCount,
                $"{LogPrefix} 직렬화 필드를 {fields.Count}개밖에 세지 못했습니다(바닥값 " +
                $"{MinSerializedFieldCount}개, 2026-09-06 실측 93개). 필드가 정말 줄어든 것이 아니라면 " +
                "파서가 눈이 먼 것입니다 — 이 상태의 '위반 0건'은 측정이 아닙니다.");

            Assert.IsEmpty(violations,
                $"{LogPrefix} 직렬화 스키마에 <b>상한을 뜻하는 필드</b>가 생겼습니다({violations.Count}건):\n"
                + string.Join("\n", violations) + "\n\n" +
                "T-D-9: 「오늘의 상한」을 파일에 적으면 그 숫자가 <b>위조 대상</b>이 되고, " +
                "재계산된 숫자는 위조 대상이 아닙니다. 상한은 회복제 개수의 함수로 언제든 복원되므로 " +
                "저장할 이유가 없습니다(CurrencyRules.DailyCapCoins). " +
                "정말 필요하다면 그건 T-D-9를 다시 여는 결정이고, 이 감사가 빨개지는 것이 맞습니다.");
        }

        [Test]
        public void 면제표는_오늘_비어_있다()
        {
            Assert.AreEqual(0, FieldExemptions.Length,
                $"{LogPrefix} 면제가 {FieldExemptions.Length}건 생겼습니다. 이 테스트는 면제가 " +
                "<b>조용히</b> 늘어나는 것을 막기 위해 일부러 실패합니다 — 각 항목의 근거를 읽고, " +
                "정말 상한 저장이 아니라면 이 기대값을 함께 올리세요.");
        }

        // ====================================================================
        // 2. 존재 — 로드 경로가 값을 다듬는다 (T-14-5-b)
        // ====================================================================

        [Test]
        public void 로드경로가_일일_래칫_세_값을_클램프에_통과시킨다()
        {
            (string body, string parameterName, string path) = LoadPath();
            Assert.IsNotNull(body,
                $"{LogPrefix} {ModelTypeName}.{LoadMethodName}(…) 선언을 찾지 못했습니다. " +
                "이름이 바뀌었거나 로드 경로가 옮겨졌습니다 — 여기서 멈추는 것이 맞습니다. " +
                "그대로 두면 아래 모든 '통과'가 '아무것도 안 봤음'이 됩니다.");

            Dictionary<string, string> reads = ReadStatementsByMember(body, parameterName);
            Assert.GreaterOrEqual(reads.Count, MustClampOnLoad.Length + LoadClampExemptions.Length,
                $"{LogPrefix} 로드 경로에서 '{parameterName}.' 읽기를 {reads.Count}건밖에 " +
                "찾지 못했습니다. 파서가 눈이 멀었습니다 — 이 상태의 '전부 클램프됨'은 측정이 아닙니다.\n" +
                "찾은 것: " + string.Join(", ", reads.Keys));

            Debug.Log($"{LogPrefix} {Rel(path)} :: {LoadMethodName}({parameterName}) — " +
                      $"저장값 읽기 {reads.Count}건 / 다듬는 것 " +
                      $"{CountSanitized(reads)}건");

            var missing = new List<string>();
            foreach ((string member, string why) in MustClampOnLoad)
            {
                if (!reads.TryGetValue(member, out string statement))
                {
                    missing.Add($"{member} — 로드 경로에서 <b>읽지도 않습니다</b>. 이름이 바뀌었다면 " +
                                $"이 명부를 함께 고치세요.\n      {why}");
                    continue;
                }
                if (IsSanitized(statement)) continue;
                missing.Add($"{member} — 클램프 없이 그대로 씁니다.\n      {why}\n      그 문장: {statement.Trim()}");
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 로드 경로가 저장값을 <b>그대로 믿고</b> 있습니다:\n  " +
                string.Join("\n  ", missing) + "\n\n" +
                "T-14-5-b: 이건 방어이기 전에 <b>위생</b>입니다. 세이브는 평문 JSON이고 앞으로도 " +
                "평문입니다 — 값을 거부하지 말고 <b>다듬으세요</b>(경고도 실패도 남기지 않습니다). " +
                "손상된 파일·구버전 파일에도 같은 코드가 같은 답을 내야 합니다.");
        }

        private static int CountSanitized(Dictionary<string, string> reads)
        {
            int n = 0;
            foreach (KeyValuePair<string, string> e in reads) if (IsSanitized(e.Value)) n++;
            return n;
        }

        /// <summary>
        /// ★ <b>새 저장 필드가 클램프 없이 슬쩍 들어오는 것</b>을 잡는다.
        /// 위 테스트는 <b>아는 세 값</b>만 보므로, 네 번째 값이 생기면 못 본다.
        /// <para>면제는 <see cref="LoadClampExemptions"/>에 <b>사유와 함께</b> 적어야 통과한다 —
        /// 사람이 한 번 읽고 판단하게 만드는 것이 이 테스트의 목적이다.</para>
        /// </summary>
        [Test]
        public void 로드경로의_모든_저장값이_다듬어지거나_명부에_있다()
        {
            (string body, string parameterName, string _) = LoadPath();
            Assert.IsNotNull(body, $"{LogPrefix} 로드 경로를 찾지 못했습니다.");

            var exempt = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string member, string why) in LoadClampExemptions) exempt[member] = why;

            Dictionary<string, string> reads = ReadStatementsByMember(body, parameterName);
            Assert.IsNotEmpty(reads, $"{LogPrefix} 저장값 읽기를 하나도 못 찾았습니다 — 파서 고장.");

            var unexplained = new List<string>();
            foreach (KeyValuePair<string, string> entry in reads)
            {
                if (IsSanitized(entry.Value)) continue;
                if (exempt.ContainsKey(entry.Key)) continue;
                unexplained.Add($"{entry.Key}\n      그 문장: {entry.Value.Trim()}");
            }

            Assert.IsEmpty(unexplained,
                $"{LogPrefix} 로드 경로에 <b>다듬지 않는 저장값</b>이 새로 생겼습니다:\n  " +
                string.Join("\n  ", unexplained) + "\n\n" +
                $"클램프/정규화를 붙이거나, 붙일 필요가 없는 이유를 {nameof(LoadClampExemptions)}에 " +
                "적으세요(bool처럼 범위 밖 값이 없다 / 왕복만 한다 등). 근거 없이 명부에만 넣으면 " +
                "다음 사람이 그 판단을 다시 할 수 없습니다.");
        }

        [Test]
        public void 로드경로_면제항목이_아직_로드경로에_있다()
        {
            (string body, string parameterName, string _) = LoadPath();
            Assert.IsNotNull(body, $"{LogPrefix} 로드 경로를 찾지 못했습니다.");

            Dictionary<string, string> reads = ReadStatementsByMember(body, parameterName);

            var stale = new List<string>();
            foreach ((string member, string why) in LoadClampExemptions)
            {
                Assert.IsNotEmpty(why, $"{LogPrefix} {member}의 면제 사유가 비었습니다.");
                if (!reads.ContainsKey(member)) { stale.Add($"{member} — {why}"); continue; }
                if (IsSanitized(reads[member]))
                    stale.Add($"{member} — 이제 클램프를 지납니다. 면제가 필요 없어졌습니다.\n      {why}");
            }

            Assert.IsEmpty(stale,
                $"{LogPrefix} 면제 명부가 낡았습니다 — 아래 항목은 지우세요:\n  " +
                string.Join("\n  ", stale) + "\n" +
                "남겨 두면 같은 자리가 다시 뚫려도 이 감사가 침묵합니다(명부가 조용히 늙는 형태).");
        }

        // ====================================================================
        // 3. 호출부 책임 — IdleTick 문서가 «소스 스캔으로 잠근다»고 약속한 것
        // ====================================================================

        [Test]
        public void 수급_진입점_호출부가_벽시계나_프레임_델타를_넘기지_않는다()
        {
            Assert.IsNotEmpty(MonotonicEntryPoints, $"{LogPrefix} 진입점 표가 비었습니다.");
            Assert.IsNotEmpty(ForbiddenArgumentTokens, $"{LogPrefix} 금지 토큰표가 비었습니다.");

            int callSites = 0;
            var violations = new List<string>();
            var report = new StringBuilder(LogPrefix).Append(" 호출부 스캔\n");

            foreach ((string path, string stripped) in AllProduction())
            {
                foreach ((string entry, string why) in MonotonicEntryPoints)
                {
                    foreach (string argument in CallArguments(stripped, entry))
                    {
                        callSites++;
                        report.Append("  ").Append(Rel(path)).Append("\t").Append(entry)
                              .Append('(').Append(Compact(argument)).Append(")\n");

                        foreach ((string token, string tokenWhy) in ForbiddenArgumentTokens)
                        {
                            if (!EntitlementAuditSource.ContainsIdentifier(argument, token)) continue;
                            violations.Add($"{Rel(path)} :: {entry}({Compact(argument)}) — '{token}'\n" +
                                           $"      진입점: {why}\n      토큰: {tokenWhy}");
                        }
                    }
                }
            }
            Debug.Log(report.Append("  호출부 ").Append(callSites).Append("건\n").ToString());

            Assert.GreaterOrEqual(callSites, MinProductionCallSites,
                $"{LogPrefix} 프로덕션 호출부를 {callSites}건밖에 찾지 못했습니다(바닥값 " +
                $"{MinProductionCallSites}, 2026-09-06 실측 5건). 배선이 정말 사라진 것이 아니라면 " +
                "파서가 <b>선언을 호출로</b> 또는 <b>호출을 선언으로</b> 오인한 것입니다 — " +
                "이 상태의 '위반 0건'은 측정이 아닙니다.");

            Assert.IsEmpty(violations,
                $"{LogPrefix} 단조 시계를 받기로 한 진입점에 <b>벽시계·프레임 델타</b>가 들어갔습니다:\n  " +
                string.Join("\n  ", violations) + "\n\n" +
                "이 함수들은 그것을 알 수 없으므로 <b>호출부의 책임</b>입니다(CurrencyRules.IdleTick 문서). " +
                "벽시계를 넣으면 시계를 앞으로 돌리는 것만으로 동전이 나오고(T-3-a), 프레임 델타를 " +
                "넣으면 기계가 잠든 시간이 통째로 사라집니다. " +
                "Time.realtimeSinceStartupAsDouble의 <b>차</b>를 넘기세요.");
        }

        /// <summary>
        /// ★ SECURITY_MODEL T-11의 장치 그대로 — <b>배선 라운드가 이 테스트를 켜는 것을 잊을 수 없게</b>
        /// 하는 자리였다. 유휴 수급 진입점에 프로덕션 호출부가 생기는 순간 빨개지고, 그때 사람이
        /// 이 문단을 읽는다.
        ///
        /// <para>★★ <b>2026-09-06 — 실제로 그 일이 일어났고, 이 테스트가 시킨 대로 바꿔 썼다.</b>
        /// 옛 이름은 <c>유휴_수급_배선은_아직_0줄이다</c>였고 본문은 «호출부 0건 + <c>Assert.Ignore</c>»였다.
        /// 그 실패 메시지가 <i>"이 테스트를 «배선됨»을 검증하는 형태로 바꿔 쓰세요(지우지 마세요)"</i>라고
        /// 적어 두었으므로, <b>지우지 않고</b> 그 형태로 옮겼다. 장치가 설계대로 작동한 사례라 기록으로 남긴다.</para>
        ///
        /// <para>이제 잠그는 것은 그 문단이 «그때 할 일»로 적어 둔 둘이다:
        /// ① 인자가 <b>단조 시계 두 시점의 차</b>인가(누적 프레임 델타가 아닌가)
        /// ② <c>isIdleEarning</c>이 <b>집중 세션 중 false</b>인가.
        /// ②는 인자 텍스트만 봐서는 알 수 없으므로(지역 변수로 넘어온다) <b>호출부가 사는 파일이
        /// 집중 세션 판정을 실제로 읽는가</b>로 잰다. 더 깊은 구조 단언(기산점 전진 순서)은
        /// <c>Tests/EditMode/CurrencyIdleTodoTierWiringTests</c>가, 실제 거동은
        /// <c>Tests/PlayMode/CurrencyWiringRuntimeTests</c>가 맡는다 — 셋이 서로 다른 자다.</para>
        /// </summary>
        [Test]
        public void 유휴_수급_배선이_단조_델타와_집중세션_분기를_지킨다()
        {
            int callSites = 0;
            var found = new List<string>();
            var offenders = new List<string>();
            var callerFiles = new List<(string Path, string Stripped)>();

            foreach ((string path, string stripped) in AllProduction())
            {
                foreach (string argument in CallArguments(stripped, "TickIdleIncome"))
                {
                    callSites++;
                    found.Add($"{Rel(path)} :: TickIdleIncome({Compact(argument)})");
                    callerFiles.Add((path, stripped));

                    foreach ((string token, string why) in ForbiddenArgumentTokens)
                    {
                        if (!EntitlementAuditSource.ContainsIdentifier(argument, token)) continue;
                        offenders.Add($"{Rel(path)} :: TickIdleIncome({Compact(argument)}) — '{token}'\n      {why}");
                    }
                }
            }

            Assert.AreEqual(1, callSites,
                $"{LogPrefix} 유휴 수급 프로덕션 호출부가 {callSites}건입니다(1이어야 합니다):\n  " +
                string.Join("\n  ", found) + "\n\n" +
                "0이면 배선이 사라진 것이고(하루 종일 켜 둬도 동전이 안 는다), 2 이상이면 " +
                "<b>같은 1초가 두 번</b> 지급됩니다 — 호출부마다 자기 기산점을 들고 있기 때문입니다.");

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 유휴 수급 인자에 <b>벽시계·프레임 델타</b>가 들어갔습니다:\n  " +
                string.Join("\n  ", offenders) + "\n\n" +
                "Time.realtimeSinceStartupAsDouble의 <b>차</b>를 넘기세요.");

            // ② isIdleEarning이 «집중 세션»에서 파생되는가 — 인자가 지역 변수라 파일 수준으로 잰다.
            //    ★ 부재가 아니라 <b>존재</b> 단언이다: 이 니들이 썩으면 조용히 초록이 아니라 빨강이 된다.
            const string FocusSessionFlag = "IsSessionActive";
            foreach ((string path, string stripped) in callerFiles)
            {
                Assert.IsTrue(EntitlementAuditSource.ContainsIdentifier(stripped, FocusSessionFlag),
                    $"{LogPrefix} {Rel(path)}가 유휴 수급을 부르면서 「{FocusSessionFlag}」를 읽지 않습니다. " +
                    "그러면 isIdleEarning이 <b>집중 세션과 무관한 그림자 상태</b>에서 왔다는 뜻이고, " +
                    "그 둘이 갈라지는 날 같은 1초가 집중과 유휴 양쪽에서 지급됩니다(I-7′). " +
                    "CurrencyModel.TickIdleIncome 문서가 «그림자 상태를 따로 만들지 마라»라고 못박은 자리입니다.");
            }

            TestContext.WriteLine($"{LogPrefix} 유휴 호출부 {callSites}건 — " + string.Join(" / ", found));
        }

        private static string Compact(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ====================================================================
        // 4. 양성 대조 — 「0건」이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        [Test]
        public void 양성대조_스캐너가_실제_스키마와_로드경로와_호출부를_찾아낸다()
        {
            List<(string Path, string Stripped)> all = AllProduction();

            // (1) 스키마 — 알려진 앵커. 전부 <b>존재 단언</b>이라 이름이 바뀌면 시끄럽게 빨개진다.
            var allFields = new List<string>();
            foreach ((string _, string stripped) in all) allFields.AddRange(ScanFields(stripped).FieldNames);

            foreach (string anchor in new[]
                     { "todayGrantedCoins", "potionsUsedToday", "idleWindowUsedSeconds" })
            {
                Assert.Contains(anchor, allFields,
                    $"{LogPrefix} 알려진 재화 세이브 필드 '{anchor}'를 찾지 못했습니다. " +
                    "필드가 실제로 사라졌다면 이 앵커를 갱신하고, 아니라면 파서가 깨진 것입니다 — " +
                    "어느 쪽이든 위 '위반 0건'을 지금은 믿을 수 없습니다.");
            }

            // (2) 로드 경로
            (string body, string parameterName, string path) = LoadPath();
            Assert.IsNotNull(body, $"{LogPrefix} 로드 경로를 찾지 못했습니다.");
            Assert.IsNotNull(parameterName,
                $"{LogPrefix} 로드 경로의 파라미터 이름을 뽑지 못했습니다 — " +
                "'{파라미터}.' 읽기 스캔이 통째로 0건이 됩니다(조용한 초록).");
            Assert.Greater(CountSanitized(ReadStatementsByMember(body, parameterName)), 0,
                $"{LogPrefix} {Rel(path)}의 로드 경로에서 클램프를 하나도 못 찾았습니다.");

            // (3) 호출부 — 실제 배선을 하나는 봐야 한다.
            bool sawMonotonicCall = false;
            foreach ((string _, string stripped) in all)
            {
                foreach ((string entry, string _2) in MonotonicEntryPoints)
                {
                    foreach (string argument in CallArguments(stripped, entry))
                    {
                        if (EntitlementAuditSource.ContainsIdentifier(argument, "realtimeSinceStartupAsDouble"))
                            sawMonotonicCall = true;
                    }
                }
            }
            Assert.IsTrue(sawMonotonicCall,
                $"{LogPrefix} 단조 시계를 <b>실제로 넘기는</b> 호출부를 하나도 못 찾았습니다. " +
                "그러면 위의 '금지 토큰 0건'은 «올바른 시계를 쓴다»가 아니라 «아무 시계도 안 쓴다»입니다.");
        }

        // ====================================================================
        // 5. 네거티브 컨트롤 — 가짜 소스를 <b>같은 함수</b>에 흘린다 (T-15-7 A 3행: 필수)
        // ====================================================================

        [Test]
        public void NegativeControl_상한_필드가_생기면_반드시_잡는다()
        {
            foreach (string field in new[]
                     {
                         "public int dailyCapCoins;",          // ← T-15-7 (A) 3행이 지정한 그 문장
                         "public int dailyCap;",
                         "public int todayLimitCoins;",
                         "public int todayCap;",
                         "public double windowCapSeconds;",
                         "public int dailyLimit;",
                     })
            {
                string fake = "[Serializable]\n" +
                              "private sealed class FakeSave\n" +
                              "{\n" +
                              "    public int version;\n" +
                              "    " + field + "\n" +
                              "}\n";
                FieldScan scan = ScanFields(EntitlementAuditSource.StripComments(fake));
                Assert.IsNotEmpty(scan.Violations,
                    $"{LogPrefix} 스캐너가 상한 필드를 놓쳤습니다 → {field}\n" +
                    "이 상태에서는 프로덕션의 '위반 0건'이 아무 뜻도 없습니다.");
            }
        }

        /// <summary>
        /// ★ <b>반대 방향</b>. 실재하는 정직한 필드들을 같은 스캐너에 흘린다.
        /// 이게 없으면 «넓게 잡아 다 통과시킨다»와 «정확히 잡는다»를 구분할 수 없고,
        /// 반대로 오탐이 나면 이 감사는 첫날에 꺼진다.
        /// </summary>
        [Test]
        public void NegativeControl_정직한_재화_필드는_오탐하지_않는다()
        {
            string fake = "[Serializable]\n" +
                          "private sealed class FakeSave\n" +
                          "{\n" +
                          "    public int todayGrantedCoins;\n" +
                          "    public int potionsUsedToday;\n" +
                          "    public float idleWindowUsedSeconds;\n" +
                          "    public int archeryCoinsToday;\n" +
                          "    public bool todoCoinPaidToday;\n" +
                          "    public int dayBoundaryOffsetMinutes;\n" +
                          "    public int capacityHint;\n" +
                          "    public int windowIndex;\n" +
                          "}\n";
            FieldScan scan = ScanFields(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} 정직한 필드를 상한 저장으로 오탐했습니다 — 낱말 <b>쌍</b> 판정이 " +
                "깨졌습니다(today/window 하나만 보면 todayGrantedCoins·idleWindowUsedSeconds가 " +
                "첫날에 빨개집니다).\n잡힌 것: " + string.Join(" / ", scan.Violations));
        }

        [Test]
        public void NegativeControl_주석_속_필드는_위반이_아니다()
        {
            // 실제 상황이다: CharacterSaveStore가 «dailyCap / todayLimit 계열 — 상한은 필드가 아니라
            // 함수다»라고 <b>금지 목록을 주석에 적어</b> 두고 있다. 주석을 세면 그 정직한 문서가 빨개진다.
            string fake = "[Serializable]\n" +
                          "private sealed class FakeSave\n" +
                          "{\n" +
                          "    //   · dailyCap / todayLimit 계열 — 상한은 필드가 아니라 함수다(T-D-9)\n" +
                          "    /* public int windowCapSeconds; */\n" +
                          "    public int version;\n" +
                          "}\n";
            FieldScan scan = ScanFields(EntitlementAuditSource.StripComments(fake));
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} 주석 속 금지 목록을 위반으로 셌습니다. 그러면 «무엇을 만들지 " +
                "않기로 했는지»를 적어 둔 파일이 벌을 받습니다 — 그런 감사는 문서를 지우게 만듭니다.\n" +
                "잡힌 것: " + string.Join(" / ", scan.Violations));
        }

        [Test]
        public void NegativeControl_클램프_없는_로드_경로를_잡는다()
        {
            const string sanitized =
                "public static class Fake\n" +
                "{\n" +
                "    internal static void RestoreFromSave(FakeState state)\n" +
                "    {\n" +
                "        PotionsUsedToday = Rules.ClampPotionsUsed(state.PotionsUsedToday);\n" +
                "        Offset = state.HasOffset\n" +
                "            ? Rules.ClampOffsetMinutes(state.OffsetMinutes)\n" +
                "            : 0;\n" +
                "    }\n" +
                "}\n";

            string strippedOk = EntitlementAuditSource.StripComments(sanitized);
            string bodyOk = IncomeTimeSourceAuditTests.MethodBodyOrNull(strippedOk, LoadMethodName);
            Assert.IsNotNull(bodyOk, $"{LogPrefix} 가짜 로드 경로를 못 잘랐습니다.");
            string paramOk = FirstParameterNameOrNull(DeclaredParameterListOrNull(strippedOk, LoadMethodName));
            Assert.AreEqual("state", paramOk, $"{LogPrefix} 파라미터 이름을 못 뽑았습니다.");

            Dictionary<string, string> okReads = ReadStatementsByMember(bodyOk, paramOk);
            Assert.IsTrue(IsSanitized(okReads["PotionsUsedToday"]),
                $"{LogPrefix} 한 줄짜리 클램프를 못 봤습니다.");
            Assert.IsTrue(IsSanitized(okReads["OffsetMinutes"]),
                $"{LogPrefix} <b>세 줄에 걸친</b> 클램프를 못 봤습니다 — 줄 단위로 자르면 여기서 " +
                "거짓 빨강이 납니다(실제 로드 경로의 dayBoundaryOffsetMinutes가 정확히 이 모양입니다).");

            const string raw =
                "public static class Fake\n" +
                "{\n" +
                "    internal static void RestoreFromSave(FakeState state)\n" +
                "    {\n" +
                "        PotionsUsedToday = state.PotionsUsedToday;\n" +
                "    }\n" +
                "}\n";

            string strippedBad = EntitlementAuditSource.StripComments(raw);
            string bodyBad = IncomeTimeSourceAuditTests.MethodBodyOrNull(strippedBad, LoadMethodName);
            string paramBad = FirstParameterNameOrNull(DeclaredParameterListOrNull(strippedBad, LoadMethodName));
            Dictionary<string, string> badReads = ReadStatementsByMember(bodyBad, paramBad);

            Assert.IsFalse(IsSanitized(badReads["PotionsUsedToday"]),
                $"{LogPrefix} 클램프가 <b>없는</b> 대입을 «다듬어졌다»로 셌습니다 — " +
                "그러면 이 감사의 '전부 클램프됨'은 언제나 참이고 아무것도 지키지 않습니다.");
        }

        [Test]
        public void NegativeControl_호출부와_선언부를_구분한다()
        {
            const string source =
                "public static class Fake\n" +
                "{\n" +
                "    public static bool TickDayRollover(double nowMonotonic) { return nowMonotonic > 0; }\n" +
                "    public static int TickIfDue(double t) => (int)t;\n" +
                "    public static void Drive()\n" +
                "    {\n" +
                "        TickDayRollover(Time.realtimeSinceStartupAsDouble);\n" +
                "    }\n" +
                "}\n";

            string stripped = EntitlementAuditSource.StripComments(source);

            List<string> rolloverCalls = CallArguments(stripped, "TickDayRollover");
            Assert.AreEqual(1, rolloverCalls.Count,
                $"{LogPrefix} 선언을 호출로 셌거나 호출을 놓쳤습니다 — 잡은 것: " +
                string.Join(" | ", rolloverCalls));
            Assert.IsTrue(EntitlementAuditSource.ContainsIdentifier(
                    rolloverCalls[0], "realtimeSinceStartupAsDouble"),
                $"{LogPrefix} 인자 원문을 잘못 잘랐습니다: '{rolloverCalls[0]}'");

            Assert.IsEmpty(CallArguments(stripped, "TickIfDue"),
                $"{LogPrefix} <b>식 본문(=>) 선언</b>을 호출로 셌습니다. 그러면 모든 진입점 선언이 " +
                "호출부로 세어져 «파라미터 이름»이 인자로 검사됩니다(무의미한 통과).");
        }

        [Test]
        public void NegativeControl_호출부에_벽시계를_넣으면_반드시_잡는다()
        {
            foreach (string argument in new[]
                     {
                         "DateTime.UtcNow.Ticks",
                         "System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()",
                         "Environment.TickCount",
                         "Time.deltaTime",
                         "Time.unscaledDeltaTime",
                         "Time.time",
                     })
            {
                string source =
                    "public static class Fake\n" +
                    "{\n" +
                    "    public static void Drive() { CurrencyModel.TickDayRollover(" + argument + "); }\n" +
                    "}\n";

                List<string> calls = CallArguments(EntitlementAuditSource.StripComments(source), "TickDayRollover");
                Assert.AreEqual(1, calls.Count, $"{LogPrefix} 호출부를 못 찾았습니다 → {argument}");

                bool caught = false;
                foreach ((string token, string _) in ForbiddenArgumentTokens)
                {
                    if (!EntitlementAuditSource.ContainsIdentifier(calls[0], token)) continue;
                    caught = true;
                    break;
                }
                Assert.IsTrue(caught,
                    $"{LogPrefix} 금지 인자를 놓쳤습니다 → {argument}\n" +
                    "이 상태에서는 프로덕션의 '위반 0건'이 아무 뜻도 없습니다.");
            }
        }

        [Test]
        public void NegativeControl_정당한_인자는_오탐하지_않는다()
        {
            foreach (string argument in new[]
                     {
                         "Time.realtimeSinceStartupAsDouble",
                         "nowMonotonic",
                         "deltaSeconds, isIdleEarning, TodayGrantedCoins, PotionsUsedToday",
                         "_lastCheckMonotonic + CurrencyDayRolloverTicker.CheckIntervalSeconds",
                     })
            {
                foreach ((string token, string _) in ForbiddenArgumentTokens)
                {
                    Assert.IsFalse(EntitlementAuditSource.ContainsIdentifier(argument, token),
                        $"{LogPrefix} 정당한 인자를 '{token}'으로 오탐했습니다 → {argument}\n" +
                        "낱말 경계 판정이 깨졌습니다(예: Time.timeScale이 Time.time으로 잡히는 형태). " +
                        "정직한 배선을 빨갛게 만드는 감사는 몇 번 만에 꺼집니다.");
                }
            }
        }

        [Test]
        public void 니들표는_전부_사유를_갖고_있다()
        {
            Assert.IsNotEmpty(MustClampOnLoad, $"{LogPrefix} 클램프 대상 표가 비었습니다.");
            Assert.IsNotEmpty(LoadClampExemptions, $"{LogPrefix} 면제 명부가 비었습니다 — " +
                "정말 0건이 기대값이라면 이 단언을 지우지 말고 그렇게 고쳐 적으세요(거짓 통과 #5).");

            foreach ((string first, string second, string why) in ForbiddenFieldPairs)
                Assert.IsNotEmpty(why, $"{LogPrefix} 쌍 '{first}'+'{second}'의 사유가 비었습니다.");
            foreach ((string member, string why) in MustClampOnLoad)
                Assert.IsNotEmpty(why, $"{LogPrefix} '{member}'의 사유가 비었습니다.");
            foreach ((string entry, string why) in MonotonicEntryPoints)
                Assert.IsNotEmpty(why, $"{LogPrefix} 진입점 '{entry}'의 사유가 비었습니다.");
            foreach ((string token, string why) in ForbiddenArgumentTokens)
                Assert.IsNotEmpty(why, $"{LogPrefix} 토큰 '{token}'의 사유가 비었습니다.");
        }
    }
}
