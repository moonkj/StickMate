using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 부채꼴 [오늘 할일] 버튼에서 자라나는 팝오버 — docs/UX_FLOW.md <b>32-6</b> 확정 설계. 300×336.
    /// 지금까지 이 앱에서 할일을 <b>적을</b> 방법은 ⌃⌥⌘J 데모 경로뿐이었다(사용자가 발견할 수 없는
    /// 기능). 이 패널이 그 입구다.
    ///
    /// ============================================================================
    /// 삭제는 "되돌리기"가 아니라 <b>인라인 확인 3초</b>다
    /// ============================================================================
    /// <see cref="TodoListModel"/>에는 복구 API가 없다. <see cref="TodoListModel.Add"/>로 되살리면 새 Id에
    /// 맨 뒤 순서라 <b>원래대로 돌아오지 않는다</b> — 되돌려주겠다고 써놓고 다른 결과를 주는 것이 이
    /// 프로젝트가 가장 싫어하는 형태다. 인라인 확인은 모델 변경 0으로 같은 안전을 준다.
    ///
    /// ============================================================================
    /// 빈 상태는 포스트잇과 <b>정반대</b>로 처리한다
    /// ============================================================================
    /// 포스트잇(앰비언트)은 0건이면 카드를 숨기지만, 이 패널은 사용자가 <b>직접 열었으므로</b> 숨기면
    /// 막다른 길이 된다. 목록 자리에 안내 두 줄을 남기고 입력칸을 그대로 둔다(가짜 일러스트 없음).
    ///
    /// ============================================================================
    /// 2026-09-08 — 행 글자는 <b>흘러나가지 않고 잘린다</b> / 취소선은 <b>글리프가 아니라 선</b>이다
    /// ============================================================================
    /// ux-widgets 감사(docs/UX_WIDGETS.md <b>R6-0-2</b>)가 이 패널에서 결함 2건을 실측으로 찾았다.
    /// 둘 다 500×512 고도화(R6)와 <b>무관하게 지금 크기에서 이미 재현되던 것</b>이라 먼저 고쳤다.
    /// <list type="number">
    /// <item>라벨이 <c>Overflow</c>로 그려지고 마스크가 없어 <b>패널 밖으로 흘렀다</b> —
    ///   60자면 474pt까지 나가 차단막 밖 바탕화면에 글자가 그려졌다.
    ///   → <see cref="UiChrome.Ellipsize"/>(정보창이 같은 사고로 만든 함수)를 <see cref="RowLabelWidth"/>로 건다.</item>
    /// <item>취소선이 U+0336 결합문자 조립이라 <b>폰트 폴백에 기대고</b> 있었다(Windows 미확인).
    ///   → <see cref="TodoPostItWidget"/>가 이미 검증한 <b>1pt Image 선</b>으로 통일한다.</item>
    /// </list>
    /// 어느 쪽도 새 설계가 아니다 — 이 저장소에 이미 있는 처방 둘을 그대로 가져왔다.
    ///
    /// <b>쓸어담기 중복 금지</b>: <see cref="TodoListModel.SweepCompleted"/>는
    /// <see cref="TodoPostItWidget"/>가 이미 0.5초 주기로 부른다(카드가 숨겨져 있어도 돈다). 이 패널은
    /// 호출하지 않고 <see cref="StickmanEventBus.TodoListChanged"/>만 구독한다 — 청소 주체는 하나여야 한다.
    /// </summary>
    public sealed class TodoBoardPopover : PopoverPanel
    {
        private const float Width = 300f;
        private const float Height = 336f;
        private const float ContentWidth = Width - UiChrome.Space4 * 2f;   // 268.

        private const int VisibleRows = 6;
        private const float RowHeight = 33f;
        private const float RowGap = 4f;
        private const float RailWidth = 22f;
        private const float RowWidth = ContentWidth - RailWidth - UiChrome.Space1;  // 242.

        // ==================== 행 내부 가로 배분 — 숫자를 두 벌 두지 않는다 ====================
        // ★★ 2026-09-08 ux-widgets 감사 R6-0-2(가) — 여기에는 <b>162</b>(라벨 상자)와 <b>32</b>([✕] 왼끝)가
        //   서로를 모른 채 손으로 맞춰져 있었다. 그리고 상자를 162로 잡아 봐야 소용이 없었다 —
        //   <see cref="UiChrome.AddText"/>는 <c>horizontalOverflow = Overflow</c>로 그리고 이 패널에는
        //   <c>RectMask2D</c>도 <c>Mask</c>도 없어서 <b>상자가 자르지 않기 때문</b>이다. 실측:
        //   17자면 잉크가 [✕] 버튼 위를 덮었고, 입력 상한 60자면 패널(300) 밖 474pt까지 흘러
        //   <b>클릭관통 차단막 바깥의 바탕화면에 글자가 그려졌다</b>(비침해 원칙 2에 정면으로 걸린다).
        //   이제 (가) 라벨 폭은 <see cref="RowWidth"/>에서 파생되고 (나) 그 <b>같은 상수</b>를
        //   <see cref="UiChrome.Ellipsize"/>가 예산으로 받는다. 폭이 넓어지는 날(R6 500×512)
        //   상자와 «자르는 기준»이 함께 움직인다 — 이 저장소가 폭 1042 때 한쪽만 따라가서 깨진 적이 있다.
        private const float RowBoxLeft = 10f;                                     // 체크박스 좌여백.
        private const float RowBoxSize = 20f;                                     // 체크박스 한 변.
        private const float RowLabelLeft = RowBoxLeft + RowBoxSize + UiChrome.Space2;   // 38.
        private const float RowLabelHeight = 18f;
        private const float RowDeleteSize = 22f;                                  // [✕] 한 변(최소 타깃).
        private const float RowDeleteRightInset = 10f;                            // 행 오른끝 ~ [✕] 오른끝.
        private const float RowLabelToDeleteGap = 10f;                            // 라벨 상자 ~ [✕] 숨구멍.
        private const float RowDeleteLeft = RowWidth - RowDeleteRightInset - RowDeleteSize;  // 210.

        /// <summary>행 라벨 상자의 폭(pt). <b>배치와 말줄임이 같은 값을 쓴다.</b>
        /// <para>★ <c>public</c>인 이유: 이 값을 재는 감사 테스트가 숫자를 <b>베끼지 않게</b> 한다
        /// (CLAUDE.md 협업 프로토콜 — 상한값은 그 상수를 참조해 검증한다).
        /// <see cref="PopoverPanel.CloseChipWidth"/>가 같은 사정으로 이미 public이다.</para></summary>
        public const float RowLabelWidth =
            RowWidth - RowLabelLeft - RowLabelToDeleteGap - RowDeleteSize - RowDeleteRightInset;   // 162.

        private const float ListTop = -46f;
        private const float ListHeight = VisibleRows * RowHeight + (VisibleRows - 1) * RowGap;  // 218.

        /// <summary>삭제 확인이 열려 있는 시간. 지나면 조용히 취소된다.</summary>
        private const float DeleteConfirmSeconds = 3f;

        /// <summary>입력칸이 받는 글자 수 상한. <b>말줄임이 감당해야 하는 최악</b>이 이 값이다
        /// (R6-0-2 실측: 60자면 잉크 720pt로 패널 300 밖 474pt까지 흘렀다).
        /// <para>★ <c>public</c>인 이유: 그 최악을 재현하는 테스트가 60을 <b>손으로 베끼면</b>
        /// 상한이 바뀌는 날 조용히 «짧은 글자»를 넣고 초록이 된다.</para></summary>
        public const int InputCharacterLimit = 60;

        private enum Tab { Active = 0, Archive = 1 }

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Box;          // 체크박스.
            public Image BoxCheck;
            public Text Label;
            /// <summary>완료 취소선 — 1pt <see cref="Image"/> 선. 결합문자(U+0336) 조립이 아니다.</summary>
            public Image Strike;
            /// <summary>말줄임을 <b>다시 계산해야 하는지</b> 판정하는 캐시(원본 문자열).
            /// <see cref="UiChrome.Ellipsize"/>는 폭을 재려고 <c>Text.text</c>를 여러 번 바꾸므로
            /// 내용이 실제로 바뀐 순간에만 부른다(그 함수의 «호출부 규약»).</summary>
            public string LastLabelSource;
            public bool LastCompleted;
            public Image DeleteButton;
            public Text DeleteGlyph;
            public RectTransform Confirm;
            public Image ConfirmYes;
            public Image ConfirmNo;
            public int BoundId = -1;
        }

        private readonly RowView[] _rows = new RowView[VisibleRows];
        private readonly List<TodoItem> _page = new List<TodoItem>(VisibleRows);

        private readonly Image[] _tabChips = new Image[2];
        private readonly Text[] _tabLabels = new Text[2];
        private InputField _input;
        private Image _addSurface;
        private Text _addLabel;
        private Text _softCapNotice;
        private Text _emptyTitle;
        private Text _emptyBody;
        private Text _footer;
        private Image _railUp;
        private Image _railDown;

        private Tab _tab = Tab.Active;
        private int _scroll;
        private int _confirmingId = -1;
        private float _confirmTimer;
        private bool _softCapHit;

        protected override Vector2 PanelSizePoints => new Vector2(Width, Height);
        protected override string TitleText => "오늘 할일";

        // ★★ 2026-09-08 (사용자 신고 "각 메뉴들의 창을 마우스로 끌어서 움직일수있게 변경했었는데
        //   지금은 또 안됨") — 이 팝오버는 <b>드래그가 애초에 안 붙어 있었다</b>.
        //   2026-09-07 라운드의 요청 원문이 "모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)"으로
        //   세 개만 지목했고, 나머지 팝오버 2종은 그때 배정 밖이었다 — 그래서 «되던 게 깨진 것»이
        //   아니라 «처음부터 없던 것»인데, 사용자에게는 <b>겉보기가 같다</b>.
        //   FocusSessionPopover가 남겨 둔 예고("켜려면 이 두 줄과 세이브 칸만 있으면 된다")대로
        //   두 줄과 UiWindowId·CharacterSaveStore 3필드를 더해 켠다.
        //
        //   손잡이는 «패널 안이면서 어떤 버튼 위도 아닌» 자리다 — 버튼 목록은
        //   <see cref="PopoverPanel"/>가 빌드 직후 한 번 긁어 자동으로 들고 있으므로
        //   여기서 사각형을 손으로 적지 않는다(새 버튼이 늘어도 판정이 따라온다).
        protected override bool WindowDragEnabled => true;

        protected override UiWindowId WindowDragId => UiWindowId.TodoBoard;

        /// <summary>지금 보고 있는 탭(0=할일, 1=완료함) — 테스트/진단 전용.</summary>
        public int ActiveTab => (int)_tab;

        /// <summary>삭제 확인이 열려 있는 항목 Id(-1이면 없음).</summary>
        public int PendingDeleteId => _confirmingId;

        private void OnEnable() => StickmanEventBus.TodoListChanged += OnTodoListChanged;

        protected override void OnDisable()
        {
            base.OnDisable();
            StickmanEventBus.TodoListChanged -= OnTodoListChanged;
        }

        private void OnTodoListChanged()
        {
            if (IsOpen) RefreshContent();
        }

        // ==================== 내용 만들기 ====================

        protected override void BuildContent(RectTransform content)
        {
            BuildTabs();

            // ---- 입력 줄 ----
            _input = CreateInputField(content);
            UiChrome.PlaceTopLeft(_input.GetComponent<RectTransform>(), 0f, 0f, 190f, 30f);
            _input.onEndEdit.AddListener(OnInputSubmitted);

            _addSurface = UiChrome.AddSurface(content, "Add", UiChrome.Accent, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(_addSurface.rectTransform, ContentWidth - 66f, 0f, 66f, 30f);
            _addLabel = UiChrome.AddText(_addSurface.rectTransform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(_addLabel.rectTransform);
            _addLabel.text = "추가";
            Wire(_addSurface, "add", AddFromInput);

            _softCapNotice = UiChrome.AddText(content, "SoftCap", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.WarmAccent);
            UiChrome.PlaceTopLeft(_softCapNotice.rectTransform, 0f, -32f, ContentWidth, 12f);
            _softCapNotice.text = "할일이 많아요. 먼저 정리해볼까요?";
            _softCapNotice.gameObject.SetActive(false);

            // ---- 목록 ----
            for (int i = 0; i < VisibleRows; i++) _rows[i] = BuildRow(content, i);

            _emptyTitle = UiChrome.AddText(content, "EmptyTitle", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(_emptyTitle.rectTransform, 0f, ListTop - ListHeight * 0.5f + 20f, ContentWidth, 18f);
            _emptyTitle.text = "아직 비어 있어요";

            _emptyBody = UiChrome.AddText(content, "EmptyBody", UiChrome.FontLabel,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_emptyBody.rectTransform, 0f, ListTop - ListHeight * 0.5f, ContentWidth, 16f);
            _emptyBody.text = "위에 적어두면 제가 가끔 챙겨줄게요.";

            // ---- 페이지 넘김 레일 ([▲][▼] — 휠에 기대지 않는다) ----
            _railUp = BuildRail(content, "RailUp", "▲", ListTop, () => Scroll(-1));
            _railDown = BuildRail(content, "RailDown", "▼", ListTop - ListHeight + RowHeight, () => Scroll(1));

            _footer = UiChrome.AddText(content, "Footer", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.PlaceTopLeft(_footer.rectTransform, 0f, ListTop - ListHeight - UiChrome.Space1, ContentWidth, 12f);
        }

        private void BuildTabs()
        {
            string[] names = { "할일", "완료함" };
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                Image chip = UiChrome.AddSurface(Panel, "Tab" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                // ★ 2026-09-02 — 여기 있던 <c>Width - Space4 - 22f - ...</c>는 닫기 칩 폭 22를 숫자로
                //   베낀 식이었다. 칩이 44로 넓어지자 탭 칩과 <b>18pt 겹쳤다</b>(계산: 탭1 212~258 대
                //   닫기 240~284). 이제 CloseChipLeft에서 파생한다 — 유지되는 것은 <b>닫기 칩과의
                //   간격 Space1(4pt)</b>이고, 탭 두 개는 칩이 자란 만큼 왼쪽으로 22pt 밀린다
                //   (@300: 164/212 → 142/190). 제목 <b>상자</b>(16~236)는 전부터 탭 칩 위를 덮고 있었고
                //   지금도 그렇다 — 제목 글자는 MiddleLeft + Overflow라 "오늘 할일"이 x≈76에서 끝나므로
                //   글리프는 겹치지 않는다(상자끼리의 겹침은 이번 변경 이전부터 있던 상태다).
                UiChrome.PlaceTopLeft(chip.rectTransform,
                    CloseChipLeft - UiChrome.Space1 - (2 - i) * 46f - (1 - i) * 2f,
                    -UiChrome.Space3, 46f, 22f);
                UiChrome.AddOutline(chip.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
                Text label = UiChrome.AddText(chip.rectTransform, "Label", UiChrome.FontCaption,
                    TextAnchor.MiddleCenter, UiChrome.TextSecondary);
                UiChrome.Stretch(label.rectTransform);
                label.text = names[i];
                _tabChips[i] = chip;
                _tabLabels[i] = label;
                Wire(chip, "tab" + i, () => SelectTab((Tab)index));
            }
        }

        private RowView BuildRow(RectTransform content, int index)
        {
            var view = new RowView();
            view.Surface = UiChrome.AddSurface(content, "Row" + index, UiChrome.CardSurface, UiChrome.RadiusCard);
            view.Rect = view.Surface.rectTransform;
            UiChrome.PlaceTopLeft(view.Rect, 0f, ListTop - index * (RowHeight + RowGap), RowWidth, RowHeight);
            UiChrome.AddOutline(view.Rect, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);

            view.Box = UiChrome.AddSurface(view.Rect, "Box", UiChrome.SubtleSurface, 4);
            UiChrome.PlaceTopLeft(view.Box.rectTransform, RowBoxLeft, -(RowHeight - RowBoxSize) * 0.5f, RowBoxSize, RowBoxSize);
            UiChrome.AddOutline(view.Box.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.SubtleSurface), 4);
            view.BoxCheck = UiChrome.AddCircle(view.Box.rectTransform, "Dot", 10f, UiChrome.Accent);
            view.BoxCheck.gameObject.SetActive(false);

            view.Label = UiChrome.AddText(view.Rect, "Label", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.PlaceTopLeft(view.Label.rectTransform, RowLabelLeft,
                -(RowHeight - RowLabelHeight) * 0.5f, RowLabelWidth, RowLabelHeight);

            // ---- 취소선(완료 표시) ----
            // ★ 2026-09-08 R6-0-2(나) / docs/UX_WIDGETS.md UW-6-3 — 여기 있던 것은 글자마다 U+0336을
            //   끼워 넣는 <b>결합문자 조립</b>이었다. 폰트는 LegacyRuntime.ttf + OS 폴백이고,
            //   이 저장소는 같은 종류의 글리프 가용성 문제로 이미 한 번 데였다(U+2715 두부 논쟁,
            //   <see cref="UiChrome"/> 참고 — macOS는 실기 캡처로 확인됐지만 <b>Windows는 미확인</b>).
            //   <see cref="TodoPostItWidget"/>는 같은 문제를 이미 <b>1pt Image 선</b>으로 풀어 놨다.
            //   신규 설계를 만들지 않고 그 처방을 그대로 옮긴다 — 앵커/두께/색까지 같다.
            //   (라벨 <b>뒤</b>가 아니라 <b>다음 형제</b>로 만든다: uGUI는 나중 형제가 위에 그려진다.)
            view.Strike = UiChrome.AddSurface(view.Rect, "Strike", UiChrome.TextTertiary, UiChrome.RadiusDot);
            RectTransform strikeRect = view.Strike.rectTransform;
            strikeRect.anchorMin = strikeRect.anchorMax = new Vector2(0f, 1f);
            strikeRect.pivot = new Vector2(0f, 0.5f);
            // 라벨은 행 안에서 세로 정중앙이다(위 PlaceTopLeft의 -(RowHeight - RowLabelHeight)*0.5).
            // 그래서 선의 y도 -RowHeight*0.5 하나로 떨어진다 — 두 식이 갈라질 여지를 두지 않는다.
            strikeRect.anchoredPosition = new Vector2(RowLabelLeft, -RowHeight * 0.5f);
            strikeRect.sizeDelta = new Vector2(0f, 1f);
            view.Strike.raycastTarget = false;
            view.Strike.gameObject.SetActive(false);

            view.DeleteButton = UiChrome.AddSurface(view.Rect, "Delete", new Color(0f, 0f, 0f, 0f), UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(view.DeleteButton.rectTransform, RowDeleteLeft,
                -(RowHeight - RowDeleteSize) * 0.5f, RowDeleteSize, RowDeleteSize);
            view.DeleteGlyph = UiChrome.AddText(view.DeleteButton.rectTransform, "Glyph", UiChrome.FontBody,
                TextAnchor.MiddleCenter, UiChrome.TextTertiary);
            UiChrome.Stretch(view.DeleteGlyph.rectTransform);
            view.DeleteGlyph.text = "✕";

            // ---- 삭제 확인 오버레이(같은 자리에서 3초) ----
            var confirmGo = new GameObject("Confirm", typeof(RectTransform));
            confirmGo.transform.SetParent(view.Rect, false);
            view.Confirm = confirmGo.GetComponent<RectTransform>();
            UiChrome.Stretch(view.Confirm);

            Image confirmSurface = UiChrome.AddSurface(view.Confirm, "Surface", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            UiChrome.Stretch(confirmSurface.rectTransform);
            Text ask = UiChrome.AddText(view.Confirm, "Ask", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextSecondary);
            UiChrome.PlaceTopLeft(ask.rectTransform, 10f, -(RowHeight - 16f) * 0.5f, 90f, 16f);
            ask.text = "삭제할까요?";

            view.ConfirmYes = UiChrome.AddSurface(view.Confirm, "Yes", UiChrome.Accent, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(view.ConfirmYes.rectTransform, RowWidth - 116f, -(RowHeight - 22f) * 0.5f, 52f, 22f);
            Text yes = UiChrome.AddText(view.ConfirmYes.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.OnAccentSolid, bold: true);
            UiChrome.Stretch(yes.rectTransform);
            yes.text = "삭제";

            view.ConfirmNo = UiChrome.AddSurface(view.Confirm, "No", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(view.ConfirmNo.rectTransform, RowWidth - 60f, -(RowHeight - 22f) * 0.5f, 52f, 22f);
            UiChrome.AddOutline(view.ConfirmNo.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text no = UiChrome.AddText(view.ConfirmNo.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(no.rectTransform);
            no.text = "취소";

            view.Confirm.gameObject.SetActive(false);

            int rowIndex = index;
            Wire(view.Surface, "row" + index, () => OnRowClicked(rowIndex));
            Wire(view.DeleteButton, "rowDelete" + index, () => OnDeleteClicked(rowIndex));
            Wire(view.ConfirmYes, "rowYes" + index, () => OnConfirmDelete(rowIndex));
            Wire(view.ConfirmNo, "rowNo" + index, CancelDeleteConfirm);
            return view;
        }

        private Image BuildRail(RectTransform content, string name, string glyph, float y, System.Action action)
        {
            Image surface = UiChrome.AddSurface(content, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(surface.rectTransform, ContentWidth - RailWidth, y, RailWidth, RowHeight);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Glyph", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;
            Wire(surface, name, action);
            return surface;
        }

        private InputField CreateInputField(Transform parent)
        {
            Image surface = UiChrome.AddSurface(parent, "TodoInput", UiChrome.CardSurface, UiChrome.RadiusChip);
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            Text text = UiChrome.AddText(surface.rectTransform, "Text", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            text.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            text.supportRichText = false;

            Text placeholder = UiChrome.AddText(surface.rectTransform, "Placeholder", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextTertiary);
            UiChrome.Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(UiChrome.Space3, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-UiChrome.Space3, 0f);
            placeholder.text = "할일을 적어보세요";

            var input = surface.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = InputCharacterLimit;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        // ==================== 동작 ====================

        private void OnInputSubmitted(string _)
        {
            // Enter로 확정된 경우에만 추가한다 — 포커스를 잃은 것만으로 멋대로 추가하면
            // "적다 만 것"이 목록에 남는다.
            if (!Input.GetKey(KeyCode.Return) && !Input.GetKey(KeyCode.KeypadEnter)) return;
            if (TryClaimAction("addEnter")) AddFromInput();
        }

        private void AddFromInput()
        {
            if (_input == null) return;
            string text = _input.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            int softCap = Config != null ? Config.todoActiveCountSoftCap : 15;
            _softCapHit = TodoListModel.Add(text, softCap);
            _input.text = string.Empty;
            _tab = Tab.Active;
            _scroll = 0;
            Debug.Log($"[할일패널] 추가 — \"{text.Trim()}\" (미완료 {TodoListModel.UncompletedCount}건" +
                $"{(_softCapHit ? $", 소프트캡 {softCap}건 초과" : string.Empty)}). 저장 스키마 v4에 남습니다.");
            CharacterSaveStore.Save();   // 사용자가 적은 것은 즉시 남긴다(주기 저장만 믿지 않는다).
            RefreshContent();
        }

        private void SelectTab(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            _scroll = 0;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void Scroll(int delta)
        {
            int max = Mathf.Max(0, CurrentSource().Count - VisibleRows);
            int next = Mathf.Clamp(_scroll + delta, 0, max);
            if (next == _scroll) return;
            _scroll = next;
            CancelDeleteConfirm();
            RefreshContent();
        }

        private void OnRowClicked(int rowIndex)
        {
            if (_tab == Tab.Archive) return;                 // 완료함은 읽기 전용.
            if (_confirmingId >= 0) return;                   // 확인 중에는 행 클릭을 먹지 않는다.
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0) return;

            TodoListModel.ToggleComplete(row.BoundId);
            CharacterSaveStore.Save();
            RefreshContent();
        }

        private void OnDeleteClicked(int rowIndex)
        {
            if (_tab == Tab.Archive) return;
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0) return;
            _confirmingId = row.BoundId;
            _confirmTimer = 0f;
            RefreshContent();
        }

        private void OnConfirmDelete(int rowIndex)
        {
            RowView row = _rows[rowIndex];
            if (row.BoundId < 0 || row.BoundId != _confirmingId) return;
            TodoListModel.Remove(row.BoundId);
            _confirmingId = -1;
            CharacterSaveStore.Save();
            Debug.Log("[할일패널] 삭제 확정 — 되돌리기가 아니라 인라인 확인을 쓰는 이유는 모델에 복구 API가 없어서다(32-6).");
            RefreshContent();
        }

        private void CancelDeleteConfirm()
        {
            if (_confirmingId < 0) return;
            _confirmingId = -1;
            RefreshContent();
        }

        protected override void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            base.Update();
            if (!IsOpen || _confirmingId < 0) return;

            _confirmTimer += Time.unscaledDeltaTime;
            if (_confirmTimer < DeleteConfirmSeconds) return;
            CancelDeleteConfirm();
        }

        // ==================== 갱신 ====================

        private IReadOnlyList<TodoItem> CurrentSource()
            => _tab == Tab.Active ? TodoListModel.ActiveItems : TodoListModel.CompletedArchive;

        protected override void RefreshContent()
        {
            for (int i = 0; i < 2; i++)
            {
                bool on = (int)_tab == i;
                _tabChips[i].color = on ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface) : UiChrome.CardSurface;
                _tabLabels[i].color = on ? UiChrome.TextOnAccent : UiChrome.TextSecondary;
                _tabLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }

            IReadOnlyList<TodoItem> source = CurrentSource();
            _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, source.Count - VisibleRows));

            _page.Clear();
            for (int i = _scroll; i < source.Count && _page.Count < VisibleRows; i++) _page.Add(source[i]);

            for (int i = 0; i < VisibleRows; i++)
            {
                RowView row = _rows[i];
                bool used = i < _page.Count;
                if (row.Rect.gameObject.activeSelf != used) row.Rect.gameObject.SetActive(used);
                if (!used) { row.BoundId = -1; continue; }

                TodoItem item = _page[i];
                row.BoundId = item.Id;

                // ★ 2026-09-08 R6-0-2(가) — 글자 수가 아니라 <b>실제 폭</b>으로 자른다.
                //   내용이 실제로 바뀐 순간에만 부른다: UiChrome.Ellipsize는 폭을 재려고
                //   Text.text를 log2(길이)번 바꾸므로(그 함수의 «호출부 규약») 상주 앱의
                //   갱신 경로에서 매번 부르면 잘라 낸 문자열이 계속 새로 할당된다.
                //   이 패널의 RefreshContent는 TodoListChanged/사용자 조작에서만 도는데,
                //   포스트잇의 0.5초 SweepCompleted가 그 이벤트를 낼 수 있어 상시 경로에 가깝다.
                bool labelChanged = row.LastCompleted != item.Completed
                    || !string.Equals(row.LastLabelSource, item.Text, System.StringComparison.Ordinal);
                if (labelChanged)
                {
                    row.LastLabelSource = item.Text;
                    row.LastCompleted = item.Completed;
                    row.Label.text = UiChrome.Ellipsize(row.Label, item.Text, RowLabelWidth);
                    ApplyStrikethrough(row);
                }
                row.Label.color = item.Completed
                    ? new Color(UiChrome.TextPrimary.r, UiChrome.TextPrimary.g, UiChrome.TextPrimary.b, 0.5f)
                    : UiChrome.TextPrimary;
                if (row.BoxCheck.gameObject.activeSelf != item.Completed)
                    row.BoxCheck.gameObject.SetActive(item.Completed);

                bool archive = _tab == Tab.Archive;
                if (row.DeleteButton.gameObject.activeSelf == archive)
                    row.DeleteButton.gameObject.SetActive(!archive);

                bool confirming = item.Id == _confirmingId;
                if (row.Confirm.gameObject.activeSelf != confirming) row.Confirm.gameObject.SetActive(confirming);
            }

            bool empty = source.Count == 0;
            if (_emptyTitle.gameObject.activeSelf != empty) _emptyTitle.gameObject.SetActive(empty);
            if (_emptyBody.gameObject.activeSelf != empty) _emptyBody.gameObject.SetActive(empty);
            _emptyTitle.text = _tab == Tab.Active ? "아직 비어 있어요" : "완료한 일이 아직 없어요";
            _emptyBody.text = _tab == Tab.Active ? "위에 적어두면 제가 가끔 챙겨줄게요." : "체크한 일이 여기 모입니다.";

            bool needRail = source.Count > VisibleRows;
            if (_railUp.gameObject.activeSelf != needRail) _railUp.gameObject.SetActive(needRail);
            if (_railDown.gameObject.activeSelf != needRail) _railDown.gameObject.SetActive(needRail);

            bool showSoftCap = _softCapHit && _tab == Tab.Active;
            if (_softCapNotice.gameObject.activeSelf != showSoftCap) _softCapNotice.gameObject.SetActive(showSoftCap);

            // ★ 이제 목록은 저장 스키마 v4로 파일에 남는다 — "앱을 끄면 사라져요"는 더 이상 사실이 아니다.
            _footer.text = _tab == Tab.Active
                ? $"완료함 {TodoListModel.CompletedArchive.Count}건 · 앱을 껐다 켜도 남아요"
                : "완료한 일은 지우지 않고 모아둬요.";
        }

        /// <summary>완료 항목에 취소선을 긋는다. 레거시 uGUI <see cref="Text"/>에는
        /// <c>&lt;s&gt;</c>가 없어서(리치텍스트는 b/i/size/color뿐) 글자 폭을 재서 1pt 선을 직접 놓는다.
        ///
        /// <para>★ 2026-09-08 — <see cref="TodoPostItWidget"/>의 같은 이름 메서드가 <b>정본</b>이고
        /// 이것은 그 이식본이다. 여기 있던 U+0336 결합문자 조립은 <b>폰트 폴백에 기대는 방식</b>이라
        /// Windows에서 두부가 될지 미확인이었다(docs/UX_WIDGETS.md R6-0-2(나)·R6-11 #3).
        /// R6에서 완료 행이 그 날짜에 <b>영구히</b> 남게 되면 그 위험이 «2.5초»에서 «영구»로 승격된다.</para>
        ///
        /// <para>폭 측정은 <see cref="UiChrome.Ellipsize"/> 직후, 즉 <b>내용이 바뀐 순간에만</b> 한다.
        /// 상자 폭으로 한 번 더 클램프하는 것은 방어다 — 말줄임이 어떤 이유로 통과되더라도
        /// 선만은 상자를 넘지 않는다.</para></summary>
        private static void ApplyStrikethrough(RowView row)
        {
            if (row.Strike == null) return;

            bool on = row.LastCompleted;
            if (row.Strike.gameObject.activeSelf != on) row.Strike.gameObject.SetActive(on);
            if (!on) return;

            float width = Mathf.Min(row.Label.preferredWidth, row.Label.rectTransform.rect.width);
            row.Strike.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), 1f);
        }

        // ==================== 전역 폴링 경로 ====================

        protected override void OnGlobalClick(Vector2 cursor)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!ContainsScreenPoint(_tabChips[i].rectTransform, cursor)) continue;
                if (TryClaimAction("tab" + i)) SelectTab((Tab)i);
                return;
            }

            if (ContainsScreenPoint(_addSurface.rectTransform, cursor))
            {
                if (TryClaimAction("add")) AddFromInput();
                return;
            }
            if (_railUp.gameObject.activeSelf && ContainsScreenPoint(_railUp.rectTransform, cursor))
            {
                if (TryClaimAction("RailUp")) Scroll(-1);
                return;
            }
            if (_railDown.gameObject.activeSelf && ContainsScreenPoint(_railDown.rectTransform, cursor))
            {
                if (TryClaimAction("RailDown")) Scroll(1);
                return;
            }

            for (int i = 0; i < VisibleRows; i++)
            {
                RowView row = _rows[i];
                if (row.BoundId < 0 || !ContainsScreenPoint(row.Rect, cursor)) continue;

                if (row.Confirm.gameObject.activeSelf)
                {
                    if (ContainsScreenPoint(row.ConfirmYes.rectTransform, cursor))
                    {
                        if (TryClaimAction("rowYes" + i)) OnConfirmDelete(i);
                        return;
                    }
                    if (ContainsScreenPoint(row.ConfirmNo.rectTransform, cursor))
                    {
                        if (TryClaimAction("rowNo" + i)) CancelDeleteConfirm();
                    }
                    return;
                }

                if (row.DeleteButton.gameObject.activeSelf && ContainsScreenPoint(row.DeleteButton.rectTransform, cursor))
                {
                    if (TryClaimAction("rowDelete" + i)) OnDeleteClicked(i);
                    return;
                }
                if (TryClaimAction("row" + i)) OnRowClicked(i);
                return;
            }
        }

        // ==================== 테스트 진입점 ====================

        /// <summary>테스트/진단 전용 — 입력칸에 글자를 넣고 [추가]와 같은 경로로 확정한다.</summary>
        public void AddForTests(string text)
        {
            if (_input != null) _input.text = text;
            AddFromInput();
        }

        /// <summary>화면에 실제로 보이는 행의 개수(빈 행 제외).</summary>
        public int VisibleRowCount => _page.Count;

        /// <summary>i번째로 보이는 행이 물고 있는 항목 Id(-1 = 없음).</summary>
        public int RowItemId(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null ? _rows[index].BoundId : -1;

        /// <summary>i번째 행 사각형(Unity 스크린 픽셀) — 테스트가 실제 클릭 경로로 누른다.</summary>
        public Rect RowScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].Rect)
                : new Rect();

        /// <summary>i번째 행의 삭제 [✕] 사각형.</summary>
        public Rect RowDeleteScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].DeleteButton.rectTransform)
                : new Rect();

        /// <summary>삭제 확인의 [삭제] 사각형.</summary>
        public Rect RowConfirmYesScreenRect(int index)
            => index >= 0 && index < VisibleRows && _rows[index] != null
                ? ScreenRectOf(_rows[index].ConfirmYes.rectTransform)
                : new Rect();

        private RowView RowOrNull(int index)
            => index >= 0 && index < VisibleRows ? _rows[index] : null;

        /// <summary>i번째 행에 <b>실제로 그려진</b> 라벨 문자열(말줄임 적용 후) — 테스트/진단 전용.
        /// 원본이 아니라 화면에 있는 것을 돌려준다: 둘이 갈라지는 것이 이 결함의 본체였다.</summary>
        public string RowLabelText(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? row.Label.text : null;
        }

        /// <summary>i번째 행 라벨의 <b>실측 잉크 폭</b>(pt). 폰트가 직접 잰 값이다
        /// (<see cref="Text.preferredWidth"/>) — 글자 수 모형이 아니다.</summary>
        public float RowLabelInkWidth(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? row.Label.preferredWidth : 0f;
        }

        /// <summary>i번째 행 라벨 <b>상자</b>의 화면 사각형.</summary>
        public Rect RowLabelScreenRect(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Label != null ? ScreenRectOf(row.Label.rectTransform) : new Rect();
        }

        /// <summary>i번째 행의 취소선이 켜져 있는가.</summary>
        public bool RowStrikeActive(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null && row.Strike.gameObject.activeSelf;
        }

        /// <summary>i번째 행 취소선의 <b>로컬</b> 크기(pt) — 폭·두께를 그대로 잰다.</summary>
        public Vector2 RowStrikeSizePoints(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null ? row.Strike.rectTransform.sizeDelta : Vector2.zero;
        }

        /// <summary>i번째 행 취소선의 화면 사각형.</summary>
        public Rect RowStrikeScreenRect(int index)
        {
            RowView row = RowOrNull(index);
            return row != null && row.Strike != null ? ScreenRectOf(row.Strike.rectTransform) : new Rect();
        }
    }
}
