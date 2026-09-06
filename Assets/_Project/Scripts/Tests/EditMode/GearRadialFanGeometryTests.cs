using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 부채꼴 <b>5버튼 한 호</b> 기하 확정치 회귀 — docs/UX_FLOW.md <b>36-3 / 53-0</b>.
    /// (2026-09-06: 호 4 + 위성 1 · 30°/R111 → <b>호 5 · 22.5°/R148</b>. 스팬 90°는 불변.)
    ///
    /// 왜 EditMode 순수 계산인가: 36-3의 채택안은 <b>실측 격자 전수 계산</b>으로
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
        /// ★★ <b>2026-09-06 — 여기 있던 「호와 위성의 분할이 유지된다」를 삭제했다.</b>
        ///
        /// <para><b>왜 지웠나</b>: 그 테스트가 잠그던 것은 <c>ArcButtonCount == ButtonCount −
        /// SatelliteButtonCount</c>와 «[앱 종료]가 위성으로 분류되는가»였다. 사용자 신고
        /// <i>"끄기 버튼만 따로 떨어져 있다"</i>로 <b>위성 개념 자체가 폐지됐고</b> 그 세 상수
        /// (<c>SatelliteButtonCount</c> · <c>ArcButtonCount</c> · <c>SatelliteOrbitRadiusPoints</c>)와
        /// <c>IsSatelliteSlot</c>이 프로덕션에서 사라졌다 — 없는 것을 잠그는 테스트는 컴파일되지 않고,
        /// 억지로 살려 두면 «위성이 하나여야 한다»는 <b>거짓 사실</b>을 러너가 매 라운드 주장하게 된다.</para>
        ///
        /// <para>★ <b>그 테스트가 지키던 진짜 값어치는 버려지지 않았다.</b> 그것은
        /// «각도식의 분모가 실제 호 개수와 갈라지지 않는가»였고, 궤도가 하나가 된 지금 그 위험은
        /// <b>«궤도가 다시 갈라지는가»</b>로 모습을 바꿨다 —
        /// <see cref="전역_단일_이름표_링의_전제인_단일_궤도가_유지된다"/>가 그 자리를 이어받는다.
        /// 함께 삭제된 <c>대조_분모를_ButtonCount로_되돌리면_이_테스트가_빨개진다</c>도 같은 이유다:
        /// 되돌릴 분모가 없으면 그 돌연변이는 <b>구조적으로 만들 수 없다</b>.</para>
        /// </summary>
        [Test]
        public void 다섯_슬롯이_모두_한_호_위에_있다()
        {
            float first = GearRadialMenuWidget.SlotRadiusPoints(0);
            for (int i = 1; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.AreEqual(first, GearRadialMenuWidget.SlotRadiusPoints(i), 0.001f,
                    $"{i}번 슬롯이 다른 궤도({GearRadialMenuWidget.SlotRadiusPoints(i):F1}pt)를 돕니다 — " +
                    "2026-09-06 사용자 신고 «끄기 버튼만 따로 떨어져 있다»가 정확히 이 상태였습니다.");
            }

            // 각도도 «호 하나»여야 한다: 등간격이고, 축을 벗어난 예외 슬롯이 없다.
            for (int i = 1; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float gap = GearRadialMenuWidget.SlotOffsetDegrees(i - 1)
                            - GearRadialMenuWidget.SlotOffsetDegrees(i);
                Assert.AreEqual(GearRadialMenuWidget.ButtonAngleStepDegrees, gap, 0.001f,
                    $"슬롯 {i - 1}→{i} 각 간격이 {gap:F2}도로 균일하지 않습니다 — " +
                    "한 칸만 축 위로 빠지면(옛 위성) 그 칸이 «메뉴에서 떨어져 나온 미아»로 읽힙니다.");
            }
        }

        /// <summary>
        /// ★★★ <b>돌연변이 검증</b> — <b>2026-09-06에 겨누는 대상이 바뀌었다.</b>
        ///
        /// <para>옛 판은 «각도식의 분모를 <c>ArcButtonCount</c>에서 <c>ButtonCount</c>로 되돌리는»
        /// 돌연변이를 잡았다. 위성이 폐지되어 <b>그 두 상수가 하나로 합쳐졌으므로 그 돌연변이는
        /// 구조적으로 만들 수 없다</b> — 없는 함정을 지키는 테스트는 지웠다.</para>
        ///
        /// <para><b>지금 가장 위험한 한 줄은 다른 자리다</b>: 다섯 칸을 한 호에 올리면서
        /// <c>ButtonAngleStepDegrees</c>를 <b>30° 그대로 두는 것</b>. 그것이 이 라운드의 인계안이었고,
        /// 컴파일도 되고 <b>겹침 단언도 통과한다</b>(30°/R148은 이웃이 오히려 더 멀다) — 무너지는 것은
        /// <b>스팬</b>이고, 그 피해는 «화면 구석에서만, 그것도 세로 일렬 폴백이라는 다른 모습으로»
        /// 나타나서 눈으로 안 보인다. 2026-09-06 실측(화면 8종 × 예약 띠 16종 · 12,265,232 표본):
        /// <code>
        ///   간격 30° (스팬 120°)   세로 일렬 폴백 <b>1.4960%</b>   (현행의 3.97배)
        ///   간격 22.5°(스팬  90°)  세로 일렬 폴백 <b>0.3942%</b>
        /// </code>
        /// 아래는 그 스팬 차이를 <b>프로덕션 상수에서 다시 계산해</b> 재현한다 — 숫자를 베끼지 않는다.</para>
        /// </summary>
        [Test]
        public void 대조_간격을_30도로_되돌리면_스팬이_120도가_된다()
        {
            const float revertedStep = 30f;   // 폐지된 위성 시절의 간격. 상수가 아니라 «옛 값»이라 여기 적는다.
            int n = GearRadialMenuWidget.ButtonCount;

            float realSpan = GearRadialMenuWidget.ButtonAngleStepDegrees * (n - 1);
            float mutantSpan = revertedStep * (n - 1);

            Assert.AreEqual(90f, realSpan, 0.001f,
                $"현행 스팬이 {realSpan:F1}도입니다 — 36-3-1의 핵심 결론('스팬 90도')이 이미 깨져 있어 " +
                "이 대조의 전제가 성립하지 않습니다.");
            Assert.Greater(mutantSpan, realSpan,
                $"되돌린 간격의 스팬이 {mutantSpan:F0}도로 현행 {realSpan:F0}도보다 넓지 않습니다 — " +
                "이 대조가 아무것도 잡지 못합니다.");
            Assert.AreEqual(120f, mutantSpan, 0.001f,
                $"되돌림 스팬이 {mutantSpan:F0}도입니다 — 53-1이 '네 모서리 각도창 73.3~73.8도로는 못 메운다'고 " +
                "계산한 값은 120도입니다.");

            // ── 되돌림이 «겹침»으로는 안 잡힌다는 것까지 보인다. 이게 이 함정이 조용한 이유다.
            float hitDiameter = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.HitPaddingPoints * 2f;
            float mutantSpacing = 2f * GearRadialMenuWidget.OrbitRadiusPoints
                * Mathf.Sin(revertedStep * 0.5f * Mathf.Deg2Rad);
            Assert.Greater(mutantSpacing, hitDiameter,
                "되돌림에서 히트 원이 겹칩니다 — 그렇다면 겹침 단언이 이 되돌림을 잡아 주므로 " +
                "'조용히 틀어진다'는 이 테스트의 존재 근거가 사라집니다(그때는 이 대조를 지워도 됩니다).");

            Debug.Log($"[부채꼴기하-TEST] 간격 되돌림 피해 — 스팬 {realSpan:F0} -> {mutantSpan:F0}도 " +
                      $"(이웃 중심거리는 {2f * GearRadialMenuWidget.OrbitRadiusPoints * Mathf.Sin(GearRadialMenuWidget.ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad):F2} -> {mutantSpacing:F2}pt로 " +
                      "오히려 넓어져 겹침 단언에 안 걸린다).");
        }

        /// <summary>
        /// ★★ <b>두 번째 돌연변이</b> — 간격만 22.5°로 줄이고 <b>궤도를 111pt에 두는 것</b>.
        /// 이쪽은 반대로 <b>시끄럽게</b> 깨진다(히트 원이 겹친다). 두 대조를 나란히 두는 이유는
        /// «간격과 궤도가 한 쌍»이라는 사실을 러너가 말하게 하기 위해서다 — 하나만 고치면 반드시 틀린다.
        /// </summary>
        [Test]
        public void 대조_간격만_줄이고_궤도를_111pt에_두면_히트원이_겹친다()
        {
            const float oldOrbit = 111f;      // 위성 시절의 궤도.
            float step = GearRadialMenuWidget.ButtonAngleStepDegrees;
            float hitDiameter = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.HitPaddingPoints * 2f;

            float mutantSpacing = 2f * oldOrbit * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
            Assert.Less(mutantSpacing, hitDiameter,
                $"옛 궤도 {oldOrbit:F0}pt + 현행 간격 {step:0.##}도에서 이웃 중심거리가 {mutantSpacing:F2}pt로 " +
                $"히트 지름 {hitDiameter:F0}pt를 넘습니다 — 그렇다면 궤도를 넓힌 근거가 사라진 것이므로 " +
                "OrbitRadiusPoints 문서의 유도를 다시 확인하십시오.");

            float realSpacing = 2f * GearRadialMenuWidget.OrbitRadiusPoints
                * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad);
            Assert.GreaterOrEqual(realSpacing, hitDiameter,
                $"현행 궤도에서도 히트 원이 겹칩니다({realSpacing:F2} < {hitDiameter:F0}pt).");

            Debug.Log($"[부채꼴기하-TEST] 궤도 되돌림 피해 — 이웃 중심거리 {realSpacing:F2} -> {mutantSpacing:F2}pt " +
                      $"(히트 지름 {hitDiameter:F0}pt, 겹침 {hitDiameter - mutantSpacing:F2}pt).");
        }

        /// <summary>36-3-3 확정 기하(2026-09-06 갱신: 30도/111pt → <b>22.5도/148pt</b>).
        /// 세 값이 서로 맞물려 있어 하나만 바뀌어도 근거가 무너진다 — 그 맞물림 자체는 위 두 대조가 잰다.</summary>
        [Test]
        public void 확정_기하는_22_5도_간격_반지름_148pt_스팬_90도다()
        {
            Assert.AreEqual(22.5f, GearRadialMenuWidget.ButtonAngleStepDegrees, 0.001f, "각 간격이 22.5도가 아닙니다.");
            Assert.AreEqual(148f, GearRadialMenuWidget.OrbitRadiusPoints, 0.001f, "궤도 반지름이 148pt가 아닙니다.");
            Assert.AreEqual(44f, GearRadialMenuWidget.ButtonDiameterPoints, 0.001f,
                "버튼 지름이 44pt가 아닙니다 — HIG 최소 타깃이라 줄이지 않기로 했습니다(36-3-3).");

            // ★ 분모는 ButtonCount다 — 다섯이 전부 한 호 위에 있으므로(2026-09-06 위성 폐지).
            float span = GearRadialMenuWidget.ButtonAngleStepDegrees * (GearRadialMenuWidget.ButtonCount - 1);
            Assert.AreEqual(90f, span, 0.001f,
                $"부채꼴 스팬이 {span:F0}도입니다 — 36-3-1의 핵심 결론은 '스팬 90도'이고, 모서리에서 부채꼴을 " +
                "막는 것은 반지름이 아니라 각도 스팬입니다. 버튼이 넷에서 다섯이 되고 위성이 폐지되는 " +
                "동안에도 이 값은 한 도(度)도 안 바뀌었습니다.");
            Assert.Less(span, 120f,
                "새 스팬이 기존 3버튼(120도)보다 넓습니다 — 버튼을 늘리고도 더 튼튼해진다는 36-3의 근거가 무너집니다.");

            // 22.5 = 45/2 — θ₀(45도 스냅)와 정확히 정합해야 슬롯 각도가 부동소수 나머지를 남기지 않는다.
            Assert.AreEqual(0f, Mathf.Repeat(45f, GearRadialMenuWidget.ButtonAngleStepDegrees), 0.001f,
                "간격이 45도의 약수가 아닙니다 — θ₀ 스냅 격자와 어긋납니다.");

            // DPI: 궤도는 4의 배수라야 x1.25/1.50/1.75에서 정수 픽셀이 된다(53-8).
            Assert.AreEqual(0f, GearRadialMenuWidget.OrbitRadiusPoints % 4f, 0.001f,
                $"궤도 {GearRadialMenuWidget.OrbitRadiusPoints:F0}pt가 4의 배수가 아닙니다 — " +
                "배율 125/150/175%에서 소수 픽셀이 됩니다.");
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
            // 다섯이 전부 한 호 위에 등간격으로 서므로(2026-09-06 위성 폐지) 전 쌍의 최소값이
            // 곧 이웃 간격이고, 그것이 "인접 2·R·sin(step/2)" 공식의 대상 그 자체다.
            for (int i = 0; i + 1 < GearRadialMenuWidget.ButtonCount; i++)
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

            // 36-3-1이 표에 적은 "원 사이 여백 13.5pt". ★ 2026-09-06 — 등호가 아니라 <b>하한</b>으로
            // 잠근다: 궤도/간격이 22.5도·148pt로 바뀌면서 실측이 13.75pt가 됐는데, 이는 표의 값보다
            // <b>넓어진</b> 것이라 «원이 붙어 보인다»는 위험이 줄어든 방향이다. 등호로 두면 개선까지
            // 빨갛게 만들고, 그러면 다음 사람이 허용오차를 늘려 하한 자체를 죽인다.
            float visualGap = minSpacing - GearRadialMenuWidget.ButtonDiameterPoints;
            Assert.GreaterOrEqual(visualGap, 13.5f - 0.05f,
                $"원 사이 시각 여백이 {visualGap:F2}pt로 36-3-1 표의 13.5pt보다 좁습니다 — " +
                "원이 서로 붙어 보이기 시작하는 방향입니다.");
        }

        /// <summary>슬롯 각도는 θ₀ 좌우 대칭이어야 한다 — 그래야 "가운데가 화면 안쪽"이 유지된다.</summary>
        [Test]
        public void 슬롯_각도는_기준각_좌우_대칭이다()
        {
            // ★ 2026-09-06 — 다섯 칸 전부 한 호 위: {+45, +22.5, 0, −22.5, −45}. 끝 두 칸의 ±45도는
            //   위성 시절의 호 넷과 <b>같은 값</b>이다(스팬 90도가 안 바뀌었다는 사실의 다른 표현).
            Assert.AreEqual(45f, GearRadialMenuWidget.SlotOffsetDegrees(0), 0.001f, "0번 슬롯이 θ₀+45도가 아닙니다.");
            Assert.AreEqual(22.5f, GearRadialMenuWidget.SlotOffsetDegrees(1), 0.001f, "1번 슬롯이 θ₀+22.5도가 아닙니다.");
            Assert.AreEqual(0f, GearRadialMenuWidget.SlotOffsetDegrees(2), 0.001f,
                "2번 슬롯이 기준각 축 위(0도)가 아닙니다 — 홀수 개 부채꼴의 가운데 칸은 축 위입니다.");
            Assert.AreEqual(-22.5f, GearRadialMenuWidget.SlotOffsetDegrees(3), 0.001f, "3번 슬롯이 θ₀−22.5도가 아닙니다.");
            Assert.AreEqual(-45f, GearRadialMenuWidget.SlotOffsetDegrees((int)GearMenuButton.Quit), 0.001f,
                "[앱 종료]가 호의 끝(θ₀−45도)에 있지 않습니다 — 다섯 번째 호 슬롯이라는 배치가 깨졌습니다.");

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

            // ★ 2026-09-06 — 다섯이 <b>서로 같다</b>. 위성이 폐지되어 "궤도"라는 말이 처음으로
            //   다섯 전부에 대해 사실이 됐다(그전에는 넷만 같고 [앱 종료] 하나가 168pt에 있었다).
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.AreEqual(GearRadialMenuWidget.OrbitRadiusPoints, GearRadialMenuWidget.SlotRadiusPoints(i),
                    0.001f, $"{i}번 슬롯의 궤도가 {GearRadialMenuWidget.OrbitRadiusPoints:F0}pt가 아닙니다.");
            }
        }

        /// <summary>
        /// ★★ <b>2026-09-06 — 여기 있던 위성 이격 테스트 두 개를 삭제했다.</b>
        /// <c>위성은_어떤_호_버튼보다도_멀리_떨어져_있다</c>(53-2의 «오폭 방지 핵심 숫자» 67.23pt)와
        /// <c>세로일렬_폴백에서도_위성이_가장_먼_칸이다</c>(53-7).
        ///
        /// <para><b>왜 삭제인가</b>: 둘 다 <c>SatelliteOrbitRadiusPoints</c>·<c>ArcButtonCount</c>를
        /// 참조했고, 사용자 신고 <i>"끄기 버튼만 따로 떨어져 있다"</i>로 <b>위성 개념 자체가 폐지되면서</b>
        /// 그 상수들이 프로덕션에서 사라졌다. 그리고 첫 번째 테스트가 지키던 명제
        /// «되돌릴 수 없는 버튼이 나머지보다 더 멀리 있다»는 이제 <b>의도적으로 거짓</b>이다 —
        /// 다섯이 등거리·등간격으로 서는 것이 이번 라운드의 목적 그 자체다.
        /// 거짓이 된 명제를 지키는 테스트를 남기면 러너가 매 라운드 <b>틀린 설계</b>를 주장한다.</para>
        ///
        /// <para>★ <b>그 테스트들이 막던 위험은 어디로 갔나</b> — 두 갈래로 갈라져 살아 있다:
        /// <list type="bullet">
        ///   <item><b>오폭</b>(종료를 잘못 누름)의 방어는 원래도 <b>2단 확인 3초</b>가 실질이었고
        ///     그쪽은 손대지 않았다 — <c>InfoGearRadialMenuTests</c>의 무장/자동해제 테스트가 잠근다.
        ///     위치로 벌려 두는 방어는 <b>사용자가 기각했다</b>(그 자리가 «미아»로 읽혔다).</item>
        ///   <item><b>히트 원 겹침</b>은 <see cref="인접_버튼의_히트_원이_겹치지_않는다"/>가
        ///     이제 <b>다섯 전 쌍</b>에 대해 잠근다(옛 판은 호 넷만 봤다).</item>
        /// </list></para>
        ///
        /// <para>아래 테스트는 그중 <b>세로 일렬 폴백의 순서</b>만 이어받는다 — 그 배치에서는
        /// 여전히 «가장 위험한 것이 가장 멀다»가 성립하고, 그것은 슬롯 번호가 지키는 성질이다.</para>
        /// </summary>
        [Test]
        public void 세로일렬_폴백에서_앱종료가_가장_먼_칸이다()
        {
            Assert.AreEqual(GearRadialMenuWidget.ButtonCount - 1, (int)GearMenuButton.Quit,
                "[앱 종료]가 마지막 번호가 아닙니다 — 세로 일렬 폴백은 번호 순으로 멀어지므로, " +
                "번호가 중간이면 종료 버튼이 다른 버튼들 사이에 끼게 됩니다.");

            // 폴백은 인스턴스 메서드라 여기서 못 돌린다 — 대신 그 배치가 쓰는 순수 함수를 직접 잰다.
            var origin = new Vector2(500f, 400f);
            float previous = -1f;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float d = Vector2.Distance(origin,
                    GearRadialMenuWidget.ColumnSlotCenterPoints(origin, 1f, i));
                Assert.Greater(d, previous,
                    $"세로 일렬에서 {i}번 칸이 {i - 1}번보다 가깝습니다 — 번호 순 거리 정렬이 깨졌습니다.");
                previous = d;
            }
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

        // ====================================================================
        // ★★ 호버 이름표가 «자기 버튼»에 가장 가깝다 (2026-09-06 debugger 신고)
        // ====================================================================

        /// <summary>
        /// 배치가 끝난 뒤 <see cref="GearRadialMenuWidget.HoverLabelRingRadiusPoints"/>에 들어가는 값 —
        /// <c>FinalizeLayout</c>이 «실제로 놓인 버튼까지의 최대 거리»를 재는 것과 같다.
        /// 평행이동은 <b>전 버튼을 같은 벡터로</b> 옮기므로 원점 기준 거리는 슬롯 궤도 그대로다.
        /// <para>★ 궤도를 <b>프로덕션 함수에서</b> 가져온다 — 숫자를 베끼면 그 사본이 조용히 갈라진다.</para>
        /// </summary>
        private static float RingRadiusFor(float diameter)
        {
            float farthest = 0f;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                farthest = Mathf.Max(farthest, GearRadialMenuWidget.SlotRadiusPoints(i));
            }
            return GearRadialMenuWidget.HoverLabelRingRadius(farthest, diameter);
        }

        /// <summary>
        /// ★★★ <b>이름표는 자기 버튼 옆에 뜬다.</b> 2026-09-06 debugger 확정 결함:
        /// 호 슬롯 1·2(±15°)의 이름표가 <b>자기 버튼보다 [앱 종료]에 1.5배 더 가까이</b> 떴다.
        ///
        /// <para><b>근본 원인</b>: <c>_labelRingRadiusPoints</c>가 <b>전역 단일 링</b>이고, 그 링은
        /// «가장 바깥 버튼»에서 나온다. 위성이 궤도 168로 나가 있으면 링이 198pt가 되는데 호 버튼은
        /// 111pt에 있으므로, 호 버튼의 이름표가 자기 원에서 <b>87pt</b>나 떨어진 자리(= 위성 옆)에
        /// 놓인다. 이름표는 «지금 커서가 올라간 버튼이 무엇인가»를 말하는 물건이라, 다른 버튼 옆에
        /// 뜨면 그 문장이 <b>틀린 말</b>이 된다.</para>
        ///
        /// <para><b>이 단언이 왜 폭에 무관해야 하는가</b>: 알약 폭은 글자에 따라 34~80pt로 변한다.
        /// 특정 폭 하나로 재면 다음 라운드에 대사가 한 글자 길어졌을 때 조용히 뒤집힌다 —
        /// 36-4의 「폭 무관 보장」과 같은 이유로 <b>폭을 훑는다</b>.</para>
        ///
        /// <para>여기서는 화면을 충분히 크게 잡아 <b>클램프가 걸리지 않는 순수 기하</b>를 잰다.
        /// 클램프가 걸리는 구성은 아래 별도 테스트가 «현실 화면»으로 따로 본다.</para>
        /// </summary>
        [Test]
        public void 호버_이름표는_다른_어떤_버튼보다_자기_버튼에_가깝다()
        {
            // 클램프가 절대 안 걸리도록 충분히 큰 화면 + 한복판 앵커.
            var screen = new Vector2(4000f, 4000f);
            var gear = new Vector2(2000f, 2000f);
            float d = GearRadialMenuWidget.ButtonDiameterPoints;
            float ring = RingRadiusFor(d);

            int checkedSamples = 0;
            float worstMargin = float.MaxValue;
            string worstWhere = null;

            for (float baseDegrees = 0f; baseDegrees < 360f; baseDegrees += 15f)
            {
                for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
                {
                    Vector2 own = GearRadialMenuWidget.SlotCenterPoints(gear, baseDegrees, i);

                    for (float pillWidth = 34f; pillWidth <= 80f; pillWidth += 2f)
                    {
                        Vector2 label = GearRadialMenuWidget.HoverLabelCenterPoints(
                            own, gear, ring, d, columnLayout: false,
                            labelSizePoints: new Vector2(pillWidth, GearRadialMenuWidget.HoverLabelHeightPoints),
                            screenPoints: screen,
                            leftMargin: GearRadialMenuWidget.ScreenMarginPoints,
                            rightMargin: GearRadialMenuWidget.ScreenMarginPoints,
                            bottomMargin: GearRadialMenuWidget.ScreenMarginPoints,
                            topMargin: GearRadialMenuWidget.TopMarginPoints);

                        float toOwn = Vector2.Distance(label, own);
                        for (int j = 0; j < GearRadialMenuWidget.ButtonCount; j++)
                        {
                            if (j == i) continue;
                            float toOther = Vector2.Distance(
                                label, GearRadialMenuWidget.SlotCenterPoints(gear, baseDegrees, j));
                            checkedSamples++;
                            float margin = toOther - toOwn;
                            if (margin >= worstMargin) continue;
                            worstMargin = margin;
                            worstWhere = $"θ₀={baseDegrees:F0}도 · 슬롯 {i}({NameOfSlot(i)})의 이름표(폭 {pillWidth:F0}pt)가 " +
                                $"자기 버튼에서 {toOwn:F2}pt인데 슬롯 {j}({NameOfSlot(j)})에서는 {toOther:F2}pt";
                        }
                    }
                }
            }

            Assert.Greater(checkedSamples, 0, "표본이 0건입니다 — 이 단언은 아무것도 재지 않았습니다.");
            Assert.Greater(worstMargin, 0f,
                $"호버 이름표가 자기 버튼보다 다른 버튼에 더 가깝습니다({worstMargin:F2}pt 역전). {worstWhere}. " +
                "이름표는 «지금 커서가 올라간 버튼이 무엇인가»를 말하는 물건이라, 다른 버튼 옆에 뜨면 " +
                "그 문장 자체가 틀린 말이 됩니다. 원인은 링 반지름이 «가장 바깥 버튼» 하나에서 나오는데 " +
                "슬롯마다 궤도가 다른 것입니다 — 궤도가 갈라져 있으면 전역 단일 링은 구조적으로 틀립니다.");

            Debug.Log($"[부채꼴기하-TEST] 이름표 최근접 판정 — 표본 {checkedSamples}건, " +
                      $"최악 여유 {worstMargin:F2}pt (링 {ring:F1}pt).");
        }

        /// <summary>
        /// ★ 위 보장이 <b>현실 화면 + 예약 띠</b>에서도 서는가.
        ///
        /// <para><b>표본 자격을 두 겹으로 거른다</b> — 둘 다 «프로덕션이 실제로 만드는 배치만 본다»는
        /// 같은 규율이고, <c>GearMenuHoverLabelGeometryTests</c>가 이미 쓰는 관례 그대로다:</para>
        /// <list type="number">
        ///   <item><b>버튼이 화면 밖인 배치는 뺀다.</b> 배치 사다리는 그런 각도를 <b>채택하지 않는다</b>
        ///     (회전 → 평행이동 → 축소 → 세로 일렬로 도망간다). 안 거르면 제품에서 일어나지 않는
        ///     자리에서 빨개진다 — 실제로 이 테스트의 초판이 그랬다(33건 전부 그런 표본이었다).</item>
        ///   <item><b>이름표 클램프가 걸린 표본은 빼되 <u>세어서</u> 보고한다.</b> 프로덕션이 그 경우
        ///     «화면 밖으로 나간 이름표는 아예 읽을 수 없으므로 그쪽이 먼저다»로 <b>보장을 명시적으로
        ///     포기</b>한 자리다. 클램프 여부는 클램프가 물 수 없는 큰 화면으로 <b>같은 프로덕션 함수</b>를
        ///     한 번 더 불러 비교해서 판정한다 — 클램프 식을 여기 베껴 적지 않는다.</item>
        /// </list>
        ///
        /// <para>★ 거른 뒤의 표본 수를 <b>하한으로 못박는다</b>. 필터가 너무 세게 물어 표본이 0이 되면
        /// 이 테스트는 «무엇이든 통과»가 되는데, 그 실패는 초록으로 생겨서 조용하다.</para>
        /// </summary>
        [Test]
        public void 이름표_최근접_보장은_현실_화면과_예약띠에서도_선다()
        {
            var screens = new[] { new Vector2(1512f, 982f), new Vector2(1366f, 768f), new Vector2(1920f, 1080f) };
            // (좌, 우, 하, 상) — macOS 메뉴바 / Windows 하단·우측 도킹.
            var margins = new[]
            {
                new Vector4(8f, 8f, 8f, 40f),
                new Vector4(8f, 8f, 48f, 40f),
                new Vector4(8f, 93f, 8f, 40f),
            };

            float d = GearRadialMenuWidget.ButtonDiameterPoints;
            float ring = RingRadiusFor(d);
            var size = new Vector2(57f, GearRadialMenuWidget.HoverLabelHeightPoints);
            int judged = 0, clamped = 0, offScreenLayouts = 0, violations = 0;
            string firstViolation = null;

            foreach (Vector2 screen in screens)
            {
                foreach (Vector4 m in margins)
                {
                    for (float x = 120f; x <= screen.x - 120f; x += 60f)
                    for (float y = 120f; y <= screen.y - 120f; y += 60f)
                    {
                        var gear = new Vector2(x, y);
                        float baseDegrees = GearRadialMenuWidget.SnapFanBaseAngle(
                            gear, screen, GearRadialMenuWidget.FanUpBiasPoints);

                        // ── 자격 ①: 사다리가 이 각도를 그대로 채택할 수 있는 배치인가.
                        bool fits = true;
                        for (int i = 0; i < GearRadialMenuWidget.ButtonCount && fits; i++)
                        {
                            Rect b = GearRadialMenuWidget.ButtonClampBox(
                                GearRadialMenuWidget.SlotCenterPoints(gear, baseDegrees, i), d);
                            fits = b.xMin >= m.x && b.yMin >= m.z
                                   && b.xMax <= screen.x - m.y && b.yMax <= screen.y - m.w;
                        }
                        if (!fits) { offScreenLayouts++; continue; }

                        for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
                        {
                            Vector2 own = GearRadialMenuWidget.SlotCenterPoints(gear, baseDegrees, i);

                            Vector2 label = GearRadialMenuWidget.HoverLabelCenterPoints(
                                own, gear, ring, d, false, size, screen, m.x, m.y, m.z, m.w);

                            // ── 자격 ②: 클램프가 걸렸는가. 같은 프로덕션 함수를 «클램프 불가» 인자로
                            //    한 번 더 불러 비교한다(여백 하한도 함께 밀어내야 좌·하단 클램프가 잡힌다).
                            Vector2 unclamped = GearRadialMenuWidget.HoverLabelCenterPoints(
                                own, gear, ring, d, false, size, new Vector2(1e6f, 1e6f),
                                -1e6f, -1e6f, -1e6f, -1e6f);
                            if ((label - unclamped).sqrMagnitude > 1e-6f) { clamped++; continue; }

                            judged++;
                            float toOwn = Vector2.Distance(label, own);
                            for (int j = 0; j < GearRadialMenuWidget.ButtonCount; j++)
                            {
                                if (j == i) continue;
                                float toOther = Vector2.Distance(
                                    label, GearRadialMenuWidget.SlotCenterPoints(gear, baseDegrees, j));
                                if (toOther > toOwn) continue;
                                violations++;
                                firstViolation ??= $"화면 {screen.x:F0}x{screen.y:F0} · 앵커({x:F0},{y:F0}) · " +
                                    $"θ₀={baseDegrees:F0}도 · 슬롯 {i}({NameOfSlot(i)})의 이름표가 " +
                                    $"자기 버튼 {toOwn:F2}pt vs 슬롯 {j}({NameOfSlot(j)}) {toOther:F2}pt";
                            }
                        }
                    }
                }
            }

            // 무효 측정 방지 — 필터가 다 먹어 «잰 것이 없는데 초록»이 되지 못하게 한다.
            Assert.Greater(judged, 2000,
                $"판정한 이름표가 {judged}개뿐입니다(클램프 제외 {clamped} / 배치 제외 {offScreenLayouts}) — " +
                "필터가 스윕을 통째로 먹었고, 아래 판정은 무효입니다.");
            Assert.AreEqual(0, violations,
                $"이름표가 다른 버튼에 더 가까운 표본이 {violations}건입니다(판정 {judged}건 중). " +
                $"첫 사례: {firstViolation}");

            Debug.Log($"{"[부채꼴기하-TEST]"} 현실 화면 이름표 스윕 — 판정 {judged}건 / 클램프 제외 {clamped}건 / " +
                      $"화면 밖 배치 제외 {offScreenLayouts}건 / 역전 {violations}건.");
        }

        /// <summary>
        /// ★★ <b>전역 단일 링이 성립하는 전제</b>를 못박는다 — 위 두 테스트가 초록인 이유가
        /// «우연»이 아니라 «구조»임을 보이는 자리다.
        ///
        /// <para><c>_labelRingRadiusPoints</c>는 <b>가장 바깥 버튼</b> 하나에서 나오는 값 하나다.
        /// 그것이 <b>모든</b> 슬롯에 대해 정확하려면 <b>모든 슬롯이 같은 궤도를 돌아야 한다</b>.
        /// 궤도가 갈라지는 순간(예: 옛 위성 168pt) 안쪽 궤도 버튼의 이름표는 자기 원에서
        /// «바깥 궤도 − 안쪽 궤도»만큼 떠 버린다 — 그것이 2026-09-06 신고의 정체다.</para>
        ///
        /// <para>★ <b>그래서 슬롯별 링을 만들지 않았다.</b> 지금은 궤도가 하나뿐이라 슬롯별 링이
        /// 전역 링과 <b>비트 동일</b>하고, 쓰이지 않을 분기를 미리 깔면 그 코드는 반드시 썩는다.
        /// 대신 <b>전제 자체</b>를 여기서 잠근다 — 누군가 다시 궤도를 가르면 이 테스트가
        /// «그때는 슬롯별 링이 필요하다»고 말해 준다.</para>
        /// </summary>
        [Test]
        public void 전역_단일_이름표_링의_전제인_단일_궤도가_유지된다()
        {
            float first = GearRadialMenuWidget.SlotRadiusPoints(0);
            for (int i = 1; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.AreEqual(first, GearRadialMenuWidget.SlotRadiusPoints(i), 0.001f,
                    $"{i}번 슬롯의 궤도가 {GearRadialMenuWidget.SlotRadiusPoints(i):F0}pt로 0번({first:F0}pt)과 " +
                    "다릅니다. 호버 이름표 링은 «가장 바깥 버튼» 하나에서 나오는 전역 값 하나라, 궤도가 " +
                    "갈라지면 안쪽 궤도 버튼의 이름표가 자기 원에서 그 차이만큼 떠 다른 버튼 옆에 붙습니다 " +
                    "(2026-09-06 사용자 신고의 정체). 궤도를 다시 가르려면 " +
                    "GearRadialMenuWidget.FinalizeLayout의 링을 «슬롯별»로 바꾸는 작업이 함께 와야 합니다.");
            }

            // 같은 사실을 <b>다른 축</b>에서 한 번 더 잰다 — 각도에도 예외 슬롯이 없어야 «한 호»다.
            for (int i = 1; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float gap = GearRadialMenuWidget.SlotOffsetDegrees(i - 1)
                            - GearRadialMenuWidget.SlotOffsetDegrees(i);
                Assert.AreEqual(GearRadialMenuWidget.ButtonAngleStepDegrees, gap, 0.001f,
                    $"슬롯 {i - 1}→{i}의 각 간격이 {gap:F2}도로 균일하지 않습니다 — 궤도는 하나인데 " +
                    "각도에 예외가 있으면 «한 호»가 아니고, 이름표 방향(원점→버튼)도 그만큼 어긋납니다.");
            }
        }

        private static string NameOfSlot(int index) => index switch
        {
            (int)GearMenuButton.FocusMode => "집중 모드",
            (int)GearMenuButton.Character => "캐릭터",
            (int)GearMenuButton.Todo => "오늘 할일",
            (int)GearMenuButton.Action => "행동",
            (int)GearMenuButton.Quit => "앱 종료",
            _ => $"슬롯{index}",
        };

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
