using NUnit.Framework;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>절대 불변 원칙 2 위반 회귀</b> (2026-09-02) — 팝오버가 macOS 메뉴바를 <b>21pt 덮었다</b>.
    ///
    /// ============================================================================
    /// 재현 좌표는 전부 실측이다 (macOS 15.6 / 1512×982pt / Retina 배율 2 / 2026-09-01 빌드 캡처)
    /// ============================================================================
    /// <list type="bullet">
    ///  <item>[행동 명령] 팝오버 480×560pt가 상단 <c>y = 12pt</c>에 앉았다.</item>
    ///  <item>macOS 메뉴바는 <c>y 0 ~ 33pt</c> → <b>겹침 21pt</b>.</item>
    ///  <item>겹치는 가로 구간 <c>x 808 ~ 1284</c>는 제어센터·입력기·WiFi·배터리·검색·시계 자리라
    ///        그 아이콘들의 <b>아래 2/3(21/33)</b>가 잘려 보였다.</item>
    ///  <item>원인은 한 줄이다 — 옛 <c>PopoverPanel.UpdatePlacement</c>가 <b>네 변에 똑같이</b>
    ///        12pt를 줬다. 화면의 네 변은 대칭이 아닌데 대칭으로 다뤘다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 이 검사가 <b>실제로 무엇인가를 잡는지</b> 증명한다
    /// ============================================================================
    /// 이 파일의 검사는 전부 짝을 이룬다: <b>인셋을 0으로 되돌리면(=고치기 전) 옛 증상이 그대로
    /// 재현되어야 한다</b>. 그 짝이 없으면 "수정 후 통과"는 입력이 애초에 무해했던 것인지 진짜로
    /// 고쳐진 것인지 구분하지 못한다(이 저장소가 하룻밤에 여섯 번 겪은 실패 유형).
    ///
    /// <para>단위: 이 파일은 <b>OS 포인트</b>로 잰다. 실제 호출부는 픽셀(배율 2)로 부르지만
    /// <see cref="SurfaceSafeAreaPolicy"/>는 단위를 모르는 순수 함수라 결과가 같다 — 오히려
    /// 포인트로 재야 실측 표(45pt / 21pt)와 눈으로 대조된다.</para>
    /// </summary>
    public sealed class SurfaceSafeAreaPolicyTests
    {
        // ---- 실측 좌표 (macOS 15.6, 2026-09-01) ----
        private const float ScreenHeightPoints = 982f;
        private const float MenuBarHeightPoints = 33f;      // CGDisplayBounds 982 − visibleFrame(75+874)
        private const float DockThicknessPoints = 75f;      // visibleFrame.y — 캐릭터의 발판이다
        private const float ActionPopoverHeight = 560f;
        private const float MeasuredOldTopEdge = 12f;       // 고치기 전 팝오버 상단 OS y
        private const float MeasuredOverlap = MenuBarHeightPoints - MeasuredOldTopEdge;   // 21pt

        /// <summary>화면 여백은 <b>프로덕션 상수를 참조</b>한다(숫자로 베끼지 않는다 — CLAUDE.md).</summary>
        private const float Margin = PopoverPanel.ScreenMarginPoints;

        /// <summary>부채꼴이 화면 위쪽에서 열렸을 때 팝오버가 가고 싶어 하는 자리 = 화면 맨 위.
        /// (옛 코드는 여기서 <c>margin + half</c>로 잘려 상단 12pt에 앉았다.)</summary>
        private static float DesiredCenterYPushedToTop
            => ScreenHeightPoints - ActionPopoverHeight * 0.5f;   // 상단 y=0을 요구하는 값

        private static float TopEdgeOf(float centerY)
            => SurfaceSafeAreaPolicy.TopEdgeFromScreenTop(centerY, ActionPopoverHeight, ScreenHeightPoints);

        // ==================== ① 상단 — 메뉴바를 덮지 않는다 ====================

        [Test]
        public void 상단_인셋을_알면_팝오버가_메뉴바_아래_45pt에_앉는다()
        {
            float centerY = SurfaceSafeAreaPolicy.ClampCenterY(DesiredCenterYPushedToTop,
                ActionPopoverHeight, ScreenHeightPoints, MenuBarHeightPoints, Margin);

            float topEdge = TopEdgeOf(centerY);

            Assert.AreEqual(MenuBarHeightPoints + Margin, topEdge, 0.01f,
                $"팝오버 상단이 {topEdge:F2}pt입니다 — 메뉴바 하단({MenuBarHeightPoints}) + 여백({Margin}) " +
                $"= {MenuBarHeightPoints + Margin}pt여야 합니다.");
            Assert.GreaterOrEqual(topEdge, MenuBarHeightPoints,
                "팝오버가 아직 메뉴바를 덮습니다 — 절대 불변 원칙 2 위반입니다.");
        }

        /// <summary>★ 네거티브 컨트롤. 인셋을 0으로 되돌리면(= 이 라운드 이전) <b>실측 그대로</b>
        /// 상단 12pt / 겹침 21pt가 재현되어야 한다. 재현되지 않으면 위 검사는 아무것도 잡지 않는다.</summary>
        [Test]
        public void 네거티브_컨트롤_인셋_0이면_옛_증상_상단12pt_겹침21pt가_그대로_재현된다()
        {
            float centerY = SurfaceSafeAreaPolicy.ClampCenterY(DesiredCenterYPushedToTop,
                ActionPopoverHeight, ScreenHeightPoints, 0f, Margin);

            float topEdge = TopEdgeOf(centerY);

            Assert.AreEqual(MeasuredOldTopEdge, topEdge, 0.01f,
                $"인셋 0에서 상단이 {topEdge:F2}pt입니다 — 실측된 옛 증상(12pt)이 재현되지 않으면 " +
                "①의 통과는 '고쳤다'가 아니라 '원래 무해했다'일 수 있습니다.");
            Assert.AreEqual(MeasuredOverlap, MenuBarHeightPoints - topEdge, 0.01f,
                "옛 겹침 21pt가 재현되지 않았습니다.");
        }

        // ==================== ② 하단 — 강제하지 않는다 (Dock은 발판이다) ====================

        [Test]
        public void 하단은_강제하지_않는다_Dock_위로_예전과_똑같이_내려간다()
        {
            float desiredBottom = ActionPopoverHeight * 0.5f - 500f;   // 화면 아래로 밀어붙이는 값

            float withMenuBar = SurfaceSafeAreaPolicy.ClampCenterY(desiredBottom,
                ActionPopoverHeight, ScreenHeightPoints, MenuBarHeightPoints, Margin);
            float withoutMenuBar = SurfaceSafeAreaPolicy.ClampCenterY(desiredBottom,
                ActionPopoverHeight, ScreenHeightPoints, 0f, Margin);

            Assert.AreEqual(withoutMenuBar, withMenuBar, 0.001f,
                "상단 인셋이 <b>아래쪽</b> 한계를 움직였습니다 — Dock 띠는 이 앱이 의도적으로 쓰는 " +
                "캐릭터 발판(Core/DockGeometry)이라 두 띠를 같은 규칙으로 묶으면 발판 설계와 " +
                "정면충돌합니다(41-1 ②).");
            Assert.AreEqual(Margin, withMenuBar - ActionPopoverHeight * 0.5f, 0.01f,
                "아래쪽 한계가 '여백 + 반높이'가 아닙니다.");

            // 그리고 그 자리는 실제로 Dock 띠 위에 걸친다 — "강제하지 않는다"의 의미가 이것이다.
            float bottomEdgeFromScreenBottom = withMenuBar - ActionPopoverHeight * 0.5f;
            Assert.Less(bottomEdgeFromScreenBottom, DockThicknessPoints,
                "이 검사의 전제(팝오버가 Dock 띠에 닿을 수 있다)가 성립하지 않습니다.");
        }

        // ==================== ③ 표면이 안전 영역보다 클 때 — 상단 우선 ====================

        [Test]
        public void 표면이_안전영역보다_크면_상단을_고정하고_아래로_넘긴다()
        {
            float tall = ScreenHeightPoints;   // 화면 전체 높이짜리 표면(안전 영역보다 확실히 크다)

            float centerY = SurfaceSafeAreaPolicy.ClampCenterY(ScreenHeightPoints * 0.5f,
                tall, ScreenHeightPoints, MenuBarHeightPoints, Margin);

            float topEdge = SurfaceSafeAreaPolicy.TopEdgeFromScreenTop(centerY, tall, ScreenHeightPoints);

            Assert.AreEqual(MenuBarHeightPoints + Margin, topEdge, 0.01f,
                $"넘치는 표면의 상단이 {topEdge:F2}pt입니다 — '가운데로 맞추기'를 고르면 위아래로 반씩 " +
                "잘려 메뉴바를 다시 덮습니다. 상단 우선이 이 규칙의 전부입니다(41-1 ④).");
        }

        // ==================== ④ 톱니(y가 아래로 자라는 계) 어댑터 ====================

        /// <summary>톱니 히트 사각형 반지름 — 시각 반지름 + 히트 패딩(프로덕션과 같은 유도, 실측 19.8pt).</summary>
        private const float GearHitRadius = 19.8f;

        [Test]
        public void 톱니는_메뉴바_안으로_드래그되지_않는다()
        {
            // 사용자가 톱니를 화면 맨 위로 끌었다(= y를 0으로 요구).
            float y = SurfaceSafeAreaPolicy.ClampTopDownCenterY(0f, GearHitRadius * 2f,
                ScreenHeightPoints, MenuBarHeightPoints, 0f);

            Assert.AreEqual(MenuBarHeightPoints + GearHitRadius, y, 0.01f,
                $"톱니 중심이 y={y:F2}pt입니다 — 히트 사각형 상단이 메뉴바 하단({MenuBarHeightPoints})보다 " +
                "위로 올라가면 그 자리가 <b>저장되어 재부팅해도 유지된다</b>(41-1 ③ / 41-8).");
            // 부동소수 여유 0.01pt — 이 앱의 어떤 렌더 경로도 0.01pt를 그리지 못한다.
            Assert.GreaterOrEqual(y - GearHitRadius, MenuBarHeightPoints - 0.01f,
                "톱니 히트 사각형이 메뉴바를 덮습니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 인셋 0이면 옛 결함(히트 사각형을 메뉴바 <b>안에 통째로</b>
        /// 집어넣을 수 있다)이 그대로 재현된다.</summary>
        [Test]
        public void 네거티브_컨트롤_인셋_0이면_톱니가_메뉴바_안에_통째로_들어간다()
        {
            float y = SurfaceSafeAreaPolicy.ClampTopDownCenterY(0f, GearHitRadius * 2f,
                ScreenHeightPoints, 0f, 0f);

            Assert.AreEqual(GearHitRadius, y, 0.01f,
                "인셋 0에서 옛 하한(minY = r)이 재현되지 않았습니다.");

            // 겹침을 실제로 센다 — "위에 있다"가 아니라 "몇 pt를 덮는다"를 재야 등급을 말할 수 있다.
            float overlap = Mathf.Min(MenuBarHeightPoints, y + GearHitRadius) - Mathf.Max(0f, y - GearHitRadius);
            Assert.AreEqual(MenuBarHeightPoints, overlap, 0.01f,
                $"옛 증상(톱니가 메뉴바 0~{MenuBarHeightPoints}pt를 세로로 <b>전부</b> 덮는다)이 " +
                "재현되지 않으면 위 검사는 아무것도 잡지 않습니다.");
        }

        [Test]
        public void 톱니의_아래쪽_한계는_인셋과_무관하다()
        {
            float withInset = SurfaceSafeAreaPolicy.ClampTopDownCenterY(ScreenHeightPoints + 999f,
                GearHitRadius * 2f, ScreenHeightPoints, MenuBarHeightPoints, 0f);
            float withoutInset = SurfaceSafeAreaPolicy.ClampTopDownCenterY(ScreenHeightPoints + 999f,
                GearHitRadius * 2f, ScreenHeightPoints, 0f, 0f);

            Assert.AreEqual(withoutInset, withInset, 0.001f,
                "상단 인셋이 톱니의 아래쪽 한계를 움직였습니다 — 이 라운드는 위쪽만 건드립니다.");
            Assert.AreEqual(ScreenHeightPoints - GearHitRadius, withInset, 0.01f,
                "톱니 아래쪽 한계가 예전(screen.y − r)과 달라졌습니다.");
        }

        // ==================== ⑤ 정보창(화면 중앙 원점) 어댑터 ====================

        private const float InfoWindowHeight = 861f;
        private const float InfoWindowMargin = 16f;   // CharacterInfoWindow.ScreenMargin 실측

        [Test]
        public void 정보창은_위로_끌어도_메뉴바를_덮지_않는다()
        {
            float offset = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(9999f, InfoWindowHeight,
                ScreenHeightPoints, MenuBarHeightPoints, InfoWindowMargin);

            float topEdge = (ScreenHeightPoints - InfoWindowHeight) * 0.5f - offset;

            Assert.AreEqual(MenuBarHeightPoints + InfoWindowMargin, topEdge, 0.01f,
                $"정보창 상단이 {topEdge:F2}pt입니다 — 메뉴바 하단 + 여백이어야 합니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 인셋 0이면 정보창도 상단 16pt까지 올라가 메뉴바를 17pt 덮는다.
        /// 이 결함은 페르소나 보고에 없었다(코드에서 찾았다). 그래서 더더욱 짝이 필요하다.</summary>
        [Test]
        public void 네거티브_컨트롤_인셋_0이면_정보창이_메뉴바를_17pt_덮는다()
        {
            float offset = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(9999f, InfoWindowHeight,
                ScreenHeightPoints, 0f, InfoWindowMargin);

            float topEdge = (ScreenHeightPoints - InfoWindowHeight) * 0.5f - offset;

            Assert.AreEqual(InfoWindowMargin, topEdge, 0.01f,
                "인셋 0에서 옛 대칭 클램프(상단 = 여백)가 재현되지 않았습니다.");
            Assert.AreEqual(17f, MenuBarHeightPoints - topEdge, 0.01f,
                "옛 겹침 17pt가 재현되지 않으면 위 검사는 아무것도 잡지 않습니다.");
        }

        [Test]
        public void 정보창의_아래쪽_이동_한계는_인셋과_무관하다()
        {
            float withInset = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(-9999f, InfoWindowHeight,
                ScreenHeightPoints, MenuBarHeightPoints, InfoWindowMargin);
            float withoutInset = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(-9999f, InfoWindowHeight,
                ScreenHeightPoints, 0f, InfoWindowMargin);

            Assert.AreEqual(withoutInset, withInset, 0.001f,
                "상단 인셋이 정보창의 아래쪽 이동 한계를 움직였습니다.");
        }

        // ==================== ⑥ 병적인 입력에서 죽지 않는다 ====================

        [Test]
        public void 화면_높이가_없거나_숫자가_아니면_요청값을_그대로_돌려준다()
        {
            const float desired = 123.5f;

            Assert.AreEqual(desired,
                SurfaceSafeAreaPolicy.ClampCenterY(desired, 100f, 0f, 33f, 12f), 0.001f,
                "화면 높이 0에서 값을 지어냈습니다 — 관측이 없을 때는 아무것도 하지 않습니다.");
            Assert.AreEqual(desired,
                SurfaceSafeAreaPolicy.ClampCenterY(desired, 100f, float.NaN, 33f, 12f), 0.001f,
                "NaN 화면 높이에서 값을 지어냈습니다.");
            Assert.AreEqual(desired,
                SurfaceSafeAreaPolicy.ClampCenterY(desired, 100f, 982f, float.NaN, 12f), 0.001f,
                "NaN 인셋에서 값을 지어냈습니다 — 조회 실패는 '추정하라'가 아닙니다.");
            Assert.IsFalse(float.IsNaN(SurfaceSafeAreaPolicy.ClampTopDownCenterY(50f, 40f, 982f, 33f, 0f)),
                "y-down 어댑터가 NaN을 냈습니다.");
        }

        [Test]
        public void 음수_인셋과_음수_여백은_0으로_접는다()
        {
            float a = SurfaceSafeAreaPolicy.ClampCenterY(DesiredCenterYPushedToTop,
                ActionPopoverHeight, ScreenHeightPoints, -50f, -50f);
            float b = SurfaceSafeAreaPolicy.ClampCenterY(DesiredCenterYPushedToTop,
                ActionPopoverHeight, ScreenHeightPoints, 0f, 0f);

            Assert.AreEqual(b, a, 0.001f,
                "음수 인셋/여백이 표면을 화면 밖으로 밀어냈습니다 — 잘못된 관측이 들어와도 " +
                "지금보다 나빠지지 않아야 합니다.");
        }

        // ==================== ⑦ 인셋 조회 실패는 0이다 (짐작하지 않는다) ====================

        [Test]
        public void 상단_인셋을_못_물으면_0이고_배치는_예전과_같다()
        {
            ReservedTopBarProbe.ResetForTests();
            try
            {
                // 플랫폼 서비스가 없다 = 에디터/모바일. 예외 없이 0이어야 한다.
                Assert.AreEqual(0f, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                    "상단 예약 띠를 못 물었는데 0이 아닌 값을 냈습니다 — 짐작값이 실제보다 크면 " +
                    "멀쩡한 화면 위쪽을 낭비하고, 작으면 그대로 덮습니다. 둘 다 나쁩니다.");
            }
            finally { ReservedTopBarProbe.ResetForTests(); }
        }

        [Test]
        public void 테스트_주입_인셋은_즉시_반영되고_되돌릴_수_있다()
        {
            ReservedTopBarProbe.ResetForTests();
            try
            {
                ReservedTopBarProbe.SetInsetPointsForTests(MenuBarHeightPoints);
                Assert.AreEqual(MenuBarHeightPoints, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                    "주입한 인셋이 반영되지 않으면 PlayMode에서 메뉴바 없이 클램프를 밀어 볼 수 없습니다.");

                ReservedTopBarProbe.ResetForTests();
                Assert.AreEqual(0f, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                    "주입값이 남았습니다 — 정적 상태가 다음 테스트로 샙니다.");
            }
            finally { ReservedTopBarProbe.ResetForTests(); }
        }
    }
}
