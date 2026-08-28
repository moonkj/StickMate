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
    /// ★ 사용자 명시 요청 "내려갈때도 매달려서 내려가는형태로"(2026-08-28)의 실측 검증.
    ///
    /// 리더 지시: "코드로 캐릭터를 가장자리에 놓고 상태 전이를 유도해 검증하고, 안전 규칙(발판 소실 시
    /// 즉시 낙하, 타임아웃)도 실측하라." 이 파일이 정확히 그 셋을 담는다:
    ///   (1) 정상 시퀀스 — Walk -> LedgeHang(매달림) -> Fall -> 아래 발판 착지
    ///   (2) 안전 규칙 A  — 매달린 도중 붙잡은 발판이 사라지면 **즉시** Fall
    ///   (3) 안전 규칙 B  — 유지시간이 아무리 길어도 ledgeHangMaxDuration 상한에서 반드시 손을 놓는다
    ///
    /// 검증 방식: 실제 씬(Main.unity)의 StickmanAgent를 그대로 쓰되, **결정론적 발판 배치**와
    /// **결정론적 이동 의도**만 주입한다(둘 다 StickmanBlackboard의 public 필드라 새 훅이 필요 없다).
    ///   · 발판   : 아래 TestFootholdService — 위쪽 창(부분 폭) 하나 + 아래쪽 바닥(전체 폭) 하나.
    ///   · 의도   : 아래 ScriptedIntentSource — "오른쪽으로 걷는 중 + 매달리기 요청" 펄스.
    /// AutoWanderController의 확률 추첨(ledgeHangChance)을 우회하는 것은 의도적이다 — 확률은 "얼마나
    /// 자주 하느냐"의 문제고, 여기서 잠가야 하는 것은 "하기로 했을 때 정확히 무슨 일이 일어나느냐"다.
    ///
    /// StickConfig는 **복제본**을 블랙보드에 꽂아 쓴다(Object.Instantiate) — 타임아웃 검증을 위해 값을
    /// 바꿔야 하는데, 씬이 참조하는 DefaultStickConfig.asset 원본을 건드리면 테스트가 프로젝트 자산을
    /// 수정하게 된다(CLAUDE.md 절대 불변 원칙 3의 정신). TearDown에서 원본 참조를 그대로 되돌린다.
    /// </summary>
    public sealed class LedgeHangDescentTests
    {
        private const string LogPrefix = "[LEDGEHANG-TEST]";

        private const long UpperHandle = 7001L;
        private const long LowerHandle = 7002L;

        private const float SettleWaitSeconds = 2.5f;
        private const float MaxObserveSeconds = 12f;

        /// <summary>결정론적 발판 목록을 돌려주는 최소 스텁. 목록은 테스트가 실행 중에 바꿀 수 있다
        /// (발판 소실 시나리오에서 위쪽 창을 통째로 제거하기 위해).</summary>
        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>테스트가 직접 값을 세팅하는 이동 의도 소스(AutoWanderController 대체).</summary>
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

        private float _upperTopWorldY;
        private float _lowerTopWorldY;
        private float _upperRightEdgeWorldX;

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
        /// 공통 준비 — 씬을 띄우고, 결정론적 발판/의도를 주입하고, 캐릭터를 **위쪽 창의 오른쪽
        /// 가장자리**에 정확히 세운 뒤 Walk로 만든다. 이 시점의 캐릭터는 "지금 막 모서리에 도착한"
        /// 상태이며, 매달리기 펄스 하나만 소비되면 곧바로 LedgeHang으로 들어가야 한다.
        /// </summary>
        private IEnumerator SetUpAtLedgeEdge()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            yield return new WaitForSeconds(SettleWaitSeconds); // 최초 낙하/스냅이 끝나 능동 상태에 안착할 시간.

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            // ── 결정론적 발판 배치(OS 화면 좌표, y는 화면 위에서 아래로 증가) ──
            //   위쪽 창 : 화면 세로 25% 지점이 상단, 가로 20%~60% 구간만 차지 -> 오른쪽 모서리가 생긴다.
            //   아래 바닥: 화면 세로 85% 지점이 상단, 가로 전체 -> 어디서 떨어져도 받아준다.
            float w = Screen.width;
            float h = Screen.height;
            float upperTopOs = h * 0.25f;
            float lowerTopOs = h * 0.85f;

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(UpperHandle, new Rect(w * 0.20f, upperTopOs, w * 0.40f, h * 0.30f), true));
            _service.Footholds.Add(new PlatformFoothold(LowerHandle, new Rect(0f, lowerTopOs, w, h * 0.15f), false));

            _poller = new FootholdPoller(_service, _clonedConfig);
            bb.FootholdPoller = _poller;

            _intent = new ScriptedIntentSource { MoveInputX = 1f, LedgeHangRequested = false };
            bb.IntentSource = _intent;

            Camera cam = bb.MainCamera;
            // 모서리에서 살짝 안쪽(5 OS px)에 세운다 — parkourDetectionRadius 안에 확실히 들어오면서도
            // 아직 발판 위에 있는 위치다(= 실제로 "가장자리에 도달한" 순간의 좌표).
            Vector3 standWorld = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(w * 0.60f - 5f, upperTopOs), 10f, _clonedConfig);
            _upperTopWorldY = standWorld.y;
            _upperRightEdgeWorldX = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(w * 0.60f, upperTopOs), 10f, _clonedConfig).x;
            _lowerTopWorldY = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(w * 0.5f, lowerTopOs), 10f, _clonedConfig).y;

            bb.Body.position = new Vector2(standWorld.x, standWorld.y);
            bb.Body.transform.position = new Vector3(standWorld.x, standWorld.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = UpperHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, 위쪽창 상단 월드Y={_upperTopWorldY:F3}, " +
                $"오른쪽 모서리 월드X={_upperRightEdgeWorldX:F3}, 아래 바닥 상단 월드Y={_lowerTopWorldY:F3}, " +
                $"낙차={(_upperTopWorldY - _lowerTopWorldY):F3}유닛, 손끝~발끝={bb.LedgeHangDropDepth:F3}유닛, " +
                $"시작 위치={bb.Body.position}, 상태={bb.Machine.CurrentStateId}");

            // 전제 확인: 이 배치에서 "내려갈 발판"이 실제로 감지되어야 매달리기가 성립한다.
            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 전제 실패 — 캐릭터가 위쪽 창에 접지하지 못했습니다.");
            Assert.IsTrue(bb.TryFindDescendTarget(info, 1, out long target, out float targetY),
                $"{LogPrefix} 전제 실패 — 오른쪽 모서리 아래에서 내려갈 발판을 찾지 못했습니다.");
            Assert.AreEqual(LowerHandle, target, $"{LogPrefix} 전제 실패 — 내려갈 발판이 아래쪽 바닥이 아닙니다.");
            Debug.Log($"{LogPrefix} 내려갈 발판 확인 — 핸들={target}, 상단 월드Y={targetY:F3}");
        }

        // ============================================================================
        // (1) 정상 시퀀스 — 가장자리에서 매달렸다가 손을 놓고 아래 발판에 착지한다
        // ============================================================================

        [UnityTest]
        public IEnumerator LedgeHangDescendsThenFallsOntoLowerFoothold()
        {
            yield return SetUpAtLedgeEdge();
            StickmanBlackboard bb = _agent.Blackboard;
            float dropDepth = bb.LedgeHangDropDepth;

            _intent.LedgeHangRequested = true; // 이 펄스를 WalkState가 소비한다.

            bool sawHang = false;
            bool sawFall = false;
            bool landedOnLower = false;
            float minHangY = float.PositiveInfinity;
            float maxHangY = float.NegativeInfinity;
            float hangElapsed = 0f;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                StickmanStateId state = bb.Machine.CurrentStateId;

                if (state == StickmanStateId.LedgeHang)
                {
                    if (!sawHang)
                    {
                        sawHang = true;
                        _intent.LedgeHangRequested = false; // 펄스 소비 완료 — 착지 후 재발동을 막는다.
                    }
                    hangElapsed += Time.deltaTime;
                    float y = bb.Body.position.y;
                    if (y < minHangY) minHangY = y;
                    if (y > maxHangY) maxHangY = y;
                }
                else if (sawHang && state == StickmanStateId.Fall)
                {
                    sawFall = true;
                }
                else if (sawFall && (state == StickmanStateId.Idle || state == StickmanStateId.Walk))
                {
                    landedOnLower = bb.CurrentFootholdHandle == LowerHandle;
                    Debug.Log($"{LogPrefix} 착지 — 상태={state}, 발판핸들={bb.CurrentFootholdHandle}, " +
                        $"월드Y={bb.Body.position.y:F3}(아래 바닥 상단 {_lowerTopWorldY:F3})");
                    break;
                }
            }

            Debug.Log($"{LogPrefix} 시퀀스 관찰 종료 — 매달림={sawHang}, 낙하={sawFall}, 아래발판착지={landedOnLower}, " +
                $"매달린 시간={hangElapsed:F2}초, 매달린 동안 월드Y 범위=[{minHangY:F3}, {maxHangY:F3}], 총 {elapsed:F2}초");

            Assert.IsTrue(sawHang, $"{LogPrefix} 가장자리에서 LedgeHang으로 전이하지 못했습니다 — " +
                "WalkState의 매달리기 분기 또는 TryFindDescendTarget 회귀입니다.");
            Assert.IsTrue(sawFall, $"{LogPrefix} 매달린 뒤 손을 놓고 Fall로 전이하지 못했습니다(무한 매달림 의심).");
            Assert.IsTrue(landedOnLower,
                $"{LogPrefix} 손을 놓은 뒤 아래쪽 발판(핸들 {LowerHandle})에 착지하지 못했습니다 — " +
                $"실제 핸들={bb.CurrentFootholdHandle}. 매달리기가 '내려가기'로 이어지지 않았습니다.");

            // 매달린 자세의 기하학: 발(루트)이 모서리보다 정확히 '손끝~발끝' 거리만큼 아래에 있어야
            // 손이 모서리에 닿아 보인다. 매달림 구간의 **가장 낮은 지점**(= 잡기 보간이 끝난 뒤 유지되는
            // 높이)으로 확인한다. 허용 오차는 잡기 보간의 마지막 한 프레임분을 감안해 넉넉히 잡는다.
            float expectedHangY = _upperTopWorldY - dropDepth;
            Assert.AreEqual(expectedHangY, minHangY, 0.05f,
                $"{LogPrefix} 매달린 높이가 어긋납니다 — 기대 {expectedHangY:F3}(모서리 {_upperTopWorldY:F3} − 손끝~발끝 {dropDepth:F3}), " +
                $"실제 최저 {minHangY:F3}. 손이 모서리에서 떨어져 보이거나 파묻혀 보인다는 뜻입니다.");

            // "그냥 뚝 떨어지지 않는다"는 것이 이 기능의 요지 — 매달린 구간이 실제로 존재해야 한다.
            Assert.Greater(hangElapsed, 0.2f,
                $"{LogPrefix} 매달린 시간이 {hangElapsed:F2}초뿐입니다 — 사실상 그냥 떨어진 것과 같습니다.");
        }

        // ============================================================================
        // (2) 안전 규칙 A — 매달린 도중 붙잡은 발판이 사라지면 즉시 낙하
        // ============================================================================

        [UnityTest]
        public IEnumerator LedgeHangFallsImmediatelyWhenGrabbedFootholdDisappears()
        {
            yield return SetUpAtLedgeEdge();
            StickmanBlackboard bb = _agent.Blackboard;

            // 이 시나리오에서는 손을 놓는 시점이 오기 전에 발판을 없애야 하므로 유지시간을 길게 준다.
            _clonedConfig.ledgeHangHoldDurationMin = 5f;
            _clonedConfig.ledgeHangHoldDurationMax = 5f;
            _clonedConfig.ledgeHangMaxDuration = 30f; // 타임아웃이 먼저 걸려 결과가 오염되지 않게.

            _intent.LedgeHangRequested = true;

            float elapsed = 0f;
            while (bb.Machine.CurrentStateId != StickmanStateId.LedgeHang && elapsed < 3f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — LedgeHang에 진입하지 못했습니다.");
            _intent.LedgeHangRequested = false;

            yield return new WaitForSeconds(0.6f); // 잡기 보간이 끝나 확실히 매달려 있는 상태로.
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 발판을 없애기도 전에 매달림이 끝났습니다.");

            // ★ 붙잡고 있던 창이 닫혔다(또는 다른 창에 완전히 가려졌다).
            _service.Footholds.RemoveAll(f => f.Handle == UpperHandle);
            _poller.PollImmediately();
            Debug.Log($"{LogPrefix} 붙잡은 발판(핸들 {UpperHandle})을 목록에서 제거했습니다 — 즉시 낙하해야 합니다.");

            int framesUntilFall = 0;
            while (bb.Machine.CurrentStateId == StickmanStateId.LedgeHang && framesUntilFall < 10)
            {
                yield return null;
                framesUntilFall++;
            }

            StickmanStateId after = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 발판 소실 후 {framesUntilFall}프레임 만에 상태={after}로 전이했습니다.");

            Assert.AreNotEqual(StickmanStateId.LedgeHang, after,
                $"{LogPrefix} 붙잡은 발판이 사라졌는데도 계속 매달려 있습니다 — 안전 규칙 위반입니다.");
            Assert.LessOrEqual(framesUntilFall, 2,
                $"{LogPrefix} 발판 소실 후 낙하까지 {framesUntilFall}프레임이 걸렸습니다 — '즉시'가 아닙니다.");
        }

        // ============================================================================
        // (3) 안전 규칙 B — 무한 매달림 금지(절대 상한 타임아웃)
        // ============================================================================

        [UnityTest]
        public IEnumerator LedgeHangAlwaysReleasesAtMaxDurationTimeout()
        {
            yield return SetUpAtLedgeEdge();
            StickmanBlackboard bb = _agent.Blackboard;

            // 유지시간을 사실상 무한대로 두고, 오직 절대 상한만이 손을 놓게 만든다.
            _clonedConfig.ledgeHangHoldDurationMin = 999f;
            _clonedConfig.ledgeHangHoldDurationMax = 999f;
            _clonedConfig.ledgeHangMaxDuration = 0.9f;

            _intent.LedgeHangRequested = true;

            float waitEnter = 0f;
            while (bb.Machine.CurrentStateId != StickmanStateId.LedgeHang && waitEnter < 3f)
            {
                yield return null;
                waitEnter += Time.deltaTime;
            }
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — LedgeHang에 진입하지 못했습니다.");
            _intent.LedgeHangRequested = false;

            float hangDuration = 0f;
            while (bb.Machine.CurrentStateId == StickmanStateId.LedgeHang && hangDuration < 6f)
            {
                yield return null;
                hangDuration += Time.deltaTime;
            }

            Debug.Log($"{LogPrefix} 타임아웃 실측 — 유지시간 설정 999초, 절대 상한 {_clonedConfig.ledgeHangMaxDuration:F2}초, " +
                $"실제 매달린 시간 {hangDuration:F3}초, 이후 상태={bb.Machine.CurrentStateId}");

            Assert.AreNotEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 절대 상한을 넘겼는데도 계속 매달려 있습니다 — '무한 매달림 금지' 위반입니다.");
            Assert.AreEqual(_clonedConfig.ledgeHangMaxDuration, hangDuration, 0.25f,
                $"{LogPrefix} 손을 놓은 시점이 절대 상한과 어긋납니다 — 실제 {hangDuration:F3}초.");
        }
    }
}
