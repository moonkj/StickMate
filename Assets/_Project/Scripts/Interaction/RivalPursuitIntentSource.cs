using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 11절 라이벌 스틱맨의 단순 추적 AI. States/AutoWanderController.cs(26절, 플레이어의
    /// 배회용)를 재사용하지 않고 별도로 구현한다 — 관전 전용 스펙터클이라 정교함이 필요 없다(목표
    /// 위치를 향해 좌우로 이동, 도착 근처에서는 멈춤). 점프/파쿠르는 최소 스코프에서 다루지 않는다
    /// (JumpRequested는 항상 false — Idle/Walk 상태의 해당 분기가 자연히 발동하지 않는다).
    /// </summary>
    public sealed class RivalPursuitIntentSource : IMovementIntentSource
    {
        private readonly StickmanBlackboard _blackboard;
        private readonly StickConfig _config;
        private readonly System.Func<Vector2> _targetProvider;

        public float MoveInputX { get; private set; }
        public bool JumpRequested => false;

        /// <summary>라이벌은 관전 전용 스펙터클이라 매달려 내려가기도 다루지 않는다(항상 false —
        /// WalkState의 해당 분기가 자연히 발동하지 않는다, JumpRequested와 같은 이유).</summary>
        public bool LedgeHangRequested => false;

        /// <summary>라이벌은 경계에서 뛰어내리지도, 턱을 기어오르지도 않는다(위와 같은 이유 — 관전 전용
        /// 스펙터클이라 좌우 추적만으로 충분하다). WalkState의 해당 분기가 자연히 발동하지 않는다.</summary>
        public bool HopDownRequested => false;

        /// <inheritdoc cref="HopDownRequested"/>
        public bool StepUpRequested => false;

        public RivalPursuitIntentSource(StickmanBlackboard blackboard, StickConfig config, System.Func<Vector2> targetProvider)
        {
            _blackboard = blackboard;
            _config = config;
            _targetProvider = targetProvider;
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard?.Body == null || _targetProvider == null)
            {
                MoveInputX = 0f;
                return;
            }

            Vector2 targetPos = _targetProvider();
            float dx = targetPos.x - _blackboard.Body.position.x;
            float stopDistance = _config != null ? _config.rivalStopDistance : 0.6f;
            MoveInputX = Mathf.Abs(dx) <= stopDistance ? 0f : Mathf.Sign(dx);
        }
    }
}
