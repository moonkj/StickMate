using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-03 — <see cref="IReservedScreenEdgeService"/>를 <b>찾아서 캐시하는</b> 배선 한 곳.
    /// <see cref="ReservedTopBarProbe"/>의 네 방향판이며, 규약은 한 글자도 다르지 않다.
    ///
    /// ============================================================================
    /// 규약 — <b>실패는 0이다. 짐작값으로 메우지 않는다.</b>
    /// ============================================================================
    /// 못 물으면 <see cref="ReservedEdgeInsets.Unknown"/>(네 변 0 · 측정 비트 없음)이고, 소비 측은
    /// 그때 <b>아무것도 바꾸지 않는다</b> = 이 계약 도입 이전과 한 픽셀도 다르지 않은 배치.
    ///
    /// <para>★ 특히 <b>화면 폭에서 빼서 추정하는 것</b>은 이 규약을 정면으로 깬다. "우측 도킹
    /// 작업표시줄은 보통 48~62pt니까 그만큼 밀어 두자"는 값은 <b>관측이 아니다</b> — 실제보다 크면
    /// 멀쩡한 화면을 낭비하고, 작으면 그대로 덮는다. 못 재면 0을 돌려주고 소비 측이 예전처럼 놓는다.</para>
    ///
    /// ============================================================================
    /// 캐시 — 하루 종일 켜져 있는 앱이라 매 프레임 P/Invoke를 막는다
    /// ============================================================================
    /// 예약 띠 두께는 초당 60번 바뀌는 값이 아니다. <see cref="RefreshIntervalSeconds"/>마다 한 번만
    /// 다시 묻는다. 주기 상수는 <see cref="ReservedTopBarProbe"/>에서 <b>가져다 쓴다</b> — 두 벌이
    /// 되면 반드시 한쪽만 고쳐진다.
    ///
    /// ============================================================================
    /// ★ 상단 프로브와의 관계 (여기를 안 읽으면 다음 사람이 반드시 틀린다)
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>산술은 한 벌이다.</b> 양 플랫폼의 <see cref="IReservedTopBarService"/> 구현이
    ///        <see cref="IReservedScreenEdgeService"/> 조회를 호출해 <c>Top</c>만 꺼낸다. 즉 두 프로브가
    ///        보는 상단 값은 <b>같은 뺄셈 한 줄</b>에서 나온다.</item>
    ///  <item><b>다만 주입(테스트 오버라이드)은 한쪽 방향으로만 흐른다.</b>
    ///        <see cref="SetInsetsForTests"/>는 상단 프로브에도 같은 상단 값을 심고,
    ///        <see cref="ResetForTests"/>는 상단 프로브도 함께 걷는다. 반대로
    ///        <c>ReservedTopBarProbe.ResetForTests()</c>만 부르면 <b>이쪽은 안 걷힌다</b> —
    ///        네 방향을 쓰는 테스트는 반드시 이 클래스의 <see cref="ResetForTests"/>를 불러라.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 프로브만이 「측정된 0」과 「아직 못 잼」을 구분해서 말할 수 있다
    /// ============================================================================
    /// <see cref="Insets"/>가 돌려주는 <see cref="ReservedEdgeInsets.MeasuredEdges"/>가 그 답이다.
    /// 상단 전용 프로브는 <c>float</c> 하나라 <b>구조적으로 구분이 불가능</b>하다.
    ///
    /// <para><b>이게 왜 중요한가</b>: 양 플랫폼 모두 <b>기동 직후 몇 초 동안 구조적으로 못 잰다</b>
    /// (macOS는 <c>LibUniWinC</c>의 창 부착 전, Windows는 <c>_overlayHwnd</c> 확보 전).
    /// <c>Start()</c>에서 한 번 읽고 굳히는 소비 측은 그때 <b>반드시 0을 얻는다</b> —
    /// 2026-09-03에 그 형태로 오진이 났다. 자세한 것은 <see cref="ReservedTopBarProbe"/>의
    /// "일회성으로 읽지 마라" 절.</para>
    ///
    /// <para>뺄셈 자체는 여기에도, 플랫폼 구현에도 없다 — <see cref="ReservedEdgeGeometry"/>
    /// 한 곳에만 있다(그래야 값으로 검증하는 테스트를 쓸 수 있다).</para>
    /// </summary>
    public static class ReservedEdgeProbe
    {
        /// <summary>예약 띠 두께를 다시 묻는 주기(초). 상단 프로브와 <b>같은 상수 하나</b>를 쓴다.</summary>
        public const float RefreshIntervalSeconds = ReservedTopBarProbe.RefreshIntervalSeconds;

        private static IReservedScreenEdgeService _service;
        private static bool _resolved;

        private static bool _hasOverride;
        private static ReservedEdgeInsets _overrideInsets;

        // ★ 2026-09-05 (M-7) — 하단 강제는 «플랫폼»이 입력이다. 실기가 없는 이 머신에서 그 분기를
        //   실제로 실행해 보려면 주입 창구가 있어야 한다(SetPlatformForTests / ResetForTests).
        private static bool _hasPlatformOverride;
        private static RuntimePlatform _platformOverride;

        private static ReservedEdgeInsets _cached;
        private static float _cachedAt = float.NegativeInfinity;

        /// <summary>지금까지 관측된 묶음 — 진단/로그 전용. 아직 한 번도 못 물었으면 <see cref="ReservedEdgeInsets.Unknown"/>.</summary>
        public static ReservedEdgeInsets LastInsets => _cached;

        /// <summary>
        /// 지금 화면 네 변에 예약된 띠의 두께. 못 물으면 <see cref="ReservedEdgeInsets.Unknown"/>.
        /// <paramref name="service"/>가 null이어도 안전하다(에디터/모바일).
        /// </summary>
        public static ReservedEdgeInsets Insets(IPlatformWindowService service)
        {
            if (_hasOverride) return _overrideInsets;

            float now = Time.unscaledTime;
            if (now - _cachedAt < RefreshIntervalSeconds) return _cached;
            _cachedAt = now;

            IReservedScreenEdgeService probe = Resolve(service);
            _cached = probe != null && probe.TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets measured)
                ? measured
                : ReservedEdgeInsets.Unknown;
            return _cached;
        }

        /// <summary>한 변의 두께(OS 포인트). 못 물었거나 그 변을 못 쟀으면 <b>0</b>.</summary>
        public static float EdgeInsetPoints(IPlatformWindowService service, ReservedEdge edge)
            => Insets(service).PointsFor(edge);

        /// <summary>
        /// ★ 2026-09-05 (M-7) — <b>UI 표면 배치에 실제로 적용할</b> 하단 인셋(OS 포인트).
        ///
        /// <para><b>macOS에서는 언제나 0</b>이다(Dock은 발판이다). Windows에서만 실측 두께가 그대로
        /// 나온다. 판정 규칙 자체는 <see cref="SurfaceSafeAreaPolicy.EnforcesBottomReservedBand"/>가
        /// 갖고 있고 이 함수는 <b>사실(실측 두께) + 지금 플랫폼</b>을 그 규칙에 넣어 주기만 한다 —
        /// 그래서 판정은 <c>#if</c> 없이 EditMode에서 양쪽 답을 다 실행해 볼 수 있다.</para>
        ///
        /// <para><b>왜 여기(프로브)에 있는가</b>: <see cref="SurfaceSafeAreaPolicy"/>는 OS도 엔진도
        /// 부르지 않는 순수 산술로 남겨야 한다. <c>Application.platform</c>은 엔진 조회이므로
        /// <b>사실 조회 층</b>인 이 파일이 맡는다. 같은 이유로 <see cref="Insets"/>의 캐시도 여기 있다.</para>
        ///
        /// <para><b>호출 비용</b>: <see cref="Insets"/> 캐시 조회 1회 + 비교 몇 개. 새 P/Invoke는 0줄이다 —
        /// 하단 두께는 이미 같은 <c>GetMonitorInfo</c>/<c>visibleFrame</c> 한 번에 딸려 오던 값이다.</para>
        /// </summary>
        public static float EnforcedBottomInsetPoints(IPlatformWindowService service)
            => SurfaceSafeAreaPolicy.EffectiveBottomInsetPoints(
                CurrentPlatform, Insets(service).PointsFor(ReservedEdge.Bottom));

        /// <summary>지금 판정에 쓰는 플랫폼. 테스트가 <see cref="SetPlatformForTests"/>로 갈아끼운 값이
        /// 있으면 그것이 이긴다(실기 없이 Windows 분기를 실제로 <b>실행</b>해 보기 위한 유일한 창구).</summary>
        public static RuntimePlatform CurrentPlatform
            => _hasPlatformOverride ? _platformOverride : Application.platform;

        /// <summary>
        /// 테스트 전용 — "지금 플랫폼"을 고정한다. <b>이 창구가 없으면 Windows 하단 강제는 이 머신에서
        /// 한 줄도 실행되지 않고</b>, 소스 문자열 감사만 남아 «있다고 적혀 있다»까지밖에 못 잰다.
        /// </summary>
        public static void SetPlatformForTests(RuntimePlatform platform)
        {
            _hasPlatformOverride = true;
            _platformOverride = platform;
        }

        private static IReservedScreenEdgeService Resolve(IPlatformWindowService service)
        {
            if (_resolved && _service != null) return _service;

            // 데코레이터를 벗긴다 — 실제 구현이 안쪽에 있다(ReservedTopBarProbe와 같은 관례).
            IPlatformWindowService inner = service is FallbackPlatformWindowService decorator
                ? decorator.Inner
                : service;

            // (1) 플랫폼 서비스가 네 방향 계약을 직접 구현했으면 그것을 쓴다(Windows가 이 경로다).
            if (inner is IReservedScreenEdgeService direct)
            {
                _service = direct;
                _resolved = true;
                return _service;
            }

#if UNITY_STANDALONE_OSX
            // (2) macOS 조립 — CGDisplayBounds(화면 전체) − visibleFrame(작업영역)의 뺄셈 네 줄.
            //     MacWindowService는 이 인터페이스를 직접 달지 않고 별도 어댑터로 조립한다.
            if (MacOS.MacReservedScreenEdgeService.TryCreate(inner, out MacOS.MacReservedScreenEdgeService mac))
            {
                _service = mac;
                _resolved = true;
                return _service;
            }
#endif

            // (3) 구식 상단 전용 계약만 있는 구현 — 상단만 '측정됨'으로 좁혀 받는다.
            //     ★ 나머지 세 변을 0으로 채우되 측정 비트를 주지 않는 것이 핵심이다.
            //     "없다"로 위장하면 다음 라운드가 "좌우는 이미 0으로 확인됐다"는 거짓 근거를 얻는다.
            if (inner is IReservedTopBarService topOnly)
            {
                _service = new TopBarOnlyView(topOnly);
                _resolved = true;
                return _service;
            }

            return null;
        }

        /// <summary>
        /// 테스트 전용 — 네 변을 고정한다. <b>상단 프로브에도 같은 상단 값을 심는다</b>:
        /// 둘은 같은 화면의 같은 사실을 보므로, 한쪽만 심으면 <b>물리적으로 존재할 수 없는 세계</b>에서
        /// 검증하게 된다(상단 33pt인데 상단 0pt인 화면).
        /// </summary>
        public static void SetInsetsForTests(ReservedEdgeInsets insets)
        {
            _hasOverride = true;
            _overrideInsets = insets;
            ReservedTopBarProbe.SetInsetPointsForTests(insets.PointsFor(ReservedEdge.Top));
        }

        /// <summary>주입한 값을 걷고 실제 조회로 되돌린다. 캐시와 <b>상단 프로브</b>도, 그리고
        /// <b>플랫폼 주입</b>(<see cref="SetPlatformForTests"/>)도 함께 버린다 — 하나라도 남으면
        /// 다음 테스트가 «물리적으로 존재할 수 없는 세계»에서 검증하게 된다.</summary>
        public static void ResetForTests()
        {
            _hasOverride = false;
            _overrideInsets = ReservedEdgeInsets.Unknown;
            _hasPlatformOverride = false;
            _platformOverride = default;
            _service = null;
            _resolved = false;
            _cached = ReservedEdgeInsets.Unknown;
            _cachedAt = float.NegativeInfinity;
            ReservedTopBarProbe.ResetForTests();
        }

        /// <summary>
        /// 구식 <see cref="IReservedTopBarService"/>만 구현한 플랫폼을 네 방향 계약으로 좁혀 보여 준다.
        /// 좌·우·하단은 <b>모른다</b>(측정 비트 없음)로 남는다 — 0으로 위장하지 않는다.
        /// </summary>
        private sealed class TopBarOnlyView : IReservedScreenEdgeService
        {
            private readonly IReservedTopBarService _topBar;

            internal TopBarOnlyView(IReservedTopBarService topBar) { _topBar = topBar; }

            public bool TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets)
            {
                insets = ReservedEdgeInsets.Unknown;
                if (_topBar == null) return false;
                if (!_topBar.TryGetReservedTopInsetPoints(out float top)) return false;

                insets = ReservedEdgeInsets.TopOnly(top);
                return insets.IsMeasured(ReservedEdge.Top);
            }
        }
    }
}
