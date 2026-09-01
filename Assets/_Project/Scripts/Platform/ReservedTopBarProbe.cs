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
    ///  (1) <b>소비자가 넷이다</b>(팝오버 / 정보창 / 설정창 / 톱니). 넷이 각자 캐스팅하고 각자 캐시하면
    ///      "메뉴바를 안 덮는다"는 규칙이 네 벌이 되고, 다음 라운드에 반드시 한 벌만 고쳐진다.
    ///  (2) <b>매 프레임 P/Invoke를 막는다.</b> 팝오버는 열려 있는 동안 매 프레임 자리를 다시 계산한다
    ///      (<c>PopoverPanel.UpdatePlacement</c>). 메뉴바 두께는 초당 60번 바뀌는 값이 아니므로
    ///      <see cref="RefreshIntervalSeconds"/>마다 한 번만 다시 묻는다 — 하루 종일 켜져 있는 앱이다.
    ///
    /// <para><b>실패는 0이다.</b> 상단 예약 띠를 못 찾으면 인셋 0으로 본다 = 이 라운드 이전과
    /// <b>한 픽셀도 다르지 않은</b> 배치. 짐작값으로 메우지 않는다(그 이유는
    /// <see cref="IReservedTopBarService"/> 문서).</para>
    /// </summary>
    public static class ReservedTopBarProbe
    {
        /// <summary>메뉴바/작업표시줄 두께를 다시 묻는 주기(초).</summary>
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
