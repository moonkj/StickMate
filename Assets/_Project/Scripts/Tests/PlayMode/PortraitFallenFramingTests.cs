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
    /// ★ 2026-08-30 — "넘어짐(Fallen) 초상화에서 머리가 액자 밖으로 잘린다" 결함의 회귀 잠금.
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나
    /// ============================================================================
    /// 옛 구현은 몸을 <b>발을 회전축으로</b> −78도 눕혔다. 발은 로컬 원점이라 회전해도 제자리인데
    /// 머리는 원점에서 키만큼 떨어져 있어(회전 반경 = 키) 눕히는 순간 머리만 액자 밖으로 쓸려나갔다.
    /// 랙돌 / 던져짐 / 일어나는 중 — 세 상태 모두 이 포즈를 쓰므로 캐릭터를 던지면 초상화가
    /// <b>머리 없는 그림</b>이 됐다(증거: Logs/evidence_20260830_portrait_drag/1_수정전_붙잡힘=Fallen.png).
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    ///  ① 넘어짐 포즈에서 <b>머리 원 전체(중심 + 반지름)</b>가 카메라 가시 사각형 안에 완전히 들어온다.
    ///  ② 그려진 <b>모든 선</b>이(획 굵기의 바깥쪽까지) 가시 사각형 안에 들어온다 — 머리만 맞추고
    ///     발이 튀어나가는 부분 최적화를 막는다.
    ///  ③ 랙돌 / 던져짐 / 일어나는 중 <b>세 상태 모두</b> 실제로 그 프레이밍을 받는다(매핑 + 기하 동시 검증).
    ///  ④ (네거티브 컨트롤) 옛 "발 회전축" 변환을 그대로 되돌려 놓으면 ①이 <b>실제로 깨진다</b> —
    ///     이 테스트가 이 버그를 진짜로 잡는다는 증명이다.
    ///
    /// ============================================================================
    /// 측정 방식 — 프로덕션 공식을 베끼지 않는다
    /// ============================================================================
    /// 머리 위치를 다시 계산하지 않고 <b>실제로 그려진 머리 링 LineRenderer의 꼭짓점</b>을 읽어
    /// 무게중심과 외접반경을 낸다(28각형의 꼭짓점은 정확히 반지름 위에 있다). 가시 사각형도
    /// 카메라의 orthographicSize / aspect를 그대로 읽는다. 그래서 프로덕션이 어떤 공식을 쓰든
    /// "그려진 그림이 액자 안에 있는가"만 본다.
    /// </summary>
    public sealed class PortraitFallenFramingTests
    {
        private const string LogPrefix = "[넘어짐프레이밍-TEST]";

        private CharacterInfoWindow _window;
        private StickmanAgent _pinnedAgent;
        private IMovementIntentSource _originalIntent;

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pinnedAgent != null && _pinnedAgent.Blackboard != null)
            {
                StickmanBlackboard bb = _pinnedAgent.Blackboard;
                if (bb.Machine != null && bb.Machine.CurrentStateId != StickmanStateId.Idle)
                {
                    bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                }
                if (_originalIntent != null) bb.IntentSource = _originalIntent;
            }
            _pinnedAgent = null;
            _originalIntent = null;

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

        /// <summary>씬 로드 → 창 열기 → 자율 배회 고정(창이 포즈를 덮어쓰지 않게).</summary>
        private IEnumerator SetUpOpenWindowAndPinState()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
            _pinnedAgent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_pinnedAgent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(_pinnedAgent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");

            _originalIntent = _pinnedAgent.Blackboard.IntentSource;
            _pinnedAgent.Blackboard.IntentSource = new StillIntentSource();

            _window.Open("테스트");
            yield return null;
            yield return null;

            // "N프레임"이 아니라 조건 달성까지 실시간 대기 + 타임아웃(이 프로젝트 PlayMode 표준 관례).
            const float TimeoutSeconds = 15f;
            const float RequiredStableSeconds = 0.5f;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            float idleSince = -1f;
            StickmanStateId last = _pinnedAgent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = _pinnedAgent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= RequiredStableSeconds) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last,
                $"{LogPrefix} 상태가 Idle로 안정되지 않아 창이 포즈를 덮어쓸 수 있습니다 — 관측 전제가 깨졌습니다.");
        }

        // ==================== 측정 도구 ====================

        private static Transform Figure(CharacterPortraitStage stage)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            return figure;
        }

        /// <summary>카메라가 실제로 보고 있는 사각형(촬영장 로컬 좌표). orthographicSize/aspect를 그대로 읽는다.</summary>
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

        /// <summary>그려진 머리 링에서 <b>실측</b>한 중심과 외접반경(촬영장 로컬 좌표).</summary>
        private static void MeasureHead(CharacterPortraitStage stage, out Vector2 center, out float radius)
        {
            Transform figure = Figure(stage);
            Transform head = figure.Find("Head");
            Assert.IsNotNull(head, $"{LogPrefix} 미니 피규어에 Head가 없습니다 — 그림이 그려지지 않았습니다.");
            var lr = head.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr, $"{LogPrefix} Head에 LineRenderer가 없습니다.");
            Assert.Greater(lr.positionCount, 8, $"{LogPrefix} 머리 링의 점이 너무 적습니다.");

            var sum = Vector2.zero;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                Vector3 local = stage.transform.InverseTransformPoint(head.TransformPoint(lr.GetPosition(i)));
                sum += new Vector2(local.x, local.y);
            }
            center = sum / n;

            radius = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 local = stage.transform.InverseTransformPoint(head.TransformPoint(lr.GetPosition(i)));
                radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(local.x, local.y)));
            }
            Assert.Greater(radius, 0.0001f, $"{LogPrefix} 머리 반경이 0으로 측정됐습니다.");
        }

        /// <summary>그려진 모든 선의 획 바깥쪽까지 포함한 사각형(촬영장 로컬 좌표).</summary>
        private static Rect MeasureInk(CharacterPortraitStage stage, out int lineCount)
        {
            Transform figure = Figure(stage);
            var lines = figure.GetComponentsInChildren<LineRenderer>(true);
            lineCount = lines.Length;
            Assert.Greater(lineCount, 4, $"{LogPrefix} 그려진 선이 너무 적습니다({lineCount}개).");

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

        private static void AssertHeadFullyInside(CharacterPortraitStage stage, string what)
        {
            Rect view = VisibleRect(stage);
            MeasureHead(stage, out Vector2 c, out float r);

            float leftGap = (c.x - r) - view.xMin;
            float rightGap = view.xMax - (c.x + r);
            float bottomGap = (c.y - r) - view.yMin;
            float topGap = view.yMax - (c.y + r);

            Debug.Log($"{LogPrefix} {what} — 가시 사각형 x[{view.xMin:F3},{view.xMax:F3}] y[{view.yMin:F3},{view.yMax:F3}], " +
                $"머리 중심=({c.x:F3},{c.y:F3}) 반경={r:F3}, 여백 좌={leftGap:F3} 우={rightGap:F3} 하={bottomGap:F3} 상={topGap:F3}.");

            Assert.Greater(leftGap, 0f, $"{LogPrefix} {what}: 머리가 액자 왼쪽으로 {-leftGap:F3}유닛 잘렸습니다.");
            Assert.Greater(rightGap, 0f, $"{LogPrefix} {what}: 머리가 액자 오른쪽으로 {-rightGap:F3}유닛 잘렸습니다.");
            Assert.Greater(bottomGap, 0f, $"{LogPrefix} {what}: 머리가 액자 아래로 {-bottomGap:F3}유닛 잘렸습니다.");
            Assert.Greater(topGap, 0f, $"{LogPrefix} {what}: 머리가 액자 위로 {-topGap:F3}유닛 잘렸습니다.");
        }

        // ============================================================================
        // (1) 핵심 — 머리 원 전체 + 모든 선이 액자 안에 들어온다, 그리고 옛 방식은 실제로 깨진다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FallenPoseKeepsTheWholeHeadAndEveryStrokeInsideTheFrame()
        {
            yield return SetUpOpenWindowAndPinState();

            var stage = PrimaryStage();
            stage.SetPose(PortraitPose.Fallen);
            yield return null;
            Assert.AreEqual(PortraitPose.Fallen, stage.Pose, $"{LogPrefix} 포즈가 Fallen으로 들어가지 않았습니다.");

            // 어떤 장비 상태에서 잰 것인지 증거로 남긴다(모자를 쓰면 그림이 키보다 커진다).
            Debug.Log($"{LogPrefix} 장비 — 모자={EquipmentModel.IsEquipped(EquipmentSlot.Head)}, " +
                $"선글라스={EquipmentModel.IsEquipped(EquipmentSlot.Eyes)}, " +
                $"나비넥타이={EquipmentModel.IsEquipped(EquipmentSlot.Neck)}, " +
                $"망토={EquipmentModel.IsEquipped(EquipmentSlot.Shoulders)} (레벨 {CharacterProgressionModel.Level}).");

            // ① 절대 조건 — 머리 원 전체가 액자 안.
            AssertHeadFullyInside(stage, "넘어짐(SetPose)");

            // ② 머리만 맞추고 발이 튀어나가면 안 된다 — 그려진 모든 획이 액자 안.
            Rect view = VisibleRect(stage);
            Rect inkRect = MeasureInk(stage, out int lineCount);
            Debug.Log($"{LogPrefix} 잉크 사각형 x[{inkRect.xMin:F3},{inkRect.xMax:F3}] y[{inkRect.yMin:F3},{inkRect.yMax:F3}] " +
                $"(선 {lineCount}개, 피규어 배율={Figure(stage).localScale.x:F4}).");

            Assert.GreaterOrEqual(inkRect.xMin, view.xMin,
                $"{LogPrefix} 그림이 액자 왼쪽으로 {view.xMin - inkRect.xMin:F3}유닛 삐져나갔습니다(발/다리 쪽).");
            Assert.LessOrEqual(inkRect.xMax, view.xMax,
                $"{LogPrefix} 그림이 액자 오른쪽으로 {inkRect.xMax - view.xMax:F3}유닛 삐져나갔습니다(머리/모자 쪽).");
            Assert.GreaterOrEqual(inkRect.yMin, view.yMin,
                $"{LogPrefix} 그림이 액자 아래로 삐져나갔습니다.");
            Assert.LessOrEqual(inkRect.yMax, view.yMax,
                $"{LogPrefix} 그림이 액자 위로 삐져나갔습니다.");

            // 누운 사람이 액자 위쪽에 떠 있으면 "쓰러져 있다"로 읽히지 않는다 — 원래 연출 의도의 잠금.
            float inkCenterFromBottom = (inkRect.center.y - view.yMin) / view.height;
            Assert.Less(inkCenterFromBottom, 0.5f,
                $"{LogPrefix} 넘어진 그림의 중심이 액자 아래에서 {inkCenterFromBottom:P0} 지점입니다 — " +
                "절반보다 위면 바닥에 누운 것으로 읽히지 않습니다.");

            // ④ 네거티브 컨트롤 — 옛 "발 회전축" 변환을 그대로 되돌려 놓으면 머리가 실제로 잘린다.
            //    (같은 프레임 안에서 재고 되돌린다. ApplyBreathing이 다음 프레임에 y를 되돌려 놓는다.)
            var metrics = Object.FindFirstObjectByType<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} 씬에서 StickmanMetrics를 찾지 못했습니다 — 네거티브 컨트롤 불가.");
            float h = metrics.TotalHeight;

            Transform figure = Figure(stage);
            Vector3 keepPos = figure.localPosition;
            Quaternion keepRot = figure.localRotation;
            Vector3 keepScale = figure.localScale;

            figure.localScale = Vector3.one;
            figure.localRotation = Quaternion.Euler(0f, 0f, -78f);
            figure.localPosition = new Vector3(-h * 0.30f, h * 0.30f, 0f);

            MeasureHead(stage, out Vector2 oldCenter, out float oldRadius);
            float oldRightGap = view.xMax - (oldCenter.x + oldRadius);
            Debug.Log($"{LogPrefix} (네거티브 컨트롤) 옛 발-회전축 방식 — 머리 중심=({oldCenter.x:F3},{oldCenter.y:F3}), " +
                $"오른쪽 여백={oldRightGap:F3}유닛(음수면 잘림).");

            figure.localPosition = keepPos;
            figure.localRotation = keepRot;
            figure.localScale = keepScale;

            Assert.Less(oldRightGap, 0f,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 옛 방식으로 되돌렸는데도 머리가 액자 안에 있습니다. " +
                "이 테스트가 원래 결함을 잡지 못한다는 뜻이므로 측정 방식을 의심해야 합니다.");
        }

        // ============================================================================
        // (2) 랙돌 / 던져짐 / 일어나는 중 — 세 상태 모두 실제로 그 프레이밍을 받는다
        // ============================================================================
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RagdollThrowTumbleAndGetupAllGetAFramedPortrait()
        {
            yield return SetUpOpenWindowAndPinState();

            var stage = PrimaryStage();
            StickmanBlackboard bb = _pinnedAgent.Blackboard;

            var fallenStates = new[]
            {
                StickmanStateId.Ragdoll,
                StickmanStateId.ThrowTumble,
                StickmanStateId.Getup,
            };

            var metrics = Object.FindFirstObjectByType<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} 씬에서 StickmanMetrics를 찾지 못했습니다.");

            for (int i = 0; i < fallenStates.Length; i++)
            {
                StickmanStateId id = fallenStates[i];
                Assert.AreEqual(PortraitPose.Fallen, CharacterPortraitStage.PoseForState(id),
                    $"{LogPrefix} {id}가 넘어짐 포즈로 매핑되지 않습니다 — 문구와 그림이 어긋납니다.");

                // ★ 공중으로 올린 뒤에 전이시킨다. 땅에 붙어 있으면 ThrowTumble이 첫 Tick에서
                //   "회전할 시간이 부족합니다(착지까지 0.00초)"로 스스로 빠져나가, 창이 그 상태를
                //   한 번도 못 보고 지나간다(첫 실행에서 실측으로 걸렸다).
                bb.Body.linearVelocity = Vector2.zero;
                bb.Body.position = new Vector2(bb.Body.position.x, bb.Body.position.y + metrics.TotalHeight * 4f);
                bb.CurrentFootholdHandle = 0L;
                bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

                // 안티-공허 장치: 직전 반복의 Fallen이 남아 있으면 이 관측은 아무것도 증명하지 못한다.
                yield return WaitForPose(stage, fallen: false, what: $"{id} 관측 전 초기화");
                Assert.AreNotEqual(PortraitPose.Fallen, stage.Pose,
                    $"{LogPrefix} {id} 관측 전에 포즈가 Fallen에서 내려오지 않았습니다 — 관측이 공허해집니다.");

                bb.Machine.ChangeState(id, isForcedInterrupt: true);
                yield return WaitForPose(stage, fallen: true, what: $"{id} 전이 후");

                Assert.AreEqual(PortraitPose.Fallen, stage.Pose,
                    $"{LogPrefix} {id}로 전이했는데 초상화 포즈가 {stage.Pose}입니다(현재 상태 " +
                    $"{bb.Machine.CurrentStateId}) — 창이 포즈를 밀어넣지 않았습니다.");
                AssertHeadFullyInside(stage, $"{id} 실제 전이");
            }

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        /// <summary>포즈가 원하는 쪽이 될 때까지 조건 기반 대기(프레임 수가 아니라 실시간 + 타임아웃).</summary>
        private static IEnumerator WaitForPose(CharacterPortraitStage stage, bool fallen, string what)
        {
            const float TimeoutSeconds = 2f;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if ((stage.Pose == PortraitPose.Fallen) == fallen) yield break;
            }
            Debug.Log($"{LogPrefix} {what}: {TimeoutSeconds:F0}초 안에 포즈가 " +
                $"{(fallen ? "Fallen" : "Fallen 아님")}이 되지 않았습니다(현재 {stage.Pose}).");
        }
    }
}
