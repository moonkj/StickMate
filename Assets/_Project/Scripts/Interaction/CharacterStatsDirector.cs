using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 정보창 하단 스탯의 "언제 세는가"를 담당하는 유일한 주체 — 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 기존 판정 로직을 <b>한 줄도</b> 건드리지 않는다
    /// ============================================================================
    /// 리더 지시: "기존 판정 로직을 건드리지 말고 읽기 전용 이벤트 구독으로 카운트를 누적해라
    /// (직전 라운드의 XP 보너스 훅과 같은 패턴)". 그래서 이 파일은 BattleMinigameDirector /
    /// ArcheryState를 <b>참조조차 하지 않는다</b>(grep으로 검증 가능) —
    /// 전부 <see cref="StickmanEventBus"/> 구독뿐이다. Interaction/CharacterProgressionDirector.cs와
    /// 같은 이벤트를 보지만 하는 일이 다르다(저쪽은 XP 적립, 이쪽은 횟수 기록).
    ///
    /// ============================================================================
    /// 왜 CharacterProgressionDirector에 합치지 않았는가
    /// ============================================================================
    /// 저쪽의 책임은 "성장 곡선"이고 이쪽은 "기록"이다. 특히 활쏘기는 <b>분모가 다르다</b> —
    /// 성장은 정중앙(Bullseye)만 보상하지만 명중률은 <b>모든 발</b>을 세야 한다. 한 핸들러에 두 규칙을
    /// 섞으면 나중에 한쪽 조건을 고칠 때 다른 쪽이 조용히 함께 바뀐다.
    ///
    /// ============================================================================
    /// 매 프레임 할당 금지 (24시간 상주 앱)
    /// ============================================================================
    /// Update()는 float 하나를 더하고 <see cref="FlushIntervalSeconds"/>마다 한 번 모델에 반영한다.
    /// 문자열도 GC 할당도 없다. 저장은 하지 않는다 — 디스크 쓰기는 CharacterProgressionDirector의
    /// 주기 저장/종료 저장 <b>한 경로</b>가 전담한다(두 컴포넌트가 번갈아 같은 파일을 쓰지 않게).
    ///
    /// ============================================================================
    /// 원칙 1(행동-텍스트 싱크) — 무관하다
    /// ============================================================================
    /// 대사를 만들지 않고 상태 전이도 일으키지 않는다. 이미 확정된 사실을 세기만 한다.
    /// </summary>
    public sealed class CharacterStatsDirector : MonoBehaviour
    {
        /// <summary>누적 시간을 모델에 반영하는 주기. 매 프레임 반영하면 IsDirty가 항상 켜져 있어
        /// 주기 저장이 의미를 잃는다(그래도 60초 주기 저장보다는 촘촘해야 종료 시 손실이 없다).</summary>
        private const float FlushIntervalSeconds = 10f;

        private StickmanAgent _agent;
        private float _pendingSeconds;

        // 활쏘기 한 발은 Aim/Release 두 번 발행된다 — 같은 발을 두 번 세지 않도록 마지막으로 기록한
        // 발의 인덱스를 기억한다(CharacterProgressionDirector와 같은 중복 방어). 인덱스는 세션마다
        // 0부터 다시 시작하므로 새 세션이 열릴 때 반드시 초기화한다 — 그러지 않으면 "1발만 쏘고
        // 중단된 세션" 다음 세션의 0번 발이 통째로 누락된다(명중률의 분모가 조용히 틀어진다).
        private int _lastCountedShotIndex = -1;

        // 넘어짐 진입 감지용 — 전역 이벤트가 아니라 내 상태머신을 직접 본다(TickRagdollCounter 문서).
        private StickmanStateId _previousStateId = StickmanStateId.Idle;
        private bool _hasPreviousState;
        private bool _firstRunStamped;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에 이 컴포넌트가 남아 있어도
            // 기록이 두 배로 들어가지 않게 하는 2차 방어(1차는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            if (_agent == null) enabled = false;
        }

        private void Start()
        {
            // 값 요약은 첫 Update(EnsureFirstRunStamped)에서 찍는다 — 여기서는 저장 파일 로드 전일 수
            // 있어 0으로 보일 수 있다(Start 실행 순서 비보장).
            Debug.Log("[기록] 준비 완료 — 격파/활쏘기/넘어짐/함께한 시간을 읽기 전용으로 집계합니다.");
        }

        private void OnEnable()
        {
            StickmanEventBus.BattleMinigamePhaseChanged += OnBattlePhaseChanged;
            StickmanEventBus.ArcheryShotChanged += OnArcheryShotChanged;
            StickmanEventBus.ArcheryOverlayChanged += OnArcheryOverlayChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.BattleMinigamePhaseChanged -= OnBattlePhaseChanged;
            StickmanEventBus.ArcheryShotChanged -= OnArcheryShotChanged;
            StickmanEventBus.ArcheryOverlayChanged -= OnArcheryOverlayChanged;
            Flush(); // 씬 종료/컴포넌트 비활성에서 마지막 조각을 잃지 않게.
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            EnsureFirstRunStamped();
            TickRagdollCounter();

            // Time.unscaledDeltaTime: "함께한 시간"은 게임 시간이 아니라 사람의 시간이다.
            _pendingSeconds += Time.unscaledDeltaTime;
            if (_pendingSeconds < FlushIntervalSeconds) return;
            Flush();
        }

        /// <summary>
        /// "근속"의 기준점을 첫 <b>Update</b>에서 찍는다. Start()에서 찍으면 안 된다 —
        /// 저장 파일 로드는 Interaction/CharacterProgressionDirector.Start()가 하는데 두 Start()의
        /// <b>실행 순서가 보장되지 않아</b>, 이쪽이 먼저 돌면 방금 찍은 값을 곧이어 로드가 0으로
        /// 덮어쓴다(실측: 저장 파일에 firstRunUnixSeconds가 계속 0으로 남아 근속이 매 실행 1일차로
        /// 초기화됐다). Update는 모든 Start 이후이므로 순서에 의존하지 않는다.
        /// </summary>
        private void EnsureFirstRunStamped()
        {
            if (_firstRunStamped) return;
            _firstRunStamped = true;
            CharacterStatsModel.EnsureFirstRunInitialized();
            Debug.Log($"[기록] 근속 {CharacterStatsModel.DaysTogether}일차, " +
                $"함께한 시간 {CharacterStatsModel.FormatCompanionTime()}, " +
                $"격파 {CharacterStatsModel.BattleWins}회, " +
                $"활쏘기 {CharacterStatsModel.ArcheryBullseyes}/{CharacterStatsModel.ArcheryShots}발, " +
                $"넘어짐 {CharacterStatsModel.RagdollFalls}회.");
        }

        private void Flush()
        {
            if (_pendingSeconds <= 0f) return;
            CharacterStatsModel.AddCompanionSeconds(_pendingSeconds);
            _pendingSeconds = 0f;
        }

        private void OnBattlePhaseChanged(BattleMinigamePhase phase)
        {
            if (phase != BattleMinigamePhase.Success) return;
            CharacterStatsModel.AddBattleWin();
            Debug.Log($"[기록] 격파 성공 누적 {CharacterStatsModel.BattleWins}회.");
        }

        /// <summary>
        /// "넘어진 횟수" — <b>내 상태머신</b>이 Ragdoll로 들어간 순간만 센다.
        ///
        /// ★ StickmanEventBus.StateTransitioned를 구독하지 <b>않는</b> 이유:
        /// 그 이벤트에는 화자 정보가 없다(DialogueIntent와 달리 OriginMachine을 싣지 않는다). 지금은
        /// 씬에 상태머신이 하나뿐이라 우연히 맞겠지만, 같은 StickmanStateMachine 클래스를 굴리는 두
        /// 번째 개체가 생기는 순간 남의 전이가 내 기록에 섞인다. 그래서 내 에이전트의 상태 ID를 직접
        /// 읽어 진입 순간만 센다 — 매 프레임 enum 비교 하나라 24시간 상주 앱에서도 비용이 없다.
        /// </summary>
        private void TickRagdollCounter()
        {
            var machine = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            if (machine == null) return;

            StickmanStateId now = machine.CurrentStateId;
            bool wasRagdoll = _previousStateId == StickmanStateId.Ragdoll;
            bool hadPrevious = _hasPreviousState;
            _previousStateId = now;
            _hasPreviousState = true;
            if (now != StickmanStateId.Ragdoll || wasRagdoll) return;
            if (!hadPrevious) return; // 앱 시작 첫 프레임의 상태는 "넘어진 사건"이 아니다.

            CharacterStatsModel.AddRagdollFall();
        }

        private void OnArcheryOverlayChanged(ArcheryOverlayEvent overlay)
        {
            if (overlay.Phase == SpectacleOverlayPhase.Started) _lastCountedShotIndex = -1;
        }

        /// <summary>명중률의 분모는 <b>쏜 발 전부</b>다 — Release 시점에 한 번만 센다(Aim은 아직
        /// 쏘지 않은 상태이므로 세면 분모가 두 배가 된다).</summary>
        private void OnArcheryShotChanged(ArcheryShotEvent shot)
        {
            if (shot.Phase != ArcheryShotPhase.Release) return;
            if (shot.ShotIndex == _lastCountedShotIndex) return;
            _lastCountedShotIndex = shot.ShotIndex;

            CharacterStatsModel.AddArcheryShot(shot.Result == ArcheryShotResult.Bullseye);
            Debug.Log($"[기록] 활쏘기 {CharacterStatsModel.ArcheryBullseyes}/{CharacterStatsModel.ArcheryShots}발 " +
                $"(이번 발: {shot.Result}).");
        }
    }
}
