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
    /// ★★ 2026-09-02 — 페르소나 소은 실측 + 리더 코드 확인: <b>"활쏘기 접근 중 캐릭터가 뒷걸음친다.
    /// 몸은 과녁을 보는데 발과 몸통은 반대로 간다."</b>
    ///
    /// ============================================================================
    /// 무엇이 버그이고 무엇이 아닌가 — 먼저 갈라 둔다
    /// ============================================================================
    /// <para><b>이동 자체는 설계다.</b> 활쏘기는 과녁 반대쪽으로 신장 1배만큼 <b>물러선 자리</b>에서
    /// 쏜다(<c>Interaction/ArcheryDirector.BackStepRatio = 1.0</c>, 그 문서에 "행진 대신 한 걸음만
    /// 물러선다"고 적혀 있다). 그래서 접근 보행이 과녁에서 <b>멀어지는</b> 것은 정상이다.
    /// 소은이 잰 "과녁 -2.84에서 멀어지는 쪽으로 62pt"도 그 설계값과 일치한다.</para>
    ///
    /// <para><b>버그는 방향 부호 하나다.</b> <c>ArcheryState.TickApproach</c>는 매 프레임
    /// <c>SetFacingSign(dir)</c>로 "걷는 동안에는 진행 방향을 본다"를 지시하는데, 같은 프레임
    /// <b>뒤쪽</b>에 도는 <c>StickmanBlackboard.TickPose</c>가 배회 AI의 <c>MoveInputX</c> 부호로
    /// 그것을 덮었다(호출 순서: <c>StickmanAgent.Update</c> = <c>_autoWander.Tick</c> →
    /// <c>_machine.Tick</c> → <c>TickPose</c>). 배회 AI는 활쏘기가 어디로 걸어가는지 모르므로
    /// 직전까지 걷던 방향을 계속 내보내고, 그 둘이 어긋나면 <b>발과 몸이 반대로 도는 문워크</b>가 된다.</para>
    ///
    /// ============================================================================
    /// 이 테스트의 재현 설계 — 우연이 끼어들 자리를 없앤다
    /// ============================================================================
    /// 디렉터를 쓰지 않는다. 대신 블랙보드에 <b>접근 목표를 왼쪽으로</b> 직접 꽂고, 스크립트 의도
    /// 소스로 배회 AI가 <b>오른쪽</b>을 가리키게 고정한다. 즉 "상태는 왼쪽으로 걸어가는데 배회는
    /// 오른쪽을 말한다"는 어긋남을 100% 확률로 만든다(실기에서는 배회 페이즈에 따라 가끔만 나는 조합이라
    /// 그대로 두면 초록이 우연이 된다).
    ///
    /// <para><b>판정</b>: 실제로 왼쪽으로 이동하는 프레임에서 <c>FacingSign</c>이 &gt; 0이면 실패다.
    /// 관찰은 <b>벽시계(초)</b> 예산으로 돈다(배치모드가 2,000fps 이상으로 돌아 프레임 수 예산은
    /// 실제로 수십 ms밖에 안 되는 사고가 이 저장소에 있었다).</para>
    ///
    /// <para><b>플랫폼</b>: <c>States/</c>는 플랫폼 분기가 한 줄도 없다 — Windows/macOS 동일 경로.</para>
    /// </summary>
    public sealed class ArcheryApproachFacingTests
    {
        private const string LogPrefix = "[ARCHERY-FACING-TEST]";

        private const long FloorHandle = 9201L;
        private const float SettleWaitSeconds = 2.5f;

        /// <summary>접근 보행을 관찰하는 벽시계 예산(초). 접근 거리는 신장 1배 남짓이라 넉넉하다.</summary>
        private const float ObserveSeconds = 1.2f;

        /// <summary>"실제로 이동 중"으로 볼 최소 수평 속도(월드 유닛/초). 감쇠 잔량을 배제한다.</summary>
        private const float MovingSpeedThreshold = 0.3f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>배회 AI 대역 — 이동 의도를 테스트가 고정한다(실제 AutoWanderController와 같은 계약).</summary>
        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;
        private float _floorTopWorldY;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                bb.FacingLocked = false;
                if (bb.Machine != null && bb.Machine.CurrentStateId == StickmanStateId.Archery)
                {
                    bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        private IEnumerator SetUpFloor()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");

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
            Assert.IsNotNull(cam, $"{LogPrefix} 블랙보드에 카메라가 없습니다.");

            float w = Screen.width;
            float h = Screen.height;
            float floorTopOs = h * 0.70f;

            var service = new TestFootholdService();
            service.Footholds.Add(new PlatformFoothold(FloorHandle, new Rect(0f, floorTopOs, w, h - floorTopOs), false));
            bb.FootholdPoller = new FootholdPoller(service, _clonedConfig);
            bb.FootholdPoller.PollImmediately();

            Vector3 center = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, floorTopOs), 10f, _clonedConfig);
            _floorTopWorldY = center.y;
            Place(bb, center.x);

            GroundSensor.GroundInfo info = bb.SenseGround();
            Assert.IsTrue(info.Grounded, $"{LogPrefix} 준비 실패 — 바닥에 접지하지 못했습니다.");

            Debug.Log($"{LogPrefix} 준비 완료 — 발판 X범위={info.CurrentFootholdLeftWorldX:F3}~" +
                $"{info.CurrentFootholdRightWorldX:F3}, 캐릭터 X={bb.Body.position.x:F3}, 신장={bb.CharacterHeightWorld:F3}유닛");
        }

        private void Place(StickmanBlackboard bb, float worldX)
        {
            bb.Body.position = new Vector2(worldX, _floorTopWorldY);
            bb.Body.transform.position = new Vector3(worldX, _floorTopWorldY, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FloorHandle;
            bb.ResetGroundLossTimer();
        }

        // ============================================================================
        // ★ 본체 — 접근 보행 중 몸은 반드시 "가는 쪽"을 본다
        // ============================================================================

        [UnityTest]
        public IEnumerator 접근_보행_중_바라보는_방향이_진행_방향과_같다()
        {
            yield return SetUpFloor();
            StickmanBlackboard bb = _agent.Blackboard;

            // 배회 AI는 오른쪽을 말한다(활쏘기가 발동하기 직전까지 오른쪽으로 걷고 있던 상황).
            var intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = intent;
            Assert.Greater(Mathf.Abs(intent.MoveInputX), _clonedConfig.moveInputDeadzone,
                $"{LogPrefix} 재현 조건 실패 — 이동 의도가 불감대를 못 넘으면 덮어쓰기 자체가 일어나지 않습니다.");

            // 접근 목표는 **왼쪽**(디렉터의 back-step과 같은 형태: 과녁 반대쪽으로 한 걸음).
            float height = bb.CharacterHeightWorld;
            float startX = bb.Body.position.x;
            float standX = startX - height;                   // 왼쪽으로 신장 1배 = BackStepRatio 1.0
            float targetX = standX + height * 4.6f;           // 과녁은 오른쪽(= 배회 의도와 같은 쪽)

            bb.ArcheryStandWorldX = standX;
            bb.ArcheryTargetWorld = new Vector2(targetX, _floorTopWorldY + height * 0.5f);
            bb.ArcheryGroundWorldY = _floorTopWorldY;
            bb.ArcheryFacingSign = 1f;                        // 과녁은 오른쪽에 있다

            // ★ 디렉터가 전이 직전에 하는 것과 같은 초기화 — 과녁 쪽(오른쪽)을 보게 해 둔다.
            //   이 상태에서 왼쪽으로 걸어가므로, 아무도 방향을 고쳐주지 않으면 그대로 문워크가 된다.
            bb.SetFacingSign(1f);
            bb.Machine.ChangeState(StickmanStateId.Archery, isForcedInterrupt: true);

            Assert.AreEqual(StickmanStateId.Archery, bb.Machine.CurrentStateId,
                $"{LogPrefix} 활쏘기 상태로 들어가지 못했습니다.");

            Debug.Log($"{LogPrefix} 관찰 시작 — 시작 X={startX:F3} → 접근 목표 X={standX:F3}(왼쪽), " +
                $"과녁 X={targetX:F3}(오른쪽), 배회 이동의도={intent.MoveInputX:F1}(오른쪽). " +
                $"관찰 예산={ObserveSeconds:F2}초(벽시계).");

            float elapsed = 0f;
            int movingFrames = 0;
            int desyncFrames = 0;
            float worstVelocity = 0f;
            float worstFacing = 0f;

            while (elapsed < ObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (bb.Machine.CurrentStateId != StickmanStateId.Archery) break;
                // ★ 접근 페이즈만 판정한다. 도착(BeginIntro)하면 상태가 과녁 쪽으로 돌아서고
                //   FacingLocked를 건다 — 그 뒤로는 남은 수평 속도가 지수 감쇠로 죽는 동안
                //   "속도는 왼쪽 / 몸은 오른쪽"이 **정상**이므로 여기서 관찰을 끝낸다.
                if (bb.FacingLocked) break;

                float vx = bb.Body.linearVelocity.x;
                if (Mathf.Abs(vx) < MovingSpeedThreshold) continue;   // 정지/감쇠 구간은 판정 대상이 아니다

                movingFrames++;
                float facing = bb.FacingSign;
                if (facing * vx < 0f)
                {
                    desyncFrames++;
                    if (Mathf.Abs(vx) > Mathf.Abs(worstVelocity)) { worstVelocity = vx; worstFacing = facing; }
                }
            }

            Debug.Log($"{LogPrefix} 결과 — 관찰 {elapsed:F2}초, 이동 프레임 {movingFrames}개 중 " +
                $"방향 어긋남 {desyncFrames}개. 최악 표본: 속도 {worstVelocity:F2}유닛/초, 바라보는 방향 {worstFacing:F0}. " +
                $"최종 상태={bb.Machine.CurrentStateId}, 최종 X={bb.Body.position.x:F3}");

            Assert.Greater(movingFrames, 0,
                $"{LogPrefix} 재현 실패 — 접근 보행이 관찰되지 않았습니다(이동 프레임 0개). " +
                "목표까지 이미 도착했거나 상태가 즉시 빠져나갔다는 뜻이라, 이 테스트는 아무것도 재지 못했습니다.");
            Assert.AreEqual(0, desyncFrames,
                $"{LogPrefix} 접근 보행 {movingFrames}프레임 중 {desyncFrames}프레임에서 몸이 진행 방향과 " +
                $"**반대**를 봤습니다(예: 속도 {worstVelocity:F2}유닛/초인데 바라보는 방향 {worstFacing:F0}). " +
                "발은 왼쪽으로 가는데 걷기 사이클은 오른쪽 전진 사이클 그대로라 유저 눈에는 미끄러짐(문워크)입니다. " +
                "원인: ArcheryState.TickApproach의 SetFacingSign이 같은 프레임 뒤쪽 " +
                "StickmanBlackboard.TickPose에 덮이고 있습니다(StickmanBlackboard.IsFacingSelfManaged 참고).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 이 수정이 <b>배회 보행의 방향 갱신까지 죽이지는 않았는가</b>.
        /// 같은 리그에서 평범한 Walk로 두고 이동 의도를 뒤집으면 몸도 함께 돌아야 한다.
        /// (이 짝이 없으면 "모든 상태에서 방향을 고정" 같은 오답이 위 테스트를 통과시킨다.)
        /// </summary>
        [UnityTest]
        public IEnumerator 평범한_보행에서는_이동_의도가_여전히_방향을_바꾼다()
        {
            yield return SetUpFloor();
            StickmanBlackboard bb = _agent.Blackboard;

            var intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = intent;
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            float budget = 0.5f;
            float elapsed = 0f;
            while (elapsed < budget && bb.FacingSign < 0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.AreEqual(1f, bb.FacingSign, 0.001f,
                $"{LogPrefix} 오른쪽 이동 의도인데 {elapsed:F2}초 동안 오른쪽을 보지 않았습니다.");

            intent.MoveInputX = -1f;
            elapsed = 0f;
            while (elapsed < budget && bb.FacingSign > 0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 이동 의도 반전 후 {elapsed:F2}초 만에 방향 전환 — " +
                $"현재 FacingSign={bb.FacingSign:F0}, 상태={bb.Machine.CurrentStateId}");

            Assert.AreEqual(-1f, bb.FacingSign, 0.001f,
                $"{LogPrefix} 이동 의도를 왼쪽으로 뒤집었는데 {budget:F2}초가 지나도 몸이 돌지 않았습니다 — " +
                "방향 자기소유 목록이 너무 넓어져 배회 보행의 방향 갱신까지 막았다는 뜻입니다.");
        }
    }
}
