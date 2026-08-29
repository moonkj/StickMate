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

        // 캐릭터 루트는 **발 높이**가 y=0이다(SceneBootstrapper 프리팹 지오메트리 주석 참고).
        // 링은 18절이 지정한 "발밑"이므로 발 높이 근처를 중심으로 잡는다 — 머리 위 오버레이
        // (HardwareReactionRenderer 2.32 / StressGaugeRenderer 어깨 1.33)와 세로로 완전히 갈라진다.
        private const float RingCenterY = 0.08f;
        private const float RingRadius = 0.54f;
        private const int RingSegments = 40;

        private const float GlanceY = 1.72f;   // 머리 옆(정수리 약 1.79 바로 아래).
        private const float GlanceX = 0.46f;

        private const float TapShakeAmplitude = 0.075f;
        private const float TapShakeSpeed = 17f;
        private const int TapMarkCount = 3;

        private const float StrokeWidth = 0.05f;
        private const int SortingOrder = 7;   // 캐릭터 획(0~5) 앞, 그라피티(9) 뒤.

        private static readonly Color TrackColor = new Color(0.42f, 0.46f, 0.52f, 0.55f);
        private static readonly Color CalmArcColor = new Color(0.36f, 0.72f, 0.62f, 1f);
        private static readonly Color GlanceColor = new Color(0.86f, 0.76f, 0.32f, 1f);
        private static readonly Color TapColor = new Color(0.94f, 0.46f, 0.30f, 1f);

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터/감시자. <b>같은 GameObject의 컴포넌트만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 라이벌은 플레이어 프리팹의 복제본이라 폴백을 두면 라이벌 발밑에도 링이
        /// 한 벌 더 생긴다(2026-08-29 격파 미니게임에서 실측 확인된 버그와 같은 함정).
        /// </summary>
        private StickmanAgent _agent;
        private FocusWatchDirector _director;
        private Material _lineMaterial;

        private GameObject _container;
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
            if (_agent == null) return; // 자기 캐릭터가 없는 사본(라이벌) — 전역 이벤트를 받아도 무시한다.

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
                center.x += shake * TapShakeAmplitude;
                center.y += Mathf.Cos(_tierTimer * TapShakeSpeed * 1.3f) * TapShakeAmplitude * 0.6f;
            }
            _container.transform.position = center;

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

            CreateLine("RingTrack", BuildCircle(Vector3.zero, RingRadius, RingSegments),
                TrackColor, StrokeWidth * 0.8f, loop: true);
            _arc = CreateLine("RingProgress", new[] { Vector3.zero, Vector3.zero }, CalmArcColor, StrokeWidth, loop: false);

            RebuildTierVisuals();

            Debug.Log($"[포모도로] 타이머 링 생성 — 캐릭터 발밑(y+{RingCenterY:F2}) 반지름 {RingRadius:F2}유닛, " +
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
                float relY = GlanceY - RingCenterY;
                for (int side = -1; side <= 1; side += 2)
                {
                    var pts = new Vector3[5];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float t = i / (float)(pts.Length - 1);
                        float a = Mathf.Lerp(-50f, 50f, t) * Mathf.Deg2Rad;
                        pts[i] = new Vector3(GlanceX * side + Mathf.Sin(a) * 0.06f * side,
                            relY + Mathf.Cos(a) * 0.12f - 0.12f, 0f);
                    }
                    _glanceLines.Add(CreateLine(side < 0 ? "GlanceL" : "GlanceR", pts, GlanceColor, StrokeWidth * 0.8f, loop: false));
                }
            }

            if (_tier == FocusWatchTier.WindowTap)
            {
                // 링을 두드린 자국 — 링 바깥 위쪽에 짧은 선 3개.
                for (int i = 0; i < TapMarkCount; i++)
                {
                    float angle = (60f + i * 30f) * Mathf.Deg2Rad;
                    var inner = new Vector3(Mathf.Cos(angle) * (RingRadius + 0.08f), Mathf.Sin(angle) * (RingRadius + 0.08f), 0f);
                    var outer = new Vector3(Mathf.Cos(angle) * (RingRadius + 0.28f), Mathf.Sin(angle) * (RingRadius + 0.28f), 0f);
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

        private Vector3 RingWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 body = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;
            Vector3 target = new Vector3(body.x, body.y + RingCenterY, 0f);

            // 링이 화면 밖으로 잘려 나가지 않게 뷰포트 안으로 클램프한다 — 캐릭터는 창 상단 테두리에
            // 서 있는 시간이 길고 화면 최하단 안전망에 서 있을 때도 있어서, 링 반지름만큼은 반드시
            // 여유를 둬야 한다(HardwareReactionRenderer.FollowHead()에서 실측으로 배운 교훈).
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

        private LineRenderer CreateLine(string name, Vector3[] points, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container.transform, false);

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
