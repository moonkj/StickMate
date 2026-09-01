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
    /// 출하 배율(characterScale 0.75)에서 왔다. BattleMinigamePlacementTests와 같은 기준이다.
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

        private static ArcheryDirector.Placement Place(float footX, float lo, float hi, float roll01)
            => ArcheryDirector.ResolvePlacement(footX, lo, hi, CharInset, TargetInset, BackStep,
                MinDistance, MaxDistance, roll01);

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

        [Test]
        public void 오른쪽_끝에_서_있으면_왼쪽으로_쏜다()
        {
            float lo = -VisibleHalfWidth, hi = VisibleHalfWidth;
            ArcheryDirector.Placement p = Place(hi - 1f, lo, hi, 0.5f);
            Assert.IsTrue(p.Ok);
            Assert.AreEqual(-1f, p.Facing, Eps,
                "오른쪽 끝에 서서 오른쪽으로 쏘려 하고 있습니다 — 과녁이 화면 밖으로 나갑니다.");
            Assert.Less(p.TargetX, p.StandX, "왼쪽을 보는데 과녁이 오른쪽에 있습니다.");

            ArcheryDirector.Placement q = Place(lo + 1f, lo, hi, 0.5f);
            Assert.AreEqual(1f, q.Facing, Eps, "왼쪽 끝에서 왼쪽으로 쏘려 하고 있습니다.");
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
            Assert.AreEqual(1f, q.Facing, Eps, "좌우 대칭 위치에서는 오른쪽을 향해 쏜다(기존 규칙).");
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
    }
}
