using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 화면 <b>왼쪽 아래 구석 호버 패널</b> — docs/UX_FLOW.md 34-4 ~ 34-6.
    /// 커서를 구석에 두면 손잡이가 살짝 나타나고, 계속 머무르면 <b>크기 다이얼</b>이 열리며,
    /// 위로 끌어올리면 <b>캐릭터만 보여주는 미리보기 카드</b>가 함께 들어 있는 하나의 패널이 된다.
    ///
    /// ============================================================================
    /// "클릭관통 ON인데 호버를 어떻게 감지하나" — 새 기술이 필요 없다 (34-4-1)
    /// ============================================================================
    /// 이 앱의 커서 감지는 클릭관통과 <b>완전히 분리된 경로</b>로 이미 존재한다.
    /// <see cref="ICursorPositionService"/>의 클래스 문서가 <i>"클릭 관통(SetClickThrough) ON 상태에서도
    /// 커서 근접 반응을 위해 전역 커서 좌표 폴링이 독립적으로 필요"</i>라고 <b>바로 이 요구사항을 위해</b>
    /// 만들어졌다고 못박고 있고, <see cref="InfoGearIconWidget"/>가 이미 같은 방식으로 돌고 있다.
    ///
    /// → 호버 감지에는 콜라이더도, 창 포커스도, 클릭관통 해제도 필요 없다. <b>좌표 하나를 받아 사각형과
    /// 비교할 뿐</b>이다. 패널이 숨어 있는 동안 화면 왼쪽 아래에는 <b>콜라이더가 하나도 없다</b> —
    /// 그 구석의 클릭관통은 100% 그대로다.
    ///
    /// 콜라이더가 필요한 시점은 패널이 실제로 보이기 시작한 뒤(PEEK 이상)뿐이다. 위로 끌어올리려면
    /// 그 위에서 마우스 다운이 우리 앱에 잡혀야 하고, 안 그러면 그 클릭이 바탕화면/Finder로 새어 나가
    /// 유저의 선택을 해제한다(그게 곧 비침해 위반이다).
    ///
    /// ============================================================================
    /// 상태 머신 (34-4-3) — ★ 대사가 아니라 <b>상태</b>가 먼저다
    /// ============================================================================
    /// <code>
    ///           안에 180ms                        계속 머무름 +260ms
    ///  HIDDEN ────────────► PEEK ──────────────────────────────────► COLLAPSED
    ///    ▲                   │  이탈 +320ms                            │  ▲ 위로 끌기
    ///    └───────────────────┴──── 이탈 +400ms ──────────────────────┘  ▼
    ///                    ESC / IsSuspended / 880창 열림             EXPANDED
    /// </code>
    /// 2단계로 나눈 이유: <b>스쳐 지나가면 얇은 손잡이만 잠깐 보이고 사라진다</b>(방해 0). 머무르면
    /// 열린다. "숨겨진 컨트롤"의 발견성과 비침해를 동시에 만족시키는 유일한 구조다.
    ///
    /// ============================================================================
    /// 두 드래그가 충돌하지 않는 규칙 — <b>영역으로 나눈다</b> (34-5-1)
    /// ============================================================================
    /// 패널 안에는 드래그가 두 종류다(값 조절 / 펼치기). 모드 전환 없이 <b>누른 위치</b>로 결정한다.
    /// 다이얼 원환(20 ≤ r ≤ 90pt) 안에서 시작하면 값 조절, 그 밖(그립/카드)에서 시작하면 펼침/접힘이다.
    /// 기어 아이콘은 같은 문제를 "판정을 뗄 때로 미루는 것"으로 풀었지만, 여기서는 두 동작이 동시에
    /// 진행될 수 없는 <b>별개 영역</b>에 있으므로 누른 순간에 확정해도 안전하다. 더 단순한 쪽을 택한다.
    ///
    /// ============================================================================
    /// 절대 불변 원칙 (34-8)
    /// ============================================================================
    ///  · 원칙 1(행동-텍스트 싱크): 다이얼 숫자·켜진 눈금 수·카드 속 크기·실캐릭터 크기가 전부 같은
    ///    하나의 인덱스에서 파생된다. "1.50×라고 떠 있는데 캐릭터는 그대로"가 구조적으로 불가능하다.
    ///  · 원칙 2(비침해): <see cref="StickmanAgent.IsSuspended"/>면 <b>연출 없이 그 프레임에</b> 거둔다
    ///    (차단막 포함). 복귀해도 자동으로 다시 열지 않는다. 숨어 있는 동안 콜라이더 0개.
    ///  · 원칙 3(유저 자산 불변): 이 패널은 <b>읽기만</b> 한다 — 커서 좌표, Dock 두께, 캐릭터 상태.
    ///    카드 속 캐릭터는 화면 밖 10200 좌표의 전용 미니 피규어이지 실캐릭터가 아니다.
    ///  · 탈출구 4중: 커서를 치운다 / ESC(+3초 억제) / 기어 메뉴에서 끄기(영구) / 전체화면 자동 숨김.
    /// </summary>
    public sealed class CornerHoverPanel : MonoBehaviour
    {
        // ==================== 감지 영역 (34-4-2) ====================

        /// <summary>감지 영역 크기(OS 포인트). 아무것도 그리지 않는다 — 좌표 비교만 한다.</summary>
        private const float DetectWidthPoints = 140f;
        private const float DetectHeightPoints = 120f;

        /// <summary>화면 구석에서 들여놓는 최소 여백. macOS는 화면 <b>네 모서리</b>에 핫코너
        /// (Mission Control/화면 보호기)를 걸 수 있다 — 우리 패널이 정확히 그 자리에서 뜨면 유저가
        /// Mission Control을 부르려다 매번 우리 패널을 부른다. 24pt 안쪽으로 들여 그 충돌을
        /// <b>구조적으로</b> 없앤다.</summary>
        private const float CornerMarginPoints = 24f;

        /// <summary>Dock 위에 겹쳐 뜨면 앱 전환을 방해한다(원칙 2). Dock 두께 위로 이만큼 더 띄운다.</summary>
        private const float DockGapPoints = 12f;

        // ==================== 크기 / 타이밍 (34-4-3 / 34-4-4 / 34-5-2) ====================

        private const float PeekWidthPoints = 104f;
        private const float PeekHeightPoints = 14f;
        private const float PanelWidthPoints = 264f;
        private const float CollapsedHeightPoints = 148f;

        /// <summary>public인 이유: 이 값은 카드 하단(<see cref="CardHeightPoints"/> +
        /// <see cref="CardRisePoints"/>)과 다이얼 원환 상단(<see cref="DialCenterFromBottomPoints"/> +
        /// <see cref="SizeDialWidget.HitOuterRadius"/>)이 정확히 맞닿도록 손으로 맞춘 값이다 —
        /// <c>CornerHoverPanelGeometryInvariantTests</c>가 그 등식을 잠근다. 넷 중 하나만 바꾸면 원환이
        /// 카드 밑을 먹거나(클릭 오작동) 둘 사이에 보기 싫은 틈이 생긴다 — 반드시 그 테스트를 먼저
        /// 통과시켜라(통합검증 R2, M2).</summary>
        public const float ExpandedHeightPoints = 392f;

        private const float HiddenToPeekSeconds = 0.18f;
        private const float PeekToCollapsedSeconds = 0.26f;
        private const float CollapsedToPeekSeconds = 0.32f;
        private const float PeekToHiddenSeconds = 0.40f;

        /// <summary>패널 사각형 바깥으로 이만큼 나가야 "떠났다"로 본다 — 경계선 위에서 커서가 1px 떨릴 때
        /// 패널이 깜빡이는 것을 막는다.</summary>
        private const float LeaveSlackPoints = 24f;

        /// <summary>값이 바뀐 직후 이 시간 동안은 어떤 이유로도 접히지 않는다 — 손을 떼자마자
        /// 사라지면 <b>방금 바꾼 결과를 볼 수 없다</b>.</summary>
        private const float ChangeHoldSeconds = 0.8f;

        /// <summary>ESC로 껐을 때의 억제 시간. 이게 없으면 커서가 아직 감지 영역 안이라 180ms 뒤 다시
        /// 뜨고, 그러면 ESC는 탈출구가 아니라 <b>깜빡임 버튼</b>이 된다.</summary>
        private const float EscapeSuppressSeconds = 3f;

        /// <summary>전체화면 게임을 끄는 순간 커서가 구석에 있는 것은 흔하다 — 복귀 직후 오폭 방지.</summary>
        private const float ResumeSuppressSeconds = 1f;

        // ★ 2026-09-01 — 아래 넷(PeekGrow/Collapse/ContentReveal/ContentHide)을 public으로 올린다.
        //   값은 한 글자도 바뀌지 않는다. 이유는 <b>테스트가 이 수를 베껴 적지 않게</b> 하기 위해서다:
        //   등장/퇴장 연출은 프레임이 아니라 <b>벽시계 초</b>(Time.unscaledDeltaTime)로 굴러가는데,
        //   CornerHoverPanelTests가 대기 예산을 "프레임 180번"으로 적어 두는 바람에 배치 모드
        //   (실측 0.11~0.45ms/프레임 = 2,200~8,900fps)에서 180프레임이 고작 0.014~0.082초밖에 되지
        //   않아, 상자가 게이트(0.9 × 0.14s = 0.126s)에 닿기도 전에 판정이 끝나 10회 중 10회 실패했다.
        //   ContentGateBlend를 public으로 둔 것과 같은 이유이고 같은 관례다.
        public const float PeekGrowSeconds = 0.14f;
        private const float ExpandSeconds = 0.20f;
        public const float CollapseSeconds = 0.14f;

        /// <summary>
        /// ★ 2026-08-31 사용자 신고 <i>"크기조절 원이 먼저 떠 있고 상자가 나중에 커짐"</i>의 수정 상수.
        ///
        /// <para><b>무엇이 잘못됐었나</b>: 다이얼과 카드는 패널의 <b>자식</b>인데 패널에는 마스크가 없다.
        /// 그래서 상자 크기(<see cref="_peekBlend"/>)와 내용물의 보임 여부가 <b>서로 다른 출처</b>에서
        /// 나왔고, PEEK(104×14pt)에서도 다이얼(눈금이 패널 바닥 기준 27~129pt를 차지한다)이 100%
        /// 그려졌다. 즉 <b>상자보다 원이 먼저</b> 보이고, 260ms 뒤에야 상자가 원을 감싸며 자랐다.</para>
        ///
        /// <para><b>규칙</b>: 내용물(다이얼·카드)은 상자가 이만큼 자란 뒤에만 알파가 오른다. 값 0.9는
        /// 취향이 아니라 <b>기하에서 유도</b>됐다 — 눈금 꼭대기(78 + <see cref="SizeDialWidget.TickVisualOuterRadius"/>
        /// = 129pt)가 상자 안에 들어가는 최소 블렌드가 0.858이다. <c>CornerHoverPanelTests</c>의
        /// <c>내용물은_상자가_다_자란_뒤에만_보인다</c>가 이 등식을 잠근다.</para>
        /// </summary>
        public const float ContentGateBlend = 0.9f;

        public const float ContentRevealSeconds = 0.10f;

        /// <summary>사라질 때는 나타날 때보다 빠르다 — 상자가 줄기 시작하려면 내용물이 <b>먼저</b>
        /// 다 사라져야 하고(등장의 정확한 역순), 그동안 상자가 멈춰 서 있기 때문이다.</summary>
        public const float ContentHideSeconds = 0.07f;

        /// <summary>HIDDEN/PEEK → 내용물이 <b>완전히</b> 보일 때까지 걸리는 벽시계 시간(초).
        /// 상자가 게이트까지 자라고(<see cref="ContentGateBlend"/> × <see cref="PeekGrowSeconds"/>),
        /// 상자가 다 자란 뒤(나머지 <see cref="PeekGrowSeconds"/>) 내용물이 오르는
        /// (<see cref="ContentRevealSeconds"/>) 순서라, 상한은 이 둘의 <b>합</b>이다.
        /// 테스트는 이 값에서 대기 예산을 유도한다 — 숫자를 베껴 적으면 상수가 바뀌는 순간 거짓 실패한다.</summary>
        public const float OpenSequenceSeconds = PeekGrowSeconds + ContentRevealSeconds;

        /// <summary>내용물이 다 사라지고(<see cref="ContentHideSeconds"/>) 그 뒤 상자가 손잡이까지
        /// 줄어드는(<see cref="CollapseSeconds"/>) 데 걸리는 벽시계 시간(초).</summary>
        public const float CloseSequenceSeconds = ContentHideSeconds + CollapseSeconds;

        /// <summary>이만큼 보여야 누를 수 있다. <b>보이지 않는 다이얼이 클릭을 먹으면</b> "보이는 곳을
        /// 누르면 먹는다"가 거꾸로 뒤집힌다(SizeDialWidget.SetCenterInParentPoints 문서와 같은 원칙).</summary>
        private const float ContentInteractiveReveal = 0.5f;

        /// <summary>펼침 드래그의 데드존(pt). 이 아래는 "클릭"이다(손떨림으로 열리지 않게).</summary>
        private const float ExpandDeadZonePoints = 10f;

        /// <summary>데드존을 지난 뒤 이만큼 더 끌면 완전히 펼쳐진다. 패널 높이 증가분(244pt)보다
        /// <b>짧은 손동작</b>이다 — 끌어야 하는 거리가 결과 높이와 같으면 손목이 화면 밖으로 나간다.</summary>
        private const float ExpandTravelPoints = 96f;

        /// <summary>뗐을 때 이 이상이면 펼침 확정. 절반보다 살짝 낮다 — 사람은 "충분히 끌었다"를
        /// 실제보다 늦게 느낀다.</summary>
        private const float ExpandCommitProgress = 0.45f;

        /// <summary>드래그를 발견하지 못한 사용자를 위한 <b>두 번째 길</b>. 숨은 제스처만 있는 UI는
        /// 접근성 실패다.</summary>
        private const float ToggleMovePoints = 6f;
        private const float ToggleSeconds = 0.25f;

        // ==================== 카드 (34-6) ====================

        /// <summary>카드 크기. 액자는 <see cref="CharacterInfoWindow.PortraitContentSize"/>와
        /// <b>정확히 같은 값</b>을 쓴다 — <see cref="CharacterPortraitStage.DesignAspect"/>가 그 상수에서
        /// 파생되고 카메라 프레이밍이 그 종횡비 위에서 이미 검증됐기 때문이다(33-7/33-8).
        /// 종횡비를 새로 정하면 왕관/털모자 꼭대기가 잘리는지를 처음부터 다시 유도해야 한다.
        /// public인 이유: <see cref="ExpandedHeightPoints"/> 문서 참고 — 이 값이 바뀌면 그 등식도
        /// 같이 깨진다.</summary>
        public const float CardWidthPoints = 232f;
        public const float CardHeightPoints = 212f;
        private const float CardCaptionHeightPoints = 22f;

        /// <summary>카드는 t=0.35부터 알파가 오른다. 처음부터 보이면 "패널이 커진다"가 아니라
        /// "카드가 늘어난다"로 읽힌다.</summary>
        private const float CardFadeStart = 0.35f;

        /// <summary>public인 이유: <see cref="ExpandedHeightPoints"/> 문서 참고.</summary>
        public const float CardRisePoints = 12f;

        /// <summary>다이얼 중심은 <b>패널 바닥 기준</b>으로 고정이다. 그래서 릴스 ①의 "가장자리에서
        /// 반쯤 나온 다이얼" → ②의 "완전히 드러난 링"이 크롭이 아니라 <b>패널 높이</b>로 재현된다.
        /// public인 이유: <see cref="ExpandedHeightPoints"/> 문서 참고.</summary>
        public const float DialCenterFromBottomPoints = 78f;

        private const float GripWidthPoints = 36f;
        private const float GripHeightPoints = 3f;
        private const float GripFromTopPoints = 8f;

        /// <summary>포스트잇(30000)/정보창(31000)/부채꼴(31500)보다 위, 팝오버(31700)보다 아래.
        /// 이 패널은 화면 좌하단이라 실제로 겹치지 않지만, 겹친다면 방금 사용자가 부른 팝오버가 이겨야 한다.</summary>
        private const int SortingOrder = 31600;

        private const float PollInterval = 0.05f;

        // ==================== 상태 ====================

        private enum Stage { Hidden = 0, Peek = 1, Collapsed = 2, Expanded = 3 }

        private StickmanAgent _agent;
        private StickConfig _config;
        private CharacterInfoWindow _infoWindow;
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private CanvasGroup _group;
        private RectTransform _panel;
        private Image _panelBody;
        private RectTransform _cardRoot;
        private CanvasGroup _cardGroup;
        private RawImage _cardImage;
        private Text _cardFallback;
        private Text _cardCaption;
        private BoxCollider2D _clickBlocker;

        private SizeDialWidget _dial;
        private CharacterPortraitStage _stage;
        private float _lastPixelsPerCanvasUnit = -1f;

        private Stage _stage_ = Stage.Hidden;
        private float _insideTimer;
        private float _outsideTimer;
        private float _pollTimer;
        private float _suppressUntil;
        private float _holdUntil;

        /// <summary>펼침 진행도 0..1. 애니메이션과 드래그가 같은 값을 쓴다(두 경로가 갈라지지 않게).</summary>
        private float _expand;
        private float _expandTarget;

        /// <summary>내용물(다이얼·카드) 등장 진행도 0..1. <b>상자의 성장 진행도에서만 파생</b>된다
        /// (<see cref="ContentGateBlend"/>) — 두 그림이 각자의 타이밍을 갖는 순간 등장 순서가 어긋난다.</summary>
        private float _contentReveal;

        private bool _dragActive;
        private bool _dragIsDial;
        private Vector2 _dragStart;
        private float _dragStartTime;
        private float _expandAtDragStart;
        private bool _leftPrev;
        private bool _leftInitialized;

        /// <summary>이 플랫폼이 전역 커서 좌표를 주는가. false면 감지 영역이 통째로 비활성이다
        /// (부품은 만들어 두되 스스로 나타나지 않는다 — Start의 문단 참고).</summary>
        private bool _hoverSupported;

        private bool _hiddenBySuspend;
        private bool _wasSuspended;

        /// <summary>★ 2026-09-01 — 적용 대기(34-3-6의 2단계 적용) 상태는 이제 이 패널이 갖지 않는다.
        /// <see cref="CharacterScaleController"/>가 게이트·대기·강제적용을 통째로 소유하고, 여기서는
        /// 그 상태를 <b>읽어서 캡션만</b> 그린다 — 설정창 슬라이더가 같은 규칙을 다시 구현하지 않게
        /// 하기 위해서다(35-1-3 ①). 이 필드는 캡션을 실제로 바꾼 프레임에만 건드리려고 남긴 캐시다.</summary>
        private bool _pendingCaptionShown;

        private string _lastCaption;

        // ==================== 진단/테스트용 공개 상태 ====================

        public bool IsVisible => _stage_ != Stage.Hidden;
        public bool IsExpanded => _stage_ == Stage.Expanded;
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;

        /// <summary>상자가 손잡이(PEEK)에서 패널(COLLAPSED)까지 자란 진행도 0..1(진단/테스트용).</summary>
        public float PanelGrowProgress => _peekBlend;

        /// <summary>내용물(다이얼·카드)이 드러난 진행도 0..1(진단/테스트용).</summary>
        public float ContentRevealProgress => _contentReveal;

        /// <summary>다이얼 그림이 지금 얼마나 보이는가 0..1 — <b>위젯이 실제로 들고 있는 값</b>을 읽는다
        /// (패널이 시킨 값이 아니라). 둘이 다르면 배선이 끊긴 것이다.</summary>
        public float DialRevealProgress => _dial != null ? _dial.Reveal : 0f;

        /// <summary>지금 보이는 패널 사각형(Unity 스크린 픽셀). 차단막이 정확히 이 크기다.</summary>
        public Rect PanelScreenRect { get; private set; }

        /// <summary>감지 영역(Unity 스크린 픽셀). 여기에는 <b>아무것도 그리지 않는다</b>.</summary>
        public Rect DetectScreenRect { get; private set; }

        public float DialValue => _dial != null ? _dial.Value : 0f;

        // ==================== 수명 주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            _infoWindow = GetComponent<CharacterInfoWindow>();
            BuildUi();
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;

            // 배율의 단일 소스에게 "이 캐릭터에 넣어라"를 알려 준다(설정창도 같은 줄을 갖는다 —
            // 둘 중 무엇이 먼저 돌아도 되고, 씬이 다시 로드되면 새 인스턴스로 갈아탄다).
            CharacterScaleController.Bind(_agent);

            // 커서 좌표를 못 얻는 플랫폼(모바일 스크린샷 백드롭 모드 / 폴백 / 배치 모드)에서는
            // "구석 호버" 개념 자체가 없다(터치엔 호버가 없다) — 34-4-6의 첫 번째 "거부" 상태다.
            //
            // ★ 이때 <b>감지만 끄고 부품은 그대로 둔다</b>(예전 구현은 캔버스/촬영장을 파괴했다).
            //   파괴하면 "이 패널이 어떻게 보이는지"를 확인할 방법이 사라져 배치 모드에서 증거 캡처도,
            //   보이는 상태의 회귀 테스트도 불가능해진다 — 실제로 캡처 하네스가 빈 화면을 찍었다.
            //   부품은 캔버스가 비활성이고 촬영장 카메라도 꺼져 있어 매 프레임 비용이 0이며,
            //   880 정보창도 같은 방식으로 촬영장을 상시 들고 있다(같은 관례).
            _hoverSupported = _agent != null && _agent.TryGetCursorPosition(out _);
            if (!_hoverSupported)
            {
                Debug.Log("[구석패널] 전역 커서 좌표를 얻을 수 없어 호버 감지를 끕니다 — 이 플랫폼에는 " +
                    "호버가 없습니다(모바일 대체 진입은 미설계, docs/UX_FLOW.md 34-9 #13). " +
                    "패널은 스스로 나타나지 않습니다.");
            }

            _restoreDeadline = Time.unscaledTime + RestoreGraceSeconds;
            RestoreSavedScale();
        }

        /// <summary>★ 다른 UI(설정창 슬라이더)가 배율을 바꾸면 이 다이얼도 같은 프레임에 따라온다 —
        /// 35-1-3 ①이 지적한 "설정창에서 바꾸고 구석 패널을 열면 옛 값을 가리킨다"(원칙 1 위반)의 수정.</summary>
        private void OnEnable()
        {
            StickmanEventBus.CharacterScaleChanged += OnCharacterScaleChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.CharacterScaleChanged -= OnCharacterScaleChanged;
            // 패널이 꺼진 채 차단막만 남으면 그 영역이 이유 없이 클릭관통 해제로 남는다(비침해).
            if (_clickBlocker != null) _clickBlocker.enabled = false;
        }

        private void OnCharacterScaleChanged(CharacterScaleChangeEvent e)
        {
            // 사용자가 지금 이 다이얼을 끌고 있으면 에코를 무시한다 — 안 그러면 끄는 손이 되돌려진다
            // (RestoreSavedScale 문서의 (c)와 같은 이유).
            if (_dial == null || (_dragActive && _dragIsDial)) return;
            _scaleRestored = true;   // 다른 UI가 값을 확정했다 = 복원이 더 이상 덮어쓸 자격이 없다.
            _dial.SetValue(e.Value);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_clickBlocker != null) Destroy(_clickBlocker.gameObject);
            if (_stage != null) Destroy(_stage.gameObject);
        }

        /// <summary>
        /// 저장된 크기를 <b>한 번만</b> 되돌린다. Start가 아니라 Update에서 재시도하는 이유는 톱니 위치
        /// 복원과 같다 — 저장 파일을 읽는 쪽(CharacterProgressionDirector.Start)과 실행 순서가
        /// 보장되지 않기 때문이다.
        ///
        /// <para>★ <b>반드시 끝이 있어야 한다</b>. 처음에는 "저장이 없으면 매 프레임 캐릭터의 현재
        /// 배율로 다이얼을 맞춘다"고 짰는데, 그러면 <b>드래그 중에 다이얼이 매 프레임 되돌려진다</b>
        /// (드래그 중에는 아직 실캐릭터에 적용하지 않으므로 두 값이 다른 것이 정상이다). 실제로
        /// 그 상태로 다이얼을 돌려도 값이 0.75에 붙박여 있었고, 회귀 테스트가 그걸 잡았다.
        /// 그래서 (a) 저장을 찾으면 즉시 끝, (b) 못 찾아도 <see cref="RestoreGraceSeconds"/> 뒤에는 끝,
        /// (c) 사용자가 다이얼을 잡는 순간 끝 — 셋 중 무엇이든 먼저 오면 다시는 덮어쓰지 않는다.</para>
        /// </summary>
        private bool _scaleRestored;

        /// <summary>저장 파일 로드를 기다려 주는 시간. 이 뒤로는 복원 시도를 멈춘다(위 문단 (b)).</summary>
        private const float RestoreGraceSeconds = 2f;

        private float _restoreDeadline;

        private void RestoreSavedScale()
        {
            if (_scaleRestored) return;

            // ★ 2026-09-01 — <b>적용 대기 중이면 복원은 끼어들지 않는다.</b>
            //   이 줄이 없으면 다음이 가능하다: 랙돌 중 설정창 슬라이더로 1.35를 고르면 컨트롤러는
            //   유예로 등록하면서 <b>저장 모델에는 즉시</b> 1.35를 적는데, 그 직후 아래 복원 분기가
            //   "저장값이 생겼다"고 보고 게이트 없이 즉시 적용해 버린다 = 유예가 조용히 무효화된다.
            //   (아래 (c) "사용자가 다이얼을 잡는 순간 끝"의 일반화 — 값을 잡는 손이 둘이 됐다.)
            //
            //   반대로 "컨트롤러에 값이 있으면 무조건 그만둔다"로 넓히면 안 된다: 컨트롤러는 정적이라
            //   씬을 다시 로드해도 값이 남는데, 새 캐릭터는 배포 기본 배율로 태어난다. 그때 복원을
            //   막으면 몸만 원래 크기인 화면이 된다(테스트가 실제로 그 상태를 잡아냈다).
            if (CharacterScaleController.HasPendingApply)
            {
                _scaleRestored = true;
                if (_dial != null) _dial.SetValue(CharacterScaleController.Value);
                return;
            }

            if (UiLayoutModel.HasCharacterScale)
            {
                _scaleRestored = true;

                // ★ 2026-08-31 상한 2.0 → 1.5(StickConfig.MaxCharacterScale). 이미 1.5를 넘겨 저장해 둔
                //   사용자가 있다. clamp를 여기서 <b>한 번</b> 하고 그 값을 저장 모델에도 되쓴다 —
                //   안 그러면 다이얼(ValueToIndex가 24칸으로 clamp)은 1.50×를 그리는데 저장 모델은
                //   2.00×로 남아 "표시와 진실이 둘"이 된다(원칙 1). 다이얼을 한 번도 안 만지면
                //   그 불일치가 영원히 남는다.
                float saved = UiLayoutModel.CharacterScale;
                float v = Mathf.Clamp(saved, StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale);
                if (!Mathf.Approximately(v, saved))
                {
                    UiLayoutModel.SetCharacterScale(v);
                    Debug.Log($"[구석패널] 저장된 크기 {saved:F2}×가 상한을 넘어 {v:F2}×로 낮췄습니다 " +
                        $"(상한 {StickConfig.MaxCharacterScale:F2}×).");
                }

                if (_dial != null) _dial.SetValue(v);
                // ★ 2026-09-01 — 적용을 직접 하지 않고 단일 소스를 지난다. 그래야 이 값이 설정창
                //   슬라이더에도 같은 프레임에 실려 간다(원칙 1). 복원은 게이트를 거치지 않는다 —
                //   시작 시점에 랙돌일 수 없고, 여기서 유예하면 첫 화면이 옛 크기로 뜬다.
                CharacterScaleController.AdoptRestored(v, "저장된 크기 복원");
                return;
            }

            // 저장이 없으면 <b>지금 프리팹에 구워진 배율</b>이 곧 현재 값이다.
            if (_dial != null && _agent != null) _dial.SetValue(_agent.CurrentCharacterScale);
            if (Time.unscaledTime >= _restoreDeadline) _scaleRestored = true;
        }

        // ==================== 루프 ====================

        private void Update()
        {
            // ★★ 원칙 2 — 전체화면 게임이 감지되면 연출 없이 그 프레임에 거둔다(차단막 포함).
            //    PopoverPanel.Update()의 같은 블록을 그대로 옮겼다(주석의 이유도 그대로 성립한다:
            //    연출 0.12초 동안에도 차단막이 살아 있고, macOS 히트테스트는 커서 아래 픽셀 알파를 본다).
            bool suspended = _agent != null && _agent.IsSuspended;
            if (suspended)
            {
                if (!_hiddenBySuspend)
                {
                    _hiddenBySuspend = true;
                    HideImmediately("전체화면 감지 — 자동 숨김(비침해 원칙 2)");
                }
                _wasSuspended = true;
                return;
            }
            if (_wasSuspended)
            {
                _wasSuspended = false;
                _hiddenBySuspend = false;
                _suppressUntil = Time.unscaledTime + ResumeSuppressSeconds;
            }

            // ★ 이 Update()는 숨어 있을 때도 매 프레임 돈다(구석 감지 폴링). 그래서 홀드 조건은
            // "열려 있다"가 아니라 <b>보이는 중</b>이어야 한다 — 상시 표면이 무조건 홀드를 걸면
            // 24시간 내내 Calm이 성립하지 않아 절감이 통째로 사라진다.
            // 보이는 동안은 정의상 커서가 구석에 머물러 있고(Peek 이상), 크기 다이얼 드래그는
            // TickPointer()가 Update()마다 OS 커서를 폴링해 따라가므로 30Hz 루프에서 계단식으로
            // 끊긴다 — 신고된 정보창 드래그와 정확히 같은 인과다.
            // SizeDialWidget은 MonoBehaviour가 아니라 이 패널이 직접 굴리는 평범한 클래스라,
            // 다이얼의 홀드도 여기 한 줄이 전부다(중복 배선 금지).
            if (IsVisible) FramePacing.HoldActiveForInteraction();

            RestoreSavedScale();
            TickPendingScale();

            // ESC — 즉시 숨김 + 억제. 억제 없는 탈출구는 탈출구가 아니다.
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                _suppressUntil = Time.unscaledTime + EscapeSuppressSeconds;
                HideImmediately($"ESC — {EscapeSuppressSeconds:F0}초 동안 다시 뜨지 않습니다");
                return;
            }

            ApplyCanvasScaleFactor();
            // 기하를 두 번 계산한다: 앞의 것은 <b>이번 프레임의 호버/포인터 판정</b>이 쓰고, 뒤의 것은
            // 애니메이션이 움직인 크기를 <b>같은 프레임의 배치/차단막</b>에 반영한다. 한 번만 계산하면
            // 차단막이 그림보다 한 프레임 늦어 펼치는 동안 가장자리 클릭이 새어 나간다.
            UpdateGeometry();
            TickHoverStateMachine();
            TickPointer();
            TickAnimation();
            UpdateGeometry();
            ApplyLayout();
            SyncClickBlocker();
            TickCard();
        }

        /// <summary>감지 영역이 지금 살아 있는가 — 34-4-6의 "거부" 상태 전부가 여기 모인다.</summary>
        private bool DetectionArmed
        {
            get
            {
                if (!_hoverSupported) return false;
                if (!UiLayoutModel.CornerPanelEnabled) return false;
                if (Time.unscaledTime < _suppressUntil) return false;
                // 880 정보창이 열려 있는 동안은 비활성 — 같은 캐릭터를 두 액자에 동시에 띄우지 않고,
                // 초상화 카메라 2대가 동시에 돌지 않는다(34-6-5 동시 표시 정책).
                if (_infoWindow != null && _infoWindow.IsOpen) return false;
                return true;
            }
        }

        private void TickHoverStateMachine()
        {
            // 드래그 중에는 접힘 타이머를 완전히 정지한다 — 끌다가 영역을 벗어나는 것은 정상 동작이다.
            if (_dragActive || HoldStageForTests) return;

            _pollTimer += Time.unscaledDeltaTime;
            bool poll = _pollTimer >= PollInterval || IsVisible;
            if (!poll) return;
            float dt = _pollTimer;
            _pollTimer = 0f;

            if (!DetectionArmed)
            {
                if (IsVisible) HideImmediately(ResolveDisarmReason());
                return;
            }

            bool inside = TryGetCursorUnityScreen(out Vector2 cursor) && IsCursorInActiveRect(cursor);
            if (inside) { _insideTimer += dt; _outsideTimer = 0f; }
            else { _outsideTimer += dt; _insideTimer = 0f; }

            switch (_stage_)
            {
                case Stage.Hidden:
                    // 창을 끌고 구석을 **스쳐 지나가는** 커서에는 반응하지 않는다(180ms = 의도적 정지의 하한).
                    if (_insideTimer >= HiddenToPeekSeconds) SetStage(Stage.Peek, "커서가 구석에 머무름");
                    break;

                case Stage.Peek:
                    if (_insideTimer >= HiddenToPeekSeconds + PeekToCollapsedSeconds)
                        SetStage(Stage.Collapsed, "계속 머무름");
                    else if (_outsideTimer >= PeekToHiddenSeconds)
                        SetStage(Stage.Hidden, "커서 이탈");
                    break;

                case Stage.Collapsed:
                case Stage.Expanded:
                    if (Time.unscaledTime < _holdUntil) break;   // 방금 값을 바꿨다 — 결과를 볼 시간을 준다.
                    if (_outsideTimer >= CollapsedToPeekSeconds) SetStage(Stage.Peek, "커서 이탈");
                    break;
            }
        }

        private string ResolveDisarmReason()
        {
            if (!_hoverSupported) return "이 플랫폼에는 전역 커서 호버가 없음";
            if (!UiLayoutModel.CornerPanelEnabled) return "구석 패널이 꺼져 있음(영구 설정)";
            if (_infoWindow != null && _infoWindow.IsOpen) return "880 정보창이 열림 — 초상화 액자 중복 방지";
            return "감지 억제 중";
        }

        /// <summary>커서가 "지금 반응해야 하는 사각형" 안인가. 숨어 있을 때는 감지 영역, 보일 때는
        /// 패널 사각형 + 여유다(패널 위에 커서를 얹은 채로는 접히면 안 된다).</summary>
        private bool IsCursorInActiveRect(Vector2 cursor)
        {
            if (_stage_ == Stage.Hidden) return DetectScreenRect.Contains(cursor);
            float slack = LeaveSlackPoints * PixelsPerPoint;
            Rect r = PanelScreenRect;
            var grown = new Rect(r.xMin - slack, r.yMin - slack, r.width + slack * 2f, r.height + slack * 2f);
            return grown.Contains(cursor) || DetectScreenRect.Contains(cursor);
        }

        private void SetStage(Stage next, string reason)
        {
            if (_stage_ == next) return;
            Stage prev = _stage_;
            _stage_ = next;
            _insideTimer = 0f;
            _outsideTimer = 0f;

            if (next == Stage.Hidden)
            {
                _expand = 0f;
                _expandTarget = 0f;
                // 상자와 내용물을 <b>같은 순간에</b> 0으로 되돌린다. 여기서 안 지우면 ESC/전체화면으로
                // 즉시 거둔 뒤 다시 뜰 때 상자가 이미 커진 채로 시작한다(= 원이 먼저 보이는 그 사고).
                _peekBlend = 0f;
                _contentReveal = 0f;
                if (_dial != null) _dial.SetReveal(0f);
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                if (_clickBlocker != null) _clickBlocker.enabled = false;
            }
            else
            {
                if (_canvas != null && !_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
                if (next != Stage.Expanded) _expandTarget = 0f;
                else _expandTarget = 1f;
            }

            if (_stage != null) _stage.SetRenderingEnabled(next == Stage.Expanded);

            Debug.Log($"[구석패널] {prev} → {next} ({reason}). 크기 {DialValue:F2}×, 펼침 {_expand:F2}.");
        }

        /// <summary>
        /// ★ 2026-08-31 — 배타적 모달 규칙의 <b>네 번째 표면</b> 배선(사용자 신고 "캐릭터 창도 맥에서처럼
        /// 여러창으로 겹쳐보임" 조사 중 발견한 구멍).
        ///
        /// 2026-08-30 라운드가 "정리 책임을 여는 쪽 한 곳에 둔다"고 못박고
        /// <see cref="CharacterInfoWindow.CloseOverlappingSurfaces"/>를 만들었는데, 그 뒤에 추가된 이
        /// 패널이 그 목록에 들어가지 않았다 — 그 라운드 문서가 예고한 "네 번째 진입점이 생기면 또 샌다"가
        /// 그대로 일어났다.
        ///
        /// 평소에는 <see cref="DetectionArmed"/>가 정보창이 열린 것을 보고 스스로 접히므로 눈에 띄지
        /// 않지만, <b>다이얼을 끌고 있는 동안에는 그 자기 치유가 동작하지 않는다</b>
        /// (<see cref="TickHoverStateMachine"/> 첫 줄이 <c>_dragActive</c>면 통째로 early-return한다 —
        /// 끌다가 영역을 벗어나는 것이 정상 동작이라 일부러 그렇게 만들었다). 그 상태에서 단축키/
        /// 우클릭으로 정보창을 열면 두 액자가 겹친 채 남고 <b>초상화 카메라 2대가 동시에 돈다</b>.
        ///
        /// 그래서 "닫는 책임"을 여는 쪽에서 부를 수 있게 공개한다. 내부 동작은 ESC/전체화면 감지와
        /// 완전히 같은 경로(<see cref="HideImmediately"/>)이며 새 분기를 만들지 않는다.
        /// </summary>
        public void ForceHide(string reason) => HideImmediately(reason);

        private void HideImmediately(string reason)
        {
            _dragActive = false;
            if (_dial != null && _dial.IsDragging) _dial.EndDrag(Vector2.zero, Time.unscaledTime, out _);
            _leftInitialized = false;
            SetStage(Stage.Hidden, reason);
        }

        // ==================== 기하 ====================

        private float PixelsPerPoint => ScreenCoordinateConverter.CanvasToUnityScreen(1f, _config);

        /// <summary>감지 영역 원점(화면 좌하단 기준, OS 포인트). 하단 막대(macOS Dock / Windows
        /// 작업표시줄)를 피한다(34-4-2).
        ///
        /// <para><b>★ 2026-09-01 Windows 패리티 감사</b> — 원래 이 함수는 <see cref="IDockMetricsService"/>
        /// 하나만 물어봤다. 그 인터페이스는 <b>macOS 전용</b>이다(Dock 타일 크기/개수). Windows의
        /// <c>Win32WindowService</c>는 대신 <see cref="IReservedBottomBarService"/>로 작업표시줄의
        /// <b>정확한 사각형</b>을 실측해 내놓는데 여기서 그것을 묻지 않았다 — 그 결과 Windows에서는
        /// 좌하단 감지 영역과 패널이 작업표시줄에 파묻혀, 손잡이를 가리키기도 어렵고 클릭해도
        /// 작업표시줄이 먼저 먹는다. 발판/안전망 쪽은 이미
        /// <c>FallbackPlatformWindowService.TryGetDockRectOsScreen</c>이 두 플랫폼을 같은 자리에서
        /// 처리하고 있었고, UI 배치만 macOS 경로에 갇혀 있었다.</para>
        ///
        /// <para>순서: <b>OS가 확정값을 주는 쪽을 먼저</b> 본다. Windows는 실측 사각형이라 추정이
        /// 필요 없고, macOS는 이 인터페이스를 애초에 구현하지 않아
        /// (<c>FallbackPlatformWindowService</c>가 false를 돌려준다) 아래 Dock 경로로 그대로 내려간다 —
        /// 즉 <b>macOS 거동은 한 픽셀도 바뀌지 않는다</b>.</para></summary>
        private Vector2 ResolveDetectOriginPoints()
        {
            float x = CornerMarginPoints;
            float y = CornerMarginPoints;

            // (1) Windows 작업표시줄 등 "OS가 예약한 하단 막대" — 실측 사각형이 있으면 그 두께를 쓴다.
            //     false는 "지금 하단 막대가 없다"는 확정 신호다(자동 숨김이거나 좌/우/상단 배치).
            var bottomBar = _agent != null ? _agent.PlatformService as IReservedBottomBarService : null;
            if (bottomBar != null && bottomBar.TryGetReservedBottomBarOsScreen(out Rect barRect)
                && barRect.height > 0f)
            {
                return new Vector2(x, barRect.height + DockGapPoints);
            }

            // (2) macOS Dock — 타일 크기에서 두께를 유도한다(위 (1)은 macOS에서 항상 false다).
            var dockService = _agent != null ? _agent.PlatformService as IDockMetricsService : null;
            if (dockService != null && dockService.TryGetDockMetrics(out DockMetrics dock)
                && dock.IsBottomOriented && !dock.IsAutoHidden)
            {
                float padding = _config != null ? _config.dockThicknessTilePaddingPoints
                    : DockGeometry.DefaultDockThicknessTilePaddingPoints;
                float thickness = DockGeometry.DockThicknessPoints(dock.TileSizePoints, padding);
                y = thickness + DockGapPoints;
            }
            return new Vector2(x, y);
        }

        private void UpdateGeometry()
        {
            float px = PixelsPerPoint;
            Vector2 origin = ResolveDetectOriginPoints() * px;
            DetectScreenRect = new Rect(origin.x, origin.y, DetectWidthPoints * px, DetectHeightPoints * px);

            Vector2 size = PanelSizePointsAt(PeekBlend, _expand);
            PanelScreenRect = new Rect(origin.x, origin.y, size.x * px, size.y * px);
        }

        /// <summary>블렌드/펼침 진행도에서 패널 크기(pt)를 얻는다. <b>런타임과 테스트가 같은 식을 쓴다</b> —
        /// 내용물 게이트(<see cref="ContentGateBlend"/>)의 근거가 이 식 위에서만 성립하므로, 테스트가
        /// 식을 베껴 쓰면 상수가 바뀔 때 테스트만 조용히 옛 식을 잠근다.</summary>
        public static Vector2 PanelSizePointsAt(float peekBlend, float expand)
            => new Vector2(
                Mathf.Lerp(PeekWidthPoints, PanelWidthPoints, peekBlend),
                Mathf.Lerp(PeekHeightPoints,
                    Mathf.Lerp(CollapsedHeightPoints, ExpandedHeightPoints, expand), peekBlend));

        /// <summary>PEEK(손잡이)에서 COLLAPSED(패널)로 가는 블렌드 0..1. 애니메이션이 이 값을 움직인다.</summary>
        private float _peekBlend;
        private float PeekBlend => _peekBlend;

        /// <summary>
        /// ★ <b>등장 순서를 정하는 단 하나의 함수</b>(2026-08-31 수정).
        /// <code>
        ///   열림:  상자가 자란다(0.14s) ──► 다 자란 뒤 내용물이 뜬다(0.10s)
        ///   닫힘:  내용물이 사라진다(0.07s) ──► 다 사라진 뒤 상자가 줄어든다(0.14s)
        /// </code>
        /// 닫힘이 열림의 <b>정확한 역순</b>이어야 하므로, 줄어드는 방향에서는 내용물이 0이 될 때까지
        /// 상자를 <b>멈춰 세운다</b>. 교착은 불가능하다 — 내용물의 목표는 상자가 아니라 <see cref="Stage"/>가
        /// 정하므로(상자가 멈춰 있어도) 반드시 0에 도달한다.
        /// </summary>
        private void TickAnimation()
        {
            float dt = Time.unscaledDeltaTime;

            bool boxOpen = _stage_ == Stage.Collapsed || _stage_ == Stage.Expanded;

            // (1) 내용물 — 상자가 ContentGateBlend까지 자란 뒤에만 오른다. 판정에는 <b>이번 프레임에
            //     상자를 움직이기 전 값</b>을 쓴다(같은 프레임에 둘이 동시에 시작하지 않게).
            float contentTarget = boxOpen && _peekBlend >= ContentGateBlend ? 1f : 0f;
            float contentSpeed = contentTarget > _contentReveal ? 1f / ContentRevealSeconds : 1f / ContentHideSeconds;
            _contentReveal = Mathf.MoveTowards(_contentReveal, contentTarget, contentSpeed * dt);

            // (2) 상자 — 자랄 때는 그냥 자라고, 줄어들 때는 내용물이 다 사라진 뒤에 줄어든다.
            float peekTarget = boxOpen ? 1f : 0f;
            bool shrinking = peekTarget < _peekBlend;
            if (!shrinking || _contentReveal <= 0f)
            {
                float peekSpeed = shrinking ? 1f / CollapseSeconds : 1f / PeekGrowSeconds;
                _peekBlend = Mathf.MoveTowards(_peekBlend, peekTarget, peekSpeed * dt);
            }

            if (!_dragActive || _dragIsDial)
            {
                float speed = _expandTarget > _expand ? 1f / ExpandSeconds : 1f / CollapseSeconds;
                _expand = Mathf.MoveTowards(_expand, _expandTarget, speed * dt);
            }

            if (_group != null)
            {
                float alphaTarget = _stage_ == Stage.Hidden ? 0f : 1f;
                _group.alpha = Mathf.MoveTowards(_group.alpha, alphaTarget, dt / PeekGrowSeconds);
            }
        }

        private void ApplyLayout()
        {
            if (_panel == null) return;

            float px = PixelsPerPoint;
            float widthPoints = PanelScreenRect.width / Mathf.Max(0.0001f, px);
            float heightPoints = PanelScreenRect.height / Mathf.Max(0.0001f, px);
            _panel.sizeDelta = new Vector2(widthPoints, heightPoints);

            Vector2 originPoints = ResolveDetectOriginPoints();
            _panel.anchoredPosition = originPoints;   // 피벗이 좌하단이라 그대로 원점이다.

            // 다이얼 중심은 패널 <b>바닥</b> 기준 고정 — 패널은 위로만 자란다(34-5-3).
            if (_dial != null)
            {
                _dial.PixelsPerPoint = px;
                // 그림과 히트 판정이 <b>같은 한 쌍의 수</b>에서 나온다(SetCenterInParentPoints 문서).
                _dial.SetCenterInParentPoints(new Vector2(PanelWidthPoints * 0.5f, DialCenterFromBottomPoints));
                _dial.CenterScreen = new Vector2(
                    PanelScreenRect.xMin + PanelWidthPoints * 0.5f * px,
                    PanelScreenRect.yMin + DialCenterFromBottomPoints * px);
                // ★ 보임의 출처는 상자의 성장 진행도 <b>하나뿐</b>이다(TickAnimation 문서).
                _dial.SetReveal(_contentReveal);
            }

            if (_cardRoot != null)
            {
                // 펼침에서 파생된 알파(설계 34-5-3) × 상자 게이트. 카드도 다이얼과 <b>같은 규칙</b>을
                // 따른다 — 카드는 232×212pt라 상자가 덜 자란 동안 보이면 사방으로 삐져나온다.
                float expandAlpha = _expand <= CardFadeStart ? 0f
                    : Mathf.InverseLerp(CardFadeStart, 1f, _expand);
                float cardAlpha = expandAlpha * _contentReveal;
                if (_cardGroup != null) _cardGroup.alpha = cardAlpha;
                _cardRoot.gameObject.SetActive(cardAlpha > 0.001f);
                // 12pt 미끄러짐은 <b>펼침</b>에서만 파생된다(게이트로 흔들리지 않게).
                float rise = Mathf.Lerp(-CardRisePoints, 0f, expandAlpha);
                _cardRoot.anchoredPosition = new Vector2(0f, -(UiChrome.Space3) + rise);
            }
        }

        // ==================== 입력 (34-5-1) ====================

        private void TickPointer()
        {
            if (_buttonService == null || !IsVisible) return;
            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) { AbortDrag("버튼 상태를 읽지 못함"); return; }
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }

            bool hasCursor = TryGetCursorUnityScreen(out Vector2 cursor);
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>버튼 상태 + 커서만으로 판정하는 <b>단일 경로</b>. 실제 입력과 테스트가 이 함수를
        /// 공유한다(InfoGearIconWidget.ProcessPointer와 같은 관례 — PlayMode는 진짜 전역 클릭을
        /// 만들 수 없다).</summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursor, bool hasCursor)
        {
            bool prev = _leftPrev;
            _leftPrev = buttonDown;
            _leftInitialized = true;

            if (buttonDown && !prev) BeginDrag(cursor, hasCursor);
            else if (buttonDown && _dragActive) UpdateDrag(cursor, hasCursor);
            else if (!buttonDown && prev) EndDrag(cursor, hasCursor);
        }

        /// <summary>테스트 전용 진입점 — 실제 입력과 <b>완전히 같은 처리 경로</b>에 먹인다.</summary>
        public void FeedPointerForTests(bool buttonDown, Vector2 cursorUnityScreen)
            => ProcessPointer(buttonDown, cursorUnityScreen, true);

        /// <summary>테스트 전용 — 호버 상태를 직접 밀어 넣는다(전역 커서를 만들 수 없는 PlayMode용).</summary>
        public void ForceStageForTests(bool expanded)
            => SetStage(expanded ? Stage.Expanded : Stage.Collapsed, "테스트");

        /// <summary>테스트 전용 — PEEK(손잡이)로 접는다. 실제 "커서 이탈"과 <b>같은 SetStage</b>를 타므로
        /// 닫힘 연출의 순서(내용물 먼저, 상자 나중)를 제품 경로 그대로 관찰할 수 있다.
        /// 배치 모드에는 전역 커서가 없어 이 전이를 자연 발생시킬 수 없다.</summary>
        public void ForcePeekForTests() => SetStage(Stage.Peek, "테스트: 커서 이탈");

        /// <summary>테스트/증거 캡처 전용 — 호버 상태 머신을 잠시 멈춘다. 배치 모드에는 전역 커서가
        /// 없어 매 프레임 "커서 이탈"로 판정되므로, 강제로 띄운 상태를 몇 프레임 유지할 수 없다.
        /// <b>제품 경로는 이 값을 절대 켜지 않는다</b>(기본 false).</summary>
        public bool HoldStageForTests;

        private void BeginDrag(Vector2 cursor, bool hasCursor)
        {
            if (!hasCursor || !IsVisible) return;
            if (!PanelScreenRect.Contains(cursor)) return;

            _dragActive = true;
            // 사용자가 잡은 순간부터는 복원이 다이얼을 덮어쓰지 않는다(위 RestoreSavedScale 문단 (c)).
            _scaleRestored = true;
            _dragStart = cursor;
            _dragStartTime = Time.unscaledTime;
            _expandAtDragStart = _expand;

            // ★ 누른 위치로 두 드래그를 가른다(클래스 문서). 다이얼 원환이면 값 조절, 아니면 펼침/접힘.
            //   ★ 아직 뜨지 않은(=보이지 않는) 다이얼은 누를 수 없다 — 안 보이는 컨트롤이 클릭을 먹으면
            //     "보이는 곳을 누르면 먹는다"가 거꾸로 뒤집힌다(ContentInteractiveReveal 문서).
            _dragIsDial = _dial != null && _contentReveal >= ContentInteractiveReveal && _dial.IsInRing(cursor);
            if (_dragIsDial) _dial.BeginDrag(cursor, Time.unscaledTime);
        }

        private void UpdateDrag(Vector2 cursor, bool hasCursor)
        {
            if (!hasCursor) return;

            if (_dragIsDial)
            {
                if (_dial.DragTo(cursor)) OnDialValueChanged(applyToCharacter: false);
                return;
            }

            float px = Mathf.Max(0.0001f, PixelsPerPoint);
            float dy = (cursor.y - _dragStart.y) / px;
            float progress = Mathf.Clamp01((Mathf.Abs(dy) - ExpandDeadZonePoints) / ExpandTravelPoints);
            _expand = dy >= 0f
                ? Mathf.Clamp01(_expandAtDragStart + progress)
                : Mathf.Clamp01(_expandAtDragStart - progress);
        }

        private void EndDrag(Vector2 cursor, bool hasCursor)
        {
            if (!_dragActive) return;
            _dragActive = false;
            _holdUntil = Time.unscaledTime + ChangeHoldSeconds;

            if (_dragIsDial)
            {
                bool committed = _dial.EndDrag(cursor, Time.unscaledTime, out bool changed);
                if (changed) OnDialValueChanged(applyToCharacter: committed);
                if (committed && _dial.IsOnHub(cursor)) { /* 원환 안에서 시작했으므로 여기 오지 않는다 */ }
                return;
            }

            float px = Mathf.Max(0.0001f, PixelsPerPoint);
            float moved = (cursor - _dragStart).magnitude / px;
            bool tap = moved < ToggleMovePoints && Time.unscaledTime - _dragStartTime < ToggleSeconds;

            if (tap && hasCursor && _dial != null && _contentReveal >= ContentInteractiveReveal
                && _dial.IsOnHub(cursor))
            {
                // 중앙 숫자 클릭 → 기본값 복귀(34-3-1 탈출구 ③).
                float baked = _agent != null ? _agent.BakedCharacterScale : 0.75f;
                _dial.SetValue(baked);
                OnDialValueChanged(applyToCharacter: true);
                return;
            }

            if (tap)
            {
                _expandTarget = _stage_ == Stage.Expanded ? 0f : 1f;
                SetStage(_expandTarget > 0.5f ? Stage.Expanded : Stage.Collapsed, "그립/카드 클릭 토글");
                return;
            }

            bool expand = _expand >= ExpandCommitProgress;
            _expandTarget = expand ? 1f : 0f;
            SetStage(expand ? Stage.Expanded : Stage.Collapsed, expand ? "위로 끌어 펼침" : "아래로 끌어 접힘");
        }

        private void AbortDrag(string reason)
        {
            if (!_dragActive) return;
            _dragActive = false;
            if (_dragIsDial && _dial != null) _dial.EndDrag(_dragStart, Time.unscaledTime, out _);
            Debug.Log($"[구석패널] 드래그 취소 — {reason}.");
        }

        // ==================== 값 적용 (34-3-6) ====================

        /// <summary>
        /// 다이얼 값이 바뀌었다.
        /// <list type="bullet">
        /// <item><b>드래그 중</b>(applyToCharacter=false): 미리보기 카드의 미니 피규어만 즉시 반응한다.
        /// 그쪽은 Collider/Rigidbody가 0개라 100% 안전하고 지연이 0프레임이다.</item>
        /// <item><b>손을 뗀 뒤</b>(applyToCharacter=true): 실캐릭터에 넣는다. 상태가 능동(IDLE/WALK 등)이면
        /// 그 프레임에, 랙돌/스펙터클 중이면 대기하며 중앙에 "곧 적용" 캡션을 띄운다.</item>
        /// </list>
        /// 어느 경우든 <b>다이얼 숫자와 실제 값이 같은 인덱스에서 파생</b>되므로 표시와 결과가 어긋날 수 없다.
        /// </summary>
        private void OnDialValueChanged(bool applyToCharacter)
        {
            _holdUntil = Time.unscaledTime + ChangeHoldSeconds;
            if (!applyToCharacter) return;

            // ★ 2026-09-01 — 기억(UiLayoutModel) / 게이트 / 유예 / 강제적용이 전부 단일 소스로 옮겨졌다
            //   (Core/CharacterScaleController, 35-1-3 ①). 여기 남은 것은 "다이얼이 정한 값을 넘긴다"뿐이며,
            //   설정창 슬라이더도 <b>같은 문</b>을 지난다 — 그래서 규칙이 두 벌이 될 수 없다.
            CharacterScaleController.Request(_dial.Value, "구석 다이얼");
        }

        /// <summary>유예 상태를 <b>읽어서</b> "곧 적용" 캡션만 그린다(판정은 단일 소스가 한다).
        /// 실제로 바뀐 프레임에만 위젯을 건드린다 — 하루 종일 켜져 있는 앱이다.</summary>
        private void TickPendingScale()
        {
            CharacterScaleController.Tick();

            bool pending = CharacterScaleController.HasPendingApply;
            if (pending == _pendingCaptionShown) return;
            _pendingCaptionShown = pending;
            if (_dial != null) _dial.SetPendingCaption(pending);
        }

        // ==================== 미리보기 카드 (34-6) ====================

        private void TickCard()
        {
            if (_stage == null || _cardImage == null) return;
            if (_stage_ != Stage.Expanded) return;

            // ★ 포즈와 캡션이 <b>같은 스냅샷</b>에서 파생된다 — 원칙 1을 이미지에도 적용(33절 규칙).
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            StickmanStateId id = bb != null && bb.Machine != null ? bb.Machine.CurrentStateId : StickmanStateId.Idle;
            _stage.SetPose(CharacterPortraitStage.PoseForState(id));

            EnsureCardTexture();
            UpdateCaption(id);
        }

        private void EnsureCardTexture()
        {
            float pixelsPerCanvasUnit = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (Mathf.Approximately(pixelsPerCanvasUnit, _lastPixelsPerCanvasUnit) && _stage.HasTexture) return;
            _lastPixelsPerCanvasUnit = pixelsPerCanvasUnit;

            Vector2 design = CharacterInfoWindow.PortraitContentSize;
            bool ok = _stage.TryEnsureTexture(design.x, design.y, pixelsPerCanvasUnit);
            _cardImage.enabled = ok;
            if (ok) _cardImage.texture = _stage.Texture;
            if (_cardFallback != null)
            {
                _cardFallback.gameObject.SetActive(!ok);
                // 33절 정보창과 <b>같은 문구</b>다. 검은 상자를 띄우지 않는다.
                _cardFallback.text = "미리보기를 준비하지 못했어요";
            }
        }

        private void UpdateCaption(StickmanStateId id)
        {
            if (_cardCaption == null) return;
            string caption = ResolveCaption(id);
            if (caption == _lastCaption) return;   // 24시간 상주 앱 — 같은 글자를 매 프레임 대입하지 않는다.
            _lastCaption = caption;
            _cardCaption.text = caption;
        }

        /// <summary>상태에서 파생된 캡션. <b>상태가 먼저 확정된 뒤</b> 그 상태로부터만 만든다(원칙 1).</summary>
        private static string ResolveCaption(StickmanStateId id)
        {
            switch (id)
            {
                case StickmanStateId.Walk: return "걷는 중";
                case StickmanStateId.Jump: return "뛰는 중";
                case StickmanStateId.Fall: return "떨어지는 중";
                // 2026-09-01: 빠뜨리면 default가 "쉬는 중"이라 허공에서 허둥대는데 쉰다고 말한다(원칙1 위반).
                case StickmanStateId.GroundLossHang: return "허둥대는 중";
                case StickmanStateId.ParkourClimb: return "오르는 중";
                case StickmanStateId.LedgeHang: return "매달린 중";
                case StickmanStateId.Ragdoll:
                case StickmanStateId.ThrowTumble: return "넘어진 중";
                case StickmanStateId.Getup: return "일어나는 중";
                case StickmanStateId.Dragged: return "붙잡힌 중";
                case StickmanStateId.RodeoCursor: return "커서에 올라탄 중";
                case StickmanStateId.LandingCrouch: return "착지하는 중";
                case StickmanStateId.Runaway: return "자리에 없음";
                default: return "쉬는 중";
            }
        }

        // ==================== 배선 ====================

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        /// <summary>차단막은 <b>지금 보이는 사각형만</b> 덮는다(펼침 애니메이션 중에는 그 프레임의 실제
        /// 크기). 그 밖은 전부 관통 그대로다.</summary>
        private void SyncClickBlocker()
        {
            if (_clickBlocker == null) return;
            if (!IsVisible) { _clickBlocker.enabled = false; return; }

            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(PanelScreenRect.xMin, PanelScreenRect.yMin, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(PanelScreenRect.xMax, PanelScreenRect.yMax, depth));
            _clickBlocker.enabled = true;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        private bool TryGetCursorUnityScreen(out Vector2 cursor)
        {
            cursor = default;
            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return false;
            cursor = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config);
            return true;
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("CornerHoverPanelCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // ★★ 씬 루트에 단다(캐릭터의 자식이 아니다) — PopoverPanel/CharacterInfoWindow와 같은 이유.
            // 캐릭터 자손으로 두면 이 캔버스 안의 UI 이름이 이름으로 캐릭터 파츠를 찾는 코드에 걸릴 수
            // 있다(2026-08-30 부채꼴의 "Head"로 실제로 터진 사고). 정리는 OnDestroy가 책임진다.
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // ★ 2026-08-31 — 여기 있던 alpha 0.86이 이 패널의 창 알파를 <b>0.72</b>로 만들고 있었다
            //   (0.86² = 0.7396, 그 위에 보더 α0.16이 다시 곱해져 0.72 → 데스크톱이 <b>28% 비침</b>).
            //   유리를 포기한 것이 아니라 <b>유리에서 "비침"만</b> 뺐다 — 나머지 단서(그림자/보더/상단
            //   하이라이트)는 UiChrome.Flatten으로 겉보기 색 그대로 α=1에 옮겨졌다. 자세한 근거는
            //   UiChrome.AddGlassPanel 문서.
            _panel = UiChrome.AddGlassPanel(canvasGo.transform, "Panel", UiChrome.RadiusPanel, out _panelBody);
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = Vector2.zero;    // 좌하단 고정 — 패널은 위로만 자란다(34-5-3).
            _panel.sizeDelta = new Vector2(PeekWidthPoints, PeekHeightPoints);

            // 그립 — 여기서 시작하는 드래그는 "펼침"이다. PEEK에서는 이것만 보인다.
            Image grip = UiChrome.AddSurface(_panel, "Grip", UiChrome.TextTertiary, UiChrome.RadiusDot);
            var gripRt = grip.rectTransform;
            gripRt.anchorMin = gripRt.anchorMax = new Vector2(0.5f, 1f);
            gripRt.pivot = new Vector2(0.5f, 1f);
            gripRt.sizeDelta = new Vector2(GripWidthPoints, GripHeightPoints);
            gripRt.anchoredPosition = new Vector2(0f, -GripFromTopPoints);
            grip.raycastTarget = false;

            BuildCard();

            _dial = new SizeDialWidget(_panel, _config != null ? _config.ResolveCharacterScale() : 0.75f);
            // 다이얼은 패널 바닥 기준으로 고정된다 — 앵커를 좌하단에 두고 매 프레임 위치를 계산한다.
            BuildStage();

            var blockerGo = new GameObject("CornerHoverPanelBlocker");
            _clickBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickBlocker.isTrigger = true;   // 캐릭터 물리에는 전혀 관여하지 않는다.
            _clickBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        private void BuildCard()
        {
            var cardGo = new GameObject("PreviewCard", typeof(RectTransform), typeof(CanvasGroup));
            cardGo.transform.SetParent(_panel, false);
            _cardRoot = cardGo.GetComponent<RectTransform>();
            _cardRoot.anchorMin = _cardRoot.anchorMax = new Vector2(0.5f, 1f);
            _cardRoot.pivot = new Vector2(0.5f, 1f);
            _cardRoot.sizeDelta = new Vector2(CardWidthPoints, CardHeightPoints);
            _cardGroup = cardGo.GetComponent<CanvasGroup>();
            _cardGroup.alpha = 0f;

            // ★ 카드는 패널(불투명) <b>위에</b> 얹히지만 그래도 α=1이어야 한다 — 이 프로젝트의 블렌드는
            //   알파 채널에도 적용되므로 반투명 겹은 <b>불투명 바탕 위에서도</b> 창 알파를 내린다
            //   (dstA=1 위의 α0.86 → 0.88). "우리 것 위니까 괜찮다"가 성립하지 않는 구조다.
            RectTransform glass = UiChrome.AddGlassPanel(_cardRoot, "CardGlass", UiChrome.RadiusCard, out _);
            UiChrome.Stretch(glass);

            // 액자 — 잉크색에 따라 뒤집히는 밝은 판(34-1의 PortraitSurface 예외가 여기에도 그대로 적용된다).
            Image frame = UiChrome.AddSurface(glass, "Frame", CharacterPortraitStage.ResolveBackdropColor(_config), 8);
            var frameRt = frame.rectTransform;
            frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 1f);
            frameRt.pivot = new Vector2(0.5f, 1f);
            // ★ 액자를 <b>정확히</b> PortraitContentSize로 잡는다(여백을 더하지 않는다) — 카메라 프레이밍
            //   (CharacterPortraitStage.DesignAspect)이 이 종횡비 위에서 이미 검증됐기 때문이다.
            //   카드 212 = 위 여백 4 + 액자 180 + 아래 캡션 자리 28.
            Vector2 design = CharacterInfoWindow.PortraitContentSize;
            frameRt.sizeDelta = design;
            frameRt.anchoredPosition = new Vector2(0f, -UiChrome.Space1);
            frame.raycastTarget = false;
            UiChrome.AddOutline(frameRt, "FrameBorder", UiChrome.CardBorder, 8);

            var imageGo = new GameObject("PortraitImage", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(frameRt, false);
            UiChrome.Stretch(imageGo.GetComponent<RectTransform>());
            _cardImage = imageGo.GetComponent<RawImage>();
            _cardImage.raycastTarget = false;
            _cardImage.enabled = false;   // RT가 준비되면 켠다.

            _cardFallback = UiChrome.AddText(frameRt, "Fallback", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.TextTertiary, wrap: true);
            UiChrome.Stretch(_cardFallback.rectTransform, UiChrome.Space3);
            _cardFallback.text = "미리보기를 준비하고 있어요";
            _cardFallback.gameObject.SetActive(false);

            // 하단 캡션 한 줄. ★ 좌우 화살표/페이지 점은 <b>채택하지 않는다</b>(34-6-4) — 우리 카드는
            // 캐릭터 한 장뿐이고, 한 장짜리 페이저는 없는 깊이가 있다고 말하는 장식이다.
            _cardCaption = UiChrome.AddText(glass, "Caption", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.TextSecondary);
            var capRt = _cardCaption.rectTransform;
            capRt.anchorMin = new Vector2(0f, 0f);
            capRt.anchorMax = new Vector2(1f, 0f);
            capRt.pivot = new Vector2(0.5f, 0f);
            // 액자 아래 남은 28pt 안에 들어간다(위 액자 배치 주석 참고) — 겹치면 글자가 종이 위에 얹힌다.
            capRt.offsetMin = new Vector2(UiChrome.Space2, UiChrome.Space1 * 0.5f);
            capRt.offsetMax = new Vector2(-UiChrome.Space2, UiChrome.Space1 * 0.5f + CardCaptionHeightPoints);
            _cardCaption.text = "쉬는 중";

            _cardRoot.gameObject.SetActive(false);
        }

        private void BuildStage()
        {
            Material lineMaterial = null;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            if (source != null) lineMaterial = source.sharedMaterial;

            // ★ 880 정보창과 <b>다른 좌표</b>에 세운다 — 같은 자리에 두면 카메라 하나가 미니 피규어
            //   두 개를 함께 찍는다(34-6-3).
            _stage = CharacterPortraitStage.Create(_config, StickmanMetrics.Find(this), lineMaterial,
                CharacterPortraitStage.SecondaryStageWorldX);
            _stage.SetRenderingEnabled(false);
        }

        /// <summary>씬에 EventSystem이 있어도 입력 모듈이 없으면 Button.onClick이 영원히 발동하지
        /// 않는다(이 프로젝트가 실제로 밟았던 함정) — 다른 위젯들과 같은 보강.</summary>
        private static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null)
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
