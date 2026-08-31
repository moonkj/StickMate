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
    /// ★ GETUP(기상) 중 캐릭터 잉크가 발판 아래로 뚫고 나가지 않는다 — <b>결정론적</b> 회귀 테스트.
    ///
    /// ============================================================================
    /// 왜 이 파일이 따로 있는가 (FloorContactVisibilityTests가 이미 같은 불변식을 재는데)
    /// ============================================================================
    /// 그 테스트는 <b>자연 발생 랙돌</b>에 의존한다. 디버거의 실측으로 이 결함의 발생률이
    /// <b>사이클당 약 3~4%</b>임이 확인됐고(자연 발생 80사이클 중 3회), 그래서 전체 스위트 4회 중
    /// 1회만 실패하는 플레이키로 보였다. "격리 실행 2회 통과"는 아무것도 반증하지 못한다
    /// (3~4%에서 2회 연속 통과 확률 ≈ 93%).
    ///
    /// 이 파일은 그 확률을 없앤다: 랙돌 진입 직후 각속도를 −600~600도/s로 <b>강제 산포</b>해
    /// "널브러진 정착 각도"를 인위적으로 흩뿌린다. 디버거가 진단용으로 쓴 임시 하네스
    /// (Tests/PlayMode/ZZGetupAngleSweep)의 스윕을 그대로 정식 승격한 것이며, 그 하네스는 이 파일로
    /// 대체되어 삭제됐다.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가 (근본 원인, 2026-08-31 디버거 확정)
    /// ============================================================================
    /// GETUP은 팔다리를 Kinematic으로 되돌린 뒤 각도를 보간하므로 <b>콜라이더의 충돌 해소가 없다</b>
    /// (RAGDOLL 구간에는 있어서, 잉크가 발판 상단 아래 최대 4.6pt에서 멈춘다). 그 상태에서 접지 규약
    /// ("루트 원점 = 발바닥")이 루트 Y를 발판 상단에 못박으면, 아직 누워 있는 몸의 반대편 파츠는
    /// 기하학적으로 발판 아래로 갈 수밖에 없다. 실측 최악 <b>발판 상단 아래 20.5pt</b>.
    /// 침투 경로는 둘이며 <b>둘 다</b> 이 스윕이 덮는다:
    ///   · 진입 스냅  — 정착각 |각도| &gt;= 87도에서 GETUP <b>첫 프레임</b>부터(약 1.2pt/도)
    ///   · 보간 중 스윙 — 정착각이 안전(83.8도)해도 보간 도중 최대 +7.70pt
    ///
    /// ★ 임계값(1pt)은 <b>절대 올리지 않는다.</b> 획 두께(2.5pt)의 절반 미만 = 육안 불가능이라는
    /// 제품 불변식이고, 반대로 안전망 상수(8pt)를 실측 최악치까지 올리는 처방은 서 있을 때 발이
    /// 19pt(키의 27%) 뜨게 만들어 사용자가 <b>세 번 신고한 "떠 있다"</b>를 정면 재발시킨다.
    /// 수정은 상수가 아니라 유도값이다(States/StickmanBlackboard.TickInkFloorClearance).
    /// </summary>
    public sealed class GetupFloorClearanceTests
    {
        private const string LogPrefix = "[GETUP-FLOOR]";

        /// <summary>잉크가 화면 아래로 나가도 되는 허용치(OS 포인트). FloorContactVisibilityTests와
        /// <b>같은 값을 의도적으로 중복</b>해 둔다 — 두 파일은 같은 제품 불변식을 서로 다른 경로(자연
        /// 발생 / 강제 산포)로 재는 관계라, 한쪽에서 이 값을 올리려는 시도가 다른 쪽에서 그대로
        /// 실패로 드러나는 편이 안전하다.</summary>
        private const float MaxInkBelowScreenPoints = 1f;

        /// <summary>랙돌 진입 직후 강제로 넣는 각속도(도/초). 이 산포가 "널브러진 정착 각도"를 만들고,
        /// 그 각도가 곧 침투 깊이를 결정한다(약 1.2pt/도). 디버거의 원본 스윕과 같은 31점이다.</summary>
        private static readonly float[] SpinsDegreesPerSecond =
        {
              0f,   60f,  -60f,  120f, -120f,  180f, -180f,  240f, -240f,  300f, -300f,
            360f, -360f,  420f, -420f,  480f, -480f,  540f, -540f,  600f, -600f,
             90f,  -90f,  150f, -150f,  210f, -210f,  270f, -270f,  330f, -330f
        };

        /// <summary>스폰 낙하 -> 착지 확인 타임아웃(초).</summary>
        private const float LandingTimeoutSeconds = 10f;

        /// <summary>한 사이클(랙돌 강제 -> 정착 -> 기상 -> Idle 복귀) 관찰 타임아웃(초).</summary>
        private const float CycleTimeoutSeconds = 15f;

        /// <summary>랙돌을 강제하기 전 캐릭터가 안정되기를 기다리는 시간(초).</summary>
        private const float PreImpactSettleSeconds = 0.8f;

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;

        // 스윕 결과(코루틴은 값을 반환할 수 없어 필드로 받는다).
        private float _worstBelowPt;
        private float _worstBelowSpin;
        private float _worstBelowSettleAngle;
        private string _worstBelowPart;
        private float _maxObservedSettleAngle;
        private int _cyclesWithoutGetup;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null && _originalConfig != null)
            {
                _agent.Blackboard.Config = _originalConfig;
            }
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _originalConfig = null;
            _agent = null;
        }

        // ============================================================================
        // T1 — 본 조건: 어떤 정착 각도에서도 기상 중 잉크가 화면 아래로 잘리지 않는다
        // ============================================================================

        [UnityTest]
        public IEnumerator 기상_중_어떤_정착각에서도_잉크가_화면_아래로_잘리지_않는다()
        {
            yield return RunSweep(floorClearanceEnabled: true);

            Assert.AreEqual(0, _cyclesWithoutGetup,
                $"{LogPrefix} 전제 실패 — {_cyclesWithoutGetup}개 사이클에서 GETUP을 한 번도 관측하지 " +
                "못했습니다. 랙돌 강제/정착 판정이 바뀌었을 수 있으며, 그러면 이 스윕은 아무것도 " +
                "검증하지 못한 채 초록불이 됩니다.");

            Assert.LessOrEqual(_worstBelowPt, MaxInkBelowScreenPoints,
                $"{LogPrefix} 기상 중 캐릭터 잉크가 화면 아래로 {_worstBelowPt:F2}pt 잘려 나갔습니다 " +
                $"(허용 {MaxInkBelowScreenPoints}pt, spin={_worstBelowSpin:F0}도/s, " +
                $"정착각={_worstBelowSettleAngle:F1}도, 최저파츠={_worstBelowPart}). " +
                "원인 후보: (a) StickConfig.getupFloorClearanceEnabled가 꺼졌다, " +
                "(b) StickmanAgent.Update()에서 TickInkFloorClearance() 호출이 빠졌거나 " +
                "**접지 안전망보다 앞으로** 옮겨졌다(그러면 SnapToGround가 리프트를 도로 눌러 내린다), " +
                "(c) 새 부품이 잉크를 더하면서 ICharacterInkExtentProvider를 구현하지 않았다, " +
                "(d) 리프트를 상태(GetupState.Tick) 안으로 옮겼다 — 전이 프레임에는 새 상태의 Tick이 " +
                "돌지 않으므로 가장 깊은 첫 프레임을 통째로 놓친다.");
        }

        // ============================================================================
        // T1n — 네거티브 컨트롤: 클리어런스를 끄면 같은 스윕이 실제로 바닥을 뚫는다
        //       (= 이 테스트가 "돌아갈 것 같다"가 아니라 진짜 결함을 잡는다는 증거)
        // ============================================================================

        [UnityTest]
        public IEnumerator 네거티브컨트롤_바닥_클리어런스를_끄면_같은_스윕이_바닥을_뚫는다()
        {
            yield return RunSweep(floorClearanceEnabled: false);

            Assert.Greater(_worstBelowPt, MaxInkBelowScreenPoints,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 바닥 클리어런스를 껐는데도 잉크가 화면 아래로 " +
                $"나가지 않았습니다(최악 {_worstBelowPt:F2}pt, 관측 최대 정착각 " +
                $"{_maxObservedSettleAngle:F1}도). 이 스윕이 실제로 결함을 재현한다는 증거가 성립하지 " +
                "않으므로, 시나리오(각속도 산포 폭/관측 구간)를 다시 설계해야 합니다.");
        }

        // ============================================================================

        private IEnumerator RunSweep(bool floorClearanceEnabled)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"{LogPrefix} Camera.main을 찾지 못했습니다.");
            Assert.IsTrue(cam.orthographic, $"{LogPrefix} 이 테스트는 orthographic 카메라를 가정합니다.");

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            Assert.IsNotNull(_originalConfig, $"{LogPrefix} 전제 실패 — 블랙보드에 StickConfig가 없습니다.");

            // 배포 에셋을 직접 만지지 않는다(DockSinkholeRegressionTests와 같은 관례) — 스위치를
            // 되돌리는 것을 잊으면 그 값이 프로젝트에 그대로 남는다.
            _clonedConfig = Object.Instantiate(_originalConfig);
            _clonedConfig.getupFloorClearanceEnabled = floorClearanceEnabled;
            bb.Config = _clonedConfig;

            Renderer[] renderers = _agent.GetComponentsInChildren<Renderer>(true);
            Assert.IsTrue(renderers.Length > 0, $"{LogPrefix} 캐릭터 렌더러를 찾지 못했습니다.");
            var body = _agent.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, $"{LogPrefix} 캐릭터 루트에 Rigidbody2D가 없습니다.");

            // 배치 모드에는 실제 창이 없으므로 유일한 발판은 화면 최하단 안전망(더미 발판)이다 —
            // 즉 "화면 바닥까지의 거리"가 곧 "발판 상단까지의 거리 − 안전망 여백"이다.
            float screenBottomWorldY = cam.transform.position.y - cam.orthographicSize;
            float ptPerUnit = NullPlatformWindowService.ReferenceScreenHeightPoints / (2f * cam.orthographicSize);

            Debug.Log($"{LogPrefix} 스윕 시작 — 클리어런스={floorClearanceEnabled} " +
                $"스핀 {SpinsDegreesPerSecond.Length}점, 렌더러 {renderers.Length}개, " +
                $"화면바닥 월드Y={screenBottomWorldY:F4}, pt/유닛={ptPerUnit:F3}, " +
                $"안전망여백={NullPlatformWindowService.BottomSafetyNetInsetPoints}pt.");

            yield return WaitForLanding();

            _worstBelowPt = float.NegativeInfinity;
            _worstBelowSpin = 0f;
            _worstBelowSettleAngle = 0f;
            _worstBelowPart = "-";
            _maxObservedSettleAngle = 0f;
            _cyclesWithoutGetup = 0;

            float threshold = _clonedConfig.ragdollForceThreshold;

            for (int i = 0; i < SpinsDegreesPerSecond.Length; i++)
            {
                float spin = SpinsDegreesPerSecond[i];
                yield return new WaitForSeconds(PreImpactSettleSeconds);

                _agent.ReportExternalImpact(threshold * 5f);
                yield return new WaitForFixedUpdate();
                body.angularVelocity = spin;   // 정착 각도를 인위적으로 흩뿌린다.

                float settleAngle = float.NaN;
                float worstBelow = float.NegativeInfinity;
                string worstPart = "-";
                bool sawGetup = false;
                bool inGetup = false;
                int episodes = 0;
                bool done = false;
                float startTime = Time.time;

                while (!done && Time.time - startTime < CycleTimeoutSeconds)
                {
                    yield return null;   // ★ 매 프레임 관측 — 0.05초 샘플링은 짧은 관통을 놓칠 수 있다.
                    StickmanStateId state = bb.Machine.CurrentStateId;

                    if (state == StickmanStateId.Getup)
                    {
                        if (!inGetup) { inGetup = true; episodes++; }
                        if (!sawGetup) { sawGetup = true; settleAngle = body.rotation; }

                        // 에피소드 1만 본다 — 기상 도중 다시 랙돌이 되면(재랙돌) 그 뒤의 값은 이
                        // 스윕이 만든 각도와 무관해져 원인 추적이 흐려진다.
                        if (episodes == 1 && StickmanInkBounds.TryCompute(renderers, out Bounds ink))
                        {
                            float belowPt = -((ink.min.y - screenBottomWorldY) * ptPerUnit);
                            if (belowPt > worstBelow)
                            {
                                worstBelow = belowPt;
                                worstPart = LowestPartName(renderers);
                            }
                        }
                    }
                    else
                    {
                        inGetup = false;
                        if (sawGetup && (state == StickmanStateId.Idle || state == StickmanStateId.Walk)) done = true;
                    }
                }

                if (!sawGetup) _cyclesWithoutGetup++;
                float absSettle = Mathf.Abs(settleAngle);
                if (!float.IsNaN(absSettle) && absSettle > _maxObservedSettleAngle) _maxObservedSettleAngle = absSettle;
                if (worstBelow > _worstBelowPt)
                {
                    _worstBelowPt = worstBelow;
                    _worstBelowSpin = spin;
                    _worstBelowSettleAngle = settleAngle;
                    _worstBelowPart = worstPart;
                }

                Debug.Log($"{LogPrefix} spin={spin:F0}도/s 정착각={settleAngle:F1}도(|{absSettle:F1}|) " +
                    $"기상중 화면바닥아래최대={worstBelow:F2}pt 최저파츠={worstPart} " +
                    $"기상에피소드={episodes} 사이클완료={done}");
            }

            Debug.Log($"{LogPrefix} 스윕 종료 — 클리어런스={floorClearanceEnabled} " +
                $"최악={_worstBelowPt:F2}pt(spin={_worstBelowSpin:F0}, 정착각={_worstBelowSettleAngle:F1}도, " +
                $"{_worstBelowPart}), 관측 최대 정착각={_maxObservedSettleAngle:F1}도, " +
                $"기상 미관측 사이클={_cyclesWithoutGetup}개, 허용={MaxInkBelowScreenPoints}pt.");
        }

        private IEnumerator WaitForLanding()
        {
            float elapsed = 0f;
            while (elapsed < LandingTimeoutSeconds)
            {
                yield return new WaitForSeconds(0.05f);
                elapsed += 0.05f;
                StickmanStateId s = _agent.Blackboard.Machine.CurrentStateId;
                if (s == StickmanStateId.Idle || s == StickmanStateId.Walk) yield break;
            }
            Assert.Fail($"{LogPrefix} {LandingTimeoutSeconds:F0}초 안에 스폰 낙하 후 착지를 확인하지 못했습니다 — " +
                "착지 자체가 안 되는 회귀일 수 있습니다.");
        }

        /// <summary>실패 메시지에 "무엇이 뚫었는지"를 남기기 위한 진단 — 가장 낮은 렌더러 이름.
        /// 실패 순간에만 관심 있는 값이지만, 최악값이 갱신될 때만 부르므로 사이클당 몇 번뿐이다.</summary>
        private static string LowestPartName(Renderer[] renderers)
        {
            string name = "-";
            float lowest = float.PositiveInfinity;
            var one = new Renderer[1];
            for (int i = 0; i < renderers.Length; i++)
            {
                one[0] = renderers[i];
                if (!StickmanInkBounds.TryCompute(one, out Bounds b)) continue;
                if (b.min.y < lowest) { lowest = b.min.y; name = renderers[i].name; }
            }
            return name;
        }
    }
}
