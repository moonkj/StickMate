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

        // Idle/Walk(지상 상태)에서 발판을 잃은 뒤 실제로 Fall로 전이하기까지의 유예 누적 시간.
        // Idle<->Walk를 오가는 동안에도 값이 보존되어야 발판 경계에서 상태가 왔다갔다 할 때마다
        // 유예 타이머가 리셋되는 오탐을 막을 수 있어(상태 인스턴스 밖인) 블랙보드에 둔다.
        private float _groundLossTimer;

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
    }
}
