using System.Collections;
using System.Collections.Generic;
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
    /// ★ 등급 리본이 <b>화면에서</b> 등급을 말하는가 — 2026-09-03 사용자 확정 「안 C1」의 회귀 잠금.
    ///
    /// ============================================================================
    /// 왜 이 파일이 있는가 — 색은 등급을 못 나른다
    /// ============================================================================
    /// <c>design/art/PALETTE_SPEC.md</c> §12-0 실측: 브라스 램프는 인접 쌍 ΔE 15.16~18.48로 <b>변별</b>은
    /// 되지만 <b>식별</b> 하한 48.6을 넘는 쌍이 「일반↔전설」 하나뿐이다. 즉 <b>카드 한 장만 보고 색으로
    /// 등급을 맞히는 것은 정상 시각에서도 안 된다.</b> 그래서 주 채널이 <b>칸 수</b>가 됐고,
    /// <c>persona-newcomer</c>가 같은 것을 다른 말로 확인했다 — *"세면 되니까 확실했다."*
    ///
    /// <c>Tests/EditMode/RarityColorSingleSourceTests</c>는 <b>색</b>이 제 일을 하는지(대비·단조·단일 출처)를
    /// 이미 잠갔다. 이 파일이 잠그는 것은 그 뒤의 것 — <b>칸이 실제로 화면에 그려지는가, 그리고 낱말과
    /// 같은 등급을 말하는가</b>이다.
    ///
    /// ============================================================================
    /// ★ 기대값을 프로덕션 함수로 만들지 않는다 (TEAM.md 「생성기와 검사기가 같이 틀린다」)
    /// ============================================================================
    /// <c>UiChrome.RarityFilledCells</c>를 기대값으로 쓰면 그 함수가 틀어질 때 기대값도 함께 틀어져
    /// <b>아무것도 못 잰다</b>. 그래서 이 파일은 그 함수를 <b>한 번도 부르지 않는다</b>. 대신 두 성질만 본다:
    /// <list type="number">
    ///   <item><b>단조</b> — 등급이 오르면 칸이 <b>엄격히</b> 는다(같은 등급끼리는 같다).</item>
    ///   <item><b>전사(全射)</b> — 카탈로그 전체에서 관측된 칸 수 집합이 정확히 <c>{1 … 등급 수}</c>다.</item>
    /// </list>
    /// 이 둘이 함께 성립하면 「등급 k -> k+1칸」이 <b>유일한 해</b>다. 산술을 베끼지 않고 못 박는다.
    /// 그리고 관측값은 계산이 아니라 <b>지금 화면의 <see cref="Image"/> 색</b>에서 나온다.
    ///
    /// ============================================================================
    /// 여기서 잠그는 것
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>① 리본이 <b>카드 상단 여백</b> 안에 있고 썸네일을 <b>1pt도</b> 침범하지 않는다(세로 예산 0pt).</item>
    ///   <item>② 칸 수가 단조·전사다(위 참조). 칸 총수는 등급 단 수와 같다.</item>
    ///   <item>③ <b>낱말과 칸 수가 같은 등급을 말한다</b> — 두 채널이 갈라지면 화면이 두 가지를 동시에 말한다.</item>
    ///   <item>④ <b>잠긴 카드에서도 리본이 안 흐려진다.</b> 인계본의 <c>opacity .25</c>는 실측 대비
    ///     1.45~1.69로 "거기 리본이 있다"조차 안 보였다. 잠김은 이미 여섯 곳이 말한다.</item>
    ///   <item>⑤ 보관함 — 「할 줄 아는 것」과 헤더 줄에는 리본이 <b>없다</b>(0칸짜리 등급을 만들지 않는다),
    ///     장비 줄에는 있고 설명/상태 상자와 <b>겹치지 않는다</b>.</item>
    /// </list>
    /// </summary>
    public sealed class RarityRibbonSurfaceTests
    {
        private const string LogPrefix = "[등급리본-TEST]";

        /// <summary>[보관함] 탭 인덱스. <c>CharacterInfoWindow.Tab</c>이 private이라 창구가 여는 순서를 쓴다
        /// (<c>InventoryPagerRailTests</c>가 쓰는 관례 그대로다).</summary>
        private const int TabInventory = 2;

        private const float SettleTimeoutSeconds = 3f;

        private static readonly MethodInfo SetUnlockOverride = typeof(EquipmentDebugUnlock).GetMethod(
            "SetTestOverride", BindingFlags.Static | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            SetUnlockOverride?.Invoke(null, new object[] { null });
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // ① 세로 예산 — 리본은 카드를 1pt도 키우지 않았고 썸네일을 침범하지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RibbonSitsInsideTheCardTopMarginWithoutTouchingTheThumbnail()
        {
            yield return OpenWindow();

            int judged = 0;
            foreach (int i in VisibleCards())
            {
                Rect card = _window.CardRawScreenRect(i);
                Rect ribbon = _window.CardRarityRibbonRawScreenRect(i);
                Rect thumb = _window.CardThumbRawScreenRect(i);
                if (card.width <= 1f) continue;

                Assert.Greater(ribbon.width, 1f,
                    $"{LogPrefix} {i}번 카드에 리본이 없습니다 — 등급의 주 채널이 통째로 사라졌습니다.");
                Assert.Greater(thumb.width, 1f, $"{LogPrefix} {i}번 카드의 썸네일을 찾지 못했습니다.");

                // 화면 y는 위가 양수 — 리본은 카드 위 끝 <b>아래</b>, 썸네일 위 끝 <b>위</b>에 있어야 한다.
                Assert.LessOrEqual(ribbon.yMax, card.yMax + 0.5f,
                    $"{LogPrefix} {i}번 카드의 리본이 카드 위로 삐져나왔습니다(리본 {ribbon.yMax:F1} > 카드 {card.yMax:F1}).");
                Assert.GreaterOrEqual(ribbon.yMin, thumb.yMax - 0.5f,
                    $"{LogPrefix} {i}번 카드의 리본이 <b>썸네일을 침범</b>했습니다 " +
                    $"(리본 아래 {ribbon.yMin:F1} < 썸네일 위 {thumb.yMax:F1}). 카드 상단 여백 안에 들어가야 합니다.");

                // 가로는 썸네일과 <b>같은 자리</b>여야 한다 — 인셋을 숫자로 적지 않고 파생시킨 결과다.
                Assert.AreEqual(thumb.xMin, ribbon.xMin, 0.75f,
                    $"{LogPrefix} {i}번 카드의 리본 왼쪽 끝이 썸네일과 어긋났습니다.");
                Assert.AreEqual(thumb.xMax, ribbon.xMax, 0.75f,
                    $"{LogPrefix} {i}번 카드의 리본 오른쪽 끝이 썸네일과 어긋났습니다.");

                judged++;
            }

            Assert.Greater(judged, 0,
                $"{LogPrefix} 잰 카드가 0장입니다 — 관측 전제가 성립하지 않으므로 이 판정은 무효입니다.");
            Debug.Log($"{LogPrefix} 카드 {judged}장의 리본이 상단 여백 안에 있고 썸네일과 가로 정렬됩니다.");
        }

        // ============================================================================
        // ② 칸 수 — 단조 + 전사. 산술을 베끼지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CellCountRisesWithRarityAndCoversEveryStep()
        {
            yield return OpenWindow();

            var byRarity = new Dictionary<ItemRarity, int>();
            int judged = 0;

            foreach (int i in VisibleCards())
            {
                if (!_window.TryGetCardSlotForTests(i, out EquipmentSlot slot)) continue;
                ItemRarity rarity = ItemCatalog.Rarity(slot, _window.CardItemForTests(i));
                int fill = _window.CardRarityFilledCellsForTests(i);

                Assert.AreNotEqual(-1, fill,
                    $"{LogPrefix} {i}번 카드의 리본을 관측할 수 없습니다(리본이 만들어지지 않았습니다).");
                Assert.AreEqual(System.Enum.GetValues(typeof(ItemRarity)).Length,
                    _window.RarityRibbonCellCountForTests(i),
                    $"{LogPrefix} {i}번 카드 리본의 칸 총수가 등급 단 수와 다릅니다 — " +
                    "빈 칸 트랙이 없으면 총 폭이 등급마다 달라져 '채움 비율'이 다시 '세는 일'이 됩니다.");

                if (byRarity.TryGetValue(rarity, out int seen))
                {
                    Assert.AreEqual(seen, fill,
                        $"{LogPrefix} 같은 등급({ItemCatalog.RarityName(rarity)})인데 칸 수가 {seen}과 {fill}로 " +
                        "다릅니다 — 같은 등급이 화면에서 두 가지로 보입니다.");
                }
                else
                {
                    byRarity[rarity] = fill;
                }
                judged++;
            }

            Assert.Greater(judged, 0, $"{LogPrefix} 잰 카드가 0장입니다 — 이 판정은 무효입니다.");

            // (가) 단조 — 등급이 오르면 칸이 <b>엄격히</b> 는다.
            int previous = 0;
            var trace = new List<string>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                if (!byRarity.TryGetValue(r, out int fill)) continue;
                Assert.Greater(fill, previous,
                    $"{LogPrefix} {ItemCatalog.RarityName(r)}의 칸 수 {fill}이 아래 등급({previous}) 이하입니다 — " +
                    "칸 수가 등급을 말하지 못합니다.");
                previous = fill;
                trace.Add($"{ItemCatalog.RarityName(r)}={fill}");
            }

            // (나) 전사 — 카탈로그 전체에서 나온 칸 수 집합이 정확히 {1 … 등급 수}다.
            //     (가)와 (나)가 함께 서면 「등급 k -> k+1칸」이 유일한 해다.
            int steps = System.Enum.GetValues(typeof(ItemRarity)).Length;
            Assert.AreEqual(steps, byRarity.Count,
                $"{LogPrefix} 화면에 나타난 등급이 {byRarity.Count}종뿐입니다(기대 {steps}종) — " +
                "관측 모집단이 부족해 단조성만으로는 매핑을 못 박습니다: " + string.Join(" ", trace));
            for (int expected = 1; expected <= steps; expected++)
            {
                Assert.IsTrue(byRarity.ContainsValue(expected),
                    $"{LogPrefix} {expected}칸짜리 등급이 화면에 하나도 없습니다: " + string.Join(" ", trace));
            }

            Debug.Log($"{LogPrefix} 카드 {judged}장 / 칸 수 단조·전사 확인 — {string.Join(" ", trace)}");
        }

        // ============================================================================
        // ③ 낱말과 칸 수가 같은 등급을 말한다 (두 채널의 교차 대조)
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator DetailWordAndRibbonCellsAgreeOnTheSameRarity()
        {
            yield return OpenWindow();

            // 등급마다 한 장씩만 눌러 본다(클릭마다 중복 방지 창 0.35초를 넘겨야 한다).
            //
            // ★ <b>「온전히 보이는」 카드만 고른다</b> — 이 창의 규칙이 "보이지 않는 것은 눌리지 않는다"라
            //   마스크에 잘린 카드에 클릭을 보내면 도착하지 않는다.
            // ★★ <b>그래서 여기서 등급 4종을 다 보겠다고 요구하면 안 된다</b>(2026-09-03 실측으로 배웠다):
            //   배치모드 PlayMode 화면은 <b>640×480</b>이고 <c>ClampPanelToScreen</c>이 1042pt 창을
            //   608pt로 줄이는데 <b>내용은 함께 접히지 않는다</b>. 그 결과 온전히 보이는 카드가
            //   <b>등급 1종</b>뿐이었고, 초판은 그 사정으로 <b>제품이 멀쩡한데 빨개졌다</b>.
            //   <b>모집단을 요구하는 단언은 화면 크기에 종속된다 — 그건 거짓 빨강이다.</b>
            //   등급 4단을 전부 훑는 일은 <see cref="CellCountRisesWithRarityAndCoversEveryStep"/>이
            //   맡는다(그쪽은 클릭이 필요 없어서 잘린 카드도 잰다). 여기가 잠그는 것은
            //   <b>두 채널이 서로 어긋나지 않는가</b>이고, 그건 한 장에서도 성립하거나 깨진다.
            var picked = new Dictionary<ItemRarity, int>();
            foreach (int i in VisibleCards())
            {
                if (!_window.TryGetCardSlotForTests(i, out EquipmentSlot slot)) continue;
                if (!IsFullyVisible(i)) continue;
                ItemRarity rarity = ItemCatalog.Rarity(slot, _window.CardItemForTests(i));
                if (!picked.ContainsKey(rarity)) picked[rarity] = i;
            }

            Assert.Greater(picked.Count, 0,
                $"{LogPrefix} 온전히 보이는(= 클릭이 도착하는) 카드가 <b>한 장도</b> 없습니다 — " +
                "관측 전제가 성립하지 않으므로 이 판정은 무효입니다.");

            var names = new List<string>();
            var order = new List<ItemRarity>();
            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                order.Add(r);
                names.Add(ItemCatalog.RarityName(r));
            }

            int judged = 0;
            foreach (KeyValuePair<ItemRarity, int> pair in picked)
            {
                yield return ClickCardBody(pair.Value);

                // ★ <b>클릭이 도착했는지를 먼저, 독립적으로 확인한다.</b> 이게 없으면 "두 채널이
                //   어긋났다"와 "클릭이 안 갔다"가 <b>똑같이 생긴 빨강</b>으로 나온다 — 이 저장소가
                //   반복해서 당한 형태다. 보유 아이템이면 상세 패널 <b>이름</b>이 그 증거다
                //   (잠긴 것은 <c>???</c>라 아이템을 특정할 수 없으므로 이 확인을 건너뛴다).
                _window.TryGetCardSlotForTests(pair.Value, out EquipmentSlot clickedSlot);
                int clickedItem = _window.CardItemForTests(pair.Value);
                if (EquipmentModel.IsItemOwned(clickedSlot, clickedItem))
                {
                    Assert.AreEqual(EquipmentModel.ItemName(clickedSlot, clickedItem),
                        _window.DetailNameTextForTests,
                        $"{LogPrefix} 클릭이 {pair.Value}번 카드에 <b>도착하지 않았습니다</b> — " +
                        "아래 등급 대조는 다른 아이템을 보고 판정하게 되므로 여기서 멈춥니다.");
                }

                string meta = _window.DetailMetaTextForTests;
                Assert.IsNotNull(meta, $"{LogPrefix} 상세 패널 메타 줄을 읽지 못했습니다.");

                // 그 줄에 등급 낱말이 <b>정확히 하나</b> 있어야 한다.
                int found = -1, hits = 0;
                for (int k = 0; k < names.Count; k++)
                {
                    if (meta.IndexOf(names[k], System.StringComparison.Ordinal) < 0) continue;
                    hits++;
                    found = k;
                }
                Assert.AreEqual(1, hits,
                    $"{LogPrefix} 상세 메타(\"{meta}\")에서 등급 낱말이 {hits}개 잡혔습니다(기대 1개). " +
                    "낱말이 없으면 색이 못 하는 일(식별)을 아무도 안 하고, 둘이면 어느 것이 등급인지 모릅니다.");

                int fill = _window.CardRarityFilledCellsForTests(pair.Value);

                // (가) 두 채널의 교차 대조 — 낱말의 <b>자리</b>와 리본의 <b>칸 수</b>가 같은 단을 가리킨다.
                Assert.AreEqual(found + 1, fill,
                    $"{LogPrefix} 화면이 두 가지를 말합니다 — 낱말은 \"{names[found]}\"({found + 1}번째 단)인데 " +
                    $"리본은 {fill}칸입니다.");

                // (나) 그 낱말이 <b>데이터</b>가 말하는 등급과도 같다.
                Assert.AreEqual(ItemCatalog.RarityName(pair.Key), names[found],
                    $"{LogPrefix} 카탈로그는 {ItemCatalog.RarityName(pair.Key)}라는데 상세 패널은 " +
                    $"\"{names[found]}\"라고 적었습니다.");
                Assert.AreEqual(pair.Key, order[found], $"{LogPrefix} 등급 낱말의 자리가 열거 순서와 어긋났습니다.");

                judged++;
            }

            Assert.AreEqual(picked.Count, judged, $"{LogPrefix} 고른 {picked.Count}장 중 {judged}장만 쟀습니다.");
            Debug.Log($"{LogPrefix} 등급 {judged}종에서 낱말과 칸 수가 같은 단을 가리킵니다" +
                      $"(온전히 보이는 카드가 화면 크기에 따라 달라지므로 이 수는 4가 아닐 수 있습니다 — " +
                      $"4단 전수는 {nameof(CellCountRisesWithRarityAndCoversEveryStep)}가 잠근다)."); 
        }

        // ============================================================================
        // ④ 잠긴 카드에서도 리본은 흐려지지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LockedCardKeepsItsRibbonAtFullStrength()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            yield return OpenWindow();

            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();
            yield return null;
            yield return null;

            // 같은 등급의 <b>보유</b> 카드와 <b>잠긴</b> 카드를 한 쌍 찾는다.
            var owned = new Dictionary<ItemRarity, int>();
            var locked = new Dictionary<ItemRarity, int>();
            foreach (int i in VisibleCards())
            {
                if (!_window.TryGetCardSlotForTests(i, out EquipmentSlot slot)) continue;
                int item = _window.CardItemForTests(i);
                ItemRarity rarity = ItemCatalog.Rarity(slot, item);
                Dictionary<ItemRarity, int> bucket = EquipmentModel.IsItemOwned(slot, item) ? owned : locked;
                if (!bucket.ContainsKey(rarity)) bucket[rarity] = i;
            }

            int pairs = 0;
            foreach (KeyValuePair<ItemRarity, int> pair in owned)
            {
                if (!locked.TryGetValue(pair.Key, out int lockedCard)) continue;

                Assert.IsTrue(_window.IsCardRarityRibbonVisibleForTests(lockedCard),
                    $"{LogPrefix} 잠긴 카드의 리본이 꺼져 있습니다 — 잠김은 이미 이름·바탕·썸네일·실루엣·" +
                    "자물쇠·버튼 여섯 곳이 말합니다. 리본은 등급 하나만 말해야 합니다.");
                Assert.AreEqual(_window.CardRarityFilledCellsForTests(pair.Value),
                    _window.CardRarityFilledCellsForTests(lockedCard),
                    $"{LogPrefix} 같은 등급({ItemCatalog.RarityName(pair.Key)})인데 잠긴 카드의 칸 수가 다릅니다.");
                Assert.AreEqual(_window.CardRarityFillColorForTests(pair.Value),
                    _window.CardRarityFillColorForTests(lockedCard),
                    $"{LogPrefix} 잠긴 카드의 리본이 <b>흐려졌습니다</b>. 인계본의 opacity .25는 실측 대비 " +
                    "1.45~1.69로 '거기 리본이 있다'조차 안 보였습니다(UX_HANDOFF_REVIEW_BRASS_ARCHIVE §5-5).");
                pairs++;
            }

            Assert.Greater(pairs, 0,
                $"{LogPrefix} QA 해금을 끄고 Lv.{CharacterProgressionModel.Level}인데도 " +
                "같은 등급의 보유/잠김 쌍이 하나도 없습니다 — 관측 전제가 성립하지 않습니다.");
            Debug.Log($"{LogPrefix} 등급 {pairs}종에서 잠긴 카드의 리본이 보유 카드와 같습니다.");
        }

        // ============================================================================
        // ⑤ 보관함 — 없는 등급은 없게, 있는 등급은 남의 칸을 안 밟고
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator InventoryShowsRibbonsOnlyWhereRarityExists()
        {
            yield return OpenInventory();

            int equipmentRows = 0, actionRows = 0, headerRows = 0;

            for (int page = 0; page <= _window.MaxInventoryScrollForTests; page++)
            {
                for (int row = 0; row < 64; row++)
                {
                    int catalogIndex = _window.InventoryRowCatalogIndexForTests(row);
                    if (catalogIndex == -2) break;                     // 그 줄부터는 꺼져 있다.

                    bool visible = _window.IsInventoryRibbonVisibleForTests(row);

                    if (catalogIndex < 0)
                    {
                        headerRows++;
                        Assert.IsFalse(visible,
                            $"{LogPrefix} 헤더 줄({row})에 리본이 떠 있습니다 — 제목에는 등급이 없습니다.");
                        continue;
                    }

                    ItemCatalogEntry entry = ItemCatalog.At(catalogIndex);
                    Assert.IsNotNull(entry, $"{LogPrefix} {catalogIndex}번 카탈로그 항목이 비었습니다.");

                    if (!entry.Slot.HasValue)
                    {
                        actionRows++;
                        Assert.IsFalse(visible,
                            $"{LogPrefix} 「할 줄 아는 것」 줄(\"{entry.DisplayName}\")에 리본이 떠 있습니다 — " +
                            "행동에는 등급이 없고, 빈 트랙만 남기면 '0칸짜리 등급'으로 읽힙니다.");
                        continue;
                    }

                    equipmentRows++;
                    Assert.IsTrue(visible,
                        $"{LogPrefix} 장비 줄(\"{entry.DisplayName}\")에 리본이 없습니다.");
                    Assert.Greater(_window.InventoryRibbonFilledCellsForTests(row), 0,
                        $"{LogPrefix} 장비 줄(\"{entry.DisplayName}\")의 리본이 <b>0칸</b>입니다 — " +
                        "어떤 등급도 0칸이 아닙니다.");

                    // 리본은 설명 칸에서 자리를 <b>떼어 왔다</b>. 그 거래가 지켜졌는지 사각형으로 확인한다.
                    Rect ribbon = _window.InventoryRibbonRawScreenRect(row);
                    Rect description = _window.InventoryDescriptionRawScreenRect(row);
                    Rect status = _window.InventoryStatusSlotRawScreenRect(row);
                    Assert.Greater(ribbon.width, 1f, $"{LogPrefix} {row}번 줄 리본의 사각형이 비었습니다.");
                    Assert.GreaterOrEqual(ribbon.xMin, description.xMax - 0.5f,
                        $"{LogPrefix} {row}번 줄에서 리본이 <b>설명 칸을 밟았습니다</b> " +
                        $"(리본 {ribbon.xMin:F1} < 설명 끝 {description.xMax:F1}).");
                    Assert.LessOrEqual(ribbon.xMax, status.xMin + 0.5f,
                        $"{LogPrefix} {row}번 줄에서 리본이 <b>상태 슬롯을 밟았습니다</b> " +
                        $"(리본 {ribbon.xMax:F1} > 상태 시작 {status.xMin:F1}).");
                }

                if (!_window.CanScrollInventoryForTests(+1)) break;
                _window.ScrollInventoryForTests(+1);
                yield return null;
            }

            Assert.Greater(equipmentRows, 0, $"{LogPrefix} 장비 줄을 하나도 못 봤습니다 — 이 판정은 무효입니다.");
            Assert.Greater(actionRows, 0,
                $"{LogPrefix} 「할 줄 아는 것」 줄을 하나도 못 봤습니다 — " +
                "'등급이 없는 줄에는 리본이 없다'가 실제로 검증되지 않았습니다(양성 대조 없음).");
            Assert.Greater(headerRows, 0, $"{LogPrefix} 헤더 줄을 하나도 못 봤습니다.");

            Debug.Log($"{LogPrefix} 보관함 — 장비 {equipmentRows}줄에 리본 / 행동 {actionRows}줄·헤더 {headerRows}줄에는 없음.");
        }

        // ==================== 도구 ====================

        /// <summary>지금 탭에서 살아 있는 카드 인덱스(마스크에 잘린 것도 포함 — 색은 잘려도 칠해진다).</summary>
        private IEnumerable<int> VisibleCards()
        {
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (_window.IsCardVisibleForTests(i)) yield return i;
            }
        }

        /// <summary>캐러셀 마스크에 <b>안 잘린</b> 카드인가 — 클릭을 보낼 대상은 여기서 고른다.</summary>
        private bool IsFullyVisible(int index)
        {
            Rect raw = _window.CardRawScreenRect(index);
            Rect visible = _window.CardVisibleScreenRect(index);
            return raw.width > 1f && visible.width >= raw.width - 1f && visible.height >= raw.height - 1f;
        }

        /// <summary>카드 <b>본체</b>를 눌러 선택을 옮긴다(착용은 하단 버튼이 한다).
        /// 대기는 벽시계 기준이다 — 배치모드 PlayMode는 2,000fps 이상으로 돈다(CLAUDE.md).</summary>
        private IEnumerator ClickCardBody(int cardIndex)
        {
            yield return new WaitForSecondsRealtime(0.4f);

            Rect card = _window.CardRawScreenRect(cardIndex);
            Assert.Greater(card.width, 1f, $"{LogPrefix} {cardIndex}번 카드의 사각형이 비었습니다.");

            var point = new Vector2(card.center.x, card.yMax - card.height * 0.25f);
            _window.FeedClickForTests(point);
            yield return null;
            yield return null;
        }

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Open("등급 리본 테스트");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }

        private IEnumerator OpenInventory()
        {
            yield return OpenWindow();

            Rect tab = _window.TabScreenRect(TabInventory);
            Assert.Greater(tab.width, 0f, $"{LogPrefix} [보관함] 탭의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(tab.center);
            yield return null;
            yield return SettlePanelHeight();
        }

        /// <summary>★ L-8(2026-09-05) — 창 높이가 탭과 무관하게 고정이 되면서 <b>기다릴 애니메이션이
        /// 사라졌다</b>. 호출부 형태를 그대로 두려고 함수만 남긴다(한 프레임 넘긴다).</summary>
        private IEnumerator SettlePanelHeight()
        {
            yield return null;
        }
    }
}
