using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-08-31 사용자 신고 — <b>"캐릭터의 사이즈를 키우면 캐릭터 창에서 캐릭터가 사이즈가 벗어남.
    /// 캐릭터창에서 캐릭터는 사이즈나, 마우스로 잡았을때나 변함없이 그냥 보여져야함. 아이템 착용만 적용되서"</b>
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나 — <b>액자와 그림의 기준 키가 갈라져 있었다</b>
    /// ============================================================================
    /// 촬영장의 모든 치수(관절 좌표 / 획 두께 / 액세서리 / 액자)는 <b>캐릭터 실측 전신 높이</b>에
    /// 비례한다. 둘이 같은 키를 쓰면 배율이 얼마든 액자 속 그림 크기는 일정하다 — 원래 설계가 그랬다.
    /// 그런데 액자(카메라 <c>orthographicSize</c>/중심)는 <c>BuildCamera()</c>에서 <b>앱 시작 때 한 번</b>
    /// 계산됐고, 그림은 <c>Rebuild()</c>가 돌 때마다 <b>그 순간의 키</b>로 다시 그려졌다.
    /// 크기 다이얼(2026-08-31)이 <c>metrics.Remeasure()</c>로 키를 바꾸기 시작하면서 둘이 갈라졌다.
    ///
    /// <code>
    ///   배율 0.75로 시작 → 액자는 0.75 기준으로 굳는다
    ///   다이얼로 1.50     → 키만 2배(아직 안 굽었으므로 화면은 멀쩡)
    ///   캐릭터를 <b>마우스로 잡는다</b> → 포즈 Standing→Busy → Rebuild()
    ///                      → 그림만 2배, 액자는 그대로 → <b>액자 밖으로 튀어나감</b>
    /// </code>
    ///
    /// 사용자가 "사이즈나 <b>마우스로 잡았을 때나</b>"를 한 문장에 넣은 이유가 이것이다 —
    /// 두 개의 버그가 아니라 <b>크기 변경이 심어 둔 어긋남이 잡는 순간 터진 것</b>이다.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    ///  ① 배율 0.35 / 0.75 / 1.50(설정 전 구간)에서 <b>액자 대비 그림 크기가 같다</b>(표시 크기 불변).
    ///  ② 세 배율 모두에서 그려진 <b>모든 선</b>이 액자 안에 들어온다(사용자가 본 "벗어남"의 직접 잠금).
    ///  ③ 배율을 키운 뒤 <b>붙잡아도</b> ①②가 그대로다(신고 문장의 두 번째 절).
    ///  ④ (네거티브 컨트롤) 액자만 옛 값으로 되돌리면 ②가 <b>실제로 깨진다</b> —
    ///     이 테스트가 이 버그를 진짜로 잡는다는 증명이다.
    ///
    /// ============================================================================
    /// 측정 방식 — 프로덕션 공식을 베끼지 않는다
    /// ============================================================================
    /// 키나 배율을 다시 계산하지 않고 <b>실제로 그려진 LineRenderer 꼭짓점</b>(획 두께의 바깥쪽까지)과
    /// <b>카메라가 실제로 보고 있는 사각형</b>만 읽는다. 그리고 둘의 비(比)를 본다 — 그래서 프로덕션이
    /// 어떤 방식으로 크기를 맞추든 "액자 안에서 같은 크기로 보이는가"만 판정한다.
    /// </summary>
    public sealed class PortraitScaleInvarianceTests
    {
        private const string LogPrefix = "[초상화크기불변-TEST]";

        /// <summary>액자 대비 그림 높이의 허용 편차. 배율이 4.29배(0.35→1.50) 벌어지는 조건에서 0.5%면
        /// "따라온다"와 "안 따라온다"를 확실히 가른다(안 따라오면 비가 4.29배로 튄다).</summary>
        private const float RatioTolerance = 0.005f;

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
        private IMovementIntentSource _originalIntent;
        private float _restoreScale;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_agent != null)
            {
                if (_restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
                if (_agent.Blackboard != null)
                {
                    StickmanBlackboard bb = _agent.Blackboard;
                    if (bb.Machine != null && bb.Machine.CurrentStateId != StickmanStateId.Idle)
                    {
                        bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                    }
                    if (_originalIntent != null) bb.IntentSource = _originalIntent;
                }
            }
            _agent = null;
            _originalIntent = null;
            _restoreScale = 0f;

            if (_window != null) _window.Close("테스트 정리");
            _window = null;
            yield return null;
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            return found[0];
        }

        /// <summary>880 정보창이 쓰는 <b>주 촬영장</b>(구석 호버 패널의 2번 촬영장과 구분한다).</summary>
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
                $"{LogPrefix} X={CharacterPortraitStage.StageWorldX:F0}에 선 촬영장이 {atPrimaryX}개입니다 — 1개여야 합니다.");
            return primary;
        }

        private IEnumerator SetUpOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(_agent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");

            _originalIntent = _agent.Blackboard.IntentSource;
            _agent.Blackboard.IntentSource = new StillIntentSource();
            _restoreScale = _agent.CurrentCharacterScale;
            Assert.Greater(_restoreScale, 0f, $"{LogPrefix} 현재 배율을 읽지 못했습니다.");

            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        // ==================== 측정 도구 ====================

        private static Camera StageCamera(CharacterPortraitStage stage)
        {
            Camera cam = stage.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(cam, $"{LogPrefix} 촬영장 카메라가 없습니다.");
            Assert.IsTrue(cam.orthographic, $"{LogPrefix} 초상화 카메라가 직교가 아닙니다.");
            Assert.Greater(cam.aspect, 0.01f, $"{LogPrefix} 카메라 종횡비가 비정상입니다({cam.aspect}).");
            return cam;
        }

        /// <summary>카메라가 실제로 보고 있는 사각형(촬영장 로컬 좌표).</summary>
        private static Rect VisibleRect(CharacterPortraitStage stage)
        {
            Camera cam = StageCamera(stage);
            Vector3 c = cam.transform.localPosition;
            float halfY = cam.orthographicSize;
            float halfX = halfY * cam.aspect;
            return new Rect(c.x - halfX, c.y - halfY, halfX * 2f, halfY * 2f);
        }

        /// <summary>그려진 모든 선의 획 바깥쪽까지 포함한 사각형(촬영장 로컬 좌표).</summary>
        private static Rect MeasureInk(CharacterPortraitStage stage)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");

            var lines = figure.GetComponentsInChildren<LineRenderer>(true);
            Assert.Greater(lines.Length, 4, $"{LogPrefix} 그려진 선이 너무 적습니다({lines.Length}개) — 그림이 없습니다.");

            float scale = Mathf.Abs(figure.lossyScale.x);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                float pad = Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f * scale;
                for (int p = 0; p < lr.positionCount; p++)
                {
                    Vector3 local = stage.transform.InverseTransformPoint(lr.transform.TransformPoint(lr.GetPosition(p)));
                    min = Vector2.Min(min, new Vector2(local.x - pad, local.y - pad));
                    max = Vector2.Max(max, new Vector2(local.x + pad, local.y + pad));
                }
            }
            return new Rect(min, max - min);
        }

        /// <summary>액자 대비 그림 크기(세로/가로 비). <b>이 값이 배율 불변이어야 한다</b> —
        /// 그것이 곧 "표시 크기가 그대로"라는 사용자 요구의 정의다.</summary>
        private static Vector2 InkFillRatio(CharacterPortraitStage stage)
        {
            Rect ink = MeasureInk(stage);
            Rect view = VisibleRect(stage);
            return new Vector2(ink.width / view.width, ink.height / view.height);
        }

        private static void AssertInkInsideFrame(CharacterPortraitStage stage, string what)
        {
            Rect ink = MeasureInk(stage);
            Rect view = VisibleRect(stage);
            float left = ink.xMin - view.xMin, right = view.xMax - ink.xMax;
            float bottom = ink.yMin - view.yMin, top = view.yMax - ink.yMax;

            Debug.Log($"{LogPrefix} {what} — 액자 x[{view.xMin:F3},{view.xMax:F3}] y[{view.yMin:F3},{view.yMax:F3}], " +
                $"그림 x[{ink.xMin:F3},{ink.xMax:F3}] y[{ink.yMin:F3},{ink.yMax:F3}], " +
                $"여백 좌={left:F3} 우={right:F3} 하={bottom:F3} 상={top:F3}.");

            Assert.GreaterOrEqual(left, 0f, $"{LogPrefix} {what}: 그림이 액자 왼쪽으로 {-left:F3}유닛 벗어났습니다.");
            Assert.GreaterOrEqual(right, 0f, $"{LogPrefix} {what}: 그림이 액자 오른쪽으로 {-right:F3}유닛 벗어났습니다.");
            Assert.GreaterOrEqual(bottom, 0f, $"{LogPrefix} {what}: 그림이 액자 아래로 {-bottom:F3}유닛 벗어났습니다.");
            Assert.GreaterOrEqual(top, 0f, $"{LogPrefix} {what}: 그림이 액자 위로 {-top:F3}유닛 벗어났습니다.");
        }

        /// <summary>배율을 적용하고 촬영장이 그 값으로 다시 구워질 때까지 기다린다.</summary>
        private IEnumerator ApplyScaleAndSettle(float scale)
        {
            _agent.ApplyCharacterScale(scale, "테스트");
            yield return null;
            yield return null;   // Update() 한 번이면 서명이 바뀌어 Rebuild가 돈다(키가 서명에 들어 있다).
            Assert.AreEqual(scale, _agent.CurrentCharacterScale, 0.01f,
                $"{LogPrefix} 배율 {scale:F2}가 실제로 적용되지 않았습니다.");
        }

        // ============================================================================
        // (1) 핵심 — 배율을 바꿔도 액자 속 표시 크기가 변하지 않고, 절대 벗어나지 않는다
        // ============================================================================
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PortraitDisplaySizeIsIndependentOfCharacterScale()
        {
            yield return SetUpOpenWindow();
            var stage = PrimaryStage();
            stage.SetPose(PortraitPose.Standing);
            yield return null;

            float[] scales = { StickConfig.MinCharacterScale, 0.75f, StickConfig.MaxCharacterScale };
            Vector2 first = Vector2.zero;

            for (int i = 0; i < scales.Length; i++)
            {
                yield return ApplyScaleAndSettle(scales[i]);

                Vector2 ratio = InkFillRatio(stage);
                Debug.Log($"{LogPrefix} 배율 {scales[i]:F2}× — 액자 대비 그림 채움비 = " +
                    $"가로 {ratio.x:F4} / 세로 {ratio.y:F4} (카메라 직교크기 {StageCamera(stage).orthographicSize:F4}).");

                AssertInkInsideFrame(stage, $"배율 {scales[i]:F2}×");

                if (i == 0) { first = ratio; continue; }
                Assert.AreEqual(first.y, ratio.y, RatioTolerance,
                    $"{LogPrefix} 배율 {scales[0]:F2}×에서 {scales[i]:F2}×로 바뀌자 액자 대비 그림 <세로> 크기가 " +
                    $"{first.y:F4} → {ratio.y:F4}로 달라졌습니다 — 초상화는 캐릭터 크기 설정과 무관해야 합니다.");
                Assert.AreEqual(first.x, ratio.x, RatioTolerance,
                    $"{LogPrefix} 배율 {scales[0]:F2}×에서 {scales[i]:F2}×로 바뀌자 액자 대비 그림 <가로> 크기가 " +
                    $"{first.x:F4} → {ratio.x:F4}로 달라졌습니다.");
            }
        }

        // ============================================================================
        // (2) 신고 문장 재현 — 키운 뒤 <b>마우스로 잡아도</b> 그대로여야 한다
        // ============================================================================
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GrabbingAfterEnlargingDoesNotChangeThePortraitSize()
        {
            yield return SetUpOpenWindow();
            var stage = PrimaryStage();

            // 붙잡힘이 실제로 받는 포즈를 <b>매핑에서</b> 읽어 온다 — 여기에 Busy를 손으로 적으면
            // 나중에 매핑이 바뀌었을 때 테스트가 조용히 다른 것을 검사하게 된다.
            PortraitPose grabbedPose = CharacterPortraitStage.PoseForState(StickmanStateId.Dragged);
            Assert.AreNotEqual(PortraitPose.Hidden, grabbedPose,
                $"{LogPrefix} 붙잡힘의 포즈가 Hidden입니다 — 잴 그림이 없습니다(매핑 전제가 바뀌었습니다).");

            // ★ 상태 머신을 Dragged로 강제하지 않고 포즈만 직접 넣는다. 실제 붙잡기는 스크립트 커서
            //   장착이 전제라(PortraitDragIndependenceTests 참고) 여기서 흉내 내면 그 배선이 이 테스트의
            //   플레이키 요인이 된다. 이 테스트가 볼 것은 "붙잡힘이 만들어 내는 <b>다시 굽기</b>가
            //   배율을 새어 들여보내는가"이고, 그 경로는 포즈 전환 = Rebuild()로 완전히 같다.
            stage.SetPose(grabbedPose);
            yield return null;
            yield return null;
            Vector2 before = InkFillRatio(stage);

            // 사용자의 실제 순서: 크기를 키운다 → 그 다음 캐릭터를 잡는다(= 다시 굽는다).
            yield return ApplyScaleAndSettle(StickConfig.MaxCharacterScale);
            stage.SetPose(PortraitPose.Standing);
            yield return null;
            stage.SetPose(grabbedPose);
            yield return null;
            yield return null;

            Vector2 after = InkFillRatio(stage);
            Debug.Log($"{LogPrefix} 잡은 상태({grabbedPose}) 채움비 — 배율 {_restoreScale:F2}×에서 " +
                $"가로 {before.x:F4}/세로 {before.y:F4} → 배율 {StickConfig.MaxCharacterScale:F2}×에서 " +
                $"가로 {after.x:F4}/세로 {after.y:F4}.");

            AssertInkInsideFrame(stage, $"배율 {StickConfig.MaxCharacterScale:F2}× + 붙잡힘");

            Assert.AreEqual(before.y, after.y, RatioTolerance,
                $"{LogPrefix} 크기를 키운 뒤 붙잡자 액자 대비 그림 세로 크기가 {before.y:F4} → {after.y:F4}로 " +
                "달라졌습니다 — 사용자 신고 그대로의 증상입니다.");
            Assert.AreEqual(before.x, after.x, RatioTolerance,
                $"{LogPrefix} 같은 조건에서 가로 크기가 {before.x:F4} → {after.x:F4}로 달라졌습니다.");
        }

        // ============================================================================
        // (3) 네거티브 컨트롤 — 액자만 옛 값으로 되돌리면 실제로 벗어난다
        // ============================================================================
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FreezingTheFrameAtStartupHeightActuallyBreaksIt()
        {
            yield return SetUpOpenWindow();
            var stage = PrimaryStage();
            stage.SetPose(PortraitPose.Standing);
            yield return null;

            yield return ApplyScaleAndSettle(StickConfig.MinCharacterScale);
            Camera cam = StageCamera(stage);
            float frozenSize = cam.orthographicSize;
            Vector3 frozenPos = cam.transform.localPosition;

            yield return ApplyScaleAndSettle(StickConfig.MaxCharacterScale);

            // 옛 코드(BuildCamera에서 한 번만 계산)를 그대로 재현한다 — 그림은 새 키, 액자는 옛 키.
            cam.orthographicSize = frozenSize;
            cam.transform.localPosition = frozenPos;

            Rect ink = MeasureInk(stage);
            Rect view = VisibleRect(stage);
            bool escaped = ink.xMin < view.xMin || ink.xMax > view.xMax
                        || ink.yMin < view.yMin || ink.yMax > view.yMax;

            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 액자 고정 {frozenSize:F4} vs 그림 세로 {ink.height:F4} — " +
                $"벗어남={escaped}.");

            Assert.IsTrue(escaped,
                $"{LogPrefix} 네거티브 컨트롤이 통과해 버렸습니다 — 액자를 옛 값으로 얼렸는데도 그림이 " +
                "액자 안에 남아 있습니다. 측정 방식(그림/액자 사각형)을 먼저 의심해야 합니다.");

            // 프로덕션 경로로 되돌리면 다시 들어와야 한다(수정이 실제로 원인을 고쳤다는 증명).
            stage.SetPose(PortraitPose.Busy);
            stage.SetPose(PortraitPose.Standing);
            yield return null;
            yield return null;
            AssertInkInsideFrame(stage, "액자를 프로덕션 경로로 되돌린 뒤");
        }
    }
}
