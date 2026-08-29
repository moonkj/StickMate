using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// docs/UX_FLOW.md 19절 "상시(항상)" 노출 채널이 표현하는 3단계. 19절이 트레이 점 색상으로 정한
    /// 초록/노랑/빨강과 같은 3단이며, 경계값은 <see cref="StressGaugeRenderer.TierForLevel"/> 한 곳에서만
    /// 계산한다(같은 경계를 두 곳에서 따로 계산해 어긋난 전례가 이 프로젝트에 이미 두 번 있다).
    /// </summary>
    public enum StressMoodTier
    {
        /// <summary>정상 — 아무것도 그리지 않는다(평소 idle 그대로).</summary>
        Calm,

        /// <summary>주의 — 어깨가 처지기 시작한다.</summary>
        Caution,

        /// <summary>경고 — SULKY 임계값 이상. 어깨가 더 처지고 한숨이 잦아진다.</summary>
        Alarm,
    }

    /// <summary>
    /// ★ 스트레스 게이지 시각 레이어 — docs/UX_FLOW.md 19절 "게이지 노출 방식 — 상시 노출 대신 '필요시만'"의
    /// <b>상시 채널</b>("캐릭터의 표정/자세로만 은은하게 암시 — 수치 없음")을 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Core/StressGauge.cs(값 보관 + 통지)와 Interaction/StressGaugeDirector.cs(격파훈련 과다/장시간
    /// 방치/긴급정지 반복/자연 감소 4개 트리거 + SULKY 전이)는 Phase 5에 완성돼 있었다. 그런데
    /// <b>StickmanEventBus.StressLevelChanged를 구독하는 코드가 프로젝트 전체에 0건이었고</b> Director
    /// 자신도 씬 어디에도 배치돼 있지 않았다 — 창 도둑/크래시/하드웨어 반응과 완전히 같은 유형의
    /// "로직 완성, 화면엔 0픽셀" 실패다(이 프로젝트에서 6번째).
    ///
    /// ============================================================================
    /// 무엇을 그리는가 — 그리고 무엇을 <b>일부러 안 그리는가</b>
    /// ============================================================================
    /// 19절은 "관찰형 앱에 체력바/게이지 UI가 상시 떠 있으면 캐릭터가 아니라 '육성 게임'처럼 보여 앱의
    /// 톤이 깨진다"고 못박았다. 그래서 이 렌더러는 <b>막대 게이지도, 퍼센트 숫자도 절대 그리지 않는다</b>.
    /// 대신 19절이 상시 채널에 허용한 것만 그린다 — "어깨가 처지고 표정이 흐려지는 점진적 비주얼 변화".
    ///
    ///  · 어깨 처짐 표시(양 어깨에서 바깥·아래로 늘어지는 짧은 호 2개) — 단계가 오를수록 더 깊이 처진다.
    ///  · 한숨 퍼프(머리 옆에서 천천히 떠올라 흩어지는 작은 원) — 단계가 오를수록 더 자주 난다.
    ///  · 색은 24절이 가출/반항 계열에 지정한 <b>채도 낮은 팔레트</b>를 따른다(인질극 계열의 "밝은
    ///    강조색 반짝임"과 시각적으로 확실히 갈라놓기 위해서다).
    ///
    /// 나머지 두 채널은 이 렌더러의 몫이 아니다: "필요시(트레이)"의 색점은 트레이가 없는 이 앱에서
    /// 우클릭 제어 메뉴 행이 대신하고(Interaction/AppControlDirector.cs가 <see cref="TierForLevel"/>을
    /// 그대로 재사용해 라벨을 만든다 — 경계 계산이 두 벌이 되지 않는다), "원할 때(설정창)"의 상세
    /// 게이지 바는 설정창 자체가 아직 없어 이번 스코프 밖이다.
    ///
    /// ============================================================================
    /// SpectacleEventLock — 이 렌더러도, StressGauge도 참여하지 않는다
    /// ============================================================================
    /// Phase 5 설계 결정 1(확정): 참여 기준은 "ChangeState()를 직접 호출해 단일 상태 슬롯을 다투는가"다.
    /// StressGauge는 값 보관 + 이벤트 발행만 하고, 이 렌더러는 현재 상태 위에 얹는 순수 오버레이라
    /// 둘 다 비참여가 맞다(HardwareReactionRenderer와 정확히 같은 논리). 반면 같은 Director 안의
    /// SULKY 전이는 ChangeState를 호출하므로 <b>그 부분만</b> 락에 참여한다 — 이미 그렇게 구현돼 있고
    /// 이 라운드에서 건드리지 않았다.
    ///
    /// ============================================================================
    /// 이벤트 빈도에 대한 주의(성능)
    /// ============================================================================
    /// StressLevelChanged는 자연 감소(StickConfig.stressPassiveDecayPerHour) 때문에 게이지가 0보다 클 때
    /// <b>수 프레임마다 한 번씩 계속 발행된다</b>(값이 0일 때는 clamp 때문에 발행되지 않는다).
    /// 그래서 이 렌더러는 이벤트를 받을 때마다 도형을 다시 만들지 않고 <b>단계(Tier)가 실제로 바뀐
    /// 순간에만</b> 재구성한다 — 24시간 상주 앱에서 매 프레임 GameObject를 만들고 부수면 안 된다.
    /// </summary>
    public sealed class StressGaugeRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const float FadeInSeconds = 0.9f;    // 은은하게 — "점진적 비주얼 변화"(19절)라 확 튀면 안 된다.
        private const float FadeOutSeconds = 1.1f;   // 회복은 더 느긋하게(달래진 직후 급변하면 게임 UI처럼 보인다).

        // ============================================================================
        // ★ 2026-08-29 리더 지시 — 캐릭터 기준 치수는 전부 **전신 높이 대비 비율**이다.
        // ============================================================================
        // 캐릭터 루트(Rigidbody2D.position)는 **발 높이**가 로컬 y=0이다. 종전에는 그 위에 얹는 값을
        // 전부 절대 월드유닛 상수로 적어 두었는데, StickConfig.characterScale이 0.5가 되는 순간
        // (사용자 요구 "지금의 절반 + 추후 조정 가능") 캐릭터만 절반이 되고 이 상수들은 그대로라
        // 어깨 표시가 **머리 위 허공**에 뜬다. 그래서 배치·크기·속도를 전부 비율로 옮긴다.
        //
        // 기준 치수의 유일한 조회 경로는 Core/StickmanMetrics.cs다 — 상수 복사가 아니라 계층 실측이고,
        // 같은 계산이 렌더러마다 한 벌씩 생겨 그 중 하나가 조용히 어긋나는 이 프로젝트의 반복 실패
        // (Dock 구간 이중 계산, 씬 지면 Y 이중 정의)를 구조적으로 막는다.
        //
        // 아래 비율의 분자는 **검증을 마친 종전 값 그 자체**이고 분모는 배율 1.0 기준 신장이다.
        // 따라서 배율 1.0에서는 지금까지와 완전히 같은 그림이 나온다(= 회귀 없음의 증거).
        //
        // ★ 단 하나의 예외: 어깨 높이.
        //   종전 상수 ShoulderY = 1.33은 접지 보정(SceneBootstrapper의 footLift, 배율 1.0에서 0.4847)이
        //   들어가기 **전** 프리팹(정수리 약 1.79)에서 손으로 옮겨 적은 값이다. 지금 프리팹의 실측 어깨는
        //   1.7647이므로, 1.33은 배율 1.0에서조차 어깨가 아니라 **갈비뼈 언저리(0.43유닛 아래)**를
        //   가리키고 있었다. 여기에 비율화를 그대로 적용하면 "틀린 위치를 배율에 맞춰 정확히 유지"하게
        //   되므로, 이 한 값만은 비율 대신 StickmanMetrics.ShoulderLocalY 실측을 쓴다
        //   (리더 지시: "전용 멤버가 있으면 비율 계산 대신 그걸 써라 — 그게 더 정확하다").
        //   같은 이유로 한숨 퍼프의 높이도 머리 실측(HeadCenterLocalY / HeadRadius)에 붙인다.
        private const float ShoulderHalfSpanRatio = 0.40f / StickConfig.BaselineCharacterTotalHeight;   // 어깨 표시가 좌우로 벌어지는 거리.
        private const float DroopReachRatio = 0.30f / StickConfig.BaselineCharacterTotalHeight;         // 처짐 호가 바깥으로 뻗는 길이.
        private const float DroopDepthAlarmRatio = 0.34f / StickConfig.BaselineCharacterTotalHeight;    // 경고 단계 처짐 깊이.
        private const float DroopDepthCautionRatio = 0.19f / StickConfig.BaselineCharacterTotalHeight;  // 주의 단계 처짐 깊이.
        private const float SlumpBackStartXRatio = 0.10f / StickConfig.BaselineCharacterTotalHeight;    // 굽은 등 획 시작 x.
        private const float SlumpBackReachXRatio = 0.16f / StickConfig.BaselineCharacterTotalHeight;    // 굽은 등 획이 뒤로 뻗는 길이.
        private const float SlumpBackRiseYRatio = 0.12f / StickConfig.BaselineCharacterTotalHeight;     // 굽은 등 획 시작 높이(어깨 위).
        private const float SlumpBackDropYRatio = 0.30f / StickConfig.BaselineCharacterTotalHeight;     // 굽은 등 획이 내려가는 깊이.
        private const float SighSpawnXRatio = 0.52f / StickConfig.BaselineCharacterTotalHeight;         // 한숨 퍼프가 나는 가로 위치(머리 옆).
        private const float SighRadiusRatio = 0.085f / StickConfig.BaselineCharacterTotalHeight;        // 한숨 퍼프 원의 반지름.
        private const float StrokeWidthRatio = 0.048f / StickConfig.BaselineCharacterTotalHeight;       // 획 두께.
        private const float ClampMarginRatio = 0.75f / StickConfig.BaselineCharacterTotalHeight;        // 화면 경계 여유(FollowShoulders 참고).

        // ★ 이동 속도도 비율이다(리더 지시 3 — 배치만 비율화하고 속도를 절대로 남기면 알갱이가 몸 쪽으로
        // 두 배 깊이 파고든다. HardwareReactionRenderer의 땀방울에서 실제로 발생한 실패다).
        // 배율 0.5에서 절대 속도를 그대로 두면 한숨 퍼프가 같은 수명 동안 **몸 길이의 두 배 비율**로
        // 솟아올라 머리를 훌쩍 넘어가 버린다.
        private const float SighRiseSpeedRatio = 0.42f / StickConfig.BaselineCharacterTotalHeight;      // 한숨 퍼프 상승(유닛/초).
        private const float SighDriftSpeedRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight;     // 한숨 퍼프 좌우 표류(유닛/초).

        // 어깨/머리 실측을 못 구했을 때의 폴백 비율(배율 1.0 프리팹 기준). StickmanMetrics 자신이 쓰는
        // 것과 같은 값이라, 이 경로로 떨어져도 "어깨가 발밑에 있다" 같은 값은 나오지 않는다.
        private const float BaselineShoulderRatio = 1.7646944f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHeadCenterRatio = 2.0546944f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHeadRadiusRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight;
        // 한숨 퍼프는 머리 중심에서 머리 반경의 이만큼 위에 난다(종전 1.62 - 머리중심 1.57 = 0.05, 반경 0.22 기준).
        private const float SighAboveHeadCenterRatio = 0.05f / 0.22f;

        private const int SortingOrder = 8;            // 캐릭터 획(0~5) 위, 그라피티(9)/격파(10~15) 아래.

        private const float SighLifeSeconds = 1.5f;
        private const int SighMaxAlive = 3;

        // 24절 "처지고 어두운 표정, 채도 낮은 팔레트" — 인질극 계열의 밝은 강조색과 반대 방향으로 잡는다.
        private static readonly Color CautionColor = new Color(0.72f, 0.63f, 0.36f, 1f);
        private static readonly Color AlarmColor = new Color(0.70f, 0.42f, 0.40f, 1f);

        private sealed class Puff
        {
            public Transform Root;
            public LineRenderer Line;
            public float Age;
            public float DriftX;
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 라이벌은 플레이어 프리팹의 복제본이라 이 컴포넌트를 함께 갖게 되고,
        /// 폴백을 두면 라이벌 어깨에도 처짐 표시가 한 벌 더 뜬다(2026-08-29 격파 미니게임에서 실측으로
        /// 확인된 버그와 같은 함정). SceneBootstrapper가 라이벌에서 제거하는 것이 1차 방어, 이 가드가 2차.
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        // ==================== 캐릭터 실측 치수 조회 ====================

        /// <summary>캐릭터 치수의 <b>유일한</b> 조회 경로(Core/StickmanMetrics.cs). 값이 매 프레임 필요한
        /// 경로라 컴포넌트를 한 번만 찾아 캐시한다(GetComponentInParent를 24시간 내내 돌리지 않는다).
        /// 못 찾으면(손으로 조립한 테스트 리그 등) null을 캐시하고 아래 비율 폴백으로 떨어진다.</summary>
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
        // (아래 4개는 Tests/PlayMode/RendererScaleRatioTests.cs가 배율 1.0/0.5 양쪽에서 단언한다.)

        /// <summary>어깨 표시가 붙는 로컬 Y(발바닥 기준). <b>실측 어깨 높이 그 자체</b>이며 비율이 아니다.</summary>
        public float ShoulderAnchorLocalY
        {
            get
            {
                StickmanMetrics m = Metrics;
                return m != null ? m.ShoulderLocalY : Height * BaselineShoulderRatio;
            }
        }

        /// <summary>한숨 퍼프가 나는 로컬 Y(발바닥 기준) — 머리 실측(중심 + 반경 비율)에 붙는다.</summary>
        public float SighSpawnLocalY
        {
            get
            {
                StickmanMetrics m = Metrics;
                float headCenter = m != null ? m.HeadCenterLocalY : Height * BaselineHeadCenterRatio;
                float headRadius = m != null ? m.HeadRadius : Height * BaselineHeadRadiusRatio;
                return headCenter + headRadius * SighAboveHeadCenterRatio;
            }
        }

        /// <summary>어깨 표시가 좌우로 벌어지는 거리(월드 유닛).</summary>
        public float ShoulderHalfSpan => Height * ShoulderHalfSpanRatio;

        /// <summary>획 두께(월드 유닛).</summary>
        public float StrokeWidth => Height * StrokeWidthRatio;

        private float SighSpawnX => Height * SighSpawnXRatio;
        private float SighRiseSpeed => Height * SighRiseSpeedRatio;
        private float SighDriftSpeed => Height * SighDriftSpeedRatio;
        private float SighRadius => Height * SighRadiusRatio;

        private GameObject _container;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(4);
        private readonly List<Puff> _puffs = new List<Puff>(SighMaxAlive);

        private StressMoodTier _tier = StressMoodTier.Calm;
        private float _alpha;          // 0~1 페이드 진행도.
        private float _sighTimer;
        private bool _fadingOut;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 화면에 기분 표시가 떠 있는지(Calm이면 false).</summary>
        public bool IsVisible => _container != null;

        /// <summary>지금 표현 중인 단계.</summary>
        public StressMoodTier CurrentTier => _tier;

        /// <summary>마지막으로 관측한 게이지 값(0~1). 진단 로그/테스트 확인용이며 화면에는 절대 표시하지 않는다(19절).</summary>
        public float LastObservedLevel { get; private set; }

        /// <summary>이 연출이 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 연출이 만든 콜라이더 수 — 항상 0이어야 한다(관전 전용, 클릭관통 유지).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        /// <summary>
        /// 게이지 값 -> 3단계. <b>경계 계산의 유일한 생산자</b>다 — Interaction/AppControlDirector.cs의
        /// 메뉴 라벨("스트레스: ● 주의")도 이 메서드만 호출한다.
        ///
        /// 경고 경계를 별도 상수로 두지 않고 <see cref="StickConfig.stressSulkyThreshold"/>를 그대로
        /// 재사용하는 이유: 19절이 "임계값 근접(예: 80%) 시 SULKY로 전이"라고 정한 그 지점이 곧 유저가
        /// 빨간 신호를 봐야 하는 지점이다. 두 값을 따로 두면 "빨간 점인데 부루퉁하지는 않은" 또는 그
        /// 반대의 어긋난 상태가 생긴다(Dock 구간/화면 클램프에서 이미 두 번 겪은 실패 유형).
        /// </summary>
        public static StressMoodTier TierForLevel(float level, StickConfig config)
        {
            // ★★ 마스터 스위치(2026-08-29, 사용자 신고 "몸주위로 이상한 주황색 선들이 생김" 대응).
            //
            // 이 프로젝트의 "기본 OFF" 관례는 <b>도달 불가능한 임계값</b>을 넣는 것이다
            // (StickConfig.stressRunawayThreshold = 2.0이 같은 이유로 그렇게 되어 있다 — 게이지가
            // 0~1로 클램프되므로 1보다 큰 값은 원리적으로 도달할 수 없다). 그런데 이 메서드만은
            // 임계값을 올리는 것만으로 꺼지지 않았다: 아래 caution 계산이 <c>Clamp(..., 0f, alarm)</c>로
            // **alarm(=stressSulkyThreshold=0.8)까지 눌러버리기** 때문에, caution을 2.0으로 올려도
            // 실효값이 0.8이 되어 "주황(주의)은 사라지지만 게이지가 0.8을 넘으면 빨강(경고)이 그대로
            // 뜨는" 반쪽 상태가 된다. 그리고 alarm 쪽은 Clamp01이라 1을 넘길 수조차 없다.
            //
            // 그래서 clamp보다 **앞에서** 원본 값을 한 번 본다: 1보다 크면 "이 상시 표시를 끈다"는
            // 뜻으로 해석해 항상 Calm을 돌려준다. 기능을 지우는 것이 아니라 조용하게 만드는 것이며,
            // 값을 원래 기본값 0.4로 되돌리면 아래 기존 경로를 100% 그대로 탄다(거동 동일).
            float rawCaution = config != null ? config.stressTierCautionLevel : 0.4f;
            if (rawCaution > 1f) return StressMoodTier.Calm;

            float alarm = config != null ? Mathf.Clamp01(config.stressSulkyThreshold) : 0.8f;
            float caution = config != null ? Mathf.Clamp(config.stressTierCautionLevel, 0f, alarm) : 0.4f;
            if (level >= alarm) return StressMoodTier.Alarm;
            if (level >= caution) return StressMoodTier.Caution;
            return StressMoodTier.Calm;
        }

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable()
        {
            StickmanEventBus.StressLevelChanged += OnStressLevelChanged;
            // 이 컴포넌트가 켜지는 시점에 이미 게이지가 쌓여 있을 수 있다(정적 클래스라 씬 생명주기와
            // 무관하게 값이 살아 있다) — 그 경우 아무 이벤트도 오지 않으므로 여기서 한 번 맞춰준다.
            OnStressLevelChanged(StressGauge.CurrentLevel);
        }

        private void OnDisable()
        {
            StickmanEventBus.StressLevelChanged -= OnStressLevelChanged;
            // 이 컴포넌트가 꺼질 때 어깨 표시가 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnStressLevelChanged(float level)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본(라이벌) — 전역 이벤트를 받아도 무시한다.

            LastObservedLevel = level;
            StressMoodTier target = TierForLevel(level, _agent.Config);
            // 단계가 그대로면 아무것도 하지 않는다 — 이 이벤트는 자연 감소 때문에 수 프레임마다 계속
            // 날아오므로(클래스 문서 "이벤트 빈도에 대한 주의"), 여기서 걸러내지 않으면 24시간 상주 앱이
            // 매 프레임 GameObject를 만들고 부수게 된다.
            if (target == _tier) return;

            ApplyTier(target, level);
        }

        private void ApplyTier(StressMoodTier target, float level)
        {
            StressMoodTier previous = _tier;
            _tier = target;

            if (target == StressMoodTier.Calm)
            {
                if (_container == null) return;
                _fadingOut = true;
                Debug.Log($"[스트레스] 단계 {TierLabel(previous)} -> {TierLabel(target)} — 기분이 정상 범위로 " +
                    $"돌아와 어깨 처짐/한숨을 {FadeOutSeconds:F2}초에 걸쳐 걷습니다(수치는 화면에 표시하지 않는다 — 19절).");
                return;
            }

            Rebuild(target);
            _fadingOut = false;
            Debug.Log($"[스트레스] 단계 {TierLabel(previous)} -> {TierLabel(target)} " +
                $"(내부 게이지 {level:F3} — 화면에는 숫자도 막대도 그리지 않는다, 19절) — " +
                $"어깨 처짐 표시 + 한숨 퍼프 시작. 시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0).");
        }

        // ==================== 생성 ====================

        private void Rebuild(StressMoodTier tier)
        {
            float keptAlpha = _alpha;
            Teardown();

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[스트레스] 기분 표시를 그리지 못했습니다 — 캐릭터 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("StressMoodOverlay");
            _container.transform.SetParent(null, false);
            _container.transform.position = AnchorWorldPosition();

            bool alarm = tier == StressMoodTier.Alarm;
            Color color = alarm ? AlarmColor : CautionColor;
            // 단계가 오를수록 더 깊이 처진다 — "점진적 비주얼 변화"(19절)를 한 개의 숫자로 표현한다.
            float droop = Height * (alarm ? DroopDepthAlarmRatio : DroopDepthCautionRatio);
            float shoulderLocalY = ShoulderAnchorLocalY;
            float droopReach = Height * DroopReachRatio;
            float halfSpan = ShoulderHalfSpan;

            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = halfSpan * side;
                var pts = new Vector3[5];
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)(pts.Length - 1);
                    // 어깨에서 바깥으로 나가며 아래로 늘어지는 호(2차 곡선).
                    pts[i] = new Vector3(x0 + side * t * droopReach, shoulderLocalY - droop * t * t, 0f);
                }
                _lines.Add(CreateLine(side < 0 ? "ShoulderDroopL" : "ShoulderDroopR", pts, color, loop: false));
            }

            if (alarm)
            {
                // 경고 단계에서만 목/등이 굽은 것을 한 획 더 얹는다(자세가 더 무너졌다는 신호).
                var back = new Vector3[4];
                float startX = Height * SlumpBackStartXRatio;
                float reachX = Height * SlumpBackReachXRatio;
                float riseY = Height * SlumpBackRiseYRatio;
                float dropY = Height * SlumpBackDropYRatio;
                for (int i = 0; i < back.Length; i++)
                {
                    float t = i / (float)(back.Length - 1);
                    back[i] = new Vector3(-startX - t * reachX, shoulderLocalY + riseY - t * dropY, 0f);
                }
                _lines.Add(CreateLine("SlumpBack", back, color, loop: false));
            }

            _alpha = keptAlpha;
            _sighTimer = 0f;
            ApplyAlphaToAll(_alpha);
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            // ★ 2026-08-29 실측 버그 수정(배율 0.75 육안 검증 중 발견).
            // OnEnable()은 "켜지는 시점에 이미 게이지가 쌓여 있을 수 있다"를 처리하려고 곧바로
            // OnStressLevelChanged를 부른다. 그런데 그 시점에는 StickmanAgent가 아직 Blackboard/Body를
            // 배선하기 전이라 Rebuild()가 "캐릭터 배선이 없습니다" 경고만 남기고 아무것도 그리지 못한다.
            // 그 사이 _tier는 이미 Caution/Alarm으로 **확정**돼 버려서, 이후 StressLevelChanged가 아무리
            // 날아와도 `target == _tier`에서 전부 걸러진다 — 즉 어깨 처짐이 **영원히 0픽셀**이 된다.
            // 실측 Player.log: "기분 표시를 그리지 못했습니다" 직후 "단계 안정 -> 주의 ... 시각 오브젝트 0개".
            // 이 프로젝트가 반복해서 겪은 "로직은 도는데 화면엔 0픽셀"과 정확히 같은 실패다.
            //
            // 그래서 배선이 준비된 뒤 한 번 따라잡는다. 배선이 준비됐을 때만 시도하므로 헛도는 프레임이
            // 없고, Calm이거나 사라지는 중이면 애초에 그릴 것이 없어 곧바로 빠져나간다.
            if (_container == null)
            {
                if (_tier == StressMoodTier.Calm || _fadingOut) return;
                var pending = _agent != null ? _agent.Blackboard : null;
                if (pending == null || pending.Body == null) return; // 아직 배선 전 — 다음 프레임에 다시 본다.
                Debug.Log($"[스트레스] 시작 프레임 경합 보정 — 단계 {TierLabel(_tier)}가 배선 완료 전에 확정돼 " +
                    "그려지지 못했습니다. 배선이 준비되어 지금 다시 그립니다.");
                Rebuild(_tier);
                if (_container == null) return;
            }

            float dt = Time.deltaTime;

            _alpha = _fadingOut
                ? Mathf.MoveTowards(_alpha, 0f, dt / FadeOutSeconds)
                : Mathf.MoveTowards(_alpha, 1f, dt / FadeInSeconds);

            if (_fadingOut && _alpha <= 0f && _puffs.Count == 0)
            {
                Teardown();
                return;
            }

            FollowShoulders();
            ApplyAlphaToAll(_alpha);
            TickSighs(dt);
        }

        /// <summary>
        /// 어깨 표시를 캐릭터에 붙여 따라다니게 하되 <b>화면 안에 머무르게 클램프</b>한다.
        /// HardwareReactionRenderer.FollowHead()와 같은 이유다 — 캐릭터는 창의 상단 테두리나 Dock을
        /// 발판으로 삼아 화면 최상단에 서 있는 시간이 길고, 그때 어깨 높이조차 잘려 나갈 수 있다.
        /// 여유(margin)도 절대 유닛이 아니라 전신 높이 비율이다 — 배율 0.5에서 절대 0.75유닛을 그대로
        /// 두면 캐릭터 반 키만큼을 화면 안쪽으로 끌어당겨, 화면 끝에 선 캐릭터에서 어깨 표시만 몸을
        /// 벗어나 따로 논다.
        /// </summary>
        private void FollowShoulders()
        {
            Vector3 target = AnchorWorldPosition();

            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                float margin = Height * ClampMarginRatio; // 어깨 표시 + 한숨 퍼프가 함께 들어오는 여유.
                Vector3 camPos = cam.transform.position;
                target.x = Mathf.Clamp(target.x, camPos.x - halfW + margin, camPos.x + halfW - margin);
                target.y = Mathf.Clamp(target.y, camPos.y - halfH + margin, camPos.y + halfH - margin);
            }

            _container.transform.position = target;
        }

        private Vector3 AnchorWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 basePos = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;
            return new Vector3(basePos.x, basePos.y, 0f);
        }

        private void TickSighs(float dt)
        {
            bool alarm = _tier == StressMoodTier.Alarm;
            float interval = alarm ? 1.4f : 2.4f;

            if (!_fadingOut && _container != null)
            {
                _sighTimer += dt;
                if (_sighTimer >= interval && _puffs.Count < SighMaxAlive)
                {
                    _sighTimer = 0f;
                    SpawnSigh(alarm);
                }
            }

            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff p = _puffs[i];
                if (p?.Line == null) { _puffs.RemoveAt(i); continue; }

                p.Age += dt;
                float t = Mathf.Clamp01(p.Age / SighLifeSeconds);
                if (t >= 1f)
                {
                    if (p.Root != null) Destroy(p.Root.gameObject);
                    _puffs.RemoveAt(i);
                    continue;
                }

                p.Root.localPosition += new Vector3(p.DriftX * dt, SighRiseSpeed * dt, 0f); // 둘 다 전신 높이 비율에서 유도된 속도다.
                float scale = Mathf.Lerp(0.6f, 1.5f, t);
                p.Root.localScale = new Vector3(scale, scale, 1f);

                Color c = p.Line.startColor;
                c.a = (1f - t) * 0.75f * _alpha;
                p.Line.startColor = c;
                p.Line.endColor = c;
            }
        }

        private void SpawnSigh(bool alarm)
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            float facing = blackboard != null && blackboard.FacingSign != 0f ? Mathf.Sign(blackboard.FacingSign) : 1f;

            var go = new GameObject("SighPuff");
            go.transform.SetParent(_container.transform, false);
            // 컨테이너는 발바닥(AnchorWorldPosition)에 놓이므로 이 로컬 Y가 곧 "발바닥에서 얼마나 위"다.
            go.transform.localPosition = new Vector3(SighSpawnX * facing, SighSpawnLocalY, 0f);

            Vector3[] shape = BuildCircle(Vector3.zero, SighRadius, 9);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            Color color = alarm ? AlarmColor : CautionColor;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = StrokeWidth * 0.8f;
            lr.endWidth = StrokeWidth * 0.8f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
            lr.loop = true;
            lr.positionCount = shape.Length;
            lr.SetPositions(shape);

            _puffs.Add(new Puff { Root = go.transform, Line = lr, Age = 0f, DriftX = facing * SighDriftSpeed });
        }

        // ==================== 종료 ====================

        private void ApplyAlphaToAll(float alpha)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                c.a = alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        private void Teardown()
        {
            _lines.Clear();
            _puffs.Clear();
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _alpha = 0f;
            _fadingOut = false;
        }

        // ==================== 도형 유틸 ====================

        private LineRenderer CreateLine(string name, Vector3[] points, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = StrokeWidth;
            lr.endWidth = StrokeWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
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

        public static string TierLabel(StressMoodTier tier)
        {
            switch (tier)
            {
                case StressMoodTier.Caution: return "주의";
                case StressMoodTier.Alarm: return "경고";
                default: return "안정";
            }
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
