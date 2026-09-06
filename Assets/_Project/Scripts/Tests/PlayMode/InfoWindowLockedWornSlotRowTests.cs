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
    /// ★★ 2026-09-06 persona-newcomer 실기 신고 회귀 — <b>같은 창이 자기 자신을 반증하고 있었다.</b>
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 정보창 컬럼 1의 <b>착용 슬롯 줄</b>은 <c>WornIndex &gt;= 0</c>만 보고 아이템을 적었다.
    /// 그 바로 옆 <b>액자</b>(<see cref="CharacterPortraitStage"/>)와 실제 <b>몸</b>
    /// (<c>CharacterAccessoryRenderer</c>)은 처음부터 <b>잠금까지</b> 함께 봤다.
    ///
    /// <para>그래서 세이브에 <c>고글</c>(Lv11)·<c>요정 날개</c>(Lv28)가 착용으로 들어 있는 저레벨
    /// 캐릭터에서 <b>슬롯 줄에는 이름·등급·아이콘이 전부 뜨는데 액자에는 아무것도 안 그려졌다</b>.
    /// 화면 한 장이 두 가지 사실을 동시에 주장하면 사용자가 할 수 있는 해석은 "고장"뿐이다.</para>
    ///
    /// <para><b>모델은 옳았다.</b> <c>EquipmentModel.RestoreFromSave</c>가 잠금을 검사하지 않는 것은
    /// 의도된 설계다(검사하면 레벨이 낮게 복원되는 순간 착용물이 조용히 사라진다). 그 문서가
    /// «대신 렌더러/UI가 그릴 때 <see cref="EquipmentModel.IsUnlocked"/>로 함께 본다»고 약속하는데,
    /// <b>슬롯 줄만 그 약속을 안 지키고 있었다</b>.</para>
    ///
    /// ============================================================================
    /// 여기서 잠그는 것 — <b>말한 것 == 그린 것</b>
    /// ============================================================================
    /// 이 파일은 «잠금 술어가 옳게 적혔는가»를 <b>재현하지 않는다</b>(그러면 프로덕션과 테스트가
    /// 같은 식을 두 벌 갖게 되고, 같이 틀리면 아무도 모른다). 대신 <b>두 표면의 관측값</b>을 직접
    /// 맞대 본다:
    /// <list type="bullet">
    ///   <item>슬롯 줄 — <see cref="CharacterInfoWindow.SlotRowNameForTests"/>가 지금 적고 있는 글자.</item>
    ///   <item>액자 — 미니 피규어 아래에 지금 <b>실제로 존재하는 렌더러 수</b>. 술어가 아니라 그림이다.</item>
    /// </list>
    ///
    /// <para>기준선은 <b>같은 슬롯을 정말로 벗겨 놓은 상태</b>다 — 프로덕션 문구("비어 있음")를
    /// 여기 베껴 적지 않기 위해서다(CLAUDE.md: 테스트에 프로덕션 문자열을 베끼지 않는다).</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 무의미하게 초록이 되지 않게
    /// ============================================================================
    /// "둘 다 비어 보인다"는 <b>아무것도 안 걸친 캐릭터</b>에서도 초록이다. 그래서 각 단계가
    /// 대조군을 함께 단언한다:
    /// <list type="number">
    ///   <item>해금된 것을 걸치면 <b>두 눈금이 모두 움직인다</b>(글자가 바뀌고 렌더러가 늘어난다) —
    ///         두 관측이 애초에 무언가를 볼 수 있다는 증명.</item>
    ///   <item>잠긴 것을 걸친 상태에서 <b>모델은 여전히 그것을 들고 있다</b>
    ///         (<see cref="EquipmentModel.WornIndex"/>) — 세이브를 지워서 초록이 된 것이 아님을 증명.</item>
    ///   <item>레벨이 요구치에 닿으면 <b>같은 세이브가 저절로 되살아난다</b> — 잠긴 동안 화면이
    ///         숨겼을 뿐 데이터는 한 글자도 안 없앴다는 증명.</item>
    /// </list>
    ///
    /// <para>QA 해금 스위치(<see cref="EquipmentDebugUnlock"/>)는 에디터/PlayMode에서 항상 켜져 있어
    /// <b>잠긴 아이템이 하나도 없다</b>. 그 상태로는 이 신고를 물어볼 수 없으므로 테스트 동안만 강제로
    /// 끈다(<c>SetTestOverride</c>는 internal이고 PlayMode 어셈블리에는 InternalsVisibleTo가 없어
    /// 리플렉션을 쓴다 — <c>InfoWindowDetailPanelReadOnlyTests</c>가 이미 쓰는 관례다).</para>
    /// </summary>
    public sealed class InfoWindowLockedWornSlotRowTests
    {
        private const string LogPrefix = "[잠긴착용-TEST]";

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
        // ① 잠긴 착용물 — 슬롯 줄이 액자와 <b>같은 말</b>을 한다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LockedWornItemIsNotAnnouncedAsWornInTheSlotRow()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            yield return OpenWindow();

            // 잠금을 실제로 살린다: QA 해금 OFF + 레벨 1.
            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();
            yield return Resync();

            Assert.IsTrue(TryPickProbeSlot(out int row, out EquipmentSlot slot, out int lockedItem, out int ownedItem),
                $"{LogPrefix} QA 해금을 끄고 Lv.{CharacterProgressionModel.Level}인데도 «잠긴 것과 보유한 것이 " +
                "둘 다 있는» [장비] 카테고리가 하나도 없습니다 — 관측 전제가 성립하지 않습니다.");

            string lockedName = EquipmentModel.ItemName(slot, lockedItem);
            string ownedName = EquipmentModel.ItemName(slot, ownedItem);
            int requiredLevel = EquipmentModel.RequiredLevel(slot, lockedItem);

            // ---- 기준선 A: 그 슬롯을 <b>정말로</b> 벗겨 둔 상태 ----
            EquipmentModel.TryWear(slot, EquipmentModel.NotWorn, _config);
            yield return Resync();
            string emptyText = SlotRowText(row);
            int emptyParts = PortraitPartCount();
            Assert.Greater(emptyParts, 0,
                $"{LogPrefix} 액자에 렌더러가 하나도 없습니다 — 관측 전제가 깨졌습니다(그림이 안 그려졌다면 " +
                "무엇이 안 그려지는지 잴 수 없습니다).");

            // ---- 대조군 B: 해금된 것을 걸치면 두 눈금이 모두 움직인다 ----
            Assert.IsTrue(EquipmentModel.TryWear(slot, ownedItem, _config),
                $"{LogPrefix} 보유한 «{ownedName}»을 걸치지 못했습니다 — 대조군을 만들 수 없습니다.");
            yield return Resync();
            string ownedText = SlotRowText(row);
            int ownedParts = PortraitPartCount();

            Assert.AreEqual(ownedName, ownedText,
                $"{LogPrefix} 해금된 «{ownedName}»을 걸쳤는데 슬롯 줄이 \"{ownedText}\"라고 적습니다 — " +
                "정상 경로가 회귀했습니다(이 고침은 잠긴 것만 걸러야 합니다).");
            Assert.AreNotEqual(emptyText, ownedText,
                $"{LogPrefix} 걸치기 전후로 슬롯 줄이 \"{ownedText}\"로 같습니다 — " +
                "이 관측은 착용을 볼 수 없으므로 아래 단언은 아무것도 증명하지 못합니다.");
            Assert.Greater(ownedParts, emptyParts,
                $"{LogPrefix} «{ownedName}»을 걸쳤는데 액자의 렌더러가 {emptyParts} -> {ownedParts}로 " +
                "늘지 않았습니다 — 이 관측은 액세서리를 볼 수 없으므로 아래 단언이 무의미해집니다.");

            // ---- 본 사건 C: 잠긴 것을 걸친 세이브 ----
            //   해금을 잠깐 켜서 «걸친 상태»를 만든 뒤 다시 끈다. 이것이 곧 저장 파일이
            //   앞선 레벨에서 만들어졌다가 낮은 레벨로 복원된 상태다(RestoreFromSave가 만드는 바로 그 상태).
            SetUnlockOverride.Invoke(null, new object[] { true });
            Assert.IsTrue(EquipmentModel.TryWear(slot, lockedItem, _config),
                $"{LogPrefix} 해금을 켠 상태에서도 «{lockedName}»을 걸치지 못했습니다 — 재현을 만들 수 없습니다.");
            SetUnlockOverride.Invoke(null, new object[] { false });
            yield return Resync();

            // 대조군 — 재현이 <b>정말로</b> 만들어졌는가.
            Assert.AreEqual(lockedItem, EquipmentModel.WornIndex(slot),
                $"{LogPrefix} 모델이 «{lockedName}»을 더 이상 들고 있지 않습니다 — 화면이 숨긴 것이 아니라 " +
                "세이브가 지워진 것이라면 이 초록은 거짓입니다(RestoreFromSave 계약 위반).");
            Assert.IsFalse(EquipmentModel.IsItemOwned(slot, lockedItem),
                $"{LogPrefix} Lv.{CharacterProgressionModel.Level}인데 «{lockedName}»(Lv.{requiredLevel})이 " +
                "보유 상태입니다 — 잠금이 살아 있지 않으면 아래 단언은 아무것도 증명하지 못합니다.");

            string lockedText = SlotRowText(row);
            int lockedParts = PortraitPartCount();

            // (1) 액자는 그리지 않는다 — 관측으로 확인한다(술어를 다시 짜지 않는다).
            Assert.AreEqual(emptyParts, lockedParts,
                $"{LogPrefix} 잠긴 «{lockedName}»이 액자에 그려졌습니다(렌더러 {emptyParts} -> {lockedParts}). " +
                "이 테스트의 전제(액자는 잠금까지 본다)가 깨졌습니다.");

            // (2) ★ 고침의 본체 — 슬롯 줄이 그 그림과 <b>같은 말</b>을 한다.
            Assert.AreEqual(emptyText, lockedText,
                $"{LogPrefix} 액자에는 아무것도 안 그려졌는데 슬롯 줄은 \"{lockedText}\"라고 적습니다 " +
                $"(빈 슬롯은 \"{emptyText}\"). 같은 창이 두 가지를 동시에 주장합니다 — 2026-09-06 신고 재발.");
            Assert.AreNotEqual(lockedName, lockedText,
                $"{LogPrefix} 잠긴 «{lockedName}»의 이름이 슬롯 줄에 그대로 떴습니다 " +
                $"(Lv.{requiredLevel} 필요 / 지금 Lv.{CharacterProgressionModel.Level}).");

            Debug.Log($"{LogPrefix} {EquipmentModel.SlotName(slot)} — 빈 슬롯 \"{emptyText}\"/렌더러 {emptyParts}, " +
                $"해금 «{ownedName}» \"{ownedText}\"/{ownedParts}, 잠긴 «{lockedName}»(Lv.{requiredLevel}) " +
                $"\"{lockedText}\"/{lockedParts} — 슬롯 줄과 액자가 일치합니다.");
        }

        // ============================================================================
        // ② 숨긴 것이지 지운 것이 아니다 — 레벨이 닿으면 그 세이브가 되살아난다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator HiddenLockedItemComesBackWhenTheLevelReachesIt()
        {
            Assert.IsNotNull(SetUnlockOverride,
                $"{LogPrefix} EquipmentDebugUnlock.SetTestOverride를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            yield return OpenWindow();

            SetUnlockOverride.Invoke(null, new object[] { false });
            CharacterProgressionModel.ResetForTesting();
            yield return Resync();

            Assert.IsTrue(TryPickProbeSlot(out int row, out EquipmentSlot slot, out int lockedItem, out _),
                $"{LogPrefix} 잠긴 아이템이 있는 [장비] 카테고리를 찾지 못했습니다.");

            string lockedName = EquipmentModel.ItemName(slot, lockedItem);
            int requiredLevel = EquipmentModel.RequiredLevel(slot, lockedItem);

            // 잠긴 것을 걸친 세이브를 만든다(①과 같은 경로).
            SetUnlockOverride.Invoke(null, new object[] { true });
            Assert.IsTrue(EquipmentModel.TryWear(slot, lockedItem, _config),
                $"{LogPrefix} «{lockedName}»을 걸치지 못했습니다.");
            SetUnlockOverride.Invoke(null, new object[] { false });
            yield return Resync();

            string hiddenText = SlotRowText(row);
            int hiddenParts = PortraitPartCount();
            Assert.AreNotEqual(lockedName, hiddenText,
                $"{LogPrefix} 잠긴 «{lockedName}»이 아직 슬롯 줄에 떠 있습니다 — ①이 잡아야 할 상태입니다.");

            // 레벨을 요구치까지 올린다(해금 스위치는 <b>계속 꺼진 채로</b> — 레벨이 유일한 문이다).
            yield return RaiseLevelTo(requiredLevel);

            string revivedText = SlotRowText(row);
            int revivedParts = PortraitPartCount();

            Assert.AreEqual(lockedItem, EquipmentModel.WornIndex(slot),
                $"{LogPrefix} 레벨이 오른 뒤 모델의 착용 자리가 달라졌습니다 — 잠긴 동안 세이브가 바뀌었습니다.");
            Assert.AreEqual(lockedName, revivedText,
                $"{LogPrefix} Lv.{CharacterProgressionModel.Level}(요구 {requiredLevel})인데 슬롯 줄이 " +
                $"\"{revivedText}\"입니다 — 숨긴 것이 아니라 지운 것이 됐습니다.");
            Assert.Greater(revivedParts, hiddenParts,
                $"{LogPrefix} 레벨이 닿았는데 액자의 렌더러가 {hiddenParts} -> {revivedParts}로 늘지 않았습니다 — " +
                "슬롯 줄만 되살아나고 그림은 그대로면 다시 두 가지를 주장하는 것입니다.");

            Debug.Log($"{LogPrefix} «{lockedName}»(Lv.{requiredLevel}) — Lv.1에서 \"{hiddenText}\"/{hiddenParts}, " +
                $"Lv.{CharacterProgressionModel.Level}에서 \"{revivedText}\"/{revivedParts}로 되살아났습니다.");
        }

        // ==================== 도구 ====================

        /// <summary>슬롯 줄과 액자를 <b>둘 다</b> 지금 상태로 맞춘다.
        /// <para>QA 해금 스위치를 뒤집는 것만으로는 창이 다시 그리지 않는다 — 그 스위치는 이벤트를
        /// 흘리지 않기 때문이다. 저장 복원 경로도 마지막에 이 통지를 한 번 흘리므로
        /// (<c>CharacterSaveStore</c>), 여기서 같은 통지를 쓰는 것이 곧 <b>같은 경로</b>다.
        /// 액자는 서명에 해금 마스크가 들어 있어 다음 <c>Update</c>에서 스스로 다시 굽는다.</para>
        ///
        /// <para>★ <b>세 프레임을 기다리는 이유는 <c>Destroy</c>가 지연되기 때문이다.</b> 액자의
        /// 재구성은 옛 선/면을 <c>Destroy</c>로 예약만 하고 그 프레임 끝에 실제로 지워진다. 재구성이
        /// 도는 <b>그 프레임 안에서</b> 렌더러를 세면 <b>옛 것과 새 것이 함께</b> 잡혀, 아무것도
        /// 안 바뀌었는데 수가 두 배로 보인다. 프레임 수가 아니라 <b>사건 순서</b>에 매인 대기라
        /// 벽시계 규약(CLAUDE.md)의 대상이 아니다 — 여기서 재는 것은 시간이 아니다.</para></summary>
        private IEnumerator Resync()
        {
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            yield return null;   // 이 프레임의 Update가 다시 굽고, 옛 것은 프레임 끝에 지워진다.
            yield return null;   // 지워진 뒤.
            yield return null;   // 여유 한 장 — 재구성이 한 번 더 연쇄돼도 안전하게.
        }

        /// <summary>미니 피규어 아래에 지금 <b>실제로 존재하는</b> 렌더러 수 = 액자가 그린 양.
        /// <para>술어가 아니라 <b>그림</b>을 센다. 프로덕션 함수로 기대값을 만들면 그 함수가 틀어질 때
        /// 기대값도 함께 틀어져 아무것도 못 잰다(TestApi 조각의 리본 절과 같은 이유).</para></summary>
        private int PortraitPartCount()
        {
            CharacterPortraitStage stage = _window.PortraitStageForTests;
            Assert.IsNotNull(stage, $"{LogPrefix} 정보창이 초상화 촬영장을 갖고 있지 않습니다.");
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            return figure.GetComponentsInChildren<Renderer>(true).Length;
        }

        private string SlotRowText(int row)
        {
            string text = _window.SlotRowNameForTests(row);
            Assert.IsNotNull(text, $"{LogPrefix} {row}번 슬롯 줄의 이름 상자를 읽지 못했습니다.");
            return text;
        }

        /// <summary>지금 화면의 [장비] 슬롯 줄 중 «잠긴 것»과 «보유한 것»이 <b>둘 다</b> 있는 첫 줄.
        /// <para>슬롯 번호도 아이템 번호도 <b>베끼지 않는다</b> — 카탈로그의 요구 레벨이 바뀌거나
        /// 카테고리가 은퇴해도 이 함수는 그대로 옳다. 행 번호 → 카테고리 규칙도 창에게 묻는다.</para></summary>
        private bool TryPickProbeSlot(out int row, out EquipmentSlot slot, out int lockedItem, out int ownedItem)
        {
            for (int i = 0; i < _window.SlotRowCountForTests; i++)
            {
                if (!_window.TryGetSlotRowSlotForTests(i, out EquipmentSlot candidate)) continue;

                int locked = -1, owned = -1;
                int count = EquipmentModel.ItemCount(candidate);
                for (int k = 0; k < count; k++)
                {
                    if (EquipmentModel.IsItemOwned(candidate, k)) { if (owned < 0) owned = k; }
                    else if (locked < 0) locked = k;
                }
                if (locked < 0 || owned < 0) continue;

                row = i;
                slot = candidate;
                lockedItem = locked;
                ownedItem = owned;
                return true;
            }

            row = -1;
            slot = default;
            lockedItem = -1;
            ownedItem = -1;
            return false;
        }

        /// <summary>레벨을 <paramref name="level"/>까지 올린다(다른 PlayMode 픽스처와 같은 관례).
        /// 성장 통지가 슬롯 줄과 액자를 함께 다시 그린다.</summary>
        private IEnumerator RaiseLevelTo(int level)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(_config) + 1f, _config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨을 {level}까지 올리지 못했습니다(지금 {CharacterProgressionModel.Level}).");
            // 액자는 이벤트가 아니라 <b>서명</b>(해금 마스크 포함)으로 스스로 따라온다 —
            // 그래도 지연 Destroy 때문에 Resync와 같은 만큼 기다린다.
            yield return null;
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

            _window.Open("잠긴 착용물 테스트");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }
    }
}
