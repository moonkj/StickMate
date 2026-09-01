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
    /// ★ FX/PET 신규 4종(물방울·나뭇잎·풍선·달팽이)의 <b>정보창 초상화 미리보기</b>가 실제로
    /// 그려진다 — 2026-09-01.
    ///
    /// ============================================================================
    /// 이 파일이 잡는 실패 — 같은 구멍의 <b>두 번째 입구</b>
    /// ============================================================================
    /// 직전 라운드가 신규 4종의 <b>실제 캐릭터 연출</b>을 채우고
    /// <see cref="AppearanceNewItemsRenderTests"/>로 잠갔다. 그런데
    /// <c>CharacterPortraitStage.DrawFxPreview</c>/<c>DrawPetPreview</c>의 <c>switch</c>에는
    /// 그 4종의 <c>case</c>가 없어서, <b>착용하고 정보창을 열면 액자가 비었다</b>.
    ///
    /// 이 저장소의 확정 규칙은 <b>"착용했는데 화면이 그대로면 그건 착용이 아니다"</b>이고,
    /// 정보창 액자는 사용자가 아이템을 고르는 <b>바로 그 화면</b>이다. 즉 화면 속 캐릭터에서만
    /// 보이고 액자에서는 안 보이는 상태는 규칙 위반이 절반만 고쳐진 것이었다.
    /// 이 파일은 그 절반을 잠근다 — <c>switch</c>는 <b>케이스를 빠뜨려도 컴파일이 통과</b>하므로
    /// 사람 리뷰로는 반복해서 놓친다(이 프로젝트가 이미 여러 번 겪었다).
    ///
    /// ============================================================================
    /// 무엇을 재는가 — 플래그가 아니라 <b>실제로 그려진 선</b>
    /// ============================================================================
    /// "착용됐다"는 모델 상태를 믿지 않는다. 촬영장의 <c>MiniFigure</c> 밑을 훑어
    ///   ① 그 아이템의 도형 이름이 실제로 있고, ② 점이 2개 이상이며,
    ///   ③ 점들이 만드는 사각형이 0이 아니고(한 점에 뭉친 껍데기가 아니고),
    ///   ④ 알파가 실제로 0을 넘고 렌더러가 켜져 있으며,
    ///   ⑤ 획 바깥쪽까지 포함해 <b>액자 가시 사각형 안</b>에 들어오는지
    /// 를 전부 본다. ⑤가 없으면 "그리기는 했는데 액자 밖이라 안 보인다"가 초록으로 통과한다
    /// (이 촬영장은 실제로 2026-08-31에 그 사고를 낸 적이 있다 — PortraitScaleInvarianceTests).
    ///
    /// <b>공허한 통과 방지</b>: 같은 프레임에 몸(<c>Head</c>/<c>HeadFill</c>/<c>Torso</c>)이 실제로
    /// 그려져 있는지도 함께 확인한다. 그림 자체가 없으면 위 검사는 아무것도 증명하지 못한다.
    ///
    /// <b>네거티브 컨트롤</b>: FX "없음" + 펫 미착용에서는 같은 이름의 도형이 <b>하나도</b> 없어야
    /// 한다 — 위 검사가 아이템과 무관하게 아무거나 세고 있는 것이 아님을 같은 파일에서 증명한다.
    ///
    /// <b>획 두께</b>: 신규 4종의 획이 <b>기존 미리보기와 같은 두께</b>인지도 잰다. 미리보기가
    /// 자기만의 획 상수를 새로 들이면 그것이 곧 이 저장소가 반복해서 겪은 이중 정의다
    /// (Tasklist 38-12 #10과 같은 뿌리). 몸 획 결함 자체는 P6 소관이라 여기서 건드리지 않고,
    /// <b>미리보기 9종이 한 벌로 움직이는지</b>만 잠근다.
    /// </summary>
    public sealed class PortraitNewItemPreviewTests
    {
        private const string LogPrefix = "[초상화신규외형-TEST]";

        // 카테고리 안의 자리(Interaction/AppearanceShapeBuilder.cs의 같은 이름 상수와 같은 값).
        // 상수를 다시 적는 이 저장소의 관례를 따른다 — 어긋나면 착용 단언이 즉시 빨개진다.
        private const int FxNone = 0;
        private const int FxSparkle = 2;
        private const int FxBubble = 4;
        private const int FxLeaf = 5;
        private const int PetBall = 0;
        private const int PetBalloon = 4;
        private const int PetSnail = 5;

        /// <summary>신규 4종 중 가장 높은 요구 레벨(달팽이 Lv.30).</summary>
        private const int TopRequiredLevel = 30;

        // 도형 이름 = CharacterPortraitStage의 미리보기 case가 만드는 이름 그대로.
        private static readonly string[] BubbleShapes = { "FxBubbleA", "FxBubbleB" };
        private static readonly string[] LeafShapes =
            { "FxLeafABlade", "FxLeafAStem", "FxLeafBBlade", "FxLeafBStem" };
        private static readonly string[] BalloonShapes = { "PetBalloonString", "PetBalloonBody" };
        private static readonly string[] SnailShapes = { "PetSnailFoot", "PetSnailShell", "PetSnailCore" };

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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_agent != null && _agent.Blackboard != null && _originalIntent != null)
            {
                _agent.Blackboard.IntentSource = _originalIntent;
            }
            _agent = null;
            _originalIntent = null;

            if (_window != null) _window.Close("테스트 정리");
            _window = null;

            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // FX — 물방울 / 나뭇잎
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 물방울을_걸치면_액자에_방울이_그려진다()
        {
            yield return SetUpOpenWindow();
            yield return AssertPreviewDrawn(EquipmentSlot.Fx, FxBubble, "물방울", BubbleShapes);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 나뭇잎을_걸치면_액자에_잎이_그려진다()
        {
            yield return SetUpOpenWindow();
            yield return AssertPreviewDrawn(EquipmentSlot.Fx, FxLeaf, "나뭇잎", LeafShapes);
        }

        // ============================================================================
        // PET — 풍선 / 달팽이
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 풍선을_걸치면_액자에_끈과_주머니가_그려진다()
        {
            yield return SetUpOpenWindow();
            yield return AssertPreviewDrawn(EquipmentSlot.Pet, PetBalloon, "풍선", BalloonShapes);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 달팽이를_걸치면_액자에_발과_껍데기가_그려진다()
        {
            yield return SetUpOpenWindow();
            yield return AssertPreviewDrawn(EquipmentSlot.Pet, PetSnail, "달팽이", SnailShapes);
        }

        // ============================================================================
        // 네거티브 컨트롤 — 안 걸치면 하나도 없다
        // ============================================================================

        /// <summary>위 네 테스트가 "아이템과 무관하게 아무거나 세고 있는 것"이 아님을 증명한다.
        /// FX "없음"(0번)과 펫 미착용에서는 신규 4종의 도형 이름이 <b>하나도</b> 없어야 한다.</summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 안_걸치면_신규_4종_미리보기가_하나도_없다()
        {
            yield return SetUpOpenWindow();
            CharacterPortraitStage stage = PrimaryStage();

            Wear(EquipmentSlot.Fx, FxNone);
            Wear(EquipmentSlot.Pet, EquipmentModel.NotWorn);
            yield return null;
            yield return null;   // 장비 변경 -> 서명 변경 -> Rebuild가 도는 데 필요한 프레임.

            AssertBodyIsActuallyDrawn(stage, "미착용 대조군");

            AssertNoneDrawn(stage, "물방울", BubbleShapes);
            AssertNoneDrawn(stage, "나뭇잎", LeafShapes);
            AssertNoneDrawn(stage, "풍선", BalloonShapes);
            AssertNoneDrawn(stage, "달팽이", SnailShapes);
        }

        // ============================================================================
        // 획 두께 — 신규 4종이 자기만의 상수를 새로 들이지 않았는가
        // ============================================================================

        /// <summary>
        /// 신규 4종의 미리보기 획이 <b>기존 미리보기(반짝임 / 공)와 같은 두께</b>다.
        ///
        /// <para>이 단언의 뜻은 "두께가 0.048이다"가 아니라 <b>"미리보기 9종이 한 벌로 움직인다"</b>이다.
        /// 신규 케이스가 자기 획 상수를 따로 들이면 이 저장소가 반복해서 겪은 이중 정의가 되고
        /// (Tasklist 38-12 #10과 같은 뿌리), 나중에 초상화 획을 고치는 사람이 <b>4종만 옛 두께로</b>
        /// 남기게 된다. 값을 못박지 않았으므로 훗날 초상화 획이 바뀌어도 이 테스트는 그대로 유효하다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 신규_4종_미리보기의_획이_기존_미리보기와_같다()
        {
            yield return SetUpOpenWindow();
            CharacterPortraitStage stage = PrimaryStage();

            float sparkle = 0f, bubble = 0f, leaf = 0f, ball = 0f, balloon = 0f, snail = 0f;

            yield return WearAndSettle(EquipmentSlot.Fx, FxSparkle, "반짝임");
            sparkle = StrokeOf(stage, "FxSparkleA0");

            yield return WearAndSettle(EquipmentSlot.Fx, FxBubble, "물방울");
            bubble = StrokeOf(stage, "FxBubbleA");

            yield return WearAndSettle(EquipmentSlot.Fx, FxLeaf, "나뭇잎");
            leaf = StrokeOf(stage, "FxLeafABlade");

            yield return WearAndSettle(EquipmentSlot.Pet, PetBall, "공");
            ball = StrokeOf(stage, "PetBallRing");

            yield return WearAndSettle(EquipmentSlot.Pet, PetBalloon, "풍선");
            balloon = StrokeOf(stage, "PetBalloonBody");

            yield return WearAndSettle(EquipmentSlot.Pet, PetSnail, "달팽이");
            snail = StrokeOf(stage, "PetSnailShell");

            Debug.Log($"{LogPrefix} 미리보기 획 — 반짝임 {sparkle:F5} / 물방울 {bubble:F5} / " +
                $"나뭇잎 {leaf:F5} / 공 {ball:F5} / 풍선 {balloon:F5} / 달팽이 {snail:F5}.");

            Assert.Greater(sparkle, 0f, $"{LogPrefix} 기준이 될 기존 미리보기(반짝임) 획을 읽지 못했습니다.");
            float tolerance = sparkle * 0.001f;
            Assert.AreEqual(sparkle, bubble, tolerance,
                $"{LogPrefix} 물방울 미리보기 획({bubble:F5})이 기존 미리보기({sparkle:F5})와 다릅니다 — " +
                "미리보기가 자기 획 상수를 새로 들였습니다(이중 정의).");
            Assert.AreEqual(sparkle, leaf, tolerance,
                $"{LogPrefix} 나뭇잎 미리보기 획({leaf:F5})이 기존 미리보기({sparkle:F5})와 다릅니다.");
            Assert.AreEqual(ball, balloon, ball * 0.001f,
                $"{LogPrefix} 풍선 미리보기 획({balloon:F5})이 기존 펫 미리보기({ball:F5})와 다릅니다.");
            Assert.AreEqual(ball, snail, ball * 0.001f,
                $"{LogPrefix} 달팽이 미리보기 획({snail:F5})이 기존 펫 미리보기({ball:F5})와 다릅니다.");
        }

        // ============================================================================
        // 공용 단언
        // ============================================================================

        private IEnumerator AssertPreviewDrawn(EquipmentSlot slot, int itemIndex, string label,
            string[] shapeNames)
        {
            CharacterPortraitStage stage = PrimaryStage();
            yield return WearAndSettle(slot, itemIndex, label);

            // 공허한 통과 방지 ① — 그림 자체가 그려져 있는가.
            AssertBodyIsActuallyDrawn(stage, label);

            // 공허한 통과 방지 ② — 미리보기를 아예 안 그리는 포즈가 아닌가.
            Assert.IsTrue(stage.Pose == PortraitPose.Standing || stage.Pose == PortraitPose.Busy,
                $"{LogPrefix} 초상화 포즈가 {stage.Pose}입니다 — 넘어짐/가출 포즈는 미리보기를 애초에 " +
                "그리지 않으므로 이 회차는 아무것도 증명하지 못합니다(대조군 실패).");

            Rect view = VisibleRect(stage);
            for (int i = 0; i < shapeNames.Length; i++)
            {
                string name = shapeNames[i];
                LineRenderer lr = FindPart(stage, name);
                Assert.IsNotNull(lr,
                    $"{LogPrefix} {label}을(를) 걸쳤는데 초상화에 '{name}'이(가) 없습니다 — " +
                    "정보창을 열어도 액자가 그대로입니다(\"착용했는데 화면이 그대로면 그건 착용이 아니다\").");
                Assert.GreaterOrEqual(lr.positionCount, 2,
                    $"{LogPrefix} {label}의 '{name}'이(가) 점 {lr.positionCount}개입니다 — 선이 아닙니다.");
                Assert.Greater(Extent(lr), 0f,
                    $"{LogPrefix} {label}의 '{name}'이(가) 한 점에 뭉쳐 있습니다(크기 0).");
                Assert.Greater(lr.startColor.a, 0.5f,
                    $"{LogPrefix} {label}의 '{name}'이(가) 거의 투명합니다(알파 {lr.startColor.a:F2}).");
                Assert.IsTrue(lr.enabled,
                    $"{LogPrefix} {label}의 '{name}'이(가) 꺼져 있습니다.");

                Rect ink = MeasureInk(stage, lr);
                AssertInsideFrame(ink, view, label, name);
            }

            Debug.Log($"{LogPrefix} {label} — 도형 {shapeNames.Length}개가 액자 안에 그려졌습니다 " +
                $"(포즈 {stage.Pose}, 액자 x[{view.xMin:F3},{view.xMax:F3}] y[{view.yMin:F3},{view.yMax:F3}]).");
        }

        private static void AssertNoneDrawn(CharacterPortraitStage stage, string label, string[] shapeNames)
        {
            for (int i = 0; i < shapeNames.Length; i++)
            {
                Assert.IsNull(FindPart(stage, shapeNames[i]),
                    $"{LogPrefix} 안 걸쳤는데 {label}의 '{shapeNames[i]}'이(가) 초상화에 있습니다 — " +
                    "미리보기가 착용 상태와 무관하게 그려지고 있습니다.");
            }
        }

        /// <summary>공허한 통과 방지 — 미리보기를 재기 전에 <b>몸이 실제로 그려져 있는지</b> 확인한다.</summary>
        private static void AssertBodyIsActuallyDrawn(CharacterPortraitStage stage, string what)
        {
            Assert.IsNotNull(FindPart(stage, "Head"),
                $"{LogPrefix} {what} — 초상화에 머리 링(Head)이 없습니다. 그림 자체가 안 그려졌다면 " +
                "미리보기 검사는 아무것도 증명하지 못합니다(대조군 실패).");
            Assert.IsNotNull(FindPart(stage, "HeadFill"),
                $"{LogPrefix} {what} — 초상화에 머리 채움(HeadFill)이 없습니다(대조군 실패).");
            Assert.IsNotNull(FindPart(stage, "Torso"),
                $"{LogPrefix} {what} — 초상화에 몸통이 없습니다(대조군 실패).");
        }

        private static void AssertInsideFrame(Rect ink, Rect view, string label, string name)
        {
            float left = ink.xMin - view.xMin, right = view.xMax - ink.xMax;
            float bottom = ink.yMin - view.yMin, top = view.yMax - ink.yMax;

            Assert.GreaterOrEqual(left, 0f,
                $"{LogPrefix} {label}의 '{name}'이(가) 액자 왼쪽으로 {-left:F4}유닛 벗어났습니다 — " +
                "그렸지만 사용자에게는 보이지 않습니다.");
            Assert.GreaterOrEqual(right, 0f,
                $"{LogPrefix} {label}의 '{name}'이(가) 액자 오른쪽으로 {-right:F4}유닛 벗어났습니다.");
            Assert.GreaterOrEqual(bottom, 0f,
                $"{LogPrefix} {label}의 '{name}'이(가) 액자 아래로 {-bottom:F4}유닛 벗어났습니다.");
            Assert.GreaterOrEqual(top, 0f,
                $"{LogPrefix} {label}의 '{name}'이(가) 액자 위로 {-top:F4}유닛 벗어났습니다.");
        }

        // ============================================================================
        // 측정 도구 (PortraitScaleInvarianceTests와 같은 방식)
        // ============================================================================

        /// <summary>카메라가 실제로 보고 있는 사각형(촬영장 로컬 좌표).</summary>
        private static Rect VisibleRect(CharacterPortraitStage stage)
        {
            Camera cam = stage.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(cam, $"{LogPrefix} 촬영장 카메라가 없습니다.");
            Assert.IsTrue(cam.orthographic, $"{LogPrefix} 초상화 카메라가 직교가 아닙니다.");
            Assert.Greater(cam.aspect, 0.01f, $"{LogPrefix} 카메라 종횡비가 비정상입니다({cam.aspect}).");

            Vector3 c = cam.transform.localPosition;
            float halfY = cam.orthographicSize;
            float halfX = halfY * cam.aspect;
            return new Rect(c.x - halfX, c.y - halfY, halfX * 2f, halfY * 2f);
        }

        /// <summary>선 하나가 차지하는 사각형(획 <b>바깥쪽</b>까지, 촬영장 로컬 좌표).
        /// 숨쉬기로 미니 피규어가 위아래로 흔들리므로 좌표는 촬영장 기준으로 다시 환산한다.</summary>
        private static Rect MeasureInk(CharacterPortraitStage stage, LineRenderer lr)
        {
            float scale = Mathf.Abs(lr.transform.lossyScale.x);
            float pad = Mathf.Max(lr.startWidth, lr.endWidth) * 0.5f * scale;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int p = 0; p < lr.positionCount; p++)
            {
                Vector3 local = stage.transform.InverseTransformPoint(
                    lr.transform.TransformPoint(lr.GetPosition(p)));
                min = Vector2.Min(min, new Vector2(local.x - pad, local.y - pad));
                max = Vector2.Max(max, new Vector2(local.x + pad, local.y + pad));
            }
            return new Rect(min, max - min);
        }

        /// <summary>선이 만드는 사각형의 큰 변. 0이면 한 점에 뭉친 껍데기다.</summary>
        private static float Extent(LineRenderer lr)
        {
            if (lr == null || lr.positionCount < 2) return 0f;
            Vector3 min = lr.GetPosition(0), max = min;
            for (int i = 1; i < lr.positionCount; i++)
            {
                Vector3 p = lr.GetPosition(i);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 size = max - min;
            return Mathf.Max(size.x, size.y);
        }

        private static float StrokeOf(CharacterPortraitStage stage, string partName)
        {
            LineRenderer lr = FindPart(stage, partName);
            Assert.IsNotNull(lr, $"{LogPrefix} 획을 재려는 도형 '{partName}'이(가) 초상화에 없습니다.");
            return lr.startWidth;
        }

        /// <summary>미니 피규어에 그 이름의 선이 실제로 그려져 있는가(없으면 null).</summary>
        private static LineRenderer FindPart(CharacterPortraitStage stage, string partName)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            Transform part = figure.Find(partName);
            return part != null ? part.GetComponent<LineRenderer>() : null;
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

        // ============================================================================
        // 씬 준비
        // ============================================================================

        private IEnumerator SetUpOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var windows = Object.FindObjectsByType<CharacterInfoWindow>(FindObjectsSortMode.None);
            Assert.AreEqual(1, windows.Length,
                $"{LogPrefix} 씬의 CharacterInfoWindow가 {windows.Length}개입니다 — 1개여야 합니다.");
            _window = windows[0];

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(_agent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");

            _originalIntent = _agent.Blackboard.IntentSource;
            _agent.Blackboard.IntentSource = new StillIntentSource();

            EnsureOwned();
            ClearAllSlots();

            _window.Open("테스트");
            yield return null;
            yield return null;

            // 앱 시작 낙하가 남아 있으면 포즈가 잠시 흔들린다 — 액자가 서 있는 그림으로 안정될
            // 때까지 <b>벽시계</b>로 기다린다(배치 모드는 프레임이 밀리초라 프레임 수로는 못 잰다).
            CharacterPortraitStage stage = PrimaryStage();
            yield return TestClock.WaitUntil(
                () => stage.Pose == PortraitPose.Standing || stage.Pose == PortraitPose.Busy,
                10f, "초상화 포즈가 서 있는 그림(Standing/Busy)으로 안정되기");
        }

        /// <summary>착용 -> 서명 변경 -> Rebuild가 한 바퀴 돌 때까지. 재구성은 <c>Update</c>에서
        /// 도는 <b>구조적</b> 대기라 프레임 2개가 맞다(시간 기반 연출이 아니다).</summary>
        private IEnumerator WearAndSettle(EquipmentSlot slot, int itemIndex, string label)
        {
            Assert.IsTrue(Wear(slot, itemIndex),
                $"{LogPrefix} {label}을(를) 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다 " +
                $"(요구 레벨/보유 조건 확인, 지금 레벨 {CharacterProgressionModel.Level}).");
            yield return null;
            yield return null;
        }

        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        private static void ClearAllSlots()
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, null);
            }
        }

        /// <summary>신규 4종이 잠겨 있으면 레벨을 올려 연다. 지금은 디버그 전체 해제가 켜져 있어
        /// 보통 한 번도 돌지 않는다 — 그 스위치가 꺼지는 날에도 이 파일이 계속 유효하도록 남긴다.</summary>
        private void EnsureOwned()
        {
            if (EquipmentModel.IsItemOwned(EquipmentSlot.Pet, PetSnail)
                && EquipmentModel.IsItemOwned(EquipmentSlot.Fx, FxLeaf)) return;

            StickConfig config = _agent != null ? _agent.Config : null;
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < TopRequiredLevel; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, TopRequiredLevel,
                $"{LogPrefix} 레벨 {TopRequiredLevel}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
        }
    }
}
