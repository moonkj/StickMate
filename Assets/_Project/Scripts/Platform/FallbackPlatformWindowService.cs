using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// IPlatformWindowService 데코레이터 — BUG-P1-B1(Blocker, docs/BUG_REPORT_PHASE1.md) 대응.
    ///
    /// 문제: 내부(실제) 서비스의 EnumerateFootholds()가 빈 리스트를 반환하면(예: Win32WindowService가
    /// "제목 있는 가시 창"을 하나도 못 찾음 — 유저가 모든 창을 최소화한 흔한 상황) GroundSensor/
    /// GroundedTick/CheckScreenBoundsOrFall이 전부 무력화되어 캐릭터가 화면 밖으로 영원히 낙하한다.
    ///
    /// [2026-08-27, Architect 핫픽스 — BUG-P1-R3-B1] Debugger 3차 검토에서 발견: "real.Count==0일 때만
    /// 대체"하는 원래 구현은 서로 떨어진 두 발판 사이의 빈 틈으로 AutoWanderController가 점프를 시도해
    /// 착지에 실패하는 경우(real.Count는 계속 1 이상이므로 폴백이 개입하지 않음) 여전히 무한 낙하가
    /// 재발했다. 수정: 내부 서비스가 무엇을 반환하든 상관없이 화면 하단 안전망을 매번
    /// 목록 끝에 추가한다(대체가 아니라 항상 추가) — GroundSensor.Sense()는 리스트를 순서대로 훑다가
    /// 첫 매치를 채택하므로(끝에 있는 이 안전망보다 앞선 실제 발판이 항상 우선), 진짜 발판 위에 서 있는
    /// 정상 동작에는 전혀 영향이 없고 오직 "그 어떤 실제 발판도 발밑에 없을 때"만 조용히 개입한다.
    ///
    /// [같은 핫픽스에서 함께 발견/수정] 원래 구현은 이 합성 사각형을 `new Rect(0f, 0f, width, height)`로
    /// 만들었는데, PlatformFoothold.ScreenRect의 좌표계(ScreenCoordinateConverter.cs 문서 참고: 좌상단
    /// 원점, y가 아래로 갈수록 증가)에서 y=0은 화면의 "맨 위"다 — 즉 원래 코드는 "화면 하단"이 아니라
    /// "화면 맨 위"에 안전망을 놓고 있었다(주석과 실제 동작이 반대였던 버그). 이제
    /// `ScreenCoordinateConverter.WorldToOsScreen`과 동일한 (Screen.height * dpi) 기준으로 화면 진짜
    /// 하단 근처에 배치한다.
    ///
    /// 나머지 메서드(CreateOverlayWindow/SetClickThrough/SetAlwaysOnTop/IsFullscreenAppActive)는 그대로
    /// 내부 서비스에 위임한다(이 데코레이터는 발판 열거에만 관여).
    ///
    /// ICursorPositionService 통과: 내부 서비스가 이 인터페이스를 구현하면(Win32WindowService 등)
    /// 그대로 델리게이트한다 — StickmanAgent가 `_platformService as ICursorPositionService`로 커서
    /// 조회 지원 여부를 판정하는 기존 설계(UX_FLOW.md 9절-3, Debugger 승인)가 이 데코레이터로 감싼
    /// 뒤에도 그대로 동작해야 하기 때문이다.
    ///
    /// ★ 2026-08-29 — 그 안전망은 더 이상 "화면 전체 폭 한 장"이 아니다. macOS Dock 가로 구간을
    /// 잘라낸 **두 조각**(Dock 왼쪽 바깥 / 오른쪽 바깥)이며, 그렇게 바꾼 이유(사용자 신고 "처음엔
    /// 독위에서 잘다니다가 좀 다니다 보면 다시 독과 겹쳐서 걸음")와 Dock 구간의 물리 바닥과의 역할
    /// 차이는 아래 AppendBottomSafetyNet()의 문서에 전부 적어뒀다.
    ///
    /// 의도적으로 감싸지 않는 대상: ScreenshotBackdropPlatformService(모바일)는 이 데코레이터로 감싸지
    /// 않는다 — 그 서비스의 "발판 0개" 반환은 버그가 아니라 "유저가 아직 발판을 탭 지정하지 않음"이라는
    /// 의도된 신호이고, 상위 온보딩 코드가 IsConfigured로 이 상태를 감지해 탭 지정 흐름을 노출해야
    /// 한다(UX_FLOW.md 3절/9절-7). 여기서 항상 발판이 있는 것처럼 위장하면 그 온보딩 게이트가 조용히
    /// 무력화된다 — 배선은 StickmanAgent.CreatePlatformService() 참고.
    /// </summary>
    public sealed class FallbackPlatformWindowService : IPlatformWindowService, ICursorPositionService, ILocalClickCaptureService, IDesktopIconLayoutService, IGlobalPointerButtonService, IGlobalKeyStateService, IReservedBottomBarService, IReservedTopBarService, IRawWindowRectSource, IWindowEnumerationCostSource, IForeignFullscreenTierSource
    {
        private readonly IPlatformWindowService _inner;
        private readonly ICursorPositionService _innerCursor; // null이면 내부 서비스가 커서 조회를 지원하지 않음
        private readonly ILocalClickCaptureService _innerClickCapture; // null이면 내부 서비스가 부분적 클릭관통 해제를 지원하지 않음
        private readonly IDesktopIconLayoutService _innerIconLayout; // null이면 내부 서비스가 아이콘 좌표 조회를 지원하지 않음
        // null이면 내부 서비스가 Dock 실측을 지원하지 않음 -> dockFootholdWidthFraction 고정 추정 폴백.
        private readonly IDockMetricsService _innerDockMetrics;
        // null이면 이 플랫폼이 "하단 예약 막대의 정확한 사각형"을 알려주지 못함(macOS가 그렇다).
        // 있으면 그 값이 **절대적**이다 — 아래 TryGetDockRectOsScreen 0순위 참고.
        private readonly IReservedBottomBarService _innerBottomBar;

        // ★★ 2026-09-02 — **네 번째** 통과 누락. FallbackServicePassthroughTests가 잡았다.
        // Win32WindowService가 IReservedTopBarService(상단 도킹 작업표시줄/툴바 두께 실측)를 달았는데
        // 이 데코레이터가 통과시키지 않았다.
        //
        // ★ 다만 **이번 것은 앞의 셋과 달리 사용자에게 보이는 결함이 아니었다** — 그 차이를 여기 적어
        //   두지 않으면 다음 사람이 "또 조용히 죽었다"고 잘못 읽는다. 상단 인셋의 유일한 소비 경로인
        //   ReservedTopBarProbe.Resolve()는 `as`가 아니라 **decorator.Inner로 벗긴 뒤** 캐스팅하므로
        //   Windows 인셋은 실제로 도달하고 있었다(Tests/EditMode/FallbackServicePassthroughTests의
        //   `상단_인셋은_데코레이터를_거쳐도_소비_측에_도달한다`가 수정 **전**에도 초록이었다 — 실측).
        //   그래도 통과시키는 이유는 두 가지다:
        //     (1) 벗기기는 **한 겹**만 한다. 데코레이터가 하나 더 끼는 날 조용히 null이 된다.
        //     (2) 이 저장소의 모든 선택적 캐퍼빌리티 소비자는 `PlatformService as I...` 관례를 쓴다.
        //         다음 소비자가 관례대로 쓰면 그때 죽는다.
        //
        // null이면 이 플랫폼이 상단 예약 띠 두께를 알려주지 못함.
        // ★ macOS에서는 여기가 **항상 null**이다 — MacWindowService는 이 인터페이스를 직접 달지 않고
        //   ReservedTopBarProbe가 MacReservedTopBarService로 조립한다(그 조립은 프로브가 벗긴
        //   inner를 보고 하므로 이 필드와 무관하게 계속 동작한다). 즉 이 통과 경로가 macOS에서
        //   false를 돌려주는 것은 정상이며, 그것이 macOS 인셋을 0으로 만들지 않는다.
        private readonly IReservedTopBarService _innerTopBar;

        // ★ 사용자 신고 "마우스로 안 잡힘" 조사 중 함께 발견(2026-08-28): 이 데코레이터가
        // IGlobalPointerButtonService를 통과시키지 않아, StickmanClickHitbox의
        // `PlatformService as IGlobalPointerButtonService` 캐스팅이 **항상 null**이었다
        // (실측 로그: "전역버튼경로=미지원"). 그래서 창 포커스와 무관한 전역 버튼 폴링 경로가
        // 실제로는 한 번도 활성화된 적이 없었다. ICursorPositionService와 동일한 위임 패턴으로 통과시킨다.
        private readonly IGlobalPointerButtonService _innerButton; // null이면 내부 서비스가 전역 버튼 조회를 지원하지 않음
        private readonly IForeignFullscreenTierSource _innerTier;  // null이면 등급 조회 미지원 -> bool로 강등
        private readonly IGlobalKeyStateService _innerKeyState;     // null이면 내부 서비스가 전역 키 조회를 지원하지 않음
        // null이면 내부 서비스가 "가려짐 필터 이전 원본 창 목록"을 지원하지 않음 -> RawWindows가 빈 목록.
        private readonly IRawWindowRectSource _innerRawWindows;

        // ★★ 2026-09-01 계측 결함 수정 — 사용자 실기 로그가 `전체 창 -1개, 정밀검사 -1회`로 찍혔다.
        //
        // 원인은 이 데코레이터다. Windows에서 StickmanAgent가 만드는 서비스는
        // `new FallbackPlatformWindowService(new Win32WindowService())`인데, 계측을 소비하는
        // FootholdPoller는 `service as IWindowEnumerationCostSource`로 캐스팅한다. 이 데코레이터가
        // 그 인터페이스를 통과시키지 않아 캐스팅이 **항상 null**이었고, 폴러는 규약대로 -1(모르는 값)을
        // 보고했다. 즉 "창이 800개일 때 비싸지는가"를 원격에서 확인할 수단이 통째로 죽어 있었다.
        //
        // 이 저장소는 **같은 결함을 이미 두 번 겪었다**: IGlobalPointerButtonService(2026-08-28,
        // "전역버튼경로=미지원")와 IRawWindowRectSource가 그것이다. 세 번째다 —
        // 그래서 Tests/EditMode/FallbackServicePassthroughTests가 이제 "IPlatformWindowService를
        // 구현한 서비스가 노출하는 선택적 인터페이스는 전부 이 데코레이터를 통과한다"를
        // 인터페이스 이름 하드코딩 없이 리플렉션으로 잠근다(네 번째를 막는 것이 목적이다).
        private readonly IWindowEnumerationCostSource _innerCost;
        private readonly StickConfig _config; // desktopDpiScale만 읽는다 — null이면 배율 1로 취급.

        // 합성 발판 캐시 무효화 판정에 쓰는 직전 오버레이 창 원점(아래 AppendBottomSafetyNet 참고).
        private Vector2 _cachedOverlayOrigin = new Vector2(float.NaN, float.NaN);

        // 재사용 버퍼. "실제 발판 전부 + Dock + 안전망 조각들"을 매 호출 다시 채워 넣지만 리스트 자체는 재할당하지
        // 않는다(24시간 상주 앱, GC 압박 방지 컨벤션 — FootholdPoller.cs와 동일 원칙). EnumerateFootholds()가
        // 매 프레임이 아니라 FootholdPoller의 폴링 주기에서만 호출되므로 Add() 비용은 무시 가능하다.
        private readonly List<PlatformFoothold> _combined = new List<PlatformFoothold>(8);

        // 바닥 안전망 캐시 — 2026-08-29부터 Dock 가로 구간을 잘라낸 **두 조각**이다
        // (AppendBottomSafetyNet 문서 참고). Dock이 비활성이면 왼쪽 조각 하나가 전체 폭을 차지한다.
        private PlatformFoothold _safetyNetLeft;
        private PlatformFoothold _safetyNetRight;
        private bool _hasSafetyNetLeft;
        private bool _hasSafetyNetRight;
        private float _cachedScreenWidth = -1f;
        private float _cachedScreenHeight = -1f;
        // 캐시 무효화용 직전 Dock 가로 구간(설정을 런타임에 바꿔도 안전망 구멍이 함께 따라오게 한다).
        private bool _cachedHasDock;
        private float _cachedDockLeftOsX = float.NaN;
        private float _cachedDockRightOsX = float.NaN;
        // ★ 2026-09-01 — 권위 있는 화면 경계(모니터 좌/우/하단)도 캐시 키에 넣는다. 창을 다른 모니터로
        // 옮기면 Dock 구간은 그대로여도 경계가 바뀌는 경우가 있어, 이게 키에 없으면 안전망이 이전
        // 모니터 기준으로 굳는다(= 다시 화면 밖 조각이 생긴다).
        private bool _cachedHasScreenBounds;
        private float _cachedScreenLeftOsX = float.NaN;
        private float _cachedScreenRightOsX = float.NaN;
        private float _cachedScreenBottomOsY = float.NaN;

        public FallbackPlatformWindowService(IPlatformWindowService inner, StickConfig config = null)
        {
            _inner = inner;
            _innerCursor = inner as ICursorPositionService;
            _innerClickCapture = inner as ILocalClickCaptureService;
            _innerIconLayout = inner as IDesktopIconLayoutService;
            _innerDockMetrics = inner as IDockMetricsService;
            _innerBottomBar = inner as IReservedBottomBarService;
            _innerTopBar = inner as IReservedTopBarService;
            _innerButton = inner as IGlobalPointerButtonService;
            // 등급 조회는 <b>선택적 능력</b>이다(IForeignFullscreenTierSource 문서 참고).
            // 지원하지 않는 내부 서비스(Null/모바일)에서는 null로 남고, 아래 조회가
            // 기존 bool 하나로 강등한다 = 등급 1이 없던 예전 동작 그대로.
            _innerTier = inner as IForeignFullscreenTierSource;
            _innerKeyState = inner as IGlobalKeyStateService;
            _innerRawWindows = inner as IRawWindowRectSource;
            _innerCost = inner as IWindowEnumerationCostSource;
            _config = config;
        }

        /// <summary>
        /// 감싸고 있는 실제 플랫폼 서비스. 진단(플랫폼별 부가 정보 조회 — 예: macOS의 창 소유 앱 이름)
        /// 목적으로만 노출한다. 게임플레이 코드는 절대 이 프로퍼티로 구체 타입에 의존하지 말고
        /// IPlatformWindowService 계약만 사용할 것(아키텍처 2절 플랫폼 추상화 원칙).
        /// </summary>
        public IPlatformWindowService Inner => _inner;

        // 미지원 내부 서비스일 때 돌려줄 빈 목록 — 매 조회마다 새 배열/리스트를 만들지 않도록 1회만 만든다.
        private static readonly IReadOnlyList<PlatformFoothold> EmptyRawWindows = new List<PlatformFoothold>(0).AsReadOnly();

        /// <summary>
        /// IWindowEnumerationCostSource 통과. <b>미지원을 0으로 위장하지 않는다</b> — 내부 서비스가
        /// 계측을 지원하지 않으면 그 인터페이스의 규약대로 -1을 그대로 내보낸다(0개 열거와 미지원은
        /// 완전히 다른 사실이고, 원격 진단에서 그 둘을 섞으면 잘못된 결론이 나온다).
        ///
        /// <para>이 데코레이터가 목록 끝에 덧붙이는 합성 발판(Dock/안전망)은 <b>OS 열거를 거치지
        /// 않으므로</b> 이 숫자에 더하지 않는다 — 이 채널이 답하는 질문은 "OS가 콜백한 최상위 창이 몇
        /// 개였나"이고, 합성 발판은 그 질문과 무관하다.</para>
        /// </summary>
        public int LastEnumeratedWindowCount => _innerCost != null ? _innerCost.LastEnumeratedWindowCount : -1;

        /// <inheritdoc cref="IWindowEnumerationCostSource.LastDwmProbeCount"/>
        public int LastDwmProbeCount => _innerCost != null ? _innerCost.LastDwmProbeCount : -1;

        /// <summary>
        /// IRawWindowRectSource 통과(ICursorPositionService 등과 동일한 위임 패턴). 이 데코레이터가
        /// 목록 끝에 덧붙이는 <b>합성 발판(Dock/안전망, Handle&lt;0)은 여기에 절대 섞지 않는다</b> —
        /// 이 채널의 계약은 "OS가 실제로 열거해 준 원본 창"이고, 창 도둑이 합성 사각형을 대상으로 삼으면
        /// 존재하지도 않는 창의 고스트를 그리게 된다.
        /// </summary>
        public IReadOnlyList<PlatformFoothold> RawWindows
            => _innerRawWindows != null ? _innerRawWindows.RawWindows : EmptyRawWindows;

        /// <summary>
        /// 합성 안전망 발판에 부여하는 핸들. GroundSensor.GroundInfo.GroundedFootholdHandle이 이 값이면
        /// "실제 창이 아니라 안전망 위에 서 있다"는 뜻이다(진단 로그가 이 상수를 참조한다).
        /// 2026-08-29부터 안전망은 Dock 가로 구간을 잘라낸 두 조각이며, 이 핸들은 그 중 **Dock 왼쪽
        /// 바깥 조각**(그리고 Dock이 비활성일 때의 전체 폭 한 조각)을 가리킨다 — 오른쪽 조각은
        /// <see cref="SyntheticFootholdHandleRight"/>다.
        /// </summary>
        public const long SyntheticFootholdHandle = -1L;

        /// <summary>
        /// Dock **오른쪽 바깥** 안전망 조각의 핸들. 왼쪽 조각(-1)과 반드시 구분되어야 한다 —
        /// GroundSensor.Sense()의 발판 고착(preferredHandle)이 핸들로 발판을 식별하므로, 두 조각이
        /// 같은 핸들을 쓰면 "지금 딛고 있는 조각"의 좌우 경계(GroundInfo.CurrentFoothold*WorldX)가
        /// 반대편 조각의 것으로 잘못 잡혀 AutoWanderController의 경계 판정이 어긋난다
        /// (GroundSensor.TryGetFootholdEdgeWorld/TryGetFootholdTopWorldY도 핸들로 첫 매치를 고른다).
        /// </summary>
        public const long SyntheticFootholdHandleRight = -3L;

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            IReadOnlyList<PlatformFoothold> real = _inner.EnumerateFootholds();

            _combined.Clear();
            if (real != null)
            {
                for (int i = 0; i < real.Count; i++) _combined.Add(real[i]);
            }
            // ★ 2026-08-28 (2) — Dock 발판(사용자: "독위에서만 걷고 독아래로 가면 바닥으로 내려가야").
            // 안전망보다 **앞에** 넣는다: 둘 다 합성 발판이지만 Dock 쪽이 더 높으므로 낙하 스윕 판정이
            // 먼저 만나야 한다(GroundSensor.TryFindLandingCrossing은 가장 높은 것을 채택하므로 순서에
            // 무관하지만, "실제 창 -> Dock -> 바닥" 이라는 고도 순서를 목록 순서로도 드러내 둔다).
            if (TryGetDockFoothold(out PlatformFoothold dock)) _combined.Add(dock);
            // 항상 마지막에 추가 — 실제 발판이 우선 채택되도록. 2026-08-29부터 Dock 가로 구간을
            // 잘라낸 두 조각(Dock 왼쪽 바깥 / 오른쪽 바깥)이 들어간다(AppendBottomSafetyNet 문서 참고).
            AppendBottomSafetyNet(_combined);
            return _combined;
        }

        /// <summary>
        /// Dock에 부여하는 합성 발판 핸들. 안전망(-1)과 구분해 진단 로그가 "지금 Dock 위인지 화면
        /// 바닥인지"를 사람이 읽을 수 있게 한다.
        /// </summary>
        public const long DockFootholdHandle = -2L;

        /// <summary>
        /// ★ macOS Dock을 발판으로 합성한다 (2026-08-28, 사용자 요청 "독위에서만 걷고 독아래로 가면
        /// 바닥으로 내려가야하는데").
        ///
        /// 사각형은 **한 글자도 여기서 계산하지 않는다** — 전부 <see cref="TryGetDockRectOsScreen"/>
        /// 단일 소스에서 나온다(같은 값이 안전망의 구멍에도 그대로 쓰인다). 어떻게 그 사각형을 구하는지,
        /// 왜 Dock 창의 bounds를 쓸 수 없는지, 각 계수의 실측 근거는 전부 그 메서드와
        /// Platform/IDockMetricsService.cs의 문서에 있다.
        ///
        /// false를 돌려주는 경우 = Dock 발판이 존재하지 않아야 하는 경우(자동 숨김 / 좌우 세로 Dock /
        /// 두께 0 / 폭 0). 그때는 모든 낙하가 화면 바닥 안전망으로 간다.
        /// </summary>
        public bool TryGetDockFoothold(out PlatformFoothold dock)
        {
            dock = default;
            if (!TryGetDockRectOsScreen(out Rect rect)) return false;
            dock = new PlatformFoothold(DockFootholdHandle, rect, true);
            return true;
        }

        // Dock 구간 로그는 **내용이 바뀔 때만** 남긴다 — 이 함수는 발판 폴링마다(0.3초) 불리므로
        // 그대로 두면 로그가 잠긴다. 그러면서도 "지금 Dock을 어디로 보고 있는가"는 항상 최신 1줄로 남는다.
        private string _lastDockSpanLog;

        private void LogDockSpanOnce(string message)
        {
            if (_lastDockSpanLog == message) return;
            _lastDockSpanLog = message;
            Debug.Log("[Dock실측] " + message);
        }

        /// <summary>
        /// ★★★ Dock 사각형의 **단일 소스**. 이 프로젝트에서 Dock 기하를 아는 곳은 여기 하나뿐이다.
        ///
        /// 이 메서드 하나에서 세 곳이 전부 파생된다:
        ///   (a) <see cref="TryGetDockFoothold"/>      — Dock 위에 서는 발판 사각형(가로 구간 + 상단 Y).
        ///   (b) <see cref="TryGetDockSpanOsScreen"/>  — 그 사각형의 좌/우 끝만 뽑아 쓰는 얇은 래퍼.
        ///   (c) <see cref="AppendBottomSafetyNet"/>   — 화면 최하단 안전망에서 잘라낼 **구멍**의 좌/우 끝.
        /// 즉 "안전망의 구멍"과 "Dock 발판"은 정의상 정확히 같은 X 구간이라, 둘이 어긋나 틈(발판이
        /// 하나도 없는 X 구간 -> 낙하 고착)이나 겹침(Dock 아래를 걸어다님)이 생기는 것이 구조적으로
        /// 불가능하다. (이 프로젝트는 두 곳이 따로 계산해 어긋난 버그가 이미 2회 있었다 —
        /// BUG-P1-R4-B1, BUG-P1-R5-B2. 리더가 그래서 단일 소스를 못박았다.)
        ///
        /// ============================================================================
        /// 어떻게 구하는가 (2026-08-29 2차 라운드에서 전면 재보정)
        /// ============================================================================
        /// Dock 창의 bounds는 쓸 수 없다 — Dock 프로세스가 소유한 창은 'Dock'과 'Wallpaper-' 둘뿐이고
        /// 둘 다 화면 전체 크기이며, 시스템 전체 창 143개 중 Dock 막대 모양인 창은 소유자를 불문하고
        /// 하나도 없다(전수 덤프로 확정). 정확한 나머지 경로 둘은 화면 기록/접근성 권한을 요구해 금지다.
        /// 상세는 Platform/IDockMetricsService.cs 인터페이스 문서 1절.
        ///
        /// 그래서 타일 개수 N에서 계산한다. 직전 라운드가 틀린 이유는 공식이 아니라 **N을 몰랐던 것**
        /// 하나였고(실행 중이지만 고정 안 된 앱 수를 상수 6으로 때려박음 -> 좌우 각 77pt 과대 -> 이번
        /// "부양" 신고), 이제 N을 NSWorkspace로 정확히 센다.
        ///
        ///     폭 = N x (tilesize + dockTilePitchPaddingPoints)
        ///        + dockPanelFixedPaddingPoints
        ///        + 구분선수 x dockSeparatorWidthPoints
        ///     좌우 = 화면 가로 정중앙 정렬 후 dockFootholdEdgeInsetPoints만큼 안쪽으로 깎음
        ///     두께 = tilesize + dockThicknessTilePaddingPoints,  상단 Y = 화면 바닥 - 두께
        ///
        /// 실측 검증(6표본, 최대 오차 1.0pt)과 각 계수의 근거는 IDockMetricsService.cs 3~4절과
        /// StickConfig의 각 필드 Tooltip에 있다.
        ///
        /// Dock이 비활성(자동 숨김 / 좌우 세로 배치 / 두께 0 / 폭 비율 0)이면 false — 그때 안전망은
        /// 예전처럼 화면 전체 폭 한 조각으로 남는다(잘라낼 Dock 자체가 없으므로 겹칠 일도 없다).
        /// </summary>
        /// <param name="rect">오버레이 창 원점 기준 OS 좌표(PlatformFoothold.ScreenRect와 같은 공간).</param>
        public bool TryGetDockRectOsScreen(out Rect rect)
        {
            rect = default;
            if (_config == null) return false;

            // ★★ 0순위 — 플랫폼이 하단 예약 막대의 **정확한 사각형**을 알고 있으면 그것으로 끝낸다.
            //    (Windows 작업표시줄: GetMonitorInfo의 rcMonitor/rcWork 차. 근거와 이전 오동작은
            //     Platform/IReservedBottomBarService.cs 문서 참고 — 사용자 신고 "작업표시줄에 걸쳐서
            //     돌아다닌다"의 수정 지점이다.)
            //
            //    여기서 false는 **폴백 신호가 아니라 확정 신호**다("지금 하단 예약 막대가 없다" —
            //    자동 숨김이거나 작업표시줄이 좌/우/상단에 있는 경우). 그런데도 아래 고정 비율 추정으로
            //    흘려보내면 존재하지도 않는 막대 위에 캐릭터가 부양한다 — 그래서 즉시 return한다.
            //    이 조기 반환이 macOS에 영향을 주지 않는 이유: MacWindowService는 이 인터페이스를
            //    구현하지 않으므로 _innerBottomBar가 null이고, 이 블록 자체가 실행되지 않는다.
            if (_innerBottomBar != null)
            {
                if (!_innerBottomBar.TryGetReservedBottomBarOsScreen(out Rect barRect))
                {
                    LogDockSpanOnce("하단 예약 막대 없음(OS 확정) — 발판을 만들지 않고 화면 최하단 " +
                        "안전망만 전체 폭으로 둡니다.");
                    return false;
                }
                if (barRect.width <= 0f || barRect.height <= 0f) return false;

                LogDockSpanOnce($"하단 예약 막대 실측(OS 확정) — rect={barRect} " +
                    $"(폭 {barRect.width:F0}, 두께 {barRect.height:F0}, 상단 y={barRect.yMin:F0}). " +
                    "추정 공식은 사용하지 않습니다.");
                rect = barRect;
                return true;
            }

            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(_config));
            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * dpi;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;

            float width;
            float thickness;

            // ★ 1순위 — OS에서 읽은 타일 구성으로 계산한다.
            if (_config.dockMetricsFromSystemEnabled && _innerDockMetrics != null
                && _innerDockMetrics.TryGetDockMetrics(out DockMetrics m))
            {
                // 세로(좌/우) Dock이면 "화면 하단의 가로 띠"라는 Dock 발판 개념 자체가 성립하지 않는다.
                // 자동 숨김이면 평소 화면에 없으므로 발판으로 삼으면 캐릭터가 허공에 선다. 둘 다 비활성화.
                if (!m.IsBottomOriented || m.IsAutoHidden)
                {
                    LogDockSpanOnce($"Dock 발판 비활성화 — {(m.IsAutoHidden ? "자동 숨김이 켜져 있음" : "Dock이 화면 하단이 아님(좌/우 세로 Dock)")}. " +
                        "바닥 안전망만 화면 전체 폭으로 남습니다.");
                    return false;
                }

                // 타일 수를 정확히 셌으면(IsTileCountExact) 보정 상수를 **더하지 않는다** — 세는 데
                // 성공했는데도 더하면 직전 라운드의 과대 추정(좌우 각 77pt)이 그대로 재발한다.
                int extra = m.IsTileCountExact ? 0 : Mathf.Max(0, _config.dockExtraRunningAppTileEstimate);
                int tiles = Mathf.Max(1, m.TileCount + extra);
                float pitch = Mathf.Max(1f, m.TileSizePoints + _config.dockTilePitchPaddingPoints);
                int separators = Mathf.Max(0, m.SeparatorCount);

                width = tiles * pitch
                        + _config.dockPanelFixedPaddingPoints
                        + separators * _config.dockSeparatorWidthPoints;
                width = Mathf.Clamp(width, 0f, screenW);

                thickness = m.TileSizePoints + _config.dockThicknessTilePaddingPoints;

                LogDockSpanOnce($"Dock 계산 — tilesize={m.TileSizePoints:F0}pt, 타일 {m.TileCount}개" +
                    $"({(m.IsTileCountExact ? "정확히 셈" : $"셀 수 없어 +{extra}개 보정")}), 구분선 {separators}개, " +
                    $"피치 {pitch:F1}pt -> 폭 {width:F1}pt (화면의 {(width / Mathf.Max(1f, screenW)):P1}), " +
                    $"두께 {thickness:F1}pt, 가장자리 여유 {_config.dockFootholdEdgeInsetPoints:F1}pt.");
            }
            else
            {
                // 2순위 — 폴백(비-macOS이거나 조회 실패). 예전과 완전히 동일한 고정 비율 추정.
                float widthFraction = _config.dockFootholdWidthFraction;
                if (widthFraction <= 0f) return false;
                width = screenW * Mathf.Clamp01(widthFraction);
                thickness = _config.dockFootholdThicknessPoints;
            }

            if (thickness <= 0f) return false;

            // 가운데 정렬은 추정이 아니라 실측이다 — 표본 6개 전부에서 패널 중심이 화면 정중앙과
            // 0.25pt 이내로 일치했고, 타일 1개 변화가 좌우로 정확히 대칭으로 나타났다.
            float left = origin.x + (screenW - width) * 0.5f;
            float right = left + width;

            // 안쪽으로 깎기(narrow bias) — 근거는 StickConfig.dockFootholdEdgeInsetPoints Tooltip.
            // 깎다가 폭이 0 이하가 되면(타일이 극단적으로 적은 Dock) Dock 발판 자체를 포기한다.
            float inset = Mathf.Max(0f, _config.dockFootholdEdgeInsetPoints);
            left += inset;
            right -= inset;
            if (right - left <= 0f) return false;

            rect = new Rect(left, origin.y + screenH - thickness, right - left, thickness);
            return true;
        }

        /// <summary>
        /// Dock 가로 구간(좌/우 끝)만 필요한 호출부를 위한 얇은 래퍼. 값은 전적으로
        /// <see cref="TryGetDockRectOsScreen"/>에서 나온다 — 여기서 따로 계산하는 것은 하나도 없다.
        /// </summary>
        public bool TryGetDockSpanOsScreen(out float dockLeftOsX, out float dockRightOsX)
        {
            if (!TryGetDockRectOsScreen(out Rect rect))
            {
                dockLeftOsX = 0f;
                dockRightOsX = 0f;
                return false;
            }
            dockLeftOsX = rect.xMin;
            dockRightOsX = rect.xMax;
            return true;
        }

        /// <summary>
        /// ★★ 화면 최하단 "바닥 안전망"을 목록 끝에 덧붙인다 — 2026-08-29부터 **Dock 가로 구간을
        /// 잘라낸 두 조각**(Dock 왼쪽 바깥 / Dock 오른쪽 바깥)이다.
        ///
        /// ============================================================================
        /// 왜 한 장짜리 전체 폭 안전망이 버그였는가 (사용자 신고 2026-08-29:
        /// "처음엔 독위에서 잘다니다가 좀 다니다 보면 다시 독과 겹쳐서 걸음")
        /// ============================================================================
        /// 직전 구성은 Dock 발판(화면 바닥-75pt, 가로 정중앙 65%)과 이 안전망(화면 최하단, **가로 화면
        /// 전체 폭**) 두 장이었다. 그래서 다음 순서가 성립했다:
        ///   (1) 캐릭터가 Dock 위(중앙 65% 구간)를 정상적으로 걷는다.        ← 사용자가 처음 본 정상 동작
        ///   (2) Dock 가로 끝을 벗어나면 정상 낙하한다.
        ///   (3) 화면 최하단 안전망에 착지한다.                              ← 여기까지가 의도된 동작
        ///   (4) 그런데 그 안전망이 **화면 전체 폭**이라, 계속 걸어서 **다시 Dock 가로 구간 안쪽으로**
        ///       들어간다(GroundSensor.Sense()의 발판 고착은 핸들이 같은 한 X 범위 안에서 자유롭게
        ///       이동할 수 있게 해준다 — 그 발판이 화면 전체 폭이었으므로 제한이 사실상 없었다).
        ///   (5) 그 자리에서 캐릭터는 화면 최하단 높이(OS y≈942)인데 그 위 75pt를 Dock이 차지하고 있으니
        ///       **Dock과 겹쳐 보인다**. 우리 오버레이는 항상 최상단이라 캐릭터가 Dock 위에 덧그려진다.
        ///
        /// 수정: 안전망을 "전체 폭 사각형 하나"가 아니라 **Dock 좌측 바깥 조각 + 우측 바깥 조각**으로
        /// 쪼갠다. 그러면 X 좌표별로 바닥이 정확히 하나씩만 존재하게 된다:
        ///   • Dock 가로 범위 **안**  -> 바닥은 Dock 상단(OS y≈907)뿐   -> 캐릭터가 Dock **위**에 선다.
        ///   • Dock 가로 범위 **밖**  -> 바닥은 화면 최하단(OS y≈942)   -> Dock 옆 바닥에 선다.
        /// 이것이 사용자가 원래 요청한 동작이다("독위에서만 걷고 독아래로 가면 바닥으로 내려가야").
        /// 안전망 조각의 끝은 곧 발판의 끝이므로 AutoWanderController의 경계 판정(26-2)이 Dock 경계에서
        /// 캐릭터를 멈춰 세우고 되돌린다 — 즉 (4)의 "걸어서 Dock 밑으로 들어가는" 경로가 사라진다.
        /// 반대로 Dock 위로 다시 올라가려면 위에서 떨어지거나 점프/파쿠르가 필요하다(의도된 동작).
        ///
        /// ============================================================================
        /// 이 "논리적 발판"과 씬의 "물리 지면 콜라이더(PhysicsGround)"의 역할 차이 — 절대 혼동 금지
        /// ============================================================================
        /// 두 가지는 **일부러** 모양이 다르다(리더 지시 1항):
        ///   • 이 안전망(논리적 발판, 두 조각) : GroundSensor의 접지/착지/경계 **판정**에만 쓰인다.
        ///     Dock 구간에 구멍이 뚫려 있어야 "Dock 밑을 걸어다니는" 판정이 원천적으로 불가능해진다.
        ///   • Editor/SceneBootstrapper.CreateGroundCollider()의 PhysicsGround(BoxCollider2D, 폭 200유닛)
        ///     : Unity 2D 물리의 **실제 충돌면**이며 **전체 폭 그대로 유지한다**. RAGDOLL은 상태머신의
        ///     접지 판정이 아니라 순수 물리로 굴러다니므로, 여기까지 구멍을 뚫으면 Dock 가로 구간에서
        ///     랙돌이 바닥을 그대로 통과해 화면 아래로 사라진다. 논리적 구멍은 "그 X에서는 서 있을 수
        ///     없다"는 뜻이지 "그 X에는 물리 바닥이 없다"는 뜻이 아니다.
        /// 즉 Dock 구간의 최하단에서 캐릭터는 (물리적으로는 떠받쳐지지만) 논리적으로는 접지하지 못한다.
        /// 그 상태로 흘러드는 예외 경로(사용자가 그리로 던짐 등)는 StickmanBlackboard의 최종 안전망
        /// (LostCharacterRescueSeconds초 이상 Fall이면 RescueToSafeGround)이 회수한다 — 그 구조 대신
        /// 물리 바닥에 구멍을 뚫는 선택은 "랙돌이 화면 밖으로 사라진다"는 훨씬 나쁜 실패로 이어진다.
        ///
        /// 구멍의 좌/우 끝은 <see cref="TryGetDockSpanOsScreen"/> **하나**에서만 나온다(Dock 발판 사각형도
        /// 같은 메서드에서 나온다) — 두 곳이 따로 계산해 틈/겹침이 생기는 것을 구조적으로 막는다(리더 지시 2항).
        /// Dock이 비활성(폭 0/두께 0, 예: Dock 자동 숨김)이면 잘라낼 것이 없으므로 예전과 100% 동일한
        /// **화면 전체 폭 한 조각**이 된다.
        /// </summary>
        private void AppendBottomSafetyNet(List<PlatformFoothold> target)
        {
            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(_config));
            float width = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            float height = (Screen.height > 0 ? Screen.height : 1080f) * dpi;

            // 오버레이 창이 화면 좌상단이 아닌 곳에서 시작할 수 있다(macOS: 메뉴바/Dock을 뺀 가운데
            // 구간). 이 합성 발판은 "우리 창의 하단"을 뜻하므로, 창 원점만큼 통째로 평행이동해야
            // ScreenCoordinateConverter가 만들어내는 캐릭터의 OS 좌표와 같은 공간에 놓인다 —
            // 안 그러면 캐릭터의 발 높이와 이 발판의 상단 Y가 창 오프셋만큼 어긋나 접지 판정이
            // 영원히 실패한다(드래그&던지기 배선 라운드에 실측으로 확인: 상태가 Fall에 고착).
            Vector2 overlayOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;

            // 잘라낼 구멍 = Dock 발판과 **정확히 같은** X 구간(위 문서 참고, 단일 소스).
            bool hasDock = TryGetDockSpanOsScreen(out float dockLeftOsX, out float dockRightOsX);

            // ★ 2026-09-01 — 좌표 출처 통일(BottomSafetyNetPolicy 문서 참고). 안전망은 **오버레이 창**
            // 기하에서 나오고 작업표시줄은 **모니터** 기하에서 나와, 둘이 어긋나면 화면 밖 + 막대 뒤에
            // 발판 조각이 생겼다(실측: 모니터 오른쪽 밖 2pt, 모니터 하단보다 39px 아래).
            bool hasScreenBounds = TryGetScreenEdgesOsScreen(
                out float screenLeftOsX, out float screenRightOsX, out float screenBottomOsY);

            if (!Mathf.Approximately(width, _cachedScreenWidth) || !Mathf.Approximately(height, _cachedScreenHeight)
                || !Mathf.Approximately(overlayOrigin.x, _cachedOverlayOrigin.x) || !Mathf.Approximately(overlayOrigin.y, _cachedOverlayOrigin.y)
                || hasDock != _cachedHasDock
                || !Mathf.Approximately(dockLeftOsX, _cachedDockLeftOsX) || !Mathf.Approximately(dockRightOsX, _cachedDockRightOsX)
                || hasScreenBounds != _cachedHasScreenBounds
                || !Mathf.Approximately(screenLeftOsX, _cachedScreenLeftOsX)
                || !Mathf.Approximately(screenRightOsX, _cachedScreenRightOsX)
                || !Mathf.Approximately(screenBottomOsY, _cachedScreenBottomOsY))
            {
                _cachedScreenWidth = width;
                _cachedScreenHeight = height;
                _cachedOverlayOrigin = overlayOrigin;
                _cachedHasDock = hasDock;
                _cachedDockLeftOsX = dockLeftOsX;
                _cachedDockRightOsX = dockRightOsX;
                _cachedHasScreenBounds = hasScreenBounds;
                _cachedScreenLeftOsX = screenLeftOsX;
                _cachedScreenRightOsX = screenRightOsX;
                _cachedScreenBottomOsY = screenBottomOsY;

                // BUG-P1-R5-B2 대응(Coder 발견/수정, 2026-08-28) — "바로 바탕화면에서 구동" 라운드가 처음
                // 만든 실제 Standalone .app을 직접 실행해 Player.log에 캐릭터 위치를 초 단위로 남기는
                // 임시 디버그 로그로 확인한 결과, 화면에 실제 OS 창이 하나도 안 보이는 흔한 상황(이번
                // 검증 환경 포함)에서 캐릭터가 FallState에 영원히 갇혀(footholds=1이지만 grounded=False)
                // 좌우로 전혀 움직이지 않는 것을 실측으로 재현했다. 근본 원인은 이 안전망의 두께가
                // Editor/SceneBootstrapper.cs가 씬에 굽는 지면 Y의 기준
                // (NullPlatformWindowService.DummyFootholdHeightFraction)과 서로 다른 값이었던 것이다.
                // 수정: 그 공개 상수를 그대로 참조해 **단일 소스**로 묶는다(그 선언부 문서에 유도 과정).
                // ★ 2026-08-28: 그 상수가 화면 높이의 20% -> 화면 최하단(BottomSafetyNetInsetPoints)으로
                // 내려갔다. 여기 계산식은 한 글자도 바뀌지 않지만 결과는 크게 달라진다 — 실측 1512x982
                // 화면 기준 발판 상단이 OS y=785.6(화면 중간쯤)에서 화면 최하단 근처로 내려간다.
                // ★ 2026-08-29(3): 그 상수가 40pt -> 6pt로 더 내려갔다(사용자 3차 신고 "아직도 바닥을
                // 정확히 파악못하는거 같음"). 같은 화면에서 발판 상단 OS y=942 -> 976. 40pt의 근거였던
                // "발끝이 루트보다 0.55유닛 아래"는 LineRenderer.bounds의 계측 아티팩트였고, 정점 기하로
                // 다시 재보니 실제로 필요한 값은 6pt였다 — 유도 과정은 그 상수 선언부에 있다.
                float thickness = height * NullPlatformWindowService.DummyFootholdHeightFraction;

                // 폭 배율은 NullPlatformWindowService의 더미 발판과 공유하는 단일 소스다(현재 1 =
                // 화면 폭과 정확히 일치; 계산 구조를 남겨 나중에 다시 조정할 여지를 둔다 — 그 선언부의
                // "되돌림" 문단 참고).
                float widenedWidth = width * NullPlatformWindowService.DummyFootholdWidthMultiplier;
                float widenedX = (width - widenedWidth) / 2f;

                // y = height - 두께: ScreenCoordinateConverter와 동일한 좌상단원점/y하향증가 좌표계에서
                // "화면 진짜 하단에서 위로 두께만큼"을 뜻한다(위 클래스 주석의 핫픽스 설명 참고).
                float netLeftOsX = overlayOrigin.x + widenedX;
                float netRightOsX = netLeftOsX + widenedWidth;
                float netTopOsY = overlayOrigin.y + height - thickness;

                // 화면 경계 접기 + 구멍 잘라내기는 전부 BottomSafetyNetPolicy 하나에 있다(플랫폼 중립
                // 순수 함수라 EditMode에서 실기 좌표를 그대로 재현해 검증할 수 있다 — 그 문서 참고).
                BottomSafetyNetPolicy.Pieces pieces = BottomSafetyNetPolicy.Resolve(
                    new Rect(netLeftOsX, netTopOsY, Mathf.Max(0f, netRightOsX - netLeftOsX), thickness),
                    hasScreenBounds, screenLeftOsX, screenRightOsX, screenBottomOsY,
                    hasDock, dockLeftOsX, dockRightOsX);

                _hasSafetyNetLeft = pieces.HasLeft;
                _hasSafetyNetRight = pieces.HasRight;
                _safetyNetLeft = new PlatformFoothold(SyntheticFootholdHandle, pieces.Left, isTopmost: true);
                _safetyNetRight = new PlatformFoothold(SyntheticFootholdHandleRight, pieces.Right, isTopmost: true);
            }

            if (_hasSafetyNetLeft) target.Add(_safetyNetLeft);
            if (_hasSafetyNetRight) target.Add(_safetyNetRight);
        }

        /// <summary>
        /// ★ 2026-09-01 — OS가 확언하는 <b>화면 경계</b>(모니터 좌/우/하단). 안전망을 이 안으로 접어
        /// "화면 밖 + 막대 뒤" 조각이 생기는 것을 막는다(<see cref="BottomSafetyNetPolicy"/> 문서).
        ///
        /// <para>출처는 하단 예약 막대 사각형 하나다. Win32 구현이 그것을
        /// <c>Rect(rcMonitor.Left, rcWork.Bottom, rcMonitor.Right − rcMonitor.Left,
        /// rcMonitor.Bottom − rcWork.Bottom)</c>로 만들기 때문에, 그 사각형의
        /// <c>xMin/xMax/yMax</c>가 곧 <c>rcMonitor</c>의 좌/우/하단이다. 즉 새 네이티브 호출을 만들지
        /// 않고도 <b>작업표시줄과 완전히 같은 관측</b>에서 화면 경계를 얻는다 — 출처를 하나로 모으는
        /// 것이 이번 수정의 전부이므로, 여기서 다른 API를 하나 더 부르면 고치려던 문제를 다시 만든다.</para>
        ///
        /// <para>막대가 없으면(자동 숨김 / 좌·우·상단 배치) false다. 그때는 접지 않는다 — 접을 근거가
        /// 없는데 지어내면 그게 새 버그다. <b>macOS는 <see cref="IReservedBottomBarService"/>를 구현하지
        /// 않으므로 언제나 이 경로</b>이고, 따라서 macOS 동작은 이 라운드에서 한 글자도 바뀌지 않는다.
        /// (막대가 없어도 화면 경계를 알고 싶다면 모니터 사각형 전용 캐퍼빌리티가 따로 있어야 한다 —
        /// 이번 신고 경로는 막대가 <b>있는</b> 상태였으므로 그 확장은 별도 배정으로 남긴다.)</para>
        /// </summary>
        private bool TryGetScreenEdgesOsScreen(out float leftOsX, out float rightOsX, out float bottomOsY)
        {
            leftOsX = 0f;
            rightOsX = 0f;
            bottomOsY = 0f;
            if (_innerBottomBar == null) return false;
            if (!_innerBottomBar.TryGetReservedBottomBarOsScreen(out Rect barRect)) return false;
            if (barRect.width <= 0f || barRect.height <= 0f) return false;

            leftOsX = barRect.xMin;
            rightOsX = barRect.xMax;
            bottomOsY = barRect.yMax;
            return true;
        }

        public bool CreateOverlayWindow() => _inner.CreateOverlayWindow();

        public void SetClickThrough(bool enabled) => _inner.SetClickThrough(enabled);

        public void SetAlwaysOnTop(bool enabled) => _inner.SetAlwaysOnTop(enabled);

        /// <summary>
        /// ============================================================================
        /// ★★ 2026-09-02 — 여기가 <b>원장의 17%</b>가 새던 구멍이다
        /// ============================================================================
        /// <c>FootholdPoller</c>는 발판 열거를 스톱워치로 감싸고 스스로를 "네이티브 창 열거가
        /// 일어나는 <b>유일한</b> 지점"이라 단언했지만 거짓이었다. 이 호출도 네이티브 창 목록을
        /// <b>따로</b> 조회한다 — macOS는 <c>CGWindowListCopyWindowInfo</c>, Windows는 전경 창
        /// 조회 + 프로세스 이미지 경로 조회다. 주기는 <c>StickConfig.fullscreenPollInterval</c>
        /// 1.5초(초당 0.67회)이고, 발판 폴링 0.3초(초당 3.33회)와 합치면
        /// <b>초당 4회 중 0.67회 = 17%가 계측 밖</b>이었다.
        ///
        /// <para>그 결과가 무엇이었나: 이 호출이 200ms 블로킹되면 같은 프레임의 장부가
        /// <c>창열거 0.0ms/0회</c> + <c>기타로직 200ms</c>로 찍힌다 — <b>원인이 창 목록 조회인데
        /// 원장이 "아니다"라고 말한다.</b></para>
        ///
        /// <para><b>왜 여기(데코레이터)인가.</b> 이 클래스는 <c>MacWindowService</c>와
        /// <c>Win32WindowService</c>를 <b>둘 다</b> 감싼다(<c>StickmanAgent.CreatePlatformService()</c>).
        /// 계측을 플랫폼 구현체 안에 넣으면 한쪽만 고쳐지고 반대쪽은 이 개발 머신에서 영원히
        /// 검증되지 않는다 — <c>FullscreenSuspendPolicy.cs</c>로 이미 겪은 사고다. 감싸지 않는
        /// 서비스(Null/모바일)는 <c>IsFullscreenAppActive()</c>가 상수 <c>false</c>라 잴 것이 없다.</para>
        ///
        /// <para>비용: 판정당(1.5초) <c>Stopwatch.GetTimestamp()</c> 2회. <b>할당 0</b>, 동작 무변화.</para>
        /// </summary>
        public bool IsFullscreenAppActive()
        {
            // System.Diagnostics를 using으로 열지 않는다 — 이 파일의 Debug.Log가 UnityEngine.Debug와
            // System.Diagnostics.Debug 사이에서 모호해진다(CS0104).
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool active = _inner.IsFullscreenAppActive();
            StallAttribution.RecordFullscreenProbe(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            return active;
        }

        /// <summary>
        /// 등급 조회도 <b>같은 원장 칸</b>에 잰다 — 실제 폴링 경로가 이쪽으로 옮겨가므로, 여기서
        /// 재지 않으면 <c>[스톨구간]</c>의 전체화면 판정 비용이 <b>0으로 보고되는</b> 거짓 원장이 된다
        /// (이 클래스 문서가 경고하는 그 사고 그대로다).
        /// </summary>
        public ForeignFullscreenTier GetForeignFullscreenTier()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            ForeignFullscreenTier tier = _innerTier != null
                ? _innerTier.GetForeignFullscreenTier()
                // 강등 경로: 등급을 모르는 내부 서비스는 예전 계약(bool) 그대로 해석한다.
                : (_inner.IsFullscreenAppActive() ? ForeignFullscreenTier.Full : ForeignFullscreenTier.None);
            StallAttribution.RecordFullscreenProbe(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            return tier;
        }

        // IGlobalPointerButtonService — 위 _innerButton 선언부의 사고 기록 참고. 순수 통과.
        public bool TryGetPrimaryButtonPressed(out bool pressed)
        {
            if (_innerButton != null) return _innerButton.TryGetPrimaryButtonPressed(out pressed);
            pressed = false;
            return false;
        }

        public bool TryGetSecondaryButtonPressed(out bool pressed)
        {
            if (_innerButton != null) return _innerButton.TryGetSecondaryButtonPressed(out pressed);
            pressed = false;
            return false;
        }

        // IGlobalKeyStateService — 전역 단축키(앱 제어) 조회. 위 두 채널과 동일한 순수 위임 패턴.
        public bool TryGetKeyPressed(GlobalKey key, out bool pressed)
        {
            if (_innerKeyState != null) return _innerKeyState.TryGetKeyPressed(key, out pressed);
            pressed = false;
            return false;
        }

        public bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition)
        {
            if (_innerCursor != null) return _innerCursor.TryGetGlobalCursorPosition(out osScreenPosition);
            osScreenPosition = default;
            return false;
        }

        // ILocalClickCaptureService(UX_FLOW.md 15절) — 발판 열거와 달리 이 데코레이터는 부분적
        // 클릭관통 해제 자체에는 아무 로직도 얹지 않고 내부 서비스가 지원하면 그대로 통과시킨다
        // (ICursorPositionService와 동일한 delegate 패턴). 내부 서비스가 지원하지 않으면(이론상 발생
        // 안 함 — Win32/Null 둘 다 구현) 항상 실패로 안전하게 처리한다.
        public bool RequestLocalClickCapture(Rect hitboxOsScreen, object owner)
            => _innerClickCapture != null && _innerClickCapture.RequestLocalClickCapture(hitboxOsScreen, owner);

        public void UpdateLocalClickCaptureRegion(Rect hitboxOsScreen, object owner)
            => _innerClickCapture?.UpdateLocalClickCaptureRegion(hitboxOsScreen, owner);

        public void ReleaseLocalClickCapture(object owner)
            => _innerClickCapture?.ReleaseLocalClickCapture(owner);

        public bool IsLocalClickCaptureOwnedBy(object owner)
            => _innerClickCapture != null && _innerClickCapture.IsLocalClickCaptureOwnedBy(owner);

        // IDesktopIconLayoutService(UX_FLOW.md 27-2/27-5절) — 발판 열거와 달리 이 데코레이터는 아이콘
        // 좌표 조회 자체에는 아무 로직도 얹지 않고 내부 서비스가 지원하면 그대로 통과시킨다
        // (ICursorPositionService/ILocalClickCaptureService와 동일한 delegate 패턴). 내부 서비스가
        // 지원하지 않으면(현재 Win32WindowService가 정직하게 미구현 상태) 항상 조회 실패로 안전하게 처리한다.
        public bool TryGetIconRegion(out Rect osScreenRegion)
        {
            if (_innerIconLayout != null) return _innerIconLayout.TryGetIconRegion(out osScreenRegion);
            osScreenRegion = default;
            return false;
        }

        public IReadOnlyList<Rect> EnumerateIconRects()
            => _innerIconLayout != null ? _innerIconLayout.EnumerateIconRects() : System.Array.Empty<Rect>();

        // IReservedBottomBarService — 순수 통과(ICursorPositionService와 동일한 위임 패턴).
        // 이 데코레이터 자신은 발판/안전망 계산에서 위 TryGetDockRectOsScreen을 통해서만 이 값을 쓰고,
        // 여기 통과 경로는 UI 배치(구석 패널의 하단 막대 회피 등) 소비자를 위한 것이다.
        public bool TryGetReservedBottomBarOsScreen(out Rect osScreenRect)
        {
            if (_innerBottomBar != null) return _innerBottomBar.TryGetReservedBottomBarOsScreen(out osScreenRect);
            osScreenRect = default;
            return false;
        }

        // IReservedTopBarService — 순수 통과. 이 데코레이터는 이 값을 **소비하지 않는다**(합성 발판은
        // 화면 하단에만 놓는다). 위 _innerTopBar 선언부에 "왜 이번 것은 사용자 결함이 아니었는가"와
        // "macOS에서 여기가 항상 false인 것이 왜 정상인가"가 적혀 있다.
        public bool TryGetReservedTopInsetPoints(out float insetPoints)
        {
            if (_innerTopBar != null) return _innerTopBar.TryGetReservedTopInsetPoints(out insetPoints);
            insetPoints = 0f;
            return false;
        }
    }
}
