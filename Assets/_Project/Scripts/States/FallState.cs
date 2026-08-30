using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태: StickConfig.gravityScale에 따라 자유낙하(중력 자체는 Rigidbody2D 설정으로 처리 —
    /// 이 상태는 착지/화면이탈 감지만 담당).
    /// 전이: 발판 착지 감지 -> LandingCrouch(낙하 높이 >= StickConfig.rollLandingHeightThreshold) /
    ///                        Idle/Walk(그 미만 — 착지 시 이동 입력 유무로 분기) /
    ///       화면(발판 좌우 범위) 이탈 -> Fall 유지(사실상 no-op) /
    ///       외력 임계값 초과 -> Ragdoll(강제 인터럽트, Phase 2).
    /// 낙하 중 자세는 이 상태가 아니라 StickmanBlackboard.TickPose()가 상태 ID로 적용한다
    /// (States/StickmanPoseAnimator.ApplyFallPose — 팔은 위/바깥, 다리는 살짝 접힘).
    /// </summary>
    public sealed class FallState : IStickmanState
    {
        private readonly StickmanBlackboard _blackboard;

        // 착지 확정 유예 타이머. StickmanStateMachine.cs 전이 규칙 주석("Fall -> Idle/Walk : 발판 착지
        // 감지(fallGraceDuration 유예 적용)")을 그대로 반영 — 스쳐 지나가는 한 프레임짜리 접촉만으로
        // 착지가 확정돼 바로 다음 프레임에 다시 Fall로 되돌아가는 채터링(chattering)을 막기 위함.
        // StickConfig.fallGraceDuration은 "발판을 잃을 때"(StickmanBlackboard.GroundedTick)와
        // "착지를 확정할 때"(여기) 양쪽에 재사용되는 공용 히스테리시스 값이다.
        private float _landingConfirmTimer;

        // 이 Fall 페이즈가 시작된 시점의 월드 Y — 착지 확정 시 낙하 높이를 계산해 구르기 이펙트 트리거
        // (UX_FLOW.md 4절 "구르기(ROLL)") 판정에 사용한다.
        private float _fallStartWorldY;

        // ★ 헤드라인 기능 수정(2026-08-28) — 스윕 착지 판정용 "직전 프레임 발 위치".
        // 왜 필요한가는 States/GroundSensor.TryFindLandingCrossing()의 문서에 유도 과정까지 적어뒀다:
        // 허용오차 밴드(±groundSnapTolerance)를 한 시점만 보는 기존 판정은 낙하 속도가 약 11유닛/초를
        // 넘는 순간(=자유낙하 2.2유닛 이후) 원리적으로 성립하지 않아, 실제 타 앱 창 위에는 절대 착지할
        // 수 없었다. 이 필드는 "이번 프레임에 발이 지나간 선분"의 시작점이다.
        private Vector2 _prevFootWorldPos;
        private bool _hasPrevFootSample;

        /// <summary>FallbackPlatformWindowService가 합성 안전망 발판에 부여하는 핸들(그 클래스 참고).
        /// 2026-08-29부터 안전망은 Dock 좌/우 바깥 두 조각이라 핸들도 둘이다(-1 왼쪽, -3 오른쪽).</summary>
        private const long SyntheticSafetyNetHandle = -1L;
        private const long SyntheticSafetyNetRightHandle = -3L;

        /// <summary>같은 클래스가 합성 Dock 발판에 부여하는 핸들 — 착지 로그에서 구분해 표시한다.</summary>
        private const long SyntheticDockHandle = -2L;

        /// <summary>
        /// "위로 움직이는 중"으로 볼 최소 상승 속도(월드 유닛/초). 정확히 0으로 두면 접지 직전의 미세한
        /// 수치 진동(+1e-5 같은 값)만으로도 착지가 계속 거부되어 원래의 "느린 하강 착지" 경로가 죽는다.
        /// 아주 작은 값이면 충분하다 — 실제로 문제가 되는 "던져 올린" 상승은 유닛/초 단위다.
        /// </summary>
        private const float UpwardLandingVelocityEpsilon = 0.05f;

        // 같은 발판에 연속 착지할 때 로그가 중복되지 않도록 하는 직전 값(long.MinValue = 아직 없음).
        private long _lastLoggedLandingHandle = long.MinValue;

        public FallState(StickmanBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public StickmanStateId StateId => StickmanStateId.Fall;

        public void Enter(StateTransitionContext context)
        {
            _landingConfirmTimer = 0f;
            _fallStartWorldY = _blackboard.Body != null ? _blackboard.Body.position.y : 0f;
            _prevFootWorldPos = _blackboard.Body != null ? _blackboard.Body.position : Vector2.zero;
            _hasPrevFootSample = _blackboard.Body != null;
            // 공중에서는 어떤 발판도 붙잡고 있지 않다(발판 고착 해제) — StickmanBlackboard.CurrentFootholdHandle
            // 문서 참고. 이 초기화 덕분에 다음 착지에서 발판을 새로 획득하고, 낡은 핸들로 인한 접지 실패가
            // 스스로 회복된다.
            _blackboard.CurrentFootholdHandle = 0L;
            _blackboard.ReportFootholdChangeIfNeeded("Fall 진입 — 공중");
            // 낙하 중 공중 자세(팔은 위/바깥, 다리는 살짝 접힘)는 StickmanBlackboard.TickPose()가 상태
            // ID를 보고 매 프레임 적용한다(States/StickmanPoseAnimator.ApplyFallPose) — Walk를 제외한
            // 모든 능동 상태의 포즈가 그 한 곳에서 결정된다는 이 프로젝트의 계약을 그대로 따른다.
            // 2026-08-29 이전에는 그 분기가 없어 낙하 중에도 Idle 중립 포즈(막대기)로 떨어졌다.
        }

        public void Tick(float deltaTime)
        {
            GroundSensor.GroundInfo info = _blackboard.SenseGround();
            if (_blackboard.CheckScreenBoundsOrFall(info)) return; // 이미 Fall이라 사실상 no-op이지만 안전하게 유지

            // ★ 1순위: 스윕 교차 착지(GroundSensor.TryFindLandingCrossing 문서 참고). 이번 프레임에 발이
            // 어떤 발판의 상단선을 위->아래로 가로질렀다면 낙하 속도와 무관하게 그 자리에서 착지를
            // 확정한다. 유예(fallGraceDuration)를 적용하지 않는 이유: 교차는 "스쳐 지나간 접촉"이 아니라
            // 기하학적으로 확정된 관통이므로, 채터링 방지를 위한 유예가 필요 없고 오히려 그 유예 때문에
            // 실제 창 위 착지가 전부 실패해왔다(같은 문서의 유도 참고).
            if (_hasPrevFootSample && _blackboard.Body != null)
            {
                Vector2 currFoot = _blackboard.Body.position;
                if (_blackboard.TryFindLandingCrossing(_prevFootWorldPos, currFoot, out long crossedHandle, out float landingWorldY))
                {
                    ConfirmLanding(landingWorldY, crossedHandle);
                    return;
                }
                _prevFootWorldPos = currFoot;
            }
            else if (_blackboard.Body != null)
            {
                _prevFootWorldPos = _blackboard.Body.position;
                _hasPrevFootSample = true;
            }

            // 2순위(기존 경로 유지): 아주 느린 하강/미세 진동으로 교차가 성립하지 않는 경우를 위해
            // 허용오차 밴드 + 유예 시간 판정을 그대로 남긴다.
            //
            // ★ 2026-08-28 추가 — **상승 중 착지 금지**(사용자 신고 "갑자기 다른 창 위로 올라감"의
            // 두 번째 경로). 1순위 스윕 교차 판정에는 원래부터 방향 조건이 있었지만
            // (GroundSensor.TryFindLandingCrossing: `currOs.y <= prevOs.y`면 즉시 false + 상단선을
            // 위->아래로 지났을 때만 인정), 이 2순위 밴드 판정에는 방향 개념이 아예 없었다. 그래서
            // 캐릭터를 **위로 던지면** 상승 중에 어떤 창 상단선의 ±groundSnapTolerance 밴드에 들어가고,
            // 포물선 정점 부근에서는 속도가 0에 가까워 그 밴드에 fallGraceDuration(0.1초)을 쉽게
            // 채운다 -> 지나쳐 올라가던 창 위에 그대로 "착지"했다. 사람이 바닥을 아래에서 뚫고 올라가며
            // 착지하지는 않으므로, 몸이 위로 움직이는 동안에는 이 경로도 성립시키지 않는다.
            bool movingUpward = _blackboard.Body != null && _blackboard.Body.linearVelocity.y > UpwardLandingVelocityEpsilon;
            // ★ 2026-08-29 — 뛰어내리기 직후의 drop-through 유예(StickmanBlackboard
            // .DropThroughIgnoredFootholdHandle 문서 참고). 1순위 스윕 경로는 블랙보드 래퍼가 이미
            // 무시 핸들을 넘겨 걸러주지만, 이 2순위 밴드 경로는 GroundSensor.Sense()의 결과를 직접
            // 보므로 여기서 한 번 더 확인해야 한다 — 방금 떠난 발판의 상단선 허용오차 밴드 안에
            // 몸이 잠시 남아 있는 동안 이 경로로 제자리 착지가 확정되는 것을 막는다.
            bool ignoredByDropThrough = _blackboard.IsFootholdDropThroughIgnored(info.GroundedFootholdHandle);
            if (!info.Grounded || movingUpward || ignoredByDropThrough)
            {
                _landingConfirmTimer = 0f;
                return;
            }

            _landingConfirmTimer += deltaTime;
            float grace = _blackboard.Config != null ? _blackboard.Config.fallGraceDuration : 0.1f;
            if (_landingConfirmTimer < grace) return;

            ConfirmLanding(info.GroundWorldY, info.GroundedFootholdHandle);
        }

        /// <summary>
        /// 착지 확정 공통 처리 — 위치 스냅 + 하강 속도 제거 + 구르기 훅 + Idle/Walk 전이.
        /// 스윕 교차 경로와 기존 밴드+유예 경로가 **같은** 후처리를 쓰도록 한 곳에 모은다.
        /// </summary>
        private void ConfirmLanding(float landingWorldY, long footholdHandle)
        {
            if (_blackboard.Body != null)
            {
                // 발판 상단으로 즉시 스냅한다. 스윕 경로에서는 이 스냅이 필수다 — 교차를 감지한 시점의
                // 몸은 이미 상단선 "아래"로 내려가 있으므로(그래서 교차인 것이다) 스냅하지 않으면 다음
                // 프레임의 Idle/Walk 접지 판정이 허용오차 밖이 되어 곧바로 Fall로 되돌아간다.
                Vector2 pos = _blackboard.Body.position;
                // ★ Rigidbody2D뿐 아니라 Transform에도 함께 쓴다(StickmanBlackboard.MoveBodyToWorld) —
                // autoSyncTransforms가 꺼져 있어 Rigidbody2D.position에만 대입하면 이 프레임의 그림은
                // "물리가 방금 적분해 둔 발판 아래 위치"로 그려진다. 실측으로 착지 첫 프레임에만
                // 잉크가 화면 아래로 8.82pt 잘려 나갔다(FloorContactVisibilityTests 실패의 원인).
                _blackboard.MoveBodyToWorld(new Vector2(pos.x, landingWorldY));
                Vector2 v = _blackboard.Body.linearVelocity;
                if (v.y < 0f)
                {
                    v.y = 0f;
                    _blackboard.Body.linearVelocity = v;
                }
            }

            // 착지 확정 — 낙하 높이가 임계값 이상이면 구르기 착지 훅 발행(UX_FLOW.md 4절 "구르기(ROLL)").
            // 부수 연출(먼지 파티클 등)을 위한 신호로 그대로 유지한다.
            float fallHeight = _fallStartWorldY - landingWorldY;
            float rollThreshold = _blackboard.Config != null ? _blackboard.Config.rollLandingHeightThreshold : 2f;
            bool crouchLanding = fallHeight >= rollThreshold;
            if (crouchLanding)
            {
                // 좌표를 함께 싣는다 — 라이벌도 같은 FallState 타입을 쓰므로(RivalStickmanAgent), 좌표가
                // 없으면 구독자가 "누구의 착지인지" 알 수 없다(StickmanEventBus.LandingImpactEvent 문서).
                float footX = _blackboard.Body != null ? _blackboard.Body.position.x : 0f;
                StickmanEventBus.RaiseLandingRollRequested(fallHeight, new Vector2(footX, landingWorldY));
            }

            // 발판 고착 확정 — 이 시점부터 접지 판정은 이 핸들만 본다(리더 지시 3~5항).
            _blackboard.CurrentFootholdHandle = footholdHandle;
            _blackboard.ReportFootholdChangeIfNeeded("착지");

            // 헤드라인 기능 검증용 착지 증거 로그(리더가 화면을 볼 수 없으므로 로그가 유일한 판별 수단).
            // 착지는 이산 이벤트라 상주 앱의 로그를 크게 더럽히지 않지만, 같은 발판에 연속으로 다시
            // 착지하는 경우(경계 진동)는 한 줄로 접어 중복을 막는다.
            if (footholdHandle != _lastLoggedLandingHandle)
            {
                _lastLoggedLandingHandle = footholdHandle;
                Debug.Log($"[FallState] 착지 확정 — 발판핸들={footholdHandle}" +
                    $"{(footholdHandle == SyntheticSafetyNetHandle ? "(화면 최하단 안전망-Dock왼쪽바깥)" : footholdHandle == SyntheticSafetyNetRightHandle ? "(화면 최하단 안전망-Dock오른쪽바깥)" : footholdHandle == SyntheticDockHandle ? "(Dock)" : "(실제 창)")}, " +
                    $"착지 월드Y={landingWorldY:F3}, 낙하높이={fallHeight:F2}유닛.");
            }

            _blackboard.ResetGroundLossTimer();

            // ★★ 무릎앉아 착지(2026-08-29, 사용자 명시 요청 "떨어질때 무릎앉아 형태로 멋지게 착지해야지").
            //
            // 위 LandingRollRequested는 2026-08-27부터 여기서 발행되고 있었지만 **구독자가 프로젝트 전체에
            // 0명**이었다 — 즉 "부드러운 착지 연출"은 판정만 있고 실물이 통째로 없었다(이 프로젝트에서
            // 6번 반복된 "로직은 있는데 아무도 안 듣는" 패턴). 그래서 상태 전이 자체는 이벤트 구독자에게
            // 맡기지 않고 여기서 직접 확정한다:
            //   · 착지 후 무엇을 하는가는 "있으면 좋은 연출"이 아니라 **흐름 그 자체**다. 구독자에게
            //     맡기면 이 메서드가 이미 Idle/Walk로 전이한 뒤 구독자가 다시 ChangeState를 부르는
            //     순서 의존이 생기고, 구독자가 사라지면 조용히 예전 거동으로 되돌아간다.
            //   · 깊이/유지시간의 입력이 되는 낙하 높이는 이벤트 페이로드와 **같은 값**을 블랙보드
            //     스냅샷으로 넘긴다(LastImpactMagnitude -> RagdollState와 완전히 같은 관례).
            // 낙차가 임계값 미만이면(예: Dock 단차 0.855유닛) 이 분기를 타지 않으므로 예전과 100% 동일하게
            // 곧바로 Idle/Walk로 복귀한다 — 한 계단 내려올 때마다 무릎을 꿇지 않는다.
            bool crouchEnabled = _blackboard.Config == null || _blackboard.Config.landingCrouchEnabled;
            if (crouchLanding && crouchEnabled)
            {
                _blackboard.LastLandingFallHeight = fallHeight;
                _blackboard.Machine.ChangeState(StickmanStateId.LandingCrouch);
                return;
            }

            float deadzone = _blackboard.Config != null ? _blackboard.Config.moveInputDeadzone : 0.15f;
            StickmanStateId next = Mathf.Abs(_blackboard.MoveInputX) > deadzone ? StickmanStateId.Walk : StickmanStateId.Idle;
            _blackboard.Machine.ChangeState(next);
        }

        public void Exit() { }
    }
}
