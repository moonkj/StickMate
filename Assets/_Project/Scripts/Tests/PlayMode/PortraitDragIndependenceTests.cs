using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-08-30 사용자 신고 — "캐릭터를 잡으면 캐릭터창에서는 가만히 있어야 하는데 옆으로 이상하게 됨".
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    /// 초상화 미니 피규어는 <b>실제 캐릭터의 위치/회전/발버둥과 완전히 독립</b>이다. 초상화가 반영해도
    /// 되는 것은 (1) 상태 ID의 3버킷 매핑, (2) 장착 액세서리, (3) 잉크색 — 그 셋뿐이다.
    /// 그러므로 <b>붙잡혀 있는 동안 미니 피규어의 회전은 한 번도 변하지 않고, 값 자체도 고정값이어야</b> 한다.
    ///
    /// 이 테스트가 의미를 가지려면 "실제 캐릭터는 그 시간 동안 실제로 흔들리고 있어야" 한다 —
    /// 그래서 몸통 비틀림 폭을 함께 측정해 <b>대조군</b>으로 단언한다(캐릭터가 가만히 있어서 초상화도
    /// 가만히 있는 "무의미한 통과"를 구조적으로 막는다).
    ///
    /// 관측 방식: 촬영장 루트의 자식 "MiniFigure" 트랜스폼의 localRotation/localPosition을 매 프레임
    /// 기록한다(Interaction/CharacterPortraitStage.cs가 포즈를 적용하는 바로 그 트랜스폼).
    /// </summary>
    public sealed class PortraitDragIndependenceTests
    {
        private const string LogPrefix = "[초상화독립-TEST]";
        private const long FlatGroundHandle = 9411L;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly System.Collections.Generic.List<PlatformFoothold> Footholds
                = new System.Collections.Generic.List<PlatformFoothold>();
            public System.Collections.Generic.IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private CharacterInfoWindow _window;
        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private CursorPositionQuery _originalCursor;
        private Vector2 _savedOrigin;
        private Vector2 _cursorWorld;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
                StickmanBlackboard bb = _agent.Blackboard;
                if (bb.Machine != null && bb.Machine.CurrentStateId == StickmanStateId.Dragged)
                {
                    bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
                if (_originalConfig != null) bb.Config = _originalConfig;
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
                if (_originalPoller != null) bb.FootholdPoller = _originalPoller;
                bb.CursorProvider = _originalCursor;
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_window != null) _window.Close("테스트 정리");
            _window = null;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            yield return null;
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            return found[0];
        }

        /// <summary>
        /// 880 정보창이 쓰는 <b>주 촬영장</b> 하나만 고른다.
        ///
        /// <para>★ 2026-08-31 — 씬의 촬영장이 <b>둘</b>이 됐다. 화면 좌하단 구석 호버 패널이 자기
        /// 촬영장을 <see cref="CharacterPortraitStage.SecondaryStageWorldX"/>(10200)에 따로 세우기
        /// 때문이다(docs/UX_FLOW.md 34-6-3: 두 창이 동시에 열릴 수 있고 각자 다른 크기의 RT를 요구해서,
        /// 하나를 공유하면 RT가 서로를 밀어내며 매 프레임 재생성된다).</para>
        ///
        /// <para>그래서 "씬에 정확히 하나"라는 옛 단언은 더 이상 사실이 아니다. 다만 그 단언이 실제로
        /// 지키려던 위험은 <b>"한 자리에 두 촬영장이 겹치는 것"</b>(카메라 하나가 미니 피규어 둘을 함께
        /// 찍는 것)이었으므로, 같은 조건을 <b>X 좌표별</b>로 다시 세운다 — 오히려 더 정확한 단언이다.</para>
        /// </summary>
        private static CharacterPortraitStage PrimaryStage()
        {
            var found = Object.FindObjectsByType<CharacterPortraitStage>(FindObjectsSortMode.None);
            CharacterPortraitStage primary = null;
            int atPrimaryX = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                if (Mathf.Abs(found[i].transform.position.x - CharacterPortraitStage.StageWorldX) > 1f) continue;
                atPrimaryX++;
                primary = found[i];
            }
            Assert.AreEqual(1, atPrimaryX,
                $"X={CharacterPortraitStage.StageWorldX:F0}에 선 촬영장이 {atPrimaryX}개입니다 — 1개여야 합니다" +
                "(0개면 SceneBootstrapper 배치 누락, 2개 이상이면 카메라 하나가 미니 피규어 둘을 함께 찍습니다). " +
                $"씬 전체 촬영장 수 = {found.Length}(구석 호버 패널 몫 1개가 정상적으로 더 있습니다).");
            return primary;
        }

        private bool TryGetScriptedCursor(out Vector2 osScreenPosition)
        {
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null) { osScreenPosition = default; return false; }
            osScreenPosition = ScreenCoordinateConverter.WorldToOsScreen(cam, _cursorWorld, _clonedConfig, out _);
            return true;
        }

        /// <summary>씬 로드 → 창 열기 → 배회 고정 + 스크립트 커서 장착까지.</summary>
        private IEnumerator SetUpOpenWindowAndScriptedCursor()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");

            StickmanBlackboard bb = _agent.Blackboard;
            Assert.IsNotNull(bb, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");

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
            float groundWorldY = physicsGround.GetComponent<BoxCollider2D>().bounds.max.y;

            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(bb.MainCamera,
                new Vector2(0f, groundWorldY), _clonedConfig, out _);
            var service = new TestFootholdService();
            service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, Screen.width, Mathf.Max(1f, Screen.height - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(service, _clonedConfig);
            bb.IntentSource = new StillIntentSource();
            bb.CursorProvider = TryGetScriptedCursor;

            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        private static Transform FindMiniFigure(CharacterPortraitStage stage)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            return figure;
        }

        // ============================================================================
        // (1) 핵심 — 붙잡혀 흔들리는 동안 초상화는 1도도 움직이지 않는다
        // ============================================================================
        [UnityTest]
        public IEnumerator PortraitDoesNotFollowTheRealCharacterWhileDragged()
        {
            yield return SetUpOpenWindowAndScriptedCursor();

            var stage = PrimaryStage();
            Transform figure = FindMiniFigure(stage);
            StickmanBlackboard bb = _agent.Blackboard;

            // 붙잡기 — DragStruggleTests와 같은 방식(커서를 몸에 붙이고 상태를 강제 전이).
            Vector2 grabAt = bb.Body.position;
            _cursorWorld = grabAt;
            bb.CurrentFootholdHandle = 0L;
            bb.Machine.ChangeState(StickmanStateId.Dragged, isForcedInterrupt: true);
            yield return null;
            yield return null;   // 창의 TickPresenceLine이 새 상태를 보고 포즈를 밀어넣을 여유.

            float baseZ = Mathf.DeltaAngle(figure.localEulerAngles.z, 0f);
            Vector3 basePos = figure.localPosition;
            PortraitPose observedPose = stage.Pose;

            float maxRotDelta = 0f, maxPosDelta = 0f;
            float minTwist = float.MaxValue, maxTwist = float.MinValue;
            float bodyMinX = float.MaxValue, bodyMaxX = float.MinValue;

            float t = 0f;
            while (t < 1.6f)
            {
                yield return null;
                t += Time.deltaTime;
                // 커서를 좌우로 흔들어 실제 캐릭터를 실제로 끌고 다닌다.
                _cursorWorld = grabAt + new Vector2(Mathf.Sin(t * 8f) * 1.5f, 0f);

                Assert.AreEqual(StickmanStateId.Dragged, bb.Machine.CurrentStateId,
                    $"{LogPrefix} 관찰 중 Dragged가 풀렸습니다 — 관측 전제가 깨졌습니다.");

                float z = Mathf.DeltaAngle(figure.localEulerAngles.z, 0f);
                maxRotDelta = Mathf.Max(maxRotDelta, Mathf.Abs(Mathf.DeltaAngle(z, baseZ)));
                maxPosDelta = Mathf.Max(maxPosDelta, Vector3.Distance(figure.localPosition, basePos));

                float twist = Mathf.DeltaAngle(bb.Body.transform.eulerAngles.z, 0f);
                minTwist = Mathf.Min(minTwist, twist);
                maxTwist = Mathf.Max(maxTwist, twist);
                bodyMinX = Mathf.Min(bodyMinX, bb.Body.position.x);
                bodyMaxX = Mathf.Max(bodyMaxX, bb.Body.position.x);
            }

            float twistRange = maxTwist - minTwist;
            float travel = bodyMaxX - bodyMinX;
            Debug.Log($"{LogPrefix} 관찰 {t:F2}초 — 초상화 포즈={observedPose}, 초상화 회전z={baseZ:F2}도, " +
                $"초상화 위치={basePos}, 회전 변동 최대={maxRotDelta:F3}도, 위치 변동 최대={maxPosDelta:F4}유닛 / " +
                $"(대조군) 실제 몸통 비틀림 폭={twistRange:F1}도, 실제 가로 이동={travel:F2}유닛.");

            // 대조군 — 실제 캐릭터가 정말 흔들리고 끌려다녔는가(아니면 이 테스트는 아무것도 증명 못 한다).
            Assert.Greater(twistRange, 3f,
                $"{LogPrefix} 실제 캐릭터가 흔들리지 않았습니다(비틀림 폭 {twistRange:F1}도) — 대조군 실패.");
            Assert.Greater(travel, 1f,
                $"{LogPrefix} 실제 캐릭터가 끌려다니지 않았습니다(가로 이동 {travel:F2}유닛) — 대조군 실패.");

            // 절대 조건 — 초상화는 완전히 고정이다.
            Assert.AreEqual(0f, maxRotDelta, 0.001f,
                $"{LogPrefix} 붙잡혀 있는 동안 초상화가 회전했습니다({maxRotDelta:F3}도).");
            Assert.AreEqual(0f, maxPosDelta, 0.0001f,
                $"{LogPrefix} 붙잡혀 있는 동안 초상화가 움직였습니다({maxPosDelta:F4}유닛).");

            // 그리고 그 고정값은 "옆으로 누운 값"이 아니라 <b>똑바로 선 값</b>이어야 한다 —
            // 사용자 신고("옆으로 이상하게 됨")의 직접 잠금.
            Assert.AreEqual(0f, baseZ, 0.001f,
                $"{LogPrefix} 붙잡힌 동안 초상화가 {baseZ:F1}도 기울어 있습니다 — 액자 속 인물이 옆으로 누웠습니다.");
            Assert.AreEqual(0f, basePos.x, 0.0001f,
                $"{LogPrefix} 붙잡힌 동안 초상화가 가로로 {basePos.x:F3}유닛 밀려 있습니다 — 액자 중앙에서 벗어났습니다.");
        }

        // ============================================================================
        // (2) 매핑 잠금 — Dragged는 "뭔가 하는 중"(정지된 준비 자세)이다
        // ============================================================================
        [Test]
        public void DraggedMapsToBusySoThePortraitStaysUpright()
        {
            Assert.AreEqual(PortraitPose.Busy, CharacterPortraitStage.PoseForState(StickmanStateId.Dragged),
                "붙잡힌 상태가 Fallen으로 매핑되면 액자 속 인물이 옆으로 눕습니다(2026-08-30 사용자 신고). " +
                "붙잡힌 캐릭터는 넘어져 있는 것이 아니라 '붙잡혀 버둥거리는 중'이므로 서 있는 계열이어야 합니다.");
        }
    }
}
