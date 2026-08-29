using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 화면 우상단 상시 <b>맞물린 톱니 두 개</b> — 정보/장비 창의 주 진입점.
    /// 2026-08-29 사용자 원문: "바탕화면 오른쪽 상단에 기어 표시같은걸 띄워놓고 클릭하면 기어가
    /// 회전하면서 캐릭터 창이 나오게끔".
    /// 2026-08-30 사용자 원문(이번 라운드): "바탕화면 기어표시도 지금 너무 단순하게 되어있잖아 클릭하면
    /// <b>큰기어와 작은기어가 맞물려 움직이면서</b> 캐릭터 창이뜨게.. 기어의 디자인도 좀 멋있게 바꿔줘".
    ///
    /// ============================================================================
    /// "진짜 맞물린 것처럼 보이는가" — 이 요구의 핵심은 기구학이다
    /// ============================================================================
    /// 두 기어를 대충 같이 돌리면 그림이 거짓말을 한다. 그래서 실제 기어의 관계를 그대로 코드로 지킨다:
    ///  · <b>모듈(이 크기)이 같다</b>: 잇수 ∝ 피치 반지름. 큰 기어 <see cref="BigToothCount"/>개,
    ///    작은 기어 <see cref="SmallToothCount"/>개이고 반지름 비도 정확히 그 비율이다.
    ///  · <b>중심 거리 = 두 피치 반지름의 합</b>(<see cref="CenterDistancePoints"/>) — 이 값이어야
    ///    한쪽의 이 끝(팁원)이 다른 쪽의 이 뿌리(루트원)까지 파고들어 "물린" 그림이 된다.
    ///  · <b>회전은 반대 방향, 속도는 잇수에 반비례</b>: ω작 = −ω큰 × (N큰 / N작).
    ///    맞물림 위상도 초기에 한 번 맞춰두면(작은 기어의 이가 큰 기어의 골에 오도록) 그 비율을
    ///    지키는 한 영원히 유지된다 — 즉 애니메이션 중에 이가 서로 파고드는 그림이 나오지 않는다.
    /// 회귀 테스트: Tests/PlayMode/InfoGearMeshingTests.cs가 방향/속도비/중심거리를 절대 조건으로 잠근다.
    ///
    /// ============================================================================
    /// 왜 uGUI가 아니라 LineRenderer인가
    /// ============================================================================
    /// 이 앱의 시각 요소는 전부 프로시저럴 선화다(스프라이트 에셋이 없다). 캐릭터와 같은 LineRenderer +
    /// <b>캐릭터의 머티리얼을 빌려 쓰는</b> 관례를 그대로 따르면 그림체도 일관되고(잉크색 전환에도 함께
    /// 따라간다) 의존성도 늘지 않는다.
    ///
    /// ============================================================================
    /// 크기는 <b>화면 고정</b>이다 — 캐릭터 배율을 따라가지 않는다
    /// ============================================================================
    /// 캐릭터에 붙는 액세서리와 정반대의 규칙이다. 이건 캐릭터의 일부가 아니라 화면 구석의 작은 UI
    /// 버튼이라, 캐릭터가 커지고 작아진다고 함께 커지면 이상하다(리더 지시). 그래서 모든 치수를
    /// <b>OS 포인트</b>로 정하고 ScreenCoordinateConverter로 환산한다(Retina에서도 물리적으로 같은 크기).
    ///
    /// ============================================================================
    /// 클릭 판정 — 기존 메커니즘을 그대로 재사용한다(새로 만들지 않는다)
    /// ============================================================================
    ///  (1) 아이콘 영역을 덮는 isTrigger BoxCollider2D — UniWindowController의 hitTestType=Raycast가
    ///      "커서 아래 Collider2D가 있는가"로 클릭관통을 판정하므로, <b>이 작은 영역만</b> 클릭을 받고
    ///      나머지 화면은 100% 관통 그대로다. 이번 라운드에 기어가 둘이 되면서 그 사각형은
    ///      <b>두 기어를 함께 덮는 최소 사각형</b>으로 넓어졌다(그 이상은 넓히지 않는다).
    ///  (2) 전역 폴링(IGlobalPointerButtonService + 커서 좌표) — macOS에서 비활성 앱의 첫 클릭이
    ///      "앱 활성화"에만 소비되는 경우에도 확실히 잡는다.
    /// <b>비침해 보장</b>: (2)는 "버튼이 눌렸다"만으로는 아무 일도 하지 않는다. 반드시 그 순간 커서가
    /// 아이콘 사각형 <b>안</b>일 때만 반응한다.
    ///
    /// <b>메뉴바를 피한다</b>: 세로 여백 <see cref="MarginTopPoints"/>는 macOS 메뉴바(노치 기준 최대 약
    /// 38pt)보다 확실히 아래에 아이콘을 놓기 위한 값이다.
    ///
    /// ============================================================================
    /// 클릭 -> 회전 -> 창 열림
    /// ============================================================================
    /// 클릭 즉시 창을 띄우지 않고 <see cref="SpinSeconds"/> 동안 두 기어를 맞물려 돌린 뒤
    /// <see cref="CharacterInfoWindow.Open"/>을 부른다. "눌렀다"는 피드백이 먼저 오고 창이 뒤따르는
    /// 흐름이라, 클릭이 먹었는지 알 수 없는 오버레이 앱의 고질적 불확실성이 사라진다.
    /// </summary>
    public sealed class InfoGearIconWidget : MonoBehaviour
    {
        // ==================== 화면 고정 치수(전부 OS 포인트) ====================

        /// <summary>화면 오른쪽 끝에서 <b>큰 기어 중심</b>까지의 거리.</summary>
        private const float MarginRightPoints = 30f;

        /// <summary>화면 위쪽 끝에서 <b>큰 기어 중심</b>까지의 거리. macOS 메뉴바(최대 약 38pt)보다
        /// 확실히 아래여야 한다 — 클래스 문서 "메뉴바를 피한다" 참고.</summary>
        private const float MarginTopPoints = 58f;

        // ---- 큰 기어 ----
        private const float BigOuterPoints = 13f;   // 이 끝(팁원).
        private const float BigRootPoints = 10.2f;  // 이 뿌리(루트원).
        private const float BigHubPoints = 3.6f;    // 가운데 축.
        private const float BigRimPoints = 7.0f;    // 안쪽 림(스포크가 닿는 원).
        private const int BigToothCount = 10;

        /// <summary>작은 기어의 크기비 = 잇수비(모듈이 같아야 물린다 — 클래스 문서 참고).</summary>
        private const int SmallToothCount = 6;
        private const float SmallScale = (float)SmallToothCount / BigToothCount;

        private const float SmallOuterPoints = BigOuterPoints * SmallScale;
        private const float SmallRootPoints = BigRootPoints * SmallScale;
        private const float SmallHubPoints = BigHubPoints * SmallScale * 1.35f; // 너무 작아지지 않게 살짝 키운다.

        /// <summary>피치 반지름 = (팁 + 루트) / 2. 중심 거리는 두 피치 반지름의 합이어야 물린다.</summary>
        private const float BigPitchPoints = (BigOuterPoints + BigRootPoints) * 0.5f;
        private const float SmallPitchPoints = (SmallOuterPoints + SmallRootPoints) * 0.5f;
        private const float CenterDistancePoints = BigPitchPoints + SmallPitchPoints;

        /// <summary>작은 기어 중심이 큰 기어 중심에서 놓이는 방향(도) — 회전각이 아니라 <b>배치 각</b>이다. 화면 우상단이라 <b>왼쪽 아래</b>로 물려야 화면 안에 남는다.</summary>
        private const float SmallGearOffsetAngleDegrees = 214f;

        private const float StrokeWidthPoints = 1.7f;
        private const float HitPaddingPoints = 5f;

        // 이 프로필(피치 대비 비율) — 사다리꼴 이를 또렷하게 만든다.
        private const float ToothTipHalfFraction = 0.17f;    // 이 끝(마루)의 반각.
        private const float ToothRootHalfFraction = 0.30f;   // 이 뿌리의 반각(마루보다 넓어야 사다리꼴).

        private const float SpinSeconds = 0.52f;
        private const float BigSpinTurns = 0.75f;   // 큰 기어 기준 회전량. 작은 기어는 잇수비만큼 더 돈다.
        private const float IdleAlpha = 0.70f;      // 평소에는 은은하게(관찰형 앱 — 화면을 지배하지 않는다).
        private const float ActiveAlpha = 0.95f;    // 커서가 위에 있거나 창이 열려 있을 때.
        private const float AlphaFadeSpeed = 6f;

        private const float ClickPollInterval = 0.05f;
        private const int SortingOrder = 40;        // 캐릭터/액세서리보다 위(화면 UI다).

        private StickmanAgent _agent;
        private StickConfig _config;
        private CharacterInfoWindow _window;
        private IGlobalPointerButtonService _buttonService;
        private Camera _camera;

        private GameObject _container;
        private Transform _bigGear;
        private Transform _smallGear;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(10);
        private BoxCollider2D _clickTarget;
        private Material _lineMaterial;

        private float _spinTimer = -1f;   // 음수 = 회전 중 아님.
        private float _alpha = IdleAlpha;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private bool _builtGeometry;
        private float _builtRadiusWorld = -1f;
        private Color _builtInk = new Color(-1f, -1f, -1f, -1f);

        /// <summary>지금 회전 연출 중인가(테스트/진단 전용).</summary>
        public bool IsSpinning => _spinTimer >= 0f;

        /// <summary>큰 기어 중심의 Unity 스크린 좌표(픽셀). 실측 검증용.</summary>
        public Vector2 IconScreenCenter { get; private set; }

        /// <summary>두 기어를 함께 덮는 히트 사각형(Unity 스크린 픽셀). "이 밖에서는 절대 안 걸린다"를
        /// 테스트가 직접 확인할 수 있게 노출한다.</summary>
        public Rect IconScreenRect { get; private set; }

        /// <summary>큰 기어의 현재 회전각(도). 회귀 테스트가 방향/속도를 직접 잰다.</summary>
        public float BigGearAngleDegrees => _bigGear != null ? _bigGear.localEulerAngles.z : 0f;

        /// <summary>작은 기어의 현재 회전각(도).</summary>
        public float SmallGearAngleDegrees => _smallGear != null ? _smallGear.localEulerAngles.z : 0f;

        /// <summary>맞물림 속도비 = 큰 기어 잇수 / 작은 기어 잇수. 작은 기어가 이만큼 <b>더 빨리</b> 돈다.</summary>
        public static float MeshRatio => (float)BigToothCount / SmallToothCount;

        /// <summary>두 기어 중심 사이 거리(OS 포인트) — 두 피치 반지름의 합이어야 한다.</summary>
        public static float CenterDistance => CenterDistancePoints;

        public static int BigTeeth => BigToothCount;
        public static int SmallTeeth => SmallToothCount;

        /// <summary>큰 기어의 이를 두 중심을 잇는 선에 맞추는 각(도). 작은 기어는 그 반대 위상으로
        /// 도형이 구워져 있어, 이 각에서 출발해 회전비만 지키면 맞물림이 계속 유지된다.</summary>
        private static float BuildPhaseAngle => SmallGearOffsetAngleDegrees % (360f / BigToothCount);

        /// <summary>테스트 전용 — 클릭 없이 회전 연출만 시작한다(창은 회전이 끝나면 정상적으로 열린다).</summary>
        public void StartSpinForTests() => _spinTimer = 0f;

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 라이벌 복제본에 이 컴포넌트가 남아 있어도
            // 톱니가 두 벌 겹쳐 뜨지 않게 하는 2차 방어(1차는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            _window = GetComponent<CharacterInfoWindow>();
        }

        private void Start()
        {
            if (_agent == null)
            {
                enabled = false;
                return;
            }
            _buttonService = _agent.PlatformService as IGlobalPointerButtonService;
            Debug.Log("[톱니] 준비 완료 — 화면 우상단에 맞물린 기어 2개가 상시 표시됩니다(오른쪽 " +
                $"{MarginRightPoints:F0}pt / 위 {MarginTopPoints:F0}pt, 큰 기어 반지름 {BigOuterPoints:F0}pt / " +
                $"잇수 {BigToothCount}, 작은 기어 {SmallOuterPoints:F1}pt / 잇수 {SmallToothCount}, " +
                $"중심 거리 {CenterDistancePoints:F1}pt). 클릭하면 두 기어가 **반대 방향으로** 돌고" +
                $"(작은 쪽이 {MeshRatio:F2}배 빠르게) 그 뒤 캐릭터 정보창이 열립니다. " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — 콜라이더 경로만")}. " +
                "★ 클릭 판정은 두 기어를 덮는 작은 사각형 안에서만 일어나며, 그 밖은 100% 클릭관통 그대로입니다.");
        }

        private void OnDestroy()
        {
            if (_container != null) Destroy(_container);
            if (_clickTarget != null) Destroy(_clickTarget.gameObject);
        }

        private void LateUpdate()
        {
            if (_agent == null) return;
            if (_camera == null) _camera = _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (_camera == null) return;

            PlaceOnScreen();
            TickSpin();
            TickHoverAlpha();
            TickClick();
        }

        // ==================== 화면 배치 ====================

        /// <summary>화면 우상단 고정 위치로 매 프레임 옮긴다 — <b>캐릭터 위치와 완전히 무관</b>하다.</summary>
        private void PlaceOnScreen()
        {
            float depth = Mathf.Abs(_camera.transform.position.z);

            // OS 포인트 -> Unity 픽셀(Retina 대응) -> 월드 유닛.
            float marginRightPx = ScreenCoordinateConverter.CanvasToUnityScreen(MarginRightPoints, _config);
            float marginTopPx = ScreenCoordinateConverter.CanvasToUnityScreen(MarginTopPoints, _config);
            IconScreenCenter = new Vector2(Screen.width - marginRightPx, Screen.height - marginTopPx);

            float pxPerPoint = ScreenCoordinateConverter.CanvasToUnityScreen(1f, _config);
            float rad = SmallGearOffsetAngleDegrees * Mathf.Deg2Rad;
            Vector2 smallOffsetPx = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (CenterDistancePoints * pxPerPoint);

            // 두 기어를 함께 덮는 최소 사각형(+여유). 그 이상은 넓히지 않는다(비침해).
            float bigR = (BigOuterPoints + HitPaddingPoints) * pxPerPoint;
            float smallR = (SmallOuterPoints + HitPaddingPoints) * pxPerPoint;
            float minX = Mathf.Min(IconScreenCenter.x - bigR, IconScreenCenter.x + smallOffsetPx.x - smallR);
            float maxX = Mathf.Max(IconScreenCenter.x + bigR, IconScreenCenter.x + smallOffsetPx.x + smallR);
            float minY = Mathf.Min(IconScreenCenter.y - bigR, IconScreenCenter.y + smallOffsetPx.y - smallR);
            float maxY = Mathf.Max(IconScreenCenter.y + bigR, IconScreenCenter.y + smallOffsetPx.y + smallR);
            IconScreenRect = new Rect(minX, minY, maxX - minX, maxY - minY);

            Vector3 centerWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x, IconScreenCenter.y, depth));
            Vector3 unitEdgeWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x + pxPerPoint, IconScreenCenter.y, depth));
            float worldPerPoint = Mathf.Abs(unitEdgeWorld.x - centerWorld.x);

            EnsureBuilt(worldPerPoint);
            if (_container == null) return;

            _container.transform.position = new Vector3(centerWorld.x, centerWorld.y, 0f);

            if (_clickTarget != null)
            {
                Vector3 rectCenterWorld = _camera.ScreenToWorldPoint(
                    new Vector3(IconScreenRect.center.x, IconScreenRect.center.y, depth));
                Vector3 rectMaxWorld = _camera.ScreenToWorldPoint(
                    new Vector3(IconScreenRect.xMax, IconScreenRect.yMax, depth));
                _clickTarget.transform.position = new Vector3(rectCenterWorld.x, rectCenterWorld.y, 0f);
                _clickTarget.size = new Vector2(Mathf.Abs(rectMaxWorld.x - rectCenterWorld.x) * 2f,
                    Mathf.Abs(rectMaxWorld.y - rectCenterWorld.y) * 2f);
            }
        }

        /// <summary>배율(화면 해상도/DPI)이나 잉크색이 바뀌지 않으면 도형을 다시 만들지 않는다 —
        /// 24시간 상주 앱. 잉크색을 서명에 넣는 이유는 CharacterAccessoryRenderer와 같다(⌃⌥⌘C /
        /// 정보창 [외형] 탭에서 색을 바꿔도 이 아이콘이 옛 색으로 남지 않게).</summary>
        private void EnsureBuilt(float worldPerPoint)
        {
            Color ink = ResolveInk();
            bool sameSize = _builtGeometry && Mathf.Abs(worldPerPoint - _builtRadiusWorld) < worldPerPoint * 0.01f;
            if (sameSize && ink == _builtInk) return;

            Build(worldPerPoint, ink);
            _builtRadiusWorld = worldPerPoint;
            _builtInk = ink;
            _builtGeometry = true;
        }

        private Color ResolveInk() => _config != null ? _config.ResolveInkColor() : Color.black;

        private void Build(float worldPerPoint, Color ink)
        {
            if (_container != null) Destroy(_container);
            _lines.Clear();

            _lineMaterial = ResolveLineMaterial();

            _container = new GameObject("InfoGearIcon");
            _container.transform.SetParent(null, false); // 씬 루트 — 캐릭터가 걷거나 랙돌로 회전해도 따라 돌면 안 된다.

            float stroke = StrokeWidthPoints * worldPerPoint;

            // ---- 큰 기어 ----
            var bigGo = new GameObject("BigGear");
            bigGo.transform.SetParent(_container.transform, false);
            _bigGear = bigGo.transform;

            AddLine(_bigGear, "Teeth", BuildGearOutline(BigToothCount,
                BigOuterPoints * worldPerPoint, BigRootPoints * worldPerPoint, 0f), ink, stroke, loop: true);
            AddLine(_bigGear, "Rim", BuildCircle(BigRimPoints * worldPerPoint, 20), ink, stroke * 0.85f, loop: true);
            AddLine(_bigGear, "Hub", BuildCircle(BigHubPoints * worldPerPoint, 14), ink, stroke * 0.85f, loop: true);
            // 스포크(살) 4개 — 각각 <b>독립된 선</b>이다. 한 LineRenderer로 왕복시키면 중심을 가로지르는
            // 연결선이 함께 그려져 기어가 아니라 조준경처럼 보인다(첫 육안 검증에서 실제로 그랬다).
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.5f * i + Mathf.PI * 0.25f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                AddLine(_bigGear, "Spoke" + i, new[]
                {
                    dir * (BigHubPoints * worldPerPoint * 1.05f),
                    dir * (BigRimPoints * worldPerPoint * 0.98f),
                }, ink, stroke * 0.7f, loop: false);
            }

            // ---- 작은 기어 ----
            var smallGo = new GameObject("SmallGear");
            smallGo.transform.SetParent(_container.transform, false);
            _smallGear = smallGo.transform;
            float rad = SmallGearOffsetAngleDegrees * Mathf.Deg2Rad;
            _smallGear.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * (CenterDistancePoints * worldPerPoint);

            // ★ 맞물림 위상: 두 중심을 잇는 선 위에서 작은 기어의 <b>이</b>가 큰 기어의 <b>골</b>에
            //   오도록 초기 각을 맞춘다. 이 관계는 회전비를 지키는 한 계속 유지된다.
            float smallPitchAngle = 360f / SmallToothCount;
            float smallPhase = SmallGearOffsetAngleDegrees + 180f + smallPitchAngle * 0.5f; // 중심선 반대편에 골이 오게.

            AddLine(_smallGear, "Teeth", BuildGearOutline(SmallToothCount,
                SmallOuterPoints * worldPerPoint, SmallRootPoints * worldPerPoint, smallPhase), ink, stroke * 0.9f, loop: true);
            AddLine(_smallGear, "Hub", BuildCircle(SmallHubPoints * worldPerPoint, 12), ink, stroke * 0.8f, loop: true);
            // 작은 기어에도 살 3개 — 회전이 <b>눈에 보이게</b> 하는 최소 장치다(이만 있으면 6갈래
            // 대칭이라 돌고 있는지 알아보기 어렵다. 육안 검증에서 실제로 그랬다).
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * 2f / 3f * i;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                AddLine(_smallGear, "SmallSpoke" + i, new[]
                {
                    dir * (SmallHubPoints * worldPerPoint * 1.1f),
                    dir * (SmallRootPoints * worldPerPoint * 0.82f),
                }, ink, stroke * 0.65f, loop: false);
            }

            // 큰 기어의 이 위상도 중심선에 맞춘다. 도형을 다시 만들지 않고 각도만 준다.
            _bigGear.localRotation = Quaternion.Euler(0f, 0f, BuildPhaseAngle);

            if (_clickTarget == null)
            {
                var hitGo = new GameObject("InfoGearClickTarget");
                _clickTarget = hitGo.AddComponent<BoxCollider2D>();
                _clickTarget.isTrigger = true; // 캐릭터가 톱니에 부딪혀 튕기면 안 된다(메뉴 차단막과 같은 이유).
            }

            ApplyAlphaToAll();
        }

        /// <summary>
        /// 사다리꼴 이를 가진 기어 윤곽. 이 하나당 5점 — 뿌리(앞) / 마루(앞) / 마루(뒤) / 뿌리(뒤) /
        /// 골 중앙. 원에 삼각 홈을 낸 예전 모양과 달리 <b>이 끝이 평평</b>해 진짜 기어로 읽힌다.
        /// </summary>
        private static Vector3[] BuildGearOutline(int teeth, float outer, float root, float phaseDegrees)
        {
            var pts = new Vector3[teeth * 5];
            float pitch = Mathf.PI * 2f / teeth;
            float tipHalf = pitch * ToothTipHalfFraction;
            float rootHalf = pitch * ToothRootHalfFraction;
            float phase = phaseDegrees * Mathf.Deg2Rad;

            int k = 0;
            for (int i = 0; i < teeth; i++)
            {
                float center = phase + i * pitch;
                pts[k++] = Polar(center - rootHalf, root);
                pts[k++] = Polar(center - tipHalf, outer);
                pts[k++] = Polar(center + tipHalf, outer);
                pts[k++] = Polar(center + rootHalf, root);
                pts[k++] = Polar(center + pitch * 0.5f, root * 0.985f); // 골 중앙(살짝 눌러 둥근 골).
            }
            return pts;
        }

        private static Vector3 Polar(float angleRadians, float radius)
            => new Vector3(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius, 0f);

        private static Vector3[] BuildCircle(float radius, int segments)
        {
            var pts = new Vector3[Mathf.Max(3, segments)];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = Polar(a, radius);
            }
            return pts;
        }

        private void AddLine(Transform parent, string name, Vector3[] points, Color color, float width, bool loop)
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
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.sortingOrder = SortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            _lines.Add(lr);
        }

        // ==================== 회전 연출 ====================

        /// <summary>
        /// 두 기어를 <b>반대 방향</b>으로, <b>잇수에 반비례하는 속도</b>로 돌린다(클래스 문서 기구학).
        /// ease-out(감속)은 기계가 돌다 멈추는 느낌 — 등속으로 돌리면 뚝 끊겨 보인다.
        /// </summary>
        private void TickSpin()
        {
            if (_container == null || _bigGear == null || _smallGear == null) return;
            if (_spinTimer < 0f) return;

            _spinTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_spinTimer / SpinSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            float bigDelta = -eased * 360f * BigSpinTurns;             // 시계 방향.
            float smallDelta = -bigDelta * MeshRatio;                   // 반시계 + 잇수비만큼 빠르게.

            // 작은 기어의 맞물림 위상은 도형에 이미 구워져 있으므로(BuildGearOutline의 phaseDegrees)
            // 회전에는 델타만 준다. 큰 기어는 중심선 정렬 각(BuildPhaseAngle)에서 출발한다.
            float bigPhase = BuildPhaseAngle;

            _bigGear.localRotation = Quaternion.Euler(0f, 0f, bigPhase + bigDelta);
            _smallGear.localRotation = Quaternion.Euler(0f, 0f, smallDelta);

            if (t < 1f) return;

            _spinTimer = -1f;
            _bigGear.localRotation = Quaternion.Euler(0f, 0f, bigPhase);
            _smallGear.localRotation = Quaternion.identity;

            // 회전이 끝난 <b>다음에</b> 창이 나타난다(사용자 요구: "기어가 회전하면서 캐릭터 창이 나오게끔").
            if (_window != null) _window.Open("우상단 톱니 아이콘 클릭");
        }

        private void TickHoverAlpha()
        {
            bool highlight = IsSpinning || (_window != null && _window.IsOpen) || IsCursorOverIcon();
            float target = highlight ? ActiveAlpha : IdleAlpha;
            float next = Mathf.MoveTowards(_alpha, target, AlphaFadeSpeed * Time.unscaledDeltaTime);
            if (Mathf.Approximately(next, _alpha)) return;
            _alpha = next;
            ApplyAlphaToAll();
        }

        private void ApplyAlphaToAll()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                c.a = _alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        // ==================== 클릭 ====================

        private void TickClick()
        {
            if (_buttonService == null) return;

            _clickPollTimer += Time.unscaledDeltaTime;
            if (_clickPollTimer < ClickPollInterval) return;
            _clickPollTimer = 0f;

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }
            bool rising = left && !_leftPrev;
            _leftPrev = left;
            if (!rising) return;

            // ★ 비침해 — 버튼이 눌렸다는 사실만으로는 아무 일도 하지 않는다. 커서가 아이콘 사각형
            //   안일 때만 반응한다(클래스 문서 "비침해 보장").
            if (!IsCursorOverIcon()) return;
            if (IsSpinning) return;

            if (_window != null && _window.IsOpen)
            {
                _window.Close("우상단 톱니 아이콘 클릭(토글 닫기)");
                return;
            }

            _spinTimer = 0f;
            Debug.Log("[톱니] 클릭 — 큰 기어와 작은 기어가 맞물려 돈 뒤 캐릭터 정보창이 열립니다.");
        }

        private bool IsCursorOverIcon()
        {
            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return false;
            Vector2 cursor = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config);
            return IconScreenRect.Contains(cursor);
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
