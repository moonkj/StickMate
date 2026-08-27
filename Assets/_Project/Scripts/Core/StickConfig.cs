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

        [Header("윈도우 창 도둑 (docs/UX_FLOW.md 27-1절, Phase 4)")]
        [Tooltip("유휴 판정 저확률 추첨 주기(초). 10/11절과 동일한 스펙터클 트리거 패턴 재사용.")]
        public float windowTheftCheckInterval = 60f;

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 명시값 2~4%.")]
        public float windowTheftChance = 0.03f;

        [Tooltip("종료 후 다음 발동까지의 최소 쿨다운(초). UX 명시값 15분.")]
        public float windowTheftCooldownSeconds = 900f;

        [Tooltip("대상 창 선정 기준 — 창 폭이 캐릭터 신장(OS 화면 픽셀 환산)의 이 배수 이하여야 후보로 " +
                 "채택된다. 작은 창일수록 '정말 밀리는 듯' 긴장감이 산다(27-1 근거).")]
        public float windowTheftMaxTargetWidthMultiplier = 3f;

        [Tooltip("1/2회차 시도 각각의 지속 시간(초). 2회 고정(1회는 성의 없어 보이고 3회 이상은 지루함).")]
        public float windowTheftAttemptDuration = 1.2f;

        [Tooltip("2회 시도 후 포기 리액션(헥헥거림) 지속 시간(초) — 이 시간이 끝나면 정상적으로 Idle로 복귀.")]
        public float windowTheftGiveUpDuration = 1.5f;

        [Header("바탕화면 청소부 / 블랙홀 (docs/UX_FLOW.md 27-2/27-5절, Phase 4 — 복제 스프라이트 공용 파이프라인)")]
        [Tooltip("청소부 유휴 판정 저확률 추첨 주기(초).")]
        public float desktopTidyCheckInterval = 60f;

        [Tooltip("청소부 판정 주기마다 발동할 확률(0~1). UX는 구체 수치를 명시하지 않아 다른 유휴 " +
                 "스펙터클(창 도둑 2~4%)과 같은 대역으로 임시 추정.")]
        public float desktopTidyChance = 0.03f;

        [Tooltip("청소부 종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 창 도둑과 동일한 15분으로 임시 추정.")]
        public float desktopTidyCooldownSeconds = 900f;

        [Tooltip("청소부 정렬 연출(복제 스프라이트 슬라이드~짠 포즈)의 지속 시간(초). 이 시간 후 정상 " +
                 "종료(오버레이 페이드아웃) — 실제 슬라이드 애니메이션은 Phase2+ 렌더링 담당.")]
        public float desktopTidyDurationSeconds = 2.5f;

        [Tooltip("블랙홀 유휴 판정 저확률 추첨 주기(초).")]
        public float blackholeCheckInterval = 60f;

        [Tooltip("블랙홀 판정 주기마다 발동할 확률(0~1). UX 미명시 — 청소부와 동일 대역으로 임시 추정.")]
        public float blackholeChance = 0.03f;

        [Tooltip("블랙홀 종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 청소부와 동일하게 임시 추정.")]
        public float blackholeCooldownSeconds = 900f;

        [Tooltip("블랙홀 소용돌이~튕겨나옴 연출의 지속 시간(초). 실제 궤적 애니메이션은 Phase2+ 렌더링 담당.")]
        public float blackholeDurationSeconds = 2.5f;

        [Header("화면 낙서 그라피티 (docs/UX_FLOW.md 27-3절, Phase 4)")]
        [Tooltip("유휴 판정 저확률 추첨 주기(초).")]
        public float graffitiCheckInterval = 60f;

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 미명시 — 방해성이 가장 낮은 항목이라 다른 스펙터클보다 " +
                 "약간 높게 임시 추정.")]
        public float graffitiChance = 0.04f;

        [Tooltip("종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 방해성이 낮아 다른 스펙터클보다 짧게 임시 추정.")]
        public float graffitiCooldownSeconds = 600f;

        [Tooltip("캐릭터로부터 그리기 후보 영역까지의 최소 반경(OS 화면 픽셀). UX 명시값 200px.")]
        public float graffitiMinRadiusPx = 200f;

        [Tooltip("캐릭터로부터 그리기 후보 영역까지의 최대 반경(OS 화면 픽셀). UX 명시값 300px.")]
        public float graffitiMaxRadiusPx = 300f;

        [Tooltip("낙서 영역의 정사각형 한 변 길이(OS 화면 픽셀) — 발판과의 겹침 판정에 쓰이는 후보 사각형 크기.")]
        public float graffitiRegionSizePx = 96f;

        [Tooltip("낙서가 유지되는 시간(초) 최소. UX 명시값 3~5초 구간(페이드아웃은 별도로 Phase2+ 렌더링이 처리).")]
        public float graffitiHoldDurationMin = 3f;

        [Tooltip("낙서가 유지되는 시간(초) 최대.")]
        public float graffitiHoldDurationMax = 5f;

        [Tooltip("겹치지 않는 빈 영역을 찾기 위해 무작위 후보를 시도하는 최대 횟수. 전부 실패하면 이번 " +
                 "유휴 판정 주기는 스킵하고 다음 주기로 이연한다(27-3, 억지로 창 위에 그리지 않음).")]
        public int graffitiCandidateSearchAttempts = 8;

        [Header("윈도우 크래시 (docs/UX_FLOW.md 27-4절, Phase 4)")]
        [Tooltip("유휴 판정 저확률 추첨 주기(초). 원문의 키보드타건속도/에러창 감지 트리거는 UX 설계에서 " +
                 "이미 배제되어 이 저확률 추첨으로 대체되었다(26-5 키보드 폐기 결정과의 충돌 방지, 27-4 근거).")]
        public float windowCrashCheckInterval = 60f;

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 명시값 1~3%(다른 스펙터클보다 낮게 — 시각적 충격이 큼).")]
        public float windowCrashChance = 0.02f;

        [Tooltip("종료 후 다음 발동까지의 최소 쿨다운(초). UX 명시값 20~30분 구간의 중간값(25분).")]
        public float windowCrashCooldownSeconds = 1500f;

        [Tooltip("캐릭터의 해머 스윙 모션 지속 시간(초) — 크랙 오버레이 자체의 3초 수명과는 독립적이다.")]
        public float windowCrashSwingDuration = 0.4f;

        [Tooltip("크랙 유리 오버레이가 유지되는 시간(초). UX 명시값 3초, 이후 파편화 페이드아웃(0.3~0.5초, " +
                 "Phase2+ 렌더링 담당)과 함께 제거.")]
        public float windowCrashOverlayDurationSeconds = 3f;

        [Header("PC 하드웨어 반응 (docs/UX_FLOW.md 23/27-6절, Phase 4)")]
        [Tooltip("배터리 잔량 폴링 주기(초). UX 명시값 60~120초 — 물리적으로 급변하지 않는 값이라 저빈도로 충분.")]
        public float hardwareBatteryPollInterval = 90f;

        [Tooltip("이 값(0~1) 이하면 배터리 부족 반응 후보. UX 명시값 20%.")]
        public float hardwareLowBatteryThreshold = 0.2f;

        [Tooltip("충전 중 여부 폴링 주기(초). Unity에는 OS 충전 이벤트 콜백이 없어(크로스플랫폼 API " +
                 "부재) 항상 이 폴백 폴링을 사용한다 — UX 명시값 30초.")]
        public float hardwareChargingPollInterval = 30f;

        [Tooltip("CPU 근사 지표(프레임타임) 샘플링 주기(초). UX 명시값 5~10초. **알려진 한계**: Unity에는 " +
                 "프로세스별 실제 CPU 사용률을 얻는 크로스플랫폼 API가 없어, 이 앱 자신의 프레임타임 저하를 " +
                 "'시스템 부하'의 매우 거친 근사 지표로 대신 사용한다(Interaction/HardwareReactionDirector.cs 참고).")]
        public float hardwareCpuSampleInterval = 7f;

        [Tooltip("CPU 과부하로 판정하는 평균 프레임타임 임계값(초). 기본값은 대략 20fps 미만에 해당하는 " +
                 "근사치 — 정확한 CPU% 문턱값이 아니라 '체감 버벅임' 근사 임계값이다.")]
        public float hardwareCpuHighFrameTimeThresholdSeconds = 0.05f;

        [Tooltip("CPU 과부하 판정을 확정하기까지 샘플이 연속으로 임계값을 넘겨야 하는 누적 시간(초). " +
                 "UX 명시값 30~60초(샘플 주기와 판정 주기는 별개 — 27-6 표 근거).")]
        public float hardwareCpuSustainWindowSeconds = 45f;

        [Tooltip("네트워크 연결성 폴링 주기(초). UX 명시값 15~30초.")]
        public float hardwareNetworkPollInterval = 20f;

        [Tooltip("네트워크 끊김을 확정하기 전 연속으로 끊김이 관찰되어야 하는 폴링 횟수. 순간적 로밍 " +
                 "끊김 오탐 방지(23절 근거) — 2회 연속(=폴링 주기의 2배 시간) 확인.")]
        public int hardwareNetworkConfirmPollCount = 2;

        [Tooltip("배터리 부족 판정을 확정하기 전 연속으로 저잔량이 관찰되어야 하는 폴링 횟수. 순간적 " +
                 "판독 오류 방지 — 2회 연속 확인.")]
        public int hardwareBatteryConfirmPollCount = 2;

        [Tooltip("같은 하드웨어 반응이 회복(정상 범위 복귀) 이후 다시 알림 가능해지기까지의 최소 대기 " +
                 "시간(초). UX 명시값 5~10분 구간의 중간값(7분) — '회복 확인 전 재알림 금지' 규칙과 별개로, " +
                 "회복 이후에도 이 시간만큼은 재알림을 유예한다(빠른 flapping 방지).")]
        public float hardwareReactionCooldownSeconds = 420f;

        [Header("색상 (임시 플레이스홀더 — 디자이너 확정 전까지)")]
        public Color primaryOutlineColor = Color.black;
        public Color dialogueBubbleColor = Color.white;
    }
}
