using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 캐릭터 선 색 프리셋(사용자 요청, 2026-08-28). 밝은 바탕화면에서는 검정이, 어두운 바탕화면에서는
    /// 흰색이 보인다 — 데스크톱 펫은 사용자의 배경을 고를 수 없으므로 둘 다 지원해야 한다.
    /// 값을 숫자로 직렬화하므로(Black=0) 기존 에셋은 자동으로 Black을 유지한다.
    /// </summary>
    public enum StickmanInkColor
    {
        Black = 0,
        White = 1,
    }

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

        [Header("포즈 애니메이션 (능동 상태 절차적 팔다리 제어, 2026-08-28 근본 재구현)")]
        // 왜 모터 관련 필드가 전부 사라졌는가(walkCycleMotorGain / walkCycleMaxMotorTorque /
        // walkCycleMaxMotorSpeedDegPerSec / walkCycleLegAngleLimitDegrees / walkCycleArmAngleLimitDegrees):
        // 능동 상태에서 HingeJoint2D 모터로 중력과 싸우던 물리 기반 접근 자체를 폐기했기 때문이다
        // (States/StickmanPoseAnimator.cs 클래스 문서 참고). 이제 팔다리는 Kinematic + transform
        // 직접 제어이므로 토크/게인/각도제한이라는 개념이 존재하지 않는다 — 목표 각도가 곧 실제 각도다.
        // walkCycleFrequencyPerSpeed / walkCycleLegSwingDegrees / walkCycleArmSwingRatio 제거
        // (2026-08-28 키프레임 보행 사이클 교체): 보행이 사인파 진폭/주파수 계수로 표현되지 않는다.
        // 관절 각도는 States/StickmanPoseAnimator.cs의 8키 포즈 표가 정의하고(튜닝 스칼라가 아니라
        // 서로 정합성을 갖는 "애니메이션 에셋"이라 그쪽에 둔다), 사이클 주파수는 그 표에서 계산한
        // 보폭과 실제 이동 속도로 역산한다 — 임의 계수를 곱하던 예전 방식이 디딤발이 미끄러지는
        // 문워크의 원인이었다.

        [Tooltip("보행 키포즈 표(States/StickmanPoseAnimator.cs)의 **모든 관절 각도에 곱해지는 전체 진폭 " +
                 "배율**. 1이면 리더가 지정한 표 그대로(엉덩이 ±25도, 스윙 무릎 50도), 0.8이면 전체적으로 " +
                 "얌전하게, 1.2면 과장되게 걷는다. 표의 개별 값은 서로 정합성을 가져야 하므로 코드 상수로 " +
                 "두고, 나중에 튜닝할 여지는 이 하나의 스칼라로만 연다. 보폭 역산도 이 배율이 적용된 " +
                 "각도로 계산되므로 값을 바꿔도 발이 미끄러지지 않는다.")]
        public float walkPoseAmplitudeScale = 1f;

        [Tooltip("키포즈 표에서 기하학적으로 계산한 한 사이클 이동 거리(보폭×2)에 곱하는 보정 계수. " +
                 "1이면 계산값 그대로 쓴다. 실제 화면에서 디딤발이 앞으로 밀리면(=사이클이 너무 느림) " +
                 "1보다 작게, 뒤로 끌리면 1보다 크게 미세 조정한다. 값이 커질수록 한 걸음을 더 크게 " +
                 "잡는다고 가정하므로 다리 놀리는 속도가 느려진다(주파수 = 이동속도 / 사이클 이동거리).\n" +
                 "기본값 0.93의 근거(실측): 1.0(순수 기하학값)으로 .app을 100초 돌린 로그에서 디딤 국면 " +
                 "전체의 발 순수 이동량이 몸 이동량의 평균 +7%로 남았다 — 각도가 목표각을 지수 감쇠로 " +
                 "따라가느라 실제 진폭이 표보다 조금 작아(엉덩이 실측 ±23.8도 vs 표 ±25도) 보폭이 그만큼 " +
                 "짧아지기 때문이다. 사이클을 그 비율만큼 빠르게 돌려 상쇄한다.")]
        public float walkStrideScale = 0.93f;

        [Tooltip("Idle(및 Walk를 제외한 모든 능동 상태)에서 양다리를 바깥쪽으로 벌리는 각도(도). 0이면 " +
                 "두 다리가 완전히 붙어 수직으로 서고, 값이 커질수록 졸라맨 그림의 '/ \\' 처럼 벌어진다. " +
                 "사용자 확정 참고 실루엣(2026-08-28): 머리 O / 몸통 | / 팔 /|\\ / 다리 / \\.")]
        public float idleLegSpreadDegrees = 12f;

        [Tooltip("Idle에서 양팔을 몸통 옆으로 내려 벌리는 각도(도) — 위 idleLegSpreadDegrees와 동일한 부호 " +
                 "규약(왼팔은 -, 오른팔은 +). 졸라맨 '/|\\' 실루엣이 나오도록 다리보다 크게 잡는다.")]
        public float idleArmSpreadDegrees = 40f;

        [Tooltip("팔다리 각도가 목표각을 따라가는 지수 감쇠 계수(1/초). 클수록 즉각적이고 작을수록 " +
                 "부드럽다. 프레임레이트 독립 공식 t = 1 - exp(-rate*deltaTime)에 쓰이므로(단순 " +
                 "Lerp(a,b,rate*dt)와 달리 fps가 달라도 같은 체감 속도가 나온다), 값의 의미는 " +
                 "\"약 1/rate 초 만에 목표까지의 오차가 63% 줄어든다\"이다. 사용자 요청(2026-08-28, " +
                 "\"부드럽게 움직여야 함\"을 두 번 강조)으로 도입 — 이전에는 목표각을 매 프레임 즉시 " +
                 "대입해 상태 전환·프레임레이트 변동 순간마다 각도가 툭툭 튀었다. 2026-08-28 키프레임 " +
                 "보행 교체 후 14 -> 35로 상향: 보행 곡선 자체가 이미 Catmull-Rom으로 매끄러워 스무딩이 " +
                 "부드러움을 만들 필요가 없어졌고, 오히려 낮은 값은 1.5Hz 보행에서 다리 각도 진폭을 " +
                 "17%나 깎아 **디딤발이 미끄러지는(문워크) 원인**이 됐다(실측 slip 0.5 -> 0.05). 이제 " +
                 "이 값의 역할은 상태 전환(Idle<->Walk, GETUP 복귀)에서만 각도가 튀지 않게 하는 것이다.")]
        public float poseSmoothingRate = 35f;

        [Tooltip("보행 사이클 주파수 산출에 쓰는 수평 속도의 지수 감쇠 계수(1/초). poseSmoothingRate와 " +
                 "같은 공식·같은 단위. 걷기 시작/멈춤처럼 속도가 급변할 때 다리 흔들기 주파수가 함께 " +
                 "튀지 않도록 입력 자체를 완만하게 만든다(속도가 0에서 차오르며 보폭도 자연스럽게 빨라진다).")]
        public float walkSpeedSmoothingRate = 6f;

        [Tooltip("Walk 중 몸 전체가 상하로 흔들리는 진폭(월드 유닛). 실제 걷기처럼 한 걸음마다 몸이 살짝 " +
                 "오르내리게 해 뻣뻣함을 줄인다(2026-08-28 사용자 \"너무 뻣뻣하게 움직임\" 대응). 보행 " +
                 "사인파의 2배 주파수로 진동한다(다리가 모였을 때 높고 벌어졌을 때 낮다). **시각 전용**이라 " +
                 "Rigidbody2D.position은 건드리지 않는다 — 접지 판정이 루트의 물리 위치를 발 높이로 쓰기 " +
                 "때문에 그걸 흔들면 접지 로직이 깨진다(States/StickmanPoseAnimator.SetBodyOffset 참고).")]
        public float walkBounceAmplitude = 0.025f;

        [Tooltip("Idle에서 몸 전체가 호흡처럼 아주 느리게 오르내리는 진폭(월드 유닛). 완전 정지는 " +
                 "\"얼어붙은 것\"처럼 보이므로 항상 미세하게 살아있게 만든다. walkBounceAmplitude와 같은 " +
                 "시각 전용 오프셋 경로를 쓴다.")]
        public float idleBreathAmplitude = 0.012f;

        [Tooltip("Idle 호흡 모션의 주파수(Hz). 0.8이면 약 1.25초에 한 번 오르내린다.")]
        public float idleBreathFrequencyHz = 0.8f;

        [Tooltip("Idle 호흡에 맞춰 양팔 각도가 중립에서 벌어졌다 모이는 범위(도). 1~2도 정도의 아주 작은 " +
                 "값이라 \"움직인다\"기보다 \"살아있다\"로만 읽힌다.")]
        public float idleBreathArmDegrees = 1.5f;

        [Tooltip("Idle 중립 자세에서 무릎을 굽혀두는 각도(도). 0(완전히 편 상태)이면 사용자 지적대로 " +
                 "\"막대기\" 느낌이 난다 — 사람은 서 있을 때도 무릎이 완전히 펴져 있지 않다. " +
                 "Editor/SceneBootstrapper.cs의 IdleKneeBendDegrees와 반드시 같은 값이어야 한다(프리팹 " +
                 "저장 자세와 런타임 목표각이 어긋나면 첫 프레임에 튄다).")]
        public float idleKneeBendDegrees = 4f;

        [Tooltip("Idle 중립 자세에서 팔꿈치를 굽혀두는 각도(도). 무릎보다 크게 잡는다 — 사람은 서 있을 때 " +
                 "팔꿈치가 눈에 띄게 굽어 있다. SceneBootstrapper.IdleElbowBendDegrees와 같은 값이어야 한다.")]
        public float idleElbowBendDegrees = 10f;

        // walkKneeBendDegrees / walkElbowBendDegrees 제거(2026-08-28): 보행 중 무릎/팔꿈치 굽힘은
        // 이제 단일 최대치가 아니라 위상별 키프레임 표(StickmanPoseAnimator.LegKneeKeys/ArmElbowKeys)가
        // 정의한다 — "스윙에서 45~50도로 크게 접히고 디딤에서 5~20도로 거의 펴진다"는 **비대칭**이
        // 자연스러움의 핵심이라 하나의 스칼라로는 표현할 수 없다.

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

        // getupMotorGain / getupMaxMotorTorque 제거(2026-08-28 근본 재구현): GETUP도 더 이상 관절
        // 모터로 몸을 일으키지 않는다. RagdollRig가 루트 회전각을, StickmanPoseAnimator가 팔다리 각도를
        // 각각 "널브러진 실제 각도 -> 직립 중립 각도"로 progress에 따라 직접 보간한다(100% 예측 가능,
        // 절대 실패하지 않음). getupDuration 하나로 전체 연출 시간이 결정된다.

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

        [Tooltip("Idle 종료 후 제자리 점프를 할 확률(0~1). 나머지(1 - Walk확률 - 이 값)는 Idle 연장. 26-1.\n" +
                 "★ 기본값 0 = 무작위 점프 비활성(2026-08-28 사용자 피드백 \"이상하게 점프도 하고\" 대응). " +
                 "UX 26-1의 5% 스펙 자체는 폐기하지 않고 이 값만 0으로 내린 것이므로, 되살리려면 이 필드를 " +
                 "0.05로 되돌리면 원래 동작이 그대로 복원된다(States/AutoWanderController.cs 로직 무수정).")]
        public float wanderPostIdleJumpChance = 0f;

        [Tooltip("진행 방향 앞쪽, 지금 딛고 있는 발판의 잔여 길이가 이 값(유닛) 이하이면 경계 도달로 판정. 26-2.")]
        public float wanderEdgeStopDistance = 0.3f;

        [Tooltip("경계 도달 시 정지 후 방향을 반전하기까지의 대기 시간 최소(초). 26-2.")]
        public float wanderEdgeTurnPauseMin = 0.3f;

        [Tooltip("경계 도달 시 정지 후 방향을 반전하기까지의 대기 시간 최대(초). 26-2.")]
        public float wanderEdgeTurnPauseMax = 0.8f;

        [Tooltip("경계 도달 시 정지 대신 진행 방향을 유지한 채 점프를 시도할 확률(0~1). 화면 자체의 물리적 " +
                 "끝(더 이상 발판이 없음)에서는 이 확률과 무관하게 항상 0으로 강제된다(화면 밖 낙하 방지). 26-2.\n" +
                 "★ 기본값 0 = 발판 경계 점프 비활성(위 wanderPostIdleJumpChance와 같은 사용자 피드백 대응). " +
                 "되살리려면 0.10으로 되돌리면 된다 — 그러면 26-2의 '90% 정지 / 10% 점프' 분기가 그대로 복원된다.")]
        public float wanderEdgeJumpAttemptChance = 0f;

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

        [Tooltip("드래그 중 캐릭터가 커서를 뒤쫓는 SmoothDamp 시간 상수(초). 작을수록 커서에 더 즉각적으로 " +
                 "달라붙는다.\n" +
                 "★ 0 = 스무딩 없이 잡은 지점이 커서에 즉시 밀착(2026-08-28 사용자 피드백 \"마우스에 딱 붙어서 " +
                 "끌려가야 하는데 이상하게 끌려감\" 대응, 현재 기본값). 0보다 크면 예전처럼 스프링·댐퍼 관성 " +
                 "추종이 되살아난다 — 값만 바꾸면 되고 로직은 두 경로를 모두 갖고 있다.\n" +
                 "이 값은 '위치 추종'에만 관여한다. 놓을 때의 던지기 속도는 아래 " +
                 "dragThrowVelocitySampleWindowSeconds 구간의 **커서 이동 이력**으로만 계산되므로 이 값을 " +
                 "0으로 둬도 던지기 손맛은 전혀 변하지 않는다(States/DragThrowState.cs 참고).")]
        public float dragFollowSmoothTime = 0f;

        [Header("로데오 커서 (docs/UX_FLOW.md 13절, Phase 3)")]
        [Tooltip("로데오 커서(마우스가 일정 시간 멈추면 캐릭터가 커서로 다가가 올라타는 UX 13절 기능) " +
                 "자동 발동 스위치.\n" +
                 "★ 기본값 OFF(2026-08-28 사용자 피드백 \"갑자기 마우스쪽으로 자기혼자 이동\" 대응 — 의도된 " +
                 "기능인 줄 모르고 버그로 인식했고, 드래그&던지기 테스트를 계속 방해했다). 기능/상태 " +
                 "(States/RodeoCursorState.cs)는 그대로 살아 있고 감시자(Interaction/RodeoCursorWatcher.cs)가 " +
                 "이 값만 확인해 폴링을 건너뛴다 — 이 값을 true(에셋에서 1)로 바꾸면 즉시 원래대로 발동한다.")]
        public bool rodeoCursorEnabled = false;

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

        [Tooltip("BUG-P5-M2 대응(docs/BUG_REPORT_PHASE5.md) — UX 24절 '1단계(로데오/인질극 발동 확률에 " +
                 "스트레스 가중치)'용. 스트레스 게이지(StressGauge.CurrentLevel, 0~1)가 이 값 이상이면 " +
                 "아래 rodeoStressTriggerSecondsMultiplier가 적용되어 로데오가 좀 더 쉽게 발동한다. UX " +
                 "명시값 근사치(60%대).")]
        public float stressRodeoWeightThreshold = 0.6f;

        [Tooltip("위 임계값 이상일 때 rodeoStillTriggerSeconds에 곱해지는 배율(24절 '약한 가중치' — " +
                 "1보다 작을수록 정지 판정 시간이 짧아져 로데오가 더 자주 발동). 과하게 공격적이지 않도록 " +
                 "기본값은 완만한 단축(30%)만 적용한다.")]
        public float rodeoStressTriggerSecondsMultiplier = 0.7f;

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

        [Header("투두 말풍선 (docs/UX_FLOW.md 17절, Phase 5)")]
        [Tooltip("들고 다니는 모드 리마인더 유휴 판정 주기(초). 할일 개수와 무관한 고정 주기(17절 명시).")]
        public float todoReminderCheckInterval = 45f;

        [Tooltip("판정 주기마다 리마인더가 실제로 발동할 확률(0~1).")]
        public float todoReminderChance = 0.2f;

        [Tooltip("들고 다니는 모드 — 종이를 펼쳐 대사를 보여주는 지속 시간(초). 이후 자동으로 Idle 복귀.")]
        public float todoReminderHoldSeconds = 3f;

        [Tooltip("활성(미완료) 할일이 이 개수를 넘으면 추가 시 '먼저 정리해볼까?' 같은 가벼운 안내만 " +
                 "노출(추가 자체는 막지 않음 — 강제 차단은 스트레스 유발이므로 지양, 17절).")]
        public int todoActiveCountSoftCap = 15;

        [Tooltip("체크 완료된 항목이 포스트잇 목록에서 취소선 상태로 남아있다가 자동으로 정리(완료함으로 " +
                 "이동)되기까지의 시간(초).")]
        public float todoCompletedLingerSeconds = 2.5f;

        [Tooltip("완료 체크를 실수로 눌렀을 때 되돌릴 수 있는 허용 시간(초). UX 명시값 3초.")]
        public float todoUndoWindowSeconds = 3f;

        [Tooltip("포스트잇 카드에 한 번에 노출하는 최대 줄 수. 초과분은 '[+N개 더보기]'로 접힘(17절).")]
        public int todoPostItMaxVisibleRows = 4;

        [Header("포모도로 감시자 (docs/UX_FLOW.md 18절, Phase 5)")]
        [Tooltip("딴짓 감지 관찰 창 길이(초). UX 명시값 2분.")]
        public float pomodoroObservationWindowSeconds = 120f;

        [Tooltip("타이머 시작 직후 무조건 관찰만 하고 경고를 발동하지 않는 유예 시간(초). UX 명시값 2분.")]
        public float pomodoroGraceSeconds = 120f;

        [Tooltip("한 관찰 창 안에서 이 횟수 이상 전경 창(포커스) 전환이 있으면 '산만함' 후보로 카운트. UX 명시값 6회.")]
        public int pomodoroFocusSwitchThreshold = 6;

        [Tooltip("산만함 후보 창이 이 횟수만큼 연속되어야 1단계(눈치주기)가 발동. UX 명시값 3회 연속.")]
        public int pomodoroTier1ConsecutiveWindows = 3;

        [Tooltip("1단계 발동 후 추가로 이만큼 더 연속되면 2단계(부드러운 리마인드)로 에스컬레이션. UX 명시값 +2주기.")]
        public int pomodoroTier2AdditionalWindows = 2;

        [Tooltip("2단계 발동 후 추가로 이만큼 더 연속되면 3단계(창 두드림)로 에스컬레이션.")]
        public int pomodoroTier3AdditionalWindows = 2;

        [Tooltip("이 시간(초) 이상 커서가 완전히 정지해 있으면 '자리비움' 극단값 신호(보조, 단독 판정 금지)로 반영.")]
        public float pomodoroMouseIdleSeconds = 90f;

        [Tooltip("아주 짧은 구간 동안 커서 이동 속도(OS px/초)가 이 값을 넘으면 '정처 없이 훑는' 극단값 신호로 반영.")]
        public float pomodoroMouseErraticSpeedThreshold = 4000f;

        [Tooltip("위 극단값 판정에 쓰는 순간 이동 속도 샘플 구간(초).")]
        public float pomodoroMouseErraticSampleSeconds = 1.0f;

        [Tooltip("집중 모드 시작 포즈(안경+팔짱) 유지 시간(초).")]
        public float pomodoroStartPoseHoldSeconds = 2f;

        [Tooltip("타이머 정상 만료 축하 포즈 유지 시간(초).")]
        public float pomodoroCompletePoseHoldSeconds = 2.5f;

        [Tooltip("유저가 중도에 집중 모드를 끌 때(패널티 없는 톤) 포즈 유지 시간(초).")]
        public float pomodoroCancelPoseHoldSeconds = 1.5f;

        [Tooltip("2단계 '어? 딴 데 보고 있네?' 대사 노출 유지 시간(초).")]
        public float pomodoroNudgeDialogueHoldSeconds = 2f;

        [Header("스트레스 게이지 (docs/UX_FLOW.md 19절, Phase 5)")]
        [Tooltip("격파훈련 과다 판정 관찰 창 길이(초). UX 명시값 5분.")]
        public float stressOveruseWindowSeconds = 300f;

        [Tooltip("위 창 안에서 격파 미니게임/드래그&던지기 진입이 이 횟수를 넘으면 초과분마다 스트레스 증가. UX 명시값 8회.")]
        public int stressOveruseTriggerCount = 8;

        [Tooltip("과다 상호작용 초과 1건마다 더해지는 스트레스 증가량(0~1 게이지 기준).")]
        public float stressOveruseIncrement = 0.12f;

        [Tooltip("상호작용이 전혀 없는 상태가 이 시간(초)을 넘으면 '심심함/외로움' 스트레스가 누적되기 " +
                 "시작한다. UX 명시값 반나절(12시간).")]
        public float stressNeglectThresholdSeconds = 43200f;

        [Tooltip("방치 임계값을 넘긴 뒤, 초과 1시간당 더해지는 스트레스 증가량.")]
        public float stressNeglectIncrementPerHourOver = 0.05f;

        [Tooltip("긴급정지 반복 사용 판정 관찰 창 길이(초).")]
        public float stressEmergencyStopWindowSeconds = 600f;

        [Tooltip("위 창 안에서 긴급정지 사용이 이 횟수를 넘으면 초과분마다 아주 약한 스트레스 증가(19절 — " +
                 "긴급정지는 유저의 정당한 권리이므로 사용을 주저하게 만들 정도로 강하면 안 됨).")]
        public int stressEmergencyStopTriggerCount = 3;

        [Tooltip("긴급정지 과다 사용 초과 1건마다 더해지는 스트레스 증가량 — 다른 트리거보다 훨씬 약하게 유지할 것.")]
        public float stressEmergencyStopIncrement = 0.03f;

        [Tooltip("최근 상호작용이 있어 '방치' 상태가 아닐 때, 시간당 자연 감소하는 스트레스량(게이지가 " +
                 "한 번 오른 뒤 영원히 안 내려가는 단조증가를 막기 위한 Coder 판단 — Tasklist 참고).")]
        public float stressPassiveDecayPerHour = 0.05f;

        [Tooltip("이 값(0~1) 이상이면 SULKY(부루퉁함) 상태가 발동 후보가 된다. UX 명시값 80%.")]
        public float stressSulkyThreshold = 0.8f;

        [Tooltip("SULKY 발동 저확률 추첨 주기(초).")]
        public float stressSulkyCheckInterval = 30f;

        [Tooltip("판정 주기마다 SULKY가 발동할 확률(0~1) — 게이지가 임계값을 넘은 동안에만 적용.")]
        public float stressSulkyChance = 0.5f;

        [Tooltip("SULKY 종료 후 다음 발동까지의 최소 쿨다운(초).")]
        public float stressSulkyCooldownSeconds = 90f;

        [Tooltip("SULKY 한숨/처진 자세 유지 시간(초).")]
        public float stressSulkyHoldSeconds = 2f;

        [Header("가출 (docs/UX_FLOW.md 20/24절, Phase 5)")]
        [Tooltip("스트레스 게이지가 이 값(0~1) 이상이면 가출(2단계, 확정 발동)이 트리거된다. 24절 — " +
                 "1단계(인질극/로데오 확률 가중)와 달리 확률이 아니라 임계값 도달 시 확정 발동.")]
        public float stressRunawayThreshold = 1.0f;

        [Tooltip("'나 안 해!' 대사 이후 화면 가장자리로 뛰어가는 애니메이션 유지 시간(초). 실제 이동/모션 " +
                 "연출은 Phase2+ 렌더링 담당 — 이 시간 동안은 상태만 확정 유지.")]
        public float runawayFleeDurationSeconds = 1.2f;

        [Tooltip("가출 은신처(화면 네 모서리)를 화면 가장자리로부터 안쪽으로 띄우는 여백(OS 화면 픽셀).")]
        public float runawayHideSpotMarginPx = 60f;

        [Tooltip("아무 조치가 없어도 스스로 복귀하는 안전망 타임아웃(초). UX 명시값 1~2시간 구간(기본 1.5시간).")]
        public float runawayAutoReturnSeconds = 5400f;

        [Tooltip("간식을 받아먹고 화해 대사를 하는 동안의 유지 시간(초), 이후 정상 Idle 복귀.")]
        public float runawayReconcileHoldSeconds = 1.5f;

        [Tooltip("자동 타임아웃/수동 소환/긴급 강제소환으로 스스로 돌아올 때의 대사 유지 시간(초).")]
        public float runawaySelfReturnHoldSeconds = 1.2f;

        [Tooltip("숨어있는 동안 은신처 근처에 은은한 단서(흔들림/소리)를 알리는 주기(초). 실제 연출은 " +
                 "Phase2+ 렌더링 담당 — 지금은 트리거 이벤트만 발행.")]
        public float runawayHintPulseIntervalSeconds = 8f;

        [Tooltip("간식을 받아 화해했을 때 감소하는 스트레스량(0~1) — 완전 리셋은 아님(20절 명시).")]
        public float runawaySnackStressRelief = 0.5f;

        [Header("색상 (임시 플레이스홀더 — 디자이너 확정 전까지)")]

        [Tooltip("캐릭터 선 색 프리셋(사용자 요청, 2026-08-28: '캐릭터를 흰색 or 검은색으로 선택할수있게'). " +
                 "배경이 어두운 바탕화면에서는 검은 캐릭터가 거의 보이지 않으므로 흰색이 필요하다. " +
                 "이 값만 바꾸면 프리팹을 다시 만들 필요 없이 런타임에 즉시 반영된다 — " +
                 "StickmanAgent.ApplyInkColorFromConfig()가 시작 시 모든 LineRenderer 색을 이 값으로 " +
                 "일괄 갱신하기 때문이다(Core/StickmanAgent.cs 참고).")]
        public StickmanInkColor inkColor = StickmanInkColor.Black;

        [Tooltip("inkColor == Black일 때 쓰는 실제 색. 기존 필드를 그대로 재사용하므로 지금까지의 " +
                 "모든 배선/문서가 무효화되지 않는다.")]
        public Color primaryOutlineColor = Color.black;

        [Tooltip("inkColor == White일 때 쓰는 실제 색.")]
        public Color whiteInkColor = Color.white;

        /// <summary>
        /// 지금 설정된 프리셋의 실제 선 색. 캐릭터를 그리는 모든 경로(에디터 프리팹 생성/런타임 갱신)는
        /// 반드시 이 메서드를 거쳐야 한다 — primaryOutlineColor를 직접 읽으면 프리셋 전환이 무시된다.
        ///
        /// [눈 색에 대한 결정과 근거 — 리더 질문에 대한 답]
        /// 눈동자 점도 **선과 같은 색**을 쓴다(반대색이 아니다). 이 캐릭터의 머리는 "링(테두리)만 있고
        /// 안쪽은 완전히 비어 바탕화면이 그대로 비치는" 구조라, 눈은 '얼굴 위의 무늬'가 아니라
        /// **배경 위에 찍힌 잉크 점**이다. 따라서 잉크와 같은 색일 때만 링과 함께 보인다:
        ///   - 검정 잉크 + 밝은 배경 -> 검은 링 안에 검은 점 두 개(현재 상태, 정상)
        ///   - 흰 잉크 + 어두운 배경 -> 흰 링 안에 흰 점 두 개(정상)
        /// 반대색으로 하면 정확히 망가진다: 흰 캐릭터인데 눈만 검정이면, 흰색이 필요한 이유였던 그
        /// **어두운 배경 위에 검은 점**을 찍는 셈이라 눈이 사라진다. 즉 "눈은 선과 같은 색"이 이 구조에서
        /// 유일하게 성립하는 선택이다.
        /// </summary>
        public Color ResolveInkColor()
            => inkColor == StickmanInkColor.White ? whiteInkColor : primaryOutlineColor;
        public Color dialogueBubbleColor = Color.white;

        [Tooltip("Main Camera 배경 RGB(알파는 0 = 완전 투명, Editor/SceneBootstrapper.cs 참고)의 밝은 " +
                 "배경색. 이력(2026-08-28): 자체 제작 Objective-C 플러그인으로 창 투명화를 시도하던 " +
                 "라운드들에서는 네이티브 투명화가 한 번도 성공하지 못해 알파=0 픽셀이 RGB와 무관하게 " +
                 "검정으로 합성되는 사고(완전히 균일한 검정 화면)가 재발했고, 그래서 알파를 1로 고정한 " +
                 "적이 있다. 이번 라운드에 자체 플러그인을 전부 제거하고 검증된 오픈소스 " +
                 "UniWindowController(com.kirurobo.uniwinc)로 교체하면서 알파 0을 다시 켰다. 다만 그 " +
                 "사고의 교훈인 방어책은 그대로 유지한다 — 알파만 0으로 두고 RGB는 이 밝은 회색을 " +
                 "유지하므로, 만에 하나 투명화가 또 실패하더라도 최악의 결과가 '밝은 회색 창 안의 검정 " +
                 "캐릭터'(최소한 보이는 상태)이지 '검정-on-검정'이 아니다. 같은 이유로 " +
                 "UniWindowController의 autoSwitchCameraBackground는 false로 꺼둔다(켜져 있으면 " +
                 "라이브러리가 배경을 Color.clear = RGB(0,0,0)로 덮어써 이 방어책을 무력화한다). " +
                 "primaryOutlineColor(검정) 캐릭터 선과 대비되는 밝은 배경. 매직 넘버를 코드에 직접 두지 " +
                 "않는다는 이 클래스 상단 컨벤션에 따라, 이전에 SceneBootstrapper.cs에 하드코딩돼 있던 " +
                 "동일 목적의 값(0.85,0.85,0.85)을 이 필드로 승격했다.")]
        public Color backgroundFallbackColor = new Color(0.94f, 0.94f, 0.94f);
    }
}
