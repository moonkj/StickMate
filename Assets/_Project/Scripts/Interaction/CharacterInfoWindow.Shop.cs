using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>[상점] 탭의 본문</b> — 2026-09-06 배선. 사용자 질의 *"상점 관련 아이템은 다 구현되어 있음?"*에
    /// 대한 답이 <b>"아니오"</b>였고(그때까지 <c>TabPage.Placeholder</c> + 문구 한 줄), 이 파일이 그 자리를 채운다.
    ///
    /// ============================================================================
    /// 이 화면이 <b>주장하지 않는</b> 것 (원칙 1)
    /// ============================================================================
    /// 설계 정본 <c>docs/UX_SHOP_AND_CURRENCY.md</c> §3은 <b>유예 자동 해금</b>(동전 = 앞당기기,
    /// 시간이 지나면 그냥 열림)을 전제로 카드 메타에 <c>3시간</c> 같은 남은 시간을 적는다.
    /// <b>그 기능은 이 빌드에 없다</b> — <see cref="ItemGraceBaseline"/>은 세이브 v10에 <b>자리만</b>
    /// 있고 로직이 0줄이다(<c>CurrencyModel</c> §20-4). 그래서 남은 유예를 그리지 않고, 설계 문구에서
    /// 그 절을 <b>지금 참인 절</b>(요구 레벨에 닿으면 열린다 — 실재하는 경로다)로 바꿔 적는다.
    /// <b>유예가 배선되는 라운드가 그 절만 되돌리면 된다</b>(문장 형태는 설계 그대로 유지했다).
    ///
    /// <para>같은 이유로 §3-3의 S4(레벨 잠금 = 못 삼)도 따르지 않는다. <b>이 빌드에서 동전은 해금이다</b> —
    /// <c>CurrencyRules.SeedCoins</c> 문서가 리더 승인과 함께 그렇게 적어 두었다(<i>"시드로 처음 살 수
    /// 있는 것은 레벨 위쪽 아이템이다"</i>). 그 판정을 여기서 다시 만들지 않는다 —
    /// 보유는 <see cref="ItemCatalogEntry.IsOwned"/>(레벨 파생 ∪ 구매)가 유일하게 답한다.</para>
    ///
    /// ============================================================================
    /// 무엇을 파는가 — 판단을 여기서 만들지 않는다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>은퇴한 카테고리는 없다</b> — <see cref="ItemCatalog.IsListed"/> 하나에게만 묻는다
    ///         (2026-09-06 [머리] 은퇴). 새 술어를 만들면 "살 수는 있는데 못 입는" 물건이 생긴다.</item>
    ///   <item><b>DLC 팩 아이템은 노출하지 않는다</b> — 기본 코호트(<see cref="ItemCatalog.BaseCohortId"/>)만
    ///         싣는다. 팩은 동전이 아니라 스토어 결제로 오는 층이고 <b>이 프로젝트에는 결제 백엔드가
    ///         없다</b>(<c>ItemCatalog</c>가 스스로 적어 둔 사실). 지금 팩은 0개라 이 필터는 아무것도
    ///         거르지 않지만, 팩이 실리는 날 <b>조용히 동전으로 팔리는 사고</b>를 막는다.</item>
    ///   <item><b>행동(Action)은 상품이 아니다</b> — 슬롯도 등급도 없어 가격이 정의되지 않는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 그림은 <b>카드 탭과 같은 렌더러</b>다 (신규 0벌)
    /// ============================================================================
    /// 썸네일은 <see cref="BuildCardArt"/>(→ <see cref="AccessoryCardIcon"/>), 등급 리본은
    /// <see cref="BuildRarityRibbon"/>, 치수는 <see cref="CardWidth"/>·<see cref="CardHeight"/>와
    /// 그 파생 상수를 <b>그대로</b> 쓴다. 상점 전용 좌표는 <b>열 수 하나</b>뿐이고 그것도 폭에서 파생한다 —
    /// 이 저장소가 폭 1042에서 당한 사고(헤더는 따라갔는데 카드줄은 안 따라감)를 구조적으로 막는다.
    ///
    /// ============================================================================
    /// 구매는 <b>두 번 눌러야</b> 확정된다 (설계 §3-5)
    /// ============================================================================
    /// 재화 소모는 되돌릴 수 없다. 첫 클릭은 라벨을 <c>정말 살까요?</c>로 바꾸고
    /// (<b>새 표면 0개</b> — 이 창은 바깥 클릭으로 안 닫히므로 확인 팝업을 얹으면 탈출구를 하나 더
    /// 설계·검증해야 한다), 둘째 클릭이 확정한다. 탈출은 <b>아무것도 안 하기</b>다 —
    /// <see cref="ShopConfirmSeconds"/> 뒤 자동 복귀 / 다른 카드 클릭 / 탭 전환 / 격자 드래그.
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 어휘 ====================

        /// <summary>동전 글리프. ★ <b>헤더 칩과 상점이 같은 한 자리를 쓴다</b> — 두 곳이 각자 기호를
        /// 들고 있으면 한쪽만 바뀌는 날이 오고, 그날 화면은 같은 재화를 두 기호로 말한다.</summary>
        internal const string CoinGlyph = "◎";

        /// <summary>헤더 동전 칩의 라벨(<see cref="BuildHeader"/>가 쓴다).</summary>
        private const string CoinChipLabel = CoinGlyph + " 동전";

        /// <summary>이미 가지고 있는 상품의 버튼/메타 낱말. <c>ItemCatalogEntry.ResolveStatusSlot</c>의
        /// <c>보유</c>·<c>착용 중</c>과 <b>같은 어휘 가족</b>이다(같은 사실을 두 말로 하지 않는다).
        /// <para>★ <c>public</c>인 이유는 <b>테스트가 문구를 베끼지 않게</b> 하기 위해서다
        /// (<c>SettingsControls.NotBuiltWord</c>와 같은 관례) — PlayMode 어셈블리에는
        /// <c>InternalsVisibleTo</c>가 없어 <c>internal</c>로는 참조할 수 없다.</para></summary>
        public const string ShopOwnedWord = "보유 중";

        /// <summary>잔액이 모자란 상품의 버튼 낱말. <b>가격을 그대로 두고 비활성만 하지 않는다</b> —
        /// 이 면(<see cref="UiChrome.CardActionSurface"/>)에서는 활성/비활성 잉크가 같은 값이라
        /// (그 면 위에서 AA를 넘는 잉크가 하나뿐이다 — <c>SettingsControls</c> F1 문단), 라벨을 안 바꾸면
        /// <b>살 수 있는 카드와 픽셀 단위로 같아진다</b>. 못 사는 이유의 전문은 아래 상세 줄이 말한다.</summary>
        public const string ShopShortOfCoinsWord = "동전 부족";

        /// <summary>확인 단계 라벨 — 설계 §3-5 확정 문구(폭 58.5pt / 칸 <see cref="CardActionWidth"/>).</summary>
        public const string ShopConfirmWord = "정말 살까요?";

        /// <summary>확인 단계가 저절로 풀리는 시간(초). 설계 §3-5의 3초.
        /// <para>판정은 0.25초 주기(<see cref="SlowRefreshInterval"/>)에 하므로 실제 복귀는 최대
        /// 3.25초다 — 매 프레임 시계를 보는 대신 치른 값이고, 탈출구의 성질(가만히 두면 풀린다)은
        /// 그대로다.</para></summary>
        private const float ShopConfirmSeconds = 3f;

        // ==================== 치수 — 새 숫자는 「열 수」 하나뿐이다 ====================

        /// <summary>상점 격자의 열 수. <b>숫자를 적지 않고 폭에서 파생</b>한다(설계 폭에서 5열).
        /// 카드 폭이 바뀌면 열 수가 저절로 따라온다.</summary>
        private const int ShopColumns = (int)((PageContentWidth + CardGap) / CardStepX);

        /// <summary>격자가 실제로 쓰는 폭. <b>상세 카드도 이 폭을 쓴다</b> — 한 페이지의 오른쪽 끝선은
        /// 하나여야 한다(<c>InfoWindowCardRowEdgeTests</c>가 카드 탭에서 잠그는 그 불변식과 같은 규칙).</summary>
        private const float ShopGridWidth = ShopColumns * CardWidth + (ShopColumns - 1) * CardGap;

        /// <summary>격자 뷰포트 높이 — [보관함] 목록과 <b>같은 골격</b>이다. 두 전체폭 페이지가 같은 자리에서
        /// 시작하고 끝나므로, 탭을 오갈 때 아래 상세 카드가 위아래로 튀지 않는다.</summary>
        private const float ShopGridHeight = InventoryListHeight;

        /// <summary>상세 카드의 위 끝 / 높이 — [보관함]과 같은 값(같은 이유).</summary>
        private const float ShopDetailY = InventoryDetailY;

        private const float ShopDetailHeight = InventoryDetailHeight;

        // ==================== 상품 목록 — 정적 사실이다 ====================

        /// <summary>상점이 파는 항목의 <b>카탈로그 인덱스</b> 목록. 카탈로그는 실행 중에 바뀌지 않으므로
        /// 한 번만 만든다(<see cref="InventoryLines"/>와 같은 관례).
        /// <para>★ 여기 들어가는 조건은 전부 <b>정적</b>이다 — 보유/잔액/레벨 같은 <b>런타임 상태는
        /// 목록에 영향을 주지 않는다</b>. 다 산 카드도 자리를 지킨다("죽이되 지우지 않는다" — 선반이
        /// 비면 고장난 것처럼 보인다). 그래서 이 캐시가 낡을 일이 없다.</para></summary>
        private static int[] _shopEntries;

        private static int[] ShopEntries
        {
            get
            {
                if (_shopEntries != null) return _shopEntries;

                var list = new System.Collections.Generic.List<int>(ItemCatalog.Count);
                for (int i = 0; i < ItemCatalog.Count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.At(i);
                    if (!IsShopMerchandise(entry)) continue;
                    list.Add(i);
                }
                _shopEntries = list.ToArray();
                return _shopEntries;
            }
        }

        /// <summary>이 항목이 <b>동전으로 파는 상품</b>인가. 세 조건 전부 정적이다(클래스 문서 참고).
        /// <para>★ 은퇴 판정을 여기서 다시 쓰지 않는다 — <see cref="ItemCatalog.IsListed"/>가 유일한 술어다.</para></summary>
        private static bool IsShopMerchandise(ItemCatalogEntry entry)
        {
            if (entry == null) return false;
            if (entry.Category != ItemCategory.Equipment) return false;   // 행동에는 슬롯도 등급도 없다 = 가격이 없다
            if (!entry.Slot.HasValue || entry.ItemIndex < 0) return false;
            if (!ItemCatalog.IsListed(entry)) return false;               // 은퇴한 카테고리(2026-09-06 [머리])
            return entry.CohortId == ItemCatalog.BaseCohortId;            // DLC 팩은 동전 경제 밖이다
        }

        /// <summary>상점 <paramref name="index"/>번 자리의 항목. 범위 밖이면 <c>null</c>.</summary>
        private static ItemCatalogEntry ShopEntryAt(int index)
        {
            int[] entries = ShopEntries;
            return (uint)index < (uint)entries.Length ? ItemCatalog.At(entries[index]) : null;
        }

        // ==================== 상태 — 순수 판정 ====================

        /// <summary>상점 카드 한 장이 가질 수 있는 상태. <b>전부 상호배타</b>이고, 이 셋 말고는 없다.
        /// <para>확인 단계(<c>정말 살까요?</c>)는 여기 없다 — 그건 <b>아이템의 상태</b>가 아니라
        /// 화면의 일시적 단계라 카드 인덱스 하나로 따로 산다(<see cref="_shopConfirmIndex"/>).</para></summary>
        internal enum ShopState
        {
            /// <summary>이미 가지고 있다(레벨 파생이든 구매든 — 화면은 둘을 구별하지 않는다, 설계 §3-3 S3/S3b).</summary>
            Owned = 0,

            /// <summary>지금 살 수 있다.</summary>
            Affordable = 1,

            /// <summary>잔액이 모자라다.</summary>
            Insufficient = 2,
        }

        /// <summary>
        /// 이 항목의 상점 상태. <b>순수 함수</b>다 — 화면도 테스트도 이 하나를 부른다.
        /// <para>보유 판정을 여기서 만들지 않는다(<see cref="ItemCatalogEntry.IsOwned"/>가 레벨 파생 ∪
        /// 구매 ∪ 훗날 엔타이틀먼트의 유일한 답이다). 여기가 더하는 것은 <b>잔액 비교 한 줄</b>뿐이다.</para>
        /// </summary>
        internal static ShopState ResolveShopState(ItemCatalogEntry entry, StickConfig config, int coinBalance)
        {
            if (entry == null) return ShopState.Owned;   // 없는 것은 팔 수 없다(버튼이 죽는 쪽으로 물러난다)
            if (entry.IsOwned(config)) return ShopState.Owned;
            return coinBalance >= ShopPriceCoins(entry) ? ShopState.Affordable : ShopState.Insufficient;
        }

        /// <summary>이 항목의 가격. <b>출처는 <see cref="CurrencyRules.PriceCoins"/> 하나</b>다 —
        /// 여기에 600/1,400/3,200/9,600을 적으면 그 순간 가격이 두 곳에서 산다.</summary>
        internal static int ShopPriceCoins(ItemCatalogEntry entry)
        {
            if (entry != null && entry.Slot.HasValue && entry.ItemIndex >= 0)
            {
                return CurrencyRules.PriceCoins(ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex));
            }

            // 여기 오면 목록 필터(IsShopMerchandise)가 샌 것이다. 0을 돌려주면 <b>공짜로 사지는
            // 아이템</b>이 생기므로 가장 비싼 단으로 물러난다 — 안전한 방향은 언제나 비싼 쪽이다.
            Debug.LogError("[상점] 슬롯이 없는 항목의 가격을 물었습니다 — 상품 목록 필터가 샜습니다. " +
                           "가장 비싼 단으로 물러납니다(공짜 아이템을 만들지 않기 위해서).");
            return CurrencyRules.PriceCoins(ItemRarity.Legendary);
        }

        // ==================== 화면 부품 ====================

        private sealed class ShopCard
        {
            /// <summary>이 카드가 맡은 <b>상점 목록</b>의 자리(카탈로그 인덱스가 아니다).</summary>
            public int Index;

            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
            public Image Thumb;
            public Text Name;
            public Text Rarity;
            public Text Meta;
            public Text Category;

            public Image ActionSurface;
            public Image ActionOutline;
            public RectTransform ActionRect;
            public Text ActionLabel;
            public Button ActionButton;
        }

        private GameObject _shopPage;
        private ScrollRect _shopScroll;
        private RectTransform _shopViewport;
        private RectTransform _shopContent;
        private ShopCard[] _shopCards = System.Array.Empty<ShopCard>();
        private Text _shopDetailName;
        private Text _shopDetailBody;

        /// <summary>지금 아래 상세 줄이 설명하고 있는 자리.</summary>
        private int _shopSelected;

        /// <summary>확인 단계에 들어간 카드(−1 = 없음).</summary>
        private int _shopConfirmIndex = -1;

        private float _shopConfirmUntil;

        /// <summary>마지막으로 산 아이템 아이디 — 상세 줄이 <b>한 번</b> 축하 문장을 말하는 근거.
        /// 선택이 옮겨가면 그 문장은 사라진다(그 자리는 원래 설명문의 자리다).</summary>
        private string _shopBoughtId;

        /// <summary>마지막으로 칠할 때의 잔액. 유휴 수급으로 잔액이 오르면 <c>동전 부족</c> 카드가
        /// 저절로 살아나야 하는데, 갱신을 사건에만 걸면 그 순간을 놓친다 — 0.25초 주기가 이 값으로
        /// "다시 칠할 필요가 있는가"를 판정한다(같으면 <see cref="Text"/> 하나도 건드리지 않는다).</summary>
        private int _shopPaintedBalance = int.MinValue;

        private bool _shopGrabbed;
        private bool _shopMoved;
        private float _shopGrabScreenY;
        private float _shopStartContentY;

        /// <summary>누른 자리가 [구매] 칩이면 <b>뗄 때까지 보류</b>한다(−1이면 없음) —
        /// 카드 탭의 <see cref="_pendingEquipCard"/>와 같은 이유다(미는 손짓이 구매가 되면 안 된다).</summary>
        private int _pendingShopCard = -1;

        // ==================== 구성 ====================

        private void BuildShopPage(RectTransform body)
        {
            var pageGo = new GameObject("ShopPage", typeof(RectTransform));
            pageGo.transform.SetParent(body, false);
            var page = pageGo.GetComponent<RectTransform>();
            // ★ 3컬럼은 카드 탭의 것이다 — [상점]은 [보관함]처럼 본문 <b>전체 폭</b>을 쓴다.
            UiChrome.PlaceTopLeft(page, 0f, 0f, PanelWidth, BodyHeight);
            _shopPage = pageGo;

            // ---- 격자(세로 스크롤) ----
            var gridGo = new GameObject("ShopGrid", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            gridGo.transform.SetParent(page, false);
            var grid = gridGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(grid, PagePadX, PageTopY, ShopGridWidth, ShopGridHeight);

            // 카드 사이 빈틈을 잡아도 끌리게 하는 투명 판(컬럼 3 격자와 같은 장치).
            Image handle = gridGo.GetComponent<Image>();
            handle.color = Color.clear;
            handle.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(grid, false);
            _shopViewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_shopViewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_shopViewport, false);
            _shopContent = contentGo.GetComponent<RectTransform>();
            _shopContent.anchorMin = _shopContent.anchorMax = _shopContent.pivot = new Vector2(0f, 1f);
            _shopContent.anchoredPosition = Vector2.zero;

            _shopScroll = gridGo.GetComponent<ScrollRect>();
            _shopScroll.viewport = _shopViewport;
            _shopScroll.content = _shopContent;
            _shopScroll.horizontal = false;
            _shopScroll.vertical = true;
            // 관성을 끄고 Clamped로 두는 이유는 컬럼 3과 같다 — 전역 폴링 드래그와 <b>같은 절대값
            // 공식</b>이라야 두 경로가 동시에 돌아도 결과가 더해지지 않는다.
            _shopScroll.movementType = ScrollRect.MovementType.Clamped;
            _shopScroll.inertia = false;
            _shopScroll.scrollSensitivity = CardStepY * 0.5f;
            _shopScroll.horizontalScrollbar = null;
            _shopScroll.verticalScrollbar = null;

            int count = ShopEntries.Length;
            int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, ShopColumns));
            float contentHeight = Mathf.Max(ShopGridHeight,
                rows * CardHeight + Mathf.Max(0, rows - 1) * CardGap);
            _shopContent.sizeDelta = new Vector2(ShopGridWidth, contentHeight);

            var cards = new ShopCard[count];
            for (int i = 0; i < count; i++) cards[i] = BuildShopCard(_shopContent, i);
            _shopCards = cards;

            // ---- 상세 줄 ---- [보관함]과 같은 자리·같은 모양(전체폭 페이지 둘이 한 골격을 쓴다).
            Image detail = UiChrome.AddSurface(page, "ShopDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            RectTransform drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, PagePadX, ShopDetailY, ShopGridWidth, ShopDetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.SubtleSurface), UiChrome.RadiusCard);

            _shopDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, ShopGridWidth - 30f, 17f, "—", bold: true);

            _shopDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_shopDetailBody.rectTransform, 15f, -42f, ShopGridWidth - 30f, 34f);
            _shopDetailBody.lineSpacing = 1.6f;
        }

        /// <summary>카드 한 장. <b>치수는 전부 카드 탭의 상수</b>이고, 이 함수가 정하는 것은
        /// 격자 안의 자리(행·열)뿐이다.</summary>
        private ShopCard BuildShopCard(RectTransform content, int index)
        {
            ItemCatalogEntry entry = ShopEntryAt(index);
            EquipmentSlot slot = entry != null && entry.Slot.HasValue ? entry.Slot.Value : EquipmentSlot.Head;
            int itemIndex = entry != null ? entry.ItemIndex : 0;

            Image surface = UiChrome.AddSurface(content, "ShopCard" + index, UiChrome.CardSurface, UiChrome.RadiusCard);
            RectTransform rt = surface.rectTransform;
            int columns = Mathf.Max(1, ShopColumns);
            UiChrome.PlaceTopLeft(rt, (index % columns) * CardStepX, -(index / columns) * CardStepY,
                CardWidth, CardHeight);

            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);

            Image thumb = UiChrome.AddSurface(rt, "Thumb", UiChrome.CardSurfaceMuted, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(thumb.rectTransform, ThumbX, ThumbY, ThumbWidth, ThumbHeight);
            thumb.raycastTarget = false;

            // 등급 리본 — 카드 탭과 <b>같은 부품</b>. 등급은 아이템의 정적 사실이라 여기서 한 번만 칠한다.
            RarityRibbon ribbon = BuildRarityRibbon(rt, CardRibbonInset, -CardRibbonTopMargin, CardRibbonWidth);
            ItemRarity rarity = entry != null ? ItemCatalog.Rarity(slot, itemIndex) : ItemRarity.Common;
            ApplyRarityRibbon(ribbon, rarity);

            // ★ 상점은 <b>잠긴 것도 실명 + 소재색</b>으로 보여준다(설계 §3-3 — [장비] 탭의 `???` 규칙과
            //   의도적으로 반대다). 상점의 일은 상품을 보여 주는 것이고, 그 차이가 두 탭이 같은
            //   아이템을 놓고도 서로 다른 화면인 이유다. <b>[장비] 탭의 규칙은 한 글자도 건드리지 않았다.</b>
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(thumb.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(IconSize, IconSize);
            irt.anchoredPosition = Vector2.zero;
            if (entry != null) BuildCardArt(irt, slot, itemIndex, entry);   // 카드 탭과 같은 렌더러

            var card = new ShopCard
            {
                Index = index,
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

            // 이름은 <b>바뀌지 않는다</b>(상점은 언제나 실명이다) — 말줄임을 굽는 시점에 한 번만 잰다.
            if (entry != null)
            {
                card.Name.text = UiChrome.Ellipsize(card.Name, entry.DisplayName, CardNameWidth);
                card.Rarity.text = ItemCatalog.RarityName(rarity);
                card.Rarity.color = UiChrome.RarityColor(rarity);
                card.Category.text = entry.CategoryLabel;
                // 등급 테두리 — 카드 탭의 「기본 자리 승계」와 같은 규칙, 같은 합성(α<1을 그대로 얹지 않는다).
                card.Outline.color = UiChrome.Flatten(UiChrome.RarityBorder(rarity), UiChrome.CardSurface);
            }

            // ---- [구매] 칩 ---- 면은 카드 탭 [착용]과 <b>같은 값</b>이다(누를 수 있는 것의 시각 언어를
            //   두 벌로 만들지 않는다 — UI_ALPHA_BLEED_POLICY §7-2).
            card.ActionSurface = UiChrome.AddSurface(rt, "Action", UiChrome.CardActionSurface, UiChrome.RadiusChip);
            card.ActionRect = card.ActionSurface.rectTransform;
            UiChrome.PlaceTopLeft(card.ActionRect, CardPadX, CardActionY, CardActionWidth, CardActionHeight);
            card.ActionOutline = UiChrome.AddOutline(card.ActionRect, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardActionSurface), UiChrome.RadiusChip);
            card.ActionLabel = UiChrome.AddText(card.ActionRect, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.CardActionSurface, UiChrome.InkRole.Title, enabled: true),
                bold: true);
            UiChrome.Stretch(card.ActionLabel.rectTransform);

            var actionButton = card.ActionSurface.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = card.ActionSurface;
            // ColorTint를 끄는 이유는 카드 탭 버튼과 같다 — 밝은 면에서 pressed 곱셈이 대비를 무너뜨린다.
            actionButton.transition = Selectable.Transition.None;
            card.ActionButton = actionButton;
            int captured = index;
            actionButton.onClick.AddListener(() =>
            {
                if (SuppressedByGridDrag()) return;   // 방금 민 손짓의 끝을 클릭으로 오인하지 않는다.
                if (TryClaimAction("shopBuy" + captured)) OnShopBuyClicked(captured);
            });

            var body = surface.gameObject.AddComponent<Button>();
            body.targetGraphic = surface;
            body.transition = Selectable.Transition.None;
            body.onClick.AddListener(() =>
            {
                if (SuppressedByGridDrag()) return;
                if (TryClaimAction("shopCard" + captured)) OnShopCardClicked(captured);
            });

            return card;
        }

        // ==================== 갱신 ====================

        private void ApplyShopPage(bool visible)
        {
            if (_shopPage == null) return;
            if (_shopPage.activeSelf != visible) _shopPage.SetActive(visible);
            if (visible) return;

            // 탭을 떠나면 확인 단계는 <b>즉시</b> 풀린다(설계 §3-5의 탈출구 중 하나).
            ClearShopConfirm();
            _pendingShopCard = -1;
        }

        /// <summary>카드 전체 + 상세 줄을 다시 칠한다. <b>사건이 있을 때만</b> 부른다 —
        /// 잔액이 조용히 오르는 경우는 <see cref="TickShopTab"/>이 잡는다.</summary>
        private void RefreshShop()
        {
            if (_shopCards.Length == 0) return;

            int balance = CurrencyModel.CoinBalance;
            _shopPaintedBalance = balance;
            if (_shopSelected < 0 || _shopSelected >= _shopCards.Length) _shopSelected = 0;

            for (int i = 0; i < _shopCards.Length; i++)
            {
                ShopCard card = _shopCards[i];
                if (card == null) continue;
                ItemCatalogEntry entry = ShopEntryAt(i);
                if (entry == null) continue;

                ShopState state = ResolveShopState(entry, _config, balance);
                int price = ShopPriceCoins(entry);
                bool selected = i == _shopSelected;

                card.Meta.text = ShopCardMetaText(entry, state);
                card.Meta.color = state == ShopState.Owned ? UiChrome.InkTitle(true) : UiChrome.InkMeta;

                // 선택만 테두리를 덮는다 — 카드 탭의 우선순위(선택 > … > 등급)를 그대로 줄인 것이다.
                card.Outline.color = selected
                    ? UiChrome.TextPrimary
                    : UiChrome.Flatten(UiChrome.RarityBorder(ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex)),
                        UiChrome.CardSurface);

                StyleShopAction(card, state, price, confirming: _shopConfirmIndex == i);
            }

            RefreshShopDetail();
        }

        /// <summary>카드 메타 줄 — <b>상태 한 마디</b>. 살 수 있든 없든 "언제 그냥 열리는가"를 적는다
        /// (동전이 유일한 길이 아니라는 사실은 화면에 남아 있어야 한다).
        /// <para>★ 미보유 문구를 여기서 조립하지 않고 <see cref="ItemCatalogEntry.ResolveStatusSlot"/>에
        /// 맡긴다 — <c>Lv.n에 열림</c>은 이미 [보관함]이 쓰는 문장이고, 두 곳이 각자 조립하면
        /// 같은 사실을 다른 말로 하게 된다.</para></summary>
        private string ShopCardMetaText(ItemCatalogEntry entry, ShopState state)
        {
            switch (state)
            {
                case ShopState.Owned:
                    return ShopOwnedWord;

                case ShopState.Affordable:
                case ShopState.Insufficient:
                    return entry.ResolveStatusSlot(_config);

                default:
                    // ★ 정상값 셋은 위에서 전부 잡힌다 — 여기 오는 것은 enum에 단을 더하고 이 표를
                    //   잊었다는 뜻이다(조용히 빈 줄로 흘리면 그 카드만 상태를 잃는다).
                    Debug.LogError($"[상점] 표에 없는 카드 상태 {(int)state}입니다 — 메타 줄이 비어 나갑니다.");
                    return string.Empty;
            }
        }

        /// <summary>
        /// [구매] 칩의 라벨·잉크·테두리. <b>면은 상태와 무관하게 같다</b>(<see cref="UiChrome.CardActionSurface"/>).
        ///
        /// <para>★ 왜 비활성이라고 면을 어둡게 하지 않는가: 이 카드의 바탕은 <see cref="UiChrome.CardSurface"/>이고
        /// 거기서 <b>아래로는 3:1이 존재하지 않는다</b>(순검정까지 가도 1.27:1 — <c>UiChrome</c> 실측).
        /// 어둡게 하면 2026-09-02에 고친 「면이 아예 없는 칩」(1.00:1)이 그대로 돌아온다.
        /// 그래서 설정창 F1과 <b>같은 처방</b>을 쓴다 — 면과 잉크는 그대로 두고 <b>낱말과 테두리</b>가
        /// 못 쓴다는 사실을 말한다(<c>ApplyPagerEnabled</c>의 "죽이되 지우지 않는다"와 같은 규칙).</para>
        /// </summary>
        private static void StyleShopAction(ShopCard card, ShopState state, int price, bool confirming)
        {
            string label;
            bool interactable;
            switch (state)
            {
                case ShopState.Owned:
                    label = ShopOwnedWord;
                    interactable = false;
                    break;

                case ShopState.Affordable:
                    label = confirming ? ShopConfirmWord : $"{CoinGlyph} {price:N0}";
                    interactable = true;
                    break;

                case ShopState.Insufficient:
                    label = ShopShortOfCoinsWord;
                    interactable = false;
                    break;

                default:
                    // ★ 정상 상태 셋은 전부 위에서 잡힌다. 여기로 떨어지는 값은 「새로 만들고 이 표를
                    //   잊은 상태」뿐이고, 그때는 칩을 <b>죽여서</b> 내보낸다 — 모르는 상태에서 돈이
                    //   나가는 것보다 안 나가는 쪽이 안전한 방향이다.
                    Debug.LogError($"[상점] 표에 없는 카드 상태 {(int)state}입니다 — [구매] 칩을 잠급니다.");
                    label = ShopOwnedWord;
                    interactable = false;
                    break;
            }

            if (card.ActionLabel != null)
            {
                card.ActionLabel.text = label;
                card.ActionLabel.color = UiChrome.InkOnSurface(UiChrome.CardActionSurface,
                    UiChrome.InkRole.Title, interactable);
            }
            if (card.ActionOutline != null)
            {
                card.ActionOutline.color = UiChrome.Flatten(
                    interactable ? UiChrome.CardBorder : UiChrome.Divider, UiChrome.CardActionSurface);
            }
            if (card.ActionButton != null) card.ActionButton.interactable = interactable;
        }

        /// <summary>아래 상세 줄 — <b>못 사는 이유의 전문</b>이 사는 유일한 자리다(칩은 낱말 하나뿐이다).</summary>
        private void RefreshShopDetail()
        {
            ItemCatalogEntry entry = ShopEntryAt(_shopSelected);
            if (entry == null || _shopDetailName == null || _shopDetailBody == null) return;

            ItemRarity rarity = ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex);
            ShopState state = ResolveShopState(entry, _config, CurrencyModel.CoinBalance);
            int price = ShopPriceCoins(entry);

            string status = state == ShopState.Owned ? ShopOwnedWord : $"{CoinGlyph} {price:N0}";
            _shopDetailName.text =
                $"{entry.DisplayName}   ·   {ItemCatalog.RarityName(rarity)}   ·   {entry.CategoryLabel}   ·   {status}";
            _shopDetailBody.text = ShopDetailBodyText(entry, state, price);
        }

        /// <summary>
        /// 상세 줄의 문장. <b>설계 §3-3-b의 문장 형태를 그대로 쓰되</b>, 유예(미구현)를 말하는 절은
        /// <b>지금 참인 절</b>로 바꿨다 — 클래스 문서의 「주장하지 않는 것」 참고.
        /// <code>
        ///   설계 S1 : 동전 30개로 지금 열거나, 3시간 뒤에 그냥 열려요.
        ///   지금    : 동전 600개로 지금 열거나, 레벨 9가 되면 그냥 열려요.
        /// </code>
        /// <para>조사는 <see cref="KoreanParticle"/>가 만든다 — 숫자 뒤의 이/가는 끝자리마다 다르다.</para>
        /// </summary>
        private string ShopDetailBodyText(ItemCatalogEntry entry, ShopState state, int price)
        {
            switch (state)
            {
                case ShopState.Owned:
                    return string.Equals(_shopBoughtId, entry.Id, System.StringComparison.Ordinal)
                        ? $"{entry.DisplayName}{KoreanParticle.Objective(entry.DisplayName)} 얻었어요. " +
                          $"[{WearTabName(entry)}] 탭에서 입힐 수 있어요."
                        : entry.Description;

                case ShopState.Affordable:
                    return $"동전 {price:N0}개로 지금 열거나, {ShopWaitClause(entry)}";

                case ShopState.Insufficient:
                    return $"동전이 {(price - CurrencyModel.CoinBalance):N0}개 모자라요. {ShopWaitClause(entry)}";

                default:
                    Debug.LogError($"[상점] 표에 없는 카드 상태 {(int)state}입니다 — 설명이 비어 나갑니다.");
                    return entry.Description;
            }
        }

        /// <summary>"동전 말고 다른 길"을 말하는 절. 이 빌드에서 그 길은 <b>레벨</b>이다(유예는 아직 없다).</summary>
        private static string ShopWaitClause(ItemCatalogEntry entry)
        {
            if (!entry.RequiredLevel.HasValue) return "지금 열 수 있어요.";
            string level = entry.RequiredLevel.Value.ToString();
            return $"레벨 {level}{KoreanParticle.Subject(level)} 되면 그냥 열려요.";
        }

        /// <summary>이 아이템을 <b>입히는 탭</b>의 이름. 문자열을 적지 않고 탭 표에서 꺼낸다 —
        /// 탭 이름이 바뀌면 이 문장도 저절로 따라간다.</summary>
        private static string WearTabName(ItemCatalogEntry entry)
        {
            bool appearance = entry.Slot.HasValue && EquipmentModel.IsAppearanceSlot(entry.Slot.Value);
            return Def(appearance ? Tab.Appearance : Tab.Equipment).Name;
        }

        /// <summary>0.25초 주기로 상점 탭만 보는 일 둘 — <b>확인 단계 만료</b>와 <b>잔액 변화</b>.
        /// 둘 다 아무 일도 없으면 <see cref="Text"/> 하나 건드리지 않는다(상주 앱 규약).</summary>
        private void TickShopTab()
        {
            bool expired = _shopConfirmIndex >= 0 && Time.unscaledTime >= _shopConfirmUntil;
            if (expired) ClearShopConfirm();
            if (!expired && _shopPaintedBalance == CurrencyModel.CoinBalance) return;
            RefreshShop();
        }

        private void ClearShopConfirm()
        {
            _shopConfirmIndex = -1;
            _shopConfirmUntil = 0f;
        }

        // ==================== 조작 ====================

        /// <summary>카드 <b>본체</b> 클릭 = 고르기(아래 상세 줄이 그 상품을 설명한다).
        /// 사는 것은 하단 칩만 한다 — 카드 탭이 "고르기"와 "입기"를 나눈 것과 같은 이유다.</summary>
        private void OnShopCardClicked(int index)
        {
            if (index < 0 || index >= _shopCards.Length) return;
            if (_shopSelected == index && _shopConfirmIndex < 0) return;

            // 다른 카드를 고르면 확인 단계는 즉시 풀린다(설계 §3-5의 탈출구).
            ClearShopConfirm();
            // 축하 문장은 <b>한 번</b>이다 — 자리를 옮기는 순간 그 자리는 설명문의 것으로 돌아간다.
            if (_shopSelected != index) _shopBoughtId = null;
            _shopSelected = index;
            RefreshShop();

            ItemCatalogEntry entry = ShopEntryAt(index);
            if (entry != null) Debug.Log($"[상점] 선택 -> {entry.DisplayName}({entry.CategoryLabel}).");
        }

        /// <summary>
        /// [구매] 칩. <b>첫 클릭은 묻고, 둘째 클릭이 산다</b>(설계 §3-5).
        ///
        /// <para>상태를 여기서 <b>다시</b> 판정하는 것이 중요하다 — 클릭 경로가 둘이고
        /// (<see cref="Button.onClick"/> + 전역 폴링), 칩의 <c>interactable</c>만 믿으면 폴링 경로가
        /// 그대로 뚫린다(45-9-b가 페이지 칩에서 겪은 그 형태). 그리고 <see cref="CurrencyModel.TryPurchaseItem"/>이
        /// <b>한 번 더</b> 잔액과 중복을 본다 — 세 겹이지만 판단의 출처는 하나다.</para>
        /// </summary>
        private void OnShopBuyClicked(int index)
        {
            ItemCatalogEntry entry = ShopEntryAt(index);
            if (entry == null || index < 0 || index >= _shopCards.Length) return;

            // 버튼을 눌렀는데 아래 상세가 다른 상품을 설명하고 있으면 화면이 두 가지를 말한다.
            bool movedSelection = _shopSelected != index;
            if (movedSelection)
            {
                ClearShopConfirm();
                _shopBoughtId = null;   // 앞 상품의 축하 문장을 새 자리가 물려받지 않는다.
            }
            _shopSelected = index;

            int price = ShopPriceCoins(entry);
            ShopState state = ResolveShopState(entry, _config, CurrencyModel.CoinBalance);

            if (state != ShopState.Affordable)
            {
                ClearShopConfirm();
                Debug.Log(state == ShopState.Owned
                    ? $"[상점] {entry.DisplayName}{KoreanParticle.Topic(entry.DisplayName)} 이미 보유 중입니다 — 다시 팔지 않습니다."
                    : $"[상점] {entry.DisplayName} 구매 불가 — 가격 {price:N0}, 잔액 {CurrencyModel.CoinBalance:N0}" +
                      $"({price - CurrencyModel.CoinBalance:N0} 부족).");
                RefreshShop();
                return;
            }

            if (_shopConfirmIndex != index)
            {
                _shopConfirmIndex = index;
                _shopConfirmUntil = Time.unscaledTime + ShopConfirmSeconds;
                RefreshShop();
                return;
            }

            ClearShopConfirm();
            if (!CurrencyModel.TryPurchaseItem(entry.Id, price))
            {
                // 여기 오면 위 판정과 모델의 판정이 갈렸다는 뜻이다 — 조용히 넘기면 "눌렀는데 아무 일도
                // 없는 버튼"이 되고, 그건 이 앱이 최악이라고 부르는 형태다.
                Debug.LogWarning($"[상점] {entry.DisplayName} 구매가 거절됐습니다 — 화면은 살 수 있다고 " +
                                 $"판단했는데 재화 모델이 거절했습니다(가격 {price:N0}, 잔액 {CurrencyModel.CoinBalance:N0}).");
                RefreshShop();
                return;
            }

            _shopBoughtId = entry.Id;
            CharacterSaveStore.Save();   // "모든 토글은 즉시 반영(별도 저장 버튼 없음)" — 구매도 같다.

            Debug.Log($"[상점] {entry.DisplayName} 구매 — 동전 {price:N0} 차감, 잔액 {CurrencyModel.CoinBalance:N0}. " +
                      $"[{WearTabName(entry)}] 탭에서 바로 입힐 수 있습니다. 즉시 저장.");

            // ★ 이 넷은 <b>같은 프레임</b>에 일어나야 한다(설계 §3-5) — 하나라도 늦으면 화면이 서로
            //   다른 이야기를 한다: 헤더 잔액 / 상점 카드 / [장비]·[외형] 카드 / [보관함] 줄.
            RefreshNumbers();
            RefreshShop();
            RefreshCards();
            RefreshDetail();
            RefreshInventoryList();
        }

        // ==================== 전역 폴링 드래그 (컬럼 3 격자와 같은 형태) ====================

        private void ArmShopDrag(Vector2 cursor)
        {
            _shopGrabbed = false;
            _shopMoved = false;
            if (Def(_tab).Page != TabPage.Shop) return;
            if (_shopContent == null || _shopViewport == null) return;
            if (MaxShopScroll() <= 0f) return;                    // 넘치지 않으면 잡을 것도 없다.
            if (!ContainsScreenPoint(_shopViewport, cursor)) return;

            _shopGrabbed = true;
            _shopGrabScreenY = cursor.y;
            _shopStartContentY = _shopContent.anchoredPosition.y;
        }

        private void DragShopTo(Vector2 cursor)
        {
            if (!_shopGrabbed || _shopContent == null) return;

            // 부호 유도는 DragGridTo와 같다(피벗 (0,1) + 직접 조작).
            float delta = (cursor.y - _shopGrabScreenY) / CanvasScale();
            if (!_shopMoved && Mathf.Abs(delta) < GridDragThresholdPoints) return;
            _shopMoved = true;
            // 격자와 <b>같은 시계</b>를 쓴다 — 민 직후의 뗌이 구매로 오인되면 안 되고,
            // 그 억제는 SuppressedByGridDrag() 한 곳이 한다.
            _lastGridMoveTime = Time.unscaledTime;

            Vector2 p = _shopContent.anchoredPosition;
            p.y = Mathf.Clamp(_shopStartContentY + delta, 0f, MaxShopScroll());
            _shopContent.anchoredPosition = p;
        }

        private void EndShopDrag()
        {
            _shopGrabbed = false;
            _shopMoved = false;
        }

        private float MaxShopScroll()
        {
            if (_shopContent == null || _shopViewport == null) return 0f;
            return Mathf.Max(0f, _shopContent.rect.height - _shopViewport.rect.height);
        }

        /// <summary>누름 때 보류해 둔 구매를 <b>뗄 때</b> 확정한다 — 미는 동안 손가락 아래로 지나간
        /// 칩이 눌리지 않게. 카드 탭의 <see cref="ResolvePendingEquip"/>과 같은 규칙이다.</summary>
        private void ResolvePendingShopBuy(Vector2 cursor, bool hasCursor)
        {
            int pending = _pendingShopCard;
            _pendingShopCard = -1;
            if (pending < 0 || _shopMoved || !hasCursor) return;

            ShopCard card = pending < _shopCards.Length ? _shopCards[pending] : null;
            if (card == null || card.Rect == null || !card.Rect.gameObject.activeInHierarchy) return;
            if (!ContainsScreenPoint(card.ActionRect, cursor)) return;
            if (TryClaimAction("shopBuy" + pending)) OnShopBuyClicked(pending);
        }

        /// <summary>[상점] 페이지의 폴링 클릭 라우팅. <see cref="FeedClick"/>이 이 하나만 부른다.</summary>
        private void FeedShopClick(Vector2 cursor)
        {
            for (int i = 0; i < _shopCards.Length; i++)
            {
                ShopCard card = _shopCards[i];
                if (card == null || card.Rect == null || !card.Rect.gameObject.activeInHierarchy) continue;

                if (ContainsScreenPoint(card.ActionRect, cursor))
                {
                    // 누른 순간에는 아무 일도 하지 않는다 — 구매는 <b>뗄 때</b> 확정한다.
                    _pendingShopCard = i;
                    return;
                }
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                if (TryClaimAction("shopCard" + i)) OnShopCardClicked(i);
                return;
            }
        }

        // ==================== 진단/테스트 전용 창구 ====================
        //
        // ★ 여기서 <b>기대값을 만들어 주지 않는다</b>. 내주는 것은 «지금 화면이 실제로 무엇을 적었고
        //   무엇을 누를 수 있는가»라는 관측값뿐이다(TEAM.md 「생성기와 검사기가 같이 틀린다」).

        /// <summary>상점이 파는 상품 수 — <b>정적</b> 사실이라 창을 열지 않아도 잰다.</summary>
        public static int ShopEntryCountForTests => ShopEntries.Length;

        /// <summary>상점 <paramref name="index"/>번 자리가 가리키는 <b>카탈로그 인덱스</b>. 범위 밖이면 −1.</summary>
        public static int ShopCatalogIndexForTests(int index)
        {
            int[] entries = ShopEntries;
            return (uint)index < (uint)entries.Length ? entries[index] : -1;
        }

        /// <summary>실제로 구워진 카드 수(창을 연 뒤에만 0이 아니다).</summary>
        public int ShopCardCountForTests => _shopCards.Length;

        /// <summary>그 카드의 [구매] 칩이 <b>지금 적고 있는</b> 낱말.</summary>
        public string ShopActionLabelForTests(int index)
            => index >= 0 && index < _shopCards.Length ? _shopCards[index]?.ActionLabel?.text ?? string.Empty
                : string.Empty;

        /// <summary>그 칩을 <b>실제로 누를 수 있는가</b>(uGUI 경로 기준).</summary>
        public bool ShopActionInteractableForTests(int index)
            => index >= 0 && index < _shopCards.Length && _shopCards[index]?.ActionButton != null
               && _shopCards[index].ActionButton.interactable;

        /// <summary>그 카드의 메타 줄(<c>보유 중</c> / <c>Lv.n에 열림</c>).</summary>
        public string ShopMetaTextForTests(int index)
            => index >= 0 && index < _shopCards.Length ? _shopCards[index]?.Meta?.text ?? string.Empty
                : string.Empty;

        /// <summary>상세 줄 제목 / 본문 — 못 사는 이유의 전문이 여기 있다.</summary>
        public string ShopDetailNameForTests => _shopDetailName != null ? _shopDetailName.text : null;

        public string ShopDetailBodyForTests => _shopDetailBody != null ? _shopDetailBody.text : null;

        /// <summary>지금 확인 단계에 들어간 자리(−1이면 없음).</summary>
        public int ShopConfirmIndexForTests => _shopConfirmIndex;

        /// <summary>카드/칩의 화면 사각형 — 테스트가 좌표를 손으로 적지 않게 하는 통로.</summary>
        public Rect ShopCardRawScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _shopCards.Length ? _shopCards[index]?.Rect : null);

        public Rect ShopActionRawScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _shopCards.Length ? _shopCards[index]?.ActionRect : null);

        /// <summary>
        /// 진단/테스트 전용 — <b>클릭 핸들러가 부르는 바로 그 함수</b>를 부른다.
        ///
        /// <para>★ 왜 좌표 클릭이 아닌가(<c>InventoryPagerRailTests</c>가 먼저 겪은 것과 같은 사정):
        /// 배치모드 PlayMode 화면은 640×480이라 이 창이 608pt로 줄고, 상점 격자의 대부분이
        /// <c>Body</c> 마스크 <b>밖</b>으로 잘린다. 그 자리는 이 창의 규칙("보이지 않는 것은 눌리지
        /// 않는다")에 따라 <b>정당하게</b> 안 눌린다 — 좌표로 검증하려 들면 <b>거짓 빨강</b>이 난다.</para>
        ///
        /// <para>가드를 여기서 재현하지 않는다(세 번째 사본이 곧 다음 결함이다) — 살 수 있는가는
        /// <see cref="ShopActionInteractableForTests"/>로 따로 본다.</para>
        /// </summary>
        public void ShopBuyForTests(int index) => OnShopBuyClicked(index);

        /// <summary>같은 이유로 여는 <b>고르기</b> 진입점.</summary>
        public void ShopSelectForTests(int index) => OnShopCardClicked(index);
    }
}
