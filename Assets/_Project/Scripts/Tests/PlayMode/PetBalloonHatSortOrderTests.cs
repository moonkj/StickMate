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
    /// ★★ 실기(Windows) 실시간 사용자 신고 회귀 잠금(2026-09-07): <i>"풍선이 너무 캐릭터 뒤에 있음
    /// 캐릭터 주변에 있어야 하는데"</i>.
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님, 위치가 아니라 <b>정렬 순서 동률</b>이다)
    /// ============================================================================
    /// 풍선(및 종이비행기의 "앞" 반주기)의 sortingOrder는 옛 값이 <b>리터럴 10</b>이었다. 그런데
    /// <c>AccessoryShapeBuilder.SortHead</c>(모자류 sortingOrder)도 <b>똑같이 10</b>이다. 이 저장소
    /// 어디에도 <c>sortingLayerName</c>을 따로 지정하는 곳이 없어(다 "Default" 레이어) sortingOrder
    /// 정수 하나가 앞뒤를 가르는 유일한 근거인데, <b>동률이면 Unity가 어느 쪽이 위인지 정의하지
    /// 않는다</b> — 실제로 겹치는 구간이 있으면(풍선 매듭~끈 하단이 모자 챙~꼭대기 높이와 겹친다,
    /// <c>CharacterPetRenderer.SortPlaneFront</c> 문서의 기하 검산 참고) 그 구간에서 풍선이 모자/머리
    /// 뒤로 가려질 수 있다 — 신고 문구("캐릭터 뒤에 있음")의 정확한 재현이다.
    ///
    /// 고침은 <c>AccessoryShapeBuilder.SortHead + 1</c>로 유도한 값을 쓰는 것이다 — 매직넘버를 새로
    /// 박지 않고 모자 상수에서 유도했으므로 모자 레이어가 바뀌어도 동률이 재발하지 않는다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤)
    /// ============================================================================
    ///  P1  모자(Head)와 풍선(Pet)을 <b>동시에</b> 걸친 실제 씬에서, 풍선의 실제 LineRenderer
    ///      sortingOrder가 모자의 실제 LineRenderer sortingOrder보다 <b>확실히 크다</b>(동률도 불허).
    ///  P1n (네거티브) 신고 당시의 옛 리터럴(10)로는 실제로 측정된 모자 sortingOrder와 <b>동률</b>이었다
    ///      — 이 값을 그대로 되돌리면 이 테스트가 그 사실을 잡는다는 것을 같은 파일에서 증명한다.
    ///  P2  종이비행기의 "앞" 레이어도 같은 상수를 쓰므로 같은 조건을 만족한다(풍선만 고치고 종이
    ///      비행기를 빠뜨리는 회귀를 막는다 — 오늘 밤 궤도가 몸통까지 넓어지면서 이 위험이 커졌다).
    /// </summary>
    public sealed class PetBalloonHatSortOrderTests
    {
        private const string LogPrefix = "[풍선-모자-정렬]";

        /// <summary>PET 카테고리의 "풍선" 자리 — AppearanceShapeBuilder.PetBalloon과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(PetPlaneOrbitRadiusTests와 같은 관례).</summary>
        private const int PetBalloonItem = 4;

        /// <summary>PET 카테고리의 "종이비행기" 자리 — PetPlaneOrbitRadiusTests.PetPlaneItem과 같은 값.</summary>
        private const int PetPlaneItem = 1;

        /// <summary>HEAD 카테고리의 "천 모자" 자리 — CharacterAppearanceLayerTests.Cap과 같은 값.
        /// 요구 레벨이 없어(기본 해금) 별도 RaiseLevelTo가 필요 없다.</summary>
        private const int HeadCapItem = 0;

        /// <summary>Resources/Items/look_pet_balloon.asset의 requiredLevel 사본.</summary>
        private const int BalloonRequiredLevel = 27;

        /// <summary>Resources/Items/look_pet_plane.asset의 requiredLevel 사본.</summary>
        private const int PlaneRequiredLevel = 13;

        /// <summary>신고 당시 실제 프로덕션 값이었던 풍선/종이비행기 "앞" sortingOrder의 리터럴 사본
        /// — 네거티브 컨트롤 전용. <b>다시 프로덕션 값으로 되돌리지 마라</b> — 이 테스트가 그 값을
        /// 반증하는 대조군이다.</summary>
        private const int PreFixSortPlaneFrontOrBalloon = 10;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            EquipmentModel.TryWear(EquipmentSlot.Head, EquipmentModel.NotWorn, null);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 풍선을_모자와_함께_걸치면_풍선이_모자보다_확실히_앞이다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Agent();
            RaiseLevelTo(BalloonRequiredLevel, agent.Config);

            Assert.IsTrue(Wear(EquipmentSlot.Head, HeadCapItem),
                $"{LogPrefix} 천 모자를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetBalloonItem),
                $"{LogPrefix} 풍선을 걸치지 못했습니다({BalloonRequiredLevel}레벨을 만들었는데도 실패) — " +
                "관측 전제가 성립하지 않습니다.");

            for (int i = 0; i < 5; i++) yield return null;

            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");
            Assert.AreEqual(PetBalloonItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 풍선으로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            Assert.IsTrue(TryReadMinSortingOrder("CharacterPet", out int balloonOrder),
                $"{LogPrefix} 풍선 LineRenderer를 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsTrue(TryReadMaxSortingOrder(HatContainer(), out int hatOrder),
                $"{LogPrefix} 모자 LineRenderer를 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            Debug.Log($"{LogPrefix} 실측 — 풍선 sortingOrder(최소) {balloonOrder}, " +
                $"모자 sortingOrder(최대) {hatOrder}.");

            // P1 — 절대 조건: 풍선이 모자보다 확실히 앞(동률도 불허).
            Assert.Greater(balloonOrder, hatOrder,
                $"{LogPrefix} 풍선 sortingOrder({balloonOrder})가 모자 sortingOrder({hatOrder})보다 " +
                "크지 않습니다 — 동률이거나 뒤이면 풍선이 모자/머리 뒤로 가려질 수 있습니다" +
                "(신고 재현: \"풍선이 너무 캐릭터 뒤에 있음\").");

            // P1n — 네거티브 컨트롤: 신고 당시 리터럴(10)로는 실제 모자 sortingOrder와 동률이었다.
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 리터럴({PreFixSortPlaneFrontOrBalloon})과 " +
                $"실측 모자 sortingOrder({hatOrder}) 비교 — " +
                $"{(PreFixSortPlaneFrontOrBalloon <= hatOrder ? "동률 이하(신고 재현)" : "크다(대조 실패)")}.");
            Assert.LessOrEqual(PreFixSortPlaneFrontOrBalloon, hatOrder,
                $"{LogPrefix} 신고 당시 리터럴({PreFixSortPlaneFrontOrBalloon})이 실측 모자 sortingOrder" +
                $"({hatOrder})보다 이미 큽니다 — 이 테스트가 실제 신고(동률로 인한 가려짐)를 " +
                "반증하지 못합니다(무의미한 대조).");
        }

        /// <summary>
        /// P2 — 종이비행기도 같은 "앞" 레이어(<c>SortPlaneFront</c>)를 쓰므로 같은 보호가 있어야 한다.
        /// 오늘 밤 궤도가 머리 위부터 몸통까지 넓어지면서(<c>PlaneCenterAboveHeadInR</c>가 음수로
        /// 내려옴) 궤도의 "앞" 절반이 모자 높이 부근을 지나는 비중이 커졌다 — 풍선만 고치고
        /// 종이비행기를 빠뜨리면 이 라운드가 정확히 놓친 회귀가 된다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 종이비행기_앞레이어도_모자와_함께_걸치면_모자보다_확실히_앞이다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Agent();
            RaiseLevelTo(PlaneRequiredLevel, agent.Config);

            Assert.IsTrue(Wear(EquipmentSlot.Head, HeadCapItem),
                $"{LogPrefix} 천 모자를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetPlaneItem),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            for (int i = 0; i < 5; i++) yield return null;

            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");
            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            Assert.IsTrue(TryReadMaxSortingOrder(HatContainer(), out int hatOrder),
                $"{LogPrefix} 모자 LineRenderer를 찾지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            // 종이비행기는 반주기마다 4 <-> SortPlaneFront를 오간다(설계 그대로). 여러 프레임을 관찰해
            // "앞" 반주기(4보다 큰 값)를 실제로 잡는다 — 벽시계 기준(프레임 수 대기 금지, CLAUDE.md).
            int maxObservedOrder = int.MinValue;
            float deadline = Time.realtimeSinceStartup + 4.0f; // 궤도 주기(3.2초)보다 길게.
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryReadMaxSortingOrder("CharacterPet", out int order) && order > maxObservedOrder)
                {
                    maxObservedOrder = order;
                }
                yield return null;
            }

            Debug.Log($"{LogPrefix} 실측 — 종이비행기 최대 관측 sortingOrder {maxObservedOrder}, " +
                $"모자 sortingOrder(최대) {hatOrder}.");

            Assert.Greater(maxObservedOrder, hatOrder,
                $"{LogPrefix} 종이비행기의 \"앞\" sortingOrder({maxObservedOrder})가 모자({hatOrder})보다 " +
                "크지 않습니다 — 궤도의 앞 반주기에서 모자/머리 뒤로 가려질 수 있습니다.");
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
        }

        /// <summary>
        /// "이 아이템을 걸친 상태로 만든다". <see cref="EquipmentModel.TryWear"/>를 그냥 쓰면 안 된다 —
        /// <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다(기본 차림이 천 모자일
        /// 수 있다 — CharacterAppearanceLayerTests.Wear와 같은 이유·같은 관례). 검사해야 할 것은
        /// "TryWear가 true를 돌려줬는가"가 아니라 <b>지금 그것을 걸치고 있는가</b>다.
        /// </summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        /// <summary>모자 등 액세서리가 그려지는 컨테이너 이름 — CharacterAppearanceLayerTests의
        /// AccessoryLineNames와 같은 자리("EquipmentAccessories", CharacterAccessoryRenderer의 직속 자식).</summary>
        private static string HatContainer() => "EquipmentAccessories";

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

        /// <summary>목표 레벨까지 정상 경로(AddXp)로 올린다(CharacterAppearanceLayerTests와 같은 관례).</summary>
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
