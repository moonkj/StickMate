using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ [DLC] 진열대(2026-09-08) — <b>팩이 보이는가 / 그리고 동전 상점에는 안 섞였는가</b>.
    ///
    /// ============================================================================
    /// ★★ 이 파일의 핵심은 <b>네거티브 컨트롤</b>이다
    /// ============================================================================
    /// <c>CharacterInfoWindow.Shop.cs</c>가 스스로 적어 둔 경고: 팩 아이템을 동전 격자에 흘리면
    /// <b>$4.99짜리가 동전 몇백에 팔린다</b>. 그래서 두 단언을 <b>같은 파일에서 함께</b> 세운다:
    /// <list type="number">
    ///   <item><b>동전 상점에 팩이 0개</b>(부재 단언 — 썩으면 조용히 초록이 되는 종류다).</item>
    ///   <item><b>[DLC]에 팩이 전부</b>(존재 단언 — 위 0개가 «선반이 둘 다 비어서»가 아님을 증명한다).</item>
    /// </list>
    /// CLAUDE.md가 못박은 그대로다 — 부재 단언은 <b>실재하는 대상이 있음을 같은 테스트에서 대조</b>해야
    /// 뜻을 갖는다. 여기서는 «팩 아이템이 카탈로그에 12종 실재한다»가 그 대조다.
    ///
    /// ============================================================================
    /// 숫자·문구를 베끼지 않는다
    /// ============================================================================
    /// <b>12</b>도 <b>3</b>도 <b>$4.99</b>도 이 파일에 상수로 적지 않는다.
    /// 개수는 <see cref="PackRegistry"/>·<see cref="ItemCatalog"/>에서 <b>세고</b>,
    /// 가격은 <see cref="CharacterInfoWindow.DlcTopTierPriceLabel"/>을 들고 <b>기획 문서를 읽어</b>
    /// 대조한다(생성기와 검사기가 같이 틀리는 것을 피한다 — 프로덕션이 값을 바꾸면 문서와 갈라져 빨개진다).
    ///
    /// <para><b>이 파일이 검증하지 못하는 것</b>: 실제로 그려진 카드·라벨·클릭이다. 그건 창을 띄워야
    /// 보이므로 실기 캡처와 PlayMode의 몫이고, 여기서 잠그는 것은 <b>목록과 판정</b>이다.</para>
    /// </summary>
    public sealed class DlcShowcaseTests
    {
        private const string LogPrefix = "[DLC-TEST]";

        private static readonly string PricingDocPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "docs", "strategy", "CHANNEL_PRICING_DECISIONS.md");

        [SetUp]
        public void SetUp()
        {
            // QA 해금 스위치가 켜져 있으면 「보유」 축의 대조가 공허해진다(다른 EditMode 픽스처와 같은 규약).
            EquipmentDebugUnlock.SetTestOverride(false);
        }

        [TearDown]
        public void TearDown()
        {
            EquipmentDebugUnlock.SetTestOverride(false);
        }

        /// <summary>카탈로그에 실재하는 <b>팩 코호트</b> 장비 전량(진열대를 거치지 않고 직접 센다 —
        /// 화면에게 기대값을 물으면 화면이 틀렸을 때 기대값도 함께 틀어진다).</summary>
        private static List<ItemCatalogEntry> PackItemsInCatalog()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry == null || entry.Category != ItemCategory.Equipment) continue;
                if (!entry.Slot.HasValue || entry.ItemIndex < 0) continue;
                if (!ItemCatalog.IsListed(entry)) continue;
                if (!PackRegistry.TryFindPackOfItem(entry, out _)) continue;   // 팩 소속인가는 레지스트리가 답한다
                list.Add(entry);
            }
            return list;
        }

        // ================================================================================
        // 1. 포지티브 — 실린 팩이 전부 진열된다
        // ================================================================================

        /// <summary>★ 오늘의 팩 3종 × 4아이템이 <b>한 장도 빠짐없이</b> 카드 자리를 갖는다.
        /// <para>개수를 적지 않고 <see cref="PackRegistry"/>가 실제로 실은 것과 대조한다 —
        /// 네 번째 팩이 오는 날 이 테스트는 <b>고칠 것이 없다</b>.</para></summary>
        [Test]
        public void 실린_팩과_그_아이템이_전부_진열대에_오른다()
        {
            // 양성 대조 — 팩이 0개면 아래 대조 전체가 공허하다.
            Assert.Greater(PackRegistry.Count, 0,
                $"{LogPrefix} 실린 팩이 0개입니다 — 이 상태의 «전부 진열됐다»는 아무것도 증명하지 않습니다.");

            Assert.AreEqual(PackRegistry.Count, CharacterInfoWindow.DlcPackCountForTests,
                $"{LogPrefix} 레지스트리에 실린 팩({PackRegistry.Count})과 진열된 팩" +
                $"({CharacterInfoWindow.DlcPackCountForTests})의 수가 다릅니다 — 팩 하나가 선반에서 조용히 빠졌습니다.");

            List<ItemCatalogEntry> packItems = PackItemsInCatalog();
            Assert.Greater(packItems.Count, 0, $"{LogPrefix} 카탈로그에 팩 아이템이 0종입니다(양성 대조 실패).");
            Assert.AreEqual(packItems.Count, CharacterInfoWindow.DlcEntryCountForTests,
                $"{LogPrefix} 카탈로그의 팩 아이템 {packItems.Count}종 중 진열된 것은 " +
                $"{CharacterInfoWindow.DlcEntryCountForTests}종입니다.");

            // 아이디 단위로도 맞춘다 — 개수만 맞고 다른 물건이 실릴 수 있다.
            var displayed = new List<string>();
            for (int i = 0; i < CharacterInfoWindow.DlcEntryCountForTests; i++)
            {
                int catalogIndex = CharacterInfoWindow.DlcCatalogIndexForTests(i);
                Assert.GreaterOrEqual(catalogIndex, 0, $"{LogPrefix} 진열 {i}번이 카탈로그 밖을 가리킵니다.");
                ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                Assert.IsNotNull(entry, $"{LogPrefix} 진열 {i}번의 카탈로그 항목이 null입니다.");
                displayed.Add(entry.Id);
            }
            for (int i = 0; i < packItems.Count; i++)
            {
                Assert.Contains(packItems[i].Id, displayed,
                    $"{LogPrefix} 팩 아이템 '{packItems[i].Id}'이 진열대에 없습니다.");
            }
        }

        /// <summary>팩마다 «몇 종을 선언했는가»와 «몇 장이 걸렸는가»가 같다.</summary>
        [Test]
        public void 팩마다_선언한_아이템_수와_진열된_카드_수가_같다()
        {
            int packs = CharacterInfoWindow.DlcPackCountForTests;
            Assert.Greater(packs, 0, $"{LogPrefix} 진열된 팩이 0개입니다.");

            for (int p = 0; p < packs; p++)
            {
                string packId = CharacterInfoWindow.DlcPackIdForTests(p);
                PackDescriptor pack = PackRegistry.Find(packId);
                Assert.IsNotNull(pack, $"{LogPrefix} 진열 {p}번 팩 '{packId}'을 레지스트리에서 못 찾습니다.");
                Assert.AreEqual(pack.DeclaredItemCount, CharacterInfoWindow.DlcPackItemCountForTests(p),
                    $"{LogPrefix} '{packId}'은 {pack.DeclaredItemCount}종을 선언했는데 진열은 " +
                    $"{CharacterInfoWindow.DlcPackItemCountForTests(p)}장입니다.");
            }
        }

        /// <summary>진열 순서가 <b>코호트 번호 오름차순</b>이다.
        /// <para><c>Resources.LoadAll</c> 순서는 보장되지 않는다 — 정렬을 빼면 같은 빌드에서
        /// 팩 순서가 실행마다 달라 보일 수 있고, 그건 «화면이 흔들린다»로 신고된다.</para></summary>
        [Test]
        public void 팩_진열_순서는_코호트_번호_오름차순이다()
        {
            int previous = int.MinValue;
            for (int p = 0; p < CharacterInfoWindow.DlcPackCountForTests; p++)
            {
                PackDescriptor pack = PackRegistry.Find(CharacterInfoWindow.DlcPackIdForTests(p));
                Assert.IsNotNull(pack);
                Assert.Greater(pack.CohortId, previous,
                    $"{LogPrefix} 진열 {p}번 '{pack.PackId}'(코호트 {pack.CohortId})이 앞 팩(코호트 {previous})보다 " +
                    "앞서지 않습니다 — 정렬이 빠졌습니다.");
                previous = pack.CohortId;
            }
        }

        // ================================================================================
        // 2. ★ 네거티브 컨트롤 — 동전 상점에는 팩이 한 장도 없다
        // ================================================================================

        /// <summary>
        /// ★★ <b>오늘 문서가 경고한 그 사고를 구조로 잠근다.</b>
        /// <c>CharacterInfoWindow.Shop.cs</c>: <i>"팩이 실리는 날 조용히 동전으로 팔리는 사고를 막는다."</i>
        /// <para>이 단언이 뜻을 가지려면 <b>팩이 실재해야</b> 한다 — 위 <see cref="PackItemsInCatalog"/>가
        /// 그 대조이고, 0종이면 이 테스트는 스스로 실패한다.</para>
        /// </summary>
        [Test]
        public void 동전_상점에는_팩_아이템이_한_건도_없다()
        {
            List<ItemCatalogEntry> packItems = PackItemsInCatalog();
            Assert.Greater(packItems.Count, 0,
                $"{LogPrefix} 카탈로그에 팩 아이템이 0종입니다 — 이 부재 단언은 공허합니다(양성 대조 실패). " +
                "팩이 실재하지 않으면 «동전 상점에 팩이 없다»는 문장은 아무것도 막지 못합니다.");

            var packIds = new HashSet<string>();
            for (int i = 0; i < packItems.Count; i++) packIds.Add(packItems[i].Id);

            int shopCount = CharacterInfoWindow.ShopEntryCountForTests;
            Assert.Greater(shopCount, 0, $"{LogPrefix} 동전 상점이 비어 있습니다 — 대조가 공허합니다.");

            for (int i = 0; i < shopCount; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(CharacterInfoWindow.ShopCatalogIndexForTests(i));
                Assert.IsNotNull(entry, $"{LogPrefix} 동전 상점 {i}번의 항목이 null입니다.");
                Assert.IsFalse(packIds.Contains(entry.Id),
                    $"{LogPrefix} ★ 동전 상점 {i}번에 팩 아이템 '{entry.Id}'이 실렸습니다. " +
                    "$4.99짜리 팩 아이템이 동전으로 팔리는 상태입니다 — IsShopMerchandise의 코호트 필터가 풀렸습니다.");
                Assert.AreEqual(ItemCatalog.BaseCohortId, EntryCohortViaRegistry(entry),
                    $"{LogPrefix} 동전 상점 {i}번 '{entry.Id}'이 팩 코호트에 속합니다.");
            }
        }

        /// <summary>두 선반이 <b>겹치지 않는다</b>. 위 두 테스트가 각각 통과해도 «한 아이템이 양쪽에
        /// 다 실린» 상태는 잡히지 않으므로 교집합을 직접 센다.</summary>
        [Test]
        public void 동전_상점과_DLC_진열대는_한_건도_겹치지_않는다()
        {
            var shop = new HashSet<int>();
            for (int i = 0; i < CharacterInfoWindow.ShopEntryCountForTests; i++)
            {
                shop.Add(CharacterInfoWindow.ShopCatalogIndexForTests(i));
            }
            Assert.Greater(shop.Count, 0, $"{LogPrefix} 동전 상점이 비어 있습니다 — 교집합 검사가 공허합니다.");

            int dlc = CharacterInfoWindow.DlcEntryCountForTests;
            Assert.Greater(dlc, 0, $"{LogPrefix} DLC 진열대가 비어 있습니다 — 교집합 검사가 공허합니다.");

            for (int i = 0; i < dlc; i++)
            {
                int index = CharacterInfoWindow.DlcCatalogIndexForTests(i);
                Assert.IsFalse(shop.Contains(index),
                    $"{LogPrefix} 카탈로그 {index}번이 두 선반에 동시에 실렸습니다 " +
                    $"('{ItemCatalog.At(index)?.Id}').");
            }
        }

        /// <summary>팩 아이템은 <b>레지스트리가 아는 팩</b>에만 속한다(고아 코호트가 진열되지 않는다).
        /// <para>고아는 «살 방법이 없는 상태»라 선반에 올리면 안 된다 —
        /// <c>PackRegistry.AuditOrphanCohorts</c>가 그것을 결함으로 부르는 이유와 같다.</para></summary>
        [Test]
        public void 진열된_아이템은_전부_레지스트리가_아는_팩_소속이다()
        {
            for (int i = 0; i < CharacterInfoWindow.DlcEntryCountForTests; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(CharacterInfoWindow.DlcCatalogIndexForTests(i));
                Assert.IsNotNull(entry);
                Assert.IsTrue(PackRegistry.TryFindPackOfItem(entry, out PackDescriptor pack),
                    $"{LogPrefix} 진열 {i}번 '{entry.Id}'의 팩을 레지스트리가 모릅니다(고아 코호트).");
                Assert.AreNotEqual(ItemCatalog.BaseCohortId, pack.CohortId,
                    $"{LogPrefix} 진열 {i}번 '{entry.Id}'이 기본 코호트에 있습니다.");
            }
        }

        // ================================================================================
        // 3. 가격 — 화면 문구가 기획 문서와 같은가
        // ================================================================================

        /// <summary>
        /// ★ 화면이 적는 가격이 <c>docs/strategy/CHANNEL_PRICING_DECISIONS.md</c>의 확정값과 같다.
        ///
        /// ============================================================================
        /// ★★ 이 테스트가 <b>프로덕션 대신</b> 지고 있는 짐
        /// ============================================================================
        /// 프로덕션은 «어느 팩이 어느 티어인가»를 <b>모른다</b> — 알려면 팩 아이디를 코드에 적어야 하고
        /// 그것은 <see cref="PackManifestCorridorTests"/>가 금지한다(팩은 에셋으로 늘어난다).
        /// 그래서 <see cref="CharacterInfoWindow.DlcPriceLabel"/>은 실린 팩 전부에 상위 티어 값을 붙이고,
        /// <b>그 전제가 참인지를 여기서 문서로 확인한다.</b> 하위 티어·무료 팩이 폴더에 들어오는 순간
        /// 이 테스트가 그 라운드에 빨개진다 — 사용자가 틀린 가격을 보기 전에.
        ///
        /// <para><b>숫자도 아이디도 이 파일에 적지 않는다</b>: 가격은 프로덕션 상수에서, 팩 아이디는
        /// <see cref="PackRegistry"/>에서 온다. 이 테스트가 하는 일은 그 둘이 <b>문서의 같은 줄</b>에
        /// 함께 적혀 있는지를 보는 것뿐이다(생성기와 검사기가 같이 틀리는 것을 피한다).</para>
        /// </summary>
        [Test]
        public void 표시_가격이_기획_문서의_확정값과_같다()
        {
            Assert.IsTrue(File.Exists(PricingDocPath),
                $"{LogPrefix} 가격 정본 문서를 찾지 못했습니다: {PricingDocPath}");
            string[] lines = File.ReadAllLines(PricingDocPath);
            Assert.Greater(lines.Length, 0, $"{LogPrefix} 가격 정본 문서가 비어 있습니다.");

            string price = CharacterInfoWindow.DlcTopTierPriceLabel;

            // 음성 대조 — 이 판독기가 «없는 것»에 확실히 아니라고 답하는가. 아니면 아래 존재 단언은
            // 아무 문자열에나 통과하는 상태와 구별되지 않는다.
            Assert.IsFalse(ContainsBoth(lines, "pack.nosuchpackxyz", price),
                $"{LogPrefix} 음성 대조 실패 — 존재하지 않는 팩을 문서에서 찾았다고 합니다.");
            Assert.IsFalse(ContainsBoth(lines, "pack.", "$4.987654"),
                $"{LogPrefix} 음성 대조 실패 — 존재하지 않는 가격을 문서에서 찾았다고 합니다.");

            int packs = CharacterInfoWindow.DlcPackCountForTests;
            Assert.Greater(packs, 0, $"{LogPrefix} 진열된 팩이 0개입니다 — 아래 대조가 공허합니다.");

            for (int p = 0; p < packs; p++)
            {
                string packId = CharacterInfoWindow.DlcPackIdForTests(p);
                string label = CharacterInfoWindow.DlcPackPriceLabelForTests(p);

                Assert.AreEqual(price, label,
                    $"{LogPrefix} '{packId}'의 표시 가격이 상위 티어 상수와 다릅니다.");

                Assert.IsTrue(ContainsBoth(lines, packId, price),
                    $"{LogPrefix} ★ 화면은 '{packId}'을 {price}으로 적는데, 기획 문서" +
                    $"({Path.GetFileName(PricingDocPath)})에는 그 아이디와 그 값이 <b>같은 줄</b>에 " +
                    "함께 적힌 곳이 없습니다.\n" +
                    "  (가) 새 팩이 실렸는데 가격 확정이 문서에 안 들어왔거나,\n" +
                    "  (나) 그 팩이 하위 티어/무료인데 화면이 상위 티어 값을 적고 있습니다.\n" +
                    "후자면 사용자에게 <b>틀린 가격</b>이 나갑니다 — 문서를 먼저 고치고, " +
                    "티어가 갈리기 시작했다면 DlcPriceLabel을 티어별로 나눠야 합니다(팩 아이디를 " +
                    "코드에 적는 방법은 PackManifestCorridorTests가 막으므로, 값은 에셋이나 스토어에서 와야 합니다).");
            }
        }

        /// <summary>문서에 <paramref name="a"/>와 <paramref name="b"/>가 <b>같은 줄</b>에 있는가.
        /// 문서 전체에서 각각 찾으면 «다른 팩의 가격 줄»과 «이 팩의 이름 줄»이 우연히 통과한다.</summary>
        private static bool ContainsBoth(string[] lines, string a, string b)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(a) && lines[i].Contains(b)) return true;
            }
            return false;
        }

        // ================================================================================
        // 4. 정직성 — 이 표면은 재화를 건드리지 않는다
        // ================================================================================

        /// <summary>
        /// ★ [DLC] 본문이 <b>재화 모델을 한 번도 부르지 않는다</b>. 결제 채널이 없는데 동전이 나가는
        /// 흐름을 만들면 화면이 거짓말을 한다(클래스 문서 「주장하지 않는 것」 1번).
        /// <para>부재 단언이므로 <b>양성 대조</b>를 같이 세운다 — 같은 판독기로 [상점] 본문을 읽으면
        /// 그쪽에는 반드시 있어야 한다. 없으면 판독기가 눈이 먼 것이다.</para>
        /// </summary>
        [Test]
        public void DLC_본문은_재화_모델을_부르지_않는다()
        {
            string interaction = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");
            string dlcPath = Path.Combine(interaction, "CharacterInfoWindow.Dlc.cs");
            string shopPath = Path.Combine(interaction, "CharacterInfoWindow.Shop.cs");
            Assert.IsTrue(File.Exists(dlcPath), $"{LogPrefix} 소스를 찾지 못했습니다: {dlcPath}");
            Assert.IsTrue(File.Exists(shopPath), $"{LogPrefix} 소스를 찾지 못했습니다: {shopPath}");

            string needle = nameof(CurrencyModel) + ".";
            string dlc = EntitlementAuditSource.StripComments(File.ReadAllText(dlcPath));
            string shop = EntitlementAuditSource.StripComments(File.ReadAllText(shopPath));

            // 양성 대조 — 판독기가 실제로 이 호출을 볼 수 있는가.
            StringAssert.Contains(needle, shop,
                $"{LogPrefix} 양성 대조 실패 — [상점] 본문에서 '{needle}'을 못 찾습니다. " +
                "판독기가 눈이 멀었거나 상점의 구매 배선이 사라졌습니다(둘 다 이 감사를 공허하게 만듭니다).");

            StringAssert.DoesNotContain(needle, dlc,
                $"{LogPrefix} ★ [DLC] 본문이 '{needle}'을 부릅니다 — 결제 채널이 없는데 재화를 " +
                "건드리는 흐름이 생겼습니다.");
        }

        /// <summary>
        /// ★ [DLC] 본문이 <c>PackEntitlements.StateOf</c>를 <b>직접</b> 부르지 않는다(계약서 I-5).
        /// 판정의 유일한 창구는 <see cref="CostumeEntitlement"/>다.
        /// <para>이 단언은 <c>CostumeEntitlementSingleGateTests</c>가 전량 스캔으로 이미 세우고 있다 —
        /// 여기서는 <b>새로 생긴 파일이 그 스캔에 실제로 들어갔는지</b>를 이름으로 못박는다
        /// (그쪽은 «어느 파일도 안 부른다»를 재고, 여기는 «이 파일이 그 대상이다»를 잰다).</para>
        /// </summary>
        [Test]
        public void DLC_본문은_엔타이틀먼트_단일_창구만_쓴다()
        {
            string dlcPath = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction",
                "CharacterInfoWindow.Dlc.cs");
            string dlc = EntitlementAuditSource.StripComments(File.ReadAllText(dlcPath));

            StringAssert.DoesNotContain(nameof(PackEntitlements) + "." + nameof(PackEntitlements.StateOf), dlc,
                $"{LogPrefix} [DLC] 본문이 C층 판정을 직접 합니다 — 판정이 두 곳이 되면 「반만 열린」 상태가 됩니다.");

            // 존재 대조 — 그럼 이 파일은 무엇으로 묻는가. 단일 창구를 실제로 부르고 있어야 한다.
            StringAssert.Contains(nameof(CostumeEntitlement) + "." + nameof(CostumeEntitlement.IsOpen), dlc,
                $"{LogPrefix} [DLC] 본문이 개방 판정을 아예 하지 않습니다 — 위 부재 단언이 " +
                "«단일 창구를 쓴다»가 아니라 «아무것도 안 묻는다»를 통과시키고 있었습니다.");

            // 그리고 이 파일이 그 전량 스캔의 대상 목록에 실제로 들어 있는가(스캐너가 이 파일을 본다).
            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            bool seen = false;
            for (int i = 0; i < production.Length; i++)
            {
                if (!string.Equals(Path.GetFullPath(production[i]), Path.GetFullPath(dlcPath),
                        System.StringComparison.Ordinal))
                {
                    continue;
                }
                seen = true;
                break;
            }
            Assert.IsTrue(seen,
                $"{LogPrefix} 새 파일이 프로덕션 소스 스캔 목록에 없습니다 — 엔타이틀먼트 전량 감사가 이 파일을 못 봅니다.");
        }

        /// <summary>오늘 세 팩은 <b>채널 항목이 0개</b>라 «무료는 안 묻는 것»(규칙 C-1)으로 열려 있다.
        /// <para>이 사실이 화면 낱말(<see cref="CharacterInfoWindow.DlcOpenWord"/>)의 근거이므로,
        /// 사실이 바뀌면 문구도 함께 다시 봐야 한다는 것을 여기서 못박는다.</para></summary>
        [Test]
        public void 오늘_팩은_채널_항목이_없어_구조적으로_열려_있다()
        {
            Assert.Greater(PackRegistry.Count, 0, $"{LogPrefix} 실린 팩이 0개입니다.");

            for (int i = 0; i < PackRegistry.Count; i++)
            {
                PackDescriptor pack = PackRegistry.Packs[i];
                Assert.AreEqual(0, pack.Entitlements.Count,
                    $"{LogPrefix} '{pack.PackId}'에 채널 항목이 {pack.Entitlements.Count}개 생겼습니다 — " +
                    "결제 창구가 붙었다면 화면 낱말(미리보기/잠김)과 상세 줄 문장을 다시 봐야 합니다.");
                Assert.IsTrue(CostumeEntitlement.IsOpen(pack),
                    $"{LogPrefix} '{pack.PackId}'이 닫혀 있습니다 — 채널이 0개인데 닫혔다면 규칙 C-1이 깨진 것입니다.");
            }
        }

        /// <summary>팩 아이템 12종은 오늘 <b>전부 입을 수 있다</b>(<c>requiredLevel: 1</c>).
        /// <para>진열대의 착용 칩이 실제 기능이라는 주장의 근거다 — 이 값이 바뀌면 카드가
        /// 「잠김」으로 뜨므로 그 라운드에 화면을 다시 봐야 한다.</para></summary>
        [Test]
        public void 진열된_팩_아이템은_레벨1에서_전부_보유_상태다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                CharacterProgressionModel.ResetForTesting();   // Lv.1
                int count = CharacterInfoWindow.DlcEntryCountForTests;
                Assert.Greater(count, 0, $"{LogPrefix} 진열대가 비어 있습니다.");

                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.At(CharacterInfoWindow.DlcCatalogIndexForTests(i));
                    Assert.IsNotNull(entry);
                    Assert.IsTrue(entry.IsOwned(config),
                        $"{LogPrefix} '{entry.Id}'이 Lv.{CharacterProgressionModel.Level}에서 잠겨 있습니다 " +
                        $"(requiredLevel={entry.RequiredLevel}). 진열대의 착용 칩이 「잠김」으로 뜹니다.");
                }
            }
            finally
            {
                CharacterProgressionModel.ResetForTesting();
                Object.DestroyImmediate(config);
            }
        }

        /// <summary>이 항목의 코호트를 <b>레지스트리를 거쳐</b> 얻는다 —
        /// <c>ItemCatalogEntry.CohortId</c>는 <c>internal</c>이라 읽을 수 있지만, 그러면 이 테스트가
        /// 프로덕션과 <b>같은 필드</b>를 보게 된다. 다른 길로 재는 것이 이 대조의 요점이다.</summary>
        private static int EntryCohortViaRegistry(ItemCatalogEntry entry)
            => PackRegistry.TryFindPackOfItem(entry, out PackDescriptor pack)
                ? pack.CohortId
                : ItemCatalog.BaseCohortId;
    }
}
