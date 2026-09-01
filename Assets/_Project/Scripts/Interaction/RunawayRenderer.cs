using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 가출 시각 레이어 — docs/UX_FLOW.md 20절 "'나 안 해!' 이후 흐름" / "유저가 찾는 방법" /
    /// "간식으로 달래기"를 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Interaction/RunawayDirector.cs(확정 임계값 트리거 + 은신처 좌표 계산 + 탐색/간식/수동소환/
    /// 긴급소환 신호 배선)와 States/RunawayState.cs(5페이즈 진행 + 렌더러 토글 + 자동복귀 타임아웃)는
    /// Phase 5에 완성돼 있었다. 그런데 <b>StickmanEventBus.RunawayLifecycleChanged와
    /// RunawayHintPulseRequested를 구독하는 코드가 프로젝트 전체에 0건이었고</b> Director 자신도 씬
    /// 어디에도 배치돼 있지 않았다. 즉 캐릭터가 사라지는 것 말고는 아무 신호도 화면에 나오지 않았고,
    /// 애초에 발동조차 하지 않았다.
    ///
    /// ============================================================================
    /// 페이즈별로 무엇을 그리는가
    /// ============================================================================
    /// Fleeing      -> 캐릭터 뒤로 속도선 3줄 + 발밑 먼지("화면 가장자리로 뛰어가 퇴장", 20절).
    /// Hidden       -> <b>아무것도 상시로 그리지 않는다.</b> 숨은 자리를 상시 표시하면 20절의 찾기
    ///                 미니게임 자체가 성립하지 않는다. 대신 RunawayHintPulseRequested가 올 때마다
    ///                 그 자리에서 <b>아주 옅은 파문 한 번</b>만 퍼진다 — 20절 "너무 쉬우면 재미없고
    ///                 너무 어려우면 좌절"의 균형점이 바로 이 "간헐적·저대비" 단서다.
    /// Found        -> 발견 지점에서 방사선이 터지고("놀란 표정으로 드러남"), 캐릭터 옆에
    ///                 <b>[간식 주기] 과자</b>가 나타난다(20절 "찾은 자리에 앱 소유 UI로 [간식 주기]
    ///                 버튼 노출", 14절 사과 먹이기와 같은 패턴).
    /// Reconciled   -> 화해 반짝임이 피어오르고 과자가 사라진다.
    /// SelfReturned -> 멋쩍은 먼지 한 줌이 일고 과자가 사라진다(못 찾고 방치했거나 수동 소환/긴급 소환).
    ///
    /// ============================================================================
    /// 과자를 클릭할 수 있게 만드는 방식 — 새 입력 경로를 만들지 않는다
    /// ============================================================================
    /// 과자에는 <b>isTrigger=true BoxCollider2D</b> 하나가 붙고, 그것을
    /// StickmanClickHitbox.RegisterExtraCollider()로 등록한다 — Interaction/BattleMinigameRenderer.cs가
    /// 소환 판자/게이지를 클릭 가능하게 만든 것과 <b>완전히 같은 검증된 경로</b>다(전역 폴링 히트테스트 +
    /// UniWindowController의 Raycast 히트테스트가 둘 다 같은 Collider2D를 본다). uGUI 버튼을 쓰지 않는
    /// 이유는 이 앱의 창이 평소 클릭관통 + 비활성 앱이라 EventSystem 경로의 도달이 보장되지 않기 때문이다
    /// (Interaction/AppControlDirector.cs가 메뉴 행 클릭을 uGUI가 아닌 전역 폴링으로 판정하는 것과 같은 이유).
    ///
    /// MouseDown은 캐릭터 클릭과 과자 클릭을 구분하지 않으므로, 이 렌더러는 그 순간 커서가 <b>과자
    /// 콜라이더 안</b>인지 직접 확인한 뒤에만 RunawayDirector.OfferSnack()을 부른다. 같은 클릭에
    /// RunawayDirector.OnHitboxMouseDown도 반응하지만 그쪽은 "발견" 신호이고 Found 페이즈에서는
    /// States/RunawayState.cs가 그 신호를 읽지 않으므로 충돌하지 않는다.
    ///
    /// ============================================================================
    /// 절대 원칙 — 이 클래스가 하지 않는 일
    /// ============================================================================
    /// 실제 창/파일/아이콘을 조회하지도 변경하지도 않는다(원칙 3). 이 클래스가 아는 좌표는 State가
    /// 넘겨준 은신처 스냅샷 하나뿐이며, 그 좌표는 애초에 화면 네 모서리를 계산한 값이다.
    /// SpectacleEventLock에는 <b>Director가</b> 이미 참여하고 있고(20절/24절/25절-20이 명시적으로 요구),
    /// 이 렌더러 자신은 ChangeState를 호출하지 않으므로 락과 무관하다.
    /// </summary>
    public sealed class RunawayRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const float SpeedLineLifeSeconds = 0.34f;
        private const float SpeedLineSpawnInterval = 0.09f;
        private const float DustLifeSeconds = 0.55f;
        private const float DustSpawnInterval = 0.14f;
        private const int TransientMaxAlive = 14;   // 24시간 상주 앱 — 무한 증식 상한.

        // ============================================================================
        // ★ 2026-08-29 리더 지시 — 캐릭터 기준 치수는 전부 **전신 높이 대비 비율**이다.
        // ============================================================================
        // 종전에는 아래 값이 전부 절대 월드유닛이었다. StickConfig.characterScale이 0.5가 되면 캐릭터만
        // 절반이 되고 과자(반지름 0.30, 오프셋 y+1.02)는 그대로라, 과자가 **정수리(배율 0.5에서 1.137)
        // 근처의 허공**에 캐릭터 몸통만 한 크기로 떠 있게 된다. 먼지/속도선도 마찬가지로 몸 길이 대비
        // 두 배로 커진다.
        //
        // 기준 치수의 유일한 조회 경로는 Core/StickmanMetrics.cs다(상수 복사가 아니라 계층 실측).
        // 분자는 검증을 마친 종전 값 그 자체, 분모는 배율 1.0 기준 신장이므로 배율 1.0에서는 지금까지와
        // 완전히 같은 그림/움직임이 나온다.
        //
        // ★ 시간 상수(수명/간격)와 각진동수(SnackBobSpeed)는 길이 차원이 아니라 비율화 대상이 아니다.
        //   "작아졌다고 더 빨리 사라질" 이유가 없다 — 대신 **길이/초 차원의 속도는 전부 비율화한다**
        //   (리더 지시 3: 배치만 비율화하고 속도를 절대로 남기면 알갱이가 몸 쪽으로 두 배 깊이 파고든다).
        private const float HintRippleSeconds = 1.25f;   // 20절 "아주 미세한" 단서라 느리고 옅게.
        // 힌트 파문은 은신처(= 숨은 캐릭터가 있는 자리)를 가리키는 단서다. 캐릭터가 절반이 되었는데
        // 파문만 원래 크기로 남으면 "미세한 단서"가 아니라 화면에서 가장 큰 도형이 되어 20절의 의도가
        // 뒤집힌다. 그래서 발견 폭발(아래 FoundBurst)과 같은 기준으로 함께 비율화한다.
        private const float HintRippleStartRadiusRatio = 0.18f / StickConfig.BaselineCharacterTotalHeight;
        private const float HintRippleEndRadiusRatio = 1.05f / StickConfig.BaselineCharacterTotalHeight;
        private const float HintRippleMaxAlpha = 0.34f;  // 대비를 일부러 낮춘다(너무 쉬우면 재미없다).

        private const float FoundBurstSeconds = 0.75f;
        private const int FoundBurstRays = 8;
        private const float FoundBurstStartRadiusRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight; // 방사선 시작 반경.
        private const float FoundBurstSpeedRatio = 0.42f / StickConfig.BaselineCharacterTotalHeight;       // 방사선 속도(유닛/초).

        private const float SnackOffsetXRatio = 0.92f / StickConfig.BaselineCharacterTotalHeight;   // 캐릭터 옆(발견된 자리)에 놓이는 과자.
        private const float SnackOffsetYRatio = 1.02f / StickConfig.BaselineCharacterTotalHeight;
        private const float SnackRadiusRatio = 0.30f / StickConfig.BaselineCharacterTotalHeight;
        private const float SnackChipRadiusRatio = 0.035f / StickConfig.BaselineCharacterTotalHeight;
        private const float SnackBobAmplitudeRatio = 0.06f / StickConfig.BaselineCharacterTotalHeight;
        private const float SnackBobSpeed = 2.6f;   // 각진동수(rad/초) — 길이 차원이 아니라 절대값이 맞다.

        // 도주 속도선 — 캐릭터 뒤로 흐르는 선 3줄.
        private const float SpeedLineBackXRatio = 0.55f / StickConfig.BaselineCharacterTotalHeight;  // 몸 뒤로 물러난 생성 x.
        private const float SpeedLineBaseYRatio = 0.55f / StickConfig.BaselineCharacterTotalHeight;  // 가장 아래 줄의 높이.
        private const float SpeedLineStepYRatio = 0.42f / StickConfig.BaselineCharacterTotalHeight;  // 줄 간격.
        private const float SpeedLineSpeedRatio = 0.9f / StickConfig.BaselineCharacterTotalHeight;   // 흐르는 속도(유닛/초).
        private const float StreakLengthRatio = 0.34f / StickConfig.BaselineCharacterTotalHeight;    // 선 한 줄의 길이.

        // 발밑 먼지.
        private const float DustBackXRatio = 0.28f / StickConfig.BaselineCharacterTotalHeight;
        private const float DustYRatio = 0.06f / StickConfig.BaselineCharacterTotalHeight;
        private const float DustStartRadiusRatio = 0.06f / StickConfig.BaselineCharacterTotalHeight;
        private const float DustEndRadiusRatio = 0.36f / StickConfig.BaselineCharacterTotalHeight;

        // 화해 반짝임(머리 위쪽) / 귀가 먼지(발밑).
        private const float ReconcileSpreadXRatio = 0.45f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReconcileBaseYRatio = 1.1f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReconcileSpreadYRatio = 0.5f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReconcileStartRadiusRatio = 0.04f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReconcileEndRadiusRatio = 0.16f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReturnDustSpreadXRatio = 0.35f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReturnDustYRatio = 0.05f / StickConfig.BaselineCharacterTotalHeight;
        private const float ReturnDustEndRadiusRatio = 0.42f / StickConfig.BaselineCharacterTotalHeight;

        private const float StrokeWidthRatio = 0.052f / StickConfig.BaselineCharacterTotalHeight;
        private const int SortingEffect = 8;        // 캐릭터 획(0~5) 위, 그라피티(9) 아래.

        private static readonly Color FleeColor = new Color(0.58f, 0.60f, 0.66f, 1f);
        private static readonly Color HintColor = new Color(0.62f, 0.66f, 0.78f, 1f);
        private static readonly Color FoundColor = new Color(0.96f, 0.80f, 0.28f, 1f);
        private static readonly Color SnackColor = new Color(0.78f, 0.55f, 0.28f, 1f);
        private static readonly Color ReconcileColor = new Color(0.92f, 0.48f, 0.55f, 1f);

        private sealed class Transient
        {
            public Transform Root;
            public LineRenderer Line;
            public float Age;
            public float Life;
            public Vector2 Velocity;
            public float StartRadius;
            public float EndRadius;
            public float StartAlpha;
            public bool IsRing;
            public bool WorldAnchored; // true면 생성 위치에 고정(파문/발견 폭발), false면 캐릭터를 따라간다.
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 폴백을 두었을 때 사본 쪽에도 과자와
        /// 속도선이 한 벌 더 그려진다(2026-08-29 격파 미니게임에서 실측 확인된 버그와 같은 함정).
        /// 애초에 배치하지 않는 것이 1차 방어이고 이 가드가 2차다.
        /// </summary>
        private StickmanAgent _agent;

        // ==================== 캐릭터 실측 치수 조회 ====================

        /// <summary>캐릭터 치수의 <b>유일한</b> 조회 경로(Core/StickmanMetrics.cs). 매 프레임 쓰이는
        /// 값이라 컴포넌트를 한 번만 찾아 캐시한다. 못 찾으면 null을 캐시하고 비율 폴백으로 떨어진다.</summary>
        private StickmanMetrics _metrics;
        private bool _metricsResolved;

        private StickmanMetrics Metrics
        {
            get
            {
                if (_metrics != null) return _metrics;
                if (_metricsResolved) return null;
                _metricsResolved = true;
                _metrics = _agent != null ? _agent.Metrics : StickmanMetrics.Find(this);
                return _metrics;
            }
        }

        /// <summary>이 캐릭터의 전신 높이(월드 유닛) — 위 모든 비율의 유일한 기준값.</summary>
        private float Height
        {
            get
            {
                StickmanMetrics m = Metrics;
                return m != null ? m.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
            }
        }

        // ==================== 테스트/진단용 배치 관찰 창구 ====================
        // (Tests/PlayMode/RendererScaleRatioTests.cs가 배율 1.0/0.5 양쪽에서 단언한다.)

        /// <summary>과자가 놓이는 로컬 오프셋(발바닥 기준, x는 바라보는 방향 부호를 곱하기 전).</summary>
        public float SnackOffsetLocalX => Height * SnackOffsetXRatio;

        /// <summary>과자가 놓이는 로컬 Y(발바닥 기준).</summary>
        public float SnackOffsetLocalY => Height * SnackOffsetYRatio;

        /// <summary>과자 원의 반지름(월드 유닛). 클릭 콜라이더도 이 값에서 유도된다.</summary>
        public float SnackRadius => Height * SnackRadiusRatio;

        /// <summary>획 두께(월드 유닛).</summary>
        public float StrokeWidth => Height * StrokeWidthRatio;

        private float SnackBobAmplitude => Height * SnackBobAmplitudeRatio;
        private StickmanClickHitbox _hitbox;
        private RunawayDirector _director;
        private Material _lineMaterial;

        private GameObject _container;
        private readonly List<Transient> _transients = new List<Transient>(TransientMaxAlive);

        private RunawayLifecyclePhase _phase;
        private bool _active;
        private float _phaseTimer;
        private float _speedLineTimer;
        private float _dustTimer;

        private GameObject _snackRoot;
        private BoxCollider2D _snackCollider;
        private readonly List<LineRenderer> _snackLines = new List<LineRenderer>(5);

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 가출 연출이 진행 중인지(어느 페이즈든).</summary>
        public bool IsActive => _active;

        /// <summary>지금 표현 중인 페이즈(진행 중이 아니면 null).</summary>
        public RunawayLifecyclePhase? VisiblePhase => _active ? _phase : (RunawayLifecyclePhase?)null;

        /// <summary>지금 [간식 주기] 과자가 화면에 있는지 — Found 페이즈에서만 true여야 한다(20절).</summary>
        public bool IsSnackOffered => _snackRoot != null;

        /// <summary>이 연출이 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>
        /// 이 연출이 만든 콜라이더 수. <b>과자가 떠 있을 때만 정확히 1개</b>이고 그 외에는 0이다 —
        /// 20절이 요구한 유일한 클릭 대상이 과자뿐임을 절대 조건으로 고정한다(속도선/먼지/파문/발견
        /// 폭발은 전부 관전 전용이라 클릭관통을 유지해야 한다).
        /// </summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        /// <summary>마지막으로 통지받은 은신처 월드 좌표(Hidden/Found에서만 의미 있음). 진단/테스트용.</summary>
        public Vector2 LastHideSpotWorld { get; private set; }

        /// <summary>지금까지 그린 힌트 파문 횟수 — 20절의 "은은한 단서"가 실제로 나가고 있는지 확인용.</summary>
        public int HintPulseCount { get; private set; }

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _hitbox = GetComponent<StickmanClickHitbox>();
            _director = GetComponent<RunawayDirector>();
        }

        private void OnEnable()
        {
            StickmanEventBus.RunawayLifecycleChanged += OnLifecycleChanged;
            StickmanEventBus.RunawayHintPulseRequested += OnHintPulse;
            if (_hitbox != null) _hitbox.MouseDown += OnHitboxMouseDown;
        }

        private void OnDisable()
        {
            StickmanEventBus.RunawayLifecycleChanged -= OnLifecycleChanged;
            StickmanEventBus.RunawayHintPulseRequested -= OnHintPulse;
            if (_hitbox != null) _hitbox.MouseDown -= OnHitboxMouseDown;
            // 이 컴포넌트가 꺼질 때 과자/파문이 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnLifecycleChanged(RunawayLifecycleEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            EnsureContainer();
            if (_container == null) return;

            _phase = evt.Phase;
            _active = true;
            _phaseTimer = 0f;
            _speedLineTimer = 0f;
            _dustTimer = 0f;

            if (evt.Phase == RunawayLifecyclePhase.Hidden || evt.Phase == RunawayLifecyclePhase.Found)
            {
                LastHideSpotWorld = OsScreenToWorld(evt.HideSpotOsScreen);
            }

            switch (evt.Phase)
            {
                case RunawayLifecyclePhase.Fleeing:
                    HideSnack();
                    Debug.Log("[가출] '나 안 해!' — 뛰쳐나가는 연출 시작(속도선 + 발밑 먼지). " +
                        "이 단계는 아직 캐릭터가 보이며, 클릭해도 '발견'으로 치지 않는다(States/RunawayState.cs).");
                    break;

                case RunawayLifecyclePhase.Hidden:
                    HideSnack();
                    Debug.Log($"[가출] 은신 완료 — 은신처 월드 {LastHideSpotWorld}, OS화면 {evt.HideSpotOsScreen}. " +
                        "화면에는 아무 상시 표시도 하지 않고(그러면 찾기 미니게임이 성립하지 않는다) " +
                        $"{HintRippleSeconds:F2}초짜리 옅은 파문만 주기적으로 퍼뜨린다. " +
                        "★ 캐릭터의 Collider2D는 Kinematic으로 살아 있으므로 그 자리를 클릭하면 발견된다(20절).");
                    break;

                case RunawayLifecyclePhase.Found:
                    SpawnFoundBurst(LastHideSpotWorld);
                    ShowSnack();
                    Debug.Log($"[가출] 발견됨 — 발견 지점 월드 {LastHideSpotWorld}에서 방사선 {FoundBurstRays}줄, " +
                        $"[간식 주기] 과자 생성(클릭 가능 콜라이더 {ActiveColliderCount}개 — 과자 1개만). " +
                        "과자를 클릭하면 RunawayDirector.OfferSnack()으로 화해가 확정된다.");
                    break;

                case RunawayLifecyclePhase.Reconciled:
                    HideSnack();
                    SpawnReconcileSparkles();
                    Debug.Log("[가출] 화해 — 간식을 못 이기는 척 받아먹었다. 반짝임 연출 후 정상 복귀. " +
                        "(스트레스는 '상당량 감소, 완전 리셋은 아님' — 감소 자체는 States/RunawayState.cs 담당.)");
                    break;

                case RunawayLifecyclePhase.SelfReturned:
                    HideSnack();
                    SpawnReturnDust();
                    Debug.Log("[가출] 자진 복귀 — 자동 복귀 타임아웃 / 수동 소환 / 긴급 강제소환 중 하나. " +
                        "어떤 조치도 없이 보장되는 마지노선이다(20절 '무한정 숨어있는 상태는 절대 허용하지 않는다').");
                    break;
            }

            // 종결 페이즈에 도달하면 "진행 중"을 끈다 — 남은 파티클이 다 사라지는 순간 LateUpdate가
            // 컨테이너까지 걷어낸다(빈 GameObject를 24시간 남겨두지 않는다).
            if (evt.Phase == RunawayLifecyclePhase.Reconciled || evt.Phase == RunawayLifecyclePhase.SelfReturned)
            {
                MarkFinished();
            }
        }

        /// <summary>20절 "숨은 위치 근처 화면 가장자리에 아주 미세한 흔들림/작은 소리 이펙트로 은은한 단서".</summary>
        private void OnHintPulse(Vector2 hideSpotOsScreen)
        {
            if (_agent == null) return;
            EnsureContainer();
            if (_container == null) return;

            LastHideSpotWorld = OsScreenToWorld(hideSpotOsScreen);
            HintPulseCount++;

            // 파문 2겹(안쪽이 먼저, 바깥쪽이 조금 늦게) — 한 겹이면 "그냥 원"으로 보이고 두 겹이면
            // "무언가 꿈틀했다"로 읽힌다.
            float hintStart = Height * HintRippleStartRadiusRatio;
            float hintEnd = Height * HintRippleEndRadiusRatio;
            SpawnRing(LastHideSpotWorld, HintColor, HintRippleSeconds, hintStart,
                hintEnd, HintRippleMaxAlpha);
            SpawnRing(LastHideSpotWorld, HintColor, HintRippleSeconds * 0.72f, hintStart * 0.5f,
                hintEnd * 0.55f, HintRippleMaxAlpha * 0.8f);

            Debug.Log($"[가출] 은신처 힌트 파문 #{HintPulseCount} — 월드 {LastHideSpotWorld} " +
                $"(최대 알파 {HintRippleMaxAlpha:F2}로 일부러 옅게, 20절 '아주 미세한' 단서).");
        }

        private void OnHitboxMouseDown()
        {
            if (_snackCollider == null || _director == null) return;
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null) return;
            if (!blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld)) return;
            if (!_snackCollider.OverlapPoint(cursorWorld)) return;

            Debug.Log("[가출] [간식 주기] 과자 클릭 감지 — RunawayDirector.OfferSnack()을 호출합니다.");
            _director.OfferSnack();
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            if (_container == null) return;

            float dt = Time.deltaTime;
            _phaseTimer += dt;

            if (_active && _phase == RunawayLifecyclePhase.Fleeing)
            {
                TickFleeingEffects(dt);
            }

            TickSnack();
            TickTransients(dt);

            // 진행 중인 페이즈도 없고 남은 파티클/과자도 없으면 컨테이너 자체를 걷는다 —
            // 24시간 상주 앱에서 빈 GameObject가 영원히 남아 있지 않게 한다.
            if (!_active && _transients.Count == 0 && _snackRoot == null)
            {
                Teardown();
            }
        }

        private void TickFleeingEffects(float dt)
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null) return;

            Vector3 body = blackboard.Body.position;
            float facing = blackboard.FacingSign != 0f ? Mathf.Sign(blackboard.FacingSign) : 1f;

            _speedLineTimer += dt;
            if (_speedLineTimer >= SpeedLineSpawnInterval && _transients.Count < TransientMaxAlive)
            {
                _speedLineTimer = 0f;
                float backX = Height * SpeedLineBackXRatio;
                float baseY = Height * SpeedLineBaseYRatio;
                float stepY = Height * SpeedLineStepYRatio;
                for (int i = 0; i < 3; i++)
                {
                    float y = baseY + i * stepY;
                    SpawnStreak(new Vector3(body.x - facing * backX, body.y + y, 0f), -facing);
                }
            }

            _dustTimer += dt;
            if (_dustTimer >= DustSpawnInterval && _transients.Count < TransientMaxAlive)
            {
                _dustTimer = 0f;
                SpawnRing(new Vector2(body.x - facing * (Height * DustBackXRatio), body.y + Height * DustYRatio),
                    FleeColor, DustLifeSeconds, Height * DustStartRadiusRatio, Height * DustEndRadiusRatio, 0.8f);
            }
        }

        // ==================== 과자([간식 주기]) ====================

        private void ShowSnack()
        {
            HideSnack();
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (_container == null || blackboard == null || blackboard.Body == null) return;

            _snackRoot = new GameObject("RunawaySnackOffer");
            _snackRoot.transform.SetParent(_container.transform, false);
            _snackRoot.transform.position = SnackWorldPosition();

            // 과자 본체(원) + 초코칩 3개 — 14절 "사과 먹이기"와 같은 톤의 앱 소유 UI.
            float snackRadius = SnackRadius;
            float stroke = StrokeWidth;
            _snackLines.Add(CreateLineOn(_snackRoot.transform, "SnackBody",
                BuildCircle(Vector3.zero, snackRadius, 14), SnackColor, stroke, loop: true));
            // 초코칩 위치는 과자 반지름(0.30) 대비 비율이라 과자와 함께 저절로 따라온다.
            float chipUnit = snackRadius / 0.30f;
            var chipOffsets = new[]
            {
                new Vector3(-0.10f * chipUnit, 0.07f * chipUnit, 0f),
                new Vector3(0.11f * chipUnit, 0.04f * chipUnit, 0f),
                new Vector3(0.01f * chipUnit, -0.11f * chipUnit, 0f),
            };
            float chipRadius = Height * SnackChipRadiusRatio;
            for (int i = 0; i < chipOffsets.Length; i++)
            {
                _snackLines.Add(CreateLineOn(_snackRoot.transform, $"SnackChip{i}",
                    BuildCircle(chipOffsets[i], chipRadius, 6), SnackColor, stroke * 0.8f, loop: true));
            }

            // 클릭 대상. isTrigger인 이유는 AppControlDirector의 메뉴 차단막/BattleMinigameRenderer의
            // 판자 클릭 대상과 동일하다 — 히트테스트에는 잡히지만 물리 충돌은 절대 일으키지 않는다
            // (캐릭터가 과자에 부딪혀 튕기면 안 된다).
            _snackCollider = _snackRoot.AddComponent<BoxCollider2D>();
            _snackCollider.isTrigger = true;
            _snackCollider.size = new Vector2(snackRadius * 2.4f, snackRadius * 2.4f); // 반지름이 비율이라 클릭 영역도 함께 따라온다.
            _hitbox?.RegisterExtraCollider(_snackCollider);
        }

        private void HideSnack()
        {
            if (_snackCollider != null) _hitbox?.UnregisterExtraCollider(_snackCollider);
            _snackCollider = null;
            _snackLines.Clear();
            if (_snackRoot != null)
            {
                Destroy(_snackRoot);
                _snackRoot = null;
            }
        }

        private void TickSnack()
        {
            if (_snackRoot == null) return;
            Vector3 pos = SnackWorldPosition();
            pos.y += Mathf.Sin(Time.time * SnackBobSpeed) * SnackBobAmplitude; // "여기 있어" 하는 작은 부유(진폭은 전신 높이 비율).
            _snackRoot.transform.position = pos;
        }

        private Vector3 SnackWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 body = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;
            float facing = blackboard != null && blackboard.FacingSign != 0f ? Mathf.Sign(blackboard.FacingSign) : 1f;

            Vector3 target = new Vector3(body.x + SnackOffsetLocalX * facing, body.y + SnackOffsetLocalY, 0f);

            // 발견 지점은 화면 네 모서리라 과자가 화면 밖으로 밀려나기 쉽다 — 반드시 뷰포트 안으로
            // 끌어들인다(HardwareReactionRenderer.FollowHead()와 같은 이유이자 같은 관례).
            // 여유가 SnackRadius 배수라 비율화의 혜택을 저절로 받는다 — 절대 유닛이었다면 배율 0.5에서
            // 캐릭터 한 키 가까이를 화면 안쪽으로 끌어당겨 과자만 몸에서 떨어져 나갔을 것이다.
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                float margin = SnackRadius * 2.2f;
                Vector3 camPos = cam.transform.position;
                target.x = Mathf.Clamp(target.x, camPos.x - halfW + margin, camPos.x + halfW - margin);
                target.y = Mathf.Clamp(target.y, camPos.y - halfH + margin, camPos.y + halfH - margin);
            }
            return target;
        }

        // ==================== 일회성 파티클 ====================

        private void SpawnFoundBurst(Vector2 center)
        {
            float startRadius = Height * FoundBurstStartRadiusRatio;
            float burstSpeed = Height * FoundBurstSpeedRatio;
            for (int i = 0; i < FoundBurstRays; i++)
            {
                float angle = i / (float)FoundBurstRays * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                SpawnStreakAt(new Vector3(center.x + dir.x * startRadius, center.y + dir.y * startRadius, 0f),
                    dir * burstSpeed, FoundColor, FoundBurstSeconds, worldAnchored: true);
            }
        }

        private void SpawnReconcileSparkles()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null) return;
            Vector3 body = blackboard.Body.position;
            float spreadX = Height * ReconcileSpreadXRatio;
            float baseY = Height * ReconcileBaseYRatio;
            float spreadY = Height * ReconcileSpreadYRatio;
            for (int i = 0; i < 5; i++)
            {
                SpawnRing(new Vector2(body.x + Random.Range(-spreadX, spreadX), body.y + baseY + Random.Range(0f, spreadY)),
                    ReconcileColor, 1.0f, Height * ReconcileStartRadiusRatio, Height * ReconcileEndRadiusRatio, 0.95f);
            }
        }

        private void SpawnReturnDust()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null) return;
            Vector3 body = blackboard.Body.position;
            float spreadX = Height * ReturnDustSpreadXRatio;
            for (int i = 0; i < 3; i++)
            {
                SpawnRing(new Vector2(body.x + Random.Range(-spreadX, spreadX), body.y + Height * ReturnDustYRatio),
                    FleeColor, DustLifeSeconds * 1.4f, Height * DustStartRadiusRatio, Height * ReturnDustEndRadiusRatio, 0.7f);
            }
        }

        private void SpawnRing(Vector2 worldCenter, Color color, float life, float startRadius, float endRadius, float startAlpha)
        {
            if (_container == null || _transients.Count >= TransientMaxAlive) return;

            var go = new GameObject("RunawayRing");
            go.transform.SetParent(_container.transform, false);
            go.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);

            var lr = ConfigureTransientLine(go, color, StrokeWidth * 0.85f);
            lr.loop = true;
            lr.positionCount = 0;

            _transients.Add(new Transient
            {
                Root = go.transform,
                Line = lr,
                Age = 0f,
                Life = Mathf.Max(0.05f, life),
                StartRadius = startRadius,
                EndRadius = endRadius,
                StartAlpha = startAlpha,
                IsRing = true,
                WorldAnchored = true,
            });
        }

        private void SpawnStreak(Vector3 worldPos, float dirX)
            => SpawnStreakAt(worldPos, new Vector2(dirX * (Height * SpeedLineSpeedRatio), 0f), FleeColor,
                SpeedLineLifeSeconds, worldAnchored: true);

        private void SpawnStreakAt(Vector3 worldPos, Vector2 velocity, Color color, float life, bool worldAnchored)
        {
            if (_container == null || _transients.Count >= TransientMaxAlive) return;

            var go = new GameObject("RunawayStreak");
            go.transform.SetParent(_container.transform, false);
            go.transform.position = worldPos;

            var lr = ConfigureTransientLine(go, color, StrokeWidth);
            lr.loop = false;
            Vector2 unit = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            float streakLength = Height * StreakLengthRatio;
            lr.SetPosition(1, new Vector3(unit.x * streakLength, unit.y * streakLength, 0f));

            _transients.Add(new Transient
            {
                Root = go.transform,
                Line = lr,
                Age = 0f,
                Life = Mathf.Max(0.05f, life),
                Velocity = velocity,
                StartAlpha = 1f,
                IsRing = false,
                WorldAnchored = worldAnchored,
            });
        }

        private void TickTransients(float dt)
        {
            for (int i = _transients.Count - 1; i >= 0; i--)
            {
                Transient t = _transients[i];
                if (t?.Line == null) { _transients.RemoveAt(i); continue; }

                t.Age += dt;
                float p = Mathf.Clamp01(t.Age / t.Life);
                if (p >= 1f)
                {
                    if (t.Root != null) Destroy(t.Root.gameObject);
                    _transients.RemoveAt(i);
                    continue;
                }

                if (t.IsRing)
                {
                    float radius = Mathf.Lerp(t.StartRadius, t.EndRadius, p);
                    Vector3[] circle = BuildCircle(Vector3.zero, radius, 14);
                    t.Line.positionCount = circle.Length;
                    t.Line.SetPositions(circle);
                }
                else
                {
                    t.Root.position += (Vector3)(t.Velocity * dt);
                }

                Color c = t.Line.startColor;
                c.a = t.StartAlpha * (1f - p);
                t.Line.startColor = c;
                t.Line.endColor = c;
            }
        }

        // ==================== 종료 ====================

        /// <summary>페이즈가 끝났음을 알리는 전이가 오면(Reconciled/SelfReturned) 잔여 파티클이 다 사라진
        /// 뒤 컨테이너를 자동으로 걷는다. 그 판정은 LateUpdate가 한다.</summary>
        private void MarkFinished() => _active = false;

        private void EnsureContainer()
        {
            if (_container != null) return;
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[가출] 연출을 그리지 못했습니다 — 캐릭터 배선이 없습니다.");
                return;
            }
            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("RunawayOverlay");
            _container.transform.SetParent(null, false);
            _container.transform.position = Vector3.zero;
        }

        private void Teardown()
        {
            HideSnack();
            _transients.Clear();
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _active = false;
        }

        // ==================== 좌표/도형 유틸 ====================

        /// <summary>OS 화면 좌표 -> 월드. cameraDepth는 임의값을 넣으면 안 되고 반드시 왕복에 쓸 값을
        /// 그대로 재사용해야 한다(Platform/ScreenCoordinateConverter.cs "왕복 정밀도" 참고).</summary>
        private Vector2 OsScreenToWorld(Vector2 osScreen)
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.MainCamera == null || blackboard.Body == null) return osScreen;

            ScreenCoordinateConverter.WorldToOsScreen(blackboard.MainCamera, blackboard.Body.position,
                blackboard.Config, out float depth);
            Vector3 world = ScreenCoordinateConverter.OsScreenToWorld(blackboard.MainCamera, osScreen, depth, blackboard.Config);
            return new Vector2(world.x, world.y);
        }

        private LineRenderer ConfigureTransientLine(GameObject go, Color color, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingEffect;
            return lr;
        }

        private LineRenderer CreateLineOn(Transform parent, string name, Vector3[] points, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingEffect;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            return lr;
        }

        private static Vector3[] BuildCircle(Vector3 center, float radius, int segments)
        {
            var pts = new Vector3[Mathf.Max(3, segments)];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, 0f);
            }
            return pts;
        }

        /// <summary>GraffitiRenderer/HardwareReactionRenderer와 같은 이유로 캐릭터 LineRenderer의
        /// 머티리얼을 빌려 쓴다(Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
