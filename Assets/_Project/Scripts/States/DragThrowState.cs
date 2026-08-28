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
    /// 물리: Enter()에서 Rigidbody2D를 Kinematic으로 전환(질량 개입 없이 위치를 직접 제어)하고, 잡은
    /// 지점(grab offset)을 유지한 채 커서를 따라간다.
    ///
    /// ★ 2026-08-28 추종 방식 변경(사용자 피드백 "마우스에 딱 붙어서 끌려가야 하는데 이상하게 끌려감"):
    /// 기본값이 **즉시 밀착**으로 바뀌었다(StickConfig.dragFollowSmoothTime = 0). 원래 12절은 "몸통은
    /// 커서를 스프링·댐퍼로 뒤따라오는 관성감(순간 텔레포트처럼 딱 붙지 않음)"을 요구했지만, 실제로
    /// 만들어 보니 사용자가 이를 "이상하게 끌려간다"고 느꼈다 — 커서로 물건을 끄는 상호작용에서는
    /// 잡은 지점이 커서에서 눈에 띄게 뒤처지는 것 자체가 고장으로 읽힌다. 그래서 **잡은 지점은 커서에
    /// 밀착**시키고, 12절이 원한 "대롱대롱 매달린 느낌"은 팔다리 쪽(포즈/관성)에 맡긴다. 스프링 경로는
    /// 삭제하지 않았고 그 설정값을 0보다 크게 두면 그대로 되살아난다(FollowCursor 문서 참고).
    ///
    /// 최근 dragThrowVelocitySampleWindowSeconds(0.12초) 구간의 **커서** 위치 이력을 원형 버퍼에 쌓아두었다가,
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

        // [4/6] 추종 로그 주기(초). 1초는 짧은 드래그 한 번에 한두 줄밖에 안 남아 사용자 테스트 로그로
        // 판별하기에 표본이 부족했다 — 0.5초로 줄여 같은 드래그에서 두 배의 표본을 남긴다.
        private const float FollowLogInterval = 0.5f;

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
            if (_followLogTimer >= FollowLogInterval)
            {
                _followLogTimer = 0f;
                Vector2 body = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;
                // ★ "밀착 오차" — 사용자가 실제로 잡은 지점(몸통 - 잡은 오프셋)이 커서에서 얼마나
                // 떨어져 있는가. 에이전트는 마우스를 조작할 수 없어 드래그 손맛을 직접 검증할 수 없으므로,
                // 사용자가 테스트할 때 이 한 값만 보면 "딱 붙었는지"를 객관적으로 판별할 수 있게 한다.
                // 0에 가까울수록 밀착(즉시 추종 경로에서는 소수점 둘째 자리까지 0.00이 정상이다).
                // 참고: 지면 소프트 클램프(아래 FollowCursor)가 걸리면 커서가 지면 아래로 내려간
                // 만큼은 의도적으로 오차가 남는다 — 그건 버그가 아니라 "바닥 밑으로는 끌고 가지 않는다"는 규칙이다.
                Vector2 grabbedPointWorld = body - _grabOffset;
                float stickError = Vector2.Distance(grabbedPointWorld, cursorWorld);
                Debug.Log($"[DragThrowState] [4/6] 드래그 추종 중 — 커서 월드={cursorWorld.ToString("F2")}, " +
                    $"잡은 지점={grabbedPointWorld.ToString("F2")}, **밀착 오차={stickError:F3}유닛**, " +
                    $"몸통={body.ToString("F2")}, 목표(커서+오프셋)={(cursorWorld + _grabOffset).ToString("F2")}, " +
                    $"추종 스무딩={( _blackboard.Config != null ? _blackboard.Config.dragFollowSmoothTime : 0f):F3}초, " +
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

        /// <summary>
        /// ★ 2026-08-28 사용자 피드백 대응 — "마우스로 끌고가면 마우스에 딱 붙어서 끌려가야 하는데
        /// 이상하게 끌려감".
        ///
        /// 무엇이 문제였나(두 겹의 지연이 겹쳐 있었다):
        ///   (1) SmoothDamp(0.08초) — 목표까지의 오차를 지수적으로 줄이는 스프링이라 **원리상 목표에
        ///       도달하지 않는다**. 커서를 일정 속도로 끌면 캐릭터는 항상 `속도 × 0.08초`만큼 뒤에
        ///       끌려간다(예: 5유닛/초로 끌면 0.4유닛 = 몸통 높이의 약 1/5). 사용자가 "흐물흐물"로
        ///       느낀 것의 주범.
        ///   (2) Rigidbody2D.MovePosition() — Kinematic 바디에서 이 호출은 "다음 물리 스텝까지
        ///       이동하라"는 예약이다. 이 Tick()은 Update()(프레임)에서 도는데 물리는 FixedUpdate
        ///       주기라, 매 프레임 목표를 갱신해도 실제 반영은 항상 한 물리 스텝 뒤다.
        ///
        /// 어떻게 고쳤나: dragFollowSmoothTime이 0 이하(현재 기본값)면 스프링을 **완전히 건너뛰고**
        /// 목표 위치를 그 프레임에 즉시 대입한다. Rigidbody2D.position(물리 바디)과 Transform(렌더링)
        /// 양쪽에 모두 써서 (2)의 한 스텝 지연까지 없앤다 — 물리 바디만 갱신하면 다음 물리 스텝 전까지
        /// 화면상 위치가 그대로라 눈에는 여전히 뒤처져 보인다. 드래그 중 루트는 Kinematic이므로 이
        /// 순간이동은 물리적으로도 합법이며(질량/충돌 반작용 개입 없음), 이것이 곧 12절이 요구하는
        /// "커서에 잡힌 물건" 그 자체다.
        ///
        /// 값을 0보다 크게 두면 예전 스프링·댐퍼 경로가 그대로 되살아난다(두 경로를 모두 유지).
        ///
        /// **던지기 속도와는 완전히 별개다** — ComputeThrowVelocity()는 몸통 위치를 한 번도 읽지 않고
        /// PushSample()이 쌓은 **커서 좌표 이력**(0.12초 창)만 평균한다. 즉 추종을 아무리 즉각적으로
        /// 만들어도 던지는 손맛(12절의 손떨림 방지 스무딩 포함)은 수치 하나 바뀌지 않는다.
        /// </summary>
        private void FollowCursor(Vector2 target, float deltaTime)
        {
            if (_blackboard.Body == null) return;
            float smoothTime = _blackboard.Config != null ? _blackboard.Config.dragFollowSmoothTime : 0f;
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

            if (smoothTime <= 0f)
            {
                // 즉시 밀착 경로(기본값).
                _followVelocityRef = Vector2.zero; // 나중에 스무딩을 다시 켜도 낡은 속도에서 튀지 않도록.
                SetBodyPositionImmediate(desired);
                return;
            }

            Vector2 next = Vector2.SmoothDamp(current, desired, ref _followVelocityRef, smoothTime, Mathf.Infinity, deltaTime);
            _blackboard.Body.MovePosition(next);
        }

        /// <summary>
        /// 물리 바디와 Transform을 같은 좌표로 동시에 맞춘다(FollowCursor 문서 (2) 참고).
        /// Physics2D.autoSyncTransforms가 꺼져 있는 기본 설정에서는 둘 중 하나만 써도 나머지 하나가
        /// 다음 물리 스텝까지 낡은 값을 들고 있으므로, "화면에 보이는 위치"와 "물리가 아는 위치"가
        /// 프레임마다 어긋난다. 드래그 중에는 이 어긋남이 곧 사용자가 체감하는 지연이다.
        /// </summary>
        private void SetBodyPositionImmediate(Vector2 position)
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return;
            body.position = position;                       // 물리 바디(놓는 순간 Dynamic 복귀 기준점).
            Transform t = body.transform;
            Vector3 local = t.position;
            t.position = new Vector3(position.x, position.y, local.z); // 렌더링(이번 프레임에 바로 반영).
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
