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

        // ---- 등급 리본 (2026-09-03, 카드와 같은 규칙) ----
        //
        // ★ <b>인계본의 「4열 46px 격자」가 우리 보관함이 아니다.</b> 우리 [보관함]은 격자가 아니라
        //   <b>24pt 줄 20개</b>짜리 가상 목록이고, 썸네일이 아예 없다. 그래서 "카드 상단 여백"에
        //   해당하는 <b>세로</b> 빈칸이 없다 — 리본은 <b>가로</b>로 자리를 얻어야 한다.
        //
        // 실측으로 남는 자리는 하나뿐이었다: 설명 칸(434pt)의 <b>꼬리</b>다. 다른 후보는 전부 임자가 있다 —
        //   · 왼쪽 도트(6pt): 착용/보유/잠김을 이미 말한다. 그리고 4칸이 안 들어간다.
        //   · 이름(110) · 부제(48): 두 상자 사이가 2pt다.
        //   · 상태 슬롯(96pt): 최장 문구 <c>Lv.20에 열림</c>이 대부분을 쓰고, 훗날 가격표 자리다.
        // 그래서 설명 칸에서 <b>54pt(리본 46 + 틈 8)</b>를 떼어 낸다 = 434 -> 380pt(12.4% 감소).
        // 그 대가는 정직하게 적어 둔다: 설명 한 줄이 그만큼 일찍 말줄임된다. <b>전문은 아래 상세 카드에
        // 그대로 있고</b>, 등급은 아래 상세 카드에 <b>없던</b> 정보다 — 없던 채널을 얻고 있던 채널을
        // 조금 줄인 거래다.

        /// <summary>보관함 줄의 리본 폭(pt).
        /// <para>칸 폭 = (46 − <see cref="UiChrome.RarityCellGap"/> × 3) ÷ 4 = <b>10.0pt</b>.
        /// PALETTE_SPEC §12-3이 잰 개수 채널의 하한은 <b>총 15pt</b>(칸 3px + 틈 1px, ×1 배율)이므로
        /// 3.07배 여유다. 같은 절이 「가정 44pt 카드」에서 통과시킨 칸 폭 8.25pt보다도 넓다.</para></summary>
        private const float InventoryRibbonWidth = 46f;

        /// <summary>리본 몫을 뺀 <b>설명 글자</b> 폭. 상자 폭과 말줄임 예산이 <b>같은 값</b>이어야 한다는
        /// 기존 규약을 그대로 잇는다(예전에 상자는 폭으로, 자르기는 글자 수로 정해져 단위가 갈라졌었다).
        /// <para>0에서 막는 이유: 창이 극단적으로 좁아져 원본 상자가 하한에 닿으면 <b>설명이 먼저</b>
        /// 사라지고 리본이 남는다. 등급은 주 채널이고 설명은 아래 상세 카드에 전문이 있다.</para></summary>
        private static float InventoryDescriptionTextWidth
            => Mathf.Max(0f, InventoryDescriptionBoxWidth - InventoryRibbonWidth - UiChrome.Space2);

        /// <summary>리본의 왼쪽 x — 설명 칸 <b>바로 뒤</b>, 상태 슬롯 <b>바로 앞</b>.</summary>
        private static float InventoryRibbonX
            => InventoryDescriptionX + InventoryDescriptionTextWidth + UiChrome.Space2;

        /// <summary>줄마다 한 벌. <see cref="_inventoryViews"/>와 <b>같은 인덱스</b>다
        /// (<see cref="InventoryRowView"/>는 <c>CharacterInfoWindow.cs</c> 소유라 이 라운드가 열지 않는다).</summary>
        private readonly RarityRibbon[] _inventoryRibbons = new RarityRibbon[InventoryVisibleRows];

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
                    HideRarityRibbon(_inventoryRibbons[i]);
                    view.Surface.color = Color.clear;
                    view.Outline.color = Color.clear;
                    view.Dot.color = Color.clear;
                    view.Title.text = string.Empty;
                    view.Subtitle.text = string.Empty;
                    view.Description.text = string.Empty;
                    view.DescriptionSource = string.Empty;
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
                // ★ 2026-09-03 — 글자 수가 아니라 <b>실제 폭</b>으로 자른다(UiChrome.Ellipsize).
                //   그 함수는 폭을 재려고 Text.text를 여러 번 바꾸므로 <b>내용이 바뀐 순간에만</b>
                //   부른다 — 원본을 캐시해 비교하는 것이 그 함수의 호출부 규약이다.
                string wantedDescription = owned ? entry.ShortDescription : string.Empty;
                if (!string.Equals(view.DescriptionSource, wantedDescription, System.StringComparison.Ordinal))
                {
                    view.DescriptionSource = wantedDescription;
                    view.Description.text = UiChrome.Ellipsize(view.Description, wantedDescription,
                        InventoryDescriptionTextWidth);
                }
                view.StatusSlot.text = entry.ResolveStatusSlot(_config);

                // ★ 등급은 <b>장비에만</b> 있다. 「할 줄 아는 것」(행동 12종)은 슬롯이 없고 등급도 없다 —
                //   그 줄에서는 리본을 <b>통째로 숨긴다</b>. 빈 트랙만 남기면 "등급이 없다"가 아니라
                //   <b>"0칸짜리 등급"</b>으로 읽힌다(HideRarityRibbon 문서).
                // ★ 잠긴 장비도 리본을 <b>흐리지 않는다</b> — 카드와 같은 규칙이고 이유도 같다.
                bool hasRarity = entry.Slot.HasValue && entry.ItemIndex >= 0;
                if (hasRarity)
                {
                    ApplyRarityRibbon(_inventoryRibbons[i],
                        ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex));
                }
                else
                {
                    HideRarityRibbon(_inventoryRibbons[i]);
                }

                view.Surface.color = selected ? UiChrome.CardSurface
                    : owned ? UiChrome.CardSurface : UiChrome.CardSurfaceMuted;
                // ★ 2026-09-03 — 카드와 <b>같은 규칙</b>으로 「기본」 자리를 등급이 승계한다
                //   (사용자 지시: *"카드 외곽선을 각 레벨별로 분류하는게 어때"*).
                //   여기는 호버가 없어 <b>3상태</b>라 충돌이 카드보다 한 단 적다.
                //   ★ <b>한쪽만 고치면 같은 창 안에서 등급 표기가 갈라진다</b> — 카드 페이지와 이 줄은
                //     같은 아이템을 서로 다른 테두리로 그리게 된다.
                //   ★ 등급이 <b>없는</b> 줄(「할 줄 아는 것」)은 현행 CardBorder를 유지한다 — 없는 것을
                //     「일반」으로 칠하면 0단짜리 등급이 생긴다(HideRarityRibbon과 같은 규칙).
                view.Outline.color = selected ? UiChrome.TextPrimary
                    : worn ? UiChrome.CardBorderWorn
                    : hasRarity ? UiChrome.RarityBorder(
                        ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex))
                    : UiChrome.CardBorder;
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

        // ★★ 2026-09-03 — 여기 있던 <b>글자 수 기준 Ellipsize</b>를 지웠다.
        //
        //    (가) 이름이 <c>UiChrome.Ellipsize</c>와 같았다. "같은 이름 두 벌은 반드시 한쪽만
        //        갱신된다" — 실제로 그랬다: 저쪽은 <c>Text.preferredWidth</c> 실측 + 이진 탐색으로
        //        진화했는데 이쪽은 <c>Substring(0, n) + "..."</c>에 멈춰 있었다.
        //    (나) 말줄임표도 달랐다. 저쪽은 한 글자짜리 U+2026(<c>…</c>), 이쪽은 마침표 세 개.
        //        같은 창의 두 자리가 서로 다른 기호로 "잘렸다"를 말하고 있었다.
        //    (다) 글자 수 상한은 한글에서만 맞았다(CharacterInfoWindow.cs의 <c>CaptionKoreanAdvance</c>
        //        삭제 주석 참고).
        //
        //    자동 줄바꿈에 맡기지 <b>않는</b> 이유는 그대로다 — 두 번째 줄이 행 높이에 걸려
        //    <b>반쯤 잘린 글자</b>가 남는다. 잘렸다는 사실은 말줄임표로 드러내고, 전문은 아래
        //    상세 카드가 보여준다.

        private void RefreshInventoryDetail()
        {
            ItemCatalogEntry entry = ItemCatalog.At(_selectedInventoryIndex);
            if (entry == null) return;
            bool owned = entry.IsOwned(_config);

            if (_inventoryDetailName != null)
            {
                // ★ 2026-09-03 — 등급 <b>낱말</b>. 리본(칸 수)이 못 하는 일을 이 한 토막이 한다.
                //   장비에만 붙는다 — 행동에는 등급이 없고, 없는 것을 「일반」이라고 적으면 그건
                //   원칙 1이 금지하는 <b>없는 사실</b>이다(가격이 없을 때 0을 그리지 않는 것과 같은 규칙).
                string rarity = entry.Slot.HasValue && entry.ItemIndex >= 0
                    ? ItemCatalog.RarityName(ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex)) + "   ·   "
                    : string.Empty;
                _inventoryDetailName.text = owned
                    ? $"{entry.DisplayName}   ·   {rarity}{entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}"
                    : $"???   ·   {rarity}{entry.CategoryLabel}   ·   {entry.ResolveStatusSlot(_config)}";
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

        private void BuildInventoryPage(RectTransform body)
        {
            var pageGo = new GameObject("InventoryPage", typeof(RectTransform));
            pageGo.transform.SetParent(body, false);
            var page = pageGo.GetComponent<RectTransform>();
            // ★ 3컬럼은 카드 탭의 것이다 — [보관함]은 본문 <b>전체 폭</b>을 쓴다.
            UiChrome.PlaceTopLeft(page, 0f, 0f, PanelWidth, BodyHeight);
            _inventoryPage = pageGo;

            float rowStep = InventoryRowHeight + InventoryRowGap;

            for (int i = 0; i < InventoryVisibleRows; i++)
            {
                Image surface = UiChrome.AddSurface(page, "InvRow" + i, UiChrome.CardSurface, UiChrome.RadiusChip);
                var rt = surface.rectTransform;
                UiChrome.PlaceTopLeft(rt, PagePadX, PageTopY - i * rowStep, InventoryListWidth, InventoryRowHeight);
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
                    InventoryDescriptionTextWidth, InventoryRowHeight, string.Empty);
                // 줄바꿈하지 않는다 — 길이는 UiChrome.Ellipsize가 InventoryDescriptionWidth로 미리 자른다.
                // ★ 상자 폭과 자르기 예산이 <b>같은 상수</b>다. 예전에는 상자는 폭으로, 자르기는
                //   글자 수로 정해져 둘이 서로 다른 단위였고, 창이 넓어지면 상자만 커졌다.
                description.horizontalOverflow = HorizontalWrapMode.Overflow;
                description.verticalOverflow = VerticalWrapMode.Truncate;

                // 등급 리본 — 설명 꼬리와 상태 슬롯 사이. 줄 높이(24) 한가운데에 놓는다.
                // 세로 검산: (24 − 4) ÷ 2 = 10pt가 위아래 여백이고, 캡션 글리프 상자(≈14pt)와
                // 같은 줄에 있지만 <b>가로로 겹치지 않는다</b>(설명 칸이 리본 폭만큼 줄었다).
                _inventoryRibbons[i] = BuildRarityRibbon(rt, InventoryRibbonX,
                    -(InventoryRowHeight - UiChrome.RarityRibbonHeight) * 0.5f, InventoryRibbonWidth);

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
            float railX = PagePadX + InventoryListWidth + UiChrome.Space2;

            _pageUpRect = BuildPagerButton(page, "PageUp", "▲", railX, PageTopY, -1, "pageUp",
                out _pageUpOutline, out _pageUpLabel);
            _pageDownRect = BuildPagerButton(page, "PageDown", "▼", railX,
                PageTopY - (listHeight - InventoryRailWidth), +1, "pageDown",
                out _pageDownOutline, out _pageDownLabel);

            _pageIndicator = Label(page, "PageIndicator", UiChrome.FontCaption, TextAnchor.MiddleCenter,
                UiChrome.InkMeta, railX, PageTopY - (InventoryRailWidth + UiChrome.Space2),
                InventoryRailWidth, InventoryPageIndicatorHeight, "1 / 1");

            Image detail = UiChrome.AddSurface(page, "InventoryDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, PagePadX, InventoryDetailY, PageContentWidth, InventoryDetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _inventoryDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, PageContentWidth - 30f, 17f, "—", bold: true);

            _inventoryDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_inventoryDetailBody.rectTransform, 15f, -42f, PageContentWidth - 30f, 34f);
            _inventoryDetailBody.lineSpacing = 1.6f;

            // 지금 파는 것은 하나도 없다 — 그 사실을 화면에서도 숨기지 않는다.
            Label(drt, "Note", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                PageContentWidth - 215f, -InventoryDetailHeight + 26f, 200f, 14f, "지금은 파는 것이 없습니다");
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
