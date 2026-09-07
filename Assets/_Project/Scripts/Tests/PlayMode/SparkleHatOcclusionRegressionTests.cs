using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 2026-09-07 2차 실기(Windows) 실시간 사용자 신고 회귀 잠금: <i>"반짝임도 머리 뒤에있는거
    /// 같음 아예 안보임 캐릭터 바깥쪽에 있어야하는데"</i>.
    ///
    /// ============================================================================
    /// 왜 바로 앞 라운드가 "안전하다"고 잘못 판정했는가 (Tests/PlayMode/EffectOffsetsClearBodyAuditTests.cs)
    /// ============================================================================
    /// 그 판정문 원문: <i>"발생 높이가 머리 중심 위 2.0R로 정수리(1.0R)보다도 훨씬 위라, 가로 폭이
    /// 아무리 넓어도 그 고도에는 몸/머리 실루엣이 없다"</i>. 이 문장은 세 가지를 놓쳤다:
    ///  (1) 기준을 <b>맨머리 정수리(1.0R)</b>로만 잡았다 — 실제로 겹칠 수 있는 것은 <b>모자</b>이고,
    ///      HEAD 슬롯 6종을 <see cref="CharacterAccessoryRenderer.TryMeasureItemBounds"/>로 전수
    ///      실측하면 최고점이 <b>2.5425R</b>다(정수리의 2.5배 이상). 죽은 v1 공식
    ///      <see cref="AccessoryShapeBuilder.HatTopLocalY"/>가 말하는 1.608R조차 인계본 조각으로
    ///      이전된 모자에는 더 이상 적용되지 않는다(그 함수 문서 참고) — 실측만이 정답이다.
    ///  (2) "2.0R"는 별의 <b>중심</b>일 뿐이다. 별 자신이 아래로
    ///      <see cref="AppearanceShapeBuilder.SparkleArmInR"/>(1.00R)만큼 더 뻗는다는 사실을 빼서,
    ///      실제 하단(1.0R)이 정수리와 <b>정확히 같은 높이</b>(여유 0)인데도 "훨씬 위"라고 오독했다.
    ///  (3) sortingOrder를 <b>아예 확인하지 않았다.</b> 반짝임의 <c>SortAerial</c>(6)은
    ///      <see cref="AccessoryShapeBuilder.SortHead"/>(모자, 10)보다 <b>확실히 작다</b> — 이 저장소는
    ///      sortingLayerName을 안 쓰므로(전부 "Default") sortingOrder 하나가 앞뒤를 정하는 유일한
    ///      근거고, 겹치는 화면 영역에서는 <b>무조건</b> 모자가 이긴다(y좌표가 아무리 위여도 무관).
    ///
    /// ============================================================================
    /// 고침 (CharacterFxRenderer.cs)
    /// ============================================================================
    ///  · <c>SparkleHeightInR</c> 2.0 → <b>4.1</b>(별 하단이 실측 최고점 2.5425R의 1.2배 위로 나오도록 역산).
    ///  · sortingOrder를 공유 <c>SortAerial</c>(6)에서 전용 <c>SortSparkle</c>(=SortHead+1, 모자보다
    ///    확실히 위)로 분리. Dust/Bubble/Leaf는 몸/모자와 겹칠 고도에 있지 않음이 이미 확인돼
    ///    <c>SortAerial</c>을 그대로 둔다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 풍선/물방울 회귀 테스트와 같은 형식)
    /// ============================================================================
    ///  P1  모자(Head)와 반짝임(Fx)을 <b>동시에</b> 걸친 실제 씬에서 반짝임이 실제로 발동하면, 그
    ///      LineRenderer의 sortingOrder가 모자 LineRenderer의 sortingOrder보다 <b>확실히 크다</b>.
    ///  P1n (네거티브) 신고 당시 값(SortAerial=6)으로는 실측 모자 sortingOrder 이하였다.
    ///  P2  반짝임 별의 <b>실제 하단</b>(중심 − 팔 길이)이, HEAD 슬롯 <b>전 종목 실측</b> 중 가장 높은
    ///      지점의 1.2배보다 위다.
    ///  P2n (네거티브) 신고 당시 값(SparkleHeightInR=2.0)으로는 그 조건이 성립하지 않는다.
    /// </summary>
    public sealed class SparkleHatOcclusionRegressionTests
    {
        private const string LogPrefix = "[FX-반짝임-모자가림]";

        /// <summary>FX 카테고리의 "반짝임" 자리 — AppearanceShapeBuilder.FxSparkle과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(다른 FX/PET 회귀 테스트와 같은 관례).</summary>
        private const int FxSparkleItem = 2;

        /// <summary>HEAD 카테고리의 "천 모자" 자리 — 요구 레벨이 없어(기본 해금) 별도 RaiseLevelTo가
        /// 필요 없다. P1에서 "아무 모자나 하나"를 걸치는 용도일 뿐, P2는 6종을 전수 실측하므로
        /// 이 값에 의존하지 않는다.</summary>
        private const int HeadCapItem = 0;

        /// <summary>Resources/Items/look_fx_sparkle.asset의 requiredLevel 사본.</summary>
        private const int SparkleRequiredLevel = 12;

        /// <summary>신고 당시 실제 프로덕션 값이었던 SparkleHeightInR의 리터럴 사본 — 네거티브 컨트롤
        /// 전용. <b>다시 프로덕션 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는 대조군이다.</summary>
        private const float PreFixSparkleHeightInR = 2.0f;

        /// <summary>요구 여유 배수 — 물방울/나뭇잎/종이비행기 회귀 테스트와 같은 관례(장애물 치수×1.2).</summary>
        private const float RequiredMargin = 1.2f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentModel.TryWear(EquipmentSlot.Fx, EquipmentModel.NotWorn, null);
            EquipmentModel.TryWear(EquipmentSlot.Head, EquipmentModel.NotWorn, null);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 반짝임을_모자와_함께_걸치면_반짝임이_모자보다_확실히_앞이다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Agent();
            RaiseLevelTo(SparkleRequiredLevel, agent.Config);

            Assert.IsTrue(Wear(EquipmentSlot.Head, HeadCapItem),
                $"{LogPrefix} 천 모자를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxSparkleItem),
                $"{LogPrefix} 반짝임 FX를 걸치지 못했습니다({SparkleRequiredLevel}레벨을 만들었는데도 " +
                "실패) — 관측 전제가 성립하지 않습니다.");

            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            // 반짝임은 Idle 창에서 유도된 시간(armSeconds, 최대 3초)이 지나야 처음 발동한다
            // (SparkleCadence.Resolve) — 벽시계 기준으로 충분히 길게 기다린다(CLAUDE.md, 프레임 수 금지).
            bool sawSparkle = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (fx.LiveEffectCount > 0) { sawSparkle = true; break; }
                yield return null;
            }
            Assert.IsTrue(sawSparkle,
                $"{LogPrefix} 관측 창(12초) 안에 반짝임이 한 번도 발동하지 않았습니다 — 발동 전제가 " +
                "성립하지 않습니다.");

            Assert.IsTrue(TryReadMinSortingOrder("CharacterFx", out int sparkleOrder),
                $"{LogPrefix} 반짝임 LineRenderer를 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsTrue(TryReadMaxSortingOrder("EquipmentAccessories", out int hatOrder),
                $"{LogPrefix} 모자 LineRenderer를 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            Debug.Log($"{LogPrefix} 실측 — 반짝임 sortingOrder(최소) {sparkleOrder}, " +
                $"모자 sortingOrder(최대) {hatOrder}.");

            // P1 — 절대 조건: 반짝임이 모자보다 확실히 앞(동률도 불허).
            Assert.Greater(sparkleOrder, hatOrder,
                $"{LogPrefix} 반짝임 sortingOrder({sparkleOrder})가 모자 sortingOrder({hatOrder})보다 " +
                "크지 않습니다 — 동률이거나 뒤이면 반짝임이 모자/머리 뒤로 가려질 수 있습니다" +
                "(신고 재현: \"반짝임도 머리 뒤에있는거 같음 아예 안보임\").");

            // P1n — 네거티브 컨트롤: 신고 당시 값(SortAerial=6)은 실측 모자 sortingOrder 이하였다.
            const int preFixSortAerial = 6;
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 값({preFixSortAerial})과 실측 모자 " +
                $"sortingOrder({hatOrder}) 비교 — " +
                $"{(preFixSortAerial <= hatOrder ? "동률 이하(신고 재현)" : "크다(대조 실패)")}.");
            Assert.LessOrEqual(preFixSortAerial, hatOrder,
                $"{LogPrefix} 신고 당시 값({preFixSortAerial})이 실측 모자 sortingOrder({hatOrder})보다 " +
                "이미 큽니다 — 이 테스트가 실제 신고(가려짐)를 반증하지 못합니다(무의미한 대조).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 반짝임_실제_하단이_모든_모자의_실측_최고점보다_충분히_위에_있다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Agent();
            var metrics = agent.GetComponent<StickmanMetrics>();
            var accessory = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");
            Assert.IsNotNull(accessory, $"{LogPrefix} CharacterAccessoryRenderer가 씬에 없습니다.");
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            // HEAD 슬롯 전 종목을 실제 렌더 경로(AccessoryShapeBuilder.Append)로 구워 실측한다 —
            // 인계본으로 이전된 모자에는 죽은 v1 공식(HatTopLocalY)이 안 통하므로 반드시 실측해야
            // 한다(이 파일 클래스 문서 참고).
            int headCount = ItemCatalog.ItemCountIn(EquipmentSlot.Head);
            Assert.Greater(headCount, 0, $"{LogPrefix} HEAD 슬롯에 아이템이 없습니다 — 관측 전제가 " +
                "성립하지 않습니다.");

            float worstHatTopAboveHeadCenter = float.NegativeInfinity;
            int worstIndex = -1;
            for (int i = 0; i < headCount; i++)
            {
                bool ok = accessory.TryMeasureItemBounds(EquipmentSlot.Head, i, out _, out Vector2 max);
                Assert.IsTrue(ok, $"{LogPrefix} HEAD[{i}]의 도형을 굽지 못했습니다.");
                float aboveHeadCenter = max.y - metrics.HeadCenterLocalY;
                Debug.Log($"{LogPrefix} HEAD[{i}] 최고점 = 머리 중심 위 {aboveHeadCenter / metrics.HeadRadius:F4}R.");
                if (aboveHeadCenter > worstHatTopAboveHeadCenter)
                {
                    worstHatTopAboveHeadCenter = aboveHeadCenter;
                    worstIndex = i;
                }
            }

            float sparkleLowest = fx.SparkleLowestPointAboveHeadCenter;
            float required = worstHatTopAboveHeadCenter * RequiredMargin;

            Debug.Log($"{LogPrefix} 최고 모자 = HEAD[{worstIndex}], 머리 중심 위 " +
                $"{worstHatTopAboveHeadCenter / metrics.HeadRadius:F4}R. 반짝임 실제 하단 = 머리 중심 위 " +
                $"{sparkleLowest / metrics.HeadRadius:F4}R(요구 여유 {required / metrics.HeadRadius:F4}R " +
                $"= 최고 모자×{RequiredMargin:F1}).");

            // P2 — 절대 조건.
            Assert.Greater(sparkleLowest, required,
                $"{LogPrefix} 반짝임 실제 하단({sparkleLowest:F4})이 최고 모자({worstHatTopAboveHeadCenter:F4})" +
                $"의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 모자와 겹쳐서 가려질 수 있습니다.");

            // P2n — 네거티브 컨트롤: 신고 당시 값(SparkleHeightInR=2.0)으로는 성립하지 않았다.
            // 팔 길이(SparkleArmWorld)는 이번에 안 바뀐 값이라 지금 노출된 프로퍼티를 그대로 재사용한다.
            float preFixLowest = metrics.HeadRadius * PreFixSparkleHeightInR - fx.SparkleArmWorld;
            Debug.Log($"{LogPrefix} [네거티브] 옛 값(2.0R)의 반짝임 하단 {preFixLowest / metrics.HeadRadius:F4}R " +
                $"— 최고 모자({worstHatTopAboveHeadCenter / metrics.HeadRadius:F4}R)보다 " +
                $"{(preFixLowest < worstHatTopAboveHeadCenter ? "작다(겹침 재현)" : "크다(대조 실패)")}.");
            Assert.Less(preFixLowest, worstHatTopAboveHeadCenter,
                $"{LogPrefix} 옛 값(2.0R)의 반짝임 하단({preFixLowest:F4})이 최고 모자" +
                $"({worstHatTopAboveHeadCenter:F4})보다 작지 않습니다 — 이 테스트가 실제 결함(모자와 " +
                "겹침)을 반증하지 못합니다(무의미한 대조).");
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
        }

        /// <summary>"이 아이템을 걸친 상태로 만든다" — <see cref="EquipmentModel.TryWear"/>는 이미
        /// 걸치고 있으면 false를 돌려주므로(기본 차림일 수 있다), 검사할 것은 반환값이 아니라
        /// <b>지금 그것을 걸치고 있는가</b>다(PetBalloonHatSortOrderTests.Wear와 같은 관례).</summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        private static bool TryReadMinSortingOrder(string rootName, out int order)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null) { order = 0; return false; }
            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            if (lines.Length == 0) { order = 0; return false; }
            order = int.MaxValue;
            for (int i = 0; i < lines.Length; i++) order = Mathf.Min(order, lines[i].sortingOrder);
            return true;
        }

        private static bool TryReadMaxSortingOrder(string rootName, out int order)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null) { order = 0; return false; }
            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            if (lines.Length == 0) { order = 0; return false; }
            order = int.MinValue;
            for (int i = 0; i < lines.Length; i++) order = Mathf.Max(order, lines[i].sortingOrder);
            return true;
        }

        /// <summary>목표 레벨까지 정상 경로(AddXp)로 올린다(다른 FX/PET 회귀 테스트와 같은 관례).</summary>
        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
        }

        private sealed class StillIntent : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillIntent();

            float deadline = Time.realtimeSinceStartup + 15f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            float idleSince = -1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= 0.5f) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last, $"{LogPrefix} Idle로 안정되지 않았습니다.");
        }
    }
}
