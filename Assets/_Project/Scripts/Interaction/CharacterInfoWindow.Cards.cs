using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 카테고리 섹션 + 카드 캐러셀 + 선택 상세 패널. [장비]/[외형] 같은 <b>카드 페이지</b>가 쓰는 전부.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나눈 조각이다.
    /// <b>분할은 줄 단위로 그대로 옮겼다</b>(옮기기 전후로 코드 줄 집합이 동일함을 확인).
    /// 그 뒤 같은 라운드에서 탭 판정만 <see cref="CharacterInfoWindow.TabTable"/> 기반으로 바꿨다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 카테고리 섹션 + 카드 ====================

        /// <summary>탭이 보여주는 <paramref name="section"/>번째 카테고리. "외형 계열"의 정의는
        /// <see cref="EquipmentModel.IsAppearanceSlot"/> 하나뿐이라 여기서 숫자를 다시 적지 않는다.
        /// <para>★ 2026-09-02 — <c>tab == Tab.Appearance</c> 2분기를 <see cref="TabDef"/>로 바꿨다.
        /// 예전 형태는 <b>"외형이 아니면 장비"</b>였고, 카드가 없는 탭이 물어봐도 [장비]의 첫 카테고리를
        /// 조용히 돌려줬다.</para></summary>
        private static EquipmentSlot SectionSlot(Tab tab, int section)
        {
            TabDef def = Def(tab);
            if (def.Page != TabPage.Cards)
            {
                Debug.LogError($"[정보창] [{def.Name}] 탭에는 카테고리 섹션이 없는데 {section}번을 물었습니다.");
                return EquipmentSlot.Head;
            }

            int found = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (EquipmentModel.IsAppearanceSlot(slot) != def.AppearanceSlots) continue;
                if (found == section) return slot;
                found++;
            }
            return EquipmentSlot.Head;
        }

        /// <summary>이 탭이 실제로 보여줄 카테고리 수. 숫자를 적지 않고 <b>센다</b> —
        /// 카테고리를 지우거나 더할 때 여기와 표가 어긋나면 빈 제목줄이 남거나 한 칸이 사라진다
        /// (2026-08-30 표정 삭제가 정확히 그 경우였다).
        /// <para>카드 페이지가 아닌 탭은 <b>0</b>이다 — 예전에는 [보관함]에 물으면 4가 나왔고,
        /// 부르는 쪽이 각자 <c>_tab == Tab.Inventory</c>로 걸러야 했다.</para></summary>
        private static int SectionCountForTab(Tab tab)
        {
            TabDef def = Def(tab);
            if (def.Page != TabPage.Cards) return 0;

            int n = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsAppearanceSlot((EquipmentSlot)i) == def.AppearanceSlots) n++;
            }
            return Mathf.Min(n, SectionCount);
        }

        private static int IconSetForTab(Tab tab) => Def(tab).IconSet;

        /// <summary>
        /// 이 섹션 자리가 <b>모든 카드 탭을 통틀어</b> 최대 몇 장의 카드를 필요로 하는가.
        /// 카드는 탭을 바꿔도 다시 굽지 않는 재사용 자원이므로(클래스 문서), 한 섹션의 카드 풀은
        /// 카드 탭들 중 <b>가장 많은 쪽</b>에 맞춘다.
        /// <para>숫자를 적지 않고 <see cref="ItemCatalog"/>에서 <b>센다</b> — 아이템 에셋을 늘리는 것만으로
        /// 카드가 따라 늘어나야 원칙 4("신규 콘텐츠는 기본 로직 무수정")가 실제로 성립한다.
        /// 같은 이유로 탭도 <see cref="TabTable"/>을 돌며 센다 — 세 번째 카드 탭이 생겼을 때
        /// 여기에 <c>if</c>를 하나 더 적는 것을 잊으면 그 탭의 다섯 번째 카드부터 사라진다.</para>
        /// </summary>
        private static int CardsInSection(int section)
        {
            int n = 0;
            for (int t = 0; t < TabTable.Length; t++)
            {
                if (TabTable[t].Page != TabPage.Cards) continue;
                var tab = (Tab)t;
                if (section >= SectionCountForTab(tab)) continue;
                n = Mathf.Max(n, ItemCatalog.ItemCountIn(SectionSlot(tab, section)));
            }
            return n;
        }

        private void RefreshCards()
        {
            if (Def(_tab).Page != TabPage.Cards) return;   // 카드가 없는 탭에는 그릴 것이 없다.
            int set = IconSetForTab(_tab);

            int visible = SectionCountForTab(_tab);
            for (int s = 0; s < SectionCount; s++)
            {
                SectionView view = _sections[s];
                if (view == null) continue;
                if (view.Root != null && view.Root.activeSelf != (s < visible)) view.Root.SetActive(s < visible);
                if (s >= visible) continue;

                EquipmentSlot slot = SectionSlot(_tab, s);
                Color tint = UiChrome.CategoryTint(slot);
                view.Dot.color = tint;
                view.Title.text = EquipmentModel.SlotName(slot);
                view.Code.text = EquipmentModel.SlotCode(slot);
                view.Count.text = $"{EquipmentModel.OwnedItemCount(slot)} / {EquipmentModel.ItemCount(slot)}";

                // 카테고리가 바뀌었으면 캐러셀을 처음으로 되돌린다 — 아이템이 적은 카테고리로
                // 넘어갔을 때 스크롤이 남아 있으면 <b>빈 자리</b>가 보인다.
                if (!view.HasBoundSlot || view.BoundSlot != slot)
                {
                    view.HasBoundSlot = true;
                    view.BoundSlot = slot;
                    ResetCarousel(view);
                }

                int items = ItemCatalog.ItemCountIn(slot);
                for (int c = 0; c < view.CardCount; c++)
                {
                    ItemCard card = _cards[view.FirstCard + c];
                    if (card == null) continue;

                    bool used = c < items;
                    if (card.Rect.gameObject.activeSelf != used) card.Rect.gameObject.SetActive(used);
                    if (!used) continue;
                    ApplyCardStyle(card, slot, c, set);
                }

                // 활성 카드 수가 바뀌면 가로 폭이 달라진다 — 다음 캔버스 갱신까지 기다리면
                // 그 한 프레임 동안 스크롤 한계가 옛 값이라 끝까지 밀리지 않는다.
                if (view.Content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(view.Content);
            }
        }

        private static void ResetCarousel(SectionView view)
        {
            if (view == null || view.Content == null) return;
            Vector2 p = view.Content.anchoredPosition;
            if (Mathf.Approximately(p.x, 0f)) return;
            p.x = 0f;
            view.Content.anchoredPosition = p;
        }

        /// <summary>33-7-3 카드 상태 5종 스타일 표를 그대로 옮긴 유일한 자리.</summary>
        private void ApplyCardStyle(ItemCard card, EquipmentSlot slot, int itemIndex, int iconSet)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            for (int i = 0; i < IconSetCount; i++)
            {
                if (card.IconRoot[i] != null) card.IconRoot[i].gameObject.SetActive(i == iconSet);
            }
            if (entry == null) return;

            bool owned = entry.IsOwned(_config);
            bool worn = entry.IsEquipped();
            bool selected = slot == _selectedSlot && itemIndex == _selectedItem;
            bool hovered = _hoveredCard >= 0 && _cards[_hoveredCard] == card;
            Color tint = UiChrome.CategoryTint(slot);

            // 이름은 상자(70pt)를 넘으면 말줄임한다 — Overflow로 흘리면 오른쪽 메타("착용 중")와
            // 물리적으로 겹친다(P0-5). 내용이 바뀐 순간에만 다시 계산한다(ItemCard.NameSource 문서).
            string wantedName = owned ? entry.DisplayName : "???";
            if (!string.Equals(card.NameSource, wantedName, System.StringComparison.Ordinal))
            {
                card.NameSource = wantedName;
                card.Name.text = UiChrome.Ellipsize(card.Name, wantedName, CardNameWidth);
            }
            card.Name.color = UiChrome.InkTitle(owned);

            if (!owned)
            {
                // "LV.20" — 잠긴 카드의 메타는 <b>언제 열리는지</b> 하나만 말한다.
                card.Meta.text = $"LV.{entry.RequiredLevel}";
                card.Meta.color = UiChrome.InkMeta;
                card.Surface.color = UiChrome.CardSurfaceMuted;
                card.Thumb.color = UiChrome.ThumbSurfaceLocked;
                // 잠김 = <b>무채색 실루엣</b>. 해금 전에 소재색을 미리 보여주면 잠금 연출이 무의미해진다.
                SetIconColor(card, iconSet, new Color(UiChrome.TextTertiary.r, UiChrome.TextTertiary.g,
                    UiChrome.TextTertiary.b, 0.34f));
            }
            else
            {
                card.Meta.text = worn ? "착용 중" : "보유";
                card.Meta.color = worn ? tint : UiChrome.InkMeta;
                card.Surface.color = UiChrome.CardSurface;
                // 착용 중 썸네일 바탕은 <b>카테고리 틴트가 아니라 강조색 wash</b>다(2026-08-30).
                // 같은 라운드에 아이템별 소재색이 들어오면서, 카테고리 틴트를 그대로 깔면 그 카테고리의
                // 틴트를 쓰는 아이콘(나비넥타이=초록, 짧은망토=보라, 발자국=초록)이 <b>제 배경색과
                // 같은 색</b>이 되어 형태가 사라진다. 착용 테두리(CardBorderWorn)도 이미 강조색이므로
                // 바탕도 같은 계열로 맞추는 편이 "지금 걸치고 있는 칸"이라는 신호가 하나로 읽힌다.
                // 카테고리는 섹션 헤더의 틴트 도트와 슬롯 코드가 이미 말하고 있다.
                // ★ 2026-08-31 — wash를 <b>미리 합성한 불투명색</b>으로 넣는다. AccentSurface(α0.14)를
                //   그대로 칠하면 이 119x62pt 썸네일 위에서만 창 알파가 0.88로 내려가 <b>착용 중인 칸에만</b>
                //   뒤 창이 12% 비친다(UiChrome '알파 채널의 법칙'). 아래에 있는 것은 항상 불투명한
                //   CardSurface이므로 합성 결과 색은 완전히 같다.
                card.Thumb.color = worn ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface)
                    : UiChrome.CardSurfaceMuted;
                // 해금됐으면 <b>아이템 고유의 소재색</b>으로 되돌린다(2026-08-30). 예전에는 착용 여부에 따라
                // 아이콘 전체를 카테고리 틴트/잉크 한 색으로 덮어써서 32칸이 전부 같은 색으로 보였다.
                // "착용 중"은 이미 테두리(CardBorderWorn) + 썸네일 wash + 메타 문구 셋이 말하고 있다.
                RestoreIconColors(card, iconSet);
            }

            // 테두리 우선순위: 선택 > hover > 착용 중 > 기본. (스펙 1.4 표의 "선택됨이 최우선")
            card.Outline.color = selected ? UiChrome.TextPrimary
                : hovered ? UiChrome.CardBorderHover
                : worn && owned ? UiChrome.CardBorderWorn
                : UiChrome.CardBorder;

            if (card.LockBadge != null) card.LockBadge.gameObject.SetActive(!owned);

            // 카드 하단 버튼 — 이 창의 유일한 착용 손잡이(상세 패널은 읽기 전용이다).
            StyleActionButton(card.ActionSurface, card.ActionOutline, card.ActionLabel, card.ActionButton, owned, worn);
        }

        /// <summary>
        /// [착용]/[해제] 버튼 한 벌의 스타일 — 33-7-4의 상태 표. 상태→라벨/색 매핑이 존재하는
        /// <b>유일한 자리</b>다.
        ///
        /// <para>★ 2026-09-01 <b>강조 등급 재조정</b> — 예전에는 이 함수가 두 자리(카드 하단 / 상세 패널)를
        /// 강조 등급으로 나눠 칠했다. 사용자 신고("각 장비별 착용버튼으로 했는데 왜 옛날처럼 하단에
        /// 착용상자가 따로 있음?")로 <b>상세 패널의 중복 버튼을 걷어내면서</b> 자리가 하나로 줄었고,
        /// 등급 파라미터도 함께 지웠다 — 분기 하나짜리 등급은 다음 사람에게 "두 자리가 있다"는 거짓말이 된다.
        /// 이제 <b>카드 버튼이 이 창의 1차 행동</b>이다.</para>
        ///
        /// <para>★ 흰 채움으로 되돌리지 <b>않는다</b> — P0-4가 실측으로 걷어낸 이유가 살아 있다:
        /// 한 화면에 이 막대가 12개 뜨는데 유저가 고르는 대상은 <i>아이템</i>이다. <b>1차 행동이라는 것은
        /// 경쟁자가 없다는 뜻이지 가장 밝아야 한다는 뜻이 아니다.</b></para>
        ///
        /// <para>★★ <b>2026-09-02 — 그렇다고 이대로 둘 수도 없었다.</b> "조용한 칩"이 조용한 정도를
        /// 넘어 <b>면이 아예 없는</b> 지점까지 가 있었다(실측, 각 상태의 <b>진짜</b> 바탕 기준):
        /// <code>
        ///   착용 #32353C on #1B1F26 = 1.35 : 1      글리프 11.14 : 1
        ///   해제 #243143 on #1B1F26 = 1.26 : 1      글리프  7.16 : 1
        ///   잠김 #15181E on #15181E = <b>1.00 : 1</b>      글리프  5.73 : 1
        /// </code>
        /// 글자는 셋 다 잘 읽혔다 — <b>고칠 것은 잉크가 아니라 면</b>이다([✕]와 같은 결함, 같은 처방).
        /// 잠김이 1.00인 것은 잠긴 카드의 <b>바탕 자체</b>가 CardSurfaceMuted로 바뀌기 때문이다.</para>
        ///
        /// <para><b>어둡게 해서 구분할 수는 없다</b> — 카드 바탕이 이미 어두워 순검정까지 내려가도
        /// 최대 1.27:1이다. 3.0은 아래쪽에 존재하지 않는다. 그래서 면은 반드시 밝아지고, 두 활성
        /// 상태는 밝기가 아니라 <b>색상</b>으로 갈린다(<see cref="UiChrome.CardActionSurface"/> /
        /// <see cref="UiChrome.CardActionSurfaceWorn"/>, 각각 4.49 / 4.48 : 1).</para>
        ///
        /// <para><b>P0-4 가드는 그대로 통과한다</b>(이게 핵심이다): 새 두 면의 휘도는 0.2355 / 0.2349로,
        /// 흰 채움과 카드 바탕의 중간값 0.4584의 <b>절반</b>이다. 접근성 하한을 넘기면서도 카드에서
        /// 가장 밝은 것은 여전히 아이템 쪽이다.</para>
        ///
        /// <para>색은 전부 불투명값이다 — 투명 오버레이에서 알파를 겹치면 그 자리만 뒤 창이 비친다
        /// (UiChrome '알파 채널의 법칙'). 테두리도 생 <c>CardBorder</c>가 아니라
        /// <see cref="UiChrome.Flatten"/>을 거친다.</para>
        /// </summary>
        private static void StyleActionButton(Image surface, Image outline, Text label, Button button, bool owned, bool worn)
        {
            // ★ 잠긴 칩은 <b>실제로</b> 비활성이다 — 클릭은 예전부터 무시됐다(OnActionClicked).
            //   이 한 줄이 있어야 WCAG 2.2 1.4.11의 "inactive user interface components" 면제를
            //   정당하게 받는다. 없으면 그 칩은 1.00:1짜리 <b>활성</b> 컨트롤로 남는다.
            if (button != null) button.interactable = owned;

            // ★ 2026-09-02 — <b>면</b>을 고친다. 잉크는 멀쩡했다(11.14 / 7.16 / 5.73:1).
            //   고치기 전 면은 1.35 / 1.26 / <b>1.00</b> : 1 이었고, 셋 다 자체 하한 3.0 미달이다.
            //   특히 잠김은 칩과 카드 바탕이 <b>같은 RGB</b>였다 — 오늘 밤 [✕](1.00:1)와 같은 결함이다.
            //   면을 먼저 정하고 잉크를 그 면에서 <b>파생</b>시킨다. 순서가 뒤집히면 둘이 갈라진다.
            Color face = !owned ? UiChrome.CardSurfaceMuted
                : worn ? UiChrome.CardActionSurfaceWorn
                       : UiChrome.CardActionSurface;

            if (surface != null) surface.color = face;

            if (label != null)
            {
                // 잠긴 카드에 "LV.20"이라고 적지 않는다 — 바로 위 메타 줄이 이미 그 숫자를 말하고 있다.
                label.text = !owned ? "잠김" : worn ? "해제" : "착용";
                // 면에서 파생 — 밝은 면 위에서는 InkOnSurface가 알아서 어두운 잉크로 뒤집는다.
                label.color = UiChrome.InkOnSurface(face,
                    owned ? UiChrome.InkRole.Title : UiChrome.InkRole.Meta, enabled: owned);
            }
            if (outline != null)
            {
                // ★ 생 CardBorder/AccentBorder(α<1)를 그대로 얹지 않는다 — 그 화소의 창 알파가
                //   0.91로 내려가 <b>유저의 바탕화면이 9% 비친다</b>(어두운 배경일수록 더 안 보였다).
                //   Flatten이 겉보기 색을 그대로 두고 α=1만 보장한다.
                outline.color = UiChrome.Flatten(
                    !owned ? UiChrome.CardBorder : worn ? UiChrome.AccentBorder : UiChrome.CardBorder,
                    face);
            }
        }

        /// <summary>조각별 원래 소재색으로 되돌린다.</summary>
        private static void RestoreIconColors(ItemCard card, int iconSet)
        {
            Image[] graphics = card.IconGraphics[iconSet];
            Color[] baseColors = card.IconBaseColors[iconSet];
            if (graphics == null || baseColors == null) return;
            int count = Mathf.Min(graphics.Length, baseColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (graphics[i] != null) graphics[i].color = baseColors[i];
            }
        }

        private static void SetIconColor(ItemCard card, int iconSet, Color color)
        {
            Image[] graphics = card.IconGraphics[iconSet];
            if (graphics == null) return;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].color = color;
            }
        }

        /// <summary>선택 상세 패널(33-7-4). <b>읽기 전용</b>이다 — 이름·슬롯/보유 상태·설명, 그리고
        /// 잠겼다면 <b>왜 잠겼는지</b>를 말하는 것이 전부고, 옷을 갈아입히는 것은 카드 하단 버튼이 한다.
        ///
        /// <para>★ 2026-09-01 — 여기 있던 [착용]/[해제] 버튼을 걷어냈다(사용자 신고: "각 장비별
        /// 착용버튼으로 했는데 왜 옛날처럼 하단에 착용상자가 따로 있음?"). 카드 버튼을 넣으면서 이쪽을
        /// 안 걷어내 <b>같은 동작을 하는 버튼이 두 개</b>였다.</para>
        ///
        /// <para>★ <b>패널 자체는 남긴다.</b> 잠긴 아이템도 선택은 되고, 이 패널이 "왜 잠겼는지"를 알 수
        /// 있는 <b>유일한</b> 경로다 — 카드에는 이름(<c>???</c>)과 요구 레벨 숫자뿐이고 설명문이 없다.
        /// 버튼이 사라진 자리에는 아무것도 채우지 않는다(메타 줄은 172..502pt라 원래 닿지 않던 칸이다).</para>
        /// </summary>
        private void RefreshDetail()
        {
            if (Def(_tab).Page != TabPage.Cards) return;
            ItemCatalogEntry entry = ItemCatalog.Item(_selectedSlot, _selectedItem);
            if (entry == null) return;

            bool owned = entry.IsOwned(_config);
            bool worn = entry.IsEquipped();

            if (_detailName != null)
            {
                _detailName.text = owned ? entry.DisplayName : "???";
                _detailName.color = UiChrome.InkTitle(owned);
            }
            if (_detailMeta != null)
            {
                _detailMeta.text = !owned
                    ? $"{entry.CategoryLabel}  ·  Lv.{entry.RequiredLevel}에 열림"
                    : $"{entry.CategoryLabel}  ·  {(worn ? "착용 중" : "보유 중")}";
            }
            if (_detailBody != null)
            {
                _detailBody.text = owned
                    ? entry.Description
                    : $"레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다.";
                _detailBody.color = UiChrome.InkBody(owned);
            }
        }

        /// <summary>카드 <b>본체</b> 클릭 = <b>선택</b>(아래 상세 패널이 그 아이템을 설명한다).
        /// 착용/해제는 <b>그 카드 하단의 버튼</b>만 한다 — "고른다"와 "입는다"를 같은 클릭에 겹치면,
        /// 설명을 읽으려고 눌렀을 뿐인데 옷이 갈아입혀진다.
        /// <para>2026-09-01 이전에는 이 자리에 "착용은 상세 패널의 버튼 하나로만"이라고 적혀 있었다.
        /// 그 버튼은 카드 버튼이 들어온 뒤로 중복이었고 지금은 없다.</para></summary>
        private void OnCardClicked(int cardIndex)
        {
            ItemCard card = CardAt(cardIndex);
            if (card == null) return;
            EquipmentSlot slot = SectionSlot(_tab, card.Section);
            int item = card.Item;
            if (_selectedSlot == slot && _selectedItem == item) return;

            _selectedSlot = slot;
            _selectedItem = item;
            RefreshCards();
            RefreshDetail();
            Debug.Log($"[{Def(_tab).Name}] 선택 -> {EquipmentModel.ItemName(slot, item)}({EquipmentModel.SlotName(slot)}).");
        }

        /// <summary>
        /// ★ 카드 하단 [착용]/[해제] — 2026-09-01 사용자 요청("착용 버튼을 각 장비 하단에").
        ///
        /// <para><b>같은 카테고리 안의 상호배타</b>는 여기서 새로 만들지 않는다. 착용 상태가
        /// <c>EquipmentModel</c>의 <b>카테고리당 정수 한 칸</b>이라 모자 하나를 걸치면 그 칸이
        /// 덮어써지고 앞의 모자는 <b>구조적으로</b> 벗겨진다 — 이 버튼은 그 기존 경로
        /// (<see cref="EquipmentModel.ToggleItem"/> -> 저장 -> 이벤트)를 그대로 탈 뿐이다.
        /// 여기에 "다른 것을 벗긴다"는 코드를 한 줄이라도 더 쓰면 규칙이 두 곳에 생긴다.</para>
        ///
        /// <para>고르기(카드 본체 클릭)와 입기(이 버튼)를 나눈 이유는 두 가지다: 설명을 읽으려고 눌렀을
        /// 뿐인데 옷이 갈아입혀지는 것을 막고, <b>캐러셀을 밀다가</b> 착용되는 것을 막는다.</para>
        /// </summary>
        private void OnCardEquipClicked(int cardIndex)
        {
            ItemCard card = CardAt(cardIndex);
            if (card == null) return;
            EquipmentSlot slot = SectionSlot(_tab, card.Section);

            // 선택도 이 카드로 옮긴다 — 버튼을 눌렀는데 아래 상세 패널이 다른 아이템을 설명하고 있으면
            // 화면이 두 가지를 동시에 말하게 된다.
            _selectedSlot = slot;
            _selectedItem = card.Item;
            OnActionClicked();
        }

        private ItemCard CardAt(int index)
            => index >= 0 && index < _cards.Length ? _cards[index] : null;

        /// <summary>착용/해제를 <b>실제로 수행</b>하는 단 하나의 자리. 진입점은
        /// <see cref="OnCardEquipClicked"/> 하나뿐이다(상세 패널의 중복 버튼은 2026-09-01에 걷어냈다).
        /// 선택 상태(<c>_selectedSlot</c>/<c>_selectedItem</c>)를 읽으므로 호출 전에 그 둘이 대상 아이템을
        /// 가리키고 있어야 한다.</summary>
        private void OnActionClicked()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(_selectedSlot, _selectedItem);
            if (entry == null) return;

            if (!entry.IsOwned(_config))
            {
                // 33-7-4: 잠긴 항목은 버튼 클릭만 무시한다(선택은 되고 설명도 보인다).
                Debug.Log($"[{Def(_tab).Name}] {entry.DisplayName}{KoreanParticle.Topic(entry.DisplayName)} 아직 잠겨 있습니다 — " +
                    $"Lv.{entry.RequiredLevel}에서 열립니다(현재 Lv.{CharacterProgressionModel.Level}).");
                return;
            }

            // ★ 2026-09-01(페르소나 소은 #4-a) — 사건은 <b>둘</b>인데 서술이 하나였다: 같은 카테고리의
            //   앞 아이템이 자동으로 벗겨지는데 로그는 "털모자 착용"만 말했다. 화면에서는 강조가 옆
            //   카드로 옮겨가는 것이 보이지만, 그 카드가 <b>캐러셀 밖</b>이면 피드백이 0이라 이 한
            //   조각이 유일한 단서가 된다. 벗겨진 쪽은 토글 <b>전에</b>만 알 수 있다.
            int replacedItem = EquipmentModel.WornIndex(_selectedSlot);
            if (!EquipmentModel.ToggleItem(_selectedSlot, _selectedItem, _config)) return;

            bool nowWorn = entry.IsEquipped();
            ItemCatalogEntry replaced = nowWorn && replacedItem != EquipmentModel.NotWorn
                && replacedItem != _selectedItem ? ItemCatalog.Item(_selectedSlot, replacedItem) : null;
            Debug.Log($"[{Def(_tab).Name}] {entry.DisplayName} {(nowWorn ? "착용" : "해제")}" +
                (replaced != null
                    ? $"(같은 카테고리의 {replaced.DisplayName}{KoreanParticle.Topic(replaced.DisplayName)} 자동 해제)"
                    : string.Empty) +
                " — 초상화와 캐릭터에 즉시 반영, 즉시 저장.");
            CharacterSaveStore.Save(); // "모든 토글은 즉시 반영(별도 저장 버튼 없음)".
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
        }

        // -------------------- 카테고리 섹션 페이지([장비]/[외형] 공용) --------------------

        private void BuildSectionPage(RectTransform right)
        {
            var pageGo = new GameObject("SectionPage", typeof(RectTransform));
            pageGo.transform.SetParent(right, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, RightWidth, BodyHeight);
            _sectionPage = pageGo;

            // 카드 총량은 카탈로그가 정한다 — 빌드 때 한 번만 세고, 그 뒤로는 배열이 고정된다.
            var cards = new System.Collections.Generic.List<ItemCard>(SectionCount * 6);

            for (int s = 0; s < SectionCount; s++)
            {
                var sectionGo = new GameObject("Section" + s, typeof(RectTransform));
                sectionGo.transform.SetParent(page, false);
                var section = sectionGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(section, RightPadX, SectionsTopY - s * SectionStep,
                    RightContentWidth, SectionHeight);

                Image dot = UiChrome.AddSurface(section, "Dot", UiChrome.Accent, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, 0f, -6f, 7f, 7f);
                dot.raycastTarget = false;

                Text title = Label(section, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    15f, -2f, 70f, 14f, "—", bold: true);
                Text code = Label(section, "Code", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.InkMeta,
                    90f, -3f, 46f, 12f, "—");

                Image divider = UiChrome.AddSurface(section, "Divider", UiChrome.Divider, 2);
                // 폭은 <b>열에서 파생</b>된다 — 예전의 142/402/548/44는 폭 880 시절의 592 열에 박힌
                // 숫자였고, 창이 1042로 넓어졌을 때 헤더만 그 자리에 남아 카드줄과 끝선이 갈라졌다.
                UiChrome.PlaceTopLeft(divider.rectTransform, SectionDividerX, -9f, SectionDividerWidth, 1f);
                divider.raycastTarget = false;

                // ★ 이 카운터의 오른쪽 끝이 이 창 오른쪽 열의 <b>끝선</b>이다(카드줄이 여기에 맞춘다 —
                //   InfoWindowCardRowEdgeTests가 두 사각형의 xMax를 직접 비교해 잠근다).
                Text count = Label(section, "Count", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                    SectionCountX, -3f, SectionCountWidth, 12f, "0 / 4");

                var view = new SectionView
                {
                    Root = sectionGo, Dot = dot, Title = title, Code = code, Count = count,
                };
                _sections[s] = view;

                RectTransform content = BuildCardRow(view, section);

                view.FirstCard = cards.Count;
                view.CardCount = CardsInSection(s);
                for (int c = 0; c < view.CardCount; c++)
                {
                    cards.Add(BuildCard(content, s, c, cards.Count));
                }
            }

            _cards = cards.ToArray();
            BuildDetailPanel(page);
        }

        /// <summary>
        /// ★ 가로 카드 캐러셀 한 줄 — 2026-09-01 사용자 요청("마우스로 잡고 밀면 카드들이 넘어가는 형태").
        ///
        /// <para>포인터 이벤트를 손으로 짜지 않는다. <see cref="ScrollRect"/>가 드래그·클램프·휠을 이미
        /// 갖고 있고, 배치는 <see cref="HorizontalLayoutGroup"/>이, 폭은 <see cref="ContentSizeFitter"/>가,
        /// 잘라내기는 <see cref="RectMask2D"/>가 한다. 이 파일이 새로 만드는 것은 <b>하나도 없다</b>.</para>
        ///
        /// <para><b>관성(inertia)을 끄고 Clamped로 두는 이유</b>는 취향이 아니다 — 전역 폴링 드래그와
        /// 계산이 <b>같아야</b> 두 경로가 동시에 돌아도 결과가 어긋나지 않는다(<see cref="DragCarouselTo"/> 문단).</para>
        ///
        /// <para>뷰포트에 <b>투명한 Image</b>를 깔아 두는 이유: 카드 사이 9pt 틈을 잡아도 끌리게 하기
        /// 위해서다. 그 자리에 그래픽이 없으면 uGUI 레이캐스트가 통과해 창 바탕이 잡히고, 사용자에게는
        /// "여기는 안 밀리네"로 보인다.</para>
        /// </summary>
        private static RectTransform BuildCardRow(SectionView view, RectTransform section)
        {
            var rowGo = new GameObject("CardRow", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            rowGo.transform.SetParent(section, false);
            var row = rowGo.GetComponent<RectTransform>();
            // 폭은 섹션과 <b>같다</b>(CarouselViewportWidth = RightContentWidth). 마지막 카드는 그 끝선에
            // 걸려 반쯤 잘리고, 그 걸침이 이 창의 유일한 "더 있다" 단서다 — 그 상수 문서 참고.
            UiChrome.PlaceTopLeft(row, 0f, CardTopInSection, CarouselViewportWidth, CardHeight);

            var handle = rowGo.GetComponent<Image>();
            handle.color = Color.clear;
            handle.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(row, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.Stretch(viewport);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = new Vector2(0f, CardHeight);
            content.anchoredPosition = Vector2.zero;

            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = CardGap;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;    // 카드는 자기 폭(141)을 지킨다 — 개수로 늘어나는 것은 줄이다.
            layout.childControlHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = rowGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = CardStep * 0.5f;
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;

            view.Row = scroll;
            view.RowRect = row;
            view.Content = content;
            return content;
        }

        private ItemCard BuildCard(RectTransform content, int sectionIndex, int columnIndex, int cardIndex)
        {
            Image surface = UiChrome.AddSurface(content, "Card" + cardIndex, UiChrome.CardSurface, UiChrome.RadiusCard);
            var rt = surface.rectTransform;
            // x는 HorizontalLayoutGroup이 정한다 — 여기서는 <b>크기와 피벗</b>만 맞춰 준다.
            UiChrome.PlaceTopLeft(rt, 0f, 0f, CardWidth, CardHeight);
            Image outline = UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            Image thumb = UiChrome.AddSurface(rt, "Thumb", UiChrome.CardSurfaceMuted, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(thumb.rectTransform, ThumbX, ThumbY, ThumbWidth, ThumbHeight);
            thumb.raycastTarget = false;

            var card = new ItemCard
            {
                Section = sectionIndex,
                Item = columnIndex,
                Rect = rt,
                Surface = surface,
                Outline = outline,
                Thumb = thumb,
                Name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    ThumbX, CardNameY, CardNameWidth, CardTextHeight, "—"),
                Meta = Label(rt, "Meta", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.InkMeta,
                    CardMetaX, CardNameY, CardMetaWidth, CardTextHeight, "—"),
            };

            // ---- 카드 하단 [착용]/[해제] ---- 이 창의 <b>유일한</b> 착용 손잡이다(상세 패널의
            //   중복 버튼은 2026-09-01 사용자 신고로 걷어냈다). 1차 행동이지만 P0-4의 조용한 칩을
            //   그대로 유지한다 — 이유는 StyleActionButton 문서 참고(한 화면에 12개가 반복된다).
            card.ActionSurface = UiChrome.AddSurface(rt, "Action",
                UiChrome.CardActionSurface, UiChrome.RadiusChip);
            card.ActionRect = card.ActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(card.ActionRect, ThumbX, CardActionY, CardActionWidth, CardActionHeight);
            card.ActionOutline = UiChrome.AddOutline(card.ActionRect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardActionSurface), UiChrome.RadiusChip);
            card.ActionLabel = UiChrome.AddText(card.ActionRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.CardActionSurface, UiChrome.InkRole.Title, enabled: true),
                bold: true);
            UiChrome.Stretch(card.ActionLabel.rectTransform);
            card.ActionLabel.text = "착용";

            var actionButton = card.ActionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = card.ActionSurface;
            // ★ Unity 기본 ColorTint는 pressed에 ×0.7843137을 곱한다. 새 면은 <b>밝아서</b> 그 곱이
            //   어두운 잉크(#0B1016)와의 대비를 무너뜨린다 — 실측:
            //     착용 #838589 5.19:1 → pressed #67696C <b>3.45:1</b>
            //     해제 #5087CC 5.18:1 → pressed #3F6AA0 <b>3.44:1</b>
            //   즉 <b>누르고 있는 동안 글자가 AA 미달</b>이 된다. 상태 색은 StyleActionButton이 값으로
            //   정하므로 uGUI의 자동 틴트는 꺼 둔다([✕]와 같은 처방).
            actionButton.transition = Selectable.Transition.None;
            card.ActionButton = actionButton;
            actionButton.onClick.AddListener(() =>
            {
                if (SuppressedByCarousel()) return;   // 방금 민 손짓의 끝을 클릭으로 오인하지 않는다.
                if (TryClaimAction("equip" + cardIndex)) OnCardEquipClicked(cardIndex);
            });

            // [장비]용/[외형]용 아이콘을 미리 두 벌 굽는다(클래스 문서 "탭을 바꿔도 다시 굽지 않는다").
            for (int set = 0; set < IconSetCount; set++)
            {
                Tab tab = TabForIconSet(set);
                // 이 탭에 없는 섹션(=[외형]의 4번째)에는 구울 것이 없다. 예전에는 SectionSlot의
                // 폴백(Head)이 돌아와 <b>모자 아이콘</b>을 몰래 한 벌 더 굽고 있었다.
                bool inThisTab = sectionIndex < SectionCountForTab(tab);
                EquipmentSlot slot = inThisTab ? SectionSlot(tab, sectionIndex) : EquipmentSlot.Head;
                ItemCatalogEntry entry = inThisTab ? ItemCatalog.Item(slot, columnIndex) : null;

                var iconGo = new GameObject("Icon" + set, typeof(RectTransform));
                iconGo.transform.SetParent(thumb.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(IconSize, IconSize);
                irt.anchoredPosition = Vector2.zero;

                if (entry != null) BuildCardArt(irt, slot, columnIndex, entry);
                card.IconRoot[set] = irt;
                Image[] graphics = iconGo.GetComponentsInChildren<Image>(true);
                card.IconGraphics[set] = graphics;
                var baseColors = new Color[graphics.Length];
                for (int g = 0; g < graphics.Length; g++)
                {
                    baseColors[g] = graphics[g] != null ? graphics[g].color : UiChrome.IconInk;
                }
                card.IconBaseColors[set] = baseColors;
            }

            // 자물쇠 배지 — 썸네일 우하단에 살짝 걸치게(스펙 right −4 / bottom −3).
            Image badge = UiChrome.AddSurface(thumb.rectTransform, "LockBadge", UiChrome.ThumbSurfaceLocked, UiChrome.RadiusBadge);
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 0f);
            brt.sizeDelta = new Vector2(LockBadgeWidth, LockBadgeHeight);
            brt.anchoredPosition = new Vector2(4f, -3f);
            badge.raycastTarget = false;
            BuildLockGlyph(brt);
            card.LockBadge = brt;
            card.LockBadge.gameObject.SetActive(false);

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() =>
            {
                if (SuppressedByCarousel()) return;
                if (TryClaimAction("card" + cardIndex)) OnCardClicked(cardIndex);
            });
            return card;
        }

        /// <summary>40×40 viewBox(y가 아래로) -> 부모 중심 기준 화면 좌표(y가 위로).</summary>
        private static Vector2 FromViewBox(float x, float y, float viewWidth, float viewHeight,
            float renderWidth, float renderHeight)
        {
            return new Vector2(
                (x - viewWidth * 0.5f) * (renderWidth / viewWidth),
                (viewHeight * 0.5f - y) * (renderHeight / viewHeight));
        }

        /// <summary>
        /// ★ 2026-09-01 (로드맵 P0-a) — 카드 썸네일을 <b>몸에 붙는 것과 같은 도형</b>으로 그린다.
        ///
        /// <para>지금까지 한 아이템은 그림을 두 벌 갖고 있었다: 카드는 손으로 배치한 40×40 SVG
        /// (<see cref="ItemCatalogEntry.Icon"/>), 몸은 절차적 계산(<see cref="AccessoryShapeBuilder"/>).
        /// 그래서 도형을 고칠 때마다 카드만 옛 모양으로 남았다 — 이번 라운드에 머리 4종을 다시 그리므로,
        /// 통합하지 않으면 사용자가 지적한 "카드와 실제가 다름"이 <b>오히려 더 심해진다</b>.</para>
        ///
        /// <para><b>폴백은 남긴다.</b> 새 경로가 도형을 못 만들면(FX/PET처럼 몸 도형이 없는 카테고리가
        /// 정상적으로 여기 해당한다) 옛 아이콘을 그대로 그린다. 즉 새 경로가 통째로 틀려도 카드가
        /// 비지 않는다 — <see cref="AccessoryDefSO.icon"/>을 이번에 지우지 않은 이유가 이것이다.</para>
        /// </summary>
        private static void BuildCardArt(RectTransform root, EquipmentSlot slot, int itemIndex,
            ItemCatalogEntry entry)
        {
            // 색은 <b>카탈로그 색 그대로</b>다(몸의 WornColor 변환을 태우지 않는다). 착용 색 정책은
            // 로드맵 P5의 몫이고, 도형 통합과 색 정책을 한 라운드에 같이 바꾸면 카드 그림이 달라진
            // 이유가 좌표 때문인지 색 때문인지 판정할 수 없게 된다.
            if (AccessoryCardIcon.TryBuild(root, slot, itemIndex, IconSize, IconStroke,
                    entry.PrimaryColor, entry.SecondaryColor))
            {
                return;
            }
            BuildIcon(root, entry.Icon);
        }

        private static void BuildIcon(RectTransform root, ItemIconPart[] parts)
        {
            if (parts == null) return;
            for (int p = 0; p < parts.Length; p++)
            {
                ItemIconPart part = parts[p];
                float[] v = part.Values;
                if (v == null) continue;

                switch (part.Kind)
                {
                    case ItemIconPartKind.Polyline:
                    {
                        int count = Mathf.Min(part.PointCount, _iconPoints.Length);
                        for (int i = 0; i < count; i++)
                        {
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, IconSize, IconSize);
                        }
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, IconStroke, part.Color);
                        break;
                    }
                    case ItemIconPartKind.Polygon:
                    {
                        // 몸 경로(AccessoryCardIcon)와 <b>같은 순서</b>로 그린다: 면을 먼저 깔고 그 위에
                        // 윤곽선. 순서를 바꾸면 채움이 획을 반쯤 덮어 도형이 가늘어 보인다.
                        int count = Mathf.Min(part.PointCount, _iconPoints.Length);
                        for (int i = 0; i < count; i++)
                        {
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, IconSize, IconSize);
                        }

                        // 규약상 마지막 점이 첫 점과 같다. 삼각분할에 중복점을 넣으면 퇴화 삼각형이 생긴다.
                        int fillCount = count;
                        if (fillCount > 1 && _iconPoints[fillCount - 1] == _iconPoints[0]) fillCount--;

                        AccessoryCardIcon.AddFill(root, "Fill", _iconPoints, fillCount, part.Color);
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, IconStroke,
                            AccessoryShapeBuilder.FillOutlineColor(part.Color));
                        break;
                    }
                    case ItemIconPartKind.Ring:
                        UiChrome.AddCircle(root, "Ring", v[2] * 2f * IconScale, part.Color, IconStroke,
                            FromViewBox(v[0], v[1], 40f, 40f, IconSize, IconSize));
                        break;
                    case ItemIconPartKind.DashedRing:
                        BuildDashedRing(root, v[0], v[1], v[2], part.Color);
                        break;
                    case ItemIconPartKind.Dot:
                        UiChrome.AddCircle(root, "Dot", v[2] * 2f * IconScale, part.Color, 0f,
                            FromViewBox(v[0], v[1], 40f, 40f, IconSize, IconSize));
                        break;

                    // ★ 2026-09-02 — 종류가 늘면(Polygon이 2026-09-02에 실제로 늘었다) 그 조각만
                    //   조용히 빠진다. 아이콘 한 조각이 빠진 그림은 "원래 그런 아이콘"으로 읽혀
                    //   아무도 신고하지 않는다 — 그래서 코드가 대신 신고한다.
                    default:
                        ShapeCoverageGuard.ReportUnknownIconKind(part.Kind);
                        break;
                }
            }
        }

        /// <summary>viewBox(40) -> 실제 아이콘 크기 배율. 반지름처럼 <b>길이</b>인 값은 전부 이걸 곱해야 한다
        /// (좌표는 <see cref="FromViewBox"/>가 이미 환산한다 — 반지름은 그 경로를 타지 않아 예전에는
        /// IconSize == 40이라 우연히 맞고 있었다).</summary>
        private const float IconScale = IconSize / 40f;

        /// <summary>점선 원(FX "없음" 전용). 링 스프라이트에는 점선이 없어 짧은 호 8개로 그린다.</summary>
        private static void BuildDashedRing(RectTransform root, float cx, float cy, float r, Color color)
        {
            const int dashes = 8;
            const int pointsPerDash = 3;
            Vector2 center = FromViewBox(cx, cy, 40f, 40f, IconSize, IconSize);
            float radius = r * (IconSize / 40f);

            for (int d = 0; d < dashes; d++)
            {
                float start = d * (Mathf.PI * 2f / dashes);
                for (int i = 0; i < pointsPerDash; i++)
                {
                    float a = start + (Mathf.PI / dashes) * (i / (float)(pointsPerDash - 1));
                    _iconPoints[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }
                UiChrome.AddPolyline(root, "Dash", _iconPoints, pointsPerDash, IconStroke, color);
            }
        }

        /// <summary>자물쇠 14×15(스펙 viewBox 20×21) — 채운 몸통 + 고리 호.</summary>
        private static void BuildLockGlyph(RectTransform badge)
        {
            const float viewW = 20f, viewH = 21f, renderW = 14f, renderH = 15f;

            Image bodyImage = UiChrome.AddSurface(badge, "LockBody", UiChrome.NonTextMuted, 2);
            var brt = bodyImage.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(14f * (renderW / viewW), 10f * (renderH / viewH));
            brt.anchoredPosition = FromViewBox(10f, 14.5f, viewW, viewH, renderW, renderH);
            bodyImage.raycastTarget = false;

            int count = LockShackle.Length / 2;
            for (int i = 0; i < count; i++)
            {
                _iconPoints[i] = FromViewBox(LockShackle[i * 2], LockShackle[i * 2 + 1], viewW, viewH, renderW, renderH);
            }
            UiChrome.AddPolyline(badge, "LockShackle", _iconPoints, count,
                IconStroke * (renderW / viewW), UiChrome.NonTextMuted);
        }

        private void BuildDetailPanel(RectTransform page)
        {
            Image detail = UiChrome.AddSurface(page, "Detail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            _sectionDetailRect = drt;
            UiChrome.PlaceTopLeft(drt, RightPadX, DetailYForTab(_tab), RightContentWidth, DetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            _detailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                15f, -14f, 150f, 17f, "—", bold: true);
            // ★ 2026-09-01 오후 — 폭 330(= 172..502)은 <b>오른쪽 끝의 [착용] 버튼(525..577)을 피하려고</b>
            //   정한 값이었다. 그 버튼을 걷어낸 뒤 502..577의 75pt가 아무도 쓰지 않는 칸으로 남았다.
            //   이제 설명문과 <b>같은 오른쪽 끝</b>에서 끝나게 파생시킨다 — "Lv.9에 열림"처럼 긴 잠김
            //   문구가 그만큼 덜 밀린다. 숫자 330은 사라졌다.
            const float DetailPadX = 15f;
            const float DetailMetaX = 172f;
            _detailMeta = Label(drt, "DetailMeta", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                DetailMetaX, -14f, RightContentWidth - DetailPadX - DetailMetaX, 17f, "—");   // 405

            _detailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_detailBody.rectTransform, 15f, -42f, RightContentWidth - 30f, 48f);
            _detailBody.lineSpacing = 1.6f;   // 스펙 line-height 1.6.

            // ★ 여기에 [착용]/[해제] 버튼을 다시 만들지 마라(2026-09-01 사용자 신고로 걷어냈다).
            //   착용 손잡이는 카드 하단 하나뿐이고, 이 패널은 "고른 것이 무엇이고 왜 잠겼는가"만 말한다.
            //   되살아나면 InfoWindowSurfaceRegressionTests의 DetailPanelHasNoEquipButton이 잡는다.
        }
    }
}
