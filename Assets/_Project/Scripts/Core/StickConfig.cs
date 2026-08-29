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
        //    화면 최하단처럼 낙차가 0.855유닛뿐인 단차에서는 매달리기 판정에 걸리지 않아, 캐릭터가
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
                 "접지 허용오차 안이라 착지 판정이 흔들린다). macOS Dock 단차(0.855유닛)는 이 값보다 " +
                 "충분히 크므로 정상적으로 뛰어내린다.")]
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
                 "실패한 나머지 확률은 기존 배회 행동(정지 후 반대 방향)으로 흡수된다.")]
        [Range(0f, 1f)]
        public float stepUpChance = 0.5f;

        [Tooltip("스스로 기어오를 최대 턱 높이(월드 유닛). 이보다 높은 벽은 자율 배회로는 오르지 않는다 — " +
                 "ParkourClimb는 높이와 무관하게 parkourClimbDuration(0.5초)에 올라가므로, 높은 벽까지 " +
                 "자동으로 오르게 두면 순간이동처럼 보인다. Dock 단차(0.855유닛)는 넉넉히 포함된다.")]
        public float stepUpMaxHeight = 1.5f;

        [Tooltip("ParkourClimb로 턱 위에 올라선 뒤, 그 발판 안쪽으로 얼마나 들어가 설지(월드 유닛). " +
                 "0이면 모서리 선 위에 정확히 서게 되어 접지 판정이 경계에서 흔들리고 곧바로 다시 떨어진다. " +
                 "발판이 이 값보다 좁으면 반대편 끝을 넘지 않도록 자동으로 좁혀진다.")]
        public float parkourMantleInset = 0.25f;

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
                 "단위 근거는 아래 \"OS-px 필드 단위 규약\" 블록 참고 — Retina를 켜도 값을 바꿀 필요가 없다.")]
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

        [Header("라이벌 스틱맨 대결 (docs/UX_FLOW.md 11절, Phase 3)")]
        [Tooltip("스폰 확률 판정 주기(초, '유휴 판정 주기마다'를 구체화한 값).")]
        public float rivalSpawnCheckInterval = 90f;

        [Tooltip("판정 주기마다 라이벌이 등장할 확률(0~1). UX 명시값 3~5% 구간. " +
                 "★ 2026-08-29 기본 OFF — 사용자 피드백 '머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데' / 총평 '제대로 동작하는게 하나도 없음'. 요청하지도 않은 구경거리가 자율 확률로 계속 떠서 캐릭터를 가리고, 유저는 그게 무엇인지도 알 수 없었다. 이 사용자가 프로젝트 내내 원해온 것은 '깔끔한 졸라맨이 돌아다니는 것'이다. 기능을 지우지 않고 **자율 발동만** 끈다 — 단축키/우클릭 메뉴의 수동(강제) 발동 경로는 이 값을 읽지 않으므로 그대로 살아 있다. 구경거리를 다시 켜고 싶으면 이 값만 올리면 된다(원래 기본값은 아래 괄호). 원래 기본값 0.04.")]
        public float rivalSpawnChance = 0f;

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
                 "전례가 이 프로젝트에 이미 두 번 있다: Dock 구간, 화면 클램프).")]
        public float stressTierCautionLevel = 0.4f;

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

        [Tooltip("macOS Dock 띠의 두께(OS 포인트). Dock 발판의 상단 = 화면 바닥 - 이 값이 된다. " +
                 "기본 75는 이 환경의 실측치다(CGDisplayBounds의 화면 전체 982pt - 작업영역 874pt - " +
                 "메뉴바 33pt = 75pt). Dock 자동 숨김을 쓰거나 Dock을 좌/우로 옮겼다면 0으로 두면 " +
                 "Dock 발판이 사라지고 모든 낙하가 화면 바닥 안전망으로 간다.")]
        public float dockFootholdThicknessPoints = 75f;

        [Tooltip("Dock 발판의 가로 폭(화면 폭 대비 비율, 화면 가로 정중앙 정렬). 0이면 Dock 발판 비활성. " +
                 "왜 실제 Dock 사각형을 쓰지 않는가는 Platform/FallbackPlatformWindowService.TryGetDockFoothold의 " +
                 "문서에 실측 조사 결과와 함께 적어뒀다(요약: Dock 창은 CGWindowList에 화면 전체 크기로 " +
                 "열거되고, 진짜 막대 폭은 화면 기록 권한 없이는 얻을 수 없다). " +
                 "기본값 0.65는 이 환경 실측 폭(1069/1512 = 0.707)보다 **일부러 좁게** 잡은 값이다 — " +
                 "추정이 넓으면 Dock 없는 자리에 캐릭터가 떠 있게 되고(사용자가 신고한 바로 그 증상), " +
                 "좁으면 실제 Dock 안쪽에서 조금 일찍 떨어질 뿐이라 틀리는 방향을 안전한 쪽으로 고정했다. " +
                 "Dock에 아이콘이 많아 실제 Dock이 더 넓다면 이 값을 올려도 된다.")]
        [Range(0f, 1f)]
        public float dockFootholdWidthFraction = 0.65f;

        [Header("눈 커서 추적 (사용자 요청: '마우스 위치에 따라 눈도 움직여야')")]

        [Tooltip("눈동자가 마우스 커서를 따라갈지 여부. 기본 ON. 끄면 눈동자가 부드럽게 중립(정면)으로 " +
                 "돌아가 고정된다 — 값을 바꾸면 재빌드 없이 다음 프레임부터 즉시 반영된다" +
                 "(States/EyeController.cs가 매 프레임 이 설정 묶음을 새로 읽기 때문).")]
        public bool eyeTrackingEnabled = true;

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
                 "일괄 갱신하기 때문이다(Core/StickmanAgent.cs 참고).")]
        public StickmanInkColor inkColor = StickmanInkColor.Black;

        [Tooltip("inkColor == Black일 때 쓰는 실제 색. 기존 필드를 그대로 재사용하므로 지금까지의 " +
                 "모든 배선/문서가 무효화되지 않는다.")]
        public Color primaryOutlineColor = Color.black;

        [Tooltip("inkColor == White일 때 쓰는 실제 색.")]
        public Color whiteInkColor = Color.white;

        [Tooltip("라이벌 스틱맨(docs/UX_FLOW.md 11절 '붉은 스틱맨')의 선 색. 플레이어와 즉시 구분되어야 " +
                 "하므로 잉크색 프리셋(검정/흰색)과 별개로 고정 색을 갖는다 — 플레이어가 흰색 프리셋일 " +
                 "때도 라이벌은 붉은색 그대로다. Interaction/RivalStickmanAgent.cs가 시작 시 자기 " +
                 "LineRenderer 전체에 이 값을 적용한다(에셋 값만 바꿔도 씬 재생성 없이 반영).")]
        public Color rivalInkColor = new Color(0.85f, 0.13f, 0.13f);

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
                 "상한(stepUpMaxHeight 1.5유닛)보다 작게 두어, 그 정도 높이 변화는 스냅이 아니라 " +
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

        [Tooltip("Dock 타일 하나가 차지하는 가로 피치에서 tilesize를 뺀 나머지(OS 포인트) — 타일 사이 여백.\n" +
                 "실측 근거(2026-08-29, 이 개발 머신 tilesize=49): 앱을 하나 실행/종료해 타일을 정확히 " +
                 "1개만 바꿨더니 Dock 패널 폭이 51.5pt 변했다. 즉 피치 = 51.5 = tilesize(49) + 2.5.")]
        public float dockTilePitchPaddingPoints = 2.5f;

        [Tooltip("타일 전체 폭에 더해지는 고정분(OS 포인트) — 패널 좌우 안쪽 여백 + 구분선(보통 2개).\n" +
                 "실측 근거: 타일 21개일 때 폭 1158.0, 20개일 때 1106.5. 두 표본 모두 " +
                 "(폭 - 타일수 x 51.5) = 76.5로 일치했다.")]
        public float dockPanelFixedPaddingPoints = 76.5f;

        [Tooltip("**실행 중이지만 Dock에 고정돼 있지 않은 앱**의 타일 수 보정치. 이 값은 어떤 공개 " +
                 "설정에도 없고(앱을 켜고 끌 때마다 변한다) 공개 API로 정확히 셀 방법도 없어서, " +
                 "'모르는 만큼'을 여기서 더한다.\n\n" +
                 "★ 왜 넉넉하게(= Dock을 실제보다 넓게) 잡는가 — 리더가 요구한 '어느 쪽으로 틀릴 것인가' " +
                 "판단이다. 두 방향의 실패 모습이 다르다:\n" +
                 " · 좁게 틀리면: Dock 가로 끝 바깥이 '안전망' 구간이 되어 캐릭터가 화면 최하단(OS y≈942)에 " +
                 "서는데, 그 자리에는 **진짜 Dock 아이콘이 있다**. 우리 오버레이는 항상 최상단이라 " +
                 "캐릭터가 Dock 아이콘 위에 덧그려진다 — 사용자가 두 번 신고한 바로 그 증상이고, " +
                 "화면에서 대단히 눈에 띈다.\n" +
                 " · 넓게 틀리면: Dock이 없는 자리에서도 캐릭터가 Dock 상단 높이(OS y≈907)에 선다. " +
                 "즉 화면 맨 아래에서 35pt 떠 보인다. 그 구간의 배경은 아무 것도 없는 벽지라 " +
                 "비교 대상이 없고, 화면 좌우 맨 끝이라 눈에 잘 띄지 않는다.\n" +
                 "두 증상을 실제로 띄워 비교한 결과 겹침이 확실히 더 나쁘다. 그래서 기본값을 0이 아니라 " +
                 "여유 있게 둔다. 0으로 내리면 '고정된 앱만' 세므로 반드시 좁게 틀린다.")]
        public int dockExtraRunningAppTileEstimate = 6;

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
        // (씬의 라이벌 스틱맨은 프리팹을 **복제해 언팩**한 사본이라 씬 재생성 없이는 따라오지 않는다.)
        // ────────────────────────────────────────────────────────────────────────────────────
        //
        // ★★ 일부러 **절대값으로 남겨둔** 값들과 그 근거 (기계적 비례화 금지 — 2026-08-29 검토)
        //   · parkourDetectionRadius(0.5) / hopDownProbeOutward(0.2) / hopDownMinDropHeight(0.35)
        //     → 판정 상대가 캐릭터가 아니라 **OS가 주는 창/Dock 사각형**이다. Dock 단차(0.855유닛)는
        //       캐릭터 크기와 무관하게 고정이므로 이 값들이 함께 줄면 판정만 예민해진다.
        //   · hopDownEdgeCommitDistance(0.12) → 제약이 "walkSpeed x 한 프레임"이고 둘 다 비례하지 않는다.
        //   · stepUpMaxHeight(1.5) → 반드시 Dock 단차 0.855를 덮어야 한다. 비례로 바꾸면 배율 0.57
        //       아래에서 1.5*s < 0.855가 되어 **한 번 Dock에서 내려간 캐릭터가 영영 못 올라온다**.
        //   · groundSnapTolerance(20 OS-pt) → OS 픽셀 단위의 접지 터널링 방지 허용오차. 낙하속도 x
        //       프레임시간에서 오는 값이라 캐릭터 크기와 무관하다.
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
                 "씬을 다시 구워야 반영된다(씬의 라이벌은 프리팹 사본이라 씬까지 다시 만들어야 한다).\n\n" +
                 "★★ 하한 경고 — 배율 약 0.341 아래에서는 **Dock 단차의 동작이 바뀐다**. 근거: " +
                 "Dock 상단→화면 최하단 낙차는 OS에서 오는 0.855유닛 고정인데, 매달리기 최소 낙차" +
                 "(StickmanBlackboard.LedgeHangMinDropDepth = 손끝~발끝 거리)는 팔다리에서 유도되어 " +
                 "2.507 x 배율이다. 2.507 x 0.341 = 0.855이므로 그보다 작은 배율에서는 Dock 단차가 " +
                 "'뛰어내리기' 밴드를 벗어나 '매달리기'로 분류되고, 그 낙차에서 매달리면 발이 이미 " +
                 "목적지를 지나쳐 있어 어색해진다. 그래서 슬라이더 하한을 0.35로 막아뒀다" +
                 "(현재 기본 0.75에서는 매달리기 최소 낙차가 1.880이라 0.855가 밴드 [0.35, 1.880) 안에 넉넉히 든다).\n\n" +
                 "참고 — walkSpeed도 이 배율에 비례한다(ResolveWalkSpeed()). 처음에는 '화면 폭은 그대로니 " +
                 "속도는 두자'고 판단했지만, 그 상태로 WalkFootSlipTests가 실패했다(디딤발 미끄러짐 0.465, " +
                 "상한 0.30) — 보폭이 배율에 비례하는데 속도가 고정이면 보행 사이클 주파수가 배율의 역수만큼 " +
                 "빨라져 poseSmoothingRate(35)가 목표 각도를 못 따라가고, 그게 곧 문워크다. 속도를 함께 " +
                 "줄이면 주파수가 배율과 무관해져 기존 실측 튜닝값이 어떤 배율에서도 그대로 유효하다. " +
                 "대가는 화면을 가로지르는 시간이 배율에 반비례해 늘어나는 것이며, 더 빠르게 하고 싶으면 " +
                 "walkSpeed 자체를 올리면 된다(배율은 그 위에 곱해진다).")]
        [Range(MinCharacterScale, MaxCharacterScale)]
        // ★ 2026-08-29 사용자 요구 "캐릭터 사이즈를 지금보다는 1.5배 더 키워주고" — 0.5 -> 0.75.
        // 전신 높이 2.2746944 x 0.75 = 약 1.7060유닛(화면상 약 60pt). 배율 0.75는 Dock 임계 배율
        // (DockHopDownCriticalScale = 0.341)보다 한참 위라 Dock 단차 0.855유닛이 '뛰어내리기' 밴드
        // [0.35, 2.5072 x 0.75 = 1.880) 안에 넉넉히 남는다 — 실제 빌드에서 왕복까지 육안 확인했다.
        public float characterScale = 0.75f;

        /// <summary>슬라이더 하한. Dock 단차 임계 배율(약 0.341, 위 Tooltip 유도)보다 조금 위에 둔다.</summary>
        public const float MinCharacterScale = 0.35f;

        /// <summary>슬라이더 상한. 1.0이 이미 "너무 크다"는 피드백을 받은 크기라 2배면 충분히 넉넉하다.</summary>
        public const float MaxCharacterScale = 2f;

        /// <summary>
        /// 배율 1.0에서의 전신 높이(발바닥~정수리, 월드 유닛). Editor/SceneBootstrapper.cs의 지오메트리
        /// 상수에서 유도한 값이며(1.35 몸통상단 + 0.4846944 접지보정 + 0.22 머리반경 x 2), 런타임에는
        /// Core/StickmanMetrics.cs가 이 값을 기준으로 실측 배율을 역산한다. 프리팹이 없는 폴백 경로의
        /// 기본 신장이기도 하다.
        /// </summary>
        public const float BaselineCharacterTotalHeight = 2.2746944f;

        /// <summary>
        /// Dock 상단→화면 최하단 낙차(0.855유닛, OS 실측에서 오는 고정값)가 '뛰어내리기' 밴드에 남아
        /// 있으려면 필요한 최소 배율 = 0.855 / 2.5072(배율 1.0에서의 손끝~발끝 거리). 이 값 자체는
        /// 코드가 소비하지 않고 Tests/PlayMode/CharacterScaleInvarianceTests.cs가 계산을 재확인한다.
        /// </summary>
        public const float DockHopDownCriticalScale = 0.341f;

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
            float s = characterScale;
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
    }
}
