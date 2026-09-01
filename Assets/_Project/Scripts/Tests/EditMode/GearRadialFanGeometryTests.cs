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
        /// <summary>사용자 지시로 늘어난 4번째 버튼([행동])이 실제로 존재한다.</summary>
        [Test]
        public void 버튼은_네_개이고_행동이_마지막_슬롯이다()
        {
            Assert.AreEqual(4, GearRadialMenuWidget.ButtonCount, "부채꼴 버튼 수가 4가 아닙니다.");
            Assert.AreEqual(3, (int)GearMenuButton.Action,
                "[행동]이 마지막 슬롯이 아닙니다 — 36-3-4는 기존 0/1/2를 재번호하지 말고 끝에 붙이라고 못박았습니다.");

            // 기존 3개의 값이 그대로여야 한다. 재번호되면 그 값을 읽는 switch가 조용히 어긋난다.
            Assert.AreEqual(0, (int)GearMenuButton.FocusMode, "[집중 모드] 슬롯 번호가 바뀌었습니다.");
            Assert.AreEqual(1, (int)GearMenuButton.Character, "[캐릭터] 슬롯 번호가 바뀌었습니다.");
            Assert.AreEqual(2, (int)GearMenuButton.Todo, "[오늘 할일] 슬롯 번호가 바뀌었습니다.");
        }

        /// <summary>36-3-3 확정 기하. 세 값이 서로 맞물려 있어 하나만 바뀌어도 근거가 무너진다.</summary>
        [Test]
        public void 확정_기하는_30도_간격_반지름_111pt_스팬_90도다()
        {
            Assert.AreEqual(30f, GearRadialMenuWidget.ButtonAngleStepDegrees, 0.001f, "각 간격이 30도가 아닙니다.");
            Assert.AreEqual(111f, GearRadialMenuWidget.OrbitRadiusPoints, 0.001f, "궤도 반지름이 111pt가 아닙니다.");
            Assert.AreEqual(44f, GearRadialMenuWidget.ButtonDiameterPoints, 0.001f,
                "버튼 지름이 44pt가 아닙니다 — HIG 최소 타깃이라 줄이지 않기로 했습니다(36-3-3).");

            float span = GearRadialMenuWidget.ButtonAngleStepDegrees * (GearRadialMenuWidget.ButtonCount - 1);
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

            float sum = 0f;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++) sum += GearRadialMenuWidget.SlotOffsetDegrees(i);
            Assert.AreEqual(0f, sum, 0.001f, "슬롯 각도의 합이 0이 아닙니다 — 부채꼴이 기준각 기준 좌우 대칭이 아닙니다.");
        }

        /// <summary>
        /// ★ 기어→버튼 거리가 <b>전부 같다</b>. 36-3-2가 지적한 기존 구현의 결함(84/97/89pt로 제각각)이
        /// 사라졌는지 확인한다 — 이것이 성립해야 "궤도"라는 말이 사실이 된다.
        /// </summary>
        [Test]
        public void 기어에서_네_버튼까지_거리가_모두_같다()
        {
            var gear = new Vector2(1200f, 700f);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float d = Vector2.Distance(gear, GearRadialMenuWidget.SlotCenterPoints(gear, 225f, i));
                Assert.AreEqual(GearRadialMenuWidget.OrbitRadiusPoints, d, 0.01f,
                    $"{i}번 버튼까지의 거리가 궤도 반지름과 다릅니다({d:F1}pt).");
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
        /// ★ "촤르륵"의 예산은 0.30초로 정해져 있다(32-2). 버튼이 3→4로 늘었다고 사용자를 매번 18%
        /// 더 기다리게 만들지 않는다 — 그래서 스태거를 0.055 → 0.037초로 줄였다(36-3-3).
        /// </summary>
        [Test]
        public void 펼침_총_길이가_0_30초_예산을_지킨다()
        {
            Assert.AreEqual(0.037f, GearRadialMenuWidget.ExpandStaggerSeconds, 0.0001f,
                "스태거가 0.037초가 아닙니다 — 4개에서 0.30초 예산을 지키는 값입니다.");
            Assert.AreEqual(0.30f, GearRadialMenuWidget.ExpandTotalSeconds, 0.005f,
                $"펼침 총 길이가 {GearRadialMenuWidget.ExpandTotalSeconds:F3}초입니다 — 32-2의 예산은 0.30초입니다.");
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
