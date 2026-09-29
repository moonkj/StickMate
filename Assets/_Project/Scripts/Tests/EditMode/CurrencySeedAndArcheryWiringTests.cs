using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 원래 제목: <b>첫 실행 시드와 활쏘기 상금이 실제로 배선됐는가</b> — 2026-09-06 배선 라운드 2차.
    ///
    /// ============================================================================
    /// 이 파일이 생긴 이유 — <b>모델은 옳은데 부르는 코드가 0건이었다</b>
    /// ============================================================================
    /// 시드·활쏘기 지급의 <b>계산</b>은 <c>CurrencyRulesTests</c>가 이미 검증하고 있었다. 그런데
    /// 프로덕션 호출부가 <b>둘 다 0건</b>이었다(<c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §17-14 표).
    /// 사용자에게는 «시드를 못 받고 명중해도 동전이 안 나오는» 앱이었는데,
    /// <b>테스트는 전부 초록이었다</b> — 이 저장소가 반복해 당한 형태 그대로다.
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>두 축 중 하나는 방향이 뒤집혔다</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>시드</b> — 폐지. §1의 존재 단언이 <b>부재 단언</b>이 됐고(0건이 기대값),
    ///     §2(로드 뒤 순서)와 §5의 시드 왕복 두 테스트는 대상이 사라져 뗐다.
    ///     ★ <b>되살리면 기존 사용자 전원에게 한 번 더 나간다</b>(플래그가 전부 false).</item>
    ///   <item><b>활쏘기</b> — <b>판정은 그대로 살아 있고 동전 지급만 빠졌다.</b> 그 판정이
    ///     2026-09-07 보안 결함 수정 이후 <b>활쏘기 XP의 관문</b>이라, 이 파일의 활쏘기 절은
    ///     폐지 대상이 아니라 <b>더 중요해졌다</b>(이름만 <c>TryClaimArcheryAward</c>로 따라갔다).</item>
    /// </list>
    ///
    /// <para>그래서 이 파일은 <c>CurrencyRulesTests</c>·<c>CurrencyDayRolloverTests</c>와
    /// <b>겹치지 않는 것</b>만 잰다:</para>
    /// <list type="number">
    ///   <item><b>배선이 실재하는가 / 되살아나지 않았는가</b>(§1 소스 스캔).</item>
    ///   <item><s>순서가 맞는가</s>(§2 폐지 — 그 함정 서술은 절 주석에 남겼다).</item>
    ///   <item><b>관문 안쪽인가</b>(§3). 활쏘기 판정이 «정중앙 · Release · 같은 발 방어» 세 관문
    ///     <b>안</b>에서 일어나야 «명중 1회 = 판정 1회»가 한 이음매로 유지된다.</item>
    ///   <item><b>저장에 실리는가</b>(§5). 활쏘기 누계가 디스크에 안 남으면 재시작마다 일일 상한이
    ///     초기화되고, 그 상한이 XP 도배 방어선이라 곧 익스플로잇이 된다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 숫자를 한 개도 베끼지 않는다
    /// ============================================================================
    /// 1200·20·600을 리터럴로 적지 않는다. 기대값은 전부 <see cref="CurrencyRules"/> 상수에서 온다.
    ///
    /// ============================================================================
    /// ★ 니들(문자열)을 쓰는 곳과 그것을 못박는 방법
    /// ============================================================================
    /// 소스 스캔은 원리상 문자열을 쓴다. CLAUDE.md가 요구하는 대로 <b>모든 니들에 존재 단언을 건다</b> —
    /// 이름이 바뀌면 «조용히 초록»이 아니라 <b>시끄럽게 빨강</b>이 되도록. 그리고 「0건」류 부재 단언에는
    /// 반드시 <b>같은 스캐너로 잡히는 양성 대조</b>를 붙인다(부재 단언은 썩어도 빨개지지 않는다).
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. <c>Core/</c>·<c>Interaction/</c>만 읽고 <c>Platform/</c>을
    /// 건드리지 않으므로 활성 빌드 타깃과 무관하다(소스를 <b>파일로</b> 읽는다 — 타입 리플렉션이 아니다).</para>
    /// </summary>
    public sealed class CurrencySeedAndArcheryWiringTests
    {
        private const string LogPrefix = "[재화배선]";

        // ====================================================================
        // 니들 — 전부 nameof로 조립한다(오타가 컴파일 에러가 되도록)
        // ====================================================================

        private static string ArcheryModelCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TryClaimArcheryAward) + "(";

        /// <summary>양성 대조 앵커 — <b>배선이 실재하는 것으로 독립 확인된</b> API.
        /// <para>★★ 2026-09-29 — 앵커를 <c>CurrencyModel.PayFocusCompletionCoins</c>에서
        /// <see cref="CurrencyModel.TickDayRollover"/>로 <b>옮겼다</b>. 옛 앵커는 재화 폐지로
        /// <b>프로덕션 호출부가 0</b>이 됐다 — 그대로 두면 「스캐너 생존 확인」이 영구히 빨개진다.
        /// 새 앵커는 <c>Core/CurrencyDayRolloverTicker.cs</c>가 부르고, 그 배선은 [오늘 할일]
        /// 날짜축이 의존하므로 재화와 함께 사라질 수 없다.
        /// ⚠ <b>이 앵커로 검사 대상(활쏘기)을 쓰면 안 된다</b> — 대조가 순환이 된다.</para></summary>
        private static string KnownWiredCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TickDayRollover) + "(";

        /// <summary>세이브를 읽는 유일한 진입점.</summary>
        private static string SaveLoadCall =>
            nameof(CharacterSaveStore) + "." + nameof(CharacterSaveStore.Load) + "(";

        /// <summary>★★ 폐지된 시드 배선이 <c>Start</c>에 나타났던 형태 두 가지.
        /// 지금은 <b>둘 다 0건이어야 한다</b>(래퍼 이름까지 함께 세는 것이 요점이다 — 모델 호출만
        /// 세면 래퍼를 남겨 둔 상태를 못 본다).</summary>
        private static readonly string[] RetiredSeedCallForms = { "TryGrantSeedCoinsOnce(", "CurrencyModel.TryGrantSeedCoins(" };

        /// <summary>활쏘기 보상 판정 배선이 <b>명중 훅 안</b>에 나타나는 형태 두 가지.</summary>
        private static readonly string[] ArcheryCallForms = { "ClaimArcheryAward(", "CurrencyModel.TryClaimArcheryAward(" };

        /// <summary>배선이 사는 파일. <b>존재 단언</b>으로 쓴다 — 옮기면 시끄럽게 빨개진다.</summary>
        private const string DirectorFileSuffix = "/CharacterProgressionDirector.cs";

        // 메서드 구역을 자르는 앵커. 중괄호 짝맞추기를 <b>일부러 하지 않는다</b> —
        // 이 파일들은 보간 문자열 안에 중괄호가 들어 있어(예: $"Lv.{Level}") 짝맞추기가 조용히 어긋난다.
        // 대신 «선언 헤더 → 다음 멤버 선언»으로 자르고, 그 구역이 실제로 좁은지 음성 대조로 확인한다.
        private const string StartHeader = "void Start()";
        private const string ArcheryHookHeader = "void OnArcheryShotChanged(";

        // ====================================================================
        // 소스 도구
        // ====================================================================

        /// <summary>★ 아래 도구들은 <c>internal</c>이다 — 같은 라운드의
        /// <see cref="CurrencyIdleTodoTierWiringTests"/>가 <b>같은 스캐너</b>를 쓴다.
        /// 복제하지 않는 이유: 스캐너가 둘이면 «두 검사기가 같은 방향으로 틀리는» 형태가 열리고,
        /// 무엇보다 <b>대조(양성·음성·주석배제)는 이 파일에만 있다</b> — 스캐너를 공유해야 그 대조가
        /// 저쪽 초록까지 보증한다.</summary>
        internal struct Source
        {
            public string Path;
            public string Code;     // 주석 제거본
        }

        /// <summary>프로덕션 <c>.cs</c> 전량의 <b>주석 제거본</b>.
        /// <para>주석을 안 지우면 «계획을 적어 둔 주석»이 호출부로 잡힌다. 실제로 이 저장소의
        /// <c>CurrencyRules</c>에는 <i>"배선은 아직 없다"</i>는 주석이 여러 개 있다.</para></summary>
        internal static List<Source> Production()
        {
            var list = new List<Source>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                list.Add(new Source
                {
                    Path = path.Replace('\\', '/'),
                    Code = EntitlementAuditSource.StripComments(File.ReadAllText(path)),
                });
            }
            return list;
        }

        /// <summary>최소 수집량 가드 — 스캐너가 아무것도 못 읽고 «0건이니까 깨끗»이 되는 길을 먼저 막는다.</summary>
        internal static List<Source> ProductionOrFail()
        {
            List<Source> all = Production();
            Assert.GreaterOrEqual(all.Count, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 소스를 {all.Count}개밖에 못 읽었습니다(바닥값 " +
                $"{EntitlementAuditSource.MinProductionFileCount}). 경로가 틀렸거나 스캐너가 죽었습니다 — " +
                "이 실행의 모든 「N건」을 폐기하십시오.");
            return all;
        }

        internal static List<string> FilesCalling(List<Source> sources, string needle)
        {
            var hits = new List<string>();
            foreach (Source s in sources)
            {
                if (s.Code.IndexOf(needle, StringComparison.Ordinal) >= 0) hits.Add(s.Path);
            }
            return hits;
        }

        internal static Source DirectorSourceOrFail(List<Source> sources)
        {
            var found = new List<Source>();
            foreach (Source s in sources)
            {
                if (s.Path.EndsWith(DirectorFileSuffix, StringComparison.Ordinal)) found.Add(s);
            }
            Assert.AreEqual(1, found.Count,
                $"{LogPrefix} 「{DirectorFileSuffix}」에 해당하는 프로덕션 파일이 {found.Count}개입니다(1이어야 합니다). " +
                "파일을 옮겼거나 같은 이름이 여럿입니다 — 아래 구역 단언은 지금 아무것도 재지 못합니다.");
            return found[0];
        }

        /// <summary>
        /// <paramref name="header"/>로 시작하는 <b>멤버 하나의 구역</b>을 잘라 낸다:
        /// 헤더부터 <b>다음 멤버 선언</b>(줄 시작 + 8칸 들여쓰기 + 접근 한정자) 직전까지.
        ///
        /// <para>★ <b>중괄호 짝맞추기를 쓰지 않는 이유</b>: 이 저장소의 로그 문장은 보간 문자열이라
        /// 중괄호가 <b>문자열 안에</b> 들어 있다. 짝맞추기는 그 지점에서 조용히 어긋나고, 어긋난
        /// 결과는 «구역을 잘 잘랐다»와 똑같이 생겼다. 여기서는 못 자르면 <b>null</b>을 돌려주고
        /// 호출부가 실패시킨다.</para>
        /// </summary>
        internal static string MemberRegionOrNull(string code, string header)
        {
            int at = code.IndexOf(header, StringComparison.Ordinal);
            if (at < 0) return null;

            int end = code.Length;
            foreach (string boundary in new[] { "\n        private ", "\n        public ", "\n        internal ", "\n        protected " })
            {
                int b = code.IndexOf(boundary, at + header.Length, StringComparison.Ordinal);
                if (b >= 0 && b < end) end = b;
            }
            return code.Substring(at, end - at);
        }

        /// <summary>여러 형태 중 구역 안에 실제로 나타난 것들.</summary>
        internal static List<string> FormsPresent(string region, string[] forms)
        {
            var found = new List<string>();
            foreach (string form in forms)
            {
                if (region.IndexOf(form, StringComparison.Ordinal) >= 0) found.Add(form);
            }
            return found;
        }

        // ====================================================================
        // §1. 배선 감사 — <b>이 라운드 전에는 아래 둘이 빨갛다</b>
        // ====================================================================
        //
        // 실측(2026-09-06, 착수 직전): 프로덕션 전량에서
        //   CurrencyModel.TryGrantSeedCoins(    → 0건
        //   CurrencyModel.TryAwardArcheryCoins( → 0건
        //   CurrencyModel.PayFocusCompletionCoins( → 1건 (Interaction/FocusWatchDirector.cs)
        // 즉 아래 두 테스트는 그 시점에 실패하고 양성 대조는 통과한다.
        // 「새 테스트가 지금 빨갛지 않으면 그 테스트는 아무것도 안 잡는다」(CLAUDE.md)를 만족한다.
        //
        // ★★★ 2026-09-29 DLC·재화 폐지 R5 — 실측이 다시 바뀌었다:
        //   CurrencyModel.TryGrantSeedCoins(     → 0건 (<b>폐지. 0이 기대값이다</b>)
        //   CurrencyModel.TryClaimArcheryAward(  → 1건 (Interaction/CharacterProgressionDirector.cs)
        //   CurrencyModel.PayFocusCompletionCoins( → <b>0건</b>(죽은 잔재) ⇒ 양성 대조 앵커를
        //     CurrencyModel.TickDayRollover( 로 옮겼다(그쪽은 CurrencyDayRolloverTicker가 부른다).

        /// <summary>★★★ 2026-09-29 — <b>방향이 뒤집힌 테스트</b>. 옛 이름은
        /// <c>프로덕션에_첫실행_시드_호출부가_실재한다</c>였고 「0건이면 실패」였다. 지금은
        /// <b>0건이 기대값</b>이다(DLC·재화 폐지, 사용자 확정).
        /// <para>★ 부재 단언이라 양성 대조를 같은 실행에서 함께 잡는다
        /// (<see cref="KnownWiredCall"/>). 그것이 0이면 스캐너가 죽은 것이고 이 «0건»은 침묵이다.</para>
        /// <para>★★ <b>되살리지 마라</b>: 기존 사용자 전원의 <c>seedGranted</c>가 아직 <c>false</c>라
        /// 배선을 되살리는 순간 전원에게 동전이 한 번 더 나간다.</para></summary>
        [Test]
        public void 폐지된_첫실행_시드_호출부가_되살아나지_않았다()
        {
            List<Source> all = ProductionOrFail();

            // ── 양성 대조 먼저.
            Assert.IsNotEmpty(FilesCalling(all, KnownWiredCall),
                $"{LogPrefix} 양성 대조 실패 — 살아 있는 「{KnownWiredCall}」조차 못 찾았습니다. " +
                "스캐너가 죽었으므로 아래 «0건»은 측정이 아닙니다.");

            // ── 부재 단언. 모델 호출과 래퍼 이름을 <b>둘 다</b> 센다.
            foreach (string form in RetiredSeedCallForms)
            {
                List<string> callers = FilesCalling(all, form);
                Assert.IsEmpty(callers,
                    $"{LogPrefix} 폐지된 시드 배선(「{form}」)이 되살아났습니다: " +
                    string.Join(", ", callers) + ". 기존 사용자 전원의 seedGranted가 아직 false이므로 " +
                    "이 배선이 도는 순간 <b>전원에게 한 번 더</b> 지급됩니다.");
            }

            TestContext.WriteLine($"{LogPrefix} 시드 폐지 확인 — 형태 {RetiredSeedCallForms.Length}종 전부 0건.");
        }

        [Test]
        public void 프로덕션에_활쏘기_보상_판정_호출부가_실재한다()
        {
            List<Source> all = ProductionOrFail();
            List<string> callers = FilesCalling(all, ArcheryModelCall);

            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{ArcheryModelCall}」를 부르는 프로덕션 파일이 0건입니다. " +
                "정중앙에 맞아도 XP가 한 푼도 안 나옵니다 — 연출은 그대로 도는데 지급만 없는 상태라 " +
                "사용자에게는 «고장»으로 읽힙니다(§20-8). " +
                "★ 2026-09-29 이후 이 판정은 동전이 아니라 <b>활쏘기 XP의 관문</b>이다.");

            // ★ 지급 이음매는 <b>하나</b>여야 한다. 둘이 되면 같은 명중이 두 번 지급되거나,
            //   둘 중 하나만 쿨다운을 지나 «어떤 명중은 되고 어떤 명중은 안 되는» 형태가 된다.
            Assert.AreEqual(1, callers.Count,
                $"{LogPrefix} 활쏘기 지급 호출부가 {callers.Count}곳입니다(1이어야 합니다): " +
                string.Join(", ", callers) + ". 지급처가 여럿이면 «명중 1회»의 정의가 여러 벌이 됩니다.");

            TestContext.WriteLine($"{LogPrefix} 활쏘기 호출부 {callers.Count}건: {string.Join(", ", callers)}");
        }

        // ====================================================================
        // §2. 순서 — 「지급은 세이브 로드 뒤」 ★★★ 2026-09-29 <b>절 전체 폐지</b>
        // ====================================================================
        //
        // 뗀 것: <c>시드_지급은_세이브를_읽은_뒤에_일어난다</c> · <c>시드_배선_니들이_실재를_가리킨다</c>.
        //   시드 배선이 삭제됐다.
        //
        // ★★ <b>이 절이 지키던 함정은 사라지지 않았다</b> — 다음 지급 채널을 배선할 때 그대로 돌아온다:
        //   지급이 <c>CharacterSaveStore.Load()</c> <b>앞</b>에 있으면 복원이 그 값을 디스크 값으로
        //   덮어써 <b>지급이 없던 일이 된다</b>. 예외도 로그도 남지 않고 <b>다음 실행에서도 똑같이</b>
        //   사라진다. 롤오버 배선(<c>CheckNow</c>)이 같은 함정을 안고 있어 그쪽에는 지금도
        //   「반드시 Load 뒤」가 주석으로 못박혀 있다(<c>CharacterProgressionDirector.Start</c>).
        //
        // ★ 남아 있는 니들 <see cref="SaveLoadCall"/>·<see cref="StartHeader"/>는 아래 §3과
        //   구역 추출기 자체 대조(§5)가 계속 쓴다 — 지우지 않았다.

        // ====================================================================
        // §3. 관문 — 활쏘기 보상 판정은 «정중앙 · Release · 같은 발 방어» 안쪽에서 나간다
        // ====================================================================

        [Test]
        public void 활쏘기_보상_판정은_명중_관문_안쪽에서_나간다()
        {
            Source director = DirectorSourceOrFail(ProductionOrFail());
            string region = MemberRegionOrNull(director.Code, ArcheryHookHeader);

            Assert.IsNotNull(region,
                $"{LogPrefix} 「{ArcheryHookHeader}」 구역을 잘라 내지 못했습니다 — " +
                "명중 훅이 사라졌거나 앵커가 낡았습니다.");

            // ── 구역 추출기 대조(양성 하나, 음성 하나).
            Assert.Less(region.IndexOf(SaveLoadCall, StringComparison.Ordinal), 0,
                $"{LogPrefix} 명중 훅 구역에 Start의 내용({SaveLoadCall})이 섞였습니다 — 구역이 너무 넓습니다.");

            foreach (string gate in new[]
                     {
                         nameof(ArcheryShotResult) + "." + nameof(ArcheryShotResult.Bullseye),
                         nameof(ArcheryShotPhase) + "." + nameof(ArcheryShotPhase.Release),
                     })
            {
                StringAssert.Contains(gate, region,
                    $"{LogPrefix} 명중 훅에서 관문 「{gate}」가 사라졌습니다. " +
                    "그 관문이 없으면 빗나간 발이나 Aim 시점에도 보상이 나갑니다 — " +
                    "«명중 = 보상»이라는 규칙이 화면에서 깨집니다.");
            }

            List<string> forms = FormsPresent(region, ArcheryCallForms);
            Assert.IsNotEmpty(forms,
                $"{LogPrefix} 명중 훅 구역에서 활쏘기 보상 판정을 찾지 못했습니다(기대 형태: " +
                string.Join(" 또는 ", ArcheryCallForms) + "). " +
                "판정을 다른 구독으로 빼면 «명중 1회»의 정의가 두 벌이 되고, 둘이 갈라지는 날 " +
                "«쿨다운은 도는데 XP는 새는» 형태가 된다 — 2026-09-07 보안 결함이 정확히 그것이었다.");

            // 지급은 관문 <b>뒤</b>에 있어야 한다(관문 앞이면 관문이 아무것도 막지 못한다).
            int gateAt = region.IndexOf(nameof(ArcheryShotPhase) + "." + nameof(ArcheryShotPhase.Release),
                StringComparison.Ordinal);
            foreach (string form in forms)
            {
                Assert.Greater(region.IndexOf(form, StringComparison.Ordinal), gateAt,
                    $"{LogPrefix} 지급(「{form}」)이 Release 관문보다 앞에 있습니다 — 관문이 지급을 못 막습니다.");
            }
        }

        /// <summary>★ 활쏘기 보상 판정이 <b>단조 시계</b>를 받는지. 벽시계를 넣으면 시계를 되감는 것만으로
        /// 무한 파밍이 된다(§20-3-b — 2026-09-29 이후 그 파밍의 대상은 동전이 아니라 <b>XP</b>다).
        /// <c>DailyLimitClampAuditTests</c>가 «금지 토큰»쪽에서 같은 것을
        /// 반대 방향으로 잠그고 있어, 둘이 함께 있어야 «올바른 시계를 쓴다»가 증명된다.</summary>
        [Test]
        public void 활쏘기_보상_판정은_단조시계를_받는다()
        {
            Source director = DirectorSourceOrFail(ProductionOrFail());

            int at = director.Code.IndexOf(ArcheryModelCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"{LogPrefix} 「{ArcheryModelCall}」를 못 찾았습니다.");

            int close = director.Code.IndexOf(')', at);
            Assert.Greater(close, at, $"{LogPrefix} 호출의 닫는 괄호를 못 찾았습니다.");
            string argument = director.Code.Substring(at + ArcheryModelCall.Length, close - at - ArcheryModelCall.Length);

            StringAssert.Contains("realtimeSinceStartupAsDouble", argument,
                $"{LogPrefix} 활쏘기 지급 인자가 단조 시계가 아닙니다(실제 인자: 「{argument.Trim()}」). " +
                "벽시계를 넣으면 시계를 600초 되감는 것만으로 상금이 다시 나옵니다.");
        }

        // ====================================================================
        // §4. 스캐너 대조 — 「N건」이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        [Test]
        public void 배선_스캐너는_있는_것을_찾고_없는_것을_안_찾고_주석을_안_센다()
        {
            List<Source> all = ProductionOrFail();

            // (1) 양성 — 이미 배선된 것으로 독립 확인된 API를 찾아낸다.
            Assert.IsNotEmpty(FilesCalling(all, KnownWiredCall),
                $"{LogPrefix} 이미 배선된 「{KnownWiredCall}」조차 못 찾았습니다 — 스캐너가 죽었습니다. " +
                "위 모든 초록은 아무것도 증명하지 않습니다.");

            // (2) 음성 — 존재할 수 없는 이름은 0건이어야 한다.
            string absent = ArcheryModelCall.Replace("(", "ThatCannotExist(");
            CollectionAssert.IsEmpty(FilesCalling(all, absent),
                $"{LogPrefix} 존재하지 않는 이름 「{absent}」이 잡혔습니다 — " +
                "스캐너가 아무 문자열이나 참으로 만듭니다.");

            // (3) ★ 주석 배제가 살아 있는가 — 존재 단언과 부재 단언을 <b>같은 대상</b>에 건다.
            //     CurrencyRules.cs는 상점 가격 문단에서 TryPurchaseItem을 <b>주석으로</b> 언급하지만
            //     실제로 부르지는 않는다(그 파일은 순수 규칙이라 모델을 참조하지 않는다).
            //     두 방식으로 같은 파일을 읽어 결과가 <b>갈리는지</b>를 본다. 갈리지 않으면 둘 중 하나다 —
            //       (가) 주석 배제가 죽었다 → 이 파일의 모든 배선 감사가 주석을 호출부로 세고 있다
            //       (나) 앵커 주석이 사라졌다 → 대조를 옮겨야 한다
            //     어느 쪽이든 사람이 봐야 하므로 시끄럽게 빨개진다.
            string rulesSuffix = "/" + nameof(CurrencyRules) + ".cs";
            string purchaseNeedle = nameof(CurrencyModel) + "." + nameof(CurrencyModel.TryPurchaseItem);

            string rulesPath = null;
            foreach (Source s in all)
            {
                if (s.Path.EndsWith(rulesSuffix, StringComparison.Ordinal)) rulesPath = s.Path;
            }
            Assert.IsNotNull(rulesPath, $"{LogPrefix} {rulesSuffix}를 못 찾았습니다 — 주석 배제 대조가 성립하지 않습니다.");

            string raw = File.ReadAllText(rulesPath);
            string stripped = EntitlementAuditSource.StripComments(raw);

            Assert.GreaterOrEqual(raw.IndexOf(purchaseNeedle, StringComparison.Ordinal), 0,
                $"{LogPrefix} 주석 배제 대조의 앵커가 사라졌습니다 — {rulesSuffix}의 주석에 " +
                $"「{purchaseNeedle}」가 더 이상 없습니다. 「주석에는 있고 코드에는 없는」 다른 사례로 옮기십시오.");
            Assert.Less(stripped.IndexOf(purchaseNeedle, StringComparison.Ordinal), 0,
                $"{LogPrefix} {rulesSuffix}가 「{purchaseNeedle}」 <b>호출부</b>로 잡혔습니다. " +
                "그 파일에 있는 것이 여전히 주석뿐이라면 주석 배제가 죽은 것이고(배선 감사 전체가 무효), " +
                "정말로 부르기 시작했다면 이 대조를 다른 파일로 옮기십시오.");
        }

        // ====================================================================
        // §5. 계약 — 저장 왕복
        // ====================================================================
        //
        // ★ 원래 이 절은 «시드 1회 보장이 디스크를 왕복하는가»였다. 배선이 앱을 켤 때마다 부르므로
        //   1회 보장의 실체는 «부르지 않는 것»이 아니라 «디스크의 seedGranted가 참으로 돌아오는 것»
        //   이라는 것이 요점이었다.
        //
        // ★★★ 2026-09-29 DLC·재화 폐지 R5 — 시드 지급이 삭제돼 그 두 테스트
        //   (<c>시드는_저장과_재로드를_거쳐도_두_번_나오지_않는다</c> ·
        //   <c>파일이_있는_기존_사용자도_시드_지급_대상이다</c>)를 뗐다.
        //   <b>세이브 필드 <c>seedGranted</c>의 왕복 자체는 여전히 검증된다</b> —
        //   <c>Tests/EditMode/EquipmentMigrationTests</c>의 v10 왕복/하위 호환 테스트와
        //   <c>CurrencyDayRolloverTests.사흘_만에_재실행해도…</c>가 그 필드를 단언한다.
        //
        // ★ 남은 것은 «지급이 저장 대상으로 표시되는가» 하나이고, 대상이 활쏘기 판정으로 좁혀졌다.

        private bool _hadRealFile;
        private string _realFileBackup;

        [OneTimeSetUp]
        public void BackupSaveFile()
        {
            // ★ 개발자의 실제 저장 파일을 절대 건드리지 않는다(절대 불변 원칙 3).
            //   EditMode 전역 격리(GlobalEditModeTestIsolation)가 경로를 옮겨 놨어야 한다.
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 임시 폴더로 리디렉션돼 있지 않습니다 — " +
                "이 픽스처는 개발자의 실제 저장 파일을 만질 수 없으므로 여기서 멈춥니다.");

            string path = CharacterSaveStore.FilePath;
            _hadRealFile = File.Exists(path);
            _realFileBackup = _hadRealFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreSaveFile()
        {
            if (!CharacterSaveStore.IsRedirectedForTesting) return;
            string path = CharacterSaveStore.FilePath;
            if (_hadRealFile) File.WriteAllText(path, _realFileBackup);
            else if (File.Exists(path)) File.Delete(path);
            ResetModels();
        }

        [SetUp]
        public void ResetModels()
        {
            // 정적 상태가 앞선 테스트에서 새어 들어오면 «파일이 말한 것»과 «직전 상태»를 구분할 수 없다.
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
            AppSettingsModel.ResetForTesting();
        }

        /// <summary>
        /// ★ 판정이 <b>저장에 실리는 경로</b>가 살아 있는가. 활쏘기는 즉시 저장을 부르지 않고
        /// <c>IsDirty</c>만 세운 뒤 주기/종료 저장에 얹힌다 — 그 합류 지점
        /// (<c>CharacterProgressionDirector.IsAnythingDirty</c>)에서 <c>CurrencyModel.IsDirty</c>가
        /// 빠지면 <b>오늘의 관문 누계가 디스크에 한 줄도 남지 않는다</b>. 그러면 앱을 껐다 켤 때마다
        /// 활쏘기 일일 상한이 초기화되고, 그 상한이 <b>XP 도배 방어선</b>이라 곧 익스플로잇이 된다.
        /// 여기서는 그 전제인 «판정이 IsDirty를 세운다»를 잠근다.
        /// </summary>
        [Test]
        public void 활쏘기_판정은_저장_대상으로_표시된다()
        {
            Assert.IsFalse(CurrencyModel.IsDirty, "전제 — 초기화 직후에는 저장할 것이 없어야 합니다.");

            Assert.Greater(CurrencyModel.TryClaimArcheryAward(0.0), 0, "전제 — 보상 판정이 통과해야 합니다.");
            Assert.IsTrue(CurrencyModel.IsDirty,
                "활쏘기 판정이 저장 대상으로 표시되지 않았습니다 — 오늘 누계가 디스크에 남지 않아 " +
                "앱을 껐다 켜면 일일 상한이 초기화되고, 그 상한이 XP 도배 방어선입니다.");

            Assert.IsTrue(CharacterSaveStore.Save(), "저장이 실패/보류됐습니다.");
            Assert.IsFalse(CurrencyModel.IsDirty, "저장했는데 더티 플래그가 그대로입니다.");

            // 음성 대조 — 쿨다운에 막힌 호출은 아무것도 바꾸지 않으므로 더티를 세우지 않는다.
            Assert.AreEqual(0, CurrencyModel.TryClaimArcheryAward(1.0), "전제 — 쿨다운에 막혀야 합니다.");
            Assert.IsFalse(CurrencyModel.IsDirty,
                "아무 일도 없었는데 저장 대상이 됐습니다 — 하루 종일 켜 두는 앱이 헛되이 디스크를 두드립니다.");
        }
    }
}
