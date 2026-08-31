using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-08-31 — 사용자 신고(실제 Windows PC 첫 실행) <b>"작업표시줄에 걸쳐서 돌아다닌다"</b>의 수정.
    ///
    /// ============================================================================
    /// 왜 <see cref="IDockMetricsService"/>로는 안 되는가 (같은 문제, 정반대 사정)
    /// ============================================================================
    /// macOS는 Dock 막대의 사각형을 <b>어떤 공개 API로도 얻을 수 없다</b>(그 인터페이스 문서 1절의
    /// 전수 덤프 결론). 그래서 타일 개수 N으로 폭을 <b>계산</b>하는 기계 장치가 통째로 필요했고,
    /// <see cref="DockMetrics"/>는 그 계산의 <b>입력</b>(tilesize / 타일 수 / 구분선 수)을 나른다.
    ///
    /// Windows는 정반대다. 작업표시줄이 예약한 영역은 <c>GetMonitorInfo</c>가
    /// <c>rcMonitor</c>(모니터 전체) / <c>rcWork</c>(작업 영역 = 작업표시줄·도킹 툴바를 뺀 나머지)로
    /// <b>정확히, 권한 없이, 픽셀 단위로</b> 알려준다. 여기에 추정이 끼어들 자리가 없다.
    ///
    /// 그런데 직전 라운드까지 <see cref="Windows.Win32WindowService"/>는 이 조회를 하지 않았고
    /// <see cref="IDockMetricsService"/>도 구현하지 않았다. 그 결과
    /// <see cref="FallbackPlatformWindowService.TryGetDockRectOsScreen"/>이 <b>2순위 폴백</b>
    /// (<c>StickConfig.dockFootholdWidthFraction</c> = 화면 폭의 65%를 <b>가운데</b> 정렬,
    ///  두께 <c>dockFootholdThicknessPoints</c> = 75pt)으로 떨어졌다 — 이건 macOS Dock의 모양이다.
    /// 실제 Windows 작업표시줄은 <b>화면 가로 전체</b>를 차지하고 두께도 75pt가 아니다. 그래서
    ///   · 화면 좌우 각 17.5% 구간에서는 발판이 "화면 최하단 안전망"이 되어 캐릭터가
    ///     <b>작업표시줄 안에 서고</b>,
    ///   · 가운데 65% 구간에서도 두께가 실제와 달라 작업표시줄 위/아래로 어긋난다.
    /// 신고 문구 "걸쳐서 돌아다닌다"가 이 두 증상 그대로다.
    ///
    /// ============================================================================
    /// 계약
    /// ============================================================================
    /// "OS가 화면 <b>하단</b>에 예약해 둔 막대의 정확한 사각형"을 돌려준다. 구현하는 플랫폼에서는 이
    /// 값이 <b>절대적</b>이다 — 성공하면 그 사각형이 곧 Dock/작업표시줄 발판이고, 실패(false)는
    /// "추정해 보라"가 아니라 <b>"하단 예약 막대가 지금 존재하지 않는다"</b>는 확정 신호다
    /// (작업표시줄이 자동 숨김이거나 화면 좌/우/상단에 붙어 있는 경우 — 그때 <c>rcWork</c>의 하단은
    /// <c>rcMonitor</c>의 하단과 같다). 호출부는 그 경우 Dock 발판을 만들지 않고 화면 최하단
    /// 안전망만 전체 폭으로 남겨야 한다.
    ///
    /// 좌표계는 <see cref="PlatformFoothold.ScreenRect"/>와 같은 OS 데스크톱 좌표(좌상단 원점,
    /// y가 아래로 증가)다. 창 열거가 쓰는 좌표와 <b>같은 관측 단위</b>여야 하므로 구현체는 창 사각형과
    /// 동일한 API 계열(Win32: <c>GetWindowRect</c> ↔ <c>GetMonitorInfo</c>)에서 값을 얻어야 한다.
    ///
    /// 구현하지 않아도 되는 선택적 캐퍼빌리티다(<see cref="ICursorPositionService"/>/
    /// <see cref="IDockMetricsService"/>/<see cref="IRawWindowRectSource"/>와 동일한 관례) —
    /// <see cref="FallbackPlatformWindowService"/>가 <c>as</c> 캐스팅으로 확인하고, 없으면 예전 경로
    /// (macOS: 타일 실측 / 그 외: 고정 비율 추정)를 그대로 쓴다. 즉 <b>macOS 경로는 한 글자도 바뀌지
    /// 않는다</b>.
    ///
    /// 읽기 전용이다. 작업표시줄을 옮기거나 크기를 바꾸는 API(<c>SHAppBarMessage(ABM_SETPOS)</c> 등)는
    /// 이 인터페이스의 구현체가 절대 부르지 않는다(절대 불변 원칙 3).
    /// </summary>
    public interface IReservedBottomBarService
    {
        /// <summary>
        /// 지금 이 순간 화면 하단에 예약된 막대(작업표시줄/도킹 툴바)의 사각형.
        /// </summary>
        /// <param name="osScreenRect">성공했을 때의 OS 데스크톱 좌표 사각형. 실패하면 의미 없음.</param>
        /// <returns>하단 예약 막대가 <b>존재하지 않으면</b> false(자동 숨김 / 좌·우·상단 배치 등).</returns>
        bool TryGetReservedBottomBarOsScreen(out Rect osScreenRect);
    }
}
