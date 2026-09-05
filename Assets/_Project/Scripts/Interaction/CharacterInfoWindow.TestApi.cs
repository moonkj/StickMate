using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 진단/테스트 전용 관측 창구. <b>여기에는 화면을 바꾸는 코드가 없다</b> — 전부 읽기다.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나눈 조각이다.
    /// <b>분할은 줄 단위로 그대로 옮겼다</b>(옮기기 전후로 코드 줄 집합이 동일함을 확인).
    /// 그 뒤 같은 라운드에서 탭 판정만 <see cref="CharacterInfoWindow.TabTable"/> 기반으로 바꿨다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        /// <summary>
        /// 테스트 전용 진입점 — 실제 입력과 <b>같은 처리 경로</b>에 커서를 먹인다(PlayMode는 진짜
        /// 전역 클릭을 만들 수 없다 — PopoverPanel.FeedClickForTests와 같은 사정).
        /// </summary>
        public void FeedClickForTests(Vector2 cursorUnityScreen)
        {
            if (_open) FeedClick(cursorUnityScreen);
        }

        /// <summary>테스트 전용 — 버튼 상태와 커서를 <b>실제 입력과 같은 처리 경로</b>에 먹인다
        /// (드래그는 누름/이동/뗌의 연속이라 단발 클릭 진입점으로는 재현할 수 없다).</summary>
        public void FeedPointerForTests(bool buttonDown, Vector2 cursorUnityScreen)
        {
            if (_open) ProcessPointer(buttonDown, cursorUnityScreen, hasCursor: true);
        }

        /// <summary>진단/테스트 전용 — 창의 현재 위치(화면 중앙 원점, 캔버스 포인트).</summary>
        public Vector2 PanelOffsetPoints => _panel != null ? _panel.anchoredPosition : Vector2.zero;

        /// <summary>진단/테스트 전용 — 창의 현재 크기(캔버스 포인트).</summary>
        public Vector2 PanelSizePoints => _panel != null ? _panel.sizeDelta : Vector2.zero;

        /// <summary>진단/테스트 전용 — 지금 타이틀바를 잡고 끌고 있는가.</summary>
        public bool IsDraggingPanel => _draggingPanel;

        /// <summary>진단/테스트 전용 — 드래그 손잡이(<b>헤더</b>)의 화면 사각형.
        /// <para>★ L-2로 타이틀바가 헤더에 흡수됐다. 실제로 끌리는 자리는 여기서 탭·칩·[✕]를 뺀
        /// 나머지이고, 그 판정은 <see cref="TryBeginPanelDrag"/> 한 곳에 있다.</para></summary>
        public Rect TitleBarScreenRect => RawScreenRectOf(_titleBarRect);

        /// <summary>진단/테스트 전용 — 헤더 안에서 <b>드래그가 시작되지 않는</b> 자식들의 사각형.
        /// 테스트가 "빈 자리"를 찾을 때 이 목록을 피해서 고른다(좌표를 손으로 적지 않는다).</summary>
        public Rect[] HeaderNonDragRectsForTests()
        {
            var rects = new System.Collections.Generic.List<Rect>(_tabRects.Length + 4);
            for (int i = 0; i < _tabRects.Length; i++) rects.Add(RawScreenRectOf(_tabRects[i]));
            rects.Add(RawScreenRectOf(_closeRect));
            rects.Add(RawScreenRectOf(_settingsRect));
            rects.Add(RawScreenRectOf(_ownedChipRect));
            rects.Add(RawScreenRectOf(_coinChipRect));
            return rects.ToArray();
        }

        /// <summary>진단/테스트 전용 — 창 전체의 화면 사각형("화면 안에 들어왔는가"를 재는 창구).</summary>
        public Rect PanelScreenRect => RawScreenRectOf(_panel);

        /// <summary>헤더의 [설정] 칩 화면 사각형 — 설정창의 주 진입점이자, 설정창을 닫았을 때
        /// 이 창으로 <b>돌아오는지</b>를 검증하는 테스트가 실제로 누를 자리다(M8).</summary>
        public Rect SettingsChipScreenRect => RawScreenRectOf(_settingsRect);

        /// <summary>[✕] 버튼의 화면 사각형. ★ 2026-09-02부터 창 밖 클릭이 닫지 않으므로 <b>이 앱에서
        /// 이 창을 닫는 유일한 마우스 경로</b>다(Esc/Cmd+W는 포커스 없는 오버레이라 못 받는다 —
        /// <see cref="UiChrome"/> "창을 닫는 법" 절). 그래서 테스트가 좌표를 손으로 적지 않고
        /// 반드시 이 자리를 눌러 본다.</summary>
        public Rect CloseButtonScreenRect => RawScreenRectOf(_closeRect);

        // ==================== 진단/테스트 전용 — 카드 캐러셀 ====================
        //
        // 좌표를 테스트가 손으로 적으면 레이아웃이 바뀔 때 엉뚱한 곳을 누르게 된다([착용] 버튼 쪽에서
        // 이미 배운 것). 그래서 <b>지금 화면에 있는 사각형</b>을 그대로 내준다.

        /// <summary>지금 존재하는 카드 수(탭과 무관한 <b>풀</b> 크기).</summary>
        public int CardCountForTests => _cards.Length;

        /// <summary>이 카드가 지금 탭에서 실제로 쓰이고 있는가(카테고리마다 개수가 다르다).</summary>
        public bool IsCardVisibleForTests(int index)
        {
            ItemCard card = CardAt(index);
            return card != null && card.Rect != null && card.Rect.gameObject.activeInHierarchy;
        }

        public int CardSectionForTests(int index) => CardAt(index)?.Section ?? -1;

        public int CardItemForTests(int index) => CardAt(index)?.Item ?? -1;

        /// <summary>그 카드가 가리키는 슬롯. 섹션→슬롯 규칙(<see cref="SectionSlot"/>)을 테스트가
        /// <b>베껴 적지 않게</b> 하는 창구다 — 카테고리를 더하거나 지우면 그 규칙만 바뀌어야 한다.
        /// 카드가 없으면 false.</summary>
        public bool TryGetCardSlotForTests(int index, out EquipmentSlot slot)
        {
            ItemCard card = CardAt(index);
            bool cards = Def(_tab).Page == TabPage.Cards;
            slot = card != null && cards ? SectionSlot(_tab, card.Section) : default;
            return card != null && cards;
        }

        /// <summary>카드의 <b>잘리기 전</b> 화면 사각형. 스크롤 밖으로 밀려난 카드도 값이 나온다 —
        /// "보이지 않는데 눌리는가"를 재려면 그 자리를 알아야 한다.</summary>
        public Rect CardRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드가 스크롤 마스크에 <b>잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).</summary>
        public Rect CardVisibleScreenRect(int index) => VisibleScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드 하단 [착용]/[해제] 버튼의 잘리기 전 화면 사각형.</summary>
        public Rect CardEquipButtonRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.ActionRect);

        /// <summary>그 카드의 [착용] 버튼이 지금 화면에 보이는 넓이 비율(0 = 통째로 잘림).
        /// <para>★ 2026-09-01 — 상세 패널 버튼을 걷어내면서 <c>ActionButtonVisibleFraction</c>이 갈 곳을
        /// 잃었다. "보이지 않는 것은 눌리지 않는다"(R2 M3)는 그 버튼의 성질이 아니라 <b>이 창의 규칙</b>이라,
        /// 살아남은 버튼 쪽으로 관측 창구를 옮겨 회귀를 그대로 유지한다.</para></summary>
        public float CardEquipButtonVisibleFraction(int index)
        {
            RectTransform rt = CardAt(index)?.ActionRect;
            if (rt == null || !rt.gameObject.activeInHierarchy) return 0f;
            rt.GetWorldCorners(_corners);
            float full = (_corners[2].x - _corners[0].x) * (_corners[2].y - _corners[0].y);
            if (full <= 0f) return 0f;
            Rect visible = VisibleScreenRectOf(rt);   // _corners를 다시 쓰므로 full을 먼저 잰다.
            return Mathf.Clamp01(visible.width * visible.height / full);
        }

        /// <summary>지금 이 지점을 누르면 그 카드의 [착용] 버튼이 반응하는가(마스크까지 본 판정).</summary>
        public bool IsCardEquipButtonHittableAt(int index, Vector2 cursorUnityScreen)
            => ContainsScreenPoint(CardAt(index)?.ActionRect, cursorUnityScreen);

        // ---- P0-4 / P0-5 회귀용 관측 창구 ----

        /// <summary>카드 하단 버튼의 <b>표면색</b>. P0-4 회귀가 "카드 버튼이 화면에서 가장 밝은 면이
        /// 아니다"를 이 값으로 확인한다.</summary>
        public Color CardActionSurfaceColor(int index) => CardAt(index)?.ActionSurface?.color ?? Color.clear;

        /// <summary>카드 하단 버튼의 <b>라벨색</b>. 조용해진 표면 위에서도 읽히는지 확인한다.</summary>
        public Color CardActionLabelColor(int index) => CardAt(index)?.ActionLabel?.color ?? Color.clear;

        /// <summary>상세 패널 안에 살아 있는 <see cref="Button"/> 수. 회귀 테스트가 "걷어낸 중복 착용
        /// 버튼이 되살아나지 않았다"를 <b>색이나 라벨이 아니라 존재 여부</b>로 확인하는 창구다.
        /// 패널을 못 찾으면 −1(관측 전제 자체가 깨진 것과 0을 구별한다).
        /// <para>진단/테스트 전용 — <c>GetComponentsInChildren</c>은 할당하므로 매 프레임 경로에서
        /// 부르지 않는다(상주 앱 규약).</para></summary>
        public int DetailPanelButtonCountForTests
            => _sectionDetailRect != null ? _sectionDetailRect.GetComponentsInChildren<Button>(true).Length : -1;

        /// <summary>상세 패널이 지금 말하고 있는 이름 — 잠긴 아이템이면 <c>???</c>.</summary>
        public string DetailNameTextForTests => _detailName != null ? _detailName.text : null;

        /// <summary>상세 패널 메타 줄(<c>카테고리 · 착용 중|보유 중|Lv.n에 열림</c>).</summary>
        public string DetailMetaTextForTests => _detailMeta != null ? _detailMeta.text : null;

        /// <summary>상세 패널 설명문 — 잠긴 아이템이면 <b>왜 잠겼는지</b>가 여기에만 있다.</summary>
        public string DetailBodyTextForTests => _detailBody != null ? _detailBody.text : null;

        /// <summary>화면 픽셀 ÷ 이 값 = 캔버스 포인트. 테스트가 화면 사각형을 pt로 되돌릴 때 쓴다.</summary>
        public float CanvasScaleForTests => CanvasScale();

        /// <summary>카드 한 장의 설계 크기(pt). 인계본 설계값 186.00 × 208을 테스트가 <b>베끼지 않고</b>
        /// 묻는 통로다(비율·정렬은 여기서 나온 값끼리 비교한다).</summary>
        public Vector2 CardDesignSizePoints => new Vector2(CardWidth, CardHeight);

        /// <summary>카드 격자의 열 수(설계값 2). 컬럼 폭이 바뀌면 이 값이 따라와야 한다.</summary>
        public int CardGridColumns => CardColumns;

        /// <summary>3컬럼의 설계 폭(pt) — 306 / 292 / 444.</summary>
        public Vector3 ColumnWidthsPoints => new Vector3(Col1Width, Col2Width, Col3Width);

        /// <summary>컬럼 3 격자가 <b>세로로</b> 밀 수 있는 최대치(양수). 0이면 다 들어온다는 뜻이다.</summary>
        public float GridMaxScrollPoints => MaxGridScroll();

        /// <summary>지금 밀려 있는 양(캔버스 포인트, 아래로 밀면 양수).</summary>
        public float GridScrollPoints => _gridContent != null ? _gridContent.anchoredPosition.y : 0f;

        /// <summary>컬럼 3 스크롤 뷰포트의 화면 사각형 — 테스트가 드래그를 걸 자리를 여기서 고른다.</summary>
        public Rect GridViewportScreenRect => RawScreenRectOf(_gridViewport);

        /// <summary>컬럼 3 스크롤 콘텐츠의 총 높이(pt). 카테고리 블록 합이다.</summary>
        public float GridContentHeightPoints => _gridContent != null ? _gridContent.rect.height : 0f;

        /// <summary>카드 이름 상자 / 등급 낱말 상자의 화면 사각형(잘리기 전).</summary>
        public Rect CardNameRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Name?.rectTransform);

        public Rect CardMetaRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Rarity?.rectTransform);

        /// <summary>카드 등급 낱말 — 「낱말이 없으면 등급 표시는 미완이다」가 카드에 도달했는지 재는 통로.</summary>
        public string CardRarityWordForTests(int index) => CardAt(index)?.Rarity?.text ?? string.Empty;

        /// <summary>카드 이름이 <b>실제로 그려질 때</b> 차지하는 폭(캔버스 포인트). 상자 폭이 아니라
        /// 폰트가 잰 값이라, 말줄임이 안 걸리면 상자를 넘는 것이 이 값에서 바로 보인다.</summary>
        public float CardNameInkWidthPoints(int index)
        {
            Text t = CardAt(index)?.Name;
            return t != null ? t.preferredWidth : 0f;
        }

        /// <summary>카드에 지금 표시된 이름(말줄임이 걸렸으면 잘린 쪽).</summary>
        public string CardNameTextForTests(int index) => CardAt(index)?.Name?.text ?? string.Empty;

        /// <summary>말줄임 전 원본 이름.</summary>
        public string CardNameSourceForTests(int index) => CardAt(index)?.NameSource ?? string.Empty;

        /// <summary>카테고리 블록(제목줄 + 카드 격자)의 화면 사각형.</summary>
        public Rect CategoryBlockScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.Rect : null);

        /// <summary>카테고리 블록이 <b>마스크에 잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).
        ///
        /// <para>★ 왜 날 사각형과 따로 필요한가(2026-09-02 실측): 배치모드 PlayMode의 화면은
        /// 640×480이라 <see cref="ClampPanelToScreen"/>이 이 창을 608pt로 줄이는데, <b>내용은 함께
        /// 접히지 않는다</b> — 블록은 1042 폭 기준 자리에 그대로 있고 <c>Body</c> 마스크가 자른다.
        /// 그 자리는 이 창의 규칙("보이지 않는 것은 눌리지 않는다")에 따라 <b>정당하게</b> 잡히지 않는다.</para>
        ///
        /// <para>테스트가 드래그를 걸 지점은 여기서 고른다. 날 사각형의 중심을 잡으면 제품이 멀쩡한데도
        /// 화면 크기 때문에 빨개지는 <b>거짓 빨강</b>이 난다.</para></summary>
        public Rect CategoryBlockVisibleScreenRect(int section)
            => VisibleScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.Rect : null);

        /// <summary>카테고리 헤더의 "n / 6" 카운터 사각형. 이 창 오른쪽 열의 <b>오른쪽 끝선</b>을 정의하는
        /// 요소이고, 카드 격자 바로 위에 있다 — 회귀 테스트가 그 끝선을 숫자로 베끼지 않고 물어보는 통로.</summary>
        public Rect SectionCountScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length
                ? _sections[section]?.Count?.rectTransform : null);

        // ==================== 진단/테스트 전용 — 컬럼 1 착용 슬롯 ====================

        /// <summary>착용 슬롯 행의 화면 사각형(잘리기 전). 꺼져 있으면 넓이 0.</summary>
        public Rect SlotRowRawScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _slotRows.Length ? _slotRows[index]?.Rect : null);

        /// <summary>그 슬롯 행이 지금 적고 있는 아이템 이름(비어 있으면 "비어 있음").</summary>
        public string SlotRowNameForTests(int index)
            => index >= 0 && index < _slotRows.Length && _slotRows[index]?.Name != null
                ? _slotRows[index].Name.text : null;

        /// <summary>프리뷰 무대(액자)의 화면 사각형 — 액자가 눌리지 않았는지 재는 통로다.</summary>
        public Rect PortraitStageScreenRect
            => RawScreenRectOf(_portraitFrame != null ? _portraitFrame.rectTransform : null);

        // ==================== 진단/테스트 전용 — 컬럼 2 능력치 / 테마 세트 (2026-09-05) ====================

        /// <summary>스탯 카드가 <b>지금 화면에 쓰고 있는</b> 총합 문자열(<c>16</c>). 없으면 null.</summary>
        public string StatCardTotalForTests(int index)
            => index >= 0 && index < _statCards.Length && _statCards[index]?.Total != null
                ? _statCards[index].Total.text : null;

        /// <summary>같은 카드의 장비 기여(<c>(+8)</c>). 0 이하면 빈 문자열이다.</summary>
        public string StatCardBonusForTests(int index)
            => index >= 0 && index < _statCards.Length && _statCards[index]?.Bonus != null
                ? _statCards[index].Bonus.text : null;

        /// <summary>같은 카드의 단계 낱말(<c>초급</c> / <c>임계 미달</c>).</summary>
        public string StatCardTierForTests(int index)
            => index >= 0 && index < _statCards.Length && _statCards[index]?.ChipLabel != null
                ? _statCards[index].ChipLabel.text : null;

        /// <summary>같은 카드의 「중급까지 N」 / 「최고 단계」.</summary>
        public string StatCardRemainForTests(int index)
            => index >= 0 && index < _statCards.Length && _statCards[index]?.Remain != null
                ? _statCards[index].Remain.text : null;

        /// <summary>섹션 제목 오른쪽 「장비 합 +24」.</summary>
        public string EquipmentSumTextForTests => _statSumValue != null ? _statSumValue.text : null;

        /// <summary>세트 패널이 <b>지금 어느 얼굴인가</b> — 진행도 두 줄이면 true, 준비 중 문구면 false.</summary>
        public bool SetPanelShowsProgressForTests
            => _setReadyRoot != null && _setReadyRoot.activeSelf;

        /// <summary>세트 패널 1행(<c>오피스 워커 3/4</c>). 준비 중이면 null.</summary>
        public string SetProgressTextForTests
            => SetPanelShowsProgressForTests && _setProgress != null ? _setProgress.text : null;

        /// <summary>컬럼 2 스크롤 콘텐츠의 실제 높이(pt) — 세로 예산이 <b>실기에서</b> 얼마인지 재는 창구.
        /// 설계 폭에서 이 값이 <see cref="Column2ViewportHeightForTests"/> 이하면 스크롤이 없다.</summary>
        public float Column2ContentHeightForTests
            => _col2Content != null ? _col2Content.rect.height : 0f;

        /// <summary>컬럼 2 뷰포트 높이(pt).</summary>
        public float Column2ViewportHeightForTests
            => _col2Viewport != null ? _col2Viewport.rect.height : 0f;

        /// <summary>컬럼 2가 밀려날 수 있는 최대치(pt). 설계 폭에서는 0이어야 한다.</summary>
        public float Column2MaxScrollForTests => MaxCol2Scroll();

        // ==================== P0-1 회귀용 관측 창구 ====================

        // ==================== 진단/테스트 전용 — 프레즌스 줄 / 보관함 레일 (2026-09-02) ====================

        /// <summary>프레즌스 줄이 <b>지금 화면에 쓰고 있는</b> 문자열. hold 회귀가 이 값의 변화 횟수를 센다.</summary>
        public string PresenceTextForTests => _presenceText != null ? _presenceText.text : null;

        /// <summary>이 창이 쓰는 초상화 촬영장. "액자에 상태가 도달하지 않는다"를 재는 창구다 —
        /// 테스트가 씬 전체를 뒤져 촬영장 두 개(정보창/호버 패널) 중 어느 쪽인지 헷갈릴 일이 없다.</summary>
        public CharacterPortraitStage PortraitStageForTests => _stage;

        /// <summary>보관함 페이지 지시자 문자열.</summary>
        public string PageIndicatorTextForTests => _pageIndicator != null ? _pageIndicator.text : null;

        /// <summary>지시자가 <b>실제로 그려질 때</b> 차지하는 폭(캔버스 포인트) — 폰트가 잰 값이다.
        /// 설계가 Arial advance 0.556em을 가정해 19.46pt로 계산했는데, 그 가정을 여기서 <b>실제 폰트로</b>
        /// 확인한다(레일 폭 <see cref="InventoryRailWidthPoints"/>를 넘으면 그때가 진짜 줄바꿈 문제다).</summary>
        public float PageIndicatorInkWidthPoints => _pageIndicator != null ? _pageIndicator.preferredWidth : 0f;

        /// <summary>지시자 상자의 화면 사각형(잘리기 전). "허공에 뜨지 않았는가"를 [▲]와의 거리로 잰다.</summary>
        public Rect PageIndicatorRawScreenRect
            => RawScreenRectOf(_pageIndicator != null ? _pageIndicator.rectTransform : null);

        /// <summary>페이지 칩의 화면 사각형. <paramref name="direction"/>이 음수면 [▲], 양수면 [▼].</summary>
        public Rect PagerChipRawScreenRect(int direction)
            => RawScreenRectOf(direction < 0 ? _pageUpRect : _pageDownRect);

        /// <summary>페이지 칩 글리프 색 — 죽은 칩과 산 칩이 <b>실제로 다른지</b>를 재는 창구.</summary>
        public Color PagerGlyphColorForTests(int direction)
        {
            Text t = direction < 0 ? _pageUpLabel : _pageDownLabel;
            return t != null ? t.color : Color.clear;
        }

        /// <summary>페이지 칩 테두리 색.</summary>
        public Color PagerOutlineColorForTests(int direction)
        {
            Image i = direction < 0 ? _pageUpOutline : _pageDownOutline;
            return i != null ? i.color : Color.clear;
        }

        /// <summary>레일 폭(캔버스 포인트). 테스트가 24를 베껴 적지 않게 하는 창구다.</summary>
        public float InventoryRailWidthPoints => InventoryRailWidth;

        /// <summary>지금 보관함 스크롤(줄 단위)과 그 상한 — 칩의 겉모습이 <b>이 값에서</b> 나오는지 확인한다.</summary>
        public int InventoryScrollForTests => _inventoryScroll;

        public int MaxInventoryScrollForTests => MaxInventoryScroll;

        /// <summary>두 클릭 경로(<see cref="BuildPagerButton"/>의 <c>onClick</c>과 <see cref="FeedClick"/>의
        /// 폴링)가 <b>둘 다 보는</b> 그 판정. 칩의 겉모습도 여기서 나오므로, 테스트는 "겉모습 == 이 값"을
        /// 확인하는 것만으로 <b>표시-실제 일치</b>를 잠글 수 있다.</summary>
        public bool CanScrollInventoryForTests(int direction) => CanScrollInventory(direction);

        /// <summary>진단/테스트 전용 — 페이지 이동을 <b>클릭 핸들러가 부르는 바로 그 함수</b>로 부른다.
        ///
        /// <para>★ 왜 클릭 대신 이것이 필요한가(2026-09-02 실측): 배치모드 PlayMode의 화면은
        /// <b>640×480</b>이라 이 창이 608pt로 줄고, 우측 레일(패널 좌단 기준 x≈850 — 폭 1042에서는 x≈1012)이
        /// <c>Body</c> 마스크(16..624) <b>밖으로 통째로 잘린다</b>. 그래서 그 자리는
        /// <b>물리적으로 눌리지 않는다</b>("보이지 않는 것은 눌리지 않는다" 규칙이 정상 작동한 결과다).
        /// 클릭으로 검증하려 들면 테스트는 초록도 빨강도 아닌 <b>거짓 빨강</b>을 낸다.</para>
        ///
        /// <para>가드는 여기서 재현하지 않는다 — 세 번째 사본을 만들면 그것이 곧 다음 결함이다.
        /// 가드는 <see cref="CanScrollInventoryForTests"/>로 따로 확인한다.</para></summary>
        public void ScrollInventoryForTests(int direction) => ScrollInventory(direction);

        /// <summary>탭 버튼의 화면 사각형 — 테스트가 <b>실제 클릭 경로</b>로 탭을 누를 수 있게 연다
        /// (<c>_tabRects</c>를 리플렉션으로 뒤지던 관례를 대체한다).</summary>
        public Rect TabScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _tabRects.Length ? _tabRects[index] : null);

        /// <summary>지금 탭이 실제로 보여주는 카테고리 섹션 수(카드 페이지가 아니면 0).</summary>
        public int VisibleSectionCount => SectionCountForTab(_tab);

        /// <summary>이 창의 설계 높이(캔버스 포인트). ★ L-8로 <b>탭과 무관하게 고정</b>이다 —
        /// 실제 화면 높이(<see cref="PanelSizePoints"/>)는 <see cref="ClampPanelToScreen"/>이 화면에
        /// 맞춰 자른 뒤의 값이라, 화면이 낮은 실행 환경(배치모드 등)에서는 이 값에 닿지 않는다.</summary>
        public float TargetPanelHeightPoints => PanelHeight;

        /// <summary>설계 폭. 테스트가 1042를 베끼지 않게 하는 통로다.</summary>
        public float TargetPanelWidthPoints => PanelWidth;

        // ==================== 진단/테스트 전용 — 등급 리본 ====================
        //
        // ★ 여기서 <b>칸 수를 다시 계산해 주지 않는다</b>. 내주는 것은 «지금 화면의 Image 몇 개가
        //   트랙 색이 아닌가»라는 <b>관측값</b>이다. 프로덕션 함수로 기대값을 만들면 그 함수가 틀어질 때
        //   기대값도 함께 틀어져 아무것도 못 잰다(TEAM.md 「생성기와 검사기가 같이 틀린다」).

        /// <summary>카드 등급 리본의 화면 사각형(잘리기 전). 리본이 없으면 넓이 0.</summary>
        public Rect CardRarityRibbonRawScreenRect(int index) => RawScreenRectOf(RibbonAt(index)?.Root);

        /// <summary>카드 썸네일의 화면 사각형(잘리기 전). 리본이 <b>썸네일을 침범하지 않는가</b>를
        /// 테스트가 좌표를 손으로 적지 않고 재는 통로다.</summary>
        public Rect CardThumbRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Thumb?.rectTransform);

        /// <summary>지금 그 카드의 리본이 <b>보이는가</b>(카드 자체가 꺼져 있으면 false).</summary>
        public bool IsCardRarityRibbonVisibleForTests(int index)
        {
            RectTransform root = RibbonAt(index)?.Root;
            return root != null && root.gameObject.activeInHierarchy;
        }

        /// <summary>그 카드 리본에서 <b>트랙 색이 아닌</b> 칸의 수 = 화면이 실제로 말하고 있는 칸 수.
        /// 리본이 없으면 −1(관측 전제가 깨진 것과 "0칸"을 구별한다).</summary>
        public int CardRarityFilledCellsForTests(int index) => CountFilled(RibbonAt(index));

        /// <summary>그 카드 리본의 <b>첫 칸 색</b>. 등급색이 <c>UiChrome</c> 밖에서 새로 만들어졌는지
        /// 값으로 확인하는 통로다.</summary>
        public Color CardRarityFillColorForTests(int index)
        {
            RarityRibbon ribbon = RibbonAt(index);
            return ribbon?.Cells != null && ribbon.Cells.Length > 0 && ribbon.Cells[0] != null
                ? ribbon.Cells[0].color : Color.clear;
        }

        // ==================== 진단/테스트 전용 — 등급 테두리 ====================
        //
        // ★ 여기도 <b>기대값을 만들어 주지 않는다</b>. 내주는 것은 «지금 화면의 그 요소가 실제로 무엇을
        //   적었고 무슨 색인가»라는 관측값뿐이다.

        /// <summary>카드 <b>바깥</b> 테두리의 현재 색. 「선택 &gt; 호버 &gt; 착용 중 &gt; 등급」 우선순위가
        /// 화면에 실제로 도달하는지를 재는 통로다.</summary>
        public Color CardOutlineColorForTests(int index) => CardAt(index)?.Outline?.color ?? Color.clear;

        /// <summary>보관함 그 줄의 테두리 색 — 카드와 <b>같은 규칙</b>으로 칠해지는지 대조하는 통로다.</summary>
        public Color InventoryRowOutlineColorForTests(int row)
            => row >= 0 && row < _inventoryViews.Length && _inventoryViews[row]?.Outline != null
                ? _inventoryViews[row].Outline.color : Color.clear;

        /// <summary>리본 한 벌이 가진 칸의 총수(트랙 제외). 없으면 −1.</summary>
        public int RarityRibbonCellCountForTests(int index) => RibbonAt(index)?.Cells?.Length ?? -1;

        /// <summary>보관함 <paramref name="row"/>번째 <b>화면 줄</b>의 리본이 보이는가.
        /// 「할 줄 아는 것」과 헤더 줄에서는 false여야 한다 — 등급이 없는 것에 0칸짜리 등급을 주지 않는다.</summary>
        public bool IsInventoryRibbonVisibleForTests(int row)
        {
            RectTransform root = row >= 0 && row < _inventoryRibbons.Length
                ? _inventoryRibbons[row]?.Root : null;
            return root != null && root.gameObject.activeInHierarchy;
        }

        /// <summary>보관함 그 줄의 리본에서 트랙 색이 아닌 칸의 수. 리본이 없으면 −1.</summary>
        public int InventoryRibbonFilledCellsForTests(int row)
            => CountFilled(row >= 0 && row < _inventoryRibbons.Length ? _inventoryRibbons[row] : null);

        /// <summary>보관함 그 줄의 리본 / 설명 상자 / 상태 슬롯의 화면 사각형 — <b>겹치지 않는가</b>를
        /// 재는 통로다(리본이 설명 칸에서 자리를 떼어 왔으므로 그 거래가 지켜졌는지 봐야 한다).</summary>
        public Rect InventoryRibbonRawScreenRect(int row)
            => RawScreenRectOf(row >= 0 && row < _inventoryRibbons.Length ? _inventoryRibbons[row]?.Root : null);

        public Rect InventoryDescriptionRawScreenRect(int row)
            => RawScreenRectOf(row >= 0 && row < _inventoryViews.Length
                ? _inventoryViews[row]?.Description?.rectTransform : null);

        public Rect InventoryStatusSlotRawScreenRect(int row)
            => RawScreenRectOf(row >= 0 && row < _inventoryViews.Length
                ? _inventoryViews[row]?.StatusSlot?.rectTransform : null);

        /// <summary>보관함 그 줄이 지금 물고 있는 카탈로그 인덱스(헤더면 −1, 줄이 꺼져 있으면 −2).
        /// 테스트가 「행동 줄」을 좌표가 아니라 <b>데이터</b>로 찾는 통로다.</summary>
        public int InventoryRowCatalogIndexForTests(int row)
        {
            InventoryRowView view = row >= 0 && row < _inventoryViews.Length ? _inventoryViews[row] : null;
            if (view?.Rect == null || !view.Rect.gameObject.activeInHierarchy) return -2;
            return view.BoundCatalogIndex;
        }

        /// <summary>보관함 상세 카드의 제목 줄(<c>이름 · 등급 · 카테고리 · 상태</c>).</summary>
        public string InventoryDetailNameTextForTests
            => _inventoryDetailName != null ? _inventoryDetailName.text : null;

        /// <summary>트랙 색이 아닌 칸을 센다. 이 한 곳만 색을 비교한다 — 두 곳에서 세면 갈라진다.</summary>
        private static int CountFilled(RarityRibbon ribbon)
        {
            if (ribbon?.Cells == null) return -1;
            int n = 0;
            for (int i = 0; i < ribbon.Cells.Length; i++)
            {
                Image cell = ribbon.Cells[i];
                if (cell != null && cell.color != UiChrome.RarityTrack) n++;
            }
            return n;
        }
    }
}
