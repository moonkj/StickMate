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
    /// ★★ 실기(Windows) 실시간 사용자 신고 회귀 잠금(2026-09-07): <i>"나뭇잎도 캐릭터와 겹쳐서
    /// 떨어짐 주변으로변경필요"</i>.
    ///
    /// ============================================================================
    /// 확정된 원인 (계산으로 확정 — 추측 아님)
    /// ============================================================================
    /// 옛 스폰식 <c>spawnX = head.x + Random(-1.1, 1.1)·R</c>는 몸 중심선 좌우 <b>대칭</b>이었다.
    /// R=0.22 기준 최대 편차는 1.1R=0.242유닛인데, 캐릭터 자신의 물리적 반폭
    /// (<see cref="StickmanBlackboard.CharacterPhysicalHalfWidthWorld"/>, 배율 1.0에서
    /// <see cref="StickConfig.BaselineBodyPhysicsHalfWidth"/>=0.4유닛=1.8182R)이 <b>그보다 크다</b>.
    /// 게다가 낙하 중 좌우로 더해지는 팔랑임(진폭 0.9R)은 <b>중심선 기준 사인파</b>라, 스폰 x가 0에
    /// 가까운 잎(균등분포라 흔하다)은 팔랑임이 0을 지나는 순간 <b>몸 중심선과 정확히 겹친다</b> —
    /// "겹쳐서 떨어짐"의 정확한 재현이다. 종이비행기가 겪은 것과 같은 실패 유형(진폭이 몸통 물리
    /// 반폭보다 작아 위상 전체에서 몸을 못 벗어난다)의 낙하 버전이다.
    ///
    /// 고침은 "중심에서 좌우로 퍼진다"가 아니라 <b>몸 옆 한쪽(왼쪽 또는 오른쪽) 줄기를 골라 그
    /// 줄기를 따라 떨어진다</b>다 — <c>CharacterFxRenderer.LeafSideOffsetInR</c>(=3.5R)가 팔랑임이
    /// 안쪽으로 최대로 쏠려도(−0.9R) 지켜야 하는 최소 거리(=2.6R)를 보장한다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  P1  공식으로 유도한 낙하 경로의 <b>최악 편차</b>(줄기 거리 − 팔랑임 진폭)가 몸통 물리
    ///      반폭보다 20% 이상 여유를 두고 크다.
    ///  P1n (네거티브) 신고 당시의 옛 공식(중심선 대칭 스폰)으로는 편차가 <b>0까지 내려갈 수 있다</b>
    ///      — 즉 아무런 보장이 없었다는 것을 같은 식으로 증명한다.
    ///  P2  <b>실제로 그려지는</b> 나뭇잎 위치(<see cref="CharacterFxRenderer.LiveLeafWorldPositionsForTests"/>)를
    ///      낙하 수명(2.6초)을 넘겨 실측한 최소 편차가 물리 반폭보다 크다 — "공식은 맞는데 실제로
    ///      그려지는 것은 다르다"는 별개의 실패 유형을 반증한다.
    /// </summary>
    public sealed class LeafFallClearsBodyTests
    {
        private const string LogPrefix = "[FX-나뭇잎-낙하]";

        /// <summary>FX 카테고리의 "나뭇잎" 자리 — AppearanceShapeBuilder.FxLeaf와 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다(이 프로젝트의 기존 관례).</summary>
        private const int FxLeafItem = 5;

        /// <summary>Resources/Items/look_fx_leaf.asset의 requiredLevel 사본.</summary>
        private const int LeafRequiredLevel = 24;

        /// <summary>CharacterFxRenderer.LeafSideOffsetInR의 사본(2026-09-07 수정 값) — private라
        /// 테스트 어셈블리가 직접 참조할 수 없어 복제한다(같은 값을 두 곳에 적는 이 프로젝트의
        /// 표준 대응, CLAUDE.md 협업 프로토콜의 "internal/private 상수 복제" 예외).</summary>
        private const float LeafSideOffsetInR = 3.5f;

        /// <summary>CharacterFxRenderer.LeafSwayInR의 사본(수정 전후 값이 같다) — 위와 같은 이유로 복제.</summary>
        private const float LeafSwayInR = 0.9f;

        /// <summary>CharacterFxRenderer.LeafLifeSeconds의 사본(관측 창 산정에 필요).</summary>
        private const float LeafLifeSeconds = 2.6f;

        /// <summary>CharacterFxRenderer.LeafIntervalMaxSeconds의 사본(첫 스폰 대기 상한에 필요).</summary>
        private const float LeafIntervalMaxSeconds = 3.2f;

        /// <summary>신고 당시 실제 프로덕션 값이었던 스폰 반폭(R 배수, 중심선 대칭) — 네거티브
        /// 컨트롤 전용. <b>다시 프로덕션 값으로 되돌리지 마라</b> — 이 테스트가 그 값을 반증하는
        /// 대조군이다.</summary>
        private const float PreFixSpawnSpreadInR = 1.1f;

        /// <summary>몸통 물리 반폭 대비 요구하는 최소 여유 배수(종이비행기 궤도 테스트와 같은 관례).</summary>
        private const float RequiredMargin = 1.2f;

        /// <summary>실측 진폭과 공식값 사이에 허용하는 오차(월드 유닛).</summary>
        private const float SampleTolerance = 0.03f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentModel.TryWear(EquipmentSlot.Fx, EquipmentModel.NotWorn, null);
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 나뭇잎_낙하_경로의_최악_편차가_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            // 최악 표본 — 추가 변주 0(줄기 위에 아무것도 더해지지 않음) + 팔랑임이 안쪽으로 최대.
            float worstCaseDeviation = headRadius * (LeafSideOffsetInR - LeafSwayInR);
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"최악 편차 {worstCaseDeviation:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(worstCaseDeviation, required,
                $"{LogPrefix} 최악 편차 {worstCaseDeviation:F4}유닛이 몸통 물리 반폭 {physicalHalfWidth:F4}" +
                $"유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — 팔랑임이 안쪽으로 쏠리는 " +
                "순간 나뭇잎이 다시 몸통 폭 안으로 들어와 겹쳐 떨어질 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고 당시 공식(중심선 대칭 스폰)은 spawnX=0 + sway=0(사인파가
            // 반드시 지나가는 값)이라는 <b>실제로 도달 가능한 표본</b>에서 편차가 정확히 0이 된다 —
            // 즉 아무 최소 보장도 없었다.
            const float preFixSpawnXSample = 0f;          // PreFixSpawnSpreadInR 범위 안의 유효한 값(중심).
            const float preFixSwaySample = 0f;            // sin(위상)이 매 반주기 지나가는 값.
            float preFixReachableDeviation = Mathf.Abs(preFixSpawnXSample + preFixSwaySample) * headRadius;
            Debug.Log($"{LogPrefix} [네거티브] 신고 당시 공식의 도달 가능한 편차(스폰 {preFixSpawnXSample}R" +
                $"+팔랑임 {preFixSwaySample}R) = {preFixReachableDeviation:F4}유닛 — 물리 반폭" +
                $"({physicalHalfWidth:F4})보다 {(preFixReachableDeviation < physicalHalfWidth ? "작다(신고 재현)" : "크다(대조 실패)")}.");
            Assert.Less(preFixReachableDeviation, physicalHalfWidth,
                $"{LogPrefix} 신고 당시 공식으로 도달 가능한 최소 편차({preFixReachableDeviation:F4})가 " +
                $"물리 반폭({physicalHalfWidth:F4})보다 작지 않습니다 — 이 테스트가 실제 신고(겹쳐서 " +
                "떨어짐)를 반증하지 못합니다(무의미한 대조). 참고로 옛 스폰 범위(±" +
                $"{PreFixSpawnSpreadInR}R)는 0을 포함하므로 위 표본은 실제로 그 범위 안입니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_나뭇잎이_몸통_물리_반폭_밖에서_떨어진다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanBlackboard bb = agent.Blackboard;

            RaiseLevelTo(LeafRequiredLevel, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxLeafItem),
                $"{LogPrefix} 나뭇잎 FX를 걸치지 못했습니다({LeafRequiredLevel}레벨을 만들었는데도 실패) — " +
                "관측 전제가 성립하지 않습니다.");

            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            float physicalHalfWidth = bb.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");
            float required = physicalHalfWidth * RequiredMargin;

            // 캐릭터를 Idle+정지로 고정해 뒀으므로(LoadSceneAndPinIdle) x는 사실상 일정하다 — 그
            // 값을 "중심선"의 기준으로 삼는다(TickLeaves가 스폰 순간 head.x를 기준으로 삼는 것과 같다).
            float referenceX = bb.Body.position.x;

            float minDeviation = float.PositiveInfinity;
            bool sawLeaf = false;
            float sampleUntil = Time.realtimeSinceStartup + LeafIntervalMaxSeconds + 1.0f;
            while (Time.realtimeSinceStartup < sampleUntil)
            {
                Vector2[] positions = fx.LiveLeafWorldPositionsForTests;
                if (positions.Length > 0 && !sawLeaf)
                {
                    sawLeaf = true;
                    // 첫 목격 시점부터 수명(2.6초)을 온전히 덮도록 관측 창을 늘린다 — 낙하 도중
                    // 일부만 보고 "안 겹친다"고 성급히 결론 내리지 않기 위해서다.
                    sampleUntil = Time.realtimeSinceStartup + LeafLifeSeconds + 0.5f;
                }
                for (int i = 0; i < positions.Length; i++)
                {
                    float deviation = Mathf.Abs(positions[i].x - referenceX);
                    if (deviation < minDeviation) minDeviation = deviation;
                }
                yield return null;
            }

            Assert.IsTrue(sawLeaf,
                $"{LogPrefix} 관측 창 안에 나뭇잎이 한 장도 나타나지 않았습니다 — 관측 전제가 " +
                "성립하지 않습니다(발동 간격 최대 " +
                $"{LeafIntervalMaxSeconds:F1}초보다 길게 기다렸는데도 0장).");

            Debug.Log($"{LogPrefix} 실측한 최소 편차 {minDeviation:F4}유닛 " +
                $"(요구 여유 {required:F4} = 물리 반폭 {physicalHalfWidth:F4}×{RequiredMargin:F1}).");

            // P2 — 절대 조건: 실제로 그려지는 나뭇잎도 몸통 물리 반폭보다 확실히 밖이다.
            Assert.Greater(minDeviation, required - SampleTolerance,
                $"{LogPrefix} 실측 최소 편차 {minDeviation:F4}유닛이 요구 여유 {required:F4}유닛에 " +
                "허용오차 이상으로 못 미칩니다 — 공식은 맞아도 실제로 그려지는 나뭇잎이 몸통과 " +
                "겹칠 수 있습니다.");
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
        /// <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다(개발 기기에 저장된
        /// 차림이 우연히 같을 수 있다 — CharacterAppearanceLayerTests.Wear와 같은 이유·같은 관례).
        /// 검사해야 할 것은 "TryWear가 true를 돌려줬는가"가 아니라 <b>지금 그것을 걸치고 있는가</b>다.
        /// </summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
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
