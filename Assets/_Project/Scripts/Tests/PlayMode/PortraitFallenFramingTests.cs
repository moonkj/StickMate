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
    ///  ③ 랙돌 / 던져짐 / 일어나는 중 <b>세 상태 모두</b> 넘어짐 포즈로 매핑되고, 그 포즈가 실제로
    ///     액자에 들어가면 기하가 성립한다(매핑 + 기하 동시 검증).
    ///     <para>★★ 2026-09-02 변경 — 예전 이 항목은 <b>"창이 상태를 보고 포즈를 밀어넣는다"</b>를
    ///     함께 잠그고 있었다. 사용자 신고("장비 착용 모습만 …인데 가끔 움직임")로 <b>그 계약 자체가
    ///     폐기</b>됐다(docs/UX_FLOW.md §45-1: 캐릭터의 상태는 액자에 도달하지 않는다).
    ///     그래서 (2)는 포즈를 <see cref="CharacterPortraitStage.SetPose"/>로 직접 넣어 <b>기하만</b>
    ///     확인하고, 없어진 옛 계약은 (5)에서 <b>반대 방향으로</b> 다시 잠근다 —
    ///     상태를 몰아도 액자가 따라오지 않는지. 잠금을 지우지 않고 <b>뒤집는다</b>.</para>
    ///  ④ (네거티브 컨트롤) 두 갈래다 — <b>④-A 상수 노후 감시</b> + <b>④-B 탐지 경로 생존</b>.
    ///     <para>★★ 2026-09-06 정정 — 옛날에는 이 자리가 하나였고 <b>"옛 변환을 되돌리면 ①이 깨진다"</b>였다.
    ///     그 단언이 러너에서 <b>빨개졌다</b>(오른쪽 여백 <b>+0.022유닛</b>, 즉 옛 포즈가 액자 <b>안</b>에
    ///     들어왔다). 조사 결과 <b>상수가 낡은 것이 아니라 전제가 낡았다</b>:
    ///     옛 변환(−78° · (−h·0.30, h·0.30) · 배율 1)은 지금도 <b>2026-08-30 증거와 같은 그림</b>을 만든다
    ///     (그날 커밋 be751b7이 기록한 머리 x범위 [0.830, 1.161] ↔ 오늘 실측 [0.831, 1.161]).
    ///     바뀐 것은 <b>액자</b>다 — 두 번 넓어졌다:</para>
    ///     <list type="bullet">
    ///       <item><see cref="CharacterInfoWindow.PortraitContentSize"/>에서 파생되는 종횡비 1.044 → 1.122</item>
    ///       <item><c>CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR</c> 1.80 → <b>2.551</b>
    ///             (2026-09-05 R17, 인계본 털모자를 액자에 넣으려고)</item>
    ///     </list>
    ///     <para>액자 반폭이 <b>1.033 → 1.110 → 1.183</b>으로 커지는 동안 옛 포즈의 머리 우단은
    ///     <b>1.161에 그대로 있었다.</b> 그래서 여백이 −0.128 → −0.051 → <b>+0.022</b>로 넘어갔다.
    ///     ⇒ <b>옛 결함은 오늘의 액자에서는 더 이상 머리를 자르지 않는다.</b> 이것은 프로덕션 회귀가
    ///     아니라 <b>네거티브 컨트롤의 수명이 다한 것</b>이다.</para>
    ///     <para>그래서 잠금을 <b>지우지 않고 둘로 나눴다</b>:
    ///     <b>④-A</b>는 옛 변환을 그대로 보존하되 <b>액자와 무관한</b> 두 가지만 단언한다 —
    ///     (ⅰ) 옛 그림이 여전히 2026-08-30 증거와 같은가(= 캐릭터 조형이 바뀌면 여기서 걸린다),
    ///     (ⅱ) 옛 회전축(발)이 머리를 프로덕션보다 최소 <b>머리 반경 하나</b>만큼 더 오른쪽으로 밀어내는가.
    ///     <b>④-B</b>는 ①의 측정이 <b>오늘도 실제로 빨개지는가</b>를 지금 잰 여백에서 <b>유도한</b>
    ///     이동량으로 확인한다(고정 숫자가 없으므로 액자가 또 넓어져도 썩지 않는다).</para>
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

        // ============================================================================
        // ④-A 상수 노후 감시의 기준값 — 출처는 <b>프로덕션이 아니라 커밋 be751b7의 실측 기록</b>이다
        // ============================================================================
        // 그날 커밋 메시지 원문: "재현 실측: 액자 가시 x범위 [-0.870, 0.870], 머리 원 x범위
        // [0.830, 1.161] -> 오른쪽으로 0.291유닛 잘림."
        //
        // ★ 교정(알려진 값으로 먼저 맞춰 본다 — 이 저장소의 공통 처방):
        //   2026-09-06 러너 실측 = [0.831, 1.161]. 열흘이 지나고 액자가 두 번 넓어졌는데도
        //   <b>머리 x범위는 세 자리까지 같다.</b> 즉 옛 변환 사본(−78° / (−h·0.30, h·0.30) / 배율 1)은
        //   여전히 그날의 그림을 만들고 있고, 낡은 것은 상수가 아니라 <b>"액자가 이보다 좁다"는 전제</b>였다.
        //
        // ★ 왜 x만 잠그고 y는 안 잠그는가: y는 ApplyBreathing이 매 프레임 미세하게 움직인다
        //   (CharacterPortraitStage.Breathing). x는 그 영향을 받지 않으므로 x만이 안정된 자다.
        //
        // ★★ 왜 유닛이 아니라 <b>키(TotalHeight) 대비 비율</b>인가 — 이걸 유닛으로 적으면
        //   <b>캐릭터 크기 배율에 묶인다.</b> 촬영장의 모든 치수와 이 테스트의 h가 <b>같은 출처</b>
        //   (StickmanMetrics.TotalHeight)이고, 그 값은 사용자 크기 설정(CharacterScaleController,
        //   기본 0.75)에 따라 통째로 스케일한다. 즉 앞선 테스트가 배율을 바꿔 놓으면 절대 유닛값은
        //   전부 어긋나는데 <b>그림의 모양은 하나도 안 바뀐 것</b>이다. 비율로 적으면 배율이 약분되고,
        //   남는 것은 우리가 실제로 감시하려는 <b>캐릭터 비례</b>뿐이다.
        //
        // ★ 세 갈래로 교차 검산했다(하나였으면 못 믿는다):
        //   ① 2026-08-30 커밋 기록  0.830 / 1.161  ÷ h(1.7059) = 0.48654 / 0.68058
        //   ② 2026-09-06 러너 실측  0.831 / 1.161  ÷ h(1.7059) = 0.48713 / 0.68058
        //   ③ 닫힌 식 — 옛 변환은 머리 중심을 (h−R)만큼 떨어진 반경으로 −78° 돌린 뒤 x로 −0.30h 민다:
        //        중심비 = (1 − R/h)·sin78° − 0.30 = 0.90328 × 0.978148 − 0.30 = 0.58348
        //        R/h    = BaselineHeadVisualRadius / BaselineCharacterTotalHeight = 0.22/2.2746944 = 0.09672
        //        ⇒ 좌 0.48676 / 우 0.68019
        //   세 값의 최대 편차가 0.0006이다. 아래 기대값은 그 중앙이다.
        private const float HistoricHeadLeftRatioOfHeight = 0.4868f;
        private const float HistoricHeadRightRatioOfHeight = 0.6804f;

        /// <summary>위 두 기준값의 허용오차(키 대비 비율). 세 갈래 교차 검산의 편차가 0.0006이므로
        /// 0.010은 그 <b>16배</b> 여유다 — 부동소수/렌더 순서는 흡수한다. 동시에 0.010은 머리 반경
        /// (0.0967 h)의 10%, 다리 획(0.0551 h)의 18%라서 <b>캐릭터 비례가 실제로 바뀌면 걸린다</b>.</summary>
        private const float HistoricHeadRatioTolerance = 0.010f;

        /// <summary>④-B가 "오른쪽 여백"에 더해 추가로 미는 양(액자 <b>가로폭</b> 대비 비율).
        /// 절대 숫자를 쓰지 않는 이유가 ④-A의 교훈 그 자체다 — 액자가 넓어지면 절대값은 썩는다.
        /// 1%면 지금 액자에서 0.024유닛 = 머리 반경의 14%라, 여백을 확실히 넘기면서도
        /// "겨우 넘겨서 통과한 것"이 아님이 로그 숫자로 드러난다.</summary>
        private const float NudgeOvershootOfFrameWidth = 0.01f;

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

            // 옛/새 변환을 비교하려면 지금(프로덕션) 머리를 먼저 재 둬야 한다.
            MeasureHead(stage, out Vector2 prodCenter, out float prodRadius);
            float prodRightGap = view.xMax - (prodCenter.x + prodRadius);

            var metrics = Object.FindFirstObjectByType<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} 씬에서 StickmanMetrics를 찾지 못했습니다 — 네거티브 컨트롤 불가.");
            float h = metrics.TotalHeight;

            Transform figure = Figure(stage);
            Vector3 keepPos = figure.localPosition;
            Quaternion keepRot = figure.localRotation;
            Vector3 keepScale = figure.localScale;

            Vector2 oldCenter;
            float oldRadius;
            Vector2 nudgedCenter;
            float nudgedRadius;
            float nudgeX;
            try
            {
                // ────────────────────────────────────────────────────────────────
                // ④-A 옛 "발 회전축" 변환의 <b>역사 기록 + 상수 노후 감시</b>
                // ────────────────────────────────────────────────────────────────
                // 아래 세 줄은 be751b7이 지운 옛 프로덕션 코드 <b>원문 그대로</b>다(추정이 아니다):
                //     _figureRoot.localRotation = Quaternion.Euler(0f, 0f, -78f);
                //     _baseFigureY              = TotalHeight * 0.30f;
                //     _figureRoot.localPosition = new Vector3(-TotalHeight * 0.30f, _baseFigureY, 0f);
                // 옛 코드에는 배율 보정이 없었으므로 localScale = 1도 그 일부다.
                // ★ 그래서 이 숫자들은 "프로덕션에서 베낀 상수"가 아니라 <b>삭제된 코드의 사본</b>이며,
                //   프로덕션이 바뀌었다고 따라 고치면 안 된다 — 고치는 순간 기록이 사라진다.
                figure.localScale = Vector3.one;
                figure.localRotation = Quaternion.Euler(0f, 0f, -78f);
                figure.localPosition = new Vector3(-h * 0.30f, h * 0.30f, 0f);

                MeasureHead(stage, out oldCenter, out oldRadius);

                // ────────────────────────────────────────────────────────────────
                // ④-B ①의 탐지 경로가 <b>오늘도 살아 있는가</b>
                // ────────────────────────────────────────────────────────────────
                // 지금 프로덕션 포즈를 오른쪽 여백보다 조금 더 밀면 ①의 측정이 반드시 음수를 내야 한다.
                // 이동량을 <b>방금 잰 여백에서 유도</b>하므로 액자가 또 넓어져도 이 컨트롤은 썩지 않는다
                // (④-A가 딱 그 이유로 2026-09-06에 수명이 다했다 — 클래스 문서 참고).
                figure.localPosition = keepPos;
                figure.localRotation = keepRot;
                figure.localScale = keepScale;
                nudgeX = prodRightGap + view.width * NudgeOvershootOfFrameWidth;
                figure.localPosition = keepPos + new Vector3(nudgeX, 0f, 0f);

                MeasureHead(stage, out nudgedCenter, out nudgedRadius);
            }
            finally
            {
                figure.localPosition = keepPos;
                figure.localRotation = keepRot;
                figure.localScale = keepScale;
            }

            float oldRightGap = view.xMax - (oldCenter.x + oldRadius);
            float oldPushRight = oldCenter.x - prodCenter.x;
            float nudgedRightGap = view.xMax - (nudgedCenter.x + nudgedRadius);

            Debug.Log($"{LogPrefix} (④-A 역사 기록) 옛 발-회전축 방식 — 머리 중심=({oldCenter.x:F3},{oldCenter.y:F3}) " +
                $"반경={oldRadius:F3}, 머리 x범위=[{oldCenter.x - oldRadius:F3},{oldCenter.x + oldRadius:F3}]유닛 " +
                $"= 키({h:F3}) 대비 [{(oldCenter.x - oldRadius) / Mathf.Max(0.0001f, h):F4},{(oldCenter.x + oldRadius) / Mathf.Max(0.0001f, h):F4}]배, " +
                $"오른쪽 여백={oldRightGap:F3}유닛. 프로덕션 대비 오른쪽으로 {oldPushRight:F3}유닛 " +
                $"(머리 지름 {2f * prodRadius:F3}의 {oldPushRight / Mathf.Max(0.0001f, 2f * prodRadius):F2}배). " +
                "★ 여백이 양수여도 회귀가 아니다 — 액자가 넓어져서다(클래스 문서 ④).");
            Debug.Log($"{LogPrefix} (④-B 탐지 경로 생존) 지금 포즈를 x로 {nudgeX:F3}유닛 밀었더니 " +
                $"오른쪽 여백 {prodRightGap:F3} -> {nudgedRightGap:F3}유닛(음수여야 정상).");

            // ④-A-ⅰ 상수 노후 감시 — 옛 변환이 <b>2026-08-30 그날의 그림</b>을 여전히 만드는가.
            //   기대값의 출처는 프로덕션 함수가 아니라 <b>커밋 be751b7의 실측 기록 + 닫힌 식</b>이다
            //   (상수 선언부의 세 갈래 교차 검산 참고). 캐릭터 <b>비례</b>가 바뀌면 여기서 걸리고,
            //   캐릭터 <b>크기 배율</b>만 달라진 경우에는 걸리지 않는다(비율이라 약분된다).
            //   걸렸을 때 할 일은 이 두 숫자를 맞추는 것이 아니라, 먼저 "왜 조형이 바뀌었는가"를
            //   확인하고 새 실측을 근거와 함께 갱신하는 일이다.
            Assert.Greater(h, 0.0001f, $"{LogPrefix} 미니 피규어 키가 0입니다 — 비율 판정이 불가능합니다.");
            float oldLeftRatio = (oldCenter.x - oldRadius) / h;
            float oldRightRatio = (oldCenter.x + oldRadius) / h;

            Assert.AreEqual(HistoricHeadLeftRatioOfHeight, oldLeftRatio, HistoricHeadRatioTolerance,
                $"{LogPrefix} ④-A 실패 — 옛 변환이 만드는 머리 왼쪽 x가 키의 {oldLeftRatio:F4}배입니다" +
                $"(2026-08-30 기준 {HistoricHeadLeftRatioOfHeight:F4}배, 유닛으로는 {oldCenter.x - oldRadius:F3} / 키 {h:F3}). " +
                "옛 변환 자체는 삭제된 코드의 사본이라 변할 수 없고 비율이므로 크기 배율도 약분됩니다 — " +
                "즉 캐릭터 «비례»(머리 반경/키, 관절 위치)가 바뀌었다는 뜻입니다. [디버거로 이동]");
            Assert.AreEqual(HistoricHeadRightRatioOfHeight, oldRightRatio, HistoricHeadRatioTolerance,
                $"{LogPrefix} ④-A 실패 — 옛 변환이 만드는 머리 오른쪽 x가 키의 {oldRightRatio:F4}배입니다" +
                $"(2026-08-30 기준 {HistoricHeadRightRatioOfHeight:F4}배, 유닛으로는 {oldCenter.x + oldRadius:F3} / 키 {h:F3}). " +
                "위와 같은 사유입니다. [디버거로 이동]");

            // ④-A-ⅱ 결함의 본질 — "회전축이 발"이면 회전 반경이 <b>키</b>라서 머리가 크게 쓸려 나간다.
            //   프로덕션은 회전축이 그림 중심이라 반경이 그 절반이다. 그 차이를 <b>액자와 무관한</b>
            //   자(= 머리 반경)로 잰다. 실측 0.404유닛 vs 머리 반경 0.165유닛 = 2.45배 여유라
            //   액자가 또 넓어져도 이 단언은 흔들리지 않는다(옛 단언은 여유가 0.05유닛이었다).
            Assert.Greater(oldPushRight, prodRadius,
                $"{LogPrefix} ④-A 실패 — 옛 발-회전축 방식이 머리를 오른쪽으로 {oldPushRight:F3}유닛밖에 " +
                $"밀지 못했습니다(머리 반경 {prodRadius:F3} 미만). 두 방식의 회전축이 사실상 같아졌다는 뜻이므로, " +
                "프로덕션이 FrameFallenFigure()의 '회전축 = 그림 중심'을 잃지 않았는지 먼저 확인하십시오.");

            // ④-B 탐지 경로 생존 — 여기서 초록이면 ①의 단언은 아무것도 못 잡는 장식이다.
            Assert.Less(nudgedRightGap, 0f,
                $"{LogPrefix} ④-B 실패 — 오른쪽 여백({prodRightGap:F3})보다 큰 {nudgeX:F3}유닛을 밀었는데도 " +
                $"머리가 액자 안에 있습니다(여백 {nudgedRightGap:F3}). ①의 측정 경로가 죽었다는 뜻입니다 — " +
                "MeasureHead/VisibleRect가 무엇을 재고 있는지부터 의심하십시오.");
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

            for (int i = 0; i < fallenStates.Length; i++)
            {
                StickmanStateId id = fallenStates[i];
                Assert.AreEqual(PortraitPose.Fallen, CharacterPortraitStage.PoseForState(id),
                    $"{LogPrefix} {id}가 넘어짐 포즈로 매핑되지 않습니다. " +
                    "이 매핑은 프로덕션에서 더 이상 불리지 않지만(45-1), 2026-08-30 붙잡힘 판정의 " +
                    "근거가 여기에 있으므로 계속 잠근다.");

                // 안티-공허 장치: 직전 반복의 Fallen이 남아 있으면 이 관측은 아무것도 증명하지 못한다.
                stage.SetPose(PortraitPose.Standing);
                yield return null;
                Assert.AreNotEqual(PortraitPose.Fallen, stage.Pose,
                    $"{LogPrefix} {id} 관측 전에 포즈가 Fallen에서 내려오지 않았습니다 — 관측이 공허해집니다.");

                stage.SetPose(CharacterPortraitStage.PoseForState(id));
                yield return null;

                Assert.AreEqual(PortraitPose.Fallen, stage.Pose,
                    $"{LogPrefix} {id}의 포즈가 액자에 들어가지 않았습니다(현재 {stage.Pose}).");
                AssertHeadFullyInside(stage, $"{id} 넘어짐 프레이밍");
            }

            stage.SetPose(PortraitPose.Standing);
            yield return null;
        }

        // ============================================================================
        // (5) ★ 뒤집힌 잠금 — <b>상태를 몰아도 액자는 따라오지 않는다</b> (2026-09-02)
        // ============================================================================
        //
        // (2)가 잠그던 옛 계약("창이 상태를 보고 포즈를 밀어넣는다")은 폐기됐다. 잠금을 그냥 지우면
        // 다음 사람이 "왜 없앴지?" 하고 되살릴 수 있으므로, <b>같은 자리에 반대 방향으로</b> 남긴다.
        // 넘어짐은 (A) 종이인형이 잃는 유일한 진짜 일치였다(28개 중 1개) — 그 사실을 여기 적어 둔다.
        // 문구 "넘어져 있는 중"이 그 몫을 진다(CharacterInfoWindow.StateLabel).

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DrivingTheRealStateNeverReachesTheFrameAnyMore()
        {
            yield return SetUpOpenWindowAndPinState();

            var stage = PrimaryStage();
            StickmanBlackboard bb = _pinnedAgent.Blackboard;

            Assert.AreEqual(PortraitPose.Standing, stage.Pose,
                $"{LogPrefix} 창을 연 직후 액자 포즈가 {stage.Pose}입니다 — 서 있는 종이인형이어야 합니다.");

            bb.CurrentFootholdHandle = 0L;
            bb.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);

            // 넉넉히 기다린다 — 짧게 기다려서 초록이면 그건 증명이 아니라 운이다(벽시계).
            float deadline = Time.realtimeSinceStartup + 1.0f;
            bool sawRagdoll = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (bb.Machine.CurrentStateId == StickmanStateId.Ragdoll) sawRagdoll = true;
            }

            // 대조군 — 상태가 실제로 Ragdoll을 지났는가. 아니면 아래 단언은 공허하다.
            Assert.IsTrue(sawRagdoll,
                $"{LogPrefix} Ragdoll에 한 번도 머물지 못했습니다(현재 {bb.Machine.CurrentStateId}) — " +
                "관측 전제가 깨졌습니다.");

            Assert.AreEqual(PortraitPose.Standing, stage.Pose,
                $"{LogPrefix} 캐릭터가 넘어졌더니 액자 포즈가 {stage.Pose}가 됐습니다 — " +
                "정보창이 다시 SetPose를 부르고 있습니다(액자 불변식 위반, docs/UX_FLOW.md 45-1).");

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

    }
}
