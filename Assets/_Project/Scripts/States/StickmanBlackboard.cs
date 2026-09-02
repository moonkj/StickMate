using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.States
{
    /// <summary>
    /// Idle/Walk/Jump/Fall 등 능동 상태들이 공유하는 실행 컨텍스트("블랙보드").
    ///
    /// 왜 필요한가: IStickmanState 인터페이스는 Phase 0에서 확정된 계약(Enter/Tick/Exit)이라 시그니처를
    /// 바꿀 수 없다(팀 컨벤션, Coder 작업 지침). 상태별로 Rigidbody2D/카메라/설정/발판 폴러/상태머신을
    /// 전부 개별 생성자 인자로 늘어놓는 대신, 이 블랙보드 하나만 주입받게 해 향후 필드가 늘어나도
    /// IStickmanState 계약이나 각 상태 생성자 시그니처를 다시 건드릴 필요가 없게 한다.
    ///
    /// 이동 의도(MoveInputX/JumpPressed)는 더 이상 UnityEngine.Input을 직접 읽지 않는다(BUG-P1-B2 대응) —
    /// StickmanAgent(Core)가 매 프레임 IntentSource(IMovementIntentSource, 현재는 AutoWanderController)의
    /// Tick()만 갱신해주고, 아래 두 프로퍼티는 그 소스를 그대로 읽어 계산된다. 여러 상태가 각자 입력을
    /// 폴링하는 중복은 여전히 없다.
    /// </summary>
    public sealed class StickmanBlackboard
    {
        public Rigidbody2D Body;
        public Camera MainCamera;
        public StickConfig Config;
        public StickmanStateMachine Machine;
        public FootholdPoller FootholdPoller;

        /// <summary>
        /// StickmanAgent.ReportExternalImpact()가 RAGDOLL로 강제 전이시키기 직전에 기록하는 충격량
        /// 스냅샷(UX_FLOW.md 31-2 #2 "Ragdoll impactMagnitude" 파라미터용). RagdollState.Enter()가
        /// 이 값을 StickConfig.ragdollForceThreshold로 나눈 배율을 IHasDialogueParams로 노출해,
        /// 대사 강도와 실제 충격 강도가 항상 같은 파라미터에서 파생되도록 한다(31-1 원칙).
        /// </summary>
        public float LastImpactMagnitude;

        /// <summary>
        /// ★ 2026-09-01 (P9-b) — 위 <see cref="LastImpactMagnitude"/>와 <b>짝을 이루는 방향 스냅샷</b>
        /// (월드, 정규화 불필요, "캐릭터가 밀려나는 방향"). 크기가 "얼마나 세게"라면 이것은 "어느
        /// 쪽으로"이고, 둘이 합쳐져야 RagdollRig.EnterRagdoll(방향, 충격량)에 넘길 벡터가 완성된다.
        /// 생산자는 <see cref="RagdollImpactResolver.TryApplyImpact(StickmanBlackboard,float,UnityEngine.Vector2)"/>
        /// 하나뿐이다(전이를 확정하기 직전에 원인이 되는 물리량을 남기는 이 프로젝트의 관례 그대로).
        ///
        /// <b>이것만 소비형(consume-once)이다</b>: RagdollState.Enter()가 읽는 즉시 0으로 지운다.
        /// 크기 쪽은 대사 파생용이라 남겨도 무해하지만, 방향은 남겨 두면 <b>방향을 모르는 진입</b>
        /// (StickmanAgent.ReportExternalImpact(크기만) / 테스트의 직접 ChangeState / 원인 불명의 강제
        /// 랙돌)에서 <b>지난번 타격의 방향으로 유령 충격량이 다시 실린다</b>. 0이면
        /// RagdollRig가 충격량 경로 전체를 건너뛰므로(= 기존 무인자 거동), 지우는 것 하나로
        /// "방향을 아는 경로만 힘을 받는다"가 보장된다.
        /// </summary>
        public Vector2 LastImpactDirection;

        /// <summary>
        /// ★ 마지막 착지에서 실제로 떨어진 높이(월드 유닛). FallState.ConfirmLanding()이
        /// StickmanEventBus.LandingRollRequested에 싣는 것과 **정확히 같은 값**을 여기에도 남긴 뒤
        /// LandingCrouch로 전이하고, LandingCrouchState.Enter()가 이 스냅샷 하나로 앉는 깊이와 유지
        /// 시간을 함께 정한다("높을수록 더 깊이 앉고 더 오래 유지" — 리더 지시).
        ///
        /// LastImpactMagnitude와 완전히 같은 관례다(전이를 확정하기 직전에 원인이 되는 물리량을
        /// 스냅샷으로 남기고, 진입한 상태가 그 하나에서 모든 파생값을 만든다). 이 값을 이벤트
        /// 페이로드에서 다시 읽지 않는 이유는, 이벤트 구독자가 0명이어도 착지 연출은 반드시 동작해야
        /// 하기 때문이다 — 이 프로젝트에서 "구독자가 없어 기능이 통째로 죽어 있던" 사례가 6번 있었다.
        /// </summary>
        public float LastLandingFallHeight;

        /// <summary>
        /// ★ 마지막으로 **던져진** 순간의 속도(월드 유닛/초). DragThrowState.ReleaseAndThrow()가
        /// ThrowTumble로 전이시키기 직전에 남기고, ThrowTumbleState.Enter()가 이 하나에서 회전
        /// 방향(던진 방향)과 회전 속도(던진 세기)를 함께 파생시킨다.
        ///
        /// 위 LastImpactMagnitude / LastLandingFallHeight와 완전히 같은 관례다 — 전이를 확정하기
        /// 직전에 원인이 되는 물리량을 스냅샷으로 남기고, 진입한 상태가 그 하나에서 모든 파생값을
        /// 만든다. 이벤트 페이로드로 흘리지 않는 이유도 같다(구독자가 0명이면 연출이 통째로 죽는다).
        /// </summary>
        public Vector2 LastThrowVelocity;

        /// <summary>
        /// 이동 의도의 유일한 출처(BUG-P1-B2 대응, docs/BUG_REPORT_PHASE1.md Blocker). 예전에는
        /// StickmanAgent.Update()가 UnityEngine.Input.GetAxisRaw/GetButtonDown을 직접 읽어
        /// MoveInputX/JumpPressed 필드에 대입했지만, 키보드 의존은 실제 분리 오버레이(WS_EX_NOACTIVATE)가
        /// 완성되는 순간 영구 정지가 확정되는 구조적 결함이었다(가설 H6). 지금은 이 인터페이스(현재는
        /// docs/UX_FLOW.md 26절 배회 행동 스펙을 구현한 AutoWanderController)를 통해서만 아래 두 프로퍼티가
        /// 계산되며, StickmanAgent/State들은 그 출처가 키보드인지 AI인지 전혀 모른다.
        /// </summary>
        public IMovementIntentSource IntentSource;

        /// <summary>-1(왼쪽)~1(오른쪽). IntentSource에서 매 프레임 조회(더 이상 필드로 직접 대입되지 않음).</summary>
        public float MoveInputX => IntentSource != null ? IntentSource.MoveInputX : 0f;

        /// <summary>이번 프레임에 점프가 요청되었는지. IntentSource에서 매 프레임 조회.</summary>
        public bool JumpPressed => IntentSource != null && IntentSource.JumpRequested;

        /// <summary>이번 프레임에 "모서리를 붙잡고 매달려 내려가기"가 요청되었는지(JumpPressed와 동일한
        /// 1프레임 펄스 계약). IntentSource에서 매 프레임 조회 — WalkState가 소비한다.</summary>
        public bool LedgeHangPressed => IntentSource != null && IntentSource.LedgeHangRequested;

        /// <summary>이번 프레임에 "낙차가 작은 턱에서 그냥 앞으로 뛰어내리기"가 요청되었는지(위 두 펄스와
        /// 동일한 1프레임 계약). IntentSource에서 매 프레임 조회 — WalkState가 소비한다.</summary>
        public bool HopDownPressed => IntentSource != null && IntentSource.HopDownRequested;

        /// <summary>이번 프레임에 "낮은 턱을 기어올라 되돌아가기"가 요청되었는지(동일한 1프레임 계약).
        /// IntentSource에서 매 프레임 조회 — WalkState가 소비해 ParkourClimb로 보낸다.</summary>
        public bool StepUpPressed => IntentSource != null && IntentSource.StepUpRequested;

        /// <summary>
        /// ★ 배회 페이즈의 **계획 잔여 체류 시간**(초) — 발화 자격 게이트(docs/UX_FLOW.md 5절 규칙 8)가
        /// 읽는 유일한 상태 입력. 의도 소스가 <see cref="IPlannedDwellSource"/>를 구현한 경우에만
        /// 값이 있고(위 다섯 채널과 같은 "IntentSource에서 조회" 패턴), 구현하지 않았거나 계획을
        /// 모르면 <see cref="float.NaN"/>이다 — 그때 게이트는 막지 않는다
        /// (침묵보다 발화가 안전한 쪽이다: 규칙 8은 "컷될 대사를 줄이는" 최적화이지 검열이 아니다).
        /// </summary>
        public float PlannedWanderDwellRemainingSeconds =>
            IntentSource is IPlannedDwellSource planned ? planned.PlannedDwellRemainingSeconds : float.NaN;

        /// <summary>
        /// ★★ 2026-09-01 — <b>"이 상태"의 계획 잔여 체류 시간</b>(초). 발화 자격 게이트(규칙 8)는
        /// 이제 위 <see cref="PlannedWanderDwellRemainingSeconds"/>가 아니라 <b>이쪽</b>에 묻는다.
        ///
        /// ============================================================================
        /// 왜 갈랐나 — 게이트가 물은 것과 소스가 답한 것이 서로 다른 대상이었다
        /// ============================================================================
        /// <c>PlannedWanderDwellRemainingSeconds</c>는 <b>배회 AI 페이즈의 잔여</b>이지 <b>이 상태의
        /// 잔여</b>가 아니다. 둘이 같은 값인 것은 <b>상태가 배회 페이즈 전환 때문에 들어왔을 때뿐</b>이다.
        /// 실제로는 다음 경로들이 전부 "배회는 Moving 한복판인데 Idle로 들어오는" 모양이다:
        /// ParkourClimb 완료 → Idle / Getup → Idle / LandingCrouch → Idle·Walk / 활쏘기 종료 → Idle /
        /// GroundLossHang 복귀 → Idle·Walk. 이때 Idle은 <b>다음 프레임</b>에
        /// <c>MoveInputX &gt; deadzone</c>으로 곧장 Walk로 나가는데, 게이트에게 물으면 "2.8초 남았다"고
        /// 답한다. 실측(frame 11110~11114): 4프레임 안에 글자 블록 두 개가 각각 <b>0.02초</b>씩 번쩍였다 —
        /// 규칙 8이 없애려던 바로 그 현상이, 규칙 8이 들어간 빌드에서.
        ///
        /// ============================================================================
        /// 왜 여기이고, 왜 "From != X" 예외가 아닌가
        /// ============================================================================
        /// 이미 한 경로(<c>context.From != GroundLossHang</c>)를 손으로 막아 둔 전례가 있고, 나머지
        /// 네 경로가 그대로 남아 있었다. 예외를 하나 더 박으면 여섯 번째 경로가 생기는 날 또 샌다.
        /// 그래서 <b>진입 경로를 열거하지 않고</b>, 두 사실을 각자의 소유자에게 묻는 형태로 나눈다:
        /// <list type="number">
        ///   <item><b>계획의 길이</b>는 배회 AI가 안다(<see cref="IPlannedDwellSource"/>). 상태는
        ///         모른다 — IdleState가 계획을 지어내면 그게 원칙 1이 금지하는 "확정되지 않은 사실"이다.</item>
        ///   <item><b>그 계획이 지금 이 상태를 서술하는가</b>는 <b>상태의 탈출 조건</b>이 안다. Idle은
        ///         이동 의도가 데드존을 넘으면 나가고(IdleState.Tick), Walk는 데드존 이하가 되면
        ///         나간다(WalkState.Tick). 즉 <b>의도와 상태가 어긋나 있으면 잔여는 0이다</b> —
        ///         추정이 아니라 다음 Tick에 실제로 일어나는 일이다.</item>
        /// </list>
        /// 판정에 쓰는 데드존은 두 상태의 탈출 조건과 <b>같은 설정 필드</b>(moveInputDeadzone)여야 한다.
        /// 다른 값을 쓰면 "게이트는 남았다고 보는데 상태는 이미 나간" 틈이 그 차이만큼 생긴다.
        ///
        /// <para>배회 계획이 <b>서술하지 않는 상태</b>(파쿠르·랙돌·활쏘기 등)에는 <see cref="float.NaN"/>을
        /// 답한다. 그 상태들은 자기 길이를 스스로 알고 게이트에 직접 넘기며(ParkourClimbState의
        /// 등반 길이), 배회 잔여는 그들에 대해 아무 말도 하지 않기 때문이다. NaN이면 게이트는 막지
        /// 않는다 — 규칙 8은 컷될 대사를 줄이는 최적화이지 검열이 아니다.</para>
        /// </summary>
        public float PlannedDwellRemainingSecondsFor(StickmanStateId stateId)
        {
            bool stateWantsMove;
            if (stateId == StickmanStateId.Walk) stateWantsMove = true;
            else if (stateId == StickmanStateId.Idle) stateWantsMove = false;
            else return float.NaN; // 배회 계획이 서술하지 않는 상태 — 모른다고 답한다.

            float phaseRemaining = PlannedWanderDwellRemainingSeconds;
            if (float.IsNaN(phaseRemaining)) return float.NaN;

            float deadzone = Config != null ? Config.moveInputDeadzone : 0.15f;
            bool intentWantsMove = Mathf.Abs(MoveInputX) > deadzone;

            // ★★ 2026-09-02 — 맨틀이 **이번 프레임에 확정**됐으면 MoveInputX는 그 사실보다 낡았다.
            // StickmanAgent.Update의 순서가 `_autoWander.Tick -> _machine.Tick`이라, 벽타기가 맨틀을
            // 보고한 프레임의 이동 의도는 아직 0이고 배회 AI는 **다음 프레임**에야 그 신호를 소비해
            // EnterMoving(ClimbMantleDirection)을 부른다. 그 한 프레임 동안 게이트가 낡은 0을 보고
            // "정지 계획이 2.8초 남았다"고 답해 1프레임짜리 상태에서 대사가 파생됐다(원칙 1 위반).
            //
            // 근본 수정은 ParkourClimbState가 그 사실에서 직접 Walk를 고르는 것이고(그래서 1프레임
            // Idle 자체가 사라졌다), 이 줄은 **같은 사실을 읽는 두 번째 소비자**를 같은 진실에 맞춘다 —
            // 게이트가 어떤 경로로 불리든 "확정된 사실"이 "낡은 관측"을 이긴다.
            if (Time.frameCount == _climbMantleFrame) intentWantsMove = true;

            return intentWantsMove == stateWantsMove ? phaseRemaining : 0f;
        }

        /// <summary>
        /// StickmanAgent.TryGetCursorPosition과 동일한 시그니처(CursorPositionQuery, UX_FLOW.md 9절-3
        /// 전역 커서 폴링 채널 재사용) — 드래그&던지기(DragThrowState)/로데오 커서(RodeoCursorState)가
        /// 커서 월드 좌표를 조회하기 위해 사용한다(Phase 3). AutoWanderController.CursorProvider(26-4 훅)와는
        /// 별개의 델리게이트 인스턴스지만 둘 다 같은 StickmanAgent.TryGetCursorPosition 메서드 그룹을 가리킨다.
        /// </summary>
        public CursorPositionQuery CursorProvider;

        /// <summary>
        /// 드래그&던지기(12절) "놓기" 신호. DragThrowController(Interaction)가 마우스업/트레이 긴급정지
        /// 발생 시 true로 세팅하고, DragThrowState.Tick()이 다음 틱에 읽는 즉시 false로 되돌리는 1회성
        /// 펄스 계약이다(IMovementIntentSource.JumpRequested와 동일한 소비-후-리셋 원칙, 다만 리셋 주체가
        /// 컨트롤러가 아니라 소비자 자신이라는 점만 다르다 — 이 신호는 매 프레임이 아니라 이벤트성으로만
        /// 세팅되므로 컨트롤러 쪽에서 "다음 프레임에 리셋"할 고정된 타이밍이 없다).
        /// </summary>
        public bool DragReleaseSignaled;

        // ==== 활쏘기(2026-08-29 사용자 요청) — Director -> State 계획 스냅샷 + State -> 렌더러 힌트.
        // "매 프레임 바뀌는 연속량"이라 이벤트가 아니라 필드다(초당 60회 델리게이트 호출을 피한다).
        // 렌더러가 이 값을 못 읽거나 잘못 읽어도 판정은 1비트도 달라지지 않는다 —
        // SetCharacterVisible(가출 렌더러 토글)과 같은 "상태 → 렌더링 레이어 단방향 통보" 관례다.
        // (2026-09-02까지는 이 관례의 원본이 격파 게이지 필드였다. 격파 놀이 삭제로 함께 사라졌다.)

        /// <summary>이번 활쏘기 사이클의 과녁 중심(월드). Interaction/ArcheryDirector가 자리를 확정한
        /// 직후, <b>ChangeState(Archery)를 호출하기 전에</b> 써둔다 — ArcheryState.Enter()가 이 값에서
        /// 명중/빗나감 도달점을 계산하므로 그 전에 확정돼 있어야 한다.</summary>
        public Vector2 ArcheryTargetWorld;

        /// <summary>이번 사이클의 지면 월드 Y(= 발동 시점의 캐릭터 발바닥). 빗나간 화살이 꽂히는 높이.</summary>
        public float ArcheryGroundWorldY;

        /// <summary>이번 사이클에 캐릭터가 <b>걸어가서 서야 할</b> 월드 X(발판/화면 구간의 한쪽 끝).
        /// 사용자 명시 요구 "…만큼 캐릭터가 이동한 다음 과녁을 생성후 쏘고" — 순간이동이 아니라
        /// ArcheryState의 Approach 페이즈가 실제로 걸어서 여기까지 간 뒤에야 과녁이 생성된다.</summary>
        public float ArcheryStandWorldX;

        /// <summary>이번 사이클에 과녁이 놓인 방향(+1 오른쪽 / -1 왼쪽). 화면 끝에서 미러링됐을 수 있어
        /// 캐릭터의 현재 FacingSign과 다를 수 있으므로 별도로 스냅샷한다.</summary>
        public float ArcheryFacingSign = 1f;

        /// <summary>지금 시위가 얼마나 당겨져 있는지(0~1) — <b>순수 렌더 힌트</b>다. ArcheryState가 매
        /// 프레임 자기가 이미 계산한 값을 복사해두고 ArcheryRenderer가 읽어 시위를 그린다.
        /// 이 값을 못 읽거나 잘못 읽어도 발사 타이밍/명중 결과는 1비트도 달라지지 않는다.</summary>
        public float ArcheryDrawRatio;

        /// <summary>지금 활을 들고 있는지(= Archery 상태 진행 중인지). 렌더러가 활을 보이고 감추는 신호.</summary>
        public bool ArcheryBowVisible;

        /// <summary>활을 꺼내 든 정도(0=아직 안 듦, 1=완전히 들어 겨눔). 과녁이 등장하는 동안 0에서 1로
        /// 올라가고 그 뒤로는 사이클 내내 1을 유지한다 — 시위를 당기지 않는 구간에도 활은 계속 들려
        /// 있어야 한다(그러지 않으면 발사 후마다 활이 옆구리로 내려간다). 렌더러는 이 값으로 활을
        /// 페이드인시킨다.</summary>
        public float ArcheryReadyRatio;

        /// <summary>
        /// true인 동안 <see cref="TickPose"/>가 바라보는 방향을 갱신하지 않는다.
        /// 활쏘기처럼 "제자리에 서서 한 방향을 겨누는" 연출은, 배회 AI가 계속 내보내는 이동 의도로
        /// 몸이 홱 돌아가면 화살이 뒤통수에서 나가는 그림이 된다. 상태가 Enter에서 켜고 Exit에서
        /// 반드시 끈다(어떤 종료 경로로도 풀리도록 — 안 풀리면 캐릭터가 영영 한쪽만 보고 걷는다).
        /// </summary>
        public bool FacingLocked;

        /// <summary>
        /// Attack(전투) 상태의 IHasDialogueParams 스냅샷 입력값(BUG-M7 파이프라인, docs/BUG_REPORT_PHASE3.md
        /// Minor 1 대응). 호출자가 Machine.ChangeState(Attack)를
        /// 호출하기 직전에 이번 타격 이후 "몇 대 더 맞아야 결판나는지"를 계산해 이 필드에 써두면,
        /// AttackState.Enter()가 그 값을 그대로 스냅샷해 "한 발 더!"(&gt;=1)/"오늘은 여기까지"(0) 대사를
        /// 파생시킨다. 아무도 세팅하지 않고 ChangeState(Attack)만 호출하면 기본값 0("오늘은 여기까지")
        /// 그대로 유지된다 — 값을 채우지 않는 호출부에서도 안전한 기본 동작.
        /// </summary>
        public int AttackShotsRemaining;

        /// <summary>
        /// 유휴 혼잣말(docs/UX_FLOW.md 26-3절, Dialogue/AmbientChatter.cs)의 다음 발화 허용 시각
        /// (Time.unscaledTime 기준). Idle과 Walk가 **하나의 타이머를 공유**한다 — 둘은 2~6초마다
        /// 번갈아 일어나므로 각자 쿨다운을 두면 확률이 낮아도 체감상 수다스러워진다.
        /// </summary>
        public float NextChatterAllowedUnscaledTime;

        /// <summary>
        /// "지금 즉시 혼잣말을 하라"는 강제 발화 펄스(Interaction/AppControlDirector.cs의 데모 단축키
        /// Ctrl+Opt+Cmd+B). AmbientChatter.TryRollChatter()가 소비 즉시 리셋하며, 소비되면 확률/쿨다운을
        /// 모두 건너뛴다(DragReleaseSignaled와 동일한 1프레임 펄스 계약).
        /// </summary>
        public bool ForcedChatterSignaled;

        /// <summary>
        /// 가출(20절) 은신처 월드 좌표 스냅샷 — Interaction/RunawayDirector.cs가 ChangeState(Runaway)
        /// 직전에 세팅하고, States/RunawayState.cs의 초기 Enter()가 1회 소비한다(AttackShotsRemaining과
        /// 동일한 "펄스 세팅 → 소비" 관례).
        /// </summary>
        public Vector2 PendingRunawayHideWorldPos;

        /// <summary>가출 상태에서 "발견됨"(캐릭터 히트박스 클릭) 1회성 신호. RunawayDirector가
        /// StickmanClickHitbox.MouseDown을 구독해 세팅하고, RunawayState.Tick()이 Hidden 페이즈에서만
        /// 소비한다(다른 페이즈 중 세팅되면 조용히 무시 — DragReleaseSignaled와 동일한 소비-후-리셋 펄스).</summary>
        public bool RunawayFoundSignaled;

        /// <summary>가출 상태에서 "간식을 줌"(Found 페이즈 전용) 1회성 신호. 실제 UI(간식 주기 버튼)는
        /// Phase2+ 렌더링 담당 — 지금은 Interaction/RunawayDirector.cs가 공개한 OfferSnack()이 이 신호를 세팅한다.</summary>
        public bool RunawaySnackOfferedSignaled;

        /// <summary>가출 상태에서 "돌아오라고 부르기"(트레이 수동 소환, 20절) 1회성 신호.</summary>
        public bool RunawayManualRecallSignaled;

        /// <summary>가출 상태에서 트레이 긴급정지로 인한 "강제 소환"(24절 — 인질극의 '종료'와 달리 가출은
        /// '즉시 강제 복귀') 1회성 신호. 다른 복귀 신호와 달리 어떤 페이즈에서도 즉시 처리된다.</summary>
        public bool RunawayForceSummonSignaled;

        /// <summary>
        /// 캐릭터 렌더러 표시/숨김 제어(가출 20절 전용 통로). StickmanAgent.Awake()가 자신의 기존
        /// private SetRenderersEnabled(bool)를 그대로 이 델리게이트에 연결한다(CursorProvider와 동일한
        /// "이미 있는 private 메서드를 블랙보드로 노출" 패턴 — 새 메서드를 만들지 않는다). Suspend()/
        /// Resume()의 전체화면 은닉과는 별개의 독립 스위치다 — RunawayState는 이 필드만 사용하고
        /// StickmanAgent._isSuspended 플래그에는 관여하지 않는다(20절 예외: 가출 중 전체화면 감지가
        /// 와도 강제 취소하지 않고 그냥 함께 멈췄다가(Suspended는 Tick 자체를 건너뜀) 재개된다).
        /// </summary>
        public System.Action<bool> SetCharacterVisible;

        /// <summary>
        /// BUG-P5-M1 대응(Major, docs/BUG_REPORT_PHASE5.md) — RunawayState의 Hidden 페이즈가 렌더러를
        /// 숨긴 동안(아직 발견되지 않음)임을 StickmanAgent.Resume()에 알리는 통로. 기존 문제: Resume()은
        /// 항상 SetRenderersEnabled(true)를 무조건 호출했는데, 가출 Hidden 구간 중 전체화면 Suspend/
        /// Resume이 한 번이라도 왕복하면 이 무조건 호출이 RunawayState의 독립적인 은신 가시성 의도를
        /// 덮어써 아직 못 찾은 캐릭터가 강제로 노출됐다(20절 핵심 상호작용 위반).
        /// RunawayState.HideCharacterAtHideSpot()이 true로 세팅하고, ShowCharacterRevealed()/
        /// RestoreCharacter()/Exit()가 false로 되돌린다 — Resume()은 이 플래그가 true인 동안만
        /// SetRenderersEnabled(true) 호출을 건너뛴다("Suspend/Resume의 렌더러 제어"와 "Runaway의 렌더러
        /// 제어"가 서로의 존재를 알게 되는 최소 접점). IStickmanState에 훅을 추가하는 대안(Enter/Exit
        /// 외 제3의 메서드)도 검토했으나, 그러면 인터페이스를 구현하는 다른 10여 개 상태 전부가 영향
        /// 범위에 들어와 이 필드 하나만 추가하는 쪽이 더 침습적이지 않다고 판단했다(BUG_REPORT_PHASE5.md
        /// 수정 제안 (b) 채택, Coder 판단).
        /// </summary>
        public bool IsCharacterHiddenByRunaway;

        /// <summary>
        /// CursorProvider(OS 화면 좌표)를 이 블랙보드의 MainCamera/Config로 Unity 월드 좌표로 역변환한다.
        /// ScreenCoordinateConverter의 "cameraDepth는 같은 호출 세트 안에서 재사용" 규칙을 지키기 위해,
        /// Body 위치를 기준점으로 삼아 depth를 산출한 뒤 그 depth로 커서 좌표를 되돌린다(SenseGround()가
        /// 발판 좌표를 되돌릴 때 쓰는 것과 동일한 패턴).
        /// </summary>
        public bool TryGetCursorWorldPosition(out Vector2 worldPos)
        {
            worldPos = default;
            if (CursorProvider == null || MainCamera == null || Body == null) return false;
            if (!CursorProvider(out Vector2 osScreen)) return false;

            _ = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);
            Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, osScreen, depth, Config);
            worldPos = world;
            return true;
        }

        // Idle/Walk(지상 상태)에서 발판을 잃은 뒤 실제로 Fall로 전이하기까지의 유예 누적 시간.
        // Idle<->Walk를 오가는 동안에도 값이 보존되어야 발판 경계에서 상태가 왔다갔다 할 때마다
        // 유예 타이머가 리셋되는 오탐을 막을 수 있어(상태 인스턴스 밖인) 블랙보드에 둔다.
        private float _groundLossTimer;

        // ★ 2026-08-30 — GroundedTick()이 마지막으로 실행된 프레임 번호(TickGroundKeepingSafetyNet 참고).
        private int _groundedTickFrame = -1;

        // ★ 2026-09-01 (디버거) — 유예가 쌓이는 동안 관측한 가장 긴 프레임 시간(초).
        // 논리 발판에는 콜라이더가 없어 "서 있기"가 매 프레임 스냅으로만 유지되므로, 프레임이 한 번
        // 길어지면 창이 전혀 변하지 않았는데도 자유낙하로 허용오차 밴드를 벗어난다(임계는
        // GroundSensor.ComputeGroundLossFrameTimeThreshold가 유도 — 배포 형상에서 약 182ms).
        // 그 경우를 (c) "창이 세로로 움직임"과 구분해 로그로 남기기 위한 값이다.
        private float _worstLossDeltaTime;

        // ★ 2026-09-01 (근본 수정) — GroundedTick()이 "접지 확정"으로 끝난 마지막 프레임 번호.
        // 프레임 끝의 ApplyGroundedGravitySuppression()이 이 값 하나로 억제 여부를 정한다.
        private int _groundedConfirmedFrame = -1;

        // 같은 함수가 "아직 유예 중이고, 몸을 붙잡아 둬야 한다"고 판단한 마지막 프레임 번호.
        private int _graceHoldFrame = -1;

        // 이번 발판 상실 구간 직전에 중력 억제가 실제로 걸려 있었는가(진단 전용).
        // DescribeGroundLoss()에 그대로 넘겨 사유 (d)를 잘못 지목하지 않게 한다.
        private bool _groundedGravitySuppressionEngagedSinceLastLoss;

        // ★ 2026-08-30 — Fall 상태인데 실제로는 멈춰 있는(= 논리 발판 없는 물리면에 얹힌) 시간 누적.
        // EnforceScreenBoundsAndRescue()의 사각지대 회수 판정에 쓴다.
        private float _fallRestingTimer;

        // Active Ragdoll(아키텍처 0절) 파츠 캐시. Ragdoll/Getup 두 상태가 공유하므로 블랙보드가
        // 최초 1회만 구성해 보관한다(매 프레임 GetComponentsInChildren 재탐색 금지 컨벤션 준수).
        private RagdollRig _ragdollRig;

        // 능동 상태 절차적 팔다리 포즈 드라이버(2026-08-28 근본 재구현). GetRagdollRig()와 동일한 지연
        // 생성/캐싱 패턴 — Idle/Walk/Getup을 포함한 모든 능동 상태가 공유한다.
        private StickmanPoseAnimator _poseAnimator;

        // 머리 안 눈동자 점 제어(States/EyeController.cs). 같은 지연 생성/캐싱 패턴.
        private EyeController _eyeController;

        // 몸 바깥에서 잉크를 더 얹는 부품들(Core/ICharacterInkExtentProvider) — 지금은 액세서리
        // 렌더러 하나뿐이다. 같은 지연 생성/캐싱 패턴(계층은 런타임에 컴포넌트가 늘지 않는다는
        // 이 프로젝트의 전제를 따른다 — 씬 부트스트랩이 캐릭터 루트에 전부 붙여 둔다).
        private ICharacterInkExtentProvider[] _inkExtentProviders;

        // 캐릭터 실측 치수 조회 창구(Core/StickmanMetrics.cs). 같은 지연 생성/캐싱 패턴 — 낙하 자세의
        // "초당 몇 신장을 떨어지는가" 무차원화와 착지 깊이 램프의 신장 배수 환산에 쓴다.
        private StickmanMetrics _metrics;

        // 마지막으로 확정된 바라보는 방향(+1 오른쪽 / -1 왼쪽). 이동 의도가 불감대 이하일 때는 갱신하지
        // 않고 그대로 유지한다(정지 중에 방향이 흔들리지 않게).
        private float _facingSign = 1f;

        /// <summary>
        /// ★ 지금 실제로 딛고 있는 발판의 핸들(0 = 없음/공중). 리더 지시 3~5항의 "발판 고착" 상태.
        /// - 착지 확정(FallState.ConfirmLanding)에서만 설정된다 = 발판 전환은 낙하->착지로만 일어난다.
        /// - Fall 진입(FallState.Enter)에서 0으로 지워진다 = 공중에서는 어떤 발판도 붙잡고 있지 않다.
        /// - 이 값이 0이 아니면 SenseGround()는 그 핸들의 발판만 접지 후보로 본다(GroundSensor.Sense의
        ///   preferredHandle 문서 참고). 그 발판이 사라지거나 X 범위를 벗어나면 즉시 Grounded=false가
        ///   되고 GroundedTick()이 Fall로 보낸다.
        /// 드래그/랙돌/로데오처럼 몸을 임의 위치로 옮기는 상태를 거쳐 오면 값이 낡아 있을 수 있는데,
        /// 그때는 접지 판정이 실패해 fallGraceDuration(0.1초) 뒤 Fall -> Enter에서 0으로 초기화 ->
        /// 재획득으로 **스스로 회복된다**(고착 상태가 남지 않는다).
        /// </summary>
        public long CurrentFootholdHandle;

        /// <summary>FootholdPoller의 캐시(= OS를 직접 호출하지 않는 저렴한 조회)를 이용해 접지 상태를 계산한다.</summary>
        public GroundSensor.GroundInfo SenseGround()
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.Sense(MainCamera, foot, footholds, Config, CurrentFootholdHandle);
        }

        /// <summary>
        /// 스윕 착지 판정(GroundSensor.TryFindLandingCrossing 문서 참고 — 헤드라인 기능 "창 위 착지"가
        /// 실제로 성립하게 만드는 연속 교차 검사). FallState가 매 프레임 호출한다.
        ///
        /// ★ 2026-08-29 — 뛰어내리기 직후의 drop-through 유예를 여기서 함께 적용한다
        /// (<see cref="DropThroughIgnoredFootholdHandle"/> 문서 참고). 이 한 곳에서 넘겨주므로
        /// FallState는 "무시 핸들"이라는 개념 자체를 알 필요가 없다.
        /// </summary>
        public bool TryFindLandingCrossing(Vector2 prevFootWorldPos, Vector2 currFootWorldPos, out long handle, out float landingWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryFindLandingCrossing(MainCamera, prevFootWorldPos, currFootWorldPos, footholds, Config,
                out handle, out landingWorldY, DropThroughIgnoredFootholdHandle);
        }

        // ============================================================================
        // ★ 뛰어내리기 직후 "방금 떠난 발판" 통과 유예 (drop-through, 2026-08-29)
        // ============================================================================
        // 왜 필요한가 / 왜 이 방식인가는 States/WalkState.cs의 "발을 뗍니다" 블록 주석과
        // StickConfig.hopDownDropThroughIgnoreDuration의 Tooltip에 근거까지 적어뒀다. 요약하면:
        // 서 있는 몸은 발판 상단선에 정확히 스냅돼 있어 그대로 Fall에 들어가면 스윕 교차가 방금 떠난
        // 발판을 다시 잡는다. 예전에는 몸을 모서리 바깥으로 **순간이동**시켜 이를 피했지만(실측 0.31유닛),
        // 순간이동은 사용자가 반복 지적해온 아티팩트라 "잠깐 착지 후보에서 빼기"로 대체했다.
        //
        // 적용 범위는 의도적으로 최소다: 이 값을 세팅하는 곳은 WalkState의 뛰어내리기 블록 **한 곳**뿐이고,
        // 읽는 곳은 FallState의 착지 확정 두 경로뿐이다. 매달리기 해제/던지기/일반 낙하는 이 값을 절대
        // 세팅하지 않으므로 0(무시 없음)이라 기존 동작과 100% 동일하다.

        private long _dropThroughIgnoredHandle;
        private float _dropThroughIgnoreUntilTime = float.NegativeInfinity;

        // ★★ 2026-09-02 — 이 기한은 States/Core를 통틀어 **유일한 절대 기한**(Time.time + duration)이다.
        // 다른 모든 상태 타이머는 deltaTime 누적이라 Tick을 건너뛰면 저절로 멈추지만, 이것만은 벽시계라
        // 전체화면 Suspend 동안에도 계속 흘러간다. StickmanAgent.Suspend()의 계약("진행 중이던 상태의
        // 내부 타이머가 그대로 멈춰 있다가 Resume 이후 이어서 진행된다")이 이 한 값에서만 거짓이었고,
        // 하강 도중 전체화면이 겹치면 Resume 시점에 drop-through와 발떼기이송이 **둘 다 만료**돼
        // 그 하강이 조용히 무효가 됐다. 아래 Suspend/ResumeAbsoluteTimeWindows()가 잔여 시간을
        // 보관했다 재기점(re-base)해 계약을 실제로 참으로 만든다.
        private bool _absoluteWindowsSuspended;
        private float _suspendedDropThroughRemaining;

        // BeginDropThroughIgnore()가 **실제로 창을 열었을 때만** 갱신되는 프레임 번호.
        // BeginStepOffCarry()가 이 값으로 "직전 창을 물려받는" 경로를 구조적으로 막는다(아래 참고).
        private int _dropThroughArmedFrame = -1;

        /// <summary>
        /// 지금 착지 후보에서 제외해야 할 발판 핸들(유예가 끝났거나 애초에 없으면 0 = 제외 없음).
        /// 0은 이 프로젝트 전체에서 "발판 없음"을 뜻하는 관례값이라 그대로 재사용한다.
        /// </summary>
        public long DropThroughIgnoredFootholdHandle =>
            Time.time <= _dropThroughIgnoreUntilTime ? _dropThroughIgnoredHandle : 0L;

        /// <summary>주어진 핸들이 지금 drop-through 유예로 착지 후보에서 빠져 있는지.</summary>
        public bool IsFootholdDropThroughIgnored(long handle)
        {
            return handle != 0L && handle == DropThroughIgnoredFootholdHandle;
        }

        /// <summary>
        /// 지금부터 durationSeconds 동안 그 발판을 착지 후보에서 제외한다(뛰어내리기 전용).
        /// 유예는 시간이 지나면 스스로 풀리므로 해제 호출이 필요 없다 — 어떤 경로로 상태가 바뀌어도
        /// "무시가 영구히 남는" 사고가 구조적으로 불가능하다.
        /// </summary>
        public void BeginDropThroughIgnore(long footholdHandle, float durationSeconds)
        {
            if (footholdHandle == 0L || durationSeconds <= 0f) return;
            _dropThroughIgnoredHandle = footholdHandle;
            _dropThroughIgnoreUntilTime = Time.time + durationSeconds;
            _dropThroughArmedFrame = Time.frameCount;
        }

        /// <summary>
        /// 전체화면 Suspend 진입 시 <b>절대 기한을 얼려</b> 잔여 시간만 보관한다(위 필드 문서 참고).
        /// 멈춰 있는 동안 기한은 닫힌 것으로 읽힌다 — 어떤 경로가 끼어들어도 "만료되지 않은 척"이
        /// 아니라 "창 없음"(= 수정 이전의 기본 거동)으로 보이는 쪽이 항상 안전하다.
        /// </summary>
        public void SuspendAbsoluteTimeWindows()
        {
            if (_absoluteWindowsSuspended) return;
            _absoluteWindowsSuspended = true;
            _suspendedDropThroughRemaining = Mathf.Max(0f, _dropThroughIgnoreUntilTime - Time.time);
            _dropThroughIgnoreUntilTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Resume 시 기한을 <b>지금</b>을 기준으로 다시 세운다 — 숨어 있던 시간만큼 창이 뒤로 밀린다.
        /// 잔여가 0이면(애초에 창이 없었거나 숨기 직전에 끝났다) 이송 속도까지 함께 버린다.
        /// </summary>
        public void ResumeAbsoluteTimeWindows()
        {
            if (!_absoluteWindowsSuspended) return;
            _absoluteWindowsSuspended = false;
            float remaining = _suspendedDropThroughRemaining;
            _suspendedDropThroughRemaining = 0f;
            if (remaining <= 0f)
            {
                _dropThroughIgnoreUntilTime = float.NegativeInfinity;
                _stepOffCarryVelocityX = 0f;
                return;
            }
            _dropThroughIgnoreUntilTime = Time.time + remaining;
        }

        // ============================================================================
        // ★★ 발 떼기 수평 이송 (2026-09-02 — "Dock에서 뛰어내리기가 64회 전부 실패" 회귀 수정)
        // ============================================================================
        // 위 drop-through는 **논리** 발판만 다룬다. 그 설계는 한 가지를 참으로 가정했다 —
        // StickConfig.hopDownDropThroughIgnoreDuration의 Tooltip이 그대로 적어 둔 문장이다:
        //     "필요한 최소 시간 = hopDownEdgeCommitDistance / (walkSpeed x hopDownStepOffSpeedScale)"
        // 즉 **한 번 준 수평 속도가 모서리를 넘을 때까지 유지된다**는 가정이다. 창 상단(논리 발판만
        // 있고 콜라이더가 없다)에서는 참이지만, **Dock에서는 거짓이다** — Platform/DockPhysicsStep이
        // Dock 구간에 실제 BoxCollider2D를 깔아 두었고, 그 위에 얹힌 몸에는 쿨롱 마찰이 걸린다.
        //
        // 실측 유도(배율 0.60, 배포 형상):
        //     감속 a = 마찰계수 x 중력 = 0.4(Unity 2D 기본 재질) x 9.81 x gravityScale 3 = 11.77유닛/초²
        //     내딛는 속도 v = walkSpeed(2.5 x 0.60) x hopDownStepOffSpeedScale(0.8) = 1.20유닛/초
        //     정지 거리 = v² / 2a = 1.44 / 23.54 = **0.061유닛**
        //     실측 남은 거리(로그 64건) = 0.090 ~ 0.117유닛   ← 전부 정지 거리보다 멀다
        // 그래서 몸은 **모서리에 닿기 전에 멈추고**, 유예가 끝나면 같은 Dock에 낙차 0으로 다시 착지한다
        // (로그: "[FallState] 착지 확정 — 발판핸들=-2(Dock), 낙하높이=0.00유닛").
        //
        // ★ 왜 콜라이더를 통과시키지 않는가: 통과는 필요 없다. 캐릭터는 계단을 **뚫는** 것이 아니라
        //   **옆으로 걸어 나가야** 한다(그것이 원래 설계다). 콜라이더를 한 순간이라도 끄면 그 사이에
        //   Dock을 밟을 수 없게 되고, 그 콜라이더는 Dock 사각지대를 없애는 심장이다. 그래서 물리
        //   충돌 상태는 **한 톨도 건드리지 않고**, 원설계가 참이라고 가정했던 그 한 줄
        //   ("속도가 유지된다")을 유예 창 동안 실제로 참으로 만든다.
        //
        // 어법은 위 drop-through와 같다: **같은 타이머**를 쓰고(창이 둘로 갈릴 수 없다), 시간이 지나면
        // 스스로 풀리며, 세팅하는 곳은 WalkState의 뛰어내리기 블록 한 곳뿐이다.
        // ★ 네거티브 컨트롤: 공중에서는 이 재확정이 **아무 것도 바꾸지 않는다**(루트 linearDamping=0,
        //   공기 저항 없음) — 즉 콜라이더가 없는 창 위 뛰어내리기에서는 비트 단위로 기존 거동이다.

        private float _stepOffCarryVelocityX;

        /// <summary>
        /// 지금 다시 실어 줘야 할 발 떼기 수평 속도. drop-through 유예와 <b>같은 타이머</b>를
        /// 쓴다 — 두 창이 갈라지면 "논리적으로는 통과인데 물리적으로는 멈춰 있는" 지금의 사고가
        /// 다른 형태로 되돌아온다.
        /// </summary>
        /// <returns>유예가 살아 있고 실제 이송 속도가 있으면 true.</returns>
        public bool TryGetStepOffCarryVelocityX(out float velocityX)
        {
            velocityX = 0f;
            if (Config != null && !Config.hopDownStepOffCarryEnabled) return false;
            if (_stepOffCarryVelocityX == 0f || Time.time > _dropThroughIgnoreUntilTime) return false;
            velocityX = _stepOffCarryVelocityX;
            return true;
        }

        /// <summary>
        /// 발 떼기 수평 속도를 유예 창 동안 다시 싣도록 등록한다(뛰어내리기 전용).
        /// <see cref="BeginDropThroughIgnore"/> <b>직후에 같은 프레임에서만</b> 부른다.
        ///
        /// <para>★ 2026-09-02 — 같은 프레임 조건을 <b>실제로 강제한다</b>. 앞의
        /// <see cref="BeginDropThroughIgnore"/>는 핸들 0 / 유예 0이면 조기 return이라 창을 열지 않는데,
        /// 이 메서드는 무조건 실행됐다. 그래서 "직전 뛰어내리기의 창이 아직 살아 있는데 새 이송 속도만
        /// 갈아끼우는" 조합이 원리적으로 가능했다 — 창을 물려받는 형태다. 창을 열지 못한 호출은
        /// 이송도 열지 않고 <b>남은 값을 지운다</b>(= 수정 이전 기본 거동으로 폴백).</para>
        /// </summary>
        public void BeginStepOffCarry(float velocityX)
        {
            if (_dropThroughArmedFrame != Time.frameCount)
            {
                _stepOffCarryVelocityX = 0f;
                return;
            }
            _stepOffCarryVelocityX = velocityX;
            _stepOffCarryPhysicsTicks = 0;
            _stepOffCarryFrameTicks = 0;
        }

        /// <summary>
        /// 이송을 <b>즉시</b> 끝낸다. <see cref="States.FallState.Exit"/>가 부른다 — 이송은 "이 발 떼기가
        /// 만든 그 Fall 구간" 안에서만 의미가 있기 때문이다.
        ///
        /// <para>왜 필요했나(2026-09-02): 유예는 시간이 지나면 스스로 풀리지만 <see cref="_stepOffCarryVelocityX"/>는
        /// 어디서도 0으로 돌아가지 않았다. 그래서 창이 살아 있는 동안 <c>Fall → 다른 상태 → Fall</c> 왕복이
        /// 생기면 <b>낡은 이송 속도가 새 낙하의 x를 덮어썼다</b>(예: 낮은 단에 곧바로 착지한 뒤
        /// 스냅 상한 초과/발판 상실로 0.25초 안에 다시 Fall). 시간 조건 하나로는 이 왕복을 못 막는다 —
        /// 구간(episode)에도 묶어야 한다.</para>
        ///
        /// <para>Fall에 머무는 동안 <c>ChangeState(Fall)</c>가 다시 불리는 경로는 없다:
        /// <see cref="CheckScreenBoundsOrFall"/>은 이미 Fall이면 재전이를 걸지 않고,
        /// <see cref="GroundedTick"/>의 두 전이 경로는 <see cref="IsGroundKeepingSelfManaged"/>가
        /// Fall을 제외하므로 안전망이 Fall 중에는 아예 돌지 않는다. 즉 이 해제가 진행 중인 하강을
        /// 끊을 수 없다.</para>
        /// </summary>
        public void EndStepOffCarry()
        {
            _stepOffCarryVelocityX = 0f;
        }

        // ★★ 이송 재적용의 **주기** — 이 수정의 본질(2026-09-02 2차).
        //
        // 1차 수정은 재적용을 FallState.Tick(= StickmanAgent.Update, 프레임당 1회)에만 실었다.
        // 그런데 이 이송이 되돌리려는 마찰은 **FixedUpdate마다** 걸린다(고정 스텝 0.02초). 즉
        //   마찰 : 이송 = (프레임당 물리 스텝 수) : 1
        // 이라 한 프레임이 길어질수록 이송이 지고, 프레임이 유예(0.25초)를 통째로 삼키면 재적용이
        // **0회**가 되어 수정 자체가 없던 것이 된다. 그 프레임 안에서 몸은 정지 거리 0.061유닛만 가고
        // 멈추며(필요 0.090~0.119), Time.time 기준 유예도 함께 만료돼 제자리 착지 = 회귀 전 거동이다.
        //
        // ★ 그 조건은 가정이 아니라 **절전 등급의 정상 동작**이다: FramePacingTier.DisplayOff는
        //   FramePacingPolicy.DisplayOffTargetFps = 4fps(= 250ms/프레임)라 0.25초 창에 Update가
        //   1회(경계에 따라 0회) 실린다. Away(30fps)는 0.148초라 통과한다.
        //
        // 그래서 이제 **StickmanAgent.FixedUpdate()가 같은 메서드를 물리 주기로 부른다**(엔진 최대
        // timestep 0.333초 / 고정 스텝 0.02초 -> 250ms 프레임 하나에 12~13스텝). 프레임 경로도
        // 그대로 남겨 둔다: 두 경로가 **같은 한 구현**을 부르므로 갈릴 수 없고, 물리 배선이 어떤
        // 이유로 죽어도 이송이 통째로 사라지지는 않는다.
        //
        // ★ Time.time은 FixedUpdate 안에서 Time.fixedTime을 돌려준다(Unity 계약). 즉 유예 만료 판정도
        //   물리 해상도로 진행돼, 긴 프레임 하나가 창을 "건너뛰는" 일이 없다.
        private int _stepOffCarryPhysicsTicks;
        private int _stepOffCarryFrameTicks;

        /// <summary>이번 발 떼기 이후 이송이 <b>물리 스텝</b>에서 살아 있는 채로 평가된 횟수(진단/테스트용).</summary>
        public int StepOffCarryPhysicsTicks => _stepOffCarryPhysicsTicks;

        /// <summary>같은 값의 <b>렌더 프레임</b> 경로 횟수. 이 둘의 비가 곧 "마찰 : 이송"이다.</summary>
        public int StepOffCarryFrameTicks => _stepOffCarryFrameTicks;

        /// <summary>
        /// 유예 창이 살아 있는 동안 발 떼기 수평 속도를 다시 싣는다. <b>물리 주기와 프레임 주기가
        /// 공유하는 유일한 구현</b>(위 주석 참고).
        ///
        /// <para>상태가 Fall일 때만 동작한다 — 이송은 "발을 떼고 떨어지는 동안"의 장치이고, 다른
        /// 상태(매달리기/드래그/랙돌)의 x속도를 대신 정해 주면 그 상태의 계약을 침범한다.</para>
        /// </summary>
        public void TickStepOffCarry()
        {
            if (Body == null || Machine == null) return;
            if (Machine.CurrentStateId != StickmanStateId.Fall) return;
            if (!TryGetStepOffCarryVelocityX(out float carryX)) return;

            if (Time.inFixedTimeStep) _stepOffCarryPhysicsTicks++;
            else _stepOffCarryFrameTicks++;

            Vector2 v = Body.linearVelocity;
            if (v.x == carryX) return;
            v.x = carryX;
            Body.linearVelocity = v;
        }

        /// <summary>
        /// probeWorldPos의 x에서 딛을 수 있는 지면(가장 높은 발판 상단)의 월드 Y.
        /// GroundSensor.TryGetSurfaceWorldY 문서 참고 — 커서가 지면보다 아래에 있을 때 캐릭터를 그리로
        /// 옮겨 지면 밑에 가두는 사고(Fall 영구 고착)를 막기 위해 드래그&던지기/로데오가 쓴다.
        /// </summary>
        public bool TryGetGroundSurfaceWorldY(Vector2 probeWorldPos, out float surfaceWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryGetSurfaceWorldY(MainCamera, probeWorldPos, footholds, Config, out surfaceWorldY);
        }

        /// <summary>
        /// probeWorldPos의 x에서 **가장 낮은** 발판 상단(= 그 x에서의 바닥)의 월드 Y.
        /// GroundSensor.TryGetFloorWorldY 문서 참고 — 드래그/로데오의 "지면 아래로는 끌고 내려가지
        /// 않는다" 소프트 클램프가 써야 하는 값이다(가장 높은 표면을 쓰면 캐릭터를 위쪽 창으로
        /// 끌어올려 버린다 — 사용자 신고 "마우스로 끌었는데 갑자기 다른 창 위로 올라감").
        /// </summary>
        public bool TryGetFloorWorldY(Vector2 probeWorldPos, out float floorWorldY)
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            return GroundSensor.TryGetFloorWorldY(MainCamera, probeWorldPos, footholds, Config, out floorWorldY);
        }

        /// <summary>
        /// Idle/Walk 공용 지상 로직: 접지 중이면 유예 타이머를 리셋하고 위치를 발판에 스냅한다.
        /// 접지가 아니면 유예 타이머를 누적하다가 <see cref="StickConfig.ResolveGroundLossGraceDuration"/>
        /// (= max(fallGraceDuration, footholdPollInterval x 배수))를 넘기면 Fall로 강제 전이한다.
        ///
        /// <para>★ 2026-09-01 — 유예 동안에는 <b>몸을 그 자리에 붙잡아 둔다</b>(중력 억제 유지).
        /// 유예의 목적이 "창 열거/원점 읽기가 한 번 튄 것"의 흡수인데, 그동안 몸이 자유낙하해 버리면
        /// 튐이 지나갔을 때 이미 접지 밴드 밖이라 아무 것도 흡수하지 못하기 때문이다. 예외는
        /// "정말 걸어서 모서리를 넘어간" 경우 하나뿐이다
        /// (<see cref="GroundSensor.GroundInfo.WalkedOffPreferredFoothold"/>).</para>
        /// </summary>
        /// <returns>이번 호출로 <b>상태 전이</b>가 발생했으면 true(호출부는 나머지 로직을 생략해야 함).
        /// 2026-09-01 이전에는 그 전이가 항상 Fall이었지만, 이제 Idle/Walk에서는
        /// <see cref="StickmanStateId.GroundLossHang"/>(유예 승격)도 여기서 일어난다.</returns>
        public bool GroundedTick(float deltaTime, GroundSensor.GroundInfo info)
        {
            // ★ 2026-08-30 — 이번 프레임에 이미 접지 유지가 수행됐음을 기록한다. 아래
            // TickGroundKeepingSafetyNet()이 "상태가 스스로 불렀는가"를 이 값으로 판정해 **중복
            // 호출을 하지 않는다**(중복되면 _groundLossTimer가 두 배로 쌓여 유예가 절반으로 줄어든다).
            _groundedTickFrame = Time.frameCount;
            if (info.Grounded)
            {
                // 발판 "획득": 아직 붙잡은 발판이 없는 상태(0)에서 처음 접지하면 그 발판으로 고착한다.
                // 이 한 줄이 없으면 앱 시작 직후처럼 낙하->착지를 한 번도 거치지 않은 구간에서
                // CurrentFootholdHandle이 0으로 남아, 매 프레임 목록 첫 매치를 새로 고르는 예전 동작이
                // 그대로 살아난다 — 새 창이 열릴 때 그 창 상단으로 순간이동하는 증상(사용자 신고 3번)의
                // 잔여 경로다. 획득 이후에는 GroundSensor.Sense()가 이 핸들만 보므로 재선택이 불가능하다.
                if (CurrentFootholdHandle == 0L && info.GroundedFootholdHandle != 0L)
                {
                    CurrentFootholdHandle = info.GroundedFootholdHandle;
                    ReportFootholdChangeIfNeeded("접지 획득(공중을 거치지 않은 최초 접지)");
                }
                _groundLossTimer = 0f;
                _worstLossDeltaTime = 0f;
                // 스냅이 상한을 넘어 "발판을 놓고 Fall"로 갔으면 그 사실을 호출부에 그대로 전달한다
                // (호출부 계약: true = 이번 호출로 Fall 전이가 일어났으니 나머지 로직을 생략하라).
                bool leftGround = SnapToGround(info);
                // ★ 2026-09-01 — 접지가 "이번 프레임에" 확정됐음을 남긴다. 프레임 끝에서
                // ApplyGroundedGravitySuppression()이 이 값 하나로 중력 억제 여부를 정한다
                // (그 함수 문서 참고 — 스냅에 실패해 Fall로 간 프레임은 접지로 치지 않는다).
                if (!leftGround) _groundedConfirmedFrame = Time.frameCount;
                return leftGround;
            }

            _groundLossTimer += deltaTime;
            if (deltaTime > _worstLossDeltaTime) _worstLossDeltaTime = deltaTime;

            // ★ 2026-09-01 — 유예 동안 **몸을 붙잡아 둘지**를 이번 프레임 기준으로 기록한다.
            // 유예의 목적은 "창 열거/원점 읽기가 한 번 튄 것"을 흡수하는 것인데, 그러려면 그동안
            // 몸이 움직이지 않아야 한다 — 튐이 지나갔을 때 이미 접지 밴드 밖으로 떨어져 있으면
            // 유예를 아무리 늘려도 아무 것도 흡수하지 못한다(유예 연장만으로는 H5가 닫히지 않는다).
            // 실측 근거: 폴링 한 주기(0.3초)만 자유낙하해도 1.32유닛 = 허용오차 0.489유닛의 2.7배,
            // 스냅 상한 0.6유닛도 넘는다 → 튐이 사라져도 되돌아갈 수 없다.
            // 붙잡지 **않는** 경우는 둘뿐이다:
            //   · 정말 걸어서 모서리를 넘어갔다(발밑에 실제로 아무것도 없다 — 붙잡으면 공중부양이 된다).
            //     GroundSensor.GroundInfo.WalkedOffPreferredFoothold 참고.
            //   · 애초에 딛고 있던 발판이 없다(핸들 0 = 공중에서 시작한 Idle 등). 붙잡을 "직전 상태"가
            //     없으므로 흡수할 튐도 없고, 붙잡으면 허공에 멈춘 그림만 남는다.
            if (CurrentFootholdHandle != 0L && !info.WalkedOffPreferredFoothold)
            {
                _graceHoldFrame = Time.frameCount;

                // ★ 2026-09-01 (연출) — 붙잡기가 성립한 <b>그 순간</b> 유예를 진짜 상태로 승격한다.
                // 왜 필요한가: 붙잡음 자체는 낙하 수정의 본체라 뺄 수 없는데, 실측에서 IDLE 중에는
                // 그 구간이 10프레임 넘게 화소차 0.00%인 **완전 정지 화면**이 되어 "앱이 멈췄다"로
                // 읽혔다(WALK 중에는 다리가 돌아가 코요테 개그로 읽혔다 — 같은 빌드/물리/시간의
                // 통제 비교다). 포즈를 붙이려면 이 프로젝트 규약("상태 ID 하나로 포즈가 결정된다")상
                // 상태 승격이 맞다. 조건/근거는 States/GroundLossHangState.cs 클래스 문서 참고.
                if (TryEnterGroundLossHang()) return true;
            }
            // ★ 2026-09-01 (근본 원인 2) — 유예를 **창 열거 폴링 간격에서 유도**한다.
            // 예전에는 fallGraceDuration(0.1초)을 그대로 썼는데, 발판 캐시는 footholdPollInterval
            // (0.3초) 동안 고정이라 열거가 한 번만 튀면 그 나쁜 목록이 유예의 3배 동안 유지된다 =
            // 유예가 설계 목적(일시적 튐 흡수)을 원리적으로 수행할 수 없었다(디버거 가설 H5).
            // 숫자를 여기 적지 않는 이유는 폴링 주기를 바꾸면 유예가 자동으로 따라가야 하기 때문이다 —
            // 계산은 StickConfig.ResolveGroundLossGraceDuration() 한 곳에만 있다.
            float grace = Config != null ? Config.ResolveGroundLossGraceDuration() : 0.1f;
            if (_groundLossTimer < grace) return false;

            // 리더 지시: 발판을 잃는 순간을 **사유와 함께** 남긴다(로그가 유일한 판별 수단).
            // ★ 2026-09-01 — 예전에는 "(a)/(b)/(c) 중 하나"라고만 적어서 사용자 신고 "창에서 가끔
            // 갑자기 떨어짐"을 조사할 때 실측 로그에서 사유를 끝내 구분할 수 없었다. 이제 그 자리에서
            // 실제 값을 재서 사유를 하나로 확정하고, 창이 전혀 변하지 않았는데 프레임이 길어져 떨어진
            // 경우(사유 d)까지 분리한다 — GroundSensor.DescribeGroundLoss 문서 참고.
            var footholdsNow = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            Vector2 footNow = Body != null ? Body.position : Vector2.zero;
            string why = GroundSensor.DescribeGroundLoss(MainCamera, footNow, footholdsNow, Config,
                CurrentFootholdHandle, _worstLossDeltaTime, _groundedGravitySuppressionEngagedSinceLastLoss);
            Debug.Log($"[발판상실] 딛고 있던 발판(핸들={CurrentFootholdHandle})이 {grace:F2}초 동안 접지 조건을 " +
                $"만족하지 못해 Fall로 전이합니다 — {why}");

            _groundLossTimer = 0f;
            _worstLossDeltaTime = 0f;
            _groundedGravitySuppressionEngagedSinceLastLoss = false;
            _graceHoldFrame = -1;   // 유예가 끝났다 = 더 이상 붙잡지 않는다(이 프레임부터 실제로 떨어진다).
            Machine.ChangeState(StickmanStateId.Fall);
            return true;
        }

        public void ResetGroundLossTimer() => _groundLossTimer = 0f;

        /// <summary>
        /// 유예 붙잡기가 성립한 프레임에 <see cref="StickmanStateId.GroundLossHang"/>으로 승격한다.
        ///
        /// <para><b>Idle/Walk에서만 승격하는 이유</b>는 States/GroundLossHangState.cs 클래스 문서에
        /// 실패 시나리오까지 적어 뒀다. 요약: 스펙터클 상태에서까지 전이시키면 창 열거가 한 번 튈
        /// 때마다 진행 중이던 연출이 취소된다 — 유예가 흡수하려던 사건이 유예 때문에 눈에 보이는
        /// 사고로 바뀐다. 그 상태들은 포즈를 스스로 소유하므로 "같은 상태인데 포즈가 두 가지" 문제도
        /// 애초에 생기지 않고, 붙잡음은 위 <c>_graceHoldFrame</c>이 예전 그대로 담당한다.</para>
        ///
        /// <para>스위치(<see cref="StickConfig.groundLossHangStateEnabled"/>)를 끄면 이 승격만
        /// 사라져 2026-09-01 오전 거동으로 정확히 되돌아간다(붙잡음/유예 길이는 그대로).</para>
        /// </summary>
        /// <returns>실제로 전이시켰으면 true.</returns>
        private bool TryEnterGroundLossHang()
        {
            if (Machine == null) return false;
            if (Config != null && !Config.groundLossHangStateEnabled) return false;

            StickmanStateId id = Machine.CurrentStateId;
            if (id != StickmanStateId.Idle && id != StickmanStateId.Walk) return false;

            Machine.ChangeState(StickmanStateId.GroundLossHang);
            return true;
        }

        // ================================================================================
        // ★★ 접지 유지 안전망 (2026-08-30, 디버거 — 사용자 신고 "갑자기 독 아래로 떨어지면서
        //    관절이 이상하게 꺾임"의 근본 원인 차단)
        // ================================================================================
        // 무엇이 문제였나(PlayMode 실측 재현, Tests/PlayMode/DockSinkholeRegressionTests.cs):
        //   Dock/타 창 상단은 **논리 발판일 뿐 물리 콜라이더가 없다.** 그래서 매 프레임 접지 스냅
        //   (GroundedTick -> SnapToGround)을 부르지 않는 상태에 들어가는 순간, 캐릭터는 서 있던 그
        //   자리에서 자유낙하해 화면 최하단 물리 바닥(PhysicsGround)에 전속력으로 부딪힌다. 그 충격량은
        //   Dock 단차 1.64유닛만으로도 v = sqrt(2*9.81*3*1.64) = 9.8 > ragdollForceThreshold(8)이라
        //   **RAGDOLL로 강제 전이**되고, 캐릭터는 관절이 꺾인 채 Dock 아래에 널브러진다.
        //   실측 전이 추적(Attack 진입 1건):
        //     Idle->Attack 몸=(0.000,-10.167) -> Attack->Ragdoll(강제) 몸=(0.000,-11.886)
        //     -> Ragdoll->Getup -> Getup->Idle -> Idle->Fall -> (6초 뒤) 강제 복귀
        //
        // 왜 각 상태에 GroundedTick을 하나씩 더 넣지 않는가:
        //   그게 정확히 2026-08-29 라운드가 한 일이고(WindowTheft/TimedSpectacle에만 추가),
        //   그때 Attack/Getup/BattleMinigame(2026-09-02 삭제)이 빠져 이번 신고로 돌아왔다. 이 프로젝트에서 반복된
        //   실패 유형("안전장치를 한 곳만 고치고 같은 패턴의 다른 경로에는 안 넣기")이므로, 목록의
        //   방향을 뒤집는다: **공중/자기구동 상태만 제외하고 나머지는 전부 기본 보호**한다.
        //   앞으로 새 상태를 추가하는 사람이 아무것도 하지 않아도 안전한 쪽이 기본값이 된다.
        //   (TickPose가 "상태 ID 하나로 포즈가 결정된다"를 한 곳에 모은 것과 같은 설계다.)
        //
        // 중복 호출은 하지 않는다 — 상태가 이미 자기 Tick에서 GroundedTick을 불렀으면
        // _groundedTickFrame이 이번 프레임이라 그대로 반환한다(그래서 기존 상태들의 거동은 100% 그대로).

        /// <summary>
        /// 이 상태 ID가 **접지 유지를 스스로 책임지는가**(= 안전망이 손대면 안 되는가).
        /// 공중에 있거나(Jump/Fall/ThrowTumble) 몸 위치를 스스로 구동하거나(LedgeHang/ParkourClimb/
        /// Dragged/RodeoCursor/Runaway) 전신을 물리에 위임한(Ragdoll) 상태들이다.
        /// 여기 없는 상태는 전부 안전망의 보호를 받는다.
        ///
        /// <para>★ <see cref="StickmanStateId.GroundLossHang"/>이 <b>일부러 여기 없는</b> 이유:
        /// 이 목록은 접지 안전망뿐 아니라 <see cref="ApplyGroundedGravitySuppression"/>의 제외 목록이기도
        /// 하다. 유예 상태를 여기 넣으면 <b>중력 억제가 걸리지 않아 몸이 자유낙하한다</b> — 붙잡음이
        /// 사라지는 것이고, 그건 이번 수정이 막으려는 버그 그 자체다. 안전망 쪽 중복 호출은 그 상태가
        /// 자기 Tick에서 GroundedTick을 부르므로 <c>_groundedTickFrame</c>으로 이미 걸러진다.</para>
        /// </summary>
        public static bool IsGroundKeepingSelfManaged(StickmanStateId id)
        {
            switch (id)
            {
                case StickmanStateId.Jump:          // 상승/하강 — 접지 스냅을 걸면 점프가 사라진다.
                case StickmanStateId.Fall:          // FallState가 스윕 교차로 착지를 직접 확정한다.
                case StickmanStateId.ThrowTumble:   // 공중 회전 비행.
                case StickmanStateId.Ragdoll:       // 전신 물리 위임(아키텍처 0절).
                case StickmanStateId.Dragged:       // 커서 추종(유저가 들고 있다).
                case StickmanStateId.RodeoCursor:   // 커서 위에 올라타 있다.
                case StickmanStateId.LedgeHang:     // 모서리에 매달려 몸 위치를 직접 보간한다.
                case StickmanStateId.ParkourClimb:  // 턱 위로 몸 위치를 직접 보간한다.
                case StickmanStateId.Runaway:       // 은신처로 순간이동/은닉한다.
                    return true;
                default:
                    return false;
            }
        }

        // ================================================================================
        // ★★ 수평 표류 안전망 — 위 세로축 어법을 **가로축에 그대로 옮긴 쌍둥이**
        //    (2026-09-02, 사용자 신고 "그라피티 그릴때 윈도우버전은 캐릭터가 미끄러져이동함")
        // ================================================================================
        // 왜 마찰이 안 먹었나(디버거 실측으로 확정, 반증된 가설은 다시 파지 않는다):
        //   바로 아래 ApplyGroundedGravitySuppression()이 접지 중 gravityScale=0으로 만든다 =
        //   **수직항력 N이 0**이다. 쿨롱 마찰 상한은 μN이므로 N=0이면 마찰 상한도 0이다.
        //   즉 걷다가 연출 상태로 들어가면 잔여 수평속도가 **감속 없이 등속으로** 연출이 끝날
        //   때까지 유지된다. 실측: 3.4초 동안 192pt 이동, 감속 -0.68 pt/s^2(사실상 0).
        //   같은 몸·같은 Dock 콜라이더에서 중력 ON(Fall)은 11.77 u/s^2로 0.061유닛 만에 서는데,
        //   중력 억제(Graffiti)는 3.94유닛을 가속도 0으로 갔다. 대조군(Idle 진입)은 8초에 0.5pt다
        //   — IdleState.Enter()가 v.x를 **한 번** 0으로 대입하기 때문이고, 그 한 번뿐이라
        //   Idle 도중 외력이 들어오면 그때부터는 Idle도 똑같이 미끄러진다.
        //
        // 이것은 중력 억제의 부작용이지 그 수정의 결함이 아니다 — 세로 적분을 막는 것이 그 수정의
        // 본체이고(프레임 길이와 독립인 유일한 처방), 마찰을 되살리려면 중력을 되살려야 하므로
        // 원래 버그가 돌아온다. 그래서 **세로에서 한 일을 가로에도 똑같이 한다**: 상태가 스스로
        // 소유하지 않는 축은 안전망이 대신 0으로 유지한다.
        //
        // ★ 플랫폼 중립 결함이다. Windows 로그에 "[화면클램프] ... 상태=Graffiti"로 화면 끝까지
        //   밀린 증거가 있고, macOS에서도 그대로 재현됐다. 이 파일은 플랫폼 분기가 없다.

        /// <summary>
        /// 이 상태 ID가 <b>수평 이동을 스스로 소유하는가</b>(= 수평 표류 안전망이 손대면 안 되는가).
        /// <see cref="IsGroundKeepingSelfManaged"/>의 가로축 쌍둥이이며, 어법도 같다 —
        /// <b>허용목록이 아니라 제외목록</b>이다. 여기 없는 상태는 전부 안전망의 보호를 받으므로,
        /// 새 상태를 추가하는 사람이 아무것도 하지 않아도 "제자리 연출인데 미끄러진다"가 기본으로 막힌다.
        ///
        /// <para>★★ <b>새 상태를 여기 추가하려는 사람이 반드시 읽어야 할 계약</b>(2026-09-02, 모션
        /// 담당이 찾은 구멍): 상태 ID 단위 목록만으로는 <b>한 상태 안에서 소유권이 바뀌는 상태</b>를
        /// 표현할 수 없다. 접근 페이즈에는 목표 지점까지 걸어가고 그 뒤로는 제자리에 서는 형태
        /// (활쏘기, 그리고 앞으로 올 곡괭이질·낚시·닦기·쓰다듬기)가 그렇다.
        /// <list type="bullet">
        ///   <item>여기 넣지 <b>않으면</b>(false) 안전망이 접근 보행 속도를 매 프레임 지워 <b>영원히
        ///         도착하지 못한다</b>.</item>
        ///   <item>여기 넣으면(true) 안전망이 손을 떼므로 <b>제자리 페이즈의 표류가 안 막힌다</b>.</item>
        /// </list>
        /// 처방은 <b>true로 넣고, 그 상태가 제자리 페이즈 동안 매 프레임 스스로 수평 속도를 0으로
        /// 재확인</b>하는 것이다(한 번만 대입하고 끝내면 안 된다 — 위 Idle 대조군이 그 반례다).
        /// 참고 구현은 <see cref="States.ArcheryState"/>의 비-Approach 분기와
        /// <see cref="States.LandingCrouchState"/>이며, 둘 다 지수 감쇠로 **매 프레임** 죽인다.
        /// 지수 감쇠를 쓰는 이유는 이 안전망이 브레이크 박자를 두는 이유와 같다(아래 문서).</para>
        ///
        /// <para>★ <see cref="StickmanStateId.LandingCrouch"/>가 여기 있는 이유(승인 목록에서 빠져
        /// 있던 항목 — 리더에게 보고함): <see cref="States.LandingCrouchState"/>는 이미
        /// <c>landingCrouchHorizontalDamping</c>(12/초)으로 매 프레임 수평 속도를 죽이고 있고, 그
        /// 코드 주석이 "0으로 즉시 대입하지 않는 이유: 공중에서의 수평 이동이 착지 순간 뚝 끊기면
        /// 오히려 더 부자연스럽다"고 명시한다. 안전망은 상태 Tick <b>직후</b>에 도므로 여기서 빼면
        /// 그 튜닝된 감쇠가 통째로 죽은 코드가 되고 착지 박자가 바뀐다.</para>
        ///
        /// <para>★ <see cref="StickmanStateId.GroundLossHang"/>이 여기 <b>있는</b> 이유는
        /// <see cref="IsGroundKeepingSelfManaged"/>에서 <b>없는</b> 이유와 정반대다: 세로는 붙잡아야
        /// 하지만(중력 억제 대상), 가로는 들고 온 속도를 그대로 유지해야 한다 — 허공을 계속 걸어가는
        /// 그 그림이 이 상태의 존재 이유(코요테 개그)다.</para>
        /// </summary>
        public static bool IsHorizontalMotionSelfManaged(StickmanStateId id)
        {
            switch (id)
            {
                case StickmanStateId.Walk:          // 이동 의도를 매 프레임 속도로 바꾼다.
                case StickmanStateId.Jump:          // 도약 수평 성분을 스스로 싣는다.
                case StickmanStateId.Fall:          // 발 떼기 이송/낙하 궤적을 스스로 소유한다.
                case StickmanStateId.ThrowTumble:   // 던져진 포물선.
                case StickmanStateId.Ragdoll:       // 전신 물리 위임(아키텍처 0절).
                case StickmanStateId.Dragged:       // 커서 추종(유저가 들고 있다).
                case StickmanStateId.RodeoCursor:   // 커서 위에 올라타 있다.
                case StickmanStateId.LedgeHang:     // 몸 위치를 직접 보간한다.
                case StickmanStateId.ParkourClimb:  // 몸 위치를 직접 보간한다.
                case StickmanStateId.Runaway:       // 은신처로 순간이동/은닉한다.
                case StickmanStateId.GroundLossHang:// 들고 온 수평 속도를 유지해야 코요테 개그가 산다.
                case StickmanStateId.Archery:       // 접근 보행 -> 제자리(위 계약: 제자리 페이즈를 스스로 죽인다).
                case StickmanStateId.LandingCrouch: // 착지 감쇠를 스스로 소유한다(위 계약과 같은 형태).
                    return true;
                default:
                    return false;
            }
        }

        // ================================================================================
        // ★★ 방향 부호(facing) 소유권 — 위 두 축(세로 접지 / 가로 이동)에 이어지는 **세 번째 축**
        //    (2026-09-02, 페르소나 소은 실측 + 리더 코드 확인: "활쏘기 접근 중 캐릭터가 뒷걸음친다")
        // ================================================================================
        // 무엇이 고장났나(소스에서 확정, 추측 아님):
        //   StickmanAgent.Update의 순서는 _autoWander.Tick → _machine.Tick → _blackboard.TickPose다.
        //   활쏘기 접근 페이즈에서 ArcheryState.TickApproach()가 진행 방향으로 SetFacingSign(dir)을
        //   부르지만, **바로 뒤에 도는 TickPose가 배회 AI의 MoveInputX 부호로 그 값을 덮어썼다.**
        //   활쏘기는 과녁 반대쪽으로 한 걸음 물러선 자리에서 쏘므로(ArcheryDirector.BackStepRatio)
        //   접근 방향은 **과녁의 반대쪽**인데, 배회 AI는 그 사실을 모른 채 직전까지 걷던 방향을 계속
        //   내보낸다. 그 둘이 어긋난 프레임이 곧 "발은 왼쪽으로 가는데 몸은 오른쪽을 보는" 그림
        //   — 유저 눈에는 미끄러짐(문워크)과 같은 계열이다.
        //
        // 왜 상태 ID 목록인가: 위 두 안전망과 같은 어법을 쓴다. FacingLocked(동적 플래그)만으로는
        //   "이 상태는 원래 자기가 방향을 정한다"를 표현할 수 없다 — 그 플래그는 활쏘기의 **조준
        //   구간**을 위한 것이고, 접근 구간에는 일부러 꺼져 있어야 한다(걸으면서 방향이 바뀐다).
        //   두 개념을 한 플래그에 겹치면 "고정"의 의미가 넓어져 다른 연출이 이상해진다(리더 경고).

        /// <summary>
        /// 이 상태 ID가 <b>바라보는 방향 부호를 스스로 소유하는가</b>(= <see cref="TickPose"/>가 배회
        /// AI의 이동 의도로 방향을 덮으면 안 되는가).
        /// <see cref="IsGroundKeepingSelfManaged"/> / <see cref="IsHorizontalMotionSelfManaged"/>의
        /// 세 번째 축이며, 어법도 같다 — <b>제외목록</b>이라 여기 없는 상태는 전부 배회 AI가 방향을 준다.
        ///
        /// <para>★ <b>멤버십 규칙은 하나다</b>: <c>_blackboard.SetFacingSign(...)</c>을 스스로 부르는
        /// 상태는 반드시 여기 들어와야 한다. 안 넣으면 그 호출이 같은 프레임 뒤쪽 <c>TickPose</c>에
        /// 덮여 <b>죽은 코드</b>가 된다(활쏘기 접근 페이즈에서 실제로 일어난 일).
        /// 이 규칙은 <c>Tests/EditMode/HorizontalMotionOwnershipContractTests</c>가 소스를 읽어
        /// 전수 감사하므로, 새 상태를 만드는 사람이 기억하지 못해도 러너가 말해 준다.</para>
        ///
        /// <para>★ <b>여기 넣으면 안 되는 것</b>: 수평 이동을 배회 AI의 <c>MoveInputX</c>에서 그대로
        /// 유도하는 상태(Walk/Jump/Fall/LedgeHang 등)다. 그런 상태에서는 이동 의도가 곧 진행 방향이라
        /// 배회 AI가 주는 부호가 <b>정답</b>이고, 여기 넣으면 그 방향 갱신이 통째로 멎어 캐릭터가 한쪽만
        /// 보고 걷는다. 즉 "수평 자기소유 ⇒ 방향 자기소유"는 <b>성립하지 않는다</b>(Walk가 반례다).
        /// 판정 기준은 "수평을 소유하는가"가 아니라 <b>"이동 방향을 MoveInputX가 아닌 다른 곳에서
        /// 정하는가"</b>이다.</para>
        /// </summary>
        public static bool IsFacingSelfManaged(StickmanStateId id)
        {
            switch (id)
            {
                // 접근 보행은 목표 X(과녁 반대쪽 한 걸음)를 향하고, 도착 후에는 과녁을 겨눈다.
                // 두 구간 모두 방향의 근거가 배회 AI가 아니라 이 상태의 좌표 계산이다.
                case StickmanStateId.Archery:
                // 오르는 벽을 바라본다(ParkourClimbState.Enter의 SetFacingSign). 등지고 오르면
                // 등반 포즈의 손이 뒤로 뻗는다 — 그 상태의 주석이 이유를 적어 두었다.
                case StickmanStateId.ParkourClimb:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 이번 프레임에 <b>배회 AI의 이동 의도가 방향 부호를 정해도 되는가</b>.
        /// <see cref="TickPose"/>의 실제 판정식이며, 테스트도 이 함수를 그대로 읽는다 —
        /// 같은 판정을 두 곳에서 따로 적으면 반드시 어긋난다(이 저장소가 두 번 겪은 실패 유형).
        /// </summary>
        public bool WanderIntentMayDriveFacing(StickmanStateId id)
            => !FacingLocked && !IsFacingSelfManaged(id);

        /// <summary>
        /// StickmanAgent.Update()가 상태 Tick **직후** 1회 호출하는 접지 유지 안전망(위 문서 참고).
        /// 상태가 이미 GroundedTick()을 불렀거나, 그 상태가 접지를 스스로 관리하는 종류이거나,
        /// StickConfig.groundKeepingSafetyNetEnabled가 꺼져 있으면 아무 것도 하지 않는다.
        /// </summary>
        public void TickGroundKeepingSafetyNet(float deltaTime)
        {
            if (Machine == null || Body == null) return;
            if (Config != null && !Config.groundKeepingSafetyNetEnabled) return;
            if (_groundedTickFrame == Time.frameCount) return;      // 상태가 이미 스스로 했다.

            StickmanStateId id = Machine.CurrentStateId;
            if (IsGroundKeepingSelfManaged(id)) return;

            GroundSensor.GroundInfo info = SenseGround();
            if (CheckScreenBoundsOrFall(info)) return;
            if (!GroundedTick(deltaTime, info)) return;

            // 안전망이 실제로 개입해 Fall로 보낸 경우만 남긴다(이산 사건이라 로그가 넘치지 않는다).
            Debug.Log($"[접지안전망] 상태 {id}가 접지 유지를 하지 않아 안전망이 대신 처리했고, " +
                $"발판을 잃어 {Machine.CurrentStateId}로 전이시켰습니다. 이 안전망이 없으면 이 상태에 머무는 동안 " +
                "논리 발판(Dock/창 상단, 물리 콜라이더 없음) 위에서 그대로 자유낙하해 화면 최하단 " +
                "물리 바닥에 전속력으로 부딪히고 RAGDOLL이 됩니다(2026-08-30 신고의 근본 원인).");
        }

        // 이번 표류 구간에서 쓰는 감속률(월드 유닛/초^2). 구간이 시작될 때 진입 속도에서 유도하며,
        // 구간이 끊기면(자기소유 상태로 나갔다 / 프레임이 건너뛰었다) 다음 구간에서 다시 유도한다.
        private float _horizontalBrakeRate;
        private int _horizontalBrakeFrame = -1;

        /// <summary>이번 프레임에 안전망이 실제로 쓴 감속률(유닛/초^2). 진단/테스트가 "브레이크가
        /// 걸렸는가 / 감속률이 진입 속도에서 유도됐는가"를 로그가 아니라 값으로 단언하는 데 쓴다.
        /// <para>0 = 개입하지 않았거나 <b>즉시 대입 모드</b>(정지 박자가 0 이하)다 — 후자는 램프가
        /// 존재하지 않으므로 보고할 감속률 자체가 없다.</para></summary>
        public float HorizontalDriftBrakeRate => _horizontalBrakeFrame == Time.frameCount ? _horizontalBrakeRate : 0f;

        /// <summary>
        /// StickmanAgent.Update()가 상태 Tick <b>직후</b>(접지 안전망 바로 다음) 1회 호출하는 수평 표류
        /// 안전망. 위 <see cref="IsHorizontalMotionSelfManaged"/> 문서의 반대편 절반이다.
        ///
        /// <para><b>왜 상태 Tick 직후인가</b>: 세로축 안전망과 완전히 같은 계약이다 — 상태가 이 프레임에
        /// 스스로 쓴 값을 덮지 않기 위해서다. 상태보다 <b>먼저</b> 돌면 상태가 그 뒤에 다시 속도를
        /// 세우므로 안전망이 아무 일도 하지 못하고, 프레임 맨 끝(중력 억제 자리)으로 미루면 그 사이의
        /// 화면 클램프/구조 회수가 이미 낡은 속도를 보고 판단하게 된다.</para>
        ///
        /// <para><b>왜 접지 안전망보다 뒤인가</b>: 접지 안전망이 발판 상실을 감지해 Fall로 보낼 수 있고,
        /// Fall은 수평을 스스로 소유한다(발 떼기 이송). 순서를 뒤집으면 그 이송 속도를 이 안전망이
        /// 먼저 지운 다음 Fall이 시작되어, 뛰어내리기가 제자리 낙하가 된다.</para>
        ///
        /// <para><b>즉시 0이 아니라 정지 박자를 두는 이유</b>(모션 담당 권고 0.14초, 리더 승인 범위 안의
        /// 구현 판단): 이 저장소는 같은 질문에 이미 두 번 답했고 두 번 다 "즉시 대입하지 않는다"였다
        /// (<c>landingCrouchHorizontalDamping</c> 12/초, <c>archeryHorizontalDamping</c> 14/초 —
        /// 그 툴팁이 "뚝 끊기면 오히려 부자연스럽다"고 적어 두었다). 게다가 포즈는
        /// <c>TickPose</c>의 지수 스무딩으로 수십 ms에 걸쳐 녹아드는데 몸만 한 프레임에 정지하면
        /// 다리가 계속 도는 채로 몸이 얼어붙는 그림이 된다.
        /// 대신 <b>지수 감쇠가 아니라 선형 램프</b>를 쓴다: 지수는 원리적으로 0에 도달하지 못해
        /// "안전망이 표류를 0으로 만든다"는 보증을 줄 수 없지만, 선형 램프는
        /// <c>horizontalDriftBrakeSeconds</c> 안에 <b>정확히 0</b>에 도달하고 그 뒤로는 0에 못박힌다.
        /// 총 표류 거리는 진입 속도 v0에 대해 0.5 x v0 x 0.14초로 <b>상한이 닫혀 있다</b>
        /// (걷기 속도에서 약 0.18유닛 = 배포 환산 약 8.5pt. 실측 표류 192pt의 1/22).</para>
        ///
        /// <para>감속률을 진입 속도에서 <b>유도</b>하고 상수로 적지 않는 이유: 숫자를 pt/s^2로 박으면
        /// 캐릭터 배율/화면 기하가 바뀔 때 정지 박자가 함께 바뀐다. 시간(초)을 고정하면 어떤 속도로
        /// 들어와도 같은 박자로 선다. 구간 중 외력으로 속도가 커지면 감속률도 함께 올려(최댓값 유지)
        /// 박자 상한을 지킨다.</para>
        ///
        /// <para><see cref="StickConfig.horizontalDriftSafetyNetEnabled"/>가 꺼져 있으면 아무 것도 하지
        /// 않는다(네거티브 컨트롤). <c>horizontalDriftBrakeSeconds</c>가 0 이하이면 즉시 0으로 대입한다.</para>
        /// </summary>
        public void TickHorizontalDriftSafetyNet(float deltaTime)
        {
            if (Machine == null || Body == null) return;
            if (Config != null && !Config.horizontalDriftSafetyNetEnabled) { _horizontalBrakeFrame = -1; return; }

            if (IsHorizontalMotionSelfManaged(Machine.CurrentStateId)) { _horizontalBrakeFrame = -1; return; }

            Vector2 v = Body.linearVelocity;
            int frame = Time.frameCount;

            float brakeSeconds = Config != null ? Config.horizontalDriftBrakeSeconds : 0.14f;
            if (brakeSeconds <= 0f)
            {
                // 즉시 대입 모드. 구간을 열지 않는다(_horizontalBrakeFrame = -1) — 열어 두면 박자를
                // 다시 양수로 되돌린 순간 낡은 무한대 감속률을 물려받아 램프가 영영 안 생긴다.
                _horizontalBrakeFrame = -1;
                _horizontalBrakeRate = 0f;
                if (v.x != 0f) { v.x = 0f; Body.linearVelocity = v; }
                return;
            }

            // 새 표류 구간인가 — 직전 프레임에 개입하지 않았으면(자기소유 상태였거나 Suspend로 건너뛰었거나)
            // 지금 속도에서 감속률을 새로 유도한다.
            if (_horizontalBrakeFrame != frame - 1) _horizontalBrakeRate = 0f;
            _horizontalBrakeFrame = frame;

            float needed = Mathf.Abs(v.x) / brakeSeconds;
            if (needed > _horizontalBrakeRate) _horizontalBrakeRate = needed;   // 구간 중 외력이 들어와도 박자 상한을 지킨다.
            if (_horizontalBrakeRate <= 0f) return;                            // 이미 정확히 0이다.

            float next = Mathf.MoveTowards(v.x, 0f, _horizontalBrakeRate * deltaTime);
            if (next == v.x) return;
            v.x = next;
            Body.linearVelocity = v;
        }

        // ================================================================================
        // ★★ 접지 중 중력 억제 (2026-09-01 — 사용자 신고 "캐릭터가 창에서 가끔 갑자기 떨어짐"의 근본 원인 1)
        // ================================================================================
        // 무엇이 문제였나(디버거 조사로 확정, 반증된 가설은 다시 파지 않는다):
        //   창/Dock 상단은 **논리 발판일 뿐 물리 콜라이더가 없다.** 그래서 "서 있기"는 매 프레임
        //   SnapToGround() 한 번으로만 유지되는데, **그 사이에도 중력은 계속 적분된다.**
        //   한 프레임의 자유낙하가 접지 허용오차(groundSnapTolerance)를 넘으면 그 프레임이 끝나는
        //   순간 GroundSensor.Sense()가 Grounded=false를 내고, 그 한 프레임이 유예까지 통째로
        //   소진하므로 **단 한 프레임으로 낙하가 확정된다**(창은 1픽셀도 움직이지 않았는데).
        //   임계 프레임시간은 GroundSensor.ComputeGroundLossFrameTimeThreshold()가 계산한다 —
        //   배포 형상에서 약 182ms. 그런데 절전 프레임페이싱 티어 DisplayOff는 4fps(=250ms/프레임),
        //   엔진 최대 timestep은 333ms다. 즉 절전 등급이나 히치 한 번이면 **상시** 성립한다.
        //
        // 왜 "스냅을 더 자주/더 세게"가 아니라 중력을 끄는가:
        //   스냅은 사후 보정이라 원리적으로 프레임 길이에 진다. 반면 접지 중 gravityScale=0은
        //   **세로 적분 자체를 0으로 만들기 때문에 프레임이 아무리 길어도 낙하량이 0이다.**
        //   즉 이 처방만 프레임 길이와 독립이다.
        //
        // ★ 가장 큰 위험은 반대쪽이다 — "중력이 꺼진 채 갇히기". 그래서 벗겼다 다시 얹는다:
        //   StickmanAgent.Update()가 상태 Tick **직전**에 ReleaseGroundedGravitySuppression()으로
        //   무조건 원복하고, 그 프레임의 모든 처리가 끝난 **맨 끝**에 ApplyGroundedGravitySuppression()이
        //   다시 얹는다(잉크 바닥 클리어런스 리프트와 완전히 같은 관례). 그 결과:
        //     · 상태 로직/연출 코드가 gravityScale을 읽는 시점에는 **언제나 진짜 값**이다
        //       (ThrowTumbleState가 포물선을 계산할 때 0을 읽는 사고가 원천 차단된다).
        //     · 어떤 경로로 상태가 바뀌든(강제 인터럽트/외부 ChangeState/컴포넌트 비활성) 억제는
        //       다음 프레임 맨 앞에서 반드시 풀린다. 억제가 영구히 남을 수 있는 코드 경로가 없다.
        //     · 다시 얹는 조건은 **이번 프레임에 (a) 접지가 확정됐거나 (b) 아직 유예 중이며 몸을
        //       붙잡아 둬야 하고, 그 상태가 접지를 스스로 관리하지 않는 종류일 때**뿐이다.
        //       Jump/Fall/Ragdoll/ThrowTumble/Dragged 등은 애초에 대상이 아니다.
        //       (b)가 필요한 이유는 GroundedTick()의 _graceHoldFrame 주석에 있다 — 유예는 몸이
        //       움직이지 않을 때에만 "일시적 튐"을 흡수할 수 있다.

        // 억제 직전에 백업해 둔 원래 gravityScale. NaN = 억제 중 아님(0도 유효한 원래 값일 수 있으므로
        // 0을 "억제 아님" 표식으로 쓰지 않는다).
        private float _gravityScaleBeforeSuppression = float.NaN;

        /// <summary>지금 이 프레임에 접지 중력 억제가 걸려 있는지(진단/테스트용).</summary>
        public bool IsGroundedGravitySuppressed => !float.IsNaN(_gravityScaleBeforeSuppression);

        /// <summary>
        /// 얹어 둔 중력 억제를 벗긴다. StickmanAgent.Update()가 <b>상태 Tick보다 먼저</b> 무조건 부른다.
        /// 멱등이며, 억제 중이 아니면 아무 일도 하지 않는다.
        /// </summary>
        public void ReleaseGroundedGravitySuppression()
        {
            if (float.IsNaN(_gravityScaleBeforeSuppression)) return;
            if (Body != null) Body.gravityScale = _gravityScaleBeforeSuppression;
            _gravityScaleBeforeSuppression = float.NaN;
        }

        /// <summary>
        /// 이번 프레임에 접지가 확정됐으면 중력을 눌러 둔다(위 섹션 문서 참고).
        /// StickmanAgent.Update()가 <b>다른 모든 처리가 끝난 뒤 맨 마지막</b>에 부른다 —
        /// 그래야 이 프레임의 최종 상태(강제 인터럽트/화면 클램프/구조 회수까지 반영된 결과)를 보고
        /// 판단할 수 있고, 그 판단이 곧 <b>다음 FixedUpdate</b>에 적용된다(Unity 프레임 순서상
        /// FixedUpdate는 Update보다 앞이므로, 여기서 세운 값이 다음 물리 스텝을 지배한다).
        /// </summary>
        public void ApplyGroundedGravitySuppression()
        {
            if (Body == null || Machine == null) return;
            if (Config != null && !Config.groundedGravitySuppressionEnabled) return;
            if (!float.IsNaN(_gravityScaleBeforeSuppression)) return;      // 이미 얹혀 있다(멱등).

            // 이번 프레임에 GroundedTick()이 (a) "접지 확정"으로 끝났거나 (b) "아직 유예 중이며 몸을
            // 붙잡아 둬야 한다"고 판단했는가. 스냅 상한 초과로 발판을 놓은 프레임은 (a)에 들어오지
            // 않는다(GroundedTick 참고).
            int frame = Time.frameCount;
            if (_groundedConfirmedFrame != frame && _graceHoldFrame != frame) return;

            // 접지를 스스로 관리하는 상태(공중/자기구동/전신물리)는 대상이 아니다 —
            // 목록을 여기 다시 적지 않는 것이 핵심이다(안전망과 같은 단일 소스).
            if (IsGroundKeepingSelfManaged(Machine.CurrentStateId)) return;

            _gravityScaleBeforeSuppression = Body.gravityScale;
            Body.gravityScale = 0f;
            _groundedGravitySuppressionEngagedSinceLastLoss = true;

            // 잔여 세로 속도도 함께 지운다. 중력이 0인데 속도가 남아 있으면 등속으로 미끄러져
            // 오히려 밴드를 벗어난다(SnapToGround는 하강 속도만 지우므로 상승 잔여분이 남을 수 있다).
            Vector2 v = Body.linearVelocity;
            if (v.y != 0f)
            {
                v.y = 0f;
                Body.linearVelocity = v;
            }
        }

        // ================================================================================
        // ★★ 잉크 바닥 클리어런스 (2026-08-31, 디버거가 원인 확정한 GETUP 발판 관통)
        // ================================================================================
        // 이 프로젝트의 접지 규약은 "**루트 원점 = 발바닥**"인데, 그 규약이 참인 것은 **서 있을 때뿐**이다.
        // RAGDOLL에서 막 넘어온 GETUP은 몸이 아직 누워 있는데(루트 회전 최대 |90|도 이상) 접지 스냅이
        // 루트 Y를 발판 상단에 못박으므로, 회전한 몸의 반대편 파츠가 기하학적으로 발판 아래로 갈 수밖에
        // 없다. RAGDOLL 구간에는 콜라이더가 이걸 막아 주지만(실측: 발판 상단 아래 최대 4.6pt에서 멈춤)
        // GETUP은 팔다리가 Kinematic이라 그 방어가 사라진다 — 실측 최악 **발판 상단 아래 20.5pt**.
        //
        // 처방은 상수를 키우는 것이 **아니다**(안전망 8pt를 21pt로 올리면 서 있을 때 발이 19pt = 키의
        // 27%만큼 떠서 사용자가 세 번 신고한 "떠 있다"가 정면 재발한다). 대신 Dock 라운드와 같은
        // **유도값**을 쓴다: "지금 이 포즈의 최저 잉크가 루트 원점 높이에 정확히 닿는 데 필요한 만큼"만
        // 들어 올린다. 자세가 정착하면(progress->1) 그 필요량이 저절로 0이 되므로 **새 상수가 없다**
        // (DockGeometry.ResolveEdgeStopDistance / ResolveParkourMantleInset과 같은 관례).

        /// <summary>
        /// 지금 포즈에서 "잉크가 루트 원점(=접지선) 아래로 내려간 깊이"(월드 유닛, 항상 0 이상).
        /// 이만큼 루트를 들어 올리면 어떤 파츠도 발판 아래로 내려가지 않는다.
        ///
        /// <para>계산이 <b>루트 상대</b>라는 점이 중요하다 — 발판 Y도, 화면 좌표도 입력이 아니다.
        /// 그래서 호출부가 접지 스냅 전에 부르든 후에 부르든 결과가 같고(스냅은 루트와 자식을 통째로
        /// 옮기므로 상대 관계가 변하지 않는다), 물리 좌표(Rigidbody2D.position)와 렌더 좌표
        /// (Transform.position)가 한 프레임 어긋나 있어도(이 프로젝트는 autoSyncTransforms가 꺼져 있다)
        /// 두 좌표를 섞지 않는다.</para>
        ///
        /// <para>몸(팔다리/몸통/머리)은 StickmanPoseAnimator가, 그 바깥의 잉크(모자/망토 등)는
        /// <see cref="ICharacterInkExtentProvider"/> 구현체가 각자 답한다 — 소비자인 여기가 부품 목록을
        /// 들고 있지 않으므로 DLC로 부품이 늘어도 이 함수는 그대로다.</para>
        /// </summary>
        /// <returns>재는 데 필요한 것(포즈 드라이버/렌더러)이 하나도 없으면 false.</returns>
        public bool TryComputeInkDropBelowRoot(out float dropWorld)
        {
            dropWorld = 0f;
            if (Body == null) return false;

            StickmanPoseAnimator pose = GetPoseAnimator();
            if (pose == null || !pose.HasLimbs) return false;

            StickmanMetrics metrics = Metrics;
            float headRadius = metrics != null ? metrics.HeadRadius : 0f;
            if (!pose.TryGetLowestBodyInkWorldY(headRadius, out float lowestY)) return false;

            ICharacterInkExtentProvider[] providers = InkExtentProviders;
            for (int i = 0; i < providers.Length; i++)
            {
                if (providers[i] == null) continue;
                if (!providers[i].TryGetLowestInkWorldY(out float y)) continue;
                if (y < lowestY) lowestY = y;
            }

            float rootY = Body.transform.position.y; // 잉크 좌표와 같은 공간(렌더 좌표)에서만 뺀다.
            dropWorld = Mathf.Max(0f, rootY - lowestY);
            return true;
        }

        // 지금 루트에 얹혀 있는 클리어런스 리프트(월드 유닛). **그 프레임의 그림에만 존재하고**
        // 다음 프레임 맨 앞(StickmanAgent.Update가 상태 Tick보다 먼저 부르는 ReleaseInkFloorClearanceLift)
        // 에서 반드시 벗겨진다 — 아래 TickInkFloorClearance의 "왜 벗겼다 다시 얹는가" 참고.
        private float _inkClearanceLift;

        /// <summary>실측/디버그용 — 지금 프레임에 루트에 얹혀 있는 클리어런스 리프트(월드 유닛).</summary>
        public float InkClearanceLiftWorld => _inkClearanceLift;

        /// <summary>
        /// 얹어 둔 리프트를 벗긴다. StickmanAgent.Update()가 <b>상태 Tick보다 먼저</b> 무조건 부른다.
        ///
        /// 왜 매 프레임 벗겼다 다시 얹는가 — 리프트를 얹은 채로 두면 접지 판정이 전부 틀어진다.
        /// GroundSensor.Sense()는 발이 발판 상단 ±groundSnapTolerance(0.49유닛) 안에 있을 때만
        /// 접지로 보고, SnapToGround는 이동 요구가 groundSnapMaxDistanceWorld(0.6유닛)를 넘으면
        /// **발판을 놓고 Fall로 보낸다**. 리프트 최대치가 0.5유닛(실측 최악 20.5pt)이라 두 임계 모두
        /// 아슬아슬하게 걸린다 — "바닥을 안 뚫게 고쳤더니 기상 중에 갑자기 낙하한다"가 될 뻔한 지점이다.
        /// 벗겼다 얹으면 물리·센서·발판 판정은 **언제나 리프트 없는 진짜 접지 좌표**만 본다.
        /// </summary>
        public void ReleaseInkFloorClearanceLift()
        {
            if (_inkClearanceLift <= 0f) return;
            if (Body != null)
            {
                Vector2 p = Body.position;
                MoveBodyToWorld(new Vector2(p.x, p.y - _inkClearanceLift));
            }
            _inkClearanceLift = 0f;
        }

        /// <summary>
        /// GETUP 동안 "지금 이 포즈가 접지선 아래로 내려간 만큼"만 루트를 들어 올린다.
        /// StickmanAgent.Update()가 <b>접지 안전망과 TickPose가 전부 끝난 뒤</b> 부른다.
        ///
        /// ★ 호출 순서가 이 수정의 핵심이다(디버거가 미리 경고한 함정):
        ///   Update()는 machine.Tick() -> TickGroundKeepingSafetyNet() -> TickPose() 순서다.
        ///   상태 안에서 루트를 들어 올리면 <b>같은 프레임 뒤에 도는 안전망의 SnapToGround가 도로
        ///   발판 상단으로 눌러 버린다.</b> 그래서 리프트는 그 뒤에, 이 한 곳에서만 얹는다.
        ///
        /// ★ 왜 상태(GetupState.Tick)가 아니라 여기인가 — 상태 전이가 일어난 <b>그 프레임에는 새 상태의
        ///   Tick이 돌지 않는다</b>(ChangeState는 Exit/Enter만 부르고 Tick은 다음 프레임부터다).
        ///   실측으로 확인한 바, GETUP 최악 관통은 정확히 그 **첫 프레임**에서 나온다
        ///   (스윕 spin=600: 첫 프레임 6.36pt = 그 사이클의 최악값). 상태 안에 두면 가장 깊은 한 프레임을
        ///   통째로 놓친다. 여기(TickPose와 같은 "상태 ID만 보고 매 프레임 멱등 적용" 자리)에 두면
        ///   전이 프레임/강제 인터럽트/외부 ChangeState 등 어떤 경로로 들어와도 빠짐이 없다.
        ///
        /// 이 상태에서만 적용하는 이유: Idle/Walk는 서 있는 자세라 이 보정이 0이지만, 0을 계산하는
        /// 비용조차 24시간 상주 앱에서는 매 프레임 낭비다(GETUP은 몇 초짜리 과도 상태다).
        /// LandingCrouch도 같은 계열 결함일 수 있다는 미검증 가설이 있다(Tasklist 참고) — 확인되면
        /// 여기 상태 목록에 한 줄 더하면 된다.
        /// </summary>
        public void TickInkFloorClearance()
        {
            if (Machine == null || Body == null) return;
            if (Machine.CurrentStateId != StickmanStateId.Getup) return;
            if (Config != null && !Config.getupFloorClearanceEnabled) return;
            if (!TryComputeInkDropBelowRoot(out float drop) || drop <= 0f) return;

            Vector2 p = Body.position;
            MoveBodyToWorld(new Vector2(p.x, p.y + drop));
            _inkClearanceLift = drop;
        }

        /// <summary>몸 바깥 잉크 제공자 캐시 — GetPoseAnimator()와 동일한 지연 수집/캐싱 패턴.</summary>
        private ICharacterInkExtentProvider[] InkExtentProviders
        {
            get
            {
                if (_inkExtentProviders == null)
                {
                    _inkExtentProviders = Body != null
                        ? Body.GetComponentsInChildren<ICharacterInkExtentProvider>(true)
                        : System.Array.Empty<ICharacterInkExtentProvider>();
                }
                return _inkExtentProviders;
            }
        }

        /// <summary>
        /// Idle/Walk의 Jump 전이가 실제로 확인해야 할 조건: "접지 중이거나, 발판을 벗어난 지
        /// StickConfig.coyoteTimeDuration 이내"(BUG-P1-M5 대응, Architect 결정 — 의도된 코요테 타임으로
        /// 채택). 이전에는 이 조건을 별도로 확인하지 않고 "GroundedTick이 아직 Fall로 강제 전이시키지
        /// 않았다"는 사실 하나만으로 점프를 암묵적으로 허용했는데, 그 판단 기준(fallGraceDuration)이
        /// 발판 이탈 판정과 점프 허용 판정이라는 서로 다른 두 목적에 재사용되고 있었다. 이제는 별도
        /// 필드(coyoteTimeDuration)로 명시적으로 판정한다 — GroundedTick 호출 직후(같은 프레임)에만
        /// 호출해야 정확하다(같은 _groundLossTimer 값을 공유).
        /// </summary>
        public bool IsWithinCoyoteTime(GroundSensor.GroundInfo info)
        {
            if (info.Grounded) return true;
            float coyote = Config != null ? Config.coyoteTimeDuration : 0.1f;
            return _groundLossTimer <= coyote;
        }

        /// <summary>
        /// 접지 중 캐릭터 발을 발판 상단선에 정착(settle)시킨다.
        ///
        /// ============================================================================
        /// ★ 2026-08-29 — 이동 거리 상한(리더 지시). "미세 정착"과 "순간이동"을 코드로 구분한다.
        /// ============================================================================
        /// 이 함수는 원래 "0.001유닛보다 어긋나 있으면 발판 상단 Y를 그냥 대입"이었고 **이동 거리에
        /// 상한이 전혀 없었다**. 미세 정착이 목적인 함수가 원리적으로는 화면 끝까지 순간이동시킬 수
        /// 있는 형태였다는 뜻이다. 지금은 상한을 넘으면 끌어올리지 않고 **발판을 놓고 Fall로 보낸다** —
        /// 딛고 있던 발판이 캐릭터를 지나쳐 커졌다면 캐릭터는 공중에 남는 것이 물리적으로 맞다.
        ///
        /// 상한을 위/아래로 나누지 않은 이유(리더가 판단을 요구한 항목): 두 경우의 **올바른 처리가
        /// 똑같이 "Fall"**이기 때문이다. 위로 크게 끌려가는 것은 명백한 순간이동이고, 아래로 크게
        /// 내려가는 것은 애초에 스냅이 아니라 낙하로 처리돼야 한다(그리고 Fall에 들어가면
        /// GroundSensor.TryFindLandingCrossing의 스윕 교차 판정이 정확한 착지면을 다시 잡아준다 —
        /// 아래로 억지로 대입하는 것보다 이 경로가 언제나 더 정확하다). 값이 하나면 어긋날 일도 없다.
        ///
        /// ★ 정직한 한계 — 이 상한은 지금 코드에서는 **방어적 불변식**이지 이번 신고의 원인 제거가
        /// 아니다. GroundSensor.Sense()는 발이 발판 상단의 ±groundSnapTolerance(에셋 20 OS-pt ≈ 0.49
        /// 월드유닛) 안에 있을 때만 Grounded=true를 주고 GroundWorldY도 그때의 그 발판 상단이므로,
        /// 현재 배선에서 이 함수가 옮길 수 있는 거리는 이미 그 허용오차로 묶여 있다(= 화면 높이만큼
        /// 끌어올리는 일은 이 경로로는 일어날 수 없다). 실제 신고 원인은 RescueToSafeGround였다
        /// (그 함수 문서의 실측 로그 근거 참고). 그럼에도 상한을 두는 값어치는 분명하다: "무엇을
        /// 접지로 볼 것인가(groundSnapTolerance)"와 "몸을 얼마나 순간이동시켜도 되는가"는 서로 다른
        /// 두 결정인데 지금까지 전자 하나에 묶여 있었다. 누가 groundSnapTolerance를 올리는 순간
        /// 순간이동 허용치가 조용히 함께 커지는 구조였고, 이 상한이 그 연결을 끊는다.
        /// </summary>
        /// <returns>상한 초과로 발판을 놓고 Fall로 전이했으면 true.</returns>
        private bool SnapToGround(GroundSensor.GroundInfo info)
        {
            if (Body == null) return false;
            Vector2 pos = Body.position;
            float delta = info.GroundWorldY - pos.y; // + = 위로 끌어올림, - = 아래로 내림
            float maxSnap = Config != null ? Mathf.Max(0f, Config.groundSnapMaxDistanceWorld) : 0.6f;

            if (Mathf.Abs(delta) > maxSnap)
            {
                Debug.Log($"[스냅상한초과] 접지 스냅이 상한을 넘어 발판을 놓고 낙하시킵니다 — " +
                    $"{(delta > 0f ? "위로" : "아래로")} {Mathf.Abs(delta):F3}유닛(상한 {maxSnap:F2}) 이동 요구, " +
                    $"발 월드Y={pos.y:F3}, 발판 상단 월드Y={info.GroundWorldY:F3}, 발판핸들={CurrentFootholdHandle}. " +
                    "딛고 있던 발판이 캐릭터를 지나쳐 크게 움직였다는 뜻이라, 끌고 가지 않고 공중에 남깁니다.");
                CurrentFootholdHandle = 0L;
                ReportFootholdChangeIfNeeded("접지 스냅 상한 초과 — 발판을 놓고 낙하");
                _groundLossTimer = 0f;
                Machine?.ChangeState(StickmanStateId.Fall);
                return true;
            }

            if (Mathf.Abs(delta) > 0.001f)
            {
                MoveBodyToWorld(new Vector2(pos.x, info.GroundWorldY));
            }
            if (Body.linearVelocity.y < 0f)
            {
                Vector2 v = Body.linearVelocity;
                v.y = 0f;
                Body.linearVelocity = v;
            }
            return false;
        }

        /// <summary>
        /// ★ 몸을 특정 월드 좌표로 **순간 이동**시키는 유일한 창구(2026-08-29, 디버거 — 착지 첫 프레임
        /// 잉크가 화면 밖으로 8.82pt 잘려 나가는 회귀 FloorContactVisibilityTests 실패의 근본 원인).
        ///
        /// 왜 두 곳(Rigidbody2D.position + Transform.position)에 함께 써야 하는가 — 이 프로젝트는
        /// Physics2D.autoSyncTransforms가 **꺼져 있다**(ProjectSettings/Physics2DSettings.asset의
        /// m_AutoSyncTransforms: 0). 그래서 Rigidbody2D.position에만 대입하면 그 값은 물리 엔진 안에서만
        /// 갱신되고, **화면에 그려지는 Transform은 다음 물리 스텝까지 옛 위치에 그대로 남는다**.
        /// 프레임 순서가 FixedUpdate(물리 적분) -> Update(상태 Tick = 여기서 스냅) -> 렌더이므로,
        /// 그 한 프레임은 "물리가 방금 적분해 둔 위치"로 그려진다.
        ///
        /// 실측(Logs/dbg_diag4.log, 스폰 낙하 11.63유닛):
        ///     [f=316] st=LandingCrouch bodyY=-11.8045 rootY(Transform)=-12.1840 -> 잉크 하단 -12.2155
        ///     화면 바닥이 -12.0000이므로 그 한 프레임만 8.82pt 아래로 잘려 그려졌다.
        /// 낙하 속도 24.7유닛/초 × 그 프레임의 물리 적분량이 그대로 어긋남이 된다 — 즉 높이 떨어질수록,
        /// 프레임이 길수록 더 깊이 파묻힌 그림이 한 프레임 번쩍인다.
        ///
        /// ThrowTumbleState는 이미 같은 이유로 두 곳에 함께 쓰고 있었다(ApplyRootRotation/ConfirmLanding의
        /// 주석). 같은 계산이 두 벌로 흩어져 한쪽만 고쳐지는 것이 이 프로젝트가 반복해 겪은 실패라,
        /// 착지/스냅/클램프/구조가 전부 이 한 창구를 쓰도록 모은다. 속도는 건드리지 않는다(호출부마다
        /// 관성 처리 규칙이 다르므로 각자 책임).
        /// </summary>
        public void MoveBodyToWorld(Vector2 worldPos)
        {
            if (Body == null) return;
            Body.position = worldPos;
            Transform t = Body.transform;
            t.position = new Vector3(worldPos.x, worldPos.y, t.position.z);
        }

        // ============================================================================
        // ★ 리더 지시 6·7항 — 화면 밖 소실 방지(하드 클램프) + 최종 안전망(리스폰)
        // ============================================================================
        // 사용자 신고 4번: "그러다가 갑자기 화면 밖으로 사라져버림". 캐릭터를 영영 잃어버리는 것이
        // 가장 치명적이므로 이 규칙은 다른 모든 로직보다 **나중에, 무조건** 적용된다
        // (StickmanAgent.Update()가 상태 Tick을 전부 끝낸 뒤 마지막에 호출한다 —
        // 어떤 상태가 어떤 이유로 몸을 옮겼든 그 결과를 여기서 되돌린다).
        //
        // 왜 기존 CheckScreenBoundsOrFall로는 부족한가: 그 검사는 "발판들의 좌우 범위"를 화면으로
        // 간주한다. 그런데 실제 창은 화면 경계를 넘어갈 수 있어서(창을 화면 밖으로 반쯤 끌어다 놓는
        // 흔한 상황) 그 범위 자체가 화면 밖까지 뻗고, 그러면 캐릭터가 그 위를 걸어 화면 밖으로
        // 나가버린다. MacWindowService가 발판을 디스플레이 경계로 잘라내는 것이 1차 방어이고,
        // 여기가 그와 독립적인 2차(최종) 방어다.

        /// <summary>화면 경계에서 남겨둘 최소 여유(OS 포인트). 아래 CharacterVisualHalfWidthWorld가
        /// 더해져 실제 여유가 결정된다.</summary>
        private const float ScreenClampMarginOsPx = 8f;

        // ★ 아래쪽 전용 여유(2026-08-29, 리더 지시 — 바닥 안전망을 화면 최하단 8pt까지 내린 라운드에서
        // 발견). 물리 바닥이 이제 이 클램프 경계 바로 위(0.196유닛)에 있어서, 전속력 낙하(최대 한
        // 물리 스텝 0.75유닛 관통 가능)가 클램프 경계를 넘는 순간 이 클램프가 세로 속도를 즉시 0으로
        // 지워버려 다음 스텝의 충돌 상대속도가 0이 되고, RAGDOLL 진입 임계값 판정이 그 속도를 영영 보지
        // 못한다(논리 발판이 없는 Dock 구멍 구간으로 전속력 낙하할 때만 발생 — 정상 착지는 발판 스냅이
        // 먼저 확정하므로 무관). 40pt(≈1유닛)는 화면 최상단에서의 최대 한 스텝 관통(31pt)을 넉넉히
        // 덮는다.
        //
        // 아래 줄 바로 아래 주석의 "여유는 0이다" 결정과 모순이 아니다 — 그건 화면 **안**에서 지면과
        // 매 프레임 계속 싸우던 정상상태 버그였고(경계가 지면보다 위라 계속 되돌림), 이 여유는 화면
        // **밖**으로 한 번, 순간적으로만 더 허용하는 것이다. 정상 발판/지면은 항상 화면 안에 있으므로
        // 이 여유가 있어도 정상 동작에서는 클램프가 여전히 발동하지 않는다.
        private const float BottomClampSlackOsPoints = 40f;

        /// <summary>
        /// ★ 2026-08-28 (리더 추가 관찰: "캐릭터가 화면 왼쪽 끝에서 잘려 보인다") — 캐릭터의 **시각적
        /// 반폭**(월드 유닛). 화면 하드 클램프는 루트(=발 중심) 좌표만 보므로, 이 값을 더하지 않으면
        /// 가장자리에서 팔/머리가 화면 밖으로 잘린다. Core/StickmanAgent가 자신의 렌더러 바운즈에서
        /// 주기적으로 갱신한다(포즈에 따라 팔 벌린 너비가 바뀌므로 상수로 둘 수 없다). 0이면 예전처럼
        /// 루트만 클램프한다(테스트/폴백 경로에서도 안전).
        /// </summary>
        public float CharacterVisualHalfWidthWorld;

        /// <summary>
        /// ★ 2026-08-30 R3-M1 — 캐릭터의 **물리적 반폭**(월드 유닛). 위 시각 반폭과 이름은 비슷하지만
        /// 용도가 정반대다:
        ///   · 시각 반폭 = 렌더러 바운즈 → "화면 밖으로 잘리지 않게" 클램프에 쓴다(팔/획까지 포함).
        ///   · 물리 반폭 = 루트 Rigidbody2D의 **비-트리거 콜라이더** 바운즈 → "벽에 얼마나 가까이 설 수
        ///     있는가"를 정한다. 실제로는 머리 CircleCollider2D의 반경(배율 1.0에서
        ///     <see cref="StickConfig.BaselineBodyPhysicsHalfWidth"/> = 0.4)이 루트 캡슐 반폭(0.2)보다
        ///     넓어서 이쪽이 지배한다. 잡기 영역(GrabArea)은 isTrigger라 제외된다.
        /// Core/StickmanAgent가 시각 반폭과 같은 주기로 갱신한다. 0이면 아래
        /// <see cref="EdgeStopDistanceWorld"/>가 설정 배율에서 유도한 값으로 되메운다(절대 0이 되지 않는다).
        /// </summary>
        public float CharacterPhysicalHalfWidthWorld;

        /// <summary>
        /// ★ 자율 배회가 "발판 경계에 도달했다"고 볼 거리(월드 유닛) — <b>설정값이 아니라 유도값</b>.
        /// 2026-08-30 R3-M1: StickConfig.wanderEdgeStopDistance(0.300)가 몸이 벽에 부딪혀 설 수 있는
        /// 이격(0.305)보다 작아서, Dock 물리 계단 옆면에 붙어 선 캐릭터가 경계 밴드에 **물리적으로
        /// 들어갈 수 없었다**(되올라가기 판정을 평가할 기회조차 없었다).
        /// 유도식과 그 근거는 <see cref="DockGeometry.ResolveEdgeStopDistance"/>에 전부 적어 두었다.
        ///
        /// 실측 반폭이 없으면(프리팹 없는 테스트 리그) 설정 배율에서 유도한 값으로 되메운다 —
        /// 여기서 0을 흘리면 유도가 조용히 꺼져 예전 버그가 그대로 되살아난다.
        /// </summary>
        public float EdgeStopDistanceWorld
        {
            get
            {
                float configured = Config != null ? Config.wanderEdgeStopDistance : 0.3f;
                float halfWidth = CharacterPhysicalHalfWidthWorld;
                if (halfWidth <= 0f || float.IsNaN(halfWidth))
                {
                    float scale = Config != null ? Config.ResolveCharacterScale() : 1f;
                    halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
                }
                return DockGeometry.ResolveEdgeStopDistance(configured, halfWidth);
            }
        }

        /// <summary>
        /// ★ 등반이 끝난 뒤 발판 안쪽으로 들어가 설 거리(월드 유닛) — 위 <see cref="EdgeStopDistanceWorld"/>와
        /// <b>같은 관례의 유도값</b>이다(2026-08-31, 캐릭터 크기 다이얼 선행조건).
        ///
        /// 불변식은 <b>이 값 &gt; 경계 판정 거리</b>다. 설정 절대값(0.60)만으로는 배율 1.125까지밖에 못
        /// 지키는데 다이얼이 배율을 런타임에 2.00까지 올리므로, 경계 판정 거리와 <b>같은 입력</b>에서
        /// 유도한다. 유도식/여유의 근거는 <see cref="DockGeometry.ResolveParkourMantleInset"/>에 있다.
        ///
        /// <para>StickConfig.parkourMantleInsetDerived가 false면 유도를 통째로 건너뛴다 —
        /// Tests/PlayMode/EdgeHopDownTests의 네거티브 컨트롤이 옛 회귀를 재현하는 유일한 통로다.</para>
        /// </summary>
        public float ParkourMantleInsetWorld
        {
            get
            {
                float configured = Config != null ? Config.parkourMantleInset : 0.6f;
                if (Config != null && !Config.parkourMantleInsetDerived) return Mathf.Max(0f, configured);

                float halfWidth = CharacterPhysicalHalfWidthWorld;
                if (halfWidth <= 0f || float.IsNaN(halfWidth))
                {
                    float scale = Config != null ? Config.ResolveCharacterScale() : 1f;
                    halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
                }
                return DockGeometry.ResolveParkourMantleInset(configured, EdgeStopDistanceWorld, halfWidth);
            }
        }

        /// <summary>
        /// ★ 경계 행동(뛰어내리기/매달리기/되올라가기) <b>대상 탐지</b>의 도달거리(월드 유닛) —
        /// 2026-08-31, 사용자 신고 "캐릭터 크기를 키우면 Dock 위로 안 올라옴"의 근본 수정.
        ///
        /// 위 <see cref="EdgeStopDistanceWorld"/>는 <b>언제 평가할지</b>를 정하고, 이 값은
        /// <b>그 순간 무엇이 잡히는지</b>를 정한다. 배회 AI는 평가 거리보다 가까이 다가가지 않으므로
        /// (경계 추첨은 걷기 구간당 1회, 실패하면 그 자리에서 돌아선다) 이 값이 평가 거리보다 짧으면
        /// 경계 행동이 <b>구조적으로 성립 불가능</b>해진다. 배율 1.0을 넘으면 실제로 그렇게 됐다.
        /// 유도식/근거는 <see cref="DockGeometry.ResolveEdgeProbeReach"/>에 전부 적어 두었다.
        /// </summary>
        public float EdgeProbeReachWorld
        {
            get
            {
                float configured = Config != null ? Config.parkourDetectionRadius : 0.5f;
                return DockGeometry.ResolveEdgeProbeReach(configured, EdgeStopDistanceWorld);
            }
        }

        /// <summary>이 시간(초) 넘게 Fall이 이어지면 "유효 발판을 완전히 잃었다"고 보고 리스폰한다.</summary>
        private const float LostCharacterRescueSeconds = 6f;

        private float _fallStuckTimer;
        private long _lastReportedFootholdHandle = long.MinValue;

        // [화면클램프] 로그 throttle(2026-08-28 로그 정리 라운드). 이 로그는 "이상 신호"라
        // StickConfig.verboseDiagnosticsLogging과 무관하게 항상 남겨야 하지만, 클램프는 캐릭터가
        // 가장자리를 계속 밀 때 **매 프레임** 성립할 수 있어 그대로 두면 초당 수십 줄이 쏟아진다.
        // 최소 간격만 두어 "무슨 일이 있었다"는 신호는 잃지 않으면서 홍수만 막는다.
        private const float ScreenClampLogMinIntervalSeconds = 2f;
        private float _lastScreenClampLogTime = float.NegativeInfinity;

        // ============================================================================
        // ★ 화면 클램프 경계의 단일 소스 (2026-08-29 — "화면 물리적 끝에서 제자리 걷기" 수정)
        // ============================================================================
        // 증상: 캐릭터가 화면 좌/우 끝까지 걸어가면 걷기 애니메이션만 돌고 위치는 변하지 않는
        // "러닝머신"이 되어, Walk 지속시간(1.5~4초)이 만료될 때까지 스스로 풀리지 않았다.
        //
        // 원인: 아래 하드 클램프는 캐릭터 루트를 화면 끝에서 (ScreenClampMarginOsPx + 시각 반폭)만큼
        // 안쪽에 가둔다(실측 약 58pt). 그런데 배회 AI의 경계 판정(AutoWanderController.IsNearFootholdEdge)은
        // wanderEdgeStopDistance(0.3유닛 ≈ 24pt)를 **발판의 원시 경계**(= 화면 끝)에서 쟀다.
        // 58 > 24라서 "경계 근처"가 영영 성립하지 않았고, 캐릭터는 돌아설 이유를 못 찾은 채 클램프만
        // 계속 밀었다.
        //
        // 해법: "캐릭터가 물리적으로 갈 수 있는 한계"를 이 클래스가 **하나의 계산식**으로만 만들고
        // (ComputeScreenClampOsBounds), 클램프 본체와 조회 API(TryGetWalkableScreenBoundsWorld)가
        // 둘 다 그것만 읽게 한다. 두 곳이 각자 계산하면 반드시 다시 어긋난다 — 이 프로젝트가 이미
        // 두 번 겪은 실패 유형이다(BUG-P1-R4-B1 씬 지면 Y vs 발판 상수 이중 정의,
        // BUG-P1-R5-B2 Dock 구간 이중 계산 → 단일 소스화로 해결).

        /// <summary>
        /// 화면 하드 클램프가 캐릭터 루트를 가두는 OS 좌표 경계 묶음.
        /// 생산자는 <see cref="ComputeScreenClampOsBounds"/> 하나뿐이다.
        /// </summary>
        private readonly struct ScreenClampOsBounds
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinY;
            public readonly float MaxY;
            /// <summary>좌우에 실제로 적용된 여유(기본 여유 + 시각 반폭, OS 포인트) — 로그용.</summary>
            public readonly float SideMargin;
            public readonly float HalfWidthOsPx;
            public readonly Vector2 Origin;
            public readonly float ScreenW;
            public readonly float ScreenH;

            public ScreenClampOsBounds(float minX, float maxX, float minY, float maxY,
                float sideMargin, float halfWidthOsPx, Vector2 origin, float screenW, float screenH)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                SideMargin = sideMargin;
                HalfWidthOsPx = halfWidthOsPx;
                Origin = origin;
                ScreenW = screenW;
                ScreenH = screenH;
            }
        }

        /// <summary>
        /// 하드 클램프 경계(OS 좌표)를 계산하는 **유일한** 곳. MainCamera가 null이면 호출하지 말 것
        /// (두 호출부 모두 앞에서 null을 걸러낸다).
        /// </summary>
        private ScreenClampOsBounds ComputeScreenClampOsBounds()
        {
            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(Config));
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float screenW = (Screen.width > 0 ? Screen.width : 1920) * dpi;
            float screenH = (Screen.height > 0 ? Screen.height : 1080) * dpi;

            // 시각적 반폭을 OS 픽셀로 환산해 좌우 여유에 더한다 — 이게 없으면 루트(발)는 화면 안인데
            // 벌린 팔과 머리가 화면 밖으로 잘린다(2026-08-28 리더 관찰). 카메라의 "월드 1유닛 = 몇
            // Unity 픽셀"에 desktopDpiScale을 곱하면 OS 포인트가 된다(ScreenCoordinateConverter와 같은
            // 환산 규칙).
            float pxPerWorldUnit = MainCamera != null && MainCamera.orthographic && MainCamera.orthographicSize > 0f
                ? (Screen.height * 0.5f) / MainCamera.orthographicSize
                : 0f;
            float halfWidthOsPx = Mathf.Max(0f, CharacterVisualHalfWidthWorld) * pxPerWorldUnit * dpi;

            float sideMargin = ScreenClampMarginOsPx + halfWidthOsPx;
            float minX = origin.x + sideMargin;
            float maxX = origin.x + screenW - sideMargin;
            if (minX > maxX) { minX = maxX = origin.x + screenW * 0.5f; } // 화면보다 캐릭터가 넓은 병리적 경우

            float minY = origin.y + ScreenClampMarginOsPx;
            // ★ 아래쪽 좌우 방향 여유는 0이다(2026-08-28). 이유: 안전망 발판이 화면 최하단 근처로
            // 내려온 뒤로는 이 클램프가 **지면과 싸운다**. 실측으로 재현된 사고: 640x480 테스트
            // 화면에서 8 OS px는 0.4월드유닛이라 지면(0.245유닛)보다 위에 있었고, RAGDOLL이 지면에
            // 내려앉을 때마다 클램프가 매 프레임 위로 되돌리며 세로 속도를 0으로 만들어 **영원히
            // 안정되지 못했다**(GETUP 미도달로 StickmanRagdollRecoveryTests가 빨간불). 이 클램프의
            // 목적은 "캐릭터를 화면 밖에서 잃어버리지 않는다"이고 그 목적에는 경계 자체(여유 0)로
            // 충분하다 — 발판/지면은 언제나 화면 안에 있으므로 정상 동작에서는 아예 발동하지 않고,
            // 진짜로 화면 아래로 빠져나가는 경우만 잡는다.
            //
            // 다만 아래쪽만은 BottomClampSlackOsPoints만큼 추가 여유를 둔다(2026-08-29, 위 상수 선언부
            // 주석 참고) — 물리 바닥이 이 경계 바로 위로 내려온 뒤로 전속력 낙하의 한 스텝 관통이
            // 이 클램프에 먼저 걸려 RAGDOLL 임계값 판정을 무력화하는 새 상호작용이 생겼기 때문이다.
            float maxY = origin.y + screenH + BottomClampSlackOsPoints;

            return new ScreenClampOsBounds(minX, maxX, minY, maxY, sideMargin, halfWidthOsPx, origin, screenW, screenH);
        }

        /// <summary>
        /// ★ "캐릭터가 물리적으로 갈 수 있는 좌우 한계"(Unity 월드 X) — 화면 하드 클램프가 실제로
        /// 캐릭터를 붙잡아 세우는 바로 그 X다. 클램프 본체와 **완전히 같은 계산식**
        /// (<see cref="ComputeScreenClampOsBounds"/>)에서 파생되므로 두 값이 어긋날 수 없다.
        ///
        /// 소비자: AutoWanderController.IsNearFootholdEdge() — 발판의 경계가 곧 화면의 끝인 쪽
        /// (isTrueScreenEdge)에서는 **원시 발판 경계가 아니라 이 한계**까지의 거리로 "경계 근처"를
        /// 판정해야, 클램프에 닿기 전에 스스로 멈추고 돌아선다(위 문단의 러닝머신 증상 수정).
        ///
        /// 주의: 반환값은 **캐릭터 루트(발 중심)** 기준이며 시각 반폭이 이미 반영돼 있다
        /// (CharacterVisualHalfWidthWorld가 포즈에 따라 변하므로 프레임마다 조금씩 달라질 수 있다 —
        /// 캐싱하지 말고 필요할 때마다 물어볼 것).
        /// </summary>
        /// <returns>Body/MainCamera가 없어 계산할 수 없으면 false(그 경우 out 값은 무의미).</returns>
        public bool TryGetWalkableScreenBoundsWorld(out float leftWorldX, out float rightWorldX)
        {
            leftWorldX = 0f;
            rightWorldX = 0f;
            if (Body == null || MainCamera == null) return false;

            // 왕복 정밀도를 위해 클램프와 같은 depth(카메라 거리)를 쓴다 — ScreenCoordinateConverter의
            // 계약(WorldToOsScreen이 준 depth를 그대로 OsScreenToWorld에 넘길 것)을 그대로 지킨다.
            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);
            ScreenClampOsBounds b = ComputeScreenClampOsBounds();
            leftWorldX = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(b.MinX, os.y), depth, Config).x;
            rightWorldX = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(b.MaxX, os.y), depth, Config).x;
            return true;
        }

        /// <summary>
        /// 매 프레임 마지막에 호출 — (1) 캐릭터 OS 좌표를 오버레이 창(=화면) 안으로 하드 클램프하고,
        /// (2) 그래도 발판을 완전히 잃은 채 오래 낙하 중이면 화면 중앙 지면으로 강제 복귀시킨다.
        /// </summary>
        public void EnforceScreenBoundsAndRescue(float deltaTime)
        {
            if (Body == null || MainCamera == null) return;

            Vector2 os = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, Body.position, Config, out float depth);

            // 경계 계산은 전부 ComputeScreenClampOsBounds()에 있다 — 그 함수가 **유일한 생산자**이고
            // TryGetWalkableScreenBoundsWorld()도 같은 것을 읽는다(두 함수의 문서 참고).
            ScreenClampOsBounds b = ComputeScreenClampOsBounds();

            float clampedX = Mathf.Clamp(os.x, b.MinX, b.MaxX);
            float clampedY = Mathf.Clamp(os.y, b.MinY, b.MaxY);
            bool clamped = !Mathf.Approximately(clampedX, os.x) || !Mathf.Approximately(clampedY, os.y);
            if (clamped)
            {
                Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(clampedX, clampedY), depth, Config);
                MoveBodyToWorld(new Vector2(world.x, world.y));
                Vector2 v = Body.linearVelocity;
                if (!Mathf.Approximately(clampedX, os.x)) v.x = 0f;   // 벽에 막힌 것처럼 수평 관성 제거
                if (clampedY < os.y) v.y = 0f;                        // 아래로 뚫고 나가던 관성 제거
                Body.linearVelocity = v;
                if (Time.unscaledTime - _lastScreenClampLogTime >= ScreenClampLogMinIntervalSeconds)
                {
                    _lastScreenClampLogTime = Time.unscaledTime;
                    Debug.Log($"[화면클램프] 캐릭터가 화면 밖으로 나가려 해 되돌렸습니다 — OS ({os.x:F1},{os.y:F1}) -> " +
                        $"({clampedX:F1},{clampedY:F1}), 좌우여유={b.SideMargin:F1}pt(기본 {ScreenClampMarginOsPx:F0} + 시각반폭 {b.HalfWidthOsPx:F1}), " +
                        $"화면=({b.Origin.x:F0},{b.Origin.y:F0} {b.ScreenW:F0}x{b.ScreenH:F0}), " +
                        $"상태={(Machine != null ? Machine.CurrentStateId.ToString() : "?")}. " +
                        $"(같은 로그는 최소 {ScreenClampLogMinIntervalSeconds}초 간격으로만 남깁니다)");
                }
            }

            // (2) 최종 안전망 — 오래 낙하 중이면(= 어떤 발판에도 착지하지 못하는 상황) 강제 복귀.
            bool falling = Machine != null && Machine.CurrentStateId == StickmanStateId.Fall;

            // ★★ (1.5) Dock 사각지대 즉시 회수 (2026-08-30, 디버거) — 아래 6초 안전망보다 **먼저** 본다.
            // Dock 가로 구간의 화면 최하단은 "물리적으로는 떠받쳐지지만 논리적으로는 접지하지 않는"
            // 사각지대다(Editor/SceneBootstrapper.CreateGroundCollider 문서의 의도적 설계). 그리로
            // 흘러든 캐릭터는 **Fall 상태인데 속도가 0**이라는, 정상 낙하에서는 성립할 수 없는 조합에
            // 빠진다 — 착지가 영원히 확정되지 않고 6초 뒤 화면 가로 중앙으로 순간이동할 때까지 Dock
            // 아래에 박혀 있는다(PlayMode 실측: Idle->Fall 이후 41,000프레임 = 정확히 6초 고착).
            // 그 조합을 속도로 감지해 **가로 이동 없이 바로 위 발판(=Dock 상단)으로 올려세운다.**
            if (falling && Config != null && Config.sinkholeLiftRecoveryEnabled)
            {
                float restEps = Mathf.Max(0.0001f, SinkholeRestSpeedEpsilon);
                if (Body.linearVelocity.sqrMagnitude <= restEps * restEps) _fallRestingTimer += deltaTime;
                else _fallRestingTimer = 0f;

                float restHold = Mathf.Max(0.05f, Config.sinkholeLiftRestSeconds);
                if (_fallRestingTimer >= restHold && TryLiftOutOfSinkhole())
                {
                    _fallRestingTimer = 0f;
                    _fallStuckTimer = 0f;
                    return;
                }
            }
            else
            {
                _fallRestingTimer = 0f;
            }

            _fallStuckTimer = falling ? _fallStuckTimer + deltaTime : 0f;
            if (_fallStuckTimer >= LostCharacterRescueSeconds)
            {
                _fallStuckTimer = 0f;
                RescueToSafeGround();
            }
        }

        /// <summary>
        /// 캐릭터를 화면 가로 중앙으로 옮기고, 그 X의 **바닥**(= 그 x에서 가장 낮은 발판 상단) 위에 세운
        /// 뒤 Idle로 되돌린다. 리더 지시 7항.
        ///
        /// ============================================================================
        /// ★★ 2026-08-29 — 사용자 신고 "창이 최대이면 갑자기 제일위로 순간이동해서 떨어짐"의 **진짜 원인**
        /// ============================================================================
        /// 이 함수는 원래 TryGetGroundSurfaceWorldY(= 그 x에서 **가장 높은** 발판 상단)로 복귀 지점을
        /// 골랐다. 평소에는 그 값이 Dock 상단이라 아무 문제가 없었는데, 사용자가 창 하나를 **최대화**하면
        /// 그 창의 상단이 곧 화면 꼭대기가 되고, 화면 가로 중앙에서 "가장 높은 발판 상단" = 화면 꼭대기가
        /// 된다. 그래서 구조 안전망이 캐릭터를 화면 최상단으로 **순간이동**시켰다.
        ///
        /// 실측 증거(Player.log / Player-prev.log, 2026-08-29):
        ///   · Player-prev.log — [캐릭터구조] 15회 중 **15회 전부** 복귀 지점이 월드 (0.000, 11.193).
        ///     11.193은 최대화된 Cursor 창 상단(OS y=33) = 화면 꼭대기다.
        ///   · Player.log — 24회 중 6회가 11.193(그 창이 목록에 있던 구간), 18회는 -10.167(Dock 상단,
        ///     정상). 즉 "최대화된 창이 있을 때만" 최상단으로 튄다 — 신고 문구 그대로다.
        ///
        /// 왜 이 안전망이 그렇게 자주 돌았는가(= 증상이 반복된 이유): Dock 가로 구간의 화면 최하단은
        /// 물리 바닥(PhysicsGround, 월드 -11.02)이 논리 발판(Dock 상단 -10.167)보다 0.855유닛 아래에
        /// 있어서 "물리적으로는 떠받쳐지지만 논리적으로는 접지하지 않는" 사각지대다(그 설계 근거는
        /// Assets/Editor/SceneBootstrapper.cs의 CreateGroundCollider 문서 참고 — 랙돌이 화면 밖으로
        /// 사라지는 더 나쁜 실패를 막기 위한 의도적 선택이고, 회수는 이 안전망에 맡긴다고 명시돼 있다).
        /// 그 사각지대로 흘러든 캐릭터는 6초 뒤 여기로 오고, 여기가 캐릭터를 화면 꼭대기로 올려놓고,
        /// 거기서 다시 떨어져 같은 사각지대로 돌아오는 **무한 루프**가 됐다.
        ///
        /// 해법: "가장 높은 표면"이 아니라 **"그 x의 바닥"**(TryGetFloorWorldY)으로 복귀시킨다. 안전망의
        /// 목적은 "잃어버린 캐릭터를 딛을 수 있는 곳에 돌려놓는다"이지 "가장 높은 곳에 올려놓는다"가
        /// 아니다. 바닥은 정의상 화면 하단(Dock/합성 안전망)이므로 창을 아무리 최대화해도 이 함수가
        /// 캐릭터를 위로 끌어올릴 수 없다.
        ///
        /// 같은 클래스의 과거 수정과 정확히 같은 교훈이다: 드래그 순간이동("마우스로 끌었는데 갑자기
        /// 다른 창 위로 올라감")도 원인이 TryGetSurfaceWorldY(가장 높은 표면)였고 TryGetFloorWorldY로
        /// 바꿔 고쳤다(GroundSensor.TryGetFloorWorldY 문서). 이 함수만 예전 호출부로 남아 있었다.
        /// </summary>
        /// <summary>
        /// "Fall인데 실제로는 멈춰 있다"로 볼 최대 속도(월드 유닛/초). 정상 낙하는 첫 1~2프레임을
        /// 빼면 이 값을 즉시 넘어서므로(중력 가속 29.4유닛/초²), sinkholeLiftRestSeconds 동안
        /// 이 아래로 유지되는 것은 사실상 "물리면 위에 얹혀 있다"는 뜻이다.
        /// </summary>
        private const float SinkholeRestSpeedEpsilon = 0.05f;

        /// <summary>
        /// Dock 사각지대(물리 바닥은 있는데 논리 발판이 없는 구간)에서 **가로 이동 없이** 바로 위
        /// 발판 위로 올려세운다. 목표 높이는 <see cref="TryGetFloorWorldY"/>(그 x에서 **가장 낮은**
        /// 발판 상단)로 고른다 — <see cref="RescueToSafeGround"/>가 "가장 높은 표면"을 쓰다가
        /// 최대화된 창 꼭대기로 순간이동시켰던 사고(그 함수 문서의 실측 근거)를 되풀이하지 않기 위해서다.
        /// Dock 가로 구간에서 그 값은 정확히 Dock 상단이다(안전망 조각은 그 구간에 구멍이 뚫려 있다).
        /// </summary>
        /// <returns>실제로 올려세웠으면 true. 위에 발판이 없거나 낙차가 상한을 넘으면 false
        /// (그 경우는 "진짜로 잃어버린 것"이라 기존 6초 안전망에 그대로 맡긴다).</returns>
        private bool TryLiftOutOfSinkhole()
        {
            if (Body == null || Machine == null) return false;
            Vector2 pos = Body.position;
            if (!TryGetFloorWorldY(pos, out float floorWorldY)) return false;

            float rise = floorWorldY - pos.y;
            if (rise <= 0.001f) return false; // 위에 딛을 것이 없다 — 사각지대가 아니다.

            float maxHeights = Config != null ? Mathf.Max(0f, Config.sinkholeLiftMaxHeights) : 1.5f;
            float maxRise = maxHeights * CharacterHeightWorld;
            if (rise > maxRise) return false;

            MoveBodyToWorld(new Vector2(pos.x, floorWorldY));
            Body.linearVelocity = Vector2.zero;
            CurrentFootholdHandle = 0L; // 다음 프레임에 재획득하도록 초기화(RescueToSafeGround와 동일 관례).
            ResetGroundLossTimer();
            Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            Debug.Log($"[사각지대회수] Fall 상태인데 {(Config != null ? Config.sinkholeLiftRestSeconds : 0.35f):F2}초 동안 " +
                $"속도가 0이었습니다 = 논리 발판이 없는 물리면(Dock 가로 구간의 화면 최하단) 위에 얹혀 " +
                $"있다는 뜻입니다. 가로 이동 없이 바로 위 발판으로 올려세웁니다 — 월드 " +
                $"({pos.x:F3},{pos.y:F3}) -> ({pos.x:F3},{floorWorldY:F3}), 끌어올린 높이 {rise:F3}유닛" +
                $"(상한 {maxRise:F3} = 신장 {CharacterHeightWorld:F3} x {maxHeights:F2}). " +
                "이 회수가 없으면 6초 뒤 화면 가로 중앙으로 순간이동할 때까지 Dock 아래에 박혀 있습니다.");
            return true;
        }

        public void RescueToSafeGround()
        {
            if (Body == null || MainCamera == null) return;

            float dpi = Mathf.Max(0.0001f, ScreenCoordinateConverter.ResolveDpiScale(Config));
            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float centerOsX = origin.x + (Screen.width > 0 ? Screen.width : 1920) * dpi * 0.5f;

            Vector2 before = Body.position;
            _ = ScreenCoordinateConverter.WorldToOsScreen(MainCamera, before, Config, out float depth);
            Vector3 centerWorld = ScreenCoordinateConverter.OsScreenToWorld(MainCamera, new Vector2(centerOsX, origin.y), depth, Config);

            float targetY = centerWorld.y;
            var probe = new Vector2(centerWorld.x, before.y);
            // ★ 가장 높은 표면(TryGetGroundSurfaceWorldY)이 아니라 **바닥**을 쓴다 — 위 문서의 실측 근거.
            if (TryGetFloorWorldY(probe, out float floorY)) targetY = floorY;

            MoveBodyToWorld(new Vector2(centerWorld.x, targetY));
            Body.linearVelocity = Vector2.zero;
            CurrentFootholdHandle = 0L; // 재획득하도록 초기화
            ResetGroundLossTimer();
            Machine?.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            Debug.Log($"[캐릭터구조] {LostCharacterRescueSeconds}초 이상 착지하지 못해 강제 복귀시켰습니다 — " +
                $"월드 {before} -> ({centerWorld.x:F3},{targetY:F3}) (화면 가로 중앙의 **바닥**). " +
                "사용자가 캐릭터를 잃어버리지 않게 하는 최종 안전망입니다(리더 지시 7항). " +
                "복귀 지점은 그 x에서 가장 낮은 발판 상단이므로, 창을 최대화해도 화면 꼭대기로 " +
                "올라가지 않습니다(2026-08-29 수정 — 이 함수 문서의 실측 근거 참고).");
        }

        // ================================================================================
        // ★ 되올라가기(ParkourClimb) 맨틀 완료 신호 — 2026-08-29 "독 위로 올라간 직후 바로 다시 내려감"
        // ================================================================================
        // 배회 AI(AutoWanderController)는 상태 머신을 구독하지 않고 블랙보드만 읽는다는 기존 계약을
        // 유지하면서 "방금 턱 위로 올라섰다"를 알려야 해서, 이벤트 대신 **단조 증가 카운터**로 노출한다
        // (이벤트 구독은 24시간 상주 앱에서 해제 누락 = 누수라 이 프로젝트가 계속 피해온 형태다).
        // 소비자는 자기가 마지막으로 본 번호와 다르면 "새 맨틀이 있었다"로 판정한다.
        //
        // 왜 필요했나(실측, Logs 참고): 등반을 유발한 다음 프레임부터 배회 AI는 여전히 "발판 경계에
        // 서 있다"고 보고 경계 정지(BeginEdgePause)를 걸었고, 그 정지가 등반 도중 끝나면서 진행 방향을
        // **방금 올라온 바깥쪽으로** 뒤집고 경계 행동 추첨권까지 리셋했다. 등반이 끝난 캐릭터는
        // parkourMantleInset(당시 0.25)만큼만 안쪽에 서므로 이미 경계 판정 거리(당시 0.30) 안이라,
        // 올라선 지 9프레임(약 0.15초) 만에 같은 모서리로 다시 뛰어내렸다.
        // (두 값은 그 뒤 각각 0.60 / 유도값 0.405로 올라갔지만 — 2026-08-30 R3-M1 — 이 카운터가
        //  필요한 이유 자체는 그대로다: 대소 관계는 필요조건일 뿐 충분조건이 아니다.)
        public int ClimbMantleSequence { get; private set; }

        /// <summary>마지막 맨틀에서 캐릭터가 **올라선 방향**(+1 오른쪽 / -1 왼쪽). 턱 안쪽을 가리킨다.</summary>
        public int ClimbMantleDirection { get; private set; } = 1;

        /// <summary>이 사실이 기록된 프레임 번호. <see cref="PlannedDwellRemainingSecondsFor"/>가
        /// "이동 의도가 이 사실보다 낡았는가"를 이 값으로 판정한다(그 메서드의 주석 참고).</summary>
        private int _climbMantleFrame = -1;

        /// <summary>ParkourClimbState가 등반을 마치고 턱 위에 실제로 올라선 프레임에 1회 호출한다.</summary>
        public void ReportClimbMantleCompleted(int direction)
        {
            ClimbMantleDirection = direction >= 0 ? 1 : -1;
            ClimbMantleSequence++;
            _climbMantleFrame = Time.frameCount;
        }

        /// <summary>딛고 있는 발판이 바뀔 때마다 이전->이후를 한 줄로 남긴다(리더 지시: 순간이동 추적용).</summary>
        public void ReportFootholdChangeIfNeeded(string reason)
        {
            if (CurrentFootholdHandle == _lastReportedFootholdHandle) return;
            long before = _lastReportedFootholdHandle;
            _lastReportedFootholdHandle = CurrentFootholdHandle;
            if (before == long.MinValue) return; // 최초 1회는 "변경"이 아니다
            Debug.Log($"[발판변경] {before} -> {CurrentFootholdHandle} ({reason}).");
        }

        /// <summary>
        /// 모든 발판의 좌우 범위(GroundInfo.ScreenLeft/RightWorldX)를 벗어났는지 검사해 벗어났다면
        /// Fall로 강제 전이한다. Idle/Walk/Jump/Fall 공통으로 호출된다.
        /// </summary>
        public bool CheckScreenBoundsOrFall(GroundSensor.GroundInfo info)
        {
            if (!info.HasAnyFoothold || Body == null) return false;
            float x = Body.position.x;
            if (x >= info.ScreenLeftWorldX && x <= info.ScreenRightWorldX) return false;

            // 이미 Fall이면 재전이를 걸지 않는다 — ChangeState는 같은 상태로도 매번 Exit()/Enter()를
            // 재실행하고 TransitionGeneration을 증가시키므로(BUG_REPORT_PHASE0.md Minor m3), FallState가
            // 화면 밖에 머무는 동안 매 프레임 자기 자신으로 재전이하는 불필요한 처리를 피한다.
            if (Machine.CurrentStateId != StickmanStateId.Fall)
            {
                Machine.ChangeState(StickmanStateId.Fall);
            }
            return true;
        }

        /// <summary>
        /// Active Ragdoll 파츠 캐시(Rigidbody2D/HingeJoint2D) — RagdollState/GetupState가 공유한다.
        /// 최초 필요 시 1회만 Body.transform을 루트로 GetComponentsInChildren 탐색을 수행해 캐싱한다.
        /// </summary>
        public RagdollRig GetRagdollRig()
        {
            if (_ragdollRig == null && Body != null)
            {
                // 바라보는 방향을 **매번 물어보는** 형태로 넘긴다(값 복사 금지) — RAGDOLL 진입 시점의
                // 최신 방향으로 해부학 관절 제한을 좌우 반전해야 하기 때문이다(RagdollRig의
                // EnableJointsWithAnatomicalLimits "좌우 반전" 문서, 2026-08-29 "이상하게 넘어짐" 수정).
                _ragdollRig = new RagdollRig(Body.transform, () => FacingSign);
            }
            return _ragdollRig;
        }

        /// <summary>
        /// 능동 상태 절차적 포즈 드라이버 캐시(StickmanPoseAnimator.cs) — GetRagdollRig()와 동일한 지연
        /// 생성/캐싱 패턴. 최초 필요 시 1회만 Body.transform을 루트로 이름 기반 관절 탐색을 수행한다.
        /// </summary>
        public StickmanPoseAnimator GetPoseAnimator()
        {
            if (_poseAnimator == null && Body != null)
            {
                _poseAnimator = new StickmanPoseAnimator(Body.transform);
            }
            return _poseAnimator;
        }

        /// <summary>
        /// 매 프레임(StickmanAgent.Update()가 _machine.Tick() 직후에 1회) 호출되는 **물리 모드 + 포즈의
        /// 단일 진실 공급원**. 2026-08-28 근본 재구현의 핵심 배선이다.
        ///
        /// 왜 각 상태의 Enter/Exit이 아니라 여기인가: 상태가 14개가 넘고, 어느 하나라도 물리 모드 복구를
        /// 빠뜨리면 그 상태에서만 캐릭터가 다시 무너진다(실제로 예전 구현이 그렇게 무너졌다). 게다가
        /// 전체화면 Suspend의 강제 취소, 테스트의 직접 ChangeState, ReportExternalImpact의 강제 인터럽트
        /// 등 상태 밖에서 상태가 바뀌는 경로가 여럿이다. "지금 상태 ID가 무엇인가"만 보고 매 프레임
        /// 멱등적으로 재적용하면 그 모든 경로가 자동으로 커버된다.
        ///
        /// 규칙:
        ///   Ragdoll  -> 전신 물리 위임(RagdollRig.EnterRagdoll). 포즈에 일절 개입하지 않는다.
        ///   Getup    -> 능동 모드로 되돌리되 루트 각도는 스냅하지 않는다(GetupState가 직접 보간).
        ///   Walk     -> 능동 모드 + 직립 스냅. 팔다리 각도는 WalkState.Tick()이 이미 사인파로 세팅했다.
        ///   그 외 능동 -> 능동 모드 + 직립 스냅 + Idle 중립 포즈(졸라맨 직립 실루엣).
        /// </summary>
        public void TickPose(float deltaTime)
        {
            TickPoseRouting(deltaTime);

            // ★ 2026-09-01 상체 기울임 — 위 라우팅에는 조기 return이 열 개 넘게 있고(상태마다 포즈
            // 주인이 다르다), 기울임은 그 **전부**에서 갱신돼야 한다. 그래서 라우팅을 감싸 여기 한
            // 곳에서만 확정한다: 포즈가 이번 프레임에 요청한 목표(TickWalkPose/ApplyIdleAmbientPose)로
            // 감쇠 접근하고, 아무도 요청하지 않았으면 목표가 0이라 자동으로 직립으로 돌아온다.
            // 상태 목록을 여기 다시 적지 않는 것이 핵심이다 — 그러면 새 상태가 생길 때마다 빠뜨린다.
            StickmanPoseAnimator leanPose = GetPoseAnimator();
            if (leanPose != null) leanPose.TickBodyLean(deltaTime, BodyLeanSmoothingRate);
        }

        private void TickPoseRouting(float deltaTime)
        {
            RagdollRig rig = GetRagdollRig();
            StickmanPoseAnimator pose = GetPoseAnimator();
            if (rig == null || pose == null || Machine == null) return;

            // 바라보는 방향 갱신 — 이동 의도가 불감대를 넘을 때만 바꾼다(0 근처에서 부호가 떨리면
            // 캐릭터가 좌우로 깜빡인다). 뚜렷한 의도가 없으면 마지막 방향을 그대로 유지한다.
            float deadzone = Config != null ? Config.moveInputDeadzone : 0.15f;
            float move = MoveInputX;
            // FacingLocked: 활쏘기처럼 한 방향을 겨누는 연출 중에는 배회 AI의 이동 의도로 몸이
            // 돌아가면 안 된다(그 필드 문서 참고).
            // ★ 2026-09-02 — 판정을 WanderIntentMayDriveFacing 하나로 모았다. 예전에는 여기서
            // FacingLocked만 봤는데, 그 플래그는 활쏘기 **조준** 구간 전용이라 **접근 구간**에서는
            // 꺼져 있고, 그 사이 배회 AI의 이동 의도가 상태가 방금 정한 방향을 매 프레임 덮었다
            // (IsFacingSelfManaged 문서의 실측 사례).
            if (WanderIntentMayDriveFacing(Machine.CurrentStateId) && Mathf.Abs(move) > deadzone)
            {
                _facingSign = move >= 0f ? 1f : -1f;
                pose.SetFacing(_facingSign);
                GetEyeController()?.SetFacing(_facingSign);
            }

            // ★ 눈 커서 추적(2026-08-28 배선 완료, 사용자 명시 요청 "마우스 위치에 따라 눈도 움직여야").
            // 상태와 무관하게 **항상** 갱신한다 — 눈은 머리의 자식이므로 RAGDOLL로 머리가 뒹구는 동안에도
            // 머리를 따라 함께 움직이고(자동), 그 위에서 EyeController가 머리 로컬 공간으로 변환된
            // 시선 방향을 계속 적용한다(뒤집힌 머리에서도 화면상 커서 쪽을 본다).
            TickEyeTracking(deltaTime);

            if (Machine.CurrentStateId == StickmanStateId.Ragdoll)
            {
                // EnterRagdoll()이 아니라 멱등 버전을 쓴다 — 전자는 **진입 이벤트 1회분**의 처리
                // (각속도 완충 + 진입 충격량)를 담고 있어 매 프레임 부르면 그게 매 프레임 반복된다
                // (RagdollRig.cs 참고). ★ 2026-09-01: 각속도 완충 비율은 1(무효)이 됐지만, 이제 그
                // 자리에 진입 충격량이 들어와 이 분리가 더 중요해졌다 — 매 프레임 때리면 랙돌이
                // 영원히 가속된다.
                // 기운 채로 전신 물리에 넘기면 관절이 부착점을 되찾으며 팔이 튄다. 감쇠를 기다리지
                // 않고 **이 프레임에** 지운다(랙돌 진입 에너지 로직은 건드리지 않는다 — 이건 시각
                // 오프셋 원복일 뿐이고, 다음 물리 스텝은 다음 프레임 FixedUpdate에서 돈다).
                pose.ClearBodyLean();
                rig.EnsureRagdollMode();
                return;
            }

            // RAGDOLL -> 능동 모드로 막 전환된 프레임에는, 물리가 마음대로 굴려놓은 실제 각도에서
            // 스무딩이 이어지도록 보간 상태값을 동기화한다(안 하면 랙돌 이전의 낡은 각도에서 튄다).
            if (rig.EnterActiveMode()) pose.SyncFromTransform();
            if (Machine.CurrentStateId == StickmanStateId.Getup) return;

            // ★ 던지기 공중 회전(2026-08-29) — **SnapRootUpright보다 먼저** 빠져나가는 유일한 능동
            // 상태다. 이 상태에서는 몸 전체의 회전을 ThrowTumbleState가 루트의 시각 회전으로 직접
            // 구동하므로, 여기서 매 프레임 직립으로 스냅해버리면 회전이 통째로 사라진다(그 스냅이
            // 바로 "루트 회전각 ≈ 0"을 보장하던 장치라, 이 예외의 범위를 이 상태 하나로 좁게 묶는다).
            // 팔다리 포즈도 상태 자신이 이미 세팅했다(LandingCrouch/Walk와 같은 관례).
            if (Machine.CurrentStateId == StickmanStateId.ThrowTumble) return;

            // ★ 붙잡혔을 때 발버둥(2026-08-29, 사용자 요청 "잡았을때 막 벗어날려는듯이 몸부림 치게끔").
            // 몸통 비틀림을 루트의 시각 회전으로 만들기 때문에 여기서도 직립 스냅을 건너뛴다.
            // 스위치가 꺼져 있으면 **예전 경로 그대로**(직립 스냅 + Idle 중립 포즈)로 되돌아간다 —
            // 그래야 "끄면 예전 거동"이 말뿐이 아니라 코드로 보장된다.
            if (Machine.CurrentStateId == StickmanStateId.Dragged)
            {
                if (Config == null || !Config.dragStruggleEnabled)
                {
                    rig.SnapRootUpright();
                    pose.ApplyIdlePose(deltaTime, BuildPoseSettings(), PoseSmoothingRate);
                }
                return; // 켜져 있으면 DragThrowState.Tick()이 이미 포즈와 몸통 비틀림을 세팅했다.
            }

            rig.SnapRootUpright();
            if (Machine.CurrentStateId == StickmanStateId.Walk) return;

            // ★ 발판 상실 공중 유예(2026-09-01) — Walk/LandingCrouch/Archery와 **완전히 같은 이유**로
            // 여기서 아무 것도 하지 않는다: 포즈를 이미 GroundLossHangState.Tick()이 자기 진행 곡선으로
            // 세팅했다. 이 상태에서는 그 "아무 것도 하지 않음"이 특히 중요하다 — 연출의 첫 박자가
            // **의도적인 무반응**(직전 상태의 마지막 그림을 그대로 유지)이라서, 여기서 Idle 중립 포즈를
            // 덧씌우면 발판을 잃는 순간 다리가 차렷 자세로 모이는 "반응"이 생겨 개그가 통째로 죽는다.
            if (Machine.CurrentStateId == StickmanStateId.GroundLossHang) return;

            // ★ 무릎앉아 착지(2026-08-29) — Walk와 **완전히 같은 이유**로 여기서 아무 것도 하지 않는다:
            // 포즈를 이미 LandingCrouchState.Tick()이 자기 진행 곡선으로 세팅했다. 이 분기를 빠뜨리면
            // 아래 ApplyIdlePose가 매 프레임 그 위에 중립 포즈를 덧씌워 연출이 통째로 사라진다.
            if (Machine.CurrentStateId == StickmanStateId.LandingCrouch) return;

            // ★ 활쏘기(2026-08-29) — Walk/LandingCrouch와 **완전히 같은 이유**로 여기서 아무것도 하지
            // 않는다: 포즈를 이미 ArcheryState.Tick()이 자기 진행 곡선으로 세팅했다. 이 분기를 빠뜨리면
            // 아래 ApplyIdlePose가 매 프레임 그 위에 중립 포즈를 덧씌워 활 자세가 통째로 사라진다.
            if (Machine.CurrentStateId == StickmanStateId.Archery) return;

            // ★ 등반(2026-09-01, 사용자 신고 "사람처럼 손으로 집고 다리를 올려서 올라가야지") —
            // Walk/LandingCrouch/Archery와 **완전히 같은 이유**로 여기서 아무것도 하지 않는다:
            // 포즈를 이미 ParkourClimbState.Tick()이 자기 진행 곡선으로 세팅했다. 이 분기가 없어서
            // 지금까지 등반 내내 아래 ApplyIdlePose가 중립 포즈를 덧씌웠고, 그래서 차렷 자세의
            // 막대기가 위로 평행이동하기만 했다(= 사용자가 말한 "어설픈 점프").
            if (Machine.CurrentStateId == StickmanStateId.ParkourClimb) return;

            // ★ 낙하 중 공중 자세(2026-08-29, 사용자 요청 "떨어질때 관절이 이상하게 꺾이면서 넘어지는데").
            // 여기에 분기가 없어서 지금까지 낙하 중에도 아래 Idle 중립 포즈가 적용됐다 — 막대기가 그대로
            // 내려오는 그림이었다. Jump도 같은 포즈를 쓰되 상승 중에는 세기가 0이라 사실상 중립이고,
            // 정점을 지나 Fall로 넘어가면서 자연스럽게 만세 자세로 이어진다.
            StickmanStateId airborne = Machine.CurrentStateId;
            if (airborne == StickmanStateId.Fall || airborne == StickmanStateId.Jump)
            {
                pose.ApplyFallPose(deltaTime, BuildPoseSettings(), BuildFallPoseSettings(),
                    PoseSmoothingRate, ComputeFallPoseIntensity());
                return;
            }

            // 매달려 내려가기 — 중립 Idle 포즈가 아니라 "팔을 위로 뻗어 모서리를 잡고 몸이 아래로
            // 늘어진" 전용 포즈를 적용한다(States/StickmanPoseAnimator.ApplyLedgeHangPose). Walk와 달리
            // 상태 자신이 아니라 여기서 적용하는 이유: 이 포즈는 Idle 중립 포즈의 자리를 대체하는 것이라
            // 위 Idle 분기와 같은 층에 두는 편이 "상태 ID 하나로 포즈가 결정된다"는 이 메서드의 계약과
            // 정확히 일치한다.
            if (Machine.CurrentStateId == StickmanStateId.LedgeHang)
            {
                pose.ApplyLedgeHangPose(deltaTime, BuildLedgeHangPoseSettings(), PoseSmoothingRate);
                return;
            }

            // ★ 유휴 앰비언트 동작(26-3, 2026-08-30 배선) — Idle 중립 포즈 **위에 얹는** 짧은 변주.
            // 진행 중이 아니면 아래 한 줄(예전 경로)이 그대로 실행되므로, 스위치를 끄거나 신호가
            // 오지 않으면 거동이 100% 예전과 같다.
            if (TickIdleAmbientMotion(deltaTime))
            {
                pose.ApplyIdleAmbientPose(deltaTime, BuildPoseSettings(), PoseSmoothingRate,
                    BuildIdleAmbientPoseSettings(), _idleAmbientMotion, IdleAmbientProgress01);
                return;
            }

            pose.ApplyIdlePose(deltaTime, BuildPoseSettings(), PoseSmoothingRate);
        }

        // ==================== 유휴 앰비언트 동작 (26-3) ====================

        private WanderAmbientMotion _idleAmbientMotion;
        private float _idleAmbientElapsed;
        private float _idleAmbientDuration; // 0 = 진행 중 아님.

        /// <summary>지금 유휴 앰비언트 동작이 재생 중인지(테스트/진단용).</summary>
        public bool IsIdleAmbientMotionActive => _idleAmbientDuration > 0f;

        /// <summary>재생 중인 동작 종류(테스트/진단용). 진행 중이 아니면 마지막 값이 남아 있으므로
        /// 반드시 <see cref="IsIdleAmbientMotionActive"/>와 함께 읽어야 한다.</summary>
        public WanderAmbientMotion CurrentIdleAmbientMotion => _idleAmbientMotion;

        /// <summary>이번 동작의 총 지속 시간(초). 진행 중이 아니면 0(테스트/진단용).</summary>
        public float IdleAmbientDurationSeconds => _idleAmbientDuration;

        /// <summary>이번 동작의 진행도 0~1(테스트/진단용).</summary>
        public float IdleAmbientProgress01 =>
            _idleAmbientDuration > 0f ? Mathf.Clamp01(_idleAmbientElapsed / _idleAmbientDuration) : 0f;

        /// <summary>
        /// 유휴 앰비언트 동작 재생 시작. 구독자(Interaction/IdleAmbientMotionRenderer.cs)가
        /// StickmanEventBus.WanderAmbientMotionRequested를 받아 그대로 넘긴다.
        ///
        /// <b>새 확률/타이머를 하나도 도입하지 않는다</b> — 언제 몇 번 나올지는 전적으로 발행자
        /// (States/AutoWanderController.cs)의 기존 조건이 정한다(리더 지시: 상위 이벤트의 발행 빈도를
        /// 그대로 물려받을 것). 여기서 정하는 것은 "얼마나 오래 재생할지"뿐이며 그것도 StickConfig 값이다.
        /// </summary>
        /// <returns>실제로 시작했으면 true. 꺼져 있거나 Idle이 아니면 false(조용히 무시).</returns>
        public bool BeginIdleAmbientMotion(WanderAmbientMotion motion)
        {
            if (Config != null && !Config.idleAmbientMotionEnabled) return false;
            // Idle이 아닌 순간에 들어온 신호는 버린다 — 걷는 중에 팔이 이마로 올라가면 그것이 곧 버그다.
            if (Machine == null || Machine.CurrentStateId != StickmanStateId.Idle) return false;

            float duration = motion == WanderAmbientMotion.SitAndYawn
                ? (Config != null ? Config.idleAmbientStretchSeconds : 2f)
                : (Config != null ? Config.idleAmbientLookAroundSeconds : 0.9f);
            if (duration <= 0f) return false;

            _idleAmbientMotion = motion;
            _idleAmbientElapsed = 0f;
            _idleAmbientDuration = duration;
            return true;
        }

        /// <summary>재생 중인 유휴 앰비언트 동작을 즉시 중단(중립 복귀는 다음 프레임의 ApplyIdlePose가 한다).</summary>
        public void CancelIdleAmbientMotion() => _idleAmbientDuration = 0f;

        /// <summary>진행 중이면 시간을 진행시키고 true. 만료됐거나 Idle을 벗어났으면 정리하고 false.</summary>
        private bool TickIdleAmbientMotion(float deltaTime)
        {
            if (_idleAmbientDuration <= 0f) return false;

            // Idle을 벗어났으면 즉시 취소 — 상태 전이가 곧 취소 신호다(별도 취소 배관을 두지 않는 이유).
            if (Machine == null || Machine.CurrentStateId != StickmanStateId.Idle
                || (Config != null && !Config.idleAmbientMotionEnabled))
            {
                _idleAmbientDuration = 0f;
                return false;
            }

            _idleAmbientElapsed += deltaTime;
            if (_idleAmbientElapsed >= _idleAmbientDuration)
            {
                _idleAmbientDuration = 0f;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 보간 없이 즉시 직립 중립 포즈로 스냅한다(StickmanAgent.Awake() 전용) — 첫 프레임부터
        /// 확정된 자세로 시작하게 만든다. 매 프레임 경로인 TickPose()와 달리 deltaTime이 없는 시점이라
        /// 지수 감쇠를 적용할 수 없으므로 별도 진입점으로 둔다.
        /// </summary>
        public void SnapToIdlePose()
        {
            RagdollRig rig = GetRagdollRig();
            StickmanPoseAnimator pose = GetPoseAnimator();
            if (rig == null || pose == null) return;
            rig.EnterActiveMode();
            rig.SnapRootUpright();
            pose.ApplyIdlePoseImmediate(BuildPoseSettings());
            GetEyeController()?.LookForward();
        }

        /// <summary>
        /// 눈 커서 추적 1프레임 갱신 + 진단 로그. TickPose()에서만 호출된다.
        ///
        /// 진단(리더 지시 "커서 위치에 따른 눈동자 오프셋 값을 로그로 찍어 검증"): 실제 눈 움직임은
        /// 사용자만 볼 수 있으므로, 커서/머리 좌표와 실제 적용된 오프셋을 함께 남긴다. 상주 앱의 로그를
        /// 더럽히지 않도록 **시작 직후 EyeLogSampleCount회만** 남기고 그 뒤로는 조용해진다
        /// (StickConfig.verboseDiagnosticsLogging을 켜면 EyeLogIntervalSeconds 주기로 계속 남는다 —
        /// [발판리포트]/[창진단]과 동일한 스위치 컨벤션).
        /// </summary>
        private void TickEyeTracking(float deltaTime)
        {
            EyeController eyes = GetEyeController();
            if (eyes == null) return;

            // ★★ 2026-08-31 — "주위 살피기" 중에는 커서 추적 대신 **눈동자가 좌우를 훑는다**.
            //
            // 무엇을 고친 것인가: 예전에는 이 연출이 머리 Transform을 좌우로 밀었다
            // (StickmanPoseAnimator.SetBodyOffset의 headOffsetX). 그런데 이 리그에는 목 관절이 없다 —
            // 목은 Torso LineRenderer의 윗부분이고 루트 로컬 x=0에 고정돼 있어서, 머리만 옆으로 밀면
            // 정의상 머리가 목에서 미끄러진다(사용자 신고 "머리를 움직이는데 목에서 벗어나서 이상함").
            // 그 경로는 StickConfig.idleAmbientLookHeadShiftRatio = 0으로 껐고, 잃어버린 "두리번거림"
            // 신호를 구조적으로 안전한 곳 — 머리의 자식이고 링 안쪽으로 실측 clamp되는 눈동자 —
            // 으로 옮긴다. 어떤 배율에서도 눈이 머리 밖으로 나갈 수 없다(EyeController._measuredSafeOffset).
            //
            // 포락선은 포즈와 **완전히 같은 식**이다(ApplyIdleAmbientPose의 env). 양 끝이 정확히 0이라
            // 시작/끝에서 눈이 튀지 않고, 동작이 끝나면 다음 프레임부터 아래 커서 추적이 그대로 이어받는다
            // (SetLookDirection은 즉시 대입이지만 TickLookAt이 지수 감쇠로 되돌리므로 복귀도 부드럽다).
            if (TryGetIdleAmbientEyeSweep(out float eyeSweepX))
            {
                eyes.SetLookDirection(new Vector2(eyeSweepX, 0f));
                return; // 진단 로그는 커서 추적 표본만 남긴다(연출 중 표본이 섞이면 추적 검증이 흐려진다).
            }

            bool hasCursor = TryGetCursorWorldPosition(out Vector2 cursorWorld);
            eyes.TickLookAt(hasCursor, cursorWorld, deltaTime, BuildEyeTrackingSettings());

            bool verbose = Config != null && Config.verboseDiagnosticsLogging;
            if (!verbose && _eyeLogSamplesLeft <= 0) return;

            _eyeLogTimer += deltaTime;
            if (_eyeLogTimer < EyeLogIntervalSeconds) return;
            _eyeLogTimer = 0f;
            if (!verbose) _eyeLogSamplesLeft--;

            Vector2 offset = eyes.CurrentPupilOffset;
            Vector2 headWorld = Vector2.zero;
            float distance = -1f;
            if (Body != null)
            {
                headWorld = Body.position;
                if (hasCursor) distance = Vector2.Distance(cursorWorld, headWorld);
            }
            Debug.Log($"[눈추적] 커서={(hasCursor ? cursorWorld.ToString("F2") : "(조회 실패)")}, " +
                $"몸통={headWorld.ToString("F2")}, 거리={distance:F2}유닛, " +
                $"눈동자오프셋={offset.ToString("F4")}(길이 {offset.magnitude:F4}), " +
                $"시선={eyes.CurrentLookDirection.ToString("F3")}, 눈발견={eyes.HasEyes}, " +
                $"상태={(Machine != null ? Machine.CurrentStateId.ToString() : "?")}.");
        }

        /// <summary>
        /// "주위 살피기"가 지금 눈동자에 줘야 할 좌우 오프셋(-1~1). 진행 중이 아니거나 폭이 0이면 false.
        ///
        /// 진행 곡선은 StickmanPoseAnimator.ApplyIdleAmbientPose와 **글자 그대로 같은 두 줄**이다
        /// (sin(2*pi*p) x smoothstep(sin(pi*p))) — 팔과 눈이 같은 리듬으로 움직여야 한 동작으로 읽힌다.
        /// 두 곳에 같은 식이 있는 것은 의도적이다: 포즈 계산은 무상태 순수 함수이고 이쪽은
        /// EyeController를 잡고 있어 서로를 부를 수 없으며, 억지로 공유하면 포즈 레이어가 눈을 알게 된다.
        /// 대신 <b>같은 식이라는 사실 자체를 테스트가 잠근다</b>
        /// (Tests/EditMode/IdleAmbientLookAroundInvariantTests.cs).
        /// </summary>
        public bool TryGetIdleAmbientEyeSweep(out float sweepX)
        {
            sweepX = 0f;
            if (!IsIdleAmbientMotionActive) return false;
            if (_idleAmbientMotion != WanderAmbientMotion.LookAround) return false;
            // 상태가 이미 Idle을 벗어났으면 이번 프레임부터 연출은 없는 것으로 본다 — TickIdleAmbientMotion이
            // 같은 프레임 뒤쪽에서 이걸 정리하므로, 여기서 먼저 막지 않으면 한 프레임 눈만 남는다.
            if (Machine == null || Machine.CurrentStateId != StickmanStateId.Idle) return false;

            float amplitude = Config != null ? Mathf.Clamp01(Config.idleAmbientLookEyeSweep01) : 0.85f;
            if (amplitude <= 0f) return false;
            if (Config != null && !Config.idleAmbientMotionEnabled) return false;

            float p = Mathf.Clamp01(IdleAmbientProgress01);
            float raw = Mathf.Sin(p * Mathf.PI);
            float env = raw * raw * (3f - 2f * raw);
            sweepX = Mathf.Sin(p * Mathf.PI * 2f) * env * amplitude;
            return true;
        }

        /// <summary>StickConfig의 눈 추적 튜닝 값 묶음(미배선 경로에서도 안전한 기본값 사용).</summary>
        public EyeController.EyeTrackingSettings BuildEyeTrackingSettings()
        {
            if (Config == null) return EyeController.EyeTrackingSettings.Default;
            return new EyeController.EyeTrackingSettings(
                Config.eyeTrackingEnabled,
                Config.eyeMaxPupilOffset,
                Config.eyeTrackingFollowRate,
                Config.eyeTrackingNeutralRadiusWorld,
                Config.eyeTrackingFullRangeWorld);
        }

        // 눈 추적 진단 로그 상태(TickEyeTracking 문서 참고).
        private const float EyeLogIntervalSeconds = 2f;
        private const int EyeLogInitialSamples = 6;
        private int _eyeLogSamplesLeft = EyeLogInitialSamples;
        private float _eyeLogTimer;

        /// <summary>머리 안 눈동자 점 제어 캐시 — GetRagdollRig()와 동일한 지연 생성/캐싱 패턴.</summary>
        public EyeController GetEyeController()
        {
            if (_eyeController == null && Body != null)
            {
                _eyeController = new EyeController(Body.transform);
            }
            return _eyeController;
        }

        /// <summary>Idle 중립 다리 벌림 각도(도) — Config 미배선 시에도 안전한 기본값을 쓴다.</summary>
        public float IdleLegSpreadDegrees => Config != null ? Config.idleLegSpreadDegrees : 12f;

        /// <summary>Idle 중립 팔 벌림 각도(도).</summary>
        public float IdleArmSpreadDegrees => Config != null ? Config.idleArmSpreadDegrees : 40f;

        /// <summary>
        /// StickConfig에서 포즈 각도 설정 묶음을 구성한다(readonly struct — 매 프레임 호출 경로라 힙
        /// 할당이 없다). Config가 배선되지 않은 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다.
        /// </summary>
        public StickmanPoseAnimator.PoseSettings BuildPoseSettings()
        {
            return new StickmanPoseAnimator.PoseSettings(
                IdleLegSpreadDegrees,
                IdleArmSpreadDegrees,
                Config != null ? Config.idleKneeBendDegrees : 4f,
                Config != null ? Config.idleElbowBendDegrees : 10f,
                Config != null ? Config.idleBreathAmplitude : 0.012f,
                Config != null ? Config.idleBreathFrequencyHz : 0.8f,
                Config != null ? Config.idleBreathArmDegrees : 1.5f);
        }

        /// <summary>
        /// 캐릭터 실측 치수 조회 창구(Core/StickmanMetrics.cs) — 지연 조회 + 캐싱(GetPoseAnimator와
        /// 동일한 컨벤션). 프리팹/테스트 리그에 컴포넌트가 없으면 null이며, 호출부는 반드시
        /// <see cref="CharacterHeightWorld"/>처럼 폴백이 있는 경로를 쓴다.
        /// </summary>
        public StickmanMetrics Metrics
        {
            get
            {
                if (_metrics == null && Body != null) _metrics = StickmanMetrics.Find(Body);
                return _metrics;
            }
        }

        /// <summary>
        /// 지금 캐릭터의 실측 신장(월드 유닛). 낙하/착지 연출의 **거리·속도 성분을 무차원화**하는 분모다
        /// (리더 지시: "거리·속도 성분은 StickmanMetrics에서 파생시켜라. 각도는 크기와 무관하니 절대값").
        /// StickmanMetrics를 못 찾는 폴백 경로에서는 배율 1.0 기준 신장으로 되메운다 — 0을 돌려주면
        /// 나눗셈이 무한대가 되어 연출이 조용히 망가진다.
        /// </summary>
        public float CharacterHeightWorld
        {
            get
            {
                StickmanMetrics m = Metrics;
                float h = m != null ? m.TotalHeight : 0f;
                return h > 0.0001f ? h : StickConfig.BaselineCharacterTotalHeight;
            }
        }

        /// <summary>낙하 자세 각도 묶음(StickConfig -> StickmanPoseAnimator). BuildPoseSettings와 동일한
        /// 패턴 — Config가 없는 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다.</summary>
        public StickmanPoseAnimator.FallPoseSettings BuildFallPoseSettings()
        {
            return new StickmanPoseAnimator.FallPoseSettings(
                Config != null ? Config.fallPoseArmRaiseDegrees : 143f,
                Config != null ? Config.fallPoseElbowBendDegrees : 20f,
                Config != null ? Config.fallPoseLegSpreadDegrees : 15f,
                Config != null ? Config.fallPoseHipDegrees : 14f,
                Config != null ? Config.fallPoseKneeBendDegrees : 38f);
        }

        /// <summary>
        /// 발판 상실 공중 유예 자세(제자리 종종걸음 + 팔 허우적) 각도/배수 묶음
        /// (StickConfig -> StickmanPoseAnimator). BuildFallPoseSettings와 완전히 같은 패턴이며,
        /// 전부 <b>각도와 무차원 배수</b>라 캐릭터 배율 환산이 필요 없다(리더 지시: "각도는 크기와
        /// 무관하니 절대값, 거리·속도 성분만 StickmanMetrics에서 파생").
        /// </summary>
        public StickmanPoseAnimator.GroundLossHangPoseSettings BuildGroundLossHangPoseSettings()
        {
            return new StickmanPoseAnimator.GroundLossHangPoseSettings(
                Config != null ? Config.groundLossHangLegCycleSpeedMultiplier : 3f,
                Config != null ? Config.groundLossHangLegAmplitudeScale : 1f,
                Config != null ? Config.groundLossHangArmFlailBaseDegrees : 125f,
                Config != null ? Config.groundLossHangArmFlailDegrees : 48f,
                Config != null ? Config.groundLossHangArmFlailFrequencyRatio : 0.63f,
                Config != null ? Config.groundLossHangElbowBendDegrees : 22f);
        }

        /// <summary>활 쏘는 자세 각도 묶음(StickConfig -> StickmanPoseAnimator). BuildPoseSettings와
        /// 동일한 패턴 — Config가 없는 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다.
        ///
        /// ★ 그 기본값은 <b>StickConfig의 실효값과 같아야 한다</b>(2026-09-01). 이 자리의 리터럴은
        /// <c>Config == null</c>일 때만 쓰이므로 어긋나도 화면이 안 바뀌고, 그래서 조용히 낡는다 —
        /// 실제로 이 메서드의 네 각도가 전부 옛 값에 멈춰 있었다(88/93/-100/100 vs 104/108/-99/119).
        /// <c>Tests/EditMode/ConfigFallbackLiteralDriftTests</c>가 이제 자동으로 대조한다.
        /// 마지막 인자(몸이 가라앉는 거리)만 <b>신장 비율 -> 월드 거리</b>로 여기서 환산한다
        /// (각도는 크기 무관, 거리는 신장 비례 — 리더 지시).</summary>
        public StickmanPoseAnimator.ArcheryPoseSettings BuildArcheryPoseSettings()
        {
            return new StickmanPoseAnimator.ArcheryPoseSettings(
                Config != null ? Config.archeryBowArmDegrees : 104f,
                Config != null ? Config.archeryBowForearmDegrees : 108f,
                Config != null ? Config.archeryDrawUpperDegrees : -99f,
                Config != null ? Config.archeryDrawForearmDegrees : 119f,
                Config != null ? Config.archeryRecoilOpenDegrees : -38f,
                Config != null ? Config.archeryRecoilStraighten01 : 0.75f,
                Config != null ? Config.archeryFrontHipDegrees : 16f,
                Config != null ? Config.archeryRearHipDegrees : -18f,
                Config != null ? Config.archeryKneeBendDegrees : 12f,
                CharacterHeightWorld * (Config != null ? Config.archeryDrawBodySinkRatio : 0.022f));
        }

        /// <summary>활쏘기 포즈의 지수 감쇠 계수(1/초) — LandingCrouchPoseSmoothingRate와 같은 관례.</summary>
        public float ArcheryPoseSmoothingRate => Config != null ? Config.archeryPoseSmoothingRate : 46f;

        // ==================== 상체 기울임 (2026-09-01) ====================
        // 마스터 스위치가 꺼지면 세 용도가 **전부** 0이 된다 — 스위치의 의미를 호출부마다 해석하지
        // 않도록 여기 한 곳에서만 판단한다(Config가 없는 테스트 리그에서도 안전한 기본값을 둔다).

        private bool BodyLeanEnabled => Config == null || Config.bodyLeanEnabled;

        /// <summary>명령 속도에 도달했을 때의 전방 기울임(도). WalkState/ArcheryState가 TickWalkPose에 넘긴다.</summary>
        public float RunBodyLeanDegrees
            => BodyLeanEnabled ? (Config != null ? Config.bodyLeanRunMaxDegrees : 10f) : 0f;

        /// <summary>발판 상실 공중 유예의 '낙하 전조' 상체 기울임 목표 각도(도).
        /// 마스터 스위치(bodyLeanEnabled)가 꺼지면 다른 두 용도와 함께 0이 된다.</summary>
        public float GroundLossHangFallTellLeanDegrees
            => BodyLeanEnabled ? (Config != null ? Config.groundLossHangFallTellLeanDegrees : 26f) : 0f;

        /// <summary>'주위 살피기'의 상체 좌우 왕복 각도(도).</summary>
        public float LookAroundBodyLeanDegrees
            => BodyLeanEnabled ? (Config != null ? Config.bodyLeanLookAroundDegrees : 7f) : 0f;

        /// <summary>랙돌 임계값 미만 피격의 상체 튕김 각도(도).</summary>
        public float HitBodyLeanDegrees
            => BodyLeanEnabled ? (Config != null ? Config.bodyLeanHitDegrees : 14f) : 0f;

        /// <summary>피격 기울임의 복구 계수(1/초).</summary>
        public float HitBodyLeanRecoverRate => Config != null ? Config.bodyLeanHitRecoverRate : 7f;

        /// <summary>기울임이 목표를 따라가는 지수 감쇠 계수(1/초).</summary>
        public float BodyLeanSmoothingRate => Config != null ? Config.bodyLeanSmoothingRate : 12f;

        /// <summary>유휴 앰비언트 동작(26-3) 각도/거리 묶음. 거리 성분 2개만 신장을 곱해 환산한다
        /// (BuildArcheryPoseSettings와 완전히 같은 관례 — 폴백 리터럴이 StickConfig 실효값과 같아야
        /// 한다는 것까지 포함해서. 팔꿈치 122°와 머리 이동 0.035는 각각 98°/0으로 바뀐 뒤에도 여기만
        /// 옛 값에 남아 있었다).</summary>
        public StickmanPoseAnimator.IdleAmbientPoseSettings BuildIdleAmbientPoseSettings()
        {
            return new StickmanPoseAnimator.IdleAmbientPoseSettings(
                Config != null ? Config.idleAmbientLookArmDegrees : 107f,
                Config != null ? Config.idleAmbientLookElbowDegrees : 98f,
                CharacterHeightWorld * (Config != null ? Config.idleAmbientLookHeadShiftRatio : 0f),
                Config != null ? Config.idleAmbientStretchArmSpreadDegrees : 13f,
                Config != null ? Config.idleAmbientStretchElbowDegrees : 16f,
                Config != null ? Config.idleAmbientStretchKneeStraighten01 : 0.7f,
                CharacterHeightWorld * (Config != null ? Config.idleAmbientStretchRiseRatio : 0.030f),
                LookAroundBodyLeanDegrees);
        }

        /// <summary>공중 회전(텀블링) 자세 각도 묶음(StickConfig -> StickmanPoseAnimator).
        /// BuildFallPoseSettings와 동일한 패턴 — Config가 없는 테스트/폴백 경로에서도 안전하도록
        /// 각 값에 기본값을 둔다.</summary>
        public StickmanPoseAnimator.ThrowTumblePoseSettings BuildThrowTumblePoseSettings()
        {
            return new StickmanPoseAnimator.ThrowTumblePoseSettings(
                Config != null ? Config.throwTumbleHipDegrees : 76f,
                Config != null ? Config.throwTumbleKneeBendDegrees : 104f,
                Config != null ? Config.throwTumbleArmDegrees : 46f,
                Config != null ? Config.throwTumbleElbowBendDegrees : 96f,
                Config != null ? Config.throwTumbleLimbSpreadDegrees : 9f);
        }

        /// <summary>발버둥 자세 각도 묶음(StickConfig -> StickmanPoseAnimator). BuildFallPoseSettings와
        /// 동일한 패턴 — Config가 없는 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다.</summary>
        public StickmanPoseAnimator.DragStrugglePoseSettings BuildDragStrugglePoseSettings()
        {
            return new StickmanPoseAnimator.DragStrugglePoseSettings(
                Config != null ? Config.dragStruggleFrequencyHz : 3.4f,
                Config != null ? Config.dragStruggleHipDegrees : 34f,
                Config != null ? Config.dragStruggleKneeDegrees : 40f,
                Config != null ? Config.dragStruggleArmDegrees : 46f,
                Config != null ? Config.dragStruggleElbowDegrees : 38f);
        }

        /// <summary>무릎앉아 착지 포즈의 최대 깊이 각도 묶음(StickConfig -> StickmanPoseAnimator).</summary>
        public StickmanPoseAnimator.LandingCrouchPoseSettings BuildLandingCrouchPoseSettings()
        {
            return new StickmanPoseAnimator.LandingCrouchPoseSettings(
                Config != null ? Config.landingCrouchFrontHipDegrees : 82f,
                Config != null ? Config.landingCrouchFrontKneeDegrees : 126f,
                Config != null ? Config.landingCrouchRearHipDegrees : -40f,
                Config != null ? Config.landingCrouchRearKneeDegrees : 55f,
                Config != null ? Config.landingCrouchFrontArmDegrees : 64f,
                Config != null ? Config.landingCrouchFrontElbowDegrees : 26f,
                Config != null ? Config.landingCrouchRearArmDegrees : -128f,
                Config != null ? Config.landingCrouchRearElbowDegrees : 24f);
        }

        /// <summary>무릎앉아 포즈 각도의 지수 감쇠 계수(1/초) — 왜 poseSmoothingRate보다 높은지는
        /// StickConfig.landingCrouchPoseSmoothingRate Tooltip 참고.</summary>
        public float LandingCrouchPoseSmoothingRate =>
            Config != null && Config.landingCrouchPoseSmoothingRate > 0f
                ? Config.landingCrouchPoseSmoothingRate
                : 48f;

        /// <summary>
        /// 낙하 자세의 세기(0~1) — "지금 얼마나 빠르게 떨어지고 있는가".
        ///
        /// 하강 속도를 **신장으로 나눠** 무차원화한 뒤(초당 몇 신장을 떨어지는가)
        /// StickConfig.fallPoseFullSpeedHeightsPerSecond를 1로 보는 비율을 만든다. 그래서 캐릭터
        /// 배율이 바뀌어도 "같은 체감 속도에서 같은 자세"가 유지된다 — 속도를 절대 유닛/초로 재면
        /// 작은 캐릭터일수록 같은 낙하에서 자세가 더 세게 나오는 어긋남이 생긴다.
        ///
        /// 상승 중(velocity.y &gt; 0)에는 0이 되어 Jump 상승 구간에서는 사실상 중립 포즈다.
        /// StickConfig.fallPoseMinIntensity가 바닥을 받쳐, 정점 부근에서 자세가 한 번 완전히 풀렸다가
        /// 다시 잡히는 깜빡임을 막는다.
        /// </summary>
        public float ComputeFallPoseIntensity()
        {
            float downward = Body != null ? Mathf.Max(0f, -Body.linearVelocity.y) : 0f;
            float full = Config != null ? Config.fallPoseFullSpeedHeightsPerSecond : 7f;
            float minIntensity = Config != null ? Mathf.Clamp01(Config.fallPoseMinIntensity) : 0.16f;
            if (full <= 0.0001f) return 1f;

            float heightsPerSecond = downward / CharacterHeightWorld;
            float t = Mathf.Clamp01(heightsPerSecond / full);
            return Mathf.Max(minIntensity, t);
        }

        /// <summary>실측 검증/디버그용 — 마지막으로 확정된 바라보는 방향 부호(+1 오른쪽 / -1 왼쪽).</summary>
        public float FacingSign => _facingSign;

        /// <summary>
        /// 바라보는 방향을 강제로 지정한다(+1 오른쪽 / -1 왼쪽). <see cref="TickPose"/>가 이동 의도로
        /// 방향을 갱신하는 것과 <b>정확히 같은 3가지</b>를 한다(내부 부호 + 포즈 애니메이터 + 눈).
        ///
        /// 왜 필요한가(2026-08-29 사용자 신고 "활을 이상하게 들고있음"의 근본 원인): 활쏘기는 캐릭터가
        /// 화면 끝에 있으면 과녁을 <b>반대편으로 미러링</b>해 놓는다. 그런데 그때 몸의 방향을 함께
        /// 돌리지 않아, 캐릭터가 과녁을 등지고 선 채로 활을 반대쪽(등 뒤)으로 들고 쏘는 그림이 됐다.
        /// 게다가 활쏘기 중에는 <see cref="FacingLocked"/>가 걸려 배회 AI가 방향을 고쳐줄 수도 없었다.
        /// 이제 연출을 시작하는 쪽이 "과녁을 향해 돌아선다"를 명시적으로 지시한다.
        /// </summary>
        public void SetFacingSign(float sign)
        {
            _facingSign = sign >= 0f ? 1f : -1f;
            GetPoseAnimator()?.SetFacing(_facingSign);
            GetEyeController()?.SetFacing(_facingSign);
        }

        /// <summary>팔다리 각도 지수 감쇠 계수(1/초).</summary>
        public float PoseSmoothingRate => Config != null ? Config.poseSmoothingRate : 35f;

        /// <summary>보행 주파수 입력 속도의 지수 감쇠 계수(1/초).</summary>
        public float WalkSpeedSmoothingRate => Config != null ? Config.walkSpeedSmoothingRate : 6f;

        /// <summary>
        /// ParkourClimb 진입 판정(아키텍처 0절 파쿠르, UX_FLOW.md 4절). 지금 딛고 있는 발판의 진행방향
        /// 경계에 근접(parkourDetectionRadius 이내)했고, 그 경계 너머 가까이에 상단이 눈에 띄게 더 높은
        /// (parkourDetectionRadius 이상) 다른 발판이 있으면 "벽"으로 판정한다. 비슷하거나 더 낮은 발판은
        /// 파쿠르가 아니라 평범한 점프/낙하 대상이므로 제외한다. 좌표 변환/발판 순회는 전부 GroundSensor에
        /// 위임한다(States/*.cs가 직접 좌표 변환식을 만들지 않는 기존 컨벤션 유지).
        /// </summary>
        public bool TryFindClimbableWall(GroundSensor.GroundInfo info, int direction, out long wallHandle, out float wallTopWorldY)
        {
            wallHandle = 0L;
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            // ★ 2026-08-31 — 경계 근접 게이트만 유도값으로 넘긴다(EdgeProbeReachWorld 문서 참고).
            bool found = GroundSensor.TryFindClimbableWall(MainCamera, foot, info, direction, footholds, Config,
                out PlatformFoothold wall, out wallTopWorldY, EdgeProbeReachWorld);
            if (found) wallHandle = wall.Handle;
            return found;
        }

        /// <summary>
        /// ParkourClimb 등반 도중, handle로 식별된 발판이 여전히 존재하는지 매 프레임 재확인하고 존재하면
        /// 그 발판의 최신 상단 월드 Y를 반환한다(창이 이동했을 수 있으므로 매 프레임 재계산). 존재하지
        /// 않으면 false — "잡을 곳이 사라짐(창 이동/닫힘)" 실패 처리(UX_FLOW.md 4절)에 사용한다.
        /// </summary>
        public bool TryGetFootholdTopWorldY(long handle, out float topWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 refPos = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.TryGetFootholdTopWorldY(MainCamera, refPos, handle, footholds, Config, out topWorldY);
        }

        /// <summary>
        /// 매달려 내려가기(LedgeHang) 진입 판정 — TryFindClimbableWall의 반대 방향. 진행방향 경계 바깥으로
        /// 내려섰을 때 실제로 내려앉을 더 낮은 발판이 있는지 확인한다(없으면 매달리지 않고 기존 배회
        /// 거동대로 돌아선다). 좌표 변환/발판 순회는 전부 GroundSensor에 위임(BUG-M5 컨벤션).
        /// </summary>
        public bool TryFindDescendTarget(GroundSensor.GroundInfo info, int direction, out long targetHandle, out float targetTopWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            float outward = Config != null ? Config.ledgeHangEdgeOffset : 0.14f;
            // 상한 없음(0) — 매달리기는 "깊으면 깊을수록" 성립한다. 하한만이 안전 조건이다.
            return GroundSensor.TryFindDescendTarget(MainCamera, foot, info, direction, footholds, Config, outward,
                LedgeHangMinDropDepth, 0f, out targetHandle, out targetTopWorldY, EdgeProbeReachWorld);
        }

        /// <summary>
        /// ★ 뛰어내리기(HopDown) 진입 판정(2026-08-29, 사용자 결정 "낙차가 작으면 뛰어내리게 한다").
        /// 위 <see cref="TryFindDescendTarget"/>와 **같은 함수**를 쓰되 낙차 밴드만 반대편을 물어본다:
        /// [<see cref="StickConfig.hopDownMinDropHeight"/>, <see cref="HopDownMaxDropHeight"/>).
        ///
        /// 두 판정은 구조적으로 상호 배타다 — HopDownMaxDropHeight의 기본값이 곧
        /// <see cref="LedgeHangMinDropDepth"/>이기 때문이다. 그래서 "둘 다 성립"(어느 쪽을 할지 모호)도
        /// "둘 다 불성립"(내려갈 곳이 있는데 아무 것도 안 함)도 생기지 않는다. 다만 목적지가 여러 개인
        /// 배치에서는 서로 **다른 발판**을 고를 수 있으므로(예: 1유닛 아래에 턱, 5유닛 아래에 바닥),
        /// 호출부(AutoWanderController)는 반드시 이쪽을 **먼저** 물어야 한다 — 실제로 발이 먼저 닿는
        /// 면은 언제나 더 가까운 쪽이고, 그 위로 매달려 지나가면 몸이 발판을 파고든다.
        /// </summary>
        public bool TryFindHopDownTarget(GroundSensor.GroundInfo info, int direction, out long targetHandle, out float targetTopWorldY)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            float outward = Config != null ? Config.hopDownProbeOutward : 0.2f;
            float minDrop = Config != null ? Config.hopDownMinDropHeight : 0.35f;
            return GroundSensor.TryFindDescendTarget(MainCamera, foot, info, direction, footholds, Config, outward,
                Mathf.Max(0.0001f, minDrop), HopDownMaxDropHeight, out targetHandle, out targetTopWorldY,
                EdgeProbeReachWorld);
        }

        /// <summary>
        /// 매달리기가 성립하려면 필요한 **최소 낙차**(월드 유닛). 손끝~발끝 거리(<see cref="LedgeHangDropDepth"/>)가
        /// 본질이지만, 파쿠르 감지 반경보다 작아지는 퇴화 배치(팔을 못 찾는 폴백 등)에서 판정이 무의미해지지
        /// 않도록 그 값으로 바닥을 받쳐둔다 — 2026-08-29 이전에 GroundSensor 안에 하드코딩돼 있던
        /// Mathf.Max(detectionRadius, dropDepth)를 그대로 옮겨온 것이라 기존 거동과 100% 동일하다.
        /// 이 값이 곧 "매달리기 / 뛰어내리기"의 분기 임계값이다.
        ///
        /// ============================================================================
        /// ★ 2026-09-01 — "매달리기 진입 0회"에 대한 답: 이 값은 임계값이 아니라 **기하학이다**
        /// ============================================================================
        /// MOTION_SPEC 1절 표 #5와 UX_FLOW.md 31-4-3은 이 값(배율 1.0에서 약 2.507유닛 = <b>1.10 H</b>)을
        /// "Dock 단차 0.72 H보다 높아서 매달리기가 구조적으로 도달 불가"인 **임계값 문제**로 분류했다.
        /// 코드를 실제로 재보면 그 분류는 절반만 맞다 — 증상은 참이지만 원인은 튜닝 상수가 아니다.
        ///
        /// <code>
        /// 매달린 루트(=발) Y = 모서리 Y − LedgeHangDropDepth      (LedgeHangState.Tick의 hangPos)
        /// 진입 조건(이 클래스의 불변식): 목적지 발판 상단 ≤ 매달린 발
        ///   ⟺ 낙차 ≥ LedgeHangDropDepth
        /// </code>
        ///
        /// 즉 이 임계값은 "얼마나 깊어야 매달릴 맛이 나는가"라는 <b>연출 판단</b>이 아니라,
        /// <b>"매달린 발이 아래 발판을 파고들지 않으려면"</b>이라는 물리 조건 그 자체다. 아래 낙차에서
        /// 매달리면 다리가 창(발판) 안으로 들어간 그림이 된다 — LedgeHangState 클래스 문서의
        /// "발이 목적지를 지나치지 않음" 항목이 그 불변식이다.
        ///
        /// 그리고 이 값은 낮출 수도 없다. <see cref="LedgeHangDropDepth"/>는 어깨 높이 + 팔 길이인데,
        /// 팔을 아무리 접어도 <b>어깨 높이(≈ 0.8 H) 아래로는 내려가지 않는다</b>. Dock 단차는 0.72 H라
        /// 어깨보다도 낮다 — 자기 어깨보다 낮은 턱을 두 손으로 붙잡고 매달릴 수는 없다.
        /// <b>Dock에서 매달리기가 안 나오는 것은 버그가 아니라 그 자세가 성립하지 않기 때문이고,</b>
        /// 그래서 그 구간은 설계대로 "뛰어내리기"(HopDown)가 전담한다(두 밴드는 이 값에서 정확히 맞물린다).
        /// 매달리기가 실제로 나오는 자리는 창-창 사이처럼 1.10 H 이상 낙차가 있는 배치다.
        ///
        /// ★ 그러므로 31-2 #6의 대사 임계값(1.6 H)만 H 배수로 옮겼고, 이 값의 <b>기하 성분</b>은
        ///   <b>의도적으로 그대로 둔다</b>. 여기를 낮추면 위 불변식이 깨져 "다리가 창을 파고드는" 그림이
        ///   돌아온다. Dock 단차에서도 매달리는 그림을 원한다면 임계값이 아니라 **접힌 팔 매달림(tucked hang)
        ///   자세**를 새로 만들어야 하고, 그건 StickmanPoseAnimator의 신규 곡선이다(리더 판단 대상).
        ///
        /// ============================================================================
        /// ★★ 2026-09-02 — 기하 조건 <b>위에</b> 연출 조건 하나를 더 얹었다
        /// ============================================================================
        /// 위 기하 조건("발이 목적지를 파고들지 않는다")은 <b>필요조건이지 충분조건이 아니었다</b>.
        /// 낙차가 그 임계에 딱 걸린 배치에서는 매달린 발이 착지면 <b>바로 위</b>에 있어서, 손을 놓아도
        /// 실질적으로 떨어지지 않았다 — 실측(배율 0.60) 발바닥~바닥 <b>5.44pt</b> = 다리 획 두께의 1.77배.
        /// 그래서 <see cref="StickConfig.ledgeHangMinVisibleDropHeights"/>(0.50 H)를 <b>이 유도에만</b> 더한다:
        /// <code>
        /// LedgeHangMinDropDepth = LedgeHangDropDepth + ledgeHangMinVisibleDropHeights x 신장
        ///                       = 1.1022 H + 0.50 H = 1.6022 H  (배율 1.0에서 3.6445유닛)
        /// </code>
        /// 0.50 H인 이유: <see cref="StickConfig.landingSoftAbsorbThresholdHeights"/>(0.35 H) <b>아래는
        /// LandingCrouch에 진입조차 못 한다</b>. 그 값을 갓 넘긴 낙차는 T0.5 램프의 t0=0이라 깊이 0.08/
        /// 지속 0.14초 — "반응은 있는데 안 보인다". 0.50 H에서 깊이 0.185 / 지속 0.191초 / 먼지 0.127이 된다.
        ///
        /// <para><b>손 위치 계약은 무영향이다</b>: 매달린 루트 Y를 정하는 것은 여전히
        /// <see cref="LedgeHangDropDepth"/>이고(LedgeHangState.Tick의 hangPos), 이 프로퍼티는 오직
        /// <b>진입 판정</b>과 <see cref="HopDownMaxDropHeight"/>만 쓴다. 그래서 두 밴드는 여전히 정확히
        /// 맞물리고(틈도 겹침도 없다) LedgeHangHandAlignmentTests의 단언은 그대로 성립한다.</para>
        ///
        /// <para><b>네거티브 컨트롤</b>: 그 설정값을 0으로 두면 이 프로퍼티는 2026-09-01까지의 값과
        /// 비트 단위로 같아진다.</para>
        /// </summary>
        public float LedgeHangMinDropDepth
        {
            get
            {
                float detectionRadius = Config != null ? Config.parkourDetectionRadius : 0.5f;
                // 0.50f 폴백은 StickConfig.ledgeHangMinVisibleDropHeights의 코드 기본값과 같아야 한다
                // (Tests/EditMode/ConfigFallbackLiteralDriftTests가 이 짝을 자동으로 감시한다).
                float visibleHeights = Config != null ? Config.ledgeHangMinVisibleDropHeights : 0.50f;
                float visibleDrop = Mathf.Max(0f, visibleHeights) * CharacterHeightWorld;
                return Mathf.Max(detectionRadius, LedgeHangDropDepth + visibleDrop);
            }
        }

        /// <summary>
        /// 뛰어내리기로 처리할 낙차의 상한(이 값 자신은 제외). StickConfig.hopDownMaxDropHeight가 0 이하면
        /// <see cref="LedgeHangMinDropDepth"/>를 자동으로 쓴다(권장 기본값) — 그래야 두 밴드가 정확히
        /// 맞물려 틈도 겹침도 생기지 않는다.
        /// </summary>
        public float HopDownMaxDropHeight
        {
            get
            {
                float configured = Config != null ? Config.hopDownMaxDropHeight : 0f;
                return configured > 0f ? configured : LedgeHangMinDropDepth;
            }
        }

        /// <summary>
        /// 매달렸을 때 **발끝이 모서리보다 얼마나 아래로 내려가는가**(월드 유닛) = 손끝~발끝 거리.
        /// 한 값이 두 곳에서 동시에 쓰인다(그래서 여기 한 곳에서만 계산한다):
        ///   (1) LedgeHangState가 "손이 모서리에 정확히 닿는" 루트 Y를 정할 때 (모서리 Y − 이 값),
        ///   (2) TryFindDescendTarget이 "내려갈 발판이 매달린 발보다 아래인가"를 판정할 때.
        /// 프리팹의 실제 어깨 높이/팔 길이에서 유도되므로 목 길이나 팔 길이를 바꿔도 자동으로 따라온다.
        /// 팔을 찾지 못하는 폴백 경로(테스트 리그 등)에서는 파쿠르 감지 반경으로 되돌린다.
        /// </summary>
        public float LedgeHangDropDepth
        {
            get
            {
                StickmanPoseAnimator pose = GetPoseAnimator();
                float reach = pose != null ? pose.HangHandReachAboveRoot(BuildLedgeHangPoseSettings()) : 0f;
                return reach > 0.0001f ? reach : (Config != null ? Config.parkourDetectionRadius : 0.5f);
            }
        }

        /// <summary>
        /// 매달린 동안 매 프레임 호출 — 붙잡은 발판이 여전히 존재하는지 재확인하고, 존재하면 그 발판의
        /// 최신 상단 Y와 (direction 쪽) 모서리 X를 함께 돌려준다. 창이 옆으로 움직이면 붙잡은 손도 따라
        /// 움직여야 하므로 Y만으로는 부족하다. false면 "잡을 곳이 사라짐" -> 즉시 낙하.
        /// </summary>
        public bool TryGetFootholdEdgeWorld(long handle, int direction, out float topWorldY, out float edgeWorldX)
        {
            var footholds = FootholdPoller != null ? FootholdPoller.CachedFootholds : System.Array.Empty<PlatformFoothold>();
            Vector2 refPos = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.TryGetFootholdEdgeWorld(MainCamera, refPos, handle, direction, footholds, Config,
                out topWorldY, out edgeWorldX);
        }

        /// <summary>
        /// 매달리기 포즈 설정 묶음을 StickConfig에서 구성한다(BuildPoseSettings와 동일한 패턴 — Config가
        /// 없는 테스트/폴백 경로에서도 안전하도록 각 값에 기본값을 둔다).
        /// </summary>
        public StickmanPoseAnimator.LedgeHangPoseSettings BuildLedgeHangPoseSettings()
        {
            return new StickmanPoseAnimator.LedgeHangPoseSettings(
                Config != null ? Config.ledgeHangArmSpreadDegrees : 11f,
                Config != null ? Config.ledgeHangElbowBendDegrees : 8f,
                Config != null ? Config.ledgeHangLegSpreadDegrees : 6f,
                Config != null ? Config.ledgeHangKneeBendDegrees : 14f,
                Config != null ? Config.ledgeHangSwayAmplitudeDegrees : 5f,
                Config != null ? Config.ledgeHangSwayFrequencyHz : 0.9f);
        }

        /// <summary>
        /// 등반 4박자 포즈 설정 묶음(StickConfig -> StickmanPoseAnimator). 위 Build*PoseSettings와
        /// 완전히 같은 패턴이며, 전부 <b>각도와 무차원 비율</b>이라 캐릭터 배율 환산이 필요 없다.
        /// 거리 성분(짚는 위치/딛는 위치)은 여기가 아니라 States/ParkourClimbState.cs가
        /// <see cref="CharacterHeightWorld"/>에서 유도해 월드 유닛으로 넘긴다 — 이 프로젝트의
        /// "거리·속도는 실측 신장에서 파생" 규약 그대로다.
        /// </summary>
        public StickmanPoseAnimator.ParkourClimbPoseSettings BuildParkourClimbPoseSettings()
        {
            return new StickmanPoseAnimator.ParkourClimbPoseSettings(
                Config != null ? Config.parkourClimbReachFraction : 0.1833f,
                Config != null ? Config.parkourClimbHangFraction : 0.3250f,
                Config != null ? Config.parkourClimbPullFraction : 0.6583f,
                Config != null ? Config.parkourClimbReleaseFraction : 0.8917f,
                Config != null ? Config.parkourClimbRiseAtReach01 : 0.02f,
                Config != null ? Config.parkourClimbRiseAtHang01 : 0.10f,
                Config != null ? Config.parkourClimbRiseAtPull01 : 0.80f,
                Config != null ? Config.parkourClimbMantleHipDegrees : 40f,
                Config != null ? Config.parkourClimbMantleKneeDegrees : 39f,
                Config != null ? Config.parkourClimbTorsoLeanDegrees : 24f,
                Config != null ? Config.parkourClimbMaxBodySagHeights : 0.75f,
                Config != null ? Config.parkourClimbWallFootDropLegRatio : 0.62f,
                Config != null ? Config.parkourClimbMantleArmDegrees : 62f,
                Config != null ? Config.parkourClimbMantleElbowDegrees : 18f);
        }

        /// <summary>등반 자세의 각도 보간 계수(1/초). PoseSmoothingRate와 같은 성격의 창구다.</summary>
        public float ParkourClimbPoseSmoothingRate =>
            Config != null ? Config.parkourClimbPoseSmoothingRate : 44f;
    }
}
