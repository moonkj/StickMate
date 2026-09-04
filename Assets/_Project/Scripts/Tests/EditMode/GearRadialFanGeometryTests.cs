using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 부채꼴 4버튼 기하 확정치 회귀 — docs/UX_FLOW.md <b>36-3</b>.
    ///
    /// 왜 EditMode 순수 계산인가: 36-3의 채택안(4개 × 30° / R111)은 <b>실측 격자 전수 계산</b>으로
    /// 뽑은 값이고, 그 계산의 결론은 "스팬을 90°로 줄이면 평행이동이 사실상 0이 된다"였다. 이 값들이
    /// 코드에서 조용히 어긋나면 그 근거 전체가 무너지는데, 씬을 띄우는 PlayMode 테스트는 폴백 사다리
    /// (회전/평행이동/축소/세로일렬)에 가려 <b>확정치가 틀린 것을 못 잡는다</b> — 폴백이 화면 안에만
    /// 넣어주면 통과하기 때문이다. 그래서 <see cref="GearRadialMenuWidget.Snap45"/>와 같은 관례로
    /// 순수 함수만 직접 잠근다.
    /// </summary>
    public sealed class GearRadialFanGeometryTests
    {
        /// <summary>사용자 지시로 늘어난 버튼들이 실제로 존재하고, <b>기존 번호는 하나도 안 바뀌었다</b>.</summary>
        [Test]
        public void 버튼은_다섯_개이고_앱종료가_마지막_슬롯이다()
        {
            Assert.AreEqual(5, GearRadialMenuWidget.ButtonCount, "부채꼴 버튼 수가 5가 아닙니다.");
            Assert.AreEqual(4, (int)GearMenuButton.Quit,
                "[앱 종료]가 마지막 슬롯이 아닙니다 — 36-3-4는 기존 번호를 재번호하지 말고 끝에 붙이라고 못박았습니다.");

            // 기존 4개의 값이 그대로여야 한다. 재번호되면 그 값을 읽는 switch가 조용히 어긋난다.
            Assert.AreEqual(0, (int)GearMenuButton.FocusMode, "[집중 모드] 슬롯 번호가 바뀌었습니다.");
            Assert.AreEqual(1, (int)GearMenuButton.Character, "[캐릭터] 슬롯 번호가 바뀌었습니다.");
            Assert.AreEqual(2, (int)GearMenuButton.Todo, "[오늘 할일] 슬롯 번호가 바뀌었습니다.");
            Assert.AreEqual(3, (int)GearMenuButton.Action, "[행동] 슬롯 번호가 바뀌었습니다.");
        }

        /// <summary>
        /// ★★ 호와 위성이 <b>갈라져 있다</b>. 이 관계가 깨지면 아래 모든 각도 단언의 전제가 무너진다.
        /// </summary>
        [Test]
        public void 호와_위성의_분할이_유지된다()
        {
            Assert.AreEqual(GearRadialMenuWidget.ButtonCount - GearRadialMenuWidget.SatelliteButtonCount,
                GearRadialMenuWidget.ArcButtonCount,
                "ArcButtonCount가 (총수 − 위성수)와 다릅니다 — 각도식의 분모와 배열 크기가 갈라졌습니다.");
            Assert.AreEqual(1, GearRadialMenuWidget.SatelliteButtonCount,
                "위성이 하나가 아닙니다 — 위성열이 둘이 되면 이격·각도 배분을 다시 계산해야 합니다(R4-4).");

            for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
            {
                Assert.IsFalse(GearRadialMenuWidget.IsSatelliteSlot(i), $"{i}번 호 슬롯이 위성으로 분류됐습니다.");
            }
            Assert.IsTrue(GearRadialMenuWidget.IsSatelliteSlot((int)GearMenuButton.Quit),
                "[앱 종료]가 위성으로 분류되지 않았습니다 — 호에 끼면 스팬이 120도가 됩니다.");
        }

        /// <summary>
        /// ★★★ <b>돌연변이 검증</b> — 53-3이 "이 변경에서 가장 위험한 한 줄"이라고 지목한 자리다.
        ///
        /// <para>각도식의 분모를 <c>ArcButtonCount</c>에서 <c>ButtonCount</c>로 <b>되돌린</b> 식을 여기서
        /// 직접 계산해, 그 되돌림이 <b>실제로 무엇을 부수는지</b>를 세 갈래로 잰다. 이 대조가 서야
        /// 위의 각도 단언들이 «무엇이든 통과시키는 빈 조건»이 아님이 증명된다 — 되돌림은
        /// <b>컴파일도 되고 대부분의 테스트도 통과</b>하기 때문에 여기가 유일한 방어다.</para>
        ///
        /// <para>★★ <b>2026-09-03 정정 — 이 테스트의 초판은 틀린 것을 재고 있었다.</b>
        /// 초판은 설계 문서(53-3)의 서술을 그대로 옮겨 <b>"스팬이 120°로 넓어지는가"</b>를 물었고,
        /// 러너에서 <c>Expected: greater than 90.0f / But was: 90.0f</c>로 빨개졌다.
        /// <b>실측하면 스팬은 안 변한다</b>:
        /// <code>
        ///   되돌린 식 (분모 5):  (2 − i)·30°  ->  호 i=0..3 = +60 / +30 /   0 / −30   스팬 90°
        ///   현행     (분모 4):  (1.5 − i)·30° ->  호 i=0..3 = +45 / +15 / −15 / −45   스팬 90°
        /// </code>
        /// 위성(i=4)은 자기 가지(<c>SatelliteAngleOffsetDegrees</c>)를 타므로 되돌림에 안 끌려간다.
        /// <b>즉 위험은 실재하고 오히려 더 나쁘다</b> — 스팬이 넓어지면 화면 밖으로 나가 눈에 띄지만,
        /// <b>비대칭은 조용히 틀어진 채로 돈다</b>. 서술이 틀렸던 것이지 위험이 없던 것이 아니다.</para>
        ///
        /// <para><b>실제 피해량</b>(전부 프로덕션 상수에서 계산한다 — 아래 단언이 이 값을 다시 만든다):
        /// <code>
        ///   각도 오프셋 총합      0°     ->  60°     (부채꼴이 축에서 평균 15° 기운다)
        ///   위성 ↔ 최근접 호 각거리 15°   ->   0°     (같은 반직선 위 = 위성이 3번째 칸 뒤에 숨는다)
        ///   위성 ↔ 최근접 호 거리  67.23 ->  57.00pt (호 이웃 하한 57.46pt <b>미달</b>)
        ///   톱니->위성 접근 직선의 여유 +2.73 -> <b>−26.00pt</b> (히트 원을 <b>관통</b>한다)
        /// </code>
        /// 마지막 줄이 이 함정의 실질이다. 53-4는 *"위성을 향해 가다 빗나가면 창이 열릴 뿐"*이라는
        /// <b>오폭 방향의 비대칭</b>을 이 안의 안전 근거로 들었는데, 되돌림은 그 근거를 <b>정반대로
        /// 뒤집는다</b> — 종료 버튼을 향해 곧게 그은 선이 다른 버튼을 정통으로 지난다.</para>
        /// </summary>
        [Test]
        public void 대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다()
        {
            float step = GearRadialMenuWidget.ButtonAngleStepDegrees;

            // 되돌림 재현 — <b>호 가지만</b> 분모를 바꾼다. 위성은 자기 가지를 타므로 그대로다.
            float Mutant(int i) => GearRadialMenuWidget.IsSatelliteSlot(i)
                ? GearRadialMenuWidget.SatelliteAngleOffsetDegrees
                : ((GearRadialMenuWidget.ButtonCount - 1) * 0.5f - i) * step;

            // ── (가) 되돌림이 <b>다른 각도 집합</b>을 낸다. 같다면 이 대조는 아무것도 못 잡는다.
            int differing = 0;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                if (!Mathf.Approximately(Mutant(i), GearRadialMenuWidget.SlotOffsetDegrees(i))) differing++;
            }
            Assert.Greater(differing, 0,
                "분모를 ButtonCount로 되돌려도 슬롯 각도가 전부 같습니다 — 그렇다면 ArcButtonCount 분리가 " +
                "아무 일도 하지 않는다는 뜻이고, 위의 각도 단언들은 되돌림을 못 잡습니다.");

            // ── (나) 되돌림은 <b>축 대칭</b>을 깬다. 이것이 실제로 일어나는 일이다(스팬이 아니라).
            float realSum = 0f, mutantSum = 0f;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                realSum += GearRadialMenuWidget.SlotOffsetDegrees(i);
                mutantSum += Mutant(i);
            }
            Assert.AreEqual(0f, realSum, 0.001f,
                "현행 각도의 합이 0이 아닙니다 — 아래 대조의 전제(현행은 축 대칭)가 깨졌습니다.");
            Assert.AreNotEqual(0f, mutantSum,
                "되돌린 식도 축 대칭입니다 — 그렇다면 (나)는 아무것도 잡지 못합니다.");
            Assert.Greater(Mathf.Abs(mutantSum), step,
                $"되돌린 식의 각도 합이 {mutantSum:F0}도로 간격 {step:F0}도보다 작습니다 — " +
                "53-3이 말하는 '조용히 기운다'가 재현되지 않습니다.");

            // ── (다) 그 비대칭이 <b>위성의 이격</b>을 무너뜨린다. 여기가 이 함정의 실질이다.
            float NearestAngle(System.Func<int, float> offset)
            {
                float best = float.MaxValue;
                for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
                {
                    best = Mathf.Min(best, Mathf.Abs(offset(i)
                        - offset(GearRadialMenuWidget.ButtonCount - 1)));
                }
                return best;
            }
            float NearestDistance(System.Func<int, float> offset)
            {
                float best = float.MaxValue;
                float sat = GearRadialMenuWidget.SatelliteOrbitRadiusPoints;
                float arc = GearRadialMenuWidget.OrbitRadiusPoints;
                for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
                {
                    float a = Mathf.Abs(offset(i) - offset(GearRadialMenuWidget.ButtonCount - 1));
                    best = Mathf.Min(best, Mathf.Sqrt(sat * sat + arc * arc
                        - 2f * sat * arc * Mathf.Cos(a * Mathf.Deg2Rad)));
                }
                return best;
            }

            float realAngle = NearestAngle(GearRadialMenuWidget.SlotOffsetDegrees);
            float mutantAngle = NearestAngle(Mutant);
            Assert.Greater(realAngle, mutantAngle,
                $"되돌림의 위성 각거리 {mutantAngle:F1}도가 현행 {realAngle:F1}도보다 좁지 않습니다 — " +
                "(다)가 아무것도 잡지 못합니다.");
            Assert.AreEqual(0f, mutantAngle, 0.001f,
                $"되돌림에서 위성과 최근접 호의 각거리가 {mutantAngle:F1}도입니다 — 실측은 <b>0도</b>" +
                "(같은 반직선 위)이고, 그것이 '위성이 호 버튼 뒤에 숨는다'는 이 함정의 실질입니다.");

            // 위성이 「가장 위험한 것이 가장 멀다」의 하한(호 이웃 간격)을 실제로 깬다.
            float arcNeighbour = 2f * GearRadialMenuWidget.OrbitRadiusPoints
                * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
            float mutantDistance = NearestDistance(Mutant);
            Assert.Less(mutantDistance, arcNeighbour,
                $"되돌림의 위성 거리 {mutantDistance:F2}pt가 호 이웃 하한 {arcNeighbour:F2}pt를 안 깹니다 — " +
                "그렇다면 위 «위성은 어떤 호 버튼보다도 멀리 떨어져 있다»가 이 되돌림을 못 잡습니다.");
            Assert.Less(mutantDistance, NearestDistance(GearRadialMenuWidget.SlotOffsetDegrees),
                "되돌림이 위성을 더 가깝게 만들지 않았습니다 — 피해 서술이 성립하지 않습니다.");

            // ── (라) 53-4의 <b>접근 경로 비대칭</b>이 정반대로 뒤집힌다. 이 함정의 최대 피해다.
            //     톱니에서 위성으로 곧게 그은 선(= 축)에 대한 호 버튼 중심의 수직거리를 잰다.
            float hitRadius = GearRadialMenuWidget.ButtonDiameterPoints * 0.5f
                + GearRadialMenuWidget.HitPaddingPoints;
            float Approach(System.Func<int, float> offset)
            {
                float best = float.MaxValue;
                for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
                {
                    float a = Mathf.Abs(offset(i) - offset(GearRadialMenuWidget.ButtonCount - 1));
                    best = Mathf.Min(best,
                        GearRadialMenuWidget.OrbitRadiusPoints * Mathf.Sin(a * Mathf.Deg2Rad));
                }
                return best - hitRadius;
            }

            float realClearance = Approach(GearRadialMenuWidget.SlotOffsetDegrees);
            Assert.Greater(realClearance, 0f,
                $"현행에서도 톱니->위성 접근 직선이 호 버튼의 히트 원을 {-realClearance:F2}pt 관통합니다 — " +
                "53-4의 '위성을 향해 가다 빗나가면 창이 열릴 뿐'이라는 안전 근거가 지금 이미 거짓입니다.");
            Assert.Less(Approach(Mutant), 0f,
                $"되돌림에서 접근 직선의 여유가 {Approach(Mutant):F2}pt로 양수입니다 — 실측은 " +
                $"−{hitRadius:F2}pt(정통 관통)이고, 그것이 이 되돌림의 최대 피해입니다.");

            Debug.Log("[부채꼴기하-TEST] 되돌림 피해량 — 각도합 " +
                      $"{realSum:F0} -> {mutantSum:F0}도 / 위성 각거리 {realAngle:F0} -> {mutantAngle:F0}도 / " +
                      $"위성 거리 {NearestDistance(GearRadialMenuWidget.SlotOffsetDegrees):F2} -> {mutantDistance:F2}pt " +
                      $"(호 이웃 하한 {arcNeighbour:F2}) / 접근 여유 {realClearance:F2} -> {Approach(Mutant):F2}pt.");
        }

        /// <summary>36-3-3 확정 기하. 세 값이 서로 맞물려 있어 하나만 바뀌어도 근거가 무너진다.</summary>
        [Test]
        public void 확정_기하는_30도_간격_반지름_111pt_스팬_90도다()
        {
            Assert.AreEqual(30f, GearRadialMenuWidget.ButtonAngleStepDegrees, 0.001f, "각 간격이 30도가 아닙니다.");
            Assert.AreEqual(111f, GearRadialMenuWidget.OrbitRadiusPoints, 0.001f, "궤도 반지름이 111pt가 아닙니다.");
            Assert.AreEqual(44f, GearRadialMenuWidget.ButtonDiameterPoints, 0.001f,
                "버튼 지름이 44pt가 아닙니다 — HIG 최소 타깃이라 줄이지 않기로 했습니다(36-3-3).");

            // ★ 분모는 ArcButtonCount다. ButtonCount로 적으면 위성이 호에 낀 것으로 세어 120도가 된다.
            float span = GearRadialMenuWidget.ButtonAngleStepDegrees * (GearRadialMenuWidget.ArcButtonCount - 1);
            Assert.AreEqual(90f, span, 0.001f,
                $"부채꼴 스팬이 {span:F0}도입니다 — 36-3-1의 핵심 결론은 '스팬 90도'이고, 모서리에서 부채꼴을 " +
                "막는 것은 반지름이 아니라 각도 스팬입니다.");
            Assert.Less(span, 120f,
                "새 스팬이 기존 3버튼(120도)보다 넓습니다 — 버튼을 늘리고도 더 튼튼해진다는 36-3의 근거가 무너집니다.");
        }

        /// <summary>
        /// ★ 히트 원이 절대 겹치지 않는다 — 겹치면 "먼저 검사되는 버튼이 이긴다"가 되어 <b>보이는 것과
        /// 눌리는 것이 달라진다</b>. 인접 중심 거리는 2·R·sin(step/2)이다.
        /// </summary>
        [Test]
        public void 인접_버튼의_히트_원이_겹치지_않는다()
        {
            var gear = new Vector2(1000f, 500f);
            float minSpacing = float.MaxValue;
            // 호 안에서만 잰다 — 위성은 호 위에 없으므로 "인접 2·R·sin(step/2)" 공식의 대상이 아니다.
            // 위성의 이격은 아래 별도 테스트가 <b>더 엄격한 하한</b>으로 잠근다.
            for (int i = 0; i + 1 < GearRadialMenuWidget.ArcButtonCount; i++)
            {
                Vector2 a = GearRadialMenuWidget.SlotCenterPoints(gear, 225f, i);
                Vector2 b = GearRadialMenuWidget.SlotCenterPoints(gear, 225f, i + 1);
                minSpacing = Mathf.Min(minSpacing, Vector2.Distance(a, b));
            }

            float expected = 2f * GearRadialMenuWidget.OrbitRadiusPoints
                * Mathf.Sin(GearRadialMenuWidget.ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad);
            Assert.AreEqual(expected, minSpacing, 0.01f, "인접 중심 거리가 2·R·sin(간격/2)와 다릅니다.");

            // 히트 판정은 지름/2 + HitPadding 원이다. 두 원이 안 겹치려면 중심 거리가 그 지름 이상이어야 한다.
            float hitDiameter = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.HitPaddingPoints * 2f;
            Assert.GreaterOrEqual(minSpacing, hitDiameter,
                $"인접 히트 원이 겹칩니다(중심 거리 {minSpacing:F1}pt < 히트 지름 {hitDiameter:F1}pt) — " +
                "보이는 것과 눌리는 것이 달라집니다.");

            // 36-3-1이 표에 적은 "원 사이 여백 13.5pt"가 실제로 나오는지.
            float visualGap = minSpacing - GearRadialMenuWidget.ButtonDiameterPoints;
            Assert.AreEqual(13.5f, visualGap, 0.2f, $"원 사이 시각 여백이 {visualGap:F1}pt입니다(36-3-1 표: 13.5pt).");
        }

        /// <summary>슬롯 각도는 θ₀ 좌우 대칭이어야 한다 — 그래야 "가운데가 화면 안쪽"이 유지된다.</summary>
        [Test]
        public void 슬롯_각도는_기준각_좌우_대칭이다()
        {
            Assert.AreEqual(45f, GearRadialMenuWidget.SlotOffsetDegrees(0), 0.001f, "0번 슬롯이 θ₀+45도가 아닙니다.");
            Assert.AreEqual(15f, GearRadialMenuWidget.SlotOffsetDegrees(1), 0.001f, "1번 슬롯이 θ₀+15도가 아닙니다.");
            Assert.AreEqual(-15f, GearRadialMenuWidget.SlotOffsetDegrees(2), 0.001f, "2번 슬롯이 θ₀−15도가 아닙니다.");
            Assert.AreEqual(-45f, GearRadialMenuWidget.SlotOffsetDegrees(3), 0.001f, "3번 슬롯이 θ₀−45도가 아닙니다.");
            // ★ 위성은 <b>축 위</b>(0도)다 — 이 한 줄이 깨지면 종료 버튼이 호 쪽으로 끌려간 것이다.
            Assert.AreEqual(0f, GearRadialMenuWidget.SlotOffsetDegrees((int)GearMenuButton.Quit), 0.001f,
                "위성 [앱 종료]가 기준각 축 위에 있지 않습니다 — 이웃까지의 각거리 15도(최대값)를 잃습니다.");

            float sum = 0f;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++) sum += GearRadialMenuWidget.SlotOffsetDegrees(i);
            Assert.AreEqual(0f, sum, 0.001f, "슬롯 각도의 합이 0이 아닙니다 — 부채꼴이 기준각 기준 좌우 대칭이 아닙니다.");
        }

        /// <summary>
        /// ★ 기어→버튼 거리가 <b>전부 같다</b>. 36-3-2가 지적한 기존 구현의 결함(84/97/89pt로 제각각)이
        /// 사라졌는지 확인한다 — 이것이 성립해야 "궤도"라는 말이 사실이 된다.
        /// </summary>
        [Test]
        public void 기어에서_각_버튼까지의_거리가_그_슬롯의_궤도와_같다()
        {
            var gear = new Vector2(1200f, 700f);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float d = Vector2.Distance(gear, GearRadialMenuWidget.SlotCenterPoints(gear, 225f, i));
                Assert.AreEqual(GearRadialMenuWidget.SlotRadiusPoints(i), d, 0.01f,
                    $"{i}번 버튼까지의 거리가 그 슬롯의 궤도와 다릅니다({d:F1}pt).");
            }

            // 호 넷은 <b>서로 같고</b>, 위성만 다르다 — "궤도"라는 말이 사실이려면 이 둘이 함께 서야 한다.
            for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
            {
                Assert.AreEqual(GearRadialMenuWidget.OrbitRadiusPoints, GearRadialMenuWidget.SlotRadiusPoints(i),
                    0.001f, $"{i}번 호 슬롯의 궤도가 111pt가 아닙니다.");
            }
            Assert.Greater(GearRadialMenuWidget.SatelliteOrbitRadiusPoints, GearRadialMenuWidget.OrbitRadiusPoints,
                "위성이 호보다 바깥에 있지 않습니다 — '가장 멀고 가장 다른 자리'라는 설계 목표가 무너집니다.");
        }

        /// <summary>
        /// ★★ <b>오폭 방지의 핵심 숫자</b>(53-2). 되돌릴 수 없는 버튼이 나머지보다 <b>더 멀리</b> 있어야 한다.
        ///
        /// <para>여기서 재는 것은 절대값이 아니라 <b>부등식 두 개</b>다: 위성↔최근접 호 거리가
        /// ① 히트 원이 겹치는 임계(<c>지름 + 여백×2</c>)보다 크고 ② <b>호 이웃끼리의 간격보다도</b> 크다.
        /// ②가 없으면 "겹치지만 않으면 된다"가 되어, 종료 버튼이 다른 버튼만큼 가까워도 통과한다.</para>
        /// </summary>
        [Test]
        public void 위성은_어떤_호_버튼보다도_멀리_떨어져_있다()
        {
            var gear = new Vector2(900f, 600f);
            Vector2 satellite = GearRadialMenuWidget.SlotCenterPoints(gear, 225f, (int)GearMenuButton.Quit);

            float nearest = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < GearRadialMenuWidget.ArcButtonCount; i++)
            {
                float d = Vector2.Distance(satellite, GearRadialMenuWidget.SlotCenterPoints(gear, 225f, i));
                if (d >= nearest) continue;
                nearest = d;
                nearestIndex = i;
            }
            Assert.AreNotEqual(-1, nearestIndex, "호 버튼이 하나도 없습니다 — 이 판정은 무효입니다.");

            // 기대식(코사인 제2법칙)을 <b>프로덕션 상수에서 세운다</b> — 67.23을 베끼지 않는다.
            float expected = Mathf.Sqrt(
                GearRadialMenuWidget.SatelliteOrbitRadiusPoints * GearRadialMenuWidget.SatelliteOrbitRadiusPoints
                + GearRadialMenuWidget.OrbitRadiusPoints * GearRadialMenuWidget.OrbitRadiusPoints
                - 2f * GearRadialMenuWidget.SatelliteOrbitRadiusPoints * GearRadialMenuWidget.OrbitRadiusPoints
                  * Mathf.Cos(GearRadialMenuWidget.ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad));
            Assert.AreEqual(expected, nearest, 0.05f,
                "위성↔최근접 호 거리가 코사인 제2법칙 값과 다릅니다 — 각도나 궤도가 설계와 어긋났습니다.");

            float hitDiameter = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.HitPaddingPoints * 2f;
            Assert.Greater(nearest, hitDiameter,
                $"위성의 히트 원이 {nearestIndex}번 버튼과 겹칩니다({nearest:F2}pt < {hitDiameter:F2}pt) — " +
                "되돌릴 수 없는 버튼에서 '보이는 것과 눌리는 것이 다르다'는 최악의 결함입니다.");

            float arcNeighbour = 2f * GearRadialMenuWidget.OrbitRadiusPoints
                * Mathf.Sin(GearRadialMenuWidget.ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad);
            Assert.Greater(nearest, arcNeighbour,
                $"위성이 호 이웃 간격({arcNeighbour:F2}pt)보다 가깝습니다({nearest:F2}pt) — " +
                "'가장 위험한 버튼이 가장 멀다'가 성립하지 않습니다.");
        }

        /// <summary>세로 일렬 폴백에서도 위성이 <b>가장 먼 칸</b>이다(53-7).
        /// <para>폴백은 인스턴스 메서드라 여기서 못 돌린다 — 대신 그 배치가 쓰는 성질(슬롯 번호가 곧
        /// 거리 순서, 간격 <see cref="GearRadialMenuWidget.ColumnFallbackSpacingPoints"/>)을 잠근다.</para></summary>
        [Test]
        public void 세로일렬_폴백에서도_위성이_가장_먼_칸이다()
        {
            Assert.AreEqual(GearRadialMenuWidget.ButtonCount - 1, (int)GearMenuButton.Quit,
                "위성이 마지막 번호가 아닙니다 — 세로 일렬 폴백은 번호 순으로 멀어지므로, " +
                "번호가 중간이면 종료 버튼이 다른 버튼들 사이에 끼게 됩니다.");

            float farthest = GearRadialMenuWidget.OrbitRadiusPoints
                + GearRadialMenuWidget.ColumnFallbackSpacingPoints * (int)GearMenuButton.Quit;
            float nextFarthest = GearRadialMenuWidget.OrbitRadiusPoints
                + GearRadialMenuWidget.ColumnFallbackSpacingPoints * ((int)GearMenuButton.Quit - 1);
            Assert.Greater(farthest, nextFarthest, "폴백에서 위성이 가장 멀지 않습니다.");
        }

        /// <summary>
        /// 클램프 상자는 <b>원 중심에 정렬된 정사각형</b>이어야 한다(Ø44 → 56×56). 라벨이 있던 시절의
        /// 비대칭(폭이 글자 길이에 따라 다르고, 중심이 아래로 10pt 어긋남)이 곧 기본 위치에서 평행이동
        /// 35.5pt를 만들던 원인이었다(36-3-2).
        /// </summary>
        [Test]
        public void 클램프_상자는_원_중심에_정렬된_정사각형이다()
        {
            var center = new Vector2(400f, 300f);
            Rect box = GearRadialMenuWidget.ButtonClampBox(center, GearRadialMenuWidget.ButtonDiameterPoints);

            Assert.AreEqual(56f, box.width, 0.001f, "클램프 상자 폭이 56pt가 아닙니다(44 + 패딩 12).");
            Assert.AreEqual(box.width, box.height, 0.001f, "클램프 상자가 정사각형이 아닙니다 — 라벨 시절의 세로 비대칭이 남아 있습니다.");
            Assert.AreEqual(center.x, box.center.x, 0.001f, "상자 중심 x가 원 중심과 다릅니다.");
            Assert.AreEqual(center.y, box.center.y, 0.001f,
                "상자 중심 y가 원 중심과 다릅니다 — 라벨 알약 자리(중심 아래 10pt)가 아직 계산에 남아 있습니다.");

            // 상자는 원을 완전히 덮어야 한다(원이 상자 밖으로 삐져나오면 화면 밖 판정이 거짓말이 된다).
            Assert.GreaterOrEqual(box.width, GearRadialMenuWidget.ButtonDiameterPoints, "상자가 원보다 좁습니다.");
        }

        /// <summary>
        /// ★ "촤르륵"의 예산은 0.30초로 정해져 있다(32-2). 버튼이 늘었다고 사용자를 매번 더 기다리게
        /// 만들지 않는다 — 그래서 스태거가 0.055 → 0.037(4개) → <b>0.0275(5개)</b>로 줄어 왔다
        /// (36-3-3 / 53-2). <b>값이 아니라 예산을 잠근다</b> — 아래는 스태거 상수를 베끼지 않고
        /// 예산에서 상한을 <b>역산</b>해 비교한다.
        /// </summary>
        [Test]
        public void 펼침_총_길이가_0_30초_예산을_지킨다()
        {
            // ★ 스태거 값을 베끼지 않는다 — 예산에서 <b>역산</b>해 대조한다. 그래야 버튼이 또 늘었을 때
            //   "값이 다르다"가 아니라 "예산을 넘었다"로 정확히 빨개진다.
            const float budget = 0.30f;
            float required = (budget - GearRadialMenuWidget.ExpandSecondsPerButton)
                / (GearRadialMenuWidget.ButtonCount - 1);
            Assert.LessOrEqual(GearRadialMenuWidget.ExpandStaggerSeconds, required + 0.0001f,
                $"스태거 {GearRadialMenuWidget.ExpandStaggerSeconds:F4}초는 버튼 " +
                $"{GearRadialMenuWidget.ButtonCount}개에서 예산 {budget:F2}초를 넘깁니다(상한 {required:F4}초) — " +
                "버튼이 늘었다고 사용자를 매번 더 기다리게 하지 않는다는 32-2의 약속이 깨집니다.");
            Assert.LessOrEqual(GearRadialMenuWidget.ExpandTotalSeconds, budget + 0.005f,
                $"펼침 총 길이가 {GearRadialMenuWidget.ExpandTotalSeconds:F3}초입니다 — 32-2의 예산은 {budget:F2}초입니다.");
            Assert.Greater(GearRadialMenuWidget.ExpandStaggerSeconds, 0f,
                "스태거가 0이면 다섯 개가 동시에 튀어나옵니다 — '촤르륵'이 아닙니다.");
        }

        /// <summary>
        /// 세로 일렬 폴백 간격은 52pt 고정이며, 그 값이 <b>히트 원이 겹치지 않는 최소 간격</b>과 같아야
        /// 한다(Ø44 + 히트 여백 4×2). 라벨이 사라져 하한 계산식이 없어졌으므로 이 등식이 유일한 근거다.
        /// </summary>
        [Test]
        public void 세로일렬_폴백_간격이_히트원_비겹침_하한과_같다()
        {
            float hitDiameter = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.HitPaddingPoints * 2f;
            Assert.AreEqual(hitDiameter, GearRadialMenuWidget.ColumnFallbackSpacingPoints, 0.001f,
                "세로 일렬 간격이 히트 원 지름과 다릅니다 — 좁으면 원이 겹치고, 넓으면 근거 없는 값입니다.");
        }

        /// <summary>기준각 스냅은 36 라운드에서 손대지 않았다 — 양성 대조로 함께 잠근다(32-9 (C)).</summary>
        [Test]
        public void 기준각은_화면_중심_방향을_45도로_스냅한다()
        {
            Assert.AreEqual(225f, GearRadialMenuWidget.Snap45(new Vector2(-1f, -1f)), 0.01f, "우상단 -> 좌하");
            Assert.AreEqual(270f, GearRadialMenuWidget.Snap45(new Vector2(0f, -1f)), 0.01f,
                "화면 위쪽 한가운데인데 아래로 곧게(270도) 펼치지 않았습니다.");
        }
    }
}
