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

        private const float ListTop = -46f;
        private const float ListHeight = VisibleRows * RowHeight + (VisibleRows - 1) * RowGap;  // 218.

        /// <summary>삭제 확인이 열려 있는 시간. 지나면 조용히 취소된다.</summary>
        private const float DeleteConfirmSeconds = 3f;

        private enum Tab { Active = 0, Archive = 1 }

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Surface;
            public Image Box;          // 체크박스.
            public Image BoxCheck;
            public Text Label;
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
                UiChrome.PlaceTopLeft(chip.rectTransform,
                    Width - UiChrome.Space4 - 22f - UiChrome.Space1 - (2 - i) * 46f - (1 - i) * 2f,
                    -UiChrome.Space3, 46f, 22f);
                UiChrome.AddOutline(chip.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
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
            UiChrome.AddOutline(view.Rect, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            view.Box = UiChrome.AddSurface(view.Rect, "Box", UiChrome.SubtleSurface, 4);
            UiChrome.PlaceTopLeft(view.Box.rectTransform, 10f, -(RowHeight - 20f) * 0.5f, 20f, 20f);
            UiChrome.AddOutline(view.Box.rectTransform, "Outline", UiChrome.CardBorder, 4);
            view.BoxCheck = UiChrome.AddCircle(view.Box.rectTransform, "Dot", 10f, UiChrome.Accent);
            view.BoxCheck.gameObject.SetActive(false);

            view.Label = UiChrome.AddText(view.Rect, "Label", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            UiChrome.PlaceTopLeft(view.Label.rectTransform, 38f, -(RowHeight - 18f) * 0.5f, 162f, 18f);

            view.DeleteButton = UiChrome.AddSurface(view.Rect, "Delete", new Color(0f, 0f, 0f, 0f), UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(view.DeleteButton.rectTransform, RowWidth - 32f, -(RowHeight - 22f) * 0.5f, 22f, 22f);
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
            UiChrome.AddOutline(view.ConfirmNo.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
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
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);
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
            UiChrome.AddOutline(surface.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

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
            input.characterLimit = 60;
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
                _tabChips[i].color = on ? UiChrome.AccentSurface : UiChrome.CardSurface;
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
                row.Label.text = item.Completed ? Strikethrough(item.Text) : item.Text;
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

        /// <summary>이 프로젝트에는 TextMeshPro가 없고 레거시 Text에는 취소선 스타일이 없다 —
        /// 유니코드 결합 취소선(U+0336)으로 같은 그림을 만든다.</summary>
        private static string Strikethrough(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new System.Text.StringBuilder(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);
                sb.Append('̶');
            }
            return sb.ToString();
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
    }
}
