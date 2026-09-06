using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 탭 모델과 탭 스트립. <b>탭에 관한 사실은 전부 이 파일에 있다</b> — enum / 표 / 페이지 종류 /
    /// 섹션 슬롯 규칙 / 탭 전환 / 밑줄 그리기.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나누면서 만든 파일이고,
    /// <b>이 라운드에 실제로 바뀐 것은 여기 모여 있다</b> — 흩어져 있던 탭 정의 3곳과 2분기 불리언
    /// 4곳을 표 하나로 합쳤다. 다른 조각들은 그 판정을 <b>호출만</b> 한다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 탭 — 사실은 표 하나뿐이다 ====================
        //
        // ★ 2026-09-02 — 예전에는 같은 사실이 <b>세 곳</b>에 흩어져 있었다:
        //       private enum Tab { Equipment, Appearance, Inventory }
        //       private const int TabCount = 3;
        //       private static readonly string[] TabNames = { "장비", "외형", "보관함" };
        //   셋 중 둘만 고치면 <b>컴파일은 되고 화면만 깨진다</b>(개수만 늘리면 이름표에서
        //   IndexOutOfRange, 이름만 늘리면 네 번째 탭이 영영 안 그려진다).
        //
        //   더 나빴던 것은 <b>2분기 불리언</b>이었다. SectionSlot / SectionCountForTab /
        //   IconSetForTab / OnTabClicked이 각각 `tab == Tab.Appearance`로 판단했고, 그건 곧
        //   <b>"외형이 아니면 장비"</b>라는 뜻이다. 네 번째 탭을 얹는 순간 [상점]이 그 else로 굴러떨어져
        //   <b>에러도 로그도 없이 [장비] 화면</b>이 됐을 것이다(총괄 검토 §4-4 P0).
        //
        //   이제 탭에 관한 사실은 <see cref="TabTable"/> 한 줄씩이고, 나머지는 전부 거기서 파생된다.
        //   표에 없는 값이 오면 <b>조용히 폴백하지 않고 LogError</b>를 찍는다.

        /// <summary>탭 하나가 어떤 <b>본문</b>을 쓰는가. 탭을 늘릴 때 새로 정할 것은 이 값 하나이고,
        /// 창 높이·카드 유무·클릭 라우팅·hover는 전부 여기서 갈린다.</summary>
        private enum TabPage
        {
            /// <summary>카테고리 섹션 + 가로 카드 캐러셀([장비]/[외형]).</summary>
            Cards,

            /// <summary>20줄 가상 목록([보관함]).</summary>
            Inventory,

            /// <summary>상품 격자 + 상세 줄([상점]).
            /// <para>★ 2026-09-06 — 여기 있던 <c>Placeholder</c>("본문이 아직 없는 탭 — 문구 한 줄만
            /// 놓는다")를 <b>지웠다</b>. 쓰는 탭이 [상점] 하나였고 그 본문이 이 라운드에 생겼다.
            /// 죽은 자리를 남겨 두면 <c>ShopNotice</c>("상점은 다음 업데이트에 들어옵니다")가 코드에
            /// 남아 언젠가 다시 화면에 나간다 — 그 문장은 이제 <b>거짓</b>이다.</para></summary>
            Shop,
        }

        /// <summary>탭 순서. <b>값을 박아 둔다</b> — 이 창의 로그·테스트가 정수 인덱스로 탭을 부르고
        /// (<see cref="TabScreenRect"/>), 중간에서 하나를 지우면 그 뒤가 한 칸씩 당겨진다
        /// (<see cref="StickmanStateId"/>가 실제로 겪은 사고와 같은 종류).</summary>
        private enum Tab { Equipment = 0, Appearance = 1, Inventory = 2, Shop = 3 }

        /// <summary>카드 페이지가 아니라 아이콘셋이 없다는 표시.</summary>
        private const int NoIconSet = -1;

        /// <summary>탭 한 칸의 정의 전부. <b>여기 없는 탭 지식은 코드 어디에도 없어야 한다.</b></summary>
        private readonly struct TabDef
        {
            public readonly string Name;
            public readonly TabPage Page;

            /// <summary><see cref="TabPage.Cards"/>에서만 의미가 있다 — 이 탭이 펼치는 카테고리가
            /// 외형 계열인가(<see cref="EquipmentModel.IsAppearanceSlot"/>가 유일한 정의다).</summary>
            public readonly bool AppearanceSlots;

            /// <summary>카드가 미리 구워 두는 아이콘 두 벌 중 어느 쪽을 켤 것인가.
            /// 카드 페이지가 아니면 <see cref="NoIconSet"/>.</summary>
            public readonly int IconSet;

            public TabDef(string name, TabPage page, bool appearanceSlots = false,
                int iconSet = NoIconSet)
            {
                Name = name;
                Page = page;
                AppearanceSlots = appearanceSlots;
                IconSet = iconSet;
            }
        }

        // ★ 2026-09-06 — 여기 있던 <c>ShopNotice</c>("상점은 다음 업데이트에 들어옵니다.")를 지웠다.
        //   [상점] 본문이 생겼으므로 그 문장은 <b>거짓</b>이 됐다. 문구만 지우고 상수를 남기면
        //   다음 사람이 그것을 다시 어딘가에 그린다(테마 세트 문구가 R21에서 겪은 그 형태).

        /// <summary>★ 탭의 단일 출처. 인덱스가 곧 <see cref="Tab"/> 값이다.</summary>
        private static readonly TabDef[] TabTable =
        {
            new TabDef("장비",   TabPage.Cards, appearanceSlots: false, iconSet: 0),
            new TabDef("외형",   TabPage.Cards, appearanceSlots: true,  iconSet: 1),
            new TabDef("보관함", TabPage.Inventory),
            new TabDef("상점",   TabPage.Shop),
        };

        /// <summary>탭 수. 상수로 적지 않고 <b>표에서 센다</b>.</summary>
        private static int TabCount => TabTable.Length;

        /// <summary>표와 enum이 어긋났는지 <b>한 번</b> 확인한다. 이 검사가 없으면 enum에만 값을 더한
        /// 라운드가 아무 증상 없이 지나가고, 그 탭은 눌러도 [장비]가 뜬다.</summary>
        static CharacterInfoWindow()
        {
            int declared = System.Enum.GetValues(typeof(Tab)).Length;
            if (TabTable.Length != declared)
            {
                Debug.LogError($"[정보창] 탭 표({TabTable.Length}칸)와 enum({declared}개)이 어긋났습니다 — " +
                               "표에 없는 탭은 눌러도 아무 본문이 없습니다.");
            }

            for (int i = 0; i < TabTable.Length; i++)
            {
                TabDef def = TabTable[i];
                if (def.Page != TabPage.Cards) continue;
                if ((uint)def.IconSet < (uint)IconSetCount) continue;
                Debug.LogError($"[정보창] 탭 [{def.Name}]이 없는 아이콘셋 {def.IconSet}을 가리킵니다 " +
                               $"(구워 둔 벌 {IconSetCount}). 카드 아이콘이 빈 채로 뜹니다.");
            }

            // ★ 인계본 값(29 / 46)이 우리 접근성 하한을 <b>실제로</b> 넘는지 여기서 비교한다.
            //   숫자를 주석에 적어 두면 하한이 움직였을 때 조용히 갈라진다(CLAUDE.md 하드코딩 금지의 취지).
            if (CardActionHeight < UiChrome.MinTargetSizePoints)
            {
                Debug.LogError($"[정보창] 카드 [착용] 버튼 높이 {CardActionHeight}가 " +
                               $"WCAG 2.2 2.5.8 하한 {UiChrome.MinTargetSizePoints}보다 작습니다.");
            }
            if (SlotRowHeight < UiChrome.MinTargetSizePoints)
            {
                Debug.LogError($"[정보창] 착용 슬롯 행 높이 {SlotRowHeight}가 " +
                               $"WCAG 2.2 2.5.8 하한 {UiChrome.MinTargetSizePoints}보다 작습니다.");
            }

            // ★ [상점] 격자의 열 수는 폭에서 <b>파생</b>된다(숫자를 적지 않는다). 창이나 카드 폭이
            //   움직여 이 값이 0이 되면 격자가 나눗셈에서 죽는다 — 그 전에 여기서 말한다.
            if (ShopColumns < 1)
            {
                Debug.LogError($"[상점] 상품 격자의 열 수가 {ShopColumns}입니다 — 본문 폭 " +
                               $"{PageContentWidth}pt에 카드 한 장({CardWidth}pt)도 들어가지 않습니다.");
            }
        }

        /// <summary>이 탭의 정의. 표 밖이면 <b>조용히 넘어가지 않는다</b>.</summary>
        private static TabDef Def(Tab tab)
        {
            int i = (int)tab;
            if ((uint)i < (uint)TabTable.Length) return TabTable[i];
            Debug.LogError($"[정보창] 표에 없는 탭 {i}입니다 — enum만 늘리고 TabTable을 잊었습니다.");
            return TabTable[0];
        }

        /// <summary>부팅 로그 한 줄에 쓰는 탭 이름 나열. <b>Start()에서 한 번만</b> 부른다 —
        /// 문자열을 만드는 유일한 자리이고 상주 루프에는 없다.</summary>
        private static string TabNamesForLog()
        {
            var sb = new System.Text.StringBuilder(32);
            for (int i = 0; i < TabTable.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(TabTable[i].Name);
            }
            return sb.ToString();
        }

        /// <summary>이 아이콘셋을 쓰는 카드 탭을 <b>표에서 찾는다</b>. `set == 1 ? 외형 : 장비`로 적어 두면
        /// 카드 탭이 셋이 되는 순간 세 번째 벌이 조용히 [장비] 아이콘으로 구워진다.</summary>
        private static Tab TabForIconSet(int iconSet)
        {
            for (int i = 0; i < TabTable.Length; i++)
            {
                TabDef def = TabTable[i];
                if (def.Page == TabPage.Cards && def.IconSet == iconSet) return (Tab)i;
            }
            Debug.LogError($"[정보창] 아이콘셋 {iconSet}을 쓰는 카드 탭이 표에 없습니다.");
            return Tab.Equipment;
        }

        // ==================== 조작 ====================

        private void OnTabClicked(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            EndNameEdit(commit: true);
            ApplyTabVisibility();   // 카드 갱신보다 먼저(RefreshAll과 같은 이유 — 그 문단 참고).

            TabDef def = Def(tab);

            // 선택이 이 탭에 없는 카테고리를 가리키고 있으면 첫 카테고리로 옮긴다 — 그러지 않으면
            // [외형] 탭에서 [장비] 아이템의 설명이 보인다(화면과 상세가 다른 말을 하는 상태).
            if (def.Page == TabPage.Cards)
            {
                if (EquipmentModel.IsAppearanceSlot(_selectedSlot) != def.AppearanceSlots)
                {
                    _selectedSlot = SectionSlot(tab, 0);
                    // ★ 0번이 아니라 <b>보여주는 목록의 첫째</b>다(2026-09-06 이펙트 「없음」 은퇴).
                    //   [외형]의 첫 카테고리가 바로 이펙트라, 0번을 그대로 쓰면 상세 패널이
                    //   <b>화면에 카드가 없는 아이템</b>을 설명하게 된다.
                    _selectedItem = Mathf.Max(0, ItemCatalog.ListedItemIndex(_selectedSlot, 0));
                }
                RefreshCards();
                RefreshDetail();
            }

            // [상점]으로 들어오는 동안 잔액이 올랐을 수 있다 — 들어오는 그 프레임에 다시 칠한다
            // (탭이 꺼져 있는 동안에는 0.25초 주기 갱신도 이 페이지를 보지 않는다).
            if (def.Page == TabPage.Shop) RefreshShop();

            Debug.Log($"[정보창] 탭 전환 -> [{def.Name}].");
        }

        private void ApplyTabVisibility()
        {
            // ★ L-8 — 여기 있던 ApplyTabDetailPlacement() 호출을 지웠다. 창 높이가 탭마다 달라지던
            //   시절의 장치이고, 3컬럼에서는 상세 카드가 컬럼 1 <b>바닥에 고정</b>이라 옮길 것이 없다.
            TabPage page = Def(_tab).Page;
            bool cards = page == TabPage.Cards;
            // 컬럼 1·2는 <b>카드 탭의 것</b>이고, 좁은 창에서는 폭이 또 한 번 접는다 —
            // 두 조건의 곱은 ApplyColumnVisibility 한 곳에서만 계산한다.
            ApplyColumnVisibility();
            if (_sectionPage != null) _sectionPage.SetActive(cards);
            if (_inventoryPage != null) _inventoryPage.SetActive(page == TabPage.Inventory);
            ApplyShopPage(page == TabPage.Shop);

            // ★ 2026-09-06 — 여기 있던 <c>ready</c>(= 준비 중 탭인가) 분기와 <b>탭 밑줄</b>을 지웠다.
            //   네 탭이 전부 본문을 갖게 되면서 <c>ready</c>가 언제나 참이 됐고, 밑줄은 오직
            //   <c>active && !ready</c>에서만 색이 있었으므로 <b>영원히 투명한 겹</b>이 된다.
            //   죽은 겹을 남기면 다음 사람이 "왜 안 보이지"를 조사하게 된다(무대 조명 두 겹을 지운 것과
            //   같은 관례). 활성 표시의 주 채널은 원래부터 <b>면</b>이었다.
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;

                if (_tabSurfaces[i] != null)
                {
                    // 활성 탭은 브라스 면(7.37:1). 비활성은 스트립 바탕 그대로.
                    _tabSurfaces[i].color = active ? UiChrome.Accent : Color.clear;
                }
                if (_tabLabels[i] != null)
                {
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                    // 면에서 잉크를 파생시킨다 — 브라스 위에서는 InkOnSurface가 어두운 잉크로 뒤집는다.
                    _tabLabels[i].color = active
                        ? UiChrome.InkOnSurface(UiChrome.Accent, UiChrome.InkRole.Title, enabled: true)
                        : UiChrome.InkTab(selected: false);
                }
            }
        }

        // -------------------- 헤더 안의 탭 스트립 (§4-2) --------------------

        /// <summary>
        /// 인계본 탭 스트립 — <b>칩 4개가 든 상자</b>다(옛 밑줄 탭에서 바뀌었다).
        /// 상자는 <see cref="UiChrome.CardSurfaceMuted"/>에 <see cref="UiChrome.CardBorder"/> 테두리이고,
        /// 활성 탭만 <see cref="UiChrome.Accent"/> 면 + <see cref="UiChrome.OnAccentSolid"/> 글자다
        /// (대비 7.37 / 7.91 — §4-4 실측).
        ///
        /// <para><paramref name="x"/>는 상자 왼쪽 끝(헤더 좌표). 탭 폭은 <b>폰트에게 묻는다</b> —
        /// 글자 수 모형은 한글에서만 맞고 라틴에서는 반쯤 빈 상자를 남긴다.</para>
        /// </summary>
        private void BuildTabs(Transform header, float x)
        {
            Image strip = UiChrome.AddSurface(header, "TabStrip", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            RectTransform stripRect = strip.rectTransform;
            strip.raycastTarget = false;
            UiChrome.AddOutline(stripRect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurfaceMuted), UiChrome.RadiusCard);

            float cursor = HeaderTabStripPad;
            for (int i = 0; i < TabCount; i++)
            {
                string name = TabTable[i].Name;

                Image face = UiChrome.AddSurface(stripRect, "Tab" + name, Color.clear, UiChrome.RadiusChip);
                var rt = face.rectTransform;

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontTitle, TextAnchor.MiddleCenter,
                    UiChrome.InkTab(selected: false));
                UiChrome.Stretch(label.rectTransform);
                float width = TabLabelWidth(label, name);
                UiChrome.PlaceTopLeft(rt, cursor, -HeaderTabStripPad, width, HeaderTabHeight);

                // ★ 2026-09-06 — 여기 있던 밑줄 겹을 지웠다. 그것은 <b>준비 중 탭 전용</b> 표식이었고
                //   (활성 + 준비 중일 때만 색이 있었다), 네 탭이 전부 본문을 갖게 되면서 영원히
                //   투명해졌다. 활성 표시는 면이 한다(ApplyTabVisibility의 같은 문단).

                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                int captured = i;
                button.onClick.AddListener(() => { if (TryClaimAction("tab" + captured)) OnTabClicked((Tab)captured); });

                _tabRects[i] = rt;
                _tabLabels[i] = label;
                _tabSurfaces[i] = face;
                cursor += width + HeaderTabGap;
            }

            float stripWidth = cursor - HeaderTabGap + HeaderTabStripPad;
            UiChrome.PlaceTopLeft(stripRect, x, -(HeaderHeight - HeaderTabStripHeight) * 0.5f,
                stripWidth, HeaderTabStripHeight);
            _tabStripRightEdge = x + stripWidth;

            // 탭이 오른쪽 칩 무리와 겹치면 <b>마지막 탭이 눌리지 않는다</b>. 증상이 "안 눌린다"라서
            // 원인 추적이 가장 비싼 종류다 — 늘리는 그 라운드에 알려 준다.
            float end = x + stripWidth;
            float limit = PanelWidth - HeaderChipBlockWidth - UiChrome.Space4;
            if (end > limit)
            {
                Debug.LogError($"[정보창] 탭 {TabCount}개가 {end:F0}pt에서 끝나 헤더 오른쪽 칩 무리" +
                               $"({limit:F0}pt)를 {end - limit:F0}pt 넘겼습니다 — 마지막 탭이 겹칩니다.");
            }
        }

        /// <summary>
        /// 탭 상자 폭 = <b>실측 글자 폭</b> + 좌우 여백.
        ///
        /// <para>★ 2026-09-03 — 옛 식은 <c>label.Length × UiChrome.FontTitle + 4f</c>였고 주석은
        /// <i>"내장 폰트에는 폭 조회 API가 마땅치 않아"</i>라고 적고 있었다. <b>그 전제가 틀렸다</b> —
        /// <see cref="UnityEngine.UI.Text.preferredWidth"/>가 바로 그 API이고, 같은 저장소의
        /// <c>UiChrome.Ellipsize</c>가 이미 그것을 쓰고 있었다. 글자 수 모형은 한글에서만 맞고
        /// 라틴에서는 <b>반쯤 빈 상자</b>를 남긴다(그 문장도 이미 <c>UiChrome.Ellipsize</c> 문서에 있다).</para>
        ///
        /// <para>여백 <c>4f</c>는 <b>옛 식에서 그대로</b> 가져왔다. 이번 변경의 효과를 '측정으로 바꾼 것'
        /// 하나로 유지하기 위해서다 — 여백까지 같이 손대면 회귀 판정이 불가능해진다.</para>
        ///
        /// <para>넘침은 <see cref="BuildTabs"/> 끝의 검사가 잡는다(헤더 오른쪽 칩 무리와의 겹침).</para>
        /// </summary>
        private static float TabLabelWidth(Text label, string name)
            => SettingsControls.MeasuredWidth(label, name) + HeaderTabPadX * 2f;
    }
}
