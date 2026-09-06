using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 호버 이름표가 <b>형제 버튼을 덮지 않는다</b> — 2026-09-03 러너 실패의 회귀 잠금.
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나 (실측)
    /// ============================================================================
    /// <code>
    /// GearMenuHoverLabelTests.TheHoverLabelNeverCoversAnotherButton
    ///   [오늘 할일] 이름표가 4번 버튼([앱 종료])을 5.9px 덮습니다
    ///   Expected: greater than 22.0f   But was: 16.0699158f
    /// </code>
    /// <b>세로 일렬 폴백이 아니었다.</b> 배치 모드는 <b>정상 부채꼴</b>이었고(축소 폴백도 아니다 —
    /// 판정 반지름이 22.0 = Ø44 그대로였다), <b>기본 톱니 위치</b>에서 났다. 화면 크기를 640×480부터
    /// 2560×1440까지 바꿔도 <b>−5.93pt로 동일</b>하다(톱니가 우상단 고정이라 기하가 같다).
    /// 기어 위치 격자 스윕에서는 <b>61~71%</b>가 위반이었다.
    ///
    /// 원인은 둘이고 <b>둘 다 이번 라운드가 만든 것이 아니다</b>:
    /// <list type="number">
    ///   <item><b>방향</b> — 배치 사다리 ③단계(평행이동)는 버튼만 옮기고 기어 중심은 안 옮기는데,
    ///     이름표가 방향을 <c>버튼중심 − 기어중심</c>으로 쟀다. 기본 위치는 평행이동
    ///     (−6, −36.2)가 걸리는 자리다.</item>
    ///   <item><b>반지름</b> — 36-4의 「폭 무관 보장」은 <c>궤도 111 + 버튼반경 + 간격</c>이 전제인데,
    ///     위성이 궤도 <b>168</b>로 나가면서 호 이름표가 놓이던 반지름(≈170pt)이
    ///     <b>위성 궤도와 같은 자리</b>가 됐다. 4칸 시절엔 거기 아무것도 없었다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 왜 EditMode 순수 계산인가
    /// ============================================================================
    /// PlayMode는 <b>한 화면 한 위치</b>만 본다. 위 결함은 기어 위치 격자의 60% 이상에서 나는데도
    /// PlayMode 한 판이 초록이면 초록으로 보였다. 여기서는 <see cref="GearRadialMenuWidget.HoverLabelCenterPoints"/>
    /// (public static 순수 함수)에 <b>화면 6종 × 기어 위치 25곳 × 평행이동 5종 × 기준각 13종 × 알약 폭 5종</b>
    /// (≈ 17만 개 배치)을 직접 물려 전수로 잰다 —
    /// <see cref="GearRadialMenuWidget.SlotOffsetDegrees"/>를 public으로 만든 것과 같은 이유다.
    ///
    /// <para>★ <b>스윕은 사다리가 실제로 만드는 배치만 본다.</b> 기준각은
    /// <see cref="GearRadialMenuWidget.Snap45"/>에 회전 탐색 오프셋을 더해 만들고, <b>버튼이 화면 밖인
    /// 배치는 뺀다</b>(사다리가 그런 배치를 채택하지 않는다). 각도를 임의로 뿌리면 제품에서 일어나지
    /// 않는 겹침으로 빨개진다 — 그건 정보가 아니라 소음이다.</para>
    ///
    /// <para>★ <b>화면 클램프가 걸린 이름표는 세지 않는다.</b> 프로덕션이 그 경우 «화면 밖으로 나간
    /// 이름표는 아예 읽을 수 없으므로 그쪽이 먼저다»로 <b>보장을 명시적으로 포기</b>한 자리다.
    /// 실측상 640×480 이상에서는 이 제외가 <b>한 번도 발동하지 않는다</b>(제외는 480×360 이하에서만
    /// 생긴다) — 즉 실기 해상도에서는 보장이 무조건부다.</para>
    ///
    /// ★ 판정은 <c>GearMenuHoverLabelTests</c>가 화면에서 쓰는 것과 <b>같은 자</b>다:
    ///   원(중심 = 버튼, 반경 = 지름/2) ↔ 축 정렬 사각형(이름표)의 실제 거리.
    /// </summary>
    public sealed class GearMenuHoverLabelGeometryTests
    {
        private const string LogPrefix = "[이름표기하-TEST]";

        /// <summary>이름표 폭은 런타임 폰트가 정한다 — 그래서 <b>폭을 스윕한다</b>.
        /// 36-4가 주장한 것이 «폭에 의존하지 않는 보장»이므로, 폭 하나만 재면 그 주장을 못 잰다.</summary>
        private static readonly float[] PillWidths = { 30f, 44f, 57f, 70f, 92f };

        /// <summary>기어를 화면 어디에나 끌어다 놓을 수 있으므로 위치도 스윕한다.</summary>
        private static readonly float[] GearFractions = { 0.1f, 0.25f, 0.5f, 0.75f, 0.9f };

        /// <summary>배치 사다리 ③단계가 거는 평행이동 — <b>기어 중심과 배열 원점이 갈라지는</b> 그 상태.
        /// (−6, −36.2)는 <b>기본 톱니 위치에서 실제로 걸리는 값</b>이고, 그 자리가 이번 결함의 현장이다.</summary>
        private static readonly Vector2[] LayoutShifts =
        {
            Vector2.zero, new Vector2(-6f, -36.2f), new Vector2(9f, 0f),
            new Vector2(0f, 24f), new Vector2(-48f, 48f),
        };

        private static readonly Vector2[] Screens =
        {
            new Vector2(640f, 480f), new Vector2(1024f, 768f), new Vector2(1280f, 720f),
            new Vector2(1512f, 982f), new Vector2(1920f, 1080f), new Vector2(2560f, 1440f),
        };

        private static float SideMargin
            => GearRadialMenuWidget.EffectiveMarginPoints(GearRadialMenuWidget.ScreenMarginPoints, 0f);

        private static float TopMargin
            => GearRadialMenuWidget.EffectiveMarginPoints(GearRadialMenuWidget.TopMarginPoints, 0f);

        // ============================================================================
        // 판정기 — 본안과 역대조가 <b>같은 함수</b>를 쓴다
        // ============================================================================

        /// <summary>이름표를 놓는 두 규칙. <c>Fixed</c>가 지금 프로덕션, <c>Legacy</c>가 고치기 전이다.</summary>
        private enum Rule { Fixed, Legacy }

        private static Vector2 PlaceLabel(Rule rule, Vector2 anchor, Vector2 origin, Vector2 gear,
            float ring, float diameter, bool column, Vector2 size, Vector2 screen,
            float marginOverride = float.NaN)
        {
            // ★ 2026-09-05 qa-regression 지적 — 클램프 유무 판정용 "무한 화면" 호출은 화면 크기가
            // 커지면 상한(maxX/maxY = screen - margin - half)만 밀어낼 뿐, 하한(minX/minY =
            // margin + half)은 화면 크기와 무관해 그대로 남는다. 그래서 좌/하단 여백에 걸린
            // 클램프는 "안 걸렸다"로 오판됐다(형제 버튼을 실제로 덮지 않는데도 검사에서 빠지지
            // 않고 통과해 버리는 게 아니라, 반대로 판정 대상에서 빠지지 않아 거짓 위반으로 잡혔다).
            // marginOverride로 하한도 함께 밀어내면 두 방향 모두 클램프 불가능한 참조가 된다.
            float left = float.IsNaN(marginOverride) ? SideMargin : marginOverride;
            float top = float.IsNaN(marginOverride) ? TopMargin : marginOverride;
            if (rule == Rule.Fixed)
            {
                return GearRadialMenuWidget.HoverLabelCenterPoints(anchor, origin, ring, diameter,
                    column, size, screen, left, left, left, top);
            }

            // 고치기 전 규칙 재현: 방향을 <b>기어 중심</b>에서 재고, 알약을 <b>자기 버튼에</b> 붙인다.
            // (링 = 자기 버튼까지의 거리 + 반지름 + 간격 을 그 버튼 기준으로 쓰는 것과 같다.)
            float legacyRing = (anchor - gear).magnitude + diameter * 0.5f + GearRadialMenuWidget.HoverLabelGapPoints;
            return GearRadialMenuWidget.HoverLabelCenterPoints(anchor, gear, legacyRing, diameter,
                column, size, screen, left, left, left, top);
        }

        /// <summary>이 배치에서 «이름표 ↔ 다른 버튼 원»의 최소 여유(pt). 음수면 덮은 것이다.
        /// <para><paramref name="skippedByClamp"/>: 화면 클램프가 걸린 이름표는 <b>세지 않는다</b> —
        /// 그 경우는 프로덕션이 «화면 밖으로 나간 이름표는 아예 읽을 수 없으므로 그쪽이 먼저다»로
        /// <b>명시적으로 보장을 포기한 자리</b>다. 세면 «고칠 수 없는 것»으로 빨개진다.</para></summary>
        private static float WorstClearance(Rule rule, IList<Vector2> centers, Vector2 origin, Vector2 gear,
            float diameter, bool column, Vector2 screen, float pillWidth,
            out string worstPair, out int judged, out int skippedByClamp)
        {
            float ring = GearRadialMenuWidget.HoverLabelRingRadius(FarthestFrom(origin, centers), diameter);
            var size = new Vector2(pillWidth, GearRadialMenuWidget.HoverLabelHeightPoints);
            float radius = diameter * 0.5f;
            float worst = float.MaxValue;
            worstPair = "—";
            judged = 0;
            skippedByClamp = 0;

            for (int i = 0; i < centers.Count; i++)
            {
                Vector2 label = PlaceLabel(rule, centers[i], origin, gear, ring, diameter, column, size, screen);
                // 클램프가 걸렸는가 — <b>같은 프로덕션 함수</b>를 클램프가 물 수 없는 큰 화면으로 한 번 더
                // 불러 비교한다(클램프 식을 테스트가 베껴 적지 않는다).
                Vector2 unclamped = PlaceLabel(rule, centers[i], origin, gear, ring, diameter, column,
                    size, new Vector2(1e6f, 1e6f), marginOverride: -1e6f);
                if ((label - unclamped).sqrMagnitude > 1e-6f) { skippedByClamp++; continue; }

                judged++;
                for (int j = 0; j < centers.Count; j++)
                {
                    if (j == i) continue;
                    float dx = Mathf.Max(Mathf.Max(label.x - size.x * 0.5f - centers[j].x,
                        centers[j].x - (label.x + size.x * 0.5f)), 0f);
                    float dy = Mathf.Max(Mathf.Max(label.y - size.y * 0.5f - centers[j].y,
                        centers[j].y - (label.y + size.y * 0.5f)), 0f);
                    float clearance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    if (clearance >= worst) continue;
                    worst = clearance;
                    worstPair = $"[{GearRadialMenuWidget.NameOf(i)}] 이름표 -> [{GearRadialMenuWidget.NameOf(j)}] 버튼";
                }
            }
            return worst;
        }

        private static float FarthestFrom(Vector2 origin, IList<Vector2> centers)
        {
            float f = 0f;
            for (int i = 0; i < centers.Count; i++) f = Mathf.Max(f, (centers[i] - origin).magnitude);
            return f;
        }

        /// <summary>배치 사다리의 계약 — <b>모든 버튼의 클램프 상자가 화면 안</b>. 이걸 안 거르면
        /// 사다리가 <b>절대로 고르지 않을</b> 배치(버튼이 화면 밖인 상태)까지 재게 된다.</summary>
        private static bool AllButtonsOnScreen(IList<Vector2> centers, float diameter, Vector2 screen)
        {
            for (int i = 0; i < centers.Count; i++)
            {
                Rect b = GearRadialMenuWidget.ButtonClampBox(centers[i], diameter);
                if (b.xMin < SideMargin || b.yMin < SideMargin
                    || b.xMax > screen.x - SideMargin || b.yMax > screen.y - TopMargin) return false;
            }
            return true;
        }

        private static List<Vector2> FanCenters(Vector2 origin, float baseDegrees)
        {
            var list = new List<Vector2>(GearRadialMenuWidget.ButtonCount);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                list.Add(GearRadialMenuWidget.SlotCenterPoints(origin, baseDegrees, i));
            }
            return list;
        }

        // ============================================================================
        // ★★ 2026-09-06 — 역대조가 재현해야 하는 것은 「옛 규칙」이 <b>아니라</b> 「옛 기하」다
        // ============================================================================
        // 위성이 폐지되면서 다섯 슬롯이 <b>한 궤도</b>가 됐고, 그러자 옛 이름표 규칙(자기 버튼 기준
        // 링)과 현행 규칙(전역 링)이 <b>같은 반지름</b>을 낸다 — 즉 옛 규칙만으로는 더 이상 겹침을
        // 못 만든다(실측: 168,508개 배치에서 위반 0). 그건 «대조가 죽었다»가 아니라
        // <b>«그 결함 부류가 구조적으로 사라졌다»</b>는 뜻이고, 그 사실 자체를 잠가야 한다.
        //
        // 그래서 역대조는 이제 <b>옛 기하 + 옛 규칙</b>을 함께 재현한다: 호 4칸(±45/±15°, 궤도 111pt)
        // + 축 위 위성 1칸(0°, 궤도 168pt). 그 조합이 2026-09-03 러너 실패
        // («[오늘 할일] 이름표가 [앱 종료]를 5.9px 덮습니다») 그 자체다.
        //
        // ★ 이 숫자들은 <b>프로덕션에서 삭제된 상수</b>라 참조할 대상이 없다. 협업 프로토콜의
        //   «상수를 베끼지 마라»는 <b>살아 있는 값</b>에 대한 규칙이고, 여기 있는 것은 역사 기록이다.
        //   이 값이 «낡는» 경로는 존재하지 않는다 — 과거는 안 바뀐다.
        private const float LegacyArcOrbitPoints = 111f;
        private const float LegacySatelliteOrbitPoints = 168f;
        private const float LegacyStepDegrees = 30f;
        private const int LegacyArcCount = 4;

        private static List<Vector2> LegacyFanCenters(Vector2 origin, float baseDegrees)
        {
            var list = new List<Vector2>(GearRadialMenuWidget.ButtonCount);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                bool satellite = i >= LegacyArcCount;
                float offset = satellite ? 0f : ((LegacyArcCount - 1) * 0.5f - i) * LegacyStepDegrees;
                float r = satellite ? LegacySatelliteOrbitPoints : LegacyArcOrbitPoints;
                float a = (baseDegrees + offset) * Mathf.Deg2Rad;
                list.Add(origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return list;
        }

        private static List<Vector2> ColumnCenters(Vector2 origin, float sign)
        {
            var list = new List<Vector2>(GearRadialMenuWidget.ButtonCount);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                list.Add(GearRadialMenuWidget.ColumnSlotCenterPoints(origin, sign, i));
            }
            return list;
        }

        /// <summary>사다리가 실제로 시도하는 기준각들 — θ₀(<see cref="GearRadialMenuWidget.Snap45"/>)에
        /// 회전 탐색 오프셋을 더한 값. <b>각도를 임의로 뿌리지 않는다</b>: 사다리가 만들지 않는 각도에서
        /// 난 겹침은 제품에서 일어나지 않는다.</summary>
        private static IEnumerable<float> LadderAngles(Vector2 gear, Vector2 screen)
        {
            float b0 = GearRadialMenuWidget.Snap45(screen * 0.5f - gear);
            yield return b0;
            for (float o = GearRadialMenuWidget.RotationSearchStepDegrees;
                 o <= GearRadialMenuWidget.RotationSearchMaxDegrees + 0.01f;
                 o += GearRadialMenuWidget.RotationSearchStepDegrees)
            {
                yield return b0 + o;
                yield return b0 - o;
            }
        }

        // ============================================================================
        // ① 부채꼴 — 사다리가 실제로 만드는 배치 전수
        // ============================================================================

        [Test]
        public void 부채꼴에서_이름표가_어떤_형제_버튼도_덮지_않는다()
        {
            float worst = SweepFan(Rule.Fixed, out int judged, out int violations,
                out int skipped, out string worstWhere);

            Assert.Greater(judged, 5000,
                $"{LogPrefix} 잰 이름표가 {judged}개뿐입니다 — 스윕이 새고 있어 아래 판정은 무효입니다.");
            Assert.AreEqual(0, violations,
                $"{LogPrefix} 이름표가 형제 버튼을 덮는 배치가 {violations}건입니다. 최악 {-worst:F2}pt " +
                $"({worstWhere}). 불투명 알약이 버튼을 가리면 사용자는 자기가 무엇을 누르는지 " +
                "<b>보지 못한 채</b> 누릅니다 — 그 버튼이 [앱 종료]면 되돌릴 수 없습니다.");

            Debug.Log($"{LogPrefix} 부채꼴 이름표 {judged}개 최악 여유 {worst:F2}pt " +
                      $"(클램프로 건너뜀 {skipped}개, 가장 빠듯한 자리: {worstWhere}).");
        }

        // ============================================================================
        // ② 세로 일렬 폴백 — <b>강제로 유발</b>해서 잰다
        // ============================================================================

        /// <summary>
        /// ★ 폴백은 실기 배치에서 거의 안 걸려 <b>스윕 표본이 0</b>이다(실측). 그래서 여기서 직접 만든다.
        ///
        /// <para><b>폴백에서는 규칙이 다르다</b>: 다섯 버튼이 한 직선 위에 있어 반지름 규칙을 쓰면
        /// 이름표가 맨 끝 버튼 바깥 <b>한 자리에 겹쳐 쌓인다</b>. 그래서 가로로 민다. 세로로 밀면
        /// 간격 <see cref="GearRadialMenuWidget.ColumnFallbackSpacingPoints"/>짜리 이웃을 문다 —
        /// <b>4칸 시절에도 그랬다</b>(간격은 그때도 52였다). 이번에 함께 닫는다.</para>
        /// </summary>
        [Test]
        public void 세로일렬_폴백에서도_이름표가_형제_버튼을_덮지_않는다()
        {
            int judged = 0, violations = 0, skipped = 0;
            float worst = float.MaxValue;
            string worstWhere = "—";

            foreach (Vector2 screen in Screens)
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    foreach (float diameter in new[]
                             {
                                 GearRadialMenuWidget.ButtonDiameterPoints,
                                 GearRadialMenuWidget.ShrunkDiameterPoints,
                             })
                    {
                        foreach (float gx in new[] { 30f, screen.x * 0.5f, screen.x - 30f })
                        {
                            Vector2 gear = new Vector2(gx, sign > 0f ? 60f : screen.y - 60f);
                            List<Vector2> centers = ColumnCenters(gear, sign);
                            if (!AllButtonsOnScreen(centers, diameter, screen)) continue;

                            foreach (float width in PillWidths)
                            {
                                float c = WorstClearance(Rule.Fixed, centers, gear, gear, diameter,
                                    true, screen, width, out string pair, out int n, out int sk);
                                judged += n;
                                skipped += sk;
                                if (n == 0) continue;
                                if (c < 0f) violations++;
                                if (c >= worst) continue;
                                worst = c;
                                worstWhere = $"{screen.x:F0}x{screen.y:F0} sign{sign:+0;-0} " +
                                             $"Ø{diameter:F0} x{gx:F0} 폭{width:F0} — {pair}";
                            }
                        }
                    }
                }
            }

            Assert.Greater(judged, 100,
                $"{LogPrefix} 폴백 이름표를 {judged}개밖에 못 만들었습니다 — 이 판정은 무효입니다.");
            Assert.AreEqual(0, violations,
                $"{LogPrefix} 세로 일렬 폴백에서 이름표가 형제 버튼을 덮습니다(최악 {-worst:F2}pt, {worstWhere}).");

            Debug.Log($"{LogPrefix} 폴백 이름표 {judged}개 최악 여유 {worst:F2}pt " +
                      $"(클램프로 건너뜀 {skipped}개, {worstWhere}).");
        }

        // ============================================================================
        // ③ ★ 역대조 — 고치기 전 규칙은 <b>반드시 빨개져야 한다</b>
        // ============================================================================

        /// <summary>
        /// 위 두 초록이 «무엇이든 통과시키는 빈 조건»이 아님을 증명한다. <b>같은 스윕·같은 자</b>를 쓰고
        /// <b>옛 기하 + 옛 규칙</b>으로 바꾼다 — 그래도 통과한다면 이 파일은 아무것도 안 잠그고 있는 것이다.
        ///
        /// <para>★★ <b>2026-09-06 — 이 대조가 한 번 죽었다가 되살아났다.</b> 위성이 폐지되어 궤도가
        /// 하나가 되자 «옛 규칙»만으로는 168,508개 배치에서 <b>위반이 0건</b>이 됐고, 이 테스트가
        /// 빨개졌다. 원인은 대조가 <b>규칙만</b> 되돌리고 <b>기하는 현행</b>을 쓰고 있었기 때문이다 —
        /// 두 결함(2026-09-03 겹침 · 2026-09-06 «엉뚱한 버튼 옆»)의 실제 원인은 규칙이 아니라
        /// <b>궤도가 갈라져 있다</b>는 사실 하나였고, 그것이 사라지면 규칙 되돌림은 무해해진다.
        /// 그래서 대조를 <c>LegacyFanCenters</c>(호 111 + 위성 168)와 함께 돌린다.</para>
        /// </summary>
        [Test]
        public void 대조_옛_기하와_옛_규칙은_같은_스윕에서_형제를_덮는다()
        {
            float worst = SweepFan(Rule.Legacy, out int judged, out int violations,
                out _, out string worstWhere);

            Assert.Greater(judged, 5000, $"{LogPrefix} 대조 스윕이 비었습니다({judged}개).");
            Assert.Greater(violations, 0,
                $"{LogPrefix} ★대조 실패 — 고치기 전 규칙이 {judged}개 배치에서 <b>한 번도</b> 형제를 " +
                "덮지 않았습니다. 그렇다면 위 두 초록은 아무것도 증명하지 못합니다.");
            Assert.Less(worst, 0f,
                $"{LogPrefix} ★대조 실패 — 옛 규칙의 최악 여유가 {worst:F2}pt로 음수가 아닙니다.");

            Debug.Log($"{LogPrefix} 대조 통과 — 옛 규칙은 {judged}개 중 {violations}개 배치에서 형제를 " +
                      $"덮었습니다(최악 {worst:F2}pt, {worstWhere}).");
        }

        /// <summary>①과 ③이 <b>같은 함수</b>를 탄다 — 두 스윕이 갈라지면 대조가 대조가 아니다.</summary>
        private static float SweepFan(Rule rule, out int judged, out int violations,
            out int skipped, out string worstWhere)
        {
            judged = 0;
            violations = 0;
            skipped = 0;
            worstWhere = "—";
            float worst = float.MaxValue;

            foreach (Vector2 screen in Screens)
            {
                foreach (float fx in GearFractions)
                {
                    foreach (float fy in GearFractions)
                    {
                        var gear = new Vector2(screen.x * fx, screen.y * fy);
                        foreach (Vector2 shift in LayoutShifts)
                        {
                            Vector2 origin = gear + shift;
                            foreach (float baseDegrees in LadderAngles(gear, screen))
                            {
                                // ★ 규칙과 기하는 <b>한 쌍</b>이다 — 위 LegacyFanCenters 문단 참고.
                                List<Vector2> centers = rule == Rule.Legacy
                                    ? LegacyFanCenters(origin, baseDegrees)
                                    : FanCenters(origin, baseDegrees);
                                if (!AllButtonsOnScreen(centers, GearRadialMenuWidget.ButtonDiameterPoints,
                                        screen)) continue;

                                foreach (float width in PillWidths)
                                {
                                    float c = WorstClearance(rule, centers, origin, gear,
                                        GearRadialMenuWidget.ButtonDiameterPoints, false, screen, width,
                                        out string pair, out int n, out int sk);
                                    judged += n;
                                    skipped += sk;
                                    if (n == 0) continue;
                                    if (c < 0f) violations++;
                                    if (c >= worst) continue;
                                    worst = c;
                                    worstWhere = $"{screen.x:F0}x{screen.y:F0} 기어({fx:F2},{fy:F2}) " +
                                                 $"θ{baseDegrees:F0}° 이동{shift} 폭{width:F0} — {pair}";
                                }
                            }
                        }
                    }
                }
            }
            return worst;
        }

        // ============================================================================
        // ④ 링 반지름이 <b>가장 바깥 버튼</b>에서 파생된다 (36-4 보장의 전제)
        // ============================================================================

        [Test]
        public void 이름표_링은_가장_바깥_버튼_밖에_선다()
        {
            var origin = new Vector2(800f, 500f);
            List<Vector2> centers = FanCenters(origin, 225f);
            float farthest = FarthestFrom(origin, centers);

            // ★ 2026-09-06 — 위성이 폐지되어 «가장 바깥 버튼»은 이제 <b>아무 슬롯이나</b>다(다섯이 등거리).
            //   그래서 기대값이 SatelliteOrbitRadiusPoints에서 OrbitRadiusPoints로 바뀌었다.
            Assert.AreEqual(GearRadialMenuWidget.OrbitRadiusPoints, farthest, 0.01f,
                $"{LogPrefix} 가장 바깥 버튼까지의 거리가 궤도와 다릅니다({farthest:F2}pt) — " +
                "링 반지름의 전제가 깨졌습니다.");

            float ring = GearRadialMenuWidget.HoverLabelRingRadius(
                farthest, GearRadialMenuWidget.ButtonDiameterPoints);

            // 모든 버튼 <b>원</b>이 링 안에 있어야 «알약이 형제를 물 수 없다»가 성립한다.
            for (int i = 0; i < centers.Count; i++)
            {
                float outerEdge = (centers[i] - origin).magnitude
                    + GearRadialMenuWidget.ButtonDiameterPoints * 0.5f;
                Assert.Less(outerEdge, ring,
                    $"{LogPrefix} [{GearRadialMenuWidget.NameOf(i)}] 버튼의 바깥 끝({outerEdge:F2}pt)이 " +
                    $"이름표 링({ring:F2}pt) 밖입니다 — 그 버튼은 자기 형제의 이름표에 덮일 수 있습니다.");
            }

            // ★★ 2026-09-06 — 여기 있던 단언이 <b>정반대로 뒤집혔다</b>.
            //   옛 판: «링 > 호 궤도 기준» (위성이 호보다 바깥이므로 링이 더 커야 한다).
            //   새 판: «링 == 호 궤도 기준» — 궤도가 하나뿐이라 둘이 <b>같아야</b> 한다.
            //   이 등호가 곧 «이름표가 자기 버튼 옆에 뜬다»의 전제다(2026-09-06 사용자 신고의 수리).
            //   갈라지는 순간 안쪽 궤도 버튼의 이름표가 바깥 궤도 옆에 떠 다른 버튼을 가리킨다.
            float ownOrbitRing = GearRadialMenuWidget.HoverLabelRingRadius(
                GearRadialMenuWidget.OrbitRadiusPoints, GearRadialMenuWidget.ButtonDiameterPoints);
            Assert.AreEqual(ownOrbitRing, ring, 0.01f,
                $"{LogPrefix} 전역 링({ring:F2}pt)이 «자기 궤도 + 반경 + 간격»({ownOrbitRing:F2}pt)과 다릅니다 — " +
                "슬롯마다 궤도가 갈라졌다는 뜻이고, 그러면 안쪽 궤도 버튼의 이름표가 자기 원에서 그 차이만큼 " +
                "떠서 다른 버튼 옆에 붙습니다(2026-09-06 사용자 신고의 정체). 궤도를 다시 가르려면 " +
                "링을 «슬롯별»로 바꾸는 작업이 같은 라운드에 함께 와야 합니다.");

            Debug.Log($"{LogPrefix} 링 {ring:F2}pt (가장 바깥 버튼 {farthest:F2} + 반경 + 간격) " +
                      $"= 자기 궤도 기준 {ownOrbitRing:F2}pt — 궤도가 하나라 둘이 일치한다.");
        }
    }
}
