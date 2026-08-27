using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// IPlatformWindowService 데코레이터 — BUG-P1-B1(Blocker, docs/BUG_REPORT_PHASE1.md) 대응.
    ///
    /// 문제: 내부(실제) 서비스의 EnumerateFootholds()가 빈 리스트를 반환하면(예: Win32WindowService가
    /// "제목 있는 가시 창"을 하나도 못 찾음 — 유저가 모든 창을 최소화한 흔한 상황) GroundSensor/
    /// GroundedTick/CheckScreenBoundsOrFall이 전부 무력화되어 캐릭터가 화면 밖으로 영원히 낙하한다.
    /// NullPlatformWindowService는 생성자에서 "화면 하단 더미 발판 1개"를 항상 반환하도록 만들어져
    /// 이 문제가 없지만, 그 안전망이 에디터/미지원 플랫폼 전용으로만 존재하고 실제 데스크톱 구현체에는
    /// 이식되어 있지 않았다.
    ///
    /// 해결: 이 데코레이터가 내부 서비스를 감싸 EnumerateFootholds()가 0개를 반환하는 매 순간마다
    /// "화면 하단 가로 전체 폭"의 합성 발판 1개로 대체 반환한다 — NullPlatformWindowService와 동일한
    /// 개념을 공용 유틸로 이식한 것. 나머지 메서드(CreateOverlayWindow/SetClickThrough/SetAlwaysOnTop/
    /// IsFullscreenAppActive)는 그대로 내부 서비스에 위임한다(이 데코레이터는 발판 열거에만 관여).
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

        // 재사용 버퍼 1칸짜리 리스트. 화면 폭이 바뀌면(창 크기 변경 등) 내용만 갱신하고 리스트 자체는
        // 새로 할당하지 않는다(24시간 상주 앱, GC 압박 방지 컨벤션 — FootholdPoller.cs와 동일 원칙).
        private readonly List<PlatformFoothold> _fallbackFootholds = new List<PlatformFoothold>(1);
        private float _cachedScreenWidth = -1f;

        public FallbackPlatformWindowService(IPlatformWindowService inner)
        {
            _inner = inner;
            _innerCursor = inner as ICursorPositionService;
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
        {
            IReadOnlyList<PlatformFoothold> real = _inner.EnumerateFootholds();
            if (real != null && real.Count > 0) return real;
            return GetFallbackFootholds();
        }

        private IReadOnlyList<PlatformFoothold> GetFallbackFootholds()
        {
            float width = Screen.width > 0 ? Screen.width : 1920f;
            if (_fallbackFootholds.Count == 0 || !Mathf.Approximately(width, _cachedScreenWidth))
            {
                _cachedScreenWidth = width;
                var rect = new Rect(0f, 0f, width, FallbackFootholdHeight);
                var foothold = new PlatformFoothold(handle: -1L, screenRect: rect, isTopmost: true);
                if (_fallbackFootholds.Count == 0) _fallbackFootholds.Add(foothold);
                else _fallbackFootholds[0] = foothold;
            }
            return _fallbackFootholds;
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
