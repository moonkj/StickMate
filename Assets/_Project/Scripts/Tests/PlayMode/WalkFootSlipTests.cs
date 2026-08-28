using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 보행 발 미끄러짐(문워크) 정량 회귀 테스트 — 리더 지시의 실측 방법을 그대로 코드로 옮긴 것:
    /// "디딤 국면에서 발의 월드 X가 고정되는지 로그로 확인하면 정량 검증이 된다."
    ///
    /// 왜 필요한가: 보행 사이클 주파수는 임의 계수가 아니라 키포즈 표의 기하학적 보폭에서 역산된다
    /// (States/StickmanPoseAnimator.TickWalkPose / _distancePerCycle 문서). 그 역산이 조금이라도
    /// 어긋나면 디딤발이 바닥에서 앞뒤로 밀리고, 그게 사용자가 여러 라운드 지적한 "어색함"의 실체다.
    /// 지금까지 이 값은 **실행 로그를 사람이 읽어** 확인해왔는데, 그러면 다음 사람이 각도 표나 다리
    /// 길이나 스무딩 계수를 건드렸을 때 조용히 되돌아간다. 여기서 수치로 잠근다.
    ///
    /// 측정 정의:
    ///   · 디딤발은 **보행 위상으로** 정한다(왼다리 위상 &lt; 0.5 = 왼발 디딤). 이것이 키포즈 표가
    ///     설계한 디딤 국면이자 보폭 역산이 기준으로 삼는 바로 그 구간이므로, 역산의 정합성을 재려면
    ///     반드시 이 정의를 써야 한다.
    ///   · 한 디딤 국면에서 (a) 디딤발 월드 X가 움직인 폭과 (b) 몸이 전진한 거리를 각각 잰다.
    ///   · 미끄러짐 비율 = (a)/(b). 0이면 발이 땅에 박혀 있고 몸만 지나간 것, 1이면 발이 몸과 똑같이
    ///     끌려간 것(= 완전한 문워크).
    ///
    /// 함께 재는 두 가지(둘 다 2026-08-28 라운드에서 새로 드러난 항목):
    ///   · **접지 오차** — 디딤발의 월드 Y가 지면(루트 Y)에서 얼마나 떠 있거나 파고드는가.
    ///     StickmanPoseAnimator.ComputeFootGroundingOffset()이 이 값을 0에 붙이는 역할을 한다.
    ///   · **디딤발 불일치율** — 화면에서 더 낮은(=닿아 보이는) 발이 위상상의 디딤발과 다른 시간 비율.
    ///     0이 아니면 그 시간 동안 관객은 "흔드는 발이 땅을 긁는다"고 인지한다(알려진 잔여 과제).
    /// </summary>
    public sealed class WalkFootSlipTests
    {
        private const string LogPrefix = "[FOOTSLIP-TEST]";

        private const long FloorHandle = 8001L;
        private const float SettleWaitSeconds = 2.5f;
        private const float WarmupSeconds = 0.8f;   // 속도 측정 창(0.1초)과 각도 스무딩이 자리잡을 시간.
        private const float MeasureSeconds = 3.0f;
        private const int MinSamplesPerStance = 4;  // 너무 짧은 구간(발 교대 순간)은 통계에서 제외.

        /// <summary>결정론적 평지 하나만 돌려주는 최소 스텁(LedgeHangDescentTests와 같은 컨벤션).</summary>
        private sealed class FlatFloorService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            _agent = null;
        }

        [UnityTest]
        public IEnumerator StanceFootStaysPlantedWhileBodyMovesForward()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            // 화면 전폭 평지 하나 — 걷는 3초 동안 경계/낙하가 개입하지 않게 한다.
            float w = Screen.width;
            float h = Screen.height;
            var service = new FlatFloorService();
            service.Footholds.Add(new PlatformFoothold(FloorHandle, new Rect(0f, h * 0.55f, w, h * 0.45f), true));
            bb.FootholdPoller = new FootholdPoller(service, bb.Config);

            var intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = intent;

            // 화면 왼쪽 1/4 지점에서 출발해 오른쪽으로 걷는다(측정 구간 내내 평지 위에 머문다).
            Vector3 startWorld = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(w * 0.20f, h * 0.55f), 10f, bb.Config);
            bb.Body.position = new Vector2(startWorld.x, startWorld.y);
            bb.Body.transform.position = new Vector3(startWorld.x, startWorld.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FloorHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");
            Assert.IsTrue(pose.HasLimbs, $"{LogPrefix} 프리팹에서 팔다리를 찾지 못했습니다 — 측정 자체가 불가능합니다.");

            yield return new WaitForSeconds(WarmupSeconds);
            Assert.AreEqual(StickmanStateId.Walk, bb.Machine.CurrentStateId,
                $"{LogPrefix} 준비 구간에서 Walk를 유지하지 못했습니다(실제={bb.Machine.CurrentStateId}).");

            // ── 측정 ──────────────────────────────────────────────────────────────────────
            bool haveStance = false;
            bool stanceIsLeft = false;
            float stanceMinX = 0f, stanceMaxX = 0f, stanceStartBodyX = 0f;
            int stanceSamples = 0;

            float totalStanceDrift = 0f;   // 디딤 국면들에서 디딤발이 움직인 폭의 합
            float totalStanceBodyMove = 0f;// 같은 구간에서 몸이 전진한 거리의 합
            int stanceRuns = 0;
            float worstRunRatio = 0f;

            float elapsed = 0f;
            float measureStartBodyX = bb.Body.position.x;

            // 접지 오차(디딤발 월드Y − 지면) 극값과 "디딤발 불일치" 프레임 수.
            float groundErrMin = float.PositiveInfinity, groundErrMax = float.NegativeInfinity;
            int mismatchFrames = 0, totalFrames = 0;

            // 진단용 극값 — 실측이 기대와 어긋날 때 "어느 단계에서 어긋났는가"를 바로 짚기 위한 값들.
            float hipMin = float.PositiveInfinity, hipMax = float.NegativeInfinity;
            float relMin = float.PositiveInfinity, relMax = float.NegativeInfinity;

            while (elapsed < MeasureSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (bb.Machine.CurrentStateId != StickmanStateId.Walk) break;

                pose.GetFootWorldPositions(out Vector2 left, out Vector2 right);
                // ★ 디딤발은 "더 낮은 발"이 아니라 **보행 위상**으로 정한다 — 왼다리의 위상 오프셋이 0이고
                // 표가 t∈[0,0.5)를 디딤 국면으로 설계했으므로, 위상이 0.5 미만이면 왼발이 디딤발이다.
                bool leftIsStance = pose.WalkPhase01 < 0.5f;
                Vector2 stanceFoot = leftIsStance ? left : right;
                float bodyX = bb.Body.position.x;
                float groundY = bb.Body.position.y; // 이 프로젝트의 규약: 루트 원점 = 발 높이 = 지면.

                totalFrames++;
                if ((left.y <= right.y) != leftIsStance) mismatchFrames++;
                float groundErr = stanceFoot.y - groundY;
                if (groundErr < groundErrMin) groundErrMin = groundErr;
                if (groundErr > groundErrMax) groundErrMax = groundErr;

                pose.GetUpperAngles(out float hipL, out _, out _, out _);
                if (hipL < hipMin) hipMin = hipL;
                if (hipL > hipMax) hipMax = hipL;
                float rel = left.x - bodyX;
                if (rel < relMin) relMin = rel;
                if (rel > relMax) relMax = rel;

                if (!haveStance || leftIsStance != stanceIsLeft)
                {
                    // 직전 디딤 국면 마감
                    if (haveStance && stanceSamples >= MinSamplesPerStance)
                    {
                        float drift = stanceMaxX - stanceMinX;
                        float bodyMove = Mathf.Abs(bodyX - stanceStartBodyX);
                        if (bodyMove > 0.01f)
                        {
                            totalStanceDrift += drift;
                            totalStanceBodyMove += bodyMove;
                            stanceRuns++;
                            float ratio = drift / bodyMove;
                            if (ratio > worstRunRatio) worstRunRatio = ratio;
                            Debug.Log($"{LogPrefix} 디딤 국면 #{stanceRuns} ({(stanceIsLeft ? "왼발" : "오른발")}) — " +
                                $"발 월드X 이동폭={drift:F4}유닛, 같은 구간 몸 전진={bodyMove:F4}유닛, 미끄러짐 비율={ratio:F3}");
                        }
                    }
                    haveStance = true;
                    stanceIsLeft = leftIsStance;
                    stanceMinX = stanceMaxX = stanceFoot.x;
                    stanceStartBodyX = bodyX;
                    stanceSamples = 1;
                }
                else
                {
                    if (stanceFoot.x < stanceMinX) stanceMinX = stanceFoot.x;
                    if (stanceFoot.x > stanceMaxX) stanceMaxX = stanceFoot.x;
                    stanceSamples++;
                }
            }

            float bodyTravel = bb.Body.position.x - measureStartBodyX;
            float slipRatio = totalStanceBodyMove > 0.0001f ? totalStanceDrift / totalStanceBodyMove : 1f;
            float mismatchRatio = totalFrames > 0 ? (float)mismatchFrames / totalFrames : 1f;

            Debug.Log($"{LogPrefix} 종합 — 측정 {elapsed:F2}초, 몸 전진 {bodyTravel:F3}유닛, 디딤 국면 {stanceRuns}회, " +
                $"평균 미끄러짐 비율={slipRatio:F3} (0=완벽, 1=완전 문워크), 최악 구간={worstRunRatio:F3}, " +
                $"한 사이클 이동거리={pose.DistancePerCycle:F3}유닛, 바라보는 방향={pose.FacingSign:F0}");
            Debug.Log($"{LogPrefix} 접지 — 디딤발 월드Y − 지면 범위 [{groundErrMin:+0.0000;-0.0000}, {groundErrMax:+0.0000;-0.0000}]유닛 " +
                $"(음수=지면 파고듦 / 양수=떠 있음, 0에 가까울수록 좋다) | 디딤발 불일치율={mismatchRatio * 100f:F1}% " +
                "(화면상 더 낮은 발이 위상상의 디딤발과 다른 시간 — 남은 잔여 과제)");
            Debug.Log($"{LogPrefix} 진단 — 왼다리 엉덩이각 [{hipMin:F1}, {hipMax:F1}]도(표 기대 ±25), " +
                $"왼발의 몸 기준 X 범위 [{relMin:F3}, {relMax:F3}]유닛 (폭 {relMax - relMin:F3} — 실제 한 걸음 보폭이며, " +
                $"사이클 이동거리의 절반({pose.DistancePerCycle * 0.5f:F3})과 같아야 발이 붙는다)");

            Assert.Greater(stanceRuns, 3,
                $"{LogPrefix} 측정 구간에서 디딤 국면이 {stanceRuns}회뿐입니다 — 걷기 사이클이 거의 돌지 않았습니다.");
            Assert.Greater(bodyTravel, 1f,
                $"{LogPrefix} 몸이 {bodyTravel:F3}유닛밖에 전진하지 않았습니다 — 측정 전제가 깨졌습니다.");

            // 임계값의 의미: 디딤발이 그 구간의 몸 이동 거리의 30% 넘게 함께 끌려가면 육안으로 명확한
            // 문워크로 보인다. 현재 구현의 실측값은 이보다 한참 아래이며(위 종합 로그 참고), 이 잠금은
            // "각도 표/다리 길이/스무딩 계수를 건드려 역산이 깨지면 즉시 빨간불"을 위한 것이다.
            Assert.Less(slipRatio, 0.30f,
                $"{LogPrefix} 디딤발이 미끄러집니다(문워크) — 평균 미끄러짐 비율 {slipRatio:F3}. " +
                "보행 사이클 주파수 역산(StickmanPoseAnimator._distancePerCycle)이 실제 보폭과 어긋났을 가능성이 큽니다.");

            // 접지 보정(ComputeFootGroundingOffset)이 살아 있는지 잠근다 — 이 값이 커지면 디딤발이
            // 지면을 들락거려 "발이 땅에 안 붙은" 느낌이 난다(보정 도입 전 실측: 파고듦 0.025 / 뜸 0.070).
            float worstGroundErr = Mathf.Max(Mathf.Abs(groundErrMin), Mathf.Abs(groundErrMax));
            Assert.Less(worstGroundErr, 0.06f,
                $"{LogPrefix} 디딤발이 지면에서 {worstGroundErr:F4}유닛 벗어납니다 — " +
                "StickmanPoseAnimator.ComputeFootGroundingOffset()의 접지 보정이 깨졌을 가능성이 큽니다.");
        }
    }
}
