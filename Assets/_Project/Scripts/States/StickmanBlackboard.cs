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

        /// <summary>
        /// 격파 미니게임(10절) 클릭 판정 신호. BattleMinigameDirector(Interaction)가 캐릭터 히트박스
        /// 클릭을 감지하면 true로 세팅하고, BattleMinigameState.Tick()이 다음 틱에 소비 후 false로
        /// 되돌린다(DragReleaseSignaled와 동일한 소비-후-리셋 펄스 계약).
        /// </summary>
        public bool BattleClickSignaled;

        /// <summary>
        /// Attack(전투) 상태의 IHasDialogueParams 스냅샷 입력값(BUG-M7 파이프라인, docs/BUG_REPORT_PHASE3.md
        /// Minor 1 대응). 호출자(현재는 Interaction/RivalStickmanAgent.cs)가 Machine.ChangeState(Attack)를
        /// 호출하기 직전에 이번 타격 이후 "몇 대 더 맞아야 결판나는지"를 계산해 이 필드에 써두면,
        /// AttackState.Enter()가 그 값을 그대로 스냅샷해 "한 발 더!"(&gt;=1)/"오늘은 여기까지"(0) 대사를
        /// 파생시킨다. 아무도 세팅하지 않고 ChangeState(Attack)만 호출하면 기본값 0("오늘은 여기까지")
        /// 그대로 유지된다 — 값을 채우지 않는 호출부에서도 안전한 기본 동작.
        /// </summary>
        public int AttackShotsRemaining;

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

        // Active Ragdoll(아키텍처 0절) 파츠 캐시. Ragdoll/Getup 두 상태가 공유하므로 블랙보드가
        // 최초 1회만 구성해 보관한다(매 프레임 GetComponentsInChildren 재탐색 금지 컨벤션 준수).
        private RagdollRig _ragdollRig;

        /// <summary>FootholdPoller의 캐시(= OS를 직접 호출하지 않는 저렴한 조회)를 이용해 접지 상태를 계산한다.</summary>
        public GroundSensor.GroundInfo SenseGround()
        {
            var footholds = FootholdPoller != null
                ? FootholdPoller.CachedFootholds
                : System.Array.Empty<PlatformFoothold>();
            Vector2 foot = Body != null ? Body.position : Vector2.zero;
            return GroundSensor.Sense(MainCamera, foot, footholds, Config);
        }

        /// <summary>
        /// Idle/Walk 공용 지상 로직: 접지 중이면 유예 타이머를 리셋하고 위치를 발판에 스냅한다.
        /// 접지가 아니면 유예 타이머를 누적하다가 StickConfig.fallGraceDuration을 넘기면 Fall로
        /// 강제 전이한다(발판 경계의 미세한 흔들림으로 인한 오탐 방지, StickConfig.cs 문서 참고).
        /// </summary>
        /// <returns>이번 호출로 Fall 전이가 발생했으면 true(호출부는 나머지 로직을 생략해야 함).</returns>
        public bool GroundedTick(float deltaTime, GroundSensor.GroundInfo info)
        {
            if (info.Grounded)
            {
                _groundLossTimer = 0f;
                SnapToGround(info);
                return false;
            }

            _groundLossTimer += deltaTime;
            float grace = Config != null ? Config.fallGraceDuration : 0.1f;
            if (_groundLossTimer < grace) return false;

            _groundLossTimer = 0f;
            Machine.ChangeState(StickmanStateId.Fall);
            return true;
        }

        public void ResetGroundLossTimer() => _groundLossTimer = 0f;

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

        private void SnapToGround(GroundSensor.GroundInfo info)
        {
            if (Body == null) return;
            Vector2 pos = Body.position;
            if (Mathf.Abs(pos.y - info.GroundWorldY) > 0.001f)
            {
                Body.position = new Vector2(pos.x, info.GroundWorldY);
            }
            if (Body.linearVelocity.y < 0f)
            {
                Vector2 v = Body.linearVelocity;
                v.y = 0f;
                Body.linearVelocity = v;
            }
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
                _ragdollRig = new RagdollRig(Body.transform);
            }
            return _ragdollRig;
        }

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
            bool found = GroundSensor.TryFindClimbableWall(MainCamera, foot, info, direction, footholds, Config,
                out PlatformFoothold wall, out wallTopWorldY);
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
    }
}
