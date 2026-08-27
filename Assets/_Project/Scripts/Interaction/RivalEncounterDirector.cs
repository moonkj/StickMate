using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 11절 라이벌 스틱맨 조우의 스폰 판정 — 확률/쿨다운/상호배제(10절 격파 미니게임과
    /// 동시 발동 금지, 16절-15 전체 스펙터클 상호배제)를 전담하는 독립 컴포넌트. 실제 추적/전투 AI는
    /// Interaction/RivalStickmanAgent.cs가 담당하고, 이 클래스는 "언제 스폰할지"만 결정한다 —
    /// StickmanAgent(플레이어) 코드는 이 기능의 존재를 전혀 모른다(관전 전용, 유저 개입 없음, 26-5
    /// 마우스/키보드 무관 원칙과 동일한 정신으로 플레이어 상태머신에 아무 것도 얹지 않는다).
    /// </summary>
    public sealed class RivalEncounterDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private RivalStickmanAgent _rival;
        [SerializeField] private StickConfig _config;

        private float _checkTimer;
        private float _cooldownRemaining;

        private void OnEnable()
        {
            StickmanEventBus.RivalDuelEnded += OnDuelEnded;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.RivalDuelEnded -= OnDuelEnded;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_rival == null || _player == null || _config == null || _rival.InDuel) return;

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.rivalSpawnCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return; // 10절/16-15: 다른 스펙터클과 상호배제

            var footholds = _player.Blackboard.FootholdPoller != null ? _player.Blackboard.FootholdPoller.CachedFootholds : null;
            int minFootholds = _config.rivalSpawnMinFootholds;
            if (footholds == null || footholds.Count < minFootholds) return; // "유효 발판 부족 시 다음 판정 주기로 이연"

            if (Random.value >= _config.rivalSpawnChance) return;

            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.RivalDuel, this)) return;

            _rival.BeginDuel(_player, ComputeSpawnPosition());
        }

        private Vector2 ComputeSpawnPosition()
        {
            // "화면 가장자리에서 빨간 라이벌이 걸어 들어옴"(11절)의 근사 — 정교한 씬 경계 기반 스폰은
            // 실제 씬/프리팹 배선 시점(Phase 2+ 범위 밖)에 보강 가능. 지금은 플레이어 기준 좌우 오프셋.
            Vector2 playerPos = _player.Blackboard.Body != null ? _player.Blackboard.Body.position : Vector2.zero;
            float offset = _config != null ? _config.rivalSpawnOffsetWorldX : 6f;
            float side = Random.value < 0.5f ? -1f : 1f;
            return new Vector2(playerPos.x + side * offset, playerPos.y);
        }

        private void OnDuelEnded(RivalDuelResult result)
        {
            _cooldownRemaining = _config != null ? _config.rivalSpawnCooldownSeconds : 1200f;
            SpectacleEventLock.Release(this);
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            _rival?.ForceEndDuel(); // ForceEndDuel -> RivalDuelEnded 발행 -> 위 OnDuelEnded가 락을 해제.
        }
    }
}
