using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// FC-1 배선 감사 — Windows 조회 계층이 실패를 캐시하지 않는가 (N-23, 2026-09-26 · dev-platform)
    /// ============================================================================
    /// <see cref="GameVerdictRetryPolicyTests"/>가 <b>규칙</b>을 실행으로 잠근다. 이 파일은 그 규칙이
    /// <b>실제로 배선됐는지</b>를 잰다 — <c>Platform/Windows/WindowsGameProcessProbe.cs</c>는 파일 전체가
    /// <c>#if UNITY_STANDALONE_WIN</c> 안이라 이 머신에서 <b>타입이 존재하지 않으므로</b> 리플렉션으로는
    /// 영원히 볼 수 없다. 그래서 소스 텍스트만 읽는다(osx 타깃에서도 win 타깃에서도 같은 것을 잰다).
    ///
    /// ============================================================================
    /// ★ 「있다/없다」가 아니라 <b>순서</b>를 잰다
    /// ============================================================================
    /// 이 결함의 정확한 모양은 «캐시에 쓰는 줄이 있다»가 아니라 <b>«그 줄이 실패 여부를 묻지 않고
    /// 실행된다»</b>였다. 니들 존재만 세면 실패를 다시 캐시하도록 되돌려도 초록이다. 그래서
    /// 판정 본문에서 <b>«캐시해도 되는가»를 묻는 자리가 캐시에 쓰는 자리보다 앞</b>인지를 본다.
    /// 목록 쪽도 같다 — 옛 판의 결함은 조회 시각을 <b>시도 전에</b> 박은 것이었다.
    ///
    /// <para><b>부재 단언에는 짝을 붙인다</b>(CLAUDE.md): 니들이 실재하는지 먼저 확인하고,
    /// 순서 판정기 자체는 <b>일부러 어긋난 가짜 소스</b>로 음성 대조한다 — 판정기가 눈이 멀면
    /// 「순서가 맞다」와 「아무것도 못 봤다」가 똑같이 생긴다.</para>
    /// </summary>
    public class GameVerdictFailureCacheAuditTests
    {
        private const string LogPrefix = "[FC1배선감사]";

        /// <summary>조회 계층의 타입 이름. 경로가 아니라 <b>타입 선언</b>으로 찾으므로 파일이 옮겨져도
        /// 따라간다(못 찾으면 시끄럽게 빨개진다 — 조용한 0건이 되지 않는다).</summary>
        private const string ProbeTypeName = "WindowsGameProcessProbe";

        /// <summary>중립 규칙의 타입 이름들. 둘 다 <c>Platform/</c> 바로 아래에 있어야 한다.</summary>
        private const string RuleTypeName = "GameVerdictRetryPolicy";
        private const string RuleStateTypeName = "GameVerdictRetryState";

        private readonly struct Source
        {
            public readonly string Path;
            public readonly string Stripped;

            public Source(string path, string stripped)
            {
                Path = path;
                Stripped = stripped;
            }
        }

        private static Source FindProduction(string typeName)
        {
            string[] all = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(all.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Length}개밖에 읽지 못했습니다 — 이 상태의 판정은 " +
                "측정이 아닙니다(스캐너가 눈이 멀었습니다).");

            var hits = new List<Source>();
            foreach (string path in all)
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                if (!EntitlementAuditSource.DeclaresType(stripped, typeName)) continue;
                hits.Add(new Source(path, stripped));
            }

            Assert.AreEqual(1, hits.Count,
                $"{LogPrefix} '{typeName}' 타입을 선언하는 프로덕션 파일이 {hits.Count}개입니다(기대 1). " +
                "0개면 이름이 바뀌었거나 파일이 사라진 것이고, 2개 이상이면 쪼개진 것입니다. 어느 쪽이든 " +
                "여기서 멈추는 것이 맞습니다 — 그대로 두면 아래 모든 판정이 «검사할 파일이 없어서 " +
                "위반 0건»이 되어 조용히 초록이 됩니다.");
            return hits[0];
        }

        /// <summary>
        /// 중괄호를 세어 메서드 본문을 잘라 낸다. 못 찾으면 <c>null</c> —
        /// 호출자가 그것을 <b>실패</b>로 다룬다(조용한 빈 문자열 금지).
        /// </summary>
        private static string MethodBodyOrNull(string stripped, string signature)
        {
            int at = stripped.IndexOf(signature, System.StringComparison.Ordinal);
            if (at < 0) return null;

            int open = stripped.IndexOf('{', at);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < stripped.Length; i++)
            {
                if (stripped[i] == '{') depth++;
                else if (stripped[i] == '}')
                {
                    depth--;
                    if (depth == 0) return stripped.Substring(open + 1, i - open - 1);
                }
            }
            return null;
        }

        /// <summary>겹치지 않는 등장 횟수. 「정확히 1회」 단언에 쓴다(정규식을 쓰지 않는다 —
        /// .NET <c>\b</c>가 한글을 낱말 문자로 세는 함정을 피한다).</summary>
        private static int Occurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0, from = 0;
            while (true)
            {
                int at = haystack.IndexOf(needle, from, System.StringComparison.Ordinal);
                if (at < 0) return count;
                count++;
                from = at + needle.Length;
            }
        }

        /// <summary>
        /// <c>private const ... 이름 = 값;</c>의 <b>값 문자열</b>. 못 찾으면 <c>null</c>
        /// (호출자가 실패로 다룬다 — 조용한 빈 문자열 금지).
        /// </summary>
        private static string ConstantLiteral(string stripped, string constName)
        {
            int at = stripped.IndexOf(constName + " = ", System.StringComparison.Ordinal);
            if (at < 0) return null;
            int start = at + constName.Length + 3;
            int end = stripped.IndexOf(';', start);
            return end < 0 ? null : stripped.Substring(start, end - start).Trim();
        }

        /// <summary>
        /// <paramref name="body"/>에서 <paramref name="first"/>가 <paramref name="second"/>보다 앞에
        /// 오는가. 둘 중 하나라도 없으면 <c>false</c>와 사유를 돌려준다(없는 것을 «순서가 맞다»로
        /// 읽지 않는다 — 그게 이 감사가 막으려는 조용한 초록이다).
        /// </summary>
        private static bool ComesBefore(string body, string first, string second, out string why)
        {
            int a = body.IndexOf(first, System.StringComparison.Ordinal);
            int b = body.IndexOf(second, System.StringComparison.Ordinal);
            if (a < 0) { why = $"'{first}'가 본문에 없다"; return false; }
            if (b < 0) { why = $"'{second}'가 본문에 없다"; return false; }
            if (a >= b) { why = $"'{first}'({a})가 '{second}'({b})보다 뒤에 있다"; return false; }
            why = null;
            return true;
        }

        // ====================================================================
        // 1. 판정 캐시 — 「캐시해도 되는가」를 먼저 묻는다
        // ====================================================================

        [Test]
        public void 판정_캐시는_확정_여부를_먼저_묻고_쓴다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "public bool IsGameProcess(");
            Assert.IsNotNull(body,
                $"{LogPrefix} IsGameProcess 본문을 찾지 못했습니다 — 서명이 바뀌었다면 이 감사도 " +
                "함께 갱신하세요. 그대로 두면 아래 판정이 공허해집니다.");

            // ★ 순서만 보면 인자를 상수로 바꿔치기한 판(ShouldCacheVerdict(true, GameListReadOutcome.Success))이
            //   그대로 통과한다 — 그러면 실패 캐시가 부활하고 감사는 초록이다. 그래서 **수신자·인자 이름까지**
            //   포함한 전체 호출을 니들로 쓴다(test-engineer P1, 2026-09-26).
            Assert.IsTrue(ComesBefore(body, RuleTypeName + ".ShouldCacheVerdict(pathResolved, listOutcome)",
                    "_cachedVerdictAt = now;", out string why),
                $"{LogPrefix} 판정을 캐시에 쓰기 전에 «확정 판정인가»를 조회 결과로 묻지 않습니다({why}).\n" +
                "조회 실패에서 나온 false가 30초 굳으면 이미 감지된 게임 위에서 캐릭터가 약 30초 " +
                "동안 다시 나옵니다(원칙 2 조건부 위반 · N-23). 인자를 상수나 다른 변수로 바꾸는 것도 " +
                "여기서 막습니다 — 그 형태가 정확히 이 결함의 부활 경로입니다. 설계 정본: " +
                "docs/platform/GAME_DETECTION_FAILURE_CACHE.md 「4-1. ★ FC-1 확장 설계」.");

            // 인자의 **출처**: 두 지역 변수가 이번 폴링의 조회 결과에서 나와야 한다.
            StringAssert.Contains("string exePath = TryGetProcessImagePath(pid, now);", body,
                $"{LogPrefix} 전경 pid의 경로를 이번 폴링에 조회하지 않습니다 — 캐시 판정의 입력 출처가 " +
                "끊겼습니다.");
            StringAssert.Contains("bool pathResolved = !string.IsNullOrEmpty(exePath);", body,
                $"{LogPrefix} `pathResolved`가 경로 조회 결과에서 대입되지 않습니다 — 그 변수가 상수나 " +
                "다른 값이 되면 위 전체 호출 니들은 맞는데도 실패 캐시가 부활합니다.");
            StringAssert.Contains("listOutcome = RefreshRegisteredGamesIfStale(", body,
                $"{LogPrefix} `listOutcome`이 목록 갱신 결과에서 대입되지 않습니다 — 같은 이유로 " +
                "캐시 판정이 조회와 무관해집니다.");
        }

        /// <summary>
        /// ★ 결합 후퇴 문(설계 4-1의 명시 규칙) — <b>둘 중 하나라도</b> 후퇴 창 안이면 이 폴링은
        /// 아무것도 조회하지 않고 <c>false</c>를 돌려준다. 규칙 층에 함수가 없는 자리라 소스로 잰다
        /// (test-engineer P3, 2026-09-26).
        ///
        /// <para>무엇을 막는가: <c>||</c>를 <c>&amp;&amp;</c>로 바꾸면 한쪽이 후퇴 중인데도 다른 쪽을
        /// 계속 조회해 <b>비용 상한이 깨지고</b>(창당 3회 약속), 이 블록의 반환값이 <c>false</c>가
        /// 아니게 되면 <b>조회도 안 한 채 「게임」이라고 답</b>해 원칙 2가 반대 방향으로 깨진다.
        /// 존재 단언만으로는 둘 다 무신호였다.</para>
        /// </summary>
        [Test]
        public void 결합_후퇴_문은_둘_중_하나라도_닫히면_조회하지_않고_거짓을_돌려준다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "public bool IsGameProcess(");
            Assert.IsNotNull(body, $"{LogPrefix} IsGameProcess 본문을 찾지 못했습니다.");

            StringAssert.Contains(
                "if (!_pathRetry.TryBeginAttempt(now) || !_listRetry.TryBeginAttempt(now))", body,
                $"{LogPrefix} 결합 후퇴 문이 «경로 || 목록» 형태가 아닙니다.\n" +
                "`&&`로 바뀌면 한쪽이 후퇴 중인데도 다른 쪽 조회가 계속 돌아 창당 3회 상한이 깨집니다. " +
                "서식만 바꿨다면 이 니들도 같이 갱신하세요(그때는 이 실패가 옳은 신호입니다).");

            Assert.AreEqual(1, Occurrences(body, "return false;"),
                $"{LogPrefix} 본문의 `return false;`가 1개가 아닙니다 — 후퇴 문 블록이 조회 없이 " +
                "거짓을 돌려주는 유일한 자리여야 합니다. 이 블록이 다른 값을 돌려주면 조회도 하지 않고 " +
                "«게임»이라고 답하게 되어 감지되지 않은 앱에서 캐릭터가 사라집니다(원칙 2 반대 방향).");

            Assert.IsTrue(ComesBefore(body, "_listRetry.TryBeginAttempt(now)", "return false;", out string w1),
                $"{LogPrefix} 후퇴 문과 그 거짓 반환의 순서가 어긋났습니다({w1}).");
            Assert.IsTrue(ComesBefore(body, "return false;", "string exePath = TryGetProcessImagePath(", out string w2),
                $"{LogPrefix} 후퇴 문의 거짓 반환이 조회보다 뒤에 있습니다({w2}) — 후퇴 중에도 조회가 " +
                "먼저 돌아 비용 상한이 무의미해집니다.");
        }

        /// <summary>
        /// ★ 실패 뒤 <b>옛 확정 판정을 버리는 것</b>과 <b>캐시 히트가 후퇴 문보다 먼저 평가되는 것</b>을
        /// 한 쌍으로 잠근다(test-engineer GAP-2+3). 앞의 것이 없으면 뒤의 것은 관측 자체가 불가능하다.
        ///
        /// <para>순서가 뒤집히면(후퇴 문이 캐시 조회보다 앞) 후퇴 창 안에서 <b>유효한 캐시 판정까지</b>
        /// 버려져, 이미 «게임»으로 확정된 앱 위에 캐릭터가 최대 30초 다시 나온다 — N-23이 없애려던
        /// 바로 그 증상이 다른 경로로 되돌아온다.</para>
        /// </summary>
        [Test]
        public void 실패_뒤_옛_확정_판정을_버리고_캐시_히트는_후퇴_문보다_먼저_평가된다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "public bool IsGameProcess(");
            Assert.IsNotNull(body, $"{LogPrefix} IsGameProcess 본문을 찾지 못했습니다.");

            // (1) 실패로 판정한 폴링은 옛 확정 판정을 무효로 만든다.
            StringAssert.Contains("_cachedPidValid = false;", body,
                $"{LogPrefix} 실패 뒤 옛 확정 판정을 버리지 않습니다 — «언제 잰 값인지» 모르는 판정이 " +
                "최대 30초 더 살아남습니다.");
            Assert.IsTrue(ComesBefore(body, RuleTypeName + ".ShouldCacheVerdict(", "_cachedPidValid = false;",
                    out string why1),
                $"{LogPrefix} 캐시 무효화가 «캐시해도 되는가» 판정과 무관한 자리에 있습니다({why1}).");

            // (2) 캐시 히트는 후퇴 문보다 **먼저** 평가된다.
            Assert.IsTrue(ComesBefore(body, "now - _cachedVerdictAt < VerdictCacheSeconds",
                    "_pathRetry.TryBeginAttempt(", out string why2),
                $"{LogPrefix} 후퇴 문이 캐시 조회보다 먼저 평가됩니다({why2}) — 후퇴 창 안에서 유효한 " +
                "캐시 판정까지 버려져, 감지된 게임 위에 캐릭터가 최대 30초 다시 나옵니다.");
        }

        /// <summary>
        /// ★ 목록 후퇴는 <b>전역</b>이다 — 그 사실이 지금까지 「부재」로만 존재했다(test-engineer GAP-6).
        /// <c>_listRetry</c>에 <c>ResetIfScopeChanged</c>를 부르는 줄이 들어와도 아무것도 빨개지지
        /// 않았다. 호출이 <b>정확히 1회</b>이고 수신자가 경로 쪽임을 못박는다.
        /// </summary>
        [Test]
        public void 후퇴_범위_초기화는_경로_쪽에만_정확히_한_번_있다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "public bool IsGameProcess(");
            Assert.IsNotNull(body, $"{LogPrefix} IsGameProcess 본문을 찾지 못했습니다.");

            Assert.AreEqual(1, Occurrences(body, "ResetIfScopeChanged("),
                $"{LogPrefix} 범위 초기화 호출이 1개가 아닙니다 — 목록 쪽에도 붙으면 목록 후퇴가 " +
                "pid마다 풀려 전역이라는 설계가 깨집니다(게임바 목록은 계정 전역 값입니다).");

            // 부재 단언의 짝 — 같은 수신자 계열이 이 본문에 실재하는지 먼저 보인다.
            StringAssert.Contains("_listRetry.TryBeginAttempt(", body,
                $"{LogPrefix} `_listRetry`를 이 본문에서 찾지 못했습니다 — 아래 «목록 쪽 초기화 없음»이 " +
                "«필드가 사라져서 없음»과 구별되지 않습니다.");
            Assert.IsFalse(body.Contains("_listRetry.ResetIfScopeChanged"),
                $"{LogPrefix} 목록 후퇴를 pid로 초기화합니다 — 목록 실패 후퇴는 전역이어야 합니다.");
        }

        /// <summary>
        /// ★ 열거 결과 → 성격 매핑 4지점(test-engineer GAP-7). 하나만 뒤집혀도 재시도 대상 실패가
        /// 다시 30초 굳거나, 결정적 실패를 폴링마다 다시 묻는다. 규칙 층에도 감사에도 없던 자리다.
        /// </summary>
        [Test]
        public void 열거_결과가_성격으로_매핑되는_네_지점이_그대로다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string enumerate = MethodBodyOrNull(probe.Stripped,
                "private GameListReadOutcome EnumerateRegisteredGames(");
            string refresh = MethodBodyOrNull(probe.Stripped,
                "private GameListReadOutcome RefreshRegisteredGamesIfStale(");
            Assert.IsNotNull(enumerate, $"{LogPrefix} 열거 본문을 찾지 못했습니다.");
            Assert.IsNotNull(refresh, $"{LogPrefix} 목록 갱신 본문을 찾지 못했습니다.");

            StringAssert.Contains("if (enumRc == ERROR_NO_MORE_ITEMS) return GameListReadOutcome.Success;",
                enumerate,
                $"{LogPrefix} 「끝까지 읽었다 → 성공」 매핑이 바뀌었습니다 — 정상 종료를 재시도 대상으로 " +
                "보면 폴링마다 레지스트리를 다시 훑습니다.");
            StringAssert.Contains("return GameListReadOutcome.Retryable;", enumerate,
                $"{LogPrefix} 열거 오류를 재시도 대상으로 돌려주지 않습니다 — 일시 오류가 30초 굳습니다.");
            StringAssert.Contains("return GameListReadOutcome.SubkeyLimitReached;", enumerate,
                $"{LogPrefix} 하위 키 상한 매핑이 사라졌습니다 — 결정적 실패를 폴링마다 다시 묻게 됩니다.");
            StringAssert.Contains("if (index > 4096)", enumerate,
                $"{LogPrefix} 하위 키 상한 판정이 사라졌습니다(설계는 실상한 4,098개로 서술합니다).");

            StringAssert.Contains("outcome = GameListReadOutcome.Retryable;", refresh,
                $"{LogPrefix} 예외를 재시도 대상으로 분류하지 않습니다 — 예외가 30초 굳습니다.");
            StringAssert.Contains("GameListReadOutcome outcome = GameListReadOutcome.Retryable;", refresh,
                $"{LogPrefix} 초기값이 재시도 대상이 아닙니다 — 분류가 빠진 경로가 «유지»로 떨어지면 " +
                "그 실패가 30초 굳습니다(안전한 기본값은 «다시 묻는다»입니다).");
        }

        /// <summary>
        /// ★ 두 캐시 상수의 <b>우연한 일치</b>를 트립와이어로 잠근다(test-engineer GAP-5에 대한
        /// dev-platform 판정, 2026-09-26).
        ///
        /// <para><b>통합하지 않는다</b>가 판정이다. 저장소에 30.0초가 세 개 있지만 이유가 서로 다르다 —
        /// <c>VerdictCacheSeconds</c>는 <b>pid 재사용</b> 위험, <c>RegistryCacheSeconds</c>는
        /// <b>게임 바가 항목을 만드는 지연</b>, <c>BackoffWindowSeconds</c>는 <b>지속 실패의 재시도
        /// 상한</b>이다. 하나로 묶으면 「목록 갱신을 더 자주 하고 싶다」가 「후퇴 창도 짧아진다」로
        /// 번져, 서로 독립이어야 하는 것이 결합된다.</para>
        ///
        /// <para>그래도 두 <b>캐시</b> 상수는 설계 문서가 「각각 30초 유지」라고 한 쌍이므로, 한쪽만
        /// 조용히 바뀌는 것은 막는다. 값을 정말 바꿔야 하면 이 테스트가 무엇을 같이 고쳐야 하는지
        /// 알려 준다 — 그게 이 단언의 목적이다.</para>
        /// </summary>
        [Test]
        public void 판정_캐시와_목록_캐시_상수는_서로_같은_값이다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string verdict = ConstantLiteral(probe.Stripped, "VerdictCacheSeconds");
            string registry = ConstantLiteral(probe.Stripped, "RegistryCacheSeconds");

            Assert.IsNotNull(verdict, $"{LogPrefix} `VerdictCacheSeconds` 선언을 찾지 못했습니다.");
            Assert.IsNotNull(registry, $"{LogPrefix} `RegistryCacheSeconds` 선언을 찾지 못했습니다.");
            Assert.AreEqual(verdict, registry,
                $"{LogPrefix} 판정 캐시({verdict})와 목록 캐시({registry}) 유효기간이 갈라졌습니다.\n" +
                "설계 문서는 «판정 캐시와 게임바 목록 캐시가 각각 30초»라고 말하고 비용 표의 «정상 120회»가 " +
                "그 값에서 나옵니다. 값을 바꾸는 것이 옳다면 docs/platform/GAME_DETECTION_FAILURE_CACHE.md의 " +
                "0절 서술과 4-1 비용 표를 같이 고치세요. ★ 이 둘을 정책의 후퇴 창(BackoffWindowSeconds)과 " +
                "묶지는 않습니다 — 세 값이 30.0인 것은 우연이고 이유가 서로 다릅니다(이 테스트 문서 참고).");
        }

        // ====================================================================
        // 2. 목록 캐시 — 시각을 「시도 뒤」에 박는다
        // ====================================================================

        [Test]
        public void 목록_조회_시각은_시도_뒤에_박힌다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "private GameListReadOutcome RefreshRegisteredGamesIfStale(");
            Assert.IsNotNull(body,
                $"{LogPrefix} 목록 갱신 본문을 찾지 못했습니다 — 반환형이나 이름이 바뀌었다면 이 감사도 " +
                "함께 갱신하세요.");

            Assert.IsTrue(ComesBefore(body, "RegOpenKeyExW(", "_registryCachedAt = now;", out string why),
                $"{LogPrefix} 목록 조회 시각을 시도 **전에** 박습니다({why}).\n" +
                "옛 판의 결함이 정확히 이것이었습니다 — 시도 전에 박으면 조회가 얼마나 걸렸든 30초 창이 " +
                "시도 시점부터 시작됩니다.");

            // ★ 진짜 보호 장치는 시각의 위치가 아니라 **신선도 문의 결과 항**이다.
            //   `_registryCachedAt = now;`는 실패에서도 무조건 돌기 때문에, 결과 항이 사라지면
            //   시각이 뒤에 박혀 있어도 재시도 대상 실패가 30초 묶인다(= 원래 버그).
            Assert.IsTrue(ComesBefore(body, RuleTypeName + ".HoldsWithoutRetry(", "RegOpenKeyExW(",
                    out string gateWhy),
                $"{LogPrefix} 목록 신선도 문이 **결과 항**을 보지 않습니다({gateWhy}).\n" +
                "이 항이 없으면 재시도 대상 실패도 30초 동안 다시 묻지 않게 되어 FC-1이 조용히 원래 " +
                "버그로 돌아갑니다. 시각을 시도 뒤로 옮긴 것은 이 보호와 **별개**입니다 — " +
                "«시각을 뒤로 옮겼으니 괜찮다»로 읽고 이 항을 지우지 마세요.");
        }

        // ====================================================================
        // 2-b. 예외 경고 접기 — 설계 4-1 항목 4
        // ====================================================================

        /// <summary>
        /// 설계 정본은 예외 경고를 <b>연속 실패 구간당 1줄</b>로 묶겠다고 약속한다. 약속만 있고 장치가
        /// 없으면 그 문서가 거짓이 되므로(verify-change 2026-09-26 반증), 두 <c>catch</c>가 모두
        /// 접기 문 뒤에서만 찍는지 소스로 잰다. 묶음 장치는 저장소에 이미 있는
        /// <c>RepeatedLogFolder</c>를 재사용한다 — 규칙을 새로 만들지 않는다.
        /// </summary>
        [Test]
        public void 예외_경고는_접기_문_뒤에서만_찍힌다()
        {
            Source probe = FindProduction(ProbeTypeName);

            foreach (string signature in new[]
                     {
                         "private string TryGetProcessImagePath(",
                         "private GameListReadOutcome RefreshRegisteredGamesIfStale(",
                     })
            {
                string body = MethodBodyOrNull(probe.Stripped, signature);
                Assert.IsNotNull(body, $"{LogPrefix} '{signature}' 본문을 찾지 못했습니다.");
                Assert.IsTrue(ComesBefore(body, "ShouldEmit(", "Debug.LogWarning(", out string why),
                    $"{LogPrefix} '{signature}'의 예외 경고가 접기 문 없이 찍힙니다({why}).\n" +
                    "지속 예외 계정에서 경고가 폴링마다 나가 1시간 수백 줄이 됩니다(설계 정본 4-1 " +
                    "항목 4가 «연속 실패 구간당 1줄»을 약속합니다).");
            }

            // 접은 횟수를 방출하는 줄이 있어야 한다 — 없으면 접힌 정보가 통째로 사라진다.
            StringAssert.Contains("RepeatedLogFolder.Describe(", probe.Stripped,
                $"{LogPrefix} 접힘 요약 줄이 없습니다 — 접기가 «횟수를 잃는» 감량이 되면 그건 감량이 " +
                "아니라 실명입니다.");
        }

        // ====================================================================
        // 3. 후퇴 범위 — 경로는 pid별
        // ====================================================================

        [Test]
        public void 경로_후퇴는_pid별로_초기화된다()
        {
            Source probe = FindProduction(ProbeTypeName);
            string body = MethodBodyOrNull(probe.Stripped, "public bool IsGameProcess(");
            Assert.IsNotNull(body, $"{LogPrefix} IsGameProcess 본문을 찾지 못했습니다.");

            // ★ 수신자까지 니들에 넣는다 — `_listRetry.ResetIfScopeChanged(pid)`로 바꿔도 옛 니들
            //   (`ResetIfScopeChanged(pid)`)은 맞았고, 그러면 경로 후퇴가 전역이 되어 설계가 명시적으로
            //   기각한 「게임 위 최대 약 30초 새 노출」이 생긴다(test-engineer P1-b, 2026-09-26).
            StringAssert.Contains("_pathRetry.ResetIfScopeChanged(pid)", body,
                $"{LogPrefix} 전경 pid로 **경로** 후퇴 범위를 초기화하지 않습니다 — 경로 실패 후퇴가 " +
                "전역이 되면, 매번 실패하는 비게임 전체화면 앱이 후퇴 중일 때 게임으로 전환하면 창이 " +
                "끝날 때까지 게임을 묻지 않아 게임 위에 최대 약 30초 노출이 **새로** 생깁니다" +
                "(옛 판에는 없던 노출).");

            StringAssert.Contains("_pathRetry.TryBeginAttempt(", body,
                $"{LogPrefix} 경로 후퇴 문을 묻지 않고 조회합니다 — 지속 실패에서 폴링마다 조회합니다.");
        }

        // ====================================================================
        // 4. 규칙의 자리 — 중립 위치
        // ====================================================================

        [Test]
        public void 재시도_규칙은_플랫폼_중립_파일에_있다()
        {
            Source rule = FindProduction(RuleTypeName);
            string neutralRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");
            string norm = rule.Path.Replace('\\', '/');

            StringAssert.StartsWith(neutralRoot.Replace('\\', '/'), norm,
                $"{LogPrefix} 재시도 규칙이 Platform/ 밖에 있습니다: {norm}");
            Assert.IsFalse(norm.Contains("/Platform/Windows/") || norm.Contains("/Platform/MacOS/"),
                $"{LogPrefix} 재시도 규칙이 플랫폼 전용 폴더 안에 있습니다({norm}) — 그 자리에 두면 " +
                "반대편 플랫폼이 물리적으로 부를 수 없고(FullscreenSuspendPolicy 사고), Windows 전용 " +
                "파일은 이 머신에서 컴파일되지 않아 EditMode로 잴 수도 없습니다.");

            Assert.IsTrue(EntitlementAuditSource.DeclaresType(rule.Stripped, RuleStateTypeName),
                $"{LogPrefix} 후퇴 상태 타입({RuleStateTypeName})이 규칙 파일에 없습니다.");

            // 조회 계층이 규칙을 **다시 선언**하지 않는다(사실 조회만 한다).
            Source probe = FindProduction(ProbeTypeName);
            Assert.IsFalse(EntitlementAuditSource.DeclaresType(probe.Stripped, RuleTypeName),
                $"{LogPrefix} 조회 계층이 규칙 타입을 자체 선언했습니다 — 규칙이 두 곳으로 갈라지면 " +
                "한쪽만 고쳐지고, 그 갈라짐은 이 머신에서 컴파일조차 되지 않습니다.");
            Assert.IsTrue(EntitlementAuditSource.ContainsIdentifier(probe.Stripped, RuleTypeName),
                $"{LogPrefix} 조회 계층이 중립 규칙을 부르지 않습니다 — 규칙이 배선되지 않았습니다.");
        }

        // ====================================================================
        // 5. 음성 대조 — 순서 판정기가 실제로 구분하는가
        // ====================================================================

        /// <summary>
        /// 위 판정은 모두 «순서가 맞다»를 단언한다. 판정기가 눈이 멀면 전부 조용히 초록이 되므로,
        /// <b>일부러 어긋난 가짜 소스</b>와 <b>맞는 가짜 소스</b>를 같은 함수에 먹여 갈리는지 본다.
        /// </summary>
        [Test]
        public void 음성대조_순서_판정기가_어긋남과_정상을_구분한다()
        {
            const string good = "{ if (Rule.ShouldCacheVerdict(a, b)) { _cachedVerdictAt = now; } }";
            const string bad = "{ _cachedVerdictAt = now; if (Rule.ShouldCacheVerdict(a, b)) { } }";
            const string missing = "{ _cachedVerdictAt = now; }";

            Assert.IsTrue(ComesBefore(good, "ShouldCacheVerdict(", "_cachedVerdictAt = now;", out string _),
                $"{LogPrefix} 정상 순서를 위반으로 읽습니다(오탐) — 오탐이 나면 다음 사람이 감사를 끕니다.");
            Assert.IsFalse(ComesBefore(bad, "ShouldCacheVerdict(", "_cachedVerdictAt = now;", out string badWhy),
                $"{LogPrefix} 뒤집힌 순서를 통과시킵니다 — 실패를 다시 캐시하도록 되돌려도 초록입니다.");
            StringAssert.Contains("뒤에 있다", badWhy, $"{LogPrefix} 사유가 순서를 설명하지 않습니다.");
            Assert.IsFalse(ComesBefore(missing, "ShouldCacheVerdict(", "_cachedVerdictAt = now;", out string missWhy),
                $"{LogPrefix} 니들이 아예 없는데 «순서가 맞다»로 읽습니다 — 부재를 통과로 읽는 형태입니다.");
            StringAssert.Contains("없다", missWhy, $"{LogPrefix} 사유가 부재를 설명하지 않습니다.");

            // 본문 추출기도 같이 대조한다 — 못 찾으면 null이어야 한다(빈 문자열이면 위 판정이 공허해진다).
            Assert.IsNull(MethodBodyOrNull("class X { void A() { } }", "void ZZZ("),
                $"{LogPrefix} 없는 서명에 본문을 돌려줍니다.");
            Assert.IsNotNull(MethodBodyOrNull("class X { void A() { int i = 0; } }", "void A("),
                $"{LogPrefix} 있는 서명의 본문을 못 찾습니다.");
        }
    }
}
