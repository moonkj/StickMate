using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ [상점] 탭 화면 — 2026-09-06 배선. <b>구매 플로우가 화면에서 실제로 도는가</b>.
    ///
    /// ============================================================================
    /// 여기서 잠그는 것
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>잔액이 모자라면 칩이 죽는다</b> — 라벨이 바뀌고 <c>interactable</c>이 false이며,
    ///         그 상태로 구매 경로를 불러도 <b>잔액이 1도 줄지 않는다</b>(가드가 화면이 아니라
    ///         핸들러 안에 있다는 증거 — 클릭 경로가 둘이라 그래야 한다).</item>
    ///   <item><b>잔액이 오르면 저절로 살아난다</b> — 0.25초 주기 갱신이 잔액 변화를 본다.</item>
    ///   <item><b>두 번 눌러야 산다</b> — 첫 클릭은 라벨만 바꾸고 잔액을 건드리지 않는다(설계 §3-5).</item>
    ///   <item><b>사면 그 자리에서 잔액이 정확히 가격만큼 줄고 「보유 중」이 되며 즉시 저장된다</b> —
    ///         ★ 「저장됐다」는 <b>저장 호출의 결과</b>(<see cref="CharacterInfoWindow.ShopLastSaveSucceededForTests"/>)와
    ///         <b>디스크 실물 JSON</b>(<see cref="CharacterSaveStore.FilePath"/>) 두 가지로 잰다.</item>
    ///   <item><b>중복 구매가 차단된다</b> — 이미 산 카드를 또 눌러도 잔액이 그대로다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★ 「즉시 저장」을 <c>CurrencyModel.IsDirty</c>로 재지 않는다 (2026-09-06 거짓 빨강 수정)
    /// ============================================================================
    /// 원래 이 파일은 «즉시 저장됐다»를 <c>Assert.IsFalse(CurrencyModel.IsDirty)</c>로 쟀다.
    /// 그런데 <c>Interaction/CharacterProgressionDirector.AccrueIdleIncome()</c>가 <b>매 프레임</b>
    /// 유휴 수급을 돌려 그 플래그를 <b>다시 세운다</b>(유휴 수급이 배선된 이상 정상 동작이다).
    /// ⇒ 그 단언은 <b>구조적으로 항상 실패</b>한다. 그리고 그 빨강은 «껐다 켜면 산 것이 사라진다»는
    /// 무서운 문장을 달고 있어서, <b>실재하지 않는 데이터 유실</b>을 매 실행 신고했다
    /// (debugger 실측: 격리 저장 파일에 구매가 <b>정상적으로</b> 들어 있었다).
    ///
    /// <para>교훈은 «플래그를 바꿔 달자»가 아니다 — <b>관측 가능한 것을 재라</b>는 쪽이다.
    /// 저장이 일어났다는 사실의 종착지는 <b>디스크</b>이므로, 이제 그 파일을 직접 읽어
    /// <c>purchasedItemIds</c>에 방금 산 id가 있고 <c>coinBalance</c>가 <b>구매전잔액 − 가격</b>인지
    /// 확인한다. 그것이 이 테스트가 원래 하려던 주장(«껐다 켜도 남는다»)을 실제로 재는 유일한 방법이다.</para>
    ///
    /// ============================================================================
    /// ★ 좌표 클릭을 쓰지 않는다 — 숨기지 않는다
    /// ============================================================================
    /// 배치모드 PlayMode 화면은 <b>640×480</b>이라 1042×802 창이 608×448로 줄고, 상점 격자의 대부분이
    /// <c>Body</c> 마스크 밖으로 잘린다. 그 자리는 이 창의 규칙("보이지 않는 것은 눌리지 않는다")에 따라
    /// <b>정당하게</b> 안 눌린다 — 좌표로 검증하면 <b>거짓 빨강</b>이 난다(<c>InventoryPagerRailTests</c>가
    /// 먼저 겪은 그 함정). 그래서 <b>클릭 핸들러가 부르는 바로 그 함수</b>를 부르고, 가드는
    /// <see cref="CharacterInfoWindow.ShopActionInteractableForTests"/>로 따로 본다.
    /// <b>좌표 클릭은 실기 캡처 몫이다.</b>
    ///
    /// ============================================================================
    /// 문구와 숫자를 베끼지 않는다
    /// ============================================================================
    /// 낱말은 프로덕션 상수(<see cref="CharacterInfoWindow.ShopOwnedWord"/> 등)를 <b>참조</b>하고,
    /// 가격은 <see cref="CurrencyRules.PriceCoins"/>로 <b>따로 계산</b>한다(창에게 묻지 않는다 —
    /// 창이 틀리면 기대값도 함께 틀어진다).
    ///
    /// <para>★ 이 파일은 <b>실제 저장 파일에 씁니다</b>(구매가 즉시 저장이므로). 착용 토글을 누르는
    /// 기존 PlayMode 테스트들과 같은 성질이고, 정리 단계에서 모델을 되돌립니다.</para>
    /// </summary>
    public sealed class ShopTabSurfaceTests
    {
        private const string LogPrefix = "[상점화면-TEST]";

        /// <summary>[상점] 탭 인덱스. <c>CharacterInfoWindow.Tab</c>이 private이라 창구가 여는 순서를 쓴다.</summary>
        private const int TabShop = 3;

        /// <summary>에디터에서는 QA 해금 스위치가 켜져 있어 <b>잠긴 아이템이 하나도 없다</b> = 살 것이 없다.
        /// 그래서 테스트 동안만 강제로 끈다(리플렉션 — 이 저장소가 이미 쓰는 관례).</summary>
        private static readonly MethodInfo SetUnlockOverride = typeof(EquipmentDebugUnlock).GetMethod(
            "SetTestOverride", BindingFlags.Static | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            SetUnlockOverride?.Invoke(null, new object[] { null });
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenShopTab()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 파일도 함께 고쳐야 합니다(스위치가 켜진 채면 살 것이 하나도 없어 전부 공허해집니다).");

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();   // Lv.1 — 잠긴 상품이 있어야 살 것이 있다.
            CurrencyModel.ResetForTesting();               // 잔액 0에서 시작한다.

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;

            Rect tab = _window.TabScreenRect(TabShop);
            Assert.Greater(tab.width, 0f, $"{LogPrefix} [상점] 탭의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(tab.center);
            yield return null;

            Assert.Greater(_window.ShopCardCountForTests, 0,
                $"{LogPrefix} 상점 카드가 한 장도 구워지지 않았습니다.");
            Assert.AreEqual(CharacterInfoWindow.ShopEntryCountForTests, _window.ShopCardCountForTests,
                $"{LogPrefix} 카드 수와 상품 수가 다릅니다 — 목록과 화면이 갈라졌습니다.");
        }

        /// <summary>지금 <b>아직 안 가진</b> 첫 상품의 자리. 없으면 테스트가 공허하므로 그 자리에서 실패한다.</summary>
        private int FirstUnownedIndex()
        {
            for (int i = 0; i < _window.ShopCardCountForTests; i++)
            {
                if (_window.ShopActionLabelForTests(i) != CharacterInfoWindow.ShopOwnedWord) return i;
            }
            Assert.Fail($"{LogPrefix} 모든 상품이 「{CharacterInfoWindow.ShopOwnedWord}」입니다 — " +
                        "QA 해금 스위치가 안 꺼졌거나 레벨이 안 내려갔습니다. 이 테스트는 아무것도 못 잽니다.");
            return -1;
        }

        /// <summary>안 가진 것 중 <b>가장 비싼</b> 자리. 「잔액 부족」을 재려면 이쪽이라야 안전하다 —
        /// 다른 라운드가 첫 실행 시드나 자동 수급을 배선하면 싼 카드는 <b>살 수 있는 상태</b>가 되어
        /// 이 검사가 이유 없이 빨개진다(거짓 빨강).</summary>
        private int MostExpensiveUnownedIndex()
        {
            int best = -1, bestPrice = -1;
            for (int i = 0; i < _window.ShopCardCountForTests; i++)
            {
                if (_window.ShopActionLabelForTests(i) == CharacterInfoWindow.ShopOwnedWord) continue;
                int price = PriceOf(i);
                if (price <= bestPrice) continue;
                best = i;
                bestPrice = price;
            }
            Assert.GreaterOrEqual(best, 0,
                $"{LogPrefix} 안 가진 상품이 없습니다 — QA 해금 스위치가 안 꺼졌거나 레벨이 안 내려갔습니다.");
            return best;
        }

        /// <summary>그 자리 상품의 가격 — <b>창에게 묻지 않고</b> 재화 규칙에서 직접 만든다.</summary>
        private static int PriceOf(int index)
        {
            ItemCatalogEntry entry = ItemCatalog.At(CharacterInfoWindow.ShopCatalogIndexForTests(index));
            Assert.IsNotNull(entry, $"{LogPrefix} 상점 {index}번의 카탈로그 항목이 없습니다.");
            Assert.IsTrue(entry.Slot.HasValue, $"{LogPrefix} [{entry.Id}]에 슬롯이 없습니다.");
            return CurrencyRules.PriceCoins(ItemCatalog.Rarity(entry.Slot.Value, entry.ItemIndex));
        }

        private static string IdOf(int index)
            => ItemCatalog.At(CharacterInfoWindow.ShopCatalogIndexForTests(index)).Id;

        /// <summary>집중 세션 완주 경로로 지갑을 채운다(공개 API만 쓴다 — 잔액 세터는 internal이다).
        /// <b>벽시계 기준</b>으로 다음 주기 갱신까지 기다린다(프레임 수로 세지 않는다 — CLAUDE.md).</summary>
        private IEnumerator FundAtLeast(int coins)
        {
            while (CurrencyModel.CoinBalance < coins)
            {
                int missing = coins - CurrencyModel.CoinBalance;
                int paid = CurrencyModel.PayFocusCompletionCoins(
                    (missing + CurrencyRules.FocusCoinsPerMinute) * 60.0 / CurrencyRules.FocusCoinsPerMinute);
                Assert.Greater(paid, 0, $"{LogPrefix} 집중 지급이 0입니다 — 지갑을 채울 수 없습니다.");
            }
            // 0.25초 주기 갱신이 잔액 변화를 보고 다시 칠할 시간을 준다(주기의 2배 + 여유).
            yield return new WaitForSecondsRealtime(0.75f);
        }

        // ============================================================================
        // ★ 디스크 실물 대조 — 「껐다 켜면 사라지는가」를 실제로 재는 유일한 자
        // ============================================================================

        /// <summary>
        /// 저장 파일에서 <b>이 테스트가 보는 두 칸만</b> 꺼내는 최소 스키마.
        ///
        /// <para>프로덕션의 <c>SaveData</c>는 <c>private</c>이라 참조할 수 없어 <b>디스크 계약</b>
        /// (키 이름)을 여기 적는다. 그래도 이 니들은 <b>썩으면 조용히 초록이 되지 않는다</b>:
        /// 키 이름이 바뀌면 <see cref="JsonUtility"/>가 <c>0</c>/<c>null</c>을 채우는데, 기대값이
        /// «구매전잔액 − 가격»(0이 될 수 없다 — 아래 <c>FundAtLeast</c>가 항상 가격보다 더 채운다)과
        /// «방금 산 id»라 <b>반드시 빨개진다</b>. 존재 단언이지 부재 단언이 아니다(CLAUDE.md).</para>
        /// </summary>
        [System.Serializable]
        private sealed class SaveFileProbe
        {
            public int version;
            public int coinBalance;
            public string[] purchasedItemIds;
        }

        /// <summary>저장 파일을 <b>직접 읽어</b> 방금 산 것이 실제로 적혔는지 본다.
        /// (창이나 모델에게 묻지 않는다 — 그것들이 틀리면 기대값도 함께 틀어진다.)</summary>
        private static void AssertSaveFileHasPurchase(string id, int expectedBalance)
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리돼 있지 않습니다 — 개발자의 실제 파일을 읽게 되므로 " +
                "여기서 멈춥니다(그 파일을 기준으로 잰 값은 아무 뜻이 없습니다).");

            string path = CharacterSaveStore.FilePath;
            Assert.IsTrue(File.Exists(path),
                $"{LogPrefix} 구매가 즉시 저장이라면 저장 파일이 있어야 합니다: {path}. " +
                "파일 자체가 없다는 것은 <b>디스크에 한 번도 안 썼다</b>는 뜻입니다 — " +
                "이 경우가 바로 «껐다 켜면 산 것이 사라진다»입니다.");

            SaveFileProbe probe = JsonUtility.FromJson<SaveFileProbe>(File.ReadAllText(path));
            Assert.IsNotNull(probe, $"{LogPrefix} 저장 파일이 JSON으로 읽히지 않습니다: {path}");
            Assert.Greater(probe.version, 0,
                $"{LogPrefix} 저장 파일에서 스키마 버전을 못 읽었습니다({path}) — 파일이 비었거나 " +
                "키 이름이 바뀌었습니다. 아래 두 단언은 지금 아무것도 재지 못합니다.");

            Assert.IsNotNull(probe.purchasedItemIds,
                $"{LogPrefix} 저장 파일에 구매 이력(purchasedItemIds) 자체가 없습니다 — " +
                "필드 이름이 바뀌었거나 구매가 세이브에 실리지 않습니다.");
            CollectionAssert.Contains(probe.purchasedItemIds, id,
                $"{LogPrefix} ★ 저장 파일의 구매 이력에 방금 산 [{id}]가 없습니다" +
                $"(파일에 적힌 것: {string.Join(", ", probe.purchasedItemIds)}). " +
                "지금 껐다 켜면 이 구매는 사라집니다.");
            Assert.AreEqual(expectedBalance, probe.coinBalance,
                $"{LogPrefix} ★ 저장 파일의 잔액이 {probe.coinBalance}입니다(기대 {expectedBalance} = " +
                "구매전잔액 − 가격). 구매 전 값이면 차감이 저장되지 않은 것이고, 그 상태로 다시 켜면 " +
                "<b>돈은 그대로인데 물건도 있는</b> 상태가 됩니다.");

            Debug.Log($"{LogPrefix} 디스크 대조 통과 — {path} :: coinBalance={probe.coinBalance}, " +
                      $"purchasedItemIds=[{string.Join(", ", probe.purchasedItemIds)}] (v{probe.version}).");
        }

        // ============================================================================
        // ① 잔액이 모자라면 칩이 죽고, 그 상태로 불러도 돈이 나가지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator InsufficientCoinsKillTheChipAndBlockThePurchase()
        {
            yield return OpenShopTab();

            int index = MostExpensiveUnownedIndex();
            int price = PriceOf(index);
            int before = CurrencyModel.CoinBalance;
            Assert.Greater(price, 0, $"{LogPrefix} 가격이 0입니다 — 공짜 상품이 생겼습니다.");
            Assert.Less(before, price,
                $"{LogPrefix} 잔액 {before}이 최고가 상품 {price} 이상입니다 — 「모자란 상태」를 만들지 " +
                "못했습니다(어딘가에서 동전을 지급하고 있습니다). 이 검사는 아무것도 못 잽니다.");

            string label = _window.ShopActionLabelForTests(index);
            Assert.AreEqual(CharacterInfoWindow.ShopShortOfCoinsWord, label,
                $"{LogPrefix} 잔액 {before} / 가격 {price}인데 칩이 「{label}」입니다 — 살 수 있는 카드와 구별되지 않습니다.");
            Assert.IsFalse(_window.ShopActionInteractableForTests(index),
                $"{LogPrefix} 살 수 없는 칩이 활성입니다(눌러도 아무 일도 안 나는 버튼).");

            string detail = _window.ShopDetailBodyForTests;

            // 화면이 죽었어도 <b>핸들러</b>가 막아야 한다 — 클릭 경로가 둘이라 화면 가드만으로는 샌다.
            _window.ShopBuyForTests(index);
            yield return null;

            Assert.AreEqual(before, CurrencyModel.CoinBalance,
                $"{LogPrefix} 잔액이 모자란데 구매가 통과해 잔액이 {before} → {CurrencyModel.CoinBalance}로 움직였습니다.");
            Assert.IsFalse(CurrencyModel.IsPurchasedItem(IdOf(index)),
                $"{LogPrefix} 잔액이 모자란데 상품이 구매 이력에 남았습니다 — 공짜로 열렸습니다.");
            Assert.AreEqual(-1, _window.ShopConfirmIndexForTests,
                $"{LogPrefix} 살 수 없는 카드가 확인 단계에 들어갔습니다 — 물어볼 이유가 없습니다.");

            // 상세 줄이 실제로 무언가를 말하는가(문장을 베끼지 않고 「비어 있지 않다」만 본다).
            Assert.IsFalse(string.IsNullOrWhiteSpace(detail),
                $"{LogPrefix} 못 사는 이유가 상세 줄에 한 글자도 없습니다.");

            Debug.Log($"{LogPrefix} 잔액 {before} / 가격 {price} — 칩 「{label}」, 비활성, 구매 차단됨. 상세: \"{detail}\"");
        }

        // ============================================================================
        // ② 잔액이 차면 살아나고, 두 번 눌러야 사고, 사면 즉시 저장된다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TwoClicksBuyAndTheBalanceDropsByExactlyThePrice()
        {
            yield return OpenShopTab();

            int index = FirstUnownedIndex();
            int price = PriceOf(index);
            string id = IdOf(index);
            string deadLabel = _window.ShopActionLabelForTests(index);

            yield return FundAtLeast(price);
            int before = CurrencyModel.CoinBalance;
            Assert.GreaterOrEqual(before, price, $"{LogPrefix} 지갑을 채우지 못했습니다.");

            // (2) 잔액이 오르면 갱신 주기가 저절로 살려 낸다.
            string liveLabel = _window.ShopActionLabelForTests(index);
            Assert.AreNotEqual(deadLabel, liveLabel,
                $"{LogPrefix} 잔액이 {before}가 됐는데 칩이 여전히 「{deadLabel}」입니다 — " +
                "0.25초 주기 갱신이 잔액 변화를 보지 못합니다.");
            Assert.IsTrue(_window.ShopActionInteractableForTests(index),
                $"{LogPrefix} 살 수 있는데 칩이 죽어 있습니다.");
            StringAssert.Contains(price.ToString("N0"), liveLabel,
                $"{LogPrefix} 칩에 가격이 안 보입니다(라벨 「{liveLabel}」, 가격 {price:N0}).");

            // ── 즉시 저장의 <b>음성 대조</b>. 아직 아무것도 안 샀으니 «구매 저장»은 시도조차 없어야
            //    한다. null(시도 없음)과 false(시도했는데 실패)를 구분해 두는 것이 요점이다 —
            //    아래 양성 단언이 «원래부터 true였다»로 공허해지지 않게 한다.
            Assert.IsNull(_window.ShopLastSaveSucceededForTests,
                $"{LogPrefix} 아직 아무것도 사지 않았는데 구매 저장 기록이 " +
                $"{_window.ShopLastSaveSucceededForTests}로 남아 있습니다 — 아래 «샀더니 저장됐다»가 " +
                "공허해집니다(앞 테스트의 상태가 새고 있거나, 구매가 아닌 경로가 이 값을 씁니다).");

            // (3) 첫 클릭은 묻기만 한다.
            _window.ShopBuyForTests(index);
            yield return null;

            Assert.AreEqual(index, _window.ShopConfirmIndexForTests,
                $"{LogPrefix} 첫 클릭이 확인 단계로 들어가지 않았습니다.");
            Assert.AreEqual(CharacterInfoWindow.ShopConfirmWord, _window.ShopActionLabelForTests(index),
                $"{LogPrefix} 확인 단계인데 칩이 그대로입니다 — 색만 바뀌면 글자를 읽는 사람에게는 아무 일도 안 일어났습니다.");
            Assert.AreEqual(before, CurrencyModel.CoinBalance,
                $"{LogPrefix} 한 번 눌렀을 뿐인데 돈이 나갔습니다(되돌릴 수 없는 행동에 확인 단계가 없습니다).");
            Assert.IsFalse(CurrencyModel.IsPurchasedItem(id), $"{LogPrefix} 확인 전에 이미 샀습니다.");

            // (4) 둘째 클릭이 확정한다.
            _window.ShopBuyForTests(index);
            yield return null;

            Assert.IsTrue(CurrencyModel.IsPurchasedItem(id), $"{LogPrefix} 두 번 눌렀는데 사지 못했습니다.");
            Assert.AreEqual(before - price, CurrencyModel.CoinBalance,
                $"{LogPrefix} 잔액이 정확히 가격({price})만큼 줄지 않았습니다(전 {before} → 후 {CurrencyModel.CoinBalance}).");
            Assert.AreEqual(CharacterInfoWindow.ShopOwnedWord, _window.ShopActionLabelForTests(index),
                $"{LogPrefix} 샀는데 칩이 아직 팔고 있습니다.");
            Assert.IsFalse(_window.ShopActionInteractableForTests(index),
                $"{LogPrefix} 이미 산 카드의 칩이 살아 있습니다 — 중복 구매의 문이 열려 있습니다.");
            Assert.AreEqual(-1, _window.ShopConfirmIndexForTests,
                $"{LogPrefix} 구매 뒤에도 확인 단계가 남아 있습니다.");

            // ── 즉시 저장 ①: 구매 핸들러가 <b>저장을 부르고 그 호출이 성공했는가</b>.
            Assert.AreEqual(true, _window.ShopLastSaveSucceededForTests,
                $"{LogPrefix} 구매가 즉시 저장되지 않았습니다(저장 결과 " +
                $"{(_window.ShopLastSaveSucceededForTests.HasValue ? _window.ShopLastSaveSucceededForTests.ToString() : "시도 없음")}). " +
                "null이면 구매 경로가 저장을 <b>아예 안 불렀고</b>, false면 불렀는데 디스크에 못 썼습니다" +
                $"(저장보류={CharacterSaveStore.SaveSuspended}).");

            // ── 즉시 저장 ②: <b>디스크 실물</b>이 그렇게 말하는가. ①만으로는 «true를 돌려주는 함수»를
            //    믿는 것이고, 이 테스트가 원래 주장하려던 것은 «껐다 켜도 남는다»이다.
            AssertSaveFileHasPurchase(id, before - price);

            // (5) 한 번 더 눌러도 아무 일도 없다.
            int settled = CurrencyModel.CoinBalance;
            _window.ShopBuyForTests(index);
            yield return null;
            Assert.AreEqual(settled, CurrencyModel.CoinBalance,
                $"{LogPrefix} 이미 산 것을 또 눌렀더니 잔액이 움직였습니다.");
            Assert.AreEqual(1, CurrencyModel.PurchasedItemIds.Count,
                $"{LogPrefix} 구매 이력이 {CurrencyModel.PurchasedItemIds.Count}줄입니다 — 같은 것을 두 번 샀습니다.");

            Debug.Log($"{LogPrefix} 구매 성사 — 가격 {price:N0}, 잔액 {before:N0} → {CurrencyModel.CoinBalance:N0}, " +
                      $"칩 「{_window.ShopActionLabelForTests(index)}」, 즉시 저장 확인(호출 결과 + 디스크 실물 둘 다).");
        }

        // ============================================================================
        // ③ 다른 카드를 고르면 확인 단계가 풀린다 (설계 §3-5의 탈출구)
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SelectingAnotherCardCancelsTheConfirmStep()
        {
            yield return OpenShopTab();

            int first = FirstUnownedIndex();
            int second = -1;
            for (int i = first + 1; i < _window.ShopCardCountForTests; i++)
            {
                if (_window.ShopActionLabelForTests(i) != CharacterInfoWindow.ShopOwnedWord) { second = i; break; }
            }
            Assert.GreaterOrEqual(second, 0, $"{LogPrefix} 안 가진 상품이 하나뿐입니다 — 탈출구 대조가 공허합니다.");

            yield return FundAtLeast(PriceOf(first));

            _window.ShopBuyForTests(first);
            yield return null;
            Assert.AreEqual(first, _window.ShopConfirmIndexForTests,
                $"{LogPrefix} 확인 단계에 들어가지 않았습니다 — 아래 탈출구 대조가 공허합니다.");

            int before = CurrencyModel.CoinBalance;
            _window.ShopSelectForTests(second);
            yield return null;

            Assert.AreEqual(-1, _window.ShopConfirmIndexForTests,
                $"{LogPrefix} 다른 카드를 골랐는데 확인 단계가 살아 있습니다 — 그 상태로 한 번 더 누르면 " +
                "고르지도 않은 것이 팔립니다.");
            Assert.AreEqual(before, CurrencyModel.CoinBalance, $"{LogPrefix} 고르기만 했는데 돈이 나갔습니다.");
            Assert.IsFalse(CurrencyModel.IsPurchasedItem(IdOf(first)), $"{LogPrefix} 고르기만 했는데 샀습니다.");

            Debug.Log($"{LogPrefix} 확인 단계 탈출구 확인 — 다른 카드를 고르면 즉시 풀린다.");
        }
    }
}
