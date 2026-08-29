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
    /// ★ 붙잡혔을 때 발버둥(2026-08-29, 사용자 명시 요청 "마우스로 캐릭을 잡았을때 막 벗어날려는듯이
    /// 몸부림 치게끔 만들어줘")의 실측 잠금.
    ///
    /// ============================================================================
    /// 무엇을 보고 있는가 — "몸부림친다"를 값으로 바꾸면 무엇인가
    /// ============================================================================
    ///   (A) **관절 각도가 실제로 변한다.** 잡혀 있는 동안 엉덩이/어깨 각도의 최대-최소 폭이 눈에
    ///       띄게 커야 한다. 상태 전이만 있고 포즈가 적용되지 않는(= 화면에서는 아무 일도 안 일어나는)
    ///       이 프로젝트의 단골 실패를 이 조건 하나가 잡는다.
    ///   (B) **강약의 리듬이 있다.** 일정한 진폭으로 계속 흔들면 기계다(리더 지시). 리듬 곡선을
    ///       직접 호출해 한 주기 안에 몸부림 구간과 지침 구간이 모두 존재함을 단언한다.
    ///   (C) **드래그 추종을 방해하지 않는다.** 발버둥은 팔다리 각도와 몸통 회전이지 루트 위치가
    ///       아니다 — 커서와 몸의 밀착 오차가 0에 붙어 있어야 한다("마우스에 딱 붙어서 끌려가야
    ///       하는데 이상하게 끌려감" 신고로 만들어진 즉시 밀착 경로의 회귀 잠금).
    ///   (D) **던지기로 넘어가는 이음매가 매끄럽다.** 몸부림 마지막 프레임의 관절 각도와 공중 회전
    ///       첫 프레임의 각도가 튀지 않아야 한다.
    ///
    /// 네거티브 컨트롤: StickConfig.dragStruggleEnabled를 끄면 (A)가 실제로 사라져야 한다
    /// (다리 각도가 사실상 고정). 이 프로젝트 표준이다.
    /// </summary>
    public sealed class DragStruggleTests
    {
        private const string LogPrefix = "[발버둥-TEST]";
        private const long FlatGroundHandle = 9401L;
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
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>잡고 있는 동안 관찰한 관절/추종 통계.</summary>
        private sealed class HoldObservation
        {
            public float HipRangeDegrees;      // 엉덩이 각도의 최대-최소 폭(좌우 중 큰 쪽)
            public float ArmRangeDegrees;      // 어깨 각도의 최대-최소 폭
            public float KneeRangeDegrees;     // 무릎 굽힘의 최대-최소 폭
            public float TwistRangeDegrees;    // 몸통(루트) 회전각의 최대-최소 폭
            public float WorstStickError;      // 커서와 잡은 지점의 최대 밀착 오차(월드 유닛)
            public float MinKneeSignedProduct = float.MaxValue; // 좌우 무릎 부호 곱의 최솟값
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private CursorPositionQuery _originalCursor;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
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
            _groundWorldY = physicsGround.GetComponent<BoxCollider2D>().bounds.max.y;

            Camera cam = bb.MainCamera;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, Screen.width, Mathf.Max(1f, Screen.height - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.IntentSource = new ScriptedIntentSource { MoveInputX = 0f };
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

            Debug.Log($"{LogPrefix} 준비 완료 — 지면 월드Y={_groundWorldY:F3}, 신장={_characterHeight:F3}, " +
                $"발버둥 스위치={_clonedConfig.dragStruggleEnabled}, 주파수={_clonedConfig.dragStruggleFrequencyHz:F2}Hz, " +
                $"리듬 주기={_clonedConfig.dragStruggleBurstPeriodSeconds:F2}초(버스트 비율 " +
                $"{_clonedConfig.dragStruggleBurstDutyFraction:F2}, 지침 세기 {_clonedConfig.dragStruggleRestIntensity:F2}).");
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, _cursorWorld, _clonedConfig, out _);
            return true;
        }

        /// <summary>커서로 붙잡아 <paramref name="seconds"/>초 동안 끌면서 관절/추종을 관찰한다.
        /// 상태를 빠져나오지 않는다(호출부가 놓거나 그대로 둔다).</summary>
        private IEnumerator HoldAndObserve(float seconds, Vector2 cursorVelocity, HoldObservation result)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();

            Vector2 start = new Vector2(0f, _groundWorldY + 4f);
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            _cursorWorld = start;
            bb.Machine.ChangeState(StickmanStateId.Dragged, isForcedInterrupt: true);

            float minHip = float.MaxValue, maxHip = float.MinValue;
            float minArm = float.MaxValue, maxArm = float.MinValue;
            float minKnee = float.MaxValue, maxKnee = float.MinValue;
            float minTwist = float.MaxValue, maxTwist = float.MinValue;

            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                _cursorWorld += cursorVelocity * dt;

                if (bb.Machine.CurrentStateId != StickmanStateId.Dragged) break;

                pose.GetUpperAngles(out float lLeg, out float rLeg, out float lArm, out _);
                pose.GetJointAngles(out float lKnee, out float rKnee, out _, out _);
                float twist = Mathf.DeltaAngle(bb.Body.transform.eulerAngles.z, 0f);

                minHip = Mathf.Min(minHip, lLeg); maxHip = Mathf.Max(maxHip, lLeg);
                minArm = Mathf.Min(minArm, lArm); maxArm = Mathf.Max(maxArm, lArm);
                minKnee = Mathf.Min(minKnee, lKnee); maxKnee = Mathf.Max(maxKnee, lKnee);
                minTwist = Mathf.Min(minTwist, twist); maxTwist = Mathf.Max(maxTwist, twist);
                result.MinKneeSignedProduct = Mathf.Min(result.MinKneeSignedProduct, lKnee * rKnee);

                // 밀착 오차 — 잡은 지점(= 이 배치에서는 몸통 원점)이 커서에서 얼마나 떨어졌는가.
                result.WorstStickError = Mathf.Max(result.WorstStickError,
                    Vector2.Distance(bb.Body.position, _cursorWorld));

                // 오른다리는 왼다리와 반대 위상이라 폭 계산에는 왼쪽만 쓴다(같은 폭이 나온다).
                _ = rLeg;
            }

            // 관찰이 끝나면 잡은 상태를 정리한다 — 그대로 두면 다음 관찰까지 Dragged가 살아 있어
            // 나중에 엉뚱한 시점에 던져진다(실측 로그에서 실제로 그런 유령 던지기가 찍혔다).
            if (bb.Machine.CurrentStateId == StickmanStateId.Dragged)
            {
                bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }

            result.HipRangeDegrees = maxHip - minHip;
            result.ArmRangeDegrees = maxArm - minArm;
            result.KneeRangeDegrees = maxKnee - minKnee;
            result.TwistRangeDegrees = maxTwist - minTwist;

            Debug.Log($"{LogPrefix} 관찰 {t:F2}초 — 엉덩이 폭={result.HipRangeDegrees:F1}도, " +
                $"어깨 폭={result.ArmRangeDegrees:F1}도, 무릎 폭={result.KneeRangeDegrees:F1}도, " +
                $"몸통 비틀림 폭={result.TwistRangeDegrees:F1}도, 최대 밀착 오차={result.WorstStickError:F4}유닛, " +
                $"좌우 무릎 부호곱 최솟값={result.MinKneeSignedProduct:F1}.");
        }

        // ============================================================================
        // (1) 핵심 — 잡혀 있는 동안 관절이 실제로 움직인다
        // ============================================================================

        [UnityTest]
        public IEnumerator HeldCharacterActuallyStruggles()
        {
            yield return SetUpFlatGround();

            var obs = new HoldObservation();
            yield return HoldAndObserve(1.6f, Vector2.zero, obs);

            Assert.Greater(obs.HipRangeDegrees, 20f,
                $"{LogPrefix} 잡혀 있는 동안 다리가 거의 움직이지 않았습니다(엉덩이 폭 {obs.HipRangeDegrees:F1}도). " +
                "DragThrowState.TickStruggle 또는 StickmanBlackboard.TickPose의 Dragged 분기가 빠지면 " +
                "Idle 중립 포즈가 그대로 유지됩니다.");
            Assert.Greater(obs.ArmRangeDegrees, 20f,
                $"{LogPrefix} 잡혀 있는 동안 팔이 거의 움직이지 않았습니다(어깨 폭 {obs.ArmRangeDegrees:F1}도).");
            Assert.Greater(obs.KneeRangeDegrees, 8f,
                $"{LogPrefix} 무릎이 거의 접었다 펴지지 않았습니다(폭 {obs.KneeRangeDegrees:F1}도).");
            Assert.Greater(obs.TwistRangeDegrees, 3f,
                $"{LogPrefix} 몸통이 비틀리지 않았습니다(폭 {obs.TwistRangeDegrees:F1}도) — " +
                "TickPose가 Dragged에서도 SnapRootUpright을 호출하면 이 값이 0이 됩니다.");
            Assert.Greater(obs.MinKneeSignedProduct, 0f,
                $"{LogPrefix} 좌우 무릎이 서로 반대 방향으로 접혔습니다(부호 곱 {obs.MinKneeSignedProduct:F1}) — " +
                "사람 관절은 한 방향으로만 접힙니다.");
        }

        // ============================================================================
        // (2) 네거티브 컨트롤 — 스위치를 끄면 실제로 멈춘다
        // ============================================================================

        [UnityTest]
        public IEnumerator NegativeControl_DisablingStruggleFreezesJoints()
        {
            yield return SetUpFlatGround();
            _clonedConfig.dragStruggleEnabled = false;

            var obs = new HoldObservation();
            yield return HoldAndObserve(1.6f, Vector2.zero, obs);

            // Idle 중립 포즈는 다리를 전혀 흔들지 않는다(호흡은 팔에만 idleBreathArmDegrees=1.5도 실린다).
            Assert.Less(obs.HipRangeDegrees, 3f,
                $"{LogPrefix} 스위치를 껐는데도 다리가 움직였습니다(엉덩이 폭 {obs.HipRangeDegrees:F1}도) — " +
                "탈출구가 실제로 동작하지 않습니다.");
            Assert.Less(obs.TwistRangeDegrees, 1f,
                $"{LogPrefix} 스위치를 껐는데도 몸통이 비틀렸습니다(폭 {obs.TwistRangeDegrees:F1}도).");
        }

        // ============================================================================
        // (3) 강약의 리듬이 실제로 있는가 (곡선 자체를 직접 검증)
        // ============================================================================

        [UnityTest]
        public IEnumerator StruggleRhythmHasBurstAndRestPhases()
        {
            yield return SetUpFlatGround();

            float period = _clonedConfig.dragStruggleBurstPeriodSeconds;
            float duty = _clonedConfig.dragStruggleBurstDutyFraction;
            float rest = _clonedConfig.dragStruggleRestIntensity;

            float peak = float.MinValue;
            float trough = float.MaxValue;
            int restSamples = 0;
            const int Samples = 240;
            for (int i = 0; i < Samples; i++)
            {
                float t = period * i / (Samples - 1f);
                float v = DragThrowState.EvaluateStruggleEnvelope(t, period, duty, rest);
                Assert.GreaterOrEqual(v, rest - 0.001f,
                    $"{LogPrefix} 리듬 값이 지침 세기({rest:F2}) 아래로 내려갔습니다({v:F3}, t={t:F3}) — " +
                    "버스트 경계에서 순간적으로 축 늘어져 보입니다.");
                Assert.LessOrEqual(v, 1.001f, $"{LogPrefix} 리듬 값이 1을 넘었습니다({v:F3}).");
                peak = Mathf.Max(peak, v);
                trough = Mathf.Min(trough, v);
                if (v <= rest + 0.001f) restSamples++;
            }

            Debug.Log($"{LogPrefix} 리듬 곡선 — 최대={peak:F3}, 최소={trough:F3}, 지침 구간 비율=" +
                $"{(restSamples * 100f / Samples):F0}%(설정 {(1f - duty) * 100f:F0}%).");

            Assert.Greater(peak, 0.95f, $"{LogPrefix} 리듬에 '세게 몸부림'하는 봉우리가 없습니다(최대 {peak:F2}).");
            Assert.Less(trough, peak * 0.5f, $"{LogPrefix} 강약 차이가 없습니다 — 일정한 진폭은 기계처럼 보입니다.");
            Assert.Greater(restSamples, Samples * 0.1f,
                $"{LogPrefix} '잠깐 지침' 구간이 사실상 없습니다(전체의 {(restSamples * 100f / Samples):F0}%).");

            // 시간이 지날수록 잦아들되 하한 아래로는 내려가지 않는다.
            float half = _clonedConfig.dragStruggleFatigueHalfLifeSeconds;
            float min = _clonedConfig.dragStruggleMinIntensity;
            float f0 = DragThrowState.EvaluateStruggleFatigue(0f, half, min);
            float f1 = DragThrowState.EvaluateStruggleFatigue(half, half, min);
            float f2 = DragThrowState.EvaluateStruggleFatigue(half * 6f, half, min);
            Debug.Log($"{LogPrefix} 지침 곡선 — 0초={f0:F3}, {half:F1}초={f1:F3}, {half * 6f:F1}초={f2:F3}(하한 {min:F2}).");
            Assert.AreEqual(1f, f0, 0.001f, $"{LogPrefix} 잡힌 직후가 최대 세기가 아닙니다.");
            Assert.Less(f1, f0, $"{LogPrefix} 시간이 지나도 잦아들지 않습니다.");
            Assert.GreaterOrEqual(f2, min - 0.001f, $"{LogPrefix} 오래 잡고 있으면 하한 아래로 내려갑니다(죽은 것처럼 보입니다).");
        }

        // ============================================================================
        // (4) 발버둥이 드래그 추종을 방해하지 않는다 (회귀 잠금)
        // ============================================================================

        [UnityTest]
        public IEnumerator StruggleDoesNotBreakCursorStickiness()
        {
            yield return SetUpFlatGround();

            var obs = new HoldObservation();
            yield return HoldAndObserve(1.2f, new Vector2(2.5f, 0.8f), obs);

            Assert.Greater(obs.HipRangeDegrees, 20f,
                $"{LogPrefix} 전제 실패 — 이 테스트는 발버둥이 켜진 상태를 전제합니다(엉덩이 폭 {obs.HipRangeDegrees:F1}도).");
            Assert.Less(obs.WorstStickError, 0.02f,
                $"{LogPrefix} 발버둥 때문에 몸이 커서에서 {obs.WorstStickError:F4}유닛 떨어졌습니다 — " +
                "발버둥은 팔다리 각도와 몸통 회전이어야지 루트 위치를 흔들면 안 됩니다" +
                "(2026-08-28 '마우스에 딱 붙어서 끌려가야 하는데 이상하게 끌려감' 수정의 회귀).");
        }

        // ============================================================================
        // (5) 몸부림 -> 던지기 이음매에서 팔다리가 튀지 않는다
        // ============================================================================

        [UnityTest]
        public IEnumerator StrugglePoseCarriesIntoTumbleWithoutSnap()
        {
            yield return SetUpFlatGround();
            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();

            // 잡고 흔들다가 놓는다(실제 던지기 경로).
            Vector2 start = new Vector2(-1f, _groundWorldY + 7f);
            bb.Body.bodyType = RigidbodyType2D.Dynamic;
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            _cursorWorld = start;
            bb.Machine.ChangeState(StickmanStateId.Dragged, isForcedInterrupt: true);

            Vector2 cursorVelocity = new Vector2(5f, 0f);
            float t = 0f;
            float lastHip = 0f, lastArm = 0f;
            while (t < 0.8f)
            {
                yield return null;
                t += Time.deltaTime;
                _cursorWorld += cursorVelocity * Time.deltaTime;
                pose.GetUpperAngles(out lastHip, out _, out lastArm, out _);
            }

            bb.DragReleaseSignaled = true;
            yield return null; // 이 프레임에 놓기 -> ThrowTumble 진입 + 첫 포즈 적용

            Assert.AreEqual(StickmanStateId.ThrowTumble, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 놓은 뒤 공중 회전 상태가 아닙니다(현재 {bb.Machine.CurrentStateId}).");

            pose.GetUpperAngles(out float hipNow, out _, out float armNow, out _);
            float hipJump = Mathf.Abs(Mathf.DeltaAngle(lastHip, hipNow));
            float armJump = Mathf.Abs(Mathf.DeltaAngle(lastArm, armNow));
            Debug.Log($"{LogPrefix} 이음매 — 엉덩이 {lastHip:F1} -> {hipNow:F1}(변화 {hipJump:F1}도), " +
                $"어깨 {lastArm:F1} -> {armNow:F1}(변화 {armJump:F1}도).");

            // 한 프레임에 허용할 변화량. 두 자세 모두 같은 지수 감쇠 보간을 거치므로 원리적으로
            // 한 프레임의 변화는 제한되어 있다 — 즉시 대입(스냅)으로 바뀌면 이 조건이 즉시 깨진다.
            Assert.Less(hipJump, 30f, $"{LogPrefix} 던지는 순간 다리 각도가 {hipJump:F1}도 튀었습니다(순간이동).");
            Assert.Less(armJump, 30f, $"{LogPrefix} 던지는 순간 팔 각도가 {armJump:F1}도 튀었습니다(순간이동).");
        }
    }
}
