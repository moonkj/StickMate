using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 집중 세션 <b>3구간 경계</b>의 값 잠금 —
    /// <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> <b>8-2절 검산표 9행</b>을 그대로 재현한다.
    /// 그 9행 재현이 P2의 «끝났다» 판정(9-3절)이다.
    ///
    /// ============================================================================
    /// 기대값이 어디서 오는가 — <b>프로덕션 함수가 아니다</b>
    /// ============================================================================
    /// 아래 <see cref="GoldenRows"/>는 <b>계약서 표에서 손으로 옮긴 골든 데이터</b>다.
    /// 경계식(<c>min(clamp(0.20D, 60, 300), 0.40D)</c>)을 이 파일에 다시 적지 않는다 —
    /// 다시 적으면 식이 바뀌어도 기대값이 함께 바뀌어 <b>조용히 초록</b>이 된다
    /// (TEAM.md 「생성기와 검사기가 같이 틀린다」 — 이 저장소가 열 번째로 당한 형태).
    /// 상수 넷도 숫자로 베끼지 않고 <c>FocusSessionPhases</c>의 것을 <b>참조</b>한다.
    ///
    /// <para>순수 산술이라 씬도 플랫폼도 필요 없다 — macOS/Windows 어느 타깃에서 돌려도 같은 답이다.</para>
    /// </summary>
    public sealed class FocusSessionPhaseBoundaryTests
    {
        private const string LogPrefix = "[집중구간]";

        /// <summary>경계 바로 앞/뒤를 찌르는 간격(초). 가장 좁은 몰입기(60초 세션의 12초)보다
        /// 훨씬 작고, 가장 큰 세션(3,600초) 근처의 float 해상도(약 0.00049초)보다 훨씬 크다.</summary>
        private const float Nudge = 0.01f;

        /// <summary>사용자 요구가 명시한 세션 길이(초) — 「0~5분 적응 / 5~20분 몰입 / 20~25분 한계」.
        /// 프로덕션 상수가 아니라 <b>요구사항 쪽 숫자</b>라 여기 적는 것이 맞다.</summary>
        private const float UserRequirementSessionSeconds = 1500f;

        private readonly struct Row
        {
            public readonly float Duration, Adapt, Immersion, Limit, Sum;
            public Row(float d, float a, float i, float l, float s)
            { Duration = d; Adapt = a; Immersion = i; Limit = l; Sum = s; }
        }

        /// <summary>계약서 8-2절 검산표 — (세션 D, 적응기, 몰입기, 한계, 검산 합).</summary>
        private static readonly Row[] GoldenRows =
        {
            new Row(  60f,  24f,   12f,  24f,   60f),   // 최소 세션. 몰입기가 0이 아니다(7-4절 요구)
            new Row( 120f,  48f,   24f,  48f,  120f),
            new Row( 150f,  60f,   30f,  60f,  150f),   // 두 갈래가 만나는 지점(연속)
            new Row( 180f,  60f,   60f,  60f,  180f),
            new Row( 300f,  60f,  180f,  60f,  300f),   // MinEdgeSeconds가 지배
            new Row( 900f, 180f,  540f, 180f,  900f),   // 20 / 60 / 20 %
            new Row(1500f, 300f,  900f, 300f, 1500f),   // ★ 사용자 요구와 정확히 일치
            new Row(3000f, 300f, 2400f, 300f, 3000f),   // MaxEdgeSeconds가 지배
            new Row(3600f, 300f, 3000f, 300f, 3600f),
        };

        /// <summary>표가 비면 아래 <c>foreach</c>가 아무것도 안 재고 초록이 된다
        /// (이 저장소 거짓 통과 #5의 형태). 개수를 기대값으로 못박는다.</summary>
        [Test]
        public void 골든표는_계약서와_같은_9행이다()
        {
            Assert.AreEqual(9, GoldenRows.Length,
                $"{LogPrefix} 계약서 8-2절 검산표는 9행이다. 행을 지웠으면 계약서도 함께 고쳐라.");
            Debug.Log($"{LogPrefix} ① 통과 — 골든 9행 적재됨.");
        }

        [Test]
        public void 검산표_9행이_그대로_재현된다()
        {
            int checkedRows = 0;
            foreach (Row r in GoldenRows)
            {
                float edge = FocusSessionPhases.EdgeSeconds(r.Duration);
                float imm = FocusSessionPhases.ImmersionSeconds(r.Duration);

                Assert.AreEqual(r.Adapt, edge, 0.001f,
                    $"{LogPrefix} D={r.Duration}초의 적응기가 계약서와 다르다(계약서 {r.Adapt}, 실제 {edge}).");
                Assert.AreEqual(r.Limit, edge, 0.001f,
                    $"{LogPrefix} D={r.Duration}초의 한계 구간이 적응기와 같아야 한다(대칭 가장자리).");
                Assert.AreEqual(r.Immersion, imm, 0.001f,
                    $"{LogPrefix} D={r.Duration}초의 몰입기가 계약서와 다르다(계약서 {r.Immersion}, 실제 {imm}).");
                Assert.AreEqual(r.Sum, edge + imm + edge, 0.001f,
                    $"{LogPrefix} D={r.Duration}초의 검산 합이 세션 길이와 다르다 — 구간이 세션을 덮지 못한다.");
                Assert.Greater(imm, 0f,
                    $"{LogPrefix} D={r.Duration}초에서 몰입기가 사라졌다 — 7-4절이 요구하는 성질이 깨졌다.");
                checkedRows++;
            }
            Assert.AreEqual(GoldenRows.Length, checkedRows, $"{LogPrefix} 일부 행이 재지 않고 지나갔다.");
            Debug.Log($"{LogPrefix} ② 통과 — 검산표 {checkedRows}행 전부 재현.");
        }

        [Test]
        public void 구간_판정이_골든_경계를_반열린_구간으로_가른다()
        {
            foreach (Row r in GoldenRows)
            {
                float d = r.Duration;
                float e = r.Adapt;                 // ★ 골든에서 가져온다(EdgeSeconds를 다시 부르지 않는다)
                float mid = r.Adapt + r.Immersion; // 한계 구간의 시작

                Assert.AreEqual(FocusSessionPhase.Adapt, FocusSessionPhases.Of(d, 0f),
                    $"{LogPrefix} D={d}: 경과 0초는 언제나 적응기다.");
                Assert.AreEqual(FocusSessionPhase.Adapt, FocusSessionPhases.Of(d, e - Nudge),
                    $"{LogPrefix} D={d}: 적응기 끝 직전이 적응기가 아니다.");
                Assert.AreEqual(FocusSessionPhase.Immersion, FocusSessionPhases.Of(d, e),
                    $"{LogPrefix} D={d}: 경계 {e}초는 몰입기에 속한다(반열린 구간 [E, D−E)).");
                Assert.AreEqual(FocusSessionPhase.Immersion, FocusSessionPhases.Of(d, mid - Nudge),
                    $"{LogPrefix} D={d}: 몰입기 끝 직전이 몰입기가 아니다.");
                Assert.AreEqual(FocusSessionPhase.Limit, FocusSessionPhases.Of(d, mid),
                    $"{LogPrefix} D={d}: 경계 {mid}초는 한계 구간에 속한다.");
                Assert.AreEqual(FocusSessionPhase.Limit, FocusSessionPhases.Of(d, d),
                    $"{LogPrefix} D={d}: 완주 시점이 한계 구간이 아니다.");
                Assert.AreEqual(FocusSessionPhase.Limit, FocusSessionPhases.Of(d, d + 5f),
                    $"{LogPrefix} D={d}: 경과가 세션 길이를 넘겨도(마지막 프레임 dt) 한계로 남아야 한다.");
            }
            Debug.Log($"{LogPrefix} ③ 통과 — 9행 × 7지점 = {GoldenRows.Length * 7}개 표본이 전부 제 구간이다.");
        }

        [Test]
        public void 세션이_아니면_구간이_없다()
        {
            Assert.AreEqual(FocusSessionPhase.None, FocusSessionPhases.Of(0f, 0f),
                $"{LogPrefix} 길이 0은 세션이 아니다.");
            Assert.AreEqual(FocusSessionPhase.None, FocusSessionPhases.Of(-1f, 0f),
                $"{LogPrefix} 음수 길이는 세션이 아니다.");
            Assert.AreEqual(FocusSessionPhase.None, FocusSessionPhases.Of(float.NaN, 0f),
                $"{LogPrefix} NaN 길이는 세션이 아니다(부정 비교로 걸러야 한다).");
            Assert.AreEqual(FocusSessionPhase.Adapt, FocusSessionPhases.Of(1500f, -3f),
                $"{LogPrefix} 음수 경과는 세션 초입(적응기)으로 읽어야 한다 — 프롭이 안 뜨는 안전한 방향.");
            Debug.Log($"{LogPrefix} ④ 통과 — 비정상 입력이 전부 안전한 쪽으로 떨어진다.");
        }

        /// <summary>
        /// 출하된 다이얼 정의역(1~60분 정수) 전체에서 성질 셋을 확인한다.
        /// <b>값이 아니라 성질</b>을 재므로 프로덕션 함수를 그대로 써도 순환이 아니다.
        /// </summary>
        [Test]
        public void 다이얼_1분부터_60분까지_연속_단조_비감소이고_몰입기가_항상_존재한다()
        {
            float prevEdge = -1f, prevImm = -1f;
            int samples = 0;
            for (int m = 1; m <= 60; m++)
            {
                float d = m * 60f;
                float e = FocusSessionPhases.EdgeSeconds(d);
                float imm = FocusSessionPhases.ImmersionSeconds(d);

                Assert.GreaterOrEqual(e, prevEdge - 0.001f,
                    $"{LogPrefix} {m}분에서 가장자리가 줄었다 — 세션을 늘렸는데 적응기가 짧아지면 안 된다.");
                Assert.GreaterOrEqual(imm, prevImm - 0.001f,
                    $"{LogPrefix} {m}분에서 몰입기가 줄었다 — 세션을 늘렸는데 몰입기가 짧아지면 안 된다.");
                Assert.Greater(imm, 0f,
                    $"{LogPrefix} {m}분에서 몰입기가 사라졌다 — 코스튬이 한 번도 안 뜨는 세션이 생긴다(7-4절).");
                Assert.AreEqual(d, e + imm + e, 0.01f, $"{LogPrefix} {m}분에서 구간 합이 세션을 못 덮는다.");
                prevEdge = e; prevImm = imm; samples++;
            }
            Assert.AreEqual(60, samples, $"{LogPrefix} 다이얼 정의역을 다 돌지 않았다.");
            Debug.Log($"{LogPrefix} ⑤ 통과 — 1~60분 60개 값에서 연속·단조·몰입기 양수.");
        }

        /// <summary>25분에서 사용자 요구와 정확히 맞는 이유가 <c>0.20 × 1500 = 300 = MaxEdgeSeconds</c>라는
        /// 사실을 못박는다. 상한을 다른 값으로 옮기면 25분 요구가 깨지므로 그때 여기서 빨개진다.</summary>
        [Test]
        public void 상한_300초는_25분_요구에서_유도된_값이다()
        {
            Assert.AreEqual(FocusSessionPhases.MaxEdgeSeconds,
                FocusSessionPhases.NominalFraction * UserRequirementSessionSeconds, 0.001f,
                $"{LogPrefix} 25분(0~5/5~20/20~25분) 요구가 상한과 어긋난다.");
            Assert.Less(FocusSessionPhases.MinEdgeSeconds, FocusSessionPhases.MaxEdgeSeconds,
                $"{LogPrefix} 하한이 상한보다 크면 Clamp가 뒤집힌다.");
            Assert.Greater(FocusSessionPhases.MaxEdgeFraction, FocusSessionPhases.NominalFraction,
                $"{LogPrefix} 비율 상한이 명목 비율보다 작으면 명목 비율이 영원히 안 쓰인다.");
            Debug.Log($"{LogPrefix} ⑥ 통과 — 상한 {FocusSessionPhases.MaxEdgeSeconds}초가 25분 요구에서 유도된다.");
        }
    }
}
