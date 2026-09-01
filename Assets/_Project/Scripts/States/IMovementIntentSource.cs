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

        /// <summary>
        /// 이번 프레임에 "발판 모서리를 붙잡고 매달려 내려가기"(States/LedgeHangState.cs) 의도가 새로
        /// 발생했는지 — <see cref="JumpRequested"/>와 **완전히 동일한 1프레임 펄스 계약**을 따른다.
        ///
        /// 왜 JumpRequested를 재사용하지 않고 채널을 새로 팠는가: 점프는 "위로", 매달려 내려가기는
        /// "아래로"라 의도가 정반대이고, 소비자(WalkState)가 둘을 구분할 방법이 없으면 발판 경계에서
        /// 두 동작이 서로를 잡아먹는다(지금도 경계 점프 분기가 같은 자리에 있다). 펄스가 하나 더
        /// 늘어나는 비용보다 의도를 분리해 두는 편이 훨씬 싸다.
        /// </summary>
        bool LedgeHangRequested { get; }

        /// <summary>
        /// 이번 프레임에 "낙차가 작은 턱에서 그냥 앞으로 뛰어내리기" 의도가 새로 발생했는지 —
        /// 위 두 펄스와 **완전히 동일한 1프레임 펄스 계약**을 따른다.
        ///
        /// 왜 LedgeHangRequested를 재사용하지 않는가(2026-08-29): 목적지 판정 기준이 정반대다.
        /// 매달리기는 "손끝~발끝 거리보다 **더 깊은**" 발판만 목적지로 인정하고, 뛰어내리기는
        /// "그보다 **얕은**" 발판만 인정한다(StickmanBlackboard.TryFindHopDownTarget / TryFindDescendTarget).
        /// 한 채널로 합치면 소비자(WalkState)가 어느 쪽 판정을 다시 돌려야 하는지 알 수 없고, 잘못된
        /// 쪽을 고르면 "매달렸는데 발밑에 이미 발판이 있음"(몸이 발판을 파고듦) 또는 "얕은 턱에서
        /// 매달리려다 실패해 그냥 서 있음" 중 하나가 된다.
        /// </summary>
        bool HopDownRequested { get; }

        /// <summary>
        /// 이번 프레임에 "발판 경계 앞의 낮은 턱을 기어올라 되돌아가기"(States/ParkourClimbState.cs)
        /// 의도가 새로 발생했는지 — 역시 동일한 1프레임 펄스 계약.
        ///
        /// 왜 JumpRequested를 재사용하지 않는가(2026-08-29): WalkState의 점프 분기는 "벽이 있으면
        /// ParkourClimb, 없으면 Jump"라 **의도가 실패했을 때 점프로 흘러내린다.** 사용자 피드백
        /// ("이상하게 점프도 하고")으로 경계 점프 확률(wanderEdgeJumpAttemptChance)은 기본 0이 되었으므로,
        /// 되올라가기 의도가 점프로 새는 경로가 있으면 그 결정이 무력화된다. 이 채널은 벽 판정에
        /// 실패하면 **아무 일도 일어나지 않는다**(그 자리에 그대로 서 있다가 기존 배회 행동으로 복귀).
        /// </summary>
        bool StepUpRequested { get; }
    }

    /// <summary>
    /// ★ 선택 구현 인터페이스 — 의도 소스가 "지금 페이즈가 얼마나 더 갈 계획인가"를 아는 경우에만
    /// 구현한다(2026-09-01, 발화 자격 게이트 docs/UX_FLOW.md 5절 규칙 8).
    ///
    /// ============================================================================
    /// 왜 IMovementIntentSource에 필드를 더하지 않았는가 (교차 레이어 판단 근거)
    /// ============================================================================
    /// 규칙 8은 "Idle 대사를 할지 말지"를 <b>그 Idle이 얼마나 갈 계획인지</b>로 정한다. 그 계획을 아는
    /// 것은 IdleState가 아니라 <b>배회 AI</b>다 — Idle 상태 자체는 "지금 정지 중"이라는 사실 외에 아무
    /// 계획도 갖지 않으므로, IdleState가 계획을 지어내면 그건 원칙 1이 금지하는 "확정되지 않은 사실"이다.
    /// 그래서 값은 의도 소스에서 와야 한다.
    ///
    /// 다만 이 값은 <b>이동 의도의 일부가 아니다</b>. 필수 멤버로 넣으면 "정지한 더미"를 흉내 내는
    /// 모든 구현(테스트 스텁 35개 포함)이 자기와 무관한 계획 시간을 억지로 답해야 한다 — 그리고 그
    /// 답은 전부 "모른다"다. <see cref="StickMate.Dialogue.IHasDialogueParams"/>가 "파라미터를 노출할
    /// 수 있는 상태만 구현하는" 선택 인터페이스인 것과 정확히 같은 이유로, 이것도 선택으로 둔다.
    /// 구현하지 않은 소스는 게이트에서 <see cref="float.NaN"/>으로 읽히고, 그때 게이트는 막지 않는다
    /// (규칙 8은 "컷될 대사를 줄이는" 최적화이지 검열이 아니다).
    /// </summary>
    public interface IPlannedDwellSource
    {
        /// <summary>
        /// 지금 진행 중인 페이즈의 <b>계획 잔여 체류 시간</b>(초). 지어내는 값이 아니라 페이즈 진입에서
        /// 이미 한 번 추첨되어 확정된 길이에서 경과분을 뺀 나머지다. 계획을 알 수 없으면
        /// <see cref="float.NaN"/>.
        /// </summary>
        float PlannedDwellRemainingSeconds { get; }
    }
}
