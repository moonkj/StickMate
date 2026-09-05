using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// 컬럼 2 「능력치 / STATUS」 + 「테마 세트 / SET」, 그리고 그 둘이 들어오면서 필요해진
    /// <b>컬럼 2 세로 스크롤</b>.
    ///
    /// ============================================================================
    /// 왜 이 조각이 생겼나
    /// ============================================================================
    /// <c>CharacterInfoWindow.cs</c>가 2026-09-05 이식 라운드에 이 자리를 <b>일부러 비워 뒀다</b>
    /// (그 파일의 「인계본 컬럼 2의 첫 블록은 능력치인데 그 런타임이 0줄이다」 문단). 사용자 확정
    /// *"1.0에 전부 넣어야함"*으로 그 자리를 채우는 라운드이고, 좌표 정본은
    /// <c>docs/UX_EQUIPMENT_WINDOW_3COL_PORT.md</c> §4-3-2, 문자열 정본은
    /// <c>docs/DESIGN_SYSTEMS_STATS.md</c> §0(교정 14/14) · §1-2 · §1-4 · §1-7 · §7-3 · §7-4다.
    ///
    /// <para>★ <b>파일을 쪼갤 때 파일명으로 소스를 찾는 감사가 눈이 먼다</b>(2026-09-02 실제 사고).
    /// 이 조각은 <c>CharacterInfoWindow.</c> 접두사 + <c>.cs</c> 확장자라는 기존 명명 규칙을
    /// 그대로 따랐고, 그 감사
    /// (<c>UiInteractionFramePacingHoldTests.ReadSurfaceSource</c> → <c>SourceConstantReader.ReadSurfaceText</c>)는
    /// 그 접두사로 시작하는 파일을 <b>전부</b> 이어 읽으므로 자동으로 따라온다. 접두사를 다르게
    /// 지으면 그 순간 감사가 이 파일을 못 본다.</para>
    ///
    /// ============================================================================
    /// ★ 세로 예산 — 설계 폭에서는 <b>들어간다</b>. 여유 16pt다
    /// ============================================================================
    /// 스탯 카드 4장(326pt)이 들어오면 컬럼 2가 넘친다는 것이 §4-3-2의 예상이었다
    /// (*"사용 750 &gt; 예산 695 → 스크롤한다"*). 우리 세트 패널이 인계본 193이 아니라 56이고
    /// 블록 사이 간격을 제목-내용 간격(8)보다 크게(12) 잡으면 <b>넘치지 않는다</b>:
    /// <code>
    /// 위 여백 20
    ///  + STATUS  18 + 8 + 326 = 352
    ///  + 12
    ///  + RECORD  18 + 8 + 21 + 8 + 104 = 159
    ///  + 12
    ///  + DISPLAY 18 + 8 + 25 = 51
    ///  + 12
    ///  + SET     18 + 8 + 56 = 82
    ///  + 아래 여백 20                          = 720   vs  BodyHeight 736   → 여유 16
    /// </code>
    /// 이 등식은 <c>CharacterStatColumnLayoutTests</c>가 <b>상수에서 다시 세서</b> 잠근다 —
    /// 숫자 하나를 늘리는 사람이 그 자리에서 알게 된다.
    ///
    /// ============================================================================
    /// 그래도 스크롤을 붙인 이유 — 설계 폭이 아닌 화면이 있다
    /// ============================================================================
    /// <c>ClampPanelToScreen</c>이 세로가 짧은 화면에서 창을 줄인다(§3-4: Windows 1920×1080@150%는
    /// 가용 688로 40pt 모자란다). 그때 컬럼 2 아래쪽은 <c>RectMask2D</c>에 잘려 <b>영영 못 보였다</b> —
    /// 세로 강등 사다리가 아직 없기 때문이다(<c>CharacterInfoWindow.Layout.cs</c>의 그 문단).
    /// 스크롤이 그 구멍을 닫는다. <b>설계 폭에서는 최대 스크롤이 0이라 화면이 예전과 같다.</b>
    ///
    /// <para>드래그가 두 벌인 이유는 컬럼 3 격자와 <b>같다</b> — 이 창의 실제 클릭 경로는 전역 폴링이고
    /// uGUI 이벤트는 앱이 활성화된 뒤에만 도착한다. 두 경로가 「잡은 순간의 content.y + 이동량」이라는
    /// 같은 절대값 공식을 쓰므로 동시에 돌아도 더해지지 않는다(<c>DragGridTo</c> 문단과 같은 유도).</para>
    ///
    /// ============================================================================
    /// ★ 임계 효과 문구(§1-5) 12칸은 <b>그리지 않는다</b>
    /// ============================================================================
    /// <see cref="EquipmentStatRules.IsTierEffectActive"/>가 오늘 <b>12칸 전부 false</b>이고
    /// <see cref="EquipmentStatRules.ActiveTierEffectCount"/>가 0이다. 문구를 그리면 화면이
    /// 「오라 이펙트 발현」처럼 <b>일어나지 않는 일</b>을 약속하게 된다(원칙 1 — 유예 자동 해금이
    /// 폐기됐는데 화면이 계속 약속하던 그 결함과 같은 형태).
    /// <b>효과가 하나라도 배선되면</b> 그 함수가 true를 돌려주기 시작하고, 그때 이 자리에
    /// <c>TierEffectText</c>를 켜면 된다 — 자리는 3행에 이미 있다(단계 칩 오른쪽).
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 치수 (§4-3-2) ====================

        /// <summary>주스탯 축의 개수. <b>숫자를 적지 않는다</b> — 규칙(<see cref="EquipmentStatRules"/>)이
        /// 정한 값을 그대로 쓴다. 그 값 자체가 저장 필드 <c>statTierReached[]</c>의 칸 수에서
        /// 유도돼 있어서, 세 축(규칙 · 저장 · 화면)이 <b>한 숫자</b>에 묶인다.</summary>
        internal const int StatAxisCount = EquipmentStatRules.StatCount;

        /// <summary>임계 눈금 개수 = 임계 단계 수. ★ <see cref="EquipmentStatRules.TierCount"/>는
        /// <c>readonly</c> 배열 길이라 <c>const</c>가 아니다 — 배열 크기는 저장 상한
        /// (<see cref="CurrencyRules.MaxStatTier"/>)에서 잡고, <b>둘이 같은지는 테스트가 매 실행 잰다</b>
        /// (Core가 자기 파일에 적어 둔 것과 같은 계약이다).</summary>
        internal const int StatTickCount = CurrencyRules.MaxStatTier;

        internal const float StatCardHeight = 74f;
        internal const float StatCardGap = 10f;
        internal const float StatCardPadX = 11f;
        internal const float StatCardPadY = 11f;

        /// <summary>스탯 카드 1행(이름 · 출처 · 총합 · 보너스).</summary>
        internal const float StatHeadHeight = 18f;

        /// <summary>1행 ↔ 게이지 ↔ 3행 사이 간격(§4-3-2 내부 검산의 8).</summary>
        internal const float StatRowGap = 8f;

        /// <summary>총합 게이지 두께. 컬럼 2의 STRESS 트랙(<see cref="TrackHeight"/> 4)보다 1pt 두껍다 —
        /// 인계본 값이고, 이 게이지만 <b>임계 눈금</b>을 얹기 때문이다.</summary>
        internal const float StatGaugeHeight = 5f;

        /// <summary>스탯 카드 3행(단계 칩 + 「중급까지 N」).</summary>
        internal const float StatFootHeight = 13f;

        /// <summary>단계 칩 높이(§4-3-2). 3행보다 높아서 위아래로 2.5pt씩 패딩을 파고든다 —
        /// 패딩이 <see cref="StatCardPadY"/> 11이라 카드 밖으로는 나가지 않는다.</summary>
        internal const float StatTierChipHeight = 18f;
        internal const float StatTierChipPadX = 7f;

        /// <summary>카드 내부 콘텐츠 폭.</summary>
        internal const float StatCardContentWidth = Col2ContentWidth - StatCardPadX * 2f;   // 234

        // 1행 오른쪽 두 칸 — 오른쪽 끝에서 안쪽으로 쌓는다(§7-3: 값도 증분도 최대 2자리).
        internal const float StatBonusWidth = 40f;    // "(+33)"
        internal const float StatTotalWidth = 28f;    // "41"
        internal const float StatValueGap = 4f;
        internal const float StatNameWidth = 54f;     // "관찰력"
        internal const float StatNameGap = 6f;

        /// <summary>3행 오른쪽 「중급까지 N」 칸.</summary>
        internal const float StatRemainWidth = 92f;

        /// <summary>단계 칩이 가질 수 있는 최대 폭 — 「중급까지 N」과 겹치지 않는 한계다.
        /// <b>굽는 쪽과 재는 쪽이 같은 값을 본다</b>(둘이 갈라지면 긴 낱말에서만 겹친다).</summary>
        internal const float StatTierChipMaxWidth = StatCardContentWidth - StatRemainWidth - StatValueGap;

        /// <summary>스탯 카드 4장이 실제로 먹는 높이.</summary>
        internal const float StatCardsHeight = StatAxisCount * StatCardHeight
                                              + (StatAxisCount - 1) * StatCardGap;          // 326

        /// <summary>섹션 제목 오른쪽 「장비 합 +24」 칸.</summary>
        internal const float StatSumWidth = 74f;

        /// <summary>블록(능력치 / 기록 / 표시 / 테마 세트) <b>사이</b> 간격.
        /// <para>★ 2026-09-05 <b>24 → 12</b>. 스탯 카드 326pt가 들어오면서 세로 예산이 20pt 모자랐고
        /// (계산은 클래스 문서), 줄일 수 있는 곳이 여기와 제목-내용 간격뿐이었다.
        /// <b>블록 사이가 제목-내용보다 커야</b> 제목이 자기 내용에 붙어 읽힌다 — 그 순서를 지키면서
        /// 24+12를 12+8로 내렸다. 인계본은 둘 다 14이고, 그 값이면 20pt 넘친다.</para></summary>
        internal const float Col2BlockGap = UiChrome.Space3;

        /// <summary>제목줄과 <b>그 내용</b> 사이 간격. 위 문단과 짝이다 — 항상
        /// <see cref="Col2BlockGap"/>보다 작아야 하고, 그 부등식을
        /// <c>CharacterStatColumnLayoutTests</c>가 잠근다.</summary>
        internal const float Col2LabelGap = UiChrome.Space2;

        /// <summary>구분선을 뺀 컬럼 2의 실제 표시 폭(뷰포트 폭).</summary>
        internal const float Col2ViewportWidth = Col2Width - DividerThickness;   // 291

        /// <summary>세트 패널 높이.</summary>
        internal const float SetPanelHeight = 56f;

        /// <summary>세트 패널 1행 「오피스 워커 3/4」.</summary>
        internal const float SetProgressHeight = 16f;

        /// <summary>세트 패널 2행 「4부위 합계 +8」 / 「미완성 · 기본 중립 대사」.</summary>
        internal const float SetDetailHeight = 14f;

        internal const float SetRowGap = 6f;

        /// <summary>2행의 위 끝. <b>1행에서 파생시킨다</b> — 세 값 중 하나가 바뀌면 따라온다.
        /// 검산: 11 + 16 + 6 + 14 + 9(아래 여백) = 56 = <see cref="SetPanelHeight"/>.</summary>
        internal const float SetDetailY = -(StatCardPadY + SetProgressHeight + SetRowGap);

        // 블록 색인 — 순서가 곧 화면의 위에서 아래다.
        private const int Col2BlockStatus = 0;
        private const int Col2BlockRecord = 1;
        private const int Col2BlockDisplay = 2;
        private const int Col2BlockSet = 3;
        private const int Col2BlockCount = 4;

        // ==================== 실물 ====================

        /// <summary>컬럼 2의 세로 스크롤. 컬럼 3 격자와 <b>같은 설정</b>이다(Clamped · 관성 없음).</summary>
        private ScrollRect _col2Scroll;
        private RectTransform _col2Viewport;
        private RectTransform _col2Content;

        private bool _col2Grabbed;
        private bool _col2Moved;
        private float _col2GrabScreenY;
        private float _col2StartContentY;

        /// <summary>세로 흐름의 한 덩어리. 블록 하나의 높이가 바뀌면 <b>아래 블록의 y가 전부
        /// 움직인다</b> — 그래서 좌표를 네 곳에 굳히지 않고 <see cref="LayoutColumn2"/> 한 곳에서
        /// 정한다(<see cref="LayoutCardGrid"/>와 같은 이유·같은 형태).</summary>
        private sealed class Col2Block
        {
            public RectTransform Rect;
            public float Height;
        }

        private readonly Col2Block[] _col2Blocks = new Col2Block[Col2BlockCount];

        /// <summary>스탯 카드 한 장의 부품. 문자열은 <b>입력이 바뀐 프레임에만</b> 다시 만든다 —
        /// <see cref="RefreshNumbers"/>가 0.25초마다 도는 경로라, 무조건 만들면 하루 종일 켜 두는
        /// 앱에서 쓰레기가 계속 쌓인다(카드 이름의 <see cref="ItemCard.NameSource"/>와 같은 장치).</summary>
        private sealed class StatCardView
        {
            public RectTransform Rect;
            public Text Name;
            public Text Source;
            public Text Total;
            public Text Bonus;
            public RectTransform GaugeFill;
            public readonly Image[] Ticks = new Image[StatTickCount];
            public Image ChipSurface;
            public RectTransform ChipRect;
            public Text ChipLabel;
            public Text Remain;

            /// <summary>마지막으로 그린 입력. <c>HasShown</c>이 false면 무조건 다시 그린다.</summary>
            public bool HasShown;
            public int ShownTotal;
            public int ShownBonus;
            public int ShownTier;
            public int ShownReachedTier;
        }

        private readonly StatCardView[] _statCards = new StatCardView[StatAxisCount];

        /// <summary>섹션 제목 오른쪽 「장비 합 +24」.</summary>
        private Text _statSumValue;

        /// <summary>세트 패널 — 준비 중 문구 한 벌과 진행도 두 줄 한 벌을 <b>둘 다</b> 만들어 두고
        /// 켜고 끈다. 문구를 그때그때 바꾸면 「준비 중」과 「미완성」이 같은 Text에 섞여
        /// 어느 쪽이 참인지 로그로 되짚을 수 없다.
        /// <para>★ 2026-09-06 정정 — <b>진행도 쪽이 실제로 뜬다.</b> 앞선 서술("기본 42종의 테마가
        /// 전부 미배정이라 항상 false")은 <c>coder-systems</c>가 기본 24종(스탯 4슬롯 × 6테마)에
        /// 테마를 배정하면서 <b>거짓이 됐다</b>(<see cref="ItemCatalog.ThemeOfItem"/>).
        /// <see cref="CharacterStatReadout.TryGetThemeProgress"/>는 <b>테마가 붙은 것을 한 부위라도
        /// 걸치면</b> true다. 준비 중 문구가 남는 경우는 「스탯 4슬롯에 테마가 붙은 것이 하나도 없다」
        /// — 아무것도 안 걸쳤거나 외형·팩 아이템만 걸쳤을 때다.</para>
        /// <para>★ 그래서 그 준비 중 문구(<c>SetPanelNotice</c>, <c>CharacterInfoWindow.cs</c>)가
        /// 아직 「다음 업데이트에 들어옵니다」라고 말하는데 <b>기능은 이미 들어와 있다</b>.
        /// 문안은 <c>ux-designer</c>·<c>design-narrative</c> 소관이라 여기서 지어내지 않고
        /// <b>보고에 올렸다</b>.</para></summary>
        private GameObject _setNoticeRoot;
        private GameObject _setReadyRoot;
        private Text _setProgress;
        private Text _setDetail;

        // ==================== 굽기 ====================

        /// <summary>
        /// 컬럼 2 루트를 <b>스크롤 컨테이너</b>로 만든다. 세로 구분선(<c>Col2Rule</c>)은 뷰포트 <b>밖</b>에
        /// 둔다 — 안에 넣으면 콘텐츠와 함께 밀려 올라가 컬럼 경계가 중간에서 끊긴다.
        /// </summary>
        private RectTransform BuildColumn2Scroll(RectTransform col)
        {
            var viewportGo = new GameObject("Col2Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(col, false);
            _col2Viewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(_col2Viewport, 0f, 0f, Col2ViewportWidth, BodyHeight);

            var contentGo = new GameObject("Col2Content", typeof(RectTransform));
            contentGo.transform.SetParent(_col2Viewport, false);
            _col2Content = contentGo.GetComponent<RectTransform>();
            _col2Content.anchorMin = _col2Content.anchorMax = _col2Content.pivot = new Vector2(0f, 1f);
            _col2Content.sizeDelta = new Vector2(Col2ViewportWidth, BodyHeight);
            _col2Content.anchoredPosition = Vector2.zero;

            _col2Scroll = col.gameObject.GetComponent<ScrollRect>();
            if (_col2Scroll == null) _col2Scroll = col.gameObject.AddComponent<ScrollRect>();
            _col2Scroll.viewport = _col2Viewport;
            _col2Scroll.content = _col2Content;
            _col2Scroll.horizontal = false;
            _col2Scroll.vertical = true;
            // 컬럼 3과 같은 이유로 Clamped + 관성 없음 — 전역 폴링 드래그와 계산이 같아야 한다.
            _col2Scroll.movementType = ScrollRect.MovementType.Clamped;
            _col2Scroll.inertia = false;
            _col2Scroll.scrollSensitivity = StatCardHeight * 0.5f;
            _col2Scroll.horizontalScrollbar = null;
            _col2Scroll.verticalScrollbar = null;
            return _col2Content;
        }

        /// <summary>블록 하나를 만들어 등록한다. y는 <see cref="LayoutColumn2"/>가 정하므로
        /// 여기서는 <b>폭과 높이만</b> 맞춘다.</summary>
        private RectTransform BuildCol2Block(RectTransform content, int index, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);
            var rt = go.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(rt, 0f, 0f, Col2ViewportWidth, height);
            _col2Blocks[index] = new Col2Block { Rect = rt, Height = height };
            return rt;
        }

        /// <summary>
        /// 「능력치 / STATUS」 블록 — 제목줄(+ 장비 합) + 스탯 카드 4장.
        /// <para>★ <b>「준비 중」 분기를 두지 않는다.</b> <see cref="EquipmentStatRules.CurrentBuild"/>는
        /// 아무것도 안 걸친 상태에서도 <c>BASE</c>(8/6/5/7)라는 <b>참인 값</b>을 돌려준다 — 값이 없는
        /// 상태가 존재하지 않으므로 그 분기는 도달 불가능한 코드가 된다.</para>
        /// </summary>
        private void BuildStatusBlock(RectTransform content)
        {
            RectTransform block = BuildCol2Block(content, Col2BlockStatus, "Col2Status",
                SectionLabelHeight + Col2LabelGap + StatCardsHeight);

            BuildSectionLabel(block, "능력치", "STATUS", Col2PadX, 0f);

            // 제목줄 오른쪽 「장비 합 +24」(§4-3-2 · §0 교정).
            _statSumValue = Label(block, "StatSum", UiChrome.FontLabel, TextAnchor.MiddleRight,
                UiChrome.Accent, Col2PadX + Col2ContentWidth - StatSumWidth, 0f,
                StatSumWidth, SectionLabelHeight, CharacterStatReadout.NoValue);
            _statSumValue.raycastTarget = false;

            var cardsGo = new GameObject("StatCards", typeof(RectTransform));
            cardsGo.transform.SetParent(block, false);
            var cards = cardsGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(cards, 0f, -(SectionLabelHeight + Col2LabelGap),
                Col2ViewportWidth, StatCardsHeight);
            for (int i = 0; i < _statCards.Length; i++) _statCards[i] = BuildStatCard(cards, i);
        }

        /// <summary>스탯 카드 한 장(폭 256 · 높이 74). 내부 좌표 검산은
        /// <c>11 + 18 + 8 + 5 + 8 + 13 + 11 = 74</c>(§4-3-2)이고, 그 등식을
        /// <c>CharacterStatCardLayoutTests</c>가 상수에서 직접 다시 센다.</summary>
        private StatCardView BuildStatCard(RectTransform parent, int index)
        {
            Image surface = UiChrome.AddSurface(parent, "StatCard" + index,
                UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            RectTransform rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, Col2PadX, -index * (StatCardHeight + StatCardGap),
                Col2ContentWidth, StatCardHeight);
            surface.raycastTarget = false;
            UiChrome.AddOutline(rt, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            // ---- 1행 ----
            float headY = -StatCardPadY;
            float bonusX = StatCardPadX + StatCardContentWidth - StatBonusWidth;
            float totalX = bonusX - StatValueGap - StatTotalWidth;
            float sourceX = StatCardPadX + StatNameWidth + StatNameGap;

            Text name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                StatCardPadX, headY, StatNameWidth, StatHeadHeight, "—");
            Text source = Label(rt, "Source", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, sourceX, headY,
                Mathf.Max(0f, totalX - sourceX - StatNameGap), StatHeadHeight, "");
            Text total = Label(rt, "Total", UiChrome.FontTitle, TextAnchor.MiddleRight, UiChrome.TextPrimary,
                totalX, headY, StatTotalWidth, StatHeadHeight, "—", bold: true);
            // ★ L-4 — 인계본의 초록 #8FBF6A는 우리 팔레트에 없다. 설계가 "임시로 Accent"로 못박았다.
            Text bonus = Label(rt, "Bonus", UiChrome.FontLabel, TextAnchor.MiddleRight, UiChrome.Accent,
                bonusX, headY, StatBonusWidth, StatHeadHeight, "");

            // ---- 2행: 총합 게이지 + 임계 눈금 ----
            float gaugeY = headY - StatHeadHeight - StatRowGap;
            Image track = UiChrome.AddSurface(rt, "GaugeTrack",
                UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurfaceMuted), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(track.rectTransform, StatCardPadX, gaugeY,
                StatCardContentWidth, StatGaugeHeight);
            track.raycastTarget = false;

            Image fill = UiChrome.AddSurface(track.rectTransform, "Fill", UiChrome.Accent, UiChrome.RadiusDot);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.raycastTarget = false;

            var view = new StatCardView
            {
                Rect = rt, Name = name, Source = source, Total = total, Bonus = bonus, GaugeFill = fillRect,
            };

            for (int t = 0; t < view.Ticks.Length; t++)
            {
                // 반지름은 게이지 트랙과 같은 RadiusDot — 1pt 폭에 큰 반지름을 주면 9-슬라이스가 뭉갠다
                // (기록 구분선이 이미 쓰는 관례와 같다).
                Image tick = UiChrome.AddSurface(track.rectTransform, "Tick" + t,
                    UiChrome.PanelSurface, UiChrome.RadiusDot);
                UiChrome.PlaceTopLeft(tick.rectTransform, 0f, 0f, DividerThickness, StatGaugeHeight);
                tick.raycastTarget = false;
                view.Ticks[t] = tick;
            }

            // ---- 3행: 단계 칩 + 「중급까지 N」 ----
            float footY = gaugeY - StatGaugeHeight - StatRowGap;
            float chipY = footY + (StatTierChipHeight - StatFootHeight) * 0.5f;

            Image chip = UiChrome.AddSurface(rt, "TierChip",
                UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurfaceMuted), UiChrome.RadiusBadge);
            view.ChipRect = chip.rectTransform;
            UiChrome.PlaceTopLeft(view.ChipRect, StatCardPadX, chipY, StatTierChipMaxWidth, StatTierChipHeight);
            chip.raycastTarget = false;
            view.ChipSurface = chip;

            view.ChipLabel = UiChrome.AddText(view.ChipRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextSecondary);
            UiChrome.Stretch(view.ChipLabel.rectTransform);
            view.ChipLabel.text = "—";

            view.Remain = Label(rt, "Remain", UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.TextTertiary, StatCardPadX + StatCardContentWidth - StatRemainWidth, footY,
                StatRemainWidth, StatFootHeight, "—");

            return view;
        }

        /// <summary>
        /// 「테마 세트 / SET」 블록. 진행도 두 줄(§1-7 · §7-4)과 준비 중 문구를 둘 다 굽는다.
        /// <para>★ <b>대사 풀 전환 / 테마 이펙트 / 유휴 동작 세 줄은 그리지 않는다.</b> §6 실측에서
        /// 그 셋은 런타임이 0줄이고(<c>Aura</c>·<c>arrowTrail</c>·<c>명상</c> 전수 0건), 「격파 성공 시」는
        /// 삭제된 콘텐츠를 가리키는 죽은 참조라 문구 교체 자체가 미정이다(§C-11 · §9 U-11).
        /// 없는 기능을 네 줄로 약속하는 것은 유예 자동 해금이 폐기된 것과 <b>같은 형태의 결함</b>이다.</para>
        /// </summary>
        private void BuildSetBlock(RectTransform content)
        {
            RectTransform block = BuildCol2Block(content, Col2BlockSet, "Col2Set",
                SectionLabelHeight + Col2LabelGap + SetPanelHeight);

            BuildSectionLabel(block, "테마 세트", "SET", Col2PadX, 0f);
            float panelY = -(SectionLabelHeight + Col2LabelGap);

            Image setPanel = UiChrome.AddSurface(block, "SetPanel", UiChrome.CardSurfaceMuted, UiChrome.RadiusCard);
            UiChrome.PlaceTopLeft(setPanel.rectTransform, Col2PadX, panelY, Col2ContentWidth, SetPanelHeight);
            setPanel.raycastTarget = false;
            UiChrome.AddOutline(setPanel.rectTransform, "Outline", UiChrome.CardBorder, UiChrome.RadiusCard);

            var noticeGo = new GameObject("SetNotice", typeof(RectTransform));
            noticeGo.transform.SetParent(setPanel.rectTransform, false);
            UiChrome.Stretch(noticeGo.GetComponent<RectTransform>());
            _setNoticeRoot = noticeGo;

            Text setNotice = UiChrome.AddText(noticeGo.transform, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.InkMeta, wrap: true);
            UiChrome.Stretch(setNotice.rectTransform, UiChrome.Space3);
            setNotice.text = SetPanelNotice;
            setNotice.raycastTarget = false;

            var readyGo = new GameObject("SetReady", typeof(RectTransform));
            readyGo.transform.SetParent(setPanel.rectTransform, false);
            UiChrome.Stretch(readyGo.GetComponent<RectTransform>());
            _setReadyRoot = readyGo;
            readyGo.SetActive(false);

            _setProgress = Label(readyGo.transform, "Progress", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, StatCardPadX, -StatCardPadY, StatCardContentWidth,
                SetProgressHeight, "—");
            _setDetail = Label(readyGo.transform, "Detail", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.TextTertiary, StatCardPadX, SetDetailY, StatCardContentWidth,
                SetDetailHeight, CharacterStatReadout.SetIncomplete);
        }

        // ==================== 배치 ====================

        /// <summary>
        /// ★ 컬럼 2 세로 좌표의 <b>유일한</b> 계산기. 블록 4개를 위에서 아래로 쌓고 스크롤 콘텐츠
        /// 높이를 정한다.
        /// <para>블록 하나의 높이만 바뀌어도 <b>아래 블록의 y가 전부</b> 움직인다 — 네 곳에 좌표를
        /// 굳혀 두면 그중 하나만 따라오지 않는 날이 온다(이 저장소가 폭 1042에서 실제로 당한 형태:
        /// 헤더는 갔는데 카드줄은 안 갔다). <see cref="LayoutCardGrid"/>와 같은 이유·같은 형태다.</para>
        /// </summary>
        private void LayoutColumn2()
        {
            if (_col2Content == null) return;

            float y = -ColPadY;
            for (int i = 0; i < _col2Blocks.Length; i++)
            {
                Col2Block block = _col2Blocks[i];
                if (block == null || block.Rect == null) continue;
                UiChrome.PlaceTopLeft(block.Rect, 0f, y, Col2ViewportWidth, block.Height);
                y -= block.Height + Col2BlockGap;
            }

            float used = -y - Col2BlockGap + ColPadY;   // 마지막 블록 뒤 간격은 빼고 아래 여백을 더한다.
            _col2Content.sizeDelta = new Vector2(Col2ViewportWidth, Mathf.Max(BodyHeight, used));

            float max = MaxCol2Scroll();
            Vector2 p = _col2Content.anchoredPosition;
            float clamped = Mathf.Clamp(p.y, 0f, max);
            if (!Mathf.Approximately(p.y, clamped))
            {
                p.y = clamped;
                _col2Content.anchoredPosition = p;
            }
        }

        /// <summary>컬럼 2 content가 밀려날 수 있는 최대치(양수). 내용이 뷰포트에 들어가면 0이다 —
        /// 그때 이 창은 스크롤이 붙기 전과 <b>같은 화면</b>이다.</summary>
        private float MaxCol2Scroll()
        {
            if (_col2Content == null || _col2Viewport == null) return 0f;
            return Mathf.Max(0f, _col2Content.rect.height - _col2Viewport.rect.height);
        }

        // ==================== 전역 폴링 드래그 (컬럼 3 격자와 같은 형태) ====================

        private void ArmCol2Drag(Vector2 cursor)
        {
            _col2Grabbed = false;
            _col2Moved = false;
            if (Def(_tab).Page != TabPage.Cards) return;   // 컬럼 2는 카드 탭에만 있다.
            if (!_showCol2) return;                        // 좁은 창에서는 접혀 있다.
            if (_col2Content == null || _col2Viewport == null) return;
            if (MaxCol2Scroll() <= 0f) return;             // 넘치지 않으면 잡을 것도 없다.
            if (!ContainsScreenPoint(_col2Viewport, cursor)) return;

            _col2Grabbed = true;
            _col2GrabScreenY = cursor.y;
            _col2StartContentY = _col2Content.anchoredPosition.y;
        }

        private void DragCol2To(Vector2 cursor)
        {
            if (!_col2Grabbed || _col2Content == null) return;

            // 부호 유도는 DragGridTo와 같다: content 피벗이 (0,1)이라 y가 커질수록 아래가 드러나고,
            // 직접 조작이므로 커서를 위로 끌면 콘텐츠도 위로 간다.
            float delta = (cursor.y - _col2GrabScreenY) / CanvasScale();
            if (!_col2Moved && Mathf.Abs(delta) < GridDragThresholdPoints) return;
            _col2Moved = true;
            // 격자와 <b>같은 시계</b>를 쓴다 — 민 직후의 뗌이 이름 편집/잉크 스와치 클릭으로
            // 오인되면 안 되고, 그 억제는 SuppressedByGridDrag() 한 곳이 한다.
            _lastGridMoveTime = Time.unscaledTime;

            Vector2 p = _col2Content.anchoredPosition;
            p.y = Mathf.Clamp(_col2StartContentY + delta, 0f, MaxCol2Scroll());
            _col2Content.anchoredPosition = p;
        }

        private void EndCol2Drag()
        {
            _col2Grabbed = false;
            _col2Moved = false;
        }

        // ==================== 갱신 ====================

        /// <summary>
        /// 컬럼 2의 스탯/세트 값을 다시 쓴다. <see cref="RefreshNumbers"/>(0.25초 주기)에서 불린다.
        /// <para><b>입력이 그대로면 아무것도 하지 않는다</b> — 문자열을 만들지도, 폭을 재지도 않는다.
        /// 상주 앱이라 4Hz × 카드 4장 × 문자열 4개를 무조건 만들면 그게 곧 쓰레기다.</para>
        /// </summary>
        private void RefreshStatColumn()
        {
            // ★ 계산은 여기서 하지 않는다 — Core가 「정보창 스탯 카드가 부르는 유일한 입구」라고
            //   못박아 둔 함수 하나를 부른다(EquipmentStatRules.CurrentBuild).
            StatBuild build = EquipmentStatRules.CurrentBuild();

            if (_statSumValue != null)
            {
                string sumText = CharacterStatReadout.FormatEquipmentSum(
                    CharacterStatReadout.EquipmentSum(build.EquipmentBonus));
                if (_statSumValue.text != sumText) _statSumValue.text = sumText;
            }

            for (int i = 0; i < _statCards.Length; i++) RefreshStatCard(i, build);
            RefreshSetPanel(build);
        }

        private void RefreshStatCard(int index, in StatBuild build)
        {
            StatCardView card = _statCards[index];
            if (card == null || index >= EquipmentStatRules.StatCount) return;

            var stat = (CharacterStat)index;
            int total = build.Total.Of(stat);
            int bonus = build.EquipmentBonus.Of(stat);
            int tier = build.TierReached(stat);
            int reached = CurrencyModel.StatTierReached(index);

            if (card.HasShown && card.ShownTotal == total && card.ShownBonus == bonus
                && card.ShownTier == tier && card.ShownReachedTier == reached)
            {
                return;   // 바뀐 것이 없다 — 문자열도 폭 측정도 하지 않는다.
            }

            card.HasShown = true;
            card.ShownTotal = total;
            card.ShownBonus = bonus;
            card.ShownTier = tier;
            card.ShownReachedTier = reached;

            card.Name.text = EquipmentStatRules.StatName(stat);
            // 「모자 주스탯」 — 이 스탯이 어느 슬롯에서 오는가(§1-1). 슬롯 이름의 유일한 출처는
            // EquipmentModel이고, 슬롯↔스탯 대응의 유일한 출처는 EquipmentStatRules다.
            card.Source.text = $"{EquipmentModel.SlotName(EquipmentStatRules.StatSlotAt(index))} 주스탯";
            card.Total.text = CharacterStatReadout.FormatTotal(total);
            card.Bonus.text = CharacterStatReadout.FormatBonus(bonus);

            SetBarFill(card.GaugeFill, build.Progress01(stat));

            for (int t = 0; t < card.Ticks.Length; t++)
            {
                Image tick = card.Ticks[t];
                if (tick == null) continue;

                int tierIndex = t + 1;   // 눈금도 단계와 같은 1-기준이다.
                bool hasTick = tierIndex <= EquipmentStatRules.TierCount;
                if (tick.gameObject.activeSelf != hasTick) tick.gameObject.SetActive(hasTick);
                if (!hasTick) continue;

                RectTransform tickRect = tick.rectTransform;
                float fraction = Mathf.Clamp01(EquipmentStatRules.TierMark01(tierIndex));
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(fraction, 0f);
                tickRect.pivot = new Vector2(0.5f, 0f);
                tickRect.anchoredPosition = Vector2.zero;
                tickRect.sizeDelta = new Vector2(DividerThickness, StatGaugeHeight);

                // H-8 — 한 번 넘긴 눈금만 황동. 판정은 CharacterStatReadout 한 곳에 있다.
                tick.color = CharacterStatReadout.IsTickLit(tierIndex, tier, reached)
                    ? UiChrome.Accent : UiChrome.PanelSurface;
            }

            string tierName = EquipmentStatRules.TierName(tier);
            card.ChipLabel.text = tierName;
            card.ChipSurface.color = tier > 0
                ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurfaceMuted)
                : UiChrome.Flatten(UiChrome.TrackBackground, UiChrome.CardSurfaceMuted);
            card.ChipLabel.color = tier > 0 ? UiChrome.Accent : UiChrome.TextTertiary;

            // 칩 폭은 글자에서 <b>실제로 재서</b> 정한다(곱셈 상수 금지 — 영어화에서도 안 깨진다).
            float chipWidth = Mathf.Min(
                SettingsControls.MeasuredWidth(card.ChipLabel, tierName) + StatTierChipPadX * 2f,
                StatTierChipMaxWidth);
            card.ChipRect.sizeDelta = new Vector2(Mathf.Max(0f, chipWidth), StatTierChipHeight);

            card.Remain.text = CharacterStatReadout.FormatRemaining(total);
        }

        /// <summary>
        /// 세트 패널.
        ///
        /// <para>★ <b>완성 여부와 완성된 테마 이름은 계산하지 않는다</b> —
        /// <see cref="StatBuild.SetComplete"/> · <see cref="StatBuild.SetTheme"/>를 그대로 쓴다
        /// (<c>coder-systems</c> 인계: *"두 곳에서 같은 걸 계산하면 언젠가 갈라진다"*).
        /// 화면이 따로 내는 것은 <b>미완성일 때 몇 칸까지 왔는가</b>뿐이고, 그 숫자는 Core에 없다
        /// (§1-4의 「최빈값 개수」는 완성 판정에 필요 없어서 Core가 세지 않는다).</para>
        ///
        /// <para>그리고 그 둘이 <b>어긋나면 조용히 넘기지 않는다</b> — 아래 경고가 그 자리다.
        /// 「진행도가 4/4인데 Core는 미완성」은 규칙이 갈라졌다는 뜻이고, 그 상태로 「+8」을 그리면
        /// 화면이 붙지도 않은 보너스를 약속한다.</para>
        ///
        /// <para>스탯 4슬롯에 테마가 붙은 것이 <b>하나도 없을 때만</b> 준비 중 문구가 뜬다(아무것도
        /// 안 걸쳤거나 외형·팩만 걸쳤다) — <b>「0/4」를 그리지 않는다.</b> 아무것도 걸치지 않은
        /// 사람에게 진행 막대를 보여주는 것은 있지도 않은 목표를 만드는 일이다.
        /// <br/>★ 2026-09-06 정정: 여기 있던 *"테마가 하나도 배정되지 않은 오늘은"*은 R21 테마
        /// 배정(24종)으로 <b>거짓이 됐다</b>. 1일차 무료 4종이 전부 <c>mil</c>이라 그 넷을 걸치면
        /// 첫날부터 <b>4/4</b>가 뜬다(E2 — *"세트 완성의 첫 경험은 돈이 0원이다"*).</para>
        ///
        /// <para>★ 그리고 화면에 나가는 것은 <b>키가 아니라 이름</b>이다 —
        /// <see cref="ItemCatalog.ThemeDisplayName"/>. 키(<c>mil</c>)는 세트 판정용 안정 식별자라
        /// 한글 UI에 영문 소문자로 박히면 안 되고, 그 표는 <b>Core 한 곳</b>에 있다(화면이 자기 표를
        /// 들면 팩이 테마를 실어 오는 날 한쪽만 늘어난다).</para>
        /// </summary>
        private void RefreshSetPanel(in StatBuild build)
        {
            bool hasProgress = CharacterStatReadout.TryGetThemeProgress(
                ThemeOf(EquipmentSlot.Head), ThemeOf(EquipmentSlot.Eyes),
                ThemeOf(EquipmentSlot.Neck), ThemeOf(EquipmentSlot.Shoulders),
                out string themeName, out int matched, out int required);

            if (_setNoticeRoot != null && _setNoticeRoot.activeSelf == hasProgress)
            {
                _setNoticeRoot.SetActive(!hasProgress);
            }
            if (_setReadyRoot != null && _setReadyRoot.activeSelf != hasProgress)
            {
                _setReadyRoot.SetActive(hasProgress);
            }
            if (!hasProgress) return;

            // ★ 완성 여부의 유일한 출처는 Core다. 진행도는 표시용 보조일 뿐이다.
            bool complete = build.SetComplete;
            if (complete && !string.IsNullOrEmpty(build.SetTheme)) themeName = build.SetTheme;
            if (complete != (matched >= required))
            {
                Debug.LogError($"[정보창] 세트 판정이 갈라졌습니다 — Core={complete} / 진행도={matched}/{required}. " +
                    "EquipmentStatRules.IsSetComplete와 CharacterStatReadout.TryGetThemeProgress가 " +
                    "같은 규칙(빈 테마 제외 · 문자열 동등성)을 보고 있는지 확인하세요.");
            }

            // ★ 화면에는 키가 아니라 <b>사람이 읽을 이름</b>이 뜬다("mil 3/4" → "밀리터리 3/4").
            //   변환 규칙 한 곳은 CharacterStatReadout.ThemeLabel이고 그 안에서 Core의 표
            //   (ItemCatalog.ThemeDisplayName)를 본다 — 표는 Core, 표시 규칙은 표시 계층이다.
            string progress = CharacterStatReadout.FormatThemeProgress(
                CharacterStatReadout.ThemeLabel(themeName), matched, required);
            if (_setProgress != null && _setProgress.text != progress) _setProgress.text = progress;

            string detail = complete
                ? CharacterStatReadout.FormatSetBonus()
                : CharacterStatReadout.SetIncomplete;
            if (_setDetail != null && _setDetail.text != detail)
            {
                _setDetail.text = detail;
                _setDetail.color = complete ? UiChrome.Accent : UiChrome.TextTertiary;
            }
        }

        /// <summary>이 슬롯에 지금 걸친 것의 테마 키(미착용/미배정이면 <c>null</c>).
        /// <b>카탈로그를 화면이 직접 뒤지는 유일한 자리</b>이고, 그것을
        /// <see cref="EquipmentStatRules.SlotLoadout"/>에 맡긴다 — 착용 여부·등급·부스탯·테마의
        /// 이음매가 Core에 한 벌만 있게.</summary>
        private static string ThemeOf(EquipmentSlot slot) => EquipmentStatRules.SlotLoadout(slot).Theme;
    }
}
