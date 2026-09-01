using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 사용자 신고 회귀 (2026-09-01) — <b>"크기를 키웠을때 작은 졸라맨은 독아래에 있으면 공중에 떠 있음"</b>
    ///
    /// ============================================================================
    /// 무엇이 틀렸었나
    /// ============================================================================
    /// <see cref="CharacterPetRenderer"/>의 화면 클램프가 <b>스칼라 여백 하나</b>를 가로·세로에 그대로
    /// 썼다. 그 규약은 원점이 <b>중심</b>인 도형(공)에만 옳은데, 리틀스틱메이트/달팽이는 원점이
    /// <b>발바닥</b>이다. 그래서 세로 하한이 <c>화면바닥 + 자기키/2</c>가 되어, 발판이 화면 바닥에서
    /// 그보다 가까우면 펫만 위로 밀려 올라갔다.
    ///
    /// 이 파일이 잠그는 것은 두 가지이며, <b>둘 다 있어야</b> 재발을 막는다:
    ///   (1) 도형의 원점 규약 — 미니의 발바닥이 정확히 y=0이다. 이게 깨지면 접지 계산 전체가 무너진다.
    ///       (본체 곡선화/두께 라운드가 <c>MiniFigure</c>를 건드릴 때 이 단언이 먼저 운다.)
    ///   (2) 클램프 산술 — 발바닥 원점 펫은 세로 여백이 0이라 <b>어떤 배율에서도</b> 지면을 떠나지 않는다.
    ///
    /// ============================================================================
    /// 실측 기준값 (2026-09-01, PID 11451 라이브 계측)
    /// ============================================================================
    /// 1512x982pt / orthographicSize 12 / 배율 1.5에서
    ///   · 화면바닥 y=−12.000, Dock 상단 y=−10.167, <b>바닥 안전망 상단 y=−11.804</b>
    ///   · 수정 전 부양량 23.4pt(계산) / 23.47pt(화면 캡처 실측) — 자기 키의 37%
    /// 아래 테스트는 그 숫자를 <b>베끼지 않고</b> 프로덕션 상수에서 다시 유도한다
    /// (CLAUDE.md: 테스트에 프로덕션 상수를 숫자로 베끼지 않는다).
    /// </summary>
    public sealed class PetGroundContactClampTests
    {
        // 카메라 규약: 세로 24유닛(orthographicSize 12)이 화면 982pt에 대응한다 —
        // 그 환산이 곧 DockGeometry.ReferenceWorldUnitsPerPoint다. 숫자를 새로 적지 않는다.
        private const float ScreenPointsTall = 982f;
        private const float ScreenPointsWide = 1512f;

        private static Rect ReferenceView()
        {
            float halfY = ScreenPointsTall * DockGeometry.ReferenceWorldUnitsPerPoint * 0.5f;
            float halfX = ScreenPointsWide * DockGeometry.ReferenceWorldUnitsPerPoint * 0.5f;
            return new Rect(-halfX, -halfY, halfX * 2f, halfY * 2f);
        }

        private static float MiniHeightAt(float characterScale)
            => StickConfig.BaselineCharacterTotalHeight * characterScale * AppearanceShapeBuilder.MiniScale;

        // ────────────────────────────────────────────────────────────────────────
        // (1) 도형 원점 규약 — 이게 깨지면 아래 산술이 전부 무의미해진다.
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void MiniFigure_원점이_발바닥이다_다리끝이_정확히_y0()
        {
            const float H = 3.4120416f;   // 임의의 키. 아래 단언은 전부 H에 대한 상대값이다.
            foreach (float facing in new[] { 1f, -1f })
            {
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(H, facing);
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3[] part in parts)
                {
                    foreach (Vector3 v in part)
                    {
                        minY = Mathf.Min(minY, v.y);
                        maxY = Mathf.Max(maxY, v.y);
                    }
                }

                Assert.That(minY, Is.EqualTo(0f).Within(1e-4f),
                    $"미니의 가장 낮은 점(facing {facing})이 0이 아니다 = 원점이 발바닥이 아니다. " +
                    "CharacterPetRenderer.TickMini는 ResolveGroundY를 오프셋 없이 그대로 쓰므로, " +
                    "이 값이 0이 아니면 펫이 정확히 그만큼 지면을 뚫거나 뜬다.");
                Assert.That(maxY, Is.EqualTo(H).Within(1e-4f),
                    "미니의 정수리가 키와 다르다 — 세로 뻗음 계산(위 여백)이 어긋난다.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // (2) 클램프 산술 — 신고 그 자체.
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 바닥_안전망_위의_펫은_어떤_배율에서도_뜨지_않는다()
        {
            Rect view = ReferenceView();

            // 바닥 안전망: 화면 최하단에 붙은 얇은 발판(실측 상단 OS y=974 = 화면바닥에서 8pt 위).
            // "화면 바닥에 가장 가까운 발판"이 이 버그의 최악 조건이므로 여백을 0pt까지 훑는다.
            foreach (float groundAboveBottomPoints in new[] { 0f, 4f, 8f, 20f, 40f })
            {
                float groundY = view.yMin + groundAboveBottomPoints * DockGeometry.ReferenceWorldUnitsPerPoint;

                for (float scale = StickConfig.MinCharacterScale;
                     scale <= StickConfig.MaxCharacterScale + 1e-4f;
                     scale += 0.05f)
                {
                    float miniHeight = MiniHeightAt(scale);
                    var p = new Vector2(0f, groundY);

                    CharacterPetRenderer.ClampOriginToRect(ref p, view, miniHeight * 0.5f,
                        CharacterPetRenderer.GroundedPetVerticalMargin,
                        CharacterPetRenderer.GroundedPetVerticalMargin);

                    Assert.That(p.y, Is.EqualTo(groundY).Within(1e-5f),
                        $"배율 {scale:F2} / 발판이 화면바닥 {groundAboveBottomPoints}pt 위일 때 펫이 " +
                        $"{(p.y - groundY) / DockGeometry.ReferenceWorldUnitsPerPoint:F1}pt 떠올랐다. " +
                        "발바닥 원점 펫의 세로 여백은 0이어야 한다(신고: '독아래에 있으면 공중에 떠 있음').");
                }
            }
        }

        [Test]
        public void 옛_대칭여백_규약이었다면_실제로_떠올랐다_테스트가_구분력을_갖는지_확인()
        {
            // 위 테스트가 "무엇을 해도 통과하는" 무력한 테스트가 아님을 증명한다.
            // 옛 규약 = 세로 여백에도 halfWidth(=키/2)를 그대로 넣는 것.
            Rect view = ReferenceView();
            float groundY = view.yMin + 8f * DockGeometry.ReferenceWorldUnitsPerPoint;  // 안전망 실측 높이
            float miniHeight = MiniHeightAt(StickConfig.MaxCharacterScale);
            float half = miniHeight * 0.5f;

            var p = new Vector2(0f, groundY);
            CharacterPetRenderer.ClampOriginToRect(ref p, view, half, half, half);   // ← 옛 규약 재현

            // 폐형식: 부양 = (화면바닥 + 키/2) − 발판상단 = 키/2 − 발판이_바닥에서_뜬_높이.
            // 숫자를 베끼지 않고 그대로 유도해 비교한다 — 상한(MaxCharacterScale)이 앞으로 바뀌어도
            // 이 단언은 따라 움직인다(실제로 2026-09-01에 상한이 1.5 -> 1.0으로 내려갔다).
            float expectedLift = half - (groundY - view.yMin);
            Assert.That(p.y - groundY, Is.EqualTo(expectedLift).Within(1e-5f),
                "옛 규약의 부양량이 폐형식과 다르다 — 이 테스트가 원인을 잘못 짚고 있다.");
            Assert.That(expectedLift, Is.GreaterThan(0.2f * miniHeight),
                $"현재 상한 배율({StickConfig.MaxCharacterScale})에서는 옛 규약으로도 부양이 " +
                "자기 키의 20%에 못 미친다 = 이 회귀 테스트가 구분력을 잃었다. " +
                "상한이 더 내려갔다면 이 테스트의 전제를 다시 세워야 한다.");
        }

        [Test]
        public void 화면_꼭대기_발판_위의_펫도_가라앉지_않는다_부양의_거울상()
        {
            // 최대화된 창의 상단이 화면 꼭대기에 닿으면, 옛 규약은 펫을 키/2만큼 발판 <b>아래로</b>
            // 밀어 넣었다. 같은 결함의 거울상이라 함께 잠근다.
            Rect view = ReferenceView();
            float groundY = view.yMax;
            float miniHeight = MiniHeightAt(StickConfig.MaxCharacterScale);

            var p = new Vector2(0f, groundY);
            CharacterPetRenderer.ClampOriginToRect(ref p, view, miniHeight * 0.5f,
                CharacterPetRenderer.GroundedPetVerticalMargin,
                CharacterPetRenderer.GroundedPetVerticalMargin);

            Assert.That(p.y, Is.EqualTo(groundY).Within(1e-5f),
                "화면 꼭대기 발판에서 펫이 발판 아래로 가라앉았다 — 주인은 그 자리에 서 있으므로 그림이 어긋난다.");
        }

        [Test]
        public void 공은_중심원점이라_대칭여백이_옳다_회귀로_바뀌지_않았는지()
        {
            // 공은 ResolveGroundY + radius로 <b>중심</b>을 올려놓고 여백도 radius다.
            // 이 조합은 어떤 발판에서도 클램프가 걸리지 않는다 — 수정이 공을 건드리지 않았음을 못박는다.
            Rect view = ReferenceView();
            float radius = MiniHeightAt(StickConfig.MaxCharacterScale) * 0.5f;

            foreach (float groundAboveBottomPoints in new[] { 0f, 8f, 75f })
            {
                float groundY = view.yMin + groundAboveBottomPoints * DockGeometry.ReferenceWorldUnitsPerPoint;
                var p = new Vector2(0f, groundY + radius);
                CharacterPetRenderer.ClampOriginToRect(ref p, view, radius, radius, radius);

                Assert.That(p.y, Is.EqualTo(groundY + radius).Within(1e-5f),
                    "중심 원점 + 반지름 여백은 원래 옳았다 — 공의 거동이 바뀌면 안 된다.");
            }
        }

        [Test]
        public void 도형이_화면보다_크면_상하한이_뒤집혀도_중앙으로_보낸다()
        {
            // Mathf.Clamp는 min > max일 때 미정의 결과를 낸다(값이 튄다). 좁은 발판에서
            // ClampToOwnerFoothold가 쓰는 것과 같은 "가운데 세우기" 규약으로 막는다.
            Rect view = ReferenceView();
            float huge = view.height;   // 화면 세로 전체 = 위아래 여백 합이 화면보다 크다.

            var p = new Vector2(0f, 999f);
            CharacterPetRenderer.ClampOriginToRect(ref p, view, huge, huge, huge);

            Assert.That(p.y, Is.EqualTo(view.center.y).Within(1e-4f), "세로 상하한이 뒤집혔을 때 중앙이 아니다.");
            Assert.That(p.x, Is.EqualTo(view.center.x).Within(1e-4f), "가로 상하한이 뒤집혔을 때 중앙이 아니다.");
        }
    }
}
