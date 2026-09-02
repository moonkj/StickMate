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

        /// <summary>진단/테스트 전용 — 드래그 손잡이(타이틀바)의 화면 사각형.</summary>
        public Rect TitleBarScreenRect => RawScreenRectOf(_titleBarRect);

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

        /// <summary>카드의 <b>잘리기 전</b> 화면 사각형. 캐러셀 밖으로 밀려난 카드도 값이 나온다 —
        /// "보이지 않는데 눌리는가"를 재려면 그 자리를 알아야 한다.</summary>
        public Rect CardRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Rect);

        /// <summary>카드가 캐러셀 마스크에 <b>잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).
        /// "반쯤 걸친 카드가 있는가" — 즉 이 창의 유일한 발견 단서(<see cref="CarouselViewportWidth"/>)가
        /// 실제로 화면에 있는가를 회귀 테스트가 숫자로 확인하는 창구다.</summary>
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

        /// <summary>세로 한 칸(카테고리 섹션)의 높이. 창 높이가 섹션 수에서 파생되는지 확인할 때 쓴다.</summary>
        public float SectionStepPoints => SectionStep;

        /// <summary>카드 이름 상자 / 메타 상자의 화면 사각형(잘리기 전).</summary>
        public Rect CardNameRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Name?.rectTransform);

        public Rect CardMetaRawScreenRect(int index) => RawScreenRectOf(CardAt(index)?.Meta?.rectTransform);

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

        /// <summary>캐러셀 한 줄(잡고 미는 자리)의 화면 사각형.</summary>
        public Rect CarouselRowScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.RowRect : null);

        /// <summary>캐러셀 한 줄이 <b>마스크에 잘리고 남은</b> 화면 사각형(전부 잘리면 넓이 0).
        ///
        /// <para>★ 왜 <see cref="CarouselRowScreenRect"/>와 따로 필요한가(2026-09-02 실측): 배치모드
        /// PlayMode의 화면은 640×480이라 <see cref="ClampPanelToScreen"/>이 이 창을 608pt로 줄이는데,
        /// <b>내용은 함께 접히지 않는다</b> — 줄 자체는 1042 폭 기준 자리(패널 좌단 266..1020)에
        /// 그대로 있고 <c>Body</c> 마스크가 608에서 자른다. 그래서 <b>줄의 한가운데가 잘린 쪽</b>에
        /// 들어가고, 그 자리는 이 창의 규칙("보이지 않는 것은 눌리지 않는다",
        /// <see cref="ContainsScreenPoint"/>)에 따라 <b>정당하게</b> 잡히지 않는다.</para>
        ///
        /// <para>테스트가 드래그를 걸 지점은 여기서 고른다. 날 사각형의 중심을 잡으면 제품이 멀쩡한데도
        /// 화면 크기 때문에 빨개지는 <b>거짓 빨강</b>이 난다(<c>ScrollInventoryForTests</c>가 같은
        /// 사정으로 생긴 창구다).</para></summary>
        public Rect CarouselRowVisibleScreenRect(int section)
            => VisibleScreenRectOf(section >= 0 && section < _sections.Length ? _sections[section]?.RowRect : null);

        /// <summary>섹션 헤더의 "n / 6" 카운터 사각형. 이 창 오른쪽 열의 <b>오른쪽 끝선</b>을 정의하는
        /// 요소이고, 카드줄 바로 위에 있다 — 회귀 테스트가 그 끝선을 숫자로 베끼지 않고 물어보는 통로.</summary>
        public Rect SectionCountScreenRect(int section)
            => RawScreenRectOf(section >= 0 && section < _sections.Length
                ? _sections[section]?.Count?.rectTransform : null);

        /// <summary>지금 밀려 있는 양(캔버스 포인트, 왼쪽으로 밀면 음수).</summary>
        public float CarouselOffsetPoints(int section)
        {
            SectionView view = section >= 0 && section < _sections.Length ? _sections[section] : null;
            return view != null && view.Content != null ? view.Content.anchoredPosition.x : 0f;
        }

        /// <summary>이 카테고리에서 밀 수 있는 최대치(양수). 0이면 카드가 화면에 다 들어온다는 뜻이다.</summary>
        public float CarouselMaxScrollPoints(int section)
            => MaxCarouselScroll(section >= 0 && section < _sections.Length ? _sections[section] : null);

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

        /// <summary>지금 탭에서 창이 목표로 하는 높이(캔버스 포인트). 애니메이션 중인 실제 높이는
        /// <see cref="PanelSizePoints"/>가 준다 — 둘을 나눠 두어야 "다 줄었는가"를 기다릴 수 있다.</summary>
        public float TargetPanelHeightPoints => PanelHeightForTab(_tab);

        /// <summary>높이 애니메이션이 지금 도달한 값(<b>화면 클램프 전</b>).
        /// <para><see cref="PanelSizePoints"/>는 <see cref="ClampPanelToScreen"/>이 화면 높이로 자른
        /// <b>뒤</b>의 값이라, 화면이 낮은 실행 환경(배치모드 등)에서는 목표에 영원히 닿지 않는다 —
        /// "애니메이션이 끝났는가"를 그걸로 판정하면 테스트가 환경에 따라 거짓 실패한다.</para></summary>
        public float AnimatedPanelHeightPoints => _panelHeightPoints;

        /// <summary>
        /// ★ <b>마지막 카드 줄 아래 끝</b>과 <b>상세 패널 위 끝</b> 사이의 빈 높이(캔버스 포인트).
        ///
        /// <para>P0-1이 고친 결함이 정확히 이 값이었다: [장비](섹션 4개)에서는 20pt인데
        /// [외형](섹션 3개)에서는 <b>176pt</b>였다 — 없는 4번째 섹션의 자리를 예약했기 때문이다.
        /// 회귀 테스트는 "두 탭에서 이 값이 같다"를 본다. 숫자를 베끼지 않고 <b>탭끼리 비교</b>하므로
        /// 상수를 바꿔도 테스트가 따라온다.</para>
        /// </summary>
        public float SectionsToDetailGapPoints
        {
            get
            {
                if (Def(_tab).Page != TabPage.Cards) return float.NaN;
                int last = SectionCountForTab(_tab) - 1;
                if (last < 0 || last >= _sections.Length) return float.NaN;
                Rect row = RawScreenRectOf(_sections[last]?.RowRect);
                Rect detail = RawScreenRectOf(_sectionDetailRect);
                if (row.height <= 0f || detail.height <= 0f) return float.NaN;
                return (row.yMin - detail.yMax) / CanvasScale();   // 화면 y는 위가 양수.
            }
        }
    }
}
