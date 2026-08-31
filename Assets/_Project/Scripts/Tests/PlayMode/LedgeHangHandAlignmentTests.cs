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
    /// ★ 사용자 실사용 버그 신고(2026-08-31): "창 위에서 떨어지기전 매달리는데 제대로 경계면에서
    /// 매달리는게 아니고 좀 밑에서 매달림".
    ///
    /// 기존 LedgeHangDescentTests는 매달린 높이를 <b>루트 Y == 모서리 Y − LedgeHangDropDepth</b>로만
    /// 검사한다. 그 식은 LedgeHangState가 실제로 쓰는 식 그 자체라 <b>동어반복</b>이다 — 손끝~발끝
    /// 거리(DropDepth) 자체가 틀려도 무조건 통과한다. 그래서 이 파일은 다른 것을 잰다:
    ///
    ///     <b>실제 손끝 Transform의 월드 Y</b>(팔 아래마디의 끝점, TransformPoint로 스케일까지 반영)
    ///     vs. <b>붙잡은 발판 상단의 월드 Y</b>(플랫폼 서비스가 말하는 진짜 경계선)
    ///
    /// 둘의 차이가 곧 사용자가 눈으로 본 어긋남이다. 포즈 애니메이터의 내부 계산
    /// (HangHandReachAboveRoot)을 한 줄도 참조하지 않으므로, 그 계산이 틀리면 여기서 반드시 걸린다.
    /// </summary>
    public sealed class LedgeHangHandAlignmentTests
    {
        private const string LogPrefix = "[LEDGEHANG-HAND]";

        private const long UpperHandle = 7101L;
        private const long LowerHandle = 7102L;

        private const float SettleWaitSeconds = 2.5f;

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
        private float _savedScale = -1f;

        private TestFootholdService _service;
        private FootholdPoller _poller;
        private ScriptedIntentSource _intent;

        private float _upperTopWorldY;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null)
            {
                if (_savedScale > 0f) _agent.ApplyCharacterScale(_savedScale, "테스트 복원");
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
            _savedScale = -1f;
        }

        /// <summary>손끝(팔 아래마디의 끝점)의 월드 좌표. 마디의 원점이 관절이고 선이 로컬 −y로
        /// length만큼 그려지므로(Editor/SceneBootstrapper.CreateLimbSegment), 끝점의 로컬 좌표는
        /// (0, −length)다. TransformPoint를 쓰므로 루트 스케일까지 자동으로 반영된다.</summary>
        private static bool TryHandTipWorld(Transform root, string armName, out Vector3 tip)
        {
            tip = default;
            if (root == null) return false;
            Transform upper = root.Find(armName);
            if (upper == null) return false;
            Transform end = upper.Find(armName + "Lower") ?? upper;
            var box = end.GetComponent<BoxCollider2D>();
            float length = box != null ? box.size.y : 0f;
            tip = end.TransformPoint(new Vector3(0f, -length, 0f));
            return true;
        }

        /// <summary>양손 중 더 높은 쪽의 월드 Y(둘은 대칭이라 사실상 같다).</summary>
        private static float HandTipWorldY(Transform root)
        {
            float y = float.NegativeInfinity;
            if (TryHandTipWorld(root, "RightArm", out Vector3 r)) y = Mathf.Max(y, r.y);
            if (TryHandTipWorld(root, "LeftArm", out Vector3 l)) y = Mathf.Max(y, l.y);
            return y;
        }

        private IEnumerator SetUpAtLedgeEdge()
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
            _savedScale = _agent.CurrentCharacterScale;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

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
            Vector3 standWorld = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(w * 0.60f - 5f, upperTopOs), 10f, _clonedConfig);
            _upperTopWorldY = standWorld.y;

            bb.Body.position = new Vector2(standWorld.x, standWorld.y);
            bb.Body.transform.position = new Vector3(standWorld.x, standWorld.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = UpperHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            // 매달린 자세를 충분히 오래 유지시켜 "붙잡기 보간 + 포즈 스무딩"이 확실히 끝난 뒤 재도록 한다.
            _clonedConfig.ledgeHangHoldDurationMin = 3f;
            _clonedConfig.ledgeHangHoldDurationMax = 3f;
            _clonedConfig.ledgeHangMaxDuration = 30f;

            Debug.Log($"{LogPrefix} 준비 완료 — 화면 {w:F0}x{h:F0}, 위쪽창 상단 월드Y={_upperTopWorldY:F4}, " +
                $"배율={_agent.CurrentCharacterScale:F4}(루트 localScale={_agent.transform.localScale.y:F4}), " +
                $"손끝~발끝(DropDepth)={bb.LedgeHangDropDepth:F4}유닛");
        }

        /// <summary>매달림에 진입시키고, 자세가 정착한 뒤 손끝 Y − 모서리 Y를 실측해 돌려준다.</summary>
        private IEnumerator MeasureHandOffset(System.Action<float, float, float> report)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            _intent.LedgeHangRequested = true;

            float wait = 0f;
            while (bb.Machine.CurrentStateId != StickmanStateId.LedgeHang && wait < 3f)
            {
                yield return null;
                wait += Time.deltaTime;
            }
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — LedgeHang에 진입하지 못했습니다.");
            _intent.LedgeHangRequested = false;

            // 붙잡기 보간(0.28초) + 팔 각도 스무딩(1/19.25초 ≈ 0.05초 상수)이 완전히 정착할 시간.
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 재기도 전에 매달림이 끝났습니다.");

            Assert.IsTrue(bb.TryGetFootholdEdgeWorld(UpperHandle, 1, out float edgeTopY, out float edgeX),
                $"{LogPrefix} 전제 실패 — 붙잡은 발판의 모서리를 조회하지 못했습니다.");

            Transform root = _agent.transform;
            float handY = HandTipWorldY(root);
            float rootY = bb.Body.position.y;
            float delta = handY - edgeTopY;

            TryHandTipWorld(root, "RightArm", out Vector3 rTip);
            Debug.Log($"{LogPrefix} 실측 — 배율={_agent.CurrentCharacterScale:F4}, 루트 localScale={root.localScale.y:F4}\n" +
                $"    발판 상단(경계면) 월드Y = {edgeTopY:F4}, 모서리 X = {edgeX:F4}\n" +
                $"    루트(발바닥) 월드Y      = {rootY:F4}  (모서리보다 {(edgeTopY - rootY):F4} 아래)\n" +
                $"    손끝 월드              = ({rTip.x:F4}, {rTip.y:F4})\n" +
                $"    ★ 손끝 − 경계면        = {delta:+0.0000;-0.0000}유닛  (음수 = 경계면보다 아래에서 매달림)\n" +
                $"    코드가 쓴 DropDepth     = {bb.LedgeHangDropDepth:F4}, 실제 손끝−발바닥 = {(handY - rootY):F4}");

            report?.Invoke(delta, handY, edgeTopY);
        }

        // ============================================================================
        // (1) 기본 배율 — 손끝이 경계면에 닿아야 한다
        // ============================================================================

        [UnityTest]
        public IEnumerator HandTipTouchesLedgeTopAtDefaultScale()
        {
            yield return SetUpAtLedgeEdge();

            float delta = float.NaN;
            yield return MeasureHandOffset((d, hand, edge) => delta = d);

            // 허용 오차: 흔들림(±5도)이 손끝을 최대 0.01유닛 정도 움직인다. 0.05유닛이면 넉넉하다.
            Assert.AreEqual(0f, delta, 0.05f,
                $"{LogPrefix} 손끝이 경계면에 닿지 않습니다 — 어긋남 {delta:F4}유닛 " +
                "(음수면 사용자 신고 그대로 '경계면보다 아래에서 매달림').");
        }

        // ============================================================================
        // (2) ★ 사용자 실제 재현 — 저장된 크기 0.35(다이얼 최소). 프리팹은 0.75로 구워져 있으므로
        //     루트 localScale = 0.4667이 되고, 포즈 지오메트리를 로컬 유닛 그대로 쓰면 손이
        //     경계면보다 (1 − 0.4667) × 1.88 ≈ 1.00유닛 아래로 내려간다 = "좀 밑에서 매달림".
        // ============================================================================

        [UnityTest]
        public IEnumerator HandTipTouchesLedgeTopAtUserSavedMinScale()
        {
            yield return SetUpAtLedgeEdge();
            yield return RunAtScale(StickConfig.MinCharacterScale, 0.03f);
        }

        // ============================================================================
        // (3) 반대편 극단 — 배율 최대에서도 같아야 한다(배율 불변)
        // ============================================================================

        [UnityTest]
        public IEnumerator HandTipTouchesLedgeTopAtMaxScale()
        {
            yield return SetUpAtLedgeEdge();
            yield return RunAtScale(StickConfig.MaxCharacterScale, 0.06f);
        }

        private IEnumerator RunAtScale(float scale, float tolerance)
        {
            Assert.IsTrue(_agent.ApplyCharacterScale(scale, "매달리기 손 정렬 실측"),
                $"{LogPrefix} 전제 실패 — 배율 {scale:F2} 적용에 실패했습니다.");
            yield return null;

            // 배율이 바뀌어도 발바닥(루트 원점)은 그대로라 발판 위 서 있는 자세가 유지된다.
            float delta = float.NaN;
            yield return MeasureHandOffset((d, hand, edge) => delta = d);

            Assert.AreEqual(0f, delta, tolerance,
                $"{LogPrefix} 배율 {scale:F2}에서 손끝이 경계면에 닿지 않습니다 — 어긋남 {delta:F4}유닛. " +
                "포즈 지오메트리(로컬 유닛)를 월드 유닛으로 변환하지 않은 것이 전형적 원인입니다.");
        }
    }
}
