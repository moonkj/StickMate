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

        // ── 발버둥(2026-08-29, 사용자 요청 "잡았을때 막 벗어날려는듯이 몸부림 치게끔") ──
        /// <summary>발버둥 전용 누적 시간(초). Idle 호흡/보행 위상과 독립이라 **잡을 때마다 0에서
        /// 시작한다**(ResetWalkPhase와 같은 관례 — 매번 같은 자세에서 시작하는 편이 예측 가능하다).</summary>
        private float _struggleTime;

        /// <summary>커서 속도(신장/초)의 지수 감쇠 스무딩 값 — 한 프레임짜리 손떨림에 세기가 튀지 않게 한다.</summary>
        private float _cursorSpeedHeights;
        private Vector2 _prevCursorWorld;
        private bool _hasPrevCursor;

        /// <summary>진단/테스트용 — 지금 프레임의 발버둥 세기(0~1). 순수 연출이라 로그로는 판정이
        /// 불가능하므로 값으로 단언할 수 있게 노출한다(LandingCrouchState.CurrentCrouchAmount와 같은 관례).</summary>
        public float CurrentStruggleIntensity { get; private set; }

        /// <summary>커서 속도 스무딩 계수(1/초). 보이는 값을 정하는 튜닝 스칼라가 아니라 잡음 필터라
        /// StickConfig가 아니라 여기 상수로 둔다.</summary>
        private const float CursorSpeedSmoothingRate = 8f;

        /// <summary>몸통 비틀림이 팔다리보다 얼마나 느린가. 같은 주파수로 비틀면 몸과 팔다리가 한 덩어리로
        /// 움직여 "허우적"이 사라진다. 자세의 형태라 상수로 둔다(비틀림의 크기만 StickConfig가 정한다).</summary>
        private const float StruggleTwistFrequencyRatio = 0.61f;

        public StickmanStateId StateId => StickmanStateId.Dragged;

        public DragThrowState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Enter(StateTransitionContext context)
        {
            _holdTimer = 0f;
            _struggleTime = 0f;
            _cursorSpeedHeights = 0f;
            _hasPrevCursor = false;
            CurrentStruggleIntensity = 0f;
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
            TickStruggle(deltaTime, cursorWorld);

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

            // 발버둥으로 비틀어 둔 몸통을 반드시 직립으로 되돌린다 — 이 정리를 빠뜨리면 놓는 순간
            // 기울어진 채로 굳는다(다음 상태 중 ThrowTumble/Ragdoll은 스스로 회전을 다루지만
            // Fall/Idle은 SnapRootUpright이 다음 프레임에 고쳐줄 뿐이라 한 프레임 기울어 보인다).
            // 종료 경로가 여러 개(놓기/타임아웃/커서 소실/전체화면 강제 취소)라 한 곳에 모은다.
            ResetStruggleTwist();
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

            // 소프트 클램프(12절 "화면 경계 도달 시 ... 안쪽으로 소프트 클램프") — **세상 바닥** 아래로는
            // 끌고 내려가지 않는다. Kinematic MovePosition은 정적 바닥 콜라이더를 그대로 통과하므로,
            // 커서가 지면보다 아래(예: macOS Dock 영역)에 있을 때 그대로 따라가면 캐릭터가 바닥 밑에
            // 놓인 채 놓여진다. 그 위치는 접지 허용 오차 밖이라 Grounded가 영원히 false이고 물리 바닥이
            // 위로 올려주지도 못해 **Fall 상태에 영구 고착**된다(실측 확인, 2026-08-28).
            //
            // ★ 2026-08-28 버그 수정 — 사용자 신고 "마우스로 끌었는데 갑자기 다른 창 위로 올라감".
            // 예전에는 이 "바닥"을 TryGetGroundSurfaceWorldY(= 그 x에서 **가장 높은** 창 상단)로 물었다.
            // 이 클램프는 `desired.y < floor`일 때 desired를 **위로** 올리는 단방향 연산이므로, 커서가
            // 화면 위쪽 창의 가로 범위에 걸치기만 하면 화면 아래에서 끌던 캐릭터가 매 프레임 그 창
            // 상단으로 끌어올려졌다(실측 규모: 안전망 월드 -10.17 -> Finder 창 상단 월드 +8.1, 약 18유닛
            // 순간이동). 클램프의 원래 목적에 대응하는 값은 "그 x에서 **가장 낮은** 표면"이므로
            // TryGetFloorWorldY로 교체한다(States/GroundSensor.TryGetFloorWorldY 문서에 유도 전문).
            if (_blackboard.TryGetFloorWorldY(desired, out float floorY) && desired.y < floorY)
            {
                desired.y = floorY;
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
            // 이 두 줄짜리 패턴을 본떠 StickmanBlackboard.MoveBodyToWorld가 만들어졌다 — 중복을
            // 남기지 않도록 여기서도 그 창구를 쓴다(동작은 동일: 물리 바디 + Transform 동시 기록).
            _blackboard.MoveBodyToWorld(position);
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

        // ============================================================================
        // ★ 발버둥 (2026-08-29, 사용자 명시 요청 "마우스로 캐릭을 잡았을때 막 벗어날려는듯이
        //    몸부림 치게끔 만들어줘")
        // ============================================================================
        //
        // 무엇이 "살아있음"과 "루프 애니메이션"을 가르는가 (리더 지시의 핵심)
        // ────────────────────────────────────────────────────────────────────────────
        // 일정한 진폭으로 계속 흔들면 그건 기계다. 그래서 세기를 세 겹으로 곱한다:
        //   [1] 리듬  : "세게 몸부림 → 잠깐 지침"의 주기적 봉우리(EvaluateStruggleEnvelope).
        //   [2] 지침  : 잡혀 있는 시간이 길수록 전체 세기가 잦아든다(반감기, 하한 있음).
        //   [3] 반응  : 커서를 빠르게 흔들면 그만큼 더 격렬해진다(무차원 커서 속도 × 계수, 상한 있음).
        // 셋 다 StickConfig로 끌 수 있고(0으로 두면 그 겹만 사라진다), 마스터 스위치를 끄면 자세가
        // 예전의 Idle 중립으로 정확히 되돌아간다(StickmanBlackboard.TickPose의 Dragged 분기).
        //
        // 물리에 맡기지 않는다 (아키텍처 0절)
        // ────────────────────────────────────────────────────────────────────────────
        // 팔다리는 Kinematic이고 각도는 전부 절차적 localRotation이다(StickmanPoseAnimator).
        // 몸통 비틀림도 물리 토크가 아니라 루트의 **시각 회전** 직접 대입이다. 흔들리는 것을 물리에
        // 맡기면 관절이 제멋대로 꺾이는 그림이 되는데, 그게 이 프로젝트 사용자가 반복해서 신고한 증상이다.
        //
        // 드래그 추종을 방해하지 않는다
        // ────────────────────────────────────────────────────────────────────────────
        // 루트 **위치**는 여기서 한 픽셀도 건드리지 않는다. 위치를 흔들면 "커서에 딱 붙어 끌려온다"는
        // 2026-08-28 수정(dragFollowSmoothTime=0 즉시 대입)이 그대로 무효가 된다. 몸부림은 그 위에
        // 얹히는 팔다리 각도 + 몸통 회전이다.
        //
        // 대사는 만들지 않는다 — 사용자는 요청하지 않은 자율 연출에 반복적으로 민감했다. 나중에
        // 붙인다면 전이가 확정된 Enter()에서 이 상태의 파라미터로부터만 파생시켜야 한다(불변 원칙 1).

        private void TickStruggle(float deltaTime, Vector2 cursorWorld)
        {
            StickConfig cfg = _blackboard.Config;
            if (cfg != null && !cfg.dragStruggleEnabled)
            {
                // 스위치 OFF — 포즈는 TickPose의 Dragged 분기가 예전 경로(Idle 중립)로 처리한다.
                CurrentStruggleIntensity = 0f;
                return;
            }

            _struggleTime += deltaTime;

            // 커서 속도(신장/초) — 거리 성분이라 신장으로 나눠 무차원화한다(배율 불변).
            if (_hasPrevCursor && deltaTime > 0.0001f)
            {
                float speed = Vector2.Distance(cursorWorld, _prevCursorWorld) / deltaTime;
                float heights = speed / Mathf.Max(0.0001f, _blackboard.CharacterHeightWorld);
                _cursorSpeedHeights = Mathf.Lerp(_cursorSpeedHeights, heights,
                    1f - Mathf.Exp(-CursorSpeedSmoothingRate * deltaTime));
            }
            _prevCursorWorld = cursorWorld;
            _hasPrevCursor = true;

            float period = cfg != null ? cfg.dragStruggleBurstPeriodSeconds : 1.15f;
            float duty = cfg != null ? cfg.dragStruggleBurstDutyFraction : 0.55f;
            float rest = cfg != null ? cfg.dragStruggleRestIntensity : 0.18f;
            float rhythm = EvaluateStruggleEnvelope(_struggleTime, period, duty, rest);

            float halfLife = cfg != null ? cfg.dragStruggleFatigueHalfLifeSeconds : 4.5f;
            float minIntensity = cfg != null ? Mathf.Clamp01(cfg.dragStruggleMinIntensity) : 0.4f;
            float fatigue = EvaluateStruggleFatigue(_holdTimer, halfLife, minIntensity);

            float response = cfg != null ? cfg.dragStruggleCursorSpeedResponse : 0.12f;
            float maxBoost = cfg != null ? Mathf.Max(0f, cfg.dragStruggleMaxCursorBoost) : 0.6f;
            float boost = Mathf.Clamp(_cursorSpeedHeights * response, 0f, maxBoost);

            CurrentStruggleIntensity = Mathf.Clamp01(rhythm * fatigue * (1f + boost));

            StickmanPoseAnimator pose = _blackboard.GetPoseAnimator();
            pose?.ApplyDragStrugglePose(deltaTime, _blackboard.BuildPoseSettings(),
                _blackboard.BuildDragStrugglePoseSettings(), _blackboard.PoseSmoothingRate,
                CurrentStruggleIntensity, _struggleTime);

            ApplyStruggleTwist(cfg);
        }

        /// <summary>
        /// 몸통 비틀림을 루트의 **시각 회전**에만 적용한다(위치는 절대 건드리지 않는다).
        /// Rigidbody2D와 Transform 양쪽에 쓰는 이유는 이 프로젝트가 Physics2D.autoSyncTransforms를
        /// 꺼두었기 때문이다(둘 중 하나만 쓰면 화면과 물리가 한 프레임씩 어긋난다 — 드래그 추종이
        /// 위치를 두 곳에 모두 쓰는 것과 같은 이유, SetBodyPositionImmediate 참고).
        /// </summary>
        private void ApplyStruggleTwist(StickConfig cfg)
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return;

            float amplitude = cfg != null ? cfg.dragStruggleTwistDegrees : 9f;
            float frequency = cfg != null ? cfg.dragStruggleFrequencyHz : 3.4f;
            float angle = amplitude * CurrentStruggleIntensity *
                Mathf.Sin(_struggleTime * frequency * StruggleTwistFrequencyRatio * Mathf.PI * 2f);

            body.rotation = angle;
            Transform t = body.transform;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>비틀림을 정확히 0으로 되돌린다(Exit 전용, 멱등).</summary>
        private void ResetStruggleTwist()
        {
            Rigidbody2D body = _blackboard.Body;
            if (body == null) return;
            body.rotation = 0f;
            body.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 몸부림 리듬 곡선 — "세게 몸부림 → 잠깐 지침"의 한 주기(0~1).
        /// 앞의 <paramref name="dutyFraction"/>만큼이 사인 한 봉우리(0 → 1 → 0)이고 나머지가 지침
        /// 구간(<paramref name="restIntensity"/> 고정)이다. 봉우리 구간에서도 지침 세기 아래로는
        /// 내려가지 않게 해, 버스트의 시작/끝에서 순간적으로 축 늘어지는 것을 막는다.
        ///
        /// 설정 비의존 정적 메서드인 이유는 PlayMode 테스트가 곡선의 **형태**(주기 안에 강약이
        /// 실제로 존재하는가)를 직접 단언할 수 있게 하기 위해서다 —
        /// LandingCrouchState.EvaluateCrouchCurve와 같은 관례다.
        /// </summary>
        public static float EvaluateStruggleEnvelope(float time, float periodSeconds, float dutyFraction, float restIntensity)
        {
            float rest = Mathf.Clamp01(restIntensity);
            if (periodSeconds <= 0.0001f) return 1f;

            float duty = Mathf.Clamp(dutyFraction, 0.05f, 0.95f);
            float u = Mathf.Repeat(time, periodSeconds) / periodSeconds;
            if (u > duty) return rest;
            return Mathf.Max(rest, Mathf.Sin(Mathf.PI * (u / duty)));
        }

        /// <summary>
        /// 잡혀 있는 시간에 따른 전체 세기 감쇠(1 → <paramref name="minIntensity"/>). 반감기가 0 이하면
        /// 감쇠 없이 항상 1이다(그 겹만 끄는 탈출구). 위 곡선과 같은 이유로 정적 순수 함수다.
        /// </summary>
        public static float EvaluateStruggleFatigue(float heldSeconds, float halfLifeSeconds, float minIntensity)
        {
            float min = Mathf.Clamp01(minIntensity);
            if (halfLifeSeconds <= 0.0001f) return 1f;
            float decay = Mathf.Pow(0.5f, Mathf.Max(0f, heldSeconds) / halfLifeSeconds);
            return Mathf.Lerp(min, 1f, decay);
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

            float impulseMagnitude = speed * mass;

            // ============================================================================
            // ★★ 던진 뒤 무엇이 되는가 (2026-08-29, 사용자 요청 "마우스로 던졌을때도 이상하게
            //     관절꺽이면서 넘어지는데 던져도 공중에서 회전하면서 무릎앉아 착지할수있게 해줘")
            // ============================================================================
            // 예전에는 여기서 곧바로 "충격량 >= ragdollForceThreshold면 RAGDOLL"이었다. 랙돌은 정의상
            // 전신 물리 위임이라(아키텍처 0절) 팔다리가 제멋대로 꺾이며 뒹굴었고, 그것이 사용자가 신고한
            // 그 그림이다. 이제는 **깨끗하게 던져진 자유 비행**을 능동 상태(ThrowTumbleState)로 보낸다.
            //
            // 갈림 기준은 속도의 크기가 아니라 **원인**이다(States/ThrowTumbleState.cs 클래스 문서에
            // 근거를 적어뒀다):
            //   · 유저가 놓은 순간의 속도로 시작하는, 아무 것에도 부딪히지 않은 포물선 = 예측 가능
            //     -> 공중 회전 + 착지 정렬 + 무릎앉아(연출로 만들 수 있다).
            //   · 벽/창 충돌, 로데오에서 거칠게 털려 나감 = 예측 불가능한 외력
            //     -> 그대로 RAGDOLL(이 메서드를 거치지 않는 다른 경로들이며, 손대지 않았다).
            //     회전 도중에 벽에 부딪히면 그 충돌 콜백이 여전히 랙돌로 인터럽트한다.
            //
            // 아주 살살 놓은 경우(임계 미만)는 '던진 것'이 아니라 '내려놓은 것'이라 회전 없이 평범한
            // Fall로 보낸다 — 집었다 놓을 때마다 공중제비를 돌면 그게 오히려 고장으로 읽힌다.
            StickConfig cfg = _blackboard.Config;
            bool tumbleEnabled = cfg == null || cfg.throwTumbleEnabled;
            float characterHeight = _blackboard.CharacterHeightWorld;
            float heightsPerSecond = speed / Mathf.Max(0.0001f, characterHeight);
            // ★ 판정식은 ThrowTumbleState의 정적 순수 함수 하나만 쓴다 — 던지는 쪽과 받는 쪽이 각자
            // 계산하면 어긋나는 순간 "던졌는데 아무 일도 안 일어나는" 상태가 된다.
            if (ThrowTumbleState.IsCleanThrow(speed, characterHeight, cfg))
            {
                // 전이가 확정되기 직전에 원인이 되는 물리량을 스냅샷으로 남긴다(LastImpactMagnitude/
                // LastLandingFallHeight와 완전히 같은 관례) — 회전 방향과 속도가 전부 이 하나에서 나온다.
                _blackboard.LastThrowVelocity = throwVelocity;
                Debug.Log($"[DragThrowState] [6/6] 놓음 — 던진 속도={throwVelocity.ToString("F2")}(속력 {speed:F2}, " +
                    $"상한 {maxSpeed:F2} = {heightsPerSecond:F2}신장/초, 회전 하한 " +
                    $"{(cfg != null ? cfg.throwTumbleMinSpeedHeightsPerSecond : 1.2f):F2}신장/초), " +
                    $"충격량={impulseMagnitude:F2} -> **공중 회전(ThrowTumble)**.");
                _blackboard.Machine.ChangeState(StickmanStateId.ThrowTumble);
                return;
            }

            // ── 예전 경로(스위치를 끄거나 너무 약하게 놓은 경우) ─────────────────────────────
            // 충격량 = 속력 * 질량(StickmanAgent.OnCollisionEnter2D와 동일 단위 관례).
            bool wentRagdoll = RagdollImpactResolver.TryApplyImpact(_blackboard, impulseMagnitude);
            Debug.Log($"[DragThrowState] [6/6] 놓음 — 던진 속도={throwVelocity.ToString("F2")}(속력 {speed:F2}, " +
                $"상한 {maxSpeed:F2} = {heightsPerSecond:F2}신장/초), 충격량={impulseMagnitude:F2}, " +
                $"회전 스위치={tumbleEnabled} -> {(wentRagdoll ? "RAGDOLL" : "Fall")}.");
            if (!wentRagdoll)
            {
                _blackboard.Machine.ChangeState(StickmanStateId.Fall);
            }
        }
    }
}
