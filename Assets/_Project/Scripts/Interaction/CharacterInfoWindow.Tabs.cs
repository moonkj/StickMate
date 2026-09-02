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

            /// <summary>본문이 아직 없는 탭 — 문구 한 줄만 놓는다([상점], 2026-09-02).</summary>
            Placeholder,
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

            /// <summary><see cref="TabPage.Placeholder"/>가 본문 한가운데 적는 문구.</summary>
            public readonly string Notice;

            public TabDef(string name, TabPage page, bool appearanceSlots = false,
                int iconSet = NoIconSet, string notice = null)
            {
                Name = name;
                Page = page;
                AppearanceSlots = appearanceSlots;
                IconSet = iconSet;
                Notice = notice;
            }
        }

        /// <summary>[상점] 본문 문구. 실제 상점(재화·가격·세트)은 다음 라운드다 —
        /// 지금 여기에 값을 그리면 아직 확정되지 않은 경제 수치를 화면이 <b>주장</b>하게 된다
        /// (임계·가격이 설계 두 벌 사이에서 7건 어긋난 채 미해결이다).
        /// <para>문구는 설정창의 미완성 탭과 <b>같은 말투</b>다("… 다음 업데이트에 들어옵니다") —
        /// 같은 사실을 두 창이 다른 말로 하면 그게 두 뜻이 된다.</para></summary>
        private const string ShopNotice = "상점은 다음 업데이트에 들어옵니다.";

        /// <summary>★ 탭의 단일 출처. 인덱스가 곧 <see cref="Tab"/> 값이다.</summary>
        private static readonly TabDef[] TabTable =
        {
            new TabDef("장비",   TabPage.Cards, appearanceSlots: false, iconSet: 0),
            new TabDef("외형",   TabPage.Cards, appearanceSlots: true,  iconSet: 1),
            new TabDef("보관함", TabPage.Inventory),
            new TabDef("상점",   TabPage.Placeholder, notice: ShopNotice),
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
                    _selectedItem = 0;
                }
                RefreshCards();
                RefreshDetail();
            }

            Debug.Log($"[정보창] 탭 전환 -> [{def.Name}].");
        }

        private void ApplyTabVisibility()
        {
            ApplyTabDetailPlacement();   // 창 높이보다 먼저 — ApplyTabDetailPlacement 문서 참고.

            TabPage page = Def(_tab).Page;
            if (_sectionPage != null) _sectionPage.SetActive(page == TabPage.Cards);
            if (_inventoryPage != null) _inventoryPage.SetActive(page == TabPage.Inventory);
            ApplyPlaceholderPage(page == TabPage.Placeholder);

            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;

                // 준비 중인 탭은 <b>고르고 나서</b> 그렇게 보인다. 고르지 않은 탭을 더 흐리게 하지는
                // 않는다 — 설정창이 그걸 했다가 "죽은 탭에는 글자가 한 자도 없다"는 신고를 받았다
                // (SettingsWindow.ApplyTabVisibility의 같은 문단). 사실은 밑줄과 본문이 말한다.
                bool ready = TabTable[i].Page != TabPage.Placeholder;

                if (_tabLabels[i] != null)
                {
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                    _tabLabels[i].color = UiChrome.InkTab(active, ready);
                }
                if (_tabUnderlines[i] != null)
                {
                    _tabUnderlines[i].color = active
                        ? (ready ? UiChrome.TextPrimary : UiChrome.NonTextMuted)
                        : Color.clear;
                }
            }
        }

        // -------------------- 우측 탭 컬럼 --------------------

        private RectTransform BuildRightColumn(RectTransform body)
        {
            var go = new GameObject("RightColumn", typeof(RectTransform));
            go.transform.SetParent(body, false);
            var right = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(right, RightX, 0f, RightWidth, BodyHeight);
            return right;
        }

        /// <summary>밑줄 탭(스펙 1.3) — 칩/배경 없이 라벨 + 활성 탭 2px 밑줄 하나.</summary>
        private void BuildTabs(RectTransform right)
        {
            float x = RightPadX;
            for (int i = 0; i < TabCount; i++)
            {
                string name = TabTable[i].Name;
                float width = TabLabelWidth(name);

                Image hit = UiChrome.AddSurface(right, "Tab" + name, Color.clear, UiChrome.RadiusChip);
                var rt = hit.rectTransform;
                UiChrome.PlaceTopLeft(rt, x, TabStripY, width, TabStripHeight);

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontTitle, TextAnchor.UpperCenter,
                    UiChrome.InkTab(selected: false));
                UiChrome.Stretch(label.rectTransform);
                label.text = name;

                Image underline = UiChrome.AddSurface(rt, "Underline", Color.clear, 2);
                UiChrome.PlaceTopLeft(underline.rectTransform, 0f, -(TabStripHeight - TabUnderlineHeight),
                    width, TabUnderlineHeight);
                underline.raycastTarget = false;

                var button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                int captured = i;
                button.onClick.AddListener(() => { if (TryClaimAction("tab" + captured)) OnTabClicked((Tab)captured); });

                _tabRects[i] = rt;
                _tabLabels[i] = label;
                _tabUnderlines[i] = underline;
                x += width + TabGap;
            }

            Image line = UiChrome.AddSurface(right, "TabBottomLine", UiChrome.CardBorder, 2);
            UiChrome.PlaceTopLeft(line.rectTransform, RightPadX, TabStripY - TabStripHeight + 1f, RightContentWidth, 1f);
            line.raycastTarget = false;

            // 탭이 밑줄 밖으로 흘러나가면 <b>마지막 탭이 마스크에 잘려 눌리지 않는다</b>. 증상이
            // "안 눌린다"라서 원인 추적이 가장 비싼 종류다 — 늘리는 그 라운드에 알려 준다.
            float end = x - TabGap;                       // 마지막 탭의 오른쪽 끝
            float limit = RightPadX + RightContentWidth;  // 밑줄이 끝나는 선
            if (end > limit)
            {
                Debug.LogError($"[정보창] 탭 {TabCount}개가 {end:F0}pt에서 끝나 밑줄({limit:F0}pt)을 " +
                               $"{end - limit:F0}pt 넘겼습니다 — 마지막 탭이 잘립니다.");
            }
        }

        /// <summary>내장 폰트에는 폭 조회 API가 마땅치 않아 <b>글자 수 × 글자 크기</b>로 잡는다 —
        /// 한글은 정사각에 가까워 이 근사가 잘 맞는다. 넘침은 <see cref="BuildTabs"/> 끝의 검사가 잡는다
        /// (탭 4개 = 22..230pt, 밑줄 끝 614pt / 폭 1042에서는 776pt).</summary>
        private static float TabLabelWidth(string label) => label.Length * UiChrome.FontTitle + 4f;
    }
}
