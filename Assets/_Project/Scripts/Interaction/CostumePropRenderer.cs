using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 코스튬 <b>소환 오브젝트(프롭)</b> — 몰입기 동안 캐릭터 옆에 서 있는 정적 벡터 소품.
    /// 설계 정본은 <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 6절 +
    /// <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 4절.
    ///
    /// ============================================================================
    /// 사용자 요구의 실체 — 「Static 레이어로 1회만 그리고 고정, 매 프레임 재드로우 금지」
    /// ============================================================================
    /// Unity에 「Static 레이어」는 없다. 요구의 실체는 <b>「한 번 만든 뒤 아무도 안 만진다」</b>이고,
    /// 이 저장소에는 그 패턴이 이미 있다 — <c>GraffitiRenderer</c>의 <c>case Mode.Holding: break;</c>
    /// (유지 구간에 쓰기 0줄)와 <c>ArcheryRenderer</c>의 «1회 빌드 + 이벤트 때만 갱신»이다.
    /// <b>램프·책상·광맥벽·마법진은 움직이지 않으므로 갱신이 0이다.</b>
    ///
    /// <para>수명은 <b>정확히 네 시점</b>에만 움직인다:
    /// ① 몰입기 진입 = 빌드 1회 + 앵커 스냅샷 1회 · ② 몰입기 내내 = <b>아무 일도 하지 않는다</b> ·
    /// ③ 한계 구간 진입 = 철거 · ④ 세션 종료 3경로 전부 = 철거.</para>
    ///
    /// ============================================================================
    /// ★ 왜 <c>FocusSessionPhaseChanged</c>를 구독하지 않고 <b>매 프레임 조회</b>하는가
    /// ============================================================================
    /// 그 이벤트는 <b>한 세션에 정확히 2회</b>만 오고(적응기→몰입기, 몰입기→한계)
    /// <b>세션 종료에는 오지 않는다</b> — 세션 경계는 구간 전이가 아니기 때문이다.
    /// 그래서 이벤트만 구독하면 <b>몰입기 도중 취소·긴급정지로 끝난 세션에서 프롭이 화면에 남는다</b>
    /// (위 ④ 위반, 그리고 «창이 닫혔는데 그림만 허공에 남는» 바로 그 형태다).
    /// <see cref="FocusWatchDirector.CurrentPhase"/>는 세션이 끝나면 <b>파생으로</b>
    /// <see cref="FocusSessionPhase.None"/>이 되므로, 그 값 하나를 읽는 것이
    /// <b>세 종료 경로를 모두 덮는 유일한 단일 판정 입력</b>이다.
    /// 판정 입력을 하나로 두는 것이 이 저장소가 반복해서 배운 규칙이다 — 둘로 나누면 갈라진다.
    /// <b>조회는 float 몇 개짜리 순수 계산이라 할당이 없다.</b>
    ///
    /// ============================================================================
    /// P-GHOST — 「없는 것을 향한 자세」를 구조적으로 불가능하게
    /// ============================================================================
    /// <see cref="PropPlaced"/>는 <b>「소환을 요청했다」가 아니라 「실제로 배치됐다」</b>이다.
    /// 폴백 사다리가 F4(미소환)로 떨어지면 이 값이 <c>false</c>이고, 코스튬 포즈 층(P4)은
    /// 이 값만 보고 통째로 꺼져 기존 관망 자세로 돌아간다. 대체 키포즈 표를 따로 두지 않는다 —
    /// 표가 둘이 되면 «어느 표가 지금 유효한가»가 다시 두 파일의 문제가 되고,
    /// 그것이 2026-09-06 <b>삭제된 발밑 링을 향해 세션당 11.5번 절하던</b> 사고의 형태였다.
    ///
    /// ============================================================================
    /// 절대 불변 원칙 확인
    /// ============================================================================
    /// 원칙 2(비침해): 콜라이더 0개(<see cref="ActiveColliderCount"/>가 상시 노출한다) ·
    /// 캐릭터가 숨겨지면 <see cref="SuspendedOverlayGate"/>가 함께 감춘다.
    /// 원칙 3(유저 자산 불변): 창/파일/아이콘을 <b>읽지도 쓰지도 않는다</b> — 이 렌더러가 창에 대해
    /// 아는 것은 «지금 딛고 있는 발판의 좌우 월드 X» 두 숫자뿐이고 그것도 배치 가능 여부 판정에만 쓴다.
    /// </summary>
    public sealed class CostumePropRenderer : MonoBehaviour
    {
        private const string LogPrefix = "[코스튬프롭]";

        /// <summary>프롭은 <b>캐릭터 전체보다 뒤</b>에 그린다(발자국 −2보다도 뒤). 손이 프롭 앞을
        /// 지나가는 그림이 자동으로 나오고, 프롭이 실루엣을 절대 가리지 않는다.</summary>
        private const int SortingPropFill = -8;
        private const int SortingPropLine = -7;

        /// <summary>획 굵기(신장 배수) — 캐릭터 몸통 획과 같다(<c>ArcheryRenderer</c>와 같은 값).</summary>
        private const float StrokeWidthRatio = 0.0339f;

        /// <summary>폴백 F3의 축소율. 작업점도 함께 줄어 <b>도달 원 안으로 더 들어오므로</b>
        /// 손 접촉 여유가 오히려 커진다.</summary>
        private const float ShrunkPlacementScale = 0.70f;

        /// <summary>프롭 바깥 끝과 발판/화면 경계 사이에 남기는 여유(신장 배수).</summary>
        private const float ClearanceInH = 0.10f;

        /// <summary>화면 가장자리 여백(월드 유닛) — 다른 배치 계산과 같은 값.</summary>
        private const float ScreenEdgePadWorld = 0.10f;

        /// <summary>폴백 사다리의 어느 단에서 배치됐는가. 0 = 미배치(F4 또는 아예 시도 안 함).</summary>
        public int PlacementRung { get; private set; }

        /// <summary>★ P-GHOST-1의 그 값 — <b>프롭이 지금 화면에 실제로 서 있는가.</b>
        /// 코스튬 포즈 층은 이 값만 본다.</summary>
        public bool PropPlaced { get; private set; }

        /// <summary>지금 서 있는 프롭의 코스튬 키(없으면 <c>null</c>).</summary>
        public string PlacedCostumeKey { get; private set; }

        /// <summary>프롭을 지은 <b>누적</b> 횟수. 검증은 세션 전후 스냅샷의 차이로 재고, 그 차이가
        /// <b>세션당 정확히 1</b>이어야 한다(합격선 V14).</summary>
        public int BuildCount { get; private set; }

        /// <summary>철거한 누적 횟수 — 마찬가지로 세션 전후 차이가 1이어야 한다.</summary>
        public int TeardownCount { get; private set; }

        /// <summary>★ <c>LineRenderer</c> 점 배열을 쓴 <b>누적 횟수</b>. 빌드 프레임 이후로는
        /// <b>절대 늘지 않아야 한다</b>(합격선 P-1 = 몰입기 60초 동안 정확히 0).
        /// 계기가 안 붙은 것과 「0」을 구분하려면 <b>양성 대조</b>가 필요하다(합격선 P-6).</summary>
        public int PointWriteCount { get; private set; }

        /// <summary>이 연출이 만든 LineRenderer 수. 철거가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 연출이 만든 콜라이더 수 — <b>항상 0</b>이어야 한다(합격선 P-5).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        private StickmanAgent _agent;
        private FocusWatchDirector _director;
        private bool _directorSearched;

        private Material _lineMaterial;
        private GameObject _container;
        private bool _suspendHidden;

        /// <summary>직전 프레임에 본 구간. 여기서 <b>판정</b>하는 것이 아니라 <b>전이를 알아채기</b> 위한
        /// 값이다 — 판정의 출처는 언제나 <see cref="FocusWatchDirector.CurrentPhase"/> 하나다.</summary>
        private FocusSessionPhase _lastPhase = FocusSessionPhase.None;

        private enum Mode { None, Intro, Held, Outro }
        private Mode _mode = Mode.None;
        private float _modeTimer;

        /// <summary>배치가 확정된 좌우 부호와 축소율. 빌드 프레임에 스냅샷되고 그 뒤 안 바뀐다.</summary>
        private float _placedFacing = 1f;
        private float _placedScale = 1f;

        // 빌드 중에만 쓰는 임시 버퍼 — 필드로 두어 몰입기 중 할당이 0이 되게 한다(빌드 프레임에도
        // 재사용된다). 프롭 조각 수는 4종 기준 최대 10 남짓이다.
        private readonly List<Vector3[]> _shapePoints = new List<Vector3[]>(12);
        private readonly List<AccessoryWornShapeData> _shapeMeta = new List<AccessoryWornShapeData>(12);

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnDisable()
        {
            // 컴포넌트가 꺼지거나 파괴될 때 프롭이 화면에 영구히 남지 않게 한다(다른 렌더러 관례).
            Teardown("컴포넌트 비활성");
        }

        /// <summary>같은 GameObject의 집중 세션 디렉터 — <c>StickmanBlackboard.IsFocusSessionActive</c>와
        /// <b>한 글자도 다르지 않은 어법</b>의 읽기 전용 조회다(1회 탐색 + 캐싱, 못 찾으면 다시 찾지 않는다).
        /// 타이머·구간 판정은 전부 그쪽 소유이고 여기서는 한 줄도 건드리지 않는다.</summary>
        private FocusWatchDirector Director
        {
            get
            {
                if (!_directorSearched)
                {
                    _directorSearched = true;
                    _director = GetComponent<FocusWatchDirector>();
                }
                return _director;
            }
        }

        private StickConfig Config => _agent != null ? _agent.Config : null;

        /// <summary>마스터 스위치 — 끄면 프롭도 코스튬 포즈도 통째로 꺼진다(네거티브 컨트롤 규약).</summary>
        private bool MotionEnabled
        {
            get
            {
                StickConfig cfg = Config;
                return cfg == null || cfg.costumeFocusMotionEnabled;
            }
        }

        private float Height
        {
            get
            {
                StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
                float h = bb != null ? bb.CharacterHeightWorld : 0f;
                return h > 0f ? h : StickConfig.BaselineCharacterTotalHeight;
            }
        }

        private void LateUpdate()
        {
            // ★ 캐릭터가 숨겨져 있으면 프롭도 함께 감춘다(원칙 2). 컨테이너가 SetParent(null) 독립
            //   루트라 StickmanAgent.Suspend()가 구조적으로 못 닿는다 — 이 게이트가 그 구멍이다.
            if (SuspendedOverlayGate.FreezeAndHide(_agent, _container, ref _suspendHidden, LogPrefix, "소품")) return;

            SyncToPhase();
            TickScaleEnvelope();
        }

        /// <summary>
        /// 구간을 조회해 <b>전이가 있었을 때만</b> 짓거나 허문다.
        /// 몰입기 사이의 모든 프레임에서 이 함수는 <c>_lastPhase</c> 비교 한 번으로 빠져나간다 —
        /// 그것이 「매 프레임 재드로우 금지」의 실체다.
        /// </summary>
        private void SyncToPhase()
        {
            FocusSessionPhase phase = MotionEnabled && Director != null
                ? Director.CurrentPhase
                : FocusSessionPhase.None;

            if (phase == _lastPhase) return;
            FocusSessionPhase from = _lastPhase;
            _lastPhase = phase;

            if (phase == FocusSessionPhase.Immersion)
            {
                TryBuild();
                return;
            }

            // 몰입기를 <b>떠나는</b> 모든 길 — 한계 구간 진입 · 완주 · 중도 취소 · 긴급정지 ·
            // 마스터 스위치 OFF. 넷 이상이지만 여기 한 줄이 전부다(파생값 하나만 보기 때문).
            if (from == FocusSessionPhase.Immersion) BeginOutro();
        }

        // ==================== 빌드 ====================

        private void TryBuild()
        {
            Teardown("재빌드 방어");   // 멱등 — 남은 잔재가 있으면 먼저 걷는다.

            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            if (bb == null || bb.Body == null) return;

            // ★ 여기가 CostumeResolver를 부르는 유일한 자리다 — 세션당 1회. 매 프레임 부르는
            //   함수가 아니다(그쪽 클래스 문서의 조회 시점 계약).
            CostumeDescriptor costume = CostumeResolver.Resolve();
            if (costume == null) return;   // 코스튬 없음 = 정상 상태. 조용히 아무 일도 안 한다.

            IReadOnlyList<AccessoryWornShapeData> shapes = ResolveStageShapes(costume);
            if (shapes == null || shapes.Count == 0) return;   // 「프롭 없는 코스튬」도 선언된 사실이다.

            GroundSensor.GroundInfo ground = bb.SenseGround();
            if (!ground.Grounded) return;   // 공중에서는 물건을 세울 바닥이 없다.

            // ★ 점 풀기는 <b>바닥 확인 뒤</b>다 — 앞에 두면 세울 자리가 없는 경우에도 배열을 할당하고,
            //   그 실패 경로마다 버퍼를 비워 주는 일을 잊기 쉽다(잊으면 다음 빌드까지 참조가 남는다).
            float height = Height;
            if (!BuildNeutralPoints(shapes)) return;

            Vector2 foot = bb.Body.position;
            if (!TryResolvePlacement(bb, ground, foot.x, height, costume.PropAnchorOffsetXInH,
                    out float facing, out float scale, out float anchorX, out int rung, out float neededWidth))
            {
                float availableWorld = Mathf.Max(0f, ground.CurrentFootholdRightWorldX - ground.CurrentFootholdLeftWorldX);
                Debug.Log($"{LogPrefix} 자리 없음 — 발판 폭 {availableWorld:F2}유닛, 필요 {neededWidth:F2}유닛. " +
                    "프롭을 세우지 않고 코스튬 전용 모션도 함께 끕니다 — 없는 것을 향한 자세를 만들지 " +
                    "않는 것이 절대 불변 원칙 1입니다.");
                _shapePoints.Clear();
                _shapeMeta.Clear();
                return;
            }

            _placedFacing = facing;
            _placedScale = scale;
            PlacementRung = rung;

            // ★ F2/F3-미러에서만 방향을 <b>1회</b> 반전한다 — 캐릭터가 프롭을 바라보게. Idle 중에는
            //   MoveInputX가 0이라 배회 AI가 이 값을 덮지 않는다(제스처 G3와 같은 근거).
            if (facing != bb.FacingSign) bb.SetFacingSign(facing);

            float groundY = ground.GroundWorldY;
            _container = new GameObject("CostumeProp");
            // 캐릭터의 자식으로 붙이지 않는 이유는 과녁과 같다 — 프롭은 「땅에 세워둔 물건」이라
            // 캐릭터가 움직여도 제자리에 있어야 한다.
            _container.transform.SetParent(null, false);
            _container.transform.position = new Vector3(anchorX, groundY, 0f);

            _lineMaterial = ResolveLineMaterial();
            Color ink = ResolveInk();
            float stroke = RenderStrokeWidth(height, scale);

            for (int i = 0; i < _shapePoints.Count; i++)
            {
                AccessoryWornShapeData meta = _shapeMeta[i];
                Vector3[] pts = _shapePoints[i];
                if (meta.filled) CreateShapeLine(pts, meta, ink, stroke * 2.2f, SortingPropFill, fill: true);
                if (!meta.noStroke) CreateShapeLine(pts, meta, ink, stroke, SortingPropLine, fill: false);
            }

            _shapePoints.Clear();
            _shapeMeta.Clear();

            PropPlaced = true;
            PlacedCostumeKey = costume.CostumeKey;
            BuildCount++;
            _mode = Mode.Intro;
            _modeTimer = 0f;
            ApplyScale(0.02f);

            Debug.Log($"{LogPrefix} 소환 — {costume.CostumeKey}, 폴백 {rung}단, " +
                $"방향 {(facing > 0f ? "오른" : "왼")}쪽, 배율 {scale:F2}, 앵커 x={anchorX:F2} y={groundY:F2}, " +
                $"선 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개. " +
                "이 프레임 이후 몰입기가 끝날 때까지 점 배열을 다시 쓰지 않습니다(재드로우 0).");
        }

        /// <summary>
        /// 이 단계에서 그릴 조각. 단계는 세션 경계에서만 바뀌므로(규칙 C-6) 몰입기 진입 시점에
        /// 읽어도 <b>세션 시작에 래치한 값과 같다</b> — 누적 분은 세션 종료에만 더해지기 때문이다.
        /// 단계 조형이 없으면 기본 조각을 그대로 쓴다(그 자체가 정상 상태다).
        /// </summary>
        private static IReadOnlyList<AccessoryWornShapeData> ResolveStageShapes(CostumeDescriptor costume)
        {
            int stage = CostumeEvolutionRules.StageOf(CostumeProgressModel.MinutesOf(costume.CostumeKey));
            IReadOnlyList<CostumeStageOverride> overrides = costume.StageShapes;
            for (int i = 0; i < overrides.Count; i++)
            {
                CostumeStageOverride o = overrides[i];
                if (o.stage == stage && o.shapes != null && o.shapes.Length > 0) return o.shapes;
            }
            return costume.PropShapes;
        }

        /// <summary>
        /// 스트림을 <b>facing 중립 공간</b>(Facing = +1)으로 푼다. 좌우 반전은 컨테이너
        /// <c>localScale.x</c>가 하므로 미러 폴백에서 점을 다시 풀 필요가 없다.
        /// <b>GameObject를 하나도 만들지 않는다</b> — 폴백 사다리가 여기서 나온 좌표만 보고 판정하고,
        /// 자리가 없으면 아무것도 안 지은 채로 끝난다.
        /// </summary>
        private bool BuildNeutralPoints(IReadOnlyList<AccessoryWornShapeData> shapes)
        {
            _shapePoints.Clear();
            _shapeMeta.Clear();

            if (!TryBuildFrame(out AccessoryWornFrame frame)) return false;
            for (int i = 0; i < shapes.Count; i++)
            {
                AccessoryWornShapeData shape = shapes[i];
                if (!AccessoryWornShapeReader.TryBuild(shape, frame, false, out Vector3[] points, out string error))
                {
                    Debug.LogError($"{LogPrefix} 조각 \"{shape.name}\"의 좌표 스트림을 읽지 못했습니다 — {error}. " +
                        "프롭을 반쪽만 그리지 않고 통째로 건너뜁니다(반쪽 그림은 «왜 이것만 없지»가 됩니다).");
                    _shapePoints.Clear();
                    _shapeMeta.Clear();
                    return false;
                }
                _shapePoints.Add(points);
                _shapeMeta.Add(shape);
            }
            return _shapePoints.Count > 0;
        }

        /// <summary>
        /// 좌표 스트림이 딛는 치수. <c>AccessoryShapeBuilder.Frame</c>과 <b>같은 기저 집합</b>이며,
        /// 리그를 아는 쪽이 채워 넘긴다는 그쪽 규약을 그대로 따른다.
        ///
        /// <para>★ <b>닫힌 미해결 1건(2026-09-07)</b>: 설계 문서(<c>UX_MOTION_COSTUME_FOCUS</c> 4-1)가
        /// 프롭 좌표를 <b>신장 H 배수</b>로 적는데 <see cref="AccessoryWornBasis"/>에 H 기저가 없었다.
        /// 지금은 <see cref="AccessoryWornBasis.Height"/>가 열려 있고 <b>이 프레임이 그 값을 싣는다</b> —
        /// 아래 <c>frame.Height</c> 한 줄이 그것이다. 안 실으면 그 기저를 쓰는 조각이
        /// <b>0으로 무너지지 않고 사유와 함께 실패</b>한다(0으로 떨어뜨리면 도형이 원점으로 모이는데,
        /// 그건 「안 그려짐」과 화면상 같아서 원인을 영영 못 찾는다).</para>
        /// </summary>
        private bool TryBuildFrame(out AccessoryWornFrame frame)
        {
            frame = default;
            StickmanMetrics m = _agent != null ? _agent.Metrics : StickmanMetrics.Find(this);
            if (m == null)
            {
                Debug.LogWarning($"{LogPrefix} StickmanMetrics를 찾지 못해 프롭을 세우지 않습니다 — " +
                    "치수를 짐작해 그리면 프롭만 캐릭터와 다른 크기가 됩니다.");
                return false;
            }

            // ★ facing은 +1(중립)로 고정한다 — 좌우 반전은 컨테이너 localScale.x가 하므로 미러 폴백에서
            //   점 배열을 다시 풀 필요가 없다(PC-1: 빌드 뒤 점 배열 재기록 0).
            var rig = new AccessoryShapeBuilder.Rig(m.HeadRadius, m.HeadCenterLocalY,
                m.ShoulderLocalY, m.HipLocalY, 1f);
            frame = AccessoryShapeBuilder.Frame(rig);
            // ★ 신장 H — 리그를 아는 쪽이 싣는다(그쪽 규약 그대로). 설계의 모든 프롭 좌표가 H 배수다.
            frame.Height = Height;
            return true;
        }

        // ==================== 폴백 사다리 F1~F4 ====================

        /// <summary>
        /// 판정 기준 2개는 <c>ArcheryDirector</c>가 이미 쓰는 잣대 그대로다:
        /// ① 프롭 x 범위 전체가 <b>지금 딛고 있는 발판</b> 안인가 ② <b>화면 안</b>인가.
        /// F1(정면) → F2(반대쪽 미러) → F3(×0.70으로 F1·F2 재시도) → F4(미소환).
        /// </summary>
        private bool TryResolvePlacement(StickmanBlackboard bb, in GroundSensor.GroundInfo ground,
            float footX, float height, float anchorOffsetInH,
            out float facing, out float scale, out float anchorX, out int rung, out float neededWidth)
        {
            facing = bb.FacingSign >= 0f ? 1f : -1f;
            scale = 1f;
            anchorX = footX;
            rung = 0;

            float minLocalX = float.PositiveInfinity, maxLocalX = float.NegativeInfinity;
            for (int i = 0; i < _shapePoints.Count; i++)
            {
                Vector3[] pts = _shapePoints[i];
                for (int p = 0; p < pts.Length; p++)
                {
                    if (pts[p].x < minLocalX) minLocalX = pts[p].x;
                    if (pts[p].x > maxLocalX) maxLocalX = pts[p].x;
                }
            }
            if (minLocalX > maxLocalX) { neededWidth = 0f; return false; }

            float clearance = height * ClearanceInH;
            float offset = anchorOffsetInH * height;
            // 루트에서 프롭 원단까지 + 여유 — 로그가 말하는 「필요 폭」이 이 값이다.
            neededWidth = Mathf.Abs(offset + maxLocalX) + clearance;

            if (!bb.TryGetWalkableScreenBoundsWorld(out float screenLeft, out float screenRight))
            {
                Camera cam = bb.MainCamera;
                if (cam == null || !cam.orthographic) return false;
                float half = cam.orthographicSize * cam.aspect;
                screenLeft = cam.transform.position.x - half + ScreenEdgePadWorld;
                screenRight = cam.transform.position.x + half - ScreenEdgePadWorld;
            }

            float lo = Mathf.Max(ground.CurrentFootholdLeftWorldX, screenLeft) + clearance;
            float hi = Mathf.Min(ground.CurrentFootholdRightWorldX, screenRight) - clearance;

            float own = facing;
            // F1 정면 → F2 반대쪽 미러 → F3 축소로 둘 다 재시도. 순서가 곧 사다리다.
            if (Fits(own, 1f)) { facing = own; scale = 1f; rung = 1; anchorX = AnchorOf(own, 1f); return true; }
            if (Fits(-own, 1f)) { facing = -own; scale = 1f; rung = 2; anchorX = AnchorOf(-own, 1f); return true; }
            if (Fits(own, ShrunkPlacementScale)) { facing = own; scale = ShrunkPlacementScale; rung = 3; anchorX = AnchorOf(own, scale); return true; }
            if (Fits(-own, ShrunkPlacementScale)) { facing = -own; scale = ShrunkPlacementScale; rung = 3; anchorX = AnchorOf(-own, scale); return true; }
            return false;

            float AnchorOf(float f, float s) => footX + f * s * offset;

            bool Fits(float f, float s)
            {
                float a = AnchorOf(f, s);
                float e1 = a + f * s * minLocalX;
                float e2 = a + f * s * maxLocalX;
                return Mathf.Min(e1, e2) >= lo && Mathf.Max(e1, e2) <= hi;
            }
        }

        // ==================== 등장/퇴장 ====================

        private void BeginOutro()
        {
            if (!PropPlaced || _container == null) { Teardown("몰입기 이탈"); return; }
            _mode = Mode.Outro;
            _modeTimer = 0f;
            // ★ 포즈 층은 이 순간부터 즉시 꺼진다 — 그림이 빠져나가는 0.22초 동안 캐릭터가
            //   사라지는 물건을 계속 짚고 있으면 그것이 곧 유령 제스처다.
            PropPlaced = false;
        }

        /// <summary>
        /// 등장/퇴장 <b>그 두 구간에만</b> 루트 <c>localScale</c> 1개를 쓴다.
        /// ★ <b>유지 구간에서는 첫 줄에서 그대로 돌아간다</b> — 이 조기 반환이 없으면
        /// 「매 프레임 재드로우 금지」가 문자 그대로 거짓이 된다.
        /// </summary>
        private void TickScaleEnvelope()
        {
            if (_mode == Mode.None || _mode == Mode.Held) return;
            if (_container == null) { _mode = Mode.None; return; }

            _modeTimer += Time.deltaTime;

            if (_mode == Mode.Intro)
            {
                float intro = Mathf.Max(0.01f, ConfigFloat(c => c.costumeFocusPropIntroSeconds, 0.35f));
                float t = Mathf.Clamp01(_modeTimer / intro);
                if (t >= 1f)
                {
                    ApplyScale(1f);
                    _mode = Mode.Held;   // ← 여기서부터 Transform 쓰기가 0이 된다.
                    return;
                }
                // back-out easing — 1.0을 넘겼다가 되돌아오는 탄력(과녁 등장과 같은 기구).
                float u = 1f - t;
                const float overshoot = 1.9f;
                ApplyScale(Mathf.Max(0.02f, 1f - (u * u * ((overshoot + 1f) * u - overshoot))));
                return;
            }

            float outro = Mathf.Max(0.01f, ConfigFloat(c => c.costumeFocusPropOutroSeconds, 0.22f));
            float o = Mathf.Clamp01(_modeTimer / outro);
            if (o >= 1f) { Teardown("퇴장 완료"); return; }
            ApplyScale(1f - o);
        }

        /// <summary>바닥이 컨테이너 원점이라 원점 기준 스케일이 곧 <b>바닥에서 솟는</b> 그림이 된다.
        /// 좌우 반전도 여기 x 부호 하나로 끝난다(점 배열을 다시 쓰지 않는다).</summary>
        private void ApplyScale(float envelope)
        {
            if (_container == null) return;
            float s = _placedScale * envelope;
            _container.transform.localScale = new Vector3(_placedFacing * s, s, 1f);
        }

        // ==================== 정리 ====================

        private void Teardown(string reason)
        {
            bool had = _container != null;
            _shapePoints.Clear();
            _shapeMeta.Clear();
            PropPlaced = false;
            PlacementRung = 0;
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
                TeardownCount++;
            }
            _mode = Mode.None;
            _modeTimer = 0f;
            _suspendHidden = false;
            if (had)
            {
                Debug.Log($"{LogPrefix} 철거 — 사유={reason}, 코스튬={PlacedCostumeKey}. " +
                    "화면에 그림만 남기지 않습니다(창이 닫혔는데 그림이 남으면 «우리 앱이 그 창에 " +
                    "뭘 걸어뒀다»는 인상이 됩니다).");
            }
            PlacedCostumeKey = null;
        }

        // ==================== 공용 헬퍼 ====================

        private void CreateShapeLine(Vector3[] points, in AccessoryWornShapeData meta,
            Color ink, float width, int sortingOrder, bool fill)
        {
            string suffix = fill ? "Fill" : "Line";
            var go = new GameObject(string.IsNullOrEmpty(meta.name) ? suffix : meta.name + suffix);
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            Color c = ink;
            float a = fill
                ? (meta.alpha > 0f ? meta.alpha : 1f)
                : (meta.lineAlpha > 0f ? meta.lineAlpha : 1f);
            c.a = ink.a * a;
            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingOrder = sortingOrder;
            lr.loop = meta.loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            PointWriteCount++;   // ★ 이 줄이 늘어나는 자리는 여기 하나뿐이다(합격선 P-1의 계기).
        }

        private float ConfigFloat(System.Func<StickConfig, float> selector, float fallback)
        {
            StickConfig cfg = Config;
            return cfg != null ? selector(cfg) : fallback;
        }

        /// <summary>
        /// ★★ <b>실제로 그려지는</b> 획 두께 — 화면상 최소 두께 아래로 내려가지 않는다(2026-09-08).
        /// 하한 값의 <b>단일 소스</b>는 <see cref="StickmanAgent.MinStrokeWorldWidth"/>이고
        /// (몸·액세서리·FX·펫이 전부 그 하나를 문다) 여기 숫자를 다시 적지 않는다.
        ///
        /// <para><b>왜 필요한가</b> [실측]: 순수 비례면 배율 0.35에서 <b>0.951pt</b>다(하한 2.00pt의
        /// 절반 미만). Windows 100%(1×)에서는 device pixel 1개 미만이라 선이 아예 사라질 수 있고,
        /// Windows가 1차 출시 플랫폼이다. 기하 두께가 하한과 만나는 지점은 배율 <b>0.7358</b>이라
        /// 출하 기본 0.75에서는 <b>한 톨도 안 바뀐다</b>(2.039pt vs 2.000pt).</para>
        ///
        /// <para>★ <b>과녁과 다른 항이 하나 있다 — 컨테이너 배율로 나눈다.</b>
        /// 프롭 컨테이너는 폴백 F3에서 <c>localScale</c>이 0.70이 되고, <c>LineRenderer</c>의 두께는
        /// 그 배율을 함께 받는다(등장 연출이 커지는 것이 바로 그 성질이다). 그러면 하한을 원본 두께에만
        /// 걸어 봐야 <b>화면에서는 0.70배로 다시 내려간다</b> — 배율 0.75에서 1.43pt다.
        /// 그래서 <b>화면에 남는 값</b>이 하한이 되도록 미리 나눈다.
        /// 등장/퇴장 0.35/0.22초 동안은 포락선 때문에 더 얇아지지만, 그건 과녁 등장 연출과 같은
        /// 성질의 <b>일시적 전이</b>라 하한의 대상이 아니다.</para>
        /// </summary>
        private float RenderStrokeWidth(float height, float placementScale)
        {
            float geometric = height * StrokeWidthRatio;
            float floorOnScreen = MinStrokeWorld / Mathf.Max(0.01f, placementScale);
            return Mathf.Max(geometric, floorOnScreen);
        }

        /// <summary>이 렌더러가 쓰는 화면상 최소 두께(월드). 에이전트가 없는 사본/스텁에서는
        /// <c>StickConfig</c>의 근사 환산으로 되메운다 — 0을 흘리면 하한이 조용히 사라진다.</summary>
        private float MinStrokeWorld => _agent != null
            ? _agent.MinStrokeWorldWidth
            : StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        private Color ResolveInk()
            => Config != null ? Config.ResolveInkColor() : Color.black;

        /// <summary>캐릭터가 이미 쓰고 있는 LineRenderer 머티리얼을 그대로 빌린다.
        /// <c>Shader.Find</c>로 런타임에 찾지 않는다 — 빌드 스트리핑 위험(다른 렌더러와 같은 관례).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
