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

        // walkBounceAmplitude 제거(2026-08-28 실측): "한 걸음마다 몸이 살짝 오르내린다"는 목적은 그대로지만,
        // 그 곡선을 손으로 적은 8키 표(진폭 0.025)로 두었더니 **기하학과 위상이 반대**여서 디딤발이 지면을
        // 최대 0.025유닛 파고들고 최대 0.070유닛 떠 있었다. 이제 진폭·위상을 전부 다리 각도에서 유도한다
        // (States/StickmanPoseAnimator.ComputeFootGroundingOffset 문서에 측정치와 유도 과정). 남은 조절
        // 여지는 아래 "얼마나 적용할 것인가" 하나뿐이다.

        [Tooltip("걷는 동안 '지금 땅에 닿아 있는 발이 지면에 정확히 붙어 있도록' 몸통을 상하로 보정하는 " +
                 "정도(0~1). 1이면 낮은 쪽 발이 항상 지면에 정확히 닿고(권장), 0이면 보정을 아예 하지 않아 " +
                 "몸이 상하로 전혀 움직이지 않는다(예전 동작으로 되돌리는 안전 스위치가 아니라 '흔들림 없음'). " +
                 "진폭 자체는 다리 길이와 관절 각도에서 자동으로 나오므로 여기서 정할 값이 아니다 — 실제 " +
                 "프리팹 치수에서 약 0.07유닛(전신 높이의 3%)이며, 사람이 걸을 때 엉덩이가 오르내리는 비율과 " +
                 "같은 수준이다. **시각 전용**이라 Rigidbody2D.position은 건드리지 않는다 — 접지 판정이 " +
                 "루트의 물리 위치를 발 높이로 쓰기 때문에 그걸 흔들면 접지 로직이 깨진다" +
                 "(States/StickmanPoseAnimator.SetBodyOffset 참고).")]
        [Range(0f, 1f)]
        public float walkFootGroundingBlend = 1f;

        [Tooltip("Idle에서 몸 전체가 호흡처럼 아주 느리게 오르내리는 진폭(월드 유닛). 완전 정지는 " +
                 "\"얼어붙은 것\"처럼 보이므로 항상 미세하게 살아있게 만든다. walkFootGroundingBlend와 같은 " +
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

        // ────────────────────────────────────────────────────────────────────────────────────
        // ★ 2026-09-01 (P9-b) RAGDOLL 진입 충격량 환산 — "얻어맞으면 팔다리가 크게 튕긴다"
        // ────────────────────────────────────────────────────────────────────────────────────
        // RagdollRig.EnterRagdoll(방향, 충격량)은 P9-a에서 만들어졌지만 아무도 호출하지 않았다(배관만
        // 있고 물이 안 흘렀다). 이제 RagdollState.Enter()가 호출하는데, 그때 **판정용 충격량
        // (ragdollForceThreshold와 비교하는 그 값)을 그대로 넘기면 안 된다**:
        //
        //   실측(2026-09-01, Tests/PlayMode/RagdollEntryEnergyTests의 감도 로그):
        //   가슴 지점에 1N·s를 가하면 루트에 약 42.8도/초가 실린다. 임계값의 5배(40N·s)를 그대로
        //   넘기면 진입 각속도가 약 1712도/초 = **초당 4~5바퀴**다. 얻어맞아 넘어지는 게 아니라 팽이다.
        //
        // 그래서 판정 단위(N·s)와 연출 단위(도/초)를 분리하고, 아래 세 값으로 환산한다. 셋 다 사람이
        // 읽을 수 있는 단위로 노출한 이유는 튜닝을 "곱하기 0.29" 같은 무의미한 숫자가 아니라
        // **"임계값에 딱 맞게 맞으면 몇 도/초로 넘어지는가"**로 하게 하기 위해서다.
        // 환산식은 States/RagdollImpactResolver.ResolveEntryImpulse() 한 곳에만 있다.
        [Tooltip("RAGDOLL 진입 연출 강도 ①: 충격량이 정확히 ragdollForceThreshold일 때 목표 진입 각속도(도/초). " +
                 "가장 약한 랙돌(긴 망토 걸려 넘어짐 = 임계값의 1.02배)이 이 값 근처로 들어온다. " +
                 "0 이하로 두면 진입 충격량 기능 전체가 꺼지고 P9-a 이전 거동(초기 에너지 = 이미 실려 있던 속도뿐)으로 돌아간다")]
        public float ragdollEntryAngularVelocityAtThreshold = 100f;

        [Tooltip("RAGDOLL 진입 연출 강도 ②: 진입 각속도 상한(도/초). 아무리 세게 맞아도 이 이상으로 회전하며 들어가지 않는다. " +
                 "기본값 400도/초 = 약 1.1회전/초. 상한이 없으면 임계값의 5배 타격에서 1700도/초(초당 5바퀴)가 나온다. " +
                 "AtThreshold의 4배로 두면 대사 3구간(1~2배/2~4배/4배 초과)의 마지막 구간이 시작되는 지점에서 정확히 포화한다")]
        public float ragdollEntryAngularVelocityCap = 400f;

        [Tooltip("리그 실측 상수: 가슴 지점에 1N·s의 충격량을 가할 때 루트에 실리는 각속도(도/초). " +
                 "연출 목표(도/초)를 물리 단위(N·s)로 되돌리는 환산 계수이며 튜닝 값이 아니다 — 캐릭터 질량/관성모멘트/지렛대가 " +
                 "바뀌면 다시 재야 한다. 재측정 방법: Tests/PlayMode/RagdollEntryEnergyTests가 로그에 '…N·s당 감도 …도/초/N·s'로 찍는다")]
        public float ragdollEntryAngularSensitivityPerImpulse = 42.8f;

        [Tooltip("Ragdoll -> Getup 진입 후 직립 포즈로 보간 완료까지 걸리는 기준 시간(초). GetupState._getupProgress가 이 값으로 정규화된다")]
        public float getupDuration = 0.6f;

        // getupMotorGain / getupMaxMotorTorque 제거(2026-08-28 근본 재구현): GETUP도 더 이상 관절
        // 모터로 몸을 일으키지 않는다. RagdollRig가 루트 회전각을, StickmanPoseAnimator가 팔다리 각도를
        // 각각 "널브러진 실제 각도 -> 직립 중립 각도"로 progress에 따라 직접 보간한다(100% 예측 가능,
        // 절대 실패하지 않음). getupDuration 하나로 전체 연출 시간이 결정된다.

        [Tooltip("★ GETUP 바닥 클리어런스 리프트(기본 ON, 2026-08-31 디버거 원인 확정). 기상 보간 " +
                 "중에는 팔다리가 Kinematic이라 콜라이더가 막아주지 않는데(RAGDOLL에는 있던 방어), " +
                 "접지 규약이 루트 원점(=발바닥)을 발판 상단에 못박으므로 아직 누워 있는 몸의 반대편 " +
                 "파츠가 발판 아래로 뚫고 나간다(실측 최악 발판 상단 아래 20.5pt). 켜면 매 프레임 " +
                 "'지금 이 포즈의 최저 잉크가 발판 상단에 정확히 닿는 데 필요한 만큼만' 루트를 들어 " +
                 "올린다 — 유도값이라 상수가 없고 progress->1이면 저절로 0이 된다. " +
                 "끄면 예전 관통이 그대로 재현된다(네거티브 컨트롤: GetupFloorClearanceTests).")]
        public bool getupFloorClearanceEnabled = true;

        [Header("파쿠르 (docs/UX_FLOW.md 4절)")]
        [Tooltip("ParkourClimb 진입 판정을 위한 벽/모서리 발판 감지 반경(벽으로 인정할 최소 높이차 + 인접 발판 " +
                 "탐색 폭). ★ 2026-08-31: 세 용도 중 '경계 근접 거리'만 여기서 분리되어 **유도값**이 되었다 — " +
                 "그 거리는 배회 AI가 경계 행동을 추첨하는 거리(StickmanBlackboard.EdgeStopDistanceWorld)와 " +
                 "반드시 짝이어야 하는데, 그쪽은 캐릭터 배율에서 유도되므로 배율 1.0을 넘으면 이 절대값을 " +
                 "추월해 되올라가기/내려가기가 구조적으로 성립 불가능해졌다(사용자 신고: 캐릭터를 키우면 " +
                 "Dock 위로 못 올라옴). 유도는 Core/DockGeometry.ResolveEdgeProbeReach이며, 배포 배율 0.75에서는 " +
                 "유도값이 정확히 이 값(0.5)과 같아 거동이 바뀌지 않는다. 나머지 두 용도는 판정 상대가 OS 창 " +
                 "사각형이라 그대로 절대값이다.")]
        public float parkourDetectionRadius = 0.5f;

        [Tooltip("ParkourClimb 상태에서 매달린 위치부터 벽 상단까지 올라가는 데 걸리는 기준 시간(초)")]
        public float parkourClimbDuration = 0.5f;

        [Tooltip("착지 시 이 값(월드 유닛) 이상 낙하했으면 구르기(부드러운 착지) 이벤트를 발행한다. 실제 파티클/애니메이션은 Phase 2+ 렌더링 레이어 담당")]
        public float rollLandingHeightThreshold = 2f;

        // ── 매달려 내려가기(LedgeHang) — UX_FLOW.md 4절 "매달리기(HANG)" 행의 하강 방향 구현.
        //    사용자 명시 요청(2026-08-28): "내려갈때도 매달려서 내려가는형태로".
        //    States/LedgeHangState.cs가 소비한다. 안전 규칙(발판 소실 즉시 낙하 / 화면 밖 금지 /
        //    무한 매달림 금지)은 설정값이 아니라 상태 코드의 불변식이라 여기에 스위치를 두지 않는다 —
        //    유일하게 조절 가능한 것은 "얼마나 자주, 얼마나 오래" 매달리느냐뿐이다.

        [Tooltip("발판 가장자리에 도달했을 때 (그냥 돌아서지 않고) 모서리를 붙잡고 매달려 내려갈 확률 " +
                 "(0~1). 항상 매달리면 지루하므로 기본값은 절반 이하로 둔다 — 나머지 확률은 기존 " +
                 "배회 행동(정지 후 반대 방향 전환)으로 그대로 흡수된다. 0이면 이 동작이 완전히 꺼지고 " +
                 "직전 라운드까지의 거동과 100% 동일해진다(안전한 되돌리기 스위치).\n" +
                 "매달릴 아래쪽 발판이 실제로 존재할 때만 추첨하므로, 확률을 1로 올려도 화면 최하단 " +
                 "안전망 위에서는 발동하지 않는다(내려갈 곳이 없으므로).")]
        [Range(0f, 1f)]
        public float ledgeHangChance = 0.35f;

        [Tooltip("가장자리에서 몸을 낮춰 모서리를 붙잡기까지 걸리는 시간(초). 이 구간 동안 루트가 " +
                 "'서 있던 위치 -> 매달린 위치'로 보간되므로, 너무 짧으면 순간이동처럼 보인다.")]
        public float ledgeHangGrabDuration = 0.28f;

        [Tooltip("모서리를 붙잡은 뒤 손을 놓기까지 매달려 있는 시간(초)의 최솟값. 실제 유지시간은 " +
                 "[min, max] 사이의 난수라 매번 조금씩 다르다.")]
        public float ledgeHangHoldDurationMin = 0.55f;

        [Tooltip("매달려 있는 시간(초)의 최댓값. 위 min과 함께 난수 구간을 이룬다.")]
        public float ledgeHangHoldDurationMax = 1.5f;

        [Tooltip("★ 안전 규칙(무한 매달림 금지) — 잡기+매달림 전체에 걸리는 절대 상한(초). 어떤 이유로 " +
                 "페이즈 타이머가 진행되지 않아도 이 시간이 지나면 무조건 손을 놓고 Fall로 전이한다. " +
                 "위 hold 최댓값보다 반드시 커야 의미가 있다(UX_FLOW.md 4절 '무한 대기 금지').")]
        public float ledgeHangMaxDuration = 3f;

        [Tooltip("매달린 자세에서 몸이 모서리 바깥으로 나가는 거리(월드 유닛). 0이면 모서리 선 위에 " +
                 "정확히 걸쳐 매달리고, 값이 커질수록 발판 바깥쪽으로 더 나가 매달린다. 손을 놓으면 " +
                 "이 X에서 그대로 낙하하므로 발판 아래로 되돌아 떨어지지 않게 하는 역할도 겸한다.")]
        public float ledgeHangEdgeOffset = 0.14f;

        [Tooltip("매달린 자세에서 양팔을 위로 뻗을 때 수직에서 바깥쪽으로 벌리는 각도(도). 0이면 두 팔이 " +
                 "완전히 수직이라 몸통 선과 겹쳐 팔이 안 보인다(Idle 실루엣과 같은 이유로 약간 벌린다).")]
        public float ledgeHangArmSpreadDegrees = 11f;

        [Tooltip("매달린 자세의 팔꿈치 굽힘(도). 완전히 편 팔은 '막대기'로 보이므로 항상 조금 굽혀둔다.")]
        public float ledgeHangElbowBendDegrees = 8f;

        [Tooltip("매달린 자세에서 다리가 아래로 늘어질 때의 벌림(도)과 무릎 굽힘(도). 몸이 매달려 " +
                 "축 늘어진 느낌을 주려면 Idle보다 다리를 모으고 무릎을 조금 더 굽혀야 한다.")]
        public float ledgeHangLegSpreadDegrees = 6f;

        [Tooltip("매달린 자세의 무릎 굽힘(도). 위 ledgeHangLegSpreadDegrees와 짝이다.")]
        public float ledgeHangKneeBendDegrees = 14f;

        [Tooltip("매달린 동안 몸이 좌우로 흔들리는 진폭(도)과 주파수(Hz) 중 진폭. UX_FLOW.md 4절이 " +
                 "요구한 '대롱대롱, 미세한 흔들림'을 만든다. 0이면 흔들림 없이 정지한다.")]
        public float ledgeHangSwayAmplitudeDegrees = 5f;

        [Tooltip("매달린 흔들림의 주파수(Hz). 0.9면 약 1.1초에 한 번 좌우 왕복한다.")]
        public float ledgeHangSwayFrequencyHz = 0.9f;

        // ── 낙차가 작을 때 "그냥 뛰어내리기"(HopDown)와 낮은 턱 "되올라가기"(StepUp)
        //    ─ 2026-08-29 사용자 결정: "낙차가 작으면 뛰어내리게 한다".
        //
        //    왜 필요했나: 위 매달리기(LedgeHang)는 물리적으로 **손끝~발끝 거리(약 2.5유닛)** 이상
        //    떨어져 있는 발판으로만 내려갈 수 있다(그보다 가까우면 매달리는 순간 발이 이미 목적지를
        //    지나쳐 버린다 — GroundSensor.TryFindDescendTarget 문서 참고). 그래서 macOS Dock 상단→
        //    바닥 안전망처럼 낙차가 1.6375유닛뿐인 단차에서는(배율 0.75 기준 매달리기 최소치 1.880 미만)
        //    매달리기 판정에 걸리지 않아, 캐릭터가
        //    Dock 경계에서 그냥 되돌아설 뿐 스스로 내려오지 못했다(2026-08-29 라운드의 미해결 항목).
        //
        //    설계: 발판 경계에서 "아래에 내려앉을 발판이 있다"가 확인되면 **낙차 크기로 두 갈래**로
        //    나눈다. 두 구간은 서로 겹치지도 비지도 않는다(hopDownMaxDropHeight의 기본값이 곧
        //    매달리기 최소 낙차이므로) — 즉 "매달릴 만큼 깊으면 매달리고, 아니면 뛰어내린다".
        //      · 낙차 >= 매달리기 최소치           -> LedgeHang (기존 그대로)
        //      · hopDownMinDropHeight <= 낙차 < 그 -> 그냥 앞으로 내딛으며 낙하(Fall) = 이 블록
        //    안전 규칙(화면 밖 금지 / 무한 낙하 금지)은 기존과 동일하게 상태 코드의 불변식이며
        //    (FallState + StickmanBlackboard.EnforceScreenBoundsAndRescue), 여기 스위치는 오직
        //    "얼마나 자주, 어느 낙차에서" 하느냐만 정한다.

        [Tooltip("발판 경계에서 낙차가 작을 때(매달릴 이유가 없는 한 계단 턱) 그냥 앞으로 뛰어내릴 확률(0~1). " +
                 "1로 두면 Dock 같은 낮은 발판 위에 머무르지 못하고 경계에 닿는 족족 내려가 버리므로 " +
                 "절반 정도가 적당하다 — 나머지 확률은 기존 배회 행동(정지 후 반대 방향 전환)이 흡수한다. " +
                 "0이면 이 동작이 완전히 꺼지고 2026-08-29 이전 거동(경계에서 되돌아섬)과 100% 동일해진다.")]
        [Range(0f, 1f)]
        public float hopDownChance = 0.5f;

        [Tooltip("★ 뛰어내리기 낙차 하한(월드 유닛). 아래 발판과의 높이차가 이 값보다 작으면 그냥 " +
                 "이어진 바닥이나 다름없어 '내려간다'는 동작 자체가 성립하지 않는다(연출도 안 보이고, " +
                 "접지 허용오차 안이라 착지 판정이 흔들린다). macOS Dock 단차(1.6375유닛, tilesize 49 기준. " +
                 "가장 작은 tilesize 16에서도 0.83유닛)는 이 값보다 충분히 크므로 정상적으로 뛰어내린다.")]
        public float hopDownMinDropHeight = 0.35f;

        [Tooltip("★ 뛰어내리기 낙차 상한(월드 유닛) — 이 값 **이상**이면 뛰어내리지 않고 매달리기" +
                 "(LedgeHang) 쪽으로 넘긴다. 0 이하로 두면 '매달리기 최소 낙차'(손끝~발끝 거리, " +
                 "StickmanBlackboard.LedgeHangMinDropDepth에서 프리팹 치수로 자동 유도 — 현재 약 2.5유닛)를 " +
                 "그대로 쓴다. 기본값 0을 권장한다: 두 구간이 자동으로 정확히 맞물려 '틈(아무 것도 안 함)'도 " +
                 "'겹침(둘 다 성립)'도 생길 수 없기 때문이다.")]
        public float hopDownMaxDropHeight = 0f;

        [Tooltip("★ '내려앉을 발판이 있는가'를 물어볼 때 발판 경계 바깥으로 얼마나 나간 지점을 " +
                 "탐침으로 쓸지(월드 유닛). **탐침 전용 값이다** — 2026-08-29 이전에는 실제로 몸을 " +
                 "이 지점까지 순간이동시키는 데에도 같은 값이 쓰였지만(한 프레임에 0.31유닛 = 약 25pt를 " +
                 "건너뛰어 순간이동처럼 보였다), 지금은 몸을 전혀 옮기지 않는다(States/WalkState.cs의 " +
                 "'발을 뗍니다' 블록 주석 참고 — drop-through 방식으로 대체). 그래서 이 값은 '경계 " +
                 "바깥 어디를 기준으로 아래 발판을 찾을 것인가'만 정하며, 너무 크면 모서리에서 멀리 " +
                 "떨어진 발판을 목적지로 착각하고 너무 작으면 경계선 위의 수치 오차에 걸린다.")]
        public float hopDownProbeOutward = 0.2f;

        [Tooltip("★ 뛰어내린 직후, **방금 떠난 그 발판**을 착지 후보에서 제외해두는 시간(초). " +
                 "플랫포머의 drop-through(아래로 내려가기) 관행과 같은 장치다.\n" +
                 "왜 필요한가: 서 있는 몸은 발판 상단선에 정확히 스냅돼 있어서, 모서리를 아직 넘지 않은 " +
                 "채 Fall로 전이하면 FallState의 스윕 교차 판정이 방금 떠난 그 발판을 '위에서 아래로 " +
                 "관통했다'고 인정해 제자리에 도로 착지시킨다(2026-08-29 실측: 낙하높이 0.00유닛). " +
                 "예전에는 이를 피하려고 몸을 모서리 바깥으로 순간이동시켰지만, 순간이동은 사용자가 " +
                 "반복적으로 지적해온 아티팩트라 '착지 후보에서 잠깐 제외'로 바꿔 전진량을 0으로 만들었다.\n" +
                 "필요한 최소 시간은 '몸이 수평으로 모서리를 넘어가는 데 걸리는 시간' = " +
                 "hopDownEdgeCommitDistance / (walkSpeed x hopDownStepOffSpeedScale) ≒ 0.12/2.0 = 0.06초다. " +
                 "기본값은 그 4배 이상의 여유를 둔다. 이 시간이 지나도 여전히 그 발판 위에 있으면 " +
                 "그냥 제자리 착지로 되돌아갈 뿐이라(자기회복) 과도하게 길게 잡을 이유는 없다.")]
        public float hopDownDropThroughIgnoreDuration = 0.25f;

        [Tooltip("뛰어내리기로 결정한 뒤, 모서리까지 남은 거리가 이 값(월드 유닛) 이하가 되어야 실제로 " +
                 "발을 뗀다. 경계 판정 거리(wanderEdgeStopDistance=0.3)에서 곧바로 떼면 아직 발판 한복판인데 " +
                 "낙하가 시작돼 '바닥을 뚫고 내려가는' 것처럼 보이므로, 모서리 코앞까지 걸어가게 하는 것이 " +
                 "이 값의 역할이다(2026-08-29 이전에는 여기에 더해 '순간이동 폭을 줄인다'는 역할도 겸했는데, " +
                 "그 순간이동 자체가 사라져 지금은 연출 목적만 남았다).\n" +
                 "너무 작게 잡으면 프레임당 이동거리(walkSpeed 2.5 기준 60fps에서 약 0.04유닛, 30fps에서 " +
                 "약 0.08유닛)보다 작아져 이 창을 건너뛸 수 있는데, 그때는 그냥 걸어서 모서리를 넘어가 " +
                 "자연 낙하하므로(결과는 같다) 안전 문제는 없다. 기본값은 30fps 한 프레임보다 크게 잡아 " +
                 "저프레임에서도 뛰어내리기가 조용히 무효화되지 않게 한다.")]
        public float hopDownEdgeCommitDistance = 0.12f;

        [Tooltip("뛰어내릴 때 앞으로 내딛는 수평 속도를 walkSpeed의 몇 배로 줄 것인지. 1이면 걷던 속도 " +
                 "그대로 걸어 나가듯 떨어지고, 작을수록 모서리에 가깝게 수직으로 떨어진다. 너무 작으면 " +
                 "발판을 벗어나기 전에 아래로 가라앉아 제자리 착지가 될 수 있다(0.5 미만 비권장).")]
        public float hopDownStepOffSpeedScale = 0.8f;

        [Tooltip("★ 뛰어내린 뒤 **다시 올라오기** — 발판 경계 앞에 낮은 턱(벽)이 있을 때 스스로 기어오를 " +
                 "확률(0~1). 이 값이 0이면 한 번 Dock 아래로 내려간 캐릭터가 영영 못 올라온다(경계 점프 " +
                 "확률 wanderEdgeJumpAttemptChance가 기본 0이라 ParkourClimb를 유발할 다른 경로가 없다). " +
                 "실패한 나머지 확률은 기존 배회 행동(정지 후 반대 방향)으로 흡수된다.\n" +
                 "★ 2026-08-29 0.5 -> 0.85 상향(사용자가 이 구간을 세 차례 신고). 좁은 바닥 조각 안에서 " +
                 "Dock 모서리에 실제로 닿는 간격이 실측 약 18초라, 0.5에서는 기대 대기시간이 약 36초로 " +
                 "체감상 \"안 올라온다\"처럼 느껴졌다. 0으로 완전히 결정론적으로 만들지 않고 0.85로 " +
                 "남긴 이유는 가끔 실패해 반대 방향으로 도는 것도 배회의 자연스러운 변주이기 때문이다.")]
        [Range(0f, 1f)]
        public float stepUpChance = 0.85f;

        [Tooltip("스스로 기어오를 최대 턱 높이(월드 유닛). 이보다 높은 벽은 자율 배회로는 오르지 않는다 — " +
                 "ParkourClimb는 높이와 무관하게 parkourClimbDuration(0.5초)에 올라가므로, 높은 벽까지 " +
                 "자동으로 오르게 두면 순간이동처럼 보인다.\n" +
                 "★ 2026-08-29 재보정(사용자 신고 \"독 아래 떨어져있으면 계속 거기에서만 왔다갔다함\") — " +
                 "원래 1.5는 Dock 단차를 0.855유닛으로 보고 잡은 값인데, 같은 라운드에서 (a) 바닥 안전망을 " +
                 "화면 최하단 40pt 위 -> 8pt 위로 내리고(BottomSafetyNetInsetPoints) (b) Dock 두께를 " +
                 "하드코딩 75pt에서 tilesize+26 실측 파생으로 바꾸면서, 두 변경이 겹쳐 Dock 상단~안전망 " +
                 "상단 낙차가 실측 67pt(구 기하 35pt=0.855유닛 대비 약 2배)로 벌어졌다 — 실측: " +
                 "Dock 상단 OS y=907, 안전망 상단 OS y=974, 환산 1.637유닛. 1.5로는 더 이상 이 낙차를 " +
                 "덮지 못해 TryFindClimbableWall이 실패하고, 한 번 뛰어내리면 영영 못 올라온다(뛰어내리기 " +
                 "밴드는 여전히 이 낙차를 포함해 내려가기는 계속 성립하므로 왕복의 절반만 깨진다).\n" +
                 "Dock 타일 크기(tilesize)는 사용자 설정에 따라 달라 낙차도 함께 변하므로 한 번 더 여유를 " +
                 "둔 2.4로 올린다. 그래도 일반 창 발판(수백 pt 이상)까지 순간이동처럼 자동으로 오르지는 않는다.\n\n" +
                 "★★ 2026-08-30 (횡단 리뷰 M3) — 위 괄호의 \"작은 타일 ~1.2 ~ 큰 타일 ~2.2 추정\"이 **틀렸다**. " +
                 "실제 macOS tilesize 범위는 16~128이고 낙차 = tilesize + 18pt이므로 유닛 환산은 " +
                 "0.83(16) / 1.61(48, macOS 기본) / 1.64(49, 이 개발 머신) / 2.40(80) / 3.57(128)이다. " +
                 "즉 tilesize 80부터 이 값 2.4를 넘어서고, 128이면 1.5배 넘게 초과한다 — Dock 아이콘을 크게 " +
                 "쓰는 사용자에게 \"한 번 내려가면 영영 못 올라온다\"가 그대로 남아 있었다.\n" +
                 "그래서 이 값은 이제 **절대 상한이 아니라 하한**이다: 실제 상한 = max(이 값, 실측 Dock 낙차 " +
                 "+ 0.30유닛)을 States/AutoWanderController.ResolveStepUpMaxHeight()가 프레임마다 유도한다. " +
                 "실측은 새 OS 조회가 아니라 이미 열거된 발판 두 개(Dock 띠 / 바닥 안전망)의 상단 Y 차이라 " +
                 "권한·성능·좌표계 위험이 하나도 늘지 않는다. Dock을 못 찾으면(자동 숨김 / 세로 Dock / " +
                 "비-macOS / 전체화면 감지 중) 이 값 그대로 폴백한다. 유도식은 Core/DockGeometry.cs.")]
        public float stepUpMaxHeight = 2.4f;

        [Tooltip("ParkourClimb로 턱 위에 올라선 뒤, 그 발판 안쪽으로 얼마나 들어가 설지(월드 유닛). " +
                 "0이면 모서리 선 위에 정확히 서게 되어 접지 판정이 경계에서 흔들리고 곧바로 다시 떨어진다. " +
                 "발판이 이 값보다 좁으면 반대편 끝을 넘지 않도록 자동으로 좁혀진다.\n" +
                 "★ 2026-08-29 재보정 0.25 -> 0.45 (사용자 신고 \"독 위로 가끔 올라오긴 하지만 바로 다시 " +
                 "내려감\"). 0.25는 배회 AI의 경계 판정 거리 wanderEdgeStopDistance(0.30)보다 **작아서**, " +
                 "등반이 끝나 올라선 그 자리가 이미 '경계 근처'였다 — 실측 로그: 맨틀 X=13.326, Dock " +
                 "오른쪽 모서리 X=13.576, 남은 거리 0.250 <= 0.300. 그래서 올라선 직후 진행 방향이 바깥으로 " +
                 "뒤집히기만 하면 다음 프레임에 곧바로 뛰어내리기 추첨이 돌았다. 이 값은 반드시 " +
                 "wanderEdgeStopDistance보다 커야 하며, 그 불변식은 Tests/EditMode에서 잠가 둔다.\n" +
                 "다만 이 값만으로는 증상이 사라지지 않는다(모서리에서 조금 더 걸어가야 할 뿐 결국 같은 " +
                 "추첨이 돈다) — 실제 원인 수정은 아래 postClimbDescendCooldown이다.\n" +
                 "★★ 2026-08-30 R3-M1 재보정 0.45 -> 0.60. 위 불변식의 상대는 이제 wanderEdgeStopDistance " +
                 "**설정값(0.30)이 아니라 유도값**이다 — 경계 판정 거리가 몸의 물리 반폭에서 유도되도록 " +
                 "바뀌면서(Core/DockGeometry.ResolveEdgeStopDistance) 배율 0.75에서 0.405가 됐고, " +
                 "유휴 '주위 살피기'가 머리를 최대 0.06유닛 밀 수 있는 것까지 감안하면 0.45로는 여유가 " +
                 "0.045 이하로 쪼그라든다(= 예전의 0.005 근접 충돌과 같은 종류의 함정). 0.60은 그 여유를 " +
                 "0.195로 되돌린다. 화면상 약 6pt 더 안쪽에 설 뿐이고, 올라선 뒤 더 안전한 자리라 " +
                 "되내려감 방지에도 유리하다.")]
        public float parkourMantleInset = 0.6f;

        [Tooltip("★ 2026-08-31 — 위 맨틀 인셋을 **유도값**으로 쓸 것인가(Core/DockGeometry." +
                 "ResolveParkourMantleInset). 켜져 있으면 위 값은 **하한**이고, 경계 판정 거리 + 여유가 " +
                 "그보다 크면 그쪽이 이긴다.\n" +
                 "왜 필요한가: 캐릭터 크기 다이얼(docs/UX_FLOW.md 34-3)이 배율을 런타임에 0.35~2.00으로 " +
                 "바꾼다. 고정값 0.60이 불변식(맨틀 인셋 > 경계 판정 거리)을 지킬 수 있는 천장은 배율 " +
                 "1.125뿐이라, 그 위에서는 '올라선 자리가 이미 경계'가 다시 성립한다.\n" +
                 "★ 이 스위치를 끄면 유도가 통째로 꺼져 위 절대값을 그대로 쓴다 = 유도 도입 이전 거동으로 " +
                 "정확히 되돌아간다. postClimbDescendCooldown = 0과 같은 성격의 **네거티브 컨트롤 전용 " +
                 "스위치**이며, Tests/PlayMode/EdgeHopDownTests가 옛 회귀를 재현할 때 이 둘을 함께 끈다. " +
                 "배포 에셋에서 이 값이 true인지는 Tests/EditMode/WanderEdgeConfigInvariantTests가 잠근다.")]
        public bool parkourMantleInsetDerived = true;

        [Tooltip("★ 되올라간 직후 **다시 내려가는 행동(뛰어내리기/매달려 내려가기)을 유예**하는 시간(초). " +
                 "0 이하면 이 기능 전체가 꺼져 2026-08-29 이전 거동으로 정확히 되돌아간다(네거티브 컨트롤용 스위치).\n" +
                 "왜 필요한가(사용자 신고 \"독위로 가끔 올라오긴 하지만 바로 다시 내려감\"의 실측 원인): " +
                 "등반을 유발한 경계 판정이 등반 중에도 그대로 살아 있어 배회 AI가 경계 정지" +
                 "(wanderEdgeTurnPause 0.3~0.8초)를 걸고, 그 정지가 등반(0.5초) 도중 끝나면서 진행 방향을 " +
                 "**방금 올라온 바깥쪽으로 반전**시키고 경계 행동 추첨권까지 리셋했다. 그 결과 턱 위에 " +
                 "올라선 지 9프레임(약 0.15초) 만에 같은 모서리로 다시 뛰어내렸다(실측 로그 frame 8982 -> 8991).\n" +
                 "이 값이 0보다 크면 맨틀 완료 신호(StickmanBlackboard.ClimbMantleSequence)를 받은 배회 AI가 " +
                 "(1) 진행 중이던 경계 정지/뛰어내리기 확약을 취소하고 (2) 진행 방향을 **올라선 방향(턱 " +
                 "안쪽)** 으로 되돌려 새 걷기 구간을 시작하며 (3) 이 시간 동안 내려가는 갈래만 추첨에서 " +
                 "제외한다. 되올라가기(step-up)와 경계에서 돌아서기는 그대로 동작하므로 화면 밖으로 " +
                 "걸어 나가는 경로는 생기지 않는다.\n" +
                 "기본값 8초의 근거(임의의 숫자가 아니다): 배회 한 사이클의 최악값 = 서기 최대" +
                 "(wanderIdleDurationMax=6.0) + 걷기 최소(wanderWalkDurationMin=1.5) = 7.5초. 유예가 이보다 " +
                 "짧으면 '올라와서 한 번 쉬고 한 번 걷는' 첫 왕복 안에 유예가 끝나 체감상 여전히 금방 " +
                 "내려간다. 그 7.5초에 여유를 얹은 값이며, 이 대소 관계는 Tests/EditMode/" +
                 "WanderEdgeConfigInvariantTests가 단언한다(위 세 값 중 하나를 바꾸면 그 테스트가 알려준다).")]
        public float postClimbDescendCooldown = 8f;

        [Header("비침해 원칙")]
        [Tooltip("클릭 관통 기본 ON 여부 (원칙 2)")]
        public bool clickThroughDefaultEnabled = true;

        [Tooltip("발판 목록을 다시 열거(폴링)하는 주기(초). 매 프레임 열거 금지 — 반드시 이 주기로 제한")]
        public float footholdPollInterval = 0.3f;

        [Tooltip("전체화면 게임 감지(IsFullscreenAppActive)를 다시 확인하는 주기(초). 발판 폴링과 별도 주기로 관리한다")]
        public float fullscreenPollInterval = 1.5f;

        [Header("좌표계 변환 (Platform/ScreenCoordinateConverter.cs 참고)")]
        [Tooltip("[보통은 0으로 두세요 — 자동] Unity 화면 픽셀 ↔ OS 데스크톱 포인트 배율의 수동 오버라이드.\n" +
                 "• 0 이하(기본): 자동. ScreenCoordinateConverter가 우리 창의 OS 포인트 폭 / Screen.width로 " +
                 "매 발판 폴링마다 실측한다(Retina 2x면 0.5, 비Retina면 1.0). 외장 모니터로 옮겨도 자동 추종한다.\n" +
                 "• 0보다 큰 값: 그 값을 자동 산출 대신 강제로 쓴다. 자동 산출이 통하지 않는 환경을 " +
                 "디버깅할 때만 쓰는 탈출구다 — 켜 두면 모니터를 바꿔도 갱신되지 않는다.\n" +
                 "이 값을 직접 읽지 말 것: 소비자는 반드시 ScreenCoordinateConverter.ResolveDpiScale(config)를 " +
                 "거친다(좌표 변환 단일 소스 컨벤션 BUG-M5).")]
        public float desktopDpiScale = 0f;

        // ================================================================================
        // ★★ "OS-px 필드" 단위 규약 — 결론: **전부 OS 포인트다** (2026-08-29 Retina 대응 라운드, 리더 지시 4항)
        // ================================================================================
        // 대상 8개 필드(Assets/Editor/SceneBootstrapper.cs의 BUG-SW-M2 경고가 지목한 목록):
        //   groundSnapTolerance / wanderCursorReactionRadiusPx / rodeoStillRadiusPx / rodeoReachDistancePx /
        //   graffitiMinRadiusPx / graffitiMaxRadiusPx / graffitiRegionSizePx / runawayHideSpotMarginPx
        // (+ 코드 상수 States/StickmanBlackboard.ScreenClampMarginOsPx도 같은 규약이다.)
        //
        // 지금까지 이름과 주석이 "OS 화면 픽셀"이라고 적혀 있었지만, `macRetinaSupport`가 꺼져 있어
        // 포인트와 픽셀이 우연히 같았을 뿐이라 실제로는 검증된 적이 없는 표기였다. 이번 라운드에 각
        // 소비자를 전수 추적해 확정한 결과는 다음과 같다 — **모두 OS 포인트**다:
        //
        //   · groundSnapTolerance      : States/GroundSensor.Sense()에서 `footOs.y`(WorldToOsScreen 결과,
        //                                이미 dpi가 곱해진 OS 포인트)와 `foothold.ScreenRect.y`(CGWindowBounds,
        //                                OS 포인트)의 차이와 직접 비교된다. 양쪽이 포인트이므로 이 값도 포인트.
        //   · rodeoStillRadiusPx       : Interaction/RodeoCursorWatcher가 CGEventGetLocation(OS 포인트)끼리의
        //     rodeoReachDistancePx       거리, 그리고 그 커서와 WorldToOsScreen(OS 포인트)의 거리에 쓴다.
        //   · graffiti*Px              : Interaction/GraffitiDirector가 `Screen.width * dpi`(= OS 포인트 폭)와
        //     runawayHideSpotMarginPx    Interaction/RunawayDirector가 같은 식으로 만든 화면 사각형 안에서 쓴다.
        //   · wanderCursorReactionRadiusPx : 아직 소비자가 없는 예약 필드지만, 이름이 가리키는 커서 좌표가
        //                                OS 포인트이므로 같은 규약으로 확정해 둔다.
        //
        // ★ 그래서 `macRetinaSupport`를 켠 뒤에도 이 8개 값은 **하나도 바꾸지 않았다**. 이유:
        //   Screen.width/height가 2배(3024x1964)가 되는 동시에 자동 산출된 dpi 배율이 0.5가 되어
        //   `Screen.width * dpi`가 정확히 예전 값(1512)을 유지하고, WorldToOsScreen도 `* dpi`로 같은
        //   포인트 공간을 돌려준다. 즉 이 필드들이 사는 좌표 공간 자체가 Retina 전후로 불변이다.
        //   (물리적 크기도 불변이다 — 20pt는 Retina에서도 20pt다.)
        //
        // ⚠ 반대로 여전히 살아 있는 함정: **카메라 orthographicSize를 바꾸면** 이 값들의 "월드 환산 크기"가
        //   달라진다(월드유닛당 포인트 = 창높이[포인트] / (2*orthographicSize)). 그건 DPI와 무관한 별개의
        //   종속성이며, SceneBootstrapper의 BUG-SW-M2 경고는 그 의미로 계속 유효하다.

        [Tooltip("캐릭터 발 위치(OS 좌표)와 발판 상단 사이 허용 오차(**OS 포인트**). 이 범위 안이면 접지로 판정. " +
                 "단위 근거는 아래 \"OS-px 필드 단위 규약\" 블록 참고 — Retina를 켜도 값을 바꿀 필요가 없다.\n\n" +
                 "★ 2026-08-30: 코드 기본값을 6 -> 20으로 올려 배포 에셋(DefaultStickConfig.asset)과 통일했다. " +
                 "실행 경로는 원래 에셋값 20을 썼으므로 거동 변화는 **0**이다. 고친 이유는 지뢰였기 때문이다 — " +
                 "CreateInstance<StickConfig>()로 설정을 만드는 테스트 10곳이 매번 손으로 20을 넣어 줘야 했고, " +
                 "한 곳이라도 빠뜨리면 접지 밴드가 0.489 -> 0.147유닛으로 3.3배 좁아져 '가끔만 접지에 실패하는' " +
                 "재현 어려운 실패가 난다(2026-08-30 횡단 리뷰 m3). 두 값의 일치는 " +
                 "Tests/EditMode/DockGeometryInvariantTests가 매 실행마다 잠근다.")]
        public float groundSnapTolerance = 20f;

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

        [Tooltip("진행 방향 앞쪽, 지금 딛고 있는 발판의 잔여 길이가 이 값(유닛) 이하이면 경계 도달로 판정. 26-2.\n" +
                 "★★ 2026-08-30 R3-M1 — 이 값은 이제 **절대값이 아니라 하한**이다. 실제 판정 거리 = " +
                 "max(이 값, 몸의 물리 반폭 + 0.10)을 States/StickmanBlackboard.EdgeStopDistanceWorld가 " +
                 "유도한다(유도식/근거: Core/DockGeometry.ResolveEdgeStopDistance).\n" +
                 "왜 바뀌었나: Dock 물리 계단(Platform/DockPhysicsStep.cs)의 옆면은 바닥 안전망 조각의 " +
                 "논리 경계와 **정확히 같은 X**에 선다. 그 벽에 막혀 선 캐릭터의 루트는 몸의 물리 반폭" +
                 "(배율 0.75에서 0.300 + Box2D 접촉 이격 0.005 = 0.305) 아래로 절대 다가가지 못하는데, " +
                 "이 값이 0.300이라 **경계 밴드가 물리적으로 도달 불가능**했다. 되올라가기 판정을 " +
                 "평가할 기회조차 없이 그 걷기 구간이 끝날 때까지 벽에 붙어 있었고, 그것이 사용자가 " +
                 "세 번 신고한 \"Dock 근처에서 멈춰 있음 / 안 올라감\"이다. 이격은 0.4 x 배율이므로 " +
                 "배율 0.7375 이상에서는 이 상수가 항상 진다 — 배포 배율 0.75는 그 절벽 바로 위였다.")]
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

        [Tooltip("커서 근접 반응 트리거 반경(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고). Phase 2로 연기됨(26-4) — " +
                 "지금은 필드만 예약, AutoWanderController가 아직 소비하지 않음.")]
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

        [Tooltip("판정 주기마다 자동으로 격파 미니게임이 발동할 확률(0~1). 임시 추정치 — 체감 빈도로 튜닝 필요. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.05.")]
        public float battleAutoTriggerChance = 0f;

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

        [Tooltip("커서가 '정지'로 간주되는 이동 반경(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고). 이 반경 안의 흔들림은 무시.")]
        public float rodeoStillRadiusPx = 5f;

        [Tooltip("커서가 이만큼(초) 연속으로 정지 상태를 유지하면 로데오 커서가 발동.")]
        public float rodeoStillTriggerSeconds = 5f;

        [Tooltip("트리거 시점에 캐릭터와 커서 사이 거리가 이 값(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고) " +
                 "이내여야 '도달 가능'으로 판정해 발동한다.")]
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

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 명시값 2~4%. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.03.")]
        public float windowTheftChance = 0f;

        [Tooltip("종료 후 다음 발동까지의 최소 쿨다운(초). UX 명시값 15분.")]
        public float windowTheftCooldownSeconds = 900f;

        [Tooltip("대상 창 선정 기준 — 창 폭이 캐릭터 신장(OS 화면 픽셀 환산)의 이 배수 이하여야 후보로 " +
                 "채택된다. 작은 창일수록 '정말 밀리는 듯' 긴장감이 산다(27-1 근거).")]
        public float windowTheftMaxTargetWidthMultiplier = 3f;

        [Tooltip("대상 창 폭 상한의 **절대 하한**(OS 포인트). 최종 상한 = max(캐릭터 신장 x " +
                 "windowTheftMaxTargetWidthMultiplier, 이 값).\n" +
                 "★ 2026-08-29 신설 — 상한을 캐릭터 신장에만 비례시키면 characterScale(순수 시각 설정)이 " +
                 "게임플레이 조건(어떤 창을 훔칠 수 있는가)을 조용히 바꾼다. 실측: 배율 1.0에서 상한 279pt였던 것이 " +
                 "배율 0.75에서 237pt(신장 79.0pt x 3), 배율 0.5에서는 158pt까지 떨어져 macOS 표준 창 최소 폭" +
                 "(계산기 실측 230pt, Finder 483pt)보다 좁아진다 = 후보가 항상 0개 = 기능이 조용히 죽는다.\n" +
                 "값 근거: 배율 1.0 시절의 원래 상한 279pt를 기준선으로, 계산기(230pt)가 여유 있게 들어오도록 " +
                 "280pt. 이보다 크게 올리면 27-1이 금지하는 '큰 창을 억지로 미는' 연출이 되어 개그가 죽는다.")]
        public float windowTheftMinTargetWidthPoints = 280f;

        [Tooltip("1/2회차 시도 각각의 지속 시간(초). 2회 고정(1회는 성의 없어 보이고 3회 이상은 지루함).")]
        public float windowTheftAttemptDuration = 1.2f;

        [Tooltip("2회 시도 후 포기 리액션(헥헥거림) 지속 시간(초) — 이 시간이 끝나면 정상적으로 Idle로 복귀.")]
        public float windowTheftGiveUpDuration = 1.5f;

        [Header("바탕화면 청소부 / 블랙홀 (docs/UX_FLOW.md 27-2/27-5절, Phase 4 — 복제 스프라이트 공용 파이프라인)")]
        [Tooltip("청소부 유휴 판정 저확률 추첨 주기(초).")]
        public float desktopTidyCheckInterval = 60f;

        [Tooltip("청소부 판정 주기마다 발동할 확률(0~1). UX는 구체 수치를 명시하지 않아 다른 유휴 " +
                 "스펙터클(창 도둑 2~4%)과 같은 대역으로 임시 추정. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.03.")]
        public float desktopTidyChance = 0f;

        [Tooltip("청소부 종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 창 도둑과 동일한 15분으로 임시 추정.")]
        public float desktopTidyCooldownSeconds = 900f;

        [Tooltip("청소부 정렬 연출(복제 스프라이트 슬라이드~짠 포즈)의 지속 시간(초). 이 시간 후 정상 " +
                 "종료(오버레이 페이드아웃) — 실제 슬라이드 애니메이션은 Phase2+ 렌더링 담당.")]
        public float desktopTidyDurationSeconds = 2.5f;

        [Tooltip("블랙홀 유휴 판정 저확률 추첨 주기(초).")]
        public float blackholeCheckInterval = 60f;

        [Tooltip("블랙홀 판정 주기마다 발동할 확률(0~1). UX 미명시 — 청소부와 동일 대역으로 임시 추정. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.03.")]
        public float blackholeChance = 0f;

        [Tooltip("블랙홀 종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 청소부와 동일하게 임시 추정.")]
        public float blackholeCooldownSeconds = 900f;

        [Tooltip("블랙홀 소용돌이~튕겨나옴 연출의 지속 시간(초). 실제 궤적 애니메이션은 Phase2+ 렌더링 담당.")]
        public float blackholeDurationSeconds = 2.5f;

        [Header("화면 낙서 그라피티 (docs/UX_FLOW.md 27-3절, Phase 4)")]
        [Tooltip("유휴 판정 저확률 추첨 주기(초).")]
        public float graffitiCheckInterval = 60f;

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 미명시 — 방해성이 가장 낮은 항목이라 다른 스펙터클보다 " +
                 "약간 높게 임시 추정. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.04.")]
        public float graffitiChance = 0f;

        [Tooltip("종료 후 다음 발동까지의 최소 쿨다운(초). UX 미명시 — 방해성이 낮아 다른 스펙터클보다 짧게 임시 추정.")]
        public float graffitiCooldownSeconds = 600f;

        [Tooltip("캐릭터로부터 그리기 후보 영역까지의 최소 반경(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고). UX 명시값 200px.")]
        public float graffitiMinRadiusPx = 200f;

        [Tooltip("캐릭터로부터 그리기 후보 영역까지의 최대 반경(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고). UX 명시값 300px.")]
        public float graffitiMaxRadiusPx = 300f;

        [Tooltip("낙서 영역의 정사각형 한 변 길이(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고) — 발판과의 겹침 판정에 쓰이는 후보 사각형 크기.")]
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

        [Tooltip("판정 주기마다 발동할 확률(0~1). UX 명시값 1~3%(다른 스펙터클보다 낮게 — 시각적 충격이 큼). " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.02.")]
        public float windowCrashChance = 0f;

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

        [Tooltip("판정 주기마다 리마인더가 실제로 발동할 확률(0~1). " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.2.")]
        public float todoReminderChance = 0f;

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

        [Tooltip("19절 '상시' 노출 채널의 초록->노랑 경계(0~1). 이 값 이상이면 어깨가 처지기 시작한다 " +
                 "(Interaction/StressGaugeRenderer.cs). 노랑->빨강 경계는 별도 값을 두지 않고 아래 " +
                 "stressSulkyThreshold를 그대로 재사용한다 — 빨간 신호를 봐야 하는 시점과 실제로 " +
                 "부루퉁해지는 시점이 어긋나면 안 되기 때문(같은 경계를 두 곳에서 계산해 어긋난 " +
                 "전례가 이 프로젝트에 이미 두 번 있다: Dock 구간, 화면 클램프).\n\n" +
                 "★ 2026-08-29 기본 OFF(2.0) — 사용자 신고 '몸주위로 이상한 주황색 선들이 생김'의 " +
                 "직접 원인이다. 그 주황색 선들의 정체는 양 어깨에서 바깥·아래로 늘어지는 호 2개(어깨 " +
                 "처짐)와 머리 옆 작은 원(한숨 퍼프)이며, 색은 StressGaugeRenderer의 CautionColor" +
                 "(0.72, 0.63, 0.36)다. 이 표시는 확률 기반 구경거리가 아니라 19절이 '상시 채널'로 " +
                 "설계한 것이라, 직전 라운드에 자율 연출을 전부 OFF로 내릴 때 이것만 빠졌다.\n\n" +
                 "사용자가 요청하지 않은 표시가 캐릭터를 둘러싸는 것에 불만을 제기한 것이 이번이 " +
                 "두 번째다(직전은 하드웨어 발열 이모트 — enableAutonomousHardwareReactions=false로 " +
                 "차단). 같은 결론을 같은 방식으로 적용한다.\n\n" +
                 "왜 새 on/off 필드를 만들지 않고 임계값을 올렸는가: StressGauge.CurrentLevel은 " +
                 "0~1로 클램프되므로 1보다 큰 값을 넣으면 이 표시가 뜨는 것이 **원리적으로 불가능**해진다. " +
                 "바로 아래 stressRunawayThreshold가 정확히 같은 이유로 2.0인 선례가 있고, " +
                 "Interaction/StressGaugeRenderer.cs는 한 줄도 건드리지 않아도 된다(기능을 지우는 것이 " +
                 "아니라 조용하게 만드는 것 — 되살리려면 이 값을 원래 기본값 0.4로 되돌리면 그대로 " +
                 "예전 거동이다).")]
        public float stressTierCautionLevel = 2f;

        [Tooltip("이 값(0~1) 이상이면 SULKY(부루퉁함) 상태가 발동 후보가 된다. UX 명시값 80%.")]
        public float stressSulkyThreshold = 0.8f;

        [Tooltip("SULKY 발동 저확률 추첨 주기(초).")]
        public float stressSulkyCheckInterval = 30f;

        [Tooltip("판정 주기마다 SULKY가 발동할 확률(0~1) — 게이지가 임계값을 넘은 동안에만 적용. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.5.")]
        public float stressSulkyChance = 0f;

        [Tooltip("SULKY 종료 후 다음 발동까지의 최소 쿨다운(초).")]
        public float stressSulkyCooldownSeconds = 90f;

        [Tooltip("SULKY 한숨/처진 자세 유지 시간(초).")]
        public float stressSulkyHoldSeconds = 2f;

        [Header("가출 (docs/UX_FLOW.md 20/24절, Phase 5)")]
        [Tooltip("스트레스 게이지가 이 값(0~1) 이상이면 가출(2단계, 확정 발동)이 트리거된다. 24절 — " +
                 "1단계(인질극/로데오 확률 가중)와 달리 확률이 아니라 임계값 도달 시 확정 발동. " +
                 "★ 2026-08-29 기본 OFF(2.0) — 사용자 신고 '저 주황색선들만있다가 클릭하면 캐릭터가 나옴'의 **직접 원인**이다. 가출은 캐릭터를 화면에서 완전히 숨기고(StickmanBlackboard.SetCharacterVisible(false)) 유저가 그 자리를 클릭해야 다시 나타나는 '찾기 미니게임'인데, 그것이 스트레스 게이지만으로 자율 발동해 실측 Player.log에서 캐릭터가 90초 넘게 사라져 있었다(은신처 힌트 파문 #47까지 관측). 유저에게는 '이펙트만 남고 캐릭터가 없어진 앱'으로 보인다. StressGauge.CurrentLevel은 0~1로 클램프되므로 1보다 큰 값을 넣으면 자율 발동이 **원리적으로 불가능**해진다. RunawayDirector의 수동 발동 경로(ForceRunaway)는 이 임계값을 읽지 않으므로 그대로 살아 있다. 원래 기본값 1.0.")]
        public float stressRunawayThreshold = 2.0f;

        [Tooltip("'나 안 해!' 대사 이후 화면 가장자리로 뛰어가는 애니메이션 유지 시간(초). 실제 이동/모션 " +
                 "연출은 Phase2+ 렌더링 담당 — 이 시간 동안은 상태만 확정 유지.")]
        public float runawayFleeDurationSeconds = 1.2f;

        [Tooltip("가출 은신처(화면 네 모서리)를 화면 가장자리로부터 안쪽으로 띄우는 여백(**OS 포인트**, 아래 \"OS-px 필드 단위 규약\" 참고).")]
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

        [Header("Dock 발판 (사용자 요청: '독위에서만 걷고 독아래로 가면 바닥으로 내려가야')")]

        [Tooltip("Dock 띠 두께(OS 포인트)의 **폴백** 값. Dock 발판의 상단 = 화면 바닥 - 이 값.\n\n" +
                 "★ 2026-08-29 2차: 정상 경로에서는 더 이상 이 값을 쓰지 않는다 — 두께를 " +
                 "tilesize + dockThicknessTilePaddingPoints로 파생시키므로 Dock 크기 설정을 바꾼 " +
                 "사용자에게도 따라간다. 이 값이 쓰이는 경우는 dockMetricsFromSystemEnabled가 꺼져 " +
                 "있거나 com.apple.dock 조회가 실패했을 때뿐이다.\n\n" +
                 "기본 75는 이 환경의 실측치다(CGDisplayBounds의 화면 전체 982pt - 작업영역 874pt - " +
                 "메뉴바 33pt = 75pt, tilesize=49 기준). 0으로 두면 Dock 발판이 사라지고 모든 낙하가 " +
                 "화면 바닥 안전망으로 간다.")]
        public float dockFootholdThicknessPoints = 75f;

        [Tooltip("Dock 가로 폭의 **폴백** 값(화면 폭 대비 비율, 가로 정중앙 정렬). 0이면 Dock 발판 비활성.\n\n" +
                 "★ 2026-08-29 2차: 정상 경로에서는 쓰이지 않는다 — 폭은 타일 개수 x 피치 + 고정분 + " +
                 "구분선으로 계산하며, 타일 개수는 NSWorkspace로 정확히 센다(근거는 " +
                 "Platform/IDockMetricsService.cs). 이 비율이 쓰이는 경우는 비-macOS이거나 " +
                 "dockMetricsFromSystemEnabled가 꺼져 있거나 조회가 실패했을 때뿐이다.\n\n" +
                 "왜 실제 Dock 창의 사각형을 쓰지 않는가: Dock 프로세스가 소유한 창은 'Dock'과 " +
                 "'Wallpaper-' 둘뿐이고 둘 다 화면 전체 크기이며, 시스템 전체 창을 전수 조사해도 " +
                 "Dock 막대 모양인 창은 하나도 없다(2026-08-29 실측 확정). 정확한 나머지 경로는 " +
                 "화면 기록/접근성 권한을 요구해 금지다.\n\n" +
                 "기본값 0.65는 실측 폭(약 0.74)보다 **일부러 좁게** 잡은 값이다 — 넓게 틀리면 Dock이 " +
                 "없는 자리에 캐릭터가 떠 있게 되고(사용자가 신고한 그 증상), 좁으면 조금 일찍 떨어질 뿐이라 " +
                 "틀리는 방향을 안전한 쪽으로 고정했다.")]
        [Range(0f, 1f)]
        public float dockFootholdWidthFraction = 0.65f;

        [Header("눈 커서 추적 (★ 2026-09-01 기능 OFF — 캐릭터에 눈이 없어졌다. 아래 문서 참고)")]

        // ============================================================================
        // ★ 2026-09-01 — 눈맞춤 기능은 **꺼졌지만 삭제되지 않았다** (docs/UX_FLOW.md 38-5)
        // ============================================================================
        // 사용자 지시: "커서 눈맞춤 기능은 삭제하되 **코드는 남겨** 나중에 복원 가능하게 할 것."
        // 그림체가 두꺼운 채움 실루엣으로 바뀌면서 얼굴 요소가 전부 사라졌고(머리 = 꽉 찬 원),
        // 눈이 없는 캐릭터에게 "시선"은 원래 없는 개념이라 흉내 내지 않는다(원칙 1의 그림 버전).
        //
        // 아래 5개 필드는 **전부 그대로 보존**한다 — 되살릴 때 그때의 튜닝값이 남아 있어야 하기
        // 때문이다. 되살리는 절차 전문은 Editor/SceneBootstrapper.BakeEyes 문서에 한 번만 적혀 있고,
        // 그 절차가 유효하다는 것은 Tests/EditMode/EyeRestorePathContractTests.cs가 잠근다.
        //
        // 지금 이 값이 false여도 실제 동작에는 차이가 없다(프리팹에 눈 오브젝트 자체가 없어
        // States/EyeController가 이미 무해한 상태다). 그래도 false로 두는 이유는 **의도의 기록**이다 —
        // 누군가 눈 오브젝트만 되살렸을 때 "추적까지 같이 켜려던 것인지"를 이 값이 말해 준다.
        [Tooltip("눈동자가 마우스 커서를 따라갈지 여부.\n\n" +
                 "★ 2026-09-01 기본 OFF — 캐릭터에서 눈이 삭제되어(그림체 전환, docs/UX_FLOW.md 38절) " +
                 "따라갈 눈동자가 존재하지 않는다. 아래 튜닝값 4개는 되살리기용으로 보존해 둔 것이다.\n\n" +
                 "켜면 재빌드 없이 다음 프레임부터 즉시 반영된다(States/EyeController.cs가 매 프레임 " +
                 "이 설정 묶음을 새로 읽는다) — 단 눈 오브젝트가 프리팹에 있어야 눈에 보인다.")]
        public bool eyeTrackingEnabled = false;

        [Tooltip("눈동자가 중립에서 벗어날 수 있는 최대 거리(월드 유닛). 머리 링 반경 0.22 / 눈 중립 " +
                 "(±0.075,+0.02) / 눈동자 반경 0.018 기준으로 기하학적 상한은 0.0929이며, 이 값이 " +
                 "그보다 크면 States/EyeController.MaxSafePupilOffset(0.09)으로 자동 clamp된다 — " +
                 "즉 이 필드를 아무리 키워도 눈동자가 머리 링 밖으로 나가는 일은 구조적으로 불가능하다.")]
        public float eyeMaxPupilOffset = 0.05f;

        [Tooltip("눈동자 추적 강도 k (프레임레이트 독립 지수 감쇠 1-exp(-k*dt)의 k). 클수록 커서를 " +
                 "빠르게 따라간다. 12면 약 0.25초 안에 목표의 95%에 도달한다(즉시 스냅처럼 보이지 않으면서 " +
                 "굼뜨지도 않는 값). 0이면 눈이 전혀 움직이지 않는다.")]
        public float eyeTrackingFollowRate = 12f;

        [Tooltip("이 거리(월드 유닛) 안에 커서가 들어오면 눈이 중립(정면)으로 돌아간다 — '커서가 " +
                 "캐릭터와 겹치면 정면' 요구사항. 이 구간에서는 방향 벡터 자체가 의미가 없어(머리 " +
                 "한가운데) 눈동자가 미세하게 떨리기만 하므로 아예 중립으로 고정하는 편이 자연스럽다.")]
        public float eyeTrackingNeutralRadiusWorld = 0.6f;

        [Tooltip("이 거리(월드 유닛) 이상 떨어지면 눈동자가 최대 오프셋에서 포화되어 더 이상 커지지 " +
                 "않는다 — '커서가 아주 멀면 최대 오프셋에서 멈춤' 요구사항. NeutralRadius와 이 값 " +
                 "사이에서 오프셋이 0에서 최대까지 선형으로 커진다. 화면 세로가 약 24유닛이므로 4는 " +
                 "'캐릭터 키의 두어 배쯤 떨어지면 눈을 끝까지 돌린다'에 해당한다.")]
        public float eyeTrackingFullRangeWorld = 4f;

        // ============================================================================
        // 말풍선(대사 표시) — docs/UX_FLOW.md 5절 UX 계약의 튜닝 값
        // ============================================================================
        // 계약 자체(즉시 취소/큐잉 금지/파생 순서)는 코드의 불변식이라 설정으로 끌 수 없다. 여기 있는
        // 것은 "얼마나 오래/크게 보이는가"와 "혼잣말을 얼마나 자주 하는가"뿐이다.

        [Header("말풍선 (docs/UX_FLOW.md 5절)")]

        [Tooltip("말풍선 표시 기능 자체의 on/off. 끄면 DialogueIntent는 그대로 생성되지만(원칙 1 파이프라인 무변경) " +
                 "화면에 그리지 않는다.")]
        public bool dialogueBubbleEnabled = true;

        [Tooltip("가독성을 위한 최소 노출 시간(초, UX_FLOW.md 5절 규칙 4 권장 0.6~0.8). 상태가 그보다 " +
                 "빨리 정상 종료돼도 이 시간까지는 말풍선을 유지한 뒤 페이드아웃한다. **강제 인터럽트** " +
                 "(RAGDOLL 등)는 이 값과 무관하게 항상 즉시 제거가 이긴다 — 그건 설정이 아니라 계약이다.")]
        public float dialogueMinVisibleSeconds = 0.7f;

        [Tooltip("한 말풍선의 최대 노출 시간(초). 상태가 아주 오래 지속돼도(예: Idle 6초) 말풍선이 " +
                 "화면에 눌러앉지 않게 한다. 0 이하면 상한 없음. 이 방향(더 일찍 사라짐)은 계약이 막는 " +
                 "실패 모드('행동보다 텍스트가 오래 남음')의 반대편이라 안전하다.")]
        public float dialogueMaxVisibleSeconds = 4f;

        [Tooltip("말풍선 글자 크기(캔버스 유닛 = macOS 포인트)의 **배율 1.0 기준값**. 실제 사용값은 " +
                 "DialogueBubbleRenderer.ResolveFontSize()가 characterScale을 곱한 뒤 가독성 하한(12pt)으로 " +
                 "받친 값이다 — 기하(테두리/여백/꼬리)와 달리 글자는 단순 비례로 줄이면 읽을 수 없기 때문이다. " +
                 "Retina에서는 이 값이 물리적으로 2배 픽셀에 그려질 뿐 크기는 그대로다(CanvasScaler가 흡수).")]
        public int dialogueFontSize = 16;

        [Tooltip("만화 레터링 글자를 비스듬히 기울이는 각도(도). 사용자 요구 2026-08-31 " +
                 "'캐릭터가 말하는 텍스트는 좀 대각선으로 작성해줘'. 0이면 수평(기울기 끔).\n\n" +
                 "**부호는 여기서 정하지 않는다** — 글자가 캐릭터 왼쪽 위에 놓이면 반시계, 오른쪽 위면 " +
                 "시계 방향으로 DialogueBubbleRenderer가 자동으로 뒤집어 배치의 대각선과 맞물린다. " +
                 "여기 값은 그 '크기'이고, 대사마다 ±25% 편차가 결정적 해시로 붙어 손글씨 느낌을 낸다.\n\n" +
                 "8도의 근거: 두 줄 블록에서 양 끝 높이차가 한 줄 높이만큼 생겨 한눈에 비스듬해 보이면서도 " +
                 "글자 하나하나의 세로축은 수직에 가까워 한글 네모 글리프가 읽힌다. 15도를 넘기면 " +
                 "넘어지는 것처럼 보이고, 회전 리샘플링으로 획도 함께 무뎌진다.\n\n" +
                 "※ 글리프가 물리 14픽셀보다 작으면 회전 리샘플링이 한글 자모를 뭉개므로 이 값과 무관하게 " +
                 "기울기가 꺼진다(Retina에서는 폰트 하한 9pt = 18px라 항상 켜진다).")]
        [Range(0f, 20f)]
        public float dialogueTiltDegrees = 8f;

        [Tooltip("IDLE 진입 시 혼잣말을 할 확률(0~1, UX_FLOW.md 26-3절 '살아있는 느낌'). 0이면 유휴 " +
                 "혼잣말이 완전히 꺼진다(직전 라운드까지의 거동과 100% 동일).")]
        [Range(0f, 1f)]
        public float idleChatterChance = 0.28f;

        [Tooltip("WALK 진입 시 혼잣말을 할 확률(0~1). 걷는 중에는 유휴보다 말수가 적은 편이 자연스럽다.")]
        [Range(0f, 1f)]
        public float walkChatterChance = 0.14f;

        [Tooltip("혼잣말 사이의 최소 간격(초). Idle<->Walk 전이가 2~6초마다 일어나므로 이 쿨다운이 " +
                 "없으면 확률이 낮아도 체감상 수다스러워진다. Idle과 Walk가 하나의 타이머를 공유한다.")]
        public float ambientChatterCooldownSeconds = 11f;

        [Header("색상 (임시 플레이스홀더 — 디자이너 확정 전까지)")]

        [Tooltip("캐릭터 선 색 프리셋(사용자 요청, 2026-08-28: '캐릭터를 흰색 or 검은색으로 선택할수있게'). " +
                 "배경이 어두운 바탕화면에서는 검은 캐릭터가 거의 보이지 않으므로 흰색이 필요하다. " +
                 "이 값만 바꾸면 프리팹을 다시 만들 필요 없이 런타임에 즉시 반영된다 — " +
                 "StickmanAgent.ApplyInkColorFromConfig()가 시작 시 모든 LineRenderer 색을 이 값으로 " +
                 "일괄 갱신하기 때문이다(Core/StickmanAgent.cs 참고).\n" +
                 "★ 이 필드는 <b>배포 기본값</b>이다. 사용자가 앱에서 고른 색은 여기에 쓰지 않는다 — " +
                 "읽을 때는 반드시 ResolveInkPreset()/ResolveInkColor()를 거칠 것(아래 문단 참고).")]
        public StickmanInkColor inkColor = StickmanInkColor.Black;

        // ============================================================================
        // ★ 사용자가 고른 잉크색은 이 에셋에 **기록되지 않는다** (2026-08-31, R5 잉크색 오염)
        // ============================================================================
        // characterScale과 **정확히 같은 실패 모드**였다(아래 "이번 실행의 배율은 이 에셋에 기록되지
        // 않는다" 문단이 그 원본 근거다). 정보창의 잉크 스와치와 우클릭 메뉴 [잉크색]이
        // `_config.inkColor = next`로 **직렬화 필드**에 직접 썼고, 그 _config는 프리팹 16개 컴포넌트에
        // 배선된 배포 에셋(Assets/_Project/Data/DefaultStickConfig.asset) 그 자체다. 유니티 에디터는
        // ScriptableObject 애셋에 가한 플레이 모드 중 변경을 되돌리지 않으므로, 에디터에서 한 번
        // 눌러 보고 프로젝트를 저장하면 그 값이 출하 기본값이 되어 전 사용자에게 나간다.
        //
        // 고친 방식도 배율과 같다: 배포 기본값(위 직렬화 필드)과 이번 실행의 값(아래 [NonSerialized]
        // 필드)을 물리적으로 분리하고, 조회는 리졸버 하나로 합친다. [NonSerialized]라 Ctrl+S로도
        // AssetDatabase로도 .asset 파일에 닿을 수 없다 — 오염 경로가 타입 수준에서 사라진다.
        //
        // "그러면 재시작하면 색이 초기화되지 않나?" — 그래서 기억은 세이브 파일이 맡는다
        // (Core/CharacterAppearanceModel.cs + CharacterSaveStore 스키마 v7). 오히려 이전에는
        // <b>빌드에서 재시작마다 잉크색이 초기화</b>되고 있었다(에셋 변경이 남는 것은 에디터뿐이다).
        [System.NonSerialized] private bool _hasRuntimeInkColor;
        [System.NonSerialized] private StickmanInkColor _runtimeInkColor;

        /// <summary>이번 실행에서 잉크색이 명시적으로 지정됐는가(스와치/메뉴/저장 복원/테스트). 진단용.</summary>
        public bool HasRuntimeInkColor => _hasRuntimeInkColor;

        /// <summary>이번 실행의 잉크색(디스크의 .asset에는 남지 않는다).</summary>
        public void SetRuntimeInkColor(StickmanInkColor v)
        {
            _runtimeInkColor = v;
            _hasRuntimeInkColor = true;
        }

        /// <summary>런타임 잉크색을 지우고 배포 기본값으로 되돌린다. StickmanAgent.Awake가 매 세션
        /// 시작에 부른다 — 에셋 인스턴스는 씬 재로드에도 살아남으므로, 이걸 안 하면 앞선 씬의
        /// 잉크색이 다음 씬으로 새어 들어간다(배율에서 이미 겪은 전파 경로).</summary>
        public void ClearRuntimeInkColor() => _hasRuntimeInkColor = false;

        /// <summary>
        /// ★ 지금 유효한 잉크색 <b>프리셋</b>. <c>inkColor</c> 필드를 직접 읽지 말고 반드시 이것을 쓸 것 —
        /// 직접 읽으면 사용자가 고른 색(런타임 오버라이드)이 무시된다.
        /// </summary>
        public StickmanInkColor ResolveInkPreset() => _hasRuntimeInkColor ? _runtimeInkColor : inkColor;

        /// <summary>흰 잉크 프리셋인가 — 호출부 대부분이 필요로 하는 형태(색 반전 분기)라 여기 둔다.
        /// <c>config.inkColor == StickmanInkColor.White</c>라고 손으로 적으면 그 자리만 런타임
        /// 오버라이드를 놓친다(이 버그의 재발 경로가 정확히 그 문장이었다).</summary>
        public bool IsWhiteInk() => ResolveInkPreset() == StickmanInkColor.White;

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
            => IsWhiteInk() ? whiteInkColor : primaryOutlineColor;

        [Tooltip("말풍선 **안쪽 채움** 색. ★ 기본 알파 0 = 완전 투명(2026-08-29 사용자 요구 \"말풍선도 " +
                 "흰색바탕이 아니고 얼굴처럼 투명한데다 텍스트가 써져야함\"). 이 상태에서 말풍선은 캐릭터 " +
                 "머리와 같은 문법이 된다 — 잉크 링(테두리)만 있고 안은 비어 바탕화면이 그대로 비친다.\n" +
                 "알파를 남겨 둔 이유: 아이콘/글자가 빽빽한 바탕화면 위에서 글자가 안 읽힐 때 0.1~0.2 정도로 " +
                 "올려 아주 옅은 판을 깔 수 있는 조절 창구다. 1로 되돌리면 예전의 불투명 흰 말풍선이 된다.\n" +
                 "RGB는 흰 캐릭터 프리셋(inkColor=White)에서 자동으로 검정 쪽으로 반전되며, 그때도 알파는 " +
                 "이 값이 그대로 쓰인다(DialogueBubbleRenderer.ResolveBubbleFillColor).")]
        public Color dialogueBubbleColor = new Color(1f, 1f, 1f, 0f);

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

        [Header("진단 로그 (개발/디버깅 전용)")]
        [Tooltip("상시 진단 로그(발판리포트/창진단/히트테스트 프로브)를 촘촘한 주기로 켤지 여부. " +
                 "기본 false — 2026-08-28 정리 라운드에서 기본 OFF로 내렸다. 이력: 직전 라운드에 " +
                 "'화면을 볼 수 없는 환경에서 캐릭터가 진짜 창 위에 서 있는지'를 판별할 유일한 수단으로 " +
                 "[발판리포트](2.5초)/[창진단](7.5초)를 상시 로그로 도입했는데, 기능이 안정화된 지금은 " +
                 "그 두 줄이 Player.log의 84%(실측: 443줄 중 372줄)를 차지해 정작 중요한 경고/예외가 " +
                 "묻힌다. 그렇다고 지우면 다음 회귀 때 다시 눈이 먼 채로 조사해야 하므로, 삭제 대신 " +
                 "이 스위치로 옮긴다. false여도 [발판리포트]는 60초 심장박동 주기로 계속 남으므로 " +
                 "'지금 무엇을 딛고 있는가'는 재빌드 없이도 항상 로그에서 확인할 수 있고, 이상 신호 " +
                 "([화면클램프]/[캐릭터구조]/[발판변경])는 이 값과 무관하게 항상 남는다. " +
                 "디버깅 시에는 이 체크박스만 켜면 예전과 동일한 촘촘한 리포트로 돌아온다 " +
                 "(Platform/MacOS/MacOverlayStateEnforcer.cs의 주기 상수 참고).")]
        public bool verboseDiagnosticsLogging = false;

        // ====================================================================================
        // 접지 스냅 안전 상한 (2026-08-29 — 사용자 신고 "창이 최대이면 갑자기 제일위로 순간이동해서 떨어짐")
        // ====================================================================================

        [Header("접지 스냅 안전 상한 (2026-08-29)")]
        [Tooltip("StickmanBlackboard.SnapToGround()가 캐릭터를 발판 상단선으로 정착시킬 때 허용하는 " +
                 "**한 번의 최대 이동 거리**(월드 유닛). 이 거리를 넘으면 끌고 가지 않고 발판을 놓아 " +
                 "Fall로 보낸다(딛고 있던 발판이 캐릭터를 지나쳐 크게 움직였다는 뜻이므로, 캐릭터가 " +
                 "공중에 남는 것이 물리적으로 맞다).\n\n" +
                 "기본값 0.6을 고른 근거 — 캐릭터 신장은 약 2.27유닛이므로 0.6은 신장의 약 26%, " +
                 "무릎 높이 언저리다. 아래 두 조건을 동시에 만족해야 해서 이 대역이 나왔다:\n" +
                 " (하한) groundSnapTolerance(에셋 20 OS-pt)를 월드로 환산한 값보다 반드시 커야 한다. " +
                 "실측 환산은 Screen 982px / orthographicSize 12 기준 24/982*20 ≈ 0.49유닛이고, " +
                 "GroundSensor.Sense()는 그 허용오차 안에 있을 때만 Grounded를 주므로 정상 보행 중의 " +
                 "미세 정착과 걷다 만나는 작은 단차는 **전부 이 상한 아래**다. 0.49보다 작게 잡으면 " +
                 "정상 접지가 상한에 걸려 캐릭터가 걷다 말고 덜덜거리며 낙하한다.\n" +
                 " (상한) 그러면서도 '순간이동'이라고 부를 만한 거리보다는 작아야 한다. 되올라가기 " +
                 "상한(stepUpMaxHeight 설정값 2.4, 실측 Dock 낙차가 그보다 크면 Core/DockGeometry.cs가 " +
                 "런타임에 더 올린다)보다 작게 두어, 그 정도 높이 변화는 스냅이 아니라 " +
                 "파쿠르/낙하라는 정식 경로로만 처리되게 한다.\n\n" +
                 "위/아래 방향을 따로 두지 않은 이유: 상한을 넘었을 때의 올바른 처리가 양쪽 모두 " +
                 "똑같이 'Fall'이다(아래로 크게 내려가는 것은 스냅이 아니라 낙하여야 하고, Fall에 " +
                 "들어가면 스윕 교차 판정이 정확한 착지면을 다시 잡는다). 값이 하나면 두 값이 " +
                 "어긋날 일도 없다 — 이 프로젝트가 이미 두 번 겪은 실패 유형이다.\n\n" +
                 "정직한 메모: 이 상한은 현재 배선에서는 방어적 불변식이다. 이번 신고의 실제 원인은 " +
                 "RescueToSafeGround가 '가장 높은 발판'으로 복귀시킨 것이었고 그쪽에서 고쳤다. " +
                 "이 필드의 값어치는 '무엇을 접지로 볼 것인가'와 '몸을 얼마나 순간이동시켜도 되는가'를 " +
                 "분리하는 데 있다 — 지금까지는 groundSnapTolerance 하나가 두 결정을 겸하고 있었다.")]
        public float groundSnapMaxDistanceWorld = 0.6f;

        // ====================================================================================
        // Dock 실측 (2026-08-29 — 사용자 신고 '지금도 독이랑 계속 겹쳐')
        // ====================================================================================

        [Header("Dock 실측 (2026-08-29)")]
        [Tooltip("Dock 가로 폭을 OS 설정(com.apple.dock)에서 실제로 계산할지 여부. false면 예전처럼 " +
                 "dockFootholdWidthFraction 고정 비율 추정을 쓴다.\n\n" +
                 "왜 필요한가: Dock 폭은 타일 개수에 정비례하는데 타일 개수는 사용자마다/시점마다 " +
                 "다르다. 어떤 고정 비율을 넣어도 틀린다 — 실제로 기본값 0.65는 이 개발 머신에서 " +
                 "실측 0.73~0.77보다 좁아서, 그 차이 구간(좌우 각 약 100pt)에서 캐릭터가 화면 최하단 " +
                 "안전망으로 내려가 진짜 Dock과 겹쳐 보였다(사용자 신고 그 자체). " +
                 "Platform/IDockMetricsService.cs에 실측 데이터와 유도 과정을 적어뒀다.")]
        public bool dockMetricsFromSystemEnabled = true;

        [Tooltip("Dock 타일 하나가 차지하는 가로 피치에서 tilesize를 뺀 나머지(OS 포인트) — 타일 사이 여백.\n\n" +
                 "실측 근거(2026-08-29 2차, 이 개발 머신 tilesize=49): 앱을 하나씩 켜서 타일을 20->25개로 " +
                 "1개씩만 늘리며 매번 스크린샷에서 Dock 패널 좌우 테두리를 다시 쟀다.\n" +
                 "  N=20 -> 1123.50pt / 21 -> 1175.00 / 22 -> 1229.00 / 23 -> 1281.00 / 24 -> 1335.00 / 25 -> 1387.00\n" +
                 "최소제곱 기울기 52.84 ≈ tilesize(49) + 4. 표본 2개뿐이던 직전 라운드는 이 값을 2.5로 " +
                 "잡았고, 그 1.3pt 오차가 타일 수만큼 곱해져 폭 오차로 누적됐다.")]
        public float dockTilePitchPaddingPoints = 4f;

        [Tooltip("타일 전체 폭에 더해지는 고정분(OS 포인트) — 패널 좌우 안쪽 여백만. 구분선 몫은 아래 " +
                 "dockSeparatorWidthPoints로 분리했다(구분선 개수는 Dock 구획 구성에 따라 1~2개로 변한다).\n\n" +
                 "실측 분해: 패널 왼쪽 테두리 -> 첫 아이콘 타일 왼쪽 끝 = 9.5pt(좌우 대칭) -> 2 x 9.5 = 19, " +
                 "여기서 피치 정의상 마지막 타일 뒤에 한 번 더 붙는 여백 4pt를 빼 15.")]
        public float dockPanelFixedPaddingPoints = 15f;

        [Tooltip("Dock 구분선 하나가 차지하는 가로 폭(OS 포인트).\n\n" +
                 "실측 근거(2026-08-29): 구분선을 사이에 둔 두 아이콘의 중심 간격이 같은 구획 안의 " +
                 "피치(53pt)보다 23~25pt 넓었다. Dock 구획은 [Finder+고정앱] | [최근/실행중] | " +
                 "[기타스택+휴지통]이라 보통 구분선이 2개이고, 가운데 구획이 비면(show-recents를 끄고 " +
                 "실행 중 비고정 앱도 없을 때) 1개로 준다 — 개수는 MacWindowService가 세어서 넘긴다.")]
        public float dockSeparatorWidthPoints = 24f;

        [Tooltip("Dock 띠 두께를 tilesize에서 파생시킬 때 더하는 여백(OS 포인트). 두께 = tilesize + 이 값.\n\n" +
                 "실측 근거(2026-08-29): tilesize=49인 이 환경에서 Dock 두께가 정확히 75.00pt였다 " +
                 "(NSScreen.visibleFrame 하단 인셋 75.00 = 화면 982 - 작업영역 874 - 메뉴바 33, " +
                 "그리고 스크린샷에서 잰 패널 상단 테두리 y=907 = 982-75와도 일치). 75 - 49 = 26.\n\n" +
                 "★ 정직한 한계: 이 관계식의 보정점은 tilesize=49 한 점뿐이다. 두 번째 점을 얻으려면 " +
                 "사용자의 Dock 크기 설정을 바꿔야 하는데 그건 절대 불변 원칙 3(유저 자산 불변) 위반이라 " +
                 "하지 않았다. 그래도 하드코딩 75보다는 낫다 — Dock 크기를 바꾼 사용자에게 최소한 " +
                 "따라가긴 한다. Dock 실측이 꺼져 있거나 실패하면 dockFootholdThicknessPoints로 폴백한다.")]
        public float dockThicknessTilePaddingPoints = 26f;

        [Tooltip("계산한 Dock 패널 좌우 끝에서 안쪽으로 깎아낼 여유(OS 포인트, 한쪽당).\n\n" +
                 "왜 필요한가 — 두 가지 이유가 겹친다:\n" +
                 " (1) Dock 패널은 모서리가 크게 둥글다(실측 반경 약 20pt). 패널의 가장 바깥 X에서는 " +
                 "패널 윗면이 이미 아래로 휘어 있어서, 거기에 캐릭터를 Dock 상단 높이로 세우면 " +
                 "실제로는 살짝 떠 보인다.\n" +
                 " (2) 리더 지시 — 오차가 남으면 틀리는 방향을 '좁게'로 둔다. 넓게 틀리면 Dock이 없는 " +
                 "자리에서 캐릭터가 떠 보여 '고장'으로 읽히고(2026-08-29 2차 신고), 좁게 틀리면 Dock " +
                 "가장자리에서 조금 일찍 내려갈 뿐이라 '지저분함'으로 읽힌다. 둘 다 나쁘면 덜 고장처럼 " +
                 "보이는 쪽을 택한다.\n\n" +
                 "폭 공식의 실측 잔차는 최대 1.0pt이므로 이 값은 잔차 보정이 아니라 (1)의 기하 보정이 " +
                 "주 목적이다. 0으로 두면 계산한 패널 사각형을 그대로 쓴다.")]
        public float dockFootholdEdgeInsetPoints = 6f;

        [Tooltip("**실행 중이지만 Dock에 고정돼 있지 않은 앱**의 타일 수 보정치.\n\n" +
                 "★ 2026-08-29 2차 라운드에서 기본값을 6 -> 0으로 내렸다. 이 값은 더 이상 상시 보정이 " +
                 "아니라 **비상 폴백**이다.\n\n" +
                 "경위: 이 타일 수는 어떤 Dock 설정에도 없어서 직전 라운드는 셀 수 없다고 보고 6을 " +
                 "때려박았다. 그 결과 Dock을 실제(x 194~1318)보다 좌우 각 77pt 넓게(x 125.5~1386.5) " +
                 "잡았고, 그 77pt 띠에서 'Dock이 없는데 Dock 위에 선 것처럼 부양'하는 증상이 나왔다 " +
                 "— 사용자가 스크린샷과 함께 즉시 신고했다. '넓게 틀리면 덜 눈에 띈다'는 판단이 틀렸다.\n\n" +
                 "지금은 그 집합을 NSWorkspace.runningApplications(activationPolicy == Regular)로 " +
                 "**직접 센다** — 그게 'Dock에 타일이 생기는 앱'의 정의 그 자체다. 셈에 성공하면 " +
                 "DockMetrics.IsTileCountExact = true가 되고 이 보정치는 **무시된다**(성공했는데도 더하면 " +
                 "부양이 그대로 재발한다).\n\n" +
                 "이 값이 실제로 쓰이는 경우는 하나뿐이다: NSWorkspace 조회 자체가 실패했을 때(AppKit이 " +
                 "로드되지 않은 배치 모드 등). 그때 타일 수는 반드시 실제보다 작으므로 Dock을 좁게 보고, " +
                 "그 방향이 리더가 지정한 안전한 오답 방향이라 기본값 0으로도 무해하다.")]
        public int dockExtraRunningAppTileEstimate = 0;

        // ====================================================================================
        // ★ 캐릭터 크기 배율 (2026-08-29 — 사용자 요구 "캐릭터 사이즈가 지금의 절반정도 되어야함
        //    추후 사이즈 조정가능해야하고")
        // ====================================================================================
        // 설계: **프리팹 지오메트리 하나만이 크기의 단일 소스**다. Editor/SceneBootstrapper.cs의
        // BuildStickmanPrefab()이 아래 배율을 읽어 몸통/팔다리 길이·머리 반경·콜라이더·잡기 영역·선
        // 두께를 전부 곱해 굽고, 런타임 쪽은 그 프리팹을 **실측**해서 쓴다:
        //   · 팔다리 길이/관절 위치 → States/StickmanPoseAnimator.cs가 BoxCollider2D.size.y와
        //     HingeJoint2D.connectedAnchor에서 읽는다(보폭·매달리기 손끝 거리가 자동으로 따라온다).
        //   · 매달리기 최소 낙차   → StickmanBlackboard.LedgeHangMinDropDepth가 위 실측값에서 유도한다.
        //   · 화면 클램프 시각 반폭 → Core/StickmanAgent.TickVisualHalfWidth()가 렌더러 bounds를 잰다.
        //   · 머리 위/어깨/발밑 좌표 → Core/StickmanMetrics.cs(단일 조회 경로)가 계층을 실측해 준다.
        // 그래서 이 배율을 바꿀 때 **StickConfig의 다른 필드는 하나도 건드릴 필요가 없다**. 아래
        // "일부러 절대값으로 남겨둔 값" 표가 그 이유를 값별로 적어둔 것이다.
        //
        // ────────────────────────────────────────────────────────────────────────────────────
        // 적용 방법 (중요): 이 값을 바꾼 뒤 반드시 프리팹/씬을 다시 구워야 화면에 반영된다.
        //   에디터 : 메뉴 StickMate/Resize Stickman (characterScale 반영, 프리팹+씬 재생성)
        //            (같은 일을 하는 StickMate/Rebuild All 도 가능하다)
        //   배치   : Unity -batchmode -nographics -projectPath <repo> \
        //            -executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll -quit --force
        // ────────────────────────────────────────────────────────────────────────────────────
        //
        // ★★ 일부러 **절대값으로 남겨둔** 값들과 그 근거 (기계적 비례화 금지 — 2026-08-29 검토)
        //   · parkourDetectionRadius(0.5) / hopDownProbeOutward(0.2) / hopDownMinDropHeight(0.35)
        //     → 판정 상대가 캐릭터가 아니라 **OS가 주는 창/Dock 사각형**이다. Dock 단차(1.6375유닛,
        //       이 개발 머신 tilesize=49 기준. Core/DockGeometry.cs가 유도)는 캐릭터 크기와 무관하므로
        //       이 값들이 함께 줄면 판정만 예민해진다.
        //     ★ 2026-08-31 예외 하나 — parkourDetectionRadius의 **"경계 근접 게이트" 용도만** 배율에서
        //       유도한다(Core/DockGeometry.ResolveEdgeProbeReach). 그 게이트의 판정 상대는 창이 아니라
        //       "배회 AI가 경계 행동을 추첨하는 거리"이고, 그 거리는 몸의 물리 반폭에서 나오기 때문이다.
        //       짝이 어긋나 있던 탓에 배율 1.0 초과에서 되올라가기/내려가기가 통째로 죽어 있었다.
        //   · hopDownEdgeCommitDistance(0.12) → 제약이 "walkSpeed x 한 프레임"이고 둘 다 비례하지 않는다.
        //   · stepUpMaxHeight(설정값 2.4) → 반드시 Dock 단차 1.6375를 덮어야 한다. 비례로 바꾸면 배율
        //       0.68 아래에서 2.4*s < 1.6375가 되어 **한 번 Dock에서 내려간 캐릭터가 영영 못 올라온다**.
        //       ★ 2026-08-30: 낙차 자체가 사용자의 Dock 크기 설정(tilesize 16~128)에 따라 0.83~3.57유닛
        //       으로 변하므로, 어떤 절대값도 모든 사용자를 덮지 못한다. 그래서 런타임 상한은
        //       AutoWanderController가 **실측 낙차 + 여유**와 이 설정값 중 큰 쪽으로 유도한다(횡단 리뷰 M3).
        //   · groundSnapTolerance(20 OS-pt) → OS 픽셀 단위의 접지 터널링 방지 허용오차. 낙하속도 x
        //       프레임시간에서 오는 값이라 캐릭터 크기와 무관하다.
        //       (2026-08-30: 코드 기본값도 6 -> 20으로 맞춰 이 주석과 실제가 일치하게 됐다.)
        //   · groundSnapMaxDistanceWorld(0.6) → 하한이 위 groundSnapTolerance의 월드 환산값(약 0.49).
        //       비례로 바꾸면 배율 0.82 아래에서 0.6*s < 0.49가 되어 정상 접지가 상한에 걸린다.
        //   · wanderEdgeStopDistance(0.3) → hopDownEdgeCommitDistance(절대)보다 커야 하고, 프레임당
        //       이동거리(30fps에서 0.083)보다도 커야 한다. 둘 다 절대값이라 이쪽도 절대값이 맞다.
        //   · coyoteTimeDuration / fallGraceDuration → 거리가 아니라 시간이라 배율과 무관하다.
        //   · 팔다리 질량(프리팹 0.09/0.06) → 중력은 가속도라 낙하 거동에 영향이 없고, 질량을 함께
        //       줄이면 ragdollForceThreshold(충격량 기준)가 조용히 예민해진다. 그래서 무변경.
        //   · jumpForce(6) → 자율 배회의 점프 확률이 둘 다 0이라 도달 경로 자체가 없다. 게다가 점프 높이
        //       0.61유닛은 배율 0.5에서 키의 53%로 오히려 더 그럴듯하다(배율 1.0에서는 27%였다).
        //
        // ★★ 반대로 **비례로 바꾼** 값들
        //   · 프리팹 지오메트리 전부(몸통/팔다리/머리/눈/콜라이더/잡기영역/획 두께) — 이 빌더가 곱한다.
        //   · walkSpeed → 아래 ResolveWalkSpeed() 문서(보행 사이클 주파수를 배율과 무관하게 유지).
        //   · 눈동자 이동 폭 → States/EyeController.cs가 머리 링을 실측해 스스로 환산한다.
        //   · 획 두께 / 잡기 영역 폭 → 비례하되 "화면상 최소 크기"에서 바닥을 받친다
        //     (Editor/SceneBootstrapper.cs의 MinStrokeScreenPoints / MinGrabAreaScreenPoints).

        [Header("캐릭터 크기 (2026-08-29 사용자 요구 — 절반 + 추후 조정)")]

        [Tooltip("캐릭터 전신 크기 배율. **1.0 = 2026-08-29 이전 크기(발~정수리 약 2.275 월드유닛), " +
                 "0.5 = 그 절반(약 1.137유닛)**이 기준이다. 프리팹 지오메트리 전체(몸통/팔다리 길이, " +
                 "머리 반경, 눈, 콜라이더, 클릭 잡기 영역, 선 두께)가 이 하나에서 파생되고, 보폭/매달리기 " +
                 "손끝 거리/화면 클램프 반폭 같은 런타임 값은 그 프리팹을 실측하므로 자동으로 따라온다.\n\n" +
                 "★ 값을 바꾼 뒤에는 메뉴 'StickMate/Rebuild All (기존 자산 덮어씀, 주의)'로 프리팹과 " +
                 "씬을 다시 구워야 반영된다(프리팹 재저장은 fileID를 재할당하므로 씬도 함께 굽는다).\n\n" +
                 "★★ 분기점 안내(2026-08-30 정정) — 배율 약 0.653 아래에서는 **Dock 단차의 처리 갈래가 " +
                 "바뀐다**. 근거: Dock 상단→바닥 안전망 낙차는 OS에서 오는 1.6375유닛(이 개발 머신 " +
                 "tilesize=49 기준)인데, 매달리기 최소 낙차(StickmanBlackboard.LedgeHangMinDropDepth = " +
                 "손끝~발끝 거리)는 팔다리에서 유도되어 2.5072 x 배율이다. 2.5072 x 0.653 = 1.6375이므로 " +
                 "그보다 작은 배율에서는 Dock 단차가 '뛰어내리기'가 아니라 '매달려 내려가기'로 분류된다.\n" +
                 "★ 이것은 고장이 아니다 — 매달리기는 '낙차 >= 손끝~발끝 거리'일 때만 선택되므로 그 " +
                 "구간에서 매달린 발끝은 착지면을 지나치지 않는다(예전 주석은 부등호 방향이 반대였다). " +
                 "게다가 낙차 자체가 사용자의 Dock 크기 설정에 따라 움직여서(tilesize 59면 낙차 1.88 = " +
                 "기본 배율 0.75의 매달리기 최소치와 같아진다) 이 분기점은 배율 슬라이더만으로는 고정할 수 " +
                 "없다. 그래서 슬라이더 하한 0.35는 **이 분기점이 아니라** 시각적 가독성(작아진 획/눈)을 " +
                 "근거로 유지한다.\n" +
                 "진짜 금지 조합은 둘뿐이고 각각 따로 잠겨 있다: (1) 되올라가기 상한이 낙차를 못 덮는 것" +
                 "(Core/DockGeometry.ResolveStepUpMaxHeight가 런타임에 방어), (2) ledgeHangChance = 0인 채 " +
                 "배율이 분기점 아래인 것(그러면 내려갈 길이 하나도 없어 Dock 위에 갇힌다 — " +
                 "Tests/PlayMode/CharacterScaleInvarianceTests가 잠근다).\n" +
                 "(현재 기본 0.75에서는 매달리기 최소 낙차가 1.880이라 1.6375가 밴드 [0.35, 1.880) 안에 " +
                 "들지만 여유는 0.243유닛뿐이다 — 예전 인식(0.855 기준)의 2.2배 여유가 아니다.)\n\n" +
                 "참고 — walkSpeed도 이 배율에 비례한다(ResolveWalkSpeed()). 처음에는 '화면 폭은 그대로니 " +
                 "속도는 두자'고 판단했지만, 그 상태로 WalkFootSlipTests가 실패했다(디딤발 미끄러짐 0.465, " +
                 "상한 0.30) — 보폭이 배율에 비례하는데 속도가 고정이면 보행 사이클 주파수가 배율의 역수만큼 " +
                 "빨라져 poseSmoothingRate(35)가 목표 각도를 못 따라가고, 그게 곧 문워크다. 속도를 함께 " +
                 "줄이면 주파수가 배율과 무관해져 기존 실측 튜닝값이 어떤 배율에서도 그대로 유효하다. " +
                 "대가는 화면을 가로지르는 시간이 배율에 반비례해 늘어나는 것이며, 더 빠르게 하고 싶으면 " +
                 "walkSpeed 자체를 올리면 된다(배율은 그 위에 곱해진다).")]
        [Range(MinCharacterScale, MaxCharacterScale)]
        // ★ 2026-08-29 사용자 요구 "캐릭터 사이즈를 지금보다는 1.5배 더 키워주고" — 0.5 -> 0.75.
        // 전신 높이 2.2746944 x 0.75 = 약 1.7060유닛(화면상 약 60pt). 배율 0.75는 Dock 분기 배율
        // (DockHopDownCriticalScale = 0.6531)보다 위라 Dock 단차 1.6375유닛이 '뛰어내리기' 밴드
        // [0.35, 2.5072 x 0.75 = 1.880) 안에 남는다 — 실제 빌드에서 왕복까지 육안 확인했다.
        // ★ 2026-08-30 정정: 여유는 1.880 - 1.6375 = **0.243유닛(약 10pt)뿐**이다. 예전 주석이 낙차를
        //   0.855로 잘못 알고 "넉넉히"라고 적었으나 실제 여유는 그 인식의 1/4이다. tilesize 59 이상인
        //   사용자에게는 이 여유가 음수가 되어 매달리기로 갈린다(그 자체는 안전한 갈래 — 위 Tooltip 참고).
        public float characterScale = 0.75f;

        // ============================================================================
        // ★ 이번 실행의 배율은 이 에셋에 **기록되지 않는다** (2026-08-31, R3 Blocker 2)
        // ============================================================================
        // 왜 필드를 둘로 쪼갰나: 위 characterScale은 프리팹에 배선된 **배포 에셋**
        // (Assets/_Project/Data/DefaultStickConfig.asset)의 직렬화 필드다. 유니티 에디터는 씬
        // 오브젝트와 달리 ScriptableObject 애셋에 가한 플레이 모드 중 변경을 **되돌리지 않는다**.
        // 그래서 다이얼로 크기를 한 번 바꾸면 그 값이 그 세션 내내(씬을 다시 로드해도) 남고,
        // 에디터가 그 애셋을 저장하는 순간 **전 사용자에게 배포되는 기본값**이 되어 버린다.
        // 실제로 개발자 저장 파일의 0.35가 매 실행 복원되면서 하루치 PlayMode 전체가 0.35배로
        // 돌았고(로그 146회), 아무도 그걸 눈치채지 못했다.
        //
        // 고친 방식: "배포 기본값"과 "이번 실행의 값"을 **물리적으로 다른 필드**에 둔다.
        // 아래 필드는 [NonSerialized]라 직렬화 대상이 아니고, 따라서 Ctrl+S로도 AssetDatabase로도
        // .asset 파일에 닿을 수 없다 — 오염 경로가 타입 수준에서 사라진다.
        //
        // 소비자는 한 줄도 안 바뀐다: 예전과 똑같이 ResolveCharacterScale() / ResolveWalkSpeed()만
        // 부르면 되고, 그 안에서 "런타임 값이 있으면 그것, 없으면 배포 기본값"으로 갈린다.
        // 복제본(Object.Instantiate)을 쓰지 않은 이유: 이 에셋은 프리팹의 16개 컴포넌트에 각각
        // 배선돼 있어, 에이전트만 복제본으로 갈아타면 나머지 15개가 낡은 원본을 계속 읽는
        // **진실이 둘인 상태**가 된다(잉크색/DPI 배율이 정확히 그렇게 갈라진다).
        [System.NonSerialized] private float _runtimeCharacterScale; // 0 = 미설정(배포 기본값을 쓴다)

        /// <summary>이번 실행에서 배율이 명시적으로 지정됐는가(다이얼/저장 복원/테스트). 진단용.</summary>
        public bool HasRuntimeCharacterScale => _runtimeCharacterScale > 0f;

        /// <summary>이번 실행의 배율(디스크에 남지 않는다). 0 이하/NaN이면 "미설정"으로 되돌린다.</summary>
        public void SetRuntimeCharacterScale(float v)
        {
            _runtimeCharacterScale = (v > 0f && !float.IsNaN(v)) ? Mathf.Clamp(v, MinCharacterScale, MaxCharacterScale) : 0f;
        }

        /// <summary>런타임 배율을 지우고 배포 기본값으로 되돌린다. StickmanAgent.Awake가 매 세션
        /// 시작에 부른다 — 에셋 인스턴스는 씬 재로드에도 살아남으므로, 이걸 안 하면 앞선 씬의
        /// 배율이 다음 씬으로 새어 들어간다(테스트 스위트 사이 오염의 정확한 경로였다).</summary>
        public void ClearRuntimeCharacterScale() => _runtimeCharacterScale = 0f;

        /// <summary>슬라이더 하한. Dock 단차 임계 배율(약 0.341, 위 Tooltip 유도)보다 조금 위에 둔다.</summary>
        public const float MinCharacterScale = 0.35f;

        /// <summary>
        /// 슬라이더 상한. ★ 2026-08-31 사용자 지시 <i>"캐릭터 사이즈는 max를 1.5까지만"</i> — 2.0 → <b>1.5</b>.
        ///
        /// <para>파급(전부 이 상수에서 <b>파생</b>되므로 손으로 맞출 곳은 없다):
        /// 다이얼 눈금 수 <c>(1.5−0.35)/0.05+1 = 24칸</c>(옛 34칸), 눈금 스윕 <c>23×8° = 184°</c>
        /// (옛 264°) — 12시 쪽 빈 구역이 96°에서 176°로 넓어질 뿐, 눈금 간격 8°와 각도→값 매핑은
        /// 그대로다(<see cref="SizeDialWidget.DegreesForIndex"/>).</para>
        ///
        /// <para><b>Dock 등반과의 관계</b>: 이 상한을 내려도 "크게 만들면 Dock 계단을 못 오른다"는
        /// 별건 조사는 유효하다 — 고정 상수가 버티는 천장은 배율 <b>1.125</b>라 1.5 이하에서도 그
        /// 구간이 그대로 남는다(<c>DockGeometryInvariantTests</c>의 네거티브 컨트롤이 이 수를 찍는다).
        /// 상한을 내리는 것은 그 버그의 수정이 아니라 <b>사용자가 고른 범위</b>일 뿐이다.</para>
        ///
        /// <para>이미 2.0×로 저장해 둔 사용자는 복원 시 여기로 clamp된다
        /// (<c>CornerHoverPanel.RestoreSavedScale</c>) — 표시 숫자와 실제 배율이 갈라지지 않게
        /// 저장 모델까지 함께 내린다.</para>
        /// </summary>
        public const float MaxCharacterScale = 1.5f;

        /// <summary>
        /// 배율 1.0에서의 전신 높이(발바닥~정수리, 월드 유닛). Editor/SceneBootstrapper.cs의 지오메트리
        /// 상수에서 유도한 값이며(1.35 몸통상단 + 0.4846944 접지보정 + 0.22 머리반경 x 2), 런타임에는
        /// Core/StickmanMetrics.cs가 이 값을 기준으로 실측 배율을 역산한다. 프리팹이 없는 폴백 경로의
        /// 기본 신장이기도 하다.
        /// </summary>
        public const float BaselineCharacterTotalHeight = 2.2746944f;

        /// <summary>
        /// ★ 유휴 "주위 살피기"가 머리를 좌우로 밀 수 있는 **안전 상한** — 신장 배수
        /// (2026-08-31 사용자 신고 "머리를 움직이는데 목에서 벗어나서 이상함").
        ///
        /// 이 리그에는 목 관절이 없다. 목은 Torso LineRenderer의 윗부분이고 루트 로컬 x=0에 고정인데
        /// (Editor/SceneBootstrapper.cs), States/StickmanPoseAnimator.SetBodyOffset의 headOffsetX는
        /// 형제인 "Head" 앵커만 민다. 그래서 목선이 여전히 "머리 중심을 가리키는 선"으로 읽히려면
        /// 머리 중심이 <b>목 획의 폭 밖으로 나가면 안 된다</b>:
        ///   목 획 두께 = 0.11 x 0.7 = 0.077 -> 반폭 0.0385 -> 0.0385 / 2.2746944 = 0.01693
        /// 획 두께와 신장이 같은 배율로 커지므로 이 비율은 <b>배율에 무관</b>하다.
        /// 예전 기본값 0.035는 이 상한의 2.07배(머리 반경의 36%)였고, 그래서 육안으로 어긋나 보였다.
        ///
        /// 상수로 노출하는 이유: 이 유도를 테스트 두 곳(EditMode 불변식 / PlayMode 회귀)에 각각
        /// 옮겨 적으면 그 중 하나가 낡는 순간 조용히 초록불이 된다 — 이 프로젝트가 이미 여러 번
        /// 겪은 실패 유형이다.
        /// </summary>
        public const float MaxSafeHeadShiftRatio = (0.11f * 0.7f * 0.5f) / BaselineCharacterTotalHeight;

        /// <summary>
        /// ★ 배율 1.0에서 **몸이 벽에 얼마나 가까이 설 수 있는가**를 정하는 반폭(월드 유닛) —
        /// 2026-08-30 R3-M1(Dock 되올라오기 밴드 근접 충돌) 대응으로 신설.
        ///
        /// 루트 Rigidbody2D에 달린 **비-트리거** 콜라이더 중 가장 넓은 것의 반폭이며, 실제로는
        /// 머리 CircleCollider2D의 반경이다(Editor/SceneBootstrapper.cs: <c>headCollider.radius =
        /// 이 값 x bodyScale</c>). 루트 물리 캡슐의 반폭(0.4/2 = 0.2)보다 넓으므로 **벽에 부딪혀 서는
        /// 위치를 결정하는 것은 캡슐이 아니라 머리 원**이다(잡기 영역 GrabArea는 isTrigger라 물리
        /// 충돌을 일으키지 않으므로 여기 포함되지 않는다).
        ///
        /// 왜 상수로 노출하는가: 이 값은 "배회 AI가 발판 경계에 얼마나 다가갈 수 있는가"의 하한을
        /// 물리적으로 결정한다. 실측(Core/StickmanAgent가 콜라이더에서 잰다)이 1차 소스이고 이 상수는
        /// 프리팹이 없는 폴백 경로의 2차 소스다 — 폴백이 0을 돌려주면 유도가 조용히 꺼지므로
        /// 절대 0을 돌려주지 않게 하기 위한 바닥이다(Core/DockGeometry.ResolveEdgeStopDistance).
        /// </summary>
        public const float BaselineBodyPhysicsHalfWidth = 0.4f;

        /// <summary>
        /// ★ 획 두께의 <b>화면상 하한</b>(OS 포인트) — 2026-08-31 단일 소스화.
        ///
        /// 캐릭터가 작아지면 선도 같이 얇아지는 것이 원칙적으로 맞지만, 선에는 크기와 무관한 절대
        /// 조건이 하나 있다 — <b>"보여야 한다"</b>. 배율 1.0에서 획은 0.077유닛 ≈ 2.7pt인데
        /// (리더 지시 "화면상 2~3pt는 유지"), 그대로 비례하면 배율 0.5에서 1.36pt가 되어
        /// 안티에일리어싱에 묻힌다. 그래서 비례로 줄이되 이 값에서 바닥을 받친다.
        ///
        /// <b>왜 StickConfig에 있는가</b>: 이 하한을 지키는 곳이 이제 둘이다 —
        /// (1) Assets/Editor/SceneBootstrapper.cs가 프리팹을 구울 때,
        /// (2) Core/StickmanAgent.ApplyCharacterScale()이 다이얼로 배율을 바꿀 때.
        /// (2)는 런타임이라 Editor 어셈블리의 상수를 참조할 수 없다. 같은 숫자를 두 곳에 적으면
        /// 한쪽만 바뀌어 "구운 두께와 런타임 두께가 다른" 조용한 어긋남이 생긴다.
        /// </summary>
        public const float MinStrokeScreenPoints = 2f;

        /// <summary>월드 1유닛이 몇 OS 포인트인가의 <b>근사</b>(실측 창 높이 846pt / (2 × 직교 12) = 35.25).
        /// 위 하한을 월드 유닛으로 환산할 때, 카메라/화면을 읽을 수 없는 경로(에디터 프리팹 굽기,
        /// 헤드리스 테스트)에서만 쓴다. 런타임은 카메라 직교 크기와 실제 화면 높이를 직접 잰다.</summary>
        public const float ReferencePointsPerWorldUnitApprox = 846f / (2f * 12f);

        /// <summary>
        /// Dock 상단→바닥 안전망 낙차가 '뛰어내리기' 밴드에 남아 있으려면 필요한 최소 배율
        /// = 1.6375 / 2.5072(배율 1.0에서의 손끝~발끝 거리) = <b>0.6531</b>.
        ///
        /// ★ 2026-08-30 정정(횡단 리뷰 M1): 예전 값 0.341은 낙차를 0.855유닛으로 본 계산이었다. 그
        /// 0.855는 바닥 안전망이 화면 최하단 40pt 위였던 시절의 화석이고, 안전망이 8pt로 내려가고
        /// Dock 두께가 tilesize+26 파생으로 바뀐 뒤의 실제 낙차는 67pt = 1.6375유닛이다
        /// (유도: Core/DockGeometry.cs).
        ///
        /// ★★ 이 값은 **금지선이 아니라 거동 분기점**이다. 아래에서는 Dock 단차가 '매달려 내려가기'로
        /// 분류될 뿐이고 그 자체는 기하학적으로 안전하다. 게다가 낙차가 사용자의 tilesize에 따라
        /// 0.83~3.57유닛으로 변하므로 이 배율도 0.331~1.423으로 함께 움직인다 — 즉 배율 슬라이더
        /// 하한으로 지킬 수 있는 성질이 아니다(자세한 반증은 DockGeometry.HopDownCriticalScale 문서).
        /// 코드는 이 값을 소비하지 않으며, Tests/PlayMode/CharacterScaleInvarianceTests.cs가 매 실행마다
        /// 프리팹 실측에서 다시 계산해 이 상수와 일치하는지 확인한다.
        /// </summary>
        public const float DockHopDownCriticalScale = 0.6531f;

        /// <summary>
        /// ★ 배율이 반영된 실제 보행 속도(유닛/초). <b>walkSpeed를 직접 읽지 말고 반드시 이것을 쓸 것.</b>
        ///
        /// 왜 속도까지 비례해야 하는가(2026-08-29 실측으로 뒤집힌 판단):
        /// 처음에는 "화면/Dock 폭은 캐릭터가 작아져도 그대로니 가로지르는 시간이 배로 늘지 않게
        /// walkSpeed는 절대값으로 두자"고 결정했는데, 그 상태로 Tests/PlayMode/WalkFootSlipTests가
        /// **빨간불**이 났다(디딤발 미끄러짐 0.465, 상한 0.30). 원인이 명확하다:
        ///   보행 사이클 주파수 = 실제 이동 속도 / 한 사이클 이동 거리(보폭)
        /// 인데 보폭은 다리 길이에서 유도되어 배율에 비례한다. 속도를 고정한 채 배율만 절반으로 내리면
        /// 주파수가 그대로 2배(약 1.35Hz -> 2.7Hz)가 되고, 그 속도에서는 poseSmoothingRate(35)의 지수
        /// 감쇠가 목표 각도를 따라잡지 못해 실제 관절 진폭이 깎인다 — 그게 곧 문워크다(이 값이 14였던
        /// 시절 1.35Hz에서 진폭이 17% 깎여 slip 0.5가 났던 것과 **정확히 같은 실패**, poseSmoothingRate
        /// Tooltip 참고).
        ///
        /// 속도를 배율에 비례시키면 주파수가 배율과 무관하게 일정해져, poseSmoothingRate(35)/
        /// walkStrideScale(0.93) 같은 실측 튜닝값이 **어떤 배율에서도 그대로 유효**하다. 즉 "발이
        /// 미끄러지지 않는다"가 재튜닝 없이 구조적으로 보장된다. 부수 효과로 "초당 몇 신장을
        /// 걷는가"(약 1.1 신장/초 — 사람의 보행과 같은 수준)도 보존된다.
        ///
        /// 대가: 화면을 가로지르는 데 걸리는 시간이 배율에 반비례해 늘어난다(배율 0.5에서 2배).
        /// 데스크톱 펫에게는 오히려 자연스럽다고 판단했다 — 작은 것이 큰 것과 같은 속도로 돌아다니면
        /// 그 자체가 부자연스럽다. 더 빠르게 하고 싶으면 walkSpeed를 직접 올리면 된다(이 배율은
        /// 그 위에 곱해질 뿐이다).
        /// </summary>
        public float ResolveWalkSpeed() => walkSpeed * ResolveCharacterScale();

        /// <summary>배율을 안전 구간으로 clamp해서 돌려준다. 직렬화된 값이 예전 에셋이나 스크립트로
        /// 범위 밖으로 들어와도 지오메트리 생성이 깨지지 않게 하는 유일한 조회 경로다.</summary>
        public float ResolveCharacterScale()
        {
            // 런타임 값이 있으면 그것이 진실이고, 없으면 배포 기본값이다(위 [NonSerialized] 문단 참고).
            float s = _runtimeCharacterScale > 0f ? _runtimeCharacterScale : characterScale;
            if (s <= 0f || float.IsNaN(s)) return 1f; // 0/음수/NaN은 "설정 안 됨"으로 보고 기존 크기 유지.
            return Mathf.Clamp(s, MinCharacterScale, MaxCharacterScale);
        }

        // ============================================================================
        // PC 하드웨어 반응 — 자율 발동 마스터 스위치 (2026-08-29 사용자 피드백 대응)
        // ============================================================================
        // 왜 이 스위치가 위쪽 "PC 하드웨어 반응" 섹션이 아니라 파일 맨 끝의 별도 섹션에 있는가:
        // 같은 라운드에 다른 작업자가 위쪽 섹션들을 동시에 편집 중이라 리더가 맨 끝 신규 섹션으로
        // 지정했다. 기능적으로는 위 hardware* 필드 전체를 지배하는 상위 게이트다.
        //
        // 다른 구경거리 연출(격파/창도둑/그라피티/크래시/투두 등)은 "자율 발동 확률"을 0으로 내려서
        // 조용하게 만들 수 있었지만, 하드웨어 반응만은 그 방법이 통하지 않는다 — 트리거가 확률이 아니라
        // **실제 배터리 잔량 / 프레임타임 / 네트워크 연결성 / 충전 상태**이기 때문이다. 확률 필드가
        // 애초에 존재하지 않으므로 0으로 내릴 대상도 없고, 그래서 다른 연출을 전부 끈 뒤에도 이것만
        // 혼자 남아 계속 떴다(사용자 실측: "머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고
        // 겹치는데" — 주황색 물결 = CPU 과부하 열기, 눈처럼 내리는 것 = 그 땀방울).
        // 그래서 확률 대신 **명시적 enable 플래그**를 둔다.

        [Header("PC 하드웨어 반응 — 자율 발동 스위치 (2026-08-29 사용자 피드백)")]

        [Tooltip("배터리 부족 / 충전 중 / CPU 과부하 / 네트워크 끊김을 **스스로 감지해서** 머리 위 " +
                 "이모트를 띄울지 여부.\n\n" +
                 "★ 기본 OFF인 이유 (2026-08-29 사용자 피드백: 요청하지 않은 연출이 캐릭터를 가림) — " +
                 "사용자가 스크린샷과 함께 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데'라고 " +
                 "신고했다. 주황색 구불구불한 선은 CPU 과부하 이모트의 열기 물결, 눈처럼 내리는 것은 그 " +
                 "땀방울이다. 같은 라운드에 다른 구경거리 연출은 전부 자율 발동 확률을 0으로 내렸는데, " +
                 "이 기능만은 트리거가 확률이 아니라 실제 하드웨어 임계값이라 확률 0으로 끌 수 없어 혼자 " +
                 "남아 계속 떴다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이므로, " +
                 "요청하지 않은 자율 연출은 기본적으로 조용해야 한다.\n\n" +
                 "★ 기능을 지우는 것이 아니라 기본값을 조용하게 만드는 것이다 — " +
                 "**수동 발동 경로(전역 단축키 Ctrl+Opt+Cmd+H 데모 미리보기 / 캐릭터 우클릭 메뉴)는 이 값을 " +
                 "읽지 않으므로 OFF에서도 그대로 살아 있다.** 4종 이모트를 눈으로 확인하고 싶으면 그쪽을 쓰면 된다.\n\n" +
                 "이 값을 true로 올리면 위 hardware* 임계값/폴링 주기 설정이 전부 되살아나 배터리·발열·" +
                 "네트워크·충전 신호가 원래 규칙(지속조건 -> 회복 게이트 -> 우선순위 1개만 표현)대로 다시 " +
                 "자율 발동한다. 즉 이 하나가 4종 자율 트리거 **전부**의 상위 게이트다.")]
        public bool enableAutonomousHardwareReactions = false;

        // ============================================================================
        // 낙하 자세 + 무릎앉아 착지 (2026-08-29 사용자 명시 요청)
        // ============================================================================
        // 사용자 원문: "떨어질때 관절이 이상하게 꺾이면서 넘어지는데 떨어질때 무릎앉아 형태로 멋지게
        // 착지해야지". 두 개의 서로 다른 결함이 겹쳐 있었고 이 섹션은 그 둘을 함께 고친다.
        //
        //  (1) **낙하 중 자세가 아예 없었다.** StickmanBlackboard.TickPose()는 상태 ID로 포즈를 고르는데
        //      Fall에 해당하는 분기가 없어 Idle 중립 포즈(팔 살짝 벌리고 다리 곧게)로 떨어졌다 — 막대기가
        //      그대로 내려오는 그림이다. 아래 fallPose* 값이 "팔은 위/바깥, 다리는 살짝 접힘"이라는
        //      Alan Becker 계열 졸라맨 낙하 자세를 만든다.
        //
        //  (2) **착지 연출이 통째로 없었고, 그 자리를 RAGDOLL이 차지하고 있었다.**
        //      StickmanEventBus.LandingRollRequested는 FallState가 이미 발행하고 있었지만 구독자가 0명
        //      이었다(이 프로젝트에서 6번 반복된 "로직은 있는데 아무도 안 듣는" 패턴). 착지는 아무 연출
        //      없이 지나갔고, 그 자리는 아래 landingCrouch*가 채운다.
        //
        //  (3) 그리고 그 옆에 **착지 충격이 RAGDOLL로 새는 경로**가 열려 있었다. 씬의 물리 바닥
        //      (Editor/SceneBootstrapper.CreateGroundCollider의 PhysicsGround)에 부딪힌 충돌 콜백이
        //      StickmanAgent.OnCollisionEnter2D -> RagdollImpactResolver로 흘러가는데, 루트 질량 1 /
        //      ragdollForceThreshold 8 / gravityScale 3이면 v = sqrt(2*9.81*3*h) = 8, 즉 계산상
        //      **1.09유닛만 떨어져도** 임계값을 넘는다.
        //      ★ 실측으로 확인한 범위는 계산보다 좁다(정직하게 기록): **논리 발판이 있는 정상 착지에서는
        //      이 충격량이 0.00**이었다 — FallState가 Update에서 먼저 착지를 확정해 몸을 스냅하고 하강
        //      속도를 지우기 때문이다. 실제로 이 경로를 밟는 것은 **물리 바닥은 있는데 논리 발판은 없는
        //      구간**(안전망에 뚫린 Dock 가로 구멍)으로 떨어질 때이고, 그때는 스냅이 없어 전속력 충돌이
        //      그대로 랙돌이 된다. 아래 landingImpactRagdollShield가 그 경로를 끊는다.
        //
        // ★ 배율 대응 규약(리더 지시): **각도는 크기와 무관하므로 절대값**이 맞고, 거리/속도 성분만
        //   StickMate.Core.StickmanMetrics에서 파생시킨다. 그래서 이 섹션의 거리·속도 항목은 전부
        //   "신장(TotalHeight)의 몇 배"라는 무차원 값으로 노출되어 있고, 실제 월드 유닛은 런타임에
        //   실측 신장을 곱해 얻는다. 앉는 깊이조차 별도 거리 값이 아니라 **무릎/엉덩이 각도에서 유도**
        //   된다(StickmanPoseAnimator.ComputeFootGroundingOffset — 발이 지면에 정확히 닿는 몸 높이를
        //   실제 마디 길이로 역산하므로 어떤 배율에서도 발이 뜨거나 파묻히지 않는다).

        [Header("낙하 중 공중 자세 (2026-08-29 사용자 요청)")]

        [Tooltip("낙하 중 팔을 들어올리는 각도(도). 0 = 곧게 아래, 180 = 곧게 위. 좌우 팔에 각각 " +
                 "부호가 곱해지므로 143은 '수직 위에서 바깥으로 37도 벌어진' 만세 자세다. " +
                 "(처음 152로 뒀더니 최고 속도 구간에서 두 팔이 머리에 거의 붙어 실루엣이 뭉쳤다 — " +
                 "실물 스크린샷 확인 후 벌림을 키웠다.) 사람이 " +
                 "떨어질 때 팔이 위로 뜨는 것은 공기 저항이 아니라 **몸통이 팔보다 먼저 가속되기 " +
                 "때문**이라, 낙하 연출의 가장 큰 신호다(Alan Becker 계열 졸라맨 레퍼런스도 동일).")]
        public float fallPoseArmRaiseDegrees = 143f;

        [Tooltip("낙하 중 팔꿈치 굽힘(도, 항상 0 이상). 완전히 편 팔은 '막대기'로 보이므로 조금 굽혀둔다.")]
        public float fallPoseElbowBendDegrees = 20f;

        [Tooltip("낙하 중 다리를 벌리는 각도(도). 좌우 다리에 각각 부호가 곱해진다 — 두 다리가 완전히 " +
                 "겹치면 옆에서 본 실루엣이 외다리로 보인다.")]
        public float fallPoseLegSpreadDegrees = 15f;

        [Tooltip("낙하 중 다리를 앞으로 들어올리는 각도(도, + = 진행 방향). 무릎 굽힘과 함께 '다리를 " +
                 "살짝 접어 올린' 형태를 만든다.")]
        public float fallPoseHipDegrees = 14f;

        [Tooltip("낙하 중 무릎 굽힘(도, 항상 0 이상 = 사람 무릎이 접히는 방향). 착지 준비 자세로 자연스럽게 " +
                 "이어지도록 무릎앉아 각도(landingCrouchFrontKneeDegrees)보다는 확실히 얕게 둔다.")]
        public float fallPoseKneeBendDegrees = 38f;

        [Tooltip("낙하 자세가 **최대 진폭**이 되는 하강 속도 — 초당 몇 신장을 떨어지는가(신장 배수/초). " +
                 "기본 7은 배율 0.75(신장 1.71유닛)에서 약 12유닛/초, 즉 자유낙하 2.4유닛 지점이다. " +
                 "이 값 미만에서는 자세가 비례해서 옅어지므로, 한 계단 내려서는 정도의 짧은 낙하에서는 " +
                 "만세 자세가 거의 나오지 않는다(막 떨어지기 시작 -> 최고 속도로 자세가 점진 변화). " +
                 "★ 속도는 거리 성분이라 신장으로 나눠 무차원화한다 — 배율을 바꿔도 같은 '체감 속도'에서 " +
                 "같은 자세가 나온다.")]
        public float fallPoseFullSpeedHeightsPerSecond = 7f;

        [Tooltip("낙하 자세의 최소 진폭(0~1). 하강 속도가 0에 가까운 순간(정점 직후)에도 Idle 중립 " +
                 "포즈로 완전히 되돌아가지 않게 하는 바닥값 — 0으로 두면 점프 정점에서 자세가 한 번 " +
                 "풀렸다가 다시 잡히는 것이 보인다.")]
        public float fallPoseMinIntensity = 0.16f;

        [Header("무릎앉아 착지 (2026-08-29 사용자 요청 — 핵심)")]

        [Tooltip("무릎앉아 착지 연출 자체의 마스터 스위치. 끄면 FallState는 예전처럼 착지 즉시 " +
                 "Idle/Walk로 전이한다(LandingRollRequested 이벤트 발행은 그대로 유지). " +
                 "Tests/PlayMode/LandingCrouchTests.cs의 네거티브 컨트롤이 이 스위치를 끄고 " +
                 "'연출이 실제로 사라지는지'를 확인한다.")]
        public bool landingCrouchEnabled = true;

        [Tooltip("★ 착지 충격이 RAGDOLL로 새는 것을 막는 스위치(기본 ON). 켜면 Jump/Fall/LandingCrouch " +
                 "상태에서 **발밑에서 올라온 충돌**(접촉점이 발 높이 이하)은 외력으로 치지 않는다.\n\n" +
                 "근거: 아키텍처 0절이 RAGDOLL을 배정한 대상은 피격/던져짐 같은 **외력**이다. 자기가 " +
                 "떨어져서 땅에 닿는 것은 외력이 아니라 착지이며, 그 처리는 이 파일 위쪽 " +
                 "landingCrouch* 연출이 담당한다. 이 스위치를 끄면 논리 발판이 없는 구간(안전망에 뚫린 " +
                 "Dock 가로 구멍)으로 떨어졌을 때 전속력 지면 충돌이 그대로 랙돌이 되던 예전 거동이 " +
                 "돌아온다 — PlayMode 실측으로 on/off 대조를 확인했다. " +
                 "옆/위에서 들어오는 충돌(던져져 벽에 부딪힘)과 직접 호출 경로" +
                 "(DragThrowState의 던진 속도, RodeoCursorState의 흔들기)는 " +
                 "이 스위치와 무관하게 그대로 랙돌을 발생시킨다.")]
        public bool landingImpactRagdollShield = true;

        // ================================================================================
        // ★ 2026-08-30 (디버거) — 사용자 신고 "갑자기 독 아래로 떨어지면서 관절이 이상하게 꺾임"
        // ================================================================================
        // 실측으로 확정한 인과(재현 로그: Tests/PlayMode/DockSinkholeRegressionTests.cs, 실제 앱
        // Player.log의 "[착지충격] ... 상태=BattleMinigame ... -> RAGDOLL 전이"):
        //   Dock/창 상단은 **논리 발판일 뿐 물리 콜라이더가 없다.** 그래서 매 프레임 접지 스냅
        //   (StickmanBlackboard.GroundedTick)을 부르지 않는 상태에 들어가는 순간 캐릭터는 그 자리에서
        //   자유낙하해 화면 최하단 물리 바닥(PhysicsGround)에 전속력으로 부딪히고, 그 충격이
        //   RAGDOLL 임계값을 넘겨 **관절이 꺾인 채 Dock 아래에 널브러진다.**
        //   그 접지 스냅 호출은 2026-08-29 라운드에 WindowTheft/TimedSpectacle에만 추가됐고
        //   Attack/Getup/BattleMinigame에는 여전히 빠져 있었다("안전장치를 한 곳만 고치고 같은 패턴의
        //   다른 경로에는 안 넣는" 이 프로젝트의 반복 실패 유형).
        [Header("접지 유지 안전망 / Dock 사각지대 회수 (2026-08-30 디버거)")]

        [Tooltip("★ 접지 유지 안전망(기본 ON). 상태가 스스로 GroundedTick()을 부르지 않아도 " +
                 "StickmanAgent가 상태 Tick 직후 **한 곳에서** 대신 불러준다. 새 상태를 추가하는 " +
                 "사람이 이 호출을 빠뜨려도 '논리 발판 위에서 자유낙하 -> 물리 바닥 충돌 -> RAGDOLL'이 " +
                 "구조적으로 재발하지 않게 만드는 것이 목적이다(허용목록이 아니라 **제외목록** 방식 — " +
                 "공중/자기구동 상태만 빼고 나머지는 전부 기본 보호). 끄면 예전처럼 각 상태가 스스로 " +
                 "부른 것만 동작한다(네거티브 컨트롤).")]
        public bool groundKeepingSafetyNetEnabled = true;

        [Tooltip("★ Dock 사각지대 즉시 회수(기본 ON). Dock 가로 구간의 화면 최하단은 '물리적으로는 " +
                 "떠받쳐지지만 논리적으로는 접지하지 않는' 사각지대다(Editor/SceneBootstrapper의 " +
                 "CreateGroundCollider 문서 참고). 그리로 흘러든 캐릭터는 Fall 상태인데도 **속도 0으로 " +
                 "멈춰 있어** 착지가 영원히 확정되지 않고, 6초 뒤 화면 가로 중앙으로 순간이동하는 " +
                 "최종 안전망(RescueToSafeGround)에 걸릴 때까지 Dock 아래에 박혀 있었다. 켜면 그 상태를 " +
                 "속도로 감지해 **그 자리에서 바로 위 발판(=Dock 상단)으로 올려세운다** — 가로 " +
                 "순간이동이 없고 6초가 아니라 sinkholeLiftRestSeconds 만에 회복된다. " +
                 "끄면 예전 거동(6초 후 화면 중앙 복귀)으로 돌아간다(네거티브 컨트롤).")]
        public bool sinkholeLiftRecoveryEnabled = true;

        [Tooltip("사각지대 판정에 필요한 '멈춰 있음' 지속 시간(초). 낙하 중에는 속도가 0으로 이만큼 " +
                 "유지될 수 없으므로 오탐이 구조적으로 어렵다.")]
        public float sinkholeLiftRestSeconds = 0.35f;

        [Tooltip("★★ Dock 물리 계단(기본 ON) — 위 '사각지대 회수'가 대증요법이라면 이쪽이 원인 제거다.\n\n" +
                 "물리 바닥(PhysicsGround)은 화면 최하단에 깔린 전체 폭 한 장인데 그 위의 논리 발판" +
                 "(Dock 상단)은 1.64유닛 더 높은 곳에 떠 있어서, Dock 가로 구간 바로 아래에 큰 빈 " +
                 "공간이 있었다. 켜면 Dock 가로 구간 아래에 **Dock 상단 높이의 물리 콜라이더 계단**을 " +
                 "런타임으로 놓아(Platform/DockPhysicsStep.cs) 그 빈 공간 자체를 없앤다 — 접지 스냅이 " +
                 "잠시 끊겨도 캐릭터는 Dock 상단 높이에서 바로 멈추고, 자유낙하가 애초에 발생하지 " +
                 "않는다. 계단의 사각형은 Dock 발판/안전망 구멍과 **완전히 같은 단일 소스**에서 나오며 " +
                 "Dock 폭이 실행 중에 바뀌면 함께 따라간다(정적으로 굽지 않는 이유는 그 파일 문서 참고).\n\n" +
                 "끄면 예전처럼 Dock 구간 아래가 뻥 뚫린 채로 남아 사각지대 회수/6초 안전망에 의존한다" +
                 "(네거티브 컨트롤 — Tests/PlayMode/DockPhysicsStepTests.cs가 이 스위치로 대조한다).")]
        public bool dockPhysicsStepEnabled = true;

        [Tooltip("사각지대 회수가 캐릭터를 끌어올릴 수 있는 최대 높이(**신장 배수**). Dock 단차는 " +
                 "신장의 약 0.96배(1.64유닛 / 신장 1.71유닛)라 기본 1.5배면 충분하고, 그보다 큰 " +
                 "낙차는 '진짜로 잃어버린 것'이라 기존 6초 안전망에 맡긴다. ★ 거리 성분이라 " +
                 "신장 배수로 노출한다(캐릭터 배율 불변).")]
        public float sinkholeLiftMaxHeights = 1.5f;

        [Tooltip("무릎앉아가 **최대 깊이**가 되는 낙하 높이 — rollLandingHeightThreshold 위로 신장의 " +
                 "몇 배를 더 떨어졌을 때인가(신장 배수). 기본 3은 배율 0.75(신장 1.71유닛)에서 " +
                 "임계값 2유닛 + 5.12유닛 = 7.1유닛 낙하다. 즉 '화면 위쪽 창에서 바닥까지' 정도가 " +
                 "최대 깊이가 된다. ★ 거리 성분이라 신장 배수로 노출한다.")]
        public float landingCrouchDeepFallHeights = 3f;

        [Tooltip("임계값을 **갓 넘긴** 낙하에서의 깊이 비율(0~1). 0으로 두면 임계값 근처의 착지가 " +
                 "'앉는 시늉만 하고 마는' 밋밋한 그림이 되므로 바닥값을 준다.")]
        public float landingCrouchMinDepth01 = 0.45f;

        [Tooltip("가장 얕은 무릎앉아의 총 지속 시간(초) — 임계값을 갓 넘긴 낙하.")]
        public float landingCrouchDurationShallow = 0.32f;

        [Tooltip("가장 깊은 무릎앉아의 총 지속 시간(초) — landingCrouchDeepFallHeights 이상 낙하. " +
                 "'높을수록 더 깊이 앉고 더 오래 유지'(리더 지시)의 시간 쪽 절반이다. " +
                 "★ 시간은 거리가 아니라 배율과 무관하므로 절대값이 맞다.")]
        public float landingCrouchDurationDeep = 0.62f;

        [Tooltip("총 지속 시간 중 '눌리는' 구간의 비율(0~1). 착지 충격이므로 아주 짧고 빨라야 한다 — " +
                 "이 구간이 길면 앉는 동작이 스스로 앉는 것처럼 보여 충격 흡수로 읽히지 않는다.")]
        public float landingCrouchCompressFraction = 0.18f;

        [Tooltip("총 지속 시간 중 '가장 깊은 자세로 버티는' 구간의 비율(0~1). 이 정지 구간이 있어야 " +
                 "포즈가 한 장의 그림으로 눈에 남는다(애니메이션의 hold/moving hold 관행).")]
        public float landingCrouchHoldFraction = 0.24f;

        [Tooltip("일어서는 구간 끝에서 중립보다 살짝 더 펴지는 반동의 크기(0~1). '눌렸다가 펴지는 " +
                 "리듬'(리더 지시)을 만드는 값이며, 0이면 그냥 스르륵 일어난다. 무릎 굽힘은 어떤 " +
                 "경우에도 음수(뒤로 꺾임)가 되지 않도록 코드에서 0에서 잘린다.")]
        public float landingCrouchReboundAmount = 0.22f;

        [Tooltip("최대 깊이에서 **앞다리** 엉덩이 각도(도, + = 진행 방향). 앞발이 몸 앞쪽 바닥을 " +
                 "디디는 다리다.")]
        public float landingCrouchFrontHipDegrees = 82f;

        [Tooltip("최대 깊이에서 앞다리 무릎 굽힘(도, 항상 0 이상). 이 값이 사실상 '얼마나 낮게 앉는가'를 " +
                 "결정한다 — 몸 높이는 별도 거리 값이 아니라 이 각도에서 유도되기 때문이다" +
                 "(StickmanPoseAnimator.ComputeFootGroundingOffset).\n\n" +
                 "★ 기본값은 계산 + 실물 스크린샷 확인으로 정했다. 프리팹 기하(허벅지 0.50 / 정강이 0.45 / " +
                 "엉덩이 높이 0.9347 — 전부 배율에 비례)에서 앞다리 (82도, 126도) + 뒷다리 (−40도, 55도)면 " +
                 "몸이 **신장의 약 24%** 내려가고 그때 뒷다리 무릎이 지면 바로 위(신장의 0.5%)에 놓인다 — " +
                 "즉 '한쪽 무릎이 바닥에 닿는' 실루엣이 기하학적으로 보장된다.\n\n" +
                 "이력(실물 확인으로 두 번 조정): (58도, 92도)는 11%밖에 안 내려가 '살짝 굽힘'이었고, " +
                 "(78도, 112도)는 20%로 내려갔지만 실제 화면에서 **무릎앉기가 아니라 성큼 내딛은 런지**로 " +
                 "읽혔다(뒷무릎이 지면에서 신장의 10% 떠 있었다). 지금 값은 뒷허벅지를 더 뒤로 눕혀 " +
                 "무릎을 지면까지 내리고, 그만큼 몸이 더 낮아진다.\n\n" +
                 "★ 더 깊게 만들 수 없는 이유(기하학적 상한): 뒷무릎이 지면에 닿는 순간 엉덩이 높이는 " +
                 "허벅지 길이로 고정된다. 다리 전체가 신장의 41%뿐이므로 '무릎을 꿇은 자세'에서 몸이 " +
                 "내려갈 수 있는 최대치가 이 근처다 — 더 내리려면 무릎이 바닥을 뚫는다.")]
        public float landingCrouchFrontKneeDegrees = 126f;

        [Tooltip("최대 깊이에서 **뒷다리** 엉덩이 각도(도, − = 뒤쪽). 뒷다리는 무릎이 바닥에 닿는 쪽이다. " +
                 "허벅지를 뒤로 눕힐수록 무릎이 엉덩이에 가까워져 몸이 더 낮게 내려갈 수 있다 — 다만 " +
                 "앞다리 발끝보다 무릎이 더 내려가면 무릎이 바닥을 뚫으므로, 앞다리 각도와 짝으로 정해야 " +
                 "한다(그 계산은 landingCrouchFrontKneeDegrees Tooltip 참고).")]
        public float landingCrouchRearHipDegrees = -40f;

        [Tooltip("최대 깊이에서 뒷다리 무릎 굽힘(도, 항상 0 이상). 엉덩이 각도와 합쳐 정강이가 거의 " +
                 "수평(약 −95도)이 되게 잡으면, 정강이가 바닥을 따라 뒤로 뻗은 '한쪽 무릎 착지' 실루엣이 " +
                 "나온다. 기본값 55 = 뒷엉덩이 −40 + 55 -> 정강이 −95도.")]
        public float landingCrouchRearKneeDegrees = 55f;

        [Tooltip("최대 깊이에서 **앞팔** 어깨 각도(도, + = 진행 방향). 손이 바닥 쪽으로 내려가 몸을 " +
                 "받치는 팔이다(3점 착지의 그 팔).")]
        public float landingCrouchFrontArmDegrees = 64f;

        [Tooltip("최대 깊이에서 앞팔 팔꿈치 굽힘(도, 항상 0 이상).")]
        public float landingCrouchFrontElbowDegrees = 26f;

        [Tooltip("최대 깊이에서 **뒷팔** 어깨 각도(도, − = 뒤쪽). 뒤로 크게 젖혀 균형을 잡는 팔이며, " +
                 "이 좌우 비대칭이 '멋지게'의 실질적인 내용이다 — 두 팔이 대칭이면 그냥 쪼그려 앉은 " +
                 "그림이 된다.")]
        public float landingCrouchRearArmDegrees = -128f;

        [Tooltip("최대 깊이에서 뒷팔 팔꿈치 굽힘(도, 항상 0 이상).")]
        public float landingCrouchRearElbowDegrees = 24f;

        [Tooltip("무릎앉아 포즈 각도의 지수 감쇠 계수(1/초). poseSmoothingRate(35)보다 높게 두는 이유: " +
                 "이 포즈는 지속 상태가 아니라 0.3~0.6초짜리 **정해진 곡선**이라, 보간이 느리면 눌리는 " +
                 "구간이 통째로 뭉개져 '툭 앉았다'가 사라진다. 프레임레이트 독립 공식" +
                 "(1-exp(-k*dt))이라 이 값을 올려도 fps에 따라 결과가 달라지지 않는다.")]
        public float landingCrouchPoseSmoothingRate = 48f;

        [Tooltip("착지 직후 남은 수평 속도를 죽이는 지수 감쇠 계수(1/초). 0이면 앉은 채로 옆으로 " +
                 "미끄러진다. 너무 크면 공중에서의 수평 이동이 착지 순간 뚝 끊겨 부자연스럽다.")]
        public float landingCrouchHorizontalDamping = 12f;

        // ============================================================================
        [Header("던지기 공중 회전(텀블링) — 2026-08-29 사용자 요청")]
        // "마우스로 던졌을때도 이상하게 관절꺽이면서 넘어지는데 던져도 공중에서 회전하면서
        // 무릎앉아 착지할수있게 해줘"
        //
        // ★ 단위 규약(리더 지시 그대로): **각도/각속도는 크기와 무관한 양이라 절대값**이 맞고,
        //   **거리·속도 성분만** StickmanMetrics.TotalHeight로 나눠 무차원화한다(초당 몇 신장).
        //   그래서 아래에서 "회전 속도"는 도/초 절대값이고, 그 입력이 되는 던진 속도만 신장 배수다.
        // ============================================================================

        [Tooltip("던지기 공중 회전 연출의 마스터 스위치(끄면 예전 거동으로 완전 복귀). 끄면 " +
                 "DragThrowState가 놓는 순간 예전처럼 '충격량 >= ragdollForceThreshold면 Ragdoll, " +
                 "아니면 Fall'로 분기한다 — Tests/PlayMode/ThrowTumbleTests.cs의 네거티브 컨트롤이 " +
                 "이 스위치를 끄고 '실제로 랙돌이 되는지'를 확인한다.")]
        public bool throwTumbleEnabled = true;

        [Tooltip("공중 회전으로 볼 최소 던지기 속도(초당 몇 신장). 이보다 느리게 놓으면 '던진 것'이 " +
                 "아니라 '내려놓은 것'이므로 회전 없이 평범한 Fall로 보낸다 — 살짝 집었다 놓을 때마다 " +
                 "공중제비를 도는 것이 오히려 고장으로 읽힌다. ★ 속도는 거리 성분이라 신장으로 나눈 " +
                 "무차원 값으로 노출한다(배율 0.75, 신장 1.71유닛에서 1.2 = 약 2.05유닛/초).")]
        public float throwTumbleMinSpeedHeightsPerSecond = 1.2f;

        [Tooltip("던진 속도(초당 신장 배수) 1당 회전 속도(도/초). 세게 던질수록 빨리 돈다는 관계를 " +
                 "정하는 유일한 계수다. 던지기 속도 상한(dragThrowMaxSpeed 12유닛/초)은 배율 0.75에서 " +
                 "약 7.0신장/초이므로 기본값 90이면 630도/초(초당 1.75바퀴)가 된다.")]
        public float throwTumbleDegreesPerHeightSpeed = 90f;

        [Tooltip("회전 속도의 하한(도/초). 최소 던지기 속도 근처에서도 '회전한다'고 알아볼 수 있어야 " +
                 "한다 — 너무 느리면 회전이 아니라 기울어진 채 날아가는 것으로 보인다.")]
        public float throwTumbleMinSpinDegreesPerSecond = 220f;

        [Tooltip("회전 속도의 상한(도/초). 너무 빠르면 잔상처럼 뭉개져 자세를 알아볼 수 없고, " +
                 "착지 정렬(아래)이 마무리할 각도도 그만큼 커진다.")]
        public float throwTumbleMaxSpinDegreesPerSecond = 720f;

        [Tooltip("착지 **몇 초 전에** 회전을 끝내고 몸을 바로 세울지(초). 정렬을 착지 순간에 딱 맞추면 " +
                 "예측 오차가 그대로 '기울어진 채 착지'로 나타나므로, 이만큼 여유를 두고 먼저 끝낸 뒤 " +
                 "직립으로 떨어지게 한다. 0으로 두면 착지 순간에 정확히 맞추려 시도한다(권장하지 않음).")]
        public float throwTumbleAlignLeadSeconds = 0.1f;

        [Tooltip("정렬 구간에서 허용하는 회전 속도의 배율 상한(회전 속도 대비). 정렬은 '남은 각도 ÷ " +
                 "남은 시간'으로 매 프레임 다시 계산해 자기 보정하는데, 예측이 흔들릴 때 이 상한이 " +
                 "없으면 마지막 순간에 팽이처럼 튀는 프레임이 생긴다. 1.0이면 절대 더 빨라지지 않는다" +
                 "(대신 정렬을 못 끝낼 수 있다).")]
        public float throwTumbleAlignMaxSpeedFactor = 1.6f;

        [Tooltip("공중 회전 상태의 최대 지속 시간(초) — 안전 상한. 발판이 하나도 없거나 예측이 " +
                 "실패해 영영 착지하지 못하는 경우 이 시간이 지나면 평범한 Fall로 빠져나간다" +
                 "(그 뒤로는 기존 낙하/구조 안전망이 그대로 받는다).")]
        public float throwTumbleMaxSeconds = 6f;

        [Tooltip("정렬 구간에서 웅크린 정도를 얼마까지 풀지(0~1, 0=완전히 편 자세). 회전이 끝나고 " +
                 "착지를 준비하는 동안 몸을 펴야 '착지 자세를 잡는다'로 읽힌다 — 웅크린 공 그대로 " +
                 "떨어지면 무릎앉아로 이어지는 흐름이 끊긴다.")]
        public float throwTumbleLandingTuck01 = 0.15f;

        [Tooltip("웅크린 정도가 목표값으로 수렴하는 지수 감쇠 계수(1/초). 프레임레이트 독립 공식" +
                 "(1-exp(-k*dt))이라 fps에 따라 결과가 달라지지 않는다.")]
        public float throwTumbleTuckFadeRate = 10f;

        [Tooltip("공중 회전으로 날아온 착지는 낙차가 작아도 항상 무릎앉아로 받을지. 켜두는 이유: " +
                 "한 바퀴 이상 돌고 내려온 뒤 아무 일 없었다는 듯 똑바로 서면 그 자체가 고장으로 " +
                 "읽힌다(연출의 마무리가 없다). 앉는 깊이는 여전히 아래 충격 세기에서 파생되므로, " +
                 "살짝 던진 착지는 얕게 앉는다. 끄면 평범한 낙하와 같은 낙차 임계값" +
                 "(rollLandingHeightThreshold)을 그대로 따른다.")]
        public bool throwTumbleAlwaysCrouchOnLanding = true;

        [Tooltip("착지 충격 세기를 '같은 충격의 낙하 높이'로 환산할 때 **수평 속도**를 얼마나 " +
                 "반영할지(0~1). 1이면 옆으로 세게 던져 미끄러지듯 닿아도 수직 낙하와 같은 깊이로 " +
                 "앉는다. 환산식은 에너지 보존 그대로다(h = v²/2g) — 순수 자유낙하에서는 이 값과 " +
                 "무관하게 실제 낙하 높이와 정확히 일치하므로, 기존 무릎앉아 깊이 램프와 단위가 " +
                 "어긋날 수 없다(States/ThrowTumbleState.ConfirmLanding 참고).")]
        public float throwTumbleImpactHorizontalWeight = 0.5f;

        [Tooltip("공중 회전 중 **엉덩이** 각도(도, + = 진행 방향). 다리를 몸 앞으로 크게 접어 올린 " +
                 "웅크린 텀블링 자세를 만든다 — 사람이 공중제비를 돌 때 몸을 웅크리는 이유는 회전 " +
                 "관성을 줄이기 위해서이고, 시각적으로도 그래야 '회전'으로 읽힌다.")]
        public float throwTumbleHipDegrees = 76f;

        [Tooltip("공중 회전 중 무릎 굽힘(도, 항상 0 이상 = 뒤로 접힘).")]
        public float throwTumbleKneeBendDegrees = 104f;

        [Tooltip("공중 회전 중 **어깨** 각도(도, + = 진행 방향). 팔을 앞으로 모아 몸을 감싼다.")]
        public float throwTumbleArmDegrees = 46f;

        [Tooltip("공중 회전 중 팔꿈치 굽힘(도, 항상 0 이상). 크게 접어야 팔이 몸을 감싼 것으로 " +
                 "보인다(마디 로컬 각도라 팔뚝의 절대 각도는 어깨 각도 + 이 값이다).")]
        public float throwTumbleElbowBendDegrees = 96f;

        [Tooltip("공중 회전 중 좌우 팔다리를 벌리는 각도(도). 0이면 좌우가 완전히 겹쳐 한 쌍처럼 " +
                 "보이므로 아주 조금만 벌려 깊이감을 준다.")]
        public float throwTumbleLimbSpreadDegrees = 9f;

        // ============================================================================
        [Header("붙잡혔을 때 발버둥 — 2026-08-29 사용자 요청")]
        // "마우스로 캐릭을 잡았을때 막 벗어날려는듯이 몸부림 치게끔 만들어줘"
        //
        // 이 값들은 전부 **각도/주파수/시간**이라 캐릭터 배율과 무관한 절대값이 맞다(리더 지시).
        // 배율을 따라야 하는 거리 성분은 여기 하나도 없다 — 유일하게 거리 차원인 커서 속도만
        // StickmanMetrics의 신장으로 나눠 무차원화해서 쓴다(dragStruggleCursorSpeedResponse).
        // ============================================================================

        [Tooltip("발버둥 연출의 마스터 스위치. 끄면 드래그 중 자세가 예전과 100% 동일하게 Idle 중립 " +
                 "포즈로 돌아간다(StickmanBlackboard.TickPose의 Dragged 분기가 그 경로를 그대로 " +
                 "유지한다). Tests/PlayMode/DragStruggleTests.cs의 네거티브 컨트롤이 이 스위치를 끄고 " +
                 "'관절이 실제로 멈추는지'를 확인한다.")]
        public bool dragStruggleEnabled = true;

        [Tooltip("발버둥의 기본 주파수(Hz, 다리 기준). 팔은 이 주파수의 1.37배로 움직인다" +
                 "(StickmanPoseAnimator.StruggleArmFrequencyRatio — 정수배가 아니어야 규칙적인 " +
                 "루프로 보이지 않는다). 너무 높으면 떨림처럼, 너무 낮으면 기지개처럼 보인다.")]
        public float dragStruggleFrequencyHz = 3.4f;

        [Tooltip("발버둥 최대 세기에서 엉덩이 각도의 진폭(도). 다리를 앞뒤로 차는 크기다.")]
        public float dragStruggleHipDegrees = 34f;

        [Tooltip("발버둥 최대 세기에서 무릎이 추가로 접히는 각도(도). Idle 중립 굽힘 위에 더해지며 " +
                 "0~이 값 사이를 오간다(음수가 되지 않으므로 무릎이 반대로 꺾이지 않는다).")]
        public float dragStruggleKneeDegrees = 40f;

        [Tooltip("발버둥 최대 세기에서 어깨 각도의 진폭(도). 팔을 휘젓는 크기다.")]
        public float dragStruggleArmDegrees = 46f;

        [Tooltip("발버둥 최대 세기에서 팔꿈치가 추가로 접히는 각도(도).")]
        public float dragStruggleElbowDegrees = 38f;

        [Tooltip("발버둥 최대 세기에서 몸통이 좌우로 비틀리는 각도(도). 루트의 **시각 회전**으로만 " +
                 "만들고 루트 위치는 절대 흔들지 않는다 — 위치를 흔들면 '커서에 딱 붙어 끌려온다'는 " +
                 "이전 라운드 수정(dragFollowSmoothTime=0 즉시 대입)이 무효가 된다. 팔다리보다 느린 " +
                 "주기로 비틀려야 '허우적'이 아니라 '벗어나려 몸을 튼다'로 읽힌다.")]
        public float dragStruggleTwistDegrees = 9f;

        [Tooltip("★ 몸부림 리듬의 한 주기(초). '세게 몸부림 → 잠깐 지침'을 이 주기로 반복한다 — " +
                 "일정한 진폭으로 계속 흔들면 살아 있는 것이 아니라 루프 애니메이션으로 보인다" +
                 "(리더 지시). 한 주기 안에서 앞의 dragStruggleBurstDutyFraction만큼이 몸부림 " +
                 "구간(사인 한 봉우리)이고 나머지가 지침 구간이다.")]
        public float dragStruggleBurstPeriodSeconds = 1.15f;

        [Tooltip("한 주기 중 몸부림(버스트) 구간의 비율(0~1). 나머지가 지침 구간이다. 너무 크면 쉬는 " +
                 "느낌이 사라지고, 너무 작으면 축 늘어져 보인다.")]
        public float dragStruggleBurstDutyFraction = 0.55f;

        [Tooltip("지침 구간의 세기(0~1). 0이면 완전히 축 늘어져 '기절'처럼 보이므로, 숨을 고르는 " +
                 "정도의 바닥값을 준다.")]
        public float dragStruggleRestIntensity = 0.18f;

        [Tooltip("잡혀 있는 시간이 길어질수록 잦아드는 속도 — 세기가 절반만큼 줄어드는 데 걸리는 " +
                 "시간(초). 잡힌 직후가 가장 격렬하고 점점 지치는 것이 자연스럽다(리더 지시). " +
                 "0 이하로 두면 지침 없이 계속 같은 세기로 몸부림친다.")]
        public float dragStruggleFatigueHalfLifeSeconds = 4.5f;

        [Tooltip("아무리 오래 잡고 있어도 세기가 이 아래로는 내려가지 않는다(0~1). 0으로 두면 오래 " +
                 "잡고 있을 때 완전히 멈춰 '죽은 것'처럼 보인다.")]
        public float dragStruggleMinIntensity = 0.4f;

        [Tooltip("커서를 빠르게 흔들수록 더 격렬해지는 정도 — 커서 속도 1신장/초당 더해지는 세기. " +
                 "★ 속도는 거리 성분이라 신장으로 나눠 무차원화한다(배율이 바뀌어도 '같은 체감 " +
                 "속도'에서 같은 반응). 0으로 두면 커서 속도에 반응하지 않는다.")]
        public float dragStruggleCursorSpeedResponse = 0.12f;

        [Tooltip("커서 속도로 더할 수 있는 세기의 상한. 상한이 없으면 커서를 빠르게 흔드는 동안 " +
                 "진폭이 무한히 커져 팔다리가 뒤엉킨 것처럼 보인다.")]
        public float dragStruggleMaxCursorBoost = 0.6f;

        // ============================================================================
        // 활쏘기 (사용자 명시 요청 2026-08-29: "하는 행동중 하나가 활을 들고 화살을 쏘는건데
        // 과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동을 하는거지")
        // ============================================================================
        // ★ 거리/크기/속도 성분은 전부 **캐릭터 신장 대비 비율**이다(리더 지시: 배율이 바뀌어도
        //   연출이 같은 그림이어야 한다). 시간(초)과 각도(도)는 크기와 무관한 양이라 절대값이 맞다.
        //   기준 신장의 유일한 조회 경로는 Core/StickmanMetrics.cs다.

        [Header("활쏘기 (2026-08-29 사용자 요청)")]

        [Tooltip("자율 발동 확률(0~1). ★ 기본값 0 = 사용자가 부르지 않으면 절대 발동하지 않는다. " +
                 "이 프로젝트 사용자는 요청하지 않은 연출이 뜨는 것에 반복적으로 불만을 표했고 " +
                 "직전 라운드에 구경거리 연출 전부가 기본 OFF로 내려갔다(다른 *Chance 필드들과 동일). " +
                 "발동 경로는 전역 단축키 Ctrl+Opt+Cmd+A와 캐릭터 우클릭 메뉴 두 가지뿐이며, 그 " +
                 "수동 경로는 이 값을 읽지 않으므로 여기를 0으로 둬도 언제든 볼 수 있다.")]
        public float archeryChance = 0f;

        [Tooltip("자율 발동 추첨 주기(초). archeryChance가 0이면 추첨 자체가 무의미하다.")]
        public float archeryCheckInterval = 60f;

        [Tooltip("한 사이클이 끝난 뒤 다음 자율 발동까지의 최소 쿨다운(초).")]
        public float archeryCooldownSeconds = 600f;

        [Tooltip("과녁까지의 **기준** 거리 — 캐릭터 신장 배수. 랜덤 밴드" +
                 "(archeryMinTargetDistanceRatio ~ archeryMaxTargetDistanceRatio)의 한가운데 값이자, " +
                 "화살 비행 시간을 거리에 맞춰 늘리는 기준 사거리다" +
                 "(States/ArcheryState.ResolveFlightSeconds). 4.6이면 신장 1.71유닛(배율 0.75)에서 " +
                 "약 7.9유닛(화면상 약 276pt) 앞이다.\n\n" +
                 "★ 2026-08-29 사용자 신고 '과녁이 너무 가까이 생성됨'으로 2.8 -> 4.6. 사거리는 " +
                 "'곡선으로 멀리 날아간다'는 요구와도 직결된다 — 짧으면 포물선이 그려질 공간 자체가 " +
                 "없어 직선처럼 보인다.\n\n" +
                 "★ 2026-08-31 사용자 신고 '무조건 과녁이 화면 끝에만 생김 / 거리는 항상 랜덤' 이후로는 " +
                 "이 값이 **직접 쓰이는 배치 거리**가 아니다 — 실제 사거리는 아래 두 값 사이에서 매번 " +
                 "추첨된다(Interaction/ArcheryDirector.ResolvePlacement).")]
        public float archeryTargetDistanceRatio = 4.6f;

        [Tooltip("랜덤 사거리 밴드의 **하한** — 캐릭터 신장 배수. 이보다 가까우면 " +
                 "'쏘는' 것이 아니라 '찌르는' 것처럼 보이고 포물선도 거의 직선이 되므로, 차라리 " +
                 "발동하지 않는다(반대편 미러링 -> 그래도 안 되면 조용히 포기).")]
        public float archeryMinTargetDistanceRatio = 2.6f;

        [Tooltip("랜덤 사거리 밴드의 **상한** — 캐릭터 신장 배수(★ 2026-08-31 신설).\n\n" +
                 "신고 원문: '활쏘기 시키면 무조건 과녁이 화면 끝에만 생김. 적당히 먼 거리만 되도 " +
                 "되는데 물론 거리는 항상 랜덤으로 변경되어야 하지만'. 이전 배치는 캐릭터를 발판 한쪽 " +
                 "끝, 과녁을 반대쪽 끝에 **결정론적으로** 놓아서 넓은 바탕화면에서는 언제나 화면 " +
                 "가장자리였다.\n\n" +
                 "이 상한은 화면/창 폭과 **무관한 절대 밴드**라는 점이 중요하다 — 구간이 아무리 넓어도 " +
                 "과녁이 끝까지 밀려나지 않는다. 기본 6.6은 기준값 4.6을 가운데 두고 하한 2.6과 대칭이며, " +
                 "신장 1.71유닛(배율 0.75) 기준 약 4.4~11.3유닛(가시 폭 약 37유닛의 12~31%)이다.")]
        public float archeryMaxTargetDistanceRatio = 6.6f;

        [Tooltip("과녁 바깥 링의 반지름 — **캐릭터 신장 배수**. 과녁 중심 높이는 별도 설정값이 아니라 " +
                 "이 값에서 유도된다: 과녁 꼭대기가 정확히 캐릭터 정수리 높이가 되도록 " +
                 "중심 높이 = 신장 - 반지름 (Interaction/ArcheryDirector.TargetCenterHeight). " +
                 "그래서 '과녁은 캐릭터와 같은 키'라는 관계가 어느 배율에서도 유지되고, 화면 세로 " +
                 "판정이 캐릭터 자신의 판정과 같아진다(둘이 따로 놀 경우의 수가 없다).")]
        public float archeryTargetRadiusRatio = 0.40f;

        [Tooltip("빗나간 화살이 과녁보다 **얼마나 앞에** 떨어지는가 — 과녁 반지름 배수(1보다 커야 한다). " +
                 "과녁 뒤로 넘기지 않는 이유: 그러면 궤적이 과녁 면 안쪽을 통과해 '빗나갔다'가 아니라 " +
                 "'뚫었다'로 읽힌다. 앞 땅에 꽂히면 화살이 과녁 x에 도달하기 전에 끝나므로 겹칠 " +
                 "경우의 수 자체가 없다(States/ArcheryState.ComputeImpactWorld 주석 참고).")]
        public float archeryMissShortfallRadii = 1.5f;

        [Tooltip("화살이 날아가는 데 걸리는 시간(초). 도달점과 이 값이 정해지면 포물선의 초기 속도는 " +
                 "유일하게 결정된다 — 힘을 주고 결과를 지켜보는 물리 시뮬레이션이 아니라 **역산**이라 " +
                 "프레임레이트/충돌 우연으로 연출이 달라지지 않는다(리더 지시).\n\n" +
                 "★ 2026-08-29 사용자 신고 '화살이 너무 늦게 날라감'으로 0.85 -> 0.62초. 사거리 " +
                 "7.9유닛을 0.62초에 지나가므로 초당 약 12.7유닛(화면상 약 520pt/초)이다. " +
                 "이 값을 더 줄이면 포물선을 눈으로 따라갈 수 없고, 늘리면 정점에서 떠 있는 시간이 " +
                 "길어져 늘어진다(둘의 균형점).")]
        public float archeryArrowFlightSeconds = 0.62f;

        [Tooltip("비행 시간의 상한(초). 사거리가 길어지면 비행 시간이 함께 늘어나는데" +
                 "(ArcheryState.ResolveFlightSeconds), 창 폭만 한 25유닛짜리 사격에서까지 비례로 늘리면 " +
                 "화살이 하늘에 한참 떠 있어 늘어진다. 여기서 잘라 '멀어도 답답하지 않게' 만든다.\n\n" +
                 "★ 사거리가 기준(archeryTargetDistanceRatio × 신장)일 때 정확히 " +
                 "archeryArrowFlightSeconds가 되고, 그보다 멀면 **거리의 제곱근**에 비례해 늘어난다. " +
                 "선형으로 늘리면 먼 사격이 두 배 넘게 느려지고, 고정으로 두면 먼 사격이 눈으로 " +
                 "따라갈 수 없는 섬광이 된다 — 제곱근이 그 사이의 균형점이다.")]
        public float archeryArrowFlightMaxSeconds = 1.25f;

        [Tooltip("포물선이 '발사점-도달점 직선' 위로 부푸는 최대 높이 — **캐릭터 신장 배수**. " +
                 "이 값이 곧 포물선의 볼록함이며, 중력 상수는 여기서 역으로 유도된다" +
                 "(g = 8*apex/T^2, ArcheryRenderer.SolveGravity). 배율이 바뀌어도 궤적의 **모양**이 " +
                 "그대로 유지되는 이유다.\n\n" +
                 "★ 상한의 근거: 이 값을 키우면 화살 최고점이 캐릭터 정수리 위로 올라가고, 캐릭터가 " +
                 "화면 맨 위 창 테두리에 서 있을 때 궤적 윗부분이 화면 밖으로 잘린다. 0.38이면 " +
                 "최고점이 정수리 바로 언저리라 '캐릭터가 보이면 궤적도 보인다'가 대체로 성립한다.")]
        public float archeryArrowArcApexRatio = 0.38f;

        [Tooltip("과녁을 세울 자리까지 걸어가는 데 허용하는 최대 시간(초). 이 시간을 넘기면 그 자리에서 " +
                 "시작한다 — 발판이 도중에 사라지거나 화면 클램프에 걸려 목표 X에 영원히 도달하지 못할 " +
                 "수 있는데, 그때 '아무 일도 일어나지 않는' 것보다는 조금 가까운 거리에서라도 쏘는 편이 낫다.")]
        public float archeryApproachTimeoutSeconds = 12f;

        [Tooltip("과녁이 등장해 자리를 잡는 시간(초). 이 시간이 지나야 첫 발을 당기기 시작한다 — " +
                 "상태(ArcheryState)와 렌더러(ArcheryRenderer)가 **같은 이 값**을 읽으므로 '과녁이 아직 " +
                 "커지는 중인데 이미 쏘고 있는' 어긋남이 생기지 않는다.")]
        public float archeryTargetIntroSeconds = 0.55f;

        [Tooltip("시위를 끝까지 당기는 데 걸리는 시간(초). easeOut이라 처음에 빠르게 당기고 마지막에 " +
                 "버티듯 느려진다.")]
        public float archeryDrawSeconds = 0.42f;

        [Tooltip("완전히 당긴 채 겨누고 멈춰 있는 시간(초). 애니메이션의 hold — 이 정지가 있어야 " +
                 "'겨눴다'가 한 장의 그림으로 눈에 남는다(무릎앉아 착지의 버팀 구간과 같은 관행).")]
        public float archeryAimHoldSeconds = 0.30f;

        [Tooltip("발사 후 다음 발을 당기기까지의 회복 시간(초).")]
        public float archeryRecoverSeconds = 0.34f;

        [Tooltip("발사 순간 당기던 팔이 튕겨 나가는 반동의 지속 시간(초). 0이면 반동 없이 그냥 놓는다.")]
        public float archeryRecoilSeconds = 0.18f;

        [Tooltip("마지막 화살이 도달한 뒤 과녁이 사라지기까지의 시간(초). 상태와 렌더러가 같은 값을 읽는다. " +
                 "★ 전체 연출이 늘어지지 않도록 0.75 -> 0.55(사용자 신고 대응).")]
        public float archeryOutroSeconds = 0.55f;

        [Tooltip("활을 쏘는 동안 남은 수평 속도를 죽이는 지수 감쇠 계수(1/초) — '캐릭터가 멈춰 서고'. " +
                 "0으로 즉시 대입하지 않는 이유는 무릎앉아 착지와 같다(뚝 끊기면 오히려 부자연스럽다).")]
        public float archeryHorizontalDamping = 14f;

        [Tooltip("활쏘기 포즈 각도의 지수 감쇠 계수(1/초). poseSmoothingRate(35)보다 높게 두는 이유는 " +
                 "무릎앉아와 같다 — 당기기/발사가 0.2~0.4초짜리 정해진 곡선이라 보간이 느리면 " +
                 "'튕겨 나가는 반동'이 통째로 뭉개진다. 프레임레이트 독립 공식(1-exp(-k*dt))이라 " +
                 "이 값을 올려도 fps에 따라 결과가 달라지지 않는다.")]
        public float archeryPoseSmoothingRate = 46f;

        [Tooltip("포물선 볼록함을 **수평 사거리에 비례**시키는 계수. 0.24면 7.9유닛을 쏠 때 현 위로 " +
                 "약 1.9유닛(화면상 약 66pt) 부푼다.\n\n" +
                 "★ 2026-08-29 사용자 신고 '화살이 곡선으로 멀리 날라가야하는데' 대응으로 신설. " +
                 "볼록함을 신장에만 비례시키면 사거리를 늘리는 순간 상대적으로 납작해져 직선처럼 " +
                 "보인다 — 멀리 쏠수록 더 높이 떠야 '곡선으로 멀리 날아간다'가 된다. " +
                 "아래 archeryArrowArcApexRatio가 하한(짧은 사격 방어), 카메라 상단이 상한" +
                 "(정점이 화면 밖으로 잘리지 않게)으로 함께 걸린다.")]
        public float archeryArrowArcApexDistanceRatio = 0.18f;

        [Tooltip("★ 2026-08-29 사용자 신고 '화살이 과녁에 좀 이상하게 꽂힘 / 다 외곽에 꽂히는거 같음' 대응.\n\n" +
                 "과녁 면에 꽂힌 화살이 수평에서 아래로 기울 수 있는 **최대 각도**(도). 위 " +
                 "archeryArrowArcApexDistanceRatio로 궤적을 과장하면 착탄 순간의 접선이 아주 가팔라져 " +
                 "(실측 42.9도) 화살이 과녁 면에 거의 수직으로 꽂힌 것처럼 보였다. 실제 양궁은 이 정도 " +
                 "사거리에서 거의 수평으로 꽂힌다. 비행 중의 과장된 회전은 그대로 두고 **마지막 " +
                 "archeryImpactSettleRatio 구간에서만** 이 값 이내로 부드럽게 눕힌다.")]
        [Range(0f, 60f)]
        public float archeryFaceImpactMaxDescentDegrees = 14f;

        [Tooltip("빗나가 **땅에** 꽂힌 화살이 지면과 이루는 각도(도). 과녁 면과 달리 땅에 꽂힌 화살은 " +
                 "비스듬해야 '박혔다'로 읽힌다(수평이면 바닥에 누워 있는 것처럼 보인다). 클램프가 아니라 " +
                 "**확정 각도**다 — 사거리가 달라져도 흙에 박힌 모양이 흔들리지 않게 하기 위해서다.")]
        [Range(5f, 80f)]
        public float archeryGroundImpactDescentDegrees = 38f;

        [Tooltip("착탄 각도 보정을 시작하는 지점 — 비행 시간의 **마지막 몇 할**(0~0.6). 0.22면 마지막 " +
                 "22%(비행 1.1초 기준 약 0.24초, 60fps에서 14프레임)에 걸쳐 접선 각도에서 착탄 각도로 " +
                 "smoothstep 보간한다. 0이면 보정 없음(= 접선 각도 그대로 꽂힘, 신고된 버그 재현).")]
        [Range(0f, 0.6f)]
        public float archeryImpactSettleRatio = 0.22f;

        // ---- 자세(각도, 도) — 크기와 무관한 양이므로 비율이 아니라 절대값이 맞다(리더 지시). ----
        // 각도 규약은 States/StickmanPoseAnimator.cs 전체와 같다: 마디 로컬 -y가 끝(손/발),
        // 각도 0이 "곧게 아래", 끝 방향은 (sinθ, -cosθ). 즉 +90이 정면(진행 방향) 수평이다.
        // 좌우 미러링은 최종 적용 시점(ApplyAngle)에서 facingSign이 곱해져 자동 처리된다.

        [Tooltip("활을 든 **앞팔** 어깨 각도(도). 104 = 수평(90)에서 위로 14도 들어올린 상태. " +
                 "★ 육안 검증 후 88 -> 104로 올렸다: 이 연출의 실제 조준각이 약 29도 위쪽이라 " +
                 "(먼 과녁 + 포물선) 팔을 수평으로 두면 활만 위로 기울고 팔은 그대로여서 " +
                 "'겨눈다'가 아니라 '활을 그냥 들고 있다'로 보였다.")]
        public float archeryBowArmDegrees = 104f;

        [Tooltip("활을 든 앞팔의 **팔꿈치 절대 각도**(도) — 상대 굽힘이 아니라 전완 자체의 각도다. " +
                 "실제 적용되는 상대 각도는 코드가 (이 값 - 어깨 각도)로 계산한다. 절대 각도로 두는 " +
                 "이유: 활을 든 팔은 '곧게 뻗는다'가 핵심인데, 그건 전완의 절대 방향이 어깨와 거의 " +
                 "같다는 뜻이라 절대값으로 적는 편이 의도가 그대로 읽힌다.")]
        public float archeryBowForearmDegrees = 108f;

        [Tooltip("시위를 당기는 **뒷팔** 어깨 각도(도, 완전히 당긴 상태). -100 = 위 팔이 뒤쪽으로 " +
                 "거의 수평(진행 반대 방향)으로 뻗어 팔꿈치가 몸 뒤로 빠진 자세.")]
        public float archeryDrawUpperDegrees = -99f;

        [Tooltip("시위를 당기는 뒷팔의 **전완 절대 각도**(도, 완전히 당긴 상태). 위 팔이 뒤를 향한 채 " +
                 "전완이 앞쪽 위로 접혀 손이 뺨 근처로 온다 — 실제 활쏘기의 만작 자세는 팔이 거의 " +
                 "완전히 접힌 상태(손과 어깨 거리가 상완+전완 길이보다 훨씬 짧다)라, 상대 굽힘이 " +
                 "180도 근처의 큰 값이 된다. 그래서 이 축도 절대 각도로 적는다.\n\n" +
                 "★ 육안 검증 후 100 -> 119로 올렸다. 100이면 손이 어깨 바로 옆(어깨에서 0.17유닛)에 " +
                 "와서 위 팔과 전완이 거의 겹쳐 **한 개의 막대**로 보였다(두 선의 화면상 간격이 3pt로 " +
                 "선 두께와 비슷했다). 119면 손이 턱 높이로 올라가 어깨~손 거리가 0.32유닛이 되고 " +
                 "팔꿈치가 뒤로 빠진 V자가 8pt 폭으로 벌어져 '당기고 있다'가 읽힌다.")]
        public float archeryDrawForearmDegrees = 119f;

        [Tooltip("발사 반동에서 뒷팔이 뒤로 튕겨 나가며 어깨가 추가로 열리는 각도(도). 실제 궁수의 " +
                 "'follow-through'다 — 이 반동이 없으면 화살만 사라지고 몸은 그대로라 발사 순간이 " +
                 "눈에 안 띈다.")]
        public float archeryRecoilOpenDegrees = -38f;

        [Tooltip("발사 반동에서 뒷팔 전완이 펴지는 정도(0~1). 1이면 완전히 펴진다.")]
        public float archeryRecoilStraighten01 = 0.75f;

        [Tooltip("활 쏘는 자세의 **앞다리** 엉덩이 각도(도, + = 진행 방향). 발을 앞뒤로 벌린 안정된 " +
                 "스탠스를 만든다.")]
        public float archeryFrontHipDegrees = 16f;

        [Tooltip("활 쏘는 자세의 **뒷다리** 엉덩이 각도(도, - = 뒤쪽).")]
        public float archeryRearHipDegrees = -18f;

        [Tooltip("활 쏘는 자세의 무릎 굽힘(도, 항상 0 이상). 살짝 굽혀 버티는 느낌을 만든다.")]
        public float archeryKneeBendDegrees = 12f;

        [Tooltip("완전히 당겼을 때 몸이 가라앉는 깊이 — **캐릭터 신장 배수**. 활을 당기는 힘에 " +
                 "몸이 살짝 뒤로/아래로 실리는 것을 표현한다(루트 회전은 능동 상태에서 고정이라 " +
                 "몸통을 기울일 수 없으므로, 시각 전용 상하 오프셋으로 대신한다). 0이면 가라앉지 않는다.")]
        public float archeryDrawBodySinkRatio = 0.022f;

        // ============================================================================
        // 캐릭터 성장(레벨/XP) + 장비 (2026-08-29 사용자 요청: "캐릭터 장비 착용 및 캐릭터 정보
        // 볼수있는 창을 만들어야함" — 리더가 "진짜 장비/스킨 + 레벨/능력치 육성 요소"로 범위 확정)
        // ============================================================================
        // ★ 시간(초/분)과 XP는 크기와 무관한 양이라 절대값이 맞다(신장 비율화 대상이 아니다).
        //   실제 곡선이 몇 시간짜리인지는 Core/CharacterProgressionModel.cs 클래스 문서의 환산표 참고.

        [Header("캐릭터 성장(레벨/XP) — 2026-08-29 사용자 요청")]

        [Tooltip("다음 레벨까지 필요한 XP = 이 값 x (현재 레벨 ^ 지수). 100이면 Lv1->2에 100XP.")]
        public float progressionXpCurveBase = 100f;

        [Tooltip("XP 곡선의 지수. 값이 클수록 레벨이 오를수록 급격히 느려진다(2.0 같은 큰 값은 " +
                 "며칠 만에 사실상 진행이 멈춘다). 1.05 = 32종 장비 카탈로그의 최고 요구 레벨(24)이 " +
                 "현실적으로 도달 가능하도록 2026-08-30에 1.15에서 완화한 값(Core/CharacterProgressionModel.cs " +
                 "클래스 문서 \"지수 완화\" 문단 참고).")]
        public float progressionXpCurveExponent = 1.05f;

        [Tooltip("★ 패시브 XP — 앱이 켜져 있기만 하면 분당 이만큼 쌓인다. 관찰형 앱 철학(‘아무것도 " +
                 "안 해도 자란다’)의 핵심이라 이것이 주 경로이고 아래 보너스는 가속일 뿐이다.\n\n" +
                 "1.5 = 시간당 90XP -> Lv1->2가 약 1.1시간, Lv2->3이 약 3.6시간(리더 목표 " +
                 "‘초반 레벨업이 1~3시간 안에’). 0으로 두면 패시브 적립이 멈춘다.")]
        public float progressionPassiveXpPerMinute = 1.5f;

        [Tooltip("패시브 XP 적립 주기(초). 분당 값을 이 간격으로 나눠 조금씩 넣는다 — 60초마다 " +
                 "한 번에 넣으면 정보창의 XP 바가 뚝뚝 끊겨 보인다. 너무 짧으면 이벤트 발행만 잦아진다.")]
        public float progressionPassiveTickSeconds = 10f;

        [Tooltip("격파 미니게임 성공 1회당 보너스 XP. ★ 승패 판정 자체는 건드리지 않는다 — " +
                 "Interaction/CharacterProgressionDirector.cs가 StickmanEventBus를 읽기 전용으로 구독할 뿐이다.")]
        public float progressionBattleWinXp = 25f;

        [Tooltip("활쏘기 정중앙 명중(Bullseye) 1회당 보너스 XP.")]
        public float progressionBullseyeXp = 15f;

        [Tooltip("자동 저장 주기(초). 값이 바뀌었을 때만 실제로 디스크에 쓴다. 레벨업/장비 변경/" +
                 "이름 변경은 이 주기와 무관하게 즉시 저장된다.")]
        public float progressionAutoSaveIntervalSeconds = 60f;

        // ============================================================================
        // 장비 잠금 해제 레벨은 여기 없다 (2026-08-30 32종 확장에서 제거)
        // ============================================================================
        // 확장 전에는 카테고리당 아이템이 하나뿐이라 `equipmentUnlockLevelHead/Eyes/Neck/Shoulders`
        // 4개 필드로 충분했다. 이제 요구 레벨은 **아이템 단위(8카테고리 × 4개 = 32개)**이고,
        // 그 표는 Core/ItemCatalog.cs에 있다. 32개를 인스펙터 필드로 늘어놓지 않은 이유:
        //   · 요구 레벨은 **콘텐츠 설계**(디자인 핸드오프에 확정된 값)이지 실행 중에 만져 보는
        //     튜닝 노브가 아니다 — 이 파일의 나머지 필드와 성격이 다르다.
        //   · 인스펙터에서 한 칸만 잘못 바뀌면 저장 파일(아이템 아이디)과 조용히 어긋난 채 굴러간다.
        //   · 자산 파일(DefaultStickConfig.asset)과 코드 표가 **두 벌의 진실**이 된다.
        // 옛 4개 필드는 읽는 코드가 사라져 삭제했다(자산 파일의 남은 줄은 Unity가 무시하지만,
        // 혼동을 막으려고 함께 지웠다).

        // ============================================================================
        // 배선 감사 잔여 3건 — 구독자 0명 이벤트에 붙인 부가 연출 (2026-08-30)
        // ============================================================================
        // 이 세 묶음은 전부 **이미 발행되고 있던 이벤트**에 시각 반응만 붙인 것이다. 트리거 조건과
        // 발동 빈도는 한 줄도 바뀌지 않았고(새 확률/타이머 0개), 상위 이벤트의 기존 발행 빈도를 그대로
        // 물려받는다 — 사용자가 "요청하지 않은 연출이 뜨는 것"에 반복적으로 민감했으므로 자율 발동
        // 확률을 새로 만드는 것을 금지한 리더 지시의 직접 구현이다. 그래서 이 스위치들의 기본값은
        // **ON**이다: 구경거리성 스펙터클(archeryChance 등 기본 0)이 아니라, 이미 일어나는 동작
        // (착지/배회)에 얹히는 미세한 생동감 디테일이기 때문이다.
        //
        // ★ 거리/크기는 전부 **캐릭터 신장 배수**이고 시간(초)/각도(도)는 절대값이다(이 파일 전체 규약).

        [Header("착지 먼지 — StickmanEventBus.LandingRollRequested 구독 (2026-08-30)")]

        [Tooltip("착지 먼지 연출의 마스터 스위치. 끄면 이벤트는 그대로 발행되지만 아무것도 그려지지 " +
                 "않는다(Tests/PlayMode/EventWiringVisualTests.cs의 네거티브 컨트롤이 이 스위치를 끄고 " +
                 "'먼지가 실제로 사라지는지'를 확인한다).\n\n" +
                 "착지의 **물리적 반응**은 이 연출이 아니라 무릎앉아 착지(landingCrouchEnabled)가 " +
                 "담당한다 — 둘은 같은 임계값(rollLandingHeightThreshold)에서 함께 발동하는 별개의 층이다.")]
        public bool landingDustEnabled = true;

        [Tooltip("한 번의 착지에 피어오르는 먼지 획의 개수. 홀수여야 좌우 대칭 배치의 가운데가 " +
                 "비지 않는다(렌더러가 좌우로 부채꼴 배치한다).")]
        public int landingDustPuffCount = 5;

        [Tooltip("먼지가 피어올랐다가 완전히 사라지기까지의 시간(초). 착지 자체가 0.3~0.6초짜리 " +
                 "연출이라 그보다 짧게 끝나야 '착지의 잔향'으로 읽힌다.")]
        public float landingDustSeconds = 0.38f;

        [Tooltip("먼지가 발밑에서 좌우로 퍼지는 최대 거리 — **캐릭터 신장 배수**(최대 세기 기준). " +
                 "0.34면 배율 0.75(신장 1.71유닛)에서 약 0.58유닛, 화면상 약 20pt다.")]
        public float landingDustSpreadRatio = 0.34f;

        [Tooltip("먼지가 위로 뜨는 최대 높이 — **캐릭터 신장 배수**. 좌우 확산보다 확실히 작아야 " +
                 "'바닥에서 튄 흙먼지'로 보인다(위로 크게 뜨면 폭발처럼 보인다).")]
        public float landingDustRiseRatio = 0.12f;

        [Tooltip("먼지 획의 두께 — **캐릭터 신장 배수**.")]
        public float landingDustStrokeRatio = 0.022f;

        [Tooltip("먼지 세기가 **최대**가 되는 낙하 높이 — rollLandingHeightThreshold 위로 신장의 몇 " +
                 "배를 더 떨어졌을 때인가(신장 배수). 무릎앉아 깊이 램프(landingCrouchDeepFallHeights)와 " +
                 "**같은 기준**을 쓰므로 '깊이 앉을수록 먼지도 크다'가 자동으로 성립한다.")]
        public float landingDustFullHeights = 3f;

        [Tooltip("임계값을 **갓 넘긴** 착지에서의 먼지 세기(0~1). 0이면 임계값 근처 착지에서 먼지가 " +
                 "거의 보이지 않는다.")]
        public float landingDustMinIntensity = 0.45f;

        [Header("유휴 앰비언트 동작 — StickmanEventBus.WanderAmbientMotionRequested 구독 (2026-08-30)")]

        [Tooltip("유휴 중 짧은 동작 변주(주위 살피기 / 기지개)의 마스터 스위치. 끄면 Idle 포즈가 " +
                 "100% 예전과 같아진다(네거티브 컨트롤).\n\n" +
                 "★ 발동 조건은 이미 있던 것 그대로다(docs/UX_FLOW.md 26-3, States/AutoWanderController.cs): " +
                 "주위 살피기는 Idle 진입 후 wanderLookAroundDelayMin~Max 초 뒤 **그 Idle 구간에 1회**, " +
                 "기지개는 'Idle 연장'이 연속 3회 이상일 때 wanderRestExtendSitChance 확률로. " +
                 "이 라운드에 새 확률/타이머를 하나도 더하지 않았다.")]
        public bool idleAmbientMotionEnabled = true;

        [Tooltip("'주위 살피기'(손차양 자세 + 머리 좌우 왕복) 지속 시간(초). UX 26-3이 정한 0.6~1.0초 " +
                 "구간의 중앙값이다 — 고정값인 이유는 새 난수를 도입하지 않기 위해서다(리더 지시).")]
        public float idleAmbientLookAroundSeconds = 0.9f;

        [Tooltip("'주위 살피기'에서 이마에 손을 얹는 **어깨** 각도(도). 각도 규약은 " +
                 "States/StickmanPoseAnimator.cs 전체와 같다(0 = 곧게 아래, +90 = 진행 방향 수평). " +
                 "107과 아래 팔꿈치 122는 임의값이 아니라 **손끝이 어깨 기준 (앞 0.20, 위 0.95)x팔길이** " +
                 "= 이마 높이에 오도록 역산한 쌍이다.")]
        public float idleAmbientLookArmDegrees = 107f;

        [Tooltip("'주위 살피기'의 팔꿈치 굽힘(도, 항상 0 이상). 위 어깨 각도와 한 쌍으로 유도된 값이다.")]
        public float idleAmbientLookElbowDegrees = 122f;

        [Tooltip("'주위 살피기'에서 머리가 좌우로 왕복하는 최대 거리 — **캐릭터 신장 배수**.\n" +
                 "\n" +
                 "★★ 2026-08-31 사용자 신고 \"자꾸 머리를 움직이는데 목에서 벗어나서 이상함\" 대응으로 " +
                 "기본값 0.035 -> **0**(= 머리를 옆으로 밀지 않는다). 대신 눈동자가 좌우를 훑는다" +
                 "(idleAmbientLookEyeSweep01). 이 항목의 코드 경로는 그대로 남아 있으므로 값 하나만 " +
                 "되돌리면 예전 거동이 살아난다(wanderPostIdleJumpChance를 0으로 내렸던 것과 같은 관례).\n" +
                 "\n" +
                 "왜 0이어야 하는가 — 이 리그에는 **목 관절이 없다**. 목은 Torso LineRenderer의 윗부분이고 " +
                 "루트 로컬 x=0에 고정돼 있는데(Editor/SceneBootstrapper.CreateLineSegmentVisual), " +
                 "States/StickmanPoseAnimator.SetBodyOffset의 headOffsetX는 **머리 앵커만** 옆으로 민다. " +
                 "즉 값이 0이 아니면 머리가 정의상 목에서 미끄러진다.\n" +
                 "안전 상한(유도): 목선이 여전히 머리 중심을 가리키려면 머리 중심이 목 획 밖으로 나가면 " +
                 "안 되므로 |밀린 거리| <= 목 획 반폭이다. 목 획 두께 = 0.11 x 0.7 = 0.077, 반폭 0.0385, " +
                 "신장 2.2746944 -> **신장의 0.0169배**가 상한이다. 예전 기본값 0.035는 그 2.07배였고 " +
                 "머리 반경(신장의 0.0967배)의 36%나 되어 육안으로 확실히 어긋나 보였다. " +
                 "이 상한은 Tests/EditMode/IdleAmbientLookAroundInvariantTests.cs가 잠근다.")]
        public float idleAmbientLookHeadShiftRatio = 0f;

        [Tooltip("'기지개' 지속 시간(초). UX 26-3이 정한 1.5~2.5초 구간의 중앙값이다.")]
        public float idleAmbientStretchSeconds = 2f;

        [Tooltip("'기지개'에서 두 팔을 머리 위로 뻗을 때의 좌우 벌림(도). 실제 어깨 각도는 " +
                 "매달리기와 같은 규약으로 180 ∓ (이 값)이다 — 0이면 두 팔이 완전히 겹쳐 외팔로 보인다.")]
        public float idleAmbientStretchArmSpreadDegrees = 13f;

        [Tooltip("'기지개'에서의 팔꿈치 굽힘(도, 항상 0 이상). 완전히 펴면 막대기로 보이므로 조금 남긴다.")]
        public float idleAmbientStretchElbowDegrees = 16f;

        [Tooltip("'기지개'에서 무릎을 펴는 정도(0~1). 1이면 완전히 곧게 편다 — 중립 굽힘에 " +
                 "(1 - 이 값)을 곱하는 형태라 어떤 값에서도 무릎이 반대로 꺾이지 않는다.")]
        public float idleAmbientStretchKneeStraighten01 = 0.7f;

        [Tooltip("'기지개'에서 몸이 솟는 높이 — **캐릭터 신장 배수**. 발끝으로 서는 느낌을 주는 " +
                 "시각 전용 오프셋이라 Rigidbody2D 위치/접지 판정에는 아무 영향이 없다.")]
        public float idleAmbientStretchRiseRatio = 0.030f;

        // ============================================================================
        // ★ 상체 기울임 (2026-09-01 — 참고 이미지 "달리다 넘어지는 졸라맨" 라운드)
        // ============================================================================
        // 이 프로젝트의 몸통 오브젝트는 지금까지 **한 번도 회전한 적이 없다**(localPosition만 세팅됐다).
        // 그래서 달릴 때 상체가 앞으로 기우는 그림이 원천적으로 나올 수 없었고, 유휴 "주위 살피기"의
        // 머리 좌우 이동은 목을 함께 기울일 배관이 없어 0으로 꺼져 있었다
        // (idleAmbientLookHeadShiftRatio 문서의 "값을 되살리려면 먼저 목을 함께 기울이는 배관부터").
        // States/StickmanPoseAnimator.SetBodyLean이 그 배관이며, 회전 중심은 **엉덩이**다(다리 무영향).

        [Header("상체 기울임 (2026-09-01 — 달리기 / 주위 살피기 / 피격 리액션 공용)")]

        [Tooltip("상체 기울임 마스터 스위치. 끄면 아래 세 용도가 전부 0이 되어 상체가 언제나 " +
                 "곧게 선다(= 2026-09-01 이전 거동, 네거티브 컨트롤).")]
        public bool bodyLeanEnabled = true;

        [Tooltip("**명령 속도에 도달했을 때**의 전방 기울임(도). 실제 적용값은 보행 진폭과 같은 " +
                 "정규화 값(실측 속도/명령 속도)으로 스케일되므로, 느리게 걸으면 덜 기울고 " +
                 "성큼성큼 걸으면 이 값에 가까워진다.\n" +
                 "값의 근거: 상체가 기울면 머리 중심이 앞으로 (어깨~엉덩이 거리)x sin(각도)만큼 " +
                 "이동한다 — 배율 0.75에서 10도면 약 0.11유닛(화면상 약 4pt)이라 '기운 것이 보이되 " +
                 "말풍선/게이지 같은 머리 기준 앵커가 눈에 띄게 어긋나지는 않는' 구간이다.")]
        public float bodyLeanRunMaxDegrees = 10f;

        [Tooltip("'주위 살피기'에서 상체가 앞뒤로 한 번 왕복하는 최대 각도(도). 눈이 사라지면서" +
                 "(2026-09-01 P1) 없어진 '두리번거린다'는 시각 신호의 대체다. 머리만 밀던 옛 방식과 " +
                 "달리 목(=몸통 선)이 함께 기울므로 머리가 목에서 벗어나지 않는다.")]
        public float bodyLeanLookAroundDegrees = 7f;

        [Tooltip("랙돌 전이 임계값(ragdollForceThreshold)에 **못 미치는** 피격에서 상체가 튕기는 " +
                 "최대 각도(도). 임계값을 넘는 피격은 그대로 RAGDOLL이므로 이 값과 무관하다 — " +
                 "즉 이 항목은 '맞았는데 아무 반응이 없던' 약한 타격 전용이며 랙돌 물리에 일절 " +
                 "개입하지 않는다(순수 시각 트윈).\n" +
                 "★ 실측: 이 값은 **임펄스**이고 화면에 실제로 나오는 최대 각도는 그보다 작다 — " +
                 "복구(bodyLeanHitRecoverRate 7)와 접근(bodyLeanSmoothingRate 12)이 동시에 걸려 " +
                 "14도 임펄스의 실효 최대는 약 6.2도다(Tests/EditMode/BodyLeanHipPivotTests 실측).")]
        public float bodyLeanHitDegrees = 14f;

        [Tooltip("피격 기울임이 0으로 돌아오는 지수 감쇠 계수(1/초). 7이면 약 0.4초 안에 " +
                 "육안으로 직립이다. 0 이하면 다음 틱에 즉시 사라진다.")]
        public float bodyLeanHitRecoverRate = 7f;

        [Tooltip("상체 기울임이 목표 각도를 따라가는 지수 감쇠 계수(1/초). 걷기 진입/이탈에서 " +
                 "상체가 툭 튀지 않게 하는 유일한 장치다(프레임레이트 독립).")]
        public float bodyLeanSmoothingRate = 12f;

        [Header("Windows 프레임 페이싱 (2026-08-31 — 잔상/렉 대응, Windows 전용)")]

        [Tooltip("[Windows 전용] 초당 프레임 상한. 0 이하면 상한을 걸지 않고 지금까지의 동작(주사율 그대로)을 " +
                 "유지한다.\n" +
                 "왜 Windows에만 거는가: Windows 투명 오버레이는 UniWindowController가 요구하는 " +
                 "useFlipModelSwapchain=false, 즉 **레거시 BitBlt 스왑체인**으로만 성립한다. 그 경로에서 " +
                 "Present()는 화면을 직접 넘기는 것이 아니라 DWM의 리디렉션 표면에 복사되고, DWM은 그것을 " +
                 "자기 주기로 따로 읽어 레이어드 창으로 합성한다. 두 주기가 동기화되지 않으므로 앱이 " +
                 "빠르게 그릴수록 DWM이 '아직 다 갱신되지 않은 표면'을 읽을 확률이 올라간다 — 이때 화면 " +
                 "전체가 두 프레임이 섞인 상으로 보인다(사용자 신고: 글자 획이 유령처럼 겹쳐 보임).\n" +
                 "macOS(Metal/Quartz)에는 이 복사 경로 자체가 없어 이 값은 macOS에서 **읽히지 않는다**.")]
        public int windowsTargetFrameRate = 60;

        [Tooltip("[Windows 전용] 프레임 상한을 걸 때 QualitySettings.vSyncCount를 0으로 내릴지 여부.\n" +
                 "vSyncCount가 1 이상이면 Application.targetFrameRate는 **통째로 무시된다**(Unity 공식 문서). " +
                 "게다가 레이어드 창은 스캔아웃이 아니라 DWM 합성을 거치므로 앱 쪽 vsync는 지연만 더할 뿐 " +
                 "찢어짐을 막아주지 못한다. 그래서 Windows에서는 vsync를 끄고 명시적 상한으로 대체한다.")]
        public bool windowsDisableVSyncForFrameCap = true;

        [Header("macOS 프레임 페이싱 (2026-08-31 성능 라운드 — 상주 앱 CPU/배터리/체감 렉)")]

        [Tooltip("[macOS 전용] **몇 번의 디스플레이 새로고침마다 한 프레임을 낼 것인가**" +
                 "(QualitySettings.vSyncCount). 0이면 이 항목을 건드리지 않는다.\n" +
                 "  1 = 주사율 그대로(120Hz 패널에서 120fps)\n" +
                 "  2 = 60fps   3 = 40fps   4 = 30fps   (120Hz 패널 기준)\n" +
                 "\n" +
                 "왜 targetFrameRate가 아니라 이 방식인가 — 이게 이번 라운드의 핵심이다.\n" +
                 "사용자 신고는 '평균 수치는 낮은데 캐릭터가 부드럽지 않고 렉처럼 보인다'였다. 그건 " +
                 "평균 부하 문제가 아니라 **프레임이 화면에 나가는 간격이 불규칙**하다는 뜻이다. " +
                 "그런데 Application.targetFrameRate는 vsync를 끈 뒤 sleep으로 속도를 맞추는 방식이라, " +
                 "앱의 프레임 위상이 디스플레이 주기와 어긋난 채 자유롭게 떠다닌다 — 60fps 평균이어도 " +
                 "120Hz 화면에는 어떤 프레임은 1번, 어떤 프레임은 2번 표시되는 맥놀이(beat)가 생겨 " +
                 "**오히려 더 끊겨 보인다**. vSyncCount는 반대로 디스플레이 주기에 위상을 고정하므로 " +
                 "간격이 정확히 균일하다.\n" +
                 "\n" +
                 "macOS에서 이게 실제로 동작한다는 실측 근거: 실행 중인 .app을 `sample`로 뜬 결과 " +
                 "CVDisplayLink 스레드가 살아 있고 -[CAMetalLayer nextDrawable]에서 " +
                 "semaphore_timedwait으로 실제 back-pressure를 받고 있었다(645샘플 중 461). 즉 이 앱은 " +
                 "이미 디스플레이 링크를 통해 표시되고 있어 vsync 간격이 그대로 먹는다.\n" +
                 "\n" +
                 "왜 기본값 2(=60fps)인가: 120Hz의 정확한 약수라 프레임 간격이 완벽히 균일하고, " +
                 "육안으로 120fps와 구분되지 않으면서 렌더/합성 작업량을 절반으로 줄인다. 더 아끼려면 " +
                 "3(40fps) 또는 4(30fps)를 써라 — 이 셋만이 120Hz의 약수라 균일하다. " +
                 "**45fps 같은 비약수 값을 targetFrameRate로 주는 것은 피하라**(120/45=2.67이라 " +
                 "2번,3번,2번,3번 표시되는 진동이 생겨 60fps보다 더 끊겨 보인다).")]
        [Range(0, 4)]
        public int macVSyncInterval = 2;

        [Tooltip("[macOS 전용] 위 macVSyncInterval이 0일 때만 쓰이는 **보조** 프레임 상한" +
                 "(Application.targetFrameRate). 0 이하면 상한을 걸지 않는다.\n" +
                 "기본이 0인 이유: 이 앱에서는 vsync 간격 방식(macVSyncInterval)이 프레임 간격을 " +
                 "균일하게 만들어 주므로 언제나 그쪽이 낫다. 이 항목은 외장 모니터 등에서 vsync가 " +
                 "예상대로 동작하지 않는 것이 실측으로 확인됐을 때를 위한 탈출구다.\n" +
                 "주의: 이 값을 쓰려면 vSyncCount가 0이어야 한다(1 이상이면 Unity가 targetFrameRate를 " +
                 "통째로 무시한다). macVSyncInterval=0으로 두면 이 클래스가 자동으로 그렇게 맞춘다.")]
        public int macTargetFrameRate = 0;

        [Tooltip("프레임 시간 통계(p50/p95/p99/최댓값)를 30초마다 로그에 남길지 여부. **플랫폼 공통**.\n" +
                 "'평균은 낮은데 렉이 느껴진다'는 종류의 신고는 평균이 아니라 **분산과 최댓값**을 봐야 " +
                 "판별된다. 켜면 링 버퍼(할당 0)로 프레임 간격을 모아 30초에 한 줄 남긴다 — 로그 부담은 " +
                 "사실상 없다.\n" +
                 "기본값이 true인 이유: 지금 이 라운드가 바로 그 '체감 렉' 신고를 쫓는 중이고, 특히 " +
                 "Windows 잔상/렉은 이 개발 환경(macOS)에서 재현이 불가능해 **사용자 기기의 로그가 유일한 " +
                 "계측 수단**이다. 렉 문제가 종결되면 false로 내려도 된다.")]
        public bool logFrameTimeStats = true;

        // ============================================================================
        // 유휴 "주위 살피기" 후속 (2026-08-31 사용자 신고 2건 대응)
        // ============================================================================
        // 위쪽 "유휴 앰비언트 동작" 섹션이 아니라 파일 맨 끝의 신규 섹션에 두는 이유는 이 파일의
        // 기존 관례와 같다 — 같은 라운드에 다른 작업자가 위쪽 섹션들을 동시에 편집 중이라 충돌을
        // 피하기 위해서다(위 "PC 하드웨어 반응 — 자율 발동 마스터 스위치" 섹션 주석과 동일).

        [Header("유휴 '주위 살피기' 빈도/시선 (2026-08-31)")]

        [Tooltip("'주위 살피기'를 다시 낼 수 있을 때까지의 **최소 간격**(초). 0이면 예전 거동" +
                 "(네거티브 컨트롤).\n" +
                 "\n" +
                 "★ 2026-08-31 사용자 신고 \"너무 자주함\" 대응. 실측(States/AutoWanderController.cs의 " +
                 "확률/지속시간을 그대로 몬테카를로 1시간 시뮬레이션)으로 나온 예전 빈도는 " +
                 "**분당 9.7회, 중앙값 간격 6.3초, 최소 간격 1.4초**였다. 원인은 트리거 자체가 아니라 " +
                 "'Idle 연장'(25%)이 새 Idle 구간을 만들 때마다 그 구간에서 다시 1회 발동한다는 데 있다 " +
                 "— 한 번 쉬기 시작하면 2~6초마다 계속 나온다.\n" +
                 "기본값 30초에서의 실측: **분당 1.8회, 중앙값 간격 32.9초**(5.4배 감소).\n" +
                 "\n" +
                 "이 값은 **발행자 쪽**(AutoWanderController)에 건다 — 소비자" +
                 "(Interaction/IdleAmbientMotionRenderer.cs)가 '새 확률/타이머를 하나도 두지 않는다'는 " +
                 "계약을 그대로 지키기 위해서다. 유예는 개체별이다(사본이 서로 다른 리듬을 갖는다).")]
        public float wanderLookAroundCooldownSeconds = 30f;

        [Tooltip("'주위 살피기' 동안 **눈동자**가 좌우로 훑는 폭(0~1, 1 = 머리 링을 뚫지 않는 실측 " +
                 "최대 오프셋). 0이면 눈을 건드리지 않는다(네거티브 컨트롤).\n" +
                 "\n" +
                 "왜 머리가 아니라 눈인가: 머리를 옆으로 미는 예전 연출은 목 관절이 없는 이 리그에서 " +
                 "정의상 머리를 목에서 떼어놓는다(idleAmbientLookHeadShiftRatio 문서의 유도 참고). " +
                 "눈동자는 머리의 자식이고 States/EyeController가 링 안쪽으로 실측 clamp까지 하므로 " +
                 "어떤 배율에서도 구조적으로 어긋날 수 없다. 게다가 '두리번거린다'는 신호로는 " +
                 "눈이 원래 더 정확하다.\n" +
                 "포즈와 **같은 포락선**(양 끝 정확히 0)을 쓰므로 시작/끝에서 눈이 튀지 않고, " +
                 "동작이 끝나면 다음 프레임부터 커서 추적이 그대로 이어받는다.")]
        [Range(0f, 1f)]
        public float idleAmbientLookEyeSweep01 = 0.85f;

        // ============================================================================
        // 긴 망토 걸려 넘어짐 — 자율 발동 스위치 (2026-08-31 사용자 명시 요청으로 기본 OFF)
        // ============================================================================
        // 파일 맨 끝의 신규 섹션에 두는 이유는 이 파일의 기존 관례와 같다 — 같은 라운드에 다른
        // 작업자가 위쪽 섹션들을 동시에 편집 중이라 충돌을 피하기 위해서다
        // (위 "PC 하드웨어 반응 — 자율 발동 마스터 스위치" 섹션 주석과 동일).
        //
        // ★ 사용자 원문(2026-08-31): "그리고 걷다가 갑자기 아픈것처럼 쓰러지는데 이런건 없애줘"
        //
        // 이것이 그 연출의 **유일한 발동 경로**다. 코드베이스 전체에서
        // StickmanAgent.ReportExternalImpact()를 부르는 곳은 세 군데뿐이고
        //   · Core/RagdollLimbImpactRelay      : 실제 물리 충돌(유저가 던지거나 부딪힌 결과)
        //   · States/DragThrowState            : 유저가 직접 던진 속도
        //   · States/RodeoCursorState          : 유저가 커서로 거칠게 흔든 것
        // 나머지 둘도 전부 **유저가 시작한 행동**이다. 유저가 아무것도 하지 않았는데 스스로
        // RAGDOLL로 가는 경로는 Interaction/LongCapeTripDirector 하나뿐이었다(실측 근거는 아래).
        //
        // ★ 실측(2026-08-31 디버거) — 사용자 저장 파일에 wornShoulders=equip.shoulders.long_cape,
        //   ragdollFalls=48이 찍혀 있었고, 사용자 Player-prev.log에 인과가 그대로 남아 있었다:
        //       [긴망토] 자락을 밟고 넘어졌습니다 — 충격량 8.16 (임계값 8.00의 최소 초과분)
        //       [말풍선] 표시 (Ragdoll) "윽...!"
        //   신고의 "아픈것처럼"은 저 "윽...!"이다. 즉 추정이 아니라 확인된 원인이다.
        //
        // 왜 2026-08-29 "구경거리 전부 자율 발동 OFF" 정리에서 살아남았는가: 이 기능은 그 정리
        // **다음 날**(2026-08-30) 만들어졌고, 발동 주기를 StickConfig가 아니라 자기 파일 안의
        // private const에 숨겨 뒀다. 그래서 "*Chance 필드를 0으로" 라는 그 라운드의 정리 방식이
        // 구조적으로 닿을 수 없었다. 이번에 그 상수를 이 필드로 끌어올려 같은 규칙 아래 둔다 —
        // 다음에 같은 정리를 할 때 또 빠지지 않게 하는 것이 이 이동의 진짜 목적이다.

        [Header("긴 망토 걸려 넘어짐 — 자율 발동 (2026-08-31 사용자 요청으로 기본 OFF)")]

        [Tooltip("긴 망토를 걸치고 **걷는 동안** 자락을 밟고 스스로 넘어지는(RAGDOLL) 연출의 평균 " +
                 "발동 간격(초). 포아송 과정이라 '정확히 N초마다'가 아니라 '평균 N초에 한 번'이다.\n\n" +
                 "★ 0 이하 = 발동하지 않는다(기본값). 2026-08-31 사용자 명시 요청 " +
                 "\"걷다가 갑자기 아픈것처럼 쓰러지는데 이런건 없애줘\"에 따른 것이다. " +
                 "실측 근거는 이 필드 바로 위 섹션 주석에 있다(사용자 로그의 [긴망토] -> Ragdoll -> " +
                 "\"윽...!\" 연쇄, 저장 파일 ragdollFalls=48).\n\n" +
                 "★ 기능을 지우는 것이 아니라 기본값을 조용하게 만드는 것이다 — " +
                 "Interaction/LongCapeTripDirector의 코드 경로는 그대로 살아 있고, 이 값을 양수로 " +
                 "올리면 즉시 예전 거동으로 돌아온다(원래 기본값 90). 다른 구경거리 연출을 " +
                 "*Chance = 0으로 끈 것과 정확히 같은 방식이다.\n\n" +
                 "주의: 유저가 직접 던지거나(DragThrowState) 커서로 흔들어(RodeoCursorState) 넘어뜨리는 " +
                 "경로는 이 값을 읽지 않으므로 OFF에서도 그대로 살아 있다 — 끄는 것은 **자율 발동**뿐이다.")]
        public float longCapeTripMeanSeconds = 0f;

        // ============================================================================
        // ★★ "창에서 가끔 갑자기 떨어짐" 근본 원인 3종 (2026-09-01, 사용자 신고 + 디버거 확정)
        // ============================================================================
        // 파일 맨 끝의 신규 섹션에 두는 이유는 바로 위 섹션과 같다 — 같은 라운드에 다른 작업자가
        // 위쪽 섹션들을 편집 중이라 충돌을 피한다.
        //
        // 디버거의 1차 조사(Tasklist.md "[debugger/Teammate2] 2026-09-01")가 네이티브 프로브
        // 3600표본으로 **반증**한 것: 창 가림 오판(H1) / 폴링 지터(H2). 발판 열거 자체는 멀쩡하다.
        // **남은 진짜 원인 3가지**가 아래 세 필드에 각각 대응한다.
        //
        //   (1) 접지 중에도 중력이 켜져 있었다 → groundedGravitySuppressionEnabled
        //   (2) 발판 상실 유예(0.1초)가 창 열거 폴링 캐시(0.3초)보다 짧았다
        //                                     → groundLossGracePollIntervalMultiplier
        //   (3) 오버레이 원점 읽기가 가끔 쓰레기값을 줬다 → overlayOriginSanityCheckEnabled

        [Header("접지 안정성 (2026-09-01 신고 \"창에서 가끔 갑자기 떨어짐\")")]

        [Tooltip("원인 (1). 접지가 확정된 프레임에는 Rigidbody2D.gravityScale을 0으로 눌러 " +
                 "**세로 적분 자체를 막는다**(기본 ON).\n\n" +
                 "왜 위치 스냅만으로는 부족한가: 창/Dock 상단은 **논리 발판일 뿐 물리 콜라이더가 " +
                 "없다.** 그래서 '서 있기'가 매 프레임 SnapToGround 한 번으로만 유지되는데, 그 사이에도 " +
                 "중력은 계속 적분된다. 한 프레임이 길어지면 그 프레임의 자유낙하만으로 접지 허용오차 " +
                 "밴드(groundSnapTolerance)를 통째로 벗어나고, 그러면 창이 전혀 움직이지 않았는데도 " +
                 "캐릭터가 낙하한다.\n" +
                 "임계 프레임시간은 States/GroundSensor.ComputeGroundLossFrameTimeThreshold()가 실제 " +
                 "카메라/설정에서 계산한다 — 배포 형상(gravityScale 3, 허용오차 20 OS-pt, 1유닛≈40.9pt)에서 " +
                 "**약 182ms**다. 그런데 절전 프레임페이싱 티어 DisplayOff는 4fps(=250ms/프레임)이고 " +
                 "엔진 최대 timestep도 333ms다 — 즉 **절전 등급에 들어가거나 히치가 한 번만 나도 조건이 " +
                 "성립**한다. 이것이 신고의 가장 유력한 원인이다.\n\n" +
                 "안전장치: 억제는 매 프레임 **벗겼다 다시 얹는다**(StickmanAgent.Update가 상태 Tick " +
                 "직전에 해제하고 모든 처리가 끝난 뒤 다시 적용) — 잉크 바닥 클리어런스 리프트와 완전히 " +
                 "같은 관례다. 그래서 어떤 경로로 상태가 바뀌어도 '중력이 꺼진 채 갇히는' 상태가 " +
                 "구조적으로 남을 수 없고, 상태 로직/연출 코드가 gravityScale을 읽을 때는 언제나 진짜 값이다.\n\n" +
                 "끄면 예전 거동(스냅 전용)으로 정확히 되돌아간다 — 회귀 테스트의 네거티브 컨트롤이 이 값을 쓴다.")]
        public bool groundedGravitySuppressionEnabled = true;

        [Tooltip("원인 (2). 발판 상실 유예를 **footholdPollInterval의 몇 배**로 둘 것인가.\n\n" +
                 "실제 유예 = max(fallGraceDuration, footholdPollInterval x 이 값) — " +
                 "ResolveGroundLossGraceDuration()이 유일한 계산 지점이다. 숫자를 따로 적지 않고 " +
                 "폴링 간격에서 **유도**하므로, 폴링 주기를 바꾸면 유예가 자동으로 따라간다.\n\n" +
                 "왜 필요한가: 유예의 목적은 '창 목록이 한 번 튀는 것'을 흡수하는 것이다. 그런데 발판 " +
                 "캐시는 footholdPollInterval(0.3초) 동안 고정이라, 열거가 한 번만 튀면 그 나쁜 목록이 " +
                 "**0.3초 내내** 유지된다. 예전 유예 0.1초는 그 1/3이라 설계 목적을 원리적으로 수행할 수 " +
                 "없었다(디버거 가설 H5, 성립). 1배가 아니라 1.5배인 이유는 폴링 위상과 프레임 경계가 " +
                 "정렬돼 있지 않기 때문이다 — 나쁜 목록이 관측되는 시점이 폴링 주기 한가운데일 수 있어 " +
                 "정확히 1배면 경계에서 아슬아슬하게 진다.\n\n" +
                 "0 이하로 두면 예전 거동(fallGraceDuration 그대로)이 된다.\n" +
                 "※ 이 값은 **발판 상실** 판정에만 쓴다. 착지 확정(FallState)은 여전히 fallGraceDuration을 " +
                 "쓴다 — 두 판정은 목적이 다르고, 착지까지 늦추면 공중에 붕 뜬 채 내려앉는 그림이 된다.")]
        public float groundLossGracePollIntervalMultiplier = 1.5f;

        [Tooltip("원인 (3). 플랫폼이 보고한 오버레이 창 사각형이 화면 경계를 명백히 벗어나면 그 보고를 " +
                 "**버리고 직전 유효값을 유지**한다(기본 ON).\n\n" +
                 "실측 근거: Player.log.prevround에서 오버레이 원점이 " +
                 "(0,0)->(0,-805)->(0,-936)->(0,-937)->(0,-78)->(0,0)으로 요동친 직후 [발판상실]이 " +
                 "발생했다(화면 높이는 982pt). 원점이 틀리면 캐릭터의 OS 좌표가 통째로 틀어져 발판 " +
                 "상단과의 비교가 무너진다 = 창은 그대로인데 접지가 풀린다.\n\n" +
                 "판정과 '영구 고착 방지'는 Platform/ScreenCoordinateConverter의 " +
                 "ReportOverlayWindowOsRect()에 있다 — 같은 값이 연속으로 다시 오면 실제 이동으로 보고 " +
                 "받아들이므로, 디스플레이를 바꿔도 낡은 원점에 갇히지 않는다.")]
        public bool overlayOriginSanityCheckEnabled = true;

        /// <summary>
        /// **발판 상실** 판정에 쓸 유예 시간(초)의 유일한 계산 지점.
        /// = max(<see cref="fallGraceDuration"/>, <see cref="footholdPollInterval"/> x
        /// <see cref="groundLossGracePollIntervalMultiplier"/>).
        ///
        /// <para>착지 확정(States/FallState)은 이 값을 쓰지 않는다 — 위 툴팁의 마지막 문단 참고.</para>
        /// </summary>
        public float ResolveGroundLossGraceDuration()
        {
            float derived = footholdPollInterval * groundLossGracePollIntervalMultiplier;
            return Mathf.Max(fallGraceDuration, derived);
        }

        // ============================================================================
        // ★★ 발판 상실 공중 유예 연출 (2026-09-01 — 소은 실측 + 리더 결정 "(C) 시간은 두고 연출을 붙인다")
        // ============================================================================
        // 위 (2) 유예가 만든 부작용을 닫는 항목이다. 유예 동안 몸을 붙잡는 것은 수정과 분리 불가인데
        // (폴링 한 주기 자유낙하가 이미 허용오차의 2.7배라 발판으로 돌아올 방법이 없다), 그 결과
        // **IDLE 중에는 화면이 통째로 정지**한다. 소은의 프레임별 픽셀 추적:
        //
        //   IDLE : 모자 상단 y가 10프레임 넘게 1픽셀도 안 움직임(연속 프레임 화소차 0.00%)
        //          -> "만화적 연출"이 아니라 **"앱이 멈췄다/렉이다"** 로 읽힌다.
        //   WALK : 같은 빌드·같은 물리·같은 지속시간인데 허공을 수평으로 걸어감(다리가 계속 돌아감)
        //          -> 와일 E. 코요테 그대로. 귀엽다.
        //
        // 즉 문제는 "0.45초"라는 길이가 아니라 **그 시간에 생명 신호가 있느냐**다(같은 빌드 안의 통제
        // 비교라 다른 변수가 끼어들 여지가 없다). 그래서 시간 단축안은 기각됐고, 대신 유예를 진짜
        // 상태(StickmanStateId.GroundLossHang)로 승격해 그 상태의 포즈로 신호를 만든다.
        //
        // ★ 사용자가 실제로 보는 정지 시간 = 폴링 지연(0~footholdPollInterval) + 유예
        //   = 0.45~0.75초, 평균 약 0.6초(리더 결정 승인사항 4로 확정된 전제). 아래 두 비율이
        //   "유예의 몇 %"인 이유가 이것이다 — 폴링 주기를 바꾸면 유예가 따라가고, 연출도 함께 따라가야
        //   한다. 숫자를 초로 적으면 유예가 짧아졌을 때 연출의 뒷부분이 통째로 잘린다.

        [Header("발판 상실 공중 유예 연출 (2026-09-01 — 정지 화면을 코요테 개그로)")]

        [Tooltip("유예 구간을 **상태로 승격**할지의 마스터 스위치(기본 ON).\n\n" +
                 "끄면 Idle/Walk가 GroundLossHang으로 전이하지 않고 2026-09-01 오전 거동으로 정확히 " +
                 "되돌아간다 — 유예 동안 몸은 여전히 붙잡히지만(그건 낙하 수정의 본체라 분리 불가) " +
                 "연출이 없어 **정지 화면**이 된다. 회귀 테스트의 네거티브 컨트롤이 이 값을 쓴다: " +
                 "끈 상태에서 화면이 실제로 얼어붙는 것을 재서, 켠 상태의 움직임 수치가 의미를 갖게 한다.")]
        public bool groundLossHangStateEnabled = true;

        [Tooltip("유예 전체 길이 대비 **무반응 구간**의 비율(0~1). 발판을 잃은 뒤 이 비율만큼은 " +
                 "포즈를 한 톨도 건드리지 않는다 — 늦게 알아차리는 그 한 박자가 코요테 개그의 핵심이다.\n\n" +
                 "★ 소은의 원안은 0.12초(=유예 0.45초의 0.267)였고 여기서는 0.15로 **줄였다**. 사유: " +
                 "소은 자신이 4항에서 확정한 대로 사용자가 보는 정지에는 폴링 지연(0~0.3초, 평균 0.15초)이 " +
                 "**이미 앞에 붙어 있다.** 그 구간에는 앱이 발판 상실을 정말로 모르고 있으므로 그 자체가 " +
                 "'늦게 알아차림'이다. 원안대로 0.267을 쓰면 무반응이 평균 0.15+0.12=0.27초, 최악 0.42초가 " +
                 "되어 의도한 한 박자의 2.2~3.5배가 되고, 그건 다시 '렉'으로 읽히는 구간이다. 0.15면 " +
                 "무반응은 최소 0.068초(폴링 지연 0) ~ 최대 0.368초, 평균 0.22초이고, 대신 " +
                 "**생명 신호 구간이 0.26초에서 0.38초로 46% 길어진다.**")]
        [Range(0f, 0.5f)]
        public float groundLossHangReactionDelayRatio = 0.15f;

        [Tooltip("유예 전체 길이 대비 **낙하 전조(상체 기울임)가 시작되는** 지점의 비율(0~1). " +
                 "이 지점부터 유예가 끝날 때까지 상체가 앞으로 기운다(다리 종종거림은 계속된다).\n\n" +
                 "★ 소은의 원안은 0.35초(=0.778)였고 여기서는 0.72로 앞당겼다. 사유: 기울임은 " +
                 "**지수 감쇠로 목표를 따라가므로**(bodyLeanSmoothingRate 12/초) 남은 시간이 짧을수록 " +
                 "실제로 적용되는 각도가 작아진다. 0.778이면 남은 시간 0.1초에 목표의 70%, 0.72면 " +
                 "0.126초에 78%다 — 소은이 '단독으론 물리적으로 안 보이는 변화'라고 지적한 구간을 " +
                 "벗어나기 위한 최소 조정이다(그 지적의 실체는 각도가 아니라 **끝점 이동 pt**다).")]
        [Range(0f, 1f)]
        public float groundLossHangFallTellRatio = 0.72f;

        [Tooltip("제자리 종종걸음의 다리 사이클 주파수를 **걷기 사이클의 몇 배**로 돌릴 것인가. " +
                 "리더 승인 범위는 2~3배이고 기본값은 그 상단이다 — 유예가 0.45초뿐이라 " +
                 "배수가 곧 '몇 걸음이 보이는가'이기 때문이다(3배 = 실측 걷기 1.35Hz 기준 약 4Hz = " +
                 "생명 신호 구간 0.38초에 약 3걸음).\n\n" +
                 "숫자를 Hz로 적지 않는 이유: 걷기 주파수는 보폭(다리 길이 x 진폭)과 명령 속도에서 " +
                 "나오는 값이라 캐릭터 배율/진폭 설정이 바뀌면 함께 움직여야 한다. " +
                 "States/StickmanPoseAnimator.ApplyGroundLossHangPose가 걷기와 같은 식으로 매 프레임 유도한다.")]
        public float groundLossHangLegCycleSpeedMultiplier = 3f;

        [Tooltip("종종걸음의 다리 진폭 배수(걷기 키포즈 표에 곱한다). 1 = 걷기와 같은 보폭 각도. " +
                 "낮추면 '종종'거리는 느낌은 커지지만 **화면에서 보이는 크기가 그대로 줄어든다** — " +
                 "이 캐릭터는 높이 63pt짜리 막대라 1% 미만의 변화는 육안으로 무의미하다는 실측이 있어 " +
                 "기본값은 줄이지 않았다.")]
        public float groundLossHangLegAmplitudeScale = 1f;

        [Tooltip("팔 허우적의 **중심** 어깨 각도(도). 포즈 규약상 0 = 곧게 아래, 180 = 곧게 위다. " +
                 "기본값 125는 '위-바깥으로 든 팔'이고, 곧이어 이어지는 낙하 자세" +
                 "(fallPoseArmRaiseDegrees 143)가 아래 진폭 범위 안에 들어와 전환이 매끄럽다.")]
        public float groundLossHangArmFlailBaseDegrees = 125f;

        [Tooltip("팔 허우적의 왕복 진폭(도). 중심 각도 ± 이 값만큼 왕복한다.\n\n" +
                 "기본값 48의 근거는 **보이는 크기**다(각도가 아니라 손끝 이동 pt로 정한다). " +
                 "중심 125 ± 48 = 77~173도 구간에서 손끝의 세로 이동은 팔 두 마디 기하학상 약 0.59유닛" +
                 "(배포 환산 약 24pt)이고, 여기에 팔 각도 스무딩 감쇠(팔은 다리의 0.55배 계수)가 걸려 " +
                 "화면에는 약 19pt가 남는다 — 소은이 '물리적으로 안 보이는 변화'로 지목한 6.1pt의 약 3배다. " +
                 "40도로 낮추면 그 여유가 2.5배로 줄어 기준선에 너무 가까워진다" +
                 "(Tests/PlayMode/GroundLossHangStateTests.H8이 이 값을 실측으로 잠근다).")]
        public float groundLossHangArmFlailDegrees = 48f;

        [Tooltip("팔 왕복 주파수 / 다리 사이클 주파수 비. **정수가 아니어야** 한다 — 딱 맞아떨어지면 " +
                 "허우적이 아니라 행진처럼 보인다(dragStruggle의 팔 주파수 비와 같은 이유).")]
        public float groundLossHangArmFlailFrequencyRatio = 0.63f;

        [Tooltip("허우적 중 팔꿈치 굽힘(도, 항상 0 이상). 완전히 펴면 막대기로 보인다.")]
        public float groundLossHangElbowBendDegrees = 22f;

        [Tooltip("낙하 전조로 상체가 앞으로 기우는 **목표** 각도(도). 실제 화면 각도는 지수 감쇠 " +
                 "때문에 이보다 작다(위 groundLossHangFallTellRatio 참고).\n\n" +
                 "값의 근거는 '보이는 크기'다: 상체 길이가 약 35pt라 기울임 θ의 끝점 이동은 35sin(θ)pt다. " +
                 "소은이 '단독으론 안 보이는 변화'로 지목한 10도는 6.1pt였다. 26도 목표 -> 실효 약 20도 -> " +
                 "약 12pt로 그 2배가 된다(상한은 StickmanPoseAnimator.MaxBodyLeanDegrees 30도).\n" +
                 "bodyLeanEnabled를 끄면 이 값과 무관하게 0이다(마스터 스위치 하나로 전부 꺼진다).")]
        public float groundLossHangFallTellLeanDegrees = 26f;

        [Tooltip("★ **갇힘 방지 최후 안전망** — 이 상태에 머물 수 있는 절대 상한을 유예의 몇 배로 둘 것인가.\n\n" +
                 "정상 경로에서는 유예 만료(1.0배)에 반드시 빠져나가므로 이 상한은 도달하지 않는다. " +
                 "그럼에도 두는 이유: **이 상태에 갇히면 캐릭터가 영원히 공중에 뜬다** — 이번 라운드가 " +
                 "고치려는 원래 버그보다 나쁜 결과다. 유예 타이머는 블랙보드가 소유하고 여러 경로가 " +
                 "리셋할 수 있으므로(ResetGroundLossTimer), 상태가 자기 시계로도 한 번 더 끊는다. " +
                 "1보다 커야 정상 경로를 앞지르지 않는다.")]
        public float groundLossHangHardTimeoutGraceMultiplier = 3f;

        /// <summary>
        /// 발판 상실 공중 유예 상태에 머물 수 있는 <b>절대 상한</b>(초)의 유일한 계산 지점.
        /// = <see cref="ResolveGroundLossGraceDuration"/> x
        /// <see cref="groundLossHangHardTimeoutGraceMultiplier"/>(최소 1배).
        /// </summary>
        public float ResolveGroundLossHangHardTimeout()
        {
            float grace = ResolveGroundLossGraceDuration();
            return grace * Mathf.Max(1f, groundLossHangHardTimeoutGraceMultiplier);
        }

        // ============================================================================
        // 스톨 귀인 계측 (2026-09-01 — 사용자 신고 "실행하자마자 렉", [프레임스파이크] 55회 라운드)
        // ============================================================================
        // 이 파일의 기존 관례대로 **맨 끝 신규 섹션**에 둔다(같은 라운드에 다른 작업자가 위쪽 섹션을
        // 동시에 편집 중이라 충돌을 피하기 위해서다 — 위 섹션들의 같은 취지 주석 참고).

        [Header("스톨 귀인 계측 (2026-09-01)")]

        [Tooltip("긴 프레임(스파이크)이 났을 때 그 시간이 **어디서 갔는지**를 한 줄로 남길지 여부" +
                 "(Platform/StallAttribution.cs). 기본 true.\n\n" +
                 "왜 기본이 켜짐인가: 지금 [프레임스파이크] 로그는 '백버퍼도 그대로고 GC도 0이니 " +
                 "네이티브 창 열거/파일 IO 쪽이다'까지만 말한다. 그건 **추론이지 계측이 아니고**, " +
                 "게다가 거짓 이분법이다 — 렌더/프레젠트/OS 합성 대기도 GC를 안 만들고 백버퍼도 " +
                 "안 바꾼다. 이 스위치를 켜면 프레임이 '로직 구간(모든 Update+LateUpdate)'과 " +
                 "'그 밖(렌더/프레젠트/합성)'으로 쪼개져 실측되고, 로직 구간 안에서 창 열거 ms와 " +
                 "로그 쓰기 ms가 각각 나온다. Windows 실기가 없는 이 개발 환경에서 사용자 로그가 " +
                 "유일한 계측 수단이므로, 원인이 확정될 때까지는 켜 두는 것이 맞다.\n\n" +
                 "비용: 프레임당 Stopwatch 타임스탬프 2회(약 50ns) + 폴링당 2회 + 로그 한 줄당 2회. " +
                 "할당 0, OS 창 조회 0. 로그는 스파이크 시 5초 쿨다운 + 60초 요약 한 줄뿐이다. " +
                 "(직전 z-order 라운드에서 '진단 장치가 초당 10회 전체 창을 열거해 증상을 키운' " +
                 "사고가 있었다 — 그 교훈을 이 설계가 지킨다.)\n\n" +
                 "원인이 확정되고 수정이 검증되면 false로 내려도 된다.")]
        public bool logStallAttribution = true;

        [Tooltip("릴리즈 플레이어에서 **Debug.Log / LogWarning의 스택트레이스를 끌지** 여부" +
                 "(Platform/PlayerLogPolicy.cs). 기본 true = 끈다.\n\n" +
                 "근거: Unity 기본값(ProjectSettings의 m_StackTraceTypes)은 **다섯 종류 전부 " +
                 "ScriptOnly**였다. 즉 정보성 로그 한 줄마다 관리 스택을 캡처하고 그걸 Player.log에 " +
                 "동기로 쓴다. 이 앱은 던지기 한 번에 8줄 이상이 쏟아지므로 순수 낭비다.\n\n" +
                 "**Error/Exception/Assert의 스택트레이스는 이 스위치와 무관하게 유지된다** — " +
                 "예외 추적을 잃으면 안 되기 때문이다. 끄는 것은 Log/Warning 둘뿐이다.\n\n" +
                 "false로 두면 예전 거동(전부 ScriptOnly)이다. 스택이 필요한 회귀 조사 때 쓴다.")]
        public bool suppressInfoLogStackTraces = true;
    }
}
