#if UNITY_STANDALONE_OSX
namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// ★ 2026-09-02 — macOS <b>메뉴바 두께</b>의 사실 조회. docs/UX_FLOW.md 41-1 ③.
    ///
    /// <para>이 클래스는 <b>판정을 하지 않는다</b> — "그러니까 팝오버를 어디에 놓아라"는
    /// <see cref="SurfaceSafeAreaPolicy"/>(플랫폼 중립)의 몫이다. 여기 있는 것은 네 방향 조회에서
    /// <c>Top</c> 한 값을 꺼내는 일뿐이다(CLAUDE.md: "플랫폼 전용 코드는 사실 조회만").</para>
    ///
    /// ============================================================================
    /// 새 네이티브 코드가 0줄인 이유 — 두 조회가 이미 있다
    /// ============================================================================
    /// <code>
    ///   화면 전체 = CGDisplayBounds          → MacWindowService.TryGetMainDisplayBounds()  (0,0,1512,982)
    ///   작업 영역 = NSScreen.visibleFrame     → UniWindowController.GetMonitorRect(0)       (0,75,1512,874)
    ///
    ///   상단 인셋 = 982 − (75 + 874) = 33 pt   ✔ 이 화면의 메뉴바 실측과 일치
    ///   (참고로 하단 인셋 = visibleFrame.y = 75 = Dock 두께. Tools/와 ARCHITECTURE.md가 이미
    ///    같은 식으로 75pt를 검증해 뒀다 — 선례가 있는 유도식이다.)
    /// </code>
    /// <c>visibleFrame</c>은 <b>OS가 직접 뺀 값</b>이라 노치 맥이든 외장 모니터든 메뉴바 글꼴이 크든
    /// 우리가 33이나 38을 짐작할 필요가 없다.
    ///
    /// <para><b>안 쓰는 것</b>(2026-09-01 실측으로 전부 반증됨): <c>Screen.safeArea</c>의 top(32) ·
    /// macOS <c>statusThick</c>(22) · <c>auxiliary</c>(32). <b>셋 다 실제 33이 아니다.</b></para>
    ///
    /// ============================================================================
    /// 예외를 0으로 접는 자리
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>메뉴바 자동 숨김</b>: <c>visibleFrame</c>이 화면 전체와 같아져 뺄셈이 0을 낸다 →
    ///        false. OS가 그 띠를 예약하지 않았으므로 우리도 비우지 않는다(더 나빠지지 않는다).</item>
    ///  <item><b>보조 모니터</b>: macOS 기본값은 "메뉴바는 주 디스플레이에만"이라 인셋이 0일 수 있다.
    ///        유도식이 알아서 0을 낸다 — 특례가 필요 없다.</item>
    ///  <item><b>상식 범위를 벗어난 값</b>: 화면 높이의 25%를 넘는 인셋은 메뉴바가 아니라 조회가
    ///        어긋난 것이다(멀티모니터 원점 혼선 등). 그때는 <b>0으로 접는다</b> — 화면 위쪽을
    ///        근거 없이 잘라 먹는 쪽이 더 나쁘다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 2026-09-03 — 뺄셈은 이제 여기 없다. <see cref="MacReservedScreenEdgeService"/>가 한 벌로 갖는다
    /// ============================================================================
    /// 같은 날 화면 <b>좌·우</b> 예약 띠(Dock 좌/우 배치, Windows 좌/우 도킹 작업표시줄)를 다루는
    /// 네 방향 계약 <see cref="IReservedScreenEdgeService"/>가 생겼고, 그 구현이 <b>같은 두 조회</b>에서
    /// 네 변을 한 번에 뺀다. 상단 산술을 여기에 남겨 두면 <b>두 벌</b>이 되고, 이 저장소의 규칙대로
    /// 다음 라운드에 반드시 한쪽만 고쳐진다. 그래서 이 클래스는 네 방향 조회의 <c>Top</c>만 꺼내는
    /// <b>좁은 창</b>이 됐다.
    ///
    /// <para><b>이 클래스를 지우지 않는 이유</b>: 상단 계약의 소비 호출부가 이미 다섯 곳이고
    /// (<c>Interaction/InfoGearIconWidget.cs</c> · <c>Interaction/GearRadialMenuWidget.cs</c> ·
    /// <c>Interaction/CharacterInfoWindow.Layout.cs</c> · <c>Interaction/TodoPostItWidget.cs</c> ·
    /// <c>Interaction/PopoverPanel.cs</c>) 전부 <see cref="ReservedTopBarProbe"/>를 지난다.
    /// 계약을 갈아엎는 것과 산술을 한 벌로 만드는 것은 다른 일이고, 지금 필요한 것은 뒤쪽이다.</para>
    /// </summary>
    public sealed class MacReservedTopBarService : IReservedTopBarService
    {
        private readonly MacReservedScreenEdgeService _edges;

        public MacReservedTopBarService(MacWindowService display)
        {
            _edges = new MacReservedScreenEdgeService(display);
        }

        /// <summary>플랫폼 서비스에서 <see cref="MacWindowService"/>를 찾아 조립한다. 못 찾으면 false —
        /// 에디터/폴백 구현에서는 조용히 인셋 0으로 남는다.</summary>
        public static bool TryCreate(IPlatformWindowService service, out MacReservedTopBarService created)
        {
            var mac = service as MacWindowService;
            created = mac != null ? new MacReservedTopBarService(mac) : null;
            return created != null;
        }

        public bool TryGetReservedTopInsetPoints(out float insetPoints)
        {
            insetPoints = 0f;
            if (_edges == null) return false;
            if (!_edges.TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets)) return false;
            if (!insets.IsMeasured(ReservedEdge.Top)) return false;   // 상식 범위 밖 / 조회 어긋남.

            float inset = insets.PointsFor(ReservedEdge.Top);
            if (inset <= 0f) return false;                            // 자동 숨김 / 보조 화면.

            insetPoints = inset;
            return true;
        }
    }
}
#endif
