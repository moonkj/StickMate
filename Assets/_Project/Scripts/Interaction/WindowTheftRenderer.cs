using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 윈도우 창 도둑 시각 레이어 — docs/UX_FLOW.md 27-1절의 "유저가 보는 것"을 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가 (DialogueBubbleRenderer/BattleMinigameRenderer/GraffitiRenderer와 같은 이야기)
    /// ============================================================================
    /// Interaction/WindowTheftDirector.cs(트리거·대상 선정·취소 감시)와 States/WindowTheftState.cs
    /// (2회 시도 후 포기하는 페이즈 + 포기 대사 self-transition)는 진작 완성돼 있었는데,
    /// <b>StickmanEventBus.WindowTheftOverlayChanged를 구독하는 코드가 어디에도 없었고</b> Director 자신도
    /// 씬/프리팹 어디에도 배치돼 있지 않았다. 즉 대상 창 선정도, 2회 시도도, 취소 감시도 전부 코드로만
    /// 존재하고 화면에는 단 한 픽셀도 나오지 않았다. 이 컴포넌트가 그 빠진 조각이며, Director/State
    /// 로직은 한 줄도 바꾸지 않는다.
    ///
    /// ============================================================================
    /// 절대 원칙 3(유저 자산 불변) — 이 클래스가 하지 않는 일 (27-1 / 27-7 체크리스트)
    /// ============================================================================
    /// 대상 창을 <b>1픽셀도 움직이지 않는다</b>. 이 파일에는 창 위치/크기를 바꾸는 API가 존재하지 않으며
    /// (SetWindowPos/MoveWindow류, macOS 창 프레임 변경 API — Tests/EditMode/UserAssetImmutabilityAuditTests.cs가
    /// 정적으로 스캔한다), 애초에 이 클래스가 아는 것은 Director가 <b>읽기 전용 열거</b>로 얻어 스냅샷해
    /// 넘겨준 사각형 좌표 하나뿐이다.
    ///
    /// 그럼 무엇이 움직이는가: <b>창의 겉모습을 흉내낸 복사본(고스트) 사각형</b>이다. 진짜 창 위에 얇은
    /// 테두리 + 가짜 타이틀바 + 신호등 점 3개로 "창처럼 보이는 윤곽"을 한 겹 겹쳐 그리고, 캐릭터가
    /// 안간힘을 쓰는 동안 <b>그 복사본만</b> 캐릭터 쪽으로 몇 픽셀 딸려왔다가 부들부들 떨며 되돌아간다.
    /// 진짜 창은 그 밑에서 처음부터 끝까지 미동도 없다 — 27-1이 "성공 케이스 자체가 설계에 없다"고
    /// 못박은 그 개그가 시각적으로 성립하려면, 흔들리는 것과 안 흔들리는 것이 둘 다 보여야 한다.
    ///
    /// 콜라이더를 단 하나도 만들지 않는다(관전 전용, 27-1 "클릭관통 유지"). 그래서 이 오버레이가 떠
    /// 있는 동안에도 유저는 그 창을 평소처럼 클릭/드래그할 수 있고, 실제로 드래그하면
    /// WindowTheftDirector.MonitorTarget()이 좌표 변화를 감지해 Cancelled를 발행 → 이 렌더러가 즉시
    /// 손을 떼며 걷힌다.
    ///
    /// ============================================================================
    /// 연출 타임라인 (27-1 "유저가 보는 것")
    /// ============================================================================
    /// Started    -> 고스트 창 윤곽 생성. 캐릭터가 있는 쪽 세로 모서리에 "붙잡은 손" 힘줄 표시 3개가
    ///               생기고, 발밑에서 먼지가 주기적으로 퍼진다. 고스트는 캐릭터 쪽으로 당겨졌다
    ///               튕겨 돌아오기를 반복한다(<see cref="TugPulseSpeed"/>, 여기에 부들거림 지터).
    /// 포기 순간   -> WindowTheftState가 2회 시도를 소진하고 <b>자기 자신에게 재전이</b>하는 그 프레임
    ///               (From==To==WindowTheft)을 신호로 받아, 고스트가 제자리로 스르륵 돌아가고 힘줄/먼지가
    ///               멎는다. 이 타이밍을 렌더러가 자체 타이머로 다시 세지 않는 것이 중요하다 —
    ///               GraffitiRenderer의 Holding 주석과 같은 이유로, 두 곳에서 같은 시간을 세면 어긋난다.
    /// Completed  -> 포기 대사가 끝나고 Idle로 빠져나감. 천천히 페이드아웃.
    /// Cancelled  -> 유저가 그 창을 실제로 움직였거나/창이 닫혔거나/긴급정지. 훨씬 빠르게 걷어낸다
    ///               (유저가 지금 그 창을 만지고 있으므로 방해 시간을 최소화한다).
    /// </summary>
    public sealed class WindowTheftRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const float FadeOutSeconds = 0.55f;     // 정상 종료(포기 후 Idle 복귀).
        private const float CancelFadeSeconds = 0.14f;  // 유저가 그 창을 실제로 조작 중 — 최대한 빨리 사라진다.
        private const float SettleBackSeconds = 0.45f;  // 포기 직후 고스트가 제자리로 돌아가는 시간.

        private const float TugPulseSpeed = 7.2f;       // 당겼다 놓는 안간힘의 리듬(rad/s).
        private const float TugOffsetRatio = 0.055f;    // 창 가로 대비 최대로 딸려오는 거리(진짜 창은 0).
        private const float TugOffsetMaxWorld = 0.42f;  // 큰 창에서도 과하게 끌려가 보이지 않게 하는 상한.
        private const float JitterAmplitude = 0.028f;   // "부들부들" — 매 프레임 흔들림(월드 유닛).

        private const float StrokeWidthRatio = 0.012f;  // 창 짧은 변 대비 테두리 굵기.
        private const float StrokeWidthMin = 0.035f;
        private const float StrokeWidthMax = 0.10f;
        private const float TitleBarHeightRatio = 0.16f; // 창 세로 대비 가짜 타이틀바 높이.
        private const float TitleBarHeightMaxWorld = 0.55f;
        private const int TrafficLightCount = 3;         // macOS 창처럼 보이게 하는 신호등 점.

        private const int StrainMarkCount = 3;           // 붙잡은 손 옆의 힘줄 표시.

        /// <summary>캐릭터 발(루트 원점)에서 손까지의 높이(월드 유닛). 전신 높이 2.27, 머리 중심 앵커
        /// 2.05인 이 스틱맨 비율에서 어깨가 약 1.55, 앞으로 뻗은 손이 약 1.45다
        /// (Dialogue/DialogueBubbleRenderer의 머리 앵커 주석과 같은 실측 계열 값).</summary>
        private const float HandHeightAboveFeetWorld = 1.45f;
        // 14 -> 8: 초당 두 번 이상 깜빡이던 것을 눈에 거슬리지 않는 속도로 낮춘다(위 색 변경과 같은 이유).
        private const float StrainPulseSpeed = 8f;

        private const float DustSpawnInterval = 0.17f;   // 발밑 먼지 퍼프 생성 주기.
        private const float DustLifeSeconds = 0.52f;
        private const float DustStartRadius = 0.06f;
        private const float DustEndRadius = 0.34f;
        private const int DustSegments = 10;
        private const int DustMaxAlive = 8;              // 24시간 상주 앱 — 상한을 두어 무한 증식을 막는다.

        // 캐릭터 획(0~5)보다 뒤 = 캐릭터가 창 앞에 서서 붙잡고 있는 것처럼 보인다.
        private const int SortingGhost = -2;
        // 힘줄/먼지는 손과 발에서 나오는 것이라 캐릭터 앞쪽(그라피티 9보다 아래).
        private const int SortingEffect = 7;

        private static readonly Color GhostFrameColor = new Color(0.24f, 0.52f, 0.92f, 0.95f);
        private static readonly Color GhostTitleColor = new Color(0.24f, 0.52f, 0.92f, 0.60f);
        // ★ 2026-08-29 — 사용자 신고 "양손에 무슨 노란색이 있는데 그것도 이상함".
        // 예전 값은 채도 높은 노랑(0.98, 0.78, 0.20)이었다. 이 앱은 흑백 선화 톤이고 사용자는 프로젝트
        // 내내 "깔끔한 졸라맨"을 요구해왔다(과거 신고: "손과 발에 동그란 뭉치같은건 필요없을거 같은데",
        // "눈도 너무 커서 이상함"). 화면에서 유일하게 원색인 요소가 하필 캐릭터 손 옆에 붙어 있으니
        // 시선이 전부 거기로 끌린다 — 정보량은 거의 없는데 가장 튀는, 최악의 조합이었다.
        //
        // 지우지 않고 **잉크색 연동**을 고른 이유: 만화의 힘줄/효과선은 원래 잉크로 그리는 것이라
        // 이 앱의 톤에 정확히 맞고, 말풍선(Dialogue/DialogueBubbleRenderer)이 이미 같은 방식으로
        // StickConfig.ResolveInkColor()를 따라간다 — 흰색/검은색 프리셋을 바꿔도 자동으로 함께 간다.
        // 알파도 1.0에서 낮춰(StrainMaxAlpha) "있는지 없는지 모르게 거들기만" 하는 강도로 내렸다.
        private const float StrainMaxAlpha = 0.5f;
        private Color _strainColor = new Color(0f, 0f, 0f, StrainMaxAlpha);
        private static readonly Color DustColor = new Color(0.62f, 0.60f, 0.56f, 0.85f);

        private enum Mode { None, Straining, SettlingBack, FadingOut }

        private sealed class Puff
        {
            public LineRenderer Line;
            public Transform Root;
            public float Age;
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 사본도 이 컴포넌트를 함께 갖게 되는데,
        /// 씬 폴백을 두면 사본 렌더러가 플레이어의 이벤트에 반응해 고스트 창이 두 벌 그려진다
        /// (2026-08-29 격파 미니게임에서 실측으로 확인된 버그 — GraffitiRenderer/BattleMinigameRenderer의
        /// 같은 가드 주석 참고). 애초에 사본에 배치하지 않는 것이 1차 방어이고,
        /// 이 가드가 2차 방어다.
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _fadeSeconds = FadeOutSeconds;

        private GameObject _container;
        private Vector3 _restPosition;      // 진짜 창 좌표에 대응하는 고스트의 "제자리"(여기서 절대 벗어나 굳지 않는다).
        private float _pullSign = -1f;      // 캐릭터가 있는 쪽(+1 = 오른쪽으로 당김).
        private float _tugAmplitude;
        private float _settleFromOffset;    // 포기 시점의 오프셋(제자리로 되돌리는 보간 시작값).

        private readonly List<LineRenderer> _ghostLines = new List<LineRenderer>(8);
        private readonly List<LineRenderer> _strainLines = new List<LineRenderer>(StrainMarkCount);
        private readonly List<Puff> _puffs = new List<Puff>(DustMaxAlive);
        private float _dustTimer;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 화면에 고스트 창 오버레이가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        /// <summary>
        /// 이 오버레이가 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다
        /// (PlayMode 테스트가 "이벤트를 받으면 진짜로 오브젝트를 만들고, 끝나면 진짜로 전부 지운다"를
        /// 절대 조건으로 단언하는 데 쓴다 — 이 프로젝트가 4번 연속으로 놓친 실패 모드가 정확히 이것이다).
        /// </summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>
        /// 이 오버레이가 만든 콜라이더 수 — <b>항상 0이어야 한다</b>. 27-1이 창 도둑을 명시적으로
        /// <b>"관전 전용"</b>으로 분류하기 때문이다("기본은 관전 전용(클릭관통 유지, 이 이벤트를 위한
        /// 부분적 클릭관통 해제 없음)"). 부분적 클릭관통 해제(15절)를 쓰는 것은 유저가 직접 개입하는
        /// 10/12/13/14절뿐이고 창 도둑은 그 목록에 없다 — 같은 27절의 27-2~27-5(청소부/그라피티/
        /// 크래시/블랙홀)와 같은 부류다.
        /// ★ 2026-08-30 R3-m4 — 예전 문장은 이 분류를 "11절과 같은"이라고 적어 두었는데, 그 11절
        /// (라이벌 대결)은 같은 날 기능 전체가 삭제되어 "(삭제)"만 남았다. 살아 있는 근거(27-1 본문과
        /// 15절의 대상 목록)로 바꿔 적는다.
        /// </summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable()
        {
            StickmanEventBus.WindowTheftOverlayChanged += OnOverlayChanged;
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
        }

        private void OnDisable()
        {
            StickmanEventBus.WindowTheftOverlayChanged -= OnOverlayChanged;
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            // 이 컴포넌트가 꺼질 때 고스트 창이 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnOverlayChanged(WindowTheftOverlayEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            switch (evt.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    Begin(evt.TargetRectOsScreen);
                    break;
                case SpectacleOverlayPhase.Completed:
                    BeginFade(FadeOutSeconds, "포기하고 물러남(정상 종료)");
                    break;
                case SpectacleOverlayPhase.Cancelled:
                    BeginFade(CancelFadeSeconds, "취소(유저가 창을 실제로 조작/창이 닫힘/긴급정지)");
                    break;
            }
        }

        /// <summary>
        /// 포기 순간만 상태 머신에서 직접 받는다. WindowTheftState는 2회 시도를 소진하면 <b>자기 자신에게</b>
        /// 재전이해 포기 대사를 파생시키므로(From==To==WindowTheft), 그 self-transition이야말로 "안간힘이
        /// 끝났다"는 확정 신호다 — 렌더러가 windowTheftAttemptDuration을 따로 세어 흉내 내면 반드시 어긋난다.
        /// </summary>
        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (_agent == null) return;
            if (_mode != Mode.Straining) return;
            if (evt.From != StickmanStateId.WindowTheft || evt.To != StickmanStateId.WindowTheft) return;

            _settleFromOffset = _container != null ? _container.transform.position.x - _restPosition.x : 0f;
            _mode = Mode.SettlingBack;
            _modeTimer = 0f;
            Debug.Log("[창도둑] 2회 시도 소진 — 고스트 창이 제자리로 돌아가고 힘줄/먼지가 멎습니다. " +
                "(진짜 창은 처음부터 지금까지 단 1픽셀도 움직이지 않았다 — 27-1의 개그 포인트 그 자체.)");
        }

        // ==================== 생성 ====================

        private void Begin(Rect targetRectOsScreen)
        {
            Teardown();

            var blackboard = _agent != null ? _agent.Blackboard : null;
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam == null || blackboard.Body == null)
            {
                Debug.LogWarning("[창도둑] 고스트 창을 그리지 못했습니다 — 카메라/캐릭터 배선이 없습니다.");
                return;
            }

            // OS 화면 사각형 -> 월드 사각형. cameraDepth는 임의값을 넣으면 안 되고 반드시 왕복에 쓸
            // 값을 그대로 재사용해야 한다(Platform/ScreenCoordinateConverter.cs "왕복 정밀도" 참고).
            Vector3 characterWorld = blackboard.Body.position;
            ScreenCoordinateConverter.WorldToOsScreen(cam, characterWorld, blackboard.Config, out float depth);
            Vector3 cornerA = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(targetRectOsScreen.xMin, targetRectOsScreen.yMin), depth, blackboard.Config);
            Vector3 cornerB = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(targetRectOsScreen.xMax, targetRectOsScreen.yMax), depth, blackboard.Config);

            float xMin = Mathf.Min(cornerA.x, cornerB.x);
            float xMax = Mathf.Max(cornerA.x, cornerB.x);
            float yMin = Mathf.Min(cornerA.y, cornerB.y);
            float yMax = Mathf.Max(cornerA.y, cornerB.y);
            float sizeX = Mathf.Max(0.05f, xMax - xMin);
            float sizeY = Mathf.Max(0.05f, yMax - yMin);

            // 힘줄 표시 색을 이번 발동 시점의 잉크 프리셋으로 확정한다(말풍선과 같은 방식 —
            // Dialogue/DialogueBubbleRenderer의 ResolveInkColor() 사용부 참고).
            Color ink = blackboard.Config != null ? blackboard.Config.ResolveInkColor() : Color.black;
            ink.a = StrainMaxAlpha;
            _strainColor = ink;

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("WindowTheftGhostOverlay");
            _container.transform.SetParent(null, false);
            _restPosition = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
            _container.transform.position = _restPosition;

            // 캐릭터가 창의 어느 쪽에 서 있는지 -> 그쪽으로 당긴다(캐릭터가 창 한가운데 있으면 바라보는 방향).
            float dx = characterWorld.x - _restPosition.x;
            _pullSign = Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : Mathf.Sign(blackboard.FacingSign == 0f ? 1f : blackboard.FacingSign);
            _tugAmplitude = Mathf.Min(sizeX * TugOffsetRatio, TugOffsetMaxWorld);

            float stroke = Mathf.Clamp(Mathf.Min(sizeX, sizeY) * StrokeWidthRatio, StrokeWidthMin, StrokeWidthMax);
            float halfX = sizeX * 0.5f;
            float halfY = sizeY * 0.5f;

            // (1) 창 테두리 — 이것이 "복사본 사각형"의 본체다.
            _ghostLines.Add(CreateLine("GhostFrame", new[]
            {
                new Vector3(-halfX, -halfY, 0f),
                new Vector3(halfX, -halfY, 0f),
                new Vector3(halfX, halfY, 0f),
                new Vector3(-halfX, halfY, 0f),
            }, GhostFrameColor, stroke, SortingGhost, loop: true));

            // (2) 가짜 타이틀바 — 사각형 하나만 있으면 "창"으로 읽히지 않는다.
            float titleH = Mathf.Min(sizeY * TitleBarHeightRatio, TitleBarHeightMaxWorld);
            float titleY = halfY - titleH;
            _ghostLines.Add(CreateLine("GhostTitleBar", new[]
            {
                new Vector3(-halfX, titleY, 0f),
                new Vector3(halfX, titleY, 0f),
            }, GhostTitleColor, stroke, SortingGhost, loop: false));

            // (3) 신호등 점 3개(macOS 창 은유).
            float dotRadius = Mathf.Min(titleH * 0.22f, stroke * 2.2f);
            float dotY = halfY - titleH * 0.5f;
            for (int i = 0; i < TrafficLightCount; i++)
            {
                float dotX = -halfX + titleH * (0.55f + i * 0.62f);
                if (dotX + dotRadius > halfX) break; // 아주 좁은 창에서는 신호등을 생략한다.
                _ghostLines.Add(CreateLine($"GhostDot{i}", BuildCircle(new Vector3(dotX, dotY, 0f), dotRadius, 8),
                    GhostTitleColor, stroke * 0.9f, SortingGhost, loop: true));
            }

            // (4) 붙잡은 손 옆의 힘줄 표시 — 캐릭터 쪽 세로 모서리, **캐릭터 손 높이**에 붙는다.
            //
            // ★ 2026-08-29 위치 수정 — 사용자는 "양손에 노란색이 있다"고 했는데, 예전 코드는 마크를
            // 고스트 창 세로 모서리의 **한가운데**(markY = t * min(sizeY*0.34, 1.1))에 걸었다. 창 높이에만
            // 비례하는 값이라 캐릭터 손 높이와는 아무 관계가 없었고, 큰 창에서는 캐릭터 머리 위나 발밑
            // 엉뚱한 높이에 떠 있었다. "붙잡은 손 옆"이라는 이 마크의 존재 이유 자체가 성립하지 않았던 것.
            // 이제는 캐릭터 발 좌표(characterWorld.y)에서 손 높이만큼 올린 지점을 중심으로 삼고,
            // 그 결과가 고스트 창 세로 범위를 벗어나면 창 안으로 클램프한다(창 밖 허공에 그리지 않는다).
            float grabEdgeX = halfX * _pullSign;
            float markLength = Mathf.Min(sizeY * 0.10f, 0.36f);
            float handWorldY = characterWorld.y + HandHeightAboveFeetWorld;
            float handLocalY = Mathf.Clamp(handWorldY - _restPosition.y, -halfY + markLength, halfY - markLength);
            float markSpacing = Mathf.Min(markLength * 0.9f, halfY * 0.5f);
            for (int i = 0; i < StrainMarkCount; i++)
            {
                float t = (i - (StrainMarkCount - 1) * 0.5f) / Mathf.Max(1f, StrainMarkCount - 1);
                float markY = handLocalY + t * markSpacing * (StrainMarkCount - 1);
                float inner = grabEdgeX + _pullSign * markLength * 0.35f;
                float outer = grabEdgeX + _pullSign * markLength * 1.25f;
                _strainLines.Add(CreateLine($"StrainMark{i}", new[]
                {
                    new Vector3(inner, markY, 0f),
                    new Vector3(outer, markY + markLength * 0.35f * Mathf.Sign(t == 0f ? 1f : t), 0f),
                }, _strainColor, stroke * 1.1f, SortingEffect, loop: false));
            }

            _mode = Mode.Straining;
            _modeTimer = 0f;
            _dustTimer = 0f;

            Debug.Log($"[창도둑] 복사본(고스트) 창 오버레이 생성 — OS영역 {targetRectOsScreen}, " +
                $"월드중심 {_restPosition}, 월드크기 {sizeX:F2}x{sizeY:F2}, 당기는 방향 {(_pullSign > 0f ? "오른쪽" : "왼쪽")}, " +
                $"최대 끌림 {_tugAmplitude:F3}유닛, 시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0). " +
                "★ 움직이는 것은 이 복사본뿐이며 진짜 창의 좌표/크기를 바꾸는 API는 이 경로에 존재하지 않는다(원칙 3).");
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            if (_mode == Mode.None) return;
            _modeTimer += Time.deltaTime;

            switch (_mode)
            {
                case Mode.Straining:
                {
                    // |sin|의 반복 = 당겼다 놓았다를 반복하는 안간힘. 2회차/1회차를 렌더러가 구분하지
                    // 않는 이유는 클래스 문서의 "두 곳에서 같은 시간을 세지 않는다" 참고.
                    float strain = Mathf.Abs(Mathf.Sin(_modeTimer * TugPulseSpeed));
                    ApplyGhostOffset(_pullSign * _tugAmplitude * strain, jitter: true);
                    PulseStrainMarks(strain);
                    SpawnDustIfDue();
                    break;
                }

                case Mode.SettlingBack:
                {
                    float t = Mathf.Clamp01(_modeTimer / SettleBackSeconds);
                    // 제자리로 스르륵(SmoothStep) — 튕겨 돌아가면 "창이 반동으로 움직였다"로 읽힐 수 있다.
                    ApplyGhostOffset(Mathf.Lerp(_settleFromOffset, 0f, Mathf.SmoothStep(0f, 1f, t)), jitter: false);
                    PulseStrainMarks(1f - t);
                    break;
                }

                case Mode.FadingOut:
                {
                    float t = Mathf.Clamp01(_modeTimer / _fadeSeconds);
                    ApplyGhostOffset(Mathf.Lerp(_settleFromOffset, 0f, t), jitter: false);
                    SetAlpha(1f - t);
                    if (t >= 1f) { Teardown(); return; }
                    break;
                }
            }

            TickDust();
        }

        private void ApplyGhostOffset(float offsetX, bool jitter)
        {
            if (_container == null) return;
            float jx = jitter ? Random.Range(-JitterAmplitude, JitterAmplitude) : 0f;
            float jy = jitter ? Random.Range(-JitterAmplitude, JitterAmplitude) : 0f;
            _container.transform.position = new Vector3(_restPosition.x + offsetX + jx, _restPosition.y + jy, _restPosition.z);
        }

        private void PulseStrainMarks(float intensity)
        {
            // 0.35~1.0 진폭 -> 0.55~1.0. 깜빡임 대비를 줄여 "부드럽게 힘주는" 느낌만 남긴다.
            float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_modeTimer * StrainPulseSpeed));
            float alpha = Mathf.Clamp01(intensity) * blink * StrainMaxAlpha;
            for (int i = 0; i < _strainLines.Count; i++)
            {
                LineRenderer lr = _strainLines[i];
                if (lr == null) continue;
                Color c = _strainColor;
                c.a = alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        // ==================== 발밑 먼지 ====================

        private void SpawnDustIfDue()
        {
            _dustTimer += Time.deltaTime;
            if (_dustTimer < DustSpawnInterval) return;
            _dustTimer = 0f;
            if (_puffs.Count >= DustMaxAlive) return;

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null || _container == null) return;

            // 먼지는 캐릭터 발밑에서 난다 — 고스트 창을 따라 흔들리면 안 되므로 컨테이너의 자식으로
            // 두되 생성 시점의 월드 좌표를 로컬로 환산해 굳힌다(컨테이너가 흔들려도 티가 크지 않도록
            // 진폭 자체가 작다).
            Vector3 footWorld = (Vector3)blackboard.Body.position + Vector3.down * 0.05f;
            Vector3 local = _container.transform.InverseTransformPoint(footWorld);
            local.x += Random.Range(-0.14f, 0.14f);

            var go = new GameObject("DustPuff");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = local;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startWidth = 0.035f;
            lr.endWidth = 0.035f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingEffect;
            lr.loop = true;
            lr.positionCount = 0;
            lr.startColor = DustColor;
            lr.endColor = DustColor;

            _puffs.Add(new Puff { Line = lr, Root = go.transform, Age = 0f });
        }

        private void TickDust()
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff p = _puffs[i];
                if (p?.Line == null) { _puffs.RemoveAt(i); continue; }

                p.Age += Time.deltaTime;
                float t = Mathf.Clamp01(p.Age / DustLifeSeconds);
                if (t >= 1f)
                {
                    if (p.Root != null) Destroy(p.Root.gameObject);
                    _puffs.RemoveAt(i);
                    continue;
                }

                float radius = Mathf.Lerp(DustStartRadius, DustEndRadius, t);
                Vector3[] circle = BuildCircle(Vector3.zero, radius, DustSegments);
                p.Line.positionCount = circle.Length;
                p.Line.SetPositions(circle);

                Color c = DustColor;
                c.a = DustColor.a * (1f - t) * CurrentGlobalAlpha();
                p.Line.startColor = c;
                p.Line.endColor = c;
            }
        }

        /// <summary>페이드아웃 중이면 먼지도 함께 옅어지도록 하는 공통 배율.</summary>
        private float CurrentGlobalAlpha()
            => _mode == Mode.FadingOut ? Mathf.Clamp01(1f - _modeTimer / Mathf.Max(0.01f, _fadeSeconds)) : 1f;

        // ==================== 종료 ====================

        private void BeginFade(float seconds, string reason)
        {
            if (_mode == Mode.None || _mode == Mode.FadingOut) return;
            _settleFromOffset = _container != null ? _container.transform.position.x - _restPosition.x : 0f;
            _mode = Mode.FadingOut;
            _modeTimer = 0f;
            _fadeSeconds = Mathf.Max(0.01f, seconds);
            Debug.Log($"[창도둑] 고스트 창 오버레이 정리 시작 — {reason}, {_fadeSeconds:F2}초 페이드아웃. " +
                "(진짜 창은 이 이벤트 내내 원래 자리 그대로였다.)");
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _ghostLines.Count; i++) ApplyAlpha(_ghostLines[i], alpha);
            for (int i = 0; i < _strainLines.Count; i++) ApplyAlpha(_strainLines[i], alpha);
        }

        private static void ApplyAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null) return;
            Color s = lr.startColor;
            Color e = lr.endColor;
            s.a = alpha;
            e.a = alpha;
            lr.startColor = s;
            lr.endColor = e;
        }

        private void Teardown()
        {
            _ghostLines.Clear();
            _strainLines.Clear();
            _puffs.Clear();
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
            _settleFromOffset = 0f;
        }

        // ==================== 도형 유틸 ====================

        private LineRenderer CreateLine(string name, Vector3[] points, Color color, float width, int sortingOrder, bool loop)
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
            lr.sortingOrder = sortingOrder;
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

        /// <summary>GraffitiRenderer/BattleMinigameRenderer와 같은 이유로 캐릭터 LineRenderer의 머티리얼을
        /// 빌려 쓴다(Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
