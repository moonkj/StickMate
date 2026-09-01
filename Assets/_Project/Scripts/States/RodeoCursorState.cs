using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상호작용 상태: 커서 위에 올라타 균형을 잡는 로데오 커서(docs/UX_FLOW.md 13절). 클릭이
    /// 전혀 필요 없는 유일한 커서 상호작용 — 진입은 Interaction/RodeoCursorWatcher가 "커서 5초 이상
    /// 정지 + 캐릭터 도달 가능 거리" 조건만으로 판정한다(부분적 클릭관통 해제 대상 아님, 15절/13절 명시).
    ///
    /// 두 단계: (1) Mounting — rodeoMountDurationSeconds 동안 캐릭터 위치를 커서 위치로 Lerp("폴짝
    /// 올라타는" 접근, ParkourClimbState의 등반 Lerp와 동일 컨벤션). (2) Mounted — 매 프레임 커서
    /// 위치를 직접 따라간다(Kinematic). 이 구간의 커서 이동 속도가 rodeoShakeSpeedThresholdWorldPerSec를
    /// 넘으면 "거친 흔들기"로 판정해 즉시 튕겨 떨어진다.
    ///
    /// 3중 안전망(13절): (1) 암묵적 — 거친 흔들기로 낙하(RagdollImpactResolver를 통해 확정적으로
    /// RAGDOLL 진입, "튕겨 떨어진다"가 항상 성립하도록 임계값보다 큰 충격량을 강제한다). (2) 타임아웃 —
    /// rodeoMaxDurationSeconds(10초) 도달 시 정상 종료(EndClean, 5절 (a) 경로). (3) 트레이 긴급정지 —
    /// Interaction/RodeoCursorWatcher가 StickmanEventBus.GlobalEmergencyStopRequested를 구독해 이
    /// 상태를 강제로 Idle 전이시킨다(이 상태 자신은 긴급정지 이벤트를 직접 구독하지 않는다 — 소유권을
    /// 쥔 Watcher가 대신 처리하는 것이 SpectacleEventLock 해제와 원자적으로 묶이기 때문).
    /// </summary>
    public sealed class RodeoCursorState : IStickmanState
    {
        private enum Phase { Mounting, Mounted }

        private readonly StickmanBlackboard _blackboard;

        private Phase _phase;
        private float _mountTimer;
        private float _rideTimer;
        private Vector2 _mountStartWorldPos;
        private Vector2 _lastCursorWorld;
        private bool _hasLastCursorWorld;

        public StickmanStateId StateId => StickmanStateId.RodeoCursor;

        public RodeoCursorState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            _phase = Phase.Mounting;
            _mountTimer = 0f;
            _rideTimer = 0f;
            _hasLastCursorWorld = false;
            _mountStartWorldPos = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;

            if (_blackboard.Body != null)
            {
                _blackboard.Body.linearVelocity = Vector2.zero;
                _blackboard.Body.bodyType = RigidbodyType2D.Kinematic;
            }

            // UX 13절에 진입 대사가 명시되어 있지 않다 — DragThrowState와 동일한 관례로 만들지 않는다.
            // 떨어질 때의 "으악!" 류 코믹 반응은 RagdollState의 기존 충격 강도별 대사가 그대로 담당한다.
        }

        public void Tick(float deltaTime)
        {
            if (!_blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld))
            {
                // 커서를 잃으면(화면 밖 등) 안전하게 정상 종료 — UX에 이 케이스의 명시적 낙하 지시가
                // 없으므로 더 안전한 쪽을 선택한다(UX_FLOW.md 4절 원칙 재사용).
                EndClean();
                return;
            }

            if (_phase == Phase.Mounting) TickMounting(cursorWorld, deltaTime);
            else TickMounted(cursorWorld, deltaTime);
        }

        private void TickMounting(Vector2 cursorWorld, float deltaTime)
        {
            float duration = _blackboard.Config != null ? _blackboard.Config.rodeoMountDurationSeconds : 0.3f;
            _mountTimer += deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(_mountTimer / duration) : 1f;

            if (_blackboard.Body != null)
            {
                Vector2 pos = Vector2.Lerp(_mountStartWorldPos, cursorWorld, t);
                _blackboard.Body.MovePosition(pos);
            }

            if (t >= 1f)
            {
                _phase = Phase.Mounted;
                _lastCursorWorld = cursorWorld;
                _hasLastCursorWorld = true;
            }
        }

        private void TickMounted(Vector2 cursorWorld, float deltaTime)
        {
            _rideTimer += deltaTime;

            // ★ 2026-09-01 (P9-b) 이번 프레임의 커서 이동 벡터. 아래에서 _lastCursorWorld를 덮어쓰기
            // **전에** 잡아 둔다 — 이것이 곧 "털려서 날아가는 방향"이다(커서가 움직인 쪽으로 튕긴다).
            Vector2 shakeDelta = _hasLastCursorWorld ? cursorWorld - _lastCursorWorld : Vector2.zero;

            float speedWorldPerSec = 0f;
            if (_hasLastCursorWorld && deltaTime > 0f)
            {
                speedWorldPerSec = shakeDelta.magnitude / deltaTime;
            }
            _lastCursorWorld = cursorWorld;
            _hasLastCursorWorld = true;

            if (_blackboard.Body != null) _blackboard.Body.MovePosition(cursorWorld);

            float shakeThreshold = _blackboard.Config != null ? _blackboard.Config.rodeoShakeSpeedThresholdWorldPerSec : 20f;
            if (speedWorldPerSec >= shakeThreshold)
            {
                // 1차 안전망(암묵적): 거친 흔들기 -> 튕겨 떨어짐(낙하 -> RAGDOLL -> GETUP, 13절). "항상
                // 성공하는 탈출구"이므로 임계값보다 확실히 큰 충격량을 강제해 RagdollImpactResolver가
                // 반드시 Ragdoll로 전이시키도록 한다.
                if (_blackboard.Body != null) _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
                float threshold = _blackboard.Config != null ? _blackboard.Config.ragdollForceThreshold : 8f;
                float multiplier = _blackboard.Config != null ? _blackboard.Config.rodeoShakeImpactMultiplier : 1.25f;
                // ★ 2026-09-01 (P9-b) 방향 = 커서가 움직인 쪽. 흔들어 떼어낸 것이므로 "털린 방향으로
                // 날아간다"가 자연스럽다. 이 분기는 speedWorldPerSec >= 임계값일 때만 오므로 shakeDelta는
                // 항상 0이 아니다(그래도 0이면 RagdollRig가 충격량을 건너뛴다 — 안전한 폴백).
                RagdollImpactResolver.TryApplyImpact(_blackboard, threshold * multiplier, shakeDelta);
                return;
            }

            float maxDuration = _blackboard.Config != null ? _blackboard.Config.rodeoMaxDurationSeconds : 10f;
            if (_rideTimer >= maxDuration)
            {
                // 2차 안전망(타임아웃): 정상 종료 경로(5절 (a)) — 캐릭터가 스스로 내려온다.
                EndClean();
            }
        }

        private void EndClean()
        {
            if (_blackboard.Body != null) _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
            _blackboard.Machine.ChangeState(StickmanStateId.Idle);
        }

        public void Exit()
        {
            if (_blackboard.Body != null && _blackboard.Body.bodyType == RigidbodyType2D.Kinematic)
            {
                _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }
}
