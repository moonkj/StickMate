using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 격파 미니게임 시각 레이어 — docs/UX_FLOW.md 10절의 "유저가 보는 것"을 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가 (정직한 이력 — DialogueBubbleRenderer와 완전히 같은 이야기)
    /// ============================================================================
    /// 상태 머신(States/BattleMinigameState.cs)과 트리거/락 배선(BattleMinigameDirector)은 이미 완성돼
    /// 있었고, self-transition으로 "판정 순간의 대사"까지 파생시키는 정교한 구조였다. 그런데
    /// <b>StickmanEventBus.BattleMinigamePhaseChanged를 구독하는 코드가 어디에도 없었고</b>, 두 컨트롤러
    /// 자체가 씬/프리팹 어디에도 배치돼 있지 않았다(Assets/Editor/SceneBootstrapper.cs에 AddComponent
    /// 호출이 없었다). 즉 기 모으기도, 스위트스팟도, 파괴도 전부 코드로만 존재하고 화면에는 단 한 픽셀도
    /// 나오지 않았다. 이 컴포넌트가 그 빠진 조각이며, 상태/판정 로직은 한 줄도 바꾸지 않는다.
    ///
    /// ============================================================================
    /// 무엇을 그리는가 (10절 "유저가 보는 것" 4단계 대응)
    /// ============================================================================
    /// · 2) "기와/대리석이 순차 소환되어 허공에 쌓임(중력 적용, 살짝 흔들림)"
    ///      -> 캐릭터 정면 <see cref="StackForwardOffset"/>유닛 앞, 주먹 높이에 판자 2장이 위에서
    ///         떨어져 쌓인다(<see cref="SpawnDropSeconds"/>, 장당 <see cref="SpawnStaggerSeconds"/>
    ///         시차). 착지 후에도 아주 약한 sin 흔들림이 계속된다.
    /// · 3) "기 모으기 게이지가 1.5~2초에 걸쳐 채워짐"
    ///      -> 판자 아래의 가로 게이지 바. 채움 비율은 StickmanBlackboard.BattleChargeRatio를 그대로
    ///         읽는다(그 필드 문서 참고 — 판정에는 전혀 쓰이지 않는 단방향 렌더 힌트라, 여기서 무엇을
    ///         하든 성공/실패 판정은 1비트도 달라지지 않는다).
    /// · 4) "스위트 스팟 구간(70~85%)에 들어서면 색/반짝임으로 신호"
    ///      -> 게이지 트랙 위에 스위트스팟 구간이 항상 <see cref="SweetSpotIdleColor"/>로 표시돼 있고,
    ///         채움이 그 구간에 <b>실제로 들어와 있는 동안</b>만 <see cref="SweetSpotHotColor"/>로 바뀌며
    ///         굵어지고 깜빡인다. 구간 경계값은 하드코딩하지 않고 StickConfig.battleSweetSpot*를 읽으므로
    ///         판정 로직과 표시가 영원히 같은 값을 쓴다(둘이 어긋나면 "분명 노란 구간에서 눌렀는데
    ///         실패"라는 최악의 UX가 된다).
    /// · 성공 "오브젝트가 산산조각(파편 파티클)" -> <see cref="ShardCount"/>개의 파편 획이 중력/회전과
    ///   함께 튀어나가며 페이드아웃 + 타격점에서 방사형 임팩트 선.
    /// · 실패 "오브젝트는 그대로, 캐릭터는 손을 텀"       -> 판자가 감쇠 sin으로 짧게 흔들린다.
    /// · 3회 소진/5초 타임아웃 "민망한 퇴장 애니메이션"   -> 판자가 쪼그라들며 옅어져 사라진다.
    ///
    /// ============================================================================
    /// 클릭 입력 (10절 "캐릭터/오브젝트의 화면 히트박스 영역 위 클릭")
    /// ============================================================================
    /// 소환된 판자+게이지 영역을 덮는 <b>isTrigger=true BoxCollider2D</b> 하나를 함께 만든다. 이 콜라이더는
    /// 두 군데에서 동시에 쓰인다:
    ///   (a) OS 레벨 — UniWindowController의 hitTestType=Raycast가 매 프레임 Physics2D.GetRayIntersection
    ///       (DefaultRaycastLayers, m_QueriesHitTriggers=1)으로 커서 아래를 검사하므로, 이 콜라이더가
    ///       있는 동안만 그 사각형 위에서 클릭관통이 풀린다. 미니게임이 끝나면 콜라이더를 즉시 파괴하므로
    ///       (파편이 아직 날아가는 중이더라도) 관통이 곧바로 원복된다 — 비침해 원칙 2 유지.
    ///   (b) Unity 레벨 — StickmanClickHitbox.RegisterExtraCollider()로 등록해, 전역 폴링 경로의
    ///       판정 집합에 포함시킨다. 그러면 판자 위 클릭도 기존 캐릭터 클릭과 <b>정확히 같은</b>
    ///       MouseDown 이벤트 하나로 합류해 BattleMinigameDirector가 이미 구독 중인 경로를 그대로 탄다
    ///       — 새 입력 경로를 만들지 않는다.
    /// isTrigger인 이유는 AppControlDirector의 메뉴 차단막과 동일하다: 히트테스트에는 잡히지만
    /// OnCollisionEnter2D는 발생하지 않아 캐릭터 물리/랙돌 거동에 전혀 관여하지 않는다.
    ///
    /// ============================================================================
    /// 말풍선과의 배치 충돌 회피
    /// ============================================================================
    /// 말풍선(Dialogue/DialogueBubbleRenderer)은 캐릭터 <b>머리 위</b>에 뜨고, 이 연출은 전부 캐릭터
    /// <b>정면 옆</b>(x로 <see cref="StackForwardOffset"/>유닛 이동)의 주먹 높이 이하에만 그린다. 게이지는
    /// 판자보다 더 아래에 둬서 머리 위 공간을 아예 쓰지 않는다 — 10절은 이 미니게임에서 대사를 3번
    /// ("좋아, 간다" / "필살기다!" / "어... 어라?") 띄우므로 겹침 회피가 실제로 필요하다.
    ///
    /// 그리기 방식은 캐릭터와 동일하게 LineRenderer다(이 프로젝트에는 스프라이트 에셋이 하나도 없다).
    /// "흰 채움"은 별도 메시/스프라이트 없이, <b>선 두께를 판자 높이만큼 준 흰색 선분 1개</b>로 만든다 —
    /// 새 에셋/셰이더를 전혀 도입하지 않으면서 굵은 검은 테두리 + 흰 속이라는 캐릭터 스타일을 그대로
    /// 재현하는 가장 싼 방법이다. 머티리얼조차 새로 만들지 않고 캐릭터 LineRenderer의 것을 재사용한다
    /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).
    /// </summary>
    public sealed class BattleMinigameRenderer : MonoBehaviour
    {
        // ==================== 배치/치수 (월드 유닛) ====================
        // 화면 환산 기준: 카메라 orthographicSize=12, 창 높이 846pt -> 35.25 pt/유닛
        // (Assets/Editor/SceneBootstrapper.cs의 OrthographicSize 문서와 같은 계산).

        private const float StackForwardOffset = 1.05f; // 캐릭터 정면으로 밀어놓는 거리(약 37pt).
        private const float StackBaseHeight = 1.02f;    // 맨 아래 판자의 중심 높이(주먹~어깨 사이).
        private const float TileWidth = 1.00f;          // 판자 가로(약 35pt = 캐릭터 키의 44%).
        private const float TileHeight = 0.30f;         // 판자 세로(약 11pt).
        private const float TileGap = 0.13f;            // 판자 사이 간격.
        private const int TileCount = 2;                // "순차 소환되어 쌓임" — 2장이면 충분히 읽힌다.

        private const float GaugeWidth = 1.56f;         // 게이지 트랙 가로(약 55pt).
        // 실물 스크린샷 확인 후 상향(2026-08-29): 0.20유닛(약 8pt)은 캐릭터 키가 화면상 80pt뿐인
        // 이 앱에서 '무엇이 얼마나 찼는지'를 읽기에 너무 얇았다. 테두리를 뺀 실제 채움 두께가
        // 약 7.5pt가 되도록 잡은 값이다.
        private const float GaugeHeight = 0.26f;
        private const float GaugeDropBelowStack = 0.46f; // 맨 아래 판자에서 게이지까지의 수직 간격.

        private const float StrokeWidth = 0.077f;       // 캐릭터 몸통 획(0.11*0.7)과 같은 굵기.

        // ==================== 타이밍 ====================

        private const float SpawnDropSeconds = 0.28f;   // 판자 한 장이 위에서 떨어져 자리 잡는 시간.
        private const float SpawnStaggerSeconds = 0.12f; // "순차" 소환 — 장마다 이만큼씩 늦게 시작.
        private const float SpawnDropHeight = 0.75f;    // 낙하 시작 높이(제자리 기준 위쪽).
        private const float IdleWobbleAmplitude = 0.018f; // 착지 후 "살짝 흔들림"(10절)의 진폭.
        private const float IdleWobbleSpeed = 3.4f;
        private const float FailShakeSeconds = 0.42f;   // 실패 시 판자가 버티며 흔들리는 시간.
        private const float FailShakeAmplitude = 0.075f;
        private const float RetreatSeconds = 0.62f;     // 3회 소진/타임아웃 시 "민망한 퇴장".
        private const float ShatterSeconds = 0.95f;     // 파편이 날아가며 사라지기까지.
        private const float ImpactBurstSeconds = 0.24f; // 타격점 방사형 임팩트 선.
        private const float PlainFadeSeconds = 0.34f;   // 그 밖의 경로(긴급정지 등)로 끝났을 때의 정리 페이드.

        // ==================== 파편 ====================

        private const int ShardCount = 14;
        private const int ImpactRayCount = 7;
        private const float ShardSpeedMin = 1.6f;
        private const float ShardSpeedMax = 4.6f;
        private const float ShardGravity = 7.5f;
        private const float ShardSpinMax = 900f;
        private const float ShardLengthMin = 0.10f;
        private const float ShardLengthMax = 0.26f;

        // ==================== 색 ====================

        private static readonly Color FillColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color SweetSpotIdleColor = new Color(1f, 0.78f, 0.26f, 1f);  // 평소: 차분한 호박색.
        private static readonly Color SweetSpotHotColor = new Color(1f, 0.36f, 0.16f, 1f);   // 진입 순간: 뜨거운 주황.

        // 캐릭터 획(0~5)보다 확실히 위. 말풍선 캔버스는 별도 ScreenSpaceOverlay라 서로 경쟁하지 않는다.
        private const int SortingFill = 10;
        private const int SortingStroke = 11;

        // 게이지는 4겹을 이 순서로 쌓는다(아래 -> 위):
        //   12 흰 트랙 배경 / 13 검은 채움 / 14 스위트스팟 밴드 / 15 검은 테두리
        // 스위트스팟이 채움보다 **위**여야 한다 — 아래에 깔면 채움이 정답 구간에 도달하는 순간 그 구간을
        // 새까맣게 덮어버려서, 정작 눌러야 할 그 순간에 표시가 사라지는 최악의 UX가 된다.
        private const int SortingGaugeTrack = 12;
        private const int SortingGaugeFill = 13;
        private const int SortingGaugeSweetSpot = 14;
        private const int SortingGaugeStroke = 15;

        /// <summary>이 렌더러가 지금 무엇을 보여주고 있는지. 종료 애니메이션(Shatter/Retreat/Fade)은
        /// 상태 머신이 이미 Idle로 돌아간 뒤에도 계속 재생돼야 하므로 상태 ID와 1:1로 묶지 않는다.</summary>
        private enum Mode { None, Playing, Shatter, Retreat, PlainFade }

        private sealed class Tile
        {
            public Transform Root;
            public LineRenderer Fill;
            public LineRenderer Stroke;
            public float RestLocalY;
            public float SpawnDelay;
        }

        private sealed class Shard
        {
            public Transform Root;
            public LineRenderer Line;
            public Vector2 Velocity;
            public float Spin;
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 라이벌 스틱맨은 플레이어 프리팹의 복제본이라 이 컴포넌트를 함께 갖게
        /// 되는데, 씬 폴백을 두면 라이벌의 렌더러가 **플레이어의** StickmanAgent를 자기 것으로 착각해
        /// 전역 이벤트에 반응하고 소환물이 두 벌 그려진다(실측 확인, 2026-08-29). 에이전트가 없으면
        /// 이 컴포넌트는 조용히 아무것도 하지 않는다(DialogueBubbleRenderer의 _requireBoundSpeaker와
        /// 같은 취지의 "화자 미지정이면 그리지 않는다" 규약 — UX_FLOW.md 5절 규칙 7).
        /// </summary>
        private StickmanAgent _agent;
        private StickmanClickHitbox _hitbox;
        private Material _lineMaterial;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _elapsed;          // 소환 이후 누적 시간(순차 낙하/흔들림 위상용).
        private float _failShakeTimer;   // >0이면 실패 흔들림 재생 중.

        private GameObject _container;
        private Transform _stackRoot;
        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly List<Shard> _shards = new List<Shard>();

        private LineRenderer _gaugeTrackFill;
        private LineRenderer _gaugeTrackStroke;
        private LineRenderer _gaugeSweetSpot;
        private LineRenderer _gaugeFill;
        private LineRenderer _impactBurst;

        private BoxCollider2D _clickTarget;

        private Vector2 _anchorWorld;    // 소환 시점의 기준점(캐릭터 발밑). 소환 후에는 움직이지 않는다.
        private float _facing = 1f;

        /// <summary>테스트/진단용 — 지금 화면에 격파 미니게임 시각 요소가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _hitbox = GetComponent<StickmanClickHitbox>();
        }

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.BattleMinigamePhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.BattleMinigamePhaseChanged -= OnPhaseChanged;

            // 이 컴포넌트가 비활성화/파괴될 때 소환물이 화면에 영구히 남지 않게 한다 — Director들이
            // OnDisable()에서 SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례다.
            // 여기서는 락이 아니라 "클릭을 가로채는 콜라이더"가 남는 것이 더 위험하다(비침해 원칙 2).
            DestroyClickTarget();
            Teardown();
        }

        // ==================== 이벤트 ====================

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본(라이벌) — 전역 이벤트를 받아도 무시한다.

            // 진입: Idle/Walk -> BattleMinigame. self-transition(재판정)은 From==To라 여기서 걸러진다.
            if (evt.To == StickmanStateId.BattleMinigame && evt.From != StickmanStateId.BattleMinigame)
            {
                Spawn();
                return;
            }

            if (evt.From != StickmanStateId.BattleMinigame) return;
            if (evt.To == StickmanStateId.BattleMinigame) return; // 판정 중(재도전) — 아직 진행 중이다.

            // 이탈 확정. 클릭 가로채기는 이 순간 즉시 끝낸다(파편이 아직 날아가는 중이어도).
            DestroyClickTarget();
            Debug.Log($"[격파] 종료 — {evt.To}(으)로 전이(강제인터럽트={evt.IsForcedInterrupt}). " +
                "판자/게이지 클릭 표적을 즉시 제거해 클릭관통을 원복했습니다.");

            // 이미 결말 애니메이션이 재생 중이면(성공 산산조각 / 소진 퇴장) 그대로 끝까지 보여준다.
            // 그 외 경로(긴급정지 강제 인터럽트 등)는 조용히 페이드아웃으로 정리한다.
            if (_mode == Mode.Playing) BeginMode(Mode.PlainFade);
        }

        private void OnPhaseChanged(BattleMinigamePhase phase)
        {
            if (_mode != Mode.Playing) return;

            // 판정 결과를 화면 연출과 같은 자리에서 한 줄로 남긴다 — 이 앱은 창을 볼 수 없는 환경에서
            // 검증되는 일이 많아, "지금 유저가 무엇을 봤는지"를 로그만으로 재구성할 수 있어야 한다
            // (StickmanClickHitbox의 [n/6] 진단 로그와 같은 취지).
            float ratio = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.BattleChargeRatio : -1f;

            switch (phase)
            {
                case BattleMinigamePhase.Success:
                    Debug.Log($"[격파] 판정=성공 — 릴리즈 게이지 {ratio:P1} (스위트스팟 " +
                        $"{SweetStart():P0}~{SweetEnd():P0} 안). 판자를 산산조각 냅니다.");
                    Shatter();
                    break;

                case BattleMinigamePhase.Fail:
                    Debug.Log($"[격파] 판정=실패(재도전) — 릴리즈 게이지 {ratio:P1} (스위트스팟 " +
                        $"{SweetStart():P0}~{SweetEnd():P0} 밖). 판자는 부서지지 않고 흔들리기만 하고, " +
                        "잠시 뒤 게이지가 다시 채워집니다.");
                    _failShakeTimer = FailShakeSeconds; // 판자는 그대로 유지되고 흔들리기만 한다(10절 실패 UX).
                    break;

                case BattleMinigamePhase.Exhausted:
                    Debug.Log("[격파] 판정=소진/타임아웃 — 재도전 횟수를 다 썼거나 5초 동안 클릭이 없었습니다. " +
                        "판자가 쪼그라들며 사라지는 '민망한 퇴장'으로 정상 종료합니다.");
                    BeginMode(Mode.Retreat);
                    break;
            }
        }

        private void BeginMode(Mode mode)
        {
            _mode = mode;
            _modeTimer = 0f;
        }

        // ==================== 소환 ====================

        private void Spawn()
        {
            Teardown(); // 혹시 남아 있던 이전 사이클 잔재를 먼저 정리(멱등).

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[격파] 시각 요소를 만들지 못했습니다 — StickmanAgent/Body 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _facing = blackboard.FacingSign >= 0f ? 1f : -1f;
            _anchorWorld = blackboard.Body.position;

            _container = new GameObject("BattleMinigameVisuals");
            _container.transform.SetParent(null, false);
            // 캐릭터의 자식으로 붙이지 않는 이유: 소환된 판자는 "허공에 쌓인 물체"라 캐릭터가 걷거나
            // 던져져도 제자리에 있어야 한다(10절 "허공에 쌓임"). 캐릭터를 따라다니면 격파할 대상이
            // 아니라 캐릭터의 장식품처럼 보인다.
            _container.transform.position = new Vector3(_anchorWorld.x + _facing * StackForwardOffset, _anchorWorld.y, 0f);

            _stackRoot = new GameObject("Stack").transform;
            _stackRoot.SetParent(_container.transform, false);

            Color ink = ResolveInk();

            for (int i = 0; i < TileCount; i++)
            {
                float restY = StackBaseHeight + i * (TileHeight + TileGap);
                var tile = CreateTile(i, restY, ink);
                // 아래에서부터 순서대로 쌓인다 — 위 판자가 나중에 떨어져야 "쌓이는" 것으로 읽힌다.
                tile.SpawnDelay = i * SpawnStaggerSeconds;
                _tiles.Add(tile);
            }

            CreateGauge(ink);
            CreateClickTarget();

            _elapsed = 0f;
            _failShakeTimer = 0f;
            BeginMode(Mode.Playing);

            // 클릭 표적의 실제 OS 화면 사각형을 함께 남긴다 — 이 앱은 창을 클릭할 수도 포커스를 줄 수도
            // 없어서, "어디를 눌러야 판정에 잡히는가"를 로그로 확인할 수 있는 것 자체가 검증 수단이다
            // (StickmanClickHitbox의 준비 상태 로그와 같은 취지).
            Rect targetOs = ClickHitboxRectUtility.ComputeOsRect(
                _clickTarget, blackboard.MainCamera, blackboard.Config);
            Debug.Log($"[격파] 소환 — 판자 {TileCount}장 + 기 모으기 게이지를 캐릭터 " +
                $"{(_facing > 0f ? "오른쪽" : "왼쪽")} {StackForwardOffset:F2}유닛 앞에 배치했습니다. " +
                $"스위트스팟 {SweetStart():P0}~{SweetEnd():P0}, 클릭 표적 " +
                $"{(_clickTarget != null ? "등록됨" : "생성 실패")} " +
                $"OS사각형=x{targetOs.x:F0},y{targetOs.y:F0},w{targetOs.width:F0},h{targetOs.height:F0} " +
                $"(중심 {targetOs.center.x:F0},{targetOs.center.y:F0}).");
        }

        private Tile CreateTile(int index, float restLocalY, Color ink)
        {
            var go = new GameObject($"Tile{index}");
            go.transform.SetParent(_stackRoot, false);
            go.transform.localPosition = new Vector3(0f, restLocalY, 0f);

            float halfW = TileWidth * 0.5f;
            float halfH = TileHeight * 0.5f;

            // 흰 채움 = "두께가 판자 높이인 흰색 선분 1개". 캡을 각지게(numCapVertices=0) 해서 정확한
            // 직사각형이 되게 한다 — 둥근 캡을 쓰면 좌우 끝이 알약 모양으로 삐져나온다.
            var fill = CreateLine(go.transform, "Fill", FillColor, TileHeight, SortingFill, loop: false, capVertices: 0);
            fill.positionCount = 2;
            fill.SetPosition(0, new Vector3(-halfW + StrokeWidth * 0.5f, 0f, 0f));
            fill.SetPosition(1, new Vector3(halfW - StrokeWidth * 0.5f, 0f, 0f));

            var stroke = CreateLine(go.transform, "Stroke", ink, StrokeWidth, SortingStroke, loop: true, capVertices: 2);
            SetRectangle(stroke, halfW, halfH);

            return new Tile { Root = go.transform, Fill = fill, Stroke = stroke, RestLocalY = restLocalY };
        }

        private void CreateGauge(Color ink)
        {
            var go = new GameObject("ChargeGauge");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = new Vector3(0f, StackBaseHeight - GaugeDropBelowStack, 0f);

            float halfW = GaugeWidth * 0.5f;
            float halfH = GaugeHeight * 0.5f;
            float inner = GaugeHeight - StrokeWidth; // 테두리 안쪽 여백을 뺀 실제 채움 두께.

            _gaugeTrackFill = CreateLine(go.transform, "TrackFill", FillColor, inner, SortingGaugeTrack, loop: false, capVertices: 0);
            _gaugeTrackFill.positionCount = 2;
            _gaugeTrackFill.SetPosition(0, new Vector3(-halfW, 0f, 0f));
            _gaugeTrackFill.SetPosition(1, new Vector3(halfW, 0f, 0f));

            // 스위트스팟 구간 — 검은 채움보다 위에 얹어, 채움이 그 아래를 지나가도 정답 구간 표시는
            // 절대 가려지지 않는다("어디까지 찼는지"와 "어디가 정답 구간인지"가 동시에 읽힌다).
            _gaugeSweetSpot = CreateLine(go.transform, "SweetSpot", SweetSpotIdleColor, inner, SortingGaugeSweetSpot, loop: false, capVertices: 0);
            _gaugeSweetSpot.positionCount = 2;
            float sweetA = -halfW + GaugeWidth * SweetStart();
            float sweetB = -halfW + GaugeWidth * SweetEnd();
            _gaugeSweetSpot.SetPosition(0, new Vector3(sweetA, 0f, 0f));
            _gaugeSweetSpot.SetPosition(1, new Vector3(sweetB, 0f, 0f));

            _gaugeFill = CreateLine(go.transform, "Fill", ink, inner, SortingGaugeFill, loop: false, capVertices: 0);
            _gaugeFill.positionCount = 2;
            _gaugeFill.SetPosition(0, new Vector3(-halfW, 0f, 0f));
            _gaugeFill.SetPosition(1, new Vector3(-halfW, 0f, 0f));

            _gaugeTrackStroke = CreateLine(go.transform, "TrackStroke", ink, StrokeWidth, SortingGaugeStroke, loop: true, capVertices: 2);
            SetRectangle(_gaugeTrackStroke, halfW, halfH);
        }

        /// <summary>
        /// 판자 스택과 게이지를 한 번에 덮는 클릭 표적. 이 사각형 위에서만 클릭관통이 풀린다 —
        /// 캐릭터에서 멀리 떨어진 빈 공간까지 잡지 않도록 실제 그려진 것의 바운딩에 맞춘다
        /// (SceneBootstrapper의 GrabArea 주석과 같은 판단 기준).
        /// </summary>
        private void CreateClickTarget()
        {
            float top = StackBaseHeight + (TileCount - 1) * (TileHeight + TileGap) + TileHeight * 0.5f;
            float bottom = StackBaseHeight - GaugeDropBelowStack - GaugeHeight * 0.5f;
            const float padding = 0.12f;

            var go = new GameObject("BattleClickTarget");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0f);

            _clickTarget = go.AddComponent<BoxCollider2D>();
            _clickTarget.isTrigger = true; // 히트테스트에는 잡히고 물리 충돌은 절대 일으키지 않는다.
            _clickTarget.size = new Vector2(
                Mathf.Max(TileWidth, GaugeWidth) + padding * 2f,
                Mathf.Max(0.1f, top - bottom) + padding * 2f);

            _hitbox?.RegisterExtraCollider(_clickTarget);
        }

        private void DestroyClickTarget()
        {
            if (_clickTarget == null) return;
            _hitbox?.UnregisterExtraCollider(_clickTarget);
            Destroy(_clickTarget.gameObject);
            _clickTarget = null;
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            if (_mode == Mode.None) return;

            _elapsed += Time.deltaTime;
            _modeTimer += Time.deltaTime;

            switch (_mode)
            {
                case Mode.Playing:
                    TickTiles(1f);
                    TickGauge();
                    break;

                case Mode.Shatter:
                    TickShards();
                    TickImpactBurst();
                    HideGauge();
                    if (_modeTimer >= ShatterSeconds) Teardown();
                    break;

                case Mode.Retreat:
                {
                    // "민망한 퇴장" — 쪼그라들며 옅어진다.
                    float t = Mathf.Clamp01(_modeTimer / RetreatSeconds);
                    float scale = Mathf.Lerp(1f, 0.35f, t * t);
                    if (_stackRoot != null) _stackRoot.localScale = new Vector3(scale, scale, 1f);
                    TickTiles(1f - t);
                    HideGauge();
                    if (_modeTimer >= RetreatSeconds) Teardown();
                    break;
                }

                case Mode.PlainFade:
                {
                    float t = Mathf.Clamp01(_modeTimer / PlainFadeSeconds);
                    TickTiles(1f - t);
                    HideGauge();
                    if (_modeTimer >= PlainFadeSeconds) Teardown();
                    break;
                }
            }
        }

        private void TickTiles(float alpha)
        {
            float shakeOffset = 0f;
            if (_failShakeTimer > 0f)
            {
                _failShakeTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(_failShakeTimer / FailShakeSeconds);
                // 감쇠 sin — 세게 시작해 빠르게 잦아든다("버텼다"는 느낌).
                shakeOffset = Mathf.Sin(_failShakeTimer * 46f) * FailShakeAmplitude * k;
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                if (tile == null || tile.Root == null) continue;

                // 순차 낙하: 자기 차례가 오기 전에는 위에 떠 있고, 오면 감속하며 내려앉는다.
                float local = _elapsed - tile.SpawnDelay;
                float drop;
                float spawnAlpha;
                if (local <= 0f)
                {
                    drop = SpawnDropHeight;
                    spawnAlpha = 0f;
                }
                else if (local < SpawnDropSeconds)
                {
                    float t = local / SpawnDropSeconds;
                    // ease-out quad — 중력 낙하 후 부드럽게 안착하는 느낌.
                    drop = SpawnDropHeight * (1f - t) * (1f - t);
                    spawnAlpha = Mathf.Clamp01(t * 2f);
                }
                else
                {
                    // 착지 후 "살짝 흔들림"(10절) — 장마다 위상을 어긋나게 해 기계적으로 보이지 않게 한다.
                    drop = Mathf.Sin((_elapsed + i * 0.8f) * IdleWobbleSpeed) * IdleWobbleAmplitude;
                    spawnAlpha = 1f;
                }

                tile.Root.localPosition = new Vector3(shakeOffset, tile.RestLocalY + drop, 0f);
                float a = alpha * spawnAlpha;
                SetLineAlpha(tile.Fill, a);
                SetLineAlpha(tile.Stroke, a);
            }
        }

        private void TickGauge()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null) return;

            bool visible = blackboard.BattleChargeGaugeVisible;
            SetGaugeAlpha(visible ? 1f : 0f);
            if (!visible) return;

            float ratio = Mathf.Clamp01(blackboard.BattleChargeRatio);
            float halfW = GaugeWidth * 0.5f;
            if (_gaugeFill != null)
            {
                _gaugeFill.SetPosition(1, new Vector3(-halfW + GaugeWidth * ratio, 0f, 0f));
            }

            // 스위트스팟 신호 — "지금 누르면 성공"인 동안에만 뜨겁게 바뀌고 굵어지며 깜빡인다.
            if (_gaugeSweetSpot == null) return;
            bool hot = ratio >= SweetStart() && ratio <= SweetEnd();
            float inner = GaugeHeight - StrokeWidth;
            if (hot)
            {
                float blink = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(Time.time * 26f));
                Color c = SweetSpotHotColor;
                c.a = blink;
                _gaugeSweetSpot.startColor = c;
                _gaugeSweetSpot.endColor = c;
                _gaugeSweetSpot.startWidth = inner * 1.5f; // 트랙 밖으로 살짝 삐져나와 눈에 확 띈다.
                _gaugeSweetSpot.endWidth = inner * 1.5f;
            }
            else
            {
                _gaugeSweetSpot.startColor = SweetSpotIdleColor;
                _gaugeSweetSpot.endColor = SweetSpotIdleColor;
                _gaugeSweetSpot.startWidth = inner;
                _gaugeSweetSpot.endWidth = inner;
            }
        }

        private void HideGauge() => SetGaugeAlpha(0f);

        private void SetGaugeAlpha(float alpha)
        {
            SetLineAlpha(_gaugeTrackFill, alpha);
            SetLineAlpha(_gaugeTrackStroke, alpha);
            SetLineAlpha(_gaugeSweetSpot, alpha);
            SetLineAlpha(_gaugeFill, alpha);
        }

        // ==================== 성공: 산산조각 ====================

        private void Shatter()
        {
            if (_container == null) return;

            Color ink = ResolveInk();
            float topTileY = StackBaseHeight + (TileCount - 1) * (TileHeight + TileGap);
            var origin = new Vector3(0f, (StackBaseHeight + topTileY) * 0.5f, 0f);

            // 원래 판자는 즉시 사라지고 그 자리를 파편이 대신한다 — "산산조각"이므로 원본이 남으면 안 된다.
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i]?.Root != null) Destroy(_tiles[i].Root.gameObject);
            }
            _tiles.Clear();

            var shardRoot = new GameObject("Shards").transform;
            shardRoot.SetParent(_container.transform, false);

            for (int i = 0; i < ShardCount; i++)
            {
                var go = new GameObject($"Shard{i}");
                go.transform.SetParent(shardRoot, false);
                // 파편은 판자가 있던 사각형 안 아무 곳에서나 튀어나온다.
                go.transform.localPosition = origin + new Vector3(
                    Random.Range(-TileWidth * 0.5f, TileWidth * 0.5f),
                    Random.Range(-(topTileY - StackBaseHeight) * 0.5f - TileHeight * 0.5f,
                                  (topTileY - StackBaseHeight) * 0.5f + TileHeight * 0.5f),
                    0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                var line = CreateLine(go.transform, "Line", ink, StrokeWidth * 0.85f, SortingStroke, loop: false, capVertices: 2);
                float len = Random.Range(ShardLengthMin, ShardLengthMax);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(-len * 0.5f, 0f, 0f));
                line.SetPosition(1, new Vector3(len * 0.5f, 0f, 0f));

                // 정권지르기 방향(캐릭터가 보는 쪽)으로 치우친 부채꼴로 흩어진다 — 무작위 360도보다
                // "맞아서 날아갔다"는 인과가 훨씬 잘 읽힌다.
                float angle = Random.Range(-70f, 70f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle) * _facing, Mathf.Sin(angle));
                _shards.Add(new Shard
                {
                    Root = go.transform,
                    Line = line,
                    Velocity = dir * Random.Range(ShardSpeedMin, ShardSpeedMax) + Vector2.up * 1.2f,
                    Spin = Random.Range(-ShardSpinMax, ShardSpinMax),
                });
            }

            CreateImpactBurst(origin, ink);
            BeginMode(Mode.Shatter);

            Debug.Log($"[격파] 성공 연출 — 판자를 파편 {ShardCount}조각으로 흩고 타격점 임팩트 선 " +
                $"{ImpactRayCount}줄을 그렸습니다(캐릭터 레이어 국소 효과, 유저의 실제 창은 흔들지 않음).");
        }

        /// <summary>타격점에서 방사형으로 뻗는 짧은 선 다발. 화면 흔들림 대신 쓰는 "국소 타격감"이다
        /// (10절 명시: 화면 흔들림은 캐릭터 레이어에 한정하고 유저의 실제 창은 절대 흔들지 않는다).</summary>
        private void CreateImpactBurst(Vector3 localOrigin, Color ink)
        {
            var go = new GameObject("ImpactBurst");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = localOrigin;

            _impactBurst = CreateLine(go.transform, "Rays", ink, StrokeWidth * 0.8f, SortingStroke, loop: false, capVertices: 2);
            // 하나의 LineRenderer로 여러 갈래를 그리기 위해 중심을 매번 되짚는 지그재그 폴리라인을 쓴다
            // (LineRenderer는 선분 집합이 아니라 하나의 연속 선이므로, 중심 복귀가 곧 "갈래 나누기"다).
            _impactBurst.positionCount = ImpactRayCount * 2 + 1;
            for (int i = 0; i < ImpactRayCount; i++)
            {
                float a = (i / (float)ImpactRayCount) * Mathf.PI * 2f + 0.3f;
                var tip = new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, 0f);
                _impactBurst.SetPosition(i * 2, Vector3.zero);
                _impactBurst.SetPosition(i * 2 + 1, tip);
            }
            _impactBurst.SetPosition(ImpactRayCount * 2, Vector3.zero);
        }

        private void TickShards()
        {
            float t = Mathf.Clamp01(_modeTimer / ShatterSeconds);
            float alpha = 1f - t * t; // 처음엔 또렷하다가 끝에서 빠르게 사라진다.

            for (int i = 0; i < _shards.Count; i++)
            {
                Shard s = _shards[i];
                if (s?.Root == null) continue;
                s.Velocity += Vector2.down * (ShardGravity * Time.deltaTime);
                s.Root.localPosition += (Vector3)(s.Velocity * Time.deltaTime);
                s.Root.Rotate(0f, 0f, s.Spin * Time.deltaTime);
                SetLineAlpha(s.Line, alpha);
            }
        }

        private void TickImpactBurst()
        {
            if (_impactBurst == null) return;
            float t = Mathf.Clamp01(_modeTimer / ImpactBurstSeconds);
            if (t >= 1f)
            {
                SetLineAlpha(_impactBurst, 0f);
                return;
            }
            // 빠르게 퍼지며 사라진다.
            float scale = Mathf.Lerp(0.35f, 1.35f, t);
            _impactBurst.transform.parent.localScale = new Vector3(scale, scale, 1f);
            SetLineAlpha(_impactBurst, 1f - t);
        }

        // ==================== 정리 ====================

        private void Teardown()
        {
            DestroyClickTarget();
            _tiles.Clear();
            _shards.Clear();
            _gaugeTrackFill = null;
            _gaugeTrackStroke = null;
            _gaugeSweetSpot = null;
            _gaugeFill = null;
            _impactBurst = null;
            _stackRoot = null;
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
        }

        // ==================== 공용 헬퍼 ====================

        private float SweetStart()
            => _agent != null && _agent.Config != null ? _agent.Config.battleSweetSpotStart : 0.70f;

        private float SweetEnd()
            => _agent != null && _agent.Config != null ? _agent.Config.battleSweetSpotEnd : 0.85f;

        private Color ResolveInk()
            => _agent != null && _agent.Config != null ? _agent.Config.ResolveInkColor() : Color.black;

        /// <summary>
        /// 캐릭터가 이미 쓰고 있는 LineRenderer 머티리얼(Sprites-Default)을 그대로 빌려 쓴다.
        /// Shader.Find로 런타임에 찾지 않는 이유: 씬/프리팹이 참조하지 않는 셰이더는 빌드에서 스트리핑될
        /// 수 있어 스탠드얼론에서만 분홍색으로 깨질 위험이 있다. 캐릭터 획과 완전히 같은 머티리얼을
        /// 쓰면 그 위험이 원천적으로 없고, 투명 창에서의 알파 합성 거동도 검증된 것과 동일해진다.
        /// </summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
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
            // 그 밖의 설정(alignment/textureMode 등)은 기본값 그대로 둔다 — 캐릭터 획을 만드는
            // Assets/Editor/SceneBootstrapper.cs의 ConfigureLine()과 정확히 같은 조합이라야 투명 창에서의
            // 알파 합성 거동이 이미 검증된 것과 동일해진다.
            return lr;
        }

        private static void SetRectangle(LineRenderer lr, float halfW, float halfH)
        {
            lr.positionCount = 4;
            lr.SetPosition(0, new Vector3(-halfW, -halfH, 0f));
            lr.SetPosition(1, new Vector3(halfW, -halfH, 0f));
            lr.SetPosition(2, new Vector3(halfW, halfH, 0f));
            lr.SetPosition(3, new Vector3(-halfW, halfH, 0f));
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
