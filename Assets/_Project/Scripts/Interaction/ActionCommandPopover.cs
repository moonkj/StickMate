using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 ④ [행동] 버튼에서 자라나는 <b>행동 명령창</b> — docs/UX_FLOW.md <b>36-6/36-7/36-8</b>
    /// 확정 설계. 480×560.
    ///
    /// 2026-08-31 사용자 원문: "기어아이콘에 메뉴하나 추가해서 행동들은 거기서 클릭하면 창 하나가 떠서
    /// 행동 명령 내릴수 있게".
    ///
    /// ============================================================================
    /// 왜 독립 창/모달이 아니라 <see cref="PopoverPanel"/>인가 (36-6)
    /// ============================================================================
    /// ① 32-3의 앵커/탈출구/자동접힘 규칙(기어 재클릭·버튼 재클릭·무입력 자동 닫힘·
    ///    <c>AnyPopoverOpen()</c> 동안 자동접힘 정지)이 <b>전부 공짜로 상속된다</b>.
    ///    ★ 2026-09-02 — 그 목록에서 <b>"밖 클릭"이 빠졌다</b>(사용자 지시 "사용자가 닫기전에는
    ///    안꺼져야함"). 이 창에 미치는 영향은 아래 <see cref="OnCommandClicked"/> 문서 (3)에 적었다.
    /// ② 전체 화면을 덮는 <c>ScreenScrim</c>이 없다 — 이 창은 하루에 여러 번 열리는 런처이고, 열 때마다
    ///    바탕화면을 어둡게 덮는 것은 비침해 원칙(CLAUDE.md 2)에 대한 과청구다.
    /// ③ 명령을 내린 뒤 <b>봐야 할 것은 캐릭터</b>다. 다만 그것이 "창이 스스로 닫힌다"를 뜻하지는
    ///    <b>않는다</b> — 2026-09-02 사용자 신고("활쏘기 한번 누르면 메뉴가 사라져버리는데 유지되어야함")
    ///    이후 <b>성공해도 창은 남는다</b>. 창은 톱니 자리에 뜨고 연출은 캐릭터 자리에서 일어나며 둘 다
    ///    사용자가 옮길 수 있다(근거 전문: <see cref="OnCommandClicked"/>). 비켜야 할 때는
    ///    <b>사용자가</b> 오른쪽 위 [✕]를 누르거나 창을 끌어 옮기면 된다
    ///    (★ 2026-09-02 정정 — 예전에는 "창 바깥 아무 곳이나"였다).
    ///
    /// ============================================================================
    /// ★★ 원칙 1 — "요청 안 한 연출 금지"의 <b>정확히 반대편</b>이다 (36-8)
    /// ============================================================================
    /// 이 프로젝트는 <c>StickConfig</c>의 <c>*Chance</c> 기본값 다수를 0으로 두어 "사용자가 요청하지 않은
    /// 연출은 뜨지 않는다"를 지켜 왔다. 이 창은 그 규칙과 충돌하지 않는다 — <b>같은 규칙의 다른 쪽 면</b>
    /// 이다. 여기서는 사용자가 시작했으므로:
    ///
    /// <code>확률은 건너뛴다. 안전장치는 건너뛰지 않는다. 건너뛰지 못한 이유는 반드시 말한다.</code>
    ///
    /// 상호배제 락 / 진입 상태 조건 / 자리·대상 조건은 <b>하나도 완화하지 않는다</b>. 대신 왜 안 되는지
    /// 화면에 쓴다. 여기서 확률을 굴리면 "눌렀는데 안 나옴"이 되고 그게 곧 원칙 1 위반이다.
    ///
    /// ============================================================================
    /// ★★ 진실은 한 벌 — 회색 처리와 실제 실행은 <b>같은 판정</b>을 쓴다 (36-7)
    /// ============================================================================
    /// 각 Director의 <c>GetAvailability()</c> 하나가 모든 것을 결정한다: 타일이 회색인지, 이유 문구가
    /// 무엇인지, 그리고 눌렀을 때 실제로 실행되는지. 이 창은 <b>자기 게이트를 따로 구현하지 않는다</b> —
    /// 그 순간 진실이 두 벌이 되고, 이 프로젝트는 그 함정(Dock 구간 이중 계산, 캐릭터 치수 이중 정의)을
    /// 이미 여러 번 밟았다. <see cref="CommandAvailability"/> 클래스 문서에 그 계약이 적혀 있다.
    ///
    /// ============================================================================
    /// (다) 개발 전용 항목은 여기에 <b>단 하나도 없다</b> (36-2)
    /// ============================================================================
    /// 하드웨어 반응 미리보기 / 스트레스 게이지 순환 / 할일 알림 데모 / 집중 모드 90초 데모 / 진단 로그
    /// 토글 / 가출 <b>발동</b>은 전부 "표시된 것과 실제가 다르다"를 만드는 경로라 사용자 UI에 상설 설치될
    /// 수 없다(<see cref="StickMateDevTools"/> 뒤에만 산다). 우클릭 메뉴 18행을 그대로 옮겨 붙였다면
    /// 이 창은 "행동 명령창"이 아니라 <b>디버그 패널</b>이 됐을 것이다.
    ///
    /// 예외 하나: <b>[돌아와!]</b>는 가출 중일 때만 헤더에 나타난다. 이건 "언제든 시킬 수 있는 재주"가
    /// 아니라 <b>특정 상태에서만 존재하는 응답</b>(20절의 상시 탈출구, 원칙 4 장치)이라 명령 타일로
    /// 만들지 않았다 — 항상 보이는 타일로 두면 평소엔 늘 비활성인 칸이 그리드를 어지럽히고, 없는 상태를
    /// 사용자에게 가르치게 된다(17절 "빈 상태를 굳이 보여주지 않는다").
    /// </summary>
    public sealed class ActionCommandPopover : PopoverPanel
    {
        // ==================== 확정 치수 (36-6) ====================

        private const float Width = 480f;

        /// <summary>
        /// ★ 2026-09-02 <b>560 → 508</b> (격파 놀이 타일 삭제). 세로는 <b>내용에 정확히 맞춘 값</b>이지
        /// 임의의 라운드 수가 아니다 — 검산:
        /// <code>
        /// 콘텐츠 높이 = Height - (Space3 + 22 + Space2) - Space4 = 508 - 42 - 16 = 450
        /// 푸터 바닥   = FooterY - QuitButtonHeight = -422 - 28 = -450
        /// </code>
        /// 둘이 같다. 종전 560도 같은 방식의 <b>정확히 맞는 값</b>이었고(502 = 502), 타일을 하나 빼면서
        /// 그대로 두면 푸터 아래에 <b>정확히 한 행(52pt)의 빈 띠</b>가 생긴다. 그 띠는 아무것도 말하지
        /// 않으면서 창을 아래로 무겁게 만든다("빈 상태를 굳이 보여주지 않는다", 17절).
        ///
        /// <para><b>깨진 것</b>: 예전 주석은 "세로는 설정창(720×560)과 같은 560 — 같은 앱 가족으로
        /// 보이게 한다"고 적고 있었다. 이제 그 정렬은 성립하지 않는다. 둘 중 하나를 골라야 했고
        /// <b>빈 띠를 없애는 쪽</b>을 골랐다 — 가족처럼 보이게 하는 것은 모서리 반경·간격 토큰·타이포가
        /// 이미 하고 있고(UiChrome 한 벌), 세로 치수 일치는 그중 가장 약한 신호이기 때문이다.
        /// 가로 480은 그대로다: 면적 순서(정보창 &gt; 설정창 &gt; 행동창)가 곧 중요도 순서라는 근거는
        /// 세로가 줄어도 유지된다.</para>
        /// <para>★ 이 판단은 <b>UI 표면</b> 영역이라 리더를 거쳐 ux-designer 확인을 받아야 한다
        /// (coder 단독 결정이 아니다 — 완료 보고에 명시했다).</para>
        /// </summary>
        private const float Height = 508f;
        private const float ContentWidth = Width - UiChrome.Space4 * 2f;   // 448.

        private const float StatusRowHeight = 26f;
        private const float GroupTitleHeight = 18f;
        private const float CardPadding = 10f;

        /// <summary>명령 행 높이 — 44pt 최소 타깃을 넘긴다(아이콘 28 + 이름 + 설명 한 줄).</summary>
        private const float RowHeight = 52f;

        private const float RowWidth = ContentWidth - CardPadding * 2f;    // 428.
        private const float IconBox = 28f;
        private const float IconStroke = 1.8f;

        private const float RecallChipWidth = 88f;
        private const float RecallChipHeight = 26f;
        private const float QuitButtonWidth = 92f;
        private const float QuitButtonHeight = 28f;

        // ---- 세로 배치(콘텐츠 상단 기준, 아래로 음수) ----
        private const float StatusY = 0f;
        private const float Group1Y = -(StatusRowHeight + 8f);                       // -34.
        /// <summary>그룹1은 <b>3행</b>(말 걸기/활쏘기/그라피티). 2026-09-02까지는 4행이었다(격파 놀이).</summary>
        private const float Group1Height = CardPadding * 2f + GroupTitleHeight + 4f + RowHeight * 3f;  // 198.
        private const float Group2Y = Group1Y - Group1Height - 12f;                  // -244.
        private const float Group2CaptionHeight = 16f;
        private const float Group2Height = CardPadding * 2f + GroupTitleHeight + 4f + RowHeight * 2f
                                           + 4f + Group2CaptionHeight;              // 166.
        private const float FooterY = Group2Y - Group2Height - 12f;                  // -422.

        /// <summary>
        /// 2단 확인이 열려 있는 시간. <see cref="TodoBoardPopover"/>의 삭제 확인과 <b>같은 3초, 같은
        /// 패턴</b>을 쓴다 — 앱 안에서 "되돌릴 수 없는 행동"의 확인 방식이 두 벌이 되지 않게.
        /// </summary>
        public const float QuitConfirmSeconds = 3f;

        /// <summary>실패한 타일이 이유를 보여주는 시간. 지나면 원래 설명으로 돌아온다(36-7).</summary>
        public const float FailureNoticeSeconds = 3f;

        /// <summary>실패 흔들림 — 좌우 3pt, 0.18초. "눌렸다"와 "안 됐다"를 동시에 말하는 가장 싼 방법.</summary>
        public const float ShakeSeconds = 0.18f;
        public const float ShakeAmplitudePoints = 3f;

        /// <summary>
        /// ★ 2026-09-02 사용자 신고 — "행동 메뉴에서 활쏘기 한번 누르면 메뉴가 사라져버리는데
        /// 유지되어야함". <b>명령 타일 실행은 더 이상 이 창을 닫지 않는다</b>(아래
        /// <see cref="OnCommandClicked"/> 문서에 근거 전문).
        ///
        /// <para>그래서 이 지연은 이제 <b>[돌아와!] 칩 하나</b>만 쓴다 — 그쪽은 "명령을 고르는 목록"이
        /// 아니라 가출이라는 <b>특정 상태에서만 존재하는 일회성 응답</b>이고, 누르는 즉시 칩 자신이
        /// 사라져 창에 남을 이유가 없다.</para>
        /// </summary>
        public const float SuccessCloseDelaySeconds = 0.12f;

        /// <summary>
        /// 명령이 <b>접수됐다</b>는 순간 신호 — 눌린 타일 바닥이 액센트로 한 번 밝아졌다 꺼진다.
        ///
        /// <para>★ 왜 필요한가: 종전에는 <b>창이 닫히는 것</b>이 곧 "눌렸다"는 신호였다. 창을 유지하기로
        /// 한 이상 그 신호가 통째로 사라진다. 대부분의 명령은 실행 즉시 상호배제 락이 잡혀 다섯 타일이
        /// 전부 "지금 ○○ 중이에요"로 바뀌므로 화면이 크게 변하지만, <b>[말 걸기]는 그렇지 않다</b> —
        /// <c>AppControlDirector.ForceSayNow</c>가 <b>같은 상태로 재진입</b>할 뿐이라 가용성이 Ready
        /// 그대로고, 창에는 아무 변화도 남지 않는다. 그 한 칸 때문에 "눌렀는데 반응이 없다"가 생긴다.</para>
        ///
        /// <para>★ 이 플래시는 <b>결과를 말하지 않는다</b> — "접수했다"만 말한다. 결과(지금 무엇을 하는
        /// 중인가)는 여전히 상태에서 파생된 타일 문구와 헤더 캡션이 말한다(원칙 1: 텍스트는 확정된
        /// 상태에서만 나온다). 실패 흔들림과 <b>같은 0.18초 박자</b>를 쓴다 — 같은 클릭에 대한 응답이
        /// 성공/실패에 따라 다른 속도로 오면 사용자는 그것을 리듬이 아니라 지연으로 읽는다.</para>
        /// </summary>
        public const float AcceptFlashSeconds = ShakeSeconds;

        // ==================== 명령 정의 ====================

        /// <summary>값이 곧 화면상의 순서다(위에서 아래).</summary>
        public enum Command
        {
            SayNow = 0,
            Archery = 1,
            Graffiti = 2,
            WindowTheft = 3,
            WindowCrash = 4,
        }

        /// <summary>★ 이 값은 <see cref="Command"/>에서 <b>파생</b>된다 — 손으로 적으면 enum과
        /// 어긋나는 순간 <c>_tiles</c> 배열이 짧아져 타일 하나가 조용히 사라진다(36-7 "조용한 실패
        /// 금지"). 2026-09-02 격파 놀이 삭제로 6 → 5가 됐고, 그때 이 값을 상수로 두는 것이 정확히
        /// 그 사고의 재료였다.</summary>
        // System.Enum을 정규화해 쓴다 — 이 파일은 UnityEngine.Object를 이름으로 부르므로
        // `using System;`을 넣으면 System.Object와 CS0104(모호한 참조)로 충돌한다.
        public static readonly int CommandCount = System.Enum.GetValues(typeof(Command)).Length;

        private static readonly string[] CommandNames =
        {
            "말 걸기", "활쏘기", "그라피티", "창 도둑", "창 부수기",
        };

        private static readonly string[] CommandDescriptions =
        {
            "지금 상태 그대로 한마디 합니다",
            "과녁을 세우고 세 발 쏩니다",
            "빈 자리에 낙서했다 지웁니다",
            "작은 창을 미는 시늉을 합니다",
            "금 간 유리를 3초 덮어 보입니다",
        };

        private sealed class TileView
        {
            public Image Surface;
            public RectTransform Rect;
            public RectTransform Icon;
            public Image[] IconParts;
            public Text Name;
            public Text Description;

            public bool Ready;
            public string Reason;        // 불가 사유(미리 만들어진 문자열 — 매 폴링 할당 금지).
            public float ShakeTimer = -1f;
            public float NoticeTimer = -1f;
            public float AcceptTimer = -1f;   // 접수 플래시(-1 = 꺼짐).
            public float BaseX;
        }

        private readonly TileView[] _tiles = new TileView[CommandCount];

        /// <summary>평소 타일 바닥 — <b>칠하지 않음</b>(그룹 카드가 그대로 비친다). 팔레트 값이 아니라
        /// "채움 없음"이라 <see cref="UiChrome"/> 토큰이 아니다. 접수 플래시가 여기로 되돌아오므로
        /// 값이 두 벌이 되지 않게 한 곳에 둔다.</summary>
        private static readonly Color IdleTileSurface = new Color(0f, 0f, 0f, 0f);

        /// <summary>접수 플래시의 처음과 끝. <b>알파만 움직인다</b> — 투명한 검정으로 보간하면 사라지는
        /// 동안 색이 탁해져 "밝아졌다 꺼진다"가 "어두워졌다 꺼진다"로 읽힌다. 색상값은
        /// <see cref="UiChrome.AccentSurface"/> 하나에서만 온다(팔레트 사본 금지).</summary>
        private static readonly Color AcceptFlashPeak = UiChrome.AccentSurface;
        private static readonly Color AcceptFlashEnd = new Color(
            UiChrome.AccentSurface.r, UiChrome.AccentSurface.g, UiChrome.AccentSurface.b, 0f);

        private AppControlDirector _appControl;
        private ArcheryDirector _archery;
        private GraffitiDirector _graffiti;
        private WindowTheftDirector _theft;
        private WindowCrashDirector _crash;
        private RunawayDirector _runaway;

        private Text _statusCaption;
        private Image _recallChip;
        private Text _recallLabel;
        private Text _group2Caption;
        private Image _quitSurface;
        private Text _quitLabel;
        private Text _footerHint;

        private bool _quitArmed;
        private float _quitArmTimer;

        /// <summary>닫기 예약(-1 = 없음). <b>[돌아와!] 칩만</b> 세운다 — 명령 타일은 2026-09-02부터
        /// 창을 닫지 않는다.</summary>
        private float _closeDelayTimer = -1f;

        protected override Vector2 PanelSizePoints => new Vector2(Width, Height);
        protected override string TitleText => "행동 명령";

        // ==================== 진단/테스트 접근자 ====================

        /// <summary>타일이 지금 <b>실행 가능</b>으로 그려져 있는가 — 회색 처리가 실제 판정과 같은지
        /// 회귀 테스트가 이 값을 실제 <c>GetAvailability()</c>와 대조한다.</summary>
        public bool IsCommandReady(Command command) => _tiles[(int)command].Ready;

        /// <summary>타일에 지금 붙어 있는 불가 사유(가능하면 null).</summary>
        public string CommandReason(Command command) => _tiles[(int)command].Reason;

        /// <summary>이 타일이 지금 <b>접수 플래시</b> 중인가(진단/테스트 창구). 창이 닫히지 않게 된 뒤
        /// "눌렸다"를 말하는 유일한 신호라, 이것이 죽으면 그대로 무반응 버튼이 된다.</summary>
        public bool IsCommandAcceptFlashing(Command command) => _tiles[(int)command].AcceptTimer >= 0f;

        /// <summary>이 타일이 지금 <b>거절 흔들림</b> 중인가(진단/테스트 창구).</summary>
        public bool IsCommandRejectShaking(Command command) => _tiles[(int)command].ShakeTimer >= 0f;

        /// <summary>성공 실행이 <b>창 닫기를 예약했는가</b>(진단/테스트 창구).
        /// <para>★ 네거티브 컨트롤용이다: "닫히지 않는다"를 <c>IsOpen</c>만으로 재면 닫기 예약이 서 있어도
        /// 관측 시점이 <see cref="SuccessCloseDelaySeconds"/>보다 이르면 <b>초록으로 보인다</b>. 예약 자체가
        /// 서지 않았음을 직접 본다.</para></summary>
        public bool IsCloseScheduled => _closeDelayTimer >= 0f;

        /// <summary>타일의 화면 사각형(Unity 스크린 픽셀) — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect CommandScreenRect(Command command) => ScreenRectOf(_tiles[(int)command].Rect);

        public Rect QuitButtonScreenRect => ScreenRectOf(_quitSurface != null ? _quitSurface.rectTransform : null);
        public Rect RecallChipScreenRect => ScreenRectOf(_recallChip != null ? _recallChip.rectTransform : null);

        /// <summary>[✕ 앱 종료]가 1차 클릭을 받아 "정말 종료?" 상태인가.</summary>
        public bool IsQuitArmed => _quitArmed;

        /// <summary>[돌아와!] 칩이 지금 보이는가 — 가출 중에만 true여야 한다.</summary>
        public bool IsRecallChipVisible => _recallChip != null && _recallChip.gameObject.activeSelf;

        /// <summary>헤더 상태 캡션(테스트/진단 전용).</summary>
        public string StatusCaption => _statusCaption != null ? _statusCaption.text : string.Empty;

        // ==================== 수명 주기 ====================

        protected override void Start()
        {
            base.Start();
            ResolveDirectors();
        }

        private void OnEnable() => StickmanEventBus.StateTransitioned += OnStateTransitioned;

        protected override void OnDisable()
        {
            base.OnDisable();
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
        }

        /// <summary>
        /// 상태 전이는 가용성을 <b>즉시</b> 바꾼다. 0.25초 폴링만 두면 "이제 됩니다"가 최대 0.25초 늦게
        /// 보이고, 반대로 "이제 안 됩니다"도 늦게 보여 사용자가 그 사이에 눌러 실패를 겪는다(36-7).
        /// </summary>
        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (IsOpen) RefreshContent();
        }

        private void ResolveDirectors()
        {
            if (_appControl == null)
            {
                _appControl = GetComponent<AppControlDirector>();
                if (_appControl == null) _appControl = Object.FindFirstObjectByType<AppControlDirector>();
            }
            if (_archery == null) _archery = Object.FindFirstObjectByType<ArcheryDirector>();
            if (_graffiti == null) _graffiti = Object.FindFirstObjectByType<GraffitiDirector>();
            if (_theft == null) _theft = Object.FindFirstObjectByType<WindowTheftDirector>();
            if (_crash == null) _crash = Object.FindFirstObjectByType<WindowCrashDirector>();
            if (_runaway == null) _runaway = Object.FindFirstObjectByType<RunawayDirector>();
        }

        // ==================== 내용 만들기 ====================

        protected override void BuildContent(RectTransform content)
        {
            BuildHeaderStatus(content);

            RectTransform group1 = BuildGroupCard(content, "Group1", "혼자 노는 것", Group1Y, Group1Height);
            _tiles[(int)Command.SayNow] = BuildTile(group1, Command.SayNow, 0);
            _tiles[(int)Command.Archery] = BuildTile(group1, Command.Archery, 1);
            _tiles[(int)Command.Graffiti] = BuildTile(group1, Command.Graffiti, 2);

            RectTransform group2 = BuildGroupCard(content, "Group2", "남의 창으로 노는 것", Group2Y, Group2Height);
            _tiles[(int)Command.WindowTheft] = BuildTile(group2, Command.WindowTheft, 0);
            _tiles[(int)Command.WindowCrash] = BuildTile(group2, Command.WindowCrash, 1);

            // ★ 비침해 캡션은 <b>필수</b>다(36-1의 7·8행). 이 두 명령은 이름만 보면 남의 창을 실제로
            //   옮기거나 부수는 것처럼 읽히는데, 실제로는 창 좌표를 바꾸는 API가 설계상 존재하지 않는다.
            _group2Caption = UiChrome.AddText(group2, "Caption", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_group2Caption.rectTransform, CardPadding,
                -(CardPadding + GroupTitleHeight + 4f + RowHeight * 2f + 4f), RowWidth, Group2CaptionHeight);
            _group2Caption.text = "실제 창은 옮기지도 닫지도 않아요. 전부 그림 위 연출.";

            BuildFooter(content);
        }

        private void BuildHeaderStatus(RectTransform content)
        {
            // 캡션 줄은 <b>상시</b> 존재한다 — 가출 중에만 나타나게 하면 그때마다 레이아웃이 점프한다(36-6).
            _statusCaption = UiChrome.AddText(content, "Status", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_statusCaption.rectTransform, 0f, StatusY,
                ContentWidth - RecallChipWidth - UiChrome.Space2, StatusRowHeight);

            _recallChip = UiChrome.AddSurface(content, "Recall", UiChrome.Accent, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(_recallChip.rectTransform, ContentWidth - RecallChipWidth, StatusY,
                RecallChipWidth, RecallChipHeight);
            _recallLabel = UiChrome.AddText(_recallChip.rectTransform, "Label", UiChrome.FontLabel,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(_recallLabel.rectTransform);
            _recallLabel.text = "돌아와!";
            Wire(_recallChip, "recall", OnRecallClicked);
            _recallChip.gameObject.SetActive(false);
        }

        private RectTransform BuildGroupCard(RectTransform content, string name, string title, float y, float height)
        {
            Image card = UiChrome.AddSurface(content, name, UiChrome.CardSurface, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(card.rectTransform, 0f, y, ContentWidth, height);
            UiChrome.AddOutline(card.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            Text label = UiChrome.AddText(card.rectTransform, "Title", UiChrome.FontLabel,
                TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(label.rectTransform, CardPadding, -CardPadding, RowWidth, GroupTitleHeight);
            label.text = title;
            return card.rectTransform;
        }

        private TileView BuildTile(RectTransform card, Command command, int rowInCard)
        {
            var view = new TileView();
            int index = (int)command;
            float y = -(CardPadding + GroupTitleHeight + 4f + rowInCard * RowHeight);

            // 행 사이 1pt 구분선 — 첫 행 위에는 그리지 않는다(그룹 제목과 붙어 보인다).
            if (rowInCard > 0)
            {
                Image divider = UiChrome.AddSurface(card, "Divider" + rowInCard, UiChrome.Divider, 2);
                UiChrome.PlaceTopLeft(divider.rectTransform, CardPadding, y, RowWidth, 1f);
            }

            view.Surface = UiChrome.AddSurface(card, "Tile" + index, IdleTileSurface, UiChrome.RadiusChip);
            view.Rect = view.Surface.rectTransform;
            UiChrome.PlaceTopLeft(view.Rect, CardPadding, y, RowWidth, RowHeight);
            view.BaseX = view.Rect.anchoredPosition.x;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(view.Rect, false);
            view.Icon = iconGo.GetComponent<RectTransform>();
            view.Icon.anchorMin = view.Icon.anchorMax = view.Icon.pivot = new Vector2(0f, 1f);
            view.Icon.sizeDelta = new Vector2(IconBox, IconBox);
            // 세로 중앙: 행 52 안에서 위아래 12pt씩. 획들은 이 상자의 중심을 원점으로 놓인다.
            view.Icon.anchoredPosition = new Vector2(8f, -(RowHeight - IconBox) * 0.5f);
            view.IconParts = BuildIcon(command, view.Icon);

            view.Name = UiChrome.AddText(view.Rect, "Name", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.InkTitle(true));
            UiChrome.PlaceTopLeft(view.Name.rectTransform, 48f, -10f, 130f, 18f);
            view.Name.text = CommandNames[index];

            view.Description = UiChrome.AddText(view.Rect, "Desc", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.InkMeta);
            UiChrome.PlaceTopLeft(view.Description.rectTransform, 48f, -28f, RowWidth - 56f, 16f);
            view.Description.text = CommandDescriptions[index];

            Wire(view.Surface, "cmd" + index, () => OnCommandClicked(command));
            return view;
        }

        private void BuildFooter(RectTransform content)
        {
            _footerHint = UiChrome.AddText(content, "FooterHint", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.InkMeta);
            UiChrome.PlaceTopLeft(_footerHint.rectTransform, 0f, FooterY,
                ContentWidth - QuitButtonWidth - UiChrome.Space4, QuitButtonHeight);
            // ★ 2026-09-02 (41-2 / C19) — 뒷문장을 붙인다. 민지가 [✕]를 누르기 전에 망설인 이유가
            //   "닫으면 시킨 일도 취소되나?"였고, <b>실제로는 취소되지 않는다</b>(명령은 창과 무관하게
            //   디렉터가 돌린다). 실제로 참인 사실만 적는다.
            //   상자 폭 340pt / 이 문장 실측 약 251pt(FontCaption 10, 한글 10·공백 3) — 넘치지 않는다.
            _footerHint.text = "내가 누를 때만 실행돼요. 창을 닫아도 하던 건 계속해요.";

            // ★ 36-10 — 우클릭 메뉴가 폐지되면서 <b>마우스만으로 도달하는 유일한 종료 경로</b>가 됐다.
            //   이 앱에는 Dock 아이콘도 메뉴바 아이콘도 트레이도 없다. 이 버튼이 없으면 전역 단축키가
            //   동작하지 않는 환경(_keyService == null)에서 강제 종료 외에 끄는 방법이 사라진다 —
            //   원칙 2·4의 명백한 위반이며 신뢰를 한 번에 잃는 종류의 실패다.
            //   그리드에서 44pt 이상 떨어진 푸터 오른쪽 끝에 둔다(오조준 방지).
            _quitSurface = UiChrome.AddSurface(content, "Quit", UiChrome.SubtleSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(_quitSurface.rectTransform, ContentWidth - QuitButtonWidth, FooterY,
                QuitButtonWidth, QuitButtonHeight);
            UiChrome.AddOutline(_quitSurface.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
            _quitLabel = UiChrome.AddText(_quitSurface.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(_quitLabel.rectTransform);
            _quitLabel.text = "✕ 앱 종료";
            Wire(_quitSurface, "quit", OnQuitClicked);
        }

        // ==================== 가용성 — 판정은 Director 하나에서만 나온다 ====================

        /// <summary>
        /// ★ 36-7 절대 규칙의 구현 지점. 이 창은 <b>자기 조건을 하나도 알지 못한다</b> — 락 점유 여부도,
        /// 상태 조건도, 과녁 자리 유무도 전부 Director에게 묻는다. 그래야 회색 처리와 실제 실행이
        /// 영원히 같은 답을 낸다.
        /// </summary>
        public CommandAvailability GetAvailability(Command command)
        {
            ResolveDirectors();
            switch (command)
            {
                case Command.SayNow:
                    return _appControl != null ? _appControl.GetSayNowAvailability() : CommandAvailability.Missing;
                case Command.Archery:
                    return _archery != null ? _archery.GetAvailability() : CommandAvailability.Missing;
                case Command.Graffiti:
                    return _graffiti != null ? _graffiti.GetAvailability() : CommandAvailability.Missing;
                case Command.WindowTheft:
                    return _theft != null ? _theft.GetAvailability() : CommandAvailability.Missing;
                default:
                    return _crash != null ? _crash.GetAvailability() : CommandAvailability.Missing;
            }
        }

        /// <summary>같은 Director가 실행한다 — <see cref="GetAvailability"/>와 짝이다.</summary>
        private bool Execute(Command command, string source)
        {
            ResolveDirectors();
            switch (command)
            {
                case Command.SayNow: return _appControl != null && _appControl.ForceSayNow(source);
                case Command.Archery: return _archery != null && _archery.ForceTriggerNow(source);
                case Command.Graffiti: return _graffiti != null && _graffiti.ForceTriggerNow(source);
                case Command.WindowTheft: return _theft != null && _theft.ForceTriggerNow(source);
                default: return _crash != null && _crash.ForceTriggerNow(source);
            }
        }

        // ==================== 동작 ====================

        /// <summary>
        /// <b>성공해도 실패해도 창은 남는다.</b> 실패는 그 자리에서 이유를 말하고(36-7), 성공은 접수
        /// 플래시를 한 번 내고 이어서 <b>상태에서 파생된</b> 문구로 스스로를 갱신한다.
        ///
        /// <para>★★ 2026-09-02 사용자 신고 — "행동 메뉴에서 활쏘기 한번 누르면 메뉴가 사라져버리는데
        /// 유지되어야함". 종전에는 성공 시 창과 부채꼴을 함께 접었고, 그 사유가 "명령을 내린 목적은
        /// 보는 것이라 창이 시야를 덮으면 목적이 실패한다"였다. <b>그 전제를 실측으로 검증했고 성립하지
        /// 않았다</b>:</para>
        ///
        /// <para>(1) <b>연출은 캐릭터가 서 있는 자리에서 일어나고, 이 창은 톱니가 있는 자리에 뜬다.</b>
        /// 둘은 서로 독립이며 <b>둘 다 사용자가 옮길 수 있다</b>(캐릭터는 드래그, 톱니는 자기 자리를
        /// 저장한다). 활쏘기는 발판 위 <b>가로</b> 밴드다 — 과녁 top이 정확히 신장 1H이다
        /// (<see cref="ArcheryDirector.TargetCenterHeight"/> = H − r, 반지름 r = 0.40H). 그래서 기본
        /// 배치(톱니 우상단 → 창이 화면 상단에서 아래로 560pt)에서는 바닥/Dock 발판 위의 활쏘기와
        /// <b>세로로 만나지 않는다</b>.</para>
        ///
        /// <para>(2) <b>명령별로 가를 근거가 없다.</b> 오히려 활쏘기가 <b>가장 안 겹치는</b> 축에 속한다 —
        /// [그라피티]는 캐릭터 기준 <b>360° 임의 방위</b> 200~300px 거리에 96px 영역을 잡고
        /// (<c>GraffitiDirector.TryFindEmptyRegion</c>), [창 부수기]는 <b>남의 창 사각형 전체</b>를
        /// 덮는다. 활쏘기만 특별 취급하면 더 크게 겹치는 두 명령이 조용히 반대 규칙을 갖게 된다.</para>
        ///
        /// <para>(3) 겹치더라도 <b>탈출 비용이 1클릭</b>이다. 반대로 종전 동작은 <b>매번</b> 다시
        /// 톱니 → [행동]을 거치게 했다.
        ///
        /// <b>★★ 2026-09-02 — 이 근거의 절반이 같은 날 무효가 됐다. 정직하게 적는다.</b>
        /// 원문은 "창 바깥 아무 곳이나 누르면 <c>PopoverPanel.FeedClick</c>이 닫는다"였는데, 사용자
        /// 지시("캐릭터창이나 다른 메뉴창들 … 사용자가 닫기전에는 안꺼져야함")로 <b>바깥 클릭이
        /// 더 이상 닫지 않는다</b>. 지금도 성립하는 것과 아닌 것을 갈라 두면:
        /// <list type="bullet">
        ///   <item><b>여전히 성립</b> — 탈출은 1클릭이다. 다만 그 1클릭의 자리가 "아무 데나"가 아니라
        ///     <b>오른쪽 위 [✕]</b>로 좁아졌다(조준이 필요해졌다).</item>
        ///   <item><b>여전히 성립</b> — 창 밖 클릭은 우리가 <b>먹지 않는다</b>. 차단막은 패널
        ///     사각형만 덮으므로 그 클릭은 아래 앱에 그대로 전달된다. 이제는 그 클릭이 창을 닫지
        ///     않을 뿐이다(<c>SurfaceOutsideClickTests</c>가 픽셀로 잰다).</item>
        ///   <item><b>무효</b> — "차단막의 수명에 사실상 상한이 있다". 이제 480×560 차단막은
        ///     <b>사용자가 [✕]를 누를 때까지</b> 남는다. 팝오버에는
        ///     <see cref="PopoverPanel.DefaultIdleAutoCloseSeconds"/>(180초 무입력)라는 상한이
        ///     남아 있지만, 그 시계는 <b>커서가 화면 어디에서도 안 움직일 때만</b> 흐른다 —
        ///     "자리를 비웠다"의 감지이지 사용 중 수명 상한이 아니다.</item>
        /// </list>
        /// 그래서 이 창의 원칙 2 근거는 <b>4개에서 3개 반</b>으로 줄었다. 그 대가를 알고 치른
        /// 것이며(사용자 지시가 우선), 남은 방어선은 "차단막이 패널 사각형에서 한 픽셀도 넓지
        /// 않다" 하나다 — <see cref="UiChrome"/>의 "창을 닫는 법" 절이 전문을 담고 있다.</para>
        ///
        /// <para>★ "연출 동안만 투명하게"는 <b>채택하지 않았다</b>. 알파를 낮춰도 차단막
        /// (<c>PopoverPanel.SyncClickBlocker</c>의 <c>enabled = !_closing</c>)은 그대로 남아, 사용자가
        /// <b>보이지 않는 480×560 클릭 차단막</b>을 밟게 된다 — 그 베이스가 주석까지 달아 막고 있는 바로
        /// 그 실패 모양(비침해 원칙 2)이고, 게다가 베이스의 <c>SetGrow</c>가 매 프레임 알파를 1로 되돌려
        /// 베이스 수정 없이는 구현 자체가 불가능하다.</para>
        /// </summary>
        private void OnCommandClicked(Command command)
        {
            int i = (int)command;
            CommandAvailability availability = GetAvailability(command);

            if (availability.IsReady && Execute(command, $"행동 명령창 [{CommandNames[i]}]"))
            {
                // ★ 실행이 상태 전이를 일으켰다면 StateTransitioned가 이미 RefreshContent를 돌려 다섯 타일을
                //   "지금 ○○ 중이에요"로 바꿔 놓았다. 접수 플래시는 그 위에 "이 칸을 눌렀다"만 얹는다.
                _tiles[i].AcceptTimer = 0f;
                Debug.Log($"[행동창] [{CommandNames[i]}] 실행 — 창은 닫지 않습니다" +
                    "(2026-09-02 사용자 지시: \"메뉴가 유지되어야함\"). " +
                    $"{AcceptFlashSeconds:F2}초 접수 플래시로 눌림을 알리고, 지금 무엇을 하는 중인지는 " +
                    "상태에서 파생된 타일 문구와 헤더 캡션이 계속 말합니다.");
                return;
            }

            // 여기까지 왔다 = 회색이 아니었는데 실행이 거부됐거나(락 경합/클릭관통 거부), 애초에 회색이었다.
            // 두 경우 모두 "왜"를 그 자리에서 말한다. 실행이 거부된 경우에는 판정을 다시 물어 최신 이유를 쓴다.
            CommandAvailability after = availability.IsReady ? GetAvailability(command) : availability;
            string reason = after.IsReady ? "지금은 시작할 수 없어요" : after.Reason;

            TileView tile = _tiles[i];
            tile.ShakeTimer = 0f;
            tile.NoticeTimer = 0f;
            tile.Reason = reason;
            tile.Description.text = reason;
            tile.Description.color = UiChrome.WarmAccent;
            Debug.Log($"[행동창] [{CommandNames[i]}] 실행 거절 — {reason}. 창은 닫지 않는다(조용한 실패 금지, 36-7).");
        }

        private void OnRecallClicked()
        {
            ResolveDirectors();
            if (_runaway == null || !_runaway.TryRecallNow("행동 명령창 [돌아와!]")) { RefreshContent(); return; }
            // ★ 2026-09-02 — 명령 타일은 이제 창을 닫지 않지만 <b>이 칩은 닫는다</b>. 사용자 신고는
            //   "행동 메뉴에서 (명령을) 한번 누르면"이었고, [돌아와!]는 명령 타일이 아니라 가출 중에만
            //   존재하는 일회성 응답이다 — 누른 즉시 칩 자신이 사라지므로 그 자리에 남아 봐야 고를 것이
            //   없다. (이 판단은 사용자 지시 문면 밖이라 리더 보고 대상이다.)
            Debug.Log("[행동창] [돌아와!] — 수동 소환 신호를 세웠습니다(20절 상시 탈출구). 창을 접습니다 " +
                "(명령 타일과 달리 이 칩은 누른 즉시 사라지는 일회성 응답이라 창에 남을 이유가 없습니다).");
            _closeDelayTimer = 0f;
        }

        /// <summary>
        /// 2단 확인. 1차 클릭은 라벨만 바꾸고 <see cref="QuitConfirmSeconds"/>초 유지한다.
        ///
        /// 모달 대화상자를 쓰지 않는 이유: 데이터 손실이 없음을 확인했다
        /// (<c>CharacterProgressionDirector.OnApplicationQuit()</c>이 저장한다). 창을 하나 더 띄우는
        /// 비용이 정당화되지 않는다.
        /// </summary>
        private void OnQuitClicked()
        {
            if (!_quitArmed)
            {
                _quitArmed = true;
                _quitArmTimer = 0f;
                ApplyQuitStyle();
                Debug.Log($"[행동창] [앱 종료] 1차 클릭 — {QuitConfirmSeconds:F0}초 안에 다시 누르면 종료합니다 " +
                    "(TodoBoardPopover의 삭제 확인과 같은 패턴).");
                return;
            }

            Debug.Log("[행동창] [앱 종료] 확정 — Application.Quit()을 호출합니다. " +
                "저장은 CharacterProgressionDirector.OnApplicationQuit()이 담당하므로 데이터 손실이 없습니다. 안녕히 계세요!");
            Close("[앱 종료] 확정");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void DisarmQuit()
        {
            if (!_quitArmed) return;
            _quitArmed = false;
            ApplyQuitStyle();
        }

        private void ApplyQuitStyle()
        {
            if (_quitLabel == null || _quitSurface == null) return;
            _quitLabel.text = _quitArmed ? "정말 종료?" : "✕ 앱 종료";
            _quitLabel.color = _quitArmed ? UiChrome.WarmAccent : UiChrome.TextSecondary;
            _quitSurface.color = _quitArmed ? UiChrome.AccentSurface : UiChrome.SubtleSurface;
        }

        protected override void OnOpened()
        {
            DisarmQuit();
            _closeDelayTimer = -1f;
            for (int i = 0; i < CommandCount; i++)
            {
                _tiles[i].ShakeTimer = -1f;
                _tiles[i].NoticeTimer = -1f;
                _tiles[i].AcceptTimer = -1f;
                _tiles[i].Surface.color = IdleTileSurface;
                _tiles[i].Rect.anchoredPosition = new Vector2(_tiles[i].BaseX, _tiles[i].Rect.anchoredPosition.y);
            }
        }

        protected override void OnClosing() => DisarmQuit();

        // ==================== 루프 ====================

        protected override void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            base.Update();
            if (!IsOpen) return;

            float dt = Time.unscaledDeltaTime;

            if (_quitArmed)
            {
                _quitArmTimer += dt;
                if (_quitArmTimer >= QuitConfirmSeconds) DisarmQuit();
            }

            TickTileFeedback(dt);

            if (_closeDelayTimer < 0f) return;
            _closeDelayTimer += dt;
            if (_closeDelayTimer < SuccessCloseDelaySeconds) return;
            _closeDelayTimer = -1f;
            CollapseFanAndClose();
        }

        /// <summary>창과 <b>부채꼴을 함께</b> 접는다. <b>남은 호출자는 [돌아와!] 하나뿐</b>이다 —
        /// 명령 타일은 2026-09-02부터 창을 닫지 않는다(<see cref="OnCommandClicked"/> 문서).
        /// <para>★ 2026-09-01 — 종전 구현은
        /// <c>if (fan != null) { fan.ForceCloseAll(...); return; }</c>였다. 부채꼴이 있는 <b>정식
        /// 조립에서는 아래 Close()가 한 번도 실행되지 않고</b>, 이 창이 닫히는 일이 "부채꼴이 자기
        /// 팝오버 참조를 해석해 준다"는 간접 경로에만 매달려 있었다 — 정보창 겹침 사고
        /// (<see cref="IExclusiveSurface"/> 문서)와 <b>완전히 같은 모양의 조기 반환</b>이다.
        /// 자기 자신을 먼저 닫고, 나머지는 배타 규칙의 단일 집행 지점에 맡긴다.</para></summary>
        private void CollapseFanAndClose()
        {
            Close("[돌아와!] 소환");
            ExclusiveSurfaces.CloseAllExcept(this, "[돌아와!] 소환 — 일회성 응답이라 창에 남을 것이 없다");
        }

        private void TickTileFeedback(float dt)
        {
            for (int i = 0; i < CommandCount; i++)
            {
                TileView tile = _tiles[i];

                if (tile.ShakeTimer >= 0f)
                {
                    tile.ShakeTimer += dt;
                    float k = Mathf.Clamp01(tile.ShakeTimer / ShakeSeconds);
                    // 감쇠 사인 2주기 — 끝에서 정확히 0으로 수렴해 위치가 어긋난 채 멈추지 않는다.
                    float offset = Mathf.Sin(k * Mathf.PI * 4f) * ShakeAmplitudePoints * (1f - k);
                    tile.Rect.anchoredPosition = new Vector2(tile.BaseX + offset, tile.Rect.anchoredPosition.y);
                    if (tile.ShakeTimer >= ShakeSeconds)
                    {
                        tile.ShakeTimer = -1f;
                        tile.Rect.anchoredPosition = new Vector2(tile.BaseX, tile.Rect.anchoredPosition.y);
                    }
                }

                if (tile.AcceptTimer >= 0f)
                {
                    tile.AcceptTimer += dt;
                    float k = Mathf.Clamp01(tile.AcceptTimer / AcceptFlashSeconds);
                    // 켜지는 것이 아니라 <b>꺼지는</b> 곡선이다 — 사용자가 손을 뗀 뒤에 밝아지면 그건
                    // 응답이 아니라 지연으로 읽힌다. 첫 프레임이 가장 밝고 그대로 사라진다.
                    tile.Surface.color = Color.Lerp(AcceptFlashPeak, AcceptFlashEnd, k);
                    if (tile.AcceptTimer >= AcceptFlashSeconds)
                    {
                        tile.AcceptTimer = -1f;
                        tile.Surface.color = IdleTileSurface;
                    }
                }

                if (tile.NoticeTimer < 0f) continue;
                tile.NoticeTimer += dt;
                if (tile.NoticeTimer < FailureNoticeSeconds) continue;
                tile.NoticeTimer = -1f;
                RefreshTile((Command)i, GetAvailability((Command)i));
            }
        }

        /// <summary>
        /// 0.25초 안전 폴링(<see cref="PopoverPanel.TickSlow"/>). <b>락 해제는 이벤트가 없다</b> — 상태
        /// 전이 이벤트만 구독하면 "다른 스펙터클이 끝나서 이제 됩니다"를 영원히 못 본다. 1Hz로는 최대
        /// 1초 늦게 보여 사용자가 그 사이에 다시 눌러 실패를 두 번 겪는다(36-7).
        /// </summary>
        protected override void TickSlow() => RefreshContent();

        // ==================== 갱신 ====================

        protected override void RefreshContent()
        {
            ResolveDirectors();

            bool runaway = _runaway != null && _runaway.IsRunawayActive;
            if (_recallChip.gameObject.activeSelf != runaway) _recallChip.gameObject.SetActive(runaway);

            int readyCount = 0;
            for (int i = 0; i < CommandCount; i++)
            {
                CommandAvailability availability = GetAvailability((Command)i);
                if (availability.IsReady) readyCount++;
                // 이유 문구를 보여주는 중인 타일은 건드리지 않는다 — 3초 안내가 폴링에 지워지면
                // 사용자는 이유를 읽을 시간을 얻지 못한다.
                if (_tiles[i].NoticeTimer >= 0f) continue;
                RefreshTile((Command)i, availability);
            }

            SetStatusCaption(runaway, readyCount);
        }

        /// <summary>
        /// 헤더 한 줄은 <b>지어내지 않는다</b> — 세 문장 전부 방금 계산한 실제 값(가출 여부 / 실행 가능
        /// 타일 수)에서만 파생한다. "전부 불가"는 빈 상태가 아니라 <b>불가 상태</b>이며 이 줄이 그 이유를
        /// 대표해서 말한다(36-7 예외 상태 표).
        /// </summary>
        private void SetStatusCaption(bool runaway, int readyCount)
        {
            string text = runaway ? "지금 가출 중이에요"
                : readyCount > 0 ? "지금 시킬 수 있어요"
                : "지금은 다른 일 하는 중이에요";
            if (_statusCaption.text != text) _statusCaption.text = text;
        }

        private void RefreshTile(Command command, CommandAvailability availability)
        {
            TileView tile = _tiles[(int)command];
            bool ready = availability.IsReady;
            tile.Ready = ready;
            tile.Reason = availability.Reason;

            // 이름과 이유를 <b>같은 사다리</b>에서 뽑는다 — 옛 코드는 여기서 이름 2.10 : 이유 3.51로
            // 서열이 뒤집혀 있었다(설정창에서도 같은 역전이 독립적으로 났다).
            Color nameColor = UiChrome.InkTitle(ready);
            Color iconColor = UiChrome.InkIcon(ready);
            if (tile.Name.color != nameColor) tile.Name.color = nameColor;
            for (int i = 0; i < tile.IconParts.Length; i++)
            {
                if (tile.IconParts[i] != null) tile.IconParts[i].color = iconColor;
            }

            // 36-7: 불가일 때는 설명 자리를 <b>이유 한 줄로 교체</b>한다. 설명을 남기고 회색만 입히면
            // 사용자는 "왜 안 되는지"를 영영 알 수 없다.
            string body = ready ? CommandDescriptions[(int)command] : availability.Reason;
            if (tile.Description.text != body) tile.Description.text = body;
            // ★ 이유 문장은 비활성 타일에서 <b>가장 중요한 글자</b>다. 흐려지지 않는다.
            Color bodyColor = UiChrome.InkMeta;
            if (tile.Description.color != bodyColor) tile.Description.color = bodyColor;
        }

        // ==================== 전역 클릭(비활성 앱에서의 첫 클릭 경로) ====================

        protected override void OnGlobalClick(Vector2 cursor)
        {
            if (_recallChip.gameObject.activeSelf && ContainsScreenPoint(_recallChip.rectTransform, cursor))
            {
                if (TryClaimAction("recall")) OnRecallClicked();
                return;
            }
            if (ContainsScreenPoint(_quitSurface.rectTransform, cursor))
            {
                if (TryClaimAction("quit")) OnQuitClicked();
                return;
            }
            for (int i = 0; i < CommandCount; i++)
            {
                if (!ContainsScreenPoint(_tiles[i].Rect, cursor)) continue;
                if (TryClaimAction("cmd" + i)) OnCommandClicked((Command)i);
                return;
            }
        }

        // ==================== 명령 아이콘 5종 (28×28, 두께 1.8, 프로시저럴) ====================

        /// <summary>부채꼴 심볼과 <b>같은 프로시저럴 규약</b>이다: 상자 중심 원점, +y 위, 스트로크만으로
        /// 그린다. 비트맵을 쓰지 않는 이유는 32-4와 같다 — 임의의 배율/잉크색에서 선 굵기를 우리가
        /// 통제할 수 있어야 하고, 에셋 파일이 늘면 Addressables 매니페스트와 이중 관리가 된다.</summary>
        private static Image[] BuildIcon(Command command, RectTransform box) => command switch
        {
            Command.SayNow => BuildSpeechIcon(box),
            Command.Archery => BuildArcheryIcon(box),
            Command.Graffiti => BuildSprayIcon(box),
            Command.WindowTheft => BuildWindowPushIcon(box),
            _ => BuildWindowCrackIcon(box),
        };

        /// <summary>말 걸기 — 둥근 말풍선 외곽 4획 + 꼬리 1획 + 점 3개.</summary>
        private static Image[] BuildSpeechIcon(RectTransform p)
        {
            var top = Stroke(p, "Top", 16f, 0f, new Vector2(0f, 7f));
            var bottom = Stroke(p, "Bottom", 16f, 0f, new Vector2(0f, -3f));
            var left = Stroke(p, "Left", 10f, 90f, new Vector2(-8f, 2f));
            var right = Stroke(p, "Right", 10f, 90f, new Vector2(8f, 2f));
            var tail = Stroke(p, "Tail", 5f, -70f, new Vector2(-3.5f, -5.5f));
            var d0 = Dot(p, "Dot0", new Vector2(-4.5f, 2f));
            var d1 = Dot(p, "Dot1", new Vector2(0f, 2f));
            var d2 = Dot(p, "Dot2", new Vector2(4.5f, 2f));
            return new[] { top, bottom, left, right, tail, d0, d1, d2 };
        }

        /// <summary>활쏘기 — 동심 링 2겹 + 오른쪽 위에서 들어오는 화살대 1획 + 깃 2획.</summary>
        private static Image[] BuildArcheryIcon(RectTransform p)
        {
            Image outer = UiChrome.AddCircle(p, "Outer", 18f, UiChrome.IconInk, IconStroke);
            Image inner = UiChrome.AddCircle(p, "Inner", 8f, UiChrome.IconInk, IconStroke);
            var shaft = Stroke(p, "Shaft", 14f, -35f, new Vector2(5.7f, 4f));
            var fletch0 = Stroke(p, "Fletch0", 4.5f, 20f, new Vector2(11.0f, 7.5f));
            var fletch1 = Stroke(p, "Fletch1", 4.5f, -85f, new Vector2(9.5f, 9.2f));
            return new[] { outer, inner, shaft, fletch0, fletch1 };
        }

        /// <summary>그라피티 — 캔 몸통(둥근 사각 아웃라인 4획) + 노즐 1획 + 분사 점 3개.</summary>
        private static Image[] BuildSprayIcon(RectTransform p)
        {
            var bodyL = Stroke(p, "BodyL", 16f, 90f, new Vector2(-6f, -2f));
            var bodyR = Stroke(p, "BodyR", 16f, 90f, new Vector2(2f, -2f));
            var bodyT = Stroke(p, "BodyT", 8f, 0f, new Vector2(-2f, 6f));
            var bodyB = Stroke(p, "BodyB", 8f, 0f, new Vector2(-2f, -10f));
            var nozzle = Stroke(p, "Nozzle", 4f, 90f, new Vector2(-2f, 8.5f));
            var s0 = Dot(p, "Spray0", new Vector2(6f, 9f));
            var s1 = Dot(p, "Spray1", new Vector2(9.5f, 6f));
            var s2 = Dot(p, "Spray2", new Vector2(9.5f, 11f));
            return new[] { bodyL, bodyR, bodyT, bodyB, nozzle, s0, s1, s2 };
        }

        /// <summary>창 도둑 — 창 프레임 4획 + 타이틀바 1획 + 오른쪽 밀기 화살표 2획.</summary>
        private static Image[] BuildWindowPushIcon(RectTransform p)
        {
            var top = Stroke(p, "Top", 16f, 0f, new Vector2(-2f, 8f));
            var bottom = Stroke(p, "Bottom", 16f, 0f, new Vector2(-2f, -8f));
            var left = Stroke(p, "Left", 16f, 90f, new Vector2(-10f, 0f));
            var right = Stroke(p, "Right", 16f, 90f, new Vector2(6f, 0f));
            var titleBar = Stroke(p, "TitleBar", 16f, 0f, new Vector2(-2f, 4f));
            var arrow0 = Stroke(p, "Arrow0", 4.5f, 40f, new Vector2(9.5f, 1.6f));
            var arrow1 = Stroke(p, "Arrow1", 4.5f, -40f, new Vector2(9.5f, -1.6f));
            return new[] { top, bottom, left, right, titleBar, arrow0, arrow1 };
        }

        /// <summary>창 부수기 — 창 프레임 4획 + 중심에서 뻗는 균열 3획.</summary>
        private static Image[] BuildWindowCrackIcon(RectTransform p)
        {
            var top = Stroke(p, "Top", 18f, 0f, new Vector2(0f, 9f));
            var bottom = Stroke(p, "Bottom", 18f, 0f, new Vector2(0f, -9f));
            var left = Stroke(p, "Left", 18f, 90f, new Vector2(-9f, 0f));
            var right = Stroke(p, "Right", 18f, 90f, new Vector2(9f, 0f));
            var crack0 = Stroke(p, "Crack0", 9f, 65f, new Vector2(1.9f, 4.1f));
            var crack1 = Stroke(p, "Crack1", 8f, 195f, new Vector2(-3.9f, -1.0f));
            var crack2 = Stroke(p, "Crack2", 8f, -55f, new Vector2(2.3f, -3.3f));
            return new[] { top, bottom, left, right, crack0, crack1, crack2 };
        }

        private static Image Stroke(RectTransform parent, string name, float length, float degrees, Vector2 center)
            => UiChrome.AddStroke(parent, name, length, IconStroke, degrees, center, UiChrome.IconInk);

        private static Image Dot(RectTransform parent, string name, Vector2 center)
        {
            Image dot = UiChrome.AddCircle(parent, name, 2f, UiChrome.IconInk);
            dot.rectTransform.anchoredPosition = center;
            return dot;
        }
    }
}
