using NUnit.Framework;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 팝오버 <b>화면 클램프</b> 회귀 — 2026-09-08, docs/UX_WIDGETS.md R6-13 ⑤
    /// (persona-stress R-1 / R-2 / R-4).
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가 — 신고 원문과 산술
    /// ============================================================================
    /// R-1: <i>"팝오버에는 <b>화면에 맞춰 줄이는 기구가 없다</b>. 정보창은 ClampPanelToScreen으로
    /// 가로·세로를 줄이지만, PopoverPanel.cs:804의 `_panel.sizeDelta = PanelSizePoints`는 Awake
    /// 1회뿐이고 UpdatePlacement는 <b>중심만</b> 자른다. 고정 상수로 키우면 작은 화면에서 그대로 넘친다."</i>
    ///
    /// R-2: <i>"안전영역보다 커지면 SurfaceSafeAreaPolicy.ClampCenterY가 <b>상단 고정 + 아래로 넘침</b>으로
    /// 바뀐다. Windows 1920×1080@150%(=1280×720pt), 높이 700pt → 중심 358pt → 하단 8pt →
    /// <b>작업표시줄 48pt 띠 중 40pt를 팝오버와 차단막이 덮는다</b>."</i>
    ///
    /// R-4: <i>"기반 클래스는 sizeDelta를 다시 대입하지 않는다 … 잊으면 <b>보이는 창은 그대로인데
    /// 차단막만 커져</b> 아무것도 안 보이는 자리가 클릭을 먹는다."</i>
    ///
    /// ============================================================================
    /// 왜 EditMode에서 잴 수 있는가
    /// ============================================================================
    /// <see cref="PopoverPanel.ResolvePanelSizePoints"/> / <see cref="PopoverPanel.ResolvePanelCenterPoints"/>는
    /// <b>순수 함수</b>다 — <c>Screen</c>도 <c>#if</c>도 읽지 않고 화면 크기와 예약 띠를 <b>인자</b>로 받는다.
    /// 그래서 Windows가 없는 이 개발 머신에서 <b>Windows 쪽 답까지 실행해서</b> 검증한다
    /// (CLAUDE.md 활성 빌드 타깃 사각지대 — <see cref="SurfaceSafeAreaPolicy"/>가 같은 이유로 같은 형태다).
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 파일이 실제로 무언가를 잡는다는 증거)
    /// ============================================================================
    ///  · <see cref="PopoverPanel.ResolvePanelSizePoints"/>의 두 <c>Mathf.Min</c>을 지우고
    ///    <c>desiredSizePoints</c>를 그대로 돌려주게 하면(= 이 라운드 이전의 동작)
    ///    <c>작은_안전영역에서…</c>와 <c>차단막_사각형은…</c>이 <b>실패한다</b>.
    ///  · <c>UpdatePlacement</c>가 하단 인셋을 크기 계산에서 빼지 않게 하면
    ///    <c>하단_예약띠는_Windows에서만…</c>이 실패한다.
    ///
    /// <b>숫자를 베끼지 않는다</b>: 여백/하한/닫기 칩은 전부 프로덕션 상수를 참조한다(CLAUDE.md).
    /// </summary>
    public sealed class PopoverScreenClampTests
    {
        // ★ persona-stress R-2가 실제로 쓴 화면 — Windows 1920×1080 @150% = 1280×720 논리 포인트.
        //   이 둘은 «측정 조건»이지 프로덕션 상수가 아니므로 테스트가 소유한다.
        private static readonly Vector2 WindowsSmallScreenPoints = new Vector2(1280f, 720f);
        private const float WindowsTaskbarPoints = 48f;

        // 이 개발 머신(macOS 15.6) — 메뉴바 33pt, Dock은 인셋 0(설계상, PopoverPanel 503-506행).
        private static readonly Vector2 MacScreenPoints = new Vector2(1512f, 982f);
        private const float MacMenuBarPoints = 33f;

        private static float Margin => PopoverPanel.ScreenMarginPoints;

        /// <summary>클램프를 지난 크기·자리로 실제 패널 사각형을 만든다 — <b>차단막이 덮는 사각형과
        /// 같은 값</b>이다(PopoverPanel.UpdatePlacement가 이 둘을 한 값에서 만든다).</summary>
        private static Rect ResolveRect(Vector2 desiredSize, Vector2 desiredCenter,
            Vector2 screen, float topInset, float bottomInset)
        {
            Vector2 size = PopoverPanel.ResolvePanelSizePoints(desiredSize, screen, topInset, bottomInset, Margin);
            Vector2 center = PopoverPanel.ResolvePanelCenterPoints(size, desiredCenter, screen,
                topInset, bottomInset, Margin);
            // 화면 중앙 원점 → 화면 좌하단 원점.
            Vector2 min = screen * 0.5f + center - size * 0.5f;
            return new Rect(min.x, min.y, size.x, size.y);
        }

        private static void AssertInsideSafeArea(Rect rect, Vector2 screen,
            float topInset, float bottomInset, string what)
        {
            const float Eps = 0.001f;
            Assert.GreaterOrEqual(rect.xMin, Margin - Eps,
                $"{what}: 왼쪽이 화면 여백을 침범했습니다(rect={rect}).");
            Assert.LessOrEqual(rect.xMax, screen.x - Margin + Eps,
                $"{what}: 오른쪽이 화면 여백을 침범했습니다(rect={rect}).");
            Assert.GreaterOrEqual(rect.yMin, bottomInset + Margin - Eps,
                $"{what}: 아래쪽이 OS 예약 띠(두께 {bottomInset})를 덮었습니다 — 팝오버와 " +
                $"클릭관통 차단막이 그 띠 위에 앉습니다(원칙 2). rect={rect}");
            Assert.LessOrEqual(rect.yMax, screen.y - topInset - Margin + Eps,
                $"{what}: 위쪽이 OS 예약 띠(두께 {topInset})를 덮었습니다. rect={rect}");
        }

        /// <summary>
        /// ★ R-1 / R-2 본체 — 안전영역보다 <b>큰</b> 팝오버가 작은 화면에서 넘치지 않는다.
        /// <para>전제(양성 대조)를 먼저 못박는다: 그 크기가 실제로 <b>안 들어간다</b>는 것.
        /// 이게 없으면 아래 단언은 "원래부터 들어가는 창"으로도 통과한다(측정 무효).</para>
        /// </summary>
        [Test]
        public void 작은_안전영역에서_큰_팝오버는_화면_밖으로_나가지_않는다()
        {
            Vector2 screen = WindowsSmallScreenPoints;
            float bottom = SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                RuntimePlatform.WindowsPlayer, WindowsTaskbarPoints);
            var desired = new Vector2(500f, 700f);   // R-2가 쓴 높이 그대로.

            // ---- 전제: 이 크기는 이 화면의 안전영역에 들어가지 않는다 ----
            float safeHeight = screen.y - bottom - Margin * 2f;
            Assert.Less(safeHeight, desired.y,
                $"전제가 무너졌습니다 — 안전 높이 {safeHeight}pt가 창 높이 {desired.y}pt보다 커서 " +
                "이 테스트는 아무것도 재지 못합니다.");

            Vector2 size = PopoverPanel.ResolvePanelSizePoints(desired, screen, 0f, bottom, Margin);

            Assert.AreEqual(desired.x, size.x, 0.001f,
                "가로는 안전영역에 들어가는데 줄었습니다 — 필요 없는 축까지 깎고 있습니다.");
            Assert.AreEqual(safeHeight, size.y, 0.001f,
                $"세로가 안전 높이({safeHeight}pt)로 줄지 않았습니다. 줄이지 않으면 " +
                $"{desired.y - safeHeight}pt가 작업표시줄 위로 나가고 차단막이 그 띠를 덮습니다.");

            // ---- 그 크기로 앉힌 사각형이 실제로 안전영역 안인가 ----
            AssertInsideSafeArea(ResolveRect(desired, Vector2.zero, screen, 0f, bottom),
                screen, 0f, bottom, "중앙에서 연 큰 팝오버");
        }

        /// <summary>
        /// ★ <b>무회귀 증명</b> — 안전영역에 들어가는 팝오버는 <b>한 픽셀도</b> 안 줄어든다.
        /// 이 단언이 없으면 위 클램프가 "항상 조금씩 깎는" 형태로 잘못 구현돼도 아무도 모른다
        /// (지금 출하된 팝오버 3종 전부가 이 집합에 있다).
        /// </summary>
        [Test]
        public void 안전영역에_들어가는_팝오버는_한_픽셀도_안_줄어든다()
        {
            var desired = new Vector2(500f, 512f);   // R6-13 ②가 채택한 크기.
            Vector2 size = PopoverPanel.ResolvePanelSizePoints(desired, MacScreenPoints,
                MacMenuBarPoints, 0f, Margin);

            Assert.AreEqual(desired.x, size.x, 0.001f, "들어가는 창의 가로가 줄었습니다.");
            Assert.AreEqual(desired.y, size.y, 0.001f, "들어가는 창의 세로가 줄었습니다.");
        }

        /// <summary>
        /// ★ 플랫폼 갈림을 <b>한 실행 안에서 양쪽 다</b> 돌린다 — 하단 예약 띠는 Windows에서만 벽이다
        /// (<see cref="SurfaceSafeAreaPolicy.EnforcesBottomReservedBand"/>, 2026-09-05 리더 판정 M-7).
        /// macOS Dock 위는 이 앱이 의도적으로 쓰는 자리라 <b>예전과 비트 단위로 같아야</b> 한다.
        /// </summary>
        [Test]
        public void 하단_예약띠는_Windows에서만_높이를_깎는다()
        {
            Vector2 screen = WindowsSmallScreenPoints;
            var desired = new Vector2(500f, 700f);

            float win = SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                RuntimePlatform.WindowsPlayer, WindowsTaskbarPoints);
            float mac = SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                RuntimePlatform.OSXPlayer, WindowsTaskbarPoints);

            Assert.AreEqual(WindowsTaskbarPoints, win, 0.001f, "전제 — Windows는 하단 띠를 강제한다.");
            Assert.AreEqual(0f, mac, 0.001f, "전제 — macOS는 하단 띠를 강제하지 않는다(Dock은 발판이다).");

            float winHeight = PopoverPanel.ResolvePanelSizePoints(desired, screen, 0f, win, Margin).y;
            float macHeight = PopoverPanel.ResolvePanelSizePoints(desired, screen, 0f, mac, Margin).y;

            Assert.AreEqual(screen.y - win - Margin * 2f, winHeight, 0.001f,
                "Windows에서 작업표시줄 두께가 높이에서 안 빠졌습니다.");
            Assert.AreEqual(screen.y - Margin * 2f, macHeight, 0.001f,
                "macOS에서 Dock 두께만큼 높이가 깎였습니다 — 그 자리는 이 앱이 의도적으로 쓴다.");
            Assert.AreEqual(WindowsTaskbarPoints, macHeight - winHeight, 0.001f,
                "두 플랫폼의 차이가 정확히 작업표시줄 두께가 아닙니다.");
        }

        /// <summary>
        /// ★ 줄이더라도 <b>[✕]가 들어갈 상자</b>는 지킨다 — 2026-09-02부터 바깥 클릭이 창을 닫지
        /// 않으므로 [✕]는 이 앱에서 팝오버를 닫는 <b>유일한 마우스 경로</b>다. 클램프가 그것까지
        /// 삼키면 «영영 닫을 수 없는 창»이 만들어진다.
        /// </summary>
        [Test]
        public void 줄여도_닫기칩이_들어갈_최소_상자는_지킨다()
        {
            // 하한 자체가 [✕]를 담을 수 있는가(정의 검산 — 숫자를 베끼지 않는다).
            Assert.GreaterOrEqual(PopoverPanel.MinPanelWidthPoints,
                PopoverPanel.CloseChipWidth + UiChrome.Space4,
                "폭 하한이 [✕] 칩과 그 오른쪽 여백조차 담지 못합니다.");
            Assert.GreaterOrEqual(PopoverPanel.MinPanelHeightPoints,
                PopoverPanel.CloseChipHeight + UiChrome.Space3,
                "높이 하한이 [✕] 칩과 타이틀 줄 여백조차 담지 못합니다.");

            var absurd = new Vector2(40f, 40f);   // 안전영역이 하한보다도 작은 화면.
            Vector2 size = PopoverPanel.ResolvePanelSizePoints(new Vector2(500f, 512f), absurd, 0f, 0f, Margin);

            Assert.AreEqual(PopoverPanel.MinPanelWidthPoints, size.x, 0.001f,
                "화면이 하한보다 좁을 때 하한 아래로 줄었습니다 — [✕]가 사라집니다.");
            Assert.AreEqual(PopoverPanel.MinPanelHeightPoints, size.y, 0.001f,
                "화면이 하한보다 낮을 때 하한 아래로 줄었습니다 — [✕]가 사라집니다.");
        }

        /// <summary>
        /// ★★ R-4 — <b>차단막이 덮는 사각형 = 보이는 사각형</b>이고, 그 사각형은 어떤 자리에서도
        /// 안전영역 밖으로 나가지 않는다.
        ///
        /// <para>「차단막 = 보이는 것」이 성립하는 이유는 값이 <b>하나</b>이기 때문이다:
        /// <c>UpdatePlacement</c>가 <see cref="PopoverPanel.ResolvePanelSizePoints"/>의 결과를
        /// <c>_panel.sizeDelta</c>·<c>PanelScreenRect</c>·<c>SyncClickBlocker</c> 셋 모두에 쓴다.
        /// 여기서는 그 <b>한 값</b>으로 만든 사각형을 여러 희망 위치에 대해 전수로 잰다 —
        /// 사용자가 창을 화면 밖으로 끌어낸 경우까지 포함한다(드래그는 2026-09-07에 켜졌다).</para>
        /// </summary>
        [Test]
        public void 차단막_사각형은_어느_자리에서도_안전영역_밖으로_안_나간다()
        {
            var desired = new Vector2(500f, 512f);
            float bottom = SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                RuntimePlatform.WindowsPlayer, WindowsTaskbarPoints);

            Vector2[] screens =
            {
                WindowsSmallScreenPoints, MacScreenPoints,
                new Vector2(640f, 480f),   // 배치모드 PlayMode 화면
                new Vector2(60f, 60f),     // 안전영역이 하한 상자보다도 작은 극단 — else 가지를 살려 둔다
            };
            float[] topInsets = { 0f, MacMenuBarPoints };
            float[] bottomInsets = { 0f, bottom };
            // 사용자가 끌어낼 수 있는 자리 — 화면 밖으로 크게 나간 값을 일부러 넣는다.
            Vector2[] centers =
            {
                Vector2.zero,
                new Vector2(-9000f, -9000f), new Vector2(9000f, 9000f),
                new Vector2(-9000f, 9000f), new Vector2(9000f, -9000f),
                new Vector2(0f, -400f), new Vector2(0f, 400f),
            };

            foreach (Vector2 screen in screens)
            foreach (float top in topInsets)
            foreach (float bot in bottomInsets)
            foreach (Vector2 center in centers)
            {
                Rect rect = ResolveRect(desired, center, screen, top, bot);

                // ★ 판정 기준을 <b>결과 크기</b>가 아니라 <b>안전영역이 하한 상자를 담을 수 있는가</b>로
                //   잡는다. 결과 크기로 재면 «클램프가 아예 없는 코드»가 «큰 창이니 넘쳐도 됩니다»로
                //   스스로를 면제해 준다 — 이 테스트가 잡으려는 그 결함이 정확히 그 형태다.
                bool safeAreaCanHold =
                    screen.x - Margin * 2f >= PopoverPanel.MinPanelWidthPoints
                    && screen.y - top - bot - Margin * 2f >= PopoverPanel.MinPanelHeightPoints;
                if (safeAreaCanHold)
                {
                    AssertInsideSafeArea(rect, screen, top, bot,
                        $"screen={screen} top={top} bottom={bot} center={center}");
                }
                else
                {
                    // 안전영역이 하한 상자보다도 작다 — 어느 쪽이든 넘칠 수밖에 없고, 그때는
                    // «상단 우선»이 규칙이다(SurfaceSafeAreaPolicy: 상단엔 메뉴/시스템 표시가 있다).
                    Assert.LessOrEqual(rect.yMax, screen.y - top - Margin + 0.001f,
                        $"안전영역보다 큰 창인데 상단까지 넘겼습니다. screen={screen} top={top} rect={rect}");
                }
            }
        }

        /// <summary>
        /// ★ 클램프는 <b>고정점</b>이다 — 이미 줄인 크기를 다시 넣어도 더 줄지 않는다.
        /// <c>UpdatePlacement</c>는 매 프레임 도는데, 여기가 고정점이 아니면 창이 프레임마다
        /// 조금씩 오그라든다(상주 앱에서 몇 시간 뒤에야 눈에 띄는 종류의 결함이다).
        /// </summary>
        [Test]
        public void 클램프는_고정점이다()
        {
            Vector2 screen = WindowsSmallScreenPoints;
            float bottom = SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                RuntimePlatform.WindowsPlayer, WindowsTaskbarPoints);

            Vector2 once = PopoverPanel.ResolvePanelSizePoints(new Vector2(500f, 700f), screen, 0f, bottom, Margin);
            Vector2 twice = PopoverPanel.ResolvePanelSizePoints(once, screen, 0f, bottom, Margin);

            Assert.AreEqual(once.x, twice.x, 0.001f, "같은 화면에서 두 번 클램프하니 폭이 또 줄었습니다.");
            Assert.AreEqual(once.y, twice.y, 0.001f, "같은 화면에서 두 번 클램프하니 높이가 또 줄었습니다.");
        }

        /// <summary>못 잰 인셋(NaN·∞·음수)으로 화면을 깎지 않는다 —
        /// <see cref="SurfaceSafeAreaPolicy"/>가 세운 것과 같은 규약이다.</summary>
        [Test]
        public void 어긋난_인셋으로는_창을_깎지_않는다()
        {
            var desired = new Vector2(500f, 512f);
            Vector2 clean = PopoverPanel.ResolvePanelSizePoints(desired, MacScreenPoints, 0f, 0f, Margin);

            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -200f })
            {
                Vector2 got = PopoverPanel.ResolvePanelSizePoints(desired, MacScreenPoints, bad, bad, Margin);
                Assert.AreEqual(clean.x, got.x, 0.001f, $"인셋 {bad}가 폭을 바꿨습니다.");
                Assert.AreEqual(clean.y, got.y, 0.001f, $"인셋 {bad}가 높이를 바꿨습니다.");
            }
        }

        /// <summary>화면 크기를 아직 모를 때(0/음수)는 <b>아무것도 하지 않는다</b> —
        /// 부팅 첫 프레임에 창을 하한까지 오그라뜨리면 그 자체가 회귀다.</summary>
        [Test]
        public void 화면을_모르면_설계_크기를_그대로_둔다()
        {
            var desired = new Vector2(500f, 512f);
            foreach (Vector2 screen in new[] { Vector2.zero, new Vector2(-1f, 480f), new Vector2(640f, 0f) })
            {
                Vector2 got = PopoverPanel.ResolvePanelSizePoints(desired, screen, 0f, 0f, Margin);
                Assert.AreEqual(desired, got, $"화면 크기 {screen}에서 창 크기를 건드렸습니다.");
            }
        }
    }
}
