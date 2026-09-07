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
    /// ★★ 실기(Windows) 실시간 사용자 신고 회귀 잠금(2026-09-07): <i>"종이비행기 착용했는데
    /// 캐릭터머리뒤에서만 돌고있음 너무 범위가 좁음. 캐릭터 주위로 돌아야하는데 그래서 거의
    /// 종이비행기가 안보임."</i>
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님)
    /// ============================================================================
    /// 종이비행기 궤도의 가로 반폭(<see cref="CharacterPetRenderer.PlaneOrbitHalfWidthWorld"/> =
    /// 머리 반경 × <c>PlaneOrbitHalfWidthInR</c>)이 캐릭터 자신의 <b>물리적 반폭</b>
    /// (<see cref="StickmanBlackboard.CharacterPhysicalHalfWidthWorld"/>, 배율 1.0에서
    /// <see cref="StickConfig.BaselineBodyPhysicsHalfWidth"/> = 0.4유닛)보다 <b>작았다</b>
    /// (신고 당시 값 0.33유닛). 즉 궤도 위상이 어디에 있든(<c>cos</c>가 ±1이어도) 비행기가 몸통
    /// 자신의 폭 밖으로 단 한 번도 못 나갔다 — "궤도"가 아니라 몸통 폭 <b>안</b>에서의 제자리
    /// 떨림이었다. 여기에 반주기마다 도는 앞/뒤 레이어 전환이 겹치면서 "머리 뒤에서만 도는 것처럼"
    /// 보인 것이 정확히 재현된다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  공식으로 유도한 가로 반폭이 몸통 물리 반폭보다 20% 이상 여유를 두고 크다.
    ///  P1n (네거티브) 신고 당시의 옛 상수(1.50 R)로 <b>같은 식</b>을 계산하면 이 조건이 성립하지
    ///      않는다 — P1이 우연이 아니라 이번 수정으로 성립한다는 증거(같은 파일 안에서 대조).
    ///  P2  <b>실제로 그려지는</b> 펫 위치(<see cref="CharacterPetRenderer.PetWorldPosition"/>)를
    ///      궤도 한 바퀴(3.2초)를 넘겨 실측한 최대 진폭이 공식값과 일치하고, 역시 물리 반폭보다
    ///      크다 — "공식은 맞는데 실제로 그려지는 것은 다르다"는 별개의 실패 유형을 반증한다.
    /// </summary>
    public sealed class PetPlaneOrbitRadiusTests
    {
        private const string LogPrefix = "[PET-PLANE-ORBIT]";

        /// <summary>PET 카테고리의 "종이비행기" 자리 — AppearanceShapeBuilder.PetPlane과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(이 프로젝트의 기존 관례,
        /// PetFollowsOwnerFootholdTests의 PetBall과 같은 이유).</summary>
        private const int PetPlaneItem = 1;

        /// <summary>신고 당시(2026-09-07 수정 전) 궤도 가로 반폭 상수 — 네거티브 컨트롤 전용.
        /// CharacterPetRenderer.PlaneOrbitHalfWidthInR의 옛 값을 그대로 복제한다. 이 상수를
        /// <b>다시 프로덕션 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는 대조군이다.</summary>
        private const float PreFixOrbitHalfWidthInR = 1.50f;

        /// <summary>공식 진폭과 실측 진폭 사이에 허용하는 오차(월드 유닛) — 화면 클램프/부동소수
        /// 오차 정도만 흡수한다.</summary>
        private const float SampleTolerance = 0.02f;

        /// <summary>몸통 물리 반폭 대비 요구하는 최소 여유 배수. 1.0이면 "간신히 안 겹친다" 수준이라
        /// 신고를 재현하지 않을 정도의 여유(20%)를 요구한다.</summary>
        private const float RequiredMargin = 1.2f;

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
        public IEnumerator 종이비행기_궤도_반폭이_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");

            // 빌드 + 첫 물리 반폭 계측이 돌 시간을 준다(StickmanAgent.TickVisualHalfWidth가
            // 첫 프레임에 즉시 갱신하지만, 안전하게 몇 프레임 더 기다린다).
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다({pet.ActivePetItemIndex}).");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            float orbitHalfWidth = pet.PlaneOrbitHalfWidthWorld;
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"궤도 가로 반폭 {orbitHalfWidth:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(orbitHalfWidth, required,
                $"{LogPrefix} 궤도 가로 반폭 {orbitHalfWidth:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                $"유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 종이비행기가 다시 몸통 폭 " +
                "안에 갇혀 \"머리 뒤에서만 도는\" 신고가 재현될 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고 당시 상수로는 같은 조건이 성립하지 않았어야 한다.
            float preFixOrbitHalfWidth = headRadius * PreFixOrbitHalfWidthInR;
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 상수(1.50 R)로 계산한 궤도 반폭 " +
                $"{preFixOrbitHalfWidth:F4}유닛 — 물리 반폭 {physicalHalfWidth:F4}유닛보다 " +
                $"{(preFixOrbitHalfWidth < physicalHalfWidth ? "작다(신고 재현)" : "크다(대조 실패)")}.");
            Assert.LessOrEqual(preFixOrbitHalfWidth, physicalHalfWidth,
                $"{LogPrefix} 신고 당시 상수로도 조건이 성립합니다({preFixOrbitHalfWidth:F4} vs " +
                $"{physicalHalfWidth:F4}) — 이 테스트가 실제 신고를 반증하지 못합니다(무의미한 여유).");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_궤도_진폭이_공식값과_일치하고_몸통보다_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();

            CharacterPetRenderer pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, PetPlaneItem, null),
                $"{LogPrefix} 종이비행기를 걸치지 못했습니다.");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(PetPlaneItem, pet.ActivePetItemIndex,
                $"{LogPrefix} 펫이 종이비행기로 빌드되지 않았습니다.");

            float expectedHalfWidth = pet.PlaneOrbitHalfWidthWorld;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;

            // 궤도 주기(3.2초)보다 넉넉히 길게 실측한다 — 벽시계 기준(이 저장소 확정 규칙:
            // 프레임 수 기반 대기 금지, 배치모드 PlayMode는 2,000fps 이상으로 돌 수 있다).
            float maxDeviation = 0f;
            float deadline = Time.realtimeSinceStartup + 4.0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                float centerX = pet.HeadAnchorWorldPosition.x;
                float deviation = Mathf.Abs(pet.PetWorldPosition.x - centerX);
                if (deviation > maxDeviation) maxDeviation = deviation;
                yield return null;
            }

            Debug.Log($"{LogPrefix} 4초간 실측한 최대 가로 진폭 {maxDeviation:F4}유닛 " +
                $"(공식값 {expectedHalfWidth:F4}, 물리 반폭 {physicalHalfWidth:F4}).");

            Assert.Greater(maxDeviation, expectedHalfWidth - SampleTolerance,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 공식값 {expectedHalfWidth:F4}유닛에 " +
                "못 미칩니다 — 화면 클램프나 다른 경로가 궤도를 줄이고 있을 수 있습니다.");
            Assert.Less(maxDeviation, expectedHalfWidth + SampleTolerance,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 공식값 {expectedHalfWidth:F4}유닛을 " +
                "허용오차 이상으로 넘어섭니다 — 공식과 실제 그림이 어긋났습니다.");
            Assert.Greater(maxDeviation, physicalHalfWidth,
                $"{LogPrefix} 실측 진폭 {maxDeviation:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                "유닛을 넘지 못합니다 — 실제로 그려지는 궤도가 몸통 폭 안에 갇혀 있습니다.");
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
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
