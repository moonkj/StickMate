#if UNITY_STANDALONE_OSX
using Kirurobo;
using UnityEngine;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// ★ 2026-09-03 — macOS <b>네 변 예약 띠</b>(메뉴 막대 + Dock)의 <b>사실 조회</b>.
    /// <see cref="MacReservedTopBarService"/>의 상단 한 줄을 네 줄로 넓힌 것이고,
    /// <b>그 클래스가 이제 이 클래스를 호출한다</b>(산술을 두 벌로 만들지 않는다).
    ///
    /// <para>이 클래스에는 <b>산술도 판정도 없다</b>. 조회 두 번을 하고
    /// <see cref="ReservedEdgeGeometry"/>(플랫폼 중립)에 넘기는 것이 전부다
    /// (CLAUDE.md: "플랫폼 전용 코드는 사실 조회만"). "그러니까 표면을 어디에 놓아라"는
    /// <see cref="SurfaceSafeAreaPolicy"/>의 몫이다.</para>
    ///
    /// ============================================================================
    /// 두 조회 — 무엇을 돌려주는지 <b>직접 재서</b> 적는다
    /// ============================================================================
    /// <code>
    ///   화면 전체 = CGDisplayBounds       -> MacWindowService.TryGetMainDisplayBounds()  (0,0,1512,982)
    ///   작업 영역 = NSScreen.visibleFrame  -> UniWindowController.GetMonitorRect(0)       (0,75,1512,874)
    /// </code>
    ///
    /// <para>★ <b>여기 적힌 "실측"이 무엇으로 잰 것인지 함께 남긴다</b>(2026-09-03 debugger).
    /// 직전 판까지 이 주석은 <i>"GetMonitorRect(0)은 visibleFrame이다 — 실측 (0,75,1512,874)"</i>라고
    /// <b>주장</b>했는데 <b>그 주장을 재는 것이 저장소에 하나도 없었고</b>, 그래서 리더가
    /// <i>"이 값이 픽셀일 수도 있다"</i>는 가설을 반증할 방법이 없었다. 재현 절차는 이렇다:</para>
    /// <list type="number">
    ///  <item>배포된 <c>StickMate.app/Contents/PlugIns/LibUniWinC.bundle</c>의 네이티브 바이너리를
    ///        <c>dlopen</c>하고, C#이 P/Invoke하는 <b>바로 그 심볼</b> <c>GetMonitorRectangle</c>을 부른다.</item>
    ///  <item>같은 프로세스에서 <c>NSScreen.visibleFrame</c>과 <c>CGDisplayBounds</c>를 대조군으로 찍는다.</item>
    /// </list>
    /// <para>2026-09-03 이 개발 머신(14" M2 Pro, 배율 2.0, Dock 하단)의 결과:
    /// <c>GetMonitorRectangle(0) = true (0.00, 75.00, 1512.00 x 874.00)</c>이고
    /// <c>NSScreen.visibleFrame</c>과 <b>소수점까지 같았다</b>. ⇒ <b>포인트가 맞고 픽셀이 아니다</b>
    /// (픽셀이면 3024×1748이 나온다). 원점은 Cocoa 좌하단이다.
    /// ⇒ 상 = 982 − (75+874) = <b>33 pt</b>, 하 = 75 pt, 좌 = 0, 우 = 0.</para>
    ///
    /// <para><b>혼동 주의</b> — 앱 로그 <c>[모니터지형]</c> 줄에는 사각형이 <b>세 종류</b> 찍힌다.
    /// 이 클래스가 쓰는 것은 앞의 둘뿐이다:
    /// <c>라이브러리 L0</c>(= <c>GetMonitorRect</c>, 포인트) · <c>OS0 전체</c>(= <c>CGDisplayBounds</c>, 포인트) ·
    /// <c>Unity U0</c>(= <c>Screen.GetDisplayLayout</c>, <b>물리 픽셀</b> 3024×1748).
    /// <b>U0는 이 경로에 들어오지 않는다</b> — 셋을 섞어 읽으면 "단위가 뒤섞였다"는 오진이 나온다.</para>
    ///
    /// ============================================================================
    /// ★★ 기동 직후 약 2초 동안 이 조회는 <b>구조적으로 실패한다</b> (2026-09-03 실측)
    /// ============================================================================
    /// <c>LibUniWinC</c>는 모니터 목록을 <b>캐시</b>로 들고 있고, 그 캐시를 채우는 함수는
    /// <c>_updateScreenInfo()</c> 하나뿐이며, 그것을 부르는 곳도 <c>_setup()</c> 하나뿐이고,
    /// <c>_setup()</c>을 부르는 곳은 <c>_attachWindow(window:)</c> <b>하나뿐이다</b>
    /// (배포 바이너리 역어셈블로 확인 — 호출 지점을 전수로 셌다).
    ///
    /// <para>즉 <b>라이브러리가 Unity 창을 붙잡기 전에는</b> <c>GetMonitorCount()</c>가 0이고
    /// <c>GetMonitorRectangle</c>이 <c>false</c>를 내며, <c>UniWindowController.GetMonitorRect(0)</c>은
    /// <c>Rect.zero</c>가 된다. 부착 시점은 이 머신에서 <b>기동 후 2.19초</b>였다
    /// (<c>[MacOverlayStateEnforcer] 창 부착 감지 … 경과 2.19초</c>).</para>
    ///
    /// <para>★ <b>그래서 <c>Start()</c>에서 이 값을 한 번 읽고 굳히는 소비 측은 반드시 0을 얻는다.</b>
    /// 이것이 2026-09-03 새벽 <i>"OS 예약 띠 0.0pt"</i> 로그의 원인이고, 그 줄을 정상 상태로 읽어
    /// <i>"메뉴 막대 수정이 아무것도 안 고쳤다"</i>는 오진이 나왔다. 값 자체는 부착 후 첫 갱신
    /// (<see cref="ReservedEdgeProbe.RefreshIntervalSeconds"/>)에서 33으로 올라온다 —
    /// <b>일회성으로 읽지 마라.</b> "아직 못 쟀다"와 "띠가 없다"를 가르는 방법은
    /// <see cref="ReservedTopBarProbe"/> 문서에 적어 두었다.</para>
    ///
    /// <para><b>안 쓰는 것</b>(2026-09-01 실측으로 전부 반증됨): <c>Screen.safeArea</c>의 top(32) ·
    /// macOS <c>statusThick</c>(22) · <c>auxiliary</c>(32). <b>셋 다 실제 33이 아니다.</b></para>
    ///
    /// <para><b>멀티모니터</b>: <c>GetMonitorRect(n)</c>과 그 모니터의 <c>CGDisplayBounds</c>를
    /// <b>짝지어</b> 넣어야 값이 뜻을 갖는다. 지금은 하지 않는다. 짝이 어긋난 채로 뺄셈이 도는 것은
    /// <see cref="ReservedEdgeGeometry.IsSameDisplayPair"/>가 막는다 — 그 문서에 이 머신 배치에서
    /// <b>존재하지 않는 182pt 메뉴 막대</b>가 「측정됨」으로 나오는 구체적 수치 예가 있다.</para>
    ///
    /// <para><b>하단 값은 아직 아무도 소비하지 않는다.</b> 캐릭터가 밟는 Dock 발판은 두께가 아니라
    /// <b>사각형</b>이 필요해서 <c>Platform/IDockMetricsService.cs</c>의 타일 실측 경로를 그대로 쓴다.
    /// 여기의 하단 값은 진단용으로만 채운다 — 발판 경로를 이 값으로 바꾸는 것은 별건이다.</para>
    /// </summary>
    public sealed class MacReservedScreenEdgeService : IReservedScreenEdgeService
    {
        private readonly MacWindowService _display;

        public MacReservedScreenEdgeService(MacWindowService display)
        {
            _display = display;
        }

        /// <summary>플랫폼 서비스에서 <see cref="MacWindowService"/>를 찾아 조립한다. 못 찾으면 false —
        /// 에디터/폴백 구현에서는 조용히 "네 변 모두 모름"으로 남는다.</summary>
        public static bool TryCreate(IPlatformWindowService service, out MacReservedScreenEdgeService created)
        {
            var mac = service as MacWindowService;
            created = mac != null ? new MacReservedScreenEdgeService(mac) : null;
            return created != null;
        }

        public bool TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets)
        {
            insets = ReservedEdgeInsets.Unknown;
            if (_display == null) return false;
            if (!_display.TryGetMainDisplayBounds(out Rect display)) return false;

            // GetMonitorRect(0)은 macOS에서 visibleFrame(Cocoa 좌하단 원점, OS 포인트)이다.
            // 라이브러리가 창을 붙잡기 전에는 Rect.zero이며, 그 경우는 아래에서 "모름"이 된다.
            Rect visible = UniWindowController.GetMonitorRect(0);

            // 뺄셈도 상식 판정도 여기 없다 — 플랫폼 중립 한 벌(ReservedEdgeGeometry)에만 있다.
            return ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(display, visible, out insets);
        }
    }
}
#endif
