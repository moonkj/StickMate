using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 착지 먼지 — <see cref="StickmanEventBus.LandingRollRequested"/>의 시각 소비자
    /// (2026-08-30 배선 감사 잔여 3건 중 1건).
    ///
    /// ============================================================================
    /// 이 이벤트가 "추가로" 무엇을 표현하는가 (붙이기 전에 먼저 답해야 했던 질문)
    /// ============================================================================
    /// 같은 임계값(StickConfig.landingSoftAbsorbThresholdHeights x 신장)은 이미 <b>무릎앉아 착지</b>
    /// (StickmanStateId.LandingCrouch)가 소비하고 있다 — 즉 착지의 <b>물리적 반응은 이미 있다</b>.
    /// 그래서 이 이벤트에 상태를 하나 더 만들거나 대사를 붙이는 것은 과설계이고, 남는 자리는
    /// "그 위에 얹는 가벼운 부가 연출"뿐이다. StickmanEventBus의 LandingCrouch 열거값 문서가
    /// <b>"LandingRollRequested는 먼지 파티클 같은 부수 연출용으로 그대로 남는다"</b>고 이미 그 자리를
    /// 지정해두었으므로 그대로 따랐다.
    ///
    /// 대사를 붙이지 않은 이유(불변 원칙 1): 대사는 상태 전이가 확정된 뒤 그 상태에서 파생되어야 하는데,
    /// 착지 직후의 확정된 상태는 LandingCrouch/Idle/Walk이고 그 대사 경로는 이미
    /// Dialogue/AmbientChatter.cs가 갖고 있다. 여기서 별도로 한 줄 더 띄우면 "이벤트에서 파생된 대사"가
    /// 되어 원칙 1이 금지하는 두 번째 대사 생산자가 생긴다.
    ///
    /// ============================================================================
    /// 왜 좌표를 이벤트에서 받는가
    /// ============================================================================
    /// 발행자인 FallState/ThrowTumbleState는 <b>어느 캐릭터의 상태머신에도 등록될 수 있다</b>.
    /// 좌표 없이 "낙하 높이"만 받으면 두 번째 캐릭터가
    /// 착지할 때 플레이어 발밑에 먼지가 피는 오귀속이 구조적으로 발생한다. 그래서 이번 라운드에
    /// 페이로드를 <see cref="LandingImpactEvent"/>로 바꿔 착지 좌표를 함께 싣게 했고, 이 렌더러는
    /// <b>누가 착지했든 그 좌표에</b> 그린다(캐릭터를 따라다니지 않는다 — 먼지는 땅에 남는 것이다).
    ///
    /// 절대 원칙 3: 우리 오버레이 창 안에 LineRenderer 몇 개를 그렸다 지우는 것이 전부다. 콜라이더를
    /// 하나도 만들지 않으므로 먼지가 떠 있는 동안에도 그 자리의 다른 앱은 평소처럼 클릭된다.
    /// </summary>
    public sealed class LandingDustRenderer : MonoBehaviour
    {
        /// <summary>캐릭터 획(0~5) 바로 위, 그라피티(9) 아래.</summary>
        private const int SortingOrder = 6;

        /// <summary>먼지 한 점의 부채꼴 배치 반각(도, 수직 기준). 75도면 거의 눕는 부채꼴이라
        /// "바닥에서 옆으로 튄 흙먼지"로 읽힌다(작으면 분수처럼 위로 솟는다).</summary>
        private const float FanHalfAngleDegrees = 75f;

        /// <summary>먼지 획 한 개(작은 초승달)의 반지름 — 획 두께 배수.</summary>
        private const float PuffRadiusInStrokes = 2.2f;

        private const int PuffArcSegments = 5;

        private sealed class Puff
        {
            public Transform Root;
            public LineRenderer Line;
            public Vector2 Direction;  // 정규화된 부채꼴 방향(x는 좌우, y는 위).
            public float StartScale;
            public float EndScale;
        }

        /// <summary>같은 GameObject의 StickmanAgent만 쓴다 — 이 프리팹이 복제되면 사본도 이 컴포넌트를 갖게 되어
        /// 이 컴포넌트를 함께 갖게 되는데, 씬 전체 폴백을 두면 사본이 같은 전역 이벤트에 반응해
        /// 먼지가 두 벌 그려진다(GraffitiRenderer와 같은 규약).</summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private GameObject _container;
        private Puff[] _puffs;
        private int _puffCount;
        private float _timer;
        private float _duration;
        private float _spreadWorld;
        private float _riseWorld;

        /// <summary>테스트/진단용 — 지금 화면에 먼지가 떠 있는지.</summary>
        public bool IsVisible => _container != null;

        /// <summary>테스트/진단용 — 마지막 착지에서 계산된 세기(0~1).</summary>
        public float LastIntensity { get; private set; }

        private void Awake() => _agent = GetComponent<StickmanAgent>();

        private void OnEnable() => StickmanEventBus.LandingRollRequested += OnLandingRoll;

        private void OnDisable()
        {
            StickmanEventBus.LandingRollRequested -= OnLandingRoll;
            Teardown(); // 컴포넌트가 꺼질 때 먼지가 화면에 영구히 남지 않게(GraffitiRenderer와 같은 정리 관례).
        }

        private void OnLandingRoll(LandingImpactEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            StickConfig cfg = _agent.Blackboard != null ? _agent.Blackboard.Config : null;
            if (cfg != null && !cfg.landingDustEnabled) return;

            float height = StickmanMetrics.TotalHeightOf(this);
            if (height <= 0f) return;

            LastIntensity = ComputeIntensity(evt.FallHeight, cfg, height);
            Begin(evt.FootWorldPosition, LastIntensity, cfg, height);
        }

        /// <summary>
        /// 낙하 높이 -> 먼지 세기(0~1). 무릎앉아 깊이 램프(LandingCrouchState.Enter)와 <b>같은 식</b>을
        /// 쓴다 — 임계값을 갓 넘긴 착지가 0(하한 landingDustMinIntensity), 신장의 landingDustFullHeights
        /// 배만큼 더 떨어진 착지가 1이다. 두 층이 같은 기준을 공유하므로 "깊이 앉을수록 먼지도 크다"가
        /// 두 곳을 따로 튜닝하지 않아도 자동으로 성립한다(이 프로젝트는 같은 값을 두 곳에서 따로 계산해
        /// 어긋난 전례가 2회 있다).
        /// </summary>
        internal static float ComputeIntensity(float fallHeight, StickConfig cfg, float characterHeight)
        {
            // ★ 2026-09-01 — 무릎앉아가 6티어로 바뀌면서 이 램프도 **두 단**이 됐다(MOTION_SPEC 4-5 주).
            //   (가) T0.5 구간(0.35 H ~ 0.88 H): 이제 Dock 단차 같은 얕은 착지도 연출을 얻으므로,
            //        먼지가 갑자기 minIntensity로 튀어나오면 "한 계단마다 흙먼지"가 된다. 그래서
            //        이 구간은 minIntensity에 t0을 곱해 0에서부터 서서히 올린다 — 아주 옅게라도 나야
            //        착지가 읽힌다.
            //   (나) 그 위: 예전과 같은 램프(minIntensity -> 1). 임계값만 절대 유닛에서 H 배수가 됐다.
            float softStart = cfg != null
                ? cfg.ResolveLandingSoftAbsorbThreshold(characterHeight)
                : 0.35f * StickConfig.BaselineCharacterTotalHeight;
            float threshold = cfg != null
                ? cfg.ResolveLandingReactionThreshold(characterHeight)
                : 0.88f * StickConfig.BaselineCharacterTotalHeight;
            float fullHeights = cfg != null ? Mathf.Max(0.01f, cfg.landingDustFullHeights) : 3f;
            float minIntensity = cfg != null ? Mathf.Clamp01(cfg.landingDustMinIntensity) : 0.45f;

            if (fallHeight < threshold)
            {
                float softSpan = Mathf.Max(0.0001f, threshold - softStart);
                float t0 = Mathf.Clamp01((fallHeight - softStart) / softSpan);
                return minIntensity * t0;
            }

            float span = fullHeights * characterHeight;
            float t = span > 0.0001f ? Mathf.Clamp01((fallHeight - threshold) / span) : 1f;
            return Mathf.Lerp(minIntensity, 1f, t);
        }

        private void Begin(Vector2 footWorld, float intensity, StickConfig cfg, float characterHeight)
        {
            Teardown();

            int count = cfg != null ? Mathf.Clamp(cfg.landingDustPuffCount, 1, 24) : 5;
            _duration = cfg != null ? Mathf.Max(0.05f, cfg.landingDustSeconds) : 0.38f;
            _spreadWorld = characterHeight * (cfg != null ? cfg.landingDustSpreadRatio : 0.34f) * intensity;
            _riseWorld = characterHeight * (cfg != null ? cfg.landingDustRiseRatio : 0.12f) * intensity;
            float stroke = characterHeight * (cfg != null ? cfg.landingDustStrokeRatio : 0.022f);

            Color ink = cfg != null ? cfg.ResolveInkColor() : Color.black;
            _lineMaterial = ResolveLineMaterial();

            _container = new GameObject("LandingDust");
            _container.transform.SetParent(null, false);
            _container.transform.position = new Vector3(footWorld.x, footWorld.y, 0f);

            if (_puffs == null || _puffs.Length < count) _puffs = new Puff[count];
            _puffCount = count;

            for (int i = 0; i < count; i++)
            {
                // 수직(위)을 0으로 두고 ±FanHalfAngle로 고르게 편다. count가 1이면 정확히 수직.
                float t = count > 1 ? (i / (float)(count - 1)) * 2f - 1f : 0f;
                float a = t * FanHalfAngleDegrees * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));

                var go = new GameObject("Puff");
                go.transform.SetParent(_container.transform, false);
                go.transform.localPosition = Vector3.zero;

                LineRenderer lr = CreateArc(go.transform, ink, stroke, PuffRadiusInStrokes * stroke);
                // 부채꼴 가장자리일수록 작게 — 가운데가 크면 "튀어오른 덩어리"의 중심이 읽힌다.
                float sizeBias = Mathf.Lerp(1f, 0.6f, Mathf.Abs(t));
                _puffs[i] = new Puff
                {
                    Root = go.transform,
                    Line = lr,
                    Direction = dir,
                    StartScale = 0.35f * sizeBias,
                    EndScale = 1.15f * sizeBias,
                };
            }

            _timer = 0f;
            ApplyProgress(0f);

            Debug.Log($"[착지먼지] 발밑 {footWorld} 에 먼지 {count}점 — 세기 {intensity:F2}, " +
                $"확산 {_spreadWorld:F3}유닛/상승 {_riseWorld:F3}유닛, {_duration:F2}초 (콜라이더 0개, 관전 전용).");
        }

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            if (_container == null) return;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);
            ApplyProgress(t);
            if (t >= 1f) Teardown();
        }

        private void ApplyProgress(float t)
        {
            // 퍼짐은 easeOut(착지 순간이 가장 빠르다), 알파는 뒤로 갈수록 빠르게 사라진다.
            float ease = 1f - (1f - t) * (1f - t);
            float alpha = (1f - t) * (1f - t);

            for (int i = 0; i < _puffCount; i++)
            {
                Puff p = _puffs[i];
                if (p?.Root == null) continue;

                p.Root.localPosition = new Vector3(
                    p.Direction.x * _spreadWorld * ease,
                    p.Direction.y * _riseWorld * ease,
                    0f);
                float scale = Mathf.Lerp(p.StartScale, p.EndScale, ease);
                p.Root.localScale = new Vector3(scale, scale, 1f);
                SetLineAlpha(p.Line, alpha);
            }
        }

        private void Teardown()
        {
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _puffCount = 0;
        }

        /// <summary>작은 초승달 한 조각 — 점이나 직선보다 "먼지"로 읽힌다.</summary>
        private LineRenderer CreateArc(Transform parent, Color color, float width, float radius)
        {
            var go = new GameObject("Arc");
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
            lr.sortingOrder = SortingOrder;
            lr.loop = false;
            lr.positionCount = PuffArcSegments + 1;
            for (int i = 0; i <= PuffArcSegments; i++)
            {
                // 위쪽으로 볼록한 200도 호(양 끝이 아래를 향해 살짝 말린다).
                float a = Mathf.Lerp(-10f, 190f, i / (float)PuffArcSegments) * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.7f, 0f));
            }
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

        /// <summary>GraffitiRenderer와 같은 이유로 캐릭터 LineRenderer의
        /// 머티리얼을 빌려 쓴다(Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
