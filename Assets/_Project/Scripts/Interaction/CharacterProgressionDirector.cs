using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 캐릭터 성장의 "언제"를 담당하는 유일한 주체 — 2026-08-29 성장/장비 라운드.
    ///
    /// Core/CharacterProgressionModel.cs는 값만 보관하고, 이 컴포넌트가 XP가 들어오는 네 경로를 전부
    /// 소유한다(StressGauge ↔ StressGaugeDirector와 정확히 같은 분리).
    ///
    /// ============================================================================
    /// XP 소스 — 기존 판정 로직을 <b>한 줄도</b> 건드리지 않는다
    /// ============================================================================
    /// 리더 지시: "기존 판정 로직에 읽기 전용으로 훅만 걸어라 — 승패 판정 자체를 바꾸지 마라."
    /// 그래서 세 소스 중 보너스 2종은 <b>전부 StickmanEventBus 구독</b>으로만 구현했다. 이 파일은
    /// BattleMinigameDirector / ArcheryState를 <b>참조조차 하지 않는다</b>
    /// (grep으로 검증 가능) — 그 두 곳의 소스 코드는 이번 라운드에 수정되지 않았다.
    ///
    ///  · 패시브        : progressionPassiveTickSeconds 주기로 분당 값을 쪼개 적립.
    ///                    "아무것도 안 해도 자란다"(관찰형 앱 철학)가 주 경로다.
    ///  · 격파 승리      : BattleMinigamePhaseChanged == Success
    ///  · 활쏘기 명중    : ArcheryShotChanged.Result == Bullseye (Release 시점 1회)
    ///
    /// ============================================================================
    /// 매 프레임 할당 금지 (24시간 상주 앱)
    /// ============================================================================
    /// Update()는 타이머 두 개만 굴리고 임계값을 넘을 때만 일한다. 문자열 보간은 실제로 XP가 들어온
    /// 순간(패시브는 10초에 한 번)과 레벨업/저장 시점에만 일어난다.
    ///
    /// ============================================================================
    /// 원칙 1(행동-텍스트 싱크) — 무관하다
    /// ============================================================================
    /// 레벨업해도 대사를 만들지 않는다. 이 컴포넌트는 DialogueIntent를 생성하지도, ChangeState를
    /// 호출하지도 않으므로 SpectacleEventLock에도 참여하지 않는다(StressGauge/HardwareReaction의
    /// "순수 오버레이는 락에 참여하지 않는다"와 같은 기준).
    /// </summary>
    public sealed class CharacterProgressionDirector : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        private StickmanAgent _agent;
        private float _passiveTimer;
        private float _autoSaveTimer;

        // 활쏘기 한 발은 Aim/Release 두 번 발행된다 — 같은 발을 두 번 세지 않도록 마지막으로 보상한
        // 발의 인덱스를 기억한다(TodoPostItWidget의 TryClaimAction과 같은 성격의 중복 방어).
        private int _lastRewardedShotIndex = -1;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에 이 컴포넌트가 남아 있어도
            // XP가 두 배로 들어가지 않게 하는 2차 방어(1차 방어는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
        }

        private void Start()
        {
            if (_agent == null)
            {
                enabled = false;
                return;
            }

            CharacterSaveStore.Load();

            // ★ 2026-09-01 구석 호버 패널 삭제로 이사 온 두 가지 책임(그 패널이 유일한 주인이었다).
            //   (1) 저장된 캐릭터 크기 복원. 여기서 하는 이유는 <b>순서 때문</b>이다 — 바로 위에서
            //       저장 파일을 읽은 그 호출자가 곧바로 부르므로, 옛 구현이 안고 있던 "누가 먼저 도는지
            //       모른다"(매 프레임 재시도 + 2초 유예 마감) 경주가 아예 성립하지 않는다.
            //   (2) Bind. 설정창도 자기 Start에서 부르지만 두 Start의 순서는 보장되지 않는다.
            //       멱등이라 둘 다 불러도 안전하고, 여기 한 줄이 있어야 설정창이 없는 조립에서도 산다.
            CharacterScaleController.Bind(_agent);
            CharacterScaleController.RestoreFromSaveModel();

            Debug.Log($"[성장] 준비 완료 — {CharacterProgressionModel.CharacterName} Lv.{CharacterProgressionModel.Level} " +
                $"({CharacterProgressionModel.CurrentXp:F0}/{CharacterProgressionModel.XpToNextLevel(_config):F0} XP). " +
                $"저장 파일={(CharacterSaveStore.LoadedFromFile ? "불러옴" : "없음 — 새 캐릭터로 시작")} " +
                $"({CharacterSaveStore.FilePath}). " +
                $"패시브 {(_config != null ? _config.progressionPassiveXpPerMinute : 0f):F1}XP/분.");
        }

        private void OnEnable()
        {
            StickmanEventBus.BattleMinigamePhaseChanged += OnBattlePhaseChanged;
            StickmanEventBus.ArcheryShotChanged += OnArcheryShotChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.BattleMinigamePhaseChanged -= OnBattlePhaseChanged;
            StickmanEventBus.ArcheryShotChanged -= OnArcheryShotChanged;
        }

        private void OnApplicationQuit()
        {
            // 종료 직전 마지막 저장 — 주기 저장만 있으면 최대 1분치가 날아간다.
            // 기록(Core/CharacterStatsModel.cs)과 사용자가 옮긴 톱니 위치(Core/UiLayoutModel.cs)도 같은
            // 파일에 들어가므로 함께 본다 — 주기/종료 저장 경로는 이 컴포넌트 하나로 유지한다
            // (두 컴포넌트가 같은 파일을 번갈아 덮어쓰지 않게).
            if (IsAnythingDirty()) CharacterSaveStore.Save();
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측

            // ★ 배율 적용 유예(랙돌/스펙터클 중)를 푸는 <b>상시 구동자</b>. 설정창도 부르지만 그쪽
            //   Update는 `if (!_open) return;`으로 시작한다 — 창을 닫으면 유예가 영영 안 풀려서
            //   "랙돌 중에 크기를 바꾸고 창을 닫으면 그 크기가 사라지는" 버그가 된다.
            //   대기 값이 없으면 즉시 반환하는 0비용 호출이다(24시간 상주 앱: 할당 0).
            CharacterScaleController.Tick();

            float passiveInterval = _config != null ? Mathf.Max(1f, _config.progressionPassiveTickSeconds) : 10f;
            _passiveTimer += Time.unscaledDeltaTime;
            if (_passiveTimer >= passiveInterval)
            {
                _passiveTimer -= passiveInterval;
                float perMinute = _config != null ? _config.progressionPassiveXpPerMinute : 0f;
                if (perMinute > 0f) Grant(perMinute * (passiveInterval / 60f), null);
            }

            float saveInterval = _config != null ? Mathf.Max(5f, _config.progressionAutoSaveIntervalSeconds) : 60f;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= saveInterval)
            {
                _autoSaveTimer -= saveInterval;
                if (IsAnythingDirty()) CharacterSaveStore.Save();
            }
        }

        /// <summary>같은 파일에 실리는 모델 중 하나라도 바뀌었는가 — 안 바뀌었으면 디스크를 두드리지
        /// 않는다(하루 종일 켜져 있는 앱이다).</summary>
        private static bool IsAnythingDirty()
            => CharacterProgressionModel.IsDirty || CharacterStatsModel.IsDirty || UiLayoutModel.IsDirty
               || TodoListModel.IsDirty    // v4 — 사용자가 적은 할일은 반드시 남아야 한다.
               || CharacterAppearanceModel.IsDirty    // v7 — 잉크색(우클릭 메뉴/단축키 경로는 즉시 저장을 부르지 않는다).
               || AppSettingsModel.IsDirty;           // v8 — 설정창(슬라이더는 드래그 중 즉시 저장을 부르지 않는다).

        // ==================== 보너스 훅(전부 읽기 전용 구독) ====================

        private void OnBattlePhaseChanged(BattleMinigamePhase phase)
        {
            if (phase != BattleMinigamePhase.Success) return;
            Grant(_config != null ? _config.progressionBattleWinXp : 0f, "격파 성공");
        }

        private void OnArcheryShotChanged(ArcheryShotEvent shot)
        {
            if (shot.Result != ArcheryShotResult.Bullseye) return;
            if (shot.Phase != ArcheryShotPhase.Release) return;   // Aim/Release 중 한 번만.
            if (shot.ShotIndex == _lastRewardedShotIndex) return; // 같은 발 재발행 방어.
            _lastRewardedShotIndex = shot.ShotIndex;
            Grant(_config != null ? _config.progressionBullseyeXp : 0f, "활쏘기 정중앙 명중");
        }

        /// <summary>XP 적립의 단일 경로 — 레벨업 감지/즉시 저장/로그가 전부 여기 한 곳에만 있다.</summary>
        private void Grant(float amount, string bonusLabel)
        {
            if (amount <= 0f) return;

            int levelsGained = CharacterProgressionModel.AddXp(amount, _config);

            if (bonusLabel != null)
            {
                Debug.Log($"[성장] 보너스 +{amount:F0} XP ({bonusLabel}) — " +
                    $"Lv.{CharacterProgressionModel.Level} " +
                    $"{CharacterProgressionModel.CurrentXp:F0}/{CharacterProgressionModel.XpToNextLevel(_config):F0}.");
            }

            if (levelsGained <= 0) return;

            // 레벨업은 저장 시점이다(리더 지시). 이때 새 장비가 열렸는지도 함께 알린다.
            Debug.Log($"[성장] ★ 레벨업! Lv.{CharacterProgressionModel.Level - levelsGained} -> " +
                $"Lv.{CharacterProgressionModel.Level}. {DescribeNewUnlocks(levelsGained)}");
            StickmanEventBus.RaiseCharacterEquipmentChanged(); // 잠금 표시가 바뀌었다 — 정보창 갱신용.
            CharacterSaveStore.Save();
        }

        /// <summary>이번 레벨업으로 새로 열린 <b>아이템</b>을 사람이 읽는 문장으로. 없으면 그렇게 말한다.
        /// 2026-08-30 32종 확장 전에는 카테고리 4개만 훑었는데, 이제 해제는 아이템 단위라
        /// (카테고리는 처음부터 열려 있다) 카탈로그 전체를 훑는다. 한 번에 여러 개가 열리는 경우
        /// (오래 꺼뒀다 켜서 여러 레벨이 한꺼번에 오를 때)를 위해 개수도 함께 알린다.
        /// 레벨업은 몇 시간에 한 번 있는 사건이라 이 경로의 문자열 할당은 상시 비용이 아니다.</summary>
        private string DescribeNewUnlocks(int levelsGained)
        {
            int before = CharacterProgressionModel.Level - levelsGained;
            string firstName = null;
            int count = 0;

            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (!entry.RequiredLevel.HasValue) continue;   // 행동은 잠기지 않는다.

                int need = entry.RequiredLevel.Value;
                if (need <= before || need > CharacterProgressionModel.Level) continue;

                count++;
                if (firstName == null) firstName = entry.DisplayName;
            }

            if (count <= 0) return "새로 열린 장비는 없습니다.";

            string more = count > 1 ? $" 외 {count - 1}종" : string.Empty;
            return $"새 장비 해제: [{firstName}]{more} — 정보창({ShortcutLabel.Chord("I")} 또는 우상단 톱니)에서 " +
                   "착용할 수 있습니다.";
        }

        /// <summary>
        /// ★ 육안 검증 전용 진입점(리더 지시: "레벨을 테스트용으로 임시로 올려서 확인해라").
        /// 정상 게임플레이 경로에서는 호출되지 않는다 — 정보창/단축키/우클릭 메뉴 어디에도 이 메서드로
        /// 가는 길이 없고, 아래 <c>StickConfig.verboseDiagnosticsLogging</c>이 켜져 있을 때만 동작한다.
        /// (검증값을 원복하지 않아 사고가 난 전례가 이 프로젝트에 2번 있어, 아예 "일시적으로만 켜지는"
        ///  형태로 만들어 원복 대상 자체를 없앴다 — 진단 로그를 끄면 이 경로도 함께 닫힌다.)
        /// </summary>
        public void GrantDebugXpForVisualCheck(float amount)
        {
            if (_config == null || !_config.verboseDiagnosticsLogging)
            {
                Debug.LogWarning($"[성장] 검증용 XP 지급은 진단 로그({ShortcutLabel.Chord("D")})가 " +
                    "켜져 있을 때만 동작합니다.");
                return;
            }
            Grant(amount, "검증용 임시 지급");
        }
    }
}
