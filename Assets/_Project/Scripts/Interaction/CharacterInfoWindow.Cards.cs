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
        // ====================================================================================
        // ★ 등급 리본 — 2026-09-03 (사용자 확정 「안 C1」: 카드에 등급을 넣는다)
        // ====================================================================================
        //
        // <b>주 채널은 색이 아니라 「칸 수」다.</b> 이 결정은 취향이 아니라 두 실측에서 나왔다:
        //   · design-art(<c>design/art/PALETTE_SPEC.md</c> §12-0): 브라스 램프는 인접 쌍 ΔE 15.16~18.48로
        //     <b>변별</b>은 되지만 <b>식별</b> 하한 48.6을 넘는 쌍이 「일반↔전설」 하나뿐이다.
        //     <b>카드 한 장만 보고 색으로 등급을 맞히는 것은 정상 시각에서도 안 된다.</b>
        //   · persona-newcomer: *"등급을 알아보게 만든 건 색이 아니라 칸 수였다. 세면 되니까 확실했다.
        //     밝기는 아무리 봐도 일반과 희귀가 구분이 안 갔다."*
        //
        // 그래서 이 창은 등급을 <b>세 채널</b>로 말한다(PALETTE_SPEC §12-4):
        //   주 = 칸 수(1/2/3/4)  ·  보조 = 브라스 램프 색(리본 채움 + 카드 테두리)  ·  확정 = 낱말
        //   ★ <b>낱말은 아직 상세 패널에만 있다.</b> 2026-09-03 라운드에서 낱말을 카드로 내리는
        //     안(UI_SURFACE_SPEC §15.14-d 「안 G」)이 나왔고 — 근거는 사용자가 물은 것이
        //     "나란히 놓고 다른가"(변별)가 아니라 <b>"하나만 보고 무엇인지 맞히는가"(식별)</b>이고,
        //     식별 하한 ΔE 48.6을 넘는 등급 쌍이 여섯 중 하나뿐이라 <b>문자가 유일한 식별 채널</b>이라는
        //     것이다 — <b>착수했다가 리더 판정으로 되돌렸다</b>. 낱말 자리(썸네일 좌하단 24×12)가
        //     카드 폭에 매여 있는데 <b>창 1042 안의 3열 골격이 아직 안 정해졌기 때문</b>이다.
        //     <b>골격이 확정되면 §15.14-d를 다시 적용할 것</b>(그때 배경 넷을 다 재라 — 착용 중
        //     썸네일 wash <c>#33312D</c> 위에서 「일반」이 4.44:1로 텍스트 하한 미달이고,
        //     설계 대비표는 그 배경을 빠뜨렸다).
        //
        // ★★ <b>2026-09-03 정정 — 여기 「카드 테두리에 등급을 얹지 마라」가 1번으로 적혀 있었다.</b>
        //   그 문장은 <b>사용자 지시로 폐기됐다</b>: *"리본을 저렇게 표시하면 전설인지 일반인지 잘
        //   구분이 안갈거 같음 <b>카드 외곽선을 각 레벨별로 분류하는게 어때</b>"*.
        //   폐기 근거는 취향이 아니라 <b>옛 문장의 전제가 틀렸다는 것</b>이다(UI_SURFACE_SPEC §15.2):
        //   네 상태 중 <b>「기본」은 의미가 아니라 「아무 상태도 아님」</b>이고, 한 섹션 6장 중 통상
        //   4~5장이 항상 그 상태다. 등급은 다섯 번째가 아니라 <b>비어 있던 자리를 채운 것</b>이며
        //   <b>우선순위는 한 글자도 안 바뀌었다</b>(<see cref="ApplyCardStyle"/> 참조).
        //   그리고 "지금 뭘 쓰고 있는지"는 죽지 않는다 — 착용 파랑을 α0.75로 올려 등급 최대(4.45)보다
        //   밝게(4.09→ΔE 42.57) 떼어 놓았다. <b>이 문단을 옛 문장으로 되돌리지 마라.</b>
        //
        // ★★ <b>그래도 여기서 하지 말아야 할 것 둘</b>(실측으로 기각됐고 아직 유효하다):
        //   1. <b>카드 바탕면을 등급색으로 물들이지 마라.</b> 글자 대비가 버티는 틴트 상한이 α=0.0754인데
        //      그 α에서 인접 등급 ΔE가 1.44~1.71이다(변별 하한 7.8의 1/5) — <b>대비를 내주고 신호는
        //      하나도 못 산다</b>.
        //   2. <b>아이콘 글로우를 넣지 마라.</b> 슬롯 대비 1.15~1.22:1로 애초에 안 보인다.
        //
        // ★ <b>미보유(잠김) 카드에서도 리본을 흐리지 않는다.</b> 인계본은 <c>opacity .25</c>였는데
        //   실측 대비가 1.45~1.69로 <b>"거기 리본이 있다"조차 안 보였다</b>(UX_HANDOFF_REVIEW_BRASS_ARCHIVE §5-5).
        //   그리고 잠김은 이미 이름(<c>???</c>) · 바탕 · 썸네일 · 실루엣 · 자물쇠 배지 · 버튼 문구
        //   <b>여섯 곳</b>이 말한다. 리본은 오직 <b>등급 하나</b>만 말한다 — 한 부품이 두 가지를 말하기
        //   시작하면 그때부터 둘 다 안 읽힌다. <c>docs/UX_FLOW.md</c> 50-4-2의 9상태 표도 전 상태에서
        //   리본 칸을 「등급」으로 고정한다.

        /// <summary>등급 리본 한 벌 — <b>트랙 1 + 칸 4 = <c>Image</c> 5개</b>(PALETTE_SPEC §12-3의 원가).
        /// <para>빈 칸을 <b>지우지 않고</b> 트랙 색으로 남기는 이유: 채워진 칸만 그리면 총 폭이 등급마다
        /// 달라져 <b>세는 일</b>이 되고, 트랙을 남기면 <b>채움 비율</b>이 되어 한눈에 읽힌다(배터리 눈금).
        /// 미보유 흐림에서도 형태가 남는 것이 같은 이유다.</para></summary>
        private sealed class RarityRibbon
        {
            /// <summary>트랙 = 리본 전체 폭. 칸 사이 틈으로 이 색이 비친다.</summary>
            public RectTransform Root;

            /// <summary>칸. 길이는 <see cref="UiChrome.RarityCellCount"/>다 — 4를 적지 않는다.</summary>
            public Image[] Cells;

            /// <summary>마지막으로 칠한 <b>채움 칸 수</b>. −1 = 아직 안 칠함.
            /// <para>채움 칸 수는 등급과 <b>일대일</b>이므로(<see cref="UiChrome.RarityFilledCells"/>)
            /// 이 한 정수로 "다시 칠할 필요가 있는가"를 정확히 가른다. 4Hz 갱신 루프가 카드 24장의
            /// 칸 5개를 매번 훑지 않게 하는 장치다(상주 앱 규약).</para></summary>
            public int PaintedFill = -1;
        }

        /// <summary>카드 <b>상단 여백</b> 안에서 리본 위에 남기는 틈(pt).
        /// <para>세로 예산 검산 — 카드 상단 여백은 <c>ThumbY</c>가 만드는 <b>8pt</b>이고 그 안에
        /// 전부 들어간다: 위 <b>2</b> + 리본 <see cref="UiChrome.RarityRibbonHeight"/> <b>4</b> +
        /// 아래 <b>2</b> = 8. <b>카드는 1pt도 커지지 않았다.</b> 아래쪽 두 빈틈(이름↔버튼 2pt ·
        /// 하단 4pt)은 못 쓴다 — 그쪽은 이미 다른 것이 쓰는 여백이다(PALETTE_SPEC §12-4).</para>
        /// <para>★ 이 값이 ×1.25/×1.75에서 반 픽셀에 걸리는 것은 <b>Windows 실기 미확인</b> 항목이다 —
        /// <see cref="UiChrome.RarityRibbonHeight"/> 문서의 (가)와 같은 건이다.</para>
        /// <para>★★ <b>2026-09-03 — 여기를 4로 내리는 변경을 착수했다가 되돌렸다.</b> 카드 테두리를 2pt로
        /// 올리면 테두리(y 0~−2)와 리본(y −2~−6)이 맞닿아 상단에 ΔE 16.40짜리 홈이 생기고, 4로 내리면
        /// 그 홈이 사라지면서 ×1.25/×1.75 반 픽셀 잔차까지 <b>0</b>이 된다(UI_SURFACE_SPEC §15.6-c).
        /// 되돌린 이유는 그 계산이 틀려서가 아니라 <b>카드 폭·썸네일 자리가 아직 안 정해졌기 때문</b>이다 —
        /// 창 1042 안의 3열 골격이 확정되면 이 8pt 예산 자체가 다시 계산된다(리더 판정 2026-09-03).
        /// <b>골격이 나온 뒤에 §15.6-c를 다시 적용할 것.</b></para></summary>
        private const float CardRibbonTopMargin = 2f;

        /// <summary>카드마다 한 벌. <see cref="_cards"/>와 <b>같은 인덱스</b>다.
        /// <para><see cref="ItemCard"/> 안에 넣지 않은 이유는 소유권이다 — 그 타입은
        /// <c>CharacterInfoWindow.cs</c>가 갖고 있고 이 라운드는 그 파일을 열지 않는다.
        /// 배열 두 개가 갈라지지 않는다는 것은 <b>같은 루프에서 같은 횟수로 채운다</b>는 사실이
        /// 보장하고, <see cref="RibbonAt"/>가 길이 불일치를 조용히 넘기지 않는다.</para></summary>
        private RarityRibbon[] _cardRibbons = System.Array.Empty<RarityRibbon>();

        /// <summary>인덱스가 두 배열에서 같은 자리를 가리키는지까지 확인하고 내준다.
        /// 어긋나면 <c>null</c>이고, 리본이 안 그려지는 것으로 <b>보인다</b> — 조용히 <b>남의 카드</b>
        /// 등급을 칠하는 것보다 낫다.</summary>
        private RarityRibbon RibbonAt(int index)
            => index >= 0 && index < _cardRibbons.Length && index < _cards.Length ? _cardRibbons[index] : null;

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
                // ★ 은퇴한 카테고리는 어느 탭에도 자리가 없다(2026-09-06 [머리] 삭제).
                //   여기와 SectionCountForTab이 <b>같은 술어</b>를 봐야 한다 — 한쪽만 거르면
                //   보이는 칸 수와 그 칸이 가리키는 카테고리가 한 칸씩 어긋난다.
                if (EquipmentModel.IsRetiredSlot(slot)) continue;
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
        /// 부르는 쪽이 각자 <c>_tab == Tab.Inventory</c>로 걸러야 했다.</para>
        /// <para>★ 2026-09-06 [머리] 삭제로 [외형]이 3 → <b>2</b>가 됐다. 숫자가 아니라 술어
        /// (<see cref="EquipmentModel.IsRetiredSlot"/>)를 보므로 이 함수는 고칠 것이 없었다.</para></summary>
        private static int SectionCountForTab(Tab tab)
        {
            TabDef def = Def(tab);
            if (def.Page != TabPage.Cards) return 0;

            int n = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (EquipmentModel.IsRetiredSlot(slot)) continue;   // SectionSlot과 같은 술어 — 위 문단.
                if (EquipmentModel.IsAppearanceSlot(slot) == def.AppearanceSlots) n++;
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
                // ★ 분자·분모 <b>둘 다 보여주는 목록</b>이다(2026-09-06 이펙트 「없음」 은퇴).
                //   한쪽만 은퇴를 반영하면 이펙트가 「6 / 5」로 뜬다.
                view.Count.text =
                    $"{EquipmentModel.ListedOwnedItemCount(slot)} / {EquipmentModel.ListedItemCount(slot)}";

                // 카테고리가 바뀌었으면 격자를 처음으로 되돌린다 — 아이템이 적은 카테고리로
                // 넘어갔을 때 스크롤이 남아 있으면 <b>빈 자리</b>가 보인다.
                if (!view.HasBoundSlot || view.BoundSlot != slot)
                {
                    view.HasBoundSlot = true;
                    view.BoundSlot = slot;
                    ResetGridScroll();
                }

                // ★ 카드 자리 c는 <b>보여주는 목록의 c번째</b>이지 카탈로그의 c번이 아니다
                //   (2026-09-06 이펙트 「없음」 은퇴로 0번이 목록에서 빠졌다). 환산은
                //   <see cref="ItemCatalog.ListedItemIndex"/> 한 곳에서만 하고, 그 결과를
                //   <c>card.Item</c>에 <b>다시 심는다</b> — 클릭·호버·상세 패널은 전부 그 값을 읽으므로
                //   여기서 한 번 맞추면 나머지 경로가 자동으로 같은 아이템을 가리킨다.
                int items = ItemCatalog.ListedItemCountIn(slot);
                for (int c = 0; c < view.CardCount; c++)
                {
                    ItemCard card = _cards[view.FirstCard + c];
                    if (card == null) continue;

                    bool used = c < items;
                    if (card.Rect.gameObject.activeSelf != used) card.Rect.gameObject.SetActive(used);
                    if (!used) continue;

                    int item = ItemCatalog.ListedItemIndex(slot, c);
                    if (item < 0) continue;   // 환산이 실패하면 그리지 않는다(빈 카드가 더 낫다).
                    card.Item = item;
                    ApplyCardStyle(card, slot, item, set);

                    // ★ 리본은 <see cref="ApplyCardStyle"/> <b>안</b>이 아니라 여기에 있다.
                    //   그 함수는 카드의 <b>상태</b>(보유/착용/선택/호버) 표이고, 등급은 상태가 아니라
                    //   카드가 지금 가리키는 <b>아이템</b>에서만 나온다. 그리고 상태 표는 호버 재도색
                    //   경로(Input 조각)에서도 불리는데, 그 경로에는 카드 인덱스가 없어 리본을 찾을 수
                    //   없다 — 슬롯이 탭에서 파생되는 자리는 이 루프 하나뿐이다.
                    ApplyRarityRibbon(RibbonAt(view.FirstCard + c), ItemCatalog.Rarity(slot, item));
                }
            }

            // 활성 카드 수가 바뀌면 블록 높이와 스크롤 한계가 달라진다 — 좌표는 <b>여기 한 곳</b>에서
            // 다시 잡는다(다음 캔버스 갱신까지 기다리면 그 한 프레임 동안 옛 값으로 잘린다).
            LayoutCardGrid(visible);
            SyncSlotRows();
        }

        /// <summary>카테고리 제목줄의 <b>가로</b> 배치 — 구분선과 "n / 6" 카운터는 컬럼 폭에서 파생된다.
        /// <para>숫자를 박아 두면 창 폭이 바뀌었을 때 헤더만 옛 자리에 남아 카드 격자와 끝선이 갈라진다
        /// (이 저장소가 2026-09-02에 실제로 겪은 사고이고, InfoWindowCardRowEdgeTests가 그것을 잠근다).</para></summary>
        private void LayoutCategoryHeader(SectionView view, float usedWidth)
        {
            float countX = usedWidth - CategoryCountWidth;
            if (view.Count != null)
            {
                UiChrome.PlaceTopLeft(view.Count.rectTransform, countX, 0f,
                    CategoryCountWidth, CategoryHeaderHeight);
            }
            if (view.Divider != null)
            {
                float width = Mathf.Max(0f, countX - CategoryDividerX - CategoryDividerGap);
                UiChrome.PlaceTopLeft(view.Divider.rectTransform, CategoryDividerX,
                    -(CategoryHeaderHeight * 0.5f), width, DividerThickness);
                if (view.Divider.gameObject.activeSelf != width > 1f)
                {
                    view.Divider.gameObject.SetActive(width > 1f);
                }
            }
        }

        private void ResetGridScroll()
        {
            if (_gridContent == null) return;
            Vector2 p = _gridContent.anchoredPosition;
            if (Mathf.Approximately(p.y, 0f)) return;
            p.y = 0f;
            _gridContent.anchoredPosition = p;
        }

        /// <summary>
        /// 컬럼 1의 착용 슬롯 4행 — <b>지금 무엇을 걸치고 있는가</b>를 카테고리 순서대로 적는다.
        ///
        /// <para>인계본은 이 자리에 스탯 기여값("집중력 +6")을 적지만 그 4스탯은 런타임이 0줄이다
        /// (문서 §1-3). 없는 수치를 화면이 주장하지 않도록 지금은 <b>등급 낱말</b>을 적는다 —
        /// 실재하는 사실이고, 스탯이 들어오는 라운드(§8 4단계)에 같은 칸을 교체하면 된다.</para>
        ///
        /// <para>슬롯 행은 <b>[장비] 계열 카테고리</b>를 보여준다. [외형] 탭에서도 같은 값을 보여주는
        /// 이유는 이 컬럼의 주제가 "탭"이 아니라 <b>이 캐릭터</b>이기 때문이다 — 무대 위 인형이
        /// 탭과 무관하게 같은 것을 걸치고 있는 것과 같다.</para>
        ///
        /// <para>★★ <b>2026-09-06 — 「걸쳤다」와 「그려진다」는 다른 사실이다</b>(persona-newcomer 실기 신고).
        /// 이 줄은 <c>WornIndex >= 0</c>만 보고 아이템을 적었는데,
        /// <see cref="EquipmentModel.RestoreFromSave(EquipmentSlot,string)"/>는
        /// <b>일부러</b> 잠금을 검사하지 않는다(검사하면 레벨이 낮게 복원되는 순간 착용물이 조용히 사라진다).
        /// 그 문서가 «대신 렌더러/UI가 그릴 때 <see cref="EquipmentModel.IsUnlocked"/>로 함께 본다»고
        /// 약속하는데 <b>이 줄만 그 약속을 안 지키고 있었다</b>.</para>
        ///
        /// <para>실측 재현: 세이브에 <c>wornEyes=고글</c>(Lv11)·<c>wornShoulders=요정 날개</c>(Lv28)가
        /// 있는 Lv3 캐릭터 — 슬롯 줄은 이름·등급·아이콘까지 다 적는데 <b>바로 옆 액자에는 아무것도 안
        /// 그려진다</b>. 액자(<c>CharacterPortraitStage.EquippedAndUnlocked</c>)와 몸
        /// (<c>CharacterAccessoryRenderer</c>)은 처음부터 잠금까지 보고 있었으므로, <b>같은 창이 자기
        /// 자신을 반증</b>하고 있었다.</para>
        ///
        /// <para>고침은 <b>새 상태를 만들지 않는다</b> — 잠긴 착용물은 아래 <c>entry == null</c> 가지,
        /// 즉 이미 있는 <b>「비어 있음」</b>으로 떨어진다. 그것이 액자가 실제로 그리는 것과 같기 때문이고,
        /// 「잠김」이라는 여섯 번째 표시를 여기 새로 만들면 그때부터 이 칸이 두 가지를 말하게 된다.
        /// <b>세이브 파일은 한 글자도 안 건드린다</b> — 레벨이 올라오면 그 줄은 저절로 되살아난다.</para>
        /// </summary>
        private void SyncSlotRows()
        {
            for (int i = 0; i < _slotRows.Length; i++)
            {
                SlotRowView row = _slotRows[i];
                if (row == null) continue;

                bool used = i < SectionCountForTab(Tab.Equipment);
                if (row.Rect.gameObject.activeSelf != used) row.Rect.gameObject.SetActive(used);
                if (!used) continue;

                EquipmentSlot slot = SectionSlot(Tab.Equipment, i);
                row.Label.text = $"{EquipmentModel.SlotName(slot)}  ·  {EquipmentModel.SlotCode(slot)}";

                int worn = EquipmentModel.WornIndex(slot);

                // ★ 액자·몸과 <b>같은 술어</b>다(CharacterPortraitStage.EquippedAndUnlocked /
                //   CharacterAccessoryRenderer.EquippedAndUnlocked). 술어를 여기서 새로 짜지 않고
                //   EquipmentModel의 공개 사실 둘을 그대로 곱한다 — 잠금 규칙이 바뀌면 세 표면이
                //   동시에 따라온다. 앞 항이 없으면 미착용일 때 IsUnlocked가 "고를 것이 하나라도
                //   있는가"로 뜻이 바뀌어 빈 슬롯이 착용으로 읽힌다.
                bool drawnOnStage = EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot);
                ItemCatalogEntry entry = drawnOnStage ? ItemCatalog.Item(slot, worn) : null;

                if (entry == null)
                {
                    row.Name.text = "비어 있음";
                    row.Name.color = UiChrome.InkTitle(false);
                    row.Value.text = "—";
                    row.Value.color = UiChrome.TextTertiary;
                    row.Outline.color = UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface);
                    if (row.HasIcon) ClearSlotIcon(row);
                    continue;
                }

                row.Name.text = entry.DisplayName;
                row.Name.color = UiChrome.InkTitle(true);
                ItemRarity rarity = ItemCatalog.Rarity(slot, worn);
                row.Value.text = ItemCatalog.RarityName(rarity);
                row.Value.color = UiChrome.RarityColor(rarity);
                // ★ 2026-09-06 — 등급 테두리 α0.55를 이 줄의 면(CardSurface) 위에 미리 합성한다.
                row.Outline.color = UiChrome.Flatten(UiChrome.RarityBorder(rarity), UiChrome.CardSurface);
                BuildSlotIcon(row, slot, worn, entry);
            }
        }

        /// <summary>슬롯 행의 작은 도형. 착용이 바뀔 때만 다시 굽는다(4Hz 루프가 아니다 —
        /// <see cref="RefreshCards"/>는 사건이 있을 때만 불린다).</summary>
        private static void BuildSlotIcon(SlotRowView row, EquipmentSlot slot, int itemIndex,
            ItemCatalogEntry entry)
        {
            ClearSlotIcon(row);
            if (!AccessoryCardIcon.TryBuild(row.IconRoot, slot, itemIndex, SlotIconSize,
                    IconStroke * (SlotIconSize / IconSize), entry.PrimaryColor, entry.SecondaryColor))
            {
                BuildIcon(row.IconRoot, entry.Icon, SlotIconSize);
            }
            row.HasIcon = true;
        }

        private static void ClearSlotIcon(SlotRowView row)
        {
            for (int i = row.IconRoot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(row.IconRoot.GetChild(i).gameObject);
            }
            row.HasIcon = false;
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
            // ★ 2026-09-03 — 등급이 이 함수에 들어왔다. 예전 주석("등급은 상태가 아니므로 여기 없다")은
            //   리본에 대해서는 여전히 맞지만, <b>테두리의 마지막 칸</b>은 상태 3항식 안에 있어서
            //   상태와 <b>같은 자리에서</b> 결정돼야 한다. 호버 재도색 경로(RestyleCard)도 슬롯과
            //   아이템 인덱스를 넘겨 주므로 두 경로가 같은 값을 본다 — 갈라질 여지가 없다.
            ItemRarity rarity = ItemCatalog.Rarity(slot, itemIndex);

            // 이름은 상자를 넘으면 말줄임한다 — Overflow로 흘리면 오른쪽 등급 낱말과 물리적으로
            // 겹친다(P0-5). 내용이 바뀐 순간에만 다시 계산한다(ItemCard.NameSource 문서).
            string wantedName = owned ? entry.DisplayName : "???";
            if (!string.Equals(card.NameSource, wantedName, System.StringComparison.Ordinal))
            {
                card.NameSource = wantedName;
                card.Name.text = UiChrome.Ellipsize(card.Name, wantedName, CardNameWidth);
            }
            card.Name.color = UiChrome.InkTitle(owned);

            // ★ 등급 <b>낱말</b>이 카드에 내려왔다(UI_SURFACE_SPEC §15.14-d). 그 안이 보류된 이유가
            //   "창 1042 안의 골격이 아직 안 정해졌다"였고, 3컬럼 이식으로 그 전제가 풀렸다.
            //   낱말의 출처는 ItemCatalog.RarityName 하나다 — 여기에 "전설"을 직접 적지 않는다.
            card.Rarity.text = ItemCatalog.RarityName(rarity);
            card.Rarity.color = UiChrome.RarityColor(rarity);

            // 카테고리 라벨은 상태와 무관하다 — 잠긴 카드에서도 "무엇의 자리인가"는 말해도 된다.
            card.Category.text = entry.CategoryLabel;

            if (!owned)
            {
                // "LV.20" — 잠긴 카드의 상태 줄은 <b>언제 열리는지</b> 하나만 말한다.
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

            // 테두리 우선순위: 선택 > hover > 착용 중 > (기본을 승계한) 등급.
            //
            // ★★ 2026-09-03 사용자 지시 — *"카드 외곽선을 각 레벨별로 분류하는게 어때"*.
            //   <b>분기 개수도 순서도 그대로다.</b> 바뀐 것은 마지막 한 칸뿐이고, 그 칸은 「기본」이라는
            //   <b>상태</b>가 아니라 <b>「아무 상태도 아님」</b>이었다(클래스 문서 상단 정정 참조).
            //   그래서 상태 셋은 지금까지처럼 등급을 덮고, 덮이는 그 순간은 정확히 <b>유저가 그 카드를
            //   들여다보고 있는 순간</b>이라(호버=포인터가 그 위 / 선택=아래 상세 패널이 설명 중 /
            //   착용=자기가 입힌 것) 리본과 낱말이 등급을 계속 말한다 — <b>화면이 등급을 잃는 순간은 0</b>.
            //
            // ★ 2026-09-06 (docs/UI_ALPHA_BLEED_POLICY.md §1-E/§4-2) — α<1인 두 끝점을 <b>자기 바탕에
            //   미리 합성</b>한다. 호버 α0.62와 등급 α0.55는 이 창에서 <b>가장 넓은 비침 면적</b>이었다
            //   (카드 24장 × 탭 4개). 보이는 색은 한 톤도 안 바뀐다 — 이 저장소가 적어 둔 검산값
            //   (인접 ΔE 9.06 / 전설 4.45 / 호버 #A8AAAD 7.09)이 애초에 <b>합성 후</b> 기준이었다.
            //   바탕은 바로 위에서 정한 이 카드의 면이다(잠김이면 CardSurfaceMuted).
            Color cardFace = card.Surface.color;
            card.Outline.color = selected ? UiChrome.TextPrimary
                : hovered ? UiChrome.Flatten(UiChrome.CardBorderHover, cardFace)
                : worn && owned ? UiChrome.CardBorderWorn
                : UiChrome.Flatten(UiChrome.RarityBorder(rarity), cardFace);


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
        /// <para><b>P0-4 가드는 그대로 통과한다</b>(이게 핵심이다): 새 두 면의 휘도는 0.2355 / 0.2351로,
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

        /// <summary>
        /// 리본을 <paramref name="rarity"/>로 칠한다 — 앞에서부터
        /// <see cref="UiChrome.RarityFilledCells"/>칸은 등급색, 나머지는 <see cref="UiChrome.RarityTrack"/>.
        ///
        /// <para><b>이 함수는 색 리터럴을 하나도 모른다.</b> 등급색의 유일한 출처는 <c>UiChrome</c>이고
        /// (<c>Tests/EditMode/RarityColorSingleSourceTests</c>가 프로덕션 소스를 통째로 훑어 hex 표기와
        /// 실수 3튜플 <b>양쪽</b>으로 막는다), 칸 수의 유일한 출처도 그쪽이다.</para>
        ///
        /// <para>채움 칸 수는 등급과 일대일이라 <see cref="RarityRibbon.PaintedFill"/> 정수 하나로
        /// 재도색 필요 여부가 정확히 갈린다 — 등급이 그대로면 <see cref="Image"/> 5개를 건드리지 않는다.</para>
        /// </summary>
        private static void ApplyRarityRibbon(RarityRibbon ribbon, ItemRarity rarity)
        {
            if (ribbon == null || ribbon.Cells == null) return;
            if (ribbon.Root != null && !ribbon.Root.gameObject.activeSelf)
            {
                ribbon.Root.gameObject.SetActive(true);
            }

            int fill = UiChrome.RarityFilledCells(rarity);
            if (ribbon.PaintedFill == fill) return;
            ribbon.PaintedFill = fill;

            Color on = UiChrome.RarityColor(rarity);
            for (int i = 0; i < ribbon.Cells.Length; i++)
            {
                if (ribbon.Cells[i] == null) continue;
                ribbon.Cells[i].color = i < fill ? on : UiChrome.RarityTrack;
            }
        }

        /// <summary>등급이 <b>없는</b> 자리(보관함의 헤더 줄과 「할 줄 아는 것」)에서 리본을 통째로 숨긴다.
        /// <para>빈 트랙만 남기지 <b>않는다</b> — 그러면 "등급이 없다"가 아니라 <b>"0칸짜리 등급"</b>으로
        /// 읽힌다. 없는 것은 없게 보여야 한다.</para></summary>
        private static void HideRarityRibbon(RarityRibbon ribbon)
        {
            if (ribbon?.Root == null) return;
            if (ribbon.Root.gameObject.activeSelf) ribbon.Root.gameObject.SetActive(false);
            ribbon.PaintedFill = -1;   // 다시 보일 때 반드시 한 번은 칠하게 한다.
        }

        /// <summary>
        /// 리본 한 벌을 굽는다 — 트랙 <see cref="Image"/> 1개 + 칸 <see cref="UiChrome.RarityCellCount"/>개.
        ///
        /// <para>칸 폭은 <see cref="UiChrome.RarityCellWidth"/>가 <paramref name="width"/>에서 <b>나눠 준다</b>.
        /// 카드(139pt)와 보관함(46pt)이 각자 나누면, 등급 단이 하나 늘어난 날 한쪽만 어긋난다.</para>
        ///
        /// <para>둥근 모서리에 <see cref="UiChrome.RadiusDot"/>을 쓰는 것은 이 창의 <b>게이지 트랙</b>과
        /// 같은 선택이다(그쪽도 4pt 높이의 얇은 막대다).</para>
        ///
        /// <para><b>클릭을 먹지 않는다.</b> 리본은 카드 본체 위에 얹히므로 레이캐스트를 켜 두면
        /// 카드 상단 8pt가 "눌러도 아무 일 없는 띠"가 된다.</para>
        /// </summary>
        private static RarityRibbon BuildRarityRibbon(Transform parent, float x, float y, float width)
        {
            Image track = UiChrome.AddSurface(parent, "RarityRibbon", UiChrome.RarityTrack, UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(track.rectTransform, x, y, width, UiChrome.RarityRibbonHeight);
            track.raycastTarget = false;

            int count = UiChrome.RarityCellCount;
            float cell = UiChrome.RarityCellWidth(width);
            var cells = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Image piece = UiChrome.AddSurface(track.rectTransform, "Cell" + i,
                    UiChrome.RarityTrack, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(piece.rectTransform, i * (cell + UiChrome.RarityCellGap), 0f,
                    cell, UiChrome.RarityRibbonHeight);
                piece.raycastTarget = false;
                cells[i] = piece;
            }

            return new RarityRibbon { Root = track.rectTransform, Cells = cells };
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
            ItemRarity detailRarity = ItemCatalog.Rarity(_selectedSlot, _selectedItem);

            // ★ 2026-09-06 — 썸네일 면을 <b>먼저</b> 정하고 그 위에 등급 테두리를 합성한다.
            //   예전에는 테두리를 칠한 다음 줄에서 면을 칠했다 — α0.55가 그대로 나가면 이 자리에서만
            //   창 알파가 내려가 뒤 창이 비친다(정책 §4-2).
            Color thumbFace = owned ? UiChrome.CardSurface : UiChrome.ThumbSurfaceLocked;
            if (_detailThumb != null) _detailThumb.color = thumbFace;
            if (_detailThumbOutline != null)
            {
                _detailThumbOutline.color = UiChrome.Flatten(UiChrome.RarityBorder(detailRarity), thumbFace);
            }
            RefreshDetailThumbArt(entry, owned);

            if (_detailName != null)
            {
                _detailName.text = owned ? entry.DisplayName : "???";
                _detailName.color = UiChrome.InkTitle(owned);
            }
            if (_detailMeta != null)
            {
                // ★ 2026-09-03 — 맨 앞에 <b>등급 낱말</b>이 붙었다(사용자 확정 「안 C1」).
                //
                //   <b>낱말이 없으면 등급 표시는 미완이다.</b> 리본의 칸 수는 "몇 번째 단인가"까지만
                //   말하고 그 단의 <b>이름</b>은 말하지 못한다. 그리고 색은 그 일을 못 한다 —
                //   식별 하한 ΔE 48.6을 넘는 쌍이 여섯 쌍 중 하나뿐이다(PALETTE_SPEC §12-0).
                //   persona-newcomer: *"색만은 나한테 안 통한다. 리본을 넣을 거면 낱말을 반드시 같이."*
                //
                // ★ <b>카드에는 안 적는다. 여기 한 번뿐이다</b>(PALETTE_SPEC §12-4 / UX_FLOW 50-4-1):
                //   카드 메타 칸은 41pt이고 <c>LV.20</c> / <c>착용 중</c>이 이미 꽉 쓴다. 반면 이 줄은
                //   405pt라 네 토막이 여유롭게 들어간다.
                //
                // ★ 낱말의 출처는 <see cref="ItemCatalog.RarityName"/> <b>하나</b>다. 여기에
                //   <c>"전설"</c>을 직접 적으면 로컬라이제이션이 왔을 때 번역이 두 갈래로 갈라진다.
                string rarity = ItemCatalog.RarityName(detailRarity);
                _detailMeta.text = !owned
                    ? $"{rarity}  ·  {entry.CategoryLabel}  ·  Lv.{entry.RequiredLevel}에 열림"
                    : $"{rarity}  ·  {entry.CategoryLabel}  ·  {(worn ? "착용 중" : "보유 중")}";
            }
            if (_detailBody != null)
            {
                _detailBody.text = owned
                    ? entry.Description
                    : $"레벨 {entry.RequiredLevel}이 되면 열립니다. 지금은 실루엣만 보입니다.";
                _detailBody.color = UiChrome.InkBody(owned);
            }
        }

        /// <summary>상세 카드 썸네일의 도형 — 선택이 바뀔 때만 다시 굽는다.
        /// <para>도형은 카드와 <b>같은 경로</b>(<see cref="AccessoryCardIcon"/>)를 쓴다. 두 벌을
        /// 만들지 않는 것이 이 저장소의 규칙이다 — 모자 챙을 한 곳에서 고치면 전부 따라 바뀐다.</para></summary>
        private void RefreshDetailThumbArt(ItemCatalogEntry entry, bool owned)
        {
            if (_detailThumb == null) return;
            RectTransform host = _detailThumb.rectTransform;
            if (_detailThumbArt == null)
            {
                var go = new GameObject("Art", typeof(RectTransform));
                go.transform.SetParent(host, false);
                _detailThumbArt = go.GetComponent<RectTransform>();
                _detailThumbArt.anchorMin = _detailThumbArt.anchorMax = _detailThumbArt.pivot = new Vector2(0.5f, 0.5f);
                _detailThumbArt.sizeDelta = new Vector2(DetailThumbArtSize, DetailThumbArtSize);
                _detailThumbArt.anchoredPosition = Vector2.zero;
            }

            string key = entry.Id + (owned ? "+" : "-");
            if (string.Equals(_detailThumbArtKey, key, System.StringComparison.Ordinal)) return;
            _detailThumbArtKey = key;

            for (int i = _detailThumbArt.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_detailThumbArt.GetChild(i).gameObject);
            }

            if (!AccessoryCardIcon.TryBuild(_detailThumbArt, _selectedSlot, _selectedItem, DetailThumbArtSize,
                    IconStroke * (DetailThumbArtSize / IconSize), entry.PrimaryColor, entry.SecondaryColor))
            {
                BuildIcon(_detailThumbArt, entry.Icon, DetailThumbArtSize);
            }

            if (owned) return;
            // 잠김 = <b>무채색 실루엣</b>. 카드와 같은 처방이다.
            Image[] graphics = _detailThumbArt.GetComponentsInChildren<Image>(true);
            var muted = new Color(UiChrome.TextTertiary.r, UiChrome.TextTertiary.g, UiChrome.TextTertiary.b, 0.34f);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].color = muted;
            }
        }

        /// <summary>상세 카드 썸네일(52) 안에서 도형이 차지하는 정사각 크기.</summary>
        private const float DetailThumbArtSize = 38f;

        private RectTransform _detailThumbArt;
        private string _detailThumbArtKey;

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

        // -------------------- 컬럼 3 — 카테고리 블록의 세로 스크롤([장비]/[외형] 공용) --------------------

        private void BuildSectionPage(RectTransform body)
        {
            var pageGo = new GameObject("Col3", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            pageGo.transform.SetParent(body, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, Col3X, 0f, Col3Width, BodyHeight);
            _sectionPage = pageGo;

            // 카드 사이 빈틈을 잡아도 끌리게 하는 투명 판. 그래픽이 없으면 uGUI 레이캐스트가 통과해
            // 창 바탕이 잡히고, 사용자에게는 "여기는 안 밀리네"로 보인다.
            var handle = pageGo.GetComponent<Image>();
            handle.color = Color.clear;
            handle.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(page, false);
            _gridViewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_gridViewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_gridViewport, false);
            _gridContent = contentGo.GetComponent<RectTransform>();
            _gridContent.anchorMin = _gridContent.anchorMax = _gridContent.pivot = new Vector2(0f, 1f);
            _gridContent.sizeDelta = new Vector2(_gridWidth, BodyHeight);
            _gridContent.anchoredPosition = Vector2.zero;

            _gridScroll = pageGo.GetComponent<ScrollRect>();
            _gridScroll.viewport = _gridViewport;
            _gridScroll.content = _gridContent;
            _gridScroll.horizontal = false;
            _gridScroll.vertical = true;
            // ★ 관성을 끄고 Clamped로 두는 이유는 취향이 아니다 — 전역 폴링 드래그와 계산이
            //   <b>같아야</b> 두 경로가 동시에 돌아도 결과가 어긋나지 않는다(DragGridTo 문단).
            _gridScroll.movementType = ScrollRect.MovementType.Clamped;
            _gridScroll.inertia = false;
            _gridScroll.scrollSensitivity = CardStepY * 0.5f;
            _gridScroll.horizontalScrollbar = null;
            _gridScroll.verticalScrollbar = null;

            // 카드 총량은 카탈로그가 정한다 — 빌드 때 한 번만 세고, 그 뒤로는 배열이 고정된다.
            var cards = new System.Collections.Generic.List<ItemCard>(SectionCount * 6);
            var ribbons = new System.Collections.Generic.List<RarityRibbon>(SectionCount * 6);

            for (int s = 0; s < SectionCount; s++)
            {
                var sectionGo = new GameObject("CatBlock" + s, typeof(RectTransform));
                sectionGo.transform.SetParent(_gridContent, false);
                var section = sectionGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(section, Col3PadX, -Col3PadTop, Col3ContentWidth, CategoryHeaderHeight);

                Image dot = UiChrome.AddSurface(section, "Bar", UiChrome.Accent, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(dot.rectTransform, 0f, -2f, 3f, 15f);
                dot.raycastTarget = false;

                Text title = Label(section, "Name", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    3f + UiChrome.Space2, 0f, 84f, CategoryHeaderHeight, "—", bold: true);
                Text code = Label(section, "Code", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                    3f + UiChrome.Space2 + 84f, 0f, 62f, CategoryHeaderHeight, "—");

                Image divider = UiChrome.AddSurface(section, "Divider",
                    UiChrome.Flatten(UiChrome.Divider, UiChrome.PanelSurface), 2);
                // 폭은 <b>열에서 파생</b>된다 — 숫자를 박아 두면 창 폭이 바뀌었을 때 헤더만 옛 자리에
                // 남아 카드 격자와 끝선이 갈라진다(2026-09-02에 실제로 겪은 사고).
                UiChrome.PlaceTopLeft(divider.rectTransform, CategoryDividerX, -(CategoryHeaderHeight * 0.5f),
                    CategoryDividerWidth, DividerThickness);
                divider.raycastTarget = false;

                // ★ 이 카운터의 오른쪽 끝이 컬럼 3 콘텐츠의 <b>끝선</b>이다(카드 격자가 여기에 맞춘다 —
                //   InfoWindowCardRowEdgeTests가 두 사각형의 xMax를 직접 비교해 잠근다).
                Text count = Label(section, "Count", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextTertiary,
                    CategoryCountX, 0f, CategoryCountWidth, CategoryHeaderHeight, "0 / 6");

                var view = new SectionView
                {
                    Root = sectionGo, Rect = section, Dot = dot, Title = title, Code = code, Count = count,
                    Divider = divider,
                };
                _sections[s] = view;

                view.FirstCard = cards.Count;
                view.CardCount = CardsInSection(s);
                for (int c = 0; c < view.CardCount; c++)
                {
                    // 두 배열은 <b>같은 루프에서 같은 횟수로</b> 채운다 — 인덱스가 갈라질 여지를
                    // 구조적으로 없앤다(RibbonAt이 그래도 길이를 한 번 더 본다).
                    cards.Add(BuildCard(section, s, c, cards.Count, out RarityRibbon ribbon));
                    ribbons.Add(ribbon);
                }
            }

            _cardRibbons = ribbons.ToArray();
            _cards = cards.ToArray();
        }

        /// <summary>
        /// ★ 카드 격자의 <b>유일한</b> 좌표 계산기. 블록 y·높이 / 카드 x·y / 스크롤 콘텐츠 높이가
        /// 전부 여기서 나온다.
        ///
        /// <para>레이아웃 그룹에 맡기지 않는 이유: 카테고리마다 아이템 수가 달라 블록 높이가 서로
        /// 다르고, 그 높이가 <b>스크롤 한계</b>를 정한다. 두 곳에서 계산하면 밀 수 있는 양과 실제
        /// 콘텐츠가 갈라진다(이 창이 캐러셀 시절에 겪은 그 사고와 같은 종류다).</para>
        /// </summary>
        private void LayoutCardGrid(int visibleSections)
        {
            // ★ 이 창 오른쪽 열의 <b>끝선은 하나</b>다 — 제목줄(구분선·"n / 6")과 카드 격자가 같은 x에서
            //   끝나야 한다. 그 끝선은 컬럼 폭이 아니라 <b>카드가 실제로 쓰는 폭</b>이다: 설계 폭
            //   1042에서는 둘이 같지만(384 = 186×2 + 12), 창이 좁아져 컬럼을 접으면 컬럼 폭이 더
            //   넓어져 헤더만 오른쪽으로 늘어난다(2026-09-05 배치모드 실측 164pt 어긋남).
            int columns = Mathf.Max(1, _gridColumns);
            float gridUsedWidth = columns * CardWidth + (columns - 1) * CardGap;

            float y = -Col3PadTop;
            for (int s = 0; s < SectionCount; s++)
            {
                SectionView view = _sections[s];
                if (view == null || view.Rect == null) continue;
                if (s >= visibleSections) continue;

                int items = 0;
                for (int c = 0; c < view.CardCount; c++)
                {
                    ItemCard card = _cards[view.FirstCard + c];
                    if (card != null && card.Rect != null && card.Rect.gameObject.activeSelf) items++;
                }

                int rows = Mathf.CeilToInt(items / (float)columns);
                float blockHeight = rows <= 0
                    ? CategoryHeaderHeight
                    : -CategoryGridTopY + rows * CardHeight + (rows - 1) * CardGap;

                UiChrome.PlaceTopLeft(view.Rect, Col3PadX, y, gridUsedWidth, blockHeight);
                LayoutCategoryHeader(view, gridUsedWidth);

                for (int c = 0; c < view.CardCount; c++)
                {
                    ItemCard card = _cards[view.FirstCard + c];
                    if (card == null || card.Rect == null) continue;
                    int col = c % columns;
                    int row = c / columns;
                    UiChrome.PlaceTopLeft(card.Rect, col * CardStepX,
                        CategoryGridTopY - row * CardStepY, CardWidth, CardHeight);
                }

                y -= blockHeight + CategoryBlockGap;
            }

            float used = -y - CategoryBlockGap + Col3PadBottom;   // 마지막 블록 뒤 간격은 빼고 아래 여백을 더한다.
            if (_gridContent != null)
            {
                _gridContent.sizeDelta = new Vector2(_gridWidth, Mathf.Max(BodyHeight, used));
                // 콘텐츠가 짧아지면(탭 전환) 밀려 있던 자리가 범위 밖이 된다 — 그대로 두면 빈 화면이다.
                float max = MaxGridScroll();
                Vector2 p = _gridContent.anchoredPosition;
                float clamped = Mathf.Clamp(p.y, 0f, max);
                if (!Mathf.Approximately(p.y, clamped))
                {
                    p.y = clamped;
                    _gridContent.anchoredPosition = p;
                }
            }
        }

        private ItemCard BuildCard(RectTransform content, int sectionIndex, int columnIndex, int cardIndex,
            out RarityRibbon ribbon)
        {
            Image surface = UiChrome.AddSurface(content, "Card" + cardIndex, UiChrome.CardSurface, UiChrome.RadiusCard);
            var rt = surface.rectTransform;
            // 실제 x·y는 LayoutCardGrid가 정한다 — 여기서는 <b>크기와 피벗</b>만 맞춰 준다.
            UiChrome.PlaceTopLeft(rt, 0f, 0f, CardWidth, CardHeight);
            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);

            Image thumb = UiChrome.AddSurface(rt, "Thumb", UiChrome.CardSurfaceMuted, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(thumb.rectTransform, ThumbX, ThumbY, ThumbWidth, ThumbHeight);
            thumb.raycastTarget = false;


            // ---- 등급 리본 ---- 카드 <b>상단 여백</b>에 앉는다. 인셋과 폭을 숫자로 적지 않고
            //   썸네일에서 <b>파생</b>시키는 것이 핵심이다: 카드 폭이 바뀌면 리본도 같이 움직여야 한다
            //   (이 저장소는 폭 1042 확대 때 헤더만 옛 자리에 남아 카드줄과 끝선이 갈라진 사고를 겪었다).
            //   인계본 §4-3-3의 조합이 정확히 이것이다 — 인셋 14 · 폭 158 · 높이 4 · 틈 2.
            ribbon = BuildRarityRibbon(rt, CardRibbonInset, -CardRibbonTopMargin, CardRibbonWidth);


            var card = new ItemCard
            {
                Section = sectionIndex,
                // ★ <b>임시값</b>이다 — 카드는 탭을 바꿔도 다시 굽지 않는 재사용 자원이라, 이 자리가
                //   실제로 어느 아이템인지는 <see cref="RefreshCards"/>가 슬롯을 바인딩하면서 다시
                //   심는다(은퇴한 아이템이 목록 가운데 있으면 자리 번호 ≠ 아이템 번호다).
                Item = columnIndex,
                Rect = rt,
                Surface = surface,
                Outline = outline,
                Thumb = thumb,
                Name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    CardPadX, CardNameY, CardNameWidth, CardTextHeight, "—", bold: true),
                Rarity = Label(rt, "Rarity", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextTertiary,
                    CardRarityX, CardNameY, CardRarityWidth, CardTextHeight, "—"),
                Meta = Label(rt, "Meta", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.InkMeta,
                    CardMetaX, CardMetaY, CardMetaWidth, CardMetaHeight, "—"),
                Category = Label(rt, "Category", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                    CardPadX, CardCategoryY, CardContentWidth, CardCategoryHeight, "—"),
            };

            // ---- 카드 하단 [착용]/[해제] ---- 이 창의 <b>유일한</b> 착용 손잡이다(상세 패널의
            //   중복 버튼은 2026-09-01 사용자 신고로 걷어냈다). 1차 행동이지만 P0-4의 조용한 칩을
            //   그대로 유지한다 — 이유는 StyleActionButton 문서 참고(한 화면에 12개가 반복된다).
            card.ActionSurface = UiChrome.AddSurface(rt, "Action",
                UiChrome.CardActionSurface, UiChrome.RadiusChip);
            card.ActionRect = card.ActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(card.ActionRect, CardPadX, CardActionY, CardActionWidth, CardActionHeight);
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
            //     착용 #838589 5.17:1 → pressed #67686B <b>3.44:1</b>
            //     해제 #9E814D 5.18:1 → pressed #7C653C <b>3.45:1</b>
            //   (★ 2026-09-03 강조색이 브라스가 되면서 [해제] 면이 #5087CC -> #9E814D로 따라 움직였다.
            //    숫자는 그때 다시 쟀고 <b>결론은 그대로</b>다 — 눌린 동안 AA 미달이 된다.)
            //   즉 <b>누르고 있는 동안 글자가 AA 미달</b>이 된다. 상태 색은 StyleActionButton이 값으로
            //   정하므로 uGUI의 자동 틴트는 꺼 둔다([✕]와 같은 처방).
            actionButton.transition = Selectable.Transition.None;
            card.ActionButton = actionButton;
            actionButton.onClick.AddListener(() =>
            {
                if (SuppressedByGridDrag()) return;   // 방금 민 손짓의 끝을 클릭으로 오인하지 않는다.
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
                if (SuppressedByGridDrag()) return;
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
            BuildIcon(root, entry.Icon, IconSize);
        }

        private static void BuildIcon(RectTransform root, ItemIconPart[] parts, float renderSize)
        {
            if (parts == null) return;
            float scale = renderSize / 40f;
            float stroke = IconStroke * (renderSize / IconSize);
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
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, renderSize, renderSize);
                        }
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, stroke, part.Color);
                        break;
                    }
                    case ItemIconPartKind.Polygon:
                    {
                        // 몸 경로(AccessoryCardIcon)와 <b>같은 순서</b>로 그린다: 면을 먼저 깔고 그 위에
                        // 윤곽선. 순서를 바꾸면 채움이 획을 반쯤 덮어 도형이 가늘어 보인다.
                        int count = Mathf.Min(part.PointCount, _iconPoints.Length);
                        for (int i = 0; i < count; i++)
                        {
                            _iconPoints[i] = FromViewBox(v[i * 2], v[i * 2 + 1], 40f, 40f, renderSize, renderSize);
                        }

                        // 규약상 마지막 점이 첫 점과 같다. 삼각분할에 중복점을 넣으면 퇴화 삼각형이 생긴다.
                        int fillCount = count;
                        if (fillCount > 1 && _iconPoints[fillCount - 1] == _iconPoints[0]) fillCount--;

                        AccessoryCardIcon.AddFill(root, "Fill", _iconPoints, fillCount, part.Color);
                        UiChrome.AddPolyline(root, "Seg", _iconPoints, count, stroke,
                            AccessoryShapeBuilder.FillOutlineColor(part.Color));
                        break;
                    }
                    case ItemIconPartKind.Ring:
                        UiChrome.AddCircle(root, "Ring", v[2] * 2f * scale, part.Color, stroke,
                            FromViewBox(v[0], v[1], 40f, 40f, renderSize, renderSize));
                        break;
                    case ItemIconPartKind.DashedRing:
                        BuildDashedRing(root, v[0], v[1], v[2], part.Color, renderSize);
                        break;
                    case ItemIconPartKind.Dot:
                        UiChrome.AddCircle(root, "Dot", v[2] * 2f * scale, part.Color, 0f,
                            FromViewBox(v[0], v[1], 40f, 40f, renderSize, renderSize));
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

        /// <summary>점선 원(FX "없음" 전용). 링 스프라이트에는 점선이 없어 짧은 호 8개로 그린다.
        /// <para>★ 반지름처럼 <b>길이</b>인 값은 viewBox(40) 대비 배율을 곱해야 한다 — 좌표는
        /// <see cref="FromViewBox"/>가 이미 환산하지만 반지름은 그 경로를 타지 않는다.</para></summary>
        private static void BuildDashedRing(RectTransform root, float cx, float cy, float r, Color color,
            float renderSize)
        {
            const int dashes = 8;
            const int pointsPerDash = 3;
            Vector2 center = FromViewBox(cx, cy, 40f, 40f, renderSize, renderSize);
            float radius = r * (renderSize / 40f);
            float stroke = IconStroke * (renderSize / IconSize);

            for (int d = 0; d < dashes; d++)
            {
                float start = d * (Mathf.PI * 2f / dashes);
                for (int i = 0; i < pointsPerDash; i++)
                {
                    float a = start + (Mathf.PI / dashes) * (i / (float)(pointsPerDash - 1));
                    _iconPoints[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                }
                UiChrome.AddPolyline(root, "Dash", _iconPoints, pointsPerDash, stroke, color);
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

        /// <summary>선택 상세 카드 — <b>컬럼 1 바닥</b>(§4-3-1). 썸네일 52 + 이름 + 메타 + 설명.
        /// <para>★ 주 버튼은 없다(L-9). 인계본 상세 카드에는 [착용하기]가 있지만 그것을 넣으면
        /// 2026-09-01에 사용자가 지워 달라고 한 것을 되살리는 것이고, 넣지 않는 결정이 세로 예산을
        /// 52pt 돌려준다(최소 창 높이 780 → 728).</para></summary>
        private void BuildDetailPanel(RectTransform col)
        {
            Image detail = UiChrome.AddSurface(col, "DetailCard", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            var drt = detail.rectTransform;
            _sectionDetailRect = drt;
            UiChrome.PlaceTopLeft(drt, Col1PadX, DetailCardY, Col1ContentWidth, DetailCardHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted), UiChrome.RadiusCard);

            const float DetailPadX = 14f;
            const float DetailThumbSize = 52f;
            float textX = DetailPadX + DetailThumbSize + UiChrome.Space3;
            float textWidth = Col1ContentWidth - textX - DetailPadX;

            _detailThumb = UiChrome.AddSurface(drt, "DetailThumb", UiChrome.ThumbSurfaceLocked, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(_detailThumb.rectTransform, DetailPadX, -DetailPadX,
                DetailThumbSize, DetailThumbSize);
            _detailThumb.raycastTarget = false;
            // 등급은 이 자리에서 <b>테두리</b>로만 말한다 — 면을 물들이면 글자 대비를 내주고
            // 신호는 하나도 못 산다(카드 바탕에서 이미 실측으로 기각된 것과 같은 이유).
            // ★ 생성값도 <b>RefreshDetail이 칠할 것과 같은 규칙</b>으로 만든다 — 두 벌이면 한쪽만 고쳐진다.
            _detailThumbOutline = UiChrome.AddOutline(_detailThumb.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.ThumbSurfaceLocked), UiChrome.RadiusThumb);

            _detailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                textX, -18f, textWidth, 20f, "—", bold: true);
            _detailMeta = Label(drt, "DetailMeta", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                textX, -44f, textWidth, 14f, "—");

            _detailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_detailBody.rectTransform, DetailPadX, -(DetailPadX + DetailThumbSize + UiChrome.Space2),
                Col1ContentWidth - DetailPadX * 2f, DetailCardHeight - DetailPadX * 2f - DetailThumbSize - UiChrome.Space2);
            _detailBody.lineSpacing = 1.6f;   // 스펙 line-height 1.6.

            // ★ 여기에 [착용]/[해제] 버튼을 다시 만들지 마라(2026-09-01 사용자 신고로 걷어냈다 — L-9).
            //   착용 손잡이는 카드 하단 하나뿐이고, 이 패널은 "고른 것이 무엇이고 왜 잠겼는가"만 말한다.
            //   되살아나면 InfoWindowSurfaceRegressionTests의 DetailPanelHasNoEquipButton이 잡는다.
        }
    }
}
