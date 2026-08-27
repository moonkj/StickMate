namespace StickMate.States
{
    /// <summary>
    /// Idle/Walk/Jump 상태 전이가 소비하는 "누가 MoveInputX/JumpPressed를 채우는가"의 최소 계약.
    ///
    /// 왜 필요한가(BUG-P1-B2 대응, docs/BUG_REPORT_PHASE1.md Blocker): 이전에는 StickmanAgent.Update()가
    /// UnityEngine.Input.GetAxisRaw("Horizontal")/GetButtonDown("Jump")를 직접 읽어 StickmanBlackboard에
    /// 대입했다. 이 앱은 "아무것도 안 해도 재미있는 자율 배회 데스크톱 펫"이 P0 성공 기준이고(UX_FLOW.md
    /// 2절/8절), 실제 분리 오버레이가 완성되면 그 창은 WS_EX_NOACTIVATE라 키보드 포커스를 받을 수조차
    /// 없어(Win32WindowService.cs) 키보드 의존 이동은 구조적으로 영구 정지가 확정되는 결함이었다(가설 H6).
    ///
    /// 이 인터페이스를 도입한 이유: StickmanBlackboard/StickmanAgent가 "이동 의도가 어디서 오는가"를
    /// 전혀 몰라도 되게 만들기 위함이다. 지금은 <see cref="AutoWanderController"/>(docs/UX_FLOW.md 26절
    /// 배회 행동 스펙의 정식 구현)가 이 계약을 채우지만, 향후 대결모드(Phase 3)나 다른 소스로 구현체만
    /// 교체하면 된다 — 26-5절에서 키보드는 대결모드에서도 부활시키지 않기로 확정됐으므로, 이 프로젝트
    /// 어디에도 UnityEngine.Input을 이동 트리거로 참조하는 코드를 남기지 않는다.
    /// </summary>
    public interface IMovementIntentSource
    {
        /// <summary>-1(왼쪽)~1(오른쪽). moveInputDeadzone 이하는 기존과 동일하게 "정지"로 취급된다.</summary>
        float MoveInputX { get; }

        /// <summary>
        /// 이번 프레임에 점프 의도가 새로 발생했는지 — UnityEngine.Input.GetButtonDown과 동일하게 "정확히
        /// 1프레임만 true"인 펄스 계약을 지켜야 한다(26-7). 그렇지 않으면 착지 즉시 재점프를 시도하는 등의
        /// 버그가 생길 수 있다.
        /// </summary>
        bool JumpRequested { get; }
    }
}
