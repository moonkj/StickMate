namespace StickMate.Core
{
    /// <summary>격파 미니게임/라이벌 대결/드래그&던지기/로데오 커서 — 어느 것이 이 락을 걸고 있는지.</summary>
    public enum SpectacleEventKind
    {
        BattleMinigame,
        RivalDuel,
        DragAndThrow,
        RodeoCursor,
    }

    /// <summary>
    /// docs/UX_FLOW.md 16절-15 "모든 방해성/스펙터클 이벤트는 서로 상호 배제 락이 필요하다"의 구현체.
    /// 한 번에 하나의 스펙터클/개입 이벤트만 활성화되도록 강제하는 전역 단일 소유자 락.
    /// StickmanEventBus와 같은 이유(24시간 상주 앱, 레이어 간 결합 최소화, 씬 생명주기와 무관한 정적
    /// 상태)로 정적 클래스로 구현한다.
    ///
    /// Platform.ILocalClickCaptureService/LocalClickCaptureGate의 "부분적 클릭관통 해제 단일 소유자
    /// 락"(15절-4)과는 목적이 다른 별개의 락이다 — 이 락은 "한 번에 하나의 스펙터클 이벤트만"을
    /// 강제하고, 저 락은 "한 번에 하나만 캐릭터 클릭을 가로챌 수 있음"을 강제한다. 로데오 커서(13절)는
    /// 클릭을 전혀 쓰지 않으므로(15절 대상 아님) 이 스펙터클 락만 걸면 되고, 격파 미니게임/드래그&던지기는
    /// 이 락과 저 락을 둘 다 걸어야 한다(오너 토큰은 보통 같은 object를 재사용).
    /// </summary>
    public static class SpectacleEventLock
    {
        private static object _owner;
        private static SpectacleEventKind _activeKind;

        public static bool IsActive => _owner != null;
        public static SpectacleEventKind ActiveKind => _activeKind;
        public static object CurrentOwner => _owner;

        /// <summary>이미 다른 소유자가 점유 중이면 false. 같은 owner가 다시 요청하면(재진입) true.</summary>
        public static bool TryAcquire(SpectacleEventKind kind, object owner)
        {
            if (owner == null) return false;
            if (_owner != null && _owner != owner) return false;
            _owner = owner;
            _activeKind = kind;
            return true;
        }

        /// <summary>소유자 본인만 해제할 수 있다. 이미 해제됐거나 소유자가 아니면 no-op.</summary>
        public static void Release(object owner)
        {
            if (_owner == null || _owner != owner) return;
            _owner = null;
        }
    }
}
