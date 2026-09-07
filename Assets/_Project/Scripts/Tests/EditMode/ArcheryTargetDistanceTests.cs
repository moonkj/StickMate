using System;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 활쏘기 과녁 사거리 회귀 테스트 (사용자 신고 2026-08-31 대응)
    ///
    /// 신고 원문: "활쏘기 시키면 무조건 과녁이 화면 끝에만 생김 적당히 먼거리만 되도 되는데 물론
    /// 거리는 항상 랜덤으로 변경되어야하지만".
    ///
    /// 원인: <see cref="ArcheryDirector"/>의 배치가 <b>결정론적 최대 거리</b>였다. 캐릭터를 쓸 수 있는
    /// 구간(발판 ∩ 걸어다닐 수 있는 화면 범위)의 한쪽 끝에 세우고 과녁을 반대쪽 끝에 놓았으므로,
    /// 넓은 바탕화면에서는 사거리가 언제나 "구간 폭 - 여백" = 화면 끝이었다. 코드 어디에도 난수가
    /// 없었다(2026-08-29의 "너무 가까움" 신고를 최대 거리 고정으로 과교정한 결과).
    ///
    /// 수정: 사거리를 <b>화면 폭과 무관한 절대 밴드</b>(신장 × archeryMin~MaxTargetDistanceRatio)에서
    /// 매번 추첨하고, 구간이 좁으면 들어가는 만큼만 줄인다.
    ///
    /// 왜 통계 테스트인가: "랜덤이다"는 값 하나로 증명할 수 없다. 시드를 바꿔가며 수천 번 뽑아
    /// <b>분포 자체</b>(퍼짐, 상위 10% 쏠림, 최댓값 붙음)를 검사해야 회귀를 잡는다. 그래서 배치의
    /// 순수 함수(<see cref="ArcheryDirector.ResolvePlacement"/>)에 난수를 <b>주입</b>하는 구조로
    /// 리팩터했다 — 씬/카메라/MonoBehaviour 없이 EditMode에서 그대로 돌아간다.
    ///
    /// 좌표 기준값은 실행 환경 실측(1512x982pt, orthographicSize 12, 종횡비 1.54 → 가시 반폭 18.48유닛)과
    /// 출하 배율(characterScale 0.75)에서 왔다.
    /// (2026-09-02까지는 BattleMinigamePlacementTests가 같은 기준을 공유했다 — 격파 놀이와 함께 삭제.)
    /// </summary>
    public class ArcheryTargetDistanceTests
    {
        private const float VisibleHalfWidth = 18.48f;
        private const float Height = StickConfig.BaselineCharacterTotalHeight * 0.75f; // ≈ 1.706유닛
        private const float MinRatio = 2.6f;   // 출하 archeryMinTargetDistanceRatio
        private const float MaxRatio = 6.6f;   // 출하 archeryMaxTargetDistanceRatio
        private const float MinDistance = Height * MinRatio;  // ≈ 4.44유닛
        private const float MaxDistance = Height * MaxRatio;  // ≈ 11.26유닛

        /// <summary>ArcheryDirector가 쓰는 여백과 같은 식(신장 배수 + 과녁 반지름).</summary>
        private const float CharInset = Height * ArcheryDirector.CharacterEdgeInsetRatio;
        private const float TargetInset = Height * 0.40f + Height * ArcheryDirector.TargetEdgeInsetRatio;

        private const float Eps = 0.001f;

        /// <summary>쏘기 전에 물러서는 한 걸음(신장 1배) — ArcheryDirector.BackStepRatio와 같은 값.</summary>
        private const float BackStep = Height * ArcheryDirector.BackStepRatio;

        /// <summary>
        /// ★ 이 헬퍼는 폭 비례 하한을 <b>0으로 고정</b>한다. 아래 ①②의 기존 테스트가 잠그는 것은
        /// 2026-08-31 계약(절대 밴드 / 균등 분포 / 구간 침범 금지)이고, 그 계약은 2026-09-02
        /// <c>archeryMinDistanceSpanFraction</c> 도입 뒤에도 <b>f=0에서 비트 단위로 그대로</b> 살아 있어야
        /// 한다 — 그게 이 필드가 안전한 킬 스위치라는 뜻이다. 출하 비율(0.55)의 동작은 ④가 따로 잠근다.
        /// </summary>
        /// <remarks>
        /// ★ 2026-09-06 확장 — 새로 생긴 세 인자(g / Ucap / 화면 비례 바닥)의 기본값이 <b>전부 무효값</b>
        /// (g=0, Ucap=상한, 화면바닥=0)이다. 그래서 ①②③ 절의 기존 판정은 <b>2026-09-06 이전과 비트
        /// 단위로 같은 입력</b>을 보게 되고, 그 절들이 여전히 초록이라는 사실 자체가 "새 knob 세 개가
        /// 안전한 킬 스위치다"의 증명이 된다(⑤-1이 그 동치를 따로 전수 확인한다).
        /// <paramref name="dirRoll01"/> 기본 0은 "두 방향이 다 자격이 있으면 오른쪽"이라 예전
        /// 결정론적 규칙과 같은 갈래를 고른다.
        /// </remarks>
        private static ArcheryDirector.Placement Place(float footX, float lo, float hi, float roll01,
            float spanFraction = 0f, float maxSpanFraction = 0f, float hardCap = MaxDistance,
            float screenFloor = 0f, float dirRoll01 = 0f)
            => ArcheryDirector.ResolvePlacement(footX, lo, hi, CharInset, TargetInset, BackStep,
                MinDistance, MaxDistance, spanFraction, maxSpanFraction, hardCap, screenFloor,
                roll01, dirRoll01);

        /// <summary>시드 고정 난수 — 실행할 때마다 같은 표본을 본다(플레이키 금지).</summary>
        private static float[] Rolls(int seed, int count)
        {
            var rng = new System.Random(seed);
            var v = new float[count];
            for (int i = 0; i < count; i++) v[i] = (float)rng.NextDouble();
            return v;
        }

        // ============================================================================
        // ① 신고의 본체 — 사거리가 매번 달라지고, 최댓값에 몰리지 않는다
        // ============================================================================

        [Test]
        public void 넓은_바탕화면에서_사거리는_매번_달라진다([Values(1, 7, 12345)] int seed)
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            float[] rolls = Rolls(seed, 2000);

            double sum = 0.0, sumSq = 0.0;
            float min = float.MaxValue, max = float.MinValue;
            int topDecile = 0, belowMid = 0;
            float bandHi = MaxDistance; // 화면이 넓으므로 밴드 상한이 그대로 상한이다.
            float mid = (MinDistance + bandHi) * 0.5f;
            float decileEdge = bandHi - (bandHi - MinDistance) * 0.1f;

            foreach (float roll in rolls)
            {
                ArcheryDirector.Placement p = Place(0f, lo, hi, roll);
                Assert.IsTrue(p.Ok, "화면 전체 폭에서 배치에 실패했습니다 — 발동 자체가 사라집니다.");
                float d = p.Distance;
                sum += d; sumSq += (double)d * d;
                if (d < min) min = d;
                if (d > max) max = d;
                if (d >= decileEdge) topDecile++;
                if (d <= mid) belowMid++;
            }

            int n = rolls.Length;
            double mean = sum / n;
            double sd = Math.Sqrt(Math.Max(0.0, sumSq / n - mean * mean));

            Assert.GreaterOrEqual(min, MinDistance - Eps,
                $"최소 사거리({MinDistance:F2}유닛)보다 가까운 표본이 나왔습니다({min:F2}) — " +
                "'쏘는' 게 아니라 '찌르는' 그림이 됩니다.");
            Assert.LessOrEqual(max, bandHi + Eps,
                $"밴드 상한({bandHi:F2}유닛)을 넘은 표본이 나왔습니다({max:F2}) — " +
                "사거리 상한이 다시 화면 폭에 끌려간 것입니다.");

            // ★ 이 세 줄이 신고 문구를 그대로 옮긴 잠금이다.
            Assert.Greater(sd, (bandHi - MinDistance) * 0.2f,
                $"사거리 표준편차가 {sd:F3}유닛뿐입니다 — 사실상 고정값입니다('거리는 항상 랜덤으로 변경').");
            Assert.Less(topDecile / (float)n, 0.20f,
                $"표본의 {topDecile * 100f / n:F1}%가 밴드 상위 10%에 몰렸습니다 — " +
                "'무조건 과녁이 화면 끝에만 생김'이 재발한 상태입니다(균등이면 약 10%).");
            Assert.Greater(belowMid / (float)n, 0.30f,
                "밴드 중앙보다 가까운 사거리가 거의 안 나옵니다 — 분포가 먼 쪽으로 밀려 있습니다.");
        }

        /// <summary>네거티브 컨트롤 — <b>수정 전 알고리즘</b>(캐릭터는 한쪽 끝, 과녁은 반대쪽 끝)을 같은
        /// 파일 안에서 재현해, 위 테스트가 실제로 그 회귀를 잡는다는 것을 보인다. 조건이 헐거워서
        /// 통과하는 게 아님을 증명하는 절차다.</summary>
        [Test]
        public void 수정_전_결정론적_배치는_위_판정을_반드시_실패시킨다()
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            float bandHi = MaxDistance;
            float decileEdge = bandHi - (bandHi - MinDistance) * 0.1f;

            // 옛 코드: standX = lo + charInset, targetX = hi - targetInset (난수 없음).
            float legacyStandX = lo + CharInset;
            float legacyTargetX = hi - TargetInset;
            float legacyDistance = Mathf.Abs(legacyTargetX - legacyStandX);

            Assert.Greater(legacyDistance, decileEdge,
                $"옛 배치의 사거리 {legacyDistance:F2}유닛이 상위 10% 구간에도 못 들어간다면 " +
                "위 분포 판정은 회귀를 잡지 못한다는 뜻입니다.");
            Assert.Greater(legacyDistance, MaxDistance * 2f,
                $"옛 배치는 화면 폭 전체({legacyDistance:F2}유닛)를 사거리로 썼습니다 — " +
                $"새 밴드 상한 {MaxDistance:F2}유닛의 2배를 넘는 '화면 끝' 배치임을 못박습니다.");

            // 표준편차 0 — 같은 입력에 언제나 같은 결과(사용자가 본 '무조건').
            Assert.AreEqual(legacyDistance, Mathf.Abs((hi - TargetInset) - (lo + CharInset)), Eps,
                "옛 배치는 난수 입력이 없으므로 분산이 0입니다.");
        }

        [Test]
        public void 과녁은_화면_가장자리에_붙지_않는다([Values(3, 99)] int seed)
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            float worstGap = float.MaxValue;

            foreach (float roll in Rolls(seed, 1000))
            {
                // 화면 중앙/좌/우 어디에 서 있든 확인한다.
                foreach (float footX in new[] { 0f, -VisibleHalfWidth + 2f, VisibleHalfWidth - 2f })
                {
                    ArcheryDirector.Placement p = Place(footX, lo, hi, roll);
                    Assert.IsTrue(p.Ok);
                    float gap = p.Facing > 0f ? hi - p.TargetX : p.TargetX - lo;
                    if (gap < worstGap) worstGap = gap;
                    Assert.GreaterOrEqual(p.TargetX, lo + TargetInset - Eps, "과녁이 구간 왼쪽 밖입니다.");
                    Assert.LessOrEqual(p.TargetX, hi - TargetInset + Eps, "과녁이 구간 오른쪽 밖입니다.");
                }
            }

            // 캐릭터가 어디에 서 있든, 사거리 상한이 절대 밴드라 과녁은 진행 방향 끝까지 가지 못한다.
            // 최악의 표본은 "화면 중앙에서 밴드 상한으로 쏜 경우"(gap ≈ 18.48 - 11.26 = 7.23유닛 ≈ 250pt).
            // 옛 배치의 gap은 과녁 여백 그 자체(약 1.02유닛)였다 — 7배 차이라 판정이 충분히 예민하다.
            Assert.Greater(worstGap, Height * 3f,
                $"과녁이 진행 방향 화면 끝에서 {worstGap:F2}유닛(신장의 {worstGap / Height:F1}배)까지 " +
                "밀렸습니다 — 신고 문구 '화면 끝에만 생김'의 재발입니다.");
            Assert.Greater(worstGap, TargetInset * 4f,
                "과녁이 구간 끝 여백에 거의 붙었습니다 — 옛 '반대쪽 끝 고정' 배치의 특징입니다.");
        }

        // ============================================================================
        // ② 계약 — 밴드 선형성, 구간 침범 금지, 좁은 발판, 포기 조건
        // ============================================================================

        [Test]
        public void 추첨값_0은_최소_1은_상한이다()
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            Assert.AreEqual(MinDistance, Place(0f, lo, hi, 0f).Distance, Eps,
                "roll 0이 최소 사거리가 아닙니다 — 밴드 하한이 어긋났습니다.");
            Assert.AreEqual(MaxDistance, Place(0f, lo, hi, 1f).Distance, Eps,
                "roll 1이 밴드 상한이 아닙니다.");

            // 단조 증가(선형) — 중간 추첨값이 중간 사거리로 간다.
            float mid = Place(0f, lo, hi, 0.5f).Distance;
            Assert.AreEqual((MinDistance + MaxDistance) * 0.5f, mid, Eps,
                "추첨값과 사거리의 관계가 선형이 아닙니다 — 균등 난수를 넣어도 분포가 한쪽으로 쏠립니다.");
        }

        [Test]
        public void 캐릭터와_과녁은_언제나_구간_안이다([Values(21, 4242)] int seed)
        {
            // 좁은 창(폭 14유닛)부터 화면 전체까지 훑는다.
            float[] widths = { 14f, 20f, 26f, 2f * VisibleHalfWidth };
            foreach (float w in widths)
            {
                float lo = -w * 0.5f, hi = w * 0.5f;
                foreach (float roll in Rolls(seed, 300))
                {
                    foreach (float footX in new[] { lo + 0.2f, 0f, hi - 0.2f })
                    {
                        ArcheryDirector.Placement p = Place(footX, lo, hi, roll);
                        if (!p.Ok) continue; // 너무 좁으면 포기 — 그건 아래 테스트가 따로 잠근다.

                        string ctx = $"구간 폭 {w:F1}, 발 x={footX:F1}, roll={roll:F3}";
                        Assert.GreaterOrEqual(p.StandX, lo + CharInset - Eps, $"{ctx}: 캐릭터가 구간 왼쪽 밖입니다.");
                        Assert.LessOrEqual(p.StandX, hi - CharInset + Eps, $"{ctx}: 캐릭터가 구간 오른쪽 밖입니다.");
                        Assert.GreaterOrEqual(p.TargetX, lo + TargetInset - Eps, $"{ctx}: 과녁이 구간 왼쪽 밖입니다.");
                        Assert.LessOrEqual(p.TargetX, hi - TargetInset + Eps, $"{ctx}: 과녁이 구간 오른쪽 밖입니다.");
                        Assert.GreaterOrEqual(p.Distance, MinDistance - Eps, $"{ctx}: 최소 사거리 미만입니다.");
                        Assert.LessOrEqual(p.Distance, MaxDistance + Eps, $"{ctx}: 밴드 상한 초과입니다.");
                        Assert.AreEqual(p.Distance, Mathf.Abs(p.TargetX - p.StandX), Eps,
                            $"{ctx}: 보고된 사거리와 실제 좌표 차이가 다릅니다.");
                        Assert.AreEqual(p.Facing, Mathf.Sign(p.TargetX - p.StandX), Eps,
                            $"{ctx}: 바라보는 방향과 과녁 방향이 반대입니다(등 뒤로 쏘는 그림).");
                    }
                }
            }
        }

        [Test]
        public void 좁은_발판에서는_들어가는_만큼만_줄인다()
        {
            // 밴드 상한(11.26)은 못 채우지만 하한(4.44)은 넉넉히 되는 창.
            float half = (MinDistance + CharInset + TargetInset + 1.0f) * 0.5f;
            float lo = -half, hi = half;
            float span = (hi - TargetInset) - (lo + CharInset);

            ArcheryDirector.Placement full = Place(lo + 0.1f, lo, hi, 1f);
            Assert.IsTrue(full.Ok, "밴드 하한이 들어가는 창에서 발동을 포기했습니다.");
            Assert.AreEqual(span, full.Distance, Eps,
                "구간이 좁을 때 roll 1은 '가능한 최대'가 되어야 합니다(포기가 아니라 타협).");
            Assert.Less(full.Distance, MaxDistance,
                "이 케이스는 밴드 상한을 못 채우는 창이어야 테스트 의미가 있습니다.");
            Assert.AreEqual(span, full.MaxAvailableDistance, Eps, "진단용 최대 사거리 보고가 틀렸습니다.");

            Assert.AreEqual(MinDistance, Place(lo + 0.1f, lo, hi, 0f).Distance, Eps,
                "좁은 창에서도 하한은 그대로여야 합니다.");
        }

        [Test]
        public void 최소_사거리조차_안_나오면_조용히_포기한다()
        {
            // 폭이 최소 사거리 + 여백에 못 미치는 창.
            float half = (MinDistance + CharInset + TargetInset) * 0.5f - 0.5f;
            for (int i = 0; i <= 10; i++)
            {
                ArcheryDirector.Placement p = Place(0f, -half, half, i / 10f);
                Assert.IsFalse(p.Ok,
                    $"폭 {2f * half:F2}유닛(최소 사거리 {MinDistance:F2} + 여백보다 좁음)에서 배치에 " +
                    "성공했습니다 — 코앞 과녁 또는 발판 밖 허공 과녁이 나옵니다.");
            }

            Assert.IsFalse(Place(0f, 5f, 5f, 0.5f).Ok, "폭 0 구간에서 배치에 성공했습니다.");
            Assert.IsFalse(Place(0f, 5f, -5f, 0.5f).Ok, "뒤집힌 구간(lo > hi)을 걸러내지 못했습니다.");
        }

        /// <summary>
        /// ★ 2026-09-06 이후에도 이 판정은 그대로다 — <b>방향 추첨은 화면 끝을 이기지 못한다</b>.
        /// 좌우 추첨의 후보 자격이 "여분의 도보가 필요 없는 방향"이라, 화면 끝에서는 바깥쪽이 자격을
        /// 잃고 안쪽 한 방향만 남는다. 그래서 <b>어떤 dirRoll을 넣어도</b> 답이 같아야 한다 —
        /// 그것을 아래에서 스윕으로 못박는다(추첨이 비침해 규칙을 뚫지 못한다는 증명).
        /// </summary>
        [Test]
        public void 오른쪽_끝에_서_있으면_왼쪽으로_쏜다()
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            foreach (float dir in new[] { 0f, 0.25f, 0.499f, 0.5f, 0.75f, 1f })
            {
                ArcheryDirector.Placement p = Place(hi - 1f, lo, hi, 0.5f, dirRoll01: dir);
                Assert.IsTrue(p.Ok);
                Assert.AreEqual(-1f, p.Facing, Eps,
                    $"dirRoll={dir:F3}: 오른쪽 끝에 서서 오른쪽으로 쏘려 하고 있습니다 — " +
                    "과녁이 화면 밖으로 나가거나 화면을 가로질러 행진하게 됩니다.");
                Assert.Less(p.TargetX, p.StandX, "왼쪽을 보는데 과녁이 오른쪽에 있습니다.");

                ArcheryDirector.Placement q = Place(lo + 1f, lo, hi, 0.5f, dirRoll01: dir);
                Assert.AreEqual(1f, q.Facing, Eps,
                    $"dirRoll={dir:F3}: 왼쪽 끝에서 왼쪽으로 쏘려 하고 있습니다.");
            }
        }

        [Test]
        public void 화면_끝까지_행진하지_않고_한_걸음만_물러선다()
        {
            // 예전에는 무조건 구간 끝까지 행진했다(그 자체가 '무조건 화면 끝' 그림의 절반이었다).
            // 지금은 "이동 -> 과녁 생성 -> 발사" 순서를 지킬 만큼만(신장 1배) 물러선다.
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            ArcheryDirector.Placement p = Place(0f, lo, hi, 0.5f);
            Assert.IsTrue(p.Ok);
            Assert.AreEqual(1f, p.Facing, Eps);
            Assert.AreEqual(-BackStep, p.StandX, Eps,
                $"화면 중앙에서 한 걸음({BackStep:F2}유닛)만 물러서야 하는데 x={p.StandX:F2}입니다.");
            Assert.Greater(p.StandX - (lo + CharInset), Height * 5f,
                $"캐릭터가 구간 왼쪽 끝(x={lo + CharInset:F2})까지 행진했습니다 — " +
                "매번 화면 가장자리로 걸어가는 옛 동작입니다.");
            Assert.Greater(Mathf.Abs(p.StandX - 0f), Height * 0.5f,
                "이동 거리가 0에 가깝습니다 — 사용자가 확정한 '이동 -> 과녁 생성 -> 발사' 순서에서 " +
                "이동 단계가 눈에 보이지 않게 됩니다(PlayMode 테스트도 이 순서를 잠가 놨습니다).");

            // 앞이 모자랄 때만 뒤로 물러선다(딱 필요한 만큼). 넓은 화면에서는 방향 선택 규칙상
            // 물러설 일이 아예 없으므로(먼 쪽을 향해 쏜다), 좁은 창에서 확인한다.
            float half = (MinDistance + CharInset + TargetInset + 1.0f) * 0.5f;
            float nlo = -half, nhi = half;
            float span = (nhi - TargetInset) - (nlo + CharInset);
            ArcheryDirector.Placement q = Place(0f, nlo, nhi, 1f); // 구간이 허용하는 최대 사거리.
            Assert.IsTrue(q.Ok);
            // ★ 2026-09-06 — 여기서 오른쪽이 나오는 것은 "규칙"이 아니라 **추첨값 0을 넣었기 때문**이다
            //   (Place의 dirRoll01 기본 0 = 오른쪽). 좌우가 완전 대칭인 이 형상에서 방향이 실제로
            //   추첨된다는 사실은 ⑤-3이 잠근다. 여기서는 나머지 좌표 계약만 본다.
            Assert.AreEqual(1f, q.Facing, Eps, "dirRoll 0은 오른쪽이어야 한다(추첨 매핑의 정의).");
            Assert.AreEqual(span, q.Distance, Eps);
            Assert.AreEqual(nhi - TargetInset - q.Distance, q.StandX, Eps,
                "앞 공간이 모자랄 때 물러서는 거리가 '딱 필요한 만큼'이 아닙니다.");
            Assert.Less(q.StandX, 0f, "사거리를 확보하려면 뒤로 물러서야 하는 상황인데 제자리에 섰습니다.");
        }

        // ============================================================================
        // ③ 출하 설정값 자체 점검
        // ============================================================================

        [Test]
        public void 출하_설정의_사거리_밴드가_실제로_랜덤_구간이다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.Greater(cfg.archeryMaxTargetDistanceRatio, cfg.archeryMinTargetDistanceRatio,
                    "밴드 상한이 하한 이하입니다 — 사거리가 다시 고정값이 됩니다.");
                Assert.GreaterOrEqual(cfg.archeryMaxTargetDistanceRatio - cfg.archeryMinTargetDistanceRatio, 2f,
                    "밴드 폭이 신장의 2배 미만입니다 — 육안으로 '거리가 매번 다르다'가 안 읽힙니다.");

                // '적당히 먼 거리' — 가장 먼 사거리도 가시 폭의 절반을 넘지 않아야 한다.
                float maxUnits = StickConfig.BaselineCharacterTotalHeight * cfg.characterScale
                                 * cfg.archeryMaxTargetDistanceRatio;
                Assert.Less(maxUnits, VisibleHalfWidth,
                    $"최대 사거리 {maxUnits:F2}유닛이 가시 반폭 {VisibleHalfWidth:F2}유닛 이상입니다 — " +
                    "'적당히 먼 거리'라는 사용자 요구를 넘어 다시 화면 끝 쪽으로 갑니다.");

                // 기준값(비행 시간 계산의 레퍼런스)이 밴드 안에 있어야 화살 속도가 자연스럽다.
                Assert.GreaterOrEqual(cfg.archeryTargetDistanceRatio, cfg.archeryMinTargetDistanceRatio,
                    "기준 사거리가 밴드 하한보다 짧습니다 — 모든 사격이 기준보다 멀어져 느려집니다.");
                Assert.LessOrEqual(cfg.archeryTargetDistanceRatio, cfg.archeryMaxTargetDistanceRatio,
                    "기준 사거리가 밴드 상한보다 깁니다 — 모든 사격이 기준보다 가까워 빨라집니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        // ============================================================================
        // ④ 2026-09-02 신고 — "너무 가까이 과녁이 생기는 경향 / 창위에서는 창길이에 따라"
        // ============================================================================
        //
        // 처방: archeryMinDistanceSpanFraction(f). 밴드 하한 = max(절대하한, f × 밴드상한).
        //   · 넓은 발판  → 하한이 f×6.6H = 3.63H로 올라간다("너무 가까이"의 해소)
        //   · 좁은 발판  → 상한이 폭에 눌리는 만큼 하한도 함께 내려간다("창길이에 따라 변한다")
        //   · 상한/절대하한 → 한 값도 안 바뀐다(2026-08-31 결정 ②와 무충돌, 포기 빈도 변화 0)
        //
        // ★ 아래 테스트는 프로덕션 상수를 숫자로 베끼지 않는다 — 전부 StickConfig 필드와
        //   ArcheryDirector의 internal 상수를 읽어서 검산한다(협업 프로토콜).

        /// <summary>실측 좌표 기준(1512pt 화면 ↔ 가시 폭 2×18.48유닛)에서 유도한 환산비.
        /// 새 숫자가 아니라 위 <see cref="VisibleHalfWidth"/>에서 나온 값이다.</summary>
        private const float PtPerUnit = 1512f / (2f * VisibleHalfWidth); // ≈ 40.91 pt/유닛

        /// <summary>★ 한 배율만 보면 안 된다 — 설정창이 실제로 노출하는 범위 전체를 훑는다.</summary>
        private static readonly float[] Scales = { 0.35f, 0.60f, 0.75f, 1.00f };

        /// <summary>
        /// 실측 발판 폭 표본(pt, macOS 2026-09-02 로그).
        /// ★ <b>227pt는 바닥 안전망 조각</b>이다 — Dock(x227~1285)에 잘려 좌·우로 나뉜 두 조각이고,
        /// 이 앱에서 <b>가장 좁은 상시 발판</b>이다. "바닥 안전망 = 화면 전폭"이라는 통념이 틀렸다는
        /// 것이 이번 라운드에 실측으로 확인됐으므로, 표본에서 절대 빼지 않는다.
        /// </summary>
        private static readonly float[] FootholdWidthsPt =
            { 227f, 300f, 400f, 422f, 500f, 501f, 600f, 935f, 1058f, 1280f, 1490f, 1512f };

        private static ArcheryDirector.Placement PlaceScaled(StickConfig cfg, float scale, float footX,
            float lo, float hi, float spanFraction, float roll01,
            float maxSpanFraction = 0f, float screenFloor = 0f, float dirRoll01 = 0f)
        {
            float h = StickConfig.BaselineCharacterTotalHeight * scale;
            return ArcheryDirector.ResolvePlacement(footX, lo, hi,
                h * ArcheryDirector.CharacterEdgeInsetRatio,
                h * cfg.archeryTargetRadiusRatio + h * ArcheryDirector.TargetEdgeInsetRatio,
                h * ArcheryDirector.BackStepRatio,
                h * cfg.archeryMinTargetDistanceRatio,
                h * cfg.archeryMaxTargetDistanceRatio,
                spanFraction, maxSpanFraction, h * cfg.archeryMaxDistanceHardCapRatio, screenFloor,
                roll01, dirRoll01);
        }

        [Test]
        public void 넓은_발판에서는_밴드_하한이_폭비례로_올라간다(
            [Values(0.35f, 0.60f, 0.75f, 1.00f)] float scale)
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float h = StickConfig.BaselineCharacterTotalHeight * scale;
                float f = cfg.archeryMinDistanceSpanFraction;
                float absFloor = h * cfg.archeryMinTargetDistanceRatio;
                float bandHi = h * cfg.archeryMaxTargetDistanceRatio; // 화면 전폭이라 절대 상한으로 포화.
                float expectedLo = Mathf.Max(absFloor, f * bandHi);

                float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;

                // 경계는 표본이 아니라 roll 0/1로 직접 본다(표본 최소값은 하한에 정확히 닿지 않는다).
                ArcheryDirector.Placement atFloor = PlaceScaled(cfg, scale, 0f, lo, hi, f, 0f);
                ArcheryDirector.Placement atCeil = PlaceScaled(cfg, scale, 0f, lo, hi, f, 1f);
                ArcheryDirector.Placement legacyFloor = PlaceScaled(cfg, scale, 0f, lo, hi, 0f, 0f);
                ArcheryDirector.Placement legacyCeil = PlaceScaled(cfg, scale, 0f, lo, hi, 0f, 1f);
                Assert.IsTrue(atFloor.Ok && atCeil.Ok, $"배율 {scale}: 화면 전폭에서 배치에 실패했습니다.");

                Assert.AreEqual(expectedLo, atFloor.Distance, Eps,
                    $"배율 {scale}: 밴드 하한이 {atFloor.Distance / h:F3}H입니다. 폭 비례 하한 " +
                    $"{expectedLo / h:F3}H(= {f:F2} × {cfg.archeryMaxTargetDistanceRatio}H)가 안 걸렸습니다.");
                Assert.AreEqual(expectedLo, atFloor.BandLo, Eps, $"배율 {scale}: 보고된 밴드 하한이 다릅니다.");

                // ★ 하한이 실제로 올라갔다 — f=0(구동작)과 직접 대조한다.
                Assert.AreEqual(absFloor, legacyFloor.Distance, Eps,
                    $"배율 {scale}: f=0이 구동작 하한을 재현하지 못합니다(대조군이 깨졌습니다).");
                Assert.Greater(atFloor.Distance, absFloor + Eps,
                    $"배율 {scale}: 하한이 여전히 절대 하한 {cfg.archeryMinTargetDistanceRatio}H에 " +
                    "머물러 있습니다 — 신고('너무 가까이 생기는 경향')가 그대로입니다.");

                // ★ 상한은 한 값도 안 움직인다(2026-08-31 결정 ②).
                Assert.AreEqual(bandHi, atCeil.Distance, Eps,
                    $"배율 {scale}: 밴드 상한이 {atCeil.Distance / h:F3}H로 움직였습니다 — " +
                    "'적당히 먼 거리만 되도 된다'는 요구를 넘어 다시 화면 끝으로 갑니다.");
                Assert.AreEqual(legacyCeil.Distance, atCeil.Distance, Eps,
                    $"배율 {scale}: 폭 비례 하한이 상한을 건드렸습니다.");

                // 표본 전체가 새 밴드 안에 있고, 그 안에서 여전히 흩어진다.
                float min = float.MaxValue, max = float.MinValue;
                double sum = 0.0;
                float[] rolls = Rolls(2026, 1500);
                foreach (float roll in rolls)
                {
                    ArcheryDirector.Placement p = PlaceScaled(cfg, scale, 0f, lo, hi, f, roll);
                    Assert.IsTrue(p.Ok, $"배율 {scale}: 화면 전폭에서 배치에 실패했습니다(roll {roll:F4}).");
                    min = Mathf.Min(min, p.Distance);
                    max = Mathf.Max(max, p.Distance);
                    sum += p.Distance;
                }
                Assert.GreaterOrEqual(min, expectedLo - Eps,
                    $"배율 {scale}: 새 하한 {expectedLo / h:F3}H보다 가까운 표본 {min / h:F3}H가 나왔습니다.");
                Assert.LessOrEqual(max, bandHi + Eps,
                    $"배율 {scale}: 밴드 상한을 넘은 표본 {max / h:F3}H가 나왔습니다.");
                Assert.Greater(max - min, h,
                    $"배율 {scale}: 표본 폭이 {(max - min) / h:F2}H뿐입니다 — 랜덤이 죽었습니다.");
                Assert.AreEqual((expectedLo + bandHi) * 0.5f, (float)(sum / rolls.Length), h * 0.15f,
                    $"배율 {scale}: 표본 평균이 밴드 중앙에서 벗어났습니다 — 분포가 한쪽으로 쏠렸습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void 좁은_발판에서는_하한이_저절로_내려간다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                int narrowedCases = 0, saturatedCases = 0;

                foreach (float scale in Scales)
                {
                    float h = StickConfig.BaselineCharacterTotalHeight * scale;
                    float absFloor = h * cfg.archeryMinTargetDistanceRatio;
                    float absCeil = h * cfg.archeryMaxTargetDistanceRatio;
                    float prevFloor = 0f;

                    foreach (float widthPt in FootholdWidthsPt)
                    {
                        float half = widthPt / PtPerUnit * 0.5f;
                        ArcheryDirector.Placement atFloor = PlaceScaled(cfg, scale, 0f, -half, half, f, 0f);
                        if (!atFloor.Ok) continue; // 포기는 ④-3이 따로 잠근다.

                        // 이 발판에서 물리적으로 가능한 최대 = MaxAvailableDistance(span).
                        float bandHi = Mathf.Min(absCeil, atFloor.MaxAvailableDistance);
                        float expectedLo = Mathf.Max(absFloor, f * bandHi);
                        string ctx = $"배율 {scale}, 발판 {widthPt:F0}pt";

                        Assert.AreEqual(expectedLo, atFloor.BandLo, Eps, $"{ctx}: 밴드 하한이 어긋났습니다.");
                        Assert.AreEqual(expectedLo, atFloor.Distance, Eps,
                            $"{ctx}: roll 0이 밴드 하한이 아닙니다(선형성 파손).");
                        Assert.LessOrEqual(atFloor.BandLo, atFloor.BandHi + Eps,
                            $"{ctx}: 하한이 상한을 넘었습니다 — 밴드가 뒤집혔습니다.");

                        // ★ "창길이에 따라 변한다" — 폭이 넓어질수록 하한은 단조 증가하고, 어느 지점부터
                        //    절대 밴드로 포화한다. 좁은 쪽에서는 절대 하한까지 내려와야 한다.
                        Assert.GreaterOrEqual(atFloor.BandLo, prevFloor - Eps,
                            $"{ctx}: 발판이 넓어졌는데 하한이 내려갔습니다(단조성 파손).");
                        prevFloor = atFloor.BandLo;

                        if (atFloor.BandLo < f * absCeil - Eps) narrowedCases++;
                        else saturatedCases++;
                    }
                }

                Assert.Greater(narrowedCases, 0,
                    "어느 발판에서도 하한이 폭에 눌려 내려가지 않았습니다 — 사용자가 양보절로 말한 " +
                    "'창위에서는 창길이에 따라 변해야겠지만'이 성립하지 않습니다.");
                Assert.Greater(saturatedCases, 0,
                    "어느 발판에서도 절대 밴드로 포화하지 않았습니다 — 표본이 좁은 쪽에만 쏠려 있습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ★★ 이번 라운드의 핵심 약속 — <b>포기 빈도 변화 정확히 0%</b>.
        /// 발동 가부는 절대 하한만으로 갈리므로 f를 어떻게 두든 <see cref="ArcheryDirector.Placement.Ok"/>가
        /// 한 건도 뒤집히면 안 된다. 발판 폭 12종(★ 안전망 227pt 포함) × 배율 4종 × 발 위치 3종 ×
        /// 추첨 200회를 전수 비교한다.
        /// </summary>
        [Test]
        public void 폭_비례_하한은_포기_빈도를_늘리지_않는다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float[] rolls = Rolls(4242, 200);
                int legacyGiveUps = 0, proposedGiveUps = 0, cases = 0;

                foreach (float scale in Scales)
                foreach (float widthPt in FootholdWidthsPt)
                {
                    float half = widthPt / PtPerUnit * 0.5f;
                    foreach (float footFrac in new[] { -0.45f, 0f, 0.45f })
                    {
                        float footX = half * 2f * footFrac;
                        foreach (float roll in rolls)
                        {
                            bool legacyOk = PlaceScaled(cfg, scale, footX, -half, half, 0f, roll).Ok;
                            bool proposedOk = PlaceScaled(cfg, scale, footX, -half, half, f, roll).Ok;
                            cases++;
                            if (!legacyOk) legacyGiveUps++;
                            if (!proposedOk) proposedGiveUps++;
                            Assert.AreEqual(legacyOk, proposedOk,
                                $"배율 {scale}, 발판 {widthPt:F0}pt, 발 x={footX:F2}, roll={roll:F4}: " +
                                $"발동 가부가 뒤집혔습니다(구 {legacyOk} -> 신 {proposedOk}). " +
                                "폭 비례 하한은 '발동할 수 있는가' 판정을 건드리면 안 됩니다.");
                        }
                    }
                }

                Assert.AreEqual(legacyGiveUps, proposedGiveUps,
                    $"표본 {cases}건에서 포기 수가 {legacyGiveUps} -> {proposedGiveUps}로 변했습니다.");
                // 표본에 실제로 포기가 섞여 있어야 위 비교가 의미를 갖는다(빈 집합 비교 방지).
                Assert.Greater(legacyGiveUps, 0,
                    "표본에 포기 사례가 하나도 없습니다 — '포기가 안 늘었다'가 공허하게 참이 됩니다. " +
                    "좁은 발판(안전망 227pt 등)이 표본에서 빠졌는지 확인하십시오.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 2건 — 이 파일의 판정이 <b>정말로 f를 보고 있다</b>는 증명.
        /// (가) f=1이면 하한이 상한과 같아져 <b>밴드가 한 점으로 붕괴</b>한다(= 랜덤이 죽는다).
        ///      그래서 프로덕션 접근자가 0.9로 클램프한다.
        /// (나) f=0이면 2026-09-02 이전 동작과 <b>비트 단위로</b> 같다(= 안전한 킬 스위치).
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_f가_1이면_밴드가_붕괴하고_0이면_구동작이다(
            [Values(0.35f, 0.60f, 0.75f, 1.00f)] float scale)
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float h = StickConfig.BaselineCharacterTotalHeight * scale;
                float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
                float absFloor = h * cfg.archeryMinTargetDistanceRatio;
                float absCeil = h * cfg.archeryMaxTargetDistanceRatio;
                float[] rolls = Rolls(7, 400);

                float collapsedMin = float.MaxValue, collapsedMax = float.MinValue;
                float shippingMin = float.MaxValue, shippingMax = float.MinValue;
                foreach (float roll in rolls)
                {
                    ArcheryDirector.Placement collapsed = PlaceScaled(cfg, scale, 0f, lo, hi, 1f, roll);
                    Assert.IsTrue(collapsed.Ok, "f=1이 발동을 죽였습니다 — 붕괴는 밴드에서만 일어나야 합니다.");
                    collapsedMin = Mathf.Min(collapsedMin, collapsed.Distance);
                    collapsedMax = Mathf.Max(collapsedMax, collapsed.Distance);

                    ArcheryDirector.Placement legacy = PlaceScaled(cfg, scale, 0f, lo, hi, 0f, roll);
                    // (나) 구 알고리즘의 정의를 여기서 다시 계산해 대조한다(코드를 믿지 않는다).
                    float expectedLegacy = Mathf.Lerp(absFloor, absCeil, Mathf.Clamp01(roll));
                    Assert.AreEqual(expectedLegacy, legacy.Distance, Eps,
                        $"배율 {scale}: f=0이 구동작(절대 밴드 균등 추첨)을 재현하지 못합니다 — " +
                        "킬 스위치가 안전하지 않다는 뜻입니다.");

                    ArcheryDirector.Placement shipping =
                        PlaceScaled(cfg, scale, 0f, lo, hi, cfg.archeryMinDistanceSpanFraction, roll);
                    shippingMin = Mathf.Min(shippingMin, shipping.Distance);
                    shippingMax = Mathf.Max(shippingMax, shipping.Distance);
                }

                // (가) 붕괴 확인 — 400회 추첨의 폭이 0이다.
                Assert.AreEqual(0f, collapsedMax - collapsedMin, Eps,
                    $"배율 {scale}: f=1인데 사거리가 {collapsedMin:F3}~{collapsedMax:F3}유닛으로 " +
                    "여전히 흩어집니다 — 하한이 상한에 안 붙었다는 뜻이고, 그러면 이 파일의 다른 " +
                    "'하한이 올라갔다' 판정도 f를 안 보고 있을 수 있습니다.");
                Assert.AreEqual(absCeil, collapsedMax, Eps,
                    $"배율 {scale}: f=1의 붕괴점이 밴드 상한이 아닙니다.");

                // 출하 비율은 반대로 밴드가 살아 있어야 한다(붕괴 방어선이 실제로 작동).
                Assert.Greater(shippingMax - shippingMin, h,
                    $"배율 {scale}: 출하 비율 {cfg.archeryMinDistanceSpanFraction:F2}에서 밴드 폭이 " +
                    $"{(shippingMax - shippingMin) / h:F2}H뿐입니다 — '거리는 항상 랜덤'이 죽습니다.");
                Assert.Less(cfg.archeryMinDistanceSpanFraction, ArcheryDirector.MaxMinDistanceSpanFraction,
                    "출하 비율이 붕괴 방어선에 닿아 있습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ★ 임무 3-1 검증 — <b>가용성 조회가 난수를 안 먹어도 판정이 안 뒤집힌다</b>.
        /// <see cref="ArcheryDirector.GetAvailability"/>가 고정 roll 0.5를 쓰게 바뀌었으므로,
        /// "어떤 roll에서든 <c>Ok</c>가 같다"가 <b>구조적 사실</b>이어야 한다. 설계자 주장이 아니라
        /// 전수 스윕으로 확인한다 — 경계(roll = 0, 1, 1-ε)를 반드시 포함한다.
        /// </summary>
        [Test]
        public void 가용성_고정roll은_발동_가부를_뒤집지_않는다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                var rollList = new System.Collections.Generic.List<float>
                    { 0f, 1f, 1f - 1e-7f, 1f - 1e-4f, 1e-7f, 0.5f };
                rollList.AddRange(Rolls(31337, 150));

                // 프로덕션 폴링과 같은 고정값. 숫자를 베끼지 않고 "0.5"라는 성질만 쓴다.
                const float probe = 0.5f;
                int flips = 0, okCases = 0, giveUpCases = 0;

                foreach (float scale in Scales)
                foreach (float widthPt in FootholdWidthsPt)
                {
                    float half = widthPt / PtPerUnit * 0.5f;
                    foreach (float footFrac in new[] { -0.49f, -0.2f, 0f, 0.2f, 0.49f })
                    {
                        float footX = half * 2f * footFrac;
                        bool probeOk = PlaceScaled(cfg, scale, footX, -half, half, f, probe).Ok;
                        if (probeOk) okCases++; else giveUpCases++;

                        foreach (float roll in rollList)
                        {
                            bool actualOk = PlaceScaled(cfg, scale, footX, -half, half, f, roll).Ok;
                            if (actualOk != probeOk)
                            {
                                flips++;
                                Assert.Fail($"배율 {scale}, 발판 {widthPt:F0}pt, 발 x={footX:F2}: " +
                                    $"roll {roll:F7}에서 Ok={actualOk}인데 고정 roll {probe}에서는 " +
                                    $"Ok={probeOk}입니다. 회색 처리와 실제 실행이 어긋납니다 — " +
                                    "GetAvailability를 다시 난수로 되돌려야 합니다.");
                            }
                        }
                    }
                }

                Assert.AreEqual(0, flips);
                Assert.Greater(okCases, 0, "표본에 발동 가능한 경우가 없습니다.");
                Assert.Greater(giveUpCases, 0,
                    "표본에 포기 사례가 없습니다 — 'Ok가 안 뒤집힌다'가 공허하게 참이 됩니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// 출하 비율 0.55가 <b>설계 근거 두 축</b>을 실제로 만족하는지 — 값만 보지 않고 근거를 재계산한다.
        ///
        /// <para>★ <b>설계서(docs/MOTION_SPEC.md 24-4-2)의 산수 하나를 여기서 정정한다.</b>
        /// 위쪽 요구 "(1−f)·M/3 ≥ 1.0H"를 풀면 <c>f ≤ 1 − 3/6.6 = <b>0.5455</b></c>인데 설계서에는
        /// <c>0.5545</c>로 적혀 있다(자릿수가 뒤바뀐 것으로 보인다). 그래서 채택값 0.55는 설계서 주장과
        /// 달리 그 상한 <b>바로 바깥</b>이고, 연속 2회 평균 사거리 차가 1.000H가 아니라 <b>0.990H</b>다
        /// (설계서 24-4-2 표 자신도 0.55 행에 0.99H로 적고 있어 문서가 자기모순이다).
        /// 1.0% 미달이라 육안 판정에는 영향이 없다고 보고 리더 지시대로 0.55를 구현했으나,
        /// <b>테스트에 틀린 상한 0.5545를 박제하지는 않는다</b> — 그 숫자를 근거로 다음 사람이
        /// 값을 더 올리는 것이 이 저장소가 반복해 온 사고이기 때문이다.</para>
        /// </summary>
        [Test]
        public void 출하_폭비례_비율이_설계_근거_두_축을_만족한다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float maxRatio = cfg.archeryMaxTargetDistanceRatio;
                float radius = cfg.archeryTargetRadiusRatio;

                Assert.Greater(f, 0f, "폭 비례 하한이 꺼져 있습니다(킬 스위치가 켜진 상태로 출하).");
                Assert.LessOrEqual(f, ArcheryDirector.MaxMinDistanceSpanFraction);

                // (축 1) 아래에서 조인다 — 활끝과 과녁 앞 테두리 사이 빈 공간 ≥ 과녁 지름 2.5배.
                //   0.645H는 활 실루엣 앞끝(뽑은 화살촉) 위치의 **픽셀 실측**이다
                //   (design/motion/2026-09-02_활쏘기_활끝실측_확대.png). 프로덕션 상수가 아니라 계측값.
                const float bowTipAheadRatio = 0.645f;
                const float requiredClearAirDiameters = 2.5f;
                float floorRatio = f * maxRatio;
                float clearAirDiameters = (floorRatio - bowTipAheadRatio - radius) / (2f * radius);
                Assert.GreaterOrEqual(clearAirDiameters, requiredClearAirDiameters,
                    $"넓은 발판 하한 {floorRatio:F3}H에서 활끝~과녁 빈 공간이 과녁 지름의 " +
                    $"{clearAirDiameters:F2}배뿐입니다(요구 {requiredClearAirDiameters}). " +
                    "실측 부적합 사례(2.60H, 1.94지름)와 같은 그림이 다시 나옵니다.");

                // (축 2) 위에서 조인다 — 연속 2회 사거리 차의 기댓값 = (1−f)·M/3 ≥ 신장 1배.
                //   ★ 위 문서 주석 참고: 채택값 0.55는 이 요구를 1.0% 미달(0.990H)한다. 리더 승인
                //   범위 안이라 통과시키되, 여기서 더 나빠지는 것은 막는다.
                const float designMeanGapH = 1.0f;      // 설계 목표("적어도 캐릭터 한 몸")
                const float acceptedShortfall = 0.02f;  // 0.990H까지만 허용(현행 미달분 1.0%의 2배)
                float meanGapH = (1f - f) * maxRatio / 3f;
                Assert.GreaterOrEqual(meanGapH, designMeanGapH - acceptedShortfall,
                    $"연속 2회 사거리 차의 기댓값이 {meanGapH:F4}H입니다(설계 목표 {designMeanGapH}H). " +
                    "이보다 좁아지면 '거리는 항상 랜덤으로 변경'(2026-08-31 사용자 명시)이 육안으로 " +
                    $"안 읽힙니다. 참고: 이 요구의 정확한 상한은 f ≤ 1 − 3/{maxRatio} = " +
                    $"{1f - 3f / maxRatio:F4}이며, 설계서의 0.5545는 오기입니다.");

                // 상·하한과의 관계 — 폭 비례 하한은 언제나 그 둘 사이에 있어야 한다.
                Assert.Greater(floorRatio, cfg.archeryMinTargetDistanceRatio,
                    "폭 비례 하한이 절대 하한보다 낮아 아무 효과가 없습니다.");
                Assert.Less(floorRatio, maxRatio,
                    "폭 비례 하한이 밴드 상한 이상입니다 — 밴드가 붕괴합니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        // ============================================================================
        // ⑤ 2026-09-06 신고(세 번째) — "너무 가까운 위치에 생김 / 화면 폭 비율 기준 최소 거리 /
        //    거리·좌우 방향 둘 다 매번 랜덤"
        // ============================================================================
        //
        // 진단: ④의 폭 비례 하한(f)이 왜 안 먹혔는가 —
        //   하한 = f × 밴드상한인데 **상한이 신장 배수(절대치)**라 하한도 절대치였다.
        //   같은 3.63H가 배율 0.75에서 화면 폭의 16.8%, 0.45에서 10.1%, 0.35에서 7.8%다.
        //   신장 배수로만 보면 셋이 똑같아 보여서 세 번의 신고 동안 아무도 못 봤다.
        //
        // 처방 3종:
        //   g    archeryMaxDistanceSpanFraction        상한을 발판 폭에 비례시킨다(하한을 올릴 여유를 만든다)
        //   Ucap archeryMaxDistanceHardCapRatio        그 상한의 절대 천장(연출: 한 발 = 한 박자)
        //   s    archeryMinTargetDistanceScreenFraction ★ 하한을 **화면 폭**에 비례시킨다
        //   + 좌우 방향 추첨(dirRoll)
        //
        // ★ 이 절의 기대값은 프로덕션 함수가 아니라 **정의를 다시 쓴 식**에서 나온다
        //   (TEAM.md: 기대값을 프로덕션 함수로 만들면 아무것도 못 잰다).

        /// <summary>독립 재계산 — 프로덕션 코드를 부르지 않고 계약 문장 그대로 다시 쓴 것.</summary>
        private static void ExpectedBand(StickConfig cfg, float scale, float span, float screenWidth,
            out float lo, out float hi)
        {
            float h = StickConfig.BaselineCharacterTotalHeight * scale;
            float u0 = h * cfg.archeryMaxTargetDistanceRatio;
            float hard = Mathf.Max(u0, h * cfg.archeryMaxDistanceHardCapRatio);
            float g = Mathf.Clamp(cfg.archeryMaxDistanceSpanFraction, 0f,
                ArcheryDirector.MaxMaxDistanceSpanFraction);
            float f = cfg.archeryMinDistanceSpanFraction;
            hi = Mathf.Min(span, Mathf.Max(u0, Mathf.Min(g * span, hard)));
            lo = Mathf.Max(h * cfg.archeryMinTargetDistanceRatio,
                Mathf.Max(f * Mathf.Min(hi, u0),
                          Mathf.Min(screenWidth * cfg.archeryMinTargetDistanceScreenFraction, f * hi)));
        }

        /// <summary>실측 화면 형상. Player.log 2026-09-06 실기: 카메라픽셀 3024x1964(=1512pt @2x),
        /// orthographicSize 12 → 가시 반폭 <see cref="VisibleHalfWidth"/>. 새 숫자가 아니다.</summary>
        private const float ScreenWidthWorld = 2f * VisibleHalfWidth;

        /// <summary>
        /// ⑤-1 ★ <b>세 knob이 전부 안전한 킬 스위치</b> — g=0 · 화면바닥=0이면 2026-09-06 이전과
        /// <b>비트 단위로</b> 같다. 이게 참이라야 ①②③④의 초록이 여전히 무언가를 재는 것이다.
        /// </summary>
        [Test]
        public void 킬스위치_g0과_화면바닥0은_구동작과_비트_동일하다([Values(11, 2026)] int seed)
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                int compared = 0;
                foreach (float scale in Scales)
                foreach (float widthPt in FootholdWidthsPt)
                {
                    float half = widthPt / PtPerUnit * 0.5f;
                    foreach (float footFrac in new[] { -0.45f, 0f, 0.45f })
                    foreach (float roll in Rolls(seed, 60))
                    {
                        float footX = half * 2f * footFrac;
                        // 구동작 = 이 파일이 ④까지 쓰던 호출(새 인자 전부 무효값).
                        var legacy = PlaceScaled(cfg, scale, footX, -half, half, f, roll);
                        var offed = PlaceScaled(cfg, scale, footX, -half, half, f, roll,
                            maxSpanFraction: 0f, screenFloor: 0f);
                        Assert.AreEqual(legacy.Ok, offed.Ok);
                        if (!legacy.Ok) continue;
                        compared++;
                        Assert.AreEqual(legacy.Distance, offed.Distance, 0f,
                            $"배율 {scale}, {widthPt:F0}pt, roll {roll:F4}: 킬 스위치가 비트 동일하지 않습니다.");
                        Assert.AreEqual(legacy.BandLo, offed.BandLo, 0f);
                        Assert.AreEqual(legacy.BandHi, offed.BandHi, 0f);
                        Assert.AreEqual(legacy.StandX, offed.StandX, 0f);
                        Assert.AreEqual(legacy.TargetX, offed.TargetX, 0f);
                    }
                }
                Assert.Greater(compared, 1000, "비교 표본이 너무 적습니다 — 공허하게 참이 됩니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ⑤-2 ★★ <b>신고의 본체</b> — 넓은 발판에서 최소 사거리가 <b>화면 폭 비율</b>로 바닥을 갖는다.
        /// 사용자가 직접 제시한 대역("화면 폭의 15~25%") 안이어야 하고, 신고 상태보다 내려가면 안 된다.
        /// </summary>
        [Test]
        public void 최소_사거리가_화면_폭_비율_바닥을_갖는다(
            [Values(0.35f, 0.45f, 0.60f, 0.75f, 1.00f)] float scale)
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float h = StickConfig.BaselineCharacterTotalHeight * scale;
                float f = cfg.archeryMinDistanceSpanFraction;
                float s = cfg.archeryMinTargetDistanceScreenFraction;
                float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
                float span = (hi - (h * cfg.archeryTargetRadiusRatio + h * ArcheryDirector.TargetEdgeInsetRatio))
                             - (lo + h * ArcheryDirector.CharacterEdgeInsetRatio);

                ExpectedBand(cfg, scale, span, ScreenWidthWorld, out float wantLo, out float wantHi);

                float screenFloor = ScreenWidthWorld * s;
                ArcheryDirector.Placement atFloor = PlaceScaled(cfg, scale, 0f, lo, hi, f, 0f,
                    maxSpanFraction: cfg.archeryMaxDistanceSpanFraction, screenFloor: screenFloor);
                ArcheryDirector.Placement atCeil = PlaceScaled(cfg, scale, 0f, lo, hi, f, 1f,
                    maxSpanFraction: cfg.archeryMaxDistanceSpanFraction, screenFloor: screenFloor);
                Assert.IsTrue(atFloor.Ok && atCeil.Ok, $"배율 {scale}: 화면 전폭에서 배치에 실패했습니다.");

                // ★ 기대값은 프로덕션 함수가 아니라 계약 문장을 다시 쓴 식에서 온다(ExpectedBand).
                Assert.AreEqual(wantLo, atFloor.Distance, Eps,
                    $"배율 {scale}: roll 0의 사거리가 독립 재계산과 다릅니다.");
                Assert.AreEqual(wantHi, atCeil.Distance, Eps,
                    $"배율 {scale}: roll 1의 사거리가 독립 재계산과 다릅니다.");

                // ★ 신고 상태(구동작)와의 직접 대조 — 하한이 내려가는 일은 없어야 한다.
                //   ※ 배율 1.00에서는 폭 비례 하한(f×6.6H = 22.3%W)이 이미 화면 비례 바닥(22%W)보다
                //     크므로 «변화 없음»이 정답이다. 그래서 여기서는 «안 내려갔다»만 잠그고,
                //     «실제로 올라갔다»는 아래 배율 무의존성 테스트가 잠근다.
                ArcheryDirector.Placement legacyFloor = PlaceScaled(cfg, scale, 0f, lo, hi, f, 0f);
                Assert.GreaterOrEqual(atFloor.Distance, legacyFloor.Distance - Eps,
                    $"배율 {scale}: 하한이 신고 상태 " +
                    $"{legacyFloor.Distance / ScreenWidthWorld:P1}W보다 **내려갔습니다**.");

                // ★ 사용자가 직접 제시한 대역 — "화면 폭의 15~25%".
                float floorFraction = atFloor.Distance / ScreenWidthWorld;
                Assert.GreaterOrEqual(floorFraction, 0.15f,
                    $"배율 {scale}: 최소 사거리가 화면 폭의 {floorFraction:P1}뿐입니다 — " +
                    "사용자가 제시한 하한 15%를 못 채웁니다.");
                Assert.LessOrEqual(floorFraction, 0.25f,
                    $"배율 {scale}: 최소 사거리가 화면 폭의 {floorFraction:P1}입니다 — " +
                    "사용자가 제시한 상한 25%를 넘겼습니다.");

                // '적당히 먼 거리'(2026-08-31) — 가장 먼 사격도 화면 폭의 절반을 넘지 않는다.
                Assert.Less(atCeil.Distance, ScreenWidthWorld * 0.5f,
                    $"배율 {scale}: 최대 사거리가 화면 폭의 " +
                    $"{atCeil.Distance / ScreenWidthWorld:P1}입니다 — 다시 화면 끝으로 갑니다.");

                // 밴드는 살아 있어야 한다("거리는 항상 랜덤", 2026-08-31).
                Assert.LessOrEqual(atFloor.Distance, f * atCeil.Distance + Eps,
                    $"배율 {scale}: 하한이 f×상한을 넘었습니다 — 붕괴 클램프가 안 걸렸습니다.");
                Assert.Greater((atCeil.Distance - atFloor.Distance) / 3f, h,
                    $"배율 {scale}: 연속 2회 사거리 차의 기댓값이 " +
                    $"{(atCeil.Distance - atFloor.Distance) / 3f / h:F2}H뿐입니다(요구 1H).");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ⑤-2b ★★ <b>이 라운드의 진단 그 자체</b> — 신고의 원인은 하한이 <b>신장 배수</b>라서
        /// 화면 대비 값이 배율을 따라 미끄러진 것이었다. 같은 "3.63H"가 배율 0.75에서 16.8%W,
        /// 0.45에서 10.1%W, 0.35에서 7.8%W다. <b>신장 배수로만 보면 셋이 똑같아 보여서</b>
        /// 세 번의 신고 동안 아무도 못 봤다.
        ///
        /// <para>그래서 이 테스트는 값 하나가 아니라 <b>배율 축의 스프레드</b>를 잰다.
        /// 구동작을 같은 파일에서 함께 재어 대조군으로 쓴다(판정이 헐거워서 통과하는 게 아님을 증명).</para>
        /// </summary>
        [Test]
        public void 최소_사거리의_화면_비율이_배율에_끌려다니지_않는다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float screenFloor = ScreenWidthWorld * cfg.archeryMinTargetDistanceScreenFraction;
                float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
                float[] scales = { 0.45f, 0.60f, 0.75f, 1.00f }; // 0.35는 Ucap이 먼저 걸린다(⑤-2가 커버)

                float newMin = float.MaxValue, newMax = float.MinValue;
                float oldMin = float.MaxValue, oldMax = float.MinValue;
                int raised = 0;
                foreach (float scale in scales)
                {
                    ArcheryDirector.Placement now = PlaceScaled(cfg, scale, 0f, lo, hi, f, 0f,
                        maxSpanFraction: cfg.archeryMaxDistanceSpanFraction, screenFloor: screenFloor);
                    ArcheryDirector.Placement was = PlaceScaled(cfg, scale, 0f, lo, hi, f, 0f);
                    Assert.IsTrue(now.Ok && was.Ok);
                    float a = now.Distance / ScreenWidthWorld, b = was.Distance / ScreenWidthWorld;
                    newMin = Mathf.Min(newMin, a); newMax = Mathf.Max(newMax, a);
                    oldMin = Mathf.Min(oldMin, b); oldMax = Mathf.Max(oldMax, b);
                    if (now.Distance > was.Distance + Eps) raised++;
                }

                float newSpread = newMax - newMin, oldSpread = oldMax - oldMin;
                Assert.Less(newSpread, 0.04f,
                    $"배율 0.45~1.00에서 최소 사거리의 화면 비율이 {newMin:P1}~{newMax:P1}로 " +
                    $"{newSpread:P1}p 흔들립니다 — 하한이 여전히 배율에 끌려다닙니다.");
                // ★ 네거티브 컨트롤 — 구동작에서는 실제로 크게 흔들렸다.
                Assert.Greater(oldSpread, newSpread * 3f,
                    $"구동작 스프레드 {oldSpread:P1}p가 신동작 {newSpread:P1}p의 3배도 안 됩니다 — " +
                    "이 판정이 실제로 배율 축을 재고 있지 않다는 뜻입니다(대조군 파손).");
                Assert.GreaterOrEqual(raised, scales.Length - 1,
                    $"배율 {scales.Length}종 중 {raised}종에서만 하한이 올라갔습니다 — " +
                    "화면 비례 바닥이 대부분의 배율에서 안 걸린다는 뜻입니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>구 방향 규칙("구간이 더 넓게 남은 쪽")의 <b>독립 재현</b>. 프로덕션에서는 지워졌으므로
        /// 대조군으로 쓰려면 여기서 다시 써야 한다 — 이 한 줄이 ⑤-3의 네거티브 컨트롤 전체다.</summary>
        private static float LegacyFacing(float footX, float lo, float hi)
            => Mathf.Abs(footX - lo) <= Mathf.Abs(hi - footX) ? 1f : -1f;

        /// <summary>
        /// ⑤-3 ★★ <b>좌우 방향이 매 발동 추첨된다</b>. 예전 규칙은 <b>캐릭터 위치의 순수 함수</b>라
        /// "지금 x를 알면 방향이 100% 정해졌다" — 그게 "매번 같은 자리"의 절반이었다.
        ///
        /// <para>그래서 판정도 <b>조건부</b>로 한다: 같은 (발위치, 사거리)에서 dirRoll만 바꿔
        /// 방향이 실제로 갈리는가. 구 규칙은 정의상 갈릴 수 없다(대조군).</para>
        /// </summary>
        [Test]
        public void 좌우_방향이_매_발동_추첨된다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float g = cfg.archeryMaxDistanceSpanFraction;
                float screenFloor = ScreenWidthWorld * cfg.archeryMinTargetDistanceScreenFraction;
                float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
                const float scale = 0.75f;

                int live = 0, forced = 0, right = 0, unequalCost = 0, samples = 0, agreeLegacy = 0;
                var targetXs = new System.Collections.Generic.List<float>();

                for (int i = 0; i <= 24; i++)
                {
                    float footX = Mathf.Lerp(-VisibleHalfWidth * 0.96f, VisibleHalfWidth * 0.96f, i / 24f);
                    float legacy = LegacyFacing(footX, lo, hi);

                    for (int k = 0; k < 40; k++)
                    {
                        float roll = k / 39f;
                        ArcheryDirector.Placement a = PlaceScaled(cfg, scale, footX, lo, hi, f, roll,
                            maxSpanFraction: g, screenFloor: screenFloor, dirRoll01: 0f);
                        ArcheryDirector.Placement b = PlaceScaled(cfg, scale, footX, lo, hi, f, roll,
                            maxSpanFraction: g, screenFloor: screenFloor, dirRoll01: 0.999f);
                        Assert.IsTrue(a.Ok && b.Ok);
                        Assert.AreEqual(a.Distance, b.Distance, Eps,
                            "방향 추첨값이 **사거리**를 바꿨습니다 — 두 추첨이 섞였습니다.");

                        targetXs.Add(a.TargetX);
                        targetXs.Add(b.TargetX);
                        samples += 2;
                        if (a.Facing > 0f) right++;
                        if (b.Facing > 0f) right++;
                        // ★ 대조군 — 구 규칙은 발위치만 읽으므로 «footX를 알면 방향을 100% 맞힐 수 있다».
                        //   새 규칙에서 이 적중률이 떨어진 만큼이 곧 «추첨이 실제로 일어난 양»이다.
                        if (a.Facing == legacy) agreeLegacy++;
                        if (b.Facing == legacy) agreeLegacy++;

                        if (a.Facing == b.Facing) { forced++; continue; }
                        live++;

                        // ★★ 이 라운드의 핵심 불변식 — 추첨은 **도보 비용이 동률일 때만** 일어난다.
                        //    (실측: 6688/6688, 최대 차 0.000000H) 그래서 방향을 무작위로 골라도
                        //    도보가 한 걸음도 늘지 않는다.
                        float ta = Mathf.Abs(a.StandX - footX), tb = Mathf.Abs(b.StandX - footX);
                        if (Mathf.Abs(ta - tb) > Eps) unequalCost++;
                    }
                }

                // ★ 네거티브 컨트롤 — 구 규칙의 적중률은 정의상 100%다. 새 규칙에서 그것이 떨어져야
                //   "위치가 방향을 정한다"가 실제로 깨진 것이다(80.6% 실측).
                float agreement = agreeLegacy / (float)samples;
                Assert.Less(agreement, 0.90f,
                    $"새 규칙의 방향이 구 규칙(발위치의 순수 함수)과 {agreement:P1} 일치합니다 — " +
                    "사실상 예전처럼 위치가 방향을 정하고 있다는 뜻입니다.");
                Assert.Greater(live, 0, "어떤 (발위치, 사거리)에서도 방향이 갈리지 않았습니다 — 추첨이 죽었습니다.");
                float liveRatio = live / (float)(live + forced);
                Assert.Greater(liveRatio, 0.25f,
                    $"방향이 실제로 추첨되는 지점이 표본의 {liveRatio:P1}뿐입니다 — " +
                    "사실상 예전처럼 위치가 방향을 정합니다.");
                Assert.AreEqual(0, unequalCost,
                    $"방향이 갈린 {live}건 중 {unequalCost}건에서 두 방향의 접근 도보가 다릅니다 — " +
                    "추첨이 '더 많이 걷는 쪽'을 고를 수 있다는 뜻이고, 그러면 화면을 가로지르는 " +
                    "행진(2026-08-31 신고)이 재발합니다.");

                float rightRatio = right / (float)(2 * (live + forced));
                Assert.Greater(rightRatio, 0.35f, $"오른쪽 비율 {rightRatio:P1} — 좌우가 한쪽으로 쏠렸습니다.");
                Assert.Less(rightRatio, 0.65f, $"오른쪽 비율 {rightRatio:P1} — 좌우가 한쪽으로 쏠렸습니다.");

                // 과녁 x가 실제로 흩어지는가 — "매번 같은 자리에 생기지 않을 것"(사용자 원문).
                targetXs.Sort();
                float spread = targetXs[targetXs.Count - 1] - targetXs[0];
                Assert.Greater(spread, VisibleHalfWidth * 1.5f,
                    $"과녁 x의 퍼짐이 {spread:F2}유닛뿐입니다(실측 34.87유닛) — 매번 비슷한 자리에 생깁니다.");
                int leftHalf = 0;
                foreach (float x in targetXs) if (x < 0f) leftHalf++;
                Assert.Greater(leftHalf / (float)targetXs.Count, 0.25f, "과녁이 화면 왼쪽 절반에 거의 안 생깁니다.");
                Assert.Less(leftHalf / (float)targetXs.Count, 0.75f, "과녁이 화면 오른쪽 절반에 거의 안 생깁니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ⑤-4 ★ <b>방향 추첨이 비침해·연출 제약을 뚫지 않는다</b> — 이 라운드의 가장 큰 위험이었다.
        /// 방향을 무작위로 고르면 (가) 과녁이 구간 끝에 못박히거나 (나) 캐릭터가 화면을 가로질러
        /// 행진해 <c>archeryApproachTimeoutSeconds</c>에 걸릴 수 있다. 발판 12종 × 배율 4종 ×
        /// 발위치 5종 × 추첨 120회 × dirRoll 6종을 전수로 훑는다.
        ///
        /// <para>대조군은 <see cref="LegacyFacing"/>(구 방향 규칙) + <b>같은 밴드</b>다 —
        /// 밴드 변경과 방향 변경을 섞으면 무엇이 나빠졌는지 못 가른다.</para>
        /// </summary>
        [Test]
        public void 방향_추첨이_못박힘과_장거리_행진을_만들지_않는다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float g = cfg.archeryMaxDistanceSpanFraction;
                float screenFloor = ScreenWidthWorld * cfg.archeryMinTargetDistanceScreenFraction;
                float[] rolls = Rolls(31337, 120);
                float[] dirRolls = { 0f, 0.25f, 0.4999f, 0.5f, 0.75f, 1f };
                int pinnedNew = 0, pinnedLegacyDir = 0, cases = 0, legacyDirCases = 0;
                float worstTravelNew = 0f, worstTravelLegacyDir = 0f;

                foreach (float scale in Scales)
                {
                    float h = StickConfig.BaselineCharacterTotalHeight * scale;
                    float charInset = h * ArcheryDirector.CharacterEdgeInsetRatio;
                    float targetInset = h * cfg.archeryTargetRadiusRatio
                                        + h * ArcheryDirector.TargetEdgeInsetRatio;
                    float backStep = h * ArcheryDirector.BackStepRatio;

                    foreach (float widthPt in FootholdWidthsPt)
                    {
                        float half = widthPt / PtPerUnit * 0.5f;
                        foreach (float footFrac in new[] { -0.48f, -0.25f, 0f, 0.25f, 0.48f })
                        {
                            float footX = half * 2f * footFrac;
                            float legacyFacing = LegacyFacing(footX, -half, half);

                            foreach (float roll in rolls)
                            {
                                // 포기 판정은 dirRoll에 완전히 불변이어야 한다.
                                bool? okRef = null;
                                foreach (float dir in dirRolls)
                                {
                                    ArcheryDirector.Placement n = PlaceScaled(cfg, scale, footX, -half, half,
                                        f, roll, maxSpanFraction: g, screenFloor: screenFloor, dirRoll01: dir);
                                    if (okRef == null) okRef = n.Ok;
                                    Assert.AreEqual(okRef.Value, n.Ok,
                                        $"배율 {scale}, {widthPt:F0}pt, footX {footX:F2}, roll {roll:F4}: " +
                                        $"dirRoll {dir:F4}에서 발동 가부가 뒤집혔습니다 — " +
                                        "회색 처리(고정 roll)와 실제 실행이 어긋납니다.");
                                    if (!n.Ok) continue;
                                    cases++;

                                    float edge = n.Facing > 0f
                                        ? (half - targetInset) - n.TargetX
                                        : n.TargetX - (-half + targetInset);
                                    if (edge < 0.01f * h) pinnedNew++;
                                    worstTravelNew = Mathf.Max(worstTravelNew,
                                        Mathf.Abs(n.StandX - footX) / h);

                                    Assert.GreaterOrEqual(n.TargetX, -half + targetInset - Eps,
                                        "방향 추첨이 과녁을 구간 왼쪽 밖으로 밀었습니다.");
                                    Assert.LessOrEqual(n.TargetX, half - targetInset + Eps,
                                        "방향 추첨이 과녁을 구간 오른쪽 밖으로 밀었습니다.");
                                    Assert.GreaterOrEqual(n.StandX, -half + charInset - Eps);
                                    Assert.LessOrEqual(n.StandX, half - charInset + Eps);
                                    Assert.AreEqual(n.Facing, Mathf.Sign(n.TargetX - n.StandX), Eps,
                                        "바라보는 방향과 과녁 방향이 반대입니다(등 뒤로 쏘는 그림).");
                                }

                                // ★ 대조군 — 같은 밴드에 **구 방향 규칙**을 적용했다면 어땠는가.
                                if (okRef != true) continue;
                                ArcheryDirector.Placement probe = PlaceScaled(cfg, scale, footX, -half, half,
                                    f, roll, maxSpanFraction: g, screenFloor: screenFloor, dirRoll01: 0f);
                                float d = probe.Distance;
                                float standLo = -half + charInset, standHi = half - charInset;
                                float targetLo = -half + targetInset, targetHi = half - targetInset;
                                float slotLo = legacyFacing > 0f ? standLo : Mathf.Max(standLo, targetLo + d);
                                float slotHi = legacyFacing > 0f ? Mathf.Min(standHi, targetHi - d) : standHi;
                                if (slotHi < slotLo)
                                {
                                    if (slotLo - slotHi > Eps) continue;
                                    slotLo = slotHi = (slotLo + slotHi) * 0.5f;
                                }
                                float legacyStand = Mathf.Clamp(footX - legacyFacing * backStep, slotLo, slotHi);
                                float legacyTarget = legacyStand + legacyFacing * d;
                                float legacyEdge = legacyFacing > 0f
                                    ? targetHi - legacyTarget : legacyTarget - targetLo;
                                legacyDirCases++;
                                if (legacyEdge < 0.01f * h) pinnedLegacyDir++;
                                worstTravelLegacyDir = Mathf.Max(worstTravelLegacyDir,
                                    Mathf.Abs(legacyStand - footX) / h);
                            }
                        }
                    }
                }

                Assert.Greater(cases, 10000, "표본이 너무 적습니다.");
                Assert.Greater(legacyDirCases, 1000, "대조군 표본이 너무 적습니다 — 비교가 공허합니다.");

                // ★ 못박힘 — 구 방향 규칙 대비 **실질적으로** 늘지 않아야 한다.
                //   경계 한 점(footX − backStep == targetHi − distance)에서 과녁이 여백선에 «닿는»
                //   경우가 있어 완전 동일은 아니다. 실측 8.156% vs 8.122%(+0.03%p)이므로 0.5%p를 문턱으로 둔다.
                float pinNew = pinnedNew / (float)cases;
                float pinOld = pinnedLegacyDir / (float)legacyDirCases;
                Assert.LessOrEqual(pinNew, pinOld + 0.005f,
                    $"과녁이 구간 끝에 닿는 비율이 구 방향 규칙 {pinOld:P2} -> 방향 추첨 {pinNew:P2}로 " +
                    "늘었습니다 — 2026-08-31 신고('무조건 과녁이 화면 끝에만 생김')의 재발입니다.");

                // ★ 접근 도보 — 방향 추첨이 걷는 거리를 늘리면 안 되고(불변식),
                //   그 시간이 archeryApproachTimeoutSeconds 안이어야 한다.
                //   보행 속도는 배율에 비례하므로(StickConfig.ResolveWalkSpeed) H/초는 배율 무관이다.
                float walkHPerSecond = cfg.ResolveWalkSpeed()
                    / (StickConfig.BaselineCharacterTotalHeight * cfg.ResolveCharacterScale());
                float worstSeconds = worstTravelNew / walkHPerSecond;

                Assert.LessOrEqual(worstTravelNew, worstTravelLegacyDir + Eps,
                    $"최대 접근 도보가 구 방향 규칙 {worstTravelLegacyDir:F4}H -> 방향 추첨 " +
                    $"{worstTravelNew:F4}H로 늘었습니다 — 추첨이 '화면을 가로지르는 행진'을 만듭니다.");
                Assert.Less(worstSeconds, cfg.archeryApproachTimeoutSeconds * 0.5f,
                    $"최악의 접근 도보가 {worstSeconds:F2}초로 타임아웃 " +
                    $"{cfg.archeryApproachTimeoutSeconds:F0}초의 절반을 넘습니다 — 타임아웃으로 " +
                    "'덜 걸은 자리에서 쏘는' 사고가 납니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ⑤-5 출하 설정 3종의 <b>자기 정합성</b>. 값을 베끼지 않고 근거를 재계산한다.
        /// </summary>
        [Test]
        public void 출하_화면비례_설정이_설계_근거를_만족한다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float s = cfg.archeryMinTargetDistanceScreenFraction;
                float g = cfg.archeryMaxDistanceSpanFraction;
                float ucap = cfg.archeryMaxDistanceHardCapRatio;

                Assert.Greater(s, 0f, "화면 비례 바닥이 꺼진 채로 출하됩니다(킬 스위치 ON).");
                Assert.GreaterOrEqual(s, 0.15f, "사용자가 제시한 대역 '화면 폭의 15~25%'의 아래를 벗어납니다.");
                Assert.LessOrEqual(s, 0.25f, "사용자가 제시한 대역 '화면 폭의 15~25%'의 위를 벗어납니다.");
                Assert.LessOrEqual(s, ArcheryDirector.MaxMinTargetDistanceScreenFraction);

                Assert.Greater(g, 0f, "폭 비례 상한이 꺼져 있으면 화면 비례 바닥이 구조적으로 무효입니다.");
                Assert.LessOrEqual(g, ArcheryDirector.MaxMaxDistanceSpanFraction,
                    "폭 비례 상한이 '무조건 화면 끝' 재발 방지선을 넘었습니다.");

                // ★ g가 s를 실제로 통과시키는가 — 하한은 f × 상한으로 클램프된다.
                //   화면 전폭에서 상한 ≈ g × 화면폭이므로 요구는 f × g ≥ s 다.
                float f = cfg.archeryMinDistanceSpanFraction;
                Assert.GreaterOrEqual(f * g, s - 0.02f,
                    $"f×g = {f * g:F3} 인데 s = {s:F3} 입니다 — 붕괴 클램프가 화면 비례 바닥을 " +
                    "통째로 먹어 설정이 아무 일도 하지 않습니다(g를 올리거나 s를 내려야 합니다).");

                // ★ Ucap은 '한 발 = 한 박자' 임계 아래여야 한다 — 상수를 계산해서 비교한다.
                //   비행시간 T(d) = archeryArrowFlightSeconds × √(d / 기준사거리)
                //   임계 = 회복 + 당김 + 조준.
                float beat = cfg.archeryRecoverSeconds + cfg.archeryDrawSeconds + cfg.archeryAimHoldSeconds;
                float beatLimitRatio = cfg.archeryTargetDistanceRatio
                                       * Mathf.Pow(beat / cfg.archeryArrowFlightSeconds, 2f);
                Assert.Less(ucap, beatLimitRatio,
                    $"Ucap {ucap}H가 '한 발 = 한 박자' 임계 {beatLimitRatio:F2}H 이상입니다 — " +
                    "앞 화살이 착탄하기 전에 다음 화살이 떠나 착탄음과 발사음이 겹칩니다.");
                Assert.Greater(ucap, cfg.archeryMaxTargetDistanceRatio,
                    "Ucap이 기준 상한 이하라 폭 비례 상한이 아무 효과도 없습니다.");

                // ★ 어떤 배율에서도 최대 사거리가 화면 밖으로 나가지 않는다.
                foreach (float scale in Scales)
                {
                    float h = StickConfig.BaselineCharacterTotalHeight * scale;
                    float span = 2f * VisibleHalfWidth
                                 - h * ArcheryDirector.CharacterEdgeInsetRatio
                                 - (h * cfg.archeryTargetRadiusRatio
                                    + h * ArcheryDirector.TargetEdgeInsetRatio);
                    ExpectedBand(cfg, scale, span, ScreenWidthWorld, out float lo, out float hi);
                    Assert.Less(hi, ScreenWidthWorld * 0.5f,
                        $"배율 {scale}: 최대 사거리 {hi / ScreenWidthWorld:P1}W가 화면 폭의 절반 이상입니다 — " +
                        "'적당히 먼 거리'(2026-08-31)를 지나쳐 다시 화면 끝으로 갑니다.");
                    Assert.GreaterOrEqual(lo / ScreenWidthWorld, 0.15f,
                        $"배율 {scale}: 최소 사거리가 화면 폭의 {lo / ScreenWidthWorld:P1}입니다.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// ⑤-6 밴드 하한의 <b>근거 보고</b>(<see cref="ArcheryDirector.FloorSource"/>)가 실제와 맞는가.
        /// 실기 로그가 이 값을 찍으므로, 여기가 거짓말하면 <b>실기 판정 전체가 오염된다</b>.
        /// </summary>
        [Test]
        public void 밴드_하한_근거_보고가_실제와_일치한다()
        {
            var cfg = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                float f = cfg.archeryMinDistanceSpanFraction;
                float g = cfg.archeryMaxDistanceSpanFraction;
                float screenFloor = ScreenWidthWorld * cfg.archeryMinTargetDistanceScreenFraction;
                int abs = 0, spanSrc = 0, screenSrc = 0;

                foreach (float scale in Scales)
                foreach (float widthPt in FootholdWidthsPt)
                {
                    float h = StickConfig.BaselineCharacterTotalHeight * scale;
                    float half = widthPt / PtPerUnit * 0.5f;
                    ArcheryDirector.Placement p = PlaceScaled(cfg, scale, 0f, -half, half, f, 0f,
                        maxSpanFraction: g, screenFloor: screenFloor);
                    if (!p.Ok) continue;

                    float absFloor = h * cfg.archeryMinTargetDistanceRatio;
                    float u0 = h * cfg.archeryMaxTargetDistanceRatio;
                    float wantSpan = f * Mathf.Min(p.BandHi, u0);
                    float wantScreen = Mathf.Min(screenFloor, f * p.BandHi);
                    string ctx = $"배율 {scale}, 발판 {widthPt:F0}pt";

                    switch (p.BandLoSource)
                    {
                        case ArcheryDirector.FloorSource.Absolute:
                            abs++;
                            Assert.AreEqual(absFloor, p.BandLo, Eps, $"{ctx}: 절대 바닥이라는데 값이 다릅니다.");
                            break;
                        case ArcheryDirector.FloorSource.Span:
                            spanSrc++;
                            Assert.AreEqual(wantSpan, p.BandLo, Eps, $"{ctx}: 폭 비례라는데 값이 다릅니다.");
                            Assert.GreaterOrEqual(wantSpan, wantScreen - Eps,
                                $"{ctx}: 폭 비례로 보고했는데 화면 비례가 더 큽니다.");
                            break;
                        default:
                            screenSrc++;
                            Assert.AreEqual(wantScreen, p.BandLo, Eps, $"{ctx}: 화면 비례라는데 값이 다릅니다.");
                            // ★ 동률은 Screen이 아니다 — **엄격히** 커야 한다. 2026-09-06 실기에서
                            //   동률 14/14가 "★화면비례"로 찍혀 "처방이 걸렸다"로 읽힐 뻔했다.
                            //   그 동률의 정체는 화면 비례 바닥이 f×상한에 눌려 폭 비례와 같아진 것,
                            //   즉 **아무 일도 안 한 것**이었다.
                            Assert.Greater(wantScreen, wantSpan + Eps,
                                $"{ctx}: 화면 비례로 보고했는데 폭 비례와 동률이거나 더 작습니다 — " +
                                "로그가 '처방이 걸렸다'고 거짓말을 합니다.");
                            break;
                    }
                }

                // 세 근거가 표본에 전부 등장해야 이 판정이 의미를 갖는다(빈 집합 방지).
                Assert.Greater(abs, 0, "표본에 '절대 바닥'이 한 건도 없습니다 — 좁은 발판이 빠졌습니다.");
                Assert.Greater(screenSrc, 0,
                    "표본에 '화면 비례'가 한 건도 없습니다 — 이번 라운드의 처방이 어디에서도 안 걸렸다는 뜻입니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }
    }
}
