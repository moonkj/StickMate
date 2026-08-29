using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// "발이 바탕화면 맨 아래에 닿아 보인다" 불변식(2026-08-29, 사용자 3차 신고 "아직도 바닥과 독위치를
    /// 정확히 파악못하는거 같음" 대응 라운드의 절대 조건 테스트).
    ///
    /// 왜 기존 테스트로는 부족했나 — StickmanOnScreenFramingTests는 "발 높이가 안전망 상수가 말하는
    /// 높이와 일치하는가"만 본다. 상수 자체가 화면 바닥에서 40pt 위를 가리키고 있으면 그 테스트는
    /// 초록불인 채로 사용자 눈에는 캐릭터가 계속 떠 있다. 실제로 사용자가 세 번 신고할 때까지 어떤
    /// 자동 테스트도 이 증상을 잡지 못했다. 그래서 여기서는 상수와의 일치가 아니라 **화면 바닥까지의
    /// 절대 거리(pt)** 를 직접 재서 상한을 강제한다.
    ///
    /// 두 가지를 동시에 본다(두 방향의 실패가 서로 반대이므로 둘 다 필요하다):
    ///   (1) 접지 중일 때 발 잉크와 화면 맨 아래의 간격 ≤ MaxFootGapPoints  — 너무 높으면 "떠 있다".
    ///   (2) 어떤 상태에서도 잉크가 화면 아래로 잘려나가지 않는다             — 너무 낮으면 "잘린다".
    ///
    /// 계측은 Renderer.bounds가 아니라 StickmanInkBounds(정점 기하)로 한다. LineRenderer.bounds는
    /// Y로 +1.0유닛 부풀려져 있어서, 그 부풀림을 실측으로 착각한 것이 바로 이번에 고친 40pt의 출처였다
    /// (StickmanInkBounds 문서에 실측 수치).
    /// </summary>
    public sealed class FloorContactVisibilityTests
    {
        private const string LogPrefix = "[FLOOR-TEST]";

        /// <summary>
        /// 접지 시 발 잉크가 화면 맨 아래에서 떠 있어도 되는 최대 간격(OS 포인트).
        /// 현재 설계값 BottomSafetyNetInsetPoints=8pt 기준 실측 간격은 약 6.2pt(= 8 - 서 있는 자세의
        /// 잉크 하강분 1.82pt)이므로 12pt는 약 2배의 여유다. 이 테스트가 잡아야 할 회귀(상수가 40pt나
        /// Dock 높이 75pt로 되돌아가는 것)의 간격은 각각 약 38pt / 73pt라 확실히 걸린다.
        /// 캐릭터 키가 약 70pt이므로 12pt는 "키의 17% 이하로만 뜬다"는 뜻이기도 하다.
        /// </summary>
        private const float MaxFootGapPoints = 12f;

        /// <summary>
        /// 잉크가 화면 아래로 나가도 되는 허용치(OS 포인트). 0이 아닌 이유는 물리 스텝 경계에서 한
        /// 프레임 penetration이 생길 수 있기 때문이고, 1pt는 획 두께(약 2.5pt)의 절반도 안 되는 값이라
        /// 육안으로는 확인 불가능하다. 실측 최악 자세(LandingCrouch)가 발판 상단 아래 5.95pt이므로
        /// 상수를 2pt 이하로 낮추는 회귀는 이 검사에 걸린다.
        /// </summary>
        private const float MaxInkBelowScreenPoints = 1f;

        [UnityTest]
        public IEnumerator FeetVisuallyTouchScreenBottomAndAreNeverClipped()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"{LogPrefix} Camera.main을 찾지 못했습니다.");
            Assert.IsTrue(cam.orthographic, $"{LogPrefix} 이 테스트는 orthographic 카메라를 가정합니다.");

            Renderer[] renderers = agent.GetComponentsInChildren<Renderer>(true);
            Assert.IsTrue(renderers.Length > 0, $"{LogPrefix} 캐릭터 렌더러를 찾지 못했습니다.");

            // 배치 모드에는 실제 창이 하나도 없으므로 유일한 발판은 화면 최하단 안전망(더미 발판)이다
            // — 즉 캐릭터가 접지해 있다는 것은 곧 "화면 바닥에 서 있다"는 뜻이고, 이 테스트가 재려는
            // 상황(사용자 스크린샷: Dock 바깥에서 바닥에 선 캐릭터)과 정확히 같다.
            float screenBottomWorldY = cam.transform.position.y - cam.orthographicSize;
            float ptPerUnit = NullPlatformWindowService.ReferenceScreenHeightPoints / (2f * cam.orthographicSize);

            Debug.Log($"{LogPrefix} 시작 — inset상수={NullPlatformWindowService.BottomSafetyNetInsetPoints}pt " +
                $"f={NullPlatformWindowService.DummyFootholdHeightFraction:F5} " +
                $"screenBottomWorldY={screenBottomWorldY:F4} ptPerUnit={ptPerUnit:F3}");

            var worstBelowByState = new Dictionary<StickmanStateId, float>();
            float worstGroundedGapPt = float.NegativeInfinity;
            StickmanStateId worstGapState = StickmanStateId.Idle;
            float worstBelowPt = float.NegativeInfinity;
            StickmanStateId worstBelowState = StickmanStateId.Idle;
            int groundedSamples = 0;

            // measureGap=false인 구간은 "잘림"만 본다. 씬은 캐릭터를 화면 세로 중앙에 스폰시켜
            // **떨어지면서 착지**하게 만드는데(Editor/SceneBootstrapper의 스폰 문서), 낙하가 시작되기 전
            // 첫 몇 프레임의 상태가 Idle이라 그 구간까지 간격 검증에 넣으면 "화면 중앙에 떠 있다"는
            // 스폰 순간의 좌표가 그대로 최악값이 된다(실측 489pt). 착지 이후부터만 간격을 잰다.
            void Sample(bool measureGap)
            {
                if (!StickmanInkBounds.TryCompute(renderers, out Bounds ink)) return;
                StickmanStateId state = agent.Blackboard.Machine.CurrentStateId;
                float gapPt = (ink.min.y - screenBottomWorldY) * ptPerUnit;

                float belowPt = -gapPt;
                if (!worstBelowByState.TryGetValue(state, out float cur) || belowPt > cur) worstBelowByState[state] = belowPt;
                if (belowPt > worstBelowPt) { worstBelowPt = belowPt; worstBelowState = state; }

                if (!measureGap) return;
                if (state == StickmanStateId.Idle || state == StickmanStateId.Walk)
                {
                    groundedSamples++;
                    if (gapPt > worstGroundedGapPt) { worstGroundedGapPt = gapPt; worstGapState = state; }
                }
            }

            // 스폰 낙하 -> 착지(LandingCrouch: 실측상 잉크가 가장 아래로 내려가는 자세) 구간 — 잘림만 본다.
            for (int i = 0; i < 300; i++) { yield return null; Sample(measureGap: false); }
            // 배회(Idle/Walk) 관찰 — 여기부터 간격도 함께 잰다.
            for (int i = 0; i < 300; i++) { yield return new WaitForSeconds(0.05f); Sample(measureGap: true); }
            // RAGDOLL 강제 — 팔다리가 가장 크게 벌어지는 케이스까지 포함시킨다.
            float threshold = agent.Blackboard.Config != null ? agent.Blackboard.Config.ragdollForceThreshold : 8f;
            agent.ReportExternalImpact(threshold * 5f);
            for (int i = 0; i < 200; i++) { yield return new WaitForSeconds(0.05f); Sample(measureGap: true); }

            foreach (var kv in worstBelowByState)
            {
                Debug.Log($"{LogPrefix} state={kv.Key} 화면바닥아래최대={kv.Value:F2}pt");
            }
            Debug.Log($"{LogPrefix} 접지 샘플={groundedSamples} 최악간격={worstGroundedGapPt:F2}pt({worstGapState}) " +
                $"| 최악하향돌출={worstBelowPt:F2}pt({worstBelowState})");

            Assert.Greater(groundedSamples, 0,
                $"{LogPrefix} 관찰 구간 내내 접지 상태(Idle/Walk)가 한 번도 없었습니다 — 간격 검증이 실행되지 않았습니다.");

            Assert.LessOrEqual(worstGroundedGapPt, MaxFootGapPoints,
                $"{LogPrefix} 접지 중인 캐릭터의 발 잉크가 화면 맨 아래에서 {worstGroundedGapPt:F2}pt 떠 있습니다 " +
                $"(허용 {MaxFootGapPoints}pt). 사용자가 세 번 신고한 '캐릭터가 바닥에 안 닿고 떠 있다' 증상의 " +
                "회귀입니다 — Platform/NullPlatformWindowService.BottomSafetyNetInsetPoints를 확인하세요.");

            Assert.LessOrEqual(worstBelowPt, MaxInkBelowScreenPoints,
                $"{LogPrefix} 캐릭터 잉크가 화면 아래로 {worstBelowPt:F2}pt 잘려 나갔습니다(상태={worstBelowState}, " +
                $"허용 {MaxInkBelowScreenPoints}pt). BottomSafetyNetInsetPoints를 실측 최악값(LandingCrouch 5.95pt) " +
                "아래로 낮췄을 때 나타나는 반대쪽 실패입니다.");
        }
    }
}
