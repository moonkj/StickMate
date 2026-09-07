using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 카드 <b>그림</b>과 카드 <b>텍스트</b>가 같은 아이템을 가리키는가 — 2026-09-06 밤 사용자 신고
    /// *"현재 캐릭터창 효과 이미지와 텍스트가 맞지 않음 아까 효과 없음 지우면서 다 밀린거 같아"*.
    ///
    /// ============================================================================
    /// 실제 원인 (2026-09-07 확정)
    /// ============================================================================
    /// <see cref="CharacterInfoWindow.RefreshCards"/>(텍스트/이름/설명)는 카드 자리 c를
    /// <see cref="ItemCatalog.ListedItemIndex"/>로 환산한 뒤 그 값을 쓴다 — 은퇴한 아이템(FX
    /// 「없음」)이 목록 가운데 있으면 뒤 카드는 전부 한 칸씩 밀린다는 것을 알고 하는 환산이다.
    /// 그런데 <b>카드 그림</b>을 굽는 <c>BuildCard</c>는 카드 <b>풀을 만드는 시점에 한 번만</b> 굽고
    /// 다시 굽지 않는데, 그 자리가 <b>환산 없이</b> 카드 자리 번호(columnIndex)를 카탈로그 인덱스로
    /// 그대로 먹이고 있었다 — 텍스트 경로만 고치고 그림 경로를 빠뜨린 것이다. 그 결과 은퇴 아이템
    /// <b>뒤의 모든 카드</b>에서 그림이 텍스트보다 한 칸 앞선 아이템을 그렸다(정확히 사용자가 본
    /// 증상). 수정은 <c>Interaction/CharacterInfoWindow.Cards.cs</c>의 <c>BuildCard</c>가 그림도
    /// <see cref="ItemCatalog.ListedItemIndex"/> <b>같은 함수</b>로 환산하게 만든 것 하나다.
    ///
    /// ============================================================================
    /// 왜 EditMode가 아니라 PlayMode인가
    /// ============================================================================
    /// 버그의 실체는 "카드 <b>그림 그래픽</b>을 구운 인덱스"였다. 그 값은 <c>ItemCard.IconRoot</c>
    /// 아래 실제로 만들어진 <see cref="RectTransform"/>/<see cref="UnityEngine.UI.Image"/> 계층에만
    /// 남고, 그건 캔버스가 실제로 살아 있어야(=씬 로드 + 창 오픈) 존재한다. EditMode의
    /// <c>RetiredSlotSurfaceTests</c>는 <see cref="ItemCatalog"/> 데이터만 보므로 이 그림 경로를
    /// 애초에 볼 수 없다 — 정확히 그 사각지대에 이 버그가 숨어 있었다.
    ///
    /// ============================================================================
    /// 왜 <b>전수</b>인가 (0번/마지막 번만 보면 못 잡는다)
    /// ============================================================================
    /// 이 버그는 은퇴 아이템 <b>앞쪽</b>(자리 번호가 안 밀린 구간)에서는 증상이 없다 — 자리 번호와
    /// 카탈로그 번호가 우연히 같기 때문이다. 은퇴 아이템 <b>바로 다음 자리부터</b> 어긋나기 시작해서
    /// 그 카테고리 끝까지 계속된다. 그래서 <see cref="모든_카드의_그림과_텍스트가_같은_아이템을_가리킨다"/>는
    /// [장비]/[외형] 두 카드 탭의 <b>보이는 카드 전부</b>(= 은퇴분을 뺀 사실상 42종 전체)를 훑는다.
    /// </summary>
    public sealed class CardIconIdentityTests
    {
        private const string LogPrefix = "[카드그림-TEST]";

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;
            yield return null;   // RefreshCards/LayoutCardGrid가 한 번 돌 기회를 준다.
        }

        /// <summary>탭 라벨의 화면 중심 — InfoWindowCardCarouselTests.TabCenter와 같은 관례
        /// (탭 사각형은 창이 스스로 알고 있으므로 반사로 읽는다).</summary>
        private Vector2 TabCenter(int tabIndex)
        {
            var field = typeof(CharacterInfoWindow).GetField("_tabRects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tabs = field != null ? field.GetValue(_window) as RectTransform[] : null;
            Assert.IsNotNull(tabs, $"{LogPrefix} _tabRects를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            var corners = new Vector3[4];
            tabs[tabIndex].GetWorldCorners(corners);
            Vector3 c = (corners[0] + corners[2]) * 0.5f;
            return new Vector2(c.x, c.y);
        }

        /// <summary>한 카드가 <b>보이는 목록 안에서 몇 번째 자리(c)</b>인지 — 카드 풀은
        /// (섹션, 자리) 순서로 순차 배정되므로(<c>BuildCard</c> 호출부), 같은 섹션의 카드를
        /// 등장 순서대로 세면 곧 그 값이다. 손으로 옮겨 적지 않고 <b>매 호출마다 다시 센다</b> —
        /// 이 값이 바로 "만약 그림 경로가 환산 없이 이 번호를 카탈로그 인덱스로 먹였다면"의
        /// <b>naive(옛 버그) 인덱스</b>다.</summary>
        private int PositionWithinSection(int cardIndex, int section)
        {
            int pos = 0;
            for (int i = 0; i < cardIndex; i++)
            {
                if (_window.IsCardVisibleForTests(i) && _window.CardSectionForTests(i) == section) pos++;
            }
            return pos;
        }

        private struct Probe
        {
            public int CardIndex;
            public EquipmentSlot Slot;
            public int Position;     // naive(옛 버그) 인덱스 — 은퇴가 없었다면 카탈로그 인덱스와 같다.
            public int TextItem;     // RefreshCards가 심은, 텍스트가 실제로 읽는 인덱스.
            public int IconItem;     // BuildCard가 그림을 구운 인덱스(없으면 −1).
        }

        /// <summary>[장비]+[외형] 두 카드 탭의 <b>보이는 카드 전부</b>를 훑어 표본을 모은다.
        /// 탭 전환 한 번으로 은퇴분을 뺀 사실상 42종 전체(35종 카드 + 행동 없음)를 지나간다.</summary>
        private IEnumerator CollectAllVisibleCards(List<Probe> probes)
        {
            for (int tabIndex = 0; tabIndex <= 1; tabIndex++)   // 0=[장비] 1=[외형] — 카드가 있는 탭 전부.
            {
                if (tabIndex > 0)
                {
                    _window.FeedClickForTests(TabCenter(tabIndex));
                    yield return null;
                }

                for (int i = 0; i < _window.CardCountForTests; i++)
                {
                    if (!_window.IsCardVisibleForTests(i)) continue;
                    Assert.IsTrue(_window.TryGetCardSlotForTests(i, out EquipmentSlot slot),
                        $"{LogPrefix} 탭 {tabIndex}의 {i}번 카드가 보이는데 슬롯을 못 찾았습니다.");

                    int section = _window.CardSectionForTests(i);
                    probes.Add(new Probe
                    {
                        CardIndex = i,
                        Slot = slot,
                        Position = PositionWithinSection(i, section),
                        TextItem = _window.CardItemForTests(i),
                        IconItem = _window.CardIconItemForTests(i),
                    });
                }
            }
        }

        /// <summary>
        /// ★ 본체 — 그림과 텍스트가 <b>같은 카드에서 정확히 일치</b>하는가(전수, 표본 아님).
        /// 두 값이 갈라지면 사용자가 실기에서 본 것과 정확히 같은 증상이다: 카드에 적힌 이름을
        /// 누르면 화면과 다른 물건이 입혀진다(그림) 또는 다른 이름이 뜬다(텍스트).
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 모든_카드의_그림과_텍스트가_같은_아이템을_가리킨다()
        {
            yield return OpenWindow();

            var probes = new List<Probe>();
            yield return CollectAllVisibleCards(probes);

            Assert.Greater(probes.Count, 10,
                $"{LogPrefix} 검사한 카드가 {probes.Count}장뿐입니다 — 두 카드 탭을 통틀어 " +
                "은퇴분을 뺀 35종 안팎이 나와야 합니다. 탭 전환이 실패했을 수 있습니다.");

            bool sawShift = false;
            var mismatches = new List<string>();

            foreach (Probe p in probes)
            {
                Assert.GreaterOrEqual(p.TextItem, 0,
                    $"{LogPrefix} [{EquipmentModel.SlotName(p.Slot)}] 카드 {p.CardIndex}의 텍스트 인덱스가 " +
                    "범위 밖입니다 — ListedItemIndex 환산이 깨졌습니다.");

                if (p.TextItem != p.Position) sawShift = true;   // 은퇴 아이템 때문에 실제로 밀린 자리.

                if (p.IconItem != p.TextItem)
                {
                    string iconName = p.IconItem >= 0
                        ? EquipmentModel.ItemName(p.Slot, p.IconItem) : "(그림 없음)";
                    string iconId = p.IconItem >= 0 ? EquipmentModel.ItemId(p.Slot, p.IconItem) : "—";
                    string textName = EquipmentModel.ItemName(p.Slot, p.TextItem);
                    string textId = EquipmentModel.ItemId(p.Slot, p.TextItem);
                    mismatches.Add($"  · [{EquipmentModel.SlotName(p.Slot)}] 카드 {p.CardIndex}" +
                        $"(자리 {p.Position}번째): 그림=[{iconName}]({iconId}, idx {p.IconItem}) vs " +
                        $"텍스트=[{textName}]({textId}, idx {p.TextItem}).");
                }
            }

            Assert.IsTrue(mismatches.Count == 0,
                $"{LogPrefix} 그림-텍스트 불일치 카드 {mismatches.Count}/{probes.Count}장:\n" +
                string.Join("\n", mismatches));

            // ★ 공허 방지 — 은퇴로 인한 실제 밀림을 <b>하나 이상</b> 지나가지 못했다면 위 전수 검사는
            //   전부 "우연히 같은" 구간만 본 것이라 이 버그를 절대 못 잡는다(은퇴 앞쪽은 자리=카탈로그
            //   번호가 같아서 옛 버그도 통과한다). 그래서 반드시 밀린 구간을 하나 이상 봐야 한다.
            Assert.IsTrue(sawShift,
                $"{LogPrefix} 어느 카드도 자리 번호와 카탈로그 번호가 갈라지지 않았습니다 — 은퇴 데이터가 " +
                "사라졌거나(RetiredSlotSurfaceTests가 먼저 빨개져야 함) 카드 풀이 비었습니다. 이 상태에서는 " +
                "위 일치 검사가 옛 버그가 있어도 통과하는 공허한 검사입니다.");

            Debug.Log($"{LogPrefix} 카드 {probes.Count}장 전수 대조 — 불일치 0건, 밀림 관측 {sawShift}.");
        }

        /// <summary>
        /// ★ 음성 대조 — <b>"자리 번호를 환산 없이 그대로 먹이면 이 버그가 재현되는가"</b>를 같은
        /// 표본으로 직접 증명한다. 최소 한 장의 카드에서 naive(자리 번호) ≠ 올바른 인덱스여야 하고,
        /// 실제 그림 인덱스는 naive가 <b>아니라</b> 올바른 값과 같아야 한다 — 즉 "그림이 naive 인덱스를
        /// 먹었다면 여기서 실패했을 것"을 데이터로 못박는다(프로덕션 코드를 실제로 되돌리지 않고도
        /// 같은 표본에서 두 가설을 대조하는 방식 — CLAUDE.md "음성대조"의 두 방식 중 후자,
        /// "인덱스 파생을 일부러 다르게 갈라서 재현").
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 음성대조_자리_번호를_환산_없이_먹였다면_이_버그가_재현됐다()
        {
            yield return OpenWindow();

            var probes = new List<Probe>();
            yield return CollectAllVisibleCards(probes);

            int shiftedProbed = 0;
            foreach (Probe p in probes)
            {
                if (p.TextItem == p.Position) continue;   // 밀리지 않은 자리 — naive와 정답이 같아 이 대조에 쓸모없다.
                shiftedProbed++;

                // naive(옛 버그) 인덱스로 그렸다면 나왔을 그림은 p.Position번 아이템이었다.
                // 실제 그림(p.IconItem)은 그 값이 <b>아니라</b> 올바른 값(p.TextItem)과 같아야 한다.
                Assert.AreNotEqual(p.Position, p.IconItem,
                    $"{LogPrefix} [{EquipmentModel.SlotName(p.Slot)}] 카드 {p.CardIndex} — 그림이 naive 자리 " +
                    $"번호({p.Position})를 그대로 그렸습니다. 은퇴 아이템 뒤에서 정확히 이 형태로 재현되는 " +
                    "버그입니다(BuildCard가 ListedItemIndex 환산을 다시 빠뜨렸을 가능성).");
                Assert.AreEqual(p.TextItem, p.IconItem,
                    $"{LogPrefix} [{EquipmentModel.SlotName(p.Slot)}] 카드 {p.CardIndex} — 그림({p.IconItem})이 " +
                    $"텍스트({p.TextItem})와도, naive({p.Position})와도 다른 제3의 값입니다.");
            }

            Assert.Greater(shiftedProbed, 0,
                $"{LogPrefix} 밀린 자리를 하나도 못 봤습니다 — 이 대조가 성립하려면 은퇴로 인해 " +
                "자리 번호 ≠ 카탈로그 번호인 카드가 최소 1장 있어야 합니다.");

            Debug.Log($"{LogPrefix} 밀린 자리 {shiftedProbed}장 전부에서 그림이 naive 인덱스가 아니라 " +
                "올바른(텍스트와 같은) 인덱스를 그렸습니다.");
        }
    }
}
