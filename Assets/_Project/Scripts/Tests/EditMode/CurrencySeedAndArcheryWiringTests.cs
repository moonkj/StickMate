using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>첫 실행 시드와 활쏘기 상금이 실제로 배선됐는가</b> — 2026-09-06 배선 라운드 2차.
    ///
    /// ============================================================================
    /// 이 파일이 생긴 이유 — <b>모델은 옳은데 부르는 코드가 0건이었다</b>
    /// ============================================================================
    /// <see cref="CurrencyModel.TryGrantSeedCoins"/>와 <see cref="CurrencyModel.TryAwardArcheryCoins"/>의
    /// <b>계산</b>은 <c>CurrencyRulesTests</c>가 이미 검증하고 있었다. 그런데 프로덕션 호출부가
    /// <b>둘 다 0건</b>이었다(<c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §17-14 표).
    /// 사용자에게는 «시드를 못 받고 명중해도 동전이 안 나오는» 앱이었는데,
    /// <b>테스트는 전부 초록이었다</b> — 이 저장소가 반복해 당한 형태 그대로다.
    ///
    /// <para>그래서 이 파일은 <c>CurrencyRulesTests</c>·<c>CurrencyDayRolloverTests</c>와
    /// <b>겹치지 않는 것</b>만 잰다:</para>
    /// <list type="number">
    ///   <item><b>배선이 실재하는가</b>(§1 소스 스캔). 이 라운드 <b>전에는 전부 빨갛다</b>.</item>
    ///   <item><b>순서가 맞는가</b>(§2). 시드 지급이 <c>CharacterSaveStore.Load()</c> <b>앞</b>에 있으면
    ///     복원이 그것을 덮어써 <b>지급이 통째로 사라진다</b> — 예외도 로그도 없다.
    ///     롤오버가 같은 함정을 안고 있었고 그래서 그쪽 주석에 «반드시 Load 뒤»가 못박혀 있다.</item>
    ///   <item><b>관문 안쪽인가</b>(§3). 활쏘기 동전이 «정중앙 · Release · 같은 발 방어» 세 관문
    ///     <b>안</b>에서 나가야 «명중 1회 = 지급 1회»가 한 이음매로 유지된다.</item>
    ///   <item><b>계약이 디스크를 왕복하는가</b>(§4). 시드 1회 보장의 실체는 코드가 아니라
    ///     세이브 필드 <c>seedGranted</c>다. 그 필드가 저장·복원되지 않으면
    ///     <b>앱을 켤 때마다 1,200동전이 나온다</b>.</item>
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

        private static string SeedModelCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TryGrantSeedCoins) + "(";

        private static string ArcheryModelCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.TryAwardArcheryCoins) + "(";

        /// <summary>이미 배선된 것으로 <b>독립 확인된</b> API — 양성 대조 앵커
        /// (<c>Interaction/FocusWatchDirector.cs</c>, §17-14 실측).</summary>
        private static string KnownWiredCall =>
            nameof(CurrencyModel) + "." + nameof(CurrencyModel.PayFocusCompletionCoins) + "(";

        /// <summary>세이브를 읽는 유일한 진입점. 시드 지급은 <b>반드시 이 뒤</b>다.</summary>
        private static string SaveLoadCall =>
            nameof(CharacterSaveStore) + "." + nameof(CharacterSaveStore.Load) + "(";

        /// <summary>
        /// 시드 배선이 <see cref="Start"/>에 나타나는 형태 두 가지. <b>둘 중 하나만</b> 있으면 된다.
        /// <para>★ 래퍼 이름(<c>TryGrantSeedCoinsOnce</c>)은 <b>니들</b>이라 썩을 수 있다. 그래서
        /// <see cref="시드_배선_니들이_실재를_가리킨다"/>가 «이 이름의 메서드가 실제로 있고, 그 안에서
        /// 모델을 부른다»를 같은 실행에서 대조한다 — 이름만 바뀌면 그 테스트가 먼저 빨개진다.</para>
        /// </summary>
        private static readonly string[] SeedCallForms = { "TryGrantSeedCoinsOnce(", "CurrencyModel.TryGrantSeedCoins(" };

        /// <summary>활쏘기 동전 배선이 <b>명중 훅 안</b>에 나타나는 형태 두 가지.</summary>
        private static readonly string[] ArcheryCallForms = { "AwardArcheryCoins(", "CurrencyModel.TryAwardArcheryCoins(" };

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

        [Test]
        public void 프로덕션에_첫실행_시드_호출부가_실재한다()
        {
            List<Source> all = ProductionOrFail();
            List<string> callers = FilesCalling(all, SeedModelCall);

            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{SeedModelCall}」를 부르는 프로덕션 파일이 0건입니다. " +
                $"시드({nameof(CurrencyRules)}.{nameof(CurrencyRules.SeedCoins)})는 «평생 1회»라 " +
                "부르는 코드가 없으면 <b>아무도 영원히 못 받습니다</b>. 그리고 잔액 0은 " +
                "«아직 못 벌었다»와 똑같이 생겨서 화면만 봐서는 고장인지 알 수 없습니다.");

            TestContext.WriteLine($"{LogPrefix} 시드 호출부 {callers.Count}건: {string.Join(", ", callers)}");
        }

        [Test]
        public void 프로덕션에_활쏘기_상금_호출부가_실재한다()
        {
            List<Source> all = ProductionOrFail();
            List<string> callers = FilesCalling(all, ArcheryModelCall);

            Assert.IsNotEmpty(callers,
                $"{LogPrefix} 「{ArcheryModelCall}」를 부르는 프로덕션 파일이 0건입니다. " +
                "정중앙에 맞아도 동전이 한 푼도 안 나옵니다 — 연출은 그대로 도는데 지급만 없는 상태라 " +
                "사용자에게는 «고장»으로 읽힙니다(§20-8).");

            // ★ 지급 이음매는 <b>하나</b>여야 한다. 둘이 되면 같은 명중이 두 번 지급되거나,
            //   둘 중 하나만 쿨다운을 지나 «어떤 명중은 되고 어떤 명중은 안 되는» 형태가 된다.
            Assert.AreEqual(1, callers.Count,
                $"{LogPrefix} 활쏘기 지급 호출부가 {callers.Count}곳입니다(1이어야 합니다): " +
                string.Join(", ", callers) + ". 지급처가 여럿이면 «명중 1회»의 정의가 여러 벌이 됩니다.");

            TestContext.WriteLine($"{LogPrefix} 활쏘기 호출부 {callers.Count}건: {string.Join(", ", callers)}");
        }

        // ====================================================================
        // §2. 순서 — 시드는 반드시 세이브 로드 <b>뒤</b>
        // ====================================================================

        /// <summary>
        /// ★★ <b>이 순서가 이 라운드에서 가장 조용한 함정이다.</b>
        /// 시드 지급이 <c>CharacterSaveStore.Load()</c> 앞에 있으면, 복원이
        /// <c>coinBalance</c>와 <c>seedGranted</c>를 디스크 값으로 덮어써 <b>지급이 없던 일이 된다</b>.
        /// 예외도 로그도 남지 않고, <b>다음 실행에서도 똑같이</b> 사라진다.
        /// (롤오버가 정확히 같은 함정을 안고 있어 그쪽 주석에 «반드시 Load 뒤»가 못박혀 있다.)
        /// </summary>
        [Test]
        public void 시드_지급은_세이브를_읽은_뒤에_일어난다()
        {
            Source director = DirectorSourceOrFail(ProductionOrFail());
            string region = MemberRegionOrNull(director.Code, StartHeader);

            Assert.IsNotNull(region,
                $"{LogPrefix} 「{StartHeader}」 구역을 잘라 내지 못했습니다 — 시드 배선이 여기 없거나 " +
                "구역 앵커가 낡았습니다. 어느 쪽이든 아래 순서 판정은 성립하지 않습니다.");

            // ── 구역 추출기의 양성/음성 대조. 이게 없으면 아래 "앞뒤" 판정이
            //    «파일 전체를 구역이라고 우기는 것»과 구별되지 않는다.
            StringAssert.Contains("CharacterScaleController.Bind(", region,
                $"{LogPrefix} Start 구역에 알려진 앵커(CharacterScaleController.Bind)가 없습니다 — " +
                "구역을 엉뚱하게 잘랐거나 Start의 내용이 통째로 바뀌었습니다.");
            Assert.Less(region.IndexOf("TickIfDue(", StringComparison.Ordinal), 0,
                $"{LogPrefix} Start 구역에 Update의 내용(TickIfDue)이 섞여 들어왔습니다 — " +
                "구역이 다음 멤버까지 삼켰습니다. 이 실행의 순서 판정은 무효입니다.");

            int loadAt = region.IndexOf(SaveLoadCall, StringComparison.Ordinal);
            Assert.GreaterOrEqual(loadAt, 0,
                $"{LogPrefix} Start 구역에서 「{SaveLoadCall}」를 찾지 못했습니다. " +
                "세이브를 읽는 자리가 옮겨 갔다면 시드 배선도 그 뒤로 함께 옮겨야 합니다.");

            List<string> forms = FormsPresent(region, SeedCallForms);
            Assert.IsNotEmpty(forms,
                $"{LogPrefix} Start 구역에서 시드 배선을 찾지 못했습니다(찾은 형태: 없음, " +
                $"기대 형태: {string.Join(" 또는 ", SeedCallForms)}). " +
                "시드는 «로드 직후 1회»라 다른 자리가 없습니다 — Update에 넣으면 매 프레임 묻게 되고, " +
                "Awake에 넣으면 로드 앞이라 지급이 덮여 사라집니다.");

            foreach (string form in forms)
            {
                int seedAt = region.IndexOf(form, StringComparison.Ordinal);
                Assert.Greater(seedAt, loadAt,
                    $"{LogPrefix} 시드 지급(「{form}」)이 세이브 로드(「{SaveLoadCall}」)보다 <b>앞</b>에 있습니다. " +
                    "복원이 seedGranted=false와 coinBalance를 그대로 덮어써 지급이 통째로 사라집니다 — " +
                    "그 실패는 예외도 로그도 남기지 않고 다음 실행에서도 똑같이 반복됩니다.");
            }

            TestContext.WriteLine($"{LogPrefix} Start 구역 {region.Length}자, 로드 @{loadAt}, " +
                $"시드 형태 {forms.Count}종({string.Join(", ", forms)}).");
        }

        /// <summary>
        /// ★ 위 테스트가 쓰는 <b>래퍼 니들이 실재를 가리키는지</b> 같은 실행에서 대조한다.
        /// 니들만 맞고 실물이 없으면 위 초록은 «있는 것을 확인했다»가 아니라 «문자열을 찾았다»일 뿐이다.
        /// </summary>
        [Test]
        public void 시드_배선_니들이_실재를_가리킨다()
        {
            Source director = DirectorSourceOrFail(ProductionOrFail());
            string startRegion = MemberRegionOrNull(director.Code, StartHeader);
            Assert.IsNotNull(startRegion, $"{LogPrefix} Start 구역을 못 잘랐습니다.");

            List<string> forms = FormsPresent(startRegion, SeedCallForms);
            Assert.IsNotEmpty(forms, $"{LogPrefix} Start 구역에 시드 배선이 없습니다.");

            foreach (string form in forms)
            {
                if (form.StartsWith(nameof(CurrencyModel), StringComparison.Ordinal))
                {
                    // 모델을 직접 부르는 형태 — 위 §1이 이미 실재를 확인했다.
                    continue;
                }

                // 래퍼 형태 — 그 이름의 <b>선언</b>이 같은 파일에 있어야 하고,
                // 그 선언 구역이 모델 호출을 실제로 담고 있어야 한다.
                string wrapperName = form.Substring(0, form.Length - 1);   // 끝의 '(' 제거
                string wrapperRegion = MemberRegionOrNull(director.Code, "void " + wrapperName + "(");

                Assert.IsNotNull(wrapperRegion,
                    $"{LogPrefix} Start가 부르는 「{wrapperName}」의 선언을 같은 파일에서 못 찾았습니다 — " +
                    "니들이 실물과 갈라졌습니다(이름이 바뀌었거나 다른 파일로 옮겼습니다).");
                StringAssert.Contains(SeedModelCall, wrapperRegion,
                    $"{LogPrefix} 「{wrapperName}」 안에서 「{SeedModelCall}」를 못 찾았습니다 — " +
                    "래퍼가 이름만 남고 실제 지급을 하지 않습니다.");
            }
        }

        // ====================================================================
        // §3. 관문 — 활쏘기 동전은 «정중앙 · Release · 같은 발 방어» 안쪽에서 나간다
        // ====================================================================

        [Test]
        public void 활쏘기_상금은_명중_관문_안쪽에서_나간다()
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
                    "그 관문이 없으면 빗나간 발이나 Aim 시점에도 동전이 나갑니다 — " +
                    "«명중 = 동전»이라는 규칙이 화면에서 깨집니다.");
            }

            List<string> forms = FormsPresent(region, ArcheryCallForms);
            Assert.IsNotEmpty(forms,
                $"{LogPrefix} 명중 훅 구역에서 활쏘기 지급을 찾지 못했습니다(기대 형태: " +
                string.Join(" 또는 ", ArcheryCallForms) + "). " +
                "지급을 다른 구독으로 빼면 «명중 1회»의 정의가 두 벌이 되고, 둘이 갈라지는 날 " +
                "«XP는 들어왔는데 동전은 안 들어왔다»가 됩니다.");

            // 지급은 관문 <b>뒤</b>에 있어야 한다(관문 앞이면 관문이 아무것도 막지 못한다).
            int gateAt = region.IndexOf(nameof(ArcheryShotPhase) + "." + nameof(ArcheryShotPhase.Release),
                StringComparison.Ordinal);
            foreach (string form in forms)
            {
                Assert.Greater(region.IndexOf(form, StringComparison.Ordinal), gateAt,
                    $"{LogPrefix} 지급(「{form}」)이 Release 관문보다 앞에 있습니다 — 관문이 지급을 못 막습니다.");
            }
        }

        /// <summary>★ 활쏘기 지급이 <b>단조 시계</b>를 받는지. 벽시계를 넣으면 시계를 되감는 것만으로
        /// 무한 파밍이 된다(§20-3-b). <c>DailyLimitClampAuditTests</c>가 «금지 토큰»쪽에서 같은 것을
        /// 반대 방향으로 잠그고 있어, 둘이 함께 있어야 «올바른 시계를 쓴다»가 증명된다.</summary>
        [Test]
        public void 활쏘기_상금은_단조시계를_받는다()
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
            string absent = SeedModelCall.Replace("(", "ThatCannotExist(");
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
        // §5. 계약 — 시드 1회 보장이 <b>디스크를 왕복</b>하는가
        // ====================================================================
        //
        // ★ 여기가 «재실행해도 중복 지급 안 됨»의 실체다. 배선은 앱을 켤 때마다 부르므로,
        //   1회 보장은 «부르지 않는 것»이 아니라 «디스크의 seedGranted가 참으로 돌아오는 것»이다.
        //   CurrencyRulesTests는 메모리 안에서만 그것을 쟀다 — 저장 왕복은 여기서 처음 잰다.

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
        /// ★★ <b>앱을 두 번 켜도 시드는 한 번이다.</b> 배선은 실행마다 <see cref="CurrencyModel.TryGrantSeedCoins"/>를
        /// 부르므로, 1회 보장의 실체는 «디스크의 <c>seedGranted</c>가 참으로 돌아오는 것» 하나다.
        /// 그 필드가 저장되지 않거나 복원되지 않으면 <b>켤 때마다 시드가 나온다</b>.
        /// </summary>
        [Test]
        public void 시드는_저장과_재로드를_거쳐도_두_번_나오지_않는다()
        {
            string path = CharacterSaveStore.FilePath;
            if (File.Exists(path)) File.Delete(path);      // 「첫 실행」 = 파일이 없다

            // ── 1회차 실행 흉내: 로드(파일 없음) → 배선이 부르는 것과 같은 호출 → 주기 저장
            CharacterSaveStore.Load();
            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                "전제 — 파일이 없어야 «첫 실행»입니다. 앞선 테스트의 파일이 남아 있습니다.");
            Assert.IsFalse(CurrencyModel.SeedGranted, "전제 — 새 캐릭터는 시드를 받은 적이 없어야 합니다.");

            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.TryGrantSeedCoins(),
                "첫 실행에서 시드가 나오지 않았습니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                "지급했다는데 잔액이 그만큼이 아닙니다.");

            Assert.IsTrue(CharacterSaveStore.Save(),
                "저장이 실패/보류됐습니다 — 아래 재로드는 «저장된 것»이 아니라 «아무것도 아닌 것»을 읽게 되고, " +
                "그러면 이 테스트의 초록은 아무것도 증명하지 않습니다.");

            // ── 2회차 실행 흉내: 정적 상태를 전부 날리고 디스크에서 다시 읽는다.
            ResetModels();
            Assert.AreEqual(0, CurrencyModel.CoinBalance,
                "전제 — 초기화가 정적 상태를 안 지웠다면 아래 단언은 «파일이 말한 것»을 재지 못합니다.");
            Assert.IsFalse(CurrencyModel.SeedGranted, "전제 — 초기화 후에는 플래그가 꺼져 있어야 합니다.");

            CharacterSaveStore.Load();
            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "방금 쓴 파일을 다시 읽지 못했습니다.");
            Assert.IsTrue(CurrencyModel.SeedGranted,
                "seedGranted가 디스크를 왕복하지 못했습니다 — <b>앱을 켤 때마다 시드가 다시 나옵니다</b>. " +
                "저장 필드가 빠졌거나 복원 경로에서 누락됐습니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                "잔액이 왕복하지 못했습니다.");

            // ── 배선이 2회차에도 부른다(그게 정상 경로다). 그때 아무 일도 없어야 한다.
            Assert.AreEqual(0, CurrencyModel.TryGrantSeedCoins(),
                "두 번째 실행에서 시드가 또 나왔습니다 — 무한 시드입니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                "두 번째 호출이 0을 돌려주고도 잔액을 올렸습니다.");
        }

        /// <summary>
        /// ★ <b>기존 사용자도 받는다</b> — 확정 계약은 «신규 캐릭터만»이 아니라 «전원 평생 1회»다
        /// (<c>CurrencyRules.SeedCoins</c> 문서, U-42 리더 승인 2026-09-05).
        /// <para>여기서 재는 것은 «파일이 있는 사용자»(= 기존 사용자)가 로드 직후 지급 대상인가다.
        /// 이 단언이 깨지는 유일한 길은 배선을 «파일이 없을 때만»으로 좁히는 것이고,
        /// 그러면 지금까지 놀던 사용자 전원이 시드를 영영 못 받는다.</para>
        /// </summary>
        [Test]
        public void 파일이_있는_기존_사용자도_시드_지급_대상이다()
        {
            string path = CharacterSaveStore.FilePath;
            if (File.Exists(path)) File.Delete(path);

            // 시드를 <b>받지 않은</b> 기존 사용자를 만든다 — 레벨만 올려 놓고 저장한다.
            CharacterProgressionModel.RestoreFromSave(12, 0f, 0f, "기존사용자");
            Assert.IsTrue(CharacterSaveStore.Save(), "기존 사용자 파일을 만들지 못했습니다.");

            ResetModels();
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                "전제 — 파일이 있어야 «기존 사용자»입니다.");
            Assert.IsFalse(CurrencyModel.SeedGranted,
                "전제 — 이 사용자는 아직 시드를 받은 적이 없습니다.");
            Assert.IsTrue(CurrencyRules.CanGrantSeed(CurrencyModel.SeedGranted),
                "파일이 있다는 이유로 지급 대상에서 빠졌습니다 — U-42는 «기존 사용자 포함 전원 평생 1회»입니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.TryGrantSeedCoins(),
                "기존 사용자가 시드를 못 받았습니다.");
        }

        /// <summary>
        /// ★ 지급이 <b>저장에 실리는 경로</b>가 살아 있는가. 시드도 활쏘기도 즉시 저장을 부르지 않고
        /// <c>IsDirty</c>만 세운 뒤 주기/종료 저장에 얹힌다 — 그 합류 지점
        /// (<c>CharacterProgressionDirector.IsAnythingDirty</c>)에서 <c>CurrencyModel.IsDirty</c>가
        /// 빠지면 <b>번 동전이 통째로 사라진다</b>. 여기서는 그 전제인 «지급이 IsDirty를 세운다»를 잠근다.
        /// </summary>
        [Test]
        public void 시드와_활쏘기_지급은_저장_대상으로_표시된다()
        {
            Assert.IsFalse(CurrencyModel.IsDirty, "전제 — 초기화 직후에는 저장할 것이 없어야 합니다.");

            Assert.Greater(CurrencyModel.TryGrantSeedCoins(), 0, "전제 — 시드가 나와야 합니다.");
            Assert.IsTrue(CurrencyModel.IsDirty,
                "시드를 지급했는데 저장 대상으로 표시되지 않았습니다 — 주기/종료 저장이 이 지급을 " +
                "싣지 않고, 사용자는 다음 실행에서 시드를 다시 받게 됩니다(또는 영영 못 받습니다).");

            Assert.IsTrue(CharacterSaveStore.Save(), "저장이 실패/보류됐습니다.");
            Assert.IsFalse(CurrencyModel.IsDirty, "저장했는데 더티 플래그가 그대로입니다.");

            Assert.Greater(CurrencyModel.TryAwardArcheryCoins(0.0), 0, "전제 — 활쏘기 상금이 나와야 합니다.");
            Assert.IsTrue(CurrencyModel.IsDirty,
                "활쏘기 상금을 지급했는데 저장 대상으로 표시되지 않았습니다 — " +
                "그날 번 활쏘기 동전이 디스크에 한 푼도 남지 않습니다.");
        }
    }
}
