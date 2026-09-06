using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>일자 롤오버가 실제로 돌고, 실제로 되돌리는가</b> — 2026-09-06 배선 라운드.
    ///
    /// ============================================================================
    /// 이 파일이 생긴 이유 — <b>배선이 0건이었다</b>
    /// ============================================================================
    /// <see cref="CurrencyModel.TickDayRollover"/>는 일일 상한 · 무료 회복제 · 8시간 창 ·
    /// [오늘 할일] · 활쏘기 카운터를 되돌리는 <b>유일한</b> 코드인데(불변식 I-15′),
    /// <b>프로덕션 호출부가 0건</b>이었다(<c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §17-1).
    /// 모델 동작은 <see cref="CurrencyRulesTests"/>가 이미 검증하고 있었다 —
    /// <b>맞게 계산하는데 아무도 부르지 않는</b> 상태였고, 그 실패는 초록 테스트와 똑같이 생겼다.
    ///
    /// <para>그래서 이 파일은 <see cref="CurrencyRulesTests"/>와 <b>겹치지 않는 세 가지</b>만 잰다:</para>
    /// <list type="number">
    ///   <item><b>배선</b> — 프로덕션에 호출부가 실재하는가(소스 스캔, §1).</item>
    ///   <item><b>주기 게이트</b> — 얼마나 자주 묻는가(<see cref="CurrencyDayRolloverTicker"/>, §2).</item>
    ///   <item><b>경계 케이스</b> — 자정 1초 전/후, 여러 날 건너뛴 재실행, 상한이 <b>다시 열리는지</b>(§3~5).</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 숫자를 한 개도 베끼지 않는다
    /// ============================================================================
    /// 1500 · 2500 · 60 · 20시간을 리터럴로 적지 않는다. 기대값은 전부
    /// <see cref="CurrencyRules"/> · <see cref="CurrencyDayRolloverTicker"/> 상수에서 유도한다.
    /// 시각 리터럴(2026-09-06 23:59:59 등)만은 예외인데, 그건 <b>프로덕션 상수가 아니라
    /// 이 테스트가 고른 달력 좌표</b>이고 요율·상한이 바뀌어도 뜻이 변하지 않는다.
    ///
    /// ============================================================================
    /// ★ 벽시계를 <b>못</b> 밀어 넣는다 — 그게 결함이 아니라 설계다
    /// ============================================================================
    /// <see cref="CurrencyModel"/>에서 시각을 읽는 곳은 <c>ResolveTodayIndex</c> <b>한 곳</b>이고
    /// (security T-3-a), 거기에 가짜 시계를 주입할 구멍을 내면 <b>시간 소스가 둘</b>이 된다.
    /// 그래서 「자정을 걸치는」 검증은 두 조각으로 나눠 각각 정직하게 잰다:
    /// <list type="bullet">
    ///   <item><b>달력 쪽</b> — <see cref="CurrencyRules.LocalDayIndex"/>는 <b>시각을 인자로 받는</b>
    ///     순수 함수라 23:59:59 → 00:00:01을 <b>초 단위로</b> 그대로 넣는다(§3).</item>
    ///   <item><b>모델 쪽</b> — 「일자가 하나 늘었다」는 사실을
    ///     <see cref="CurrencyModel.SetDayIndexForTesting"/> / <see cref="CurrencyModel.RestoreFromSave"/>로
    ///     만들어 넣고, 그때 무엇이 되돌아가는지를 잰다(§4~5).</item>
    /// </list>
    /// 두 조각을 잇는 <c>ResolveTodayIndex</c>는 <b>실제 벽시계로</b> 돈다 — 그 한 줄만은
    /// 배치모드 실주행이 아니면 다른 날짜로 밀어 볼 수 없고, <b>그 사실을 숨기지 않는다</b>.
    /// </summary>
    public sealed class CurrencyDayRolloverTests
    {
        [SetUp]
        public void Reset() => CurrencyModel.ResetForTesting();

        [TearDown]
        public void Clean() => CurrencyModel.ResetForTesting();

        // ====================================================================
        // §1. 배선 감사 — <b>이 라운드 전에는 이 테스트가 빨갛다</b>
        // ====================================================================
        //
        // 실측(2026-09-06, 이 라운드 착수 직전): 프로덕션 242개 파일 중
        //   CurrencyModel.TickDayRollover(          → 0건
        //   CurrencyModel.PayFocusCompletionCoins(  → 1건 (Interaction/FocusWatchDirector.cs)
        // 즉 아래 첫 단언은 그 시점에 실패하고 양성 대조는 통과한다. 「새 테스트가 지금 빨갛지 않으면
        // 그 테스트는 아무것도 안 잡는다」(CLAUDE.md)의 조건을 만족한다.

        /// <summary>프로덕션(테스트 어셈블리 제외) 소스에서 <b>주석을 뺀</b> 코드 부분만 본다.
        /// <para>문자열 리터럴 안의 <c>//</c>나 여러 줄 블록 주석은 완벽히 가르지 못한다 —
        /// <b>못 재는 것을 재는 척하지 않는다.</b> 대신 아래 테스트가 양성/음성 대조와
        /// 최소 수집량 가드를 함께 두어, 스캐너가 죽으면 조용히 초록이 되는 길을 막는다.</para></summary>
        private static string CodePart(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line.Substring(0, at);
        }

        /// <summary><c>&lt;repo&gt;/Assets</c> 아래 <b>테스트가 아닌</b> 모든 <c>.cs</c>.
        /// 파일명 명부를 쓰지 않는다 — 파일을 쪼개거나 옮기는 순간 명부는 눈이 먼다.</summary>
        private static List<string> ProductionSources()
        {
            var found = new List<string>();
            foreach (string path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Scripts/Tests/")) continue;
                found.Add(normalized);
            }
            return found;
        }

        /// <summary>주어진 니들을 <b>전부</b> 담고 있는 프로덕션 파일 목록.
        /// <paramref name="stripComments"/>가 true면 주석을 뺀 코드만 본다.</summary>
        private static List<string> FilesContaining(IEnumerable<string> sources, bool stripComments,
            params string[] needles)
        {
            var hits = new List<string>();
            foreach (string path in sources)
            {
                bool[] seen = new bool[needles.Length];
                foreach (string raw in File.ReadAllLines(path))
                {
                    string text = stripComments ? CodePart(raw) : raw;
                    if (text.Length == 0) continue;
                    for (int i = 0; i < needles.Length; i++)
                    {
                        if (!seen[i] && text.IndexOf(needles[i], StringComparison.Ordinal) >= 0) seen[i] = true;
                    }
                }

                bool all = true;
                for (int i = 0; i < seen.Length; i++) all &= seen[i];
                if (all) hits.Add(path);
            }
            return hits;
        }

        /// <summary>주석을 뺀 <b>코드</b>에서만 찾는다(배선 감사의 기본형).</summary>
        private static List<string> FilesCalling(IEnumerable<string> sources, params string[] needles)
            => FilesContaining(sources, true, needles);

        /// <summary>
        /// ★★ <b>이 파일에서 가장 중요한 테스트다.</b> 모델이 아무리 옳아도
        /// <b>부르는 코드가 없으면 사용자에게는 아무 일도 일어나지 않는다.</b>
        /// </summary>
        [Test]
        public void 프로덕션에_일자_롤오버_호출부가_실재한다()
        {
            List<string> sources = ProductionSources();

            // 최소 수집량 가드 — 스캐너가 아무것도 못 읽고 "0건이니까 깨끗"이 되는 길을 먼저 막는다.
            Assert.Greater(sources.Count, 200,
                $"프로덕션 소스를 {sources.Count}개밖에 못 읽었습니다 — 경로가 틀렸거나 스캐너가 죽었습니다. " +
                "이 실행의 모든 「N건」을 폐기하십시오.");

            string modelCall = nameof(CurrencyModel) + "." + nameof(CurrencyModel.TickDayRollover) + "(";
            List<string> modelCallers = FilesCalling(sources, modelCall);

            Assert.IsNotEmpty(modelCallers,
                $"「{modelCall}」를 부르는 프로덕션 파일이 0건입니다. 일일 상한·8시간 창·[오늘 할일]·" +
                "활쏘기 카운터를 되돌리는 코드가 이것 하나뿐이므로(I-15′), 호출부가 없으면 " +
                "그 카운터들은 <b>영원히</b> 리셋되지 않고 그 상태가 세이브 파일에 그대로 적힙니다.");

            // ★ 여기서 멈추면 거짓 통과다 — 위 단언은 CurrencyDayRolloverTicker 하나만 있어도 통과한다
            //   (그 파일 안에 CurrencyModel.TickDayRollover( 가 있으니까). 그 티커를 <b>실제로 굴리는
            //   컴포넌트</b>가 없으면 사용자에게는 여전히 아무 일도 일어나지 않는다.
            //   그래서 티커 <b>자신을 뺀</b> 나머지에서 구동자를 찾는다 — 선언 파일은 자기 메서드
            //   시그니처 때문에 세 니들을 전부 갖고 있어, 빼지 않으면 이 단언이 <b>항상</b> 통과한다.
            string typeName = nameof(CurrencyDayRolloverTicker);
            string declaringFileSuffix = "/" + typeName + ".cs";
            List<string> outsideDeclaringFile =
                sources.FindAll(p => !p.EndsWith(declaringFileSuffix, StringComparison.Ordinal));

            Assert.AreEqual(sources.Count - 1, outsideDeclaringFile.Count,
                $"{typeName}를 선언한 파일이 정확히 1개가 아닙니다 — 제외가 아무것도 안 걸렀거나 " +
                "같은 이름의 파일이 여럿입니다. 어느 쪽이든 아래 단언은 뜻을 잃습니다.");

            List<string> tickerDrivers = FilesCalling(outsideDeclaringFile,
                typeName,
                nameof(CurrencyDayRolloverTicker.CheckNow) + "(",
                nameof(CurrencyDayRolloverTicker.TickIfDue) + "(");

            Assert.IsNotEmpty(tickerDrivers,
                $"{typeName}를 소유하면서 두 진입점(CheckNow=재실행 직후 · TickIfDue=가동 중 자정 통과)을 " +
                "<b>모두</b> 부르는 프로덕션 파일이 (티커 자신을 빼고) 없습니다. 한쪽만 배선하면 " +
                "「며칠 만에 재실행」 또는 「켠 채로 자정 통과」 중 한 경우가 통째로 죽고, " +
                "죽은 쪽은 화면에도 로그에도 흔적을 남기지 않습니다.");

            TestContext.WriteLine($"[배선] {modelCall} → {modelCallers.Count}건, " +
                $"{typeName} 구동자 → {tickerDrivers.Count}건 (프로덕션 {sources.Count}개 파일 스캔)");
        }

        /// <summary>
        /// ★ 위 테스트의 <b>양성·음성 대조</b>. 이게 없으면 위의 "실재한다"가
        /// <b>스캐너가 아무 문자열이나 찾아 주는 것</b>인지 구별되지 않는다.
        /// </summary>
        [Test]
        public void 배선_스캐너는_있는_것을_찾고_없는_것을_안_찾는다()
        {
            List<string> sources = ProductionSources();
            Assert.Greater(sources.Count, 200, "최소 수집량 미달 — 이 실행의 숫자는 전부 무효입니다.");

            // ★ 양성 대조 — 이미 배선된 것으로 <b>독립 확인된</b> API(집중 모드 완주 지급,
            //   GAME_ARCHITECTURE_REVIEW §17-14가 Interaction/FocusWatchDirector.cs에서 실측).
            string known = nameof(CurrencyModel) + "." + nameof(CurrencyModel.PayFocusCompletionCoins) + "(";
            Assert.IsNotEmpty(FilesCalling(sources, known),
                $"이미 배선돼 있는 「{known}」조차 못 찾았습니다 — 스캐너가 죽었습니다. " +
                "위 테스트의 초록은 아무것도 증명하지 않습니다.");

            // ★ 음성 대조 — 존재할 수 없는 이름은 0건이어야 한다("0건"이 진짜 0건인지).
            string absent = nameof(CurrencyModel) + "." + nameof(CurrencyModel.TickDayRollover) + "ThatCannotExist(";
            CollectionAssert.IsEmpty(FilesCalling(sources, absent),
                $"존재하지 않는 이름 「{absent}」이 잡혔습니다 — 스캐너가 아무 문자열이나 참으로 만듭니다.");

            // ================================================================
            // ★★ 주석 배제가 살아 있는가 — <b>존재 단언과 부재 단언을 같은 대상에 건다</b>
            // ================================================================
            // 부재 단언은 썩으면 <b>조용히 초록</b>이 된다(CLAUDE.md). 그래서 「주석에는 있는데
            // 코드에는 없는」 실재 사례 하나를 잡아, 두 방식으로 같은 파일을 읽고 결과가
            // <b>갈리는지</b>를 본다. 갈리지 않으면 둘 중 하나다 —
            //   (가) 주석 배제가 죽었다        → 배선 감사 전체가 주석을 호출부로 세고 있다
            //   (나) 앵커 주석이 사라졌거나 그 파일이 정말로 부르기 시작했다 → 대조를 옮겨야 한다
            // 어느 쪽이든 사람이 봐야 하므로 <b>시끄럽게</b> 빨개진다.
            string tierRead = nameof(CurrencyModel) + "." + nameof(CurrencyModel.StatTierReached) + "(";
            string anchorSuffix = "/" + nameof(EquipmentStatRules) + ".cs";

            bool anchorInRaw = FilesContaining(sources, false, tierRead)
                .Exists(p => p.EndsWith(anchorSuffix, StringComparison.Ordinal));
            bool anchorInCode = FilesCalling(sources, tierRead)
                .Exists(p => p.EndsWith(anchorSuffix, StringComparison.Ordinal));

            Assert.IsTrue(anchorInRaw,
                $"주석 배제 대조의 앵커가 사라졌습니다 — {nameof(EquipmentStatRules)}.cs의 주석에 " +
                $"「{tierRead}」가 더 이상 없습니다. 이 대조는 지금 아무것도 재지 않으므로, " +
                "「주석에는 있고 코드에는 없는」 다른 사례로 옮기십시오.");
            Assert.IsFalse(anchorInCode,
                $"{nameof(EquipmentStatRules)}.cs가 「{tierRead}」 <b>호출부</b>로 잡혔습니다. " +
                "그 파일에 있는 것이 여전히 주석뿐이라면 주석 배제가 죽은 것이고(배선 감사 전체가 무효), " +
                "정말로 부르기 시작했다면 이 대조를 다른 파일로 옮기십시오.");

            // 그리고 그 니들이 <b>코드에서는 다른 파일에</b> 실제로 잡힌다(스캐너가 늘 빈손이 아니다).
            Assert.IsNotEmpty(FilesCalling(sources, tierRead),
                $"「{tierRead}」를 코드에서 부르는 파일이 하나도 없습니다 — " +
                "위 부재 단언이 「주석 배제 덕분」인지 「스캐너가 아무것도 못 찾는 것」인지 갈리지 않습니다.");
        }

        // ====================================================================
        // §2. 주기 게이트 — 「얼마나 자주 묻는가」
        // ====================================================================

        [Test]
        public void 첫_틱은_주기를_기다리지_않고_그_뒤로는_간격을_요구한다()
        {
            var ticker = new CurrencyDayRolloverTicker();
            Assert.AreEqual(0, ticker.CheckCount, "전제 — 아직 달력을 본 적이 없어야 합니다.");
            Assert.IsTrue(double.IsNegativeInfinity(ticker.LastCheckMonotonic),
                "전제 — 기산점이 −∞라야 첫 호출이 무조건 통과합니다.");

            Assert.IsTrue(ticker.TickIfDue(0.0),
                "첫 틱이 달력을 보지 않았습니다 — 앱을 켠 뒤 한 주기 동안 어제 상태로 삽니다.");
            Assert.AreEqual(1, ticker.CheckCount);
            Assert.AreEqual(1, ticker.RolloverCount, "첫 판정에서 일자가 고정되지 않았습니다.");

            // 간격 안쪽에서는 아무리 많이 불러도 달력을 다시 보지 않는다(24시간 상주 앱의 비용).
            const int frames = 100;
            double step = CurrencyDayRolloverTicker.CheckIntervalSeconds / (frames + 1);
            double t = 0.0;
            for (int i = 0; i < frames; i++) { t += step; ticker.TickIfDue(t); }

            Assert.Less(t, CurrencyDayRolloverTicker.CheckIntervalSeconds,
                "전제 — 위 루프가 주기를 넘지 않아야 합니다.");
            Assert.AreEqual(1, ticker.CheckCount,
                $"주기({CurrencyDayRolloverTicker.CheckIntervalSeconds}초) 안에서 달력을 " +
                $"{ticker.CheckCount}번 봤습니다 — 게이트가 일을 하지 않습니다.");

            // ★ 음성 대조 — 주기를 채우면 실제로 다시 본다("항상 막는다"가 아니다).
            ticker.TickIfDue(CurrencyDayRolloverTicker.CheckIntervalSeconds);
            Assert.AreEqual(2, ticker.CheckCount,
                "주기를 채웠는데 달력을 안 봤습니다 — 게이트가 한 번 닫히면 영영 안 열립니다.");

            // 그러나 <b>롤오버는 안 일어난다</b> — 날이 안 바뀌었으니까.
            // 「달력을 봤다」와 「리셋했다」가 다른 사건임을 여기서 못박는다.
            Assert.AreEqual(1, ticker.RolloverCount,
                "같은 날인데 두 번째 리필이 일어났습니다 — (카) 공격 방어(T-14-3-a)가 뚫렸습니다.");
        }

        [Test]
        public void 긴_정지_뒤에도_한_번만_발화한다()
        {
            // 기계가 잠들었다 깨거나 배치 러너가 멈췄다 도는 경우. 잔여를 이월하면 이 자리에서
            // 여러 프레임 연속으로 달력을 읽는다(얻는 것은 하나도 없다).
            var ticker = new CurrencyDayRolloverTicker();
            ticker.TickIfDue(0.0);
            Assert.AreEqual(1, ticker.CheckCount, "전제.");

            double longStall = CurrencyDayRolloverTicker.CheckIntervalSeconds * 100.0;
            ticker.TickIfDue(longStall);
            Assert.AreEqual(2, ticker.CheckCount, "긴 정지 뒤 첫 틱이 달력을 안 봤습니다.");

            ticker.TickIfDue(longStall + 0.016);   // 바로 다음 프레임
            Assert.AreEqual(2, ticker.CheckCount,
                "정지 잔여가 이월돼 연속 발화했습니다 — 기산점을 「지금」으로 옮기지 않고 있습니다.");
            Assert.AreEqual(longStall, ticker.LastCheckMonotonic, 1e-9);
        }

        [Test]
        public void 손상된_시각은_게이트를_통과하지_못한다()
        {
            var ticker = new CurrencyDayRolloverTicker();

            Assert.IsFalse(ticker.TickIfDue(double.NaN));
            Assert.IsFalse(ticker.CheckNow(double.NaN));
            Assert.AreEqual(0, ticker.CheckCount,
                "NaN이 달력 읽기를 통과시켰습니다 — NaN 비교는 전부 false라 " +
                "「<」 게이트를 <b>거꾸로</b> 통과합니다. 그러면 NaN이 들어오는 동안 매 프레임 달력을 읽습니다.");

            // ★ 음성 대조 — 같은 티커가 정상값은 통과시킨다.
            Assert.IsTrue(ticker.TickIfDue(0.0), "정상값까지 막혔습니다.");
            Assert.AreEqual(1, ticker.CheckCount);

            // 시계가 뒤로 갔으면 게이트가 막는다(발화 폭주 방지).
            Assert.IsFalse(ticker.TickIfDue(-1000.0));
            Assert.AreEqual(1, ticker.CheckCount, "역행한 시각이 게이트를 통과했습니다.");
        }

        // ====================================================================
        // §3. 자정 경계 — 초 단위 (23:59:59 → 00:00:01)
        // ====================================================================

        [Test]
        public void 자정_1초_전과_1초_후는_다른_날이고_그_2초가_리필을_연다()
        {
            var justBefore = new DateTime(2026, 9, 6, 23, 59, 59, DateTimeKind.Utc);
            var justAfter = new DateTime(2026, 9, 7, 0, 0, 1, DateTimeKind.Utc);

            int dayBefore = CurrencyRules.LocalDayIndex(justBefore, 0);
            int dayAfter = CurrencyRules.LocalDayIndex(justAfter, 0);

            Assert.AreEqual(dayBefore + 1, dayAfter,
                "23:59:59 → 00:00:01 사이에서 일자가 넘어가지 않았습니다 — " +
                "하루 경계가 초 단위로 정확하지 않으면 리셋이 하루 늦거나 이릅니다.");

            // ★ 음성 대조 — 경계 <b>같은 쪽</b>의 2초는 날을 넘기지 않는다("항상 +1"이 아니다).
            Assert.AreEqual(dayBefore, CurrencyRules.LocalDayIndex(justBefore.AddSeconds(-2), 0),
                "자정 전 2초가 다른 날로 갔습니다.");
            Assert.AreEqual(dayAfter, CurrencyRules.LocalDayIndex(justAfter.AddSeconds(2), 0),
                "자정 후 2초가 다른 날로 갔습니다.");

            // 그리고 그 1자리 차이가 실제로 리필 조건을 연다 — 여는 것도, 안 여는 것도 함께 잰다.
            Assert.IsFalse(CurrencyRules.MayRefill(dayBefore, dayBefore, 0.0, double.NegativeInfinity),
                "자정 전인데 리필이 열렸습니다 — 하루에 두 번 상한이 풀립니다.");
            Assert.IsTrue(CurrencyRules.MayRefill(dayAfter, dayBefore, 0.0, double.NegativeInfinity),
                "자정을 넘겼는데 리필이 안 열렸습니다.");
        }

        [Test]
        public void 고정된_경계_오프셋이_자정의_위치를_옮긴다()
        {
            // KST(UTC+9). ★ 프로덕션 상수가 아니라 <b>시간대</b>다 — 요율·상한이 바뀌어도 안 움직인다.
            const int kstMinutes = 9 * 60;
            Assert.LessOrEqual(kstMinutes, CurrencyRules.MaxDayBoundaryOffsetMinutes,
                "전제 — 이 시간대가 허용 범위 안이어야 합니다.");

            var justBefore = new DateTime(2026, 9, 6, 14, 59, 59, DateTimeKind.Utc);   // KST 23:59:59
            var justAfter = new DateTime(2026, 9, 6, 15, 0, 1, DateTimeKind.Utc);      // KST 익일 00:00:01

            Assert.AreEqual(CurrencyRules.LocalDayIndex(justBefore, kstMinutes) + 1,
                CurrencyRules.LocalDayIndex(justAfter, kstMinutes),
                "고정 오프셋 기준 자정을 넘겼는데 일자가 안 늘었습니다 — " +
                "그러면 한국 사용자의 하루 경계가 오전 9시가 됩니다.");

            // ★ 음성 대조 — 같은 두 시각이 UTC 기준으로는 <b>같은 날</b>이다.
            //   이게 없으면 위 단언이 "오프셋 덕분"인지 "원래 그런지" 구별되지 않는다.
            Assert.AreEqual(CurrencyRules.LocalDayIndex(justBefore, 0),
                CurrencyRules.LocalDayIndex(justAfter, 0),
                "오프셋 0에서도 날이 갈렸습니다 — 위 단언이 오프셋의 성과라는 증거가 사라집니다.");
        }

        // ====================================================================
        // §4. 일일 상한이 <b>실제로 다시 열리는가</b>
        // ====================================================================

        /// <summary>오늘의 상한을 남김없이 채운다. 초를 상수에서 유도하고, 부동소수 때문에
        /// 마지막 1동전이 안 떨어질 수 있어 잔여가 0이 될 때까지만 반복한다.</summary>
        private static void FillTodayToCap()
        {
            double secondsToCap = CurrencyModel.DailyCapCoins() / CurrencyRules.IdleCoinsPerSecond;
            Assert.Less(secondsToCap, CurrencyRules.IdleWindowCapSeconds,
                "전제 — 8시간 창 안에서 상한에 닿을 수 있어야 합니다(T-D-15가 보장하는 관계).");

            for (int i = 0; i < 4 && CurrencyModel.RemainingDailyRoomCoins() > 0; i++)
            {
                CurrencyModel.TickIdleIncome(secondsToCap, true, out _);
            }
            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(), "전제 — 상한까지 채웠어야 합니다.");
        }

        [Test]
        public void 날짜가_바뀌면_일일_상한이_실제로_다시_열린다()
        {
            // 1일차 — 첫 판정이 일자와 경계 오프셋을 함께 못박는다.
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "전제 — 첫 판정에서 일자가 고정돼야 합니다.");
            int day1 = CurrencyModel.DayIndex;
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyModel.DailyCapCoins(),
                "전제 — 회복제를 안 썼으면 오늘 상한은 기본 상한입니다.");

            FillTodayToCap();
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyModel.TodayGrantedCoins);
            Assert.AreEqual(0, CurrencyModel.TickIdleIncome(60.0, true, out _),
                "상한에 걸렸는데 동전이 더 나왔습니다 — 상한이 아무 일도 안 하고 있습니다.");
            int walletAtCap = CurrencyModel.CoinBalance;
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, walletAtCap,
                "전제 — 오늘 번 것이 그대로 지갑에 있어야 합니다.");

            // 자정 통과 — 「어제까지 봤다」로 만들고 리필 최소 간격을 채운다.
            CurrencyModel.SetDayIndexForTesting(day1 - 1);
            Assert.IsTrue(CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds),
                "날짜가 바뀌고 최소 간격도 채웠는데 롤오버가 안 일어났습니다.");

            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins, "오늘 지급량이 0으로 안 돌아갔습니다.");
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyModel.RemainingDailyRoomCoins(),
                "잔여 예산이 상한 전액으로 돌아오지 않았습니다 — 화면의 「N / 1,500」이 어제 값을 물고 있습니다.");
            Assert.AreEqual(0.0, CurrencyModel.IdleWindowUsedSeconds, 1e-9, "8시간 창이 리셋되지 않았습니다.");

            // ★ <b>표시만 리셋되고 실제로는 안 나오는</b> 경우를 가른다 — 진짜로 다시 벌린다.
            Assert.Greater(CurrencyModel.TickIdleIncome(60.0, true, out _), 0,
                "새 날인데 유휴 수급이 다시 열리지 않았습니다.");
            Assert.Greater(CurrencyModel.CoinBalance, walletAtCap,
                "지급됐다는데 잔액이 안 늘었습니다.");
        }

        [Test]
        public void 회복제로_올린_상한도_다음_날_기본_상한으로_돌아온다()
        {
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "전제 — 첫 판정에서 일자가 고정돼야 합니다.");
            int day1 = CurrencyModel.DayIndex;

            for (int i = 0; i < CurrencyRules.MaxPotionsPerDay; i++)
            {
                Assert.IsTrue(CurrencyModel.TryUsePotion(), $"전제 — {i + 1}번째 회복제를 써야 합니다.");
            }
            Assert.AreEqual(CurrencyRules.HardCeilingCoins, CurrencyModel.DailyCapCoins(),
                "전제 — 회복제 최대치를 쓰면 오늘 상한이 절대 천장입니다.");
            FillTodayToCap();
            Assert.AreEqual(CurrencyRules.HardCeilingCoins, CurrencyModel.TodayGrantedCoins);

            CurrencyModel.SetDayIndexForTesting(day1 - 1);
            Assert.IsTrue(CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds));

            Assert.AreEqual(0, CurrencyModel.PotionsUsedToday,
                "무료 회복제가 부활하지 않았습니다(T-D-10) — 무료 1개는 별도 플래그가 아니라 " +
                "이 카운터의 첫 1회라서, 이게 안 돌아가면 무료분이 영영 사라집니다.");
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyModel.DailyCapCoins(),
                "오늘 상한이 어제의 회복제 확장분을 물고 있습니다 — 상한이 함수가 아니라 " +
                "저장된 값처럼 굴고 있다는 신호입니다(T-D-9).");
            Assert.AreEqual(CurrencyRules.BaseDailyCapCoins, CurrencyModel.RemainingDailyRoomCoins());
            Assert.IsTrue(CurrencyModel.TryUsePotion(), "새 날인데 회복제를 다시 못 씁니다.");
        }

        // ====================================================================
        // §5. 여러 날 건너뛴 재실행 + 채널 카운터가 <b>리셋될 준비가 돼 있는가</b>
        // ====================================================================

        /// <summary>
        /// ★ <b>사흘 동안 안 켰다가 다시 켠 사용자.</b> 이 경로는
        /// <c>Interaction/CharacterProgressionDirector.cs</c>의 <c>Start()</c>가 세이브를 읽은
        /// <b>직후</b> <see cref="CurrencyDayRolloverTicker.CheckNow"/>로 처리한다 —
        /// 주기 틱을 기다리면 그 사이에 어제 상태로 사는 창이 생긴다.
        ///
        /// <para>「재실행」을 <see cref="CurrencyModel.RestoreFromSave"/>로 흉내 내는 것이 정직한 이유:
        /// 그 함수가 세션 전용 상태(직전 리필 시각 · 활쏘기 쿨다운 · 소수분)를 −∞/0으로 되돌리는데,
        /// 그게 정확히 <b>프로세스가 새로 뜬 상태</b>다.</para>
        /// </summary>
        [Test]
        public void 사흘_만에_재실행해도_한_번의_롤오버로_따라잡는다()
        {
            const int skippedDays = 3;
            const int walletBefore = 4321;

            int todayAtStart = CurrencyRules.LocalDayIndex(DateTime.UtcNow, 0);

            // 사흘 전에 저장된 v10 파일 — 그날 예산을 남김없이 소진한 상태.
            CurrencyModel.RestoreFromSave(new CurrencySaveState
            {
                CoinBalance = walletBefore,
                SeedGranted = true,
                DayIndex = todayAtStart - skippedDays,
                TodayGrantedCoins = CurrencyRules.HardCeilingCoins,
                PotionsUsedToday = CurrencyRules.MaxPotionsPerDay,
                IdleWindowUsedSeconds = (float)CurrencyRules.IdleWindowCapSeconds,
                DayBoundaryOffsetSaved = true,
                DayBoundaryOffsetMinutes = 0,      // 위 todayAtStart와 같은 기준으로 맞춘다
                TodoCoinPaidToday = true,
                ArcheryCoinsToday = CurrencyRules.ArcheryDailyCoinLimit,
            });

            // 전제 — 로드 직후에는 아직 사흘 전 상태 그대로다(그래야 "따라잡았다"에 의미가 있다).
            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(), "전제 — 예산이 0이어야 합니다.");
            Assert.AreEqual(0, CurrencyModel.TickIdleIncome(600.0, true, out _), "전제 — 유휴 수급이 막혀 있어야 합니다.");
            Assert.AreEqual(0, CurrencyModel.TryPayTodoDailyCoins(), "전제 — [오늘 할일]이 막혀 있어야 합니다.");
            Assert.AreEqual(0, CurrencyModel.TryAwardArcheryCoins(0.0), "전제 — 활쏘기가 막혀 있어야 합니다.");
            Assert.IsTrue(CurrencyModel.ArcheryDailyLimitReached, "전제 — 활쏘기 일일 상한에 걸려 있어야 합니다.");

            // 재실행 직후 1회 — 프로세스가 방금 떴으므로 단조 시각은 작은 값이다.
            var ticker = new CurrencyDayRolloverTicker();
            Assert.IsTrue(ticker.CheckNow(2.5),
                $"{skippedDays}일 만에 켰는데 롤오버가 안 일어났습니다 — " +
                "그 사용자는 오늘 하루 종일 아무것도 못 법니다.");

            int todayAtEnd = CurrencyRules.LocalDayIndex(DateTime.UtcNow, 0);
            Assert.GreaterOrEqual(CurrencyModel.DayIndex, todayAtStart,
                "밀린 일자를 오늘까지 따라잡지 못했습니다.");
            Assert.LessOrEqual(CurrencyModel.DayIndex, todayAtEnd,
                "일자가 오늘보다 앞서 갔습니다.");

            // ★ 사흘이 「세 번」이 아니라 <b>한 번</b>으로 처리된다 — 밀린 날 수만큼 보상이 배가 되지 않는다.
            Assert.AreEqual(1, ticker.RolloverCount,
                "밀린 날 수만큼 롤오버가 반복됐습니다 — 일자는 래칫이라 한 번에 따라잡아야 합니다.");

            // 되돌아간 것 — 다섯 카운터가 <b>한 사건</b>으로 함께 0이 된다(I-15′).
            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins, "① 일일 지급량");
            Assert.AreEqual(0, CurrencyModel.PotionsUsedToday, "② 회복제");
            Assert.AreEqual(0.0, CurrencyModel.IdleWindowUsedSeconds, 1e-9, "③ 8시간 창");
            Assert.IsFalse(CurrencyModel.TodoCoinPaidToday, "④ [오늘 할일]");
            Assert.AreEqual(0, CurrencyModel.ArcheryCoinsToday, "⑤ 활쏘기");
            Assert.IsFalse(CurrencyModel.ArcheryDailyLimitReached, "활쏘기 상한 표시가 안 풀렸습니다.");

            // ★ 되돌아가면 <b>안 되는</b> 것 — 이게 없으면 "전부 0으로 미는" 구현도 위를 통과한다.
            Assert.AreEqual(walletBefore, CurrencyModel.CoinBalance,
                "롤오버가 지갑까지 비웠습니다 — 하루가 지날 때마다 번 돈이 사라집니다.");
            Assert.IsTrue(CurrencyModel.SeedGranted,
                "시드 플래그가 롤오버로 지워졌습니다 — 첫 실행 시드가 매일 다시 나갑니다(평생 1회 위반).");
            Assert.IsTrue(CurrencyModel.HasDayBoundaryOffset,
                "고정된 경계 오프셋이 풀렸습니다 — 시간대를 옮길 때마다 하루 경계가 흔들립니다(T-D-4).");

            // ★★ 「리셋될 준비가 돼 있는가」의 실물 확인 — 지급 함수가 실제로 다시 돈다.
            //    (지급 배선 자체는 이 라운드의 과제가 아니다. 모델이 다시 지급 가능한 상태인지만 잰다.)
            Assert.AreEqual(CurrencyRules.TodoDailyCoins, CurrencyModel.TryPayTodoDailyCoins(),
                "[오늘 할일] 하루 1회가 다시 열리지 않았습니다.");
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.TryAwardArcheryCoins(0.0),
                "활쏘기 상금이 다시 열리지 않았습니다.");
            Assert.Greater(CurrencyModel.TickIdleIncome(60.0, true, out _), 0,
                "유휴 수급이 다시 열리지 않았습니다.");
            Assert.Greater(CurrencyModel.CoinBalance, walletBefore, "다시 열렸다는데 지갑이 안 늘었습니다.");

            // 같은 프로세스에서 곧바로 또 굴려도 두 번째 리필은 없다.
            // ★ 여기서는 <b>일자 관문</b>이 막는다(날이 안 바뀌었다).
            Assert.IsFalse(ticker.CheckNow(3.0),
                "같은 프로세스에서 두 번째 리필이 즉시 일어났습니다.");
            Assert.AreEqual(1, ticker.RolloverCount);

            // ★★ 이제 <b>간격 관문만</b> 남겨 놓고 다시 본다 — 일자는 또 넘었는데 20시간이 안 지났다.
            //    이 두 줄이 없으면 위의 IsFalse가 「일자 관문 덕분」인지 「간격 관문 덕분」인지
            //    갈리지 않고, 간격 관문을 통째로 지워도 이 파일이 전부 초록이다(돌연변이 실측).
            CurrencyModel.SetDayIndexForTesting(CurrencyModel.DayIndex - 1);
            Assert.IsFalse(ticker.CheckNow(2.5 + CurrencyRules.MinRefillGapSeconds * 0.5),
                "일자는 넘었지만 최소 간격이 안 찼는데 리필이 일어났습니다 — " +
                "세션 안에서 날짜를 반복해 넘기는 (카) 공격이 그대로 통합니다(T-14-3-a).");

            // 음성 대조 — 간격을 채우면 통과한다("항상 거짓"이 아니다).
            Assert.IsTrue(ticker.CheckNow(2.5 + CurrencyRules.MinRefillGapSeconds),
                "일자도 넘고 최소 간격도 찼는데 리필이 안 됐습니다 — 이러면 정상 사용자가 잠깁니다.");
            Assert.AreEqual(2, ticker.RolloverCount);
        }

        /// <summary>
        /// ★ 롤오버 <b>없이는</b> 정말로 안 풀린다는 음성 대조. 이게 없으면 위 테스트들의 초록이
        /// 「롤오버 덕분」인지 「원래 그냥 풀리는지」 구별되지 않는다 —
        /// 이 저장소가 반복해 당한 형태가 정확히 그것이다.
        /// </summary>
        [Test]
        public void 음성대조_롤오버가_없으면_카운터는_영원히_잠긴_채다()
        {
            Assert.IsTrue(CurrencyModel.TickDayRollover(0.0), "전제.");
            FillTodayToCap();
            Assert.Greater(CurrencyModel.TryPayTodoDailyCoins(), 0, "전제.");
            Assert.Greater(CurrencyModel.TryAwardArcheryCoins(0.0), 0, "전제.");

            // 일자를 <b>건드리지 않고</b> 시간만 아무리 흘려도 — 즉 롤오버가 안 일어나면 —
            // 상한도 채널 카운터도 그대로다. 이것이 배선 0건 상태의 사용자가 겪던 일이다.
            for (int i = 1; i <= 5; i++)
            {
                Assert.IsFalse(CurrencyModel.TickDayRollover(CurrencyRules.MinRefillGapSeconds * i * 2.0),
                    "날이 안 바뀌었는데 롤오버가 일어났습니다.");
            }

            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(), "상한이 저절로 풀렸습니다.");
            Assert.AreEqual(0, CurrencyModel.TickIdleIncome(600.0, true, out _), "유휴 수급이 저절로 풀렸습니다.");
            Assert.AreEqual(0, CurrencyModel.TryPayTodoDailyCoins(), "[오늘 할일]이 저절로 풀렸습니다.");
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.ArcheryCoinsToday,
                "활쏘기 카운터가 저절로 0이 됐습니다.");
        }
    }
}
