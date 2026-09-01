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
    /// ★★ "캐릭터가 Dock에서 내려오지 못한다"(2026-09-02 회귀)의 실측 잠금.
    ///
    /// ============================================================================
    /// 무슨 일이 있었나 — 실측(신빌드 2인스턴스 111.1분 전수 집계)
    /// ============================================================================
    /// <code>
    /// [뛰어내리기] 발을 뗍니다            64회
    /// 발판변경 0 -> -2 (다시 Dock)        66회
    /// 발판변경 0 -> -1 / -3 (아래 착지)    0회   ← 한 번도 안 내려갔다
    /// 캐릭터 OS y 표본                    906.8~907.0 뿐(Dock 상단면 아래로 0.2pt도 안 내려감)
    /// </code>
    /// 로그가 그대로 찍는다: <c>[FallState] 착지 확정 — 발판핸들=-2(Dock), 낙하높이=0.00유닛</c>.
    ///
    /// ============================================================================
    /// 근본 원인 — <b>콜라이더가 막은 것이 아니라 마찰이 세웠다</b>
    /// ============================================================================
    /// 선행 조사(docs/MOTION_SPEC.md 23-1)의 유력 가설은 "drop-through는 논리 발판만 무시하므로
    /// <see cref="DockPhysicsStep"/>의 콜라이더를 통과하지 못한다"였다. 절반만 맞다 — 통과할 필요가
    /// <b>애초에 없다</b>. 캐릭터는 계단을 뚫는 것이 아니라 <b>옆으로 걸어 나가면</b> 되고, 모서리를
    /// 넘는 순간 루트 캡슐의 바닥 원이 계단 상단면을 벗어나 그대로 떨어진다.
    ///
    /// <para>진짜 원인은 <b>그 모서리에 닿지 못한다</b>는 것이다. 계단은 실제 <c>BoxCollider2D</c>라
    /// 그 위에 얹힌 몸에 쿨롱 마찰이 걸린다(ProjectSettings의 <c>m_DefaultMaterial</c>이 비어 있어
    /// Unity 2D 기본 마찰 0.4가 적용된다):</para>
    /// <code>
    /// 감속 a = 0.4 x 9.81 x gravityScale(3) = 11.77유닛/초²
    /// 배율 0.60의 내딛는 속도 v = 2.5 x 0.60 x 0.8 = 1.20유닛/초
    /// 정지 거리 = v² / 2a = 0.061유닛
    /// 실측 남은 거리(로그 64건) = 0.090 ~ 0.117유닛   ← 전부 정지 거리보다 멀다
    /// </code>
    /// 그래서 몸은 모서리에 닿기 전에 멈추고, 유예(0.25초)가 끝나면 같은 Dock에 낙차 0으로 다시
    /// 착지한다. <b>이 기전은 콜라이더가 있는 발판(= Dock)에서만 성립한다</b> — 실제 창 상단은 논리
    /// 발판일 뿐 콜라이더가 없어 마찰 자체가 없다.
    ///
    /// ============================================================================
    /// ★ 왜 기존 테스트가 이 회귀를 통과시켰는가 (구조적 이유 2개)
    /// ============================================================================
    ///   (1) <see cref="EdgeHopDownTests"/>의 "Dock"은 핸들 <b>8001</b>인 합성 발판이다.
    ///       <see cref="DockPhysicsStep"/>은 <see cref="FallbackPlatformWindowService.DockFootholdHandle"/>
    ///       (-2)만 따라가므로 그 배치에는 <b>물리 계단이 아예 서지 않는다</b> — 마찰이 없으니
    ///       뛰어내리기가 언제나 성공한다. <b>그 테스트는 이 실패를 볼 수 없는 배치였다.</b>
    ///       그래서 이 파일은 <see cref="DockPhysicsStepTests"/>와 같이 <b>핸들 -2/-1/-3</b>을 쓴다.
    ///   (2) 배율을 리그 기본 한 점에서만 봤다. 이 파일은 <b>6점 루프</b>로 돈다.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// <b>"선택 가능한 모든 배율에서 캐릭터는 Dock에서 스스로 내려올 수 있다."</b>
    /// 갈래(뛰어내리기 / 매달려 내려가기)는 배율에 따라 정당하게 바뀌는 <b>구현 세부</b>이므로
    /// 티어 이름을 잠그지 않는다 — 실제로 이번 회귀는 그 갈래가 바뀌면서 터졌다. 잠그는 것은
    /// <b>결과</b>다: 아래 발판(-1/-3)에 실제로 착지하는가.
    /// </summary>
    public sealed class DockHopDownPhysicsStepTests
    {
        private const string LogPrefix = "[DOCK하강-TEST]";

        // ★ 프로덕션과 **같은 핸들**을 쓴다. 이것이 이 파일의 핵심 장치다(클래스 문서 (1) 참고).
        private const long DockHandle = FallbackPlatformWindowService.DockFootholdHandle;              // -2
        private const long NetLeftHandle = FallbackPlatformWindowService.SyntheticFootholdHandle;      // -1
        private const long NetRightHandle = FallbackPlatformWindowService.SyntheticFootholdHandleRight; // -3

        /// <summary>Dock 상단 → 바닥 안전망 상단 낙차. 하드코딩하지 않는다(Core/DockGeometry.cs 단일 소스).</summary>
        private static readonly float DockDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        /// <summary>Dock 가로 구간(화면 폭 비율) — DockPhysicsStepTests와 같은 배치.</summary>
        private const float DockLeftFraction = 0.25f;
        private const float DockRightFraction = 0.75f;

        /// <summary>사용자가 실제로 저장해 쓰고 있던 배율(현장 로그에서 관측된 <b>테스트 입력</b>이지
        /// 프로덕션 상수가 아니다 — 그래서 여기 숫자로 적는다). 이 회귀가 터진 바로 그 점이다.</summary>
        private const float UserSavedScale = 0.60f;

        /// <summary>★ 배율 6점(docs/MOTION_SPEC.md 23-4 지침). 프로덕션 상수는 <b>참조</b>하고,
        /// 테스트 입력(현장 배율)만 숫자로 적는다.</summary>
        private static float[] ScaleSweep => new[]
        {
            StickConfig.MinCharacterScale,          // 0.35  슬라이더 하한
            StickConfig.DockHopDownCriticalScale,   // 0.4493 뛰어내리기 <-> 매달리기 분기점
            UserSavedScale,                         // 0.60  이 회귀가 터진 현장 배율
            StickConfig.DockKneelCriticalScale,     // 0.8180 T0.5 <-> T1 분기점
            0.9f,                                   // 중간 한 점(경계에만 몰리지 않게)
            StickConfig.MaxCharacterScale,          // 1.00  슬라이더 상한
        };

        /// <summary>한 번의 하강을 관찰하는 벽시계 예산(초). 낙차 1.64유닛의 자유낙하는 0.34초,
        /// 모서리까지 걸어 나가는 데 0.25초 이내, 착지 연출까지 더해도 1초를 넘지 않는다 —
        /// 4초는 그 4배 이상이다. ★ 프레임 수가 아니라 <b>초</b>로 잡는다(CLAUDE.md 협업 프로토콜:
        /// 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌 수 있어 프레임 예산은 거짓말을 한다).</summary>
        private const float ObserveBudgetSeconds = 4f;

        private const float SettleWaitSeconds = 2.0f;

        private sealed class TestFootholdService : IPlatformWindowService
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
            public bool LedgeHangRequested { get; set; }
            public bool HopDownRequested { get; set; }
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;
        private float _restoreScale;

        private TestFootholdService _service;
        private DockPhysicsStep _step;

        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockRightWorldX;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null)
            {
                if (_restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
                if (_agent.Blackboard != null)
                {
                    if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                    if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                    if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                }
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _step = null;
            _restoreScale = 0f;
        }

        // ====================================================================
        // 준비 — 프로덕션과 같은 Dock 배치(핸들 -2 + 실제 물리 계단)
        // ====================================================================

        private IEnumerator SetUpProductionDock(bool stepOffCarry)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            var steps = Object.FindObjectsByType<DockPhysicsStep>(FindObjectsSortMode.None);
            Assert.AreEqual(1, steps.Length,
                $"{LogPrefix} 씬의 DockPhysicsStep이 {steps.Length}개입니다 — 1개여야 합니다(SceneBootstrapper 배선 확인).");
            _step = steps[0];

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
            _restoreScale = _agent.CurrentCharacterScale;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            _clonedConfig.hopDownStepOffCarryEnabled = stepOffCarry;
            // 배회 AI가 끼어들면 "언제 발을 떼는가"가 비결정적이 된다 — 의도는 스크립트가 준다.
            var intent = new ScriptedIntentSource();
            bb.IntentSource = intent;

            var ground = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(ground, $"{LogPrefix} 전제 실패 — 씬에 PhysicsGround가 없습니다.");
            _floorTopWorldY = ground.GetComponent<BoxCollider2D>().bounds.max.y;
            _dockTopWorldY = _floorTopWorldY + DockDropUnits;

            Camera cam = bb.MainCamera;
            float floorTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _floorTopWorldY), _clonedConfig, out _).y;
            float dockTopOsY = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector2(0f, _dockTopWorldY), _clonedConfig, out _).y;

            float w = Screen.width;
            float h = Screen.height;
            float leftOs = w * DockLeftFraction;
            float rightOs = w * DockRightFraction;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(DockHandle, new Rect(leftOs, dockTopOsY, rightOs - leftOs, h - dockTopOsY), true));
            _service.Footholds.Add(new PlatformFoothold(NetLeftHandle, new Rect(0f, floorTopOsY, leftOs, h - floorTopOsY), false));
            _service.Footholds.Add(new PlatformFoothold(NetRightHandle, new Rect(rightOs, floorTopOsY, w - rightOs, h - floorTopOsY), false));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _dockRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(rightOs, dockTopOsY), 10f, _clonedConfig).x;

            // 계단은 DockPhysicsStep.Update()가 세운다 — 몇 프레임 준다.
            yield return null;
            yield return null;
            yield return null;

            // ★ 전제 — 물리 계단이 실제로 서 있어야 이 파일이 의미가 있다. 이게 없으면
            //   EdgeHopDownTests와 똑같이 "실패를 볼 수 없는 배치"가 된다.
            Assert.IsTrue(_step.IsActive,
                $"{LogPrefix} 전제 실패 — Dock 발판(핸들 {DockHandle})을 깔았는데 물리 계단이 서지 않았습니다. " +
                "이 상태로는 이 파일이 검증하려는 마찰 상황이 재현되지 않습니다(dockPhysicsStepEnabled / 배선 확인).");
            Assert.LessOrEqual(_step.StepBounds.max.x, _dockRightWorldX + 0.01f,
                $"{LogPrefix} 전제 실패 — 물리 계단의 오른쪽 옆면({_step.StepBounds.max.x:F3})이 논리 Dock 모서리" +
                $"({_dockRightWorldX:F3})보다 바깥입니다. 그러면 모서리를 넘어도 보이지 않는 턱 위에 남습니다.");

            Debug.Log($"{LogPrefix} 준비 — 물리바닥 상단 y={_floorTopWorldY:F4}, Dock 상단 y={_dockTopWorldY:F4}(낙차 {DockDropUnits:F4}), " +
                $"Dock 오른쪽 모서리 x={_dockRightWorldX:F4}, 계단 x {_step.StepBounds.min.x:F3}~{_step.StepBounds.max.x:F3} " +
                $"윗면 y={_step.StepBounds.max.y:F3}, 발떼기이송={stepOffCarry}.");
        }

        /// <summary>한 배율에서의 하강 시도 결과.</summary>
        private sealed class DescentResult
        {
            public float Scale;
            public string Branch = "(없음)";     // 뛰어내리기 / 매달려내려가기
            public float StartWorldX;
            public float DistanceToEdge;
            public float StepOffSpeed;
            public float LowestWorldY;
            public long LandedHandle;
            public bool Descended;
            public float Seconds;
        }

        /// <summary>
        /// Dock 오른쪽 모서리 앞에 세우고, <b>그 배율에서 시스템이 스스로 고른 갈래</b>로 하강을 시도한다.
        /// 갈래 선택 순서(뛰어내리기 -> 매달리기)는 States/AutoWanderController.TryBoundaryBehaviour와 같다 —
        /// 그래야 이 테스트가 "실제로 일어나는 일"을 보고 있다고 말할 수 있다.
        /// </summary>
        private IEnumerator TryDescendAtScale(float scale, DescentResult result)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            var intent = (ScriptedIntentSource)bb.IntentSource;
            result.Scale = scale;

            _agent.ApplyCharacterScale(scale, $"{LogPrefix} 배율 루프");
            // ★ ApplyCharacterScale은 **에이전트가 배선해 둔 원본 에셋**에 런타임 배율을 쓴다.
            //   블랙보드가 읽는 것은 복제본이므로, 이 한 줄이 없으면 WalkState가 계산하는 내딛는
            //   속도가 배율을 따라오지 않는다(실측: 배율 0.35에서도 1.50유닛/초 = 배포 0.75의 값).
            //   그러면 리그가 프로덕션보다 **빠르게** 걸어 나가 실패를 재현하지 못한다.
            _clonedConfig.SetRuntimeCharacterScale(scale);
            yield return null;

            // 모서리까지 hopDownEdgeCommitDistance만 남긴 자리 = 실제로 발을 떼는 바로 그 지점.
            float startX = _dockRightWorldX - _clonedConfig.hopDownEdgeCommitDistance;
            bb.MoveBodyToWorld(new Vector2(startX, _dockTopWorldY));
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = DockHandle;
            bb.ResetGroundLossTimer();
            intent.MoveInputX = 1f;
            intent.HopDownRequested = false;
            intent.LedgeHangRequested = false;
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
            yield return null;

            result.StartWorldX = bb.Body.position.x;
            result.DistanceToEdge = _dockRightWorldX - result.StartWorldX;
            result.StepOffSpeed = _clonedConfig.ResolveWalkSpeed() * _clonedConfig.hopDownStepOffSpeedScale;

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded,
                $"{LogPrefix} 배율 {scale:F4} 전제 실패 — Dock에 접지하지 못했습니다(핸들 {bb.CurrentFootholdHandle}).");

            if (bb.TryFindHopDownTarget(info, 1, out long hopHandle, out _))
            {
                result.Branch = $"뛰어내리기(목적지 {hopHandle})";
                intent.HopDownRequested = true;
            }
            else if (bb.TryFindDescendTarget(info, 1, out long hangHandle, out _))
            {
                result.Branch = $"매달려내려가기(목적지 {hangHandle})";
                intent.LedgeHangRequested = true;
            }
            else
            {
                // ★ 이 분기 자체가 계약 위반이다 — 내려갈 길이 하나도 없으면 캐릭터는 Dock에 갇힌다.
                Assert.Fail($"{LogPrefix} 배율 {scale:F4}에서 하강 갈래가 **하나도** 성립하지 않습니다 " +
                    $"(낙차 {DockDropUnits:F4}, 뛰어내리기 밴드 [{_clonedConfig.hopDownMinDropHeight:F3}, " +
                    $"{bb.HopDownMaxDropHeight:F3}), 매달리기 최소 {bb.LedgeHangMinDropDepth:F3}). " +
                    "이 배율에서는 캐릭터가 Dock 위에 갇힙니다.");
            }

            // ★ 보는 것은 **이 한 번의 발 떼기가 도달한 첫 착지**뿐이다(2026-09-02 리그 수정).
            //   처음에는 "언젠가 -3을 잡으면 성공"으로 봤는데, 그러면 제자리 재착지(0 -> -2) 뒤에
            //   테스트가 계속 MoveInputX=1을 주고 있어 캐릭터가 **그냥 걸어서** 모서리를 넘어가
            //   -3을 잡는다 — 네거티브 컨트롤이 통째로 무력해졌다(실측으로 잡았다).
            //   현장 로그의 판별식과 정확히 같은 것을 본다: `발판변경 0 -> -2`인가 `0 -> -3`인가.
            result.LowestWorldY = _dockTopWorldY;
            float deadline = Time.time + ObserveBudgetSeconds;
            float started = Time.time;
            bool leftGround = false;

            while (Time.time < deadline)
            {
                yield return null;
                float y = bb.Body.position.y;
                if (y < result.LowestWorldY) result.LowestWorldY = y;

                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.Fall || state == StickmanStateId.LedgeHang)
                {
                    intent.HopDownRequested = false;
                    intent.LedgeHangRequested = false;
                }

                long handle = bb.CurrentFootholdHandle;
                if (!leftGround)
                {
                    // 발을 뗐다 = 고착 핸들이 0(공중)이 된 순간. Fall/LedgeHang 진입이 이걸 만든다.
                    if (handle == 0L) leftGround = true;
                    continue;
                }
                if (handle != 0L) break;   // ★ 첫 착지 — 여기서 결과가 확정된다.
            }

            Assert.IsTrue(leftGround,
                $"{LogPrefix} 배율 {scale:F4}에서 {ObserveBudgetSeconds:F0}초 안에 발을 떼지도 못했습니다" +
                $"(갈래={result.Branch}, 상태={bb.Machine.CurrentStateId}) — 펄스가 소비되지 않았습니다.");

            result.Seconds = Time.time - started;
            result.LandedHandle = bb.CurrentFootholdHandle;
            result.Descended = result.LandedHandle == NetRightHandle || result.LandedHandle == NetLeftHandle;

            Debug.Log($"{LogPrefix} 배율 {result.Scale:F4} — 갈래={result.Branch}, 발 뗀 X={result.StartWorldX:F4}" +
                $"(모서리까지 {result.DistanceToEdge:F4}유닛, 내딛는 속도 {result.StepOffSpeed:F3}유닛/초 " +
                $"-> 필요시간 {(result.StepOffSpeed > 0f ? result.DistanceToEdge / result.StepOffSpeed : -1f):F3}초 / " +
                $"유예 {_clonedConfig.hopDownDropThroughIgnoreDuration:F2}초), " +
                $"최저 y={result.LowestWorldY:F4}(Dock 상단 {_dockTopWorldY:F4}, 아래 발판 {_floorTopWorldY:F4}) " +
                $"= 하강 {(_dockTopWorldY - result.LowestWorldY):F4}유닛, 착지 발판핸들={result.LandedHandle}, " +
                $"하강성공={result.Descended}, {result.Seconds:F2}초.");
        }

        // ====================================================================
        // T1 — ★ 본 검증: 선택 가능한 모든 배율에서 Dock에서 내려온다
        // ====================================================================

        [UnityTest]
        public IEnumerator 전_배율에서_Dock에서_실제로_내려온다()
        {
            yield return SetUpProductionDock(stepOffCarry: true);

            var failures = new List<string>();
            float[] scales = ScaleSweep;
            for (int i = 0; i < scales.Length; i++)
            {
                var r = new DescentResult();
                yield return TryDescendAtScale(scales[i], r);

                float dropped = _dockTopWorldY - r.LowestWorldY;
                if (!r.Descended)
                {
                    failures.Add($"배율 {r.Scale:F4}: 갈래={r.Branch}, 착지핸들={r.LandedHandle}" +
                        $"(기대 {NetRightHandle}), 하강 {dropped:F4}유닛(기대 >= {DockDropUnits * 0.8f:F4})");
                }
                else if (dropped < DockDropUnits * 0.8f)
                {
                    failures.Add($"배율 {r.Scale:F4}: 아래 발판을 잡긴 했지만 실제로는 {dropped:F4}유닛만 " +
                        $"내려갔습니다(낙차 {DockDropUnits:F4}) — 순간이동/재스냅 의심");
                }
                yield return new WaitForSeconds(0.3f);
            }

            Assert.IsEmpty(failures,
                $"{LogPrefix} ★회귀★ Dock에서 내려오지 못한 배율이 있습니다 — 캐릭터가 Dock 위에 갇힙니다.\n  " +
                string.Join("\n  ", failures) +
                "\n  (기전: Platform/DockPhysicsStep의 실제 콜라이더 위에서 쿨롱 마찰이 발 떼기 속도를 " +
                "모서리에 닿기 전에 0으로 만든다. StickConfig.hopDownStepOffCarryEnabled 문서의 유도 참고.)");
        }

        // ====================================================================
        // T1n — ★ 네거티브 컨트롤: 이송을 끄면 현장에서 본 그 실패가 그대로 재현된다
        // ====================================================================
        //
        // 이 저장소는 "항상 참인 단언"으로 초록불이 난 사고가 여러 건 있다. 위 T1이 무엇을 증명하려면
        // **수정을 되돌렸을 때 실제로 빨간불이 나야** 한다. 그리고 그 빨간불의 모양이 현장 로그와
        // 같아야 한다 — 착지 발판이 다시 Dock(-2)이고 하강이 0에 가깝다.
        //
        // ★ 배율은 **사용자 저장값 0.60** 한 점으로 고정한다. 그 점이 이번 회귀가 실제로 터진
        //   좌표이고, 뛰어내리기 갈래가 확실히 선택되는 구간이기 때문이다(0.4493 < 0.60).

        [UnityTest]
        public IEnumerator 네거티브_발떼기이송을_끄면_Dock에_도로_착지한다()
        {
            yield return SetUpProductionDock(stepOffCarry: false);

            var r = new DescentResult();
            yield return TryDescendAtScale(UserSavedScale, r);

            StringAssert.StartsWith("뛰어내리기", r.Branch,
                $"{LogPrefix} 전제 실패 — 배율 {UserSavedScale:F2}에서 뛰어내리기 갈래가 선택되지 않았습니다" +
                $"(실제 {r.Branch}). 이 네거티브 컨트롤은 그 갈래에서만 의미가 있습니다.");

            // ── 유도의 **검산**. 값은 어느 것도 가정하지 않고 실제 물리에서 읽는다:
            //    · 마찰계수 = 계단 콜라이더가 실제로 보고하는 값(재질 미지정이면 Unity 2D 기본).
            //    · gravityScale = **설정값**을 쓴다. Body.gravityScale은 접지 중력 억제로 0일 수 있어
            //      (StickmanBlackboard.ApplyGroundedGravitySuppression) 그대로 읽으면 0으로 나눈다.
            var stepCollider = _step.GetComponent<BoxCollider2D>();
            float mu = stepCollider != null ? stepCollider.friction : float.NaN;
            float g = Mathf.Abs(Physics2D.gravity.y) * _clonedConfig.gravityScale;
            float frictionDecel = mu * g;
            float stopDistance = frictionDecel > 0f ? (r.StepOffSpeed * r.StepOffSpeed) / (2f * frictionDecel) : float.PositiveInfinity;
            Debug.Log($"{LogPrefix} 네거티브 검산 — 계단 콜라이더 마찰계수={mu:F3}, 중력가속도={g:F3}유닛/초²" +
                $"(9.81 x gravityScale {_clonedConfig.gravityScale:F2}) -> 감속 {frictionDecel:F3}유닛/초². " +
                $"내딛는 속도 {r.StepOffSpeed:F3}유닛/초 -> 정지 거리 {stopDistance:F4}유닛 " +
                $"vs 모서리까지 {r.DistanceToEdge:F4}유닛 (정지 거리가 더 짧아야 이 실패가 성립한다).");
            Assert.Less(stopDistance, r.DistanceToEdge,
                $"{LogPrefix} 유도 검산 실패 — 마찰 정지 거리({stopDistance:F4})가 모서리까지 남은 거리" +
                $"({r.DistanceToEdge:F4})보다 짧지 않습니다. 그렇다면 아래 실패는 마찰 때문이 아니라 " +
                "다른 기전 때문이며, 이 파일의 근본 원인 서술이 틀렸다는 뜻입니다.");

            float dropped = _dockTopWorldY - r.LowestWorldY;
            Assert.IsFalse(r.Descended,
                $"{LogPrefix} 네거티브 컨트롤이 무력합니다 — 발 떼기 이송을 껐는데도 내려갔습니다" +
                $"(착지핸들 {r.LandedHandle}, 하강 {dropped:F4}유닛). 그렇다면 T1의 초록불은 이 수정이 아니라 " +
                "다른 무언가가 만들고 있다는 뜻입니다.");
            Assert.AreEqual(DockHandle, r.LandedHandle,
                $"{LogPrefix} 네거티브 컨트롤에서 Dock(-2)이 아니라 핸들 {r.LandedHandle}에 착지했습니다 — " +
                "현장 로그(‘착지 확정 — 발판핸들=-2(Dock), 낙하높이=0.00유닛’)와 다른 실패 모양입니다.");
            Assert.Less(dropped, DockDropUnits * 0.2f,
                $"{LogPrefix} 네거티브 컨트롤에서 {dropped:F4}유닛이나 내려갔습니다 — 현장 관측" +
                "(‘OS y 906.8~907.0 밖으로 한 번도 안 나감’)과 다릅니다.");
        }

        // ====================================================================
        // T2 — 콜라이더가 없는 발판에서는 이 수정이 **아무 것도 바꾸지 않는다**
        // ====================================================================
        //
        // 발 떼기 이송은 "마찰이 먹어치운 만큼을 되돌린다"는 것이지 "더 멀리 던진다"가 아니다.
        // 루트 Rigidbody2D의 linearDamping은 0이라 공중에서는 x속도가 어차피 그대로 유지되므로,
        // 콜라이더가 없는 발판(= 실제 창 상단)에서는 켜든 끄든 착지 지점이 같아야 한다.
        // 이 검사가 없으면 "혹시 이 수정이 다른 모든 뛰어내리기의 거리를 바꿔 놓은 것 아닌가"를
        // 아무도 반증할 수 없다.

        [UnityTest]
        public IEnumerator 콜라이더_없는_발판에서는_이송이_거동을_바꾸지_않는다()
        {
            yield return SetUpProductionDock(stepOffCarry: true);

            float withCarry = 0f, withoutCarry = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                bool carry = pass == 0;
                _clonedConfig.hopDownStepOffCarryEnabled = carry;

                // Dock 발판을 치우면 계단도 사라진다(DockPhysicsStep이 "Dock 발판 없음"으로 스스로 꺼진다).
                // 남는 것은 논리 발판 두 조각뿐 — 실제 창 상단과 같은 조건이다.
                StickmanBlackboard bb = _agent.Blackboard;
                var intent = (ScriptedIntentSource)bb.IntentSource;
                _agent.ApplyCharacterScale(UserSavedScale, $"{LogPrefix} 콜라이더 없음 대조");
                yield return null;

                // 논리적으로는 Dock 위와 같은 자리에 세우되, 계단이 꺼진 상태로 둔다.
                float startX = _dockRightWorldX - _clonedConfig.hopDownEdgeCommitDistance;
                bb.MoveBodyToWorld(new Vector2(startX, _dockTopWorldY));
                bb.Body.linearVelocity = Vector2.zero;
                bb.CurrentFootholdHandle = DockHandle;
                bb.ResetGroundLossTimer();
                intent.MoveInputX = 1f;
                bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

                _clonedConfig.dockPhysicsStepEnabled = false;   // ← 콜라이더만 끈다(논리 발판은 그대로).
                yield return null;
                yield return null;
                Assert.IsFalse(_step.IsActive,
                    $"{LogPrefix} 전제 실패 — dockPhysicsStepEnabled=false인데 계단이 계속 서 있습니다.");

                GroundSensor.GroundInfo info = bb.SenseGround();
                Assert.IsTrue(bb.TryFindHopDownTarget(info, 1, out _, out _),
                    $"{LogPrefix} 전제 실패 — 배율 {UserSavedScale:F2}에서 뛰어내리기 목적지를 찾지 못했습니다.");
                intent.HopDownRequested = true;

                float deadline = Time.time + ObserveBudgetSeconds;
                while (Time.time < deadline)
                {
                    yield return null;
                    if (bb.Machine.CurrentStateId == StickmanStateId.Fall) intent.HopDownRequested = false;
                    if (bb.CurrentFootholdHandle == NetRightHandle) break;
                }

                Assert.AreEqual(NetRightHandle, bb.CurrentFootholdHandle,
                    $"{LogPrefix} 콜라이더 없는 발판에서 뛰어내리기가 실패했습니다(이송={carry}, " +
                    $"착지핸들 {bb.CurrentFootholdHandle}) — 이 경로는 원래부터 정상이었습니다.");

                float landedX = bb.Body.position.x;
                if (carry) withCarry = landedX; else withoutCarry = landedX;

                _clonedConfig.dockPhysicsStepEnabled = true;
                yield return new WaitForSeconds(0.3f);
            }

            Debug.Log($"{LogPrefix} 콜라이더 없음 대조 — 착지 X: 이송 켬 {withCarry:F4} / 이송 끔 {withoutCarry:F4} " +
                $"(차이 {Mathf.Abs(withCarry - withoutCarry):F4}유닛).");

            // 허용오차는 한 프레임의 이동량 수준(60fps에서 walkSpeed 1.5 x 0.8 / 60 = 0.02유닛)의 5배.
            Assert.AreEqual(withoutCarry, withCarry, 0.10f,
                $"{LogPrefix} 콜라이더가 없는 발판인데 이송 여부에 따라 착지 X가 달라졌습니다 " +
                $"({withoutCarry:F4} -> {withCarry:F4}) — 이 수정이 '마찰 보정'을 넘어 거동 자체를 바꾸고 있습니다.");
        }
    }
}
