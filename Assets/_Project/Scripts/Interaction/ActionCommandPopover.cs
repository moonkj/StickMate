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
    /// ① 32-3의 앵커/탈출구/자동접힘 규칙(밖 클릭·기어 재클릭·버튼 재클릭·<c>AnyPopoverOpen()</c> 동안
    ///    자동접힘 정지)이 <b>전부 공짜로 상속된다</b>.
    /// ② 전체 화면을 덮는 <c>ScreenScrim</c>이 없다 — 이 창은 하루에 여러 번 열리는 런처이고, 열 때마다
    ///    바탕화면을 어둡게 덮는 것은 비침해 원칙(CLAUDE.md 2)에 대한 과청구다.
    /// ③ 명령을 내린 뒤 <b>봐야 할 것은 캐릭터</b>다. 그래서 성공하면 창이 스스로 비켜준다(아래).
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
        private const float Height = 560f;

        /// <summary>세로는 설정창(720×560)과 <b>같은 560</b> — 같은 앱 가족으로 보이게 한다.
        /// 가로는 셋 중 가장 좁다: 면적 순서(정보창 &gt; 설정창 &gt; 행동창)가 곧 중요도 순서다.</summary>
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
        private const float Group1Height = CardPadding * 2f + GroupTitleHeight + 4f + RowHeight * 4f;  // 250.
        private const float Group2Y = Group1Y - Group1Height - 12f;                  // -296.
        private const float Group2CaptionHeight = 16f;
        private const float Group2Height = CardPadding * 2f + GroupTitleHeight + 4f + RowHeight * 2f
                                           + 4f + Group2CaptionHeight;              // 166.
        private const float FooterY = Group2Y - Group2Height - 12f;                  // -474.

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

        /// <summary>성공 후 창이 비켜주기까지. 명령을 내린 목적은 "보는 것"이라 창이 시야를 덮고 있으면
        /// 목적이 실패한다(36-7).</summary>
        public const float SuccessCloseDelaySeconds = 0.12f;

        // ==================== 명령 정의 ====================

        /// <summary>값이 곧 화면상의 순서다(위에서 아래).</summary>
        public enum Command
        {
            SayNow = 0,
            Archery = 1,
            Battle = 2,
            Graffiti = 3,
            WindowTheft = 4,
            WindowCrash = 5,
        }

        public const int CommandCount = 6;

        private static readonly string[] CommandNames =
        {
            "말 걸기", "활쏘기", "격파 놀이", "그라피티", "창 도둑", "창 부수기",
        };

        private static readonly string[] CommandDescriptions =
        {
            "지금 상태 그대로 한마디 합니다",
            "과녁을 세우고 세 발 쏩니다",
            "기를 모아 판을 격파합니다",
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
            public float BaseX;
        }

        private readonly TileView[] _tiles = new TileView[CommandCount];

        private AppControlDirector _appControl;
        private ArcheryDirector _archery;
        private BattleMinigameDirector _battle;
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
        private float _closeDelayTimer = -1f;

        protected override Vector2 PanelSizePoints => new Vector2(Width, Height);
        protected override string TitleText => "행동 명령";

        // ==================== 진단/테스트 접근자 ====================

        /// <summary>타일이 지금 <b>실행 가능</b>으로 그려져 있는가 — 회색 처리가 실제 판정과 같은지
        /// 회귀 테스트가 이 값을 실제 <c>GetAvailability()</c>와 대조한다.</summary>
        public bool IsCommandReady(Command command) => _tiles[(int)command].Ready;

        /// <summary>타일에 지금 붙어 있는 불가 사유(가능하면 null).</summary>
        public string CommandReason(Command command) => _tiles[(int)command].Reason;

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
            if (_battle == null) _battle = Object.FindFirstObjectByType<BattleMinigameDirector>();
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
            _tiles[(int)Command.Battle] = BuildTile(group1, Command.Battle, 2);
            _tiles[(int)Command.Graffiti] = BuildTile(group1, Command.Graffiti, 3);

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

            view.Surface = UiChrome.AddSurface(card, "Tile" + index, new Color(0f, 0f, 0f, 0f), UiChrome.RadiusChip);
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
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.PlaceTopLeft(view.Name.rectTransform, 48f, -10f, 130f, 18f);
            view.Name.text = CommandNames[index];

            view.Description = UiChrome.AddText(view.Rect, "Desc", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(view.Description.rectTransform, 48f, -28f, RowWidth - 56f, 16f);
            view.Description.text = CommandDescriptions[index];

            Wire(view.Surface, "cmd" + index, () => OnCommandClicked(command));
            return view;
        }

        private void BuildFooter(RectTransform content)
        {
            _footerHint = UiChrome.AddText(content, "FooterHint", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextQuaternary);
            UiChrome.PlaceTopLeft(_footerHint.rectTransform, 0f, FooterY,
                ContentWidth - QuitButtonWidth - UiChrome.Space4, QuitButtonHeight);
            _footerHint.text = "내가 누를 때만 실행돼요.";

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
                case Command.Battle:
                    return _battle != null ? _battle.GetAvailability() : CommandAvailability.Missing;
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
                case Command.Battle: return _battle != null && _battle.ForceTriggerNow(source);
                case Command.Graffiti: return _graffiti != null && _graffiti.ForceTriggerNow(source);
                case Command.WindowTheft: return _theft != null && _theft.ForceTriggerNow(source);
                default: return _crash != null && _crash.ForceTriggerNow(source);
            }
        }

        // ==================== 동작 ====================

        /// <summary>
        /// 성공하면 창이 비켜주고, 실패하면 창이 남아서 이유를 말한다(36-7).
        ///
        /// 실패 시 <b>닫지 않는 것</b>이 핵심이다 — 창이 닫히면서 아무 일도 안 일어나면 사용자는 자기가
        /// 잘못 눌렀다고 생각하고 같은 실패를 반복한다. 조용한 실패는 금지다.
        /// </summary>
        private void OnCommandClicked(Command command)
        {
            int i = (int)command;
            CommandAvailability availability = GetAvailability(command);

            if (availability.IsReady && Execute(command, $"행동 명령창 [{CommandNames[i]}]"))
            {
                Debug.Log($"[행동창] [{CommandNames[i]}] 실행 — {SuccessCloseDelaySeconds:F2}초 뒤 창과 부채꼴을 " +
                    "함께 접습니다(명령을 내린 목적은 '보는 것'이라 창이 시야를 덮고 있으면 목적이 실패한다).");
                _closeDelayTimer = 0f;
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
            Debug.Log("[행동창] [돌아와!] — 수동 소환 신호를 세웠습니다(20절 상시 탈출구). 창을 접습니다.");
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
                _tiles[i].Rect.anchoredPosition = new Vector2(_tiles[i].BaseX, _tiles[i].Rect.anchoredPosition.y);
            }
        }

        protected override void OnClosing() => DisarmQuit();

        // ==================== 루프 ====================

        protected override void Update()
        {
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

        /// <summary>성공 후에는 창만이 아니라 <b>부채꼴도 함께</b> 접는다 — 화면에 캐릭터만 남아야 한다(36-7).</summary>
        private void CollapseFanAndClose()
        {
            var fan = GetComponent<GearRadialMenuWidget>();
            if (fan != null) { fan.ForceCloseAll("행동 명령 실행 — 화면을 비운다"); return; }
            Close("행동 명령 실행");
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

            Color nameColor = ready ? UiChrome.TextPrimary : UiChrome.TextDisabled;
            Color iconColor = ready ? UiChrome.IconInk : UiChrome.TextDisabled;
            if (tile.Name.color != nameColor) tile.Name.color = nameColor;
            for (int i = 0; i < tile.IconParts.Length; i++)
            {
                if (tile.IconParts[i] != null) tile.IconParts[i].color = iconColor;
            }

            // 36-7: 불가일 때는 설명 자리를 <b>이유 한 줄로 교체</b>한다. 설명을 남기고 회색만 입히면
            // 사용자는 "왜 안 되는지"를 영영 알 수 없다.
            string body = ready ? CommandDescriptions[(int)command] : availability.Reason;
            if (tile.Description.text != body) tile.Description.text = body;
            Color bodyColor = ready ? UiChrome.TextTertiary : UiChrome.TextQuaternary;
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

        // ==================== 명령 아이콘 6종 (28×28, 두께 1.8, 프로시저럴) ====================

        /// <summary>부채꼴 심볼과 <b>같은 프로시저럴 규약</b>이다: 상자 중심 원점, +y 위, 스트로크만으로
        /// 그린다. 비트맵을 쓰지 않는 이유는 32-4와 같다 — 임의의 배율/잉크색에서 선 굵기를 우리가
        /// 통제할 수 있어야 하고, 에셋 파일이 늘면 Addressables 매니페스트와 이중 관리가 된다.</summary>
        private static Image[] BuildIcon(Command command, RectTransform box) => command switch
        {
            Command.SayNow => BuildSpeechIcon(box),
            Command.Archery => BuildArcheryIcon(box),
            Command.Battle => BuildBreakIcon(box),
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

        /// <summary>격파 놀이 — 세로 판 2조각(좌우로 5° 벌어짐) + 가운데 지그재그 균열 3획.</summary>
        private static Image[] BuildBreakIcon(RectTransform p)
        {
            var leftPlank = Stroke(p, "PlankL", 20f, 95f, new Vector2(-5.5f, 0f));
            var rightPlank = Stroke(p, "PlankR", 20f, 85f, new Vector2(5.5f, 0f));
            var crack0 = Stroke(p, "Crack0", 6f, -60f, new Vector2(-1.5f, 6f));
            var crack1 = Stroke(p, "Crack1", 6f, -120f, new Vector2(1.5f, 0f));
            var crack2 = Stroke(p, "Crack2", 6f, -60f, new Vector2(-1.5f, -6f));
            return new[] { leftPlank, rightPlank, crack0, crack1, crack2 };
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
