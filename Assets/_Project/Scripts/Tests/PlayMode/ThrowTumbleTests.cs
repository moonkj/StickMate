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
    /// ★ 던지기 공중 회전 → 착지 정렬 → 무릎앉아(2026-08-29, 사용자 명시 요청 "마우스로 던졌을때도
    /// 이상하게 관절꺽이면서 넘어지는데 던져도 공중에서 회전하면서 무릎앉아 착지할수있게 해줘")의 실측 잠금.
    ///
    /// ============================================================================
    /// 무엇을 보고 있는가 — 상태 ID가 아니라 **실제 루트 회전각**
    /// ============================================================================
    /// 이 프로젝트는 "통과하는 테스트가 버그를 2라운드 연속 놓친" 전례가 있어(프레이밍 테스트의 상대
    /// 마진 방식) 연출 테스트는 절대 조건으로 쓴다. 여기서는 매 프레임 루트 Transform의 Z 회전을 읽어
    /// 프레임 간 변화량을 적분한다:
    ///   (A) **정말 회전했는가** — 누적 회전량이 한 바퀴(360도) 이상.
    ///   (B) **정수 바퀴로 끝났는가** — 누적 회전량이 360의 정수배(허용 오차 8도).
    ///   (C) **착지 시점에 몸이 바로 서 있는가** — 회전 상태의 **마지막 프레임**에서 직립(0도)과의
    ///       차이가 8도 이내. 착지 후가 아니라 착지 직전을 보는 것이 핵심이다(상태를 빠져나오면서
    ///       회전이 0으로 복구되므로, 착지 후를 보면 무엇을 하든 통과하는 무의미한 테스트가 된다).
    ///   (D) **랙돌로 가지 않았는가** — Ragdoll/Getup을 한 프레임도 보지 않아야 한다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 프로젝트 표준)
    /// ============================================================================
    /// StickConfig.throwTumbleEnabled를 끄고 **같은 던지기**를 반복하면 예전 경로로 돌아가 실제로
    /// RAGDOLL이 되어야 한다. 그것이 사용자가 신고한 "던지면 관절 꺾이며 넘어짐"의 재현이자,
    /// 위 (D)가 우연이 아니라는 증거다.
    ///
    /// ============================================================================
    /// 배율 불변성 (리더 지시)
    /// ============================================================================
    /// 런타임 프리팹은 한 배율로만 구워져 있어(에디터 툴로 굽는다) 실행 중 배율을 바꿔가며 던져볼 수
    /// 없다. 그래서 배율 의존이 실제로 들어 있는 지점 — "던진 속도 → 회전 파라미터" 파생식 —을
    /// 정적 순수 함수로 분리해두고(ThrowTumbleState.IsCleanThrow / ResolveSpinSpeedDegreesPerSecond)
    /// 배율 1.0 / 0.75 / 0.5의 신장을 직접 넣어 **같은 체감 세기에서 같은 결과**가 나오는지 단언한다.
    /// 회전 중심(엉덩이 높이)은 StickmanMetrics.HipLocalY 실측이라 정의상 배율을 따른다.
    /// </summary>
    public sealed class ThrowTumbleTests
    {
        private const string LogPrefix = "[던지기회전-TEST]";
        private const long FlatGroundHandle = 9301L;

        private const float SettleWaitSeconds = 2.0f;
        private const float MaxObserveSeconds = 8f;

        /// <summary>착지 시점 직립 허용 오차(도). 8도면 육안으로 "바로 섰다"로 읽히는 한계 근처이고,
        /// 정렬이 작동하지 않는 경우(수십~수백 도)와는 확실히 구분된다.</summary>
        private const float UprightToleranceDegrees = 8f;

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
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>한 번의 던지기를 관찰한 결과.</summary>
        private sealed class ThrowObservation
        {
            public bool SawTumble;
            public bool SawRagdoll;
            public bool SawGetup;
            public bool SawLandingCrouch;
            public bool Settled;
            public float AbsRotationDegrees;      // 회전 상태 동안 누적된 회전량의 절대 합
            public float SignedRotationDegrees;   // 부호 있는 누적(회전 방향 판정용)
            public float LastTumbleTiltDegrees = float.NaN; // 회전 상태 마지막 프레임의 직립 대비 기울기
            public float TumbleSeconds;
            public StickmanStateId FinalState;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private CursorPositionQuery _originalCursor;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;
        private float _groundWorldY;
        private float _characterHeight;
        private Vector2 _cursorWorld;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                _agent.Blackboard.CursorProvider = _originalCursor;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
        }

        // ============================================================================
        // 공통 준비 — LandingCrouchTests와 같은 배치(물리 바닥 상단에 화면 전폭 발판 1장)
        // ============================================================================

        private IEnumerator SetUpFlatGround()
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
            _originalCursor = bb.CursorProvider;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            GameObject physicsGround = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(physicsGround, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다.");
            var groundBox = physicsGround.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(groundBox, $"{LogPrefix} PhysicsGround에 BoxCollider2D가 없습니다.");
            _groundWorldY = groundBox.bounds.max.y;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, w, Mathf.Max(1f, h - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new ScriptedIntentSource { MoveInputX = 0f };
            bb.IntentSource = _intent;

            // 스크립트 커서 — 드래그&던지기의 **실제 경로**를 그대로 태우기 위한 배선.
            bb.CursorProvider = TryGetScriptedCursor;

            Vector2 start = new Vector2(0f, _groundWorldY);
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FlatGroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            yield return new WaitForSeconds(0.5f);
            _characterHeight = bb.CharacterHeightWorld;

            Debug.Log($"{LogPrefix} 준비 완료 — 지면 월드Y={_groundWorldY:F3}, 신장={_characterHeight:F3}유닛, " +
                $"회전 스위치={_clonedConfig.throwTumbleEnabled}, 회전 하한={_clonedConfig.throwTumbleMinSpeedHeightsPerSecond:F2}신장/초, " +
                $"랙돌 임계={_clonedConfig.ragdollForceThreshold:F1}, 던지기 속도 상한={_clonedConfig.dragThrowMaxSpeed:F1}.");
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, _cursorWorld, _clonedConfig, out _);
            return true;
        }

        // ============================================================================
        // 관찰기
        // ============================================================================

        /// <summary>
        /// **실제 드래그&던지기 경로**로 던진다: 캐릭터를 지정 높이에 두고 커서로 붙잡아
        /// (Dragged 진입) 지정 속도로 끌다가 놓는다. 던진 속도는 DragThrowState가 커서 이력에서
        /// 스스로 계산하므로, 이 경로를 태워야 "사용자가 실제로 던졌을 때"를 검증한 것이 된다.
        /// </summary>
        private IEnumerator DragAndRelease(float startHeight, Vector2 cursorVelocity, float dragSeconds)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            Vector2 start = new Vector2(-Mathf.Abs(cursorVelocity.x) * dragSeconds * 0.5f, _groundWorldY + startHeight);
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            _cursorWorld = start;

            bb.Machine.ChangeState(StickmanStateId.Dragged, isForcedInterrupt: true);

            float t = 0f;
            while (t < dragSeconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                _cursorWorld += cursorVelocity * dt;
            }

            bb.DragReleaseSignaled = true;
            Debug.Log($"{LogPrefix} 놓기 신호 — 커서 속도={cursorVelocity.ToString("F2")}(속력 {cursorVelocity.magnitude:F2} = " +
                $"{cursorVelocity.magnitude / _characterHeight:F2}신장/초), 놓은 위치={_cursorWorld.ToString("F2")}, " +
                $"지면까지 {(_cursorWorld.y - _groundWorldY):F2}유닛.");
        }

        /// <summary>던진 뒤의 상태/회전을 매 프레임 관찰한다.</summary>
        private IEnumerator ObserveFlight(ThrowObservation result)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            Transform root = bb.Body.transform;
            float prevZ = root.eulerAngles.z;
            bool everLeftCrouch = false;
            float elapsed = 0f;

            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                float z = root.eulerAngles.z;
                float delta = Mathf.DeltaAngle(prevZ, z);
                prevZ = z;

                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.Ragdoll) result.SawRagdoll = true;
                if (state == StickmanStateId.Getup) result.SawGetup = true;
                if (state == StickmanStateId.LandingCrouch) result.SawLandingCrouch = true;

                if (state == StickmanStateId.ThrowTumble)
                {
                    // ★ 회전 상태로 넘어온 **첫 프레임의 변화량은 버린다.** 그 프레임의 직전 표본은
                    // 아직 Dragged(발버둥으로 몸통이 최대 dragStruggleTwistDegrees만큼 비틀려 있음)
                    // 시점의 각도라, 놓는 순간의 비틀림 해제(9도)가 회전량으로 잘못 합산된다
                    // (실측으로 확인: 누적 369도 = 실제 회전 360 + 비틀림 해제 9). 회전 자체를 재는
                    // 것이 목적이므로 기준점만 다시 잡는다.
                    if (!result.SawTumble) { result.SawTumble = true; continue; }
                    result.SawTumble = true;
                    result.TumbleSeconds += Time.deltaTime;
                    result.AbsRotationDegrees += Mathf.Abs(delta);
                    result.SignedRotationDegrees += delta;
                    result.LastTumbleTiltDegrees = Mathf.Abs(Mathf.DeltaAngle(z, 0f));
                }
                else if (result.SawLandingCrouch && state != StickmanStateId.LandingCrouch)
                {
                    everLeftCrouch = true;
                }

                if ((state == StickmanStateId.Idle || state == StickmanStateId.Walk) &&
                    (everLeftCrouch || elapsed > 1.5f))
                {
                    result.Settled = true;
                    break;
                }
            }

            result.FinalState = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 비행 관찰 — 회전상태={result.SawTumble}({result.TumbleSeconds:F2}초), " +
                $"누적 회전={result.AbsRotationDegrees:F0}도(부호합 {result.SignedRotationDegrees:F0}), " +
                $"마지막 회전 프레임 기울기={result.LastTumbleTiltDegrees:F2}도, 무릎앉아={result.SawLandingCrouch}, " +
                $"랙돌={result.SawRagdoll}, 기상={result.SawGetup}, 복귀={result.Settled}({result.FinalState}), " +
                $"최종 Y={bb.Body.position.y:F3}(지면 {_groundWorldY:F3}), 총 {elapsed:F2}초.");
        }

        // ============================================================================
        // (1) 핵심 — 던지면 랙돌이 아니라 회전 후 무릎앉아로 착지한다
        // ============================================================================

        [UnityTest]
        public IEnumerator ThrownCharacterTumblesThenLandsInCrouchWithoutRagdoll()
        {
            yield return SetUpFlatGround();

            var obs = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(4.5f, 0.6f), dragSeconds: 0.35f);
            yield return ObserveFlight(obs);

            Assert.IsTrue(obs.SawTumble,
                $"{LogPrefix} 던졌는데 공중 회전 상태로 가지 않았습니다 — DragThrowState의 갈림 판정" +
                "(ThrowTumbleState.IsCleanThrow) 또는 StickmanAgent의 ThrowTumble 등록이 빠졌을 가능성이 큽니다.");
            Assert.IsFalse(obs.SawRagdoll,
                $"{LogPrefix} 던지기가 RAGDOLL로 갔습니다 — 사용자가 신고한 '관절 꺾이며 넘어짐'이 그대로 재발한 것입니다.");
            Assert.IsFalse(obs.SawGetup, $"{LogPrefix} Getup이 관측되었습니다 — 랙돌을 거쳤다는 뜻입니다.");

            // (A) 정말 회전했는가.
            Assert.GreaterOrEqual(obs.AbsRotationDegrees, 300f,
                $"{LogPrefix} 공중에서 실제로 회전하지 않았습니다(누적 {obs.AbsRotationDegrees:F0}도). " +
                "StickmanBlackboard.TickPose의 ThrowTumble 예외가 빠지면 SnapRootUpright가 매 프레임 회전을 지웁니다.");

            // (B) 정수 바퀴로 끝났는가.
            float turns = obs.AbsRotationDegrees / 360f;
            float turnError = Mathf.Abs(turns - Mathf.Round(turns)) * 360f;
            Assert.LessOrEqual(turnError, UprightToleranceDegrees,
                $"{LogPrefix} 회전이 정수 바퀴로 끝나지 않았습니다(누적 {obs.AbsRotationDegrees:F1}도 = {turns:F2}바퀴, " +
                $"오차 {turnError:F1}도) — 착지 정렬(TryPlanRotation/AdvanceRotation)이 작동하지 않습니다.");

            // (C) 착지 직전에 몸이 바로 서 있었는가 — 이 라운드의 핵심 요구.
            Assert.LessOrEqual(obs.LastTumbleTiltDegrees, UprightToleranceDegrees,
                $"{LogPrefix} 착지 직전 몸이 {obs.LastTumbleTiltDegrees:F1}도 기울어 있었습니다(허용 {UprightToleranceDegrees:F0}도) — " +
                "거꾸로/비스듬히 착지하면 무릎앉아가 읽히지 않습니다.");

            Assert.IsTrue(obs.SawLandingCrouch,
                $"{LogPrefix} 착지가 무릎앉아로 이어지지 않았습니다(ThrowTumbleState.ConfirmLanding).");
            Assert.IsTrue(obs.Settled,
                $"{LogPrefix} {MaxObserveSeconds}초 안에 Idle/Walk로 복귀하지 못했습니다(최종 {obs.FinalState}).");
            Assert.AreEqual(_groundWorldY, _agent.Blackboard.Body.position.y, 0.06f,
                $"{LogPrefix} 연출이 끝난 뒤 발 높이가 지면과 어긋났습니다.");
        }

        // ============================================================================
        // (2) 회전 방향은 던진 방향에서 나온다 (상수가 아니다)
        // ============================================================================

        [UnityTest]
        public IEnumerator SpinDirectionFollowsThrowDirection()
        {
            yield return SetUpFlatGround();

            var right = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(5f, 0f), dragSeconds: 0.35f);
            yield return ObserveFlight(right);
            Assert.IsTrue(right.SawTumble, $"{LogPrefix} 오른쪽 던지기가 회전 상태로 가지 않았습니다.");

            yield return new WaitForSeconds(0.4f);

            var left = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(-5f, 0f), dragSeconds: 0.35f);
            yield return ObserveFlight(left);
            Assert.IsTrue(left.SawTumble, $"{LogPrefix} 왼쪽 던지기가 회전 상태로 가지 않았습니다.");

            Debug.Log($"{LogPrefix} 회전 방향 대조 — 오른쪽 던지기 부호합={right.SignedRotationDegrees:F0}도, " +
                $"왼쪽 던지기 부호합={left.SignedRotationDegrees:F0}도.");

            // 오른쪽으로 던지면 앞구르기 = 시계 방향 = 음의 Z. 왼쪽은 그 반대.
            Assert.Less(right.SignedRotationDegrees, -180f,
                $"{LogPrefix} 오른쪽으로 던졌는데 앞구르기(시계 방향)로 돌지 않았습니다({right.SignedRotationDegrees:F0}도).");
            Assert.Greater(left.SignedRotationDegrees, 180f,
                $"{LogPrefix} 왼쪽으로 던졌는데 앞구르기(반시계 방향)로 돌지 않았습니다({left.SignedRotationDegrees:F0}도).");
        }

        // ============================================================================
        // (3) 아주 살살 놓으면 회전하지 않는다 (내려놓은 것과 던진 것의 구분)
        // ============================================================================

        [UnityTest]
        public IEnumerator GentleReleaseDoesNotTumble()
        {
            yield return SetUpFlatGround();

            float gentleSpeed = _clonedConfig.throwTumbleMinSpeedHeightsPerSecond * _characterHeight * 0.4f;
            var obs = new ThrowObservation();
            yield return DragAndRelease(startHeight: 5f, cursorVelocity: new Vector2(gentleSpeed, 0f), dragSeconds: 0.4f);
            yield return ObserveFlight(obs);

            Assert.IsFalse(obs.SawTumble,
                $"{LogPrefix} 살살 내려놓았는데 공중제비를 돌았습니다(커서 {gentleSpeed:F2}유닛/초 = " +
                $"{gentleSpeed / _characterHeight:F2}신장/초 < 하한 {_clonedConfig.throwTumbleMinSpeedHeightsPerSecond:F2}).");
            Assert.IsFalse(obs.SawRagdoll, $"{LogPrefix} 살살 놓았는데 랙돌이 되었습니다.");
            Assert.IsTrue(obs.Settled, $"{LogPrefix} Idle/Walk로 복귀하지 못했습니다(최종 {obs.FinalState}).");
        }

        // ============================================================================
        // (4) ★ 네거티브 컨트롤 — 스위치를 끄면 같은 던지기가 실제로 랙돌이 된다
        // ============================================================================

        [UnityTest]
        public IEnumerator NegativeControl_DisablingTumbleFallsBackToRagdoll()
        {
            yield return SetUpFlatGround();
            _clonedConfig.throwTumbleEnabled = false;

            // 예전 경로는 "충격량(속력 × 질량) >= ragdollForceThreshold"에서만 랙돌이므로, 그 임계를
            // 확실히 넘는 세기로 던진다(질량 1 기준 속력 >= 8).
            float mass = _agent.Blackboard.Body.mass;
            float needSpeed = _clonedConfig.ragdollForceThreshold / Mathf.Max(0.01f, mass);
            Assert.Less(needSpeed, _clonedConfig.dragThrowMaxSpeed,
                $"{LogPrefix} 전제 실패 — 랙돌 임계 속력({needSpeed:F2})이 던지기 속도 상한" +
                $"({_clonedConfig.dragThrowMaxSpeed:F2})보다 커서 어떤 던지기로도 재현할 수 없습니다.");

            var obs = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(needSpeed + 1.5f, 0f), dragSeconds: 0.35f);
            yield return ObserveFlight(obs);

            Assert.IsFalse(obs.SawTumble,
                $"{LogPrefix} 스위치를 껐는데도 공중 회전이 발동했습니다 — 탈출구가 실제로 동작하지 않습니다.");
            Assert.IsTrue(obs.SawRagdoll,
                $"{LogPrefix} 스위치를 껐는데 랙돌이 되지 않았습니다 — 이 테스트가 겨냥한 '예전 거동'이 " +
                "재현되지 않았다는 뜻이므로, (1)번의 '랙돌로 가지 않는다'도 우연일 수 있습니다.");
        }

        // ============================================================================
        // (5) 배율 불변성 — 파생식을 배율 1.0 / 0.75 / 0.5 신장에 직접 넣어 확인
        // ============================================================================

        [UnityTest]
        public IEnumerator ThrowTumbleDerivationIsScaleInvariant()
        {
            yield return SetUpFlatGround();

            float baseline = StickConfig.BaselineCharacterTotalHeight;
            float[] scales = { 1f, 0.75f, 0.5f };

            // "같은 체감 세기" = 초당 같은 신장 배수. 배율이 달라도 결과가 같아야 한다.
            float[] feelStrengths = { 0.6f, 1.2f, 3f, 7f }; // 신장/초 (하한 1.2를 사이에 두고 고름)
            for (int f = 0; f < feelStrengths.Length; f++)
            {
                bool? expectTumble = null;
                float? expectSpin = null;
                for (int i = 0; i < scales.Length; i++)
                {
                    float height = baseline * scales[i];
                    float speed = feelStrengths[f] * height;   // 같은 체감 세기의 절대 속도
                    bool tumble = ThrowTumbleState.IsCleanThrow(speed, height, _clonedConfig);
                    float spin = ThrowTumbleState.ResolveSpinSpeedDegreesPerSecond(speed, height, _clonedConfig);

                    if (expectTumble == null) { expectTumble = tumble; expectSpin = spin; continue; }
                    Assert.AreEqual(expectTumble.Value, tumble,
                        $"{LogPrefix} 같은 체감 세기({feelStrengths[f]:F2}신장/초)인데 배율 {scales[i]:F2}에서 " +
                        "회전 발동 여부가 달라졌습니다 — 속도를 신장으로 나누지 않은 곳이 있습니다.");
                    Assert.AreEqual(expectSpin.Value, spin, 0.01f,
                        $"{LogPrefix} 같은 체감 세기({feelStrengths[f]:F2}신장/초)인데 배율 {scales[i]:F2}에서 " +
                        $"회전 각속도가 달라졌습니다({expectSpin.Value:F1} vs {spin:F1}도/초).");
                }
                Debug.Log($"{LogPrefix} 배율 불변 확인 — 체감 세기 {feelStrengths[f]:F2}신장/초 -> " +
                    $"회전={expectTumble}, 각속도={expectSpin:F1}도/초 (배율 1.0/0.75/0.5 동일).");
            }

            // 같은 **절대** 속도라면 작은 캐릭터일수록 체감이 빨라 더 빨리 돌아야 한다(무차원화가
            // 실제로 방향성 있는 효과를 내는지 확인 — 위 단언만으로는 "전부 상수"여도 통과한다).
            float absSpeed = 5f;
            float spinBig = ThrowTumbleState.ResolveSpinSpeedDegreesPerSecond(absSpeed, baseline, _clonedConfig);
            float spinSmall = ThrowTumbleState.ResolveSpinSpeedDegreesPerSecond(absSpeed, baseline * 0.5f, _clonedConfig);
            Assert.Greater(spinSmall, spinBig,
                $"{LogPrefix} 같은 절대 속도에서 작은 캐릭터가 더 빨리 돌지 않았습니다" +
                $"({spinSmall:F1} vs {spinBig:F1}도/초) — 신장 무차원화가 상수로 대체된 것은 아닌지 확인 필요.");

            // 세게 던질수록 빨리 돈다(상·하한 사이 구간에서 단조 증가).
            float slow = ThrowTumbleState.ResolveSpinSpeedDegreesPerSecond(3f * baseline, baseline, _clonedConfig);
            float fast = ThrowTumbleState.ResolveSpinSpeedDegreesPerSecond(6f * baseline, baseline, _clonedConfig);
            Assert.Greater(fast, slow,
                $"{LogPrefix} 세게 던졌는데 회전이 빨라지지 않았습니다({fast:F1} vs {slow:F1}도/초).");
        }
    }
}
