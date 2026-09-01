using System.Collections;
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
    /// ★ 2026-09-01 사용자 신고 회귀 — <b>"각 장비별 착용버튼으로 했는데 왜 옛날처럼 하단에 착용상자가
    /// 따로 있음?"</b>
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 카드 하단에 [착용]/[해제] 버튼을 넣으면서 <b>상세 패널의 같은 버튼을 안 걷어냈다</b>. 같은 동작을
    /// 하는 손잡이가 화면에 둘이 됐고, 코드 주석 세 곳은 여전히 "착용은 상세 패널의 버튼 하나로만 한다",
    /// "밝은 채움은 화면에 상세 패널 하나뿐이다"라고 말하고 있었다 — <b>화면과 문서가 서로 다른 말을
    /// 하는 상태</b>였다.
    ///
    /// ============================================================================
    /// 결정: <b>버튼만 빼고 설명은 유지</b> (사용자 확정)
    /// ============================================================================
    /// 패널을 통째로 지우면 <b>기능이 사라진다</b>. 잠긴 아이템도 선택은 되고, 이 패널이 "왜 잠겼는지"를
    /// 알 수 있는 <b>유일한 경로</b>이기 때문이다(33-7-4). 카드에는 이름(<c>???</c>)과 요구 레벨 숫자밖에
    /// 없고 설명문이 없다.
    ///
    /// ============================================================================
    /// 여기서 잠그는 것
    /// ============================================================================
    ///  ① 상세 패널 안에 <b>누를 수 있는 것이 하나도 없다</b>(<see cref="UnityEngine.UI.Button"/> 0개).
    ///     색이나 라벨이 아니라 <b>존재 여부</b>로 본다 — 버튼을 되살리고 색만 죽이는 회귀도 잡는다.
    ///  ② 그래도 <b>잠긴 이유는 여전히 보인다</b>: 잠긴 카드를 고르면 이름은 <c>???</c>, 메타 줄과
    ///     설명문이 <b>요구 레벨</b>을 말한다.
    ///  ③ 보유한 아이템을 고르면 설명이 <b>그 아이템의 카탈로그 설명문</b>으로 바뀐다(패널이 살아
    ///     움직이는지 — ②가 "항상 같은 문장"이라 통과하는 가짜 초록이 되지 않게).
    ///
    /// <para>카드 버튼으로 착용/해제가 실제로 되는지는 <c>InfoWindowCardCarouselTests</c>가 이미
    /// 잠근다((c) 착용/자동 해제, (d) 미는 동안 착용 안 됨). 여기서 다시 세지 않는다.</para>
    ///
    /// ============================================================================
    /// 문장을 베끼지 않는다
    /// ============================================================================
    /// <c>"레벨 n이 되면 열립니다..."</c> 같은 프로덕션 문구를 여기 적으면, 카피를 다듬는 순간 이 파일이
    /// <b>프로덕션이 아니라 옛 문장</b>을 지키게 된다(CLAUDE.md). 그래서 <b>요구 레벨 숫자가 등장하는가</b>와
    /// <b>보유/잠김에서 문장이 달라지는가</b>라는 관계만 본다.
    ///
    /// ============================================================================
    /// QA 해금 스위치를 끄고 본다
    /// ============================================================================
    /// 에디터/PlayMode에서는 <see cref="EquipmentDebugUnlock"/>이 켜져 있어 <b>잠긴 아이템이 하나도
    /// 없다</b>. 그 상태로는 ②를 물어볼 수 없으므로 이 스위치를 테스트 동안만 강제로 끈다
    /// (<c>SetTestOverride</c>는 internal이고 PlayMode 어셈블리에는 InternalsVisibleTo가 없어
    /// 리플렉션을 쓴다 — 이 저장소가 이미 쓰는 관례다).
    /// </summary>
    public sealed class InfoWindowDetailPanelReadOnlyTests
    {
        private const string LogPrefix = "[상세패널-TEST]";

        private static readonly MethodInfo SetUnlockOverride = typeof(EquipmentDebugUnlock).GetMethod(
            "SetTestOverride", BindingFlags.Static | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;
        private StickConfig _config;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            SetUnlockOverride?.Invoke(null, new object[] { null });   // 실제 판정으로 되돌린다.
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            _config = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // ① 상세 패널에는 누를 것이 없다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DetailPanelHasNoEquipButton()
        {
            yield return OpenWindow();

            int buttons = _window.DetailPanelButtonCountForTests;
            Assert.AreNotEqual(-1, buttons,
                $"{LogPrefix} 상세 패널 자체를 찾지 못했습니다 — 패널을 통째로 지웠습니까? " +
                "지우면 '왜 잠겼는지'를 알 수 있는 유일한 경로가 사라집니다(사용자 결정은 '버튼만 빼고 설명은 유지').");
            Assert.AreEqual(0, buttons,
                $"{LogPrefix} 상세 패널 안에 누를 수 있는 것이 {buttons}개 있습니다 — " +
                "카드마다 [착용] 버튼이 있는데 하단에 또 착용 상자가 생겼습니다(2026-09-01 사용자 신고 재발).");

            Debug.Log($"{LogPrefix} 상세 패널은 읽기 전용입니다(Button 0개).");
        }

        // ============================================================================
        // ② 잠긴 이유는 여전히 보인다 (패널을 남긴 이유 그 자체)
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LockedItemStillExplainsWhyItIsLocked()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            yield return OpenWindow();

            // 잠금을 실제로 살린다: QA 해금 OFF + 레벨 1.
            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();
            yield return null;

            int locked = FindFullyVisibleCard(owned: false);
            Assert.GreaterOrEqual(locked, 0,
                $"{LogPrefix} QA 해금을 끄고 Lv.{CharacterProgressionModel.Level}인데도 잠긴 카드가 " +
                "화면에 하나도 없습니다 — 관측 전제가 성립하지 않습니다(카탈로그의 요구 레벨이 전부 사라졌습니까?).");

            Assert.IsTrue(_window.TryGetCardSlotForTests(locked, out EquipmentSlot slot),
                $"{LogPrefix} {locked}번 카드의 슬롯을 읽지 못했습니다.");
            int item = _window.CardItemForTests(locked);
            int required = EquipmentModel.RequiredLevel(slot, item);

            yield return ClickCardBody(locked);

            // 이름은 감춘다 — 무엇인지 알려주면 잠금이 잠금이 아니게 된다.
            Assert.AreNotEqual(EquipmentModel.ItemName(slot, item), _window.DetailNameTextForTests,
                $"{LogPrefix} 잠긴 아이템의 <b>이름이 그대로</b> 상세 패널에 떴습니다.");

            // 그런데 <b>왜</b> 잠겼는지는 말해야 한다 — 요구 레벨 숫자가 메타 줄과 설명문에 있어야 한다.
            string meta = _window.DetailMetaTextForTests;
            string body = _window.DetailBodyTextForTests;
            string requiredText = required.ToString();

            Assert.IsNotNull(meta, $"{LogPrefix} 상세 패널 메타 줄을 읽지 못했습니다.");
            Assert.IsNotNull(body, $"{LogPrefix} 상세 패널 설명문을 읽지 못했습니다.");
            StringAssert.Contains(requiredText, meta,
                $"{LogPrefix} 메타 줄(\"{meta}\")에 요구 레벨({required})이 없습니다 — " +
                "잠긴 이유를 알 수 있는 유일한 경로가 침묵합니다.");
            StringAssert.Contains(requiredText, body,
                $"{LogPrefix} 설명문(\"{body}\")에 요구 레벨({required})이 없습니다. " +
                "상세 패널을 남긴 이유가 정확히 이 문장입니다 — 카드에는 이 설명이 없습니다.");

            Debug.Log($"{LogPrefix} 잠긴 카드({slot} {item}번, Lv.{required}) 선택 → 이유가 보입니다: \"{body}\"");
        }

        // ============================================================================
        // ③ 보유 아이템을 고르면 설명이 바뀐다 (패널이 살아 있다는 증거)
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OwnedItemShowsItsOwnDescriptionInsteadOfTheLockNotice()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            yield return OpenWindow();

            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();
            yield return null;

            int locked = FindFullyVisibleCard(owned: false);
            int owned = FindFullyVisibleCard(owned: true);
            Assert.GreaterOrEqual(locked, 0, $"{LogPrefix} 잠긴 카드를 찾지 못했습니다.");
            Assert.GreaterOrEqual(owned, 0, $"{LogPrefix} 보유한 카드를 찾지 못했습니다.");

            yield return ClickCardBody(locked);
            string lockedBody = _window.DetailBodyTextForTests;
            string lockedName = _window.DetailNameTextForTests;

            yield return ClickCardBody(owned);
            string ownedBody = _window.DetailBodyTextForTests;
            string ownedName = _window.DetailNameTextForTests;

            Assert.AreNotEqual(lockedBody, ownedBody,
                $"{LogPrefix} 잠긴 아이템과 보유 아이템의 설명문이 같습니다(\"{ownedBody}\") — " +
                "패널이 선택을 따라오지 않으면 ②는 '항상 같은 문장'을 지키는 가짜 초록이 됩니다.");
            Assert.AreNotEqual(lockedName, ownedName,
                $"{LogPrefix} 잠긴 아이템과 보유 아이템의 이름 표시가 같습니다(\"{ownedName}\").");

            Assert.IsTrue(_window.TryGetCardSlotForTests(owned, out EquipmentSlot slot),
                $"{LogPrefix} {owned}번 카드의 슬롯을 읽지 못했습니다.");
            Assert.AreEqual(EquipmentModel.ItemName(slot, _window.CardItemForTests(owned)), ownedName,
                $"{LogPrefix} 보유 아이템의 이름이 상세 패널과 다릅니다.");

            // 버튼이 사라진 뒤에도 패널은 여전히 읽기 전용이어야 한다.
            Assert.AreEqual(0, _window.DetailPanelButtonCountForTests,
                $"{LogPrefix} 아이템을 고르는 사이에 상세 패널에 버튼이 생겼습니다.");

            Debug.Log($"{LogPrefix} 선택을 옮기면 설명이 따라옵니다: \"{lockedBody}\" -> \"{ownedBody}\"");
        }

        // ==================== 도구 ====================

        /// <summary>지금 화면에서 <b>온전히 보이는</b>(마스크에 안 잘린) 카드 중 보유/잠김이 맞는 첫 카드.
        /// 잘린 카드를 고르면 클릭이 도착하지 않는다 — "보이지 않는 것은 눌리지 않는다"가 이 창의 규칙이다.</summary>
        private int FindFullyVisibleCard(bool owned)
        {
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;

                Rect raw = _window.CardRawScreenRect(i);
                Rect visible = _window.CardVisibleScreenRect(i);
                if (raw.width <= 1f || visible.width < raw.width - 1f || visible.height < raw.height - 1f) continue;

                if (!_window.TryGetCardSlotForTests(i, out EquipmentSlot slot)) continue;
                if (EquipmentModel.IsItemOwned(slot, _window.CardItemForTests(i)) != owned) continue;
                return i;
            }
            return -1;
        }

        /// <summary>카드 <b>본체</b>를 눌러 선택을 옮긴다(착용은 하단 버튼이 한다). 하단 버튼 자리를
        /// 피해 카드 위쪽(썸네일)을 누른다 — 중심은 이름줄 근처라 버튼과 가깝다.</summary>
        private IEnumerator ClickCardBody(int cardIndex)
        {
            // 같은 손잡이를 연달아 누를 때는 중복 방지 창(0.35초)을 넘겨야 한다 — 그 창은 버그가 아니라
            // 설계다(한 번의 물리 클릭이 세 경로로 도착한다). 벽시계 기준이다(CLAUDE.md).
            yield return new WaitForSecondsRealtime(0.4f);

            Rect card = _window.CardRawScreenRect(cardIndex);
            Assert.Greater(card.width, 1f, $"{LogPrefix} {cardIndex}번 카드의 사각형이 비었습니다.");

            // 화면 y는 위가 양수 — 카드 위쪽 1/4 지점이 썸네일이고, 하단 버튼에서 가장 멀다.
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

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            _config = agent != null ? agent.Config : null;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig를 찾지 못했습니다.");

            _window.Open("상세 패널 테스트");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }
    }
}
