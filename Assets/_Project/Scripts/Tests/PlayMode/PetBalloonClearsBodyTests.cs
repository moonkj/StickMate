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
    /// ★★★ 실기 신고 회귀 잠금(2026-09-07, 같은 밤 3차) — <i>"풍선도 캐릭터가 멈춰있을때
    /// 캐릭터와 겹치지 않고 바깥쪽에 있어야하는데 여전히 겹쳐져있음"</i>.
    ///
    /// ============================================================================
    /// 배경 — 왜 앞선 수정(SortHead+1)으로 안 끝났는가
    /// ============================================================================
    /// 같은 밤 먼저 <see cref="PetBalloonHatSortOrderTests"/>가 "풍선이 모자 뒤로 간다"는 신고를
    /// <b>정렬 순서 동률</b>(SortBalloon == SortHead == 10)로 확정하고 SortHead+1로 고쳤다. 그런데
    /// 그 수정은 <b>그리기 순서</b>만 바꿀 뿐 <b>좌표</b>는 건드리지 않는다 — 풍선의 실제 (x,y)가
    /// 애초에 몸 실루엣 안쪽이면 순서를 아무리 앞으로 당겨도 여전히 겹쳐 보인다. 이 파일은 좌표
    /// 자체를 검산한다.
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님, 오늘 밤 이미 네 번째 같은 패턴)
    /// ============================================================================
    /// 옛 <c>CharacterPetRenderer.BalloonTetherBehindInR</c> = 0.75R이었다. 그런데 풍선 주머니
    /// (<see cref="AppearanceShapeBuilder.BalloonBody"/>)는 매듭과 <b>같은 로컬 x</b>를 중심으로 한
    /// 대칭 원이라 반경(<see cref="AppearanceShapeBuilder.BalloonRadiusInR"/>=0.80R)만큼 몸 쪽으로
    /// 파고든다. 즉 Idle(기울임 0·기울기 0)에서 주머니의 몸쪽 안쪽 가장자리는 머리 중심에서
    /// <c>(0.75 − 0.80)R = −0.05R</c> — <b>머리 중심을 이미 지나쳐 반대쪽까지 파고들어 있었다.</b>
    /// 매듭 자체만 봐도 머리 중심에서의 거리가 <c>sqrt(0.75² + 0.30²)R ≈ 0.807R</c>로 머리 반경(1R)
    /// 보다 작아 <b>매듭이 머리 원 안쪽</b>이었다 — 몸통 물리 반폭(1.8182R)과 비교할 것도 없이 머리
    /// 자체와도 겹쳐 있었다.
    ///
    /// 종이비행기 궤도(2회) · 나뭇잎 스폰폭 · 물방울 스폰편차와 <b>똑같은 결함 유형</b>이다 —
    /// 위치 상수가 캐릭터 몸 실측 치수보다 작아서 몸 밖으로 못 나간다.
    ///
    /// 고침은 나뭇잎 수정과 <b>같은 검산 방식</b>이다: "최악 편차(오프셋 − 안쪽으로 파고드는 최대량)가
    /// 몸통 물리 반폭×1.2를 넘는가". 오프셋을 0.75R → 3.5R로 올렸다(나뭇잎이 이미 3.5R로 검증됐으므로
    /// 같은 값을 재사용 — 매직넘버를 새로 고르지 않는다).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  공식으로 유도한 "최악 편차"(오프셋 − 주머니 반경)가 몸통 물리 반폭보다 20% 이상
    ///      여유를 두고 크다.
    ///  P1n (네거티브) 신고 당시의 옛 상수(0.75R)로 같은 식을 계산하면 값이 <b>음수</b>다 — 겹침이
    ///      아니라 관통이었다는 것을 같은 파일에서 증명한다.
    ///  P2  <b>실제로 그려지는</b> 풍선 매듭 위치(<see cref="CharacterPetRenderer.PetWorldPosition"/>)를
    ///      Idle에서 실측한 몸통 중심 대비 편차가 공식값과 일치하고, 몸통 물리 반폭보다 크다 —
    ///      "공식은 맞는데 실제로 그려지는 것은 다르다"는 별개의 실패 유형을 반증한다.
    /// </summary>
    public sealed class PetBalloonClearsBodyTests
    {
        private const string LogPrefix = "[PET-풍선-몸통이탈]";

        /// <summary>PET 카테고리의 "풍선" 자리 — AppearanceShapeBuilder.PetBalloon과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(PetBalloonHatSortOrderTests와
        /// 같은 관례).</summary>
        private const int PetBalloonItem = 4;

        /// <summary>Resources/Items/look_pet_balloon.asset의 requiredLevel 사본
        /// (PetBalloonHatSortOrderTests.BalloonRequiredLevel과 같은 값).</summary>
        private const int BalloonRequiredLevel = 27;

        /// <summary>CharacterPetRenderer.BalloonTetherBehindInR의 사본(2026-09-07 3차 수정 값) —
        /// private라 테스트 어셈블리가 직접 참조할 수 없어 복제한다(LeafFallClearsBodyTests와 같은
        /// 관례, CLAUDE.md 협업 프로토콜의 "internal/private 상수 복제" 예외).</summary>
        private const float BalloonTetherBehindInR = 3.5f;

        /// <summary>AppearanceShapeBuilder.BalloonRadiusInR의 사본(internal이라 복제) — 풍선 주머니가
        /// 매듭에서 몸 쪽으로 파고드는 최대량(머리 반경 배수).</summary>
        private const float BalloonRadiusInR = 0.80f;

        /// <summary>신고 당시(3차 수정 전) 묶인 자리 오프셋 — 네거티브 컨트롤 전용. <b>다시 프로덕션
        /// 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는 대조군이다.</summary>
        private const float PreFixTetherBehindInR = 0.75f;

        /// <summary>몸통 물리 반폭 대비 요구하는 최소 여유 배수(종이비행기·나뭇잎 테스트와 같은 관례).</summary>
        private const float RequiredMargin = 1.2f;

        /// <summary>실측값과 공식값 사이에 허용하는 오차(월드 유닛).</summary>
        private const float SampleTolerance = 0.02f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 풍선_묶인자리의_최악편차가_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            // 최악 표본 — 오프셋에서 풍선 주머니 자신의 반경(몸 쪽으로 파고드는 최대량)을 뺀다
            // (나뭇잎 테스트의 "오프셋 − 팔랑임 진폭"과 같은 식).
            float worstCaseDeviation = headRadius * (BalloonTetherBehindInR - BalloonRadiusInR);
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"최악 편차 {worstCaseDeviation:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(worstCaseDeviation, required,
                $"{LogPrefix} 최악 편차 {worstCaseDeviation:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                $"유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 풍선이 다시 몸통 폭 안으로 " +
                "들어와 겹칠 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고 당시 상수(0.75R)로 같은 식을 계산하면 음수다(겹침이 아니라
            // 관통 — 주머니가 이미 머리 중심을 넘어 반대쪽까지 파고든다).
            float preFixWorstCase = headRadius * (PreFixTetherBehindInR - BalloonRadiusInR);
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 상수(0.75R)로 계산한 최악 편차 " +
                $"{preFixWorstCase:F4}유닛 — {(preFixWorstCase < 0f ? "음수(관통, 신고 재현)" : "양수(대조 실패)")}.");
            Assert.Less(preFixWorstCase, physicalHalfWidth,
                $"{LogPrefix} 신고 당시 상수로도 조건이 성립합니다({preFixWorstCase:F4} vs " +
                $"{physicalHalfWidth:F4}) — 이 테스트가 실제 신고를 반증하지 못합니다(무의미한 대조).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_풍선이_Idle에서_몸통_물리_반폭_밖에_있다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            RaiseLevelTo(BalloonRequiredLevel, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetBalloonItem),
                $"{LogPrefix} 풍선을 걸치지 못했습니다({BalloonRequiredLevel}레벨을 만들었는데도 실패) — " +
                "관측 전제가 성립하지 않습니다.");

            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetBalloonItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 풍선으로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            float expectedOffset = pet.BalloonTetherOffsetWorld;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            // Idle이라 좌우 흔들림(진행)이 없다 — 매듭은 첫 프레임부터 목표(anchor)에 정확히
            // 놓이므로(TickBalloon의 !_hasPosition 분기) 수렴을 기다릴 필요가 없다. 그래도 이
            // 저장소 규약대로 벽시계 표본을 짧게 여러 프레임 떠서 "우연히 한 프레임만 맞았다"는
            // 오판을 배제한다.
            float minAbsDeviation = float.MaxValue;
            float ownerX = agent.Blackboard.Body.position.x;
            float deadline = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                float deviation = Mathf.Abs(pet.PetWorldPosition.x - ownerX);
                if (deviation < minAbsDeviation) minAbsDeviation = deviation;
                yield return null;
            }

            Debug.Log($"{LogPrefix} 1초간 실측한 최소 가로 편차 {minAbsDeviation:F4}유닛 " +
                $"(공식값 {expectedOffset:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}).");

            // P2 — 실측값이 공식값과 일치한다(허용오차 안).
            Assert.Greater(minAbsDeviation, expectedOffset - SampleTolerance,
                $"{LogPrefix} 실측 편차 {minAbsDeviation:F4}유닛이 공식값 {expectedOffset:F4}유닛에 " +
                "못 미칩니다 — 화면 클램프나 다른 경로가 오프셋을 줄이고 있을 수 있습니다.");
            Assert.Less(minAbsDeviation, expectedOffset + SampleTolerance,
                $"{LogPrefix} 실측 편차 {minAbsDeviation:F4}유닛이 공식값 {expectedOffset:F4}유닛을 " +
                "허용오차 이상으로 넘어섭니다 — 공식과 실제 그림이 어긋났습니다.");

            // 실측값이 "주머니 반경을 뺀 최악 편차" 기준으로도 몸통 물리 반폭보다 크다.
            float worstCaseDeviation = minAbsDeviation - metrics.HeadRadius * BalloonRadiusInR;
            Assert.Greater(worstCaseDeviation, physicalHalfWidth,
                $"{LogPrefix} 실측 편차에서 풍선 주머니 반경을 뺀 값({worstCaseDeviation:F4})이 몸통 " +
                $"물리 반폭({physicalHalfWidth:F4})보다 크지 않습니다 — 실제로 그려지는 풍선이 몸통 폭 " +
                "안에 걸쳐 있을 수 있습니다.");
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
        }

        /// <summary>"이 아이템을 걸친 상태로 만든다". <see cref="EquipmentModel.TryWear"/>를 그냥 쓰면
        /// 안 된다 — <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다
        /// (PetBalloonHatSortOrderTests.Wear와 같은 이유·같은 관례).</summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        /// <summary>목표 레벨까지 정상 경로(AddXp)로 올린다(기존 펫 테스트와 같은 관례).</summary>
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
