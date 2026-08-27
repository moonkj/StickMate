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
    public sealed class FallbackPlatformWindowService : IPlatformWindowService, ICursorPositionService
    {
        private const float FallbackFootholdHeight = 40f;

        private readonly IPlatformWindowService _inner;
        private readonly ICursorPositionService _innerCursor; // null이면 내부 서비스가 커서 조회를 지원하지 않음
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
                // y = height - 두께: ScreenCoordinateConverter와 동일한 좌상단원점/y하향증가 좌표계에서
                // "화면 진짜 하단 근처"를 뜻한다(위 클래스 주석의 핫픽스 설명 참고).
                var rect = new Rect(0f, height - FallbackFootholdHeight, width, FallbackFootholdHeight);
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
    }
}
