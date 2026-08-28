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
    /// ★ 사용자 결정 "낙차가 작으면 (매달리지 말고) 그냥 뛰어내리게 한다"(2026-08-29)의 실측 검증.
    ///
    /// 직전 라운드가 남긴 미해결 항목이 출발점이다 — macOS Dock 상단에서 화면 최하단까지의 낙차는
    /// 0.855유닛뿐인데 매달리기(LedgeHang)는 손끝~발끝 거리(약 2.5유닛) 이상 떨어져 있어야 성립하므로,
    /// 캐릭터가 Dock 경계에서 그냥 되돌아설 뿐 스스로 내려오지 못했다. 그래서 이 파일은 **그 Dock 배치를
    /// 그대로 재현한 발판 3장**(Dock 역할의 부분 폭 발판 + 그 좌/우 바깥의 바닥 조각 2장) 위에서 다음
    /// 네 가지를 잠근다:
    ///
    ///   (1) 작은 낙차 -> **뛰어내린다**(Walk -> Fall -> 아래 발판 착지). 매달리기로 새지 않는다.
    ///   (2) 큰 낙차   -> **여전히 매달린다**. 뛰어내리기 펄스를 줘도 소비되지 않는다(두 밴드가 배타적).
    ///   (3) 뛰어내린 뒤 **다시 올라온다**(ParkourClimb로 턱 위에 실제로 올라서서 그 발판을 딛는다).
    ///       ★ 이게 이번 작업의 핵심이다 — 못 올라오면 한 번 내려간 캐릭터가 영영 Dock 아래에 갇힌다.
    ///   (4) 위 (1)+(3)이 **스크립트 펄스 없이 자율 배회만으로** 실제로 일어난다(정책 실측).
    ///
    /// 검증 방식은 Tests/PlayMode/LedgeHangDescentTests.cs와 동일한 관례를 따른다: 실제 씬(Main.unity)의
    /// StickmanAgent를 그대로 쓰되 **결정론적 발판 배치**와 **결정론적 이동 의도**만 주입하고, StickConfig는
    /// 복제본을 꽂아 원본 자산(DefaultStickConfig.asset)을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    ///
    /// 발판의 세로 위치는 OS 픽셀이 아니라 **월드 유닛 낙차에서 역산**한다 — 해상도/DPI가 달라져도
    /// "낙차 0.855유닛"이라는 실측 조건이 그대로 유지되어야 매달리기 임계값과의 대소 관계가 보존된다.
    /// </summary>
    public sealed class EdgeHopDownTests
    {
        private const string LogPrefix = "[HOPDOWN-TEST]";

        /// <summary>macOS Dock 역할 — 화면 가로 30%~70%만 차지하는 부분 폭 발판(좌우 모서리가 생긴다).</summary>
        private const long DockHandle = 8001L;
        private const long LeftFloorHandle = 8002L;
        private const long RightFloorHandle = 8003L;

        /// <summary>실제 macOS 실측값 — Dock 상단에서 화면 최하단까지의 낙차(월드 유닛).</summary>
        private const float DockDropUnits = 0.855f;

        /// <summary>"매달려야 마땅한" 낙차(월드 유닛). 손끝~발끝 거리(약 2.5유닛)보다 확실히 크게 잡는다 —
        /// 테스트 본문이 실제 LedgeHangMinDropDepth와 비교해 이 전제를 다시 확인한다.</summary>
        private const float LargeDropUnits = 6f;

        private const float SettleWaitSeconds = 2.5f;
        private const float MaxObserveSeconds = 10f;

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
            public bool StepUpRequested { get; set; }
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private FootholdPoller _poller;
        private ScriptedIntentSource _intent;

        private float _dockTopWorldY;
        private float _floorTopWorldY;
        private float _dockLeftWorldX;
        private float _dockRightWorldX;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        /// <summary>
        /// 공통 준비 — Dock 배치를 재현하고 캐릭터를 Dock 위 지정 위치에 세운 뒤 Walk로 만든다.
        /// </summary>
        /// <param name="dropUnits">Dock 상단 -> 좌우 바닥 조각 상단의 낙차(월드 유닛).</param>
        /// <param name="startNearRightEdgeUnits">Dock 오른쪽 모서리에서 안쪽으로 몇 유닛 지점에 세울지.</param>
        private IEnumerator SetUpDockLayout(float dropUnits, float startNearRightEdgeUnits)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            float dockTopOs = h * 0.55f;

            // 낙차를 **월드 유닛으로 먼저 정하고** OS y로 역산한다(클래스 문서 참고).
            Vector3 dockTopWorld = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, dockTopOs), 10f, _clonedConfig);
            Vector2 floorOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(dockTopWorld.x, dockTopWorld.y - dropUnits), _clonedConfig, out _);
            float floorTopOs = floorOs.y;

            Assert.Less(floorTopOs, h, $"{LogPrefix} 준비 실패 — 요청 낙차 {dropUnits:F3}유닛이 화면 아래로 벗어납니다" +
                $"(Dock 상단 OS y={dockTopOs:F1}, 바닥 OS y={floorTopOs:F1}, 화면 높이={h:F0}).");
            Assert.Greater(floorTopOs, dockTopOs, $"{LogPrefix} 준비 실패 — 바닥이 Dock보다 위에 놓였습니다.");

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(DockHandle, new Rect(w * 0.30f, dockTopOs, w * 0.40f, h - dockTopOs), true));
            // 바닥 조각은 화면 끝까지 뻗지 않는다(10%~30%, 70%~90%). 화면 끝에 딱 붙여 두면 캐릭터가
            // EnforceScreenBoundsAndRescue의 하드 클램프(기본 여유 8pt + 시각 반폭)에 먼저 걸려 발판
            // 모서리로부터 wanderEdgeStopDistance(0.3유닛)보다 멀리 떨어진 곳에서 멈추고, 그러면 배회
            // AI의 경계 판정이 영영 성립하지 않아 돌아서지 못한다(실측 확인). 실제 배포에서는 Walk
            // 지속시간(1.5~4초)이 만료되면서 방향이 새로 뽑히므로 스스로 풀리지만, 이 테스트는 관찰을
            // 위해 지속시간을 길게 잡으므로 배치 쪽에서 미리 여유를 둔다.
            _service.Footholds.Add(new PlatformFoothold(LeftFloorHandle, new Rect(w * 0.10f, floorTopOs, w * 0.20f, h - floorTopOs), false));
            _service.Footholds.Add(new PlatformFoothold(RightFloorHandle, new Rect(w * 0.70f, floorTopOs, w * 0.20f, h - floorTopOs), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            _intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = _intent;

            _dockTopWorldY = dockTopWorld.y;
            _dockLeftWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.30f, dockTopOs), 10f, _clonedConfig).x;
            _dockRightWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.70f, dockTopOs), 10f, _clonedConfig).x;
            _floorTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.85f, floorTopOs), 10f, _clonedConfig).y;

            float startX = _dockRightWorldX - startNearRightEdgeUnits;
            bb.Body.position = new Vector2(startX, _dockTopWorldY);
            bb.Body.transform.position = new Vector3(startX, _dockTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = DockHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, Dock 상단 월드Y={_dockTopWorldY:F3}(X {_dockLeftWorldX:F3}~{_dockRightWorldX:F3}), " +
                $"바닥 상단 월드Y={_floorTopWorldY:F3}, 실측 낙차={(_dockTopWorldY - _floorTopWorldY):F3}유닛(요청 {dropUnits:F3}), " +
                $"매달리기 최소 낙차={bb.LedgeHangMinDropDepth:F3}유닛, 뛰어내리기 밴드=[{_clonedConfig.hopDownMinDropHeight:F3}, {bb.HopDownMaxDropHeight:F3}), " +
                $"시작 위치={bb.Body.position}, 상태={bb.Machine.CurrentStateId}");
        }

        // ============================================================================
        // (1) 작은 낙차 — 매달리지 않고 그냥 앞으로 뛰어내려 아래 발판에 착지한다
        // ============================================================================

        [UnityTest]
        public IEnumerator SmallDropStepsOffAndLandsOnLowerFoothold()
        {
            yield return SetUpDockLayout(DockDropUnits, 0.10f);
            StickmanBlackboard bb = _agent.Blackboard;

            // ── 분류 전제: 이 낙차는 "뛰어내리기"지 "매달리기"가 아니어야 한다(두 밴드의 배타성).
            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — 캐릭터가 Dock에 접지하지 못했습니다.");
            Assert.IsTrue(bb.TryFindHopDownTarget(info, 1, out long hopTarget, out float hopTargetY),
                $"{LogPrefix} 전제 실패 — 낙차 {DockDropUnits:F3}유닛에서 뛰어내릴 발판을 찾지 못했습니다. " +
                $"hopDownMinDropHeight({_clonedConfig.hopDownMinDropHeight:F3}) 회귀 의심.");
            Assert.AreEqual(RightFloorHandle, hopTarget, $"{LogPrefix} 전제 실패 — 뛰어내릴 발판이 오른쪽 바닥 조각이 아닙니다.");
            Assert.IsFalse(bb.TryFindDescendTarget(info, 1, out _, out _),
                $"{LogPrefix} 전제 실패 — 낙차 {DockDropUnits:F3}유닛이 매달리기 대상으로도 잡혔습니다. " +
                $"두 밴드가 겹치면 어느 쪽을 할지 모호해집니다(매달리기 최소 낙차={bb.LedgeHangMinDropDepth:F3}).");

            _intent.HopDownRequested = true;

            bool sawFall = false;
            bool sawLedgeHang = false;
            bool landed = false;
            float maxForwardVelocity = 0f;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                StickmanStateId state = bb.Machine.CurrentStateId;

                if (state == StickmanStateId.LedgeHang) sawLedgeHang = true;
                if (state == StickmanStateId.Fall)
                {
                    if (!sawFall)
                    {
                        sawFall = true;
                        _intent.HopDownRequested = false; // 펄스 소비 완료.
                    }
                    maxForwardVelocity = Mathf.Max(maxForwardVelocity, bb.Body.linearVelocity.x);
                }
                else if (sawFall && (state == StickmanStateId.Idle || state == StickmanStateId.Walk))
                {
                    landed = true;
                    break;
                }
            }

            Debug.Log($"{LogPrefix} 뛰어내리기 실측 — 낙하={sawFall}, 매달림={sawLedgeHang}, 착지={landed}, " +
                $"착지 발판핸들={bb.CurrentFootholdHandle}(기대 {RightFloorHandle}), 착지 월드=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), " +
                $"바닥 상단 월드Y={_floorTopWorldY:F3}, Dock 오른쪽 모서리 X={_dockRightWorldX:F3}, " +
                $"낙하 중 최대 전방속도={maxForwardVelocity:F2}유닛/초, 총 {elapsed:F2}초");

            Assert.IsFalse(sawLedgeHang,
                $"{LogPrefix} 낙차가 작은데도 매달렸습니다 — 이 낙차에서는 매달린 발이 이미 목적지를 지나칩니다.");
            Assert.IsTrue(sawFall, $"{LogPrefix} 뛰어내리기 펄스를 줬는데 Fall로 전이하지 않았습니다(WalkState 분기 회귀).");
            Assert.IsTrue(landed, $"{LogPrefix} {MaxObserveSeconds}초 안에 착지하지 못했습니다.");
            Assert.AreEqual(RightFloorHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 아래 발판(핸들 {RightFloorHandle})이 아니라 핸들 {bb.CurrentFootholdHandle}에 착지했습니다.");
            Assert.AreEqual(_floorTopWorldY, bb.Body.position.y, 0.05f,
                $"{LogPrefix} 착지 높이가 바닥 상단과 어긋납니다.");

            // "앞으로 내딛었다"의 증거 — 제자리에서 수직으로 떨어졌다면 모서리를 넘지 못한다.
            Assert.Greater(bb.Body.position.x, _dockRightWorldX,
                $"{LogPrefix} 착지 X({bb.Body.position.x:F3})가 Dock 오른쪽 모서리({_dockRightWorldX:F3})를 넘지 못했습니다 — " +
                "앞으로 내딛지 않고 제자리에서 떨어졌다는 뜻입니다.");
            Assert.Greater(maxForwardVelocity, 0.1f,
                $"{LogPrefix} 낙하 중 전방 수평속도가 관측되지 않았습니다({maxForwardVelocity:F2}) — " +
                "hopDownStepOffSpeedScale이 적용되지 않았습니다.");
        }

        // ============================================================================
        // (2) 큰 낙차 — 뛰어내리기가 아니라 여전히 매달리기로 분류/동작한다(회귀 방지)
        // ============================================================================

        [UnityTest]
        public IEnumerator LargeDropStillHangsAndRejectsHopDownPulse()
        {
            yield return SetUpDockLayout(LargeDropUnits, 0.10f);
            StickmanBlackboard bb = _agent.Blackboard;
            float bigDrop = LargeDropUnits;

            // 이 테스트의 전제 자체를 먼저 못박는다 — 프리팹 치수가 바뀌어 매달리기 최소 낙차가
            // LargeDropUnits를 넘어서면, 아래 검증은 "큰 낙차"를 보고 있지 않게 된다.
            Assert.Greater(bigDrop, bb.LedgeHangMinDropDepth,
                $"{LogPrefix} 전제 실패 — LargeDropUnits({bigDrop:F3})가 매달리기 최소 낙차" +
                $"({bb.LedgeHangMinDropDepth:F3})보다 크지 않습니다. 상수를 올려야 합니다.");

            // 관찰 중 캐릭터가 모서리 밖으로 걸어 나가버리지 않도록 보행 속도를 0으로 둔다 — 이 테스트가
            // 보려는 것은 "펄스가 어떻게 분류되는가"지 이동이 아니다.
            _clonedConfig.walkSpeed = 0f;

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — 캐릭터가 Dock에 접지하지 못했습니다.");
            Assert.IsFalse(bb.TryFindHopDownTarget(info, 1, out _, out _),
                $"{LogPrefix} 낙차 {bigDrop:F3}유닛이 뛰어내리기 대상으로 잡혔습니다 — 상한(매달리기 최소 낙차 " +
                $"{bb.LedgeHangMinDropDepth:F3})이 적용되지 않았습니다.");
            Assert.IsTrue(bb.TryFindDescendTarget(info, 1, out long hangTarget, out _),
                $"{LogPrefix} 큰 낙차인데 매달릴 발판을 찾지 못했습니다 — 기존 LedgeHang 판정의 회귀입니다.");
            Assert.AreEqual(RightFloorHandle, hangTarget, $"{LogPrefix} 매달려 내려갈 발판이 오른쪽 바닥 조각이 아닙니다.");

            // (a) 뛰어내리기 펄스를 줘도 소비되지 않아야 한다(밴드 배타성의 런타임 확인).
            _intent.HopDownRequested = true;
            float t = 0f;
            while (t < 0.5f)
            {
                yield return null;
                t += Time.deltaTime;
                Assert.AreNotEqual(StickmanStateId.Fall, bb.Machine.CurrentStateId,
                    $"{LogPrefix} 큰 낙차에서 뛰어내리기 펄스가 소비되어 Fall로 전이했습니다 — 밴드 상한이 무시됐습니다.");
                Assert.AreNotEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                    $"{LogPrefix} 매달리기 펄스를 주지 않았는데 LedgeHang으로 전이했습니다.");
            }
            _intent.HopDownRequested = false;
            Debug.Log($"{LogPrefix} 큰 낙차({bigDrop:F3}유닛)에서 뛰어내리기 펄스 {t:F2}초간 무시 확인 — 상태 계속 Walk.");

            // (b) 매달리기 펄스는 그대로 동작해야 한다.
            _intent.LedgeHangRequested = true;
            float wait = 0f;
            while (bb.Machine.CurrentStateId != StickmanStateId.LedgeHang && wait < 3f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            _intent.LedgeHangRequested = false;

            Debug.Log($"{LogPrefix} 큰 낙차 매달리기 확인 — {wait:F2}초 만에 상태={bb.Machine.CurrentStateId}");
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 큰 낙차에서 매달리기로 전이하지 못했습니다 — 기존 동작의 회귀입니다.");
        }

        // ============================================================================
        // (3) ★ 핵심 — 뛰어내린 뒤 낮은 턱을 기어올라 원래 발판(Dock)으로 되돌아온다
        // ============================================================================

        [UnityTest]
        public IEnumerator HopsDownThenClimbsBackOntoDock()
        {
            yield return SetUpDockLayout(DockDropUnits, 0.10f);
            StickmanBlackboard bb = _agent.Blackboard;

            // ── 1단계: 뛰어내려 오른쪽 바닥 조각에 착지.
            _intent.HopDownRequested = true;
            float elapsed = 0f;
            while (elapsed < MaxObserveSeconds && bb.CurrentFootholdHandle != RightFloorHandle)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (bb.Machine.CurrentStateId == StickmanStateId.Fall) _intent.HopDownRequested = false;
            }
            _intent.HopDownRequested = false;
            Assert.AreEqual(RightFloorHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 1단계 실패 — 오른쪽 바닥 조각에 내려서지 못했습니다(현재 핸들 {bb.CurrentFootholdHandle}).");
            Debug.Log($"{LogPrefix} 1단계 완료 — 내려선 월드=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), {elapsed:F2}초 소요.");

            // ── 2단계: 방향을 Dock 쪽(왼쪽)으로 돌려 걸어가다가, 턱이 감지되는 순간 되올라가기 펄스 발동.
            _intent.MoveInputX = -1f;
            if (bb.Machine.CurrentStateId != StickmanStateId.Walk)
            {
                bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
            }

            bool sawClimb = false;
            bool wallDetected = false;
            float climbElapsed = 0f;
            while (climbElapsed < MaxObserveSeconds)
            {
                yield return null;
                climbElapsed += Time.deltaTime;

                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.ParkourClimb)
                {
                    sawClimb = true;
                    _intent.StepUpRequested = false;
                    continue;
                }
                if (sawClimb && (state == StickmanStateId.Idle || state == StickmanStateId.Walk)) break;

                GroundSensor.GroundInfo info = bb.SenseGround();
                if (!sawClimb && info.Grounded && bb.TryFindClimbableWall(info, -1, out long wallHandle, out float wallTopY))
                {
                    if (!wallDetected)
                    {
                        wallDetected = true;
                        Debug.Log($"{LogPrefix} 2단계 — 되올라갈 턱 감지: 핸들={wallHandle}(기대 {DockHandle}), " +
                            $"턱 상단 Y={wallTopY:F3}, 지금 딛는 발판 Y={info.GroundWorldY:F3}, " +
                            $"높이차={(wallTopY - info.GroundWorldY):F3}유닛(상한 {_clonedConfig.stepUpMaxHeight:F2}).");
                        Assert.AreEqual(DockHandle, wallHandle, $"{LogPrefix} 감지된 턱이 Dock이 아닙니다.");
                    }
                    _intent.StepUpRequested = true;
                }
            }
            _intent.StepUpRequested = false;

            Debug.Log($"{LogPrefix} 되올라가기 실측 — 턱감지={wallDetected}, 등반={sawClimb}, " +
                $"최종 발판핸들={bb.CurrentFootholdHandle}(기대 {DockHandle}), " +
                $"최종 월드=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), Dock 상단 Y={_dockTopWorldY:F3}, " +
                $"Dock X범위 {_dockLeftWorldX:F3}~{_dockRightWorldX:F3}, 상태={bb.Machine.CurrentStateId}, {climbElapsed:F2}초 소요.");

            Assert.IsTrue(wallDetected,
                $"{LogPrefix} 아래 발판에서 Dock을 '오를 수 있는 턱'으로 감지하지 못했습니다 — " +
                "낙차가 작을 때 되올라갈 경로가 없다는 뜻이고, 그러면 한 번 내려간 캐릭터가 영영 갇힙니다.");
            Assert.IsTrue(sawClimb, $"{LogPrefix} 되올라가기 펄스를 줬는데 ParkourClimb으로 전이하지 못했습니다.");
            Assert.AreEqual(DockHandle, bb.CurrentFootholdHandle,
                $"{LogPrefix} 등반이 끝났는데 Dock을 딛고 있지 않습니다(핸들 {bb.CurrentFootholdHandle}) — " +
                "턱 옆 허공으로 올라갔다가 도로 떨어졌다는 뜻입니다.");
            Assert.AreEqual(_dockTopWorldY, bb.Body.position.y, 0.05f,
                $"{LogPrefix} 올라선 높이가 Dock 상단과 어긋납니다.");
            Assert.GreaterOrEqual(bb.Body.position.x, _dockLeftWorldX,
                $"{LogPrefix} 올라선 X가 Dock 가로 범위 왼쪽 밖입니다.");
            Assert.LessOrEqual(bb.Body.position.x, _dockRightWorldX,
                $"{LogPrefix} 올라선 X가 Dock 가로 범위 오른쪽 밖입니다 — 모서리 위에 걸쳐 서면 곧바로 다시 떨어집니다.");
        }

        // ============================================================================
        // (4) 정책 실측 — 스크립트 펄스 없이 자율 배회만으로 내려갔다가 다시 올라온다
        // ============================================================================

        [UnityTest]
        public IEnumerator AutoWanderHopsDownAndClimbsBackWithoutScriptedPulses()
        {
            yield return SetUpDockLayout(DockDropUnits, 0.60f);
            StickmanBlackboard bb = _agent.Blackboard;

            // 배회를 결정론에 가깝게 조인다 — 확률 자체를 검증하려는 게 아니라 "확률이 1일 때 그 경로가
            // 실제로 끝까지 이어지는가"를 보려는 것이다. 흔들림(지터/즉흥 방향전환/제자리 점프)만 끈다.
            _clonedConfig.wanderIdleDurationMin = 0.05f;
            _clonedConfig.wanderIdleDurationMax = 0.05f;
            _clonedConfig.wanderWalkDurationMin = 6f;
            _clonedConfig.wanderWalkDurationMax = 6f;
            _clonedConfig.wanderDurationJitterRatio = 0f;
            _clonedConfig.wanderSpontaneousTurnChance = 0f;
            _clonedConfig.wanderPostIdleWalkChance = 1f;
            _clonedConfig.wanderPostIdleJumpChance = 0f;
            _clonedConfig.wanderEdgeJumpAttemptChance = 0f;
            _clonedConfig.wanderEdgeTurnPauseMin = 0.15f;
            _clonedConfig.wanderEdgeTurnPauseMax = 0.15f;
            _clonedConfig.ledgeHangChance = 0f;   // 이 배치의 낙차는 매달리기 대상이 아니다(0으로 못박아 둔다).
            _clonedConfig.hopDownChance = 1f;
            _clonedConfig.stepUpChance = 1f;

            // ★ StickmanAgent가 들고 있는 AutoWanderController는 **원본** StickConfig로 생성돼 있어
            // 복제본 설정이 반영되지 않는다(생성자 주입). 원본 자산을 런타임에 고쳐 쓰는 것은 금지이므로
            // (CLAUDE.md 불변 원칙 3), 복제본으로 만든 컨트롤러를 IntentSource에 꽂고 이 코루틴이 직접
            // Tick한다. 에이전트 쪽 컨트롤러도 계속 Tick되지만 그 출력은 아무도 읽지 않는다.
            var wander = new AutoWanderController(bb, _clonedConfig, new System.Random(20260829));
            bb.IntentSource = wander;

            bool wasOnDock = bb.CurrentFootholdHandle == DockHandle;
            bool hoppedDownToFloor = false;
            bool climbedBackToDock = false;
            long lastHandle = bb.CurrentFootholdHandle;
            float elapsed = 0f;

            while (elapsed < 45f && !(hoppedDownToFloor && climbedBackToDock))
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                wander.Tick(dt);

                long handle = bb.CurrentFootholdHandle;
                if (handle == lastHandle) continue;
                Debug.Log($"{LogPrefix} 자율 배회 발판 이동 — {lastHandle} -> {handle} " +
                    $"(t={elapsed:F2}s, 월드=({bb.Body.position.x:F3},{bb.Body.position.y:F3}), 상태={bb.Machine.CurrentStateId})");
                lastHandle = handle;

                if (handle == DockHandle)
                {
                    wasOnDock = true;
                    if (hoppedDownToFloor) climbedBackToDock = true;
                }
                else if (wasOnDock && (handle == LeftFloorHandle || handle == RightFloorHandle))
                {
                    hoppedDownToFloor = true;
                }
            }

            Debug.Log($"{LogPrefix} 자율 배회 왕복 실측 — 스스로 내려감={hoppedDownToFloor}, 스스로 다시 올라옴={climbedBackToDock}, " +
                $"총 {elapsed:F2}초, 최종 발판핸들={bb.CurrentFootholdHandle}, 최종 상태={bb.Machine.CurrentStateId}");

            Assert.IsTrue(hoppedDownToFloor,
                $"{LogPrefix} 자율 배회가 Dock 경계에서 스스로 내려오지 못했습니다(hopDownChance=1인데도) — " +
                "직전 라운드의 미해결 증상이 그대로입니다.");
            Assert.IsTrue(climbedBackToDock,
                $"{LogPrefix} 내려간 뒤 스스로 Dock 위로 되올라오지 못했습니다(stepUpChance=1인데도) — " +
                "한 번 내려가면 영영 아래에서만 지내게 됩니다. 이번 작업의 핵심 실패입니다.");
        }
    }
}
