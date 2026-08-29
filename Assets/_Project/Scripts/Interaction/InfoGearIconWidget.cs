using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 화면 우상단 상시 톱니 아이콘 — 정보/장비 창의 <b>주 진입점</b>.
    /// 2026-08-29 사용자 원문: "개발하고 있는 이 캐릭터의 컨셉이 바탕화면에서 살고있는 코워커(동료)야.
    /// 그러니까 실행시키면 바탕화면 오른쪽 상단에 기어 표시같은걸 띄워놓고 클릭하면 기어가 회전하면서
    /// 캐릭터 창이 나오게끔".
    ///
    /// ============================================================================
    /// 왜 uGUI가 아니라 LineRenderer인가
    /// ============================================================================
    /// 이 앱의 시각 요소는 전부 프로시저럴 선화다(스프라이트 에셋이 없다). 톱니를 uGUI Image로 그리려면
    /// 텍스처가 필요하고, 그러면 이 앱에서 유일하게 "그림 파일에 의존하는 요소"가 된다. 캐릭터와 같은
    /// LineRenderer + <b>캐릭터의 머티리얼을 빌려 쓰는</b> 관례(GraffitiRenderer 등 8개 렌더러와 동일)를
    /// 그대로 따르면 그림체도 일관되고 의존성도 늘지 않는다.
    ///
    /// ============================================================================
    /// 크기는 <b>화면 고정</b>이다 — 캐릭터 배율을 따라가지 않는다
    /// ============================================================================
    /// 캐릭터에 붙는 액세서리(CharacterAccessoryRenderer)와 정반대의 규칙이다. 이건 캐릭터의 일부가
    /// 아니라 <b>화면 구석에 붙은 작은 UI 버튼</b>이라, 캐릭터가 커지고 작아진다고 함께 커지면
    /// 이상하다(리더 지시). 그래서 크기/여백을 <b>OS 포인트</b>로 정하고
    /// ScreenCoordinateConverter로 Unity 픽셀 -> 월드 유닛으로 환산한다(Retina에서도 물리적으로 같은 크기).
    ///
    /// ============================================================================
    /// 클릭 판정 — 기존 메커니즘을 그대로 재사용한다(새로 만들지 않는다)
    /// ============================================================================
    /// Interaction/StickmanClickHitbox.cs / TodoPostItWidget.cs / AppControlDirector.cs가 이미 쓰는
    /// <b>두 겹</b> 그대로다:
    ///  (1) 아이콘 사각형을 덮는 isTrigger BoxCollider2D — UniWindowController의 hitTestType=Raycast가
    ///      "커서 아래 Collider2D가 있는가"로 클릭관통을 판정하므로, 이 콜라이더가 있는 <b>그 작은
    ///      영역만</b> 클릭을 받고 나머지 화면은 100% 관통 그대로다.
    ///  (2) 전역 폴링(IGlobalPointerButtonService + 커서 좌표) — macOS에서 비활성 앱의 첫 클릭이
    ///      "앱 활성화"에만 소비되는 경우에도 확실히 잡는다.
    /// <b>비침해 보장</b>: (2)는 "버튼이 눌렸다"만으로는 아무 일도 하지 않는다. 반드시 그 순간 커서가
    /// 아이콘 사각형 <b>안</b>일 때만 반응하므로, 근처의 다른 창을 클릭하는 것은 두 경로 어느 쪽으로도
    /// 잡히지 않는다(StickmanClickHitbox가 캐릭터 콜라이더로 하는 것과 같은 논리, 같은 코드 모양).
    ///
    /// <b>메뉴바를 피한다</b>: 세로 여백 <see cref="MarginTopPoints"/>는 macOS 메뉴바(노치 디스플레이
    /// 기준 최대 약 38pt)보다 확실히 아래에 아이콘을 놓기 위한 값이다. 메뉴바/제어센터 위에 콜라이더가
    /// 겹치면 사용자가 메뉴바를 클릭하지 못하게 되어 비침해 원칙 위반이 된다.
    ///
    /// ============================================================================
    /// 클릭 -> 회전 -> 창 열림
    /// ============================================================================
    /// 클릭 즉시 창을 띄우지 않고 <see cref="SpinSeconds"/> 동안 <see cref="SpinTurns"/>바퀴를 감속
    /// (ease-out)으로 돌린 뒤 <see cref="CharacterInfoWindow.Open"/>을 부른다. "눌렀다"는 피드백이
    /// 먼저 오고 창이 뒤따르는 흐름이라, 클릭이 먹었는지 알 수 없는 오버레이 앱의 고질적 불확실성이
    /// 사라진다.
    /// </summary>
    public sealed class InfoGearIconWidget : MonoBehaviour
    {
        // ==================== 화면 고정 치수(전부 OS 포인트) ====================

        /// <summary>화면 오른쪽 끝에서 아이콘 <b>중심</b>까지의 거리.</summary>
        private const float MarginRightPoints = 30f;

        /// <summary>화면 위쪽 끝에서 아이콘 <b>중심</b>까지의 거리. macOS 메뉴바(최대 약 38pt)보다
        /// 확실히 아래여야 한다 — 클래스 문서 "메뉴바를 피한다" 참고.</summary>
        private const float MarginTopPoints = 58f;

        private const float OuterRadiusPoints = 13f;   // 톱니 끝까지의 반지름.
        private const float RootRadiusPoints = 10f;    // 톱니 골(이 뿌리).
        private const float HubRadiusPoints = 4.6f;    // 가운데 구멍.
        private const float StrokeWidthPoints = 1.7f;
        private const float HitPaddingPoints = 5f;     // 클릭 판정 여유(작은 아이콘이라 조금 넉넉하게).
        private const int ToothCount = 8;

        private const float SpinSeconds = 0.42f;
        private const float SpinTurns = 1.25f;
        // 평소에는 은은하게(관찰형 앱 — 화면을 지배하지 않는다). 0.55로 시작했는데 어두운 창 위에서
        // 거의 보이지 않아 0.70으로 올렸다(육안 검증).
        private const float IdleAlpha = 0.70f;
        private const float ActiveAlpha = 0.95f;       // 커서가 위에 있거나 창이 열려 있을 때.
        private const float AlphaFadeSpeed = 6f;

        private const float ClickPollInterval = 0.05f;
        private const int SortingOrder = 40;           // 캐릭터/액세서리보다 위(화면 UI다).

        private StickmanAgent _agent;
        private StickConfig _config;
        private CharacterInfoWindow _window;
        private IGlobalPointerButtonService _buttonService;
        private Camera _camera;

        private GameObject _container;
        private LineRenderer _gearLine;
        private LineRenderer _hubLine;
        private BoxCollider2D _clickTarget;
        private Material _lineMaterial;

        private float _spinTimer = -1f;   // 음수 = 회전 중 아님.
        private float _alpha = IdleAlpha;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private bool _builtGeometry;
        private float _builtRadiusWorld = -1f;

        /// <summary>지금 회전 연출 중인가(테스트/진단 전용).</summary>
        public bool IsSpinning => _spinTimer >= 0f;

        /// <summary>아이콘 중심의 Unity 스크린 좌표(픽셀). 실측 검증용.</summary>
        public Vector2 IconScreenCenter { get; private set; }

        /// <summary>아이콘 히트 사각형(Unity 스크린 픽셀). 실측 검증용 — "이 밖에서는 절대 안 걸린다"를
        /// 테스트가 직접 확인할 수 있게 노출한다.</summary>
        public Rect IconScreenRect { get; private set; }

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 라이벌 복제본에 이 컴포넌트가 남아 있어도
            // 톱니가 두 개 겹쳐 뜨지 않게 하는 2차 방어(1차는 SceneBootstrapper의 제거).
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
            Debug.Log("[톱니] 준비 완료 — 화면 우상단에 상시 표시됩니다(오른쪽 " +
                $"{MarginRightPoints:F0}pt / 위 {MarginTopPoints:F0}pt, 반지름 {OuterRadiusPoints:F0}pt). " +
                "클릭하면 한 바퀴 돈 뒤 캐릭터 정보창이 열립니다. " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI/콜라이더 경로만")}. " +
                "★ 클릭 판정은 이 작은 사각형 안에서만 일어나며, 그 밖은 100% 클릭관통 그대로입니다.");
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

        /// <summary>화면 우상단 고정 위치로 매 프레임 옮긴다 — <b>캐릭터 위치와 완전히 무관</b>하다
        /// (캐릭터가 화면 어디에 있든 아이콘은 같은 자리다).</summary>
        private void PlaceOnScreen()
        {
            float depth = Mathf.Abs(_camera.transform.position.z);

            // OS 포인트 -> Unity 픽셀(Retina 대응) -> 월드 유닛.
            float marginRightPx = ScreenCoordinateConverter.CanvasToUnityScreen(MarginRightPoints, _config);
            float marginTopPx = ScreenCoordinateConverter.CanvasToUnityScreen(MarginTopPoints, _config);
            IconScreenCenter = new Vector2(Screen.width - marginRightPx, Screen.height - marginTopPx);

            float outerPx = ScreenCoordinateConverter.CanvasToUnityScreen(OuterRadiusPoints, _config);
            float hitPx = ScreenCoordinateConverter.CanvasToUnityScreen(OuterRadiusPoints + HitPaddingPoints, _config);
            IconScreenRect = new Rect(IconScreenCenter.x - hitPx, IconScreenCenter.y - hitPx, hitPx * 2f, hitPx * 2f);

            Vector3 centerWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x, IconScreenCenter.y, depth));
            Vector3 edgeWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x + outerPx, IconScreenCenter.y, depth));
            float outerWorld = Mathf.Abs(edgeWorld.x - centerWorld.x);
            Vector3 hitEdgeWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x + hitPx, IconScreenCenter.y, depth));
            float hitWorld = Mathf.Abs(hitEdgeWorld.x - centerWorld.x);

            EnsureBuilt(outerWorld);
            if (_container == null) return;

            _container.transform.position = new Vector3(centerWorld.x, centerWorld.y, 0f);
            if (_clickTarget != null)
            {
                _clickTarget.transform.position = _container.transform.position;
                _clickTarget.size = new Vector2(hitWorld * 2f, hitWorld * 2f);
            }
        }

        /// <summary>배율(화면 해상도/DPI)이 바뀌지 않으면 도형을 다시 만들지 않는다 — 24시간 상주 앱.</summary>
        private void EnsureBuilt(float outerWorld)
        {
            if (_builtGeometry && Mathf.Abs(outerWorld - _builtRadiusWorld) < outerWorld * 0.01f) return;
            Build(outerWorld);
            _builtRadiusWorld = outerWorld;
            _builtGeometry = true;
        }

        private void Build(float outerWorld)
        {
            if (_container != null) Destroy(_container);

            _lineMaterial = ResolveLineMaterial();
            Color ink = _config != null ? _config.ResolveInkColor() : Color.black;

            _container = new GameObject("InfoGearIcon");
            _container.transform.SetParent(null, false); // 씬 루트 — 캐릭터가 걷거나 랙돌로 회전해도 따라 돌면 안 된다.

            float rootWorld = outerWorld * (RootRadiusPoints / OuterRadiusPoints);
            float hubWorld = outerWorld * (HubRadiusPoints / OuterRadiusPoints);
            float strokeWorld = outerWorld * (StrokeWidthPoints / OuterRadiusPoints);

            _gearLine = CreateLine("GearTeeth", BuildGearOutline(outerWorld, rootWorld), ink, strokeWorld, loop: true);
            _hubLine = CreateLine("GearHub", BuildCircle(hubWorld, 14), ink, strokeWorld, loop: true);

            if (_clickTarget == null)
            {
                var hitGo = new GameObject("InfoGearClickTarget");
                _clickTarget = hitGo.AddComponent<BoxCollider2D>();
                _clickTarget.isTrigger = true; // 캐릭터가 톱니에 부딪혀 튕기면 안 된다(메뉴 차단막과 같은 이유).
            }
        }

        /// <summary>톱니 윤곽 — 한 이당 4점(골-마루-마루-골)으로 사각 이를 만든다.</summary>
        private static Vector3[] BuildGearOutline(float outer, float root)
        {
            int points = ToothCount * 4;
            var pts = new Vector3[points];
            float step = Mathf.PI * 2f / points;
            for (int i = 0; i < points; i++)
            {
                int phase = i % 4;
                float r = (phase == 1 || phase == 2) ? outer : root;
                float a = i * step;
                pts[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return pts;
        }

        private static Vector3[] BuildCircle(float radius, int segments)
        {
            var pts = new Vector3[Mathf.Max(3, segments)];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            }
            return pts;
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
            lr.numCornerVertices = 2;
            lr.sortingOrder = SortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            return lr;
        }

        // ==================== 회전 연출 ====================

        private void TickSpin()
        {
            if (_container == null) return;
            if (_spinTimer < 0f) return;

            _spinTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_spinTimer / SpinSeconds);
            // ease-out(감속) — 기계가 돌다 멈추는 느낌. 등속으로 돌리면 뚝 끊겨 보인다.
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            _container.transform.localRotation = Quaternion.Euler(0f, 0f, -eased * 360f * SpinTurns);

            if (t < 1f) return;

            _spinTimer = -1f;
            _container.transform.localRotation = Quaternion.identity;
            // 회전이 끝난 <b>다음에</b> 창이 나타난다(사용자 요구: "기어가 회전하면서 캐릭터 창이 나오게끔").
            if (_window != null) _window.Open("우상단 톱니 아이콘 클릭");
        }

        private void TickHoverAlpha()
        {
            bool highlight = IsSpinning || (_window != null && _window.IsOpen) || IsCursorOverIcon();
            float target = highlight ? ActiveAlpha : IdleAlpha;
            _alpha = Mathf.MoveTowards(_alpha, target, AlphaFadeSpeed * Time.unscaledDeltaTime);
            ApplyAlpha(_gearLine);
            ApplyAlpha(_hubLine);
        }

        private void ApplyAlpha(LineRenderer lr)
        {
            if (lr == null) return;
            Color c = lr.startColor;
            if (Mathf.Approximately(c.a, _alpha)) return;
            c.a = _alpha;
            lr.startColor = c;
            lr.endColor = c;
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
            Debug.Log("[톱니] 클릭 — 톱니가 한 바퀴 돈 뒤 캐릭터 정보창이 열립니다.");
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
