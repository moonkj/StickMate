using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상호작용 상태: 유저가 캐릭터를 커서로 붙잡아 드래그하는 동안(docs/UX_FLOW.md 12절).
    ///
    /// 진입: Interaction/DragThrowController가 StickmanClickHitbox.MouseDown + 부분적 클릭관통 해제
    /// (Platform.ILocalClickCaptureService) + SpectacleEventLock을 모두 확보했을 때만
    /// Machine.ChangeState(Dragged)를 호출한다 — 이 상태 자신은 그 획득 절차를 전혀 모르고, 진입한
    /// 이상 "지금 드래그 중"이라는 사실만 다룬다(원칙 1: Enter() 호출 자체가 확정 신호).
    ///
    /// 물리: Enter()에서 Rigidbody2D를 Kinematic으로 전환(질량 개입 없이 위치를 직접 제어) — 커서를
    /// SmoothDamp로 뒤쫓아 "스프링·댐퍼로 따라오는 관성감"을 낸다(12절, 텔레포트처럼 딱 붙지 않음).
    /// 최근 dragThrowVelocitySampleWindowSeconds(0.12초) 구간의 위치 이력을 원형 버퍼에 쌓아두었다가,
    /// 놓는 순간 평균 속도를 계산해 dragThrowMaxSpeed로 clamp한 뒤 Dynamic 복귀 + 그 속도로 던진다 —
    /// clamp가 없으면 "실종 버그"(화면 밖으로 사라져 안 돌아옴)로 이어질 수 있다(12절 명시). 던진 속도
    /// (질량 곱 = 충격량)가 ragdollForceThreshold를 넘으면 RagdollImpactResolver를 통해 즉시 Ragdoll로
    /// 자연 전이하고, 아니면 Fall로 보내 평범한 포물선 낙하가 되게 한다.
    ///
    /// 종료 경로(전부 이 상태의 Tick()이 자체 판정): (1) DragReleaseSignaled 펄스(마우스업/트레이
    /// 긴급정지 모두 이 신호 하나로 통일), (2) dragThrowMaxHoldSeconds(10초) 초과, (3) 커서 좌표를 더
    /// 이상 조회할 수 없음(화면/모니터 경계 이탈로 간주 -> 마지막 유효 위치에서 자유낙하 시작, 12절 예외).
    ///
    /// [알려진 한계, 정직하게 문서화] 창(발판) 충돌 시 국소 충격 파티클/흔들림 이펙트(12절)는 이 프로젝트의
    /// 발판이 가상 판정(Collider2D 없음, Phase 2 교차 레이어 로그 참고)이라 실제 물리 충돌 이벤트로
    /// 감지할 수 없다 — 렌더링 레이어가 붙을 때 발판 사각형과의 거리 기반 근사 판정을 추가로 설계해야
    /// 한다(Phase 2+ 과제, WanderAmbientMotionRequested류 "트리거 조건은 나중" 패턴 재사용 권고).
    /// </summary>
    public sealed class DragThrowState : IStickmanState
    {
        // 0.12초 창이면 200fps에서도 24개 표본이면 충분 — 여유 있게 32.
        private const int SampleCapacity = 32;

        private readonly StickmanBlackboard _blackboard;
        private readonly Vector2[] _samplePositions = new Vector2[SampleCapacity];
        private readonly float[] _sampleTimes = new float[SampleCapacity];

        // 드래그 추종 상태를 1초 간격으로 남기기 위한 타이머(진단용 — 사용자가 끄는 동안 캐릭터가
        // 실제로 커서를 따라가고 있는지, 좌표가 어긋나지 않는지를 리더가 Player.log만으로 판별할 수 있게).
        private float _followLogTimer;
        private int _sampleHead;
        private int _sampleCount;
        private float _holdTimer;
        private Vector2 _followVelocityRef; // SmoothDamp 내부 상태(물리 속도 아님, 순수 추종 연산용)
        private bool _cursorEverAvailable;
        // 잡은 순간의 "커서 -> 몸통 원점" 오프셋. FollowCursor()가 이 오프셋을 유지한 채 커서를 따라가므로
        // 캐릭터는 **사용자가 실제로 붙잡은 그 지점**이 커서에 붙은 것처럼 움직인다(아래 문서 참고).
        private Vector2 _grabOffset;

        public StickmanStateId StateId => StickmanStateId.Dragged;

        public DragThrowState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            _holdTimer = 0f;
            _sampleHead = 0;
            _sampleCount = 0;
            _followVelocityRef = Vector2.zero;
            _cursorEverAvailable = false;
            _blackboard.DragReleaseSignaled = false;

            if (_blackboard.Body != null)
            {
                _blackboard.Body.linearVelocity = Vector2.zero;
                _blackboard.Body.bodyType = RigidbodyType2D.Kinematic;
            }

            _grabOffset = Vector2.zero;
            if (_blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld))
            {
                _cursorEverAvailable = true;
                PushSample(cursorWorld);
                _grabOffset = CaptureGrabOffset(cursorWorld);
            }

            _followLogTimer = 0f;
            Debug.Log($"[DragThrowState] [3/6] 드래그 시작(Dragged 진입) — 커서 월드={( _cursorEverAvailable ? cursorWorld.ToString("F2") : "(조회 실패)")}, " +
                $"몸통={_blackboard.Body?.position.ToString("F2")}, 잡은 오프셋={_grabOffset.ToString("F2")}, " +
                $"물리모드={_blackboard.Body?.bodyType}.");

            // UX 12절에는 드래그 "진입" 시점의 대사가 명시되어 있지 않다 — WalkState/JumpState와 동일한
            // 관례로, 정말 필요한 대사가 없는 상태는 DialogueIntent를 만들지 않는 것도 원칙 1을 지키는
            // 방법이다(없는 대사를 억지로 채워 넣지 않음).
        }

        public void Tick(float deltaTime)
        {
            _holdTimer += deltaTime;

            if (_blackboard.DragReleaseSignaled)
            {
                _blackboard.DragReleaseSignaled = false;
                ReleaseAndThrow();
                return;
            }

            float maxHold = _blackboard.Config != null ? _blackboard.Config.dragThrowMaxHoldSeconds : 10f;
            if (_holdTimer >= maxHold)
            {
                ReleaseAndThrow();
                return;
            }

            if (!_blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld))
            {
                // 화면/모니터 경계 이탈 등으로 커서를 더 이상 추적할 수 없음 -> 마지막 유효 좌표에서
                // 놓친 것으로 간주(12절 예외). 진입 즉시부터 한 번도 유효했던 적이 없으면 그냥 Idle로.
                if (_cursorEverAvailable) ReleaseAndThrow();
                else _blackboard.Machine.ChangeState(StickmanStateId.Idle);
                return;
            }

            _cursorEverAvailable = true;
            PushSample(cursorWorld);
            FollowCursor(cursorWorld, deltaTime);

            _followLogTimer += deltaTime;
            if (_followLogTimer >= 1f)
            {
                _followLogTimer = 0f;
                Vector2 body = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;
                Debug.Log($"[DragThrowState] [4/6] 드래그 추종 중 — 커서 월드={cursorWorld.ToString("F2")}, " +
                    $"몸통={body.ToString("F2")}, 목표(커서+오프셋)={(cursorWorld + _grabOffset).ToString("F2")}, " +
                    $"홀드 {_holdTimer:F1}초, 물리모드={_blackboard.Body?.bodyType}.");
            }
        }

        public void Exit()
        {
            // 정상 경로에서는 ReleaseAndThrow()가 이미 Dynamic 복귀를 끝내고 다음 상태로 전이했을 때만
            // Exit()가 호출된다. 방어적으로 한 번 더 확인 — 전체화면 감지 등 이 상태가 스스로 판단하지
            // 못한 경로(StickmanAgent.Suspend()의 강제 취소)로 빠져나갈 경우에도 캐릭터가 Kinematic으로
            // 얼어붙은 채 남으면 안 된다.
            if (_blackboard.Body != null && _blackboard.Body.bodyType == RigidbodyType2D.Kinematic)
            {
                _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        /// <summary>
        /// 잡은 순간의 "커서 -> 몸통 원점" 오프셋을 기록한다. 이게 없으면(= 몸통 원점을 커서에 그대로
        /// 맞추면) 문제가 두 가지 생긴다:
        ///   (1) 루트 원점은 이 프로젝트에서 **발끝**이다(SceneBootstrapper.BuildStickmanPrefab 문서 —
        ///       StickmanBlackboard.SenseGround가 Body.position을 그대로 발 높이로 취급한다). 그래서
        ///       원점을 커서에 맞추면 캐릭터가 커서 **위쪽**에 통째로 매달린 모습이 되어, 잡은 지점과
        ///       보이는 위치가 어긋난다(12절의 "대롱대롱 매달리는" 그림과 반대).
        ///   (2) 누르는 순간 캐릭터가 커서 위치로 순간이동하듯 튄다 — 12절이 명시적으로 배제한 연출
        ///       ("순간 텔레포트처럼 커서에 딱 붙지 않음").
        /// 오프셋을 유지하면 사용자가 머리를 잡으면 머리가, 다리를 잡으면 다리가 커서에 붙은 채로
        /// 따라온다. 부수 효과로 **좌표 변환에 상수 오차가 남아 있어도 드래그 추종에서는 그 오차가
        /// 상쇄된다**(잡는 순간 측정한 상대 오프셋을 그대로 유지하므로).
        ///
        /// 다만 오프셋이 무한정 커지면(예: 좌표계가 크게 어긋난 상태에서 전역 폴링 경로로 잡힌 경우)
        /// 캐릭터가 커서에서 한참 떨어진 채 끌려다니게 되므로, 전신 높이를 넘지 않는 선으로 clamp한다.
        /// </summary>
        private Vector2 CaptureGrabOffset(Vector2 cursorWorld)
        {
            if (_blackboard.Body == null) return Vector2.zero;
            Vector2 offset = _blackboard.Body.position - cursorWorld;

            // 전신 높이 근사치(콜라이더 바운즈) — 없으면 보수적인 고정값.
            float maxOffset = 2.5f;
            var collider = _blackboard.Body.GetComponent<Collider2D>();
            if (collider != null) maxOffset = Mathf.Max(0.1f, collider.bounds.size.magnitude);

            if (offset.magnitude > maxOffset) offset = offset.normalized * maxOffset;
            return offset;
        }

        private void FollowCursor(Vector2 target, float deltaTime)
        {
            if (_blackboard.Body == null) return;
            float smoothTime = _blackboard.Config != null ? _blackboard.Config.dragFollowSmoothTime : 0.08f;
            Vector2 current = _blackboard.Body.position;
            // 잡은 지점이 커서에 붙어 있는 것처럼 보이도록 오프셋을 유지한 채 따라간다(CaptureGrabOffset 참고).
            Vector2 desired = target + _grabOffset;

            // 소프트 클램프(12절 "화면 경계 도달 시 ... 안쪽으로 소프트 클램프") — 지면 아래로는
            // 끌고 내려가지 않는다. Kinematic MovePosition은 정적 바닥 콜라이더를 그대로 통과하므로,
            // 커서가 지면보다 아래(예: macOS Dock 영역)에 있을 때 그대로 따라가면 캐릭터가 바닥 밑에
            // 놓인 채 놓여진다. 그 위치는 접지 허용 오차 밖이라 Grounded가 영원히 false이고 물리 바닥이
            // 위로 올려주지도 못해 **Fall 상태에 영구 고착**된다(실측 확인, 2026-08-28).
            if (_blackboard.TryGetGroundSurfaceWorldY(desired, out float surfaceY) && desired.y < surfaceY)
            {
                desired.y = surfaceY;
            }

            Vector2 next = Vector2.SmoothDamp(current, desired, ref _followVelocityRef, Mathf.Max(0.001f, smoothTime), Mathf.Infinity, deltaTime);
            _blackboard.Body.MovePosition(next);
        }

        private void PushSample(Vector2 pos)
        {
            _samplePositions[_sampleHead] = pos;
            _sampleTimes[_sampleHead] = Time.time;
            _sampleHead = (_sampleHead + 1) % SampleCapacity;
            if (_sampleCount < SampleCapacity) _sampleCount++;
        }

        /// <summary>최근 dragThrowVelocitySampleWindowSeconds 구간의 평균 속도(12절 "0.12초 구간의
        /// 커서 이동 벡터를 평균 내어" 요구사항 — 스무딩 없이 마지막 한 프레임 값만 쓰면 손떨림에
        /// 과민 반응하므로 이 평균이 필요하다). 표본이 부족하면(방금 진입) 0 벡터.</summary>
        private Vector2 ComputeThrowVelocity()
        {
            if (_sampleCount < 2) return Vector2.zero;

            float window = _blackboard.Config != null ? _blackboard.Config.dragThrowVelocitySampleWindowSeconds : 0.12f;

            int newestIndex = (_sampleHead - 1 + SampleCapacity) % SampleCapacity;
            Vector2 newestPos = _samplePositions[newestIndex];
            float newestTime = _sampleTimes[newestIndex];

            Vector2 oldestPos = newestPos;
            float oldestTime = newestTime;
            for (int i = 0; i < _sampleCount; i++)
            {
                int idx = (newestIndex - i + SampleCapacity * 2) % SampleCapacity;
                float t = _sampleTimes[idx];
                if (newestTime - t > window) break;
                oldestPos = _samplePositions[idx];
                oldestTime = t;
            }

            float dt = newestTime - oldestTime;
            if (dt <= 0.0001f) return Vector2.zero;
            return (newestPos - oldestPos) / dt;
        }

        private void ReleaseAndThrow()
        {
            Vector2 throwVelocity = ComputeThrowVelocity();

            float maxSpeed = _blackboard.Config != null ? _blackboard.Config.dragThrowMaxSpeed : 12f;
            float speed = throwVelocity.magnitude;
            if (speed > maxSpeed && speed > 0f)
            {
                // 속도 상한(clamp, 12절) — 상한이 없으면 캐릭터가 화면 밖으로 사라져 다시 돌아오지 않는
                // "실종 버그"처럼 보일 위험이 있다.
                throwVelocity = throwVelocity / speed * maxSpeed;
                speed = maxSpeed;
            }

            float mass = _blackboard.Body != null ? _blackboard.Body.mass : 1f;

            if (_blackboard.Body != null)
            {
                _blackboard.Body.bodyType = RigidbodyType2D.Dynamic;
                _blackboard.Body.linearVelocity = throwVelocity;
            }

            // 던진 속도가 충분히 세면(충격량 = 속력 * 질량, StickmanAgent.OnCollisionEnter2D와 동일 단위
            // 관례) 즉시 RAGDOLL로 자연 전이 — 아니면 평범한 Fall(포물선 낙하)로 보낸다.
            float impulseMagnitude = speed * mass;
            bool wentRagdoll = RagdollImpactResolver.TryApplyImpact(_blackboard, impulseMagnitude);
            Debug.Log($"[DragThrowState] [6/6] 놓음 — 던진 속도={throwVelocity.ToString("F2")}(속력 {speed:F2}, " +
                $"상한 {maxSpeed:F2}), 충격량={impulseMagnitude:F2} -> {(wentRagdoll ? "RAGDOLL" : "Fall")}.");
            if (!wentRagdoll)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
            }
        }
    }
}
