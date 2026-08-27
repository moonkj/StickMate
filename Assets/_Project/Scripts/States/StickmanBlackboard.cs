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
    /// 입력 스냅샷(MoveInputX/JumpPressed)은 StickmanAgent(Core)가 매 프레임 딱 한 번만 UnityEngine.Input을
    /// 읽어 채워 넣는다 — 여러 상태가 각자 Input을 폴링하는 중복을 막기 위함.
    /// </summary>
    public sealed class StickmanBlackboard
    {
        public Rigidbody2D Body;
        public Camera MainCamera;
        public StickConfig Config;
        public StickmanStateMachine Machine;
        public FootholdPoller FootholdPoller;

        /// <summary>-1(왼쪽)~1(오른쪽). StickmanAgent.Update()가 매 프레임 갱신.</summary>
        public float MoveInputX;

        /// <summary>이번 프레임에 점프 입력이 새로 눌렸는지. StickmanAgent.Update()가 매 프레임 갱신.</summary>
        public bool JumpPressed;

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
