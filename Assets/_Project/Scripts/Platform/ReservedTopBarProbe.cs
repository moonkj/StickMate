using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-02 — <see cref="IReservedTopBarService"/>를 <b>찾아서 캐시하는</b> 배선 한 곳.
    /// 정책(<see cref="SurfaceSafeAreaPolicy"/>)과 사실 조회(플랫폼 구현) 사이의 접합부다.
    ///
    /// ============================================================================
    /// 왜 별도 클래스인가
    /// ============================================================================
    ///  (1) <b>소비 호출부가 다섯 파일이다</b> — <c>Interaction/PopoverPanel.cs</c> ·
    ///      <c>Interaction/UiWindowDrag.cs</c> · <c>Interaction/InfoGearIconWidget.cs</c> ·
    ///      <c>Interaction/GearRadialMenuWidget.cs</c> · <c>Interaction/TodoPostItWidget.cs</c>.
    ///      ★ <b>2026-09-07 이름 정정</b>: 옛 목록의 <c>CharacterInfoWindow.Layout.cs</c>가
    ///      <c>UiWindowDrag.cs</c>가 됐다. 창 3종(정보창·설정창·집중 팝오버)이 드래그로 움직이게
    ///      되면서 클램프 식과 이 조회가 그 파일 한 곳으로 모였다 — <b>개수는 그대로 다섯</b>이고
    ///      설정창은 이제 그 한 곳을 <b>거쳐서</b> 소비자가 됐다(직접 호출부는 여전히 0건이다).
    ///      다섯이 각자 캐스팅하고 각자 캐시하면 "메뉴바를 안 덮는다"는 규칙이 다섯 벌이 되고,
    ///      다음 라운드에 반드시 한 벌만 고쳐진다.
    ///      ★ <b>2026-09-03 실측 정정</b>: 이 목록은 원래 <i>"넷(팝오버/정보창/설정창/톱니)"</i>이라고
    ///      적혀 있었는데 <b>두 가지로 틀렸다</b> — 실제 호출부는 다섯 파일이고, 거기 세어져 있던
    ///      <b>설정창은 호출부가 0건</b>이라 애초에 소비자가 아니었다. 낡은 목록을 근거로
    ///      "설정창도 이미 띠를 피한다"고 판단하지 마라.
    ///  (2) <b>매 프레임 P/Invoke를 막는다.</b> 팝오버는 열려 있는 동안 매 프레임 자리를 다시 계산한다
    ///      (<c>PopoverPanel.UpdatePlacement</c>). 메뉴바 두께는 초당 60번 바뀌는 값이 아니므로
    ///      <see cref="RefreshIntervalSeconds"/>마다 한 번만 다시 묻는다 — 하루 종일 켜져 있는 앱이다.
    ///
    /// <para><b>실패는 0이다.</b> 상단 예약 띠를 못 찾으면 인셋 0으로 본다 = 이 라운드 이전과
    /// <b>한 픽셀도 다르지 않은</b> 배치. 짐작값으로 메우지 않는다(그 이유는
    /// <see cref="IReservedTopBarService"/> 문서).</para>
    ///
    /// <para>★ <b>2026-09-03 — 상단은 축 하나일 뿐이다.</b> 좌·우·하단 예약 띠(좌/우 도킹 작업표시줄,
    /// 좌/우 Dock)는 이 프로브로 <b>원리상</b> 못 잡는다 — 그때 상단 차이는 0이고 이 프로브는 정확히
    /// "상단 띠 없음"을 보고한다. 네 변이 필요하면 <see cref="ReservedEdgeProbe"/>를 써라.
    /// 두 프로브가 보는 상단 값은 <b>같은 뺄셈 한 줄</b>에서 나온다(플랫폼 구현이 네 방향 조회의
    /// <c>Top</c>을 꺼내 이 계약에 답한다).</para>
    ///
    /// ============================================================================
    /// ★★ 일회성으로 읽지 마라 — 기동 직후의 0은 "띠 없음"이 아니다 (2026-09-03 실제 사고)
    /// ============================================================================
    /// <b>양 플랫폼 모두 기동 직후 몇 초 동안 이 프로브는 구조적으로 0을 돌려준다.</b>
    /// <list type="bullet">
    ///  <item><b>macOS</b>: <c>LibUniWinC</c>가 Unity 창을 붙잡아야 모니터 캐시가 채워진다.
    ///        그전까지 <c>GetMonitorRect(0)</c>은 <c>Rect.zero</c>다(이 머신 실측: 부착까지 2.19초).
    ///        근거와 재현 절차는 <c>Platform/MacOS/MacReservedScreenEdgeService.cs</c> 클래스 문서.</item>
    ///  <item><b>Windows</b>: <c>Win32WindowService.TryGetReservedEdgeInsetsPoints</c>가
    ///        <c>_overlayHwnd == IntPtr.Zero</c>면 곧바로 false를 낸다. 그 핸들은
    ///        <c>CreateOverlayWindow()</c>에서 확보되므로 <b>같은 형태의 창</b>이 있다.</item>
    /// </list>
    ///
    /// <para><b>그래서 <c>Start()</c>/<c>Awake()</c>에서 한 번 읽고 굳히면 반드시 0이 박힌다.</b>
    /// 2026-09-03 새벽에 이 형태로 사고가 났다 — 기동 배너가 <i>"OS 예약 띠 0.0pt"</i>를 찍었고,
    /// 그 한 줄을 <b>정상 상태의 값</b>으로 읽어 <i>"메뉴 막대 수정이 아무것도 안 고쳤다"</i>는
    /// 오진이 나왔다. 실제로는 부착 후 첫 갱신에서 33pt로 올라오고 있었다.</para>
    ///
    /// <para><b>구분하는 법</b>: 이 계약(<c>float</c> 하나)은 「측정된 0」과 「아직 못 잼」을
    /// <b>표현할 수 없다</b>. 구분이 필요하면 네 방향 프로브의 마스크를 봐라 —
    /// <c>ReservedEdgeProbe.Insets(service).IsMeasured(ReservedEdge.Top)</c>가 <b>그 답 자체</b>다
    /// (<see cref="ReservedEdgeInsets.MeasuredEdges"/>가 바로 이 구분을 위해 존재한다).
    /// 매 프레임 배치를 다시 계산하는 소비 측은 그냥 이 프로브를 계속 쓰면 된다 — 값이
    /// 알아서 따라온다. <b>문제는 한 번만 읽는 쪽뿐이다.</b></para>
    /// </summary>
    public static class ReservedTopBarProbe
    {
        /// <summary>메뉴바/작업표시줄 두께를 다시 묻는 주기(초).
        /// <see cref="ReservedEdgeProbe"/>(네 방향판)가 이 상수를 그대로 가져다 쓴다 — 두 벌 금지.</summary>
        public const float RefreshIntervalSeconds = 0.5f;

        private static IReservedTopBarService _service;
        private static bool _resolved;

        private static bool _hasOverride;
        private static float _overrideInsetPoints;

        private static float _cachedInsetPoints;
        private static float _cachedAt = float.NegativeInfinity;

        /// <summary>지금까지 관측된 인셋(OS 포인트) — 진단/로그 전용. 아직 한 번도 못 물었으면 0.</summary>
        public static float LastInsetPoints => _cachedInsetPoints;

        /// <summary>
        /// 지금 화면 상단에 예약된 띠의 두께(OS 포인트). 못 물으면 <b>0</b>을 돌려준다.
        /// <paramref name="service"/>가 null이어도 안전하다(에디터/모바일).
        /// </summary>
        public static float TopInsetPoints(IPlatformWindowService service)
        {
            if (_hasOverride) return _overrideInsetPoints;

            float now = Time.unscaledTime;
            if (now - _cachedAt < RefreshIntervalSeconds) return _cachedInsetPoints;
            _cachedAt = now;

            IReservedTopBarService probe = Resolve(service);
            _cachedInsetPoints = probe != null && probe.TryGetReservedTopInsetPoints(out float inset)
                                 && !float.IsNaN(inset) && !float.IsInfinity(inset) && inset > 0f
                ? inset
                : 0f;
            return _cachedInsetPoints;
        }

        private static IReservedTopBarService Resolve(IPlatformWindowService service)
        {
            if (_resolved && _service != null) return _service;

            // 데코레이터를 벗긴다 — 실제 구현이 안쪽에 있다(MacOverlayStateEnforcer.ResolveDescriber와 같은 관례).
            IPlatformWindowService inner = service is FallbackPlatformWindowService decorator
                ? decorator.Inner
                : service;

            // (1) 플랫폼 서비스가 직접 구현했으면 그것을 쓴다. ★ MacWindowService / Win32WindowService가
            //     이 인터페이스를 달게 되는 날 아래 (2)는 통째로 지워도 된다 — 이 분기가 먼저 잡는다.
            if (inner is IReservedTopBarService direct)
            {
                _service = direct;
                _resolved = true;
                return _service;
            }

#if UNITY_STANDALONE_OSX
            // (2) macOS 조립 — CGDisplayBounds(화면 전체) − visibleFrame(작업영역)의 뺄셈이다.
            //     신규 네이티브 코드 0줄이며, 두 조회 모두 이미 코드에 있다.
            if (MacOS.MacReservedTopBarService.TryCreate(inner, out MacOS.MacReservedTopBarService mac))
            {
                _service = mac;
                _resolved = true;
                return _service;
            }
#endif
            return null;
        }

        /// <summary>테스트 전용 — 인셋을 고정한다(실제 OS 메뉴바 없이 클램프를 밀어 볼 수 있게).</summary>
        public static void SetInsetPointsForTests(float insetPoints)
        {
            _hasOverride = true;
            _overrideInsetPoints = Mathf.Max(0f, insetPoints);
        }

        /// <summary>주입한 값을 걷고 실제 조회로 되돌린다. 캐시도 함께 버린다.</summary>
        public static void ResetForTests()
        {
            _hasOverride = false;
            _overrideInsetPoints = 0f;
            _service = null;
            _resolved = false;
            _cachedInsetPoints = 0f;
            _cachedAt = float.NegativeInfinity;
        }
    }
}
