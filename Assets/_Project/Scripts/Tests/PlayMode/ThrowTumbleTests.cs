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

        /// <summary>인체 무릎 굴곡의 대략적 한계(도). 프로덕션 상수의 사본이 아니라 **바깥의 자**다
        /// (design/motion R8 §1-7 축 B가 인용한 인체 가동범위). 웅크림 배율을 올리는 사람에게
        /// "여기서부터는 사람 무릎이 아니다"를 알려주는 것이 이 값의 유일한 목적이다.</summary>
        private const float HumanKneeFlexionLimitDegrees = 140f;

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

            // ★ 회전율의 **모양**을 재기 위한 시계열(축 A). 프레임 수가 아니라 벽시계 초로 쌓는다 —
            // 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아 프레임 수 기준 예산이 무의미하다.
            public readonly List<float> TumbleSampleSeconds = new List<float>();
            public readonly List<float> TumbleSampleCumulativeAbsDegrees = new List<float>();

            // 상태가 살아 있는 동안 상태 객체에서 직접 읽는 값(계획이 _spinSpeed를 덮은 **뒤**에도
            // 세기가 남아 있는지 = Enter 래치가 실제로 성립했는지의 증거).
            public float ThrowStrength01 = float.NaN;
            public float SpinShapeAmplitude = float.NaN;
            public int PlannedTurns;
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

                    result.TumbleSampleSeconds.Add(result.TumbleSeconds);
                    result.TumbleSampleCumulativeAbsDegrees.Add(result.AbsRotationDegrees);
                    if (bb.Machine.GetState(StickmanStateId.ThrowTumble) is ThrowTumbleState tumble)
                    {
                        result.ThrowStrength01 = tumble.ThrowStrength01;
                        result.SpinShapeAmplitude = tumble.SpinShapeAmplitude;
                        result.PlannedTurns = tumble.PlannedTurns;
                    }
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
                $"마지막 회전 프레임 기울기={result.LastTumbleTiltDegrees:F2}도, 세기={result.ThrowStrength01:F3}" +
                $"(프로파일 A={result.SpinShapeAmplitude:F3}, 계획 {result.PlannedTurns}바퀴), 무릎앉아={result.SawLandingCrouch}, " +
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

        // ============================================================================
        // (6) ★★ 축 A — 회전율 정형이 "총 회전각을 보존한다"는 계약을 산술로 못박는다
        // ============================================================================
        //
        // 왜 최종 각도만 보고 끝내면 안 되는가 (이 파일에서 가장 중요한 주석):
        //   AdvanceRotation의 비례 제어는 `step = Min(speed*dt, remaining)`으로 잘리고 남은 각도가
        //   0에 수렴하므로, **K가 무엇이든 최종 각도는 목표에 도달한다.** 즉 "총 회전각이 delta다"를
        //   최종 각도로 재면 그건 자명한 참이고 아무것도 증명하지 못한다(이 저장소가 아홉 번 당한
        //   "실패한 측정과 성공한 측정이 똑같이 생겼다"의 전형이다).
        //   진짜 계약은 **"보정 없이도 딱 맞는다"**이고, 그것은 K가 만들어내는 회전율 r(u)의
        //   적분이 1이라는 뜻이다. 그래서 아래는
        //     (a) 설계식 r/ρ를 **이 테스트가 독립적으로** 적고,
        //     (b) 프로덕션 K가 그 둘과 항등식 K·ρ/(1−u) = r 을 만족하는지 대조하고,
        //     (c) 그 독립 r의 적분이 1인지 잰다.
        //   프로덕션 함수로 기대값을 만들지 않으므로(docs/TEAM.md §"기대값을 프로덕션 함수로 만들지
        //   마라") K가 틀어지면 (b)가 빨개진다.

        /// <summary>설계식(design/motion R8 §1-7)을 **테스트가 직접** 적은 것. 프로덕션을 부르지 않는다.</summary>
        private static float ExpectedRate(float u, float amplitude) => 1f + amplitude * Mathf.Cos(Mathf.PI * u);

        /// <summary>설계식의 "남은 회전 비율" ρ(u) = ∫ᵤ¹ r. 역시 테스트가 직접 적은 것이다.</summary>
        private static float ExpectedRemainingFraction(float u, float amplitude)
            => (1f - u) - amplitude * Mathf.Sin(Mathf.PI * u) / Mathf.PI;

        [UnityTest]
        public IEnumerator SpinShapeIsAreaPreservingAndIdenticalAtTheEnd()
        {
            yield return SetUpFlatGround();

            float amplitudeAtMaxStrength = _clonedConfig.throwTumbleSpinShapeAmplitude;
            Assert.Greater(amplitudeAtMaxStrength, 0f,
                $"{LogPrefix} 배포 설정에서 회전율 정형이 꺼져 있습니다(throwTumbleSpinShapeAmplitude=" +
                $"{amplitudeAtMaxStrength:F3}). 그러면 아래 단언이 전부 '등속과 같다'로 공허하게 통과합니다 — " +
                "에셋을 확인하십시오(DefaultStickConfig.asset).");

            float[] amplitudes = { amplitudeAtMaxStrength, amplitudeAtMaxStrength * 0.5f, amplitudeAtMaxStrength * 0.1f };
            const int Samples = 4000;

            foreach (float a in amplitudes)
            {
                // (b) 항등식 K(u)·ρ(u)/(1−u) = r(u). 프로덕션 K vs 테스트가 적은 r/ρ.
                float worst = 0f;
                float worstU = 0f;
                int compared = 0;
                for (int i = 0; i <= Samples; i++)
                {
                    float u = (float)i / Samples;
                    float rho = ExpectedRemainingFraction(u, a);
                    float remainingFraction = 1f - u;
                    // 프로덕션이 극한값으로 못박는 꼬리 구간은 제외하고 따로 (아래) 단언한다.
                    if (rho <= 1e-3f || remainingFraction <= 0f) continue;

                    float k = ThrowTumbleState.ResolveSpinShapeMultiplier(u, a);
                    float realized = k * rho / remainingFraction;
                    float error = Mathf.Abs(realized - ExpectedRate(u, a));
                    compared++;
                    if (error > worst) { worst = error; worstU = u; }
                }

                Assert.Greater(compared, Samples / 2,
                    $"{LogPrefix} 항등식 표본이 {compared}개뿐입니다 — 스윕이 죽었다는 뜻이라 이 단언이 공허합니다.");
                Assert.LessOrEqual(worst, 1e-4f,
                    $"{LogPrefix} 회전율 정형 계수가 설계식과 갈라졌습니다(진폭 {a:F3}에서 최대 오차 " +
                    $"{worst:E3}, u={worstU:F4}). K(u)·ρ(u)/(1−u) = r(u) 가 깨지면 총 회전각 보존이 " +
                    "무너지고, 그 손실을 착지 직전의 비례 제어가 몰아서 메우게 됩니다(= 팽이처럼 튀는 프레임).");

                // (c) ∫₀¹ r(u) du = 1 — 이것이 "총 회전각이 delta 그대로"의 산술적 실체다(심프슨).
                double integral = 0.0;
                const int N = 2000;
                for (int i = 0; i <= N; i++)
                {
                    double w = (i == 0 || i == N) ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
                    integral += w * ExpectedRate((float)i / N, a);
                }
                integral *= (1.0 / N) / 3.0;
                Assert.AreEqual(1.0, integral, 1e-5,
                    $"{LogPrefix} 정형된 회전율의 적분이 1이 아닙니다(진폭 {a:F3}에서 {integral:F9}) — " +
                    "총 회전각이 delta에서 벗어난다는 뜻입니다.");

                // K(1) = 1 — 마지막 구간은 오늘의 컨트롤러와 한 글자도 다르지 않아야 한다.
                float kEnd = ThrowTumbleState.ResolveSpinShapeMultiplier(1f, a);
                AssertBitwiseEqual(1f, kEnd,
                    $"{LogPrefix} K(1)이 정확히 1이 아닙니다(진폭 {a:F3}에서 {kEnd:R}) — 착지 직전 구간이 " +
                    "오늘과 달라지면 이 상태의 핵심 계약(정수 바퀴 · 직립 착지)이 회귀 위험에 놓입니다.");

                // 안전 상한을 건드리지 않는다 — max K < throwTumbleAlignMaxSpeedFactor.
                float maxK = 0f;
                float maxKu = 0f;
                for (int i = 0; i <= Samples; i++)
                {
                    float u = (float)i / Samples;
                    float k = ThrowTumbleState.ResolveSpinShapeMultiplier(u, a);
                    if (k > maxK) { maxK = k; maxKu = u; }
                }
                float factor = _clonedConfig.throwTumbleAlignMaxSpeedFactor;
                Debug.Log($"{LogPrefix} 정형 진폭 {a:F3} — 항등식 최대 오차 {worst:E3}, ∫r={integral:F9}, " +
                    $"max K={maxK:F4}(u={maxKu:F3}) vs 정렬 상한 {factor:F2}(여유 {(factor - maxK) / factor * 100f:F1}%).");
                Assert.Less(maxK, factor,
                    $"{LogPrefix} 정형 계수의 최댓값({maxK:F4})이 정렬 안전 상한({factor:F2})을 넘습니다 — " +
                    "그러면 정형이 상한에 잘려 설계대로 돌지 않습니다. 상한을 올려 진폭을 키우는 것은 " +
                    "'팽이처럼 튀는 프레임'을 막는 장치를 깎는 것이라 명시적으로 기각된 선택지입니다. " +
                    "throwTumbleSpinShapeAmplitude를 내리십시오.");
            }
        }

        // ============================================================================
        // (7) ★★ 탈출구 — 진폭 0이면 오늘과 **비트 단위로** 같다 (되돌릴 문)
        // ============================================================================

        [UnityTest]
        public IEnumerator ZeroAmplitudeRestoresTodaysConstantRateBitForBit()
        {
            yield return SetUpFlatGround();

            // (i) 계수 자체가 **정확히** 1f여야 한다. 1f에 아주 가까운 값이면 곱셈이 항등이 아니다.
            float[] progresses = { 0f, 0.001f, 0.11f, 0.25f, 0.4999f, 0.5f, 0.75f, 0.9999f, 1f, 1.5f, -0.3f, float.NaN };
            foreach (float u in progresses)
            {
                AssertBitwiseEqual(1f, ThrowTumbleState.ResolveSpinShapeMultiplier(u, 0f),
                    $"{LogPrefix} 진폭 0인데 정형 계수가 정확히 1이 아닙니다(u={u}).");
                AssertBitwiseEqual(1f, ThrowTumbleState.ResolveSpinShapeMultiplier(u, float.NaN),
                    $"{LogPrefix} 진폭이 NaN인데 정형 계수가 1로 폴백하지 않았습니다(u={u}) — " +
                    "연출을 끄는 쪽이 안전한 폴백입니다.");
                AssertBitwiseEqual(1f, ThrowTumbleState.ResolveSpinShapeMultiplier(u, -0.3f),
                    $"{LogPrefix} 진폭이 음수인데 정형 계수가 1로 폴백하지 않았습니다(u={u}).");
            }

            // ★ 진행도가 NaN이어도 계수는 반드시 유한해야 한다. NaN이 새면 매 프레임 경로라
            //   회전각 전체가 NaN이 되고 캐릭터가 화면에서 사라진다.
            float nanProgress = ThrowTumbleState.ResolveSpinShapeMultiplier(
                float.NaN, _clonedConfig.throwTumbleSpinShapeAmplitude);
            Assert.IsFalse(float.IsNaN(nanProgress),
                $"{LogPrefix} 진행도가 NaN일 때 정형 계수가 NaN이 되었습니다 — 각속도가 통째로 오염됩니다.");
            AssertBitwiseEqual(1f, nanProgress,
                $"{LogPrefix} 진행도가 NaN일 때 계수가 1로 폴백하지 않았습니다({nanProgress:R}).");

            // (ii) 그 1f를 곱해도 각속도의 비트가 변하지 않는다(= 프로덕션 한 줄이 항등이다).
            float[] speeds = { 0f, 1e-7f, 0.1f, 218.4f, 359.99997f, 545.7f, 720f, 1152.3f, 1e9f };
            foreach (float speed in speeds)
            {
                float k = ThrowTumbleState.ResolveSpinShapeMultiplier(0.37f, 0f);
                AssertBitwiseEqual(speed, speed * k,
                    $"{LogPrefix} 진폭 0에서 각속도({speed:R})에 계수를 곱했더니 비트가 달라졌습니다 — " +
                    "'되돌릴 문'이 실제로는 닫혀 있다는 뜻입니다.");
            }

            // (iii) 축 B도 같은 탈출구를 갖는다 — 배율 1/1이면 각도 묶음이 비트 단위로 같다.
            _clonedConfig.throwTumbleTuckScaleAtWeakThrow = 1f;
            _clonedConfig.throwTumbleTuckScaleAtStrongThrow = 1f;
            _clonedConfig.throwTumbleSpreadScaleAtWeakThrow = 1f;
            _clonedConfig.throwTumbleSpreadScaleAtStrongThrow = 1f;

            StickmanPoseAnimator.ThrowTumblePoseSettings baseline = _agent.Blackboard.BuildThrowTumblePoseSettings();
            foreach (float strength in new[] { 0f, 0.31f, 0.5f, 0.87f, 1f })
            {
                StickmanPoseAnimator.ThrowTumblePoseSettings scaled = baseline.ScaledBy(
                    ThrowTumbleState.ResolveTuckJointScale(strength, _clonedConfig),
                    ThrowTumbleState.ResolveTuckSpreadScale(strength, _clonedConfig));

                AssertBitwiseEqual(baseline.HipDegrees, scaled.HipDegrees, $"{LogPrefix} 엉덩이(세기 {strength:F2})");
                AssertBitwiseEqual(baseline.KneeBendDegrees, scaled.KneeBendDegrees, $"{LogPrefix} 무릎(세기 {strength:F2})");
                AssertBitwiseEqual(baseline.ArmDegrees, scaled.ArmDegrees, $"{LogPrefix} 어깨(세기 {strength:F2})");
                AssertBitwiseEqual(baseline.ElbowBendDegrees, scaled.ElbowBendDegrees, $"{LogPrefix} 팔꿈치(세기 {strength:F2})");
                AssertBitwiseEqual(baseline.LimbSpreadDegrees, scaled.LimbSpreadDegrees, $"{LogPrefix} 벌림(세기 {strength:F2})");
            }

            // ★ 양성 대조 — 위 다섯 단언이 "무엇이든 통과"가 아니라는 증거. 배포 배율을 되돌리면
            //   같은 비교가 **반드시 실패**해야 한다(부재 단언이 조용히 초록이 되는 것을 막는다).
            _clonedConfig.throwTumbleTuckScaleAtWeakThrow = _originalConfig.throwTumbleTuckScaleAtWeakThrow;
            StickmanPoseAnimator.ThrowTumblePoseSettings probe = baseline.ScaledBy(
                ThrowTumbleState.ResolveTuckJointScale(0f, _clonedConfig),
                ThrowTumbleState.ResolveTuckSpreadScale(0f, _clonedConfig));
            Assert.AreNotEqual(Bits(baseline.KneeBendDegrees), Bits(probe.KneeBendDegrees),
                $"{LogPrefix} 양성 대조 실패 — 배율을 되돌렸는데도 각도가 비트 단위로 같습니다. " +
                "위의 '비트 동일' 단언들이 실제로는 아무것도 재고 있지 않다는 뜻입니다.");

            Debug.Log($"{LogPrefix} 탈출구 확인 — 진폭 0에서 계수 비트 동일, 배율 1/1에서 각도 비트 동일, " +
                "양성 대조(배율 복원 시 각도가 실제로 달라짐) 통과.");
        }

        private static int Bits(float value) => System.BitConverter.ToInt32(System.BitConverter.GetBytes(value), 0);

        private static void AssertBitwiseEqual(float expected, float actual, string message)
        {
            Assert.AreEqual(Bits(expected), Bits(actual),
                $"{message} — 기대 {expected:R}(0x{Bits(expected):X8}) / 실제 {actual:R}(0x{Bits(actual):X8}).");
        }

        // ============================================================================
        // (8) 축 B — 웅크림 깊이가 던진 세기를 따라간다 (벌림만 반대 방향)
        // ============================================================================

        [UnityTest]
        public IEnumerator TuckDepthFollowsThrowStrengthAndSpreadGoesTheOtherWay()
        {
            yield return SetUpFlatGround();

            StickmanPoseAnimator.ThrowTumblePoseSettings b = _agent.Blackboard.BuildThrowTumblePoseSettings();

            float weakScale = _clonedConfig.throwTumbleTuckScaleAtWeakThrow;
            float strongScale = _clonedConfig.throwTumbleTuckScaleAtStrongThrow;
            float weakSpread = _clonedConfig.throwTumbleSpreadScaleAtWeakThrow;
            float strongSpread = _clonedConfig.throwTumbleSpreadScaleAtStrongThrow;

            Assert.Less(weakScale, strongScale,
                $"{LogPrefix} 웅크림 배율이 세기와 함께 커지지 않습니다({weakScale:F2} -> {strongScale:F2}).");
            Assert.Greater(weakSpread, strongSpread,
                $"{LogPrefix} 벌림 배율의 방향이 뒤집혀 있습니다({weakSpread:F2} -> {strongSpread:F2}) — " +
                "느슨하게 도는 몸은 팔다리가 벌어져야 두 개로 보입니다.");

            StickmanPoseAnimator.ThrowTumblePoseSettings loose = b.ScaledBy(
                ThrowTumbleState.ResolveTuckJointScale(0f, _clonedConfig),
                ThrowTumbleState.ResolveTuckSpreadScale(0f, _clonedConfig));
            StickmanPoseAnimator.ThrowTumblePoseSettings tight = b.ScaledBy(
                ThrowTumbleState.ResolveTuckJointScale(1f, _clonedConfig),
                ThrowTumbleState.ResolveTuckSpreadScale(1f, _clonedConfig));

            // 기대값은 프로덕션이 아니라 **설정 상수 x 기준 각도**에서 독립적으로 만든다.
            Assert.AreEqual(b.HipDegrees * weakScale, loose.HipDegrees, 1e-3f, $"{LogPrefix} 느슨 엉덩이");
            Assert.AreEqual(b.KneeBendDegrees * weakScale, loose.KneeBendDegrees, 1e-3f, $"{LogPrefix} 느슨 무릎");
            Assert.AreEqual(b.ArmDegrees * weakScale, loose.ArmDegrees, 1e-3f, $"{LogPrefix} 느슨 어깨");
            Assert.AreEqual(b.ElbowBendDegrees * weakScale, loose.ElbowBendDegrees, 1e-3f, $"{LogPrefix} 느슨 팔꿈치");
            Assert.AreEqual(b.LimbSpreadDegrees * weakSpread, loose.LimbSpreadDegrees, 1e-3f, $"{LogPrefix} 느슨 벌림");

            Assert.AreEqual(b.HipDegrees * strongScale, tight.HipDegrees, 1e-3f, $"{LogPrefix} 꽉 엉덩이");
            Assert.AreEqual(b.KneeBendDegrees * strongScale, tight.KneeBendDegrees, 1e-3f, $"{LogPrefix} 꽉 무릎");
            Assert.AreEqual(b.ArmDegrees * strongScale, tight.ArmDegrees, 1e-3f, $"{LogPrefix} 꽉 어깨");
            Assert.AreEqual(b.ElbowBendDegrees * strongScale, tight.ElbowBendDegrees, 1e-3f, $"{LogPrefix} 꽉 팔꿈치");
            Assert.AreEqual(b.LimbSpreadDegrees * strongSpread, tight.LimbSpreadDegrees, 1e-3f, $"{LogPrefix} 꽉 벌림");

            // 사람 무릎은 뒤로 꺾이지 않는다 — 저장소 최심(무릎앉아 앞무릎)의 바로 위까지만 허용한다.
            Assert.Greater(tight.KneeBendDegrees, _clonedConfig.landingCrouchFrontKneeDegrees,
                $"{LogPrefix} 꽉 만 텀블링 무릎({tight.KneeBendDegrees:F1}도)이 무릎앉아 최심" +
                $"({_clonedConfig.landingCrouchFrontKneeDegrees:F1}도)보다 얕습니다 — 그러면 '꽉 말았다'가 읽히지 않습니다.");
            Assert.Less(tight.KneeBendDegrees, HumanKneeFlexionLimitDegrees,
                $"{LogPrefix} 꽉 만 텀블링 무릎({tight.KneeBendDegrees:F1}도)이 인체 무릎 굴곡 한계" +
                $"(~{HumanKneeFlexionLimitDegrees:F0}도)를 넘습니다.");

            // 중간 세기가 단조롭게 놓인다(양 끝만 맞추고 가운데가 뒤집히는 구현을 배제한다).
            float previousKnee = float.NegativeInfinity;
            float previousSpread = float.PositiveInfinity;
            for (int i = 0; i <= 10; i++)
            {
                float strength = i / 10f;
                StickmanPoseAnimator.ThrowTumblePoseSettings mid = b.ScaledBy(
                    ThrowTumbleState.ResolveTuckJointScale(strength, _clonedConfig),
                    ThrowTumbleState.ResolveTuckSpreadScale(strength, _clonedConfig));
                Assert.Greater(mid.KneeBendDegrees, previousKnee,
                    $"{LogPrefix} 세기 {strength:F1}에서 무릎 굽힘이 단조 증가하지 않았습니다.");
                Assert.Less(mid.LimbSpreadDegrees, previousSpread,
                    $"{LogPrefix} 세기 {strength:F1}에서 벌림이 단조 감소하지 않았습니다.");
                previousKnee = mid.KneeBendDegrees;
                previousSpread = mid.LimbSpreadDegrees;
            }

            Debug.Log($"{LogPrefix} 축 B 확인 — 느슨(엉덩이 {loose.HipDegrees:F0} 무릎 {loose.KneeBendDegrees:F0} " +
                $"어깨 {loose.ArmDegrees:F0} 팔꿈치 {loose.ElbowBendDegrees:F0} 벌림 {loose.LimbSpreadDegrees:F0}) -> " +
                $"꽉(엉덩이 {tight.HipDegrees:F0} 무릎 {tight.KneeBendDegrees:F0} 어깨 {tight.ArmDegrees:F0} " +
                $"팔꿈치 {tight.ElbowBendDegrees:F0} 벌림 {tight.LimbSpreadDegrees:F0}).");
        }

        // ============================================================================
        // (9) 세기(s01)는 도달 가능한 던지기 대역을 0~1로 채운다
        // ============================================================================

        [UnityTest]
        public IEnumerator ThrowStrengthSpansTheReachableThrowBand()
        {
            yield return SetUpFlatGround();

            float baseline = StickConfig.BaselineCharacterTotalHeight;
            float minHeightsPerSecond = _clonedConfig.throwTumbleMinSpeedHeightsPerSecond;
            float maxSpeed = _clonedConfig.dragThrowMaxSpeed;

            foreach (float scale in new[] { 1f, 0.75f, 0.5f })
            {
                float height = baseline * scale;

                float atFloor = ThrowTumbleState.ResolveThrowStrength01(minHeightsPerSecond * height, height, _clonedConfig);
                float atCeiling = ThrowTumbleState.ResolveThrowStrength01(maxSpeed, height, _clonedConfig);
                float belowFloor = ThrowTumbleState.ResolveThrowStrength01(0f, height, _clonedConfig);
                float aboveCeiling = ThrowTumbleState.ResolveThrowStrength01(maxSpeed * 3f, height, _clonedConfig);

                Assert.AreEqual(0f, atFloor, 1e-4f,
                    $"{LogPrefix} 배율 {scale:F2}: 회전 하한 세기에서 s01이 0이 아닙니다({atFloor:F4}).");
                Assert.AreEqual(1f, atCeiling, 1e-4f,
                    $"{LogPrefix} 배율 {scale:F2}: 던지기 속도 상한에서 s01이 1이 아닙니다({atCeiling:F4}) — " +
                    "가장 세게 던져도 연출의 최대치에 닿지 못한다는 뜻입니다(maxSpin 720이 구조적으로 " +
                    "도달 불가였던 것과 같은 형태의 결함).");
                Assert.AreEqual(0f, belowFloor, 1e-6f, $"{LogPrefix} 배율 {scale:F2}: 하한 아래가 0으로 잘리지 않았습니다.");
                Assert.AreEqual(1f, aboveCeiling, 1e-6f, $"{LogPrefix} 배율 {scale:F2}: 상한 위가 1로 잘리지 않았습니다.");

                float previous = float.NegativeInfinity;
                for (int i = 0; i <= 12; i++)
                {
                    float speed = maxSpeed * i / 12f;
                    float value = ThrowTumbleState.ResolveThrowStrength01(speed, height, _clonedConfig);
                    Assert.GreaterOrEqual(value, previous,
                        $"{LogPrefix} 배율 {scale:F2}: 속도 {speed:F2}에서 세기가 단조 증가하지 않았습니다.");
                    previous = value;
                }
            }

            // 설정이 없어도(테스트/폴백 경로) 죽지 않는다.
            Assert.AreEqual(0f, ThrowTumbleState.ResolveThrowStrength01(0f, 0f, null), 1e-6f,
                $"{LogPrefix} 신장 0 · 설정 null에서 s01이 안전한 0으로 떨어지지 않았습니다.");

            Debug.Log($"{LogPrefix} 세기 대역 확인 — 하한 {minHeightsPerSecond:F2}신장/초에서 0, " +
                $"상한 {maxSpeed:F1}유닛/초에서 1(배율 1.0/0.75/0.5 공통).");
        }

        // ============================================================================
        // (10) ★★ 실제 던지기 A/B — 같은 던지기의 **총 회전각은 같고 모양만 다르다**
        // ============================================================================
        //
        // 이 라운드의 결함("던지면 회전이 늘 똑같아 보인다")을 화면 쪽에서 재는 유일한 테스트다.
        // 자로 쓰는 값은 **"전체 회전 시간 중 앞 절반을 도는 데 쓴 비율"**이다:
        //   · 등속이면 정확히 0.50 (앞 절반과 뒤 절반이 같은 시간)
        //   · 앞에서 세게 차고 나가면 0.50보다 작아진다
        // 프레임 수나 누적 각도가 아니라 **시간 비율**이라, 착지 정렬 여유(lead) 동안 회전이 0인
        // 구간이 뒤에 붙어도 흔들리지 않는다.

        [UnityTest]
        public IEnumerator SameThrowKeepsTotalRotationButChangesTheRateProfile()
        {
            yield return SetUpFlatGround();

            Assert.Greater(_clonedConfig.throwTumbleSpinShapeAmplitude, 0f,
                $"{LogPrefix} 배포 설정에서 정형이 꺼져 있어 A/B 대조가 성립하지 않습니다.");

            // A: 배포 설정 그대로(정형 ON). 세게 던져야 진폭이 실제로 커진다.
            var shaped = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(10f, 4f), dragSeconds: 0.35f);
            yield return ObserveFlight(shaped);
            Assert.IsTrue(shaped.SawTumble, $"{LogPrefix} 정형 ON 던지기가 회전 상태로 가지 않았습니다.");

            // ★ Enter 래치의 증거 — 계획이 _spinSpeed를 덮어쓴 **뒤**의 프레임에서 읽은 값이다.
            Assert.Greater(shaped.ThrowStrength01, 0.7f,
                $"{LogPrefix} 세게 던졌는데 상태가 들고 있는 세기가 {shaped.ThrowStrength01:F3}뿐입니다. " +
                "s01을 Enter에서 LastThrowVelocity로 래치하지 않고 _spinSpeed에서 역산하면 정확히 이렇게 " +
                "됩니다(계획이 _spinSpeed를 delta/usable로 덮어쓰므로 세기 정보가 이미 사라진 뒤입니다).");
            Assert.AreEqual(_clonedConfig.throwTumbleSpinShapeAmplitude * shaped.ThrowStrength01,
                shaped.SpinShapeAmplitude, 1e-3f,
                $"{LogPrefix} 진폭이 '설정 x 세기'와 다릅니다({shaped.SpinShapeAmplitude:F4}).");

            yield return new WaitForSeconds(0.4f);

            // B: 정형만 끈다(탈출구). 나머지는 한 글자도 같다.
            _clonedConfig.throwTumbleSpinShapeAmplitude = 0f;
            var flat = new ThrowObservation();
            yield return DragAndRelease(startHeight: 7f, cursorVelocity: new Vector2(10f, 4f), dragSeconds: 0.35f);
            yield return ObserveFlight(flat);
            Assert.IsTrue(flat.SawTumble, $"{LogPrefix} 정형 OFF 던지기가 회전 상태로 가지 않았습니다.");
            Assert.AreEqual(0f, flat.SpinShapeAmplitude, 1e-6f,
                $"{LogPrefix} 정형을 껐는데 상태의 진폭이 {flat.SpinShapeAmplitude:F4}입니다.");

            float shapedFraction = MeasureFirstHalfTimeFraction(shaped);
            float flatFraction = MeasureFirstHalfTimeFraction(flat);

            Debug.Log($"{LogPrefix} A/B — 정형 ON: 총 {shaped.AbsRotationDegrees:F1}도({shaped.PlannedTurns}바퀴), " +
                $"앞 절반 시간 비율 {shapedFraction:F3} / 정형 OFF: 총 {flat.AbsRotationDegrees:F1}도" +
                $"({flat.PlannedTurns}바퀴), 앞 절반 시간 비율 {flatFraction:F3}. " +
                $"세기={shaped.ThrowStrength01:F3}(A={shaped.SpinShapeAmplitude:F3}).");

            // ★ 총 회전각 보존 — 계산이 아니라 **실제 씬에서** 잰 값끼리 비교한다.
            Assert.AreEqual(flat.PlannedTurns, shaped.PlannedTurns,
                $"{LogPrefix} 두 비행의 계획 바퀴 수가 다릅니다(ON {shaped.PlannedTurns} vs OFF {flat.PlannedTurns}). " +
                "정형은 계획(정수 바퀴)에 관여하지 않으므로 같아야 합니다 — 다르면 비행 시간 편차로 " +
                "계획이 갈린 것이니 다시 돌려보고, 재현되면 정형이 TryPlanRotation을 침범한 것입니다.");
            Assert.AreEqual(flat.AbsRotationDegrees, shaped.AbsRotationDegrees, UprightToleranceDegrees,
                $"{LogPrefix} 정형이 총 회전각을 바꿨습니다(ON {shaped.AbsRotationDegrees:F1}도 vs " +
                $"OFF {flat.AbsRotationDegrees:F1}도). 정형은 면적 보존이라 총 회전각이 같아야 합니다. " +
                "차이가 360도 근처면 두 비행의 **계획 바퀴 수가 달라진 것**이므로(비행 시간 편차) 다시 " +
                "돌려보고, 그래도 갈라지면 정형이 계획을 침범하고 있는 것입니다.");
            Assert.LessOrEqual(shaped.LastTumbleTiltDegrees, UprightToleranceDegrees,
                $"{LogPrefix} 정형 ON에서 착지 직전 몸이 {shaped.LastTumbleTiltDegrees:F1}도 기울어 있었습니다 — " +
                "정형이 직립 착지 계약을 깼습니다.");

            // ★ 대조군이 실제로 등속인가 — 이 줄이 없으면 아래 단언이 무엇과 비교하는지 알 수 없다.
            Assert.AreEqual(0.5f, flatFraction, 0.06f,
                $"{LogPrefix} 정형 OFF인데 앞 절반 시간 비율이 {flatFraction:F3}입니다(등속이면 0.50). " +
                "대조군이 등속이 아니면 아래 비교가 아무것도 증명하지 못합니다.");

            // ★ 본 단언 — 정형이 실제로 앞을 빠르게 만든다.
            Assert.Less(shapedFraction, flatFraction - 0.04f,
                $"{LogPrefix} 정형을 켰는데 회전율의 모양이 등속과 구분되지 않습니다" +
                $"(ON {shapedFraction:F3} vs OFF {flatFraction:F3}). 이것이 사용자가 신고한 " +
                "'던지면 회전이 늘 똑같아 보인다'가 그대로 남아 있다는 뜻입니다.");
        }

        /// <summary>
        /// "전체 회전을 마치는 데 걸린 시간 중, 앞 절반을 도는 데 쓴 비율". 등속이면 0.50이고
        /// 앞에서 세게 차고 나갈수록 작아진다. ★ 회전이 끝난 뒤 착지까지 남는 무회전 구간
        /// (throwTumbleAlignLeadSeconds)이 뒤에 붙어도 값이 흔들리지 않도록, 분모를 상태 전체가 아니라
        /// **회전이 사실상 끝난 시점**으로 잡는다.
        /// </summary>
        private static float MeasureFirstHalfTimeFraction(ThrowObservation obs)
        {
            List<float> seconds = obs.TumbleSampleSeconds;
            List<float> cumulative = obs.TumbleSampleCumulativeAbsDegrees;
            Assert.Greater(seconds.Count, 8,
                $"{LogPrefix} 회전 표본이 {seconds.Count}개뿐이라 회전율의 모양을 잴 수 없습니다.");

            float total = cumulative[cumulative.Count - 1];
            Assert.Greater(total, 300f, $"{LogPrefix} 누적 회전이 {total:F1}도뿐입니다.");

            float halfSeconds = float.NaN;
            float fullSeconds = float.NaN;
            for (int i = 0; i < cumulative.Count; i++)
            {
                if (float.IsNaN(halfSeconds) && cumulative[i] >= total * 0.5f) halfSeconds = seconds[i];
                if (float.IsNaN(fullSeconds) && cumulative[i] >= total - 0.5f) { fullSeconds = seconds[i]; break; }
            }

            Assert.IsFalse(float.IsNaN(halfSeconds), $"{LogPrefix} 절반 지점을 찾지 못했습니다.");
            Assert.IsFalse(float.IsNaN(fullSeconds), $"{LogPrefix} 회전 완료 지점을 찾지 못했습니다.");
            Assert.Greater(fullSeconds, 0f, $"{LogPrefix} 회전 완료 시각이 0입니다.");
            return halfSeconds / fullSeconds;
        }
    }
}
