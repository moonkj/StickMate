using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// [보관함] 탭 — 20줄 가상 목록과 페이지 레일.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나눈 조각이다.
    /// <b>분할은 줄 단위로 그대로 옮겼다</b>(옮기기 전후로 코드 줄 집합이 동일함을 확인).
    /// 그 뒤 같은 라운드에서 탭 판정만 <see cref="CharacterInfoWindow.TabTable"/> 기반으로 바꿨다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 보관함(가상 목록) ====================

        /// <summary>목록의 논리적 줄 수 = 헤더 2줄 + 카탈로그 전체(장비 42 + 행동 12 = 54).
        /// <para>2026-09-02 격파 놀이 삭제로 행동이 13 → 12가 됐다.</para>
        /// <para>★ 2026-09-02 — 여기 "장비 32"라고 적혀 있었다. 실제는 <b>42종</b>이고
        /// (<c>Resources/Items/*.asset</c> 42개), 페이지 수가 32든 42든 3이라 <b>화면에는 티가 나지
        /// 않았다</b>. 숫자를 손으로 적지 않는 것이 원칙이지만 주석은 예외가 없어 이렇게 샌다 —
        /// 다음 사람이 이 숫자로 계산하면 10종을 잃는다.</para></summary>
        private static int InventoryLineCount => ItemCatalog.Count + 2;

        /// <summary>논리적 줄 번호 -> 카탈로그 인덱스. 헤더면 -1.
        /// 순서: [걸치는 것] 헤더 → 장비 전부 → [할 줄 아는 것] 헤더 → 행동 전부.
        /// 카탈로그가 이미 그 순서로 정의되어 있어 재정렬하지 않는다(정렬 규칙이 두 곳에 생기지 않게).</summary>
        private static int CatalogIndexForLine(int line)
        {
            int equipmentCount = ItemCatalog.EquipmentCount;
            if (line <= 0) return -1;                              // "걸치는 것" 헤더
            if (line <= equipmentCount) return line - 1;           // 장비
            if (line == equipmentCount + 1) return -1;             // "할 줄 아는 것" 헤더
            return line - 2;                                       // 행동
        }

        private string HeaderTextForLine(int line)
        {
            if (line == 0)
            {
                return $"걸치는 것  ({ItemCatalog.UnlockedEquipmentCount(_config)} / {ItemCatalog.EquipmentCount})";
            }
            return $"할 줄 아는 것  ({ItemCatalog.ActionCount})";
        }

        private int MaxInventoryScroll => Mathf.Max(0, InventoryLineCount - InventoryVisibleRows);

        private void RefreshInventoryList()
        {
            _inventoryScroll = Mathf.Clamp(_inventoryScroll, 0, MaxInventoryScroll);

            for (int i = 0; i < _inventoryViews.Length; i++)
            {
                InventoryRowView view = _inventoryViews[i];
                if (view == null) continue;

                int line = _inventoryScroll + i;
                if (line >= InventoryLineCount)
                {
                    view.Rect.gameObject.SetActive(false);
                    continue;
                }
                view.Rect.gameObject.SetActive(true);

                int catalogIndex = CatalogIndexForLine(line);
                view.BoundCatalogIndex = catalogIndex;

                if (catalogIndex < 0)
                {
                    // 헤더 줄 — 표면을 지우고 제목만 남긴다.
                    view.Surface.color = Color.clear;
                    view.Outline.color = Color.clear;
                    view.Dot.color = Color.clear;
                    view.Title.text = string.Empty;
                    view.Subtitle.text = string.Empty;
                    view.Description.text = string.Empty;
                    view.StatusSlot.text = string.Empty;
                    view.HeaderText.text = HeaderTextForLine(line);
                    continue;
                }

                ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                if (entry == null) continue;

                bool owned = entry.IsOwned(_config);
                bool selected = catalogIndex == _selectedInventoryIndex;
                bool worn = entry.IsEquipped();

                view.HeaderText.text = string.Empty;
                view.Title.text = owned ? entry.DisplayName : "???";
                view.Subtitle.text = entry.CategoryLabel;
                view.Description.text = owned ? Ellipsize(entry.ShortDescription, InventoryDescriptionChars) : string.Empty;
                view.StatusSlot.text = entry.ResolveStatusSlot(_config);

                view.Surface.color = selected ? UiChrome.CardSurface
                    : owned ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
                view.Outline.color = selected ? UiChrome.TextPrimary
                    : worn ? UiChrome.CardBorderWorn : UiChrome.CardBorder;
                // 도트만 글자가 아니다 — 나머지 셋은 전부 같은 사다리에서 나온다.
                view.Dot.color = entry.Slot.HasValue
                    ? (worn ? UiChrome.CategoryTint(entry.Slot.Value)
                            : owned ? UiChrome.NonTextMuted : UiChrome.TrackBackground)
                    : UiChrome.NonTextMuted;
                view.Title.color = UiChrome.InkTitle(owned);
                view.Subtitle.color = UiChrome.InkMeta;
                view.Description.color = UiChrome.InkBody(owned);
                view.StatusSlot.color = worn && entry.Slot.HasValue ? UiChrome.CategoryTint(entry.Slot.Value)
                    : UiChrome.InkMeta;
            }

            if (_pageIndicator != null)
            {
                // 마지막 페이지는 스크롤이 상한에 걸려 한 페이지 분량이 채 안 되므로 올림으로 센다
                // (나눗셈으로만 세면 마지막 페이지에서 "2/3"처럼 어긋난다 — 육안 검증에서 확인).
                int page = Mathf.CeilToInt(_inventoryScroll / (float)InventoryVisibleRows) + 1;
                int pages = Mathf.Max(1, Mathf.CeilToInt((float)InventoryLineCount / InventoryVisibleRows));
                // ★ 2026-09-02 — 예전에는 $"{page}\n/\n{pages}"였다. 폭 부족 줄바꿈이 아니라
                //   <b>명시적 개행</b>이었고(이 Text는 HorizontalWrapMode.Overflow라 애초에 줄바꿈을
                //   하지 않는다), 세로로 쌓인 1 / 3은 "3 중 1"이 아니라 <b>분수 ⅓</b>으로 읽혔다.
                //   깨진 글자가 아니라 <b>다른 뜻</b>이라 더 나쁘다(45-9-a).
                _pageIndicator.text = $"{page} / {pages}";
            }

            // 칩의 겉모습과 클릭 처리가 <b>같은 하나</b>(CanScrollInventory)를 본다 — 두 벌로 두면
            // 반드시 한쪽만 갱신되고, 그게 곧 표시-실제 불일치다(SettingsWindow.SyncPageButtons와 같은 규칙).
            ApplyPagerEnabled(_pageUpOutline, _pageUpLabel, CanScrollInventory(-1));
            ApplyPagerEnabled(_pageDownOutline, _pageDownLabel, CanScrollInventory(+1));

            RefreshInventoryDetail();
        }

        /// <summary>목록 한 줄에 들어갈 길이로 자른다. 자동 줄바꿈에 맡기면 두 번째 줄이 행 높이에
        /// 걸려 <b>반쯤 잘린 글자</b>가 남는다 — 잘렸다는 사실을 말줄임표로 <b>드러내는</b> 편이
        /// 정직하고 깔끔하다. 전문은 아래 상세 카드가 보여준다.</summary>
        private static string Ellipsize(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
            return text.Substring(0, maxChars).TrimEnd() + "...";
        }

        private void RefreshInventoryDetail()
        {
            ItemCatalogEntry entry = ItemCatalog.At(_selectedInventoryIndex);
            if (entry == null) return;
            bool owned = entry.IsOwned(_config);

            if (_inventoryDetailName != null)
            {
                _inventoryDetailName.text = owned
                    ? $"{entry.DisplayName}   ·   {entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}"
                    : $"???   ·   {entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}";
            }
            if (_inventoryDetailBody != null)
            {
                _inventoryDetailBody.text = owned
                    ? entry.Description
                    : $"레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다.";
            }
        }

        private void ScrollInventory(int delta)
        {
            int next = Mathf.Clamp(_inventoryScroll + delta * InventoryVisibleRows, 0, MaxInventoryScroll);
            if (next == _inventoryScroll) return;
            _inventoryScroll = next;
            RefreshInventoryList();
        }

        /// <summary>그 방향으로 <b>실제로 움직일 수 있는가</b>. 겉모습과 클릭 처리가 이 하나를 본다.
        ///
        /// <para>★ 설정창(<c>SettingsWindow.CanScroll</c>)에서 그대로 베끼지 <b>않았다</b>: 그쪽은
        /// 연속 스크롤(<c>float</c>)이라 <c>0.5f</c> 여유를 두지만 여기는 <b>줄 단위 정수</b>다.
        /// 정수에는 부동소수 경계가 없으므로 그 여유를 옮겨 적으면 뜻 없는 마법수가 하나 는다.</para>
        ///
        /// <para>레일을 <b>숨기는 분기도 넣지 않았다</b>. 카탈로그가 <see cref="InventoryVisibleRows"/>줄
        /// 이하로 줄면 양쪽 칩이 죽고 지시자가 <c>1 / 1</c>이 되는 것으로 충분하고, 그게 더 정직하다
        /// (레일 양 끝 캡이라 하나가 사라지면 막대 자체가 고장 난 것처럼 보인다).</para></summary>
        private bool CanScrollInventory(int direction)
            => direction < 0 ? _inventoryScroll > 0 : _inventoryScroll < MaxInventoryScroll;

        /// <summary>끝에 닿은 칩을 <b>죽이되 지우지 않는다</b>.
        ///
        /// <para>바꾸는 것은 <b>테두리와 글리프</b>뿐이고 <b>면은 그대로</b>다. 그리고 합성 바탕이
        /// 설정창과 <b>다르다</b> — 저쪽 칩 면은 <c>CardSurfaceMuted</c>, 이쪽은 <c>CardSurface</c>다.
        /// <c>CardBorder</c>/<c>Divider</c>는 알파 색이라 <b>어느 면 위에 올리느냐로 결과가 달라진다</b>.
        /// 설정창의 결과색을 그대로 옮기면 테두리만 미묘하게 어긋난다(14.5-a).</para>
        ///
        /// <para>글리프는 산문이 아니라 <b>기호</b>이므로 아이콘 사다리(<see cref="UiChrome.InkIcon"/>)를
        /// 쓴다.</para></summary>
        private static void ApplyPagerEnabled(Image outline, Text glyph, bool enabled)
        {
            if (outline == null || glyph == null) return;

            Color edge = UiChrome.Flatten(enabled ? UiChrome.CardBorder : UiChrome.Divider,
                UiChrome.CardSurface);
            if (outline.color != edge) outline.color = edge;

            Color ink = UiChrome.InkIcon(enabled);
            if (glyph.color != ink) glyph.color = ink;
        }

        private void OnInventoryRowClicked(int catalogIndex)
        {
            if (catalogIndex < 0 || _selectedInventoryIndex == catalogIndex) return;
            _selectedInventoryIndex = catalogIndex;
            RefreshInventoryList();
            ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
            if (entry != null) Debug.Log($"[보관함] 선택 -> {entry.DisplayName}({entry.CategoryLabel}).");
        }

        // -------------------- 보관함 페이지 --------------------

        private void BuildInventoryPage(RectTransform right)
        {
            var pageGo = new GameObject("InventoryPage", typeof(RectTransform));
            pageGo.transform.SetParent(right, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, RightWidth, BodyHeight);
            _inventoryPage = pageGo;

            float rowStep = InventoryRowHeight + InventoryRowGap;

            for (int i = 0; i < InventoryVisibleRows; i++)
            {
                Image surface = UiChrome.AddSurface(page, "InvRow" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, RightPadX, SectionsTopY - i * rowStep, InventoryListWidth, InventoryRowHeight);
                Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusChip);

                // 장비/행동을 완전히 같은 행 모양으로 그린다(디자이너 확정) —
                // ● 표식 / 이름 / 부제 / 설명 한 줄 / 상태 슬롯(96pt 고정, 훗날 가격표 자리).
                Image dot = UiChrome.AddSurface(rt, "Dot", UiChrome.NonTextMuted, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, UiChrome.Space2, -(InventoryRowHeight - 6f) * 0.5f, 6f, 6f);
                dot.raycastTarget = false;

                float nameX = UiChrome.Space2 + 6f + UiChrome.Space2;
                Text title = Label(rt, "Title", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    nameX, 0f, 110f, InventoryRowHeight, string.Empty);
                Text subtitle = Label(rt, "Subtitle", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.InkMeta,
                    nameX + 112f, 0f, 48f, InventoryRowHeight, string.Empty);

                Text description = Label(rt, "Description", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                    UiChrome.TextSecondary, InventoryDescriptionX, 0f,
                    Mathf.Max(40f, InventoryDescriptionWidth), InventoryRowHeight, string.Empty);
                // 줄바꿈하지 않는다 — 길이는 Ellipsize가 미리 자른다(위 상수 참고).
                description.horizontalOverflow = HorizontalWrapMode.Overflow;
                description.verticalOverflow = VerticalWrapMode.Truncate;

                Text statusSlot = Label(rt, "StatusSlot", UiChrome.FontCaption, TextAnchor.MiddleRight,
                    UiChrome.TextTertiary, InventoryListWidth - StatusSlotWidth - UiChrome.Space2, 0f,
                    StatusSlotWidth, InventoryRowHeight, string.Empty);

                Text header = Label(rt, "Header", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    0f, 0f, InventoryListWidth, InventoryRowHeight, string.Empty, bold: true);

                var button = surface.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                int captured = i;
                button.onClick.AddListener(() =>
                {
                    InventoryRowView view = _inventoryViews[captured];
                    if (view == null || view.BoundCatalogIndex < 0) return;
                    if (TryClaimAction("inv" + captured)) OnInventoryRowClicked(view.BoundCatalogIndex);
                });

                _inventoryViews[i] = new InventoryRowView
                {
                    Rect = rt, Surface = surface, Outline = outline, Dot = dot, Title = title, Subtitle = subtitle,
                    Description = description, StatusSlot = statusSlot, HeaderText = header, BoundCatalogIndex = -1,
                };
            }

            // 페이지 버튼 — 휠에 기대지 않는다(클래스 문서 참고: 우리 창은 앱이 활성일 때만 휠을 받는다).
            float listHeight = InventoryVisibleRows * rowStep - InventoryRowGap;
            float railX = RightPadX + InventoryListWidth + UiChrome.Space2;

            _pageUpRect = BuildPagerButton(page, "PageUp", "▲", railX, SectionsTopY, -1, "pageUp",
                out _pageUpOutline, out _pageUpLabel);
            _pageDownRect = BuildPagerButton(page, "PageDown", "▼", railX,
                SectionsTopY - (listHeight - InventoryRailWidth), +1, "pageDown",
                out _pageDownOutline, out _pageDownLabel);

            _pageIndicator = Label(page, "PageIndicator", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.InkMeta, railX, SectionsTopY - (InventoryRailWidth + UiChrome.Space2),
                InventoryRailWidth, InventoryPageIndicatorHeight, "1 / 1");

            Image detail = UiChrome.AddSurface(page, "InventoryDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailYForTab(Tab.Inventory), RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _inventoryDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, RightContentWidth - 30f, 17f, "—", bold: true);

            _inventoryDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_inventoryDetailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 34f);
            _inventoryDetailBody.lineSpacing = 1.6f;

            // 지금 파는 것은 하나도 없다 — 그 사실을 화면에서도 숨기지 않는다.
            Label(drt, "Note", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                RightContentWidth - 215f, -DetailHeight + 26f, 200f, 14f, "지금은 파는 것이 없습니다");
        }

        /// <summary>페이지 칩 하나. ★ 2026-09-02 — 테두리와 글리프를 <b>밖으로 내보낸다</b>.
        /// 예전에는 둘을 지역 변수로 버려서 "끝에 닿았다"를 칠할 대상 자체가 없었고, 그래서 1페이지의
        /// [▲]가 [▼]와 <b>픽셀 단위로 동일</b>한 채 눌러도 조용히 아무 일도 안 했다(45-9-b).</summary>
        private RectTransform BuildPagerButton(RectTransform page, string name, string glyph, float x, float y,
            int direction, string dedupKey, out Image outlineOut, out Text labelOut)
        {
            Image surface = UiChrome.AddSurface(page, name, UiChrome.CardSurface, UiChrome.RadiusChip);
            var rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, x, y, InventoryRailWidth, InventoryRailWidth);
            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusChip);

            Text label = UiChrome.AddText(rt, "Label", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.InkIcon(true));
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() =>
            {
                if (!CanScrollInventory(direction)) return;   // 죽은 칩은 <b>아무 일도 하지 않는다</b>.
                if (TryClaimAction(dedupKey)) ScrollInventory(direction);
            });
            outlineOut = outline;
            labelOut = label;
            return rt;
        }
    }
}
