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
    public sealed class FallbackPlatformWindowService : IPlatformWindowService, ICursorPositionService, ILocalClickCaptureService, IDesktopIconLayoutService, IGlobalPointerButtonService, IGlobalKeyStateService
    {
        private readonly IPlatformWindowService _inner;
        private readonly ICursorPositionService _innerCursor; // null이면 내부 서비스가 커서 조회를 지원하지 않음
        private readonly ILocalClickCaptureService _innerClickCapture; // null이면 내부 서비스가 부분적 클릭관통 해제를 지원하지 않음
        private readonly IDesktopIconLayoutService _innerIconLayout; // null이면 내부 서비스가 아이콘 좌표 조회를 지원하지 않음

        // ★ 사용자 신고 "마우스로 안 잡힘" 조사 중 함께 발견(2026-08-28): 이 데코레이터가
        // IGlobalPointerButtonService를 통과시키지 않아, StickmanClickHitbox의
        // `PlatformService as IGlobalPointerButtonService` 캐스팅이 **항상 null**이었다
        // (실측 로그: "전역버튼경로=미지원"). 그래서 창 포커스와 무관한 전역 버튼 폴링 경로가
        // 실제로는 한 번도 활성화된 적이 없었다. ICursorPositionService와 동일한 위임 패턴으로 통과시킨다.
        private readonly IGlobalPointerButtonService _innerButton; // null이면 내부 서비스가 전역 버튼 조회를 지원하지 않음
        private readonly IGlobalKeyStateService _innerKeyState;     // null이면 내부 서비스가 전역 키 조회를 지원하지 않음
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

        public FallbackPlatformWindowService(IPlatformWindowService inner, StickConfig config = null)
        {
            _inner = inner;
            _innerCursor = inner as ICursorPositionService;
            _innerClickCapture = inner as ILocalClickCaptureService;
            _innerIconLayout = inner as IDesktopIconLayoutService;
            _innerButton = inner as IGlobalPointerButtonService;
            _innerKeyState = inner as IGlobalKeyStateService;
            _config = config;
        }

        /// <summary>
        /// 감싸고 있는 실제 플랫폼 서비스. 진단(플랫폼별 부가 정보 조회 — 예: macOS의 창 소유 앱 이름)
        /// 목적으로만 노출한다. 게임플레이 코드는 절대 이 프로퍼티로 구체 타입에 의존하지 말고
        /// IPlatformWindowService 계약만 사용할 것(아키텍처 2절 플랫폼 추상화 원칙).
        /// </summary>
        public IPlatformWindowService Inner => _inner;

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
        /// ============================================================================
        /// 왜 CGWindowListCopyWindowInfo의 Dock 창 사각형을 그대로 쓰지 못하는가 (실측 조사 결과)
        /// ============================================================================
        /// 리더 지시는 "Dock은 Dock 프로세스가 소유한 창으로 열거되니 그 실제 사각형을 쓰라"였다.
        /// 그래서 이 환경에서 직접 열거해봤다(2026-08-28, CGWindowListCopyWindowInfo 전수 덤프):
        ///
        ///     owner='Dock'  name='Dock'        layer=20   alpha=1.0  rect=(0, 0, 1512, 982)
        ///     owner='Dock'  name='Wallpaper-'  layer=-2147483624     rect=(0, 0, 1512, 982)
        ///
        /// **Dock 창의 bounds는 Dock 막대가 아니라 화면 전체다.** macOS의 Dock 프로세스는 화면 전체를
        /// 덮는 투명 레이어 하나를 갖고 그 안에 막대를 그리며(Launchpad/Mission Control도 같은 창을
        /// 쓴다), 실제로 보이는 막대의 사각형은 공개 API로 노출되지 않는다. 그대로 발판으로 쓰면 화면
        /// 전체 폭 발판이 화면 **맨 위**(y=0)에 생겨 지금보다 훨씬 나빠진다.
        ///
        /// 다른 경로도 전부 확인했고 전부 막혔다:
        ///   • com.apple.dock 환경설정: tilesize/persistent-apps는 있지만 **실행 중인 앱 타일 수**를
        ///     알 수 없어 폭을 계산할 수 없다(실측: 예측 가능한 타일 17개로는 실제 폭 1069pt가 나오지
        ///     않는다 — 실행 중 앱들이 더 붙어 있었다).
        ///   • CGWindowListCreateImage로 Dock 창만 캡처해 알파 경계를 재면 **정확히** 나온다(실측:
        ///     x 221~1290, 폭 1069pt, 화면 가로 정중앙 정렬, 두께 68pt). 하지만 이 API는 macOS 10.15+
        ///     에서 **화면 기록 권한**을 요구하고 권한 요청 팝업을 띄운다 — 비침해 원칙(CLAUDE.md 2)과
        ///     "권한 없이 동작"이라는 이 프로젝트의 플랫폼 계약에 정면으로 어긋나 채택하지 않았다.
        ///
        /// 그래서 **정확히 알 수 있는 것만 실측값으로 쓰고, 알 수 없는 폭만 설정값**으로 뺀다:
        ///   • **세로(정확)**: Dock 띠 두께는 StickConfig.dockFootholdThicknessPoints. 상단 =
        ///     화면 바닥 - 두께. (Dock 자동 숨김을 쓰면 두께를 0으로 두면 이 발판이 사라진다.)
        ///   • **가로(추정)**: 화면 가로 정중앙 정렬 + StickConfig.dockFootholdWidthFraction 폭.
        ///     기본값 0.65는 위 실측(1069/1512 = 0.707)보다 **일부러 좁게** 잡았다 — 추정이 실제보다
        ///     넓으면 Dock이 없는 자리에 캐릭터가 서서 사용자가 신고한 그 "공중 부양"이 재발하지만,
        ///     좁으면 실제 Dock 안쪽에서 조금 일찍 떨어질 뿐이라 눈에 거슬리지 않는다. 틀리는 방향을
        ///     안전한 쪽으로 고정한 것이다.
        ///   • 0을 주면 Dock 발판 자체가 비활성화되고 전부 바닥 안전망으로 떨어진다.
        /// </summary>
        public bool TryGetDockFoothold(out PlatformFoothold dock)
        {
            dock = default;
            if (!TryGetDockSpanOsScreen(out float dockLeftOsX, out float dockRightOsX)) return false;

            float dpi = Mathf.Max(0.0001f, _config.desktopDpiScale);
            float screenH = (Screen.height > 0 ? Screen.height : 1080f) * dpi;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;

            float thickness = _config.dockFootholdThicknessPoints;
            float dockTopY = origin.y + screenH - thickness;

            dock = new PlatformFoothold(DockFootholdHandle,
                new Rect(dockLeftOsX, dockTopY, dockRightOsX - dockLeftOsX, thickness), true);
            return true;
        }

        /// <summary>
        /// ★★ Dock 가로 구간의 **단일 소스**(2026-08-29, 사용자 신고 "처음엔 독위에서 잘다니다가 좀
        /// 다니다 보면 다시 독과 겹쳐서 걸음").
        ///
        /// 이 메서드 하나가 두 곳을 동시에 파생시킨다:
        ///   (a) <see cref="TryGetDockFoothold"/> — Dock 위에 서는 발판 사각형의 좌/우 끝.
        ///   (b) <see cref="AppendBottomSafetyNet"/> — 화면 최하단 안전망에서 **잘라낼 구멍**의 좌/우 끝.
        /// 즉 "안전망의 구멍"과 "Dock 발판"은 정의상 **정확히 같은 X 구간**이라, 둘이 어긋나 틈(발판이
        /// 하나도 없는 X 구간 -> 낙하 고착)이나 겹침(Dock 아래를 걸어다님 -> 이번 버그)이 생기는 것이
        /// 구조적으로 불가능하다. 리더 지시 2항: "Dock 발판 생성과 안전망 분할이 각각 다른 값을 쓰면
        /// 틈이 생기거나 겹친다. 상수 하나에서 둘 다 파생되게 해라."
        /// (이 프로젝트는 과거 두 곳이 따로 계산해 어긋난 버그가 2회 있었다 — BUG-P1-R4-B1, BUG-P1-R5-B2.)
        ///
        /// 반환하는 좌표는 PlatformFoothold.ScreenRect와 동일한 공간(오버레이 창 원점 기준 OS 좌표)이다.
        /// Dock이 비활성(폭 비율 0 또는 두께 0)이거나 설정이 없으면 false — 그때 안전망은 예전처럼
        /// 화면 전체 폭 한 조각으로 남는다(잘라낼 Dock 자체가 없으므로 겹칠 일도 없다).
        /// </summary>
        public bool TryGetDockSpanOsScreen(out float dockLeftOsX, out float dockRightOsX)
        {
            dockLeftOsX = 0f;
            dockRightOsX = 0f;
            if (_config == null) return false;

            float widthFraction = _config.dockFootholdWidthFraction;
            float thickness = _config.dockFootholdThicknessPoints;
            if (widthFraction <= 0f || thickness <= 0f) return false;

            float dpi = Mathf.Max(0.0001f, _config.desktopDpiScale);
            float screenW = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;

            float dockWidth = screenW * Mathf.Clamp01(widthFraction);
            dockLeftOsX = origin.x + (screenW - dockWidth) * 0.5f;   // 화면 가로 정중앙(실측 확인).
            dockRightOsX = dockLeftOsX + dockWidth;
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
            float dpi = _config != null ? Mathf.Max(0.0001f, _config.desktopDpiScale) : 1f;
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

            if (!Mathf.Approximately(width, _cachedScreenWidth) || !Mathf.Approximately(height, _cachedScreenHeight)
                || !Mathf.Approximately(overlayOrigin.x, _cachedOverlayOrigin.x) || !Mathf.Approximately(overlayOrigin.y, _cachedOverlayOrigin.y)
                || hasDock != _cachedHasDock
                || !Mathf.Approximately(dockLeftOsX, _cachedDockLeftOsX) || !Mathf.Approximately(dockRightOsX, _cachedDockRightOsX))
            {
                _cachedScreenWidth = width;
                _cachedScreenHeight = height;
                _cachedOverlayOrigin = overlayOrigin;
                _cachedHasDock = hasDock;
                _cachedDockLeftOsX = dockLeftOsX;
                _cachedDockRightOsX = dockRightOsX;

                // BUG-P1-R5-B2 대응(Coder 발견/수정, 2026-08-28) — "바로 바탕화면에서 구동" 라운드가 처음
                // 만든 실제 Standalone .app을 직접 실행해 Player.log에 캐릭터 위치를 초 단위로 남기는
                // 임시 디버그 로그로 확인한 결과, 화면에 실제 OS 창이 하나도 안 보이는 흔한 상황(이번
                // 검증 환경 포함)에서 캐릭터가 FallState에 영원히 갇혀(footholds=1이지만 grounded=False)
                // 좌우로 전혀 움직이지 않는 것을 실측으로 재현했다. 근본 원인은 이 안전망의 두께가
                // Editor/SceneBootstrapper.cs가 씬에 굽는 지면 Y의 기준
                // (NullPlatformWindowService.DummyFootholdHeightFraction)과 서로 다른 값이었던 것이다.
                // 수정: 그 공개 상수를 그대로 참조해 **단일 소스**로 묶는다(그 선언부 문서에 유도 과정).
                // ★ 2026-08-28: 그 상수가 화면 높이의 20% -> 화면 최하단 40pt(BottomSafetyNetInsetPoints)로
                // 내려갔다. 여기 계산식은 한 글자도 바뀌지 않지만 결과는 크게 달라진다 — 실측 1512x982
                // 화면 기준 발판 상단이 OS y=785.6(화면 중간쯤)에서 y=942(화면 최하단)로 내려간다.
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

                // Dock이 없으면 구멍의 좌우 끝을 둘 다 안전망 오른쪽 끝에 두어, 왼쪽 조각이 전체 폭을
                // 차지하고 오른쪽 조각이 폭 0이 되게 한다(= 예전의 한 장짜리 안전망과 완전히 동일).
                // Clamp는 Dock이 안전망보다 넓거나 밖으로 벗어난 병리적 설정에서도 조각 폭이 음수가
                // 되지 않게 한다.
                float holeLeftOsX = hasDock ? Mathf.Clamp(dockLeftOsX, netLeftOsX, netRightOsX) : netRightOsX;
                float holeRightOsX = hasDock ? Mathf.Clamp(dockRightOsX, netLeftOsX, netRightOsX) : netRightOsX;

                float leftPieceWidth = holeLeftOsX - netLeftOsX;
                float rightPieceWidth = netRightOsX - holeRightOsX;

                _hasSafetyNetLeft = leftPieceWidth > MinSafetyNetPieceWidthOsPoints;
                _hasSafetyNetRight = rightPieceWidth > MinSafetyNetPieceWidthOsPoints;
                _safetyNetLeft = new PlatformFoothold(SyntheticFootholdHandle,
                    new Rect(netLeftOsX, netTopOsY, Mathf.Max(0f, leftPieceWidth), thickness), isTopmost: true);
                _safetyNetRight = new PlatformFoothold(SyntheticFootholdHandleRight,
                    new Rect(holeRightOsX, netTopOsY, Mathf.Max(0f, rightPieceWidth), thickness), isTopmost: true);
            }

            if (_hasSafetyNetLeft) target.Add(_safetyNetLeft);
            if (_hasSafetyNetRight) target.Add(_safetyNetRight);
        }

        /// <summary>
        /// 안전망 조각을 발판으로 인정하는 최소 폭(OS 포인트). 이보다 얇은 조각은 캐릭터가 설 수 없는
        /// 실오라기라 오히려 접지/낙하가 매 프레임 뒤집히는 채터링만 만든다. Dock이 화면 폭 전체를
        /// 차지하도록 설정한 극단적인 경우(dockFootholdWidthFraction=1)에는 두 조각 모두 사라지고
        /// Dock 발판만 남는다 — 그때는 "Dock 바깥"이라는 X 구간 자체가 없으므로 정상이다.
        /// </summary>
        private const float MinSafetyNetPieceWidthOsPoints = 1f;

        public bool CreateOverlayWindow() => _inner.CreateOverlayWindow();

        public void SetClickThrough(bool enabled) => _inner.SetClickThrough(enabled);

        public void SetAlwaysOnTop(bool enabled) => _inner.SetAlwaysOnTop(enabled);

        public bool IsFullscreenAppActive() => _inner.IsFullscreenAppActive();

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
    }
}
