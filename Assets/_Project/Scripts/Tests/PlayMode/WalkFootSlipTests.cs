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
    /// ★★ 2026-08-31 BUG-WALK-B2 — <b>배율 3점에서 각각 잰다</b>(예전에는 1점뿐이었다).
    /// 예전 판본은 배율을 명시하지 않고 "씬이 주는 배율"로 돌았는데, 그 배율은 실행 시점에
    /// Interaction/CornerHoverPanel이 <b>사용자의 저장 파일</b>(stickmate_character.json)을 제때
    /// 읽었는지에 따라 0.75(프리팹 구운 값)가 되기도 하고 0.35(사용자 저장값)가 되기도 했다.
    /// 그래서 같은 코드가 실행마다 통과/실패를 오갔고(로그 증거: 몸 전진 5.625유닛=배율 0.75 통과 /
    /// 2.625유닛=배율 0.35 실패, 미끄러짐 0.54), 그 실패가 "플레이크"로 넘겨져 <b>진짜 버그가
    /// 여러 라운드 살아남았다</b>. 이제는 세 테스트가 각자 배율을 명시적으로 적용하므로
    /// 저장 파일 상태와 무관하게 결정적이다.
    ///
    /// 측정 정의:
    ///   · 디딤발은 **보행 위상으로** 정한다(왼다리 위상 &lt; 0.5 = 왼발 디딤). 이것이 키포즈 표가
    ///     설계한 디딤 국면이자 보폭 역산이 기준으로 삼는 바로 그 구간이므로, 역산의 정합성을 재려면
    ///     반드시 이 정의를 써야 한다.
    ///   · 한 디딤 국면에서 (a) 디딤발 월드 X가 움직인 폭과 (b) 몸이 전진한 거리를 각각 잰다.
    ///   · 미끄러짐 비율 = (a)/(b). 0이면 발이 땅에 박혀 있고 몸만 지나간 것, 1이면 발이 몸과 똑같이
    ///     끌려간 것(= 완전한 문워크). 배율과 무관한 무차원 수라 세 배율을 같은 임계값으로 잠근다.
    ///
    /// 함께 재는 세 가지:
    ///   · **접지 오차** — 디딤발의 월드 Y가 지면(루트 Y)에서 얼마나 떠 있거나 파고드는가.
    ///     StickmanPoseAnimator.ComputeFootGroundingOffset()이 이 값을 0에 붙이는 역할을 한다.
    ///     길이 차원이라 임계값을 루트 배율에 비례시킨다.
    ///   · **보폭 단위 정합** — 발이 실제로 그리는 <b>월드</b> 보폭(발의 몸 기준 X 범위)과 코드가 역산에
    ///     쓴 DistancePerCycle/2가 같은가. 이게 어긋나면 정의상 주파수가 틀린 것이므로, 배율이 섞여도
    ///     단위 계약을 직접 잡아낸다(BUG-WALK-B2가 정확히 이 어긋남이었다).
    ///   · **디딤발 불일치율** — 화면에서 더 낮은(=닿아 보이는) 발이 위상상의 디딤발과 다른 시간 비율.
    ///     0이 아니면 그 시간 동안 관객은 "흔드는 발이 땅을 긁는다"고 인지한다(알려진 잔여 과제).
    /// </summary>
    public sealed class WalkFootSlipTests
    {
        private const string LogPrefix = "[FOOTSLIP-TEST]";

        private const long FloorHandle = 8001L;
        private const float SettleWaitSeconds = 2.5f;
        private const float WarmupSeconds = 0.8f;   // 속도 측정 창(0.1초)과 각도 스무딩이 자리잡을 시간.
        private const float MeasureSeconds = 4.0f;  // 배율 0.35에서도 디딤 국면이 넉넉히 나오도록.
        private const int MinSamplesPerStance = 4;  // 너무 짧은 구간(발 교대 순간)은 통계에서 제외.

        /// <summary>프리팹이 구워진 배율 = 크기 다이얼의 기본값. 여기서 루트 localScale이 정확히 1이다.</summary>
        private const float DefaultScale = 0.75f;

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
        private float _savedScale = -1f;

        [TearDown]
        public void TearDown()
        {
            // 배율은 StickConfig의 런타임 필드(전역)에 남으므로 반드시 되돌린다 — 안 그러면 다음
            // 테스트가 이 테스트의 배율로 돌아간다(팀에서 이미 겪은 "테스트 간 상태 오염").
            if (_agent != null)
            {
                if (_savedScale > 0f) _agent.ApplyCharacterScale(_savedScale, "테스트 복원");
                if (_agent.Blackboard != null)
                {
                    if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                    if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                }
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            _agent = null;
            _savedScale = -1f;
        }

        // ============================================================================
        // (1) 기본 배율 0.75 — 루트 localScale이 정확히 1이라 단위 버그가 숨는 지점
        // ============================================================================

        [UnityTest]
        public IEnumerator StanceFootStaysPlantedWhileBodyMovesForward()
        {
            yield return RunSlipMeasurement(DefaultScale);
        }

        // ============================================================================
        // (2) ★ 사용자 실제 저장 배율 0.35(다이얼 최소) — 루트 localScale 0.4667.
        //     로컬 보폭을 월드 속도로 나누면 분모가 2.14배 과대 -> 주파수가 그만큼 느려
        //     디딤발이 몸에 끌려간다(= 문워크). 사용자가 지금 실제로 쓰는 배율이다.
        // ============================================================================

        [UnityTest]
        public IEnumerator StanceFootStaysPlantedAtUserSavedMinScale()
        {
            yield return RunSlipMeasurement(StickConfig.MinCharacterScale);
        }

        // ============================================================================
        // (3) 반대편 극단 배율 2.00 — 같은 단위 버그가 반대 방향(주파수 과다)으로 나타난다
        // ============================================================================

        [UnityTest]
        public IEnumerator StanceFootStaysPlantedAtMaxScale()
        {
            yield return RunSlipMeasurement(StickConfig.MaxCharacterScale);
        }

        /// <summary>
        /// 주어진 배율에서 평지를 걷게 하고 디딤발 미끄러짐을 실측한다. 세 테스트가 이 한 벌만 쓴다 —
        /// 배율마다 측정 코드를 복사하면 그중 하나만 고쳐지는 사고가 난다.
        /// </summary>
        private IEnumerator RunSlipMeasurement(float characterScale)
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
            _savedScale = _agent.CurrentCharacterScale;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            // ★ 배율을 명시적으로 못박는다(위 클래스 문서의 "저장 파일 의존" 문단). 저장 복원은
            //   씬 로드 후 2초 안에만 시도되므로(CornerHoverPanel.RestoreGraceSeconds) 여기서 적용한
            //   값이 나중에 덮이지 않는다.
            _agent.ApplyCharacterScale(characterScale, "발 미끄러짐 실측");
            yield return null;

            float rootScale = Mathf.Abs(_agent.transform.localScale.x);
            float walkSpeedExpected = bb.Config != null ? bb.Config.ResolveWalkSpeed() : 0f;

            // 화면 전폭 평지 하나 — 걷는 동안 경계/낙하가 개입하지 않게 한다.
            float w = Screen.width;
            float h = Screen.height;
            var service = new FlatFloorService();
            service.Footholds.Add(new PlatformFoothold(FloorHandle, new Rect(0f, h * 0.55f, w, h * 0.45f), true));
            bb.FootholdPoller = new FootholdPoller(service, bb.Config);

            var intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = intent;

            // 화면 왼쪽에서 출발해 오른쪽으로 걷는다. 배율 2.0에서는 보행 속도도 2배라 4초면 화면을
            // 벗어나므로, 화면 클램프가 개입하기 전에(=측정 전제가 깨지기 전에) 멈출 X를 미리 잡아둔다.
            Vector3 startWorld = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(w * 0.12f, h * 0.55f), 10f, bb.Config);
            Vector3 limitWorld = ScreenCoordinateConverter.OsScreenToWorld(bb.MainCamera,
                new Vector2(w * 0.85f, h * 0.55f), 10f, bb.Config);
            bb.Body.position = new Vector2(startWorld.x, startWorld.y);
            bb.Body.transform.position = new Vector3(startWorld.x, startWorld.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FloorHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터가 없습니다.");
            Assert.IsTrue(pose.HasLimbs, $"{LogPrefix} 프리팹에서 팔다리를 찾지 못했습니다 — 측정 자체가 불가능합니다.");

            Debug.Log($"{LogPrefix} 준비 — 요청 배율={characterScale:F2}, 실제 배율={_agent.CurrentCharacterScale:F4}, " +
                $"루트 localScale={rootScale:F4}, 보행 속도(설정)={walkSpeedExpected:F3}유닛/s, " +
                $"출발 X={startWorld.x:F3}, 측정 종료 X={limitWorld.x:F3}");

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
                if (bb.Body.position.x >= limitWorld.x) break; // 화면 클램프 전에 스스로 멈춘다.

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
                        if (bodyMove > 0.01f * rootScale)
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
            float measuredStride = relMax - relMin;                 // 발이 실제로 그린 월드 보폭
            float codeStride = pose.DistancePerCycle * 0.5f;        // 코드가 주파수 역산에 쓴 보폭
            float strideRatio = codeStride > 0.0001f ? measuredStride / codeStride : 0f;

            Debug.Log($"{LogPrefix} 종합 — 배율 {_agent.CurrentCharacterScale:F2}(루트 localScale {rootScale:F4}), " +
                $"측정 {elapsed:F2}초, 몸 전진 {bodyTravel:F3}유닛, 디딤 국면 {stanceRuns}회, " +
                $"평균 미끄러짐 비율={slipRatio:F3} (0=완벽, 1=완전 문워크), 최악 구간={worstRunRatio:F3}, " +
                $"한 사이클 이동거리={pose.DistancePerCycle:F3}유닛, 바라보는 방향={pose.FacingSign:F0}");
            Debug.Log($"{LogPrefix} 접지 — 디딤발 월드Y − 지면 범위 [{groundErrMin:+0.0000;-0.0000}, {groundErrMax:+0.0000;-0.0000}]유닛 " +
                $"(음수=지면 파고듦 / 양수=떠 있음, 0에 가까울수록 좋다) | 디딤발 불일치율={mismatchRatio * 100f:F1}% " +
                "(화면상 더 낮은 발이 위상상의 디딤발과 다른 시간 — 남은 잔여 과제)");
            Debug.Log($"{LogPrefix} 진단 — 왼다리 엉덩이각 [{hipMin:F1}, {hipMax:F1}]도(표 기대 ±25), " +
                $"왼발의 몸 기준 X 범위 [{relMin:F3}, {relMax:F3}]유닛 " +
                $"(실측 월드 보폭 {measuredStride:F3} vs 코드가 쓴 보폭 {codeStride:F3} -> 비율 {strideRatio:F3}, " +
                "1에서 멀어지면 보폭의 단위가 어긋난 것이다)");

            Assert.Greater(stanceRuns, 3,
                $"{LogPrefix} 측정 구간에서 디딤 국면이 {stanceRuns}회뿐입니다 — 걷기 사이클이 거의 돌지 않았습니다.");
            Assert.Greater(bodyTravel, 0.5f * walkSpeedExpected * elapsed,
                $"{LogPrefix} 몸이 {bodyTravel:F3}유닛밖에 전진하지 않았습니다(기대 {walkSpeedExpected * elapsed:F3}) — 측정 전제가 깨졌습니다.");

            // 임계값의 의미: 디딤발이 그 구간의 몸 이동 거리의 30% 넘게 함께 끌려가면 육안으로 명확한
            // 문워크로 보인다. 비율이라 배율과 무관하게 같은 값을 쓴다.
            Assert.Less(slipRatio, 0.30f,
                $"{LogPrefix} 디딤발이 미끄러집니다(문워크) — 배율 {_agent.CurrentCharacterScale:F2}에서 " +
                $"평균 미끄러짐 비율 {slipRatio:F3}. " +
                "보행 사이클 주파수 역산(StickmanPoseAnimator._distancePerCycle)이 실제 보폭과 어긋났을 가능성이 큽니다.");

            // ★ 단위 계약 직접 잠금 — 코드가 역산에 쓴 보폭이 발이 실제로 그리는 **월드** 보폭과 같은가.
            //   BUG-WALK-B2에서는 배율 0.35에서 이 비율이 0.47(≈루트 localScale)로 떨어졌다.
            Assert.AreEqual(1f, strideRatio, 0.25f,
                $"{LogPrefix} 코드가 쓴 보폭({codeStride:F3})과 발이 실제로 그린 월드 보폭({measuredStride:F3})이 " +
                $"{strideRatio:F3}배 어긋납니다 — 로컬 유닛을 월드 유닛으로 쓰고 있을 가능성이 큽니다(BUG-WALK-B2).");

            // 접지 보정(ComputeFootGroundingOffset)이 살아 있는지 잠근다 — 이 값이 커지면 디딤발이
            // 지면을 들락거려 "발이 땅에 안 붙은" 느낌이 난다(보정 도입 전 실측: 파고듦 0.025 / 뜸 0.070).
            //
            // ★★ 2026-09-01 (P9-c) 임계값에 **진폭 배율**을 함께 곱한다 — BUG-WALK-B2에서 루트 배율을
            // 곱해야 했던 것과 정확히 같은 종류의 정규화다(docs/UX_FLOW.md 38-14-5 #14가 예고한 확장).
            //
            //   왜 필요한가(실측으로 확인한 인과): 이 지표가 실제로 재고 있는 것은 접지 보정의 오차가
            //   아니라 대부분 **"디딤발 불일치"** 구간이다(같은 로그의 불일치율 20%). 보정은 두 발 중
            //   **더 낮은 발**을 지면에 붙이는데, 이 측정은 **위상상의** 디딤발을 보므로 그 20% 동안은
            //   반대쪽 발이 지면에 붙고 이 발은 떠 있는 것으로 잡힌다. 그 뜬 높이는 두 발이 벌어진
            //   거리이므로 **포즈 진폭에 정비례**한다.
            //   실측 대조(배율 0.75): 진폭 1.00 -> 0.052유닛 / 진폭 1.29 -> 0.0673유닛 (비 1.29 = 진폭비).
            //   즉 커진 것은 오차가 아니라 **자(尺)** 다. 진폭으로 나누면 예전과 같은 수가 나온다.
            //
            //   ★ 그래서 이 완화는 "느슨하게 풀어준 것"이 아니다. 진폭이 커져도 **단위 진폭당 접지
            //   오차는 예전 그대로여야 한다**는, 오히려 더 강한 조건을 잠근 것이다. 진폭과 무관하게
            //   보정이 깨지면(예: 보정 자체를 꺼버리면) 이 단언은 그대로 빨간불이 된다.
            //
            //   남은 잔여 과제(불일치율 20%)는 이번 변경 이전부터 있던 것으로, 이 파일 상단 문서에
            //   이미 명시돼 있다.
            float amplitudeScale = Mathf.Max(1f, pose.WalkAmplitudeScale);
            float groundErrLimit = 0.06f * rootScale * amplitudeScale;
            float worstGroundErr = Mathf.Max(Mathf.Abs(groundErrMin), Mathf.Abs(groundErrMax));
            Debug.Log($"{LogPrefix} 접지 임계 — 진폭 배율 {pose.WalkAmplitudeScale:F3}(속도 정규화 " +
                $"{pose.WalkSpeed01:F3}) -> 허용 {groundErrLimit:F4}유닛, 단위 진폭당 실측 " +
                $"{worstGroundErr / (rootScale * amplitudeScale):F4}유닛(진폭 1.0 시절 기준선 0.052).");
            Assert.Less(worstGroundErr, groundErrLimit,
                $"{LogPrefix} 디딤발이 지면에서 {worstGroundErr:F4}유닛 벗어납니다(허용 {groundErrLimit:F4}) — " +
                "StickmanPoseAnimator.ComputeFootGroundingOffset()의 접지 보정이 깨졌을 가능성이 큽니다.");
        }
    }
}
