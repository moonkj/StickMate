using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 대결 시작 충돌 임팩트 — <see cref="StickmanEventBus.RivalDuelStarted"/>의 시각 소비자
    /// (2026-08-30 배선 감사 잔여 3건 중 1건).
    ///
    /// Interaction/RivalStickmanAgent.BeginDuel()은 2026-08-27부터 이 이벤트를 발행하고 있었지만
    /// <b>구독자가 프로젝트 전체에 0명</b>이었다 — 라이벌이 소리 없이 나타나 갑자기 싸움이 시작됐다.
    /// 이 렌더러는 그 시작 순간에 <b>두 캐릭터 사이 중점</b>에 짧은 방사형 임팩트 선을 한 번 튀긴다
    /// (10절 격파 미니게임의 타격 임팩트와 같은 계열의 표현 — 화면 흔들림 대신 쓰는 "국소 타격감").
    ///
    /// 왜 이 정도인가: 대결 자체는 최대 30초짜리 사건이고 그동안 두 캐릭터가 실제로 서로를 쫓아다니며
    /// 싸우는 것이 본편이다. 시작 신호는 <b>짧고 한 번</b>이어야 본편을 가리지 않는다. "VS" 글자를
    /// 띄우는 안도 검토했으나 이 앱의 캐릭터는 화면상 약 60pt라 그 사이에 글자를 넣으면 두 캐릭터를
    /// 통째로 덮는다(글자 렌더 경로도 Dialogue 레이어와 중복된다).
    ///
    /// 절대 원칙 3: 콜라이더를 하나도 만들지 않는 순수 오버레이다 — 임팩트 선이 떠 있는 0.45초 동안에도
    /// 그 자리의 다른 앱은 평소처럼 클릭된다(관전 전용, 11절 명시).
    /// </summary>
    public sealed class RivalDuelClashRenderer : MonoBehaviour
    {
        /// <summary>캐릭터 획(0~5)/먼지(6)/그라피티(9) 위 — 두 캐릭터 사이에서 확실히 보여야 한다.</summary>
        private const int SortingOrder = 14;

        /// <summary>같은 GameObject의 StickmanAgent만 쓴다(GraffitiRenderer와 같은 사본 방어 규약).</summary>
        private StickmanAgent _agent;

        /// <summary>대결 상대. 씬에 1개뿐이고 대결마다 바뀌지 않으므로 Awake에서 1회만 찾는다
        /// (매 이벤트마다 씬 탐색을 하지 않는다 — 24시간 상주 앱 성능 컨벤션).</summary>
        private RivalStickmanAgent _rival;

        private Material _lineMaterial;
        private GameObject _container;
        private LineRenderer _burst;
        private float _timer;
        private float _duration;
        private float _radius;

        /// <summary>테스트/진단용 — 지금 임팩트 선이 떠 있는지.</summary>
        public bool IsVisible => _container != null;

        /// <summary>테스트/진단용 — 마지막으로 임팩트를 그린 월드 좌표.</summary>
        public Vector2 LastClashWorldPosition { get; private set; }

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            if (_agent != null)
            {
                _rival = Object.FindFirstObjectByType<RivalStickmanAgent>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable() => StickmanEventBus.RivalDuelStarted += OnDuelStarted;

        private void OnDisable()
        {
            StickmanEventBus.RivalDuelStarted -= OnDuelStarted;
            Teardown();
        }

        private void OnDuelStarted()
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            var blackboard = _agent.Blackboard;
            StickConfig cfg = blackboard != null ? blackboard.Config : null;
            if (cfg != null && !cfg.rivalDuelClashEnabled) return;
            if (blackboard == null || blackboard.Body == null) return;

            float height = StickmanMetrics.TotalHeightOf(this);
            if (height <= 0f) return;

            // 두 캐릭터의 **가슴 높이** 중점. 루트 원점이 발바닥이므로 신장의 절반을 더한다
            // (이 프로젝트가 이미 두 번 밟은 함정: 루트를 그대로 쓰면 연출이 발밑에 깔린다).
            Vector2 playerChest = blackboard.Body.position + Vector2.up * (height * 0.5f);
            Vector2 rivalChest = _rival != null
                ? (Vector2)_rival.transform.position + Vector2.up * (height * 0.5f)
                : playerChest;
            Vector2 mid = (playerChest + rivalChest) * 0.5f;

            Begin(mid, cfg, height);
        }

        private void Begin(Vector2 world, StickConfig cfg, float characterHeight)
        {
            Teardown();

            _duration = cfg != null ? Mathf.Max(0.05f, cfg.rivalDuelClashSeconds) : 0.45f;
            _radius = characterHeight * (cfg != null ? cfg.rivalDuelClashRadiusRatio : 0.55f);
            float stroke = characterHeight * (cfg != null ? cfg.rivalDuelClashStrokeRatio : 0.028f);
            int rays = cfg != null ? Mathf.Clamp(cfg.rivalDuelClashRayCount, 3, 24) : 8;

            // 색은 라이벌 잉크(붉은색) — 이 연출은 "라이벌이 왔다"는 신호이므로 플레이어 잉크(검정)를
            // 쓰면 캐릭터 획의 일부처럼 읽힌다(GraffitiRenderer가 스프레이 색을 따로 쓰는 것과 같은 이유).
            Color ink = cfg != null ? cfg.rivalInkColor : new Color(0.85f, 0.13f, 0.13f);
            _lineMaterial = ResolveLineMaterial();

            _container = new GameObject("RivalDuelClash");
            _container.transform.SetParent(null, false);
            _container.transform.position = new Vector3(world.x, world.y, 0f);
            LastClashWorldPosition = world;

            var go = new GameObject("Rays");
            go.transform.SetParent(_container.transform, false);
            _burst = go.AddComponent<LineRenderer>();
            _burst.useWorldSpace = false;
            _burst.material = _lineMaterial;
            _burst.startColor = ink;
            _burst.endColor = ink;
            _burst.startWidth = stroke;
            _burst.endWidth = stroke;
            _burst.numCapVertices = 2;
            _burst.numCornerVertices = 2;
            _burst.sortingOrder = SortingOrder;
            _burst.loop = false;

            // 하나의 LineRenderer로 여러 갈래를 그리는 지그재그 폴리라인(중심 복귀 = 갈래 나누기).
            // BattleMinigameRenderer.CreateImpactBurst와 정확히 같은 기법이다.
            _burst.positionCount = rays * 2 + 1;
            for (int i = 0; i < rays; i++)
            {
                float a = (i / (float)rays) * Mathf.PI * 2f + 0.3f;
                var tip = new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a) * _radius, 0f);
                _burst.SetPosition(i * 2, Vector3.zero);
                _burst.SetPosition(i * 2 + 1, tip);
            }
            _burst.SetPosition(rays * 2, Vector3.zero);

            _timer = 0f;
            ApplyProgress(0f);

            Debug.Log($"[라이벌대결] 시작 임팩트 — 두 캐릭터 중점 {world} 에 {rays}갈래, " +
                $"반경 {_radius:F3}유닛, {_duration:F2}초 (콜라이더 0개, 관전 전용).");
        }

        private void LateUpdate()
        {
            if (_container == null) return;

            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);
            ApplyProgress(t);
            if (t >= 1f) Teardown();
        }

        private void ApplyProgress(float t)
        {
            if (_burst == null) return;
            float scale = Mathf.Lerp(0.3f, 1.35f, t); // 빠르게 퍼지며
            _container.transform.localScale = new Vector3(scale, scale, 1f);
            SetLineAlpha(_burst, 1f - t);              // 옅어진다.
        }

        private void Teardown()
        {
            _burst = null;
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
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

        /// <summary>캐릭터 LineRenderer의 머티리얼을 빌려 쓴다(Shader.Find 금지 — 빌드 스트리핑 위험).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
