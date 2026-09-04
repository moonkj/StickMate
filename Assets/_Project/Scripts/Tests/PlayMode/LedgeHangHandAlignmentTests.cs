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

        /// <summary>매달린 방향 쪽으로 <b>가장 멀리 나간</b> 손끝의 월드 X.
        /// <para>두 팔은 좌우 대칭이 아니다(팔꿈치 굽힘 부호가 좌우 공통이라 한쪽 전완은 몸 쪽으로,
        /// 다른 쪽은 바깥으로 돈다). 그래서 "어느 팔이 바깥인가"를 가정하지 않고 <b>둘 다 재서 극단을
        /// 고른다</b> — 바라보는 방향이 매달린 방향과 어긋난 프레임에서도 이 측정은 옳다.</para></summary>
        private static float OutermostHandTipWorldX(Transform root, int direction)
        {
            float best = direction > 0 ? float.NegativeInfinity : float.PositiveInfinity;
            if (TryHandTipWorld(root, "RightArm", out Vector3 r))
                best = direction > 0 ? Mathf.Max(best, r.x) : Mathf.Min(best, r.x);
            if (TryHandTipWorld(root, "LeftArm", out Vector3 l))
                best = direction > 0 ? Mathf.Max(best, l.x) : Mathf.Min(best, l.x);
            return best;
        }

        /// <summary>지금 이 화면에서 월드 1유닛이 몇 OS 포인트인가 — 프로덕션과 <b>같은 창구</b>로 잰다.
        /// 배치모드(640x480 = 20pt/유닛)와 배포(982pt = 40.92pt/유닛)가 2배 다르므로 상수 환산은 금지다.</summary>
        private float PointsPerWorldUnit()
            => GroundSensor.ComputeOsPointsPerWorldUnit(_agent.Blackboard.MainCamera, _clonedConfig);

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

        // ============================================================================
        // ★★ 가로축 — 「완전 끝」이 아니라 「적당히 끝쪽」에 매달린다
        //     사용자 신고 2026-09-02 "맥같은 경우도 창모서리가 타원이라 끝은 비어있는 공간에 매달려있음"
        //     사용자 요청 2026-09-03 "완전 끝말고 적당히 끝쪽만 되도 될거 같은데"
        //
        //     위 (1)(2)(3)은 전부 **세로축**(손끝 Y vs 경계면 Y)만 잰다. 사용자가 본 결함은 **가로축**에
        //     있었고 그래서 그 셋을 전부 통과한 채 살아 있었다. 아래 둘이 가로축을 잰다.
        // ============================================================================

        /// <summary>붙잡은 발판의 오른쪽 모서리 X와, 그 방향으로 가장 멀리 나간 손끝 X를 함께 재서
        /// 보고한다(양수 = 모서리 바깥 = 그려진 픽셀이 없는 자리).</summary>
        private IEnumerator MeasureHandOutsideEdge(System.Action<float, float, float, float> report)
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

            // 붙잡기 보간(ledgeHangGrabDuration) + 팔 각도 스무딩이 정착할 시간. ★ 프레임 수가 아니라
            // 벽시계 기준이다(배치모드는 2,000fps 이상이라 프레임 수 대기는 실제로 0.01초일 수 있다).
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(StickmanStateId.LedgeHang, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — 재기도 전에 매달림이 끝났습니다.");

            Assert.IsTrue(bb.TryGetFootholdEdgeWorld(UpperHandle, 1, out _, out float edgeX),
                $"{LogPrefix} 전제 실패 — 붙잡은 발판의 모서리를 조회하지 못했습니다.");

            float handX = OutermostHandTipWorldX(_agent.transform, 1);
            float bodyX = bb.Body.position.x;
            float ppu = PointsPerWorldUnit();

            Debug.Log($"{LogPrefix} 가로축 실측 — 배율={_agent.CurrentCharacterScale:F4}, 환산={ppu:F2}pt/유닛\n" +
                $"    발판 오른쪽 모서리 X = {edgeX:F4}\n" +
                $"    매달린 루트 X        = {bodyX:F4}  (모서리보다 {(edgeX - bodyX):F4} 안쪽 = {((edgeX - bodyX) * ppu):F2}pt)\n" +
                $"    ★ 바깥 손끝 X        = {handX:F4}  (모서리보다 {(edgeX - handX):F4} 안쪽 = {((edgeX - handX) * ppu):F2}pt)\n" +
                $"    곡률 여유 설정        = {_clonedConfig.ledgeHangCornerClearancePoints:F2}pt " +
                $"= {(_clonedConfig.ledgeHangCornerClearancePoints / ppu):F4}유닛");

            report?.Invoke(edgeX, handX, bodyX, ppu);
        }

        // ============================================================================
        // (4) ★ 손끝이 창 모서리 곡률 <b>안쪽</b>에 있어야 한다 = 그려진 픽셀을 잡는다
        // ============================================================================

        [UnityTest]
        public IEnumerator OuterHandTipStaysInsideTheRoundedCornerClearance()
        {
            yield return SetUpAtLedgeEdge();

            float edgeX = float.NaN, handX = float.NaN, ppu = float.NaN;
            yield return MeasureHandOutsideEdge((e, hand, body, p) => { edgeX = e; handX = hand; ppu = p; });

            // 기대값을 프로덕션 함수가 아니라 **설정 필드**에서 되살린다(생성기·검사기 공유 금지 규약).
            float requiredClearanceWorld = _clonedConfig.ledgeHangCornerClearancePoints / ppu;
            float actualInsideWorld = edgeX - handX;

            Assert.GreaterOrEqual(actualInsideWorld, requiredClearanceWorld,
                $"{LogPrefix} 바깥 손끝이 창 모서리 곡률 안으로 못 들어왔습니다 — " +
                $"모서리에서 {actualInsideWorld:F4}유닛({(actualInsideWorld * ppu):F2}pt) 안쪽인데 " +
                $"필요한 여유는 {requiredClearanceWorld:F4}유닛({_clonedConfig.ledgeHangCornerClearancePoints:F2}pt)입니다. " +
                "음수면 손이 아예 창 바깥(=그려진 픽셀이 없는 자리)에 있다는 뜻이고, 그것이 사용자 신고 " +
                "'맥같은 경우도 창모서리가 타원이라 끝은 비어있는 공간에 매달려있음' 그대로입니다.");
        }

        // ============================================================================
        // (5) ★ 매달리려고 캐릭터를 모서리 <b>바깥으로</b> 끌어내지 않는다
        //     — 결함의 실체는 "모서리 + ledgeHangEdgeOffset"으로의 강제 스냅이었다.
        // ============================================================================

        [UnityTest]
        public IEnumerator HangDoesNotDragTheBodyOutwardPastTheLedgeEdge()
        {
            yield return SetUpAtLedgeEdge();

            float standingX = _agent.Blackboard.Body.position.x;

            float edgeX = float.NaN, bodyX = float.NaN, ppu = float.NaN;
            yield return MeasureHandOutsideEdge((e, hand, body, p) => { edgeX = e; bodyX = body; ppu = p; });

            Assert.Less(bodyX, edgeX,
                $"{LogPrefix} 매달린 루트가 모서리 바깥({bodyX:F4} >= {edgeX:F4})에 있습니다 — " +
                "예전 강제 스냅(모서리 + ledgeHangEdgeOffset)이 되살아났습니다.");

            // 루트 자체도 곡률 여유만큼은 안쪽이어야 한다 — 손끝(위 (4))과 **다른 양**이다.
            // ★ "서 있던 X와 비교"로 적지 않는 이유: 준비 직후부터 진입까지 캐릭터가 실제로 걷기 때문에
            //   그 사이 이동량을 상수로 못 박으면 그 상수가 곧 다음 플래키가 된다. 대신 **모서리**를
            //   기준으로 잰다 — 창은 이 픽스처에서 움직이지 않으므로 이쪽이 흔들리지 않는 자다.
            float requiredRootInside = _clonedConfig.ledgeHangCornerClearancePoints / ppu;
            Assert.GreaterOrEqual(edgeX - bodyX, requiredRootInside,
                $"{LogPrefix} 매달린 루트가 모서리에서 {((edgeX - bodyX) * ppu):F2}pt 안쪽뿐입니다 — " +
                $"곡률 여유 {_clonedConfig.ledgeHangCornerClearancePoints:F2}pt 이상이어야 합니다. " +
                $"(참고: 서 있던 X={standingX:F4}, 매달린 X={bodyX:F4}, 모서리 X={edgeX:F4})");
        }

    }

    /// <summary>
    /// ★ <see cref="LedgeHangState.ResolveEdgeInsetWorld"/>의 <b>상·하한 계약</b>만 잰다 — 씬도 설정도
    /// 카메라도 필요 없는 순수 함수라 여기서 직접 부른다.
    ///
    /// <para>위 <see cref="LedgeHangHandAlignmentTests"/>와 <b>클래스를 나눈 이유</b>: 그쪽 [TearDown]이
    /// 전역 <c>ScreenCoordinateConverter.OverlayOriginOsScreen</c>을 되돌리는데, 씬을 안 띄운 테스트가
    /// 그 TearDown을 타면 저장한 적 없는 값(0,0)으로 전역을 덮어 <b>다음 테스트를 오염시킨다.</b></para>
    ///
    /// <para>★ 프로덕션 상수를 숫자로 베끼지 않는다 — 임의의 입력을 넣고 <b>관계</b>만 단언하므로
    /// 기본값이 바뀌어도 이 파일은 손댈 필요가 없고, 계약이 깨지면 반드시 빨개진다.</para>
    /// </summary>
    public sealed class LedgeHangEdgeInsetContractTests
    {
        private const string LogPrefix = "[LEDGEHANG-INSET]";

        [Test]
        public void 인셋은_곡률과_손_여유의_합_아래로는_절대_내려가지_않는다()
        {
            const float corner = 0.30f, hand = 0.25f;
            // 서 있던 자리가 모서리에 딱 붙어 있어도(인셋 0), 심지어 바깥(음수)이어도 하한이 이긴다.
            foreach (float standing in new[] { -1f, 0f, 0.1f, float.NaN })
            {
                float inset = LedgeHangState.ResolveEdgeInsetWorld(corner, hand, standing,
                    probeReachWorld: 2f, footholdWidthWorld: 100f, maxInsetWidthFraction: 0.25f);
                Assert.AreEqual(corner + hand, inset, 1e-5f,
                    $"{LogPrefix} 서 있던 인셋 {standing}에서 하한이 지켜지지 않았습니다 — 실제 {inset:F5}.");
            }
        }

        [Test]
        public void 인셋은_발판_폭의_설정_비율을_절대_넘지_않는다()
        {
            const float width = 1.2f, fraction = 0.25f;
            // 서 있던 자리가 창 한가운데(폭의 절반)여도, 도달거리가 아무리 커도 폭 비율에서 잘린다.
            float inset = LedgeHangState.ResolveEdgeInsetWorld(0.3f, 0.25f, standingInsetWorld: width * 0.5f,
                probeReachWorld: 99f, footholdWidthWorld: width, maxInsetWidthFraction: fraction);
            Assert.LessOrEqual(inset, width * fraction + 1e-5f,
                $"{LogPrefix} 인셋 {inset:F5}가 폭 상한 {width * fraction:F5}을 넘었습니다 — " +
                "창 한가운데에서 매달리는 그림이 가능해졌습니다.");

            // 비율을 0.5 초과로 넘겨도 내부에서 0.5(정확히 한가운데)로 잘려야 한다.
            float overshoot = LedgeHangState.ResolveEdgeInsetWorld(0.3f, 0.25f, standingInsetWorld: 99f,
                probeReachWorld: 99f, footholdWidthWorld: width, maxInsetWidthFraction: 9f);
            Assert.LessOrEqual(overshoot, width * 0.5f + 1e-5f,
                $"{LogPrefix} 비율 9를 넘겼더니 인셋이 {overshoot:F5}가 됐습니다 — 0.5 절단이 없습니다.");
        }

        [Test]
        public void 그_사이에서는_서_있던_자리를_그대로_쓴다()
        {
            const float corner = 0.20f, hand = 0.15f, standing = 0.55f;
            float inset = LedgeHangState.ResolveEdgeInsetWorld(corner, hand, standing,
                probeReachWorld: 0.9f, footholdWidthWorld: 100f, maxInsetWidthFraction: 0.25f);
            Assert.AreEqual(standing, inset, 1e-5f,
                $"{LogPrefix} 하한({corner + hand:F3})과 상한(0.9) 사이인데 서 있던 자리 {standing:F3}를 " +
                $"쓰지 않고 {inset:F5}로 옮겼습니다 — '적당히 끝쪽'이 아니라 또 다른 강제 스냅입니다.");
        }

        [Test]
        public void 폭을_모르면_폭_절단을_건너뛴다()
        {
            // 폭 조회 실패(0/NaN)에서 폭 절단이 그대로 걸리면 인셋이 0이 되어 다시 모서리 점에 매달린다.
            foreach (float width in new[] { 0f, -1f, float.NaN })
            {
                float inset = LedgeHangState.ResolveEdgeInsetWorld(0.30f, 0.25f, standingInsetWorld: 0.40f,
                    probeReachWorld: 0.9f, footholdWidthWorld: width, maxInsetWidthFraction: 0.25f);
                Assert.AreEqual(0.55f, inset, 1e-5f,
                    $"{LogPrefix} 폭={width}에서 인셋이 {inset:F5}가 됐습니다 — 폭을 모를 때는 자르지 않아야 합니다.");
            }
        }
    }
}
