using System;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★★★ 코스튬 진화 규칙 회귀 — 단계 경계 · 일일 소프트캡 · 구간 퍼센트(내림)
    /// ============================================================================
    /// 정본: <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 7절 /
    /// <c>docs/DESIGN_SYSTEMS_COSTUME_EVOLUTION.md</c> §3-3(경계) · §5-3(캡) · §7-2·§7-3(표시).
    ///
    /// ============================================================================
    /// ★ 숫자를 어디서 가져오는가 — 이 파일이 지키는 두 규칙이 서로 밀당한다
    /// ============================================================================
    /// <list type="number">
    ///  <item>CLAUDE.md: <b>프로덕션 상수를 숫자로 베끼지 마라.</b> 베끼면 기준과 대상이 갈라져도
    ///    아무도 모른다.</item>
    ///  <item>TEAM.md §5: <b>기대값을 프로덕션 함수로 만들지 마라.</b> 그 함수가 틀어지면
    ///    기대값도 함께 틀어져 <b>아무것도 못 잰다</b>(1 ULP 사고가 그 반례였다).</item>
    /// </list>
    /// 둘을 동시에 지키는 방법은 하나뿐이다 — <b>「무엇을 재는가」에 따라 자를 바꾼다</b>:
    /// <list type="bullet">
    ///  <item><b>「임계가 옮겨졌는가」</b>는 프로덕션 상수로는 잴 수 없다(상수가 곧 대상이다).
    ///    그래서 <b>사용자가 직접 쓴 네 숫자</b>(0 · 10h · 50h · 100h)를 <see cref="BoundaryHours"/>
    ///    <b>한 자리에만</b> 적고, 그 자리를 이 파일의 <b>유일한 숫자 출처</b>로 삼는다.
    ///    <c>design-systems</c> R26 §0-2가 <i>"그 넷을 하나도 옮기지 않는다"</i>고 못박은 약속이
    ///    코드에서 사라지지 않게 하는 장치다.</item>
    ///  <item><b>그 밖 전부</b>(구간 판정 · 캡 동작 · 퍼센트)는 <b>상수를 참조</b>해서 잰다.
    ///    임계가 정당하게 바뀌는 라운드가 와도 이 테스트들은 따라 움직인다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★ 이 파일의 핵심 단언 — <b>「100.0% 표시 ≡ 마스터 도달, 어긋나는 창 0분」</b>
    /// ============================================================================
    /// 그것이 «소수 1자리 <b>내림</b>»을 고른 이유이고(§7-3), 이 기능이 화면에 대해 한 약속이다.
    /// <b>전수 확인</b>(0분 ~ 마스터 너머)으로 잰다 — 표본 몇 개로는 «어긋나는 창 0분»을 말할 수 없다.
    /// 그리고 <b>반올림 판</b>을 나란히 세워 <b>그 선택이 실제로 무는지</b>를 같은 실행에서 증명한다.
    /// </summary>
    public sealed class CostumeEvolutionRulesTests
    {
        private const string LogPrefix = "[코스튬진화]";

        // ====================================================================
        // ★ 이 파일에서 숫자를 적는 <b>유일한 자리</b> — 설계 문서와 사용자 원문에서 온다
        // ====================================================================

        /// <summary>시간→분. <b>물리 사실</b>이지 설계 상수가 아니다.</summary>
        private const int MinutesPerHour = 60;

        /// <summary>★ 사용자가 직접 쓴 임계(시간). 계약서 7-1절 표 · R26 §3-3.
        /// <b>0단계는 언제나 0분에서 시작</b>하므로 목록에 없다 — 여기 있는 것은 «진화 사건 3회»다.</summary>
        private static readonly int[] BoundaryHours = { 10, 50, 100 };

        /// <summary>★ 일일 소프트캡의 <b>유도</b>(R26 §5-3): 원형 D(가장 적극적인 정상 사용자)의
        /// 집중 200분/일 × 1.20(활쏘기 일일 한도가 이미 쓴 여유율) = 240분.
        /// <b>검산을 코드에 남긴다</b> — 240만 적어 두면 다음 사람이 «왜 240인가»를 못 되짚는다.</summary>
        private const int ArchetypeDPeakFocusMinutesPerDay = 200;

        private const int HeadroomPercent = 120;

        private readonly List<Object> _made = new List<Object>();

        /// <summary>이 테스트가 들어올 때의 일자 정수. <b>남의 값이므로 반드시 원복한다</b> —
        /// 재화 모델의 일자는 <b>전역</b>이고, 밀어 놓은 채 나가면 다음 테스트가
        /// «날이 바뀐 상태»에서 시작한다(그 증상은 그 테스트의 결함처럼 보인다).</summary>
        private int _baseDayIndex;

        [SetUp]
        public void SetUp()
        {
            _baseDayIndex = CurrencyModel.DayIndex;
        }

        /// <summary>정적 전역을 전부 원복한다.
        /// <para>★ <b><c>[TearDown]</c>은 하나만 둔다</b> — NUnit은 같은 클래스에 여럿이면
        /// <b>실행 순서를 보장하지 않는다</b>.</para></summary>
        [TearDown]
        public void TearDown()
        {
            CurrencyModel.SetDayIndexForTesting(_baseDayIndex);
            CostumeProgressModel.ResetForTesting();
            CostumeCatalog.ResetForTesting();
            for (int i = 0; i < _made.Count; i++) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // ====================================================================
        // 1. 단계 구조 — 상태 4개 · 진화 사건 3회 (S4)
        // ====================================================================

        /// <summary>
        /// 「3단계」의 뜻이 <b>상태 4개 / 사건 3회</b>로 확정된 것(계약서 7-1절 S4)을 잠근다.
        /// <para>★ <see cref="CostumeManifestSO.stageShapes"/>의 유효 범위가 이 값에서 나온다 —
        /// 어긋나면 <b>영원히 그려지지 않는 조형</b>이 에셋에 앉는다.</para>
        /// </summary>
        [Test]
        public void 상태는_넷이고_진화_사건은_셋이다()
        {
            Assert.AreEqual(BoundaryHours.Length + 1, CostumeEvolutionRules.StageCount,
                $"{LogPrefix} 상태 수가 «경계 {BoundaryHours.Length}개 + 시작 상태 1개»와 다릅니다. " +
                "사용자가 쓴 네 숫자(0 · 10h · 50h · 100h)를 하나도 안 버리는 읽기가 S4입니다.");
            Assert.AreEqual(CostumeEvolutionRules.StageCount - 1, CostumeEvolutionRules.MasterStage,
                $"{LogPrefix} 마스터 단계 번호가 마지막 상태가 아닙니다.");

            Assert.IsTrue(CostumeEvolutionRules.BoundariesAreStrictlyIncreasing(),
                $"{LogPrefix} ★ 임계표가 <b>단조 증가가 아닙니다</b>. 순서가 뒤집히면 " +
                "StageOf가 조용히 틀린 답을 냅니다 — 컴파일도 되고 화면도 뜹니다.");

            // 네 상태가 전부 <b>도달 가능</b>한가(하나라도 도달 불가면 그 조형은 죽은 데이터다).
            var reached = new HashSet<int>();
            for (int stage = 0; stage < CostumeEvolutionRules.StageCount; stage++)
            {
                reached.Add(CostumeEvolutionRules.StageOf(CostumeEvolutionRules.StageStartMinutes(stage)));
            }
            Assert.AreEqual(CostumeEvolutionRules.StageCount, reached.Count,
                $"{LogPrefix} 도달 가능한 상태가 {reached.Count}개뿐입니다 — " +
                "나머지 단계의 조형은 <b>영원히 그려지지 않습니다</b>.");
        }

        /// <summary>
        /// ★★ <b>임계가 옮겨졌는가</b>. 이 단언만은 프로덕션 상수로 잴 수 없다 — 상수가 곧 대상이다.
        /// <para>R26 §0-2: <i>"그 넷을 하나도 옮기지 않는다"</i>. 그 약속이 코드에서 사라지면
        /// <b>이미 3단계였던 사용자가 2단계로 내려앉는</b> 회귀가 아무 경보 없이 출하된다
        /// (그런 라운드는 <c>stageReached</c> high-water + 세이브 v13을 함께 들고 와야 한다 —
        /// <see cref="CostumeEvolutionRules"/> 클래스 문서의 «되살릴 조건»).</para>
        /// </summary>
        [Test]
        public void 임계는_사용자가_쓴_10시간_50시간_100시간_그대로다()
        {
            for (int stage = 1; stage < CostumeEvolutionRules.StageCount; stage++)
            {
                int expected = BoundaryHours[stage - 1] * MinutesPerHour;
                Assert.AreEqual(expected, CostumeEvolutionRules.StageStartMinutes(stage),
                    $"{LogPrefix} ★ {stage}단계 임계가 {BoundaryHours[stage - 1]}시간에서 옮겨졌습니다. " +
                    "임계를 <b>올리면</b> 이미 그 단계였던 사용자가 아래로 내려앉습니다 — " +
                    "누적은 안 줄어도 <b>단계는 파생값</b>이라 강등이 실제로 일어납니다. " +
                    "옮기기로 했다면 stageReached high-water(세이브 v13)를 같은 라운드에 넣으십시오.");
            }

            Assert.AreEqual(0, CostumeEvolutionRules.StageStartMinutes(0),
                $"{LogPrefix} 0단계가 0분에서 시작하지 않습니다.");

            // 범위 밖 단계는 0단계로 본다(화면이 «음수 단계»를 그리지 않게).
            Assert.AreEqual(0, CostumeEvolutionRules.StageStartMinutes(-1));
            Assert.AreEqual(0, CostumeEvolutionRules.StageStartMinutes(CostumeEvolutionRules.StageCount));

            Debug.Log($"{LogPrefix} 임계 확인 — " +
                      string.Join(" / ", Array.ConvertAll(BoundaryHours,
                          h => $"{h}h={h * MinutesPerHour}분")) +
                      $", 상태 {CostumeEvolutionRules.StageCount}개.");
        }

        /// <summary>
        /// 구간은 <b>반열린 <c>[시작, 끝)</c></b>이고 경계는 「이상」이다. 정확히 임계 분이면 <b>그 단계</b>다.
        /// <para>★ 숫자를 베끼지 않고 <b>상수를 참조</b>해서 경계 앞뒤 한 칸을 찍는다 —
        /// 임계가 정당하게 바뀌어도 이 단언은 따라 움직인다.</para>
        /// </summary>
        [Test]
        public void 경계는_이상이고_한_분_앞은_아직_이전_단계다()
        {
            for (int stage = 1; stage < CostumeEvolutionRules.StageCount; stage++)
            {
                int boundary = CostumeEvolutionRules.StageStartMinutes(stage);

                Assert.AreEqual(stage, CostumeEvolutionRules.StageOf(boundary),
                    $"{LogPrefix} 정확히 {boundary}분인데 {stage}단계가 아닙니다 — " +
                    "경계가 «초과»로 읽히면 마스터 표시 동치성(§7-3)이 1분씩 밀립니다.");
                Assert.AreEqual(stage - 1, CostumeEvolutionRules.StageOf(boundary - 1),
                    $"{LogPrefix} {boundary - 1}분이 벌써 {stage}단계입니다 — 구간이 반열린 형태가 아닙니다.");
            }

            Assert.AreEqual(0, CostumeEvolutionRules.StageOf(0));
            Assert.AreEqual(0, CostumeEvolutionRules.StageOf(-1),
                $"{LogPrefix} 음수 누적이 0단계로 안 떨어집니다 — 손상된 파일에서 화면이 깨집니다.");
            Assert.AreEqual(CostumeEvolutionRules.MasterStage, CostumeEvolutionRules.StageOf(int.MaxValue),
                $"{LogPrefix} 아주 큰 누적이 마스터가 아닙니다.");

            // 전 구간 단조 — 누적이 늘면 단계는 절대 내려가지 않는다(강등은 문법적으로 없다).
            int previous = 0;
            int limit = CostumeEvolutionRules.StageStartMinutes(CostumeEvolutionRules.MasterStage) + MinutesPerHour;
            for (int minutes = 0; minutes <= limit; minutes++)
            {
                int stage = CostumeEvolutionRules.StageOf(minutes);
                Assert.GreaterOrEqual(stage, previous,
                    $"{LogPrefix} {minutes}분에서 단계가 내려갔습니다({previous} → {stage}). " +
                    "누적은 줄지 않으므로 강등은 <b>문법적으로 존재하지 않아야</b> 합니다.");
                previous = stage;
            }
        }

        /// <summary>
        /// <c>NextBoundaryMinutes</c>와 <c>StageStartMinutes</c>가 <b>서로 맞물리는가</b>.
        /// <para>★ 마스터의 «다음 없음»을 <c>0</c>이나 <c>int.MaxValue</c>로 적으면 화면이
        /// «0분 남았다» 또는 «영원히 안 끝난다»를 그린다. 그래서 별도 표식이다.</para>
        /// </summary>
        [Test]
        public void 다음_경계는_다음_단계의_시작이고_마스터에는_없다()
        {
            for (int stage = 0; stage < CostumeEvolutionRules.MasterStage; stage++)
            {
                Assert.AreEqual(CostumeEvolutionRules.StageStartMinutes(stage + 1),
                    CostumeEvolutionRules.NextBoundaryMinutes(stage),
                    $"{LogPrefix} {stage}단계의 «다음 경계»가 {stage + 1}단계의 시작과 다릅니다 — " +
                    "진행 막대가 실제 승급 지점과 어긋납니다.");
            }

            Assert.AreEqual(CostumeEvolutionRules.NoNextBoundary,
                CostumeEvolutionRules.NextBoundaryMinutes(CostumeEvolutionRules.MasterStage),
                $"{LogPrefix} 마스터에 «다음 경계»가 있습니다.");
            Assert.AreNotEqual(0, CostumeEvolutionRules.NoNextBoundary,
                $"{LogPrefix} «다음 없음»이 0입니다 — 화면이 «0분 남았다»를 그립니다.");
            Assert.AreNotEqual(int.MaxValue, CostumeEvolutionRules.NoNextBoundary,
                $"{LogPrefix} «다음 없음»이 int.MaxValue입니다 — 화면이 «영원히 안 끝난다»를 그립니다.");
            Assert.Less(CostumeEvolutionRules.NoNextBoundary, 0,
                $"{LogPrefix} «다음 없음»이 음수가 아닙니다 — 실재하는 분과 구별되지 않습니다.");
        }

        // ====================================================================
        // 2. ★★ 100.0% ≡ 마스터 — 어긋나는 창 0분
        // ====================================================================

        /// <summary>
        /// ★★★ <b>이 기능이 화면에 대해 한 약속</b>: «100.0% 표시»와 «다음 경계 도달»이 정확히 동치이고,
        /// <b>어긋나는 창이 0분</b>이다.
        ///
        /// <para><b>전수 확인</b>으로 잰다(0분 ~ 마스터 + 1시간). 표본 몇 개로는 «0분»을 말할 수 없다 —
        /// 그리고 어긋나는 창은 실제로 <b>1분짜리</b>라서 표본으로는 구조적으로 못 잡는다.</para>
        ///
        /// <para>잠그는 성질 세 가지:</para>
        /// <list type="number">
        ///  <item>마스터가 <b>아닌</b> 동안 천분율은 <c>[0, 999]</c>다 — <b>1000(=100.0%)이 0건</b>이다.</item>
        ///  <item>«천분율이 «없음» 표식» ⟺ «단계가 마스터» (양방향).</item>
        ///  <item>실수판은 천분율의 <b>1/10</b>이다 — 두 함수가 각자 계산하면 그게 두 번째 이음매다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 백점영퍼센트_표시는_마스터_도달과_정확히_동치다()
        {
            int master = CostumeEvolutionRules.StageStartMinutes(CostumeEvolutionRules.MasterStage);
            int limit = master + MinutesPerHour;

            int maxTenthsBelowMaster = -1;
            var mismatches = new List<string>();

            for (int minutes = 0; minutes <= limit; minutes++)
            {
                int stage = CostumeEvolutionRules.StageOf(minutes);
                int tenths = CostumeEvolutionRules.PercentTenthsToNextBoundary(minutes);
                bool isMaster = stage == CostumeEvolutionRules.MasterStage;
                bool noPercent = tenths == CostumeEvolutionRules.NoNextBoundary;

                if (isMaster != noPercent)
                {
                    mismatches.Add($"{minutes}분(단계 {stage} / 천분율 {tenths})");
                }

                if (isMaster) continue;

                Assert.GreaterOrEqual(tenths, 0,
                    $"{LogPrefix} {minutes}분에서 천분율이 음수입니다({tenths}).");
                Assert.LessOrEqual(tenths, 999,
                    $"{LogPrefix} ★ {minutes}분(단계 {stage})에서 천분율이 {tenths}입니다 — " +
                    "<b>마스터가 아닌데 100.0%가 뜨는 화면</b>입니다. " +
                    "그 창이 존재하는 순간 «100.0% ≡ 도달»이라는 이 기능의 약속이 깨집니다.");
                if (tenths > maxTenthsBelowMaster) maxTenthsBelowMaster = tenths;

                // 실수판은 천분율의 1/10이다(두 번째 이음매 금지).
                Assert.AreEqual(tenths / 10f, CostumeEvolutionRules.PercentToNextBoundary(minutes), 1e-4f,
                    $"{LogPrefix} {minutes}분에서 실수판과 천분율판이 갈렸습니다.");
            }

            Assert.IsEmpty(mismatches,
                $"{LogPrefix} ★ «퍼센트 없음»과 «마스터»가 어긋나는 분이 {mismatches.Count}칸 있습니다: " +
                string.Join(", ", mismatches) + "\n" +
                "이 둘이 어긋나는 창은 <b>0분</b>이어야 합니다 — 마스터는 퍼센트를 그리지 않고 " +
                "누적 시간만 남깁니다(§7-2).");
            Assert.AreEqual(999, maxTenthsBelowMaster,
                $"{LogPrefix} 마스터 아래 최대 천분율이 {maxTenthsBelowMaster}입니다(기대 999). " +
                "999에 <b>닿지 못하면</b> 진행 막대가 끝까지 안 차는 것이고, " +
                "1000에 <b>닿으면</b> 위 단언이 이미 빨개졌어야 합니다.");

            Assert.AreEqual(CostumeEvolutionRules.NoNextBoundaryPercent,
                CostumeEvolutionRules.PercentToNextBoundary(master), 1e-6f,
                $"{LogPrefix} 마스터의 실수판이 «없음» 표식이 아닙니다.");
            Assert.Less(CostumeEvolutionRules.NoNextBoundaryPercent, 0f,
                $"{LogPrefix} «퍼센트 없음»이 음수가 아닙니다 — 0.0%는 실재하는 값이라 구별돼야 합니다.");

            Debug.Log($"{LogPrefix} 전수 {limit + 1}칸 — 마스터 아래 천분율 최댓값 {maxTenthsBelowMaster}, " +
                      "1000 도달 0건, «퍼센트 없음 ⟺ 마스터» 어긋남 0건.");
        }

        /// <summary>
        /// ★★ <b>내림이라는 선택이 실제로 무는가</b>(음성 대조). 같은 자리에 <b>반올림</b>을 넣으면
        /// «마스터가 아닌데 100.0%»가 실제로 나오는지 세어 본다 — 안 나오면 §7-3의 근거가 사라지고,
        /// 그러면 위 초록은 «내림이라서»가 아니라 <b>«어떤 방향이어도 통과»</b>라는 뜻이다.
        ///
        /// <para>★ <b>두 자로 같은 수를 잰다</b>: (가) 전수 스윕으로 실제로 세고,
        /// (나) 닫힌 식 <c>ceil(1999·span / 2000)</c>으로 따로 구해 대조한다.
        /// 한쪽이 눈이 멀면 두 수가 갈라진다(생성기와 검사기가 같이 틀리는 형태를 피한다).</para>
        ///
        /// <para>★ 반올림은 <c>Math.Round</c>가 아니라 <b><c>floor(x + 0.5)</c></b>로 정의한다 —
        /// .NET <c>Math.Round</c>는 기본이 <b>은행가 반올림</b>이라 정확히 .5에서 짝수로 붙고,
        /// 그 미묘함이 이 대조의 요점을 흐린다(표시 규약이 뜻하는 것은 «반올림 올림»이다).</para>
        /// </summary>
        [Test]
        public void 음성대조_반올림판은_마스터가_아닌데_백점영퍼센트를_만든다()
        {
            int master = CostumeEvolutionRules.StageStartMinutes(CostumeEvolutionRules.MasterStage);

            // (가) 전수 스윕 — 반올림판이 1000을 내는 분을 실제로 모은다.
            var swept = new List<int>();
            for (int minutes = 0; minutes < master; minutes++)
            {
                int stage = CostumeEvolutionRules.StageOf(minutes);
                int start = CostumeEvolutionRules.StageStartMinutes(stage);
                int span = CostumeEvolutionRules.NextBoundaryMinutes(stage) - start;
                int done = minutes - start;

                int rounded = (int)Math.Floor(done * 1000.0 / span + 0.5);
                if (rounded >= 1000) swept.Add(minutes);
            }

            // (나) 닫힌 식 — 같은 수를 다른 방법으로 구한다.
            int closedForm = 0;
            for (int stage = 0; stage < CostumeEvolutionRules.MasterStage; stage++)
            {
                long span = CostumeEvolutionRules.NextBoundaryMinutes(stage)
                            - CostumeEvolutionRules.StageStartMinutes(stage);
                long doneMin = (1999L * span + 1999L) / 2000L;      // ceil(1999·span / 2000)
                long count = span - doneMin;                        // done ∈ [doneMin, span-1]
                if (count > 0) closedForm += (int)count;
            }

            Assert.AreEqual(closedForm, swept.Count,
                $"{LogPrefix} 두 자가 다른 수를 냈습니다(스윕 {swept.Count} / 닫힌 식 {closedForm}). " +
                "한쪽이 눈이 멀었으므로 이 테스트의 모든 수를 폐기하십시오.");

            Assert.GreaterOrEqual(swept.Count, 1,
                $"{LogPrefix} ★ 반올림판이 «마스터가 아닌 100.0%»를 <b>한 번도 안 만들었습니다</b>. " +
                "그러면 §7-3의 근거(내림이라야 어긋나는 창이 0분이다)가 사라지고, " +
                "위 [백점영퍼센트_표시는_…]의 초록도 «내림이라서»가 아니라 " +
                "«어떤 방향이어도 통과»라는 뜻이 됩니다.");

            // 그리고 그 분들에서 <b>프로덕션(내림)은 999</b>다 — 같은 입력, 다른 답.
            foreach (int minutes in swept)
            {
                Assert.AreEqual(999, CostumeEvolutionRules.PercentTenthsToNextBoundary(minutes),
                    $"{LogPrefix} {minutes}분에서 내림판이 999가 아닙니다 — " +
                    "반올림판이 100.0%를 내는 바로 그 분입니다.");
                Assert.AreNotEqual(CostumeEvolutionRules.MasterStage, CostumeEvolutionRules.StageOf(minutes),
                    $"{LogPrefix} {minutes}분이 이미 마스터입니다 — 이 대조의 전제가 깨졌습니다.");
            }

            Debug.Log($"{LogPrefix} 음성 대조 — 반올림판의 거짓 100.0%는 {swept.Count}칸" +
                      (swept.Count > 0 ? $"({string.Join(", ", swept)}분)" : "") +
                      $", 닫힌 식과 일치. 같은 분에서 내림판은 전부 999.");
        }

        // ====================================================================
        // 3. 일일 소프트캡 — 값의 유도와 동작
        // ====================================================================

        /// <summary>
        /// 캡 값의 <b>유도</b>를 코드에 남긴다(R26 §5-3): 원형 D 집중 200분/일 × 1.20 = 240분.
        /// <para>★ 240만 적어 두면 다음 사람이 «왜 240인가»를 못 되짚고, 그러면 조정 라운드가
        /// <b>근거 없이</b> 숫자를 옮긴다. 그리고 이 값은 <b>정상 사용자에게 닿지 않아야</b> 한다 —
        /// 닿으면 그건 캡이 아니라 벌이다.</para>
        /// </summary>
        [Test]
        public void 일일_소프트캡은_원형D_집중시간에_여유율을_곱한_값이다()
        {
            int derived = ArchetypeDPeakFocusMinutesPerDay * HeadroomPercent / 100;
            Assert.AreEqual(derived, CostumeEvolutionRules.DailySoftCapMinutes,
                $"{LogPrefix} 일일 소프트캡이 유도값과 다릅니다 " +
                $"({ArchetypeDPeakFocusMinutesPerDay}분 × {HeadroomPercent}% = {derived}). " +
                "옮기기로 했다면 R26 §5-3의 유도부터 고치십시오 — 숫자만 옮기면 근거가 사라집니다.");
            Assert.Greater(CostumeEvolutionRules.DailySoftCapMinutes, ArchetypeDPeakFocusMinutesPerDay,
                $"{LogPrefix} 캡이 가장 적극적인 정상 사용자의 집중 시간 이하입니다 — " +
                "그건 캡이 아니라 <b>벌</b>입니다.");

            // 하루 최대치로도 마스터까지 여러 날이 걸려야 «마지막 목표»가 남는다.
            int master = CostumeEvolutionRules.StageStartMinutes(CostumeEvolutionRules.MasterStage);
            int fastestDays = (master + CostumeEvolutionRules.DailySoftCapMinutes - 1)
                              / CostumeEvolutionRules.DailySoftCapMinutes;
            Assert.Greater(fastestDays, 7,
                $"{LogPrefix} 캡을 매일 채우면 {fastestDays}일 만에 마스터입니다 — " +
                "진화는 <b>되돌릴 수 없는 시각적 보상</b>이라 며칠 만에 끝나면 «마지막 목표»가 사라집니다.");

            Debug.Log($"{LogPrefix} 캡 {CostumeEvolutionRules.DailySoftCapMinutes}분 " +
                      $"= {ArchetypeDPeakFocusMinutesPerDay} × {HeadroomPercent}% / " +
                      $"캡 상시 소진 시 마스터까지 최소 {fastestDays}일.");
        }

        [Test]
        public void 남은_분은_음수가_되지_않고_손상된_값도_삼킨다()
        {
            int cap = CostumeEvolutionRules.DailySoftCapMinutes;

            Assert.AreEqual(cap, CostumeEvolutionRules.RemainingDailyMinutes(0));
            Assert.AreEqual(1, CostumeEvolutionRules.RemainingDailyMinutes(cap - 1));
            Assert.AreEqual(0, CostumeEvolutionRules.RemainingDailyMinutes(cap));
            Assert.AreEqual(0, CostumeEvolutionRules.RemainingDailyMinutes(cap * 10),
                $"{LogPrefix} 캡을 넘겨 저장된 파일에서 <b>음수</b>가 나왔습니다 — " +
                "그 음수는 곧 «오늘 −N분 실을 수 있다»가 되어 적립을 뒤집습니다.");
            Assert.AreEqual(cap, CostumeEvolutionRules.RemainingDailyMinutes(-50),
                $"{LogPrefix} 음수 오늘분이 «캡 전부 남음»으로 안 떨어집니다.");
            Assert.AreEqual(0, CostumeEvolutionRules.RemainingDailyMinutes(int.MaxValue));
        }

        /// <summary>
        /// ★ 캡에 <b>걸치는</b> 적립은 «전부 거부»가 아니라 <b>남은 만큼만</b> 실린다.
        /// <para><c>EquipmentMigrationTests</c>는 «캡에 도달한 뒤 0분»(전부 거부)까지만 덮는다.
        /// <b>그 사이 창</b>(남은 방 &lt; 요청 분)이 이 저장소에서 안 재어진 자리였고,
        /// 거기서 잘못 짜면 25분 완주 하나가 통째로 사라진다 — 화면에 아무 흔적이 안 남는다.</para>
        /// <para>★ 그리고 <b>실제로 더해진 분</b>을 돌려주는 것이 계약이다(승급 판정이 그 값에 걸린다).</para>
        /// </summary>
        [Test]
        public void 캡에_걸치는_적립은_남은_만큼만_실린다()
        {
            const string key = "costumefixture.capcos";
            InstallCostume(key);

            int cap = CostumeEvolutionRules.DailySoftCapMinutes;
            int room = 10;

            Assert.AreEqual(cap - room, CostumeProgressModel.AddFocusMinutes(key, cap - room),
                $"{LogPrefix} 전제 — 캡 아래 적립이 통째로 실려야 합니다.");
            Assert.AreEqual(room, CostumeProgressModel.RemainingMinutesToday,
                $"{LogPrefix} 전제 — 남은 방이 {room}분이어야 합니다.");

            int requested = room * 3;
            int added = CostumeProgressModel.AddFocusMinutes(key, requested);

            Assert.AreEqual(room, added,
                $"{LogPrefix} ★ 캡에 걸친 적립이 {added}분 실렸습니다(남은 방 {room}, 요청 {requested}). " +
                $"0이면 <b>세션 하나가 통째로 사라진 것</b>이고, {requested}면 캡이 무의미합니다 — " +
                "두 오류 다 화면에 흔적이 없습니다.");
            Assert.AreEqual(cap, CostumeProgressModel.MinutesToday,
                $"{LogPrefix} 오늘분이 캡을 넘거나 못 미칩니다.");
            Assert.AreEqual(0, CostumeProgressModel.RemainingMinutesToday);
            Assert.AreEqual(cap, CostumeProgressModel.MinutesOf(key),
                $"{LogPrefix} 코스튬 누적과 오늘분이 갈렸습니다.");

            Assert.AreEqual(0, CostumeProgressModel.AddFocusMinutes(key, 30),
                $"{LogPrefix} 캡에 도달한 뒤에도 적립이 실렸습니다.");
            Assert.AreEqual(cap, CostumeProgressModel.MinutesOf(key));

            Debug.Log($"{LogPrefix} 캡 걸침 — 남은 방 {room}분에 {requested}분 요청 → {added}분 적립, " +
                      $"오늘 {CostumeProgressModel.MinutesToday}/{cap}분.");
        }

        /// <summary>
        /// 단계는 <b>저장되지 않는다</b> — 누적 분에서 <b>파생</b>한다(계약서 3-1절, high-water 필드 없음).
        ///
        /// <para>★ 이 성질을 <b>스키마로</b> 잰다: 세이브 레코드의 public 필드가 <b>키와 분 둘뿐</b>인가.
        /// 「단계 필드가 없다」는 부재 단언이라 그냥 두면 조용히 썩으므로,
        /// <b>있는 두 필드를 함께 존재 단언</b>해 스캐너가 살아 있음을 같은 자리에서 증명한다.
        /// 이름은 <c>nameof</c>로 잡는다 — 문자열로 베끼면 개명 한 번에 죽는다.</para>
        /// </summary>
        [Test]
        public void 세이브_레코드에는_단계_필드가_없다()
        {
            var record = new CostumeFocusRecord();
            var fields = new List<string>();
            foreach (System.Reflection.FieldInfo f in record.GetType()
                         .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                fields.Add(f.Name);
            }

            // 존재 단언 — 스캐너가 실제로 필드를 본다.
            Assert.Contains(nameof(CostumeFocusRecord.costumeKey), fields,
                $"{LogPrefix} 레코드에서 키 필드를 못 찾았습니다 — " +
                "그러면 아래 «단계 필드 없음»은 «없다»가 아니라 «못 본다»입니다.");
            Assert.Contains(nameof(CostumeFocusRecord.focusMinutes), fields,
                $"{LogPrefix} 레코드에서 누적 분 필드를 못 찾았습니다.");

            // 부재 단언 — 그 둘 말고는 없다.
            Assert.AreEqual(2, fields.Count,
                $"{LogPrefix} ★ 세이브 레코드 필드가 {fields.Count}개입니다" +
                $"({string.Join(", ", fields)}). 단계는 누적에서 <b>파생</b>합니다 — " +
                "high-water 필드가 필요했던 이유(스탯은 로드아웃에 따라 내려간다)가 여기엔 없고, " +
                "필드를 더하면 <b>디스크 형태가 굳어</b> 되돌리려면 세이브 v13 + 하위 호환 1벌이 따라옵니다.");
        }

        /// <summary>
        /// ★ 일일 소프트캡은 승급을 <b>미루기만</b> 하고 <b>없애지는 않는다</b>.
        /// <para>날이 바뀌면 오늘분만 0으로 돌아가고 <b>코스튬 누적은 그대로</b> 살아 있어야 한다.
        /// 여기가 어긋나면 증상은 <i>"매일 열심히 하는데 단계가 안 오른다"</i>이고
        /// <b>화면에도 로그에도 아무 흔적이 안 남는다</b>.</para>
        /// <para>★ 날짜는 <b>실제 경로</b>로 넘긴다 — 이 모델은 벽시계를 안 읽고
        /// 재화 모델의 <b>일자 정수</b>가 바뀌었는지만 본다. 그 정수를 밀어 롤오버를 만든다.</para>
        /// </summary>
        [Test]
        public void 캡은_승급을_미루기만_하고_없애지_않는다()
        {
            const string key = "costumefixture.stagecos";
            InstallCostume(key);

            int boundary = CostumeEvolutionRules.StageStartMinutes(1);
            int cap = CostumeEvolutionRules.DailySoftCapMinutes;

            Assert.AreEqual(0, CostumeEvolutionRules.StageOf(CostumeProgressModel.MinutesOf(key)),
                $"{LogPrefix} 기록이 없는 코스튬이 0단계가 아닙니다.");

            int day = 0;
            while (CostumeProgressModel.MinutesOf(key) < boundary)
            {
                Assert.Less(day, 1 + boundary / cap + 1,
                    $"{LogPrefix} {day}일이 지나도 임계에 못 닿습니다 — 캡이 승급을 «없앤» 상태입니다.");

                int want = Math.Min(cap, boundary - CostumeProgressModel.MinutesOf(key));
                Assert.AreEqual(want, CostumeProgressModel.AddFocusMinutes(key, want),
                    $"{LogPrefix} {day}일차 적립({want}분)이 요청과 다릅니다 — " +
                    "하루 예산 안인데 실리지 않았습니다.");

                day++;
                CurrencyModel.SetDayIndexForTesting(_baseDayIndex + day);   // ★ 날이 바뀐다
                Assert.AreEqual(0, CostumeProgressModel.MinutesToday,
                    $"{LogPrefix} 일자가 넘어갔는데 오늘분이 0으로 안 돌아갔습니다 — " +
                    "그러면 캡이 <b>영원히</b> 걸린 채로 남습니다.");
            }

            Assert.AreEqual(boundary, CostumeProgressModel.MinutesOf(key),
                $"{LogPrefix} 누적이 임계에 정확히 안 닿았습니다 — 롤오버가 누적까지 지웠을 수 있습니다.");
            Assert.AreEqual(1, CostumeEvolutionRules.StageOf(CostumeProgressModel.MinutesOf(key)),
                $"{LogPrefix} 누적이 임계에 닿았는데 1단계가 아닙니다.");

            Debug.Log($"{LogPrefix} 캡 {cap}분/일로 {day}일 만에 {boundary}분 → " +
                      $"{CostumeEvolutionRules.StageOf(boundary)}단계(단계 필드 없이 파생).");
        }

        // ====================================================================
        // 픽스처
        // ====================================================================

        /// <summary>합성 코스튬 1개를 카탈로그에 싣는다.
        /// <para>★ <b>양성 대조를 안에 넣는다</b>: 실제로 실렸는지 먼저 확인하지 않으면
        /// 아래 적립 단언들이 «카탈로그가 몰라서 0» 상태를 통과로 읽는다
        /// (<c>AddFocusMinutes</c>는 모르는 키에 0을 돌려준다 — 그건 «캡에 걸렸다»와 값이 같다).</para></summary>
        private void InstallCostume(string key)
        {
            var m = ScriptableObject.CreateInstance<CostumeManifestSO>();
            _made.Add(m);
            m.name = key;
            m.costumeKey = key;
            m.requiresSchemaVersion = CostumeManifestSO.SchemaVersion;
            m.sourceKind = CostumeSourceKind.BaseTheme;
            m.sourceId = ItemCatalog.ThemeOffice;
            m.displayNameKey = key + ".name";

            var faults = new List<string>();
            CostumeCatalog.UseForTesting(new[] { m }, faults);
            Assert.IsEmpty(faults,
                $"{LogPrefix} 픽스처 코스튬이 거부됐습니다 — 아래 적립 단언이 " +
                "«카탈로그가 비어서» 0을 통과로 읽게 됩니다:\n  · " + string.Join("\n  · ", faults));
            Assert.IsNotNull(CostumeCatalog.Find(key),
                $"{LogPrefix} 픽스처 코스튬을 카탈로그가 못 찾습니다(양성 대조 실패).");

            CostumeProgressModel.ResetForTesting();

            // 오늘분을 «오늘»에 맞춰 둔다 — 첫 적립이 롤오버로 오해되지 않게.
            Assert.AreEqual(0, CostumeProgressModel.MinutesToday,
                $"{LogPrefix} 초기화 직후 오늘분이 0이 아닙니다.");
        }
    }
}
