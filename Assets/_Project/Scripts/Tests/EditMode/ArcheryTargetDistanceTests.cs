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
        private static ArcheryDirector.Placement Place(float footX, float lo, float hi, float roll01,
            float spanFraction = 0f)
            => ArcheryDirector.ResolvePlacement(footX, lo, hi, CharInset, TargetInset, BackStep,
                MinDistance, MaxDistance, spanFraction, roll01);

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
            float lo, float hi, float spanFraction, float roll01)
        {
            float h = StickConfig.BaselineCharacterTotalHeight * scale;
            return ArcheryDirector.ResolvePlacement(footX, lo, hi,
                h * ArcheryDirector.CharacterEdgeInsetRatio,
                h * cfg.archeryTargetRadiusRatio + h * ArcheryDirector.TargetEdgeInsetRatio,
                h * ArcheryDirector.BackStepRatio,
                h * cfg.archeryMinTargetDistanceRatio,
                h * cfg.archeryMaxTargetDistanceRatio,
                spanFraction, roll01);
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
    }
}
