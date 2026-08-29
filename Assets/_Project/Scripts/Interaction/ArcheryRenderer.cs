using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 활쏘기 시각 레이어 — 사용자 요청 "과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동"에서
    /// <b>유저가 실제로 보는 것 전부</b>를 그린다(과녁 등장 / 활 / 시위 당김 / 포물선을 그리는 화살 /
    /// 꽂힘). 트리거·과녁 자리 선정은 Interaction/ArcheryDirector.cs, 3발의 타이밍과 캐릭터 자세는
    /// States/ArcheryState.cs가 담당하고, 이 클래스는 그 둘이 발행한 이벤트만 소비한다.
    ///
    /// ============================================================================
    /// 관례는 BattleMinigameRenderer / WindowTheftRenderer를 그대로 따른다
    /// ============================================================================
    /// · 전역 이벤트 구독 -> LineRenderer로만 그림(이 프로젝트에는 스프라이트 에셋이 하나도 없다).
    /// · 머티리얼은 <b>캐릭터 LineRenderer의 것을 빌려 쓴다</b>. Shader.Find는 빌드 스트리핑 위험이
    ///   있어 쓰지 않는다(스탠드얼론에서만 분홍색으로 깨질 수 있다).
    /// · 씬 전체 탐색 폴백 없이 <b>같은 GameObject의 StickmanAgent만</b> 쓴다 — 라이벌 스틱맨은
    ///   플레이어 프리팹의 복제본이라, 씬 폴백을 두면 라이벌 렌더러가 전역 이벤트에 함께 반응해
    ///   과녁이 두 벌 그려진다(격파 미니게임에서 실측 확인된 버그).
    /// · 종료 시 <see cref="Teardown"/>, <see cref="OnDisable"/>에서도 정리.
    ///
    /// ============================================================================
    /// 콜라이더를 단 하나도 만들지 않는다 (절대 원칙 3 / 비침해 원칙 2)
    /// ============================================================================
    /// 과녁은 클릭할 필요가 없는 순수 관전 오브젝트다. 이 프로젝트에서 클릭관통이 풀리는 유일한 경로가
    /// 콜라이더 존재이므로(UniWindowController의 Raycast 히트테스트), 콜라이더를 아예 만들지 않는 것이
    /// 곧 "과녁이 떠 있는 동안에도 그 자리의 다른 앱은 평소처럼 클릭된다"는 보증이다.
    /// WindowCrashRenderer와 같은 계약이며 테스트가 <see cref="ActiveColliderCount"/>==0으로 잠근다.
    ///
    /// ============================================================================
    /// 포물선은 시뮬레이션이 아니라 <b>역산</b>이다 (리더 지시)
    /// ============================================================================
    /// 화살에 Rigidbody2D를 달아 힘을 주고 어디에 맞는지 지켜보는 방식이 아니다. ArcheryState가 미리
    /// 확정한 도달점과 비행 시간이 주어지면 초기 속도는 <b>유일하게 결정</b>되므로
    /// (<see cref="SolveLaunchVelocity"/>), 화살은 항상 정확히 그 점에 도착한다. 포물선의 볼록함은
    /// 중력 상수를 튜닝하는 대신 "현 높이 대비 얼마나 부풀 것인가"(<see cref="ArcApexHeight"/>)에서
    /// 역으로 유도한다 — 그래서 캐릭터 배율이 바뀌어도 궤적의 <b>모양</b>이 그대로 유지된다.
    ///
    /// ============================================================================
    /// 치수는 전부 캐릭터 신장 대비 비율이다
    /// ============================================================================
    /// 기준값의 유일한 조회 경로는 Core/StickmanMetrics.cs다(<see cref="Height"/>). 각도(조준각 등)만
    /// 절대값인데, 각도는 크기와 무관한 양이기 때문이다(리더 지시).
    /// </summary>
    public sealed class ArcheryRenderer : MonoBehaviour
    {
        // ==================== 치수 비율(캐릭터 전신 높이 대비) ====================
        // 과녁의 반지름/거리는 StickConfig에서 오고(연출 튜닝 대상), 아래는 그림의 형태를 이루는 값이라
        // 렌더러 상수로 둔다(보행 키프레임 표를 StickmanPoseAnimator 상수로 둔 것과 같은 판단 기준).

        private const float StrokeWidthRatio = 0.0339f;   // 캐릭터 몸통 획과 같은 굵기(0.077 / 2.2747).
        private const float StandSpreadRatio = 0.11f;     // 과녁 받침 다리가 바닥에서 좌우로 벌어지는 폭.
        private const float BowHalfLengthRatio = 0.30f;   // 활 상하 절반 길이(활 전체 = 신장의 60%).
        private const float BowDepthRatio = 0.155f;       // 활대가 앞으로 휘어진 깊이(깊을수록 '활'로 읽힌다).
        private const float BowMaxPullRatio = 0.21f;      // 시위를 최대로 당겼을 때 뒤로 물러나는 거리.
        private const float ArrowShaftRatio = 0.34f;      // 화살대 길이.
        private const float ArrowHeadRatio = 0.055f;      // 화살촉 날개 길이.
        private const float ArrowFletchRatio = 0.05f;     // 오늬깃 길이.
        private const float ImpactBurstRatio = 0.11f;     // 꽂힐 때 튀는 짧은 선의 길이.
        private const float DustRadiusRatio = 0.07f;      // 빗나간 화살이 땅에 꽂힐 때의 흙먼지 크기.

        // 링 반지름 비율(바깥 링 반지름 R 대비) — 3링 + 중앙점의 고전적 과녁.
        private const float Ring2Ratio = 0.68f;
        private const float Ring3Ratio = 0.36f;
        private const float BullRatio = 0.16f;

        private const int CircleSegments = 28;            // 원 근사 분할 수(작게 그려지므로 이 정도면 충분).
        private const int ImpactRayCount = 6;
        private const int DustPuffCount = 4;

        // ==================== 타이밍 ====================
        // 등장/퇴장 시간은 ArcheryState와 **같은 StickConfig 값**을 읽는다 — 두 곳이 각자 시간을 세면
        // 반드시 어긋난다(이 프로젝트가 Dock 구간/화면 클램프에서 이미 두 번 겪은 실패 유형).

        private const float StickWobbleSeconds = 0.30f;   // 꽂힌 직후 화살대가 부르르 떠는 시간.
        private const float StickWobbleDegrees = 7f;      // 그 떨림의 진폭(각도는 크기와 무관 -> 절대값).
        private const float ImpactBurstSeconds = 0.22f;
        private const float DustSeconds = 0.45f;

        // ==================== 색 ====================
        // 고전적 과녁 배색(흰/빨강). 캐릭터 잉크색과 달리 "과녁이라는 사물의 색"이라 렌더러가 소유한다.

        private static readonly Color FaceWhite = new Color(1f, 1f, 1f, 1f);
        private static readonly Color FaceRed = new Color(0.87f, 0.19f, 0.17f, 1f);

        // 캐릭터 획(0~5)보다 확실히 위. 아래에서 위로 쌓이는 순서가 곧 과녁 링의 순서다.
        private const int SortingFaceOuter = 10;
        private const int SortingFaceMid = 11;
        private const int SortingFaceInner = 12;
        private const int SortingBull = 13;
        private const int SortingOutline = 14;
        private const int SortingStand = 15;
        private const int SortingStuckArrow = 16;
        private const int SortingBow = 17;
        private const int SortingBurst = 18;

        private enum Mode { None, Playing, Outro }

        private sealed class Arrow
        {
            public Transform Root;
            public LineRenderer Line;
            public Vector2 Origin;       // 컨테이너 로컬 발사 지점.
            public Vector2 LaunchVel;    // 컨테이너 로컬 초기 속도.
            public float Gravity;
            public float Flight;
            public float Elapsed;
            public bool Stuck;
            public float StuckAge;
            public float StuckAngle;
            /// <summary>이 화살이 **꽂힐 때** 취할 각도(도). 발사 시점에 이미 확정된다 —
            /// 궤적이 역산이라 착탄 접선도 역산이 가능하고, 그래야 마지막 구간에서 접선 각도로부터
            /// 이 값으로 부드럽게 눕히는 보간의 목표점이 프레임레이트와 무관하게 고정된다.</summary>
            public float SettledAngle;
            /// <summary>각도 보정을 시작하는 시각(초). Flight * (1 - settleRatio).</summary>
            public float SettleStart;
            /// <summary>이 화살의 사전 확정 결과. **화살마다** 들고 있어야 한다 — 비행 시간(0.62초)이
            /// 다음 발의 조준 시작(발사 후 0.34초)보다 길어서, 도달 시점에는 렌더러의 "현재 계획"이
            /// 이미 다음 발로 넘어가 있다. 여기 스냅샷하지 않으면 빗나감 흙먼지가 엉뚱한 발에 붙는다.</summary>
            public ArcheryShotResult Result;
        }

        private sealed class Puff
        {
            public Transform Root;
            public LineRenderer Line;
            public Vector2 Velocity;
            public float Age;
        }

        /// <summary>이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓴다(클래스 문서 참고).</summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private StickmanMetrics _metrics;
        private bool _metricsResolved;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _elapsed;

        private GameObject _container;
        private Transform _targetRoot;
        private Transform _bowRoot;
        private LineRenderer _bowLimbs;
        private LineRenderer _bowString;
        private LineRenderer _nockedArrow;
        private LineRenderer _impactBurst;
        private Transform _impactBurstRoot;

        private readonly List<Arrow> _arrows = new List<Arrow>(4);
        private readonly List<Puff> _puffs = new List<Puff>(DustPuffCount);
        private readonly List<LineRenderer> _targetLines = new List<LineRenderer>(8);

        private Vector2 _anchorWorld;   // 컨테이너 원점 = 과녁 발밑(지면).
        private float _facing = 1f;

        // 현재 겨누고 있는 발의 계획(ArcheryShotChanged(Aim)에서 갱신).
        private bool _hasPlan;
        private Vector2 _planImpactLocal;
        private float _planFlight = 0.6f;
        private ArcheryShotResult _planResult;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 화면에 활쏘기 시각 요소가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        /// <summary>이 연출이 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 연출이 만든 콜라이더 수 — <b>항상 0</b>이어야 한다(관전 전용, 클릭관통 유지).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        /// <summary>지금 날아가는 중인 화살 수.</summary>
        public int FlyingArrowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _arrows.Count; i++) if (_arrows[i] != null && !_arrows[i].Stuck) n++;
                return n;
            }
        }

        /// <summary>지금까지 꽂힌 화살 수(3발이 끝나면 3이 된다).</summary>
        public int StuckArrowCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _arrows.Count; i++) if (_arrows[i] != null && _arrows[i].Stuck) n++;
                return n;
            }
        }

        /// <summary>이번 사이클에 실제로 스폰된 화살 총 수(날아가는 중 + 꽂힘).</summary>
        public int SpawnedArrowCount => _arrows.Count;

        /// <summary>
        /// 꽂힌 화살 하나의 <b>모양</b>을 테스트가 실측하는 창구(2026-08-29 사용자 신고
        /// "화살이 과녁에 좀 이상하게 꽂힘 / 다 외곽에 꽂히는거 같음" 회귀 잠금).
        /// </summary>
        /// <param name="descentDegrees">수평 대비 하강각(도, + = 코가 아래). 과녁 면은 완만해야 한다.</param>
        /// <param name="tipOvershootLocal">화살 폴리라인이 <b>도달점보다 진행 방향으로 더 나간</b> 거리
        /// (화살 로컬 유닛). 촉이 도달점에 꽂히는 것이 정상이므로 <b>0이어야 한다</b> — 양수면
        /// 화살이 과녁을 관통해 반대편으로 삐져나온 그림이 된다.</param>
        public bool TryGetStuckArrow(int index, out ArcheryShotResult result,
            out float descentDegrees, out float tipOvershootLocal)
        {
            result = ArcheryShotResult.Miss;
            descentDegrees = 0f;
            tipOvershootLocal = 0f;
            if (index < 0 || index >= _arrows.Count) return false;
            Arrow a = _arrows[index];
            if (a == null || !a.Stuck || a.Line == null) return false;

            result = a.Result;
            descentDegrees = DescentDegrees(a.StuckAngle);

            float maxX = float.NegativeInfinity;
            for (int i = 0; i < a.Line.positionCount; i++) maxX = Mathf.Max(maxX, a.Line.GetPosition(i).x);
            tipOvershootLocal = maxX;
            return true;
        }

        // ==================== 캐릭터 실측 치수 ====================

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

        /// <summary>이 캐릭터의 전신 높이(월드 유닛) — 모든 비율의 유일한 기준값.</summary>
        private float Height
        {
            get
            {
                StickmanMetrics m = Metrics;
                return m != null ? m.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
            }
        }

        private StickConfig Config => _agent != null ? _agent.Config : null;

        /// <summary>과녁 바깥 링 반지름(월드 유닛).</summary>
        public float TargetRadius => Height * (Config != null
            ? Mathf.Clamp(Config.archeryTargetRadiusRatio, 0.05f, 0.9f) : 0.40f);

        /// <summary>과녁 중심의 로컬 Y(지면 기준). ArcheryDirector와 <b>같은 식</b>에서 나온다.</summary>
        public float TargetCenterLocalY => ArcheryDirector.TargetCenterHeight(Height, TargetRadius);

        public float StrokeWidth => Height * StrokeWidthRatio;
        public float BowHalfLength => Height * BowHalfLengthRatio;
        public float BowMaxPull => Height * BowMaxPullRatio;
        public float ArrowShaftLength => Height * ArrowShaftRatio;

        /// <summary>과녁 면에 꽂힌 화살이 수평에서 아래로 기울 수 있는 최대 각도(도).</summary>
        public float FaceImpactMaxDescentDegrees => Config != null
            ? Mathf.Clamp(Config.archeryFaceImpactMaxDescentDegrees, 0f, 60f) : 14f;

        /// <summary>땅에 꽂힌 화살(빗나감)이 지면과 이루는 확정 각도(도).</summary>
        public float GroundImpactDescentDegrees => Config != null
            ? Mathf.Clamp(Config.archeryGroundImpactDescentDegrees, 5f, 80f) : 38f;

        /// <summary>착탄 각도 보정 구간 — 비행 시간의 마지막 몇 할(0~0.6).</summary>
        public float ImpactSettleRatio => Config != null
            ? Mathf.Clamp(Config.archeryImpactSettleRatio, 0f, 0.6f) : 0.22f;

        /// <summary>포물선 볼록함의 <b>하한</b>(신장 비례). 아주 가까운 사격에서도 궤적이 직선처럼
        /// 납작해지지 않게 받쳐준다.</summary>
        public float ArcApexHeight => Height * (Config != null
            ? Mathf.Max(0.02f, Config.archeryArrowArcApexRatio) : 0.38f);

        /// <summary>
        /// 이번 사격의 실제 볼록함(월드 유닛). ★ 2026-08-29 사용자 신고 "화살이 곡선으로 멀리
        /// 날라가야하는데" 대응 — 신장에만 비례하던 고정 볼록함은 사거리를 늘리는 순간 상대적으로
        /// 납작해져 직선처럼 보였다. 이제 <b>수평 사거리에 비례</b>시키고(멀리 쏠수록 더 높이 뜬다),
        /// 짧은 사격을 위한 하한(<see cref="ArcApexHeight"/>)과 화면 위 상한을 함께 건다.
        ///
        /// 상한이 필요한 이유: 이 앱의 발판은 다른 창의 상단 테두리라 캐릭터가 화면 맨 위에 서 있을
        /// 수 있다. 그때 볼록함을 그대로 쓰면 궤적의 정점이 화면 밖으로 잘려 "곡선"이 아니라 "위로
        /// 사라졌다 나타나는 선"이 된다(가로 배치에서 미러링/포기를 두는 것과 같은 판단 기준).
        /// </summary>
        private float ResolveApex(Vector2 fromLocal, Vector2 toLocal)
        {
            float span = Mathf.Abs(toLocal.x - fromLocal.x);
            float ratio = Config != null ? Mathf.Max(0f, Config.archeryArrowArcApexDistanceRatio) : 0.24f;
            float apex = Mathf.Max(ArcApexHeight, span * ratio);

            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam != null && cam.orthographic)
            {
                float chordMidWorldY = _anchorWorld.y + (fromLocal.y + toLocal.y) * 0.5f;
                float headroom = (cam.transform.position.y + cam.orthographicSize - ScreenEdgePadWorld) - chordMidWorldY;
                apex = Mathf.Min(apex, Mathf.Max(Height * 0.12f, headroom));
            }
            return apex;
        }

        /// <summary>궤적 정점이 화면 위로 잘리지 않게 남겨두는 여백(월드 유닛) — 다른 배치 계산과 같은 값.</summary>
        private const float ScreenEdgePadWorld = 0.10f;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable()
        {
            StickmanEventBus.ArcheryOverlayChanged += OnOverlayChanged;
            StickmanEventBus.ArcheryShotChanged += OnShotChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.ArcheryOverlayChanged -= OnOverlayChanged;
            StickmanEventBus.ArcheryShotChanged -= OnShotChanged;
            // 이 컴포넌트가 꺼지거나 파괴될 때 과녁이 화면에 영구히 남지 않게 한다(다른 렌더러들과 같은 관례).
            Teardown();
        }

        // ==================== 이벤트 ====================

        private void OnOverlayChanged(ArcheryOverlayEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본(라이벌) — 전역 이벤트를 받아도 무시한다.

            switch (evt.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    Spawn(evt);
                    break;
                case SpectacleOverlayPhase.Completed:
                case SpectacleOverlayPhase.Cancelled:
                    if (_mode == Mode.Playing) BeginMode(Mode.Outro);
                    break;
            }
        }

        private void OnShotChanged(ArcheryShotEvent evt)
        {
            if (_agent == null || _mode != Mode.Playing || _container == null) return;

            _planImpactLocal = evt.ImpactWorld - _anchorWorld;
            _planFlight = Mathf.Max(0.05f, evt.FlightSeconds);
            _planResult = evt.Result;
            _hasPlan = true;

            if (evt.Phase == ArcheryShotPhase.Release) FireArrow();
        }

        private void BeginMode(Mode mode)
        {
            _mode = mode;
            _modeTimer = 0f;
        }

        // ==================== 소환 ====================

        private void Spawn(ArcheryOverlayEvent evt)
        {
            Teardown(); // 이전 사이클 잔재를 먼저 정리(멱등).

            var blackboard = _agent.Blackboard;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[활쏘기] 시각 요소를 만들지 못했습니다 — StickmanAgent/Body 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _facing = evt.Facing >= 0f ? 1f : -1f;
            _anchorWorld = new Vector2(evt.TargetWorld.x, evt.GroundWorldY);

            _container = new GameObject("ArcheryVisuals");
            _container.transform.SetParent(null, false);
            // 캐릭터의 자식으로 붙이지 않는 이유: 과녁은 "땅에 세워둔 물건"이라 캐릭터가 움직여도
            // 제자리에 있어야 한다(BattleMinigameRenderer의 판자와 같은 판단).
            _container.transform.position = new Vector3(_anchorWorld.x, _anchorWorld.y, 0f);

            Color ink = ResolveInk();
            BuildTarget(ink);
            BuildBow(ink);

            _elapsed = 0f;
            _hasPlan = false;
            BeginMode(Mode.Playing);

            Debug.Log($"[활쏘기] 과녁 소환 — 중심 로컬y={TargetCenterLocalY:F2}, 반지름={TargetRadius:F2}유닛 " +
                $"(신장 {Height:F2} 기준, 과녁 꼭대기가 정확히 정수리 높이). 콜라이더 0개 — 과녁이 떠 있는 " +
                "동안에도 그 자리의 다른 앱은 평소처럼 클릭됩니다.");
        }

        private void BuildTarget(Color ink)
        {
            _targetRoot = new GameObject("Target").transform;
            _targetRoot.SetParent(_container.transform, false);
            _targetRoot.localPosition = new Vector3(0f, TargetCenterLocalY, 0f);

            float r = TargetRadius;

            // "채워진 원"은 별도 메시/스프라이트 없이 만든다: 반지름 r/2인 원 경로를 두께 r로 그으면
            // 스트로크가 [0, r] 구간을 덮어 결과적으로 반지름 r의 원판이 된다(굵은 선분으로 사각형을
            // 만드는 BattleMinigameRenderer의 트릭과 같은 발상 — 새 에셋/셰이더를 도입하지 않는다).
            AddTargetLine(CreateDisk(_targetRoot, "FaceOuter", FaceWhite, r, SortingFaceOuter));
            AddTargetLine(CreateDisk(_targetRoot, "FaceMid", FaceRed, r * Ring2Ratio, SortingFaceMid));
            AddTargetLine(CreateDisk(_targetRoot, "FaceInner", FaceWhite, r * Ring3Ratio, SortingFaceInner));
            AddTargetLine(CreateDisk(_targetRoot, "Bull", FaceRed, r * BullRatio, SortingBull));

            AddTargetLine(CreateRing(_targetRoot, "RingOuter", ink, r, SortingOutline));
            AddTargetLine(CreateRing(_targetRoot, "RingMid", ink, r * Ring2Ratio, SortingOutline));
            AddTargetLine(CreateRing(_targetRoot, "RingInner", ink, r * Ring3Ratio, SortingOutline));

            // 받침 — 과녁 아래 끝에서 지면까지 내려오는 A자 다리. 이것이 "지면"을 눈에 보이게 만들어,
            // 빗나간 화살이 땅에 꽂혔을 때 그 높이가 지면이라는 것이 읽히게 한다.
            float bottomLocalY = -r;                 // 과녁 로컬 기준 아래 끝.
            float groundLocalY = -TargetCenterLocalY; // 과녁 로컬 기준 지면.
            float spread = Height * StandSpreadRatio;

            var stand = CreateLine(_targetRoot, "Stand", ink, StrokeWidth, SortingStand, loop: false, capVertices: 2);
            stand.positionCount = 5;
            stand.SetPosition(0, new Vector3(-spread, groundLocalY, 0f));
            stand.SetPosition(1, new Vector3(0f, bottomLocalY, 0f));
            stand.SetPosition(2, new Vector3(spread, groundLocalY, 0f));
            stand.SetPosition(3, new Vector3(0f, groundLocalY + (bottomLocalY - groundLocalY) * 0.45f, 0f));
            stand.SetPosition(4, new Vector3(-spread * 0.55f, groundLocalY + (bottomLocalY - groundLocalY) * 0.45f, 0f));
            AddTargetLine(stand);
        }

        private void AddTargetLine(LineRenderer lr)
        {
            if (lr != null) _targetLines.Add(lr);
        }

        private void BuildBow(Color ink)
        {
            _bowRoot = new GameObject("Bow").transform;
            _bowRoot.SetParent(_container.transform, false);

            // 활대: 로컬 +x가 "쏘는 방향"이고 위아래로 휜다. y에 대해 대칭이라 좌우 어느 방향을 향하도록
            // 회전시켜도 모양이 뒤집혀 보이지 않는다(부호를 따로 다루지 않아도 되는 이유).
            _bowLimbs = CreateLine(_bowRoot, "Limbs", ink, StrokeWidth * 0.85f, SortingBow, loop: false, capVertices: 0);
            int n = 13;
            _bowLimbs.positionCount = n;
            float half = BowHalfLength;
            float depth = Height * BowDepthRatio;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1) * 2f - 1f; // -1 ~ +1
                _bowLimbs.SetPosition(i, new Vector3(depth * (1f - t * t), t * half, 0f));
            }

            _bowString = CreateLine(_bowRoot, "String", ink, StrokeWidth * 0.45f, SortingBow, loop: false, capVertices: 2);
            _bowString.positionCount = 3;

            _nockedArrow = CreateLine(_bowRoot, "NockedArrow", ink, StrokeWidth * 0.62f, SortingBow, loop: false, capVertices: 0);
            _nockedArrow.positionCount = 7;

            SetLineAlpha(_bowLimbs, 0f);
            SetLineAlpha(_bowString, 0f);
            SetLineAlpha(_nockedArrow, 0f);
        }

        // ==================== 매 프레임 ====================

        private void LateUpdate()
        {
            if (_mode == Mode.None) return;

            float dt = Time.deltaTime;
            _elapsed += dt;
            _modeTimer += dt;

            float alpha = 1f;
            if (_mode == Mode.Outro)
            {
                float outro = ConfigFloat(c => c.archeryOutroSeconds, 0.75f);
                float t = outro > 0.0001f ? Mathf.Clamp01(_modeTimer / outro) : 1f;
                alpha = 1f - t;
                // 과녁이 쪼그라들며 옅어진다(격파 미니게임의 "민망한 퇴장"과 같은 정리 관례).
                if (_targetRoot != null)
                {
                    float s = Mathf.Lerp(1f, 0.4f, t * t);
                    _targetRoot.localScale = new Vector3(s, s, 1f);
                }
                if (_modeTimer >= outro) { Teardown(); return; }
            }
            else
            {
                TickTargetIntro();
            }

            TickBow(dt, alpha);
            TickArrows(dt, alpha);
            TickPuffs(dt, alpha);
            TickImpactBurst(alpha);
            ApplyTargetAlpha(alpha);
        }

        /// <summary>과녁 등장 — 위에서 살짝 내려오며 통통 튀듯 커진다(overshoot). 시간은
        /// ArcheryState가 첫 발을 당기기까지 기다리는 그 값과 <b>같은 설정</b>에서 온다.</summary>
        private void TickTargetIntro()
        {
            if (_targetRoot == null) return;
            float intro = Mathf.Max(0.01f, ConfigFloat(c => c.archeryTargetIntroSeconds, 0.55f));
            float t = Mathf.Clamp01(_elapsed / intro);
            if (t >= 1f)
            {
                _targetRoot.localScale = Vector3.one;
                _targetRoot.localPosition = new Vector3(0f, TargetCenterLocalY, 0f);
                return;
            }

            // back-out easing — 1.0을 넘겼다가 되돌아오는 탄력.
            float u = 1f - t;
            const float overshoot = 1.9f;
            float e = 1f - (u * u * ((overshoot + 1f) * u - overshoot));
            float scale = Mathf.Max(0.02f, e);
            _targetRoot.localScale = new Vector3(scale, scale, 1f);
            _targetRoot.localPosition = new Vector3(0f, TargetCenterLocalY + Height * 0.25f * (1f - e), 0f);
        }

        private void ApplyTargetAlpha(float alpha)
        {
            for (int i = 0; i < _targetLines.Count; i++) SetLineAlpha(_targetLines[i], alpha);
        }

        /// <summary>
        /// 활 갱신 — 위치는 <b>실제 활 든 손</b>을 따라가고, 회전은 이번 발의 발사 방향을 향한다.
        /// 즉 조준선이 궤적과 항상 같은 소스에서 나오므로 "겨눈 방향과 화살이 날아간 방향이 다른"
        /// 어긋남이 원리적으로 생길 수 없다.
        /// </summary>
        private void TickBow(float deltaTime, float alpha)
        {
            if (_bowRoot == null) return;

            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            bool visible = _mode == Mode.Playing && _hasPlan && blackboard != null && blackboard.ArcheryBowVisible;
            if (!visible)
            {
                SetLineAlpha(_bowLimbs, 0f);
                SetLineAlpha(_bowString, 0f);
                SetLineAlpha(_nockedArrow, 0f);
                return;
            }

            Vector2 handLocal = ResolveBowHandLocal(blackboard);
            Vector2 v0 = SolveLaunchVelocity(handLocal, _planImpactLocal, _planFlight, ResolveApex(handLocal, _planImpactLocal));
            float angle = Mathf.Atan2(v0.y, v0.x) * Mathf.Rad2Deg;

            _bowRoot.localPosition = new Vector3(handLocal.x, handLocal.y, 0f);
            _bowRoot.localRotation = Quaternion.Euler(0f, 0f, angle);

            float draw01 = Mathf.Clamp01(blackboard.ArcheryDrawRatio);
            float half = BowHalfLength;
            Vector2 nock = ResolveNockLocal(blackboard, draw01);

            _bowString.SetPosition(0, new Vector3(0f, half, 0f));
            _bowString.SetPosition(1, new Vector3(nock.x, nock.y, 0f));
            _bowString.SetPosition(2, new Vector3(0f, -half, 0f));

            // 시위에 걸린 화살 — 오늬가 시위(=당겨진 지점)에 붙어 있고 촉이 앞을 향한다.
            BuildArrowPolyline(_nockedArrow, nock, ArrowShaftLength, withFletching: false);

            // 당기는 중에만 보인다(발사 후 회복 구간에는 화살이 없다).
            // 활은 "꺼내 드는" 정도(ready)로 페이드인한다 — 과녁이 등장하는 동안 함께 나타난다.
            float ready = Mathf.Clamp01(blackboard.ArcheryReadyRatio);
            float bowAlpha = alpha * ready;
            float arrowAlpha = draw01 > 0.02f ? bowAlpha : 0f;
            SetLineAlpha(_bowLimbs, bowAlpha);
            SetLineAlpha(_bowString, bowAlpha);
            SetLineAlpha(_nockedArrow, arrowAlpha);
        }

        /// <summary>
        /// 시위를 잡은 지점(활 로컬 좌표). * 고정 거리로 뒤로 당기는 것이 아니라 <b>실제 당기는 손
        /// (뒷팔 손끝)의 위치</b>를 활 로컬로 변환해 쓴다 - 그래야 시위와 손이 항상 붙어 있다.
        /// 고정 거리를 쓰면 자세 각도를 조금만 바꿔도 "손은 저기 있는데 시위는 여기서 당겨진" 어긋남이
        /// 생기고, 실제로 첫 육안 검증에서 그 어긋남이 눈에 띄었다.
        ///
        /// 두 가지 보정만 한다:
        ///   - draw01로 0(놓은 상태)과 손 위치 사이를 섞는다 - 발사 후 팔이 중립으로 돌아가는 동안
        ///     시위가 손을 따라 몸통까지 끌려가지 않는다.
        ///   - 활 앞으로 넘어가거나 활보다 위아래로 크게 벗어나지 않게 클램프한다(병리적 자세 방어).
        /// </summary>
        private Vector2 ResolveNockLocal(StickmanBlackboard blackboard, float draw01)
        {
            Vector2 pulled = new Vector2(-BowMaxPull, 0f);
            if (blackboard != null && _bowRoot != null)
            {
                StickmanPoseAnimator pose = blackboard.GetPoseAnimator();
                if (pose != null && pose.HasLimbs)
                {
                    pose.GetHandWorldPositions(out Vector2 drawHand, out _);
                    if (drawHand != Vector2.zero)
                    {
                        Vector3 local = _bowRoot.InverseTransformPoint(new Vector3(drawHand.x, drawHand.y, 0f));
                        pulled = new Vector2(
                            Mathf.Clamp(local.x, -BowMaxPull * 1.9f, -BowMaxPull * 0.25f),
                            Mathf.Clamp(local.y, -BowHalfLength * 0.55f, BowHalfLength * 0.55f));
                    }
                }
            }
            return Vector2.Lerp(Vector2.zero, pulled, draw01);
        }

        /// <summary>
        /// 활 든 손의 컨테이너 로컬 좌표. <b>실제 팔 마디 끝</b>을 읽으므로 자세가 바뀌면 활도 따라
        /// 움직인다. 포즈 애니메이터를 못 구하는 폴백에서는 어깨 높이 근사치를 쓴다(0을 돌려주면 활이
        /// 발밑에 그려진다).
        /// </summary>
        private Vector2 ResolveBowHandLocal(StickmanBlackboard blackboard)
        {
            if (blackboard != null)
            {
                StickmanPoseAnimator pose = blackboard.GetPoseAnimator();
                if (pose != null && pose.HasLimbs)
                {
                    pose.GetHandWorldPositions(out _, out Vector2 right);
                    // 활은 항상 "앞쪽 팔"(NeutralSign=+1인 오른팔)이 든다. 좌우 미러링은 포즈
                    // 애니메이터가 각도에 곱하는 facingSign이 이미 처리했다.
                    if (right != Vector2.zero) return right - _anchorWorld;
                }
                if (blackboard.Body != null)
                {
                    StickmanMetrics m = Metrics;
                    float shoulder = m != null ? m.ShoulderLocalY : Height * 0.776f;
                    return new Vector2(blackboard.Body.position.x + _facing * Height * 0.18f,
                        blackboard.Body.position.y + shoulder) - _anchorWorld;
                }
            }
            return new Vector2(-_facing * Height * 2.8f, Height * 0.78f);
        }

        // ==================== 화살 ====================

        private void FireArrow()
        {
            if (_container == null) return;

            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            Vector2 origin = ResolveBowHandLocal(blackboard);
            float apex = ResolveApex(origin, _planImpactLocal);
            Vector2 v0 = SolveLaunchVelocity(origin, _planImpactLocal, _planFlight, apex);

            var go = new GameObject($"Arrow{_arrows.Count}");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = new Vector3(origin.x, origin.y, 0f);

            var line = CreateLine(go.transform, "Line", ResolveInk(), StrokeWidth * 0.62f, SortingBow, loop: false, capVertices: 0);
            line.positionCount = 7;
            // ★ 화살의 기준점은 <b>촉</b>이다(오늬가 아니다). 오늬를 -shaft에 두면 촉이 정확히 로컬
            // 원점 = 궤적점에 온다. 2026-08-29 사용자 신고 "다 외곽에 꽂히는거 같음"의 실제 원인이
            // 이것이었다 — 오늬를 기준점으로 두면 도달점에 꽂히는 것은 꼬리이고 촉은 그보다
            // 화살대 길이(신장의 34% = 과녁 반지름의 85%)만큼 **더 앞**에 그려져, 정중앙에 맞은
            // 화살조차 촉이 바깥 링에 걸린 "과녁을 관통한" 그림이 된다(실측 스크린샷 확인).
            BuildArrowPolyline(line, new Vector2(-ArrowShaftLength, 0f), ArrowShaftLength);

            _arrows.Add(new Arrow
            {
                Root = go.transform,
                Line = line,
                Origin = origin,
                LaunchVel = v0,
                Gravity = SolveGravity(_planFlight, apex),
                Flight = _planFlight,
                Elapsed = 0f,
                Result = _planResult,
                SettledAngle = SettledImpactAngle(
                    ImpactTangentDegrees(origin, _planImpactLocal, _planFlight, apex),
                    _planResult == ArcheryShotResult.Miss ? GroundImpactDescentDegrees : FaceImpactMaxDescentDegrees,
                    exact: _planResult == ArcheryShotResult.Miss),
                SettleStart = _planFlight * (1f - ImpactSettleRatio),
            });
        }

        private void TickArrows(float deltaTime, float alpha)
        {
            for (int i = 0; i < _arrows.Count; i++)
            {
                Arrow a = _arrows[i];
                if (a == null || a.Root == null) continue;

                if (!a.Stuck)
                {
                    a.Elapsed += deltaTime;
                    float t = Mathf.Min(a.Elapsed, a.Flight);
                    Vector2 p = a.Origin + a.LaunchVel * t - new Vector2(0f, 0.5f * a.Gravity * t * t);
                    Vector2 v = a.LaunchVel - new Vector2(0f, a.Gravity * t);
                    a.Root.localPosition = new Vector3(p.x, p.y, 0f);

                    // 비행 중에는 **실제 접선 각도** 그대로 돈다(과장된 포물선이 눈에 보여야 한다).
                    // 마지막 SettleStart 이후 구간에서만 착탄 각도로 smoothstep 보간한다 — 급전환이
                    // 눈에 띄지 않으면서도 꽂히는 순간의 각도는 사거리/볼록함과 무관하게 고정된다.
                    float tangent = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                    a.StuckAngle = Mathf.LerpAngle(tangent, a.SettledAngle, SettleWeight(t, a));
                    a.Root.localRotation = Quaternion.Euler(0f, 0f, a.StuckAngle);

                    if (a.Elapsed >= a.Flight)
                    {
                        a.Stuck = true;
                        a.StuckAge = 0f;
                        a.Line.sortingOrder = SortingStuckArrow;
                        OnArrowLanded(p, a.Result, tangent, a.StuckAngle);
                    }
                }
                else
                {
                    // 꽂힌 직후 잠깐 부르르 떤다 — 이 작은 디테일이 "꽂혔다"를 확실하게 읽히게 한다.
                    a.StuckAge += deltaTime;
                    float k = Mathf.Clamp01(1f - a.StuckAge / StickWobbleSeconds);
                    float wobble = k > 0f ? Mathf.Sin(a.StuckAge * 52f) * StickWobbleDegrees * k * k : 0f;
                    a.Root.localRotation = Quaternion.Euler(0f, 0f, a.StuckAngle + wobble);
                }

                SetLineAlpha(a.Line, alpha);
            }
        }

        private static float SettleWeight(float elapsed, Arrow a)
            => SettleWeight(elapsed, a.Flight, a.SettleStart);

        private void OnArrowLanded(Vector2 localImpact, ArcheryShotResult result, float tangentDeg, float stuckDeg)
        {
            CreateImpactBurst(localImpact);
            if (result == ArcheryShotResult.Miss) SpawnDust(localImpact);

            Debug.Log($"[활쏘기] 화살 도달 — 결과={result}, 도달점(로컬)={localImpact.ToString("F2")}(**촉 끝** 기준), " +
                $"접선 각도={tangentDeg:F1}도 -> 꽂힌 각도={stuckDeg:F1}도(수평 대비 하강 {DescentDegrees(stuckDeg):F1}도). " +
                "사전 확정 도달점과 동일합니다(궤적 역산이므로 오차가 누적되지 않습니다).");
        }

        private void CreateImpactBurst(Vector2 localOrigin)
        {
            if (_impactBurstRoot != null) Destroy(_impactBurstRoot.gameObject);

            var go = new GameObject("ImpactBurst");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = new Vector3(localOrigin.x, localOrigin.y, 0f);
            _impactBurstRoot = go.transform;

            _impactBurst = CreateLine(go.transform, "Rays", ResolveInk(), StrokeWidth * 0.7f, SortingBurst, loop: false, capVertices: 2);
            // 하나의 LineRenderer로 여러 갈래를 그리기 위해 중심을 매번 되짚는 지그재그 폴리라인을 쓴다
            // (BattleMinigameRenderer.CreateImpactBurst와 같은 기법).
            _impactBurst.positionCount = ImpactRayCount * 2 + 1;
            float len = Height * ImpactBurstRatio;
            for (int i = 0; i < ImpactRayCount; i++)
            {
                float a = (i / (float)ImpactRayCount) * Mathf.PI * 2f + 0.4f;
                _impactBurst.SetPosition(i * 2, Vector3.zero);
                _impactBurst.SetPosition(i * 2 + 1, new Vector3(Mathf.Cos(a) * len, Mathf.Sin(a) * len, 0f));
            }
            _impactBurst.SetPosition(ImpactRayCount * 2, Vector3.zero);
            _modeTimerBurst = 0f;
        }

        private float _modeTimerBurst = 999f;

        private void TickImpactBurst(float alpha)
        {
            if (_impactBurst == null || _impactBurstRoot == null) return;
            _modeTimerBurst += Time.deltaTime;
            float t = Mathf.Clamp01(_modeTimerBurst / ImpactBurstSeconds);
            if (t >= 1f) { SetLineAlpha(_impactBurst, 0f); return; }
            float scale = Mathf.Lerp(0.4f, 1.5f, t);
            _impactBurstRoot.localScale = new Vector3(scale, scale, 1f);
            SetLineAlpha(_impactBurst, (1f - t) * alpha);
        }

        /// <summary>빗나간 화살이 땅에 꽂힐 때만 나는 흙먼지 — "저건 과녁이 아니라 땅이다"를 알려주는 신호.</summary>
        private void SpawnDust(Vector2 localImpact)
        {
            Color ink = ResolveInk();
            float radius = Height * DustRadiusRatio;
            for (int i = 0; i < DustPuffCount; i++)
            {
                var go = new GameObject($"Dust{i}");
                go.transform.SetParent(_container.transform, false);
                go.transform.localPosition = new Vector3(localImpact.x, localImpact.y, 0f);

                var line = CreateRing(go.transform, "Puff", ink, radius * Random.Range(0.6f, 1.2f), SortingBurst);
                line.startWidth = StrokeWidth * 0.5f;
                line.endWidth = StrokeWidth * 0.5f;

                float a = Random.Range(0.15f, Mathf.PI - 0.15f);
                _puffs.Add(new Puff
                {
                    Root = go.transform,
                    Line = line,
                    Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Height * Random.Range(0.25f, 0.6f),
                    Age = 0f,
                });
            }
        }

        private void TickPuffs(float deltaTime, float alpha)
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff p = _puffs[i];
                if (p == null || p.Root == null) { _puffs.RemoveAt(i); continue; }
                p.Age += deltaTime;
                if (p.Age >= DustSeconds)
                {
                    Destroy(p.Root.gameObject);
                    _puffs.RemoveAt(i);
                    continue;
                }
                p.Velocity += Vector2.down * (Height * 0.9f * deltaTime);
                p.Root.localPosition += (Vector3)(p.Velocity * deltaTime);
                SetLineAlpha(p.Line, (1f - p.Age / DustSeconds) * 0.8f * alpha);
            }
        }

        // ==================== 궤적 역산 (테스트 대상 순수 함수) ====================
        // public인 이유: PlayMode 테스트 어셈블리(StickMate.Tests.PlayMode)는 InternalsVisibleTo
        // 대상이 아니다(AssemblyInfo.cs는 EditMode만 허용). 이 셋은 부작용이 전혀 없는 순수 함수라
        // 노출해도 위험이 없고, 다른 렌더러들이 진단/테스트 창구를 public 프로퍼티로 여는 것과 같은 관례다.

        /// <summary>
        /// 현(발사점->도달점 직선) 위로 <paramref name="apexHeight"/>만큼 부푸는 포물선을 만드는 중력 상수.
        /// 유도: 초기속도를 아래 <see cref="SolveLaunchVelocity"/>로 잡으면 궤적은 항상 현의 중점에서
        /// g*T^2/8 만큼 위로 벗어난다. 그 값을 apexHeight로 두고 g에 대해 풀면 아래 식이다.
        /// </summary>
        public static float SolveGravity(float flightSeconds, float apexHeight)
        {
            float t = Mathf.Max(0.0001f, flightSeconds);
            return 8f * Mathf.Max(0f, apexHeight) / (t * t);
        }

        /// <summary>
        /// 도달점과 비행 시간이 주어졌을 때 <b>유일하게 결정되는</b> 초기 속도.
        /// p(t) = p0 + v0*t - (0, g*t^2/2) 에 p(T) = p1 을 대입해 v0에 대해 푼 것이다.
        /// 물리 시뮬레이션이 아니므로 프레임레이트/충돌 우연으로 결과가 달라지지 않는다(리더 지시).
        /// </summary>
        public static Vector2 SolveLaunchVelocity(Vector2 from, Vector2 to, float flightSeconds, float apexHeight)
        {
            float t = Mathf.Max(0.0001f, flightSeconds);
            float g = SolveGravity(t, apexHeight);
            return (to - from) / t + new Vector2(0f, 0.5f * g * t);
        }

        /// <summary>
        /// 착탄 순간(t = flightSeconds)의 <b>접선 각도</b>(도, 월드 X축 기준). 궤적이 역산이므로
        /// 이 값도 발사 전에 닫힌 형태로 구할 수 있다 — 테스트가 "볼록함을 키우면 접선이 실제로
        /// 가팔라진다"는 네거티브 컨트롤을 검증하는 데 쓴다.
        /// </summary>
        public static float ImpactTangentDegrees(Vector2 from, Vector2 to, float flightSeconds, float apexHeight)
        {
            float T = Mathf.Max(0.0001f, flightSeconds);
            float g = SolveGravity(T, apexHeight);
            Vector2 v = SolveLaunchVelocity(from, to, T, apexHeight) - new Vector2(0f, g * T);
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 어떤 방향 각도가 <b>진행 방향 수평에서 아래로</b> 얼마나 기울었는지(도). 좌우 어느 쪽으로
        /// 날아가든 같은 부호 규약(+ = 코가 아래)이 되게 정규화한다 — 이 프로젝트는 좌우 미러링이
        /// 상시라 부호를 방향마다 따로 다루면 반드시 한쪽에서 틀린다.
        /// </summary>
        public static float DescentDegrees(float angleDegrees)
        {
            float dirSign = Mathf.Cos(angleDegrees * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            float baseAngle = dirSign >= 0f ? 0f : 180f;
            return -Mathf.DeltaAngle(baseAngle, angleDegrees) * dirSign;
        }

        /// <summary>
        /// 접선 각도로부터 <b>실제로 꽂힐 각도</b>를 만든다. 수평 진행 방향(좌/우)은 그대로 두고
        /// 하강각만 손본다.
        /// <para><paramref name="exact"/>=false(과녁 면): 하강각을 <paramref name="descentDegrees"/>
        /// <b>이내로 클램프</b>한다 — 이미 완만한 짧은 사격은 건드리지 않는다.</para>
        /// <para><paramref name="exact"/>=true(땅): 하강각을 그 값으로 <b>확정</b>한다 — 땅에 박힌
        /// 화살은 사거리와 무관하게 같은 모양이어야 "박혔다"로 읽힌다.</para>
        /// </summary>
        public static float SettledImpactAngle(float tangentDegrees, float descentDegrees, bool exact)
        {
            float dirSign = Mathf.Cos(tangentDegrees * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            float baseAngle = dirSign >= 0f ? 0f : 180f;
            float descent = DescentDegrees(tangentDegrees);
            float target = exact ? descentDegrees : Mathf.Min(descent, descentDegrees);
            return Mathf.DeltaAngle(0f, baseAngle + -target * dirSign);
        }

        /// <summary>
        /// 착탄 각도 보정의 가중치(0~1) — <paramref name="settleStart"/> 전에는 0(접선 그대로),
        /// 그 뒤로 smoothstep으로 1까지 오른다. 비행 시간에 대한 비율로만 정의되므로 프레임레이트가
        /// 달라져도 같은 시점에 같은 각도가 된다.
        /// </summary>
        public static float SettleWeight(float elapsed, float flightSeconds, float settleStart)
        {
            float span = Mathf.Max(0.0001f, flightSeconds - settleStart);
            float u = Mathf.Clamp01((elapsed - settleStart) / span);
            return u * u * (3f - 2f * u);
        }

        /// <summary>궤적 위의 한 점(테스트/진단용 — 렌더러 내부와 완전히 같은 식을 쓴다).</summary>
        public static Vector2 TrajectoryPoint(Vector2 from, Vector2 to, float flightSeconds, float apexHeight, float t)
        {
            float T = Mathf.Max(0.0001f, flightSeconds);
            float g = SolveGravity(T, apexHeight);
            Vector2 v0 = SolveLaunchVelocity(from, to, T, apexHeight);
            return from + v0 * t - new Vector2(0f, 0.5f * g * t * t);
        }

        // ==================== 정리 ====================

        private void Teardown()
        {
            _arrows.Clear();
            _puffs.Clear();
            _targetLines.Clear();
            _targetRoot = null;
            _bowRoot = null;
            _bowLimbs = null;
            _bowString = null;
            _nockedArrow = null;
            _impactBurst = null;
            _impactBurstRoot = null;
            _hasPlan = false;
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
        }

        // ==================== 공용 헬퍼 ====================

        private float ConfigFloat(System.Func<StickConfig, float> selector, float fallback)
        {
            StickConfig cfg = Config;
            return cfg != null ? selector(cfg) : fallback;
        }

        private Color ResolveInk()
            => _agent != null && _agent.Config != null ? _agent.Config.ResolveInkColor() : Color.black;

        /// <summary>
        /// 캐릭터가 이미 쓰고 있는 LineRenderer 머티리얼(Sprites-Default)을 그대로 빌려 쓴다.
        /// Shader.Find로 런타임에 찾지 않는 이유는 BattleMinigameRenderer 문서와 같다(빌드 스트리핑).
        /// </summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }

        /// <summary>화살 폴리라인(촉 + 대 + 오늬깃)을 로컬 +x 방향으로 그린다. 오늬가
        /// <paramref name="nock"/>에 오고 촉이 그 앞 <paramref name="shaft"/>만큼 떨어진 곳에 온다.</summary>
        private void BuildArrowPolyline(LineRenderer lr, Vector2 nock, float shaft, bool withFletching = true)
        {
            if (lr == null) return;
            float head = Height * ArrowHeadRatio;
            float fletch = Height * ArrowFletchRatio;
            float tipX = nock.x + shaft;
            float y = nock.y;

            if (!withFletching)
            {
                // 시위에 걸려 있는 동안은 오늬깃을 그리지 않는다 — 시위 두 가닥과 겹쳐 별표처럼
                // 뭉쳐 보였다(육안 확인, 2026-08-29 사용자 "활이 이상하다").
                lr.positionCount = 4;
                lr.SetPosition(0, new Vector3(nock.x, y, 0f));
                lr.SetPosition(1, new Vector3(tipX, y, 0f));
                lr.SetPosition(2, new Vector3(tipX - head, y + head * 0.55f, 0f));
                lr.SetPosition(3, new Vector3(tipX, y, 0f));
                return;
            }

            // 오늬깃 -> 대 -> 촉(V자를 되짚어 그린다).
            lr.positionCount = 7;
            lr.SetPosition(0, new Vector3(nock.x - fletch, y + fletch * 0.7f, 0f));
            lr.SetPosition(1, new Vector3(nock.x, y, 0f));
            lr.SetPosition(2, new Vector3(nock.x - fletch, y - fletch * 0.7f, 0f));
            lr.SetPosition(3, new Vector3(nock.x, y, 0f));
            lr.SetPosition(4, new Vector3(tipX, y, 0f));
            lr.SetPosition(5, new Vector3(tipX - head, y + head * 0.55f, 0f));
            lr.SetPosition(6, new Vector3(tipX - head, y - head * 0.55f, 0f));
        }

        /// <summary>반지름 r의 <b>채워진 원판</b> — 반지름 r/2 경로를 두께 r로 그어 만든다(클래스 문서 참고).</summary>
        private LineRenderer CreateDisk(Transform parent, string name, Color color, float radius, int sortingOrder)
        {
            var lr = CreateLine(parent, name, color, radius, sortingOrder, loop: true, capVertices: 4);
            SetCirclePath(lr, radius * 0.5f);
            return lr;
        }

        /// <summary>반지름 r의 <b>테두리 원</b>.</summary>
        private LineRenderer CreateRing(Transform parent, string name, Color color, float radius, int sortingOrder)
        {
            var lr = CreateLine(parent, name, color, StrokeWidth, sortingOrder, loop: true, capVertices: 2);
            SetCirclePath(lr, radius);
            return lr;
        }

        private static void SetCirclePath(LineRenderer lr, float radius)
        {
            lr.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        private LineRenderer CreateLine(Transform parent, string name, Color color, float width,
            int sortingOrder, bool loop, int capVertices)
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
            lr.numCapVertices = capVertices;
            lr.numCornerVertices = capVertices;
            lr.sortingOrder = sortingOrder;
            lr.loop = loop;
            // 그 밖의 설정은 기본값 그대로 — 캐릭터 획을 만드는 SceneBootstrapper.ConfigureLine()과
            // 같은 조합이라야 투명 창에서의 알파 합성 거동이 이미 검증된 것과 동일해진다.
            return lr;
        }

        private static void SetLineAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null) return;
            Color s = lr.startColor;
            Color e = lr.endColor;
            s.a = alpha;
            e.a = alpha;
            lr.startColor = s;
            lr.endColor = e;
        }
    }
}
