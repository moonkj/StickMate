using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// BUG-SW-M1 반려 수정 실측 검증(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md).
    ///
    /// 이전 라운드는 씬에 바닥 Collider2D가 전혀 없고 팔다리에도 Collider2D가 없어, RAGDOLL 진입 시
    /// 캐릭터가 무엇과도 충돌하지 못해 감쇠 없는 중력만 계속 작용했다 — RagdollState.Tick()의 유일한
    /// Getup 전이 조건(전신 속도가 임계값 이하로 일정 시간 지속)이 수학적으로 영원히 충족되지 않는
    /// 구조적 결함이었다(BUG-SW-M1). 이 테스트는 StickmanAgent.ReportExternalImpact()를 임계값 이상으로
    /// 직접 호출해 RAGDOLL을 강제 진입시킨 뒤, 실제로 (a) RAGDOLL로 전이하고 (b) 유한 시간 안에 GETUP을
    /// 거쳐 (c) Idle/Walk 능동 상태로 복귀하는지를 실측 로그와 함께 확인한다 — "아마 될 것"이 아니라
    /// -logFile 결과물에서 grep으로 바로 확인 가능한 실측 증거를 남기는 것이 목적이다.
    /// StickmanPlaytestSmokeTests.cs가 커버하지 않는 영역(Ragdoll/Getup)을 메운다(Minor 1 대응).
    /// </summary>
    public sealed class StickmanRagdollRecoveryTests
    {
        private const string LogPrefix = "[RAGDOLL-TEST]";
        private const float SettleWaitSeconds = 3f; // 첫 낙하/스냅을 마치고 확실히 Idle/Walk에 있도록 대기.
        private const float MaxObserveSeconds = 15f; // Ragdoll->Getup->Idle/Walk 전체 관찰 안전망.
        private const float SampleInterval = 0.25f;
        private const float MaxAllowedFallDistance = 200f; // 여전히 무한 낙하한다면 조기 실패시키는 안전망.

        [UnityTest]
        public IEnumerator RagdollEntersAndRecoversToActiveState()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null; // Awake/Start가 완전히 실행되도록 한 프레임 더 대기.

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, "씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선을 확인하세요.");

            // 캐릭터가 먼저 정상적으로 낙하/스냅을 마치고 Idle/Walk에 안착할 시간을 준다.
            yield return new WaitForSeconds(SettleWaitSeconds);

            float initialY = agent.transform.position.y;
            StickmanStateId preImpactState = agent.Blackboard.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 충격 전 t={SettleWaitSeconds:F1}s 상태={preImpactState}, pos={agent.transform.position}");
            Assert.IsTrue(preImpactState == StickmanStateId.Idle || preImpactState == StickmanStateId.Walk,
                $"{LogPrefix} 충격을 주기 전 캐릭터가 능동 상태(Idle/Walk)에 있어야 합니다. 실제={preImpactState} " +
                "— 초기 낙하/스냅이 아직 안 끝났을 수 있습니다(BUG-SW-M2 groundSnapTolerance 대역 확인 필요).");

            // ragdollForceThreshold(기본 8) 이상으로 강제 충격 — 임계값을 확실히 넘기기 위해 5배로 여유.
            float threshold = agent.Blackboard.Config != null ? agent.Blackboard.Config.ragdollForceThreshold : 8f;
            float impact = threshold * 5f;
            agent.ReportExternalImpact(impact);
            Debug.Log($"{LogPrefix} ReportExternalImpact({impact:F1}) 호출(threshold={threshold:F1})");

            StickmanStateId postImpactState = agent.Blackboard.Machine.CurrentStateId;
            Assert.AreEqual(StickmanStateId.Ragdoll, postImpactState,
                $"{LogPrefix} 강제 충격 직후 RAGDOLL로 전이해야 합니다. 실제={postImpactState}");

            bool sawGetup = false;
            bool recoveredToActive = false;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds)
            {
                yield return new WaitForSeconds(SampleInterval);
                elapsed += SampleInterval;

                StickmanStateId state = agent.Blackboard.Machine.CurrentStateId;
                Vector3 pos = agent.transform.position;
                float maxLimbSpeed = agent.Blackboard.GetRagdollRig() != null ? agent.Blackboard.GetRagdollRig().GetMaxSpeed() : -1f;
                Debug.Log($"{LogPrefix} t={elapsed:F2}s state={state} pos=({pos.x:F3},{pos.y:F3}) maxLimbSpeed={maxLimbSpeed:F3}");

                Assert.Less(initialY - pos.y, MaxAllowedFallDistance,
                    $"{LogPrefix} 무한 낙하 의심 — t={elapsed:F2}s에 Y가 시작점보다 {initialY - pos.y:F1} 유닛 아래로 발산했습니다(BUG-SW-M1 재발 의심).");

                if (state == StickmanStateId.Getup) sawGetup = true;
                if (state == StickmanStateId.Idle || state == StickmanStateId.Walk)
                {
                    recoveredToActive = true;
                    break;
                }
            }

            Debug.Log($"{LogPrefix} 관찰 종료 — sawGetup={sawGetup}, recoveredToActive={recoveredToActive}, elapsed={elapsed:F2}s, finalState={agent.Blackboard.Machine.CurrentStateId}");

            Assert.IsTrue(sawGetup, $"{LogPrefix} GETUP 상태를 한 번도 거치지 않았습니다 — RAGDOLL이 바닥에 안착하지 못했을 가능성(BUG-SW-M1 재발 의심).");
            Assert.IsTrue(recoveredToActive,
                $"{LogPrefix} {MaxObserveSeconds}초 안에 Idle/Walk로 복귀하지 못했습니다 — RAGDOLL/GETUP에 계속 머물러 있을 수 있습니다(BUG-SW-M1 재발 의심).");
        }
    }
}
