using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 수치/색상 상수 보관용 ScriptableObject.
    /// 컨벤션: 코드에 매직 넘버/하드코딩 색상을 두지 않고 전부 이 에셋을 경유해 참조한다.
    /// Phase 0에서는 필드 정의만 하고, 기본값은 추후 UX/디자인·물리 튜닝으로 교체될 임시값이다.
    /// </summary>
    [CreateAssetMenu(fileName = "StickConfig", menuName = "StickMate/Stick Config", order = 0)]
    public sealed class StickConfig : ScriptableObject
    {
        [Header("이동")]
        [Tooltip("걷기 속도 (유닛/초)")]
        public float walkSpeed = 2.5f;

        [Tooltip("점프 시 초기 상승 속도")]
        public float jumpForce = 6f;

        [Header("물리")]
        [Tooltip("중력 스케일 (Rigidbody2D.gravityScale에 곱해 사용)")]
        public float gravityScale = 3f;

        [Tooltip("발판 경계에서 미세한 흔들림으로 인한 Fall 오탐을 막기 위한 유예 시간(초)")]
        public float fallGraceDuration = 0.1f;

        [Tooltip("발판을 막 벗어난 직후에도 이 시간(초) 이내면 점프를 허용하는 코요테 타임. 의도된 사양으로 " +
                 "채택됨(Architect 결정, 2026-08-27, docs/BUG_REPORT_PHASE1.md BUG-P1-M5) — 캐주얼 데스크톱 " +
                 "토이라 관대한 조작감이 낫다는 판단. fallGraceDuration과 값은 같아도 되지만, 서로 다른 두 " +
                 "목적(발판 이탈 판정 vs 점프 허용 판정)을 하나의 값으로 재사용하던 것을 개념적으로 분리한 필드다.")]
        public float coyoteTimeDuration = 0.1f;

        [Header("Active Ragdoll 전이 (0절 하이브리드 무빙 방식)")]
        [Tooltip("이 값 이상의 외력(충격량 크기)이 가해지면 능동 상태 어디서든 즉시 Ragdoll로 강제 전이")]
        public float ragdollForceThreshold = 8f;

        [Tooltip("Ragdoll 상태에서 전신 속도가 이 값 이하로 떨어지면 Getup 복귀 후보로 판정")]
        public float ragdollSettleSpeedThreshold = 0.3f;

        [Tooltip("ragdollSettleSpeedThreshold 이하 속도가 이 시간(초) 이상 유지되어야 실제로 Getup 전이 (순간적인 감속 오탐 방지)")]
        public float ragdollSettleHoldDuration = 0.5f;

        [Tooltip("Ragdoll -> Getup 진입 후 직립 포즈로 보간 완료까지 걸리는 기준 시간(초). GetupState._getupProgress가 이 값으로 정규화된다")]
        public float getupDuration = 0.6f;

        [Tooltip("Getup 중 각 관절이 목표 각도(직립)를 얼마나 적극적으로 따라가는지의 비례 제어 게인(도/초 per 도 오차)")]
        public float getupMotorGain = 6f;

        [Tooltip("Getup 중 관절 모터가 낼 수 있는 최대 토크")]
        public float getupMaxMotorTorque = 50f;

        [Header("파쿠르 (docs/UX_FLOW.md 4절)")]
        [Tooltip("ParkourClimb 진입 판정을 위한 벽/모서리 발판 감지 반경(경계 근접 거리이자, 벽으로 인정할 최소 높이차 겸용)")]
        public float parkourDetectionRadius = 0.5f;

        [Tooltip("ParkourClimb 상태에서 매달린 위치부터 벽 상단까지 올라가는 데 걸리는 기준 시간(초)")]
        public float parkourClimbDuration = 0.5f;

        [Tooltip("착지 시 이 값(월드 유닛) 이상 낙하했으면 구르기(부드러운 착지) 이벤트를 발행한다. 실제 파티클/애니메이션은 Phase 2+ 렌더링 레이어 담당")]
        public float rollLandingHeightThreshold = 2f;

        [Header("비침해 원칙")]
        [Tooltip("클릭 관통 기본 ON 여부 (원칙 2)")]
        public bool clickThroughDefaultEnabled = true;

        [Tooltip("발판 목록을 다시 열거(폴링)하는 주기(초). 매 프레임 열거 금지 — 반드시 이 주기로 제한")]
        public float footholdPollInterval = 0.5f;

        [Tooltip("전체화면 게임 감지(IsFullscreenAppActive)를 다시 확인하는 주기(초). 발판 폴링과 별도 주기로 관리한다")]
        public float fullscreenPollInterval = 1.5f;

        [Header("좌표계 변환 (Platform/ScreenCoordinateConverter.cs 참고)")]
        [Tooltip("Unity가 보고하는 화면 픽셀 단위 ↔ OS가 보고하는 실제 데스크톱 픽셀 단위 사이의 배율. " +
                 "고DPI(Retina 등) 환경에서 두 값이 다를 때 보정용. 기본값 1 = 배율 차이 없음으로 가정(Phase 1 근사치).")]
        public float desktopDpiScale = 1f;

        [Tooltip("캐릭터 발 위치(OS 좌표)와 발판 상단 사이 허용 오차(OS 픽셀 단위). 이 범위 안이면 접지로 판정")]
        public float groundSnapTolerance = 6f;

        [Header("입력")]
        [Tooltip("이동 입력(-1~1)의 불감대. 이 값 이하는 '입력 없음'으로 취급해 Idle<->Walk 떨림을 방지")]
        public float moveInputDeadzone = 0.15f;

        [Header("자율 배회 AI (docs/UX_FLOW.md 26절, BUG-P1-B2 대응 — AutoWanderController가 소비)")]
        [Tooltip("Idle(Resting) 유지 시간 최소(초). 26-1.")]
        public float wanderIdleDurationMin = 2.0f;

        [Tooltip("Idle(Resting) 유지 시간 최대(초). 26-1.")]
        public float wanderIdleDurationMax = 6.0f;

        [Tooltip("Walk(Moving) 지속 시간 최소(초, 발판 경계 도달로 조기 종료될 수 있음). 26-1.")]
        public float wanderWalkDurationMin = 1.5f;

        [Tooltip("Walk(Moving) 지속 시간 최대(초). 26-1.")]
        public float wanderWalkDurationMax = 4.0f;

        [Tooltip("Walk 중 즉흥 방향전환 판정 주기(초). 26-1.")]
        public float wanderTurnCheckInterval = 0.5f;

        [Tooltip("Walk 중 판정 주기마다 즉흥 방향전환이 발생할 확률(0~1). 같은 Walk 페이즈 내 최대 1회로 제한됨. 26-1.")]
        public float wanderSpontaneousTurnChance = 0.08f;

        [Tooltip("Idle 종료 후 Walk로 전이할 확률(0~1). 26-1.")]
        public float wanderPostIdleWalkChance = 0.75f;

        [Tooltip("Idle 종료 후 제자리 점프를 할 확률(0~1). 나머지(1 - Walk확률 - 이 값)는 Idle 연장. 26-1.")]
        public float wanderPostIdleJumpChance = 0.05f;

        [Tooltip("진행 방향 앞쪽, 지금 딛고 있는 발판의 잔여 길이가 이 값(유닛) 이하이면 경계 도달로 판정. 26-2.")]
        public float wanderEdgeStopDistance = 0.3f;

        [Tooltip("경계 도달 시 정지 후 방향을 반전하기까지의 대기 시간 최소(초). 26-2.")]
        public float wanderEdgeTurnPauseMin = 0.3f;

        [Tooltip("경계 도달 시 정지 후 방향을 반전하기까지의 대기 시간 최대(초). 26-2.")]
        public float wanderEdgeTurnPauseMax = 0.8f;

        [Tooltip("경계 도달 시 정지 대신 진행 방향을 유지한 채 점프를 시도할 확률(0~1). 화면 자체의 물리적 " +
                 "끝(더 이상 발판이 없음)에서는 이 확률과 무관하게 항상 0으로 강제된다(화면 밖 낙하 방지). 26-2.")]
        public float wanderEdgeJumpAttemptChance = 0.10f;

        [Tooltip("Idle/Walk 지속시간·경계 정지 대기시간에 곱해지는 지터 비율(±). 예: 0.175 = ±17.5%. 26-3.")]
        public float wanderDurationJitterRatio = 0.175f;

        [Tooltip("두리번거리기 트리거까지의 지연시간 최소(초, Idle 진입 시점부터). 26-3.")]
        public float wanderLookAroundDelayMin = 1.0f;

        [Tooltip("두리번거리기 트리거까지의 지연시간 최대(초). 26-3.")]
        public float wanderLookAroundDelayMax = 2.5f;

        [Tooltip("두리번거리기 연출 지속시간 최소(초). 실제 재생은 Phase 2+ 렌더링 레이어 담당 — 지금은 값만 보관.")]
        public float wanderLookAroundDurationMin = 0.6f;

        [Tooltip("두리번거리기 연출 지속시간 최대(초).")]
        public float wanderLookAroundDurationMax = 1.0f;

        [Tooltip("'Idle 연장'이 연속 3회 이상 선택됐을 때, 앉기/하품 연출을 트리거할 확률(0~1). 26-3.")]
        public float wanderRestExtendSitChance = 0.15f;

        [Tooltip("커서 근접 반응 트리거 반경(OS 화면 픽셀). Phase 2로 연기됨(26-4) — 지금은 필드만 예약, " +
                 "AutoWanderController가 아직 소비하지 않음.")]
        public float wanderCursorReactionRadiusPx = 150f;

        [Header("전투 공용 (Attack 상태, Phase 3)")]
        [Tooltip("Attack 상태의 모션 재생 지속 시간(초). 완료 시 진입 직전 능동 상태로 복귀한다.")]
        public float attackDuration = 0.4f;

        [Header("격파 미니게임 (docs/UX_FLOW.md 10절, Phase 3)")]
        [Tooltip("기 모으기 게이지가 채워지는 데 걸리는 시간(초) 최소. 재시도마다 이 구간에서 새로 랜덤 추첨.")]
        public float battleChargeDurationMin = 1.5f;

        [Tooltip("기 모으기 게이지 지속 시간 최대(초).")]
        public float battleChargeDurationMax = 2.0f;

        [Tooltip("스위트 스팟 구간 시작 비율(0~1, 게이지 전체 대비). 관대하게(성공 경험 우선) 잡을 것.")]
        public float battleSweetSpotStart = 0.70f;

        [Tooltip("스위트 스팟 구간 종료 비율(0~1).")]
        public float battleSweetSpotEnd = 0.85f;

        [Tooltip("실패 시 최대 재도전 횟수. 이 횟수를 초과하면 오브젝트가 스스로 사라지는 퇴장으로 종료.")]
        public int battleMaxRetries = 3;

        [Tooltip("실패 후 게이지가 자동으로 재시작되기까지의 대기 시간(초).")]
        public float battleFailRetryDelaySeconds = 1.5f;

        [Tooltip("성공 판정 후 파괴 연출을 위해 머무는 시간(초) — 실제 파티클/애니메이션은 Phase 2+ 렌더링 담당.")]
        public float battleSuccessResolveDelaySeconds = 1.0f;

        [Tooltip("이벤트 시작 후 이만큼(초) 클릭 입력이 전혀 없으면 자동 취소(유저 이탈 감지).")]
        public float battleInputTimeoutSeconds = 5f;

        [Tooltip("자동(유휴) 발동 확률 판정 주기(초).")]
        public float battleAutoTriggerCheckInterval = 60f;

        [Tooltip("판정 주기마다 자동으로 격파 미니게임이 발동할 확률(0~1). 임시 추정치 — 체감 빈도로 튜닝 필요.")]
        public float battleAutoTriggerChance = 0.05f;

        [Header("라이벌 스틱맨 대결 (docs/UX_FLOW.md 11절, Phase 3)")]
        [Tooltip("스폰 확률 판정 주기(초, '유휴 판정 주기마다'를 구체화한 값).")]
        public float rivalSpawnCheckInterval = 90f;

        [Tooltip("판정 주기마다 라이벌이 등장할 확률(0~1). UX 명시값 3~5% 구간.")]
        public float rivalSpawnChance = 0.04f;

        [Tooltip("대결 종료 후 다음 스폰이 허용되기까지의 최소 쿨다운(초). UX 명시값 20분.")]
        public float rivalSpawnCooldownSeconds = 1200f;

        [Tooltip("대결 최대 지속 시간(초). 도달 시 승부 미결이어도 무승부로 강제 종료.")]
        public float rivalMaxDurationSeconds = 30f;

        [Tooltip("스폰을 진행해도 되는 최소 유효 발판 개수(부족하면 다음 판정 주기로 이연).")]
        public int rivalSpawnMinFootholds = 2;

        [Tooltip("스폰 시 플레이어 기준 좌우 오프셋 거리(월드 유닛) — '화면 가장자리에서 걸어 들어옴' 근사.")]
        public float rivalSpawnOffsetWorldX = 6f;

        [Tooltip("라이벌 추적 AI가 목표(플레이어) 근처에서 멈추는 정지 거리(월드 유닛).")]
        public float rivalStopDistance = 0.6f;

        [Tooltip("서로 이 거리(월드 유닛) 이내로 근접하면 공격 판정 시도가 발동.")]
        public float rivalAttackRange = 1.0f;

        [Tooltip("공격 판정 시도 사이의 쿨다운(초).")]
        public float rivalAttackCooldownSeconds = 1.2f;

        [Tooltip("이 횟수만큼 피격당한 쪽이 패배로 대결이 종료된다.")]
        public int rivalDuelHitsToLose = 2;

        [Tooltip("라이벌의 타격이 상대에게 가하는 충격량은 ragdollForceThreshold에 이 배율을 곱한 값으로 " +
                 "계산된다(항상 RAGDOLL 전이를 보장하기 위해 1보다 크게 유지할 것).")]
        public float rivalAttackImpactMultiplier = 1.25f;

        [Header("드래그&던지기 (docs/UX_FLOW.md 12절, Phase 3)")]
        [Tooltip("던지기 속도 계산에 쓰는 최근 커서 이동 구간 길이(초). UX 명시값 0.12초.")]
        public float dragThrowVelocitySampleWindowSeconds = 0.12f;

        [Tooltip("던지기 속도 상한(월드 유닛/초). 이 값으로 clamp해 '실종 버그'(화면 밖으로 사라져 " +
                 "안 돌아오는 투사체)를 방지한다.")]
        public float dragThrowMaxSpeed = 12f;

        [Tooltip("마우스다운 상태로 이만큼(초) 유지되면 자동으로 놓임(release) 처리된다.")]
        public float dragThrowMaxHoldSeconds = 10f;

        [Tooltip("드래그 중 캐릭터가 커서를 뒤쫓는 스프링·댐퍼 추종의 SmoothDamp 시간 상수(초). 작을수록 " +
                 "커서에 더 즉각적으로 달라붙는다.")]
        public float dragFollowSmoothTime = 0.08f;

        [Header("로데오 커서 (docs/UX_FLOW.md 13절, Phase 3)")]
        [Tooltip("커서가 '정지'로 간주되는 이동 반경(OS 화면 픽셀). 이 반경 안의 흔들림은 무시.")]
        public float rodeoStillRadiusPx = 5f;

        [Tooltip("커서가 이만큼(초) 연속으로 정지 상태를 유지하면 로데오 커서가 발동.")]
        public float rodeoStillTriggerSeconds = 5f;

        [Tooltip("트리거 시점에 캐릭터와 커서 사이 거리가 이 값(OS 화면 픽셀) 이내여야 '도달 가능'으로 " +
                 "판정해 발동한다.")]
        public float rodeoReachDistancePx = 400f;

        [Tooltip("캐릭터가 커서 위치로 '폴짝 올라타는' 접근 단계의 지속 시간(초).")]
        public float rodeoMountDurationSeconds = 0.3f;

        [Tooltip("로데오 커서 최대 지속 시간(초) — 2차 안전망(타임아웃), 도달 시 정상 종료.")]
        public float rodeoMaxDurationSeconds = 10f;

        [Tooltip("커서 이동 속도(월드 유닛/초)가 이 값을 넘으면 '거친 흔들기'로 판정해 튕겨 떨어진다(1차 " +
                 "안전망, 암묵적 탈출구).")]
        public float rodeoShakeSpeedThresholdWorldPerSec = 20f;

        [Tooltip("거친 흔들기로 튕겨 떨어질 때 가하는 충격량은 ragdollForceThreshold에 이 배율을 곱한 값 " +
                 "으로 계산된다(항상 RAGDOLL 전이를 보장하기 위해 1보다 크게 유지할 것).")]
        public float rodeoShakeImpactMultiplier = 1.25f;

        [Header("색상 (임시 플레이스홀더 — 디자이너 확정 전까지)")]
        public Color primaryOutlineColor = Color.black;
        public Color dialogueBubbleColor = Color.white;
    }
}
