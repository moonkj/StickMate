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
    /// 재발했다. 수정: 내부 서비스가 무엇을 반환하든 상관없이 "화면 하단 가로 전체 폭" 안전망을 매번
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
    /// 의도적으로 감싸지 않는 대상: ScreenshotBackdropPlatformService(모바일)는 이 데코레이터로 감싸지
    /// 않는다 — 그 서비스의 "발판 0개" 반환은 버그가 아니라 "유저가 아직 발판을 탭 지정하지 않음"이라는
    /// 의도된 신호이고, 상위 온보딩 코드가 IsConfigured로 이 상태를 감지해 탭 지정 흐름을 노출해야
    /// 한다(UX_FLOW.md 3절/9절-7). 여기서 항상 발판이 있는 것처럼 위장하면 그 온보딩 게이트가 조용히
    /// 무력화된다 — 배선은 StickmanAgent.CreatePlatformService() 참고.
    /// </summary>
    public sealed class FallbackPlatformWindowService : IPlatformWindowService, ICursorPositionService, ILocalClickCaptureService, IDesktopIconLayoutService
    {
        private readonly IPlatformWindowService _inner;
        private readonly ICursorPositionService _innerCursor; // null이면 내부 서비스가 커서 조회를 지원하지 않음
        private readonly ILocalClickCaptureService _innerClickCapture; // null이면 내부 서비스가 부분적 클릭관통 해제를 지원하지 않음
        private readonly IDesktopIconLayoutService _innerIconLayout; // null이면 내부 서비스가 아이콘 좌표 조회를 지원하지 않음
        private readonly StickConfig _config; // desktopDpiScale만 읽는다 — null이면 배율 1로 취급.

        // 재사용 버퍼. "실제 발판 전부 + 안전망 1개"를 매 호출 다시 채워 넣지만 리스트 자체는 재할당하지
        // 않는다(24시간 상주 앱, GC 압박 방지 컨벤션 — FootholdPoller.cs와 동일 원칙). EnumerateFootholds()가
        // 매 프레임이 아니라 FootholdPoller의 폴링 주기에서만 호출되므로 Add() 비용은 무시 가능하다.
        private readonly List<PlatformFoothold> _combined = new List<PlatformFoothold>(8);
        private PlatformFoothold _fallbackFoothold;
        private float _cachedScreenWidth = -1f;
        private float _cachedScreenHeight = -1f;

        public FallbackPlatformWindowService(IPlatformWindowService inner, StickConfig config = null)
        {
            _inner = inner;
            _innerCursor = inner as ICursorPositionService;
            _innerClickCapture = inner as ILocalClickCaptureService;
            _innerIconLayout = inner as IDesktopIconLayoutService;
            _config = config;
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            IReadOnlyList<PlatformFoothold> real = _inner.EnumerateFootholds();

            _combined.Clear();
            if (real != null)
            {
                for (int i = 0; i < real.Count; i++) _combined.Add(real[i]);
            }
            _combined.Add(GetFallbackFoothold()); // 항상 마지막에 추가 — 실제 발판이 우선 채택되도록.
            return _combined;
        }

        private PlatformFoothold GetFallbackFoothold()
        {
            float dpi = _config != null ? Mathf.Max(0.0001f, _config.desktopDpiScale) : 1f;
            float width = (Screen.width > 0 ? Screen.width : 1920f) * dpi;
            float height = (Screen.height > 0 ? Screen.height : 1080f) * dpi;

            if (!Mathf.Approximately(width, _cachedScreenWidth) || !Mathf.Approximately(height, _cachedScreenHeight))
            {
                _cachedScreenWidth = width;
                _cachedScreenHeight = height;

                // BUG-P1-R5-B2 대응(Coder 발견/수정, 2026-08-28) — "바로 바탕화면에서 구동" 라운드가 처음
                // 만든 실제 Standalone .app을 직접 실행해 Player.log에 캐릭터 위치를 초 단위로 남기는
                // 임시 디버그 로그로 확인한 결과, 화면에 실제 OS 창이 하나도 안 보이는 흔한 상황(이번
                // 검증 환경 포함)에서 캐릭터가 FallState에 영원히 갇혀(footholds=1이지만 grounded=False)
                // 좌우로 전혀 움직이지 않는 것을 실측으로 재현했다.
                //
                // 근본 원인: 이 안전망 발판을 예전에는 고정 픽셀 두께(40f)로 "화면의 진짜 맨 아래"에
                // 뒀는데, Editor/SceneBootstrapper.cs가 캐릭터 스폰/RAGDOLL 안전 바닥 Y를 계산할 때
                // 기준으로 삼는 값은 그게 아니라 NullPlatformWindowService.DummyFootholdHeightFraction
                // (화면 하단에서 위로 20%)이다 — 즉 이 안전망(예전: 화면 맨 아래 40px)과 씬이 실제로
                // 캐릭터를 놓는 높이(화면 하단에서 위로 20% 지점)가 서로 다른 Y를 가정하고 있었다.
                // 에디터/배치모드 테스트는 전부 NullPlatformWindowService만 쓰므로(!UNITY_EDITOR 가드,
                // 위 클래스 문서 참고) 이 불일치가 지금까지 어떤 EditMode/PlayMode 테스트에도 걸리지
                // 않고 숨어 있었다 — 실제 macOS Standalone 빌드를 실제로 실행해봐야만(에디터가 아닌
                // 진짜 .app) 드러나는 종류의 버그였다.
                //
                // 수정: 이 안전망의 두께도 NullPlatformWindowService.DummyFootholdHeightFraction과
                // 정확히 같은 비율로 맞춘다(그 클래스가 이미 "Editor/SceneBootstrapper.cs와 단일 소스로
                // 공유해야 어긋나지 않는다"고 명시한 것과 동일한 원칙을 이 실제 플랫폼 안전망에도 적용).
                // 이러면 화면에 실제 창이 하나도 안 보이는 상황에서도, 이 안전망의 논리적 발판 Y가
                // 씬의 물리적 RAGDOLL 안전 바닥/캐릭터 스폰 Y와 정확히 일치해 캐릭터가 정상적으로
                // Grounded==true를 얻고 Idle/Walk를 오갈 수 있다. 실제 OS 창이 보이면(정상적인 사용
                // 시나리오) 그 창이 먼저 매치되므로(EnumerateFootholds()가 항상 안전망을 "끝에" 추가하는
                // 정책, 위 클래스 문서 참고) 이 변경은 그 경우에 아무 영향이 없다.
                float thickness = height * NullPlatformWindowService.DummyFootholdHeightFraction;

                // BUG-P1-R5-B3 대응(Coder 실측 발견, 2026-08-28) — 위 BUG-P1-R5-B2로 "낙하 고착이 t=0부터
                // 영원히 지속"되는 문제는 해소됐지만, 실제 Standalone .app을 60초 이상 계속 관찰해보니
                // (Architect 지시로 재검증) 한참 걸어다니다 이 안전망 발판의 가장자리 밖으로 나가면서
                // 다시 낙하 고착에 빠지는(수십 초 뒤 재발) 별도 사례를 발견했다. 원인: 이 안전망은 지금까지
                // 폭을 `Screen.width` 그대로(=카메라 뷰포트 폭 그대로) 써왔는데, 그 절반폭(예:
                // orthographicSize=5, 16:10 화면 기준 약 8유닛)은 `AutoWanderController`의 한 Walk 페이즈
                // 최대 이동거리(walkSpeed×wanderWalkDurationMax×지터, 기본값 기준 약 11.75유닛)보다 좁다
                // — `NullPlatformWindowService`(에디터/배치모드 전용)의 더미 발판은 정확히 이 문제 때문에
                // 이미 `DummyFootholdWidthMultiplier`(4배)로 폭을 넓혀뒀는데, 그 넓히기가 실제 macOS/
                // Windows 배포 환경이 쓰는 이 안전망에는 한 번도 이식되지 않았었다 — 그래서 에디터
                // 테스트는 이 가장자리에 거의 닿지 않지만(4배 넓은 관찰 범위), 실제 배포판은 정상적인
                // 배회만으로도 수십 초 안에 가장자리에 닿을 수 있었다. 수정: 동일한 배율을 여기도 적용해
                // 화면 중심(world x=0)을 기준으로 좌우 대칭으로 폭을 넓힌다 — `NullPlatformWindowService`
                // 생성자의 동일 계산식을 그대로 재사용(단일 소스 공유, 어긋남 재발 방지).
                float widenedWidth = width * NullPlatformWindowService.DummyFootholdWidthMultiplier;
                float widenedX = (width - widenedWidth) / 2f;

                // y = height - 두께: ScreenCoordinateConverter와 동일한 좌상단원점/y하향증가 좌표계에서
                // "화면 진짜 하단에서 위로 두께만큼"을 뜻한다(위 클래스 주석의 핫픽스 설명 참고).
                var rect = new Rect(widenedX, height - thickness, widenedWidth, thickness);
                _fallbackFoothold = new PlatformFoothold(handle: -1L, screenRect: rect, isTopmost: true);
            }
            return _fallbackFoothold;
        }

        public bool CreateOverlayWindow() => _inner.CreateOverlayWindow();

        public void SetClickThrough(bool enabled) => _inner.SetClickThrough(enabled);

        public void SetAlwaysOnTop(bool enabled) => _inner.SetAlwaysOnTop(enabled);

        public bool IsFullscreenAppActive() => _inner.IsFullscreenAppActive();

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
