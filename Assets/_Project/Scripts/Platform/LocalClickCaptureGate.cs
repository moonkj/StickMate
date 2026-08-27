using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 부분적 클릭관통 해제(Partial Click-Through Override, docs/UX_FLOW.md 15절)의 단일 소유자 락 +
    /// 동적 히트박스 영역 부기(bookkeeping)를 구현하는 공용 헬퍼. Win32WindowService /
    /// NullPlatformWindowService / FallbackPlatformWindowService가 각자 이 클래스의 인스턴스 하나씩을
    /// 갖고 ILocalClickCaptureService의 4개 메서드를 이 헬퍼에 위임한다 — 락/영역 갱신 로직을 여러
    /// 구현체에 중복시키지 않기 위함(FootholdPoller/ScreenCoordinateConverter류 "공용 유틸은 한 곳에"
    /// 컨벤션과 동일).
    ///
    /// 이 클래스가 다루는 범위는 정확히 "누가 지금 이 자원을 쥐고 있는가"와 "그 소유자가 지정한 히트박스
    /// 영역이 지금 무엇인가"라는 순수 부기뿐이다. 실제 OS 레벨에서 그 영역 밖 클릭만 관통시키고 영역 안
    /// 클릭만 앱으로 전달하는 진짜 히트테스트는 여기 없다 — ILocalClickCaptureService.cs 문서 상단의
    /// "핵심 한계"를 반드시 함께 읽을 것.
    /// </summary>
    public sealed class LocalClickCaptureGate
    {
        private object _owner;
        private Rect _hitboxOsScreen;

        public bool HasOwner => _owner != null;
        public object CurrentOwner => _owner;
        public Rect CurrentHitboxOsScreen => _hitboxOsScreen;

        /// <summary>이미 다른 소유자가 점유 중이면 false(15절 제약 4: 단일 소유자 락). 같은 owner가
        /// 다시 요청하면(재진입) 영역만 갱신하고 true를 반환한다.</summary>
        public bool TryRequestCapture(Rect hitboxOsScreen, object owner)
        {
            if (owner == null) return false;
            if (_owner != null && _owner != owner) return false;
            _owner = owner;
            _hitboxOsScreen = hitboxOsScreen;
            return true;
        }

        /// <summary>동적 히트박스 추적(15절 제약 1) — 소유자가 매 프레임 최신 좌표로 갱신한다. 소유자가
        /// 아니면 no-op(다른 이벤트가 몰래 남의 영역을 바꿔치기할 수 없다).</summary>
        public void UpdateRegion(Rect hitboxOsScreen, object owner)
        {
            if (_owner == null || _owner != owner) return;
            _hitboxOsScreen = hitboxOsScreen;
        }

        /// <summary>소유자 본인만 해제할 수 있다. 이미 해제됐거나 소유자가 아니면 no-op(안전한 중복 호출 허용).</summary>
        public void ReleaseCapture(object owner)
        {
            if (_owner == null || _owner != owner) return;
            _owner = null;
        }

        public bool IsOwnedBy(object owner) => owner != null && _owner == owner;
    }
}
