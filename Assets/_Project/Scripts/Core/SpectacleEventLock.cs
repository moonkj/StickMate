namespace StickMate.Core
{
    /// <summary>격파 미니게임/라이벌 대결/드래그&던지기/로데오 커서 — 어느 것이 이 락을 걸고 있는지.</summary>
    public enum SpectacleEventKind
    {
        BattleMinigame,
        RivalDuel,
        DragAndThrow,
        RodeoCursor,

        // ==== Phase 4 (docs/UX_FLOW.md 27절/28절-29) — 기존 4종과 동일한 "한 번에 하나만" 상호배제
        // 세트에 신규 편입. DesktopTidy/BlackholeSummon은 같은 전역 락을 공유하는 것만으로 27-2/27-5가
        // 요구하는 "둘 사이의 더 강한 상호배제"도 자동으로 충족된다(별도 락 불필요). ====
        WindowTheft,
        Graffiti,
        DesktopTidy,
        BlackholeSummon,
        WindowCrash,

        // ==== Phase 5 (docs/UX_FLOW.md 17~20절) — Coder 판단 기록(Tasklist.md 교차 레이어 로그에도
        // 동일 근거 기록): 이 락에 참여시킬지 여부는 "StickmanStateMachine.ChangeState()를 직접
        // 호출해 단일 상태 슬롯을 두고 경쟁하는가"로 판단했다. Interaction/HardwareReactionDirector.cs는
        // ChangeState를 전혀 호출하지 않고(현재 상태 위에 얹는 순수 오버레이 신호) 이 락에 참여하지
        // 않는 것이 승인된 선례인데, 아래 5개는 전부 ChangeState를 호출해 다른 스펙터클과 같은 단일
        // 상태 슬롯을 다툴 수 있으므로 하드웨어 반응과 달리 이 락에 참여시킨다. ====

        /// <summary>투두 '들고 다니는 모드' 리마인더(17절) — ChangeState(TodoReminder) 호출.</summary>
        TodoReminder,

        /// <summary>포모도로 감시자 시작/종료/2단계 리마인드 포즈(18절) — FocusStart/FocusComplete/
        /// FocusCancelled/FocusNudge 4개 상태가 모두 이 하나의 kind를 공유한다(서로 겹칠 일이 없는
        /// 순차적 생애주기이므로 세분화할 실익이 없다).</summary>
        FocusPose,

        /// <summary>SULKY(19절) — ChangeState(Sulky) 호출. 하드웨어 반응과 달리 실제 상태 슬롯을 쓰므로
        /// 참여시킨다(Tasklist.md 교차 레이어 로그 판단 근거 참고).</summary>
        Sulky,

        /// <summary>가출(20절) — UX_FLOW.md 25절-20이 명시적으로 요구: "가출 상태는 16절-15의 상호배제
        /// 세트(10/11/13/14절)에 포함되어야 한다." 다른 5개와 달리 수 시간 지속될 수 있어 락을 가장
        /// 오래 붙들 수 있는 항목이다.</summary>
        Runaway,
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
