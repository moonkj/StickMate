using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-02 — <b>OS가 화면 위쪽에 예약해 둔 띠</b>(macOS 메뉴바 / Windows 상단 도킹 작업표시줄)의
    /// 두께만 알려 주는 <b>사실 조회</b> 창구. <see cref="IReservedBottomBarService"/>의 쌍둥이다.
    ///
    /// <para><b>왜 필요했나</b>: [행동 명령] 팝오버가 화면 네 변에 똑같이 12pt 여백을 주는 바람에
    /// 상단 y=12pt에 앉았고, macOS 메뉴바(y 0~33pt)를 <b>21pt 덮었다</b>. 겹치는 가로 구간
    /// x 808~1284는 제어센터·입력기·WiFi·배터리·시계가 있는 자리라 그 아이콘들의 아래 2/3가
    /// 잘려 보였다. 화면의 네 변은 대칭이 아닌데 대칭으로 다뤘던 것이다(절대 불변 원칙 2 위반).</para>
    ///
    /// ============================================================================
    /// 계약 — "두께"만 말한다. 무엇을 할지는 <see cref="SurfaceSafeAreaPolicy"/>가 정한다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>단위는 OS 포인트</b>(Unity 픽셀이 아니다). Retina에서 픽셀로 바꾸는 것은 호출부의 몫이며
    ///        그 변환의 단일 소스는 <see cref="ScreenCoordinateConverter"/>다.</item>
    ///  <item><b>false = "지금 상단 예약 띠가 없다"</b>. 메뉴바 자동 숨김이거나, 작업표시줄이 좌·우·하단에
    ///        붙어 있거나, 보조 디스플레이라 메뉴바가 없는 경우다. <b>"추정하라"는 뜻이 절대 아니다</b> —
    ///        추정값이 실제보다 크면 멀쩡한 화면 위쪽을 낭비하고, 작으면 그대로 덮는다. 둘 다 나쁘다.</item>
    ///  <item><b>읽기 전용</b>. 메뉴바/작업표시줄을 숨기거나 옮기는 API는 이 인터페이스의 구현체가
    ///        절대 부르지 않는다(절대 불변 원칙 3).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 왜 높이를 하드코딩하지 않는가 — 그리고 왜 새 네이티브 코드가 0줄인가
    /// ============================================================================
    /// 메뉴바 높이는 화면마다 다르고, 노치 있는 맥은 또 다르다. 다행히 이 앱은 필요한 두 사실을
    /// <b>이미 둘 다 조회하고 있다</b>:
    /// <code>
    ///   상단 인셋(pt) = 화면 전체 높이 − (visibleFrame.y + visibleFrame.height)     ← Cocoa(좌하단 원점)
    ///                 = 982 − (75 + 874) = 33      ✔ 이 개발 머신의 메뉴바 실측과 일치
    /// </code>
    /// 화면 전체는 <c>CGDisplayBounds</c>(<c>MacWindowService.TryGetMainDisplayBounds</c>), 작업 영역은
    /// <c>UniWindowController.GetMonitorRect(0)</c>가 준다 — 둘 다 이미 코드에 있다.
    ///
    /// <para>★ 2026-09-01 밤 실측으로 <b>쓰면 안 되는 후보 3개</b>가 확정됐다:
    /// <c>Screen.safeArea</c>의 top(32) · macOS <c>statusThick</c>(22) · <c>auxiliary</c>(32) —
    /// <b>셋 다 실제 33과 다르다</b>. 위 뺄셈식만 쓴다.</para>
    ///
    /// <para><b>Windows</b>: <c>GetMonitorInfo</c>의 <c>rcWork.Top − rcMonitor.Top</c>이 그대로 이 값이다
    /// (작업표시줄이 위에 도킹된 경우). <see cref="IReservedBottomBarService"/> 구현이 이미 같은 호출에서
    /// <c>rcWork</c>/<c>rcMonitor</c>를 읽고 있으므로 <b>한 줄 더 꺼내면 된다</b> — 별도 배정 항목.</para>
    ///
    /// <para>선택적 캐퍼빌리티다(<see cref="ICursorPositionService"/>/<see cref="IDockMetricsService"/>와
    /// 같은 관례) — 소비 측은 <see cref="ReservedTopBarProbe"/>를 거치고, 구현체가 없으면 인셋 0
    /// (= 이 라운드 이전과 완전히 같은 배치)으로 돌아간다.</para>
    /// </summary>
    public interface IReservedTopBarService
    {
        /// <summary>지금 이 순간 화면 상단에 예약된 띠의 두께(OS 포인트).</summary>
        /// <param name="insetPoints">성공했을 때의 두께. 실패하면 의미 없음.</param>
        /// <returns>상단 예약 띠가 <b>존재하지 않으면</b> false(자동 숨김 / 좌·우·하단 배치 / 보조 화면).</returns>
        bool TryGetReservedTopInsetPoints(out float insetPoints);
    }
}
