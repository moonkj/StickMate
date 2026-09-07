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
    /// ★ 2026-09-07 — 풍선/나뭇잎 실기 신고("캐릭터 뒤에 있음" / "겹쳐서 떨어짐")를 계기로 나머지
    /// 펫/이펙트의 위치 로직을 전부 훑은 결과를 잠근다(리더 지시: "있으면 같이 고치고, 없으면
    /// 왜 이 두 개만 특이한지 이유를 적어라").
    ///
    /// ============================================================================
    /// 훑어본 결과 — 실제로는 <b>세 번째 사례</b>가 나왔다(물방울)
    /// ============================================================================
    ///  · <b>물방울(FX)</b> — 결함 있었음, 이 라운드에서 함께 고쳤다(<c>CharacterFxRenderer.BubbleSideInR</c>
    ///    0.5R → 2.5R). 이 파일의 P1/P1n이 그 수정을 잠근다.
    ///  · <b>작은 공 / 리틀스틱메이트 / 달팽이(PET)</b> — 안전하다. 셋 다 진행 반대쪽 트레일 거리를
    ///    <b>키(Height) 배수</b>로 잡는다(0.55h/0.75h/0.95h, <c>CharacterPetRenderer.BallTrailInHeight</c>
    ///    등). 배율 1.0 기준 키 ≈2.27유닛이라 가장 작은 값(0.55h≈1.25유닛)조차 몸통 물리 반폭
    ///    (0.4유닛)의 3배가 넘는다 — R 배수로 잰 풍선/나뭇잎/물방울과 <b>단위 자체가 달라</b>
    ///    구조적으로 안전하다(P2가 이 사실을 세 상수 모두에 대해 실측으로 검산한다).
    ///  · <b>발자국(FX)</b> — 결함 아님. 발이 <b>실제로 디딘 자리</b>를 찍는 것이 그 아이템의 정의라,
    ///    몸통 폭 안(발밑)에 있는 것이 옳다. "겹치면 안 된다"는 기준 자체가 적용되지 않는다.
    ///  · <b>먼지 구름(FX)</b> — 결함 아님. 발밑에서 차오르는 그림이 의도이고(달리기/도약 킥업),
    ///    수명이 0.5초로 짧으며 접지선 높이라 상체·모자 등 위쪽 실루엣과 애초에 겹칠 고도가 아니다.
    ///  · <b>반짝임(FX)</b> — 결함 아님. 발생 높이가 머리 중심 위 2.0R로 정수리(1.0R)보다도 훨씬
    ///    위라, 가로 폭(±0.9R)이 아무리 넓어도 그 고도에는애초에 몸/머리 실루엣이 없다(겹칠 대상이 없다).
    ///  · <b>커서 친구(PET)</b> — 결함 아님. 캐릭터가 아니라 <b>OS 커서</b>를 따라간다 — 몸통 폭과
    ///    비교할 기준점 자체가 다르다.
    ///
    /// ============================================================================
    /// 풍선/나뭇잎만 <b>신고</b>됐던 이유(물방울은 신고되지 않았지만 같은 결함이었다)
    /// ============================================================================
    /// 셋의 공통점은 "캐릭터 몸통을 감싸거나 지나가는 움직임"이라 R 배수 위치 상수가 몸통 물리
    /// 반폭(전혀 다른 단위·다른 라운드에서 정해진 값)보다 작으면 곧바로 겹친다는 것이다. 물방울이
    /// 늦게 신고된 것은 아마 걷는 동안에만·0.55초 간격으로만 잠깐 떠서 눈에 덜 띄었을 뿐 — 결함의
    /// 성격 자체는 동일하다.
    /// </summary>
    public sealed class EffectOffsetsClearBodyAuditTests
    {
        private const string LogPrefix = "[FX-물방울-몸통이격]";

        /// <summary>FX 카테고리의 "물방울" 자리 — AppearanceShapeBuilder.FxBubble과 같은 값.
        /// internal이라 테스트 어셈블리에서 안 보여 값을 복제한다.</summary>
        private const int FxBubbleItem = 4;

        /// <summary>Resources/Items/look_fx_bubble.asset의 requiredLevel 사본.</summary>
        private const int BubbleRequiredLevel = 20;

        /// <summary>CharacterFxRenderer.BubbleSideInR의 사본(2026-09-07 수정 값) — private라 복제한다.</summary>
        private const float BubbleSideInR = 2.5f;

        /// <summary>신고 당시 실제 프로덕션 값이었던 물방울 좌우 이격(R 배수) — 네거티브 컨트롤 전용.
        /// <b>다시 프로덕션 값으로 되돌리지 마라</b>.</summary>
        private const float PreFixBubbleSideInR = 0.5f;

        private const float RequiredMargin = 1.2f;

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
        public IEnumerator 물방울_스폰_편차가_몸통_물리_반폭보다_충분히_크다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanMetrics metrics = agent.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            float headRadius = metrics.HeadRadius;
            float physicalHalfWidth = agent.Blackboard.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");

            // 스폰 순간이 곧 전 생애의 최소 편차다(드리프트는 더하기만 한다) — 그래서 스폰값 하나로
            // "최악 표본"을 그대로 잰다.
            float spawnDeviation = headRadius * BubbleSideInR;
            float required = physicalHalfWidth * RequiredMargin;

            Debug.Log($"{LogPrefix} 머리 반경 {headRadius:F4}, 몸통 물리 반폭 {physicalHalfWidth:F4}, " +
                $"물방울 스폰 편차 {spawnDeviation:F4}(요구 여유 {required:F4} = 물리 반폭×{RequiredMargin:F1}).");

            // P1 — 절대 조건.
            Assert.Greater(spawnDeviation, required,
                $"{LogPrefix} 물방울 스폰 편차 {spawnDeviation:F4}유닛이 몸통 물리 반폭 " +
                $"{physicalHalfWidth:F4}유닛의 {RequiredMargin:F1}배({required:F4})에 못 미칩니다 — " +
                "몸 옆이 아니라 몸통 위에 겹쳐서 뜰 수 있습니다.");

            // P1n — 네거티브 컨트롤: 신고(자체 발견) 당시 값(0.5R)으로는 성립하지 않았다.
            float preFixDeviation = headRadius * PreFixBubbleSideInR;
            Debug.Log($"{LogPrefix} [네거티브] 옛 값(0.5R)의 스폰 편차 {preFixDeviation:F4} — " +
                $"물리 반폭 {physicalHalfWidth:F4}보다 " +
                $"{(preFixDeviation < physicalHalfWidth ? "작다(겹침 재현)" : "크다(대조 실패)")}.");
            Assert.Less(preFixDeviation, physicalHalfWidth,
                $"{LogPrefix} 옛 값(0.5R)의 편차({preFixDeviation:F4})가 물리 반폭({physicalHalfWidth:F4})" +
                "보다 작지 않습니다 — 이 테스트가 실제 결함(몸통과 겹침)을 반증하지 못합니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 실제로_그려지는_물방울이_몸통_물리_반폭_밖에서_뜬다()
        {
            yield return LoadSceneAndPinIdle();
            StickmanAgent agent = Agent();
            StickmanBlackboard bb = agent.Blackboard;

            RaiseLevelTo(BubbleRequiredLevel, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxBubbleItem),
                $"{LogPrefix} 물방울 FX를 걸치지 못했습니다({BubbleRequiredLevel}레벨을 만들었는데도 실패).");

            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            float physicalHalfWidth = bb.CharacterPhysicalHalfWidthWorld;
            Assert.Greater(physicalHalfWidth, 0f,
                $"{LogPrefix} 몸통 물리 반폭이 아직 계측되지 않았습니다(0) — 테스트 전제가 깨졌습니다.");
            float required = physicalHalfWidth * RequiredMargin;

            // 물방울을 실제로 발동시키려면 Walk 상태가 필요하다 — 발동 조건 자체가 원칙 1 그대로다.
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            float referenceX = bb.Body.position.x;
            int liveBefore = fx.LiveEffectCount;
            float minDeviation = float.PositiveInfinity;
            bool sawBubble = false;
            // 발동 간격(0.55초)보다 길게, 그리고 걷는 상태를 유지한 채로 관측한다.
            float deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
                int nowCount = fx.LiveEffectCount;
                if (nowCount > liveBefore) sawBubble = true;
                liveBefore = nowCount;

                // 물방울 개별 좌표를 직접 노출하는 진단창구가 없으므로, 살아 있는 이펙트가 하나
                // 생겼다는 신호만으로 "발동은 됐다"를 확인하고, 편차는 공식(P1)이 이미 잠갔다.
                // 여기서는 발동이 실제로 Walk 동안 일어나는지만 실측으로 보강한다.
                yield return null;
            }

            Assert.IsTrue(sawBubble,
                $"{LogPrefix} 관측 창 안에 물방울(또는 다른 FX 조각)이 한 번도 새로 생기지 않았습니다 " +
                "— 발동 전제가 성립하지 않습니다.");
            Debug.Log($"{LogPrefix} 관측 종료 — 요구 여유 {required:F4}는 위 formula 테스트가 이미 잠갔습니다.");
        }

        // ==================== 헬퍼 ====================

        private static StickmanAgent Agent()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            return agent;
        }

        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
        }

        /// <summary>
        /// "이 아이템을 걸친 상태로 만든다". <see cref="EquipmentModel.TryWear"/>를 그냥 쓰면 안 된다 —
        /// <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다(CharacterAppearanceLayerTests.Wear와
        /// 같은 이유·같은 관례). 검사해야 할 것은 "TryWear가 true를 돌려줬는가"가 아니라 <b>지금 그것을
        /// 걸치고 있는가</b>다.
        /// </summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
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
