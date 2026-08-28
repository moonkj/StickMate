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

        private void Awake()
        {
            // 심층 방어(2026-08-29): 씬 배선에서 _config가 비어 있으면 라이벌은 **아무 에러도 없이**
            // 영원히 스폰되지 않는다(Update()의 첫 줄에서 return). 실제로 그 사고가 한 번 났으므로
            // (Editor/SceneBootstrapper.cs의 NewScene 함정 주석) 플레이어의 설정으로 대신 채운다.
            if (_config == null && _player != null) _config = _player.Config;
            if (_config == null)
            {
                Debug.LogWarning("[라이벌] StickConfig가 비어 있습니다 — 스폰 판정을 할 수 없어 라이벌이 " +
                                 "등장하지 않습니다(씬 배선 확인 필요).");
            }
        }

        private void OnEnable()
        {
            StickmanEventBus.RivalDuelEnded += OnDuelEnded;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.RivalDuelEnded -= OnDuelEnded;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;

            // BUG-P3-M1(Major, docs/BUG_REPORT_PHASE3.md) 대응: OnDuelEnded 구독을 이미 위에서
            // 해제했으므로, ForceEndDuel()이 발행하는 RivalDuelEnded로는 더 이상 락이 자동 해제되지
            // 않는다 — 여기서 직접 반환한다(멱등 — SpectacleEventLock.Release()가 소유자 확인 후
            // no-op하므로 중복 호출해도 안전).
            ReleaseOwnedLock();
        }

        // 개선 R2(docs/CODE_REVIEW_FINAL.md) 판단: SpectacleEventLock.ReleaseIfOwned 헬퍼로 흡수하지
        // 않는 예외로 남긴다(리뷰어가 직접 지목한 소수 예외 중 하나) — 다른 11곳은 전부 "guardedState와
        // StickmanStateId를 비교해 강제 Idle 전이"하는 형태지만, 이 컨트롤러는 상태 비교가 아니라
        // RivalStickmanAgent.ForceEndDuel() 호출로 정리한다(대결 상대 캐릭터의 별도 상태머신을 건드리는
        // 것이지 _player의 StickmanStateMachine을 강제 전이하는 게 아니다) — guardedState 개념 자체가
        // 이 케이스에 없어 헬퍼 시그니처로 표현할 수 없다.
        private void ReleaseOwnedLock()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            _rival?.ForceEndDuel(); // 대결 중이었다면 캐릭터를 대기 상태로 되돌린다(승패는 무승부로 처리).
            SpectacleEventLock.Release(this);
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

        /// <summary>
        /// 확률/쿨다운을 건너뛰고 지금 즉시 대결을 시작한다(Interaction/AppControlDirector.cs의 데모
        /// 단축키 Ctrl+Opt+Cmd+V / 우클릭 메뉴 [라이벌 소환]).
        ///
        /// 왜 이 경로가 필요한가: 기본 스폰 조건은 90초 주기 × 4% 확률 + 20분 쿨다운
        /// (StickConfig.rivalSpawn*)이라 실사용 중에는 몇 시간을 지켜봐야 한 번 볼까 말까다. 그러면
        /// "구현했지만 실제로 스폰되는지 아무도 확인하지 못한" 상태가 그대로 유지된다 — 이 프로젝트가
        /// 이미 여러 번 겪은 "로직은 있는데 씬에 배치가 안 됨" 유형의 사고와 정확히 같은 뿌리다.
        ///
        /// **건너뛰는 것은 확률과 쿨다운뿐이다.** 상호배제 락(Core.SpectacleEventLock)과 "이미 대결
        /// 중이면 무시"는 그대로 지킨다 — 그 둘은 편의가 아니라 안전 규칙이기 때문이다.
        /// </summary>
        public void ForceSpawnNow(string reason)
        {
            if (_rival == null || _player == null)
            {
                Debug.LogWarning($"[라이벌] 강제 소환 실패({reason}) — 라이벌/플레이어 배선이 없습니다.");
                return;
            }
            if (_rival.InDuel)
            {
                Debug.Log($"[라이벌] 강제 소환 건너뜀({reason}) — 이미 대결 중입니다.");
                return;
            }
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.RivalDuel, this))
            {
                Debug.Log($"[라이벌] 강제 소환 건너뜀({reason}) — 다른 스펙터클이 진행 중입니다(상호배제 락).");
                return;
            }

            Vector2 spawn = ComputeSpawnPosition();
            _checkTimer = 0f;
            _rival.BeginDuel(_player, spawn);
            Debug.Log($"[라이벌] 강제 소환({reason}) — 스폰 좌표 {spawn}, 최대 지속 " +
                      $"{(_config != null ? _config.rivalMaxDurationSeconds : 30f):F0}초.");
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
