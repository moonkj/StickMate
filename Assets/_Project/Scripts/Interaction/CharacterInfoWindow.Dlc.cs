using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>[DLC] 탭 — 코스튬 팩 진열대</b>(2026-09-08 배선). 이 게임 최초의 DLC 팩 3종
    /// (<c>pack.cyber</c> · <c>pack.mine</c> · <c>pack.arcane</c>, 각 4종 = 12종)이 <b>엔진상 이미
    /// 동작하는데 상품으로는 화면 어디에도 없던</b> 상태를 끝내는 자리다.
    ///
    /// ============================================================================
    /// ★★ 왜 [상점] 탭에 섞지 않았는가 — <b>이 파일이 존재하는 이유 그 자체</b>
    /// ============================================================================
    /// <see cref="IsShopMerchandise"/>의 마지막 줄은 <c>entry.CohortId == ItemCatalog.BaseCohortId</c>다.
    /// 그 필터는 <b>의도적</b>이고 <c>CharacterInfoWindow.Shop.cs</c> 클래스 문서가 이유를 적어 뒀다 —
    /// <i>"팩은 동전이 아니라 스토어 결제로 오는 층이고 이 프로젝트에는 결제 백엔드가 없다 …
    /// 팩이 실리는 날 <b>조용히 동전으로 팔리는 사고</b>를 막는다."</i>
    /// <b>오늘이 그 「팩이 실리는 날」이다.</b> 그러니 그 필터는 <b>한 글자도 건드리지 않는다</b> —
    /// 풀면 $4.99짜리 팩 아이템이 동전 몇백~몇천에 팔린다. 대신 <b>선반을 하나 더 만든다</b>.
    /// <para>그 불변식을 <c>Tests/EditMode/DlcShowcaseTests</c>가 <b>양방향</b>으로 잠근다:
    /// 동전 상점에 팩이 0개(네거티브 컨트롤) · 이 탭에 팩 12종이 전부(포지티브 컨트롤).
    /// 한쪽만 두면 «둘 다 비어 있어서 통과»와 구별되지 않는다.</para>
    ///
    /// ============================================================================
    /// ★★ 이 화면이 <b>주장하지 않는</b> 것 (원칙 1 · 지금 참인 것만 적는다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>[구매] 버튼이 없다.</b> 결제 채널이 0개다(<c>NullPackEntitlementSource</c>가 오늘의
    ///     유일한 출처이고, 스팀 SDK 배선은 0줄이다 — <c>PackEntitlement</c> 클래스 문서).
    ///     그런데도 «정말 살까요?» 확인창을 띄우면 <b>화면이 거짓말을 한다</b>. 그래서 이 탭에는
    ///     재화를 차감하는 클릭 흐름이 <b>구조적으로</b> 없다 — <see cref="CurrencyModel"/>을
    ///     이 파일에서 한 번도 부르지 않는다.</item>
    ///   <item><b>「보유 중」이라고 하지 않는다.</b> 세 팩의 매니페스트는 전부 <c>entitlements: []</c>라
    ///     <see cref="CostumeEntitlement.IsOpen(PackDescriptor)"/>가 «무료는 안 묻는 것»(규칙 C-1)에
    ///     따라 <b>StateOf를 부르지 않고 곧장 true</b>를 낸다. 즉 열려 있는 것은 맞지만 <b>산 적은
    ///     없다</b>. 그 사실에 맞는 낱말이 <see cref="DlcOpenWord"/>다.</item>
    ///   <item><b>가격을 «지금 결제되는 값»으로 말하지 않는다.</b> 아래 <see cref="DlcPriceLabel"/>
    ///     문단 참고 — 실제 스토어가 붙는 날 값의 출처는 <b>스토어 SDK</b>지 이 표가 아니다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 그림은 <b>카드 탭과 같은 렌더러</b>다 (신규 0벌)
    /// ============================================================================
    /// 썸네일은 <see cref="BuildCardArt"/>(→ <see cref="AccessoryCardIcon"/>), 등급 리본은
    /// <see cref="BuildRarityRibbon"/>, 착용 칩의 면·잉크·낱말은 <see cref="StyleActionButton"/>을
    /// <b>그대로</b> 부른다. [상점]이 카드 탭 렌더러를 그대로 쓴 것과 같은 방식이고, 치수는
    /// <see cref="CardWidth"/>·<see cref="CardHeight"/>와 [상점] 격자 상수에서 <b>파생</b>한다 —
    /// 이 저장소가 폭 1042에서 당한 사고(헤더는 따라갔는데 카드줄은 안 따라감)를 구조로 막는다.
    ///
    /// ============================================================================
    /// ★★ 착용은 <b>기존 경로 그대로</b>다 — 여기에 두 번째 게이트를 만들지 않는다
    /// ============================================================================
    /// <see cref="OnDlcEquipClicked"/>는 선택을 옮기고 <see cref="OnActionClicked"/>를 부른다.
    /// 그 함수가 이 창에서 <b>착용/해제를 실제로 수행하는 유일한 자리</b>이고, 잠김 판정은
    /// <see cref="ItemCatalogEntry.IsOwned"/> 하나가 답한다.
    ///
    /// <para>★ <b>여기서 엔타이틀먼트로 착용을 막지 않는 이유</b>(실측 후 의도적 결정):
    /// 팩 아이템 12종은 <c>requiredLevel: 1</c>이라 <see cref="ItemCatalogEntry.IsOwned"/>가 이미
    /// <b>참</b>이고, <b>[장비] 탭에서도 이미 카드로 떠서 이미 입힌다</b>
    /// (<c>ItemCatalog.IsListed</c>가 팩을 거르지 않는다 — 그게 이 빌드의 실제 상태다).
    /// 이 탭만 따로 막으면 <b>같은 아이템이 한 탭에서는 입히고 다른 탭에서는 안 입히는</b>
    /// 「반만 열린」 상태가 되고, 그 화면은 <c>CostumeEntitlement</c> 클래스 문서가 규칙 C-1로
    /// 금지한 바로 그 형태다. <b>장비의 C층 게이트는 오늘 존재하지 않는다</b>(코스튬에만 있다) —
    /// 그것이 결함이라면 고칠 자리는 이 UI가 아니라 <c>Core</c>이고, 리더에게 보고했다.</para>
    ///
    /// <para>그래도 그 불일치가 <b>조용히</b> 생기지는 않게, <see cref="RefreshDlc"/>가
    /// «팩은 닫혔는데 아이템은 입힌다»를 만나면 창 세션당 한 번 경고를 남긴다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 어휘 ====================
        //
        // ★ TODO(design-narrative · marketing): 아래 세 낱말과 상세 줄 문장은 «지금 참인 사실»을
        //   가장 짧게 적은 것이고 최종 카피가 아니다. 상품 문안은 이 축의 소관이 아니다
        //   (CLAUDE.md 팀 표 — 문구는 design-narrative, 상품 서술은 marketing).
        //   결제 채널이 실제로 붙는 라운드에 이 세 상수와 DlcDetailBodyText를 함께 다시 본다.

        /// <summary>팩이 <b>지금 열려 있다</b>는 사실의 낱말. <b>「보유 중」이 아니다</b> —
        /// 산 적이 없기 때문이다(<see cref="ShopOwnedWord"/>는 동전으로 산 것의 낱말이라 같은 자리에
        /// 쓰면 화면이 «결제했다»고 주장하게 된다).
        /// <para>★ <c>public</c>인 이유는 <see cref="ShopOwnedWord"/>와 같다 — 테스트가 문구를
        /// 베끼지 않게(PlayMode 어셈블리에는 <c>InternalsVisibleTo</c>가 없다).</para></summary>
        public const string DlcOpenWord = "미리보기";

        /// <summary>팩이 닫혀 있을 때의 낱말. 오늘 이 값이 화면에 뜨는 경우는 <b>없다</b>
        /// (매니페스트 3개 전부 <c>entitlements: []</c>). 그래도 분기를 비워 두지 않는 이유는,
        /// 채널이 붙는 날 <c>else</c>가 <b>말 없는 빈 칸</b>이 되는 것을 막기 위해서다.</summary>
        public const string DlcLockedWord = "잠김";

        /// <summary>가격표에 없는 팩의 낱말. <b>«무료»가 아니다</b> — 모른다는 뜻이다
        /// (<c>PackEntitlementRef.entitlementId</c>가 빈 문자열에 대해 세운 그 어법과 같다).</summary>
        public const string DlcPriceUnknownWord = "가격 미정";

        /// <summary>
        /// ★ 상위 티어 팩의 <b>표시</b> 가격. 출처는 <c>docs/strategy/CHANNEL_PRICING_DECISIONS.md</c>
        /// 9회차 배너(문서 최상단, §0-0-G) — <i>"사이버펑크연구원 = <c>pack.cyber</c>($4.99 …) /
        /// 광부 = 신규 7번 코호트 $4.99 / 대마법사 = 신규 8번 코호트 $4.99"</i>.
        ///
        /// <para><b>이 값은 결제 금액이 아니라 안내 문구다.</b> 실제 스토어가 붙는 날 화면에 뜨는
        /// 값의 출처는 <b>스토어 SDK가 돌려주는 지역화 가격</b>이지 이 상수가 아니다(통화·환율·
        /// 지역 가격은 우리가 정하는 값이 아니다 — 같은 문서 §14-4가 KRW를 «스팀 파트너 사이트의
        /// 권장값을 그대로 쓴다»로 닫았다). 그날 이 상수는 <b>지워진다</b>.</para>
        ///
        /// <para>그때까지 이 값이 문서와 갈라지는 것을 <c>DlcShowcaseTests</c>가 막는다 —
        /// 테스트는 숫자를 베끼지 않고 <b>이 상수를 들고 문서를 읽어</b> 대조한다.</para>
        /// </summary>
        public const string DlcTopTierPriceLabel = "$4.99";

        // ==================== 치수 — 새 숫자는 하나도 없다 ====================

        /// <summary>격자 폭·높이·상세 줄 자리는 [상점]과 <b>같은 골격</b>이다. 전체폭 페이지 셋이
        /// 같은 자리에서 시작하고 끝나야 탭을 오갈 때 아래 상세 줄이 위아래로 튀지 않는다.</summary>
        private const float DlcGridWidth = ShopGridWidth;

        private const float DlcGridHeight = ShopGridHeight;
        private const float DlcDetailY = ShopDetailY;
        private const float DlcDetailHeight = ShopDetailHeight;

        /// <summary>한 팩 줄에 들어가는 카드 수. [상점] 격자와 <b>같은 파생값</b>이다(폭에서 나온다).</summary>
        private const int DlcColumns = ShopColumns;

        /// <summary>팩 제목줄 — 카테고리 제목줄(<see cref="CategoryHeaderHeight"/>)과 같은 높이·같은
        /// 시각 언어(막대 + 굵은 제목 + 오른쪽 상태 칸). 이 창에서 «묶음의 머리»는 이미 그 모양이다.</summary>
        private const float DlcPackHeaderHeight = CategoryHeaderHeight;

        private const float DlcPackHeaderGap = UiChrome.Space2;
        private const float DlcPackBlockGap = UiChrome.Space4;

        private const float DlcPackBarWidth = 3f;
        private const float DlcPackTitleX = DlcPackBarWidth + UiChrome.Space2;
        private const float DlcPackTitleWidth = 132f;
        private const float DlcPackMetaX = DlcPackTitleX + DlcPackTitleWidth;

        /// <summary>오른쪽 상태 칸. <see cref="StatusSlotWidth"/>는 [보관함]이 «훗날 가격표가 들어올
        /// 자리»로 잡아 둔 그 폭이다 — 두 표면이 같은 성격의 칸에 다른 폭을 쓰지 않게 그대로 쓴다.</summary>
        private const float DlcPackStatusWidth = StatusSlotWidth;

        private const float DlcPackStatusX = DlcGridWidth - DlcPackStatusWidth;
        private const float DlcPackMetaWidth = DlcPackStatusX - DlcPackMetaX - UiChrome.Space3;

        // ==================== 진열 목록 — 정적 사실이다 ====================

        /// <summary>팩 하나와 그 팩이 실제로 실은 아이템의 <b>카탈로그 인덱스</b>.</summary>
        private sealed class DlcPackBlock
        {
            public PackDescriptor Pack;
            public int[] Entries;
        }

        private static DlcPackBlock[] _dlcBlocks;
        private static int[] _dlcFlat;

        /// <summary>
        /// 진열할 팩 블록. <b>목록의 출처는 <see cref="PackRegistry"/> 하나</b>다 —
        /// 팩 아이디를 여기 적으면 «폴더에 매니페스트를 떨어뜨리면 새 팩»이라는 통로가 벽이 된다
        /// (<c>PackRegistry</c> 클래스 문서가 세운 규칙).
        ///
        /// <para><b>코호트 번호 순</b>으로 세운다. <c>Resources.LoadAll</c>의 순서는 보장되지 않아서,
        /// 그대로 쓰면 <b>같은 빌드에서 팩 순서가 실행마다 달라 보일 수</b> 있다.</para>
        ///
        /// <para>고아 코호트(아이템은 팩 소속이라 적었는데 매니페스트가 없다)는 여기 <b>안 들어온다</b> —
        /// 팩을 돌면서 모으기 때문이다. 그건 누락이 아니라 정확한 처리다: 그 아이템은
        /// <c>PackRegistry.Build</c>가 «살 방법이 없다»고 이미 결함으로 신고한 것이고,
        /// 살 방법이 없는 것을 상품 선반에 올리면 안 된다.</para>
        /// </summary>
        private static DlcPackBlock[] DlcBlocks
        {
            get
            {
                if (_dlcBlocks != null) return _dlcBlocks;

                var packs = new System.Collections.Generic.List<PackDescriptor>(PackRegistry.Packs);
                packs.Sort((a, b) => a.CohortId.CompareTo(b.CohortId));

                var blocks = new System.Collections.Generic.List<DlcPackBlock>(packs.Count);
                var flat = new System.Collections.Generic.List<int>(packs.Count * 4);

                for (int p = 0; p < packs.Count; p++)
                {
                    PackDescriptor pack = packs[p];
                    var mine = new System.Collections.Generic.List<int>(pack.ResolvedItemCount);
                    for (int i = 0; i < ItemCatalog.Count; i++)
                    {
                        ItemCatalogEntry entry = ItemCatalog.At(i);
                        if (!IsDlcMerchandise(entry)) continue;
                        if (entry.CohortId != pack.CohortId) continue;
                        mine.Add(i);
                    }
                    if (mine.Count == 0) continue;   // 아이템이 하나도 안 실린 팩은 선반이 아니라 결함이다(PackRegistry가 신고한다).

                    // 슬롯 순 → 자리 번호 순. 카드 줄이 [장비] 탭의 카테고리 순서와 같은 방향으로 읽힌다.
                    mine.Sort((a, b) =>
                    {
                        ItemCatalogEntry x = ItemCatalog.At(a), y = ItemCatalog.At(b);
                        int bySlot = ((int)x.Slot.Value).CompareTo((int)y.Slot.Value);
                        return bySlot != 0 ? bySlot : x.ItemIndex.CompareTo(y.ItemIndex);
                    });

                    blocks.Add(new DlcPackBlock { Pack = pack, Entries = mine.ToArray() });
                    flat.AddRange(mine);
                }

                _dlcFlat = flat.ToArray();
                _dlcBlocks = blocks.ToArray();
                return _dlcBlocks;
            }
        }

        /// <summary>진열 순서대로 편 카탈로그 인덱스(카드 <b>자리 번호</b> ↔ 아이템).</summary>
        private static int[] DlcFlatEntries
        {
            get
            {
                if (_dlcFlat == null) { var _ = DlcBlocks; }
                return _dlcFlat;
            }
        }

        /// <summary>이 항목이 <b>팩 진열대</b>에 오르는가. <see cref="IsShopMerchandise"/>와
        /// <b>마지막 줄만</b> 다르다(기본 코호트가 아니라 팩 코호트) — 그 대칭이 곧
        /// «한 아이템이 두 선반에 동시에 오르지 않는다»는 보증이고, 테스트가 그것을 직접 잰다.</summary>
        private static bool IsDlcMerchandise(ItemCatalogEntry entry)
        {
            if (entry == null) return false;
            if (entry.Category != ItemCategory.Equipment) return false;
            if (!entry.Slot.HasValue || entry.ItemIndex < 0) return false;
            if (!ItemCatalog.IsListed(entry)) return false;        // 은퇴분은 어느 선반에도 없다.
            return entry.CohortId != ItemCatalog.BaseCohortId;     // ★ 동전 경제 밖 = 이쪽 선반
        }

        private static ItemCatalogEntry DlcEntryAt(int index)
        {
            int[] flat = DlcFlatEntries;
            return (uint)index < (uint)flat.Length ? ItemCatalog.At(flat[index]) : null;
        }

        /// <summary>이 자리의 아이템이 속한 팩. 진열 목록이 팩에서 나왔으므로 <c>null</c>이 아니다.</summary>
        private static PackDescriptor DlcPackAt(int index)
        {
            DlcPackBlock[] blocks = DlcBlocks;
            int seen = 0;
            for (int b = 0; b < blocks.Length; b++)
            {
                if (index < seen + blocks[b].Entries.Length) return blocks[b].Pack;
                seen += blocks[b].Entries.Length;
            }
            return null;
        }

        // ==================== 가격 — 표시 문구이지 결제 금액이 아니다 ====================

        /// <summary>
        /// 이 팩의 표시 가격.
        ///
        /// ============================================================================
        /// ★★ 여기에 <b>팩 아이디 → 가격 표</b>를 두지 마라 (한 번 시도했다가 감사가 잡았다)
        /// ============================================================================
        /// 2026-09-08 최초 구현은 <c>{ "pack.cyber", "pack.mine", "pack.arcane" }</c> 배열을 들고
        /// 아이디로 가격을 골랐다. <c>PackManifestCorridorTests.프로덕션에_팩_아이디가_리터럴로_박혀_있지_않다</c>가
        /// 그 자리에서 <b>3건을 신고</b>했고, 그 감사가 옳다: <i>"팩은 에셋으로 늘어난다. 코드가 아이디를
        /// 알면 일곱 번째 팩은 그 목록에 없고, 증상은 예외가 아니라 <b>조용한 미적재</b>다."</i>
        ///
        /// <para><b>그래서 런타임은 티어를 모른다.</b> 가격 티어(상위 $4.99 / 하위 $2.99)의 판정 근거는
        /// <i>"신규 모션·리그가 있는가"</i>(같은 문서 P-4′)인데, 그건 매니페스트에도
        /// <see cref="PackDescriptor"/>에도 없는 값이다 — 일부러 없다(값의 주인은 스토어다).</para>
        ///
        /// <para>⇒ <b>지금 실린 팩은 전부 상위 티어다</b>(문서 9회차 배너가 셋을 그렇게 확정했다).
        /// 그 전제가 깨지는 순간 — 즉 하위 티어 팩이나 무료 팩이 폴더에 들어오는 순간 —
        /// 이 함수는 <b>틀린 값을 말하게 된다</b>. 그 사고를 막는 장치는 런타임이 아니라
        /// <b>테스트</b>에 둔다: <c>Tests/EditMode/DlcShowcaseTests.표시_가격이_기획_문서의_확정값과_같다</c>가
        /// «실린 팩 전부가 문서에서 이 값으로 확정된 그 팩인가»를 <b>기획 문서를 읽어</b> 확인하고,
        /// 새 팩이 문서 갱신 없이 들어오면 그 라운드에 빨개진다. 테스트 파일은 위 감사의 대상이
        /// 아니므로 거기서는 아이디를 이름으로 부를 수 있다.</para>
        /// </summary>
        private static string DlcPriceLabel(PackDescriptor pack)
            => pack == null ? DlcPriceUnknownWord : DlcTopTierPriceLabel;

        /// <summary>
        /// 화면에 낼 팩 이름. <b>오늘은 팩 아이디 그대로다</b> — 매니페스트가 들고 있는 것은
        /// <see cref="PackDescriptor.DisplayNameKey"/>, 즉 <b>로컬라이즈 키뿐</b>이고 번역 테이블은
        /// 이 빌드에 없다(<c>docs/localization/PLAN_1.0.md</c>). 같은 사정을 세트 패널 3행이 먼저
        /// 만나 <i>"오늘 화면에 낼 낱말이 없다"</i>고 적어 뒀다(<c>CharacterInfoWindow.Stats.cs</c>).
        ///
        /// <para><b>여기서 한글 이름을 지어내지 않는다.</b> 상품명은 이 축의 소관이 아니고
        /// (<c>marketing</c> · <c>design-narrative</c>), 지어내면 스토어 페이지와 <b>다른 이름</b>이
        /// 화면에 박힌다. 팩 아이디는 SKU 그 자체라 최소한 <b>거짓이 아니다</b>.</para>
        ///
        /// <para>번역 테이블이 오는 날 고칠 곳은 <b>이 함수 하나</b>다.</para>
        /// </summary>
        private static string DlcPackTitle(PackDescriptor pack)
            => pack == null ? "—" : pack.PackId;

        // ==================== 화면 부품 ====================

        private sealed class DlcCard
        {
            public int Index;
            public RectTransform Rect;
            public Image Surface;
            public Image Outline;
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

        private GameObject _dlcPage;
        private ScrollRect _dlcScroll;
        private RectTransform _dlcViewport;
        private RectTransform _dlcContent;
        private DlcCard[] _dlcCards = System.Array.Empty<DlcCard>();
        private Text[] _dlcPackStatus = System.Array.Empty<Text>();
        private Text _dlcDetailName;
        private Text _dlcDetailBody;

        private int _dlcSelected;
        private bool _dlcGrabbed;
        private bool _dlcMoved;
        private float _dlcGrabScreenY;
        private float _dlcStartContentY;
        private int _pendingDlcCard = -1;

        /// <summary>«팩은 닫혔는데 아이템은 입힌다»를 이 창 세션에 이미 알렸는가.
        /// 0.25초마다 같은 경고를 쏟으면 상주 앱의 로그가 못 쓰게 된다.</summary>
        private bool _dlcHalfOpenWarned;

        // ==================== 구성 ====================

        private void BuildDlcPage(RectTransform body)
        {
            var pageGo = new GameObject("DlcPage", typeof(RectTransform));
            pageGo.transform.SetParent(body, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, PanelWidth, BodyHeight);
            _dlcPage = pageGo;

            // ---- 격자(세로 스크롤) ---- [상점]과 같은 장치·같은 규칙(관성 없음 · Clamped).
            var gridGo = new GameObject("DlcGrid", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            gridGo.transform.SetParent(page, false);
            var grid = gridGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(grid, PagePadX, PageTopY, DlcGridWidth, DlcGridHeight);

            Image handle = gridGo.GetComponent<Image>();
            handle.color = Color.clear;
            handle.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(grid, false);
            _dlcViewport = viewportGo.GetComponent<RectTransform>();
            UiChrome.Stretch(_dlcViewport);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_dlcViewport, false);
            _dlcContent = contentGo.GetComponent<RectTransform>();
            _dlcContent.anchorMin = _dlcContent.anchorMax = _dlcContent.pivot = new Vector2(0f, 1f);
            _dlcContent.anchoredPosition = Vector2.zero;

            _dlcScroll = gridGo.GetComponent<ScrollRect>();
            _dlcScroll.viewport = _dlcViewport;
            _dlcScroll.content = _dlcContent;
            _dlcScroll.horizontal = false;
            _dlcScroll.vertical = true;
            _dlcScroll.movementType = ScrollRect.MovementType.Clamped;
            _dlcScroll.inertia = false;
            _dlcScroll.scrollSensitivity = CardStepY * 0.5f;
            _dlcScroll.horizontalScrollbar = null;
            _dlcScroll.verticalScrollbar = null;

            // ---- 팩 블록 ----
            DlcPackBlock[] blocks = DlcBlocks;
            var cards = new System.Collections.Generic.List<DlcCard>(DlcFlatEntries.Length);
            var statuses = new Text[blocks.Length];
            int columns = Mathf.Max(1, DlcColumns);
            float y = 0f;

            for (int b = 0; b < blocks.Length; b++)
            {
                DlcPackBlock block = blocks[b];
                statuses[b] = BuildDlcPackHeader(_dlcContent, block, y);
                y += DlcPackHeaderHeight + DlcPackHeaderGap;

                for (int i = 0; i < block.Entries.Length; i++)
                {
                    cards.Add(BuildDlcCard(_dlcContent, cards.Count, ItemCatalog.At(block.Entries[i]),
                        (i % columns) * CardStepX, -(y + (i / columns) * CardStepY)));
                }

                int rows = Mathf.CeilToInt(block.Entries.Length / (float)columns);
                y += rows * CardHeight + Mathf.Max(0, rows - 1) * CardGap + DlcPackBlockGap;
            }

            _dlcCards = cards.ToArray();
            _dlcPackStatus = statuses;
            _dlcContent.sizeDelta = new Vector2(DlcGridWidth,
                Mathf.Max(DlcGridHeight, Mathf.Max(0f, y - DlcPackBlockGap)));

            // ---- 상세 줄 ---- [상점]·[보관함]과 같은 자리·같은 모양.
            Image detail = UiChrome.AddSurface(page, "DlcDetail", UiChrome.SubtleSurface, UiChrome.RadiusCard);
            RectTransform drt = detail.rectTransform;
            UiChrome.PlaceTopLeft(drt, PagePadX, DlcDetailY, DlcGridWidth, DlcDetailHeight);
            detail.raycastTarget = false;
            UiChrome.AddOutline(drt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.SubtleSurface), UiChrome.RadiusCard);

            _dlcDetailName = Label(drt, "DetailName", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, 15f, -14f, DlcGridWidth - 30f, 17f, "—", bold: true);

            _dlcDetailBody = UiChrome.AddText(drt, "DetailBody", UiChrome.FontBody, TextAnchor.UpperLeft,
                UiChrome.TextSecondary, wrap: true);
            UiChrome.PlaceTopLeft(_dlcDetailBody.rectTransform, 15f, -42f, DlcGridWidth - 30f, 34f);
            _dlcDetailBody.lineSpacing = 1.6f;

            if (blocks.Length == 0)
            {
                // 팩이 0개인 빌드는 정상이다(매니페스트를 안 실은 빌드). 그래도 <b>말은 한다</b> —
                // 빈 선반과 «화면이 고장났다»는 겉보기가 같다.
                Debug.Log("[DLC] 실린 팩이 0개라 진열대가 비어 있습니다 — 매니페스트가 없는 빌드입니다.");
            }
        }

        /// <summary>팩 제목줄. <b>막대 색은 매니페스트가 선언한 팩 주색</b>이다(우리가 고르지 않는다).</summary>
        private Text BuildDlcPackHeader(RectTransform content, DlcPackBlock block, float y)
        {
            var headerGo = new GameObject("PackHeader_" + block.Pack.PackId, typeof(RectTransform));
            headerGo.transform.SetParent(content, false);
            var header = headerGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(header, 0f, -y, DlcGridWidth, DlcPackHeaderHeight);

            // α<1을 그대로 얹지 않는다 — 이 창의 화소는 유저 바탕화면 위에 있다(UiChrome 알파의 법칙).
            Image bar = UiChrome.AddSurface(header, "Bar",
                UiChrome.Flatten(block.Pack.PrimaryColor, UiChrome.PanelSurface), UiChrome.RadiusDot);
            UiChrome.PlaceTopLeft(bar.rectTransform, 0f, -2f, DlcPackBarWidth, 15f);
            bar.raycastTarget = false;

            Text title = Label(header, "Name", UiChrome.FontTitle, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                DlcPackTitleX, 0f, DlcPackTitleWidth, DlcPackHeaderHeight,
                DlcPackTitle(block.Pack), bold: true);
            title.text = UiChrome.Ellipsize(title, DlcPackTitle(block.Pack), DlcPackTitleWidth);
            title.raycastTarget = false;

            // «몇 종 · 얼마»는 팩의 정적 사실이라 한 번만 적는다(런타임 상태가 아니다).
            Text meta = Label(header, "Meta", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                DlcPackMetaX, 0f, DlcPackMetaWidth, DlcPackHeaderHeight,
                $"{block.Entries.Length}종  ·  {DlcPriceLabel(block.Pack)}  ·  팩 단위 판매(개별 구매 없음)");
            meta.raycastTarget = false;

            Text status = Label(header, "Status", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.Accent,
                DlcPackStatusX, 0f, DlcPackStatusWidth, DlcPackHeaderHeight, DlcOpenWord);
            status.raycastTarget = false;
            return status;
        }

        /// <summary>카드 한 장. <b>치수는 전부 카드 탭의 상수</b>이고 이 함수가 정하는 것은 자리뿐이다.</summary>
        private DlcCard BuildDlcCard(RectTransform content, int index, ItemCatalogEntry entry, float x, float y)
        {
            EquipmentSlot slot = entry.Slot.Value;
            int itemIndex = entry.ItemIndex;

            Image surface = UiChrome.AddSurface(content, "DlcCard" + index, UiChrome.CardSurface, UiChrome.RadiusCard);
            RectTransform rt = surface.rectTransform;
            UiChrome.PlaceTopLeft(rt, x, y, CardWidth, CardHeight);

            Image outline = UiChrome.AddOutline(rt, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), UiChrome.RadiusCard);

            Image thumb = UiChrome.AddSurface(rt, "Thumb", UiChrome.CardSurfaceMuted, UiChrome.RadiusThumb);
            UiChrome.PlaceTopLeft(thumb.rectTransform, ThumbX, ThumbY, ThumbWidth, ThumbHeight);
            thumb.raycastTarget = false;

            RarityRibbon ribbon = BuildRarityRibbon(rt, CardRibbonInset, -CardRibbonTopMargin, CardRibbonWidth);
            ItemRarity rarity = ItemCatalog.Rarity(slot, itemIndex);
            ApplyRarityRibbon(ribbon, rarity);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(thumb.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(IconSize, IconSize);
            irt.anchoredPosition = Vector2.zero;
            BuildCardArt(irt, slot, itemIndex, entry);   // 카드 탭·상점과 <b>같은 렌더러</b>

            var card = new DlcCard
            {
                Index = index,
                Rect = rt,
                Surface = surface,
                Outline = outline,
                Name = Label(rt, "Name", UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary,
                    CardPadX, CardNameY, CardNameWidth, CardTextHeight, "—", bold: true),
                Rarity = Label(rt, "Rarity", UiChrome.FontCaption, TextAnchor.MiddleRight, UiChrome.TextTertiary,
                    CardRarityX, CardNameY, CardRarityWidth, CardTextHeight, "—"),
                Meta = Label(rt, "Meta", UiChrome.FontLabel, TextAnchor.MiddleLeft, UiChrome.InkMeta,
                    CardMetaX, CardMetaY, CardMetaWidth, CardMetaHeight, "—"),
                Category = Label(rt, "Category", UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.TextTertiary,
                    CardPadX, CardCategoryY, CardContentWidth, CardCategoryHeight, "—"),
            };

            // 이 넷은 아이템의 <b>정적</b> 사실이라 한 번만 굽는다(런타임 상태가 아니다).
            card.Name.text = UiChrome.Ellipsize(card.Name, entry.DisplayName, CardNameWidth);
            card.Rarity.text = ItemCatalog.RarityName(rarity);
            card.Rarity.color = UiChrome.RarityColor(rarity);
            card.Category.text = entry.CategoryLabel;
            // ★ 요구사항 «각 카드에 가격 표시». 낱개 값이 아니라 <b>팩 값</b>이라는 사실을 낱말이 말한다
            //   (팩 단위 판매는 사용자 확정 — CHANNEL_PRICING_DECISIONS.md §0-0 (가)).
            card.Meta.text = "팩 " + DlcPriceLabel(DlcPackAt(index));

            // ---- 착용 칩 ---- 면·잉크·낱말을 카드 탭 함수에게 <b>그대로 맡긴다</b>(새 벌 0개).
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
            actionButton.transition = Selectable.Transition.None;
            card.ActionButton = actionButton;

            int captured = index;
            actionButton.onClick.AddListener(() =>
            {
                if (SuppressedByGridDrag()) return;
                if (TryClaimAction("dlcEquip" + captured)) OnDlcEquipClicked(captured);
            });

            var bodyButton = surface.gameObject.AddComponent<Button>();
            bodyButton.targetGraphic = surface;
            bodyButton.transition = Selectable.Transition.None;
            bodyButton.onClick.AddListener(() =>
            {
                if (SuppressedByGridDrag()) return;
                if (TryClaimAction("dlcCard" + captured)) OnDlcCardClicked(captured);
            });

            return card;
        }

        // ==================== 갱신 ====================

        private void ApplyDlcPage(bool visible)
        {
            if (_dlcPage == null) return;
            if (_dlcPage.activeSelf != visible) _dlcPage.SetActive(visible);
            if (!visible) _pendingDlcCard = -1;
        }

        /// <summary>카드 상태(착용/해제) + 팩 상태 + 상세 줄. <b>사건이 있을 때만</b> 부른다 —
        /// 이 탭에는 «잔액처럼 조용히 변하는 값»이 없어서 주기 갱신이 필요 없다.</summary>
        private void RefreshDlc()
        {
            if (_dlcCards.Length == 0) return;
            if (_dlcSelected < 0 || _dlcSelected >= _dlcCards.Length) _dlcSelected = 0;

            for (int i = 0; i < _dlcCards.Length; i++)
            {
                DlcCard card = _dlcCards[i];
                ItemCatalogEntry entry = DlcEntryAt(i);
                if (card == null || entry == null) continue;

                bool owned = entry.IsOwned(_config);
                bool worn = entry.IsEquipped();

                // 낱말·면·잉크는 카드 탭 함수 하나가 정한다(두 표면이 같은 사실을 두 말로 하지 않는다).
                StyleActionButton(card.ActionSurface, card.ActionOutline, card.ActionLabel, card.ActionButton,
                    owned, worn);

                card.Outline.color = i == _dlcSelected
                    ? UiChrome.TextPrimary
                    : UiChrome.Flatten(UiChrome.RarityBorder(ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex)),
                        UiChrome.CardSurface);
            }

            RefreshDlcPackStatus();
            RefreshDlcDetail();
        }

        /// <summary>팩 제목줄 오른쪽 상태 낱말. <b>판정의 출처는 <see cref="CostumeEntitlement"/> 하나</b>다 —
        /// 이 파일은 <c>PackEntitlements.StateOf</c>를 부르지 않는다(계약서 I-5, 감사가 그것을 잰다).</summary>
        private void RefreshDlcPackStatus()
        {
            DlcPackBlock[] blocks = DlcBlocks;
            for (int b = 0; b < blocks.Length && b < _dlcPackStatus.Length; b++)
            {
                bool open = CostumeEntitlement.IsOpen(blocks[b].Pack);
                Text status = _dlcPackStatus[b];
                if (status != null)
                {
                    status.text = open ? DlcOpenWord : DlcLockedWord;
                    status.color = open ? UiChrome.Accent : UiChrome.TextTertiary;
                }

                // ★ «팩은 닫혔는데 아이템은 입힌다» — 오늘은 일어날 수 없지만(entitlements: []),
                //   채널이 붙는 날 이 두 사실이 갈라지면 그건 조용히 넘길 일이 아니다(클래스 문서).
                if (!open && !_dlcHalfOpenWarned)
                {
                    for (int i = 0; i < blocks[b].Entries.Length; i++)
                    {
                        ItemCatalogEntry entry = ItemCatalog.At(blocks[b].Entries[i]);
                        if (entry == null || !entry.IsOwned(_config)) continue;
                        _dlcHalfOpenWarned = true;
                        Debug.LogWarning($"[DLC] '{blocks[b].Pack.PackId}'는 닫혀 있는데 그 아이템 " +
                            $"'{entry.Id}'는 IsOwned=true입니다 — 「반만 열린」 상태입니다. " +
                            "장비의 유료 권한 게이트는 Core에 있어야 합니다(이 화면이 아니라).");
                        break;
                    }
                }
            }
        }

        private void RefreshDlcDetail()
        {
            ItemCatalogEntry entry = DlcEntryAt(_dlcSelected);
            if (entry == null || _dlcDetailName == null || _dlcDetailBody == null) return;

            PackDescriptor pack = DlcPackAt(_dlcSelected);
            ItemRarity rarity = ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex);

            _dlcDetailName.text =
                $"{entry.DisplayName}   ·   {ItemCatalog.RarityName(rarity)}   ·   {entry.CategoryLabel}   ·   {DlcPackTitle(pack)}";
            _dlcDetailBody.text = DlcDetailBodyText(pack);
        }

        /// <summary>
        /// 상세 줄의 문장 — <b>오늘 참인 것만</b> 적는다.
        /// <list type="bullet">
        ///   <item>팩 단위 판매 · 개별 구매 없음 — 사용자 확정(<c>CHANNEL_PRICING_DECISIONS.md</c> §0-0 (가)).</item>
        ///   <item>지금 열려 있는 이유 — 채널 항목이 0개라 «무료는 안 묻는 것»(규칙 C-1)에 걸린다.</item>
        /// </list>
        /// ★ TODO(design-narrative · marketing): 최종 카피 대상. 여기 적힌 것은 사실 진술이다.
        /// </summary>
        private static string DlcDetailBodyText(PackDescriptor pack)
        {
            string price = DlcPriceLabel(pack);
            string head = string.Equals(price, DlcPriceUnknownWord, System.StringComparison.Ordinal)
                ? "팩 단위로만 판매돼요(개별 구매 없음) · 팩 가격은 아직 정해지지 않았어요."
                : $"팩 단위로만 판매돼요(개별 구매 없음) · 팩 가격 {price}.";

            return CostumeEntitlement.IsOpen(pack)
                ? head + " 지금은 결제 창구가 없어 전부 열려 있어요 — 카드 아래 버튼으로 바로 입어 볼 수 있어요."
                : head + " 이 팩은 지금 잠겨 있어요.";
        }

        // ==================== 조작 ====================

        /// <summary>카드 <b>본체</b> 클릭 = 고르기. 입는 것은 하단 칩만 한다
        /// (카드 탭·상점이 «고르기»와 «입기/사기»를 나눈 것과 같은 이유).</summary>
        private void OnDlcCardClicked(int index)
        {
            if (index < 0 || index >= _dlcCards.Length) return;
            if (_dlcSelected == index) return;

            _dlcSelected = index;
            RefreshDlc();

            ItemCatalogEntry entry = DlcEntryAt(index);
            if (entry != null)
            {
                Debug.Log($"[DLC] 선택 -> {entry.DisplayName}({entry.CategoryLabel}, {DlcPackTitle(DlcPackAt(index))}).");
            }
        }

        /// <summary>착용 칩. <b>착용 경로를 새로 만들지 않는다</b> — 선택을 옮기고
        /// <see cref="OnActionClicked"/>를 부른다(이 창에서 착용을 수행하는 유일한 자리).</summary>
        private void OnDlcEquipClicked(int index)
        {
            ItemCatalogEntry entry = DlcEntryAt(index);
            if (entry == null || !entry.Slot.HasValue) return;

            _dlcSelected = index;
            // 버튼을 눌렀는데 아래 상세가 다른 아이템을 설명하고 있으면 화면이 두 가지를 말한다.
            _selectedSlot = entry.Slot.Value;
            _selectedItem = entry.ItemIndex;
            OnActionClicked();
            RefreshDlc();
        }

        // ==================== 전역 폴링 드래그 ([상점] 격자와 같은 형태) ====================

        private void ArmDlcDrag(Vector2 cursor)
        {
            _dlcGrabbed = false;
            _dlcMoved = false;
            if (Def(_tab).Page != TabPage.Dlc) return;
            if (_dlcContent == null || _dlcViewport == null) return;
            if (MaxDlcScroll() <= 0f) return;
            if (!ContainsScreenPoint(_dlcViewport, cursor)) return;

            _dlcGrabbed = true;
            _dlcGrabScreenY = cursor.y;
            _dlcStartContentY = _dlcContent.anchoredPosition.y;
        }

        private void DragDlcTo(Vector2 cursor)
        {
            if (!_dlcGrabbed || _dlcContent == null) return;

            float delta = (cursor.y - _dlcGrabScreenY) / CanvasScale();
            if (!_dlcMoved && Mathf.Abs(delta) < GridDragThresholdPoints) return;
            _dlcMoved = true;
            _lastGridMoveTime = Time.unscaledTime;   // 민 직후의 뗌이 착용으로 오인되지 않게(격자와 같은 시계).

            Vector2 p = _dlcContent.anchoredPosition;
            p.y = Mathf.Clamp(_dlcStartContentY + delta, 0f, MaxDlcScroll());
            _dlcContent.anchoredPosition = p;
        }

        private void EndDlcDrag()
        {
            _dlcGrabbed = false;
            _dlcMoved = false;
        }

        private float MaxDlcScroll()
        {
            if (_dlcContent == null || _dlcViewport == null) return 0f;
            return Mathf.Max(0f, _dlcContent.rect.height - _dlcViewport.rect.height);
        }

        /// <summary>누름 때 보류해 둔 착용을 <b>뗄 때</b> 확정한다(미는 손짓이 착용이 되면 안 된다).</summary>
        private void ResolvePendingDlcEquip(Vector2 cursor, bool hasCursor)
        {
            int pending = _pendingDlcCard;
            _pendingDlcCard = -1;
            if (pending < 0 || _dlcMoved || !hasCursor) return;

            DlcCard card = pending < _dlcCards.Length ? _dlcCards[pending] : null;
            if (card == null || card.Rect == null || !card.Rect.gameObject.activeInHierarchy) return;
            if (!ContainsScreenPoint(card.ActionRect, cursor)) return;
            if (TryClaimAction("dlcEquip" + pending)) OnDlcEquipClicked(pending);
        }

        /// <summary>[DLC] 페이지의 폴링 클릭 라우팅. <see cref="FeedClick"/>이 이 하나만 부른다.</summary>
        private void FeedDlcClick(Vector2 cursor)
        {
            for (int i = 0; i < _dlcCards.Length; i++)
            {
                DlcCard card = _dlcCards[i];
                if (card == null || card.Rect == null || !card.Rect.gameObject.activeInHierarchy) continue;

                if (ContainsScreenPoint(card.ActionRect, cursor))
                {
                    _pendingDlcCard = i;   // 착용은 <b>뗄 때</b> 확정한다.
                    return;
                }
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                if (TryClaimAction("dlcCard" + i)) OnDlcCardClicked(i);
                return;
            }
        }

        // ==================== 진단/테스트 전용 창구 ====================
        //
        // ★ 기대값을 만들어 주지 않는다 — 내주는 것은 «지금 화면이 무엇을 적었는가»뿐이다.

        /// <summary>진열대에 오른 팩 수 — <b>정적</b> 사실이라 창을 열지 않아도 잰다.</summary>
        public static int DlcPackCountForTests => DlcBlocks.Length;

        /// <summary>진열대에 오른 아이템 수(팩 전체 합).</summary>
        public static int DlcEntryCountForTests => DlcFlatEntries.Length;

        /// <summary><paramref name="index"/>번 자리가 가리키는 <b>카탈로그 인덱스</b>. 범위 밖이면 −1.</summary>
        public static int DlcCatalogIndexForTests(int index)
        {
            int[] flat = DlcFlatEntries;
            return (uint)index < (uint)flat.Length ? flat[index] : -1;
        }

        /// <summary><paramref name="pack"/>번째 팩의 아이디(진열 순서 = 코호트 번호 순).</summary>
        public static string DlcPackIdForTests(int pack)
        {
            DlcPackBlock[] blocks = DlcBlocks;
            return (uint)pack < (uint)blocks.Length ? blocks[pack].Pack.PackId : null;
        }

        /// <summary>그 팩의 아이템 수.</summary>
        public static int DlcPackItemCountForTests(int pack)
        {
            DlcPackBlock[] blocks = DlcBlocks;
            return (uint)pack < (uint)blocks.Length ? blocks[pack].Entries.Length : -1;
        }

        /// <summary>그 팩의 <b>표시 가격 문구</b>(화면이 실제로 적는 그 문자열).</summary>
        public static string DlcPackPriceLabelForTests(int pack)
        {
            DlcPackBlock[] blocks = DlcBlocks;
            return (uint)pack < (uint)blocks.Length ? DlcPriceLabel(blocks[pack].Pack) : null;
        }

        /// <summary>실제로 구워진 카드 수(창을 연 뒤에만 0이 아니다).</summary>
        public int DlcCardCountForTests => _dlcCards.Length;

        /// <summary>그 카드가 지금 적고 있는 이름 / 가격 메타 / 착용 칩 낱말.</summary>
        public string DlcCardNameForTests(int index)
            => index >= 0 && index < _dlcCards.Length ? _dlcCards[index]?.Name?.text ?? string.Empty : string.Empty;

        public string DlcCardMetaForTests(int index)
            => index >= 0 && index < _dlcCards.Length ? _dlcCards[index]?.Meta?.text ?? string.Empty : string.Empty;

        public string DlcActionLabelForTests(int index)
            => index >= 0 && index < _dlcCards.Length ? _dlcCards[index]?.ActionLabel?.text ?? string.Empty : string.Empty;

        public bool DlcActionInteractableForTests(int index)
            => index >= 0 && index < _dlcCards.Length && _dlcCards[index]?.ActionButton != null
               && _dlcCards[index].ActionButton.interactable;

        /// <summary>팩 제목줄 오른쪽 상태 낱말(<see cref="DlcOpenWord"/> / <see cref="DlcLockedWord"/>).</summary>
        public string DlcPackStatusForTests(int pack)
            => pack >= 0 && pack < _dlcPackStatus.Length ? _dlcPackStatus[pack]?.text ?? string.Empty : string.Empty;

        public string DlcDetailNameForTests => _dlcDetailName != null ? _dlcDetailName.text : null;

        public string DlcDetailBodyForTests => _dlcDetailBody != null ? _dlcDetailBody.text : null;

        public Rect DlcCardRawScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _dlcCards.Length ? _dlcCards[index]?.Rect : null);

        public Rect DlcActionRawScreenRect(int index)
            => RawScreenRectOf(index >= 0 && index < _dlcCards.Length ? _dlcCards[index]?.ActionRect : null);

        /// <summary>진단/테스트 전용 — <b>클릭 핸들러가 부르는 바로 그 함수</b>를 부른다
        /// (배치모드 640×480에서는 격자 대부분이 마스크 밖이라 좌표 클릭이 거짓 빨강을 낸다).</summary>
        public void InvokeDlcEquipForTests(int index) => OnDlcEquipClicked(index);

        public void InvokeDlcSelectForTests(int index) => OnDlcCardClicked(index);
    }
}
