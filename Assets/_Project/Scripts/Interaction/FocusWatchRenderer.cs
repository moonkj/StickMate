using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 포모도로 감시자 시각 레이어 — docs/UX_FLOW.md 18절의 두 가지 시각 요구를 실제로 그리는 소비자.
    ///   (1) "화면 한켠에 작은 <b>원형 타이머 링</b>(캐릭터 발밑, 앱 소유 UI)"
    ///   (2) "경고 단계별 연출(점진적 에스컬레이션)" 1/3단계 — 2단계는 대사라 말풍선이 담당한다.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Interaction/FocusWatchDirector.cs는 기존 발판 폴링 캐시를 재활용한 포커스 전환 빈도 판정,
    /// 마우스 극단값 보조 신호, 유예 시간, 연속 관찰 창 누적, 즉시 리셋 규칙, 민감도 3단계, 4개 포즈
    /// 상태 전이까지 전부 완성돼 있었다. 그런데 <b>StickmanEventBus.FocusWatchTierChanged를 구독하는
    /// 코드가 프로젝트 전체에 0건이었고</b> Director 자신도 씬 어디에도 배치돼 있지 않았다 — 타이머
    /// 링은 아예 존재한 적이 없고, 1/3단계 경고는 이벤트만 허공에 발행되고 있었다.
    ///
    /// ============================================================================
    /// 단계별로 무엇을 그리는가 (18절 "경고 단계별 연출")
    /// ============================================================================
    /// None      -> 링만. 평온한 색.
    /// Glance    -> 1단계 "말없이 곁눈질하는 표정 변화만(대사 없음)". 머리 옆에 곁눈질 호 2개가 아주
    ///              느리게 깜빡인다. <b>가장 자주 발동해도 거슬리지 않아야 하는 단계</b>라 일부러 작고
    ///              옅게 만들었다.
    /// Nudge     -> 2단계는 대사("어? 딴 데 보고 있네?")가 본체이고 그건 상태 전이(FocusNudge)에서
    ///              파생돼 말풍선이 그린다. 여기서는 곁눈질을 조금 더 또렷하게 유지만 한다 — 같은 말을
    ///              두 곳에서 하지 않는다.
    /// WindowTap -> 3단계 "캐릭터가 타이머 위젯을 통통 두드리는 과장된 애니메이션 + 살짝 화면 흔들림".
    ///              링이 리듬에 맞춰 떨리고 두드림 자국 3개가 튄다. <b>흔들리는 것은 이 오버레이(캐릭터
    ///              레이어 국소 효과)뿐이고 실제 창은 1픽셀도 흔들지 않는다</b> — 이 클래스에는 창 좌표를
    ///              바꾸는 API가 존재하지 않는다(원칙 3, 18절이 직접 못박은 조건).
    ///
    /// ============================================================================
    /// 절대 원칙 — 이 클래스가 하지 않는 일
    /// ============================================================================
    /// 다른 창의 <b>내용</b>을 읽지 않는다. 애초에 이 클래스는 창을 조회조차 하지 않는다 — Director가
    /// 넘겨주는 것은 "지금 몇 단계인가"라는 열거값 하나와 남은 시간뿐이다. 콜라이더도 만들지 않는다
    /// (순수 관전 연출 = 클릭관통 유지). 18절의 "타이머 링 클릭으로 종료" 탈출구는 이번 스코프에
    /// 넣지 않았고, 같은 절이 병렬로 제시한 "트레이에서 집중 모드 끄기"를 우클릭 메뉴 행 +
    /// Ctrl+Opt+Cmd+F 단축키로 제공한다(트레이 긴급정지도 18절대로 항상 유효하다).
    /// </summary>
    public sealed class FocusWatchRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        // ============================================================================
        // ★ 2026-08-29 리더 지시 — 캐릭터 기준 치수는 전부 **전신 높이 대비 비율**이다.
        // ============================================================================
        // 캐릭터 루트는 **발 높이**가 y=0이다(SceneBootstrapper 프리팹 지오메트리 주석 참고).
        // 링은 18절이 지정한 "발밑"이므로 발 높이 근처를 중심으로 잡는다 — 머리 위 오버레이
        // (HardwareReactionRenderer의 이모트 / StressGaugeRenderer의 어깨 표시)와 세로로 완전히 갈라진다.
        //
        // 종전에는 이 값들이 전부 절대 월드유닛이었다. 그래서 StickConfig.characterScale이 0.5가 되면
        // 캐릭터만 절반이 되고 **링 반지름 0.54는 그대로**라, 발밑 링이 캐릭터 키의 거의 절반을 삼켜
        // 몸을 가로지르고 곁눈질 호는 정수리(배율 0.5에서 1.137) 훨씬 위 허공에 뜬다.
        //
        // 기준 치수의 유일한 조회 경로는 Core/StickmanMetrics.cs다(상수 복사가 아니라 계층 실측).
        // 아래 비율의 분자는 검증을 마친 종전 값 그 자체, 분모는 배율 1.0 기준 신장이므로 배율 1.0에서는
        // 지금까지와 완전히 같은 그림이 나온다.
        //
        // ★ 예외: 곁눈질 호의 높이(종전 GlanceY = 1.72).
        //   그 값은 "정수리 약 1.79 바로 아래"라는 주석대로 **접지 보정(footLift) 이전 프리팹**의 머리
        //   위치에서 옮겨 적은 것이다. 지금 프리팹의 실측 머리 중심은 2.0547 / 정수리 2.2747이므로
        //   1.72는 배율 1.0에서조차 머리가 아니라 목 아래를 가리킨다. 그래서 이 한 값만은 비율 대신
        //   StickmanMetrics의 머리 실측(HeadCenterLocalY / HeadRadius)에 붙인다 — 리더 지시
        //   "머리처럼 이미 전용 멤버가 있으면 비율 계산 대신 그걸 써라".
        private const float RingCenterYRatio = 0.08f / StickConfig.BaselineCharacterTotalHeight;
        private const float RingRadiusRatio = 0.54f / StickConfig.BaselineCharacterTotalHeight;
        private const int RingSegments = 40;

        private const float GlanceXRatio = 0.46f / StickConfig.BaselineCharacterTotalHeight;      // 곁눈질 호의 가로 위치(머리 옆).
        private const float GlanceArcBulgeXRatio = 0.06f / StickConfig.BaselineCharacterTotalHeight; // 호가 가로로 부푸는 폭.
        private const float GlanceArcBulgeYRatio = 0.12f / StickConfig.BaselineCharacterTotalHeight; // 호가 세로로 부푸는 폭.
        // 곁눈질 호는 머리 중심에서 머리 반경의 이만큼 위에 놓인다(종전 1.72 - 머리중심 1.57 = 0.15, 반경 0.22 기준).
        private const float GlanceAboveHeadCenterRatio = 0.15f / 0.22f;

        private const float TapShakeAmplitudeRatio = 0.075f / StickConfig.BaselineCharacterTotalHeight;
        private const float TapMarkInnerGapRatio = 0.08f / StickConfig.BaselineCharacterTotalHeight;  // 두드림 자국 안쪽 끝(링 바깥 여유).
        private const float TapMarkOuterGapRatio = 0.28f / StickConfig.BaselineCharacterTotalHeight;  // 두드림 자국 바깥쪽 끝.
        private const float StrokeWidthRatio = 0.05f / StickConfig.BaselineCharacterTotalHeight;

        // ★ TapShakeSpeed는 각진동수(rad/초)라 길이 차원이 아니다 — 비율화 대상이 아니고 절대값이 맞다.
        // 크기가 절반이 되어도 "같은 빠르기로 떠는" 것이 자연스럽다(진폭만 절반이 된다).
        private const float TapShakeSpeed = 17f;
        private const int TapMarkCount = 3;

        // 머리 실측을 못 구했을 때의 폴백 비율(배율 1.0 프리팹 기준) — StickmanMetrics 자신이 쓰는 값과 같다.
        private const float BaselineHeadCenterRatio = 2.0546944f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHeadRadiusRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHipRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        private const int SortingOrder = 7;   // 캐릭터 획(0~5) 앞, 그라피티(9) 뒤.

        private static readonly Color TrackColor = new Color(0.42f, 0.46f, 0.52f, 0.55f);
        private static readonly Color CalmArcColor = new Color(0.36f, 0.72f, 0.62f, 1f);
        private static readonly Color GlanceColor = new Color(0.86f, 0.76f, 0.32f, 1f);
        private static readonly Color TapColor = new Color(0.94f, 0.46f, 0.30f, 1f);

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터/감시자. <b>같은 GameObject의 컴포넌트만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 폴백을 두었을 때 사본 발밑에도 링이
        /// 한 벌 더 생긴다(2026-08-29 격파 미니게임에서 실측 확인된 버그와 같은 함정 —
        /// 그 기능은 2026-09-02에 삭제됐지만 함정은 모든 렌더러에 그대로 남아 있다).
        /// </summary>
        private StickmanAgent _agent;
        private FocusWatchDirector _director;
        private Material _lineMaterial;

        /// <summary>몸통 Transform — <b>회전만</b> 읽는다(<see cref="ResolveBodyRotation"/>).
        /// Interaction/CharacterAccessoryRenderer.cs와 같은 규약: 각도를 여기서 새로 계산하지 않는다.</summary>
        private Transform _torsoTransform;

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

        /// <summary>타이머 링의 반지름(월드 유닛).</summary>
        public float RingRadius => Height * RingRadiusRatio;

        /// <summary>링 중심의 로컬 Y(발바닥 기준) — 18절 "캐릭터 발밑".</summary>
        public float RingCenterLocalY => Height * RingCenterYRatio;

        /// <summary>곁눈질 호의 로컬 Y(발바닥 기준) — 머리 실측(중심 + 반경 비율)에 붙는다.</summary>
        public float GlanceLocalY
        {
            get
            {
                StickmanMetrics m = Metrics;
                float headCenter = m != null ? m.HeadCenterLocalY : Height * BaselineHeadCenterRatio;
                float headRadius = m != null ? m.HeadRadius : Height * BaselineHeadRadiusRatio;
                return headCenter + headRadius * GlanceAboveHeadCenterRatio;
            }
        }

        /// <summary>획 두께(월드 유닛).</summary>
        public float StrokeWidth => Height * StrokeWidthRatio;

        private float GlanceX => Height * GlanceXRatio;
        private float TapShakeAmplitude => Height * TapShakeAmplitudeRatio;

        private GameObject _container;

        /// <summary>
        /// ★ 2026-09-01 — <b>곁눈질 호만</b> 담는 자식(교차 레이어 항목 #22).
        ///
        /// <para>이 렌더러는 두 종류를 한 컨테이너에 그린다: 발밑 <b>타이머 링</b>(+ 두드림 자국)과
        /// 머리 옆 <b>곁눈질 호</b>. 상체가 기울 때 따라가야 하는 것은 <b>곁눈질뿐</b>이다 —
        /// 링은 18절이 "캐릭터 발밑, 앱 소유 UI"로 지정한 위젯이고, 회전 중심(엉덩이)보다 아래에 있어
        /// 함께 돌리면 발밑 링이 몸을 따라 <b>비스듬히 눕는다</b>. 그래서 컨테이너 전체를 돌리는
        /// 액세서리 방식 대신, 머리에 붙는 것만 담는 자식을 하나 두고 <b>거기만</b> 돌린다
        /// (CharacterAccessoryRenderer의 _headGroup과 같은 이유의 같은 구조).</para>
        /// </summary>
        private Transform _glanceGroup;

        private LineRenderer _arc;
        private readonly List<LineRenderer> _glanceLines = new List<LineRenderer>(2);
        private readonly List<LineRenderer> _tapLines = new List<LineRenderer>(TapMarkCount);

        private FocusWatchTier _tier = FocusWatchTier.None;
        private float _tierTimer;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 타이머 링이 떠 있는지(= 집중 세션이 진행 중인지).</summary>
        public bool IsRingVisible => _container != null;

        /// <summary>지금 표현 중인 경고 단계.</summary>
        public FocusWatchTier CurrentTier => _tier;

        /// <summary>이 연출이 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 연출이 만든 콜라이더 수 — 항상 0이어야 한다(관전 전용, 클릭관통 유지).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        /// <summary>남은 시간 비율(1=막 시작, 0=만료). 링 진행 호가 그리는 값 그 자체.</summary>
        public float RemainingRatio
        {
            get
            {
                if (_director == null || !_director.IsSessionActive) return 0f;
                float total = Mathf.Max(1f, _director.SessionDurationSeconds);
                return Mathf.Clamp01(_director.RemainingSeconds / total);
            }
        }

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _director = GetComponent<FocusWatchDirector>();
            _torsoTransform = FindDirectChild("Torso");
        }

        private Transform FindDirectChild(string childName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform t = transform.GetChild(i);
                if (t != null && t.name == childName) return t;
            }
            return null;
        }

        private void OnEnable() => StickmanEventBus.FocusWatchTierChanged += OnTierChanged;

        private void OnDisable()
        {
            StickmanEventBus.FocusWatchTierChanged -= OnTierChanged;
            // 이 컴포넌트가 꺼질 때 링이 화면에 영구히 남지 않게 한다(다른 렌더러들과 같은 정리 관례).
            Teardown();
        }

        private void OnTierChanged(FocusWatchTier tier)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            // 1단계는 "계속 재알림해도 무방"(18절)이라 같은 값이 반복해서 올 수 있다 — 그때 도형을
            // 다시 만들면 매 관찰 주기마다 깜빡이므로, 값이 실제로 바뀐 순간에만 재구성한다.
            if (tier == _tier) return;

            FocusWatchTier previous = _tier;
            _tier = tier;
            _tierTimer = 0f;

            if (_container != null) RebuildTierVisuals();

            Debug.Log($"[포모도로] 경고 단계 {TierLabel(previous)} -> {TierLabel(tier)}" +
                (tier == FocusWatchTier.None ? " (신호가 정상 범위로 돌아와 즉시 리셋 — 18절 '지금 상태'만 본다)." :
                 tier == FocusWatchTier.Glance ? " (1단계 곁눈질 — 대사 없음, 가장 약한 앰비언트 신호)." :
                 tier == FocusWatchTier.Nudge ? " (2단계 — 대사는 FocusNudge 상태 전이에서 파생되어 말풍선이 그린다)." :
                                                " (3단계 — 타이머 링을 통통 두드린다. 흔들리는 것은 링뿐, 실제 창은 1픽셀도 안 흔든다)."));
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            bool wantRing = _director != null && _director.IsSessionActive && _agent != null;

            if (wantRing && _container == null) BuildRing();
            if (!wantRing && _container != null)
            {
                Debug.Log("[포모도로] 세션이 끝나 타이머 링을 걷습니다(정상 만료 / 중도 취소 / 긴급정지).");
                Teardown();
                return;
            }
            if (_container == null) return;

            float dt = Time.deltaTime;
            _tierTimer += dt;

            Vector3 center = RingWorldPosition();
            if (_tier == FocusWatchTier.WindowTap)
            {
                // 3단계 "살짝 화면 흔들림" — **이 오버레이만** 떤다(캐릭터 레이어 국소 효과, 18절).
                // 실제 창/화면을 흔드는 코드는 이 경로에 존재하지 않는다.
                float shake = Mathf.Sin(_tierTimer * TapShakeSpeed);
                float amplitude = TapShakeAmplitude; // 전신 높이 비율 — 캐릭터가 절반이면 떨림 폭도 절반.
                center.x += shake * amplitude;
                center.y += Mathf.Cos(_tierTimer * TapShakeSpeed * 1.3f) * amplitude * 0.6f;
            }
            _container.transform.position = center;
            ApplyGlanceGroupLean();

            UpdateArc();
            UpdateTierAnimation();
        }

        private void BuildRing()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[포모도로] 타이머 링을 그리지 못했습니다 — 캐릭터 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("FocusWatchRing");
            _container.transform.SetParent(null, false);
            _container.transform.position = RingWorldPosition();

            var glance = new GameObject("GlanceGroup");
            glance.transform.SetParent(_container.transform, false);
            _glanceGroup = glance.transform;

            CreateLine("RingTrack", BuildCircle(Vector3.zero, RingRadius, RingSegments),
                TrackColor, StrokeWidth * 0.8f, loop: true);
            _arc = CreateLine("RingProgress", new[] { Vector3.zero, Vector3.zero }, CalmArcColor, StrokeWidth, loop: false);

            RebuildTierVisuals();
            ApplyGlanceGroupLean();

            Debug.Log($"[포모도로] 타이머 링 생성 — 캐릭터 발밑(y+{RingCenterLocalY:F2}) 반지름 {RingRadius:F2}유닛" +
                $"(전신 {Height:F2}유닛 기준 비율), " +
                $"남은 시간 {_director.RemainingSeconds:F0}초 / 총 {_director.SessionDurationSeconds:F0}초, " +
                $"시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0).");
        }

        /// <summary>남은 시간을 12시 방향에서 시계 방향으로 줄어드는 호로 그린다.</summary>
        private void UpdateArc()
        {
            if (_arc == null) return;

            float ratio = RemainingRatio;
            int segments = Mathf.Max(2, Mathf.CeilToInt(RingSegments * ratio));
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * ratio;
                float angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f; // 12시에서 시계 방향.
                pts[i] = new Vector3(Mathf.Cos(angle) * RingRadius, Mathf.Sin(angle) * RingRadius, 0f);
            }
            _arc.positionCount = pts.Length;
            _arc.SetPositions(pts);

            Color c = _tier == FocusWatchTier.WindowTap ? TapColor
                : _tier == FocusWatchTier.None ? CalmArcColor
                : GlanceColor;
            _arc.startColor = c;
            _arc.endColor = c;
        }

        private void RebuildTierVisuals()
        {
            for (int i = 0; i < _glanceLines.Count; i++) if (_glanceLines[i] != null) Destroy(_glanceLines[i].gameObject);
            for (int i = 0; i < _tapLines.Count; i++) if (_tapLines[i] != null) Destroy(_tapLines[i].gameObject);
            _glanceLines.Clear();
            _tapLines.Clear();
            if (_container == null) return;

            bool wantGlance = _tier == FocusWatchTier.Glance || _tier == FocusWatchTier.Nudge || _tier == FocusWatchTier.WindowTap;
            if (wantGlance)
            {
                // 머리 옆 곁눈질 호 2개("말없이 곁눈질하는 표정 변화만" — 18절 1단계).
                // 링 컨테이너 기준 좌표라 y는 링 중심에서 머리 옆까지의 상대 높이다.
                float relY = GlanceLocalY - RingCenterLocalY;
                float glanceX = GlanceX;
                float bulgeX = Height * GlanceArcBulgeXRatio;
                float bulgeY = Height * GlanceArcBulgeYRatio;
                for (int side = -1; side <= 1; side += 2)
                {
                    var pts = new Vector3[5];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float t = i / (float)(pts.Length - 1);
                        float a = Mathf.Lerp(-50f, 50f, t) * Mathf.Deg2Rad;
                        pts[i] = new Vector3(glanceX * side + Mathf.Sin(a) * bulgeX * side,
                            relY + Mathf.Cos(a) * bulgeY - bulgeY, 0f);
                    }
                    _glanceLines.Add(CreateLine(side < 0 ? "GlanceL" : "GlanceR", pts, GlanceColor,
                        StrokeWidth * 0.8f, loop: false, parent: _glanceGroup));
                }
            }

            if (_tier == FocusWatchTier.WindowTap)
            {
                // 링을 두드린 자국 — 링 바깥 위쪽에 짧은 선 3개.
                float ringRadius = RingRadius;
                float innerR = ringRadius + Height * TapMarkInnerGapRatio;
                float outerR = ringRadius + Height * TapMarkOuterGapRatio;
                for (int i = 0; i < TapMarkCount; i++)
                {
                    float angle = (60f + i * 30f) * Mathf.Deg2Rad;
                    var inner = new Vector3(Mathf.Cos(angle) * innerR, Mathf.Sin(angle) * innerR, 0f);
                    var outer = new Vector3(Mathf.Cos(angle) * outerR, Mathf.Sin(angle) * outerR, 0f);
                    _tapLines.Add(CreateLine($"TapMark{i}", new[] { inner, outer }, TapColor, StrokeWidth, loop: false));
                }
            }
        }

        private void UpdateTierAnimation()
        {
            if (_glanceLines.Count > 0)
            {
                // 1단계는 아주 느리게 깜빡인다("거슬리지 않아야 한다" — 18절).
                float speed = _tier == FocusWatchTier.Glance ? 1.5f : 2.6f;
                float alpha = 0.30f + 0.55f * (0.5f + 0.5f * Mathf.Sin(_tierTimer * speed));
                for (int i = 0; i < _glanceLines.Count; i++) ApplyAlpha(_glanceLines[i], alpha);
            }

            if (_tapLines.Count > 0)
            {
                float alpha = Mathf.Abs(Mathf.Sin(_tierTimer * TapShakeSpeed * 0.5f));
                for (int i = 0; i < _tapLines.Count; i++) ApplyAlpha(_tapLines[i], 0.25f + 0.75f * alpha);
            }
        }

        /// <summary>
        /// ★ 2026-09-01 — 곁눈질 호를 <b>엉덩이 피벗으로 상체와 같은 각도만큼</b> 돌린다
        /// (교차 레이어 항목 #22, 참고 패턴은 Interaction/CharacterAccessoryRenderer.cs 클래스 문서 3-2).
        ///
        /// <para>좌표는 <b>컨테이너(= 링 중심) 로컬</b>이므로 엉덩이도 그 기준으로 환산한다:
        /// <c>hip = HipLocalY − RingCenterLocalY</c>. 자식의 변환이 <c>위치 + R·p</c>이므로
        /// <c>위치 = hip − R·hip</c>이면 점 p가 <c>hip + R·(p − hip)</c>로 간다(= 피벗 회전 그 자체).</para>
        ///
        /// <para>기울임이 0이면 회전이 identity이고 위치가 정확히 0이라 <b>예전과 완전히 같은 그림</b>이다.
        /// 링과 두드림 자국은 이 그룹 밖에 있으므로 <b>영향을 받지 않는다</b>(18절의 발밑 위젯 유지).</para>
        /// </summary>
        private void ApplyGlanceGroupLean()
        {
            if (_glanceGroup == null) return;

            Quaternion rot = ResolveBodyRotation();
            if (rot == Quaternion.identity)
            {
                _glanceGroup.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }

            var hip = new Vector3(0f, HipLocalY - RingCenterLocalY, 0f);
            _glanceGroup.SetLocalPositionAndRotation(hip - rot * hip, rot);
        }

        /// <summary>지금 상체가 기운 각도. <b>계산하지 않고 읽는다</b> — 포즈가 Torso에 실제로 적용한
        /// 회전이 유일한 진실이므로, 이 렌더러가 각도 유도식을 한 벌 더 갖지 않는다.</summary>
        private Quaternion ResolveBodyRotation()
            => _torsoTransform != null ? _torsoTransform.localRotation : Quaternion.identity;

        /// <summary>고관절의 로컬 Y(발바닥 기준) — 기울임의 회전 중심. 실측을 못 구하면 비율 폴백.</summary>
        private float HipLocalY
        {
            get
            {
                StickmanMetrics m = Metrics;
                return m != null && m.HipLocalY > 0.0001f ? m.HipLocalY : Height * BaselineHipRatio;
            }
        }

        private Vector3 RingWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 body = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;
            Vector3 target = new Vector3(body.x, body.y + RingCenterLocalY, 0f);

            // 링이 화면 밖으로 잘려 나가지 않게 뷰포트 안으로 클램프한다 — 캐릭터는 창 상단 테두리에
            // 서 있는 시간이 길고 화면 최하단 안전망에 서 있을 때도 있어서, 링 반지름만큼은 반드시
            // 여유를 둬야 한다(HardwareReactionRenderer.FollowHead()에서 실측으로 배운 교훈).
            // 여유가 RingRadius 배수라 비율화의 혜택을 저절로 받는다 — 절대 유닛으로 적혀 있었다면
            // 배율 0.5에서 캐릭터 한 키 가까이를 화면 안쪽으로 끌어당겨 링만 몸에서 떨어져 나갔을 것이다.
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                float margin = RingRadius * 1.25f;
                Vector3 camPos = cam.transform.position;
                target.x = Mathf.Clamp(target.x, camPos.x - halfW + margin, camPos.x + halfW - margin);
                target.y = Mathf.Clamp(target.y, camPos.y - halfH + margin, camPos.y + halfH - margin);
            }
            return target;
        }

        // ==================== 종료 ====================

        private void Teardown()
        {
            _glanceLines.Clear();
            _tapLines.Clear();
            _arc = null;
            _glanceGroup = null;   // 컨테이너의 자식이라 아래 Destroy로 함께 사라진다.
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
        }

        // ==================== 도형 유틸 ====================

        private static void ApplyAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null) return;
            Color c = lr.startColor;
            c.a = alpha;
            lr.startColor = c;
            lr.endColor = c;
        }

        private LineRenderer CreateLine(string name, Vector3[] points, Color color, float width, bool loop,
            Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
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

        public static string TierLabel(FocusWatchTier tier)
        {
            switch (tier)
            {
                case FocusWatchTier.Glance: return "1단계(곁눈질)";
                case FocusWatchTier.Nudge: return "2단계(리마인드)";
                case FocusWatchTier.WindowTap: return "3단계(창 두드림)";
                default: return "정상";
            }
        }

        /// <summary>다른 렌더러들과 같은 이유로 캐릭터 LineRenderer의 머티리얼을 빌려 쓴다
        /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
