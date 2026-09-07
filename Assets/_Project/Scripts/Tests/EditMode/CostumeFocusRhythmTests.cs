using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 코스튬 LFVS <b>박자</b>의 값 잠금 — <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 3-3절·5-2절의
    /// <b>검산표 3개</b>를 그대로 재현하고, 스텝레이트 약수 조건(V4)과 정수 루프(V8)를 못박는다.
    ///
    /// ============================================================================
    /// 기대값의 출처 — <b>프로덕션 식이 아니다</b>
    /// ============================================================================
    /// 아래 골든 행은 <b>설계 문서의 표에서 손으로 옮긴 것</b>이다. 듀티식
    /// (<c>루프수 = round(목표듀티 × 주기 / 루프길이)</c>)을 이 파일에 다시 적지 않는다 —
    /// 다시 적으면 식이 바뀌어도 기대값이 함께 바뀌어 <b>조용히 초록</b>이 된다.
    ///
    /// <para>★ <b>이 표가 잡아낸 실제 함정 하나</b>: 최단 세션 절정 소구간의 루프 수 계산이 정확히
    /// <c>2.5</c>인데, <c>Mathf.RoundToInt</c>(짝수 반올림)는 <b>2</b>를, 설계 표는 <b>3</b>을 적는다.
    /// 프로덕션이 «0.5에서 올림»을 쓰지 않으면 그 행에서 빨개진다.</para>
    /// </summary>
    public sealed class CostumeFocusRhythmTests
    {
        private const string LogPrefix = "[코스튬박자]";

        /// <summary>골든 한 행 — (세션 D, 키 수, 소구간, 스텝레이트, 루프 수, W, 실효듀티%).</summary>
        private readonly struct Row
        {
            public readonly float Duration;
            public readonly int Keys, SubPhase, Fps, Loops;
            public readonly float Work, DutyPercent;
            public Row(float d, int keys, int sub, int fps, int loops, float w, float duty)
            { Duration = d; Keys = keys; SubPhase = sub; Fps = fps; Loops = loops; Work = w; DutyPercent = duty; }
        }

        /// <summary>설계 문서 3-3절(25분 4키 · 60초 4키)과 5-2절(25분 3키 오피스)의 표 전부.</summary>
        private static readonly Row[] GoldenRows =
        {
            // 25분 세션 · 4키 (3-3절 본표)
            new Row(1500f, 4, 0, 3, 2, 2.6667f, 33.3f),
            new Row(1500f, 4, 1, 5, 5, 4.0000f, 50.0f),
            new Row(1500f, 4, 2, 3, 1, 1.3333f, 16.7f),
            // 60초 세션(최단) · 4키 (3-3절 최단표) — ★ 절정 행이 짝수 반올림을 잡아낸다
            new Row(  60f, 4, 0, 3, 1, 1.3333f, 33.3f),
            new Row(  60f, 4, 1, 5, 3, 2.4000f, 60.0f),
            new Row(  60f, 4, 2, 3, 1, 1.3333f, 33.3f),
            // 25분 세션 · 3키 오피스 (5-2절) — 키 수가 다르면 표가 통째로 달라진다
            new Row(1500f, 3, 0, 3, 3, 3.0000f, 37.5f),
            new Row(1500f, 3, 1, 5, 7, 4.2000f, 52.5f),
            new Row(1500f, 3, 2, 3, 1, 1.0000f, 12.5f),
        };

        /// <summary>표가 비면 아래 <c>foreach</c>가 아무것도 안 재고 초록이 된다(거짓 통과 #5).</summary>
        [Test]
        public void 골든표는_설계문서와_같은_9행이다()
        {
            Assert.AreEqual(9, GoldenRows.Length, $"{LogPrefix} 3-3절 6행 + 5-2절 3행 = 9행이다.");
            Debug.Log($"{LogPrefix} ① 통과 — 골든 9행 적재.");
        }

        [Test]
        public void 듀티_사이클_검산표가_그대로_재현된다()
        {
            int checkedRows = 0;
            foreach (Row r in GoldenRows)
            {
                float immersion = FocusSessionPhases.ImmersionSeconds(r.Duration);
                float period = CostumeFocusRhythm.PeriodSeconds(immersion, CostumeFocusRhythm.MaxPeriodSeconds);
                float loopSeconds = r.Keys / (float)r.Fps;
                float duty = CostumeFocusRhythm.DutyOf(r.SubPhase);

                Assert.AreEqual(r.Fps,
                    CostumeFocusRhythm.StepsPerSecondOf(r.SubPhase, CostumeFocusRhythm.RestingStepsPerSecond),
                    $"{LogPrefix} D={r.Duration} 소구간 {r.SubPhase}의 스텝레이트가 설계와 다르다.");
                Assert.AreEqual(r.Loops, CostumeFocusRhythm.LoopCount(period, loopSeconds, duty),
                    $"{LogPrefix} D={r.Duration}·{r.Keys}키·소구간 {r.SubPhase}의 루프 수가 설계와 다르다 " +
                    "(0.5에서 올림이 아니라 짝수 반올림을 쓰면 60초 절정 행에서 여기가 걸린다).");

                float work = CostumeFocusRhythm.WorkSeconds(period, loopSeconds, duty);
                Assert.AreEqual(r.Work, work, 0.001f,
                    $"{LogPrefix} D={r.Duration}·{r.Keys}키·소구간 {r.SubPhase}의 작업 구간 W가 설계와 다르다.");
                Assert.AreEqual(r.DutyPercent, 100f * work / period, 0.1f,
                    $"{LogPrefix} D={r.Duration}·{r.Keys}키·소구간 {r.SubPhase}의 실효 듀티가 설계와 다르다.");
                checkedRows++;
            }
            Assert.AreEqual(GoldenRows.Length, checkedRows, $"{LogPrefix} 일부 행이 재지 않고 지나갔다.");
            Debug.Log($"{LogPrefix} ② 통과 — 검산표 {checkedRows}행 전부 재현.");
        }

        /// <summary>V8 — W는 언제나 <b>정수 루프</b>여야 한다. 반쯤 진행된 동작이 정지 구간에 얼어 있으면
        /// 결함으로 읽힌다. 다이얼 전 구간 × 키 수 3·4 × 소구간 3에서 확인한다.</summary>
        [Test]
        public void 작업_구간은_언제나_정수_루프다()
        {
            int samples = 0;
            for (int minutes = 1; minutes <= 60; minutes++)
            {
                float immersion = FocusSessionPhases.ImmersionSeconds(minutes * 60f);
                float period = CostumeFocusRhythm.PeriodSeconds(immersion, CostumeFocusRhythm.MaxPeriodSeconds);
                for (int keys = CostumeKeyposeTableSO.MinKeyposeCount; keys <= CostumeKeyposeTableSO.MaxKeyposeCount; keys++)
                {
                    for (int sub = 0; sub < CostumeFocusRhythm.SubPhaseCount; sub++)
                    {
                        int fps = CostumeFocusRhythm.StepsPerSecondOf(sub, CostumeFocusRhythm.RestingStepsPerSecond);
                        float loopSeconds = keys / (float)fps;
                        float duty = CostumeFocusRhythm.DutyOf(sub);
                        int loops = CostumeFocusRhythm.LoopCount(period, loopSeconds, duty);
                        float work = CostumeFocusRhythm.WorkSeconds(period, loopSeconds, duty);

                        Assert.GreaterOrEqual(loops, 1,
                            $"{LogPrefix} {minutes}분·{keys}키·소구간 {sub}: 루프가 0이면 동작이 통째로 사라진다.");
                        Assert.LessOrEqual(work, period + 0.001f,
                            $"{LogPrefix} {minutes}분·{keys}키·소구간 {sub}: 작업 구간이 주기를 넘었다(정지 구간이 음수).");
                        Assert.AreEqual(loops * loopSeconds, work, 0.001f,
                            $"{LogPrefix} {minutes}분·{keys}키·소구간 {sub}: W가 정수 루프가 아니다 — " +
                            "반쯤 진행된 동작이 정지 구간에 얼어붙는다.");
                        samples++;
                    }
                }
            }
            Assert.AreEqual(60 * 7 * 3, samples, $"{LogPrefix} 표본 수가 기대와 다르다 — 루프가 덜 돌았다.");
            Debug.Log($"{LogPrefix} ③ 통과 — {samples}개 조합 전부 정수 루프.");
        }

        /// <summary>
        /// V4 — 스텝레이트는 <b>15의 약수</b>여야 한다. ★ <b>음성 대조를 같은 테스트 안에서 돌린다</b>:
        /// 4fps가 <b>반드시 거부</b>되는 것을 보이지 않으면 「통과」가 「검사가 아무것도 안 한다」와
        /// 구별되지 않는다.
        /// </summary>
        [Test]
        public void 스텝레이트는_15의_약수_둘만_합법이다()
        {
            for (int rate = 1; rate <= 16; rate++)
            {
                bool legal = CostumeKeyposeTableSO.IsLegalStepRate(rate);
                bool expected = rate == CostumeFocusRhythm.RestingStepsPerSecond
                             || rate == CostumeFocusRhythm.PeakStepsPerSecond;
                Assert.AreEqual(expected, legal,
                    $"{LogPrefix} {rate}fps의 합법 여부가 기대와 다르다. 합법은 " +
                    $"{CostumeFocusRhythm.RestingStepsPerSecond}·{CostumeFocusRhythm.PeakStepsPerSecond}뿐이다.");

                // 합법 판정의 근거를 산술로 되짚는다 — 상수를 베끼지 않고 성질을 잰다.
                if (legal) Assert.AreEqual(0, 15 % rate,
                    $"{LogPrefix} {rate}fps를 합법이라 했는데 15의 약수가 아니다.");
            }
            Assert.IsFalse(CostumeKeyposeTableSO.IsLegalStepRate(4),
                $"{LogPrefix} ★ 음성 대조 — 4fps는 15 ÷ 4 = 3.75라 스텝마다 제출이 3장·4장으로 갈리고 " +
                "그 지연이 0·3·2·1로 순환한다. 반드시 거부돼야 한다.");
            Debug.Log($"{LogPrefix} ④ 통과 — 1~16fps 전수 + 4fps 음성 대조.");
        }

        /// <summary>스텝 번호는 <b>벽시계</b>에서 나오고 루프를 정확히 순환한다.</summary>
        [Test]
        public void 스텝_번호가_벽시계에서_순환한다()
        {
            const int keys = 4;
            const int fps = 3;
            for (int step = 0; step < keys * 3; step++)
            {
                float t = (step + 0.5f) / fps;   // 그 스텝의 한가운데
                Assert.AreEqual(step % keys, Mathf.FloorToInt(t * fps) % keys,
                    $"{LogPrefix} 경과 {t:F3}초에서 스텝 번호가 어긋났다.");
            }
            Debug.Log($"{LogPrefix} ⑤ 통과 — 스텝 번호 순환.");
        }

        /// <summary>단계 창 — 비면 표 전체, 적으면 다음 항목까지가 그 단계의 구간.</summary>
        [Test]
        public void 단계_창이_다음_시작까지를_구간으로_잡는다()
        {
            var table = ScriptableObject.CreateInstance<CostumeKeyposeTableSO>();
            try
            {
                table.stepsPerSecond = CostumeFocusRhythm.RestingStepsPerSecond;
                table.keyposes = new CostumeKeypose[6];

                table.stageKeyposeStart = null;
                table.StageWindow(0, out int start, out int count);
                Assert.AreEqual(0, start, $"{LogPrefix} 창이 비면 표 전체여야 한다.");
                Assert.AreEqual(6, count, $"{LogPrefix} 창이 비면 표 전체여야 한다.");

                table.stageKeyposeStart = new[] { 0, 2, 5 };
                table.StageWindow(0, out start, out count);
                Assert.AreEqual(0, start); Assert.AreEqual(2, count, $"{LogPrefix} 0단계는 [0,2)다.");
                table.StageWindow(1, out start, out count);
                Assert.AreEqual(2, start); Assert.AreEqual(3, count, $"{LogPrefix} 1단계는 [2,5)다.");
                table.StageWindow(2, out start, out count);
                Assert.AreEqual(5, start); Assert.AreEqual(1, count, $"{LogPrefix} 마지막 단계는 배열 끝까지다.");
                table.StageWindow(3, out start, out count);
                Assert.AreEqual(0, start); Assert.AreEqual(6, count,
                    $"{LogPrefix} 적지 않은 단계는 표 전체를 쓴다(그 자체가 정상 상태다).");

                Assert.IsTrue(table.IsUsable(out string error), $"{LogPrefix} 정상 표가 거부됐다: {error}");

                table.stepsPerSecond = 4;
                Assert.IsFalse(table.IsUsable(out error),
                    $"{LogPrefix} ★ 음성 대조 — 4fps 표는 실리면 안 된다.");
                Assert.IsNotNull(error, $"{LogPrefix} 거부하면서 사유를 안 남기면 만든 사람이 못 고친다.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
            Debug.Log($"{LogPrefix} ⑥ 통과 — 단계 창 + 표 검증.");
        }
    }
}
