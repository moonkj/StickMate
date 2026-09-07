using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: 손 등반(<see cref="ParkourClimbState"/>)의 상한(<c>StickConfig.stepUpMaxHeights</c>)을
    /// 넘는 더 높은 벽/창을 밧줄(갈고리)을 던져 타고 오르는 동작
    /// (docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md, game-architect §1~7 + design-motion §8).
    ///
    /// <para><b>2페이즈</b>: <c>Throw</c>(밧줄을 던진다 — 아직 원래 발판 위, 접지 중) →
    /// <c>Ascend</c>(밧줄을 타고 오른다 — 공중, 발판에서 완전히 이탈). 두 페이즈의 물리적 처지가
    /// 다르므로 목표(벽) 소실 시 취소 규칙도 페이즈별로 갈린다(§3-B): <c>Throw</c> 중 소실 →
    /// Idle/Walk(발판은 그대로 있으므로 낙하할 이유가 없다), <c>Ascend</c> 중 소실 → 즉시 Fall
    /// (<see cref="ParkourClimbState"/>와 100% 동일 규칙 — 이미 검증된 규칙을 그대로 재사용한다).</para>
    ///
    /// <para><b>Ascend는 사실 [반복 구간] + [마감 구간]이다</b>(§8-0 핵심 통찰). 마감 구간은
    /// <see cref="StickmanPoseAnimator.ApplyParkourClimbPose"/>를 <b>무변경으로 재호출</b>한다 —
    /// 밧줄 끝에서 창틀에 실제로 손이 닿아 올라서는 순간은 물리적으로 손 등반의 맨틀과 완전히 같은
    /// 사건이기 때문이다. 벽 높이가 손 등반 상한 바로 위(가장 흔한 최초 밧줄 등반 케이스)라면
    /// 반복 구간이 0회가 되어, Ascend 전체가 기존 ParkourClimb 등반 애니메이션과 픽셀 단위로
    /// 동일해진다(경계에서의 무게감 연속성을 수식으로 증명하는 방법).</para>
    ///
    /// <para><b>Ragdoll 강제 인터럽트는 새 코드가 필요 없다</b>(구조적으로 이미 커버됨) — 정본 목록은
    /// <see cref="RagdollImpactResolver"/> 클래스 문서 한 곳뿐이다. 여기서 그 목록을 다시 적지 않는다
    /// (이 저장소가 "같은 목록이 6개 파일에 복사됐다가 전부 낡았다"는 사고를 이미 겪었다).</para>
    ///
    /// <para><b>FacingLocked = true</b>(§8-4, <see cref="ParkourClimbState"/>와의 유일한 차이) —
    /// Ascend가 최대 7.5초까지 늘어날 수 있어(6.6H 상한, 배포 기본값은 <c>ropeClimbChance=0</c>이라
    /// 잠재워 둠) 파쿠르의 전제("1초 남짓이라 안전")가 더는 성립하지 않는다. 모든 종료 경로
    /// (정상 완료 / Idle·Walk 취소 / Fall 취소)에서 반드시 해제한다.</para>
    ///
    /// <para>원칙 3(유저 자산 불변) — 목표 창에 대해 아는 것은 좌표(벽 발판의 <c>ScreenRect</c>) 하나뿐이다.
    /// z-order/포커스 API를 호출하지 않고, 창을 흔들거나 당기는 시각 효과도 얹지 않는다. 렌더링
    /// 소유권은 <see cref="StickmanEventBus.RopeClimbOverlayChanged"/>를 발행하는 것으로 끝나며,
    /// 실제 밧줄/갈고리 그림은 별도 <c>Interaction/RopeClimbRenderer.cs</c>가 맡는다(ArcheryState/
    /// ArcheryRenderer 분리 관례 재사용).</para>
    /// </summary>
    public sealed class RopeClimbState : IStickmanState, IHasDialogueParams
    {
        private readonly StickmanBlackboard _blackboard;

        private enum Phase { Throw, Ascend }

        /// <summary>Throw의 3소절 — 감기(WindUp) → 던지기(Swing, 팔 스윙만) → 걸림 확인(HookConfirm).
        /// §8-1 표 그대로.</summary>
        private enum ThrowSubPhase { WindUp, Swing, HookConfirm }

        private Phase _phase;
        private ThrowSubPhase _throwSubPhase;
        private float _timer;

        private int _direction;
        private bool _hasWall;
        private long _wallHandle;
        private float _wallTopWorldY;
        private float _startWorldY;
        private float _startWorldX;
        private bool _hasMantleTarget;

        /// <summary>밧줄이 걸리는(HookConfirm에서 확정되는) 앵커 월드 좌표 — 진행 방향 쪽 벽의
        /// 가까운 모서리. Throw 도중에는 목표가 바뀌지 않으므로(§8-3-B "밧줄은 이미 걸린 지점에
        /// 고정") 한 번만 계산해 캐시한다.</summary>
        private float _anchorWorldX;
        private Vector2 _anchorWorld;

        /// <summary>이번 발의 밧줄 비행 시간(초) — §8-1, ArcheryState.ResolveFlightSeconds와 같은
        /// 제곱근-거리 스케일링을 세로축에 옮긴 값. Enter()에서 한 번 확정된다.</summary>
        private float _flightSeconds;

        /// <summary>오버레이(밧줄/갈고리 렌더러)가 지금 "떠 있다"고 발행된 상태인가 — Exit()가 안전망
        /// 으로 Cancelled를 한 번만 더 쏘게 하는 멱등 가드(§4절 금지 3 "잔여 그림" 방지).</summary>
        private bool _overlayActive;

        // ==================== Ascend 전용 ====================

        private float _ascendElapsed;
        private float _ascendTotalSeconds;
        private float _totalRiseWorld;
        private float _finalApproachRiseWorld;
        private float _repeatRiseWorld;
        private float _cycleRiseWorld;

        /// <summary>모서리 X를 구하지 못했을 때 손을 짚을 앞쪽 거리(신장 대비) — ParkourClimbState의
        /// 같은 이름 상수와 같은 값(폴백 전용, 실제 경로에서는 쓰이지 않는다).</summary>
        private const float FallbackEdgeForwardRatio = 0.2f;

        /// <summary>
        /// BUG-M7 파라미터 파이프라인(ParkourClimbDialogueParams와 같은 형태) — 오를 거리(월드 유닛)와
        /// 그 프레임의 신장 H. 대사 문안 자체는 design-narrative 몫이므로(§8-6) 여기서는 배선만 한다.
        /// </summary>
        public sealed class RopeClimbDialogueParams
        {
            public float ClimbHeightUnits;
            public float CharacterHeightWorld;
        }

        private readonly RopeClimbDialogueParams _dialogueParams = new RopeClimbDialogueParams();

        public object DialogueParams => _dialogueParams;

        // ==================== 진단/테스트 창구 ====================
        // 왜 필요한가: §8-6-B가 경고하는 사고("Throw 페이즈 하나만 규칙 8에 넘겨 거의 항상 막힌다")를
        // 로그 파싱이 아니라 값으로 잠그기 위해서다(AutoWanderController.LastEdgeStopDistanceUsed와
        // 같은 성격의 창구 — "판정을 쓰는 쪽이 그 값을 실제로 보는가"를 테스트가 직접 대조한다).

        /// <summary>직전 Enter()가 규칙 8 게이트에 실제로 넘긴 계획 잔여 체류 시간(초).</summary>
        public float LastPlannedDwellSecondsDiagnostic { get; private set; }

        /// <summary>같은 진입에서 <b>Throw 페이즈만</b>의 추정 총 지속시간(초) — 위 값과 대조해
        /// "Ascend 추정이 실제로 더해졌는가"를 테스트가 직접 비교할 수 있게 한다.</summary>
        public float LastThrowOnlySecondsDiagnostic { get; private set; }

        public RopeClimbState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.RopeClimb;

        public void Enter(StateTransitionContext context)
        {
            _phase = Phase.Throw;
            _throwSubPhase = ThrowSubPhase.WindUp;
            _timer = 0f;
            _overlayActive = false;

            _direction = _blackboard.MoveInputX >= 0f ? 1 : -1;
            _startWorldY = _blackboard.Body != null ? _blackboard.Body.position.y : 0f;
            _startWorldX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;

            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            // ★★★ 2026-09-07 3차(근본재설계 §10) — 좁은 TryFindClimbableWall이 아니라
            // TryFindRopeClimbWallWide를 쓴다. 이 상태로 들어오는 트리거(AutoWanderController의
            // 독립 우선순위 로프 평가, WalkState의 RopeClimbPressed 재확인)는 전부 이미 넓은 겹침
            // 탐색으로 벽을 찾은 뒤 이 Enter()를 호출하는데, 여기서 좁은 탐색으로 다시 확인하면
            // "생성 프레임이 본 벽"과 "Enter()가 다시 찾는 벽"이 갈라져 방금 찾은 벽을 곧바로
            // 놓친다(실기 재관측으로 실제로 겪은 사고 — Throw 진입 직후 "목표 벽 소실"로 즉시
            // 취소됐다. ResolveEffectiveEdgeBoundary 사고와 같은 계열: 판정을 쓰는 모든 지점이
            // 같은 계산원을 봐야 한다).
            float ropeStepUpMax = AutoWanderController.ResolveStepUpMaxHeightStatic(_blackboard);
            float ropeMaxHeight = AutoWanderController.ResolveRopeClimbMaxHeight(_blackboard, info.GroundWorldY);
            _hasWall = _blackboard.TryFindRopeClimbWallWide(info, _direction, out _wallHandle, out _wallTopWorldY, ropeStepUpMax, ropeMaxHeight);
            _hasMantleTarget = TryComputeMantleTargetX(out _, out _);

            if (_blackboard.Body != null)
            {
                Vector2 v = _blackboard.Body.linearVelocity;
                v.x = 0f;
                v.y = 0f;
                _blackboard.Body.linearVelocity = v;
            }

            // §8-4 — Ascend가 최대 7.5초까지 늘어나므로 파쿠르(1초 남짓이라 안전)와 달리 반드시
            // 고정한다. 모든 종료 경로에서 해제(아래 Exit() 참고).
            _blackboard.FacingLocked = true;
            if (_hasWall) _blackboard.SetFacingSign(_direction);

            _anchorWorldX = _startWorldX;
            if (_hasWall && _blackboard.TryGetFootholdEdgeWorld(_wallHandle, -_direction, out _, out float nearEdgeX))
            {
                _anchorWorldX = nearEdgeX;
            }
            _anchorWorld = new Vector2(_anchorWorldX, _wallTopWorldY);

            float climbHeightWorld = _hasWall ? Mathf.Max(0f, _wallTopWorldY - _startWorldY) : 0f;
            StickConfig cfg = _blackboard.Config;

            // §8-1 밧줄 비행 시간 — 등반의 기준 높이(손 등반 상한, 새 기준점 아님)와의 비율에
            // 제곱근을 씌운다(선형이면 너무 느려지고 고정이면 섬광이 된다는 트레이드오프, Archery와
            // 같은 이유).
            float referenceHeight = cfg != null
                ? cfg.ResolveStepUpMaxHeightWorld(_blackboard.CharacterHeightWorld)
                : StickConfig.BaselineCharacterTotalHeight * 1.0551f;
            float scale = Mathf.Sqrt(climbHeightWorld / Mathf.Max(0.0001f, referenceHeight));
            float flightBase = cfg != null ? cfg.ropeThrowFlightBaseSeconds : 0.20f;
            float flightMax = cfg != null ? cfg.ropeThrowFlightMaxSeconds : 0.55f;
            _flightSeconds = Mathf.Clamp(flightBase * scale, flightBase * 0.8f, Mathf.Max(flightBase, flightMax));

            float windUpSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowWindUpSeconds : 0.35f);
            float swingSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowSwingSeconds : 0.20f);
            float hookSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowHookConfirmSeconds : 0.18f);
            float throwTotalSeconds = windUpSec + Mathf.Max(swingSec, _flightSeconds) + hookSec;

            float speed = Mathf.Max(0.01f, cfg != null ? cfg.ropeClimbSpeedHeightsPerSecond : 0.88f)
                * _blackboard.CharacterHeightWorld;
            float ascendEstimateSeconds = climbHeightWorld / speed;

            // ★★ §8-6-B 위험 경고 — Throw만 넘기면 규칙 8(발화 자격 게이트)에 거의 항상 막힌다
            // (ParkourClimbState가 2026-09-02에 실제로 겪은 사고와 같은 함정). 반드시 Throw+Ascend
            // 전체 추정치를 넘긴다.
            float plannedDwellSeconds = throwTotalSeconds + ascendEstimateSeconds;
            LastPlannedDwellSecondsDiagnostic = plannedDwellSeconds;
            LastThrowOnlySecondsDiagnostic = throwTotalSeconds;

            _dialogueParams.ClimbHeightUnits = climbHeightWorld;
            _dialogueParams.CharacterHeightWorld = _blackboard.CharacterHeightWorld;

            if (TryRollClimbChatter())
            {
                DialogueIntent intent = DialogueIntent.TryCreate(context,
                    (id, dialogueParams) => ResolveThrowLine(dialogueParams), plannedDwellSeconds);
                // ★ 순서가 계약이다(ParkourClimbState/AmbientChatter와 같은 이유) — 규칙 8에 막힌
                // 발화는 쿨다운을 소비하지 않는다.
                if (intent != null) ReloadSharedChatterCooldown();
            }

            Debug.Log($"[밧줄등반] Throw 진입 — 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}, " +
                $"벽핸들={_wallHandle}, 시작 월드=({_startWorldX:F3},{_startWorldY:F3}), " +
                $"오를 높이={climbHeightWorld:F3}유닛, 밧줄 비행={_flightSeconds:F2}초, " +
                $"Throw 총 {throwTotalSeconds:F2}초, Ascend 예상 {ascendEstimateSeconds:F2}초, " +
                $"대사 예산={plannedDwellSeconds:F2}초.");
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.Body == null)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            if (_phase == Phase.Throw) TickThrow(deltaTime);
            else TickAscend(deltaTime);
        }

        // ============================================================================
        // Throw
        // ============================================================================

        private void TickThrow(float deltaTime)
        {
            // 아직 원래 발판 위 — Archery의 비-Approach 분기와 같은 이유로 상태가 직접
            // GroundedTick을 부른다(IsGroundKeepingSelfManaged 제외 목록에 RopeClimb이 있는 이유 —
            // Ascend는 공중이라 접지 안전망이 손대면 안 되지만, Throw는 실제로 접지 중이라 상태가
            // 스스로 재확인해야 한다).
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return;
            if (_blackboard.GroundedTick(deltaTime, info)) return;

            // §3-B — Throw 중 목표(벽) 소실 → Idle/Walk로 취소(발판은 그대로 있으므로 낙하할
            // 이유가 없다. Ascend의 즉시 Fall과 정확히 대칭인 반대쪽 규칙).
            if (!_hasWall || !_blackboard.TryGetFootholdTopWorldY(_wallHandle, out _wallTopWorldY))
            {
                CancelOverlay();
                float move = _blackboard.MoveInputX;
                float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
                StickmanStateId fallback = Mathf.Abs(move) > deadzone ? StickmanStateId.Walk : StickmanStateId.Idle;
                Debug.Log($"[밧줄등반] Throw 취소 — 목표 벽 소실, {fallback}로 복귀합니다.");
                _blackboard.Machine.ChangeState(fallback);
                return;
            }

            // 제자리 정지를 매 프레임 재확인한다 — 활쏘기의 비-Approach 분기와 같은 계약
            // (IsHorizontalMotionSelfManaged 문서: "한 번만 대입하고 끝내면 안 된다").
            Vector2 v = _blackboard.Body.linearVelocity;
            v.x = 0f;
            v.y = 0f;
            _blackboard.Body.linearVelocity = v;

            _timer += deltaTime;
            StickConfig cfg = _blackboard.Config;
            float windUpSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowWindUpSeconds : 0.35f);
            float swingSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowSwingSeconds : 0.20f);
            float hookSec = Mathf.Max(0.01f, cfg != null ? cfg.ropeThrowHookConfirmSeconds : 0.18f);
            float swingTotal = Mathf.Max(swingSec, _flightSeconds);

            float wind01;
            float thrown01;
            switch (_throwSubPhase)
            {
                case ThrowSubPhase.WindUp:
                    wind01 = Mathf.Clamp01(_timer / windUpSec);
                    thrown01 = 0f;
                    if (_timer >= windUpSec) { _throwSubPhase = ThrowSubPhase.Swing; _timer = 0f; }
                    break;

                case ThrowSubPhase.Swing:
                {
                    wind01 = 1f;
                    float raw = Mathf.Clamp01(_timer / swingSec);
                    thrown01 = Mathf.SmoothStep(0f, 1f, raw);
                    // swingTotal이 swingSec보다 길면(먼 사거리) 그 차이는 "밧줄이 날아가는 동안 팔은
                    // 이미 던진 자세로 서 있는" hold 연장이다(§8-1) — 새 소절이 아니다.
                    if (_timer >= swingTotal) { _throwSubPhase = ThrowSubPhase.HookConfirm; _timer = 0f; }
                    break;
                }

                default: // HookConfirm
                    wind01 = 1f;
                    thrown01 = 1f;
                    if (_timer >= hookSec)
                    {
                        BeginAscend();
                        return;
                    }
                    break;
            }

            RaiseThrowOverlay();

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            pose?.ApplyRopeThrowPose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.ParkourClimbPoseSmoothingRate, wind01, thrown01);
        }

        private void RaiseThrowOverlay()
        {
            Vector2 hand = _blackboard.Body.position;
            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose != null)
            {
                pose.GetHandWorldPositions(out Vector2 left, out Vector2 right);
                hand = _direction > 0 ? right : left;
            }

            float flight01 = _throwSubPhase == ThrowSubPhase.WindUp ? 0f
                : _throwSubPhase == ThrowSubPhase.Swing ? Mathf.Clamp01(_timer / Mathf.Max(0.0001f, _flightSeconds))
                : 1f;

            StickmanEventBus.RaiseRopeClimbOverlayChanged(hand, _anchorWorld, flight01, SpectacleOverlayPhase.Started);
            _overlayActive = true;
        }

        // ============================================================================
        // Ascend
        // ============================================================================

        private void BeginAscend()
        {
            _phase = Phase.Ascend;
            _ascendElapsed = 0f;
            _anchorWorld = new Vector2(_anchorWorldX, _wallTopWorldY);

            float speed = Mathf.Max(0.01f, _blackboard.Config != null ? _blackboard.Config.ropeClimbSpeedHeightsPerSecond : 0.88f)
                * _blackboard.CharacterHeightWorld;
            _totalRiseWorld = Mathf.Max(0.0001f, _wallTopWorldY - _startWorldY);
            _ascendTotalSeconds = _totalRiseWorld / speed;

            // §8-0/8-3-A — 마감 구간은 손 등반 상한으로 고정한다(기존 필드 재사용, 새 기준점 아님).
            float finalApproachMax = _blackboard.Config != null
                ? _blackboard.Config.ResolveStepUpMaxHeightWorld(_blackboard.CharacterHeightWorld)
                : StickConfig.BaselineCharacterTotalHeight * 1.0551f;
            _finalApproachRiseWorld = Mathf.Min(_totalRiseWorld, Mathf.Max(0.0001f, finalApproachMax));
            _repeatRiseWorld = Mathf.Max(0f, _totalRiseWorld - _finalApproachRiseWorld);
            _cycleRiseWorld = Mathf.Max(0.01f, _blackboard.Config != null ? _blackboard.Config.ropeClimbCycleRiseHeights : 0.9f)
                * _blackboard.CharacterHeightWorld;

            Debug.Log($"[밧줄등반] Ascend 시작 — 총 상승={_totalRiseWorld:F3}유닛, 예상 소요={_ascendTotalSeconds:F2}초, " +
                $"반복구간={_repeatRiseWorld:F3}유닛(사이클 {_cycleRiseWorld:F3}유닛), 마감구간={_finalApproachRiseWorld:F3}유닛.");
        }

        private void TickAscend(float deltaTime)
        {
            // §3-B — Ascend 중 목표(벽) 소실 → 즉시 Fall(ParkourClimbState.Tick()과 100% 동일 규칙,
            // 공중에서 손을 잡고 있던 것과 물리적으로 같은 처지이므로 이미 검증된 규칙을 재사용한다).
            if (!_hasWall || !_blackboard.TryGetFootholdTopWorldY(_wallHandle, out _wallTopWorldY))
            {
                CancelOverlay();
                Debug.Log("[밧줄등반] Ascend 취소 — 목표 벽 소실, Fall로 전이합니다.");
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
                return;
            }

            _ascendElapsed += deltaTime;
            float progress01 = _ascendTotalSeconds > 0f ? Mathf.Clamp01(_ascendElapsed / _ascendTotalSeconds) : 1f;

            Vector2 pos = _blackboard.Body.position;
            pos.y = Mathf.Lerp(_startWorldY, _wallTopWorldY, progress01);

            bool hasEdge = TryComputeMantleTargetX(out float mantleTargetX, out float nearEdgeWorldX);
            float riseWorld = progress01 * _totalRiseWorld;
            bool inFinalApproach = riseWorld > _repeatRiseWorld;
            float finalProgress01 = _finalApproachRiseWorld > 0.0001f
                ? Mathf.Clamp01((riseWorld - _repeatRiseWorld) / _finalApproachRiseWorld)
                : 1f;

            if (inFinalApproach && _hasMantleTarget && hasEdge)
            {
                pos.x = Mathf.Lerp(_startWorldX, mantleTargetX, finalProgress01);
            }
            else
            {
                pos.x = _startWorldX; // 반복 구간에서는 밧줄을 타고 곧장 위로 오른다(수평 이동 없음).
            }

            _blackboard.MoveBodyToWorld(pos);
            _blackboard.Body.linearVelocity = Vector2.zero;

            DriveAscendPose(deltaTime, pos, inFinalApproach, finalProgress01, riseWorld, hasEdge, nearEdgeWorldX);
            RaiseAscendOverlay();

            if (progress01 >= 1f)
            {
                // 올라선 발판을 즉시 고착한다 — ParkourClimbState.Tick() 완료 블록과 완전히 같은
                // 이유(다음 프레임 접지 판정이 엉뚱한 발판을 새로 고르는 것을 막는다).
                _blackboard.CurrentFootholdHandle = _wallHandle;
                _blackboard.ReportFootholdChangeIfNeeded("밧줄등반 완료 — 턱 위에 올라섬");
                _blackboard.ResetGroundLossTimer();

                // §3-C — 반드시 기존 신호를 재사용한다(새 RopeClimbMantleSequence를 만들지 않는다).
                // 배회 AI(AutoWanderController.ConsumeClimbMantleSignalIfAny)가 이 신호를 모르면
                // 손 등반에서 이미 고친 "올라서자마자 도로 뛰어내림" 버그가 밧줄 등반에서 처음부터
                // 다시 난다.
                _blackboard.ReportClimbMantleCompleted(_direction);
                CompleteOverlay();

                Debug.Log($"[밧줄등반] 완료 — 올라선 월드=({pos.x:F3},{pos.y:F3}), 발판핸들={_wallHandle}, " +
                    $"올라선 방향={(_direction > 0 ? "오른쪽" : "왼쪽")}(맨틀 신호 #{_blackboard.ClimbMantleSequence}).");

                // ParkourClimbState.Tick() 완료 블록과 같은 이유로 다음 상태는 Walk 하나뿐이다 —
                // 맨틀 신호의 소비자는 조건 없이 EnterMoving(방향)을 부르므로 "걸어 들어간다"는
                // 추정이 아니라 확정 사실이다.
                _blackboard.Machine.ChangeState(StickmanStateId.Walk);
            }
        }

        private void RaiseAscendOverlay()
        {
            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            Vector2 hand = _blackboard.Body.position;
            if (pose != null)
            {
                pose.GetHandWorldPositions(out Vector2 left, out Vector2 right);
                hand = _direction > 0 ? right : left;
            }
            StickmanEventBus.RaiseRopeClimbOverlayChanged(hand, _anchorWorld, 1f, SpectacleOverlayPhase.Started);
            _overlayActive = true;
        }

        /// <summary>
        /// Ascend 포즈 라우팅(§8-0) — 반복 구간(밧줄을 갈아 잡으며 오른다, 신규)과 마감 구간
        /// (<see cref="StickmanPoseAnimator.ApplyParkourClimbPose"/> 무변경 재호출)을 가른다.
        /// </summary>
        private void DriveAscendPose(float deltaTime, Vector2 pos, bool inFinalApproach, float finalProgress01,
            float riseWorld, bool hasEdge, float nearEdgeWorldX)
        {
            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            if (pose == null || !_hasWall) return;

            float height = _blackboard.CharacterHeightWorld;
            StickConfig cfg = _blackboard.Config;
            float toFacing = _blackboard.FacingSign * _direction >= 0f ? 1f : -1f;

            if (inFinalApproach)
            {
                float edgeForward = hasEdge
                    ? (nearEdgeWorldX - pos.x) * _direction
                    : height * FallbackEdgeForwardRatio;
                float gripInset = height * (cfg != null ? cfg.parkourClimbGripInsetRatio : 0.10f);
                float footAhead = height * (cfg != null ? cfg.parkourClimbFootPlantAheadRatio : 0.18f);

                pose.ApplyParkourClimbPose(deltaTime,
                    _blackboard.BuildPoseSettings(),
                    _blackboard.BuildLedgeHangPoseSettings(),
                    _blackboard.BuildParkourClimbPoseSettings(),
                    _blackboard.ParkourClimbPoseSmoothingRate,
                    finalProgress01,
                    _finalApproachRiseWorld,
                    _wallTopWorldY - pos.y,
                    (edgeForward + gripInset) * toFacing,
                    (edgeForward + gripInset + footAhead) * toFacing,
                    edgeForward * toFacing);
                return;
            }

            float cycleLocal01 = _cycleRiseWorld > 0.0001f ? Mathf.Repeat(riseWorld / _cycleRiseWorld, 1f) : 0f;
            float gripForwardWorld = (_anchorWorldX - pos.x) * _direction;
            pose.ApplyRopeClimbCyclePose(deltaTime,
                _blackboard.BuildLedgeHangPoseSettings(),
                _blackboard.BuildParkourClimbPoseSettings(),
                _blackboard.ParkourClimbPoseSmoothingRate,
                cycleLocal01,
                gripForwardWorld * toFacing);
        }

        // ============================================================================
        // 공용 — 맨틀 좌표(ParkourClimbState.TryComputeMantleTargetX와 완전히 같은 계산, §6-D 재사용)
        // ============================================================================

        private bool TryComputeMantleTargetX(out float targetX) => TryComputeMantleTargetX(out targetX, out _);

        private bool TryComputeMantleTargetX(out float targetX, out float nearEdgeWorldX)
        {
            targetX = _startWorldX;
            nearEdgeWorldX = _startWorldX;
            if (!_hasWall) return false;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, -_direction, out _, out float nearEdgeX)) return false;
            nearEdgeWorldX = nearEdgeX;
            if (!_blackboard.TryGetFootholdEdgeWorld(_wallHandle, _direction, out _, out float farEdgeX)) return false;

            // §6-D — ParkourMantleInsetWorld를 그대로 호출한다. 이름에 "Parkour"가 들어있지만 실제
            // 계산은 파쿠르 특유의 것이 전혀 없다(경계 판정 거리 하나에서 유도된 범용 값).
            float inset = _blackboard.ParkourMantleInsetWorld;
            float desired = nearEdgeX + _direction * Mathf.Max(0f, inset);
            targetX = Mathf.Clamp(desired, Mathf.Min(nearEdgeX, farEdgeX), Mathf.Max(nearEdgeX, farEdgeX));
            return true;
        }

        // ============================================================================
        // 자율 예산 — 공유 쿨다운 + 소스별 확률(ParkourClimbState.TryRollClimbChatter와 완전히 같은 형태)
        // ============================================================================

        internal bool TryRollClimbChatter()
        {
            if (_blackboard == null) return false;
            if (Time.unscaledTime < _blackboard.NextChatterAllowedUnscaledTime) return false;

            float chance = AppSettingsModel.ResolveRopeClimbChatterChance(_blackboard.Config);
            if (chance <= 0f) return false;
            return Random.value < chance;
        }

        private void ReloadSharedChatterCooldown()
        {
            float cooldown = _blackboard.Config != null ? _blackboard.Config.ambientChatterCooldownSeconds : 11f;
            _blackboard.NextChatterAllowedUnscaledTime = Time.unscaledTime + Mathf.Max(0f, cooldown);
        }

        /// <summary>
        /// ★ 플레이스홀더 — 문안 자체는 design-narrative 몫이다(§8-6-D, 리더 지시). 여기서는 트리거
        /// 배선(단일 지점, Enter() 한 곳)과 규칙 8 예산 계산만 확인한다. 문안을 배열/상수로 빼지
        /// 않는 이유는 ParkourClimbState.ResolveClimbLine과 같다 — DialogueCorpus.ExtractSayReact가
        /// <c>DialogueLine.Say(</c> 괄호 안의 인라인 리터럴만 스캔한다.
        /// </summary>
        internal static DialogueLine ResolveThrowLine(object dialogueParams)
        {
            return DialogueLine.Say("밧줄이다!");
        }

        // ============================================================================
        // 오버레이 생애주기(원칙 3 — 잔여 그림 방지)
        // ============================================================================

        private void CancelOverlay()
        {
            if (!_overlayActive) return;
            _overlayActive = false;
            StickmanEventBus.RaiseRopeClimbOverlayChanged(_anchorWorld, _anchorWorld, 0f, SpectacleOverlayPhase.Cancelled);
        }

        private void CompleteOverlay()
        {
            if (!_overlayActive) return;
            _overlayActive = false;
            StickmanEventBus.RaiseRopeClimbOverlayChanged(_anchorWorld, _anchorWorld, 1f, SpectacleOverlayPhase.Completed);
        }

        public void Exit()
        {
            _blackboard.FacingLocked = false;
            // 안전망 — 정상 경로(취소/완료)가 이미 오버레이를 정리했다면 _overlayActive가 false라
            // 아무 일도 안 한다. 여기까지 true로 남아 있다면 Ragdoll 강제 인터럽트처럼 이 상태의
            // Tick()을 거치지 않고 곧장 Exit()이 불렸다는 뜻이라, 잔여 그림을 여기서 지운다.
            CancelOverlay();
        }
    }
}
