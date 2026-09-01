using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-01 그림체 전환 P1 — 실제로 구워진 프리팹에서 <b>세 가지</b>를 잰다
    /// (docs/UX_FLOW.md 38-4 / 38-5).
    ///
    ///  ① <b>머리 크기가 변하지 않았다</b> — 채움(<c>HeadFill</c>)의 바깥 반경이 링(<c>HeadOutline</c>)의
    ///     경로 반경과 <b>정확히 같고</b>, 그 값이 <c>StickmanMetrics.HeadRadius</c>와도 같다.
    ///     이것이 사용자 요구("머리는 그냥 다 채워주고")를 크기 변경 없이 이행했다는 증거다.
    ///  ② <b>눈이 렌더링에서 완전히 사라졌다</b> — 캐릭터 계층 어디에도 <c>LeftEye</c>/<c>RightEye</c>가 없다
    ///     (직속 자식만이 아니라 <b>자손 전체</b>를 훑는다 — "다른 데로 옮겨졌을 뿐"을 잡기 위해서다).
    ///  ③ <b><see cref="EyeController"/>는 살아 있고 무해하다</b> — 여전히 만들어지고, 모든 메서드가
    ///     예외 없이 아무 일도 하지 않는다. 사용자 지시("코드는 남겨 나중에 복원 가능하게")가 실제로
    ///     지켜졌다는 것을 <b>클래스를 실행해서</b> 확인한다(소스에 남아 있다는 것만으로는 부족하다 —
    ///     남아 있는데 실행하면 터지는 코드는 복원 경로가 아니다).
    ///
    /// <para>★ ①이 특히 중요한 이유(계약 C1): <c>StickmanMetrics.HeadRadius</c>는 <c>HeadOutline</c>
    /// LineRenderer의 <b>첫 점 x</b>를 읽고, 거기서 액세서리 28종 리그/초상화 액자/말풍선 위치가 전부
    /// 파생된다. 채움을 만들면서 링을 지우거나 반경을 바꿨다면 그 전부가 폴백 비율로 조용히 떨어진다.</para>
    /// </summary>
    public sealed class CharacterHeadFillTests
    {
        private const string LogPrefix = "[머리채움-PLAY]";

        /// <summary>설계상 채움 폭 ÷ 머리 반경. Editor/SceneBootstrapper의 유도식
        /// (r = R/(1+k/2), W = k·r, k = 2.4)에서 W/R = k/(1+k/2) = 1.0909…다.
        /// 실측 폭이 이 값보다 크면 <b>화면상 최소 획 하한이 폭을 밀어 올린 것</b>이다.</summary>
        private const float FillWidthPerHeadRadius = 2.4f / (1f + 2.4f * 0.5f);

        private StickmanAgent _agent;
        private StickConfig _config;
        private float _restoreScale;

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            Assert.IsNotNull(_agent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");
            _agent.Blackboard.IntentSource = new StillIntentSource();

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickmanAgent에 StickConfig가 배선돼 있지 않습니다.");
            _restoreScale = _config.ResolveCharacterScale();

            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) _config.ClearRuntimeCharacterScale();
            if (_agent != null && _restoreScale > 0f) _agent.ApplyCharacterScale(_restoreScale, "테스트 정리");
            _agent = null;
            _config = null;
        }

        // ============================================================================
        // 실측 도구
        // ============================================================================

        private Transform Head()
        {
            Transform root = _agent.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i) != null && root.GetChild(i).name == "Head") return root.GetChild(i);
            }
            Assert.Fail($"{LogPrefix} 캐릭터 루트 직속에 'Head'가 없습니다 — 프리팹 계층이 바뀌었습니다.");
            return null;
        }

        private static LineRenderer ChildLine(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name) return c.GetComponent<LineRenderer>();
            }
            return null;
        }

        /// <summary>닫힌 원 경로 LineRenderer에서 (경로반경, 폭)을 실측한다.</summary>
        private static void MeasureRing(LineRenderer lr, out float pathRadius, out float width)
        {
            Assert.Greater(lr.positionCount, 8, $"{LogPrefix} '{lr.name}'의 점이 너무 적습니다({lr.positionCount}개).");
            float sum = 0f;
            for (int i = 0; i < lr.positionCount; i++)
            {
                Vector3 p = lr.GetPosition(i);
                sum += new Vector2(p.x, p.y).magnitude;
            }
            pathRadius = sum / lr.positionCount;
            width = Mathf.Max(lr.startWidth, lr.endWidth);
        }

        /// <summary>캐릭터 자손 전체에서 그 이름의 오브젝트를 찾는다(있으면 경로를 돌려준다).</summary>
        private static string FindAnywhere(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != name) continue;
                string path = all[i].name;
                for (Transform t = all[i].parent; t != null; t = t.parent) path = t.name + "/" + path;
                return path;
            }
            return null;
        }

        // ============================================================================
        // ① 머리 크기 무변경 — 배포 배율에서
        // ============================================================================
        [UnityTest]
        public IEnumerator 머리를_채워도_머리_반경이_변하지_않는다()
        {
            yield return LoadScene();

            Transform head = Head();
            LineRenderer fill = ChildLine(head, "HeadFill");
            LineRenderer ring = ChildLine(head, "HeadOutline");

            Assert.IsNotNull(fill,
                $"{LogPrefix} 'Head/HeadFill'이 없습니다 — 머리가 채워지지 않았습니다. " +
                "프리팹을 다시 굽지 않았다면 StickMate/Rebuild All(프리팹+씬 동시)을 실행하십시오.");
            Assert.IsNotNull(ring,
                $"{LogPrefix} 'Head/HeadOutline'이 없습니다 — <b>절대 지우면 안 되는</b> 링입니다. " +
                "StickmanMetrics.HeadRadius가 이 링의 첫 점 x를 읽습니다(계약 C1, docs/UX_FLOW.md 38-2).");

            MeasureRing(fill, out float fillPath, out float fillWidth);
            MeasureRing(ring, out float ringPath, out float ringWidth);

            float rootScale = Mathf.Abs(_agent.transform.localScale.y);
            StickmanMetrics metrics = _agent.Metrics;
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");

            // ★ 단위 주의 — LineRenderer의 **점 좌표는 로컬**(Transform 스케일을 탄다)이지만
            //   **폭은 월드**다(Transform 스케일을 안 따라간다 — StickmanAgent.ApplyStrokeWidthsForScale이
            //   배율마다 직접 다시 대입하는 이유가 바로 그것이다). 그래서 둘을 더하기 전에 점 쪽만 월드로 올린다.
            float fillOuterWorld = fillPath * rootScale + fillWidth * 0.5f;
            float fillInnerWorld = fillPath * rootScale - fillWidth * 0.5f;
            float ringWorld = ringPath * rootScale;

            Debug.Log($"{LogPrefix} 실측 — 채움 경로 {fillPath:F5}(로컬) / 폭 {fillWidth:F5}(월드) → 바깥 {fillOuterWorld:F5}(월드), " +
                $"링 경로 {ringPath:F5}(로컬)={ringWorld:F5}(월드) / 폭 {ringWidth:F5}, 루트 스케일 {rootScale:F4}, " +
                $"metrics.HeadRadius {metrics.HeadRadius:F5}(월드).");

            // ★ (a) 이 전환의 진짜 절대 조건 — <b>머리 크기 자체</b>는 링이 정하고, 링은 손대지 않았다.
            //   StickmanMetrics.HeadRadius(= 링 첫 점의 x, 계약 C1)가 배포 기준 그대로여야 한다.
            float expected = 0.22f * _config.ResolveCharacterScale();
            Assert.AreEqual(expected, metrics.HeadRadius, expected * 0.01f,
                $"{LogPrefix} 머리 반경이 배포 기준(0.22 × 배율 = {expected:F5})에서 벗어났습니다 — " +
                "이번 전환의 절대 조건('머리 크기는 1pt도 변하지 않는다')이 깨졌습니다.");
            Assert.AreEqual(metrics.HeadRadius, ringWorld, metrics.HeadRadius * 0.005f,
                $"{LogPrefix} 링 반경(월드 {ringWorld:F5})과 StickmanMetrics.HeadRadius({metrics.HeadRadius:F5})가 " +
                "어긋났습니다 — 계약 C1이 깨졌습니다.");

            // (b) 채움은 그 안에 정확히 들어앉는다 — 링 중심선까지 닿고(틈 없음) 바깥으로는 안 나간다(안 커짐).
            float ringOuterWorld = ringWorld + ringWidth * 0.5f;
            Assert.GreaterOrEqual(fillOuterWorld, ringWorld - 1e-5f,
                $"{LogPrefix} 채움 바깥({fillOuterWorld:F5})이 링 중심선({ringWorld:F5})에 못 미칩니다 — " +
                "얼굴 가장자리에 빈 고리가 보입니다.");
            Assert.LessOrEqual(fillOuterWorld, ringOuterWorld + 1e-5f,
                $"{LogPrefix} 채움 바깥({fillOuterWorld:F5})이 머리 바깥 실루엣({ringOuterWorld:F5})을 넘었습니다 — " +
                "머리가 커집니다.");

            // (c) 안쪽까지 실제로 찼다 — 중심에 구멍이 없다.
            Assert.LessOrEqual(fillInnerWorld, 0f,
                $"{LogPrefix} 채움 안쪽 가장자리가 {fillInnerWorld:F5}(>0)입니다 — 머리 한가운데가 비어 있습니다.");

            // (d) 하한이 폭을 밀어 올리지 않은 배포 배율에서는 설계 등식이 **정확히** 성립한다
            //     (하한이 구속하는 환경에 대한 안전성 증명은 다이얼 전 구간 테스트 + EditMode 참고).
            if (fillWidth <= ringWorld * FillWidthPerHeadRadius * 1.001f)
            {
                Assert.AreEqual(ringWorld, fillOuterWorld, ringWorld * 0.005f,
                    $"{LogPrefix} 채움 바깥 반경({fillOuterWorld:F5})이 링 반경({ringWorld:F5})과 다릅니다 — " +
                    "유도식(r = R/(1+k/2), W = k·r)이 깨졌습니다.");
            }
        }

        // ============================================================================
        // ① ' 다이얼 전 구간에서 — 획 하한이 머리를 부풀리지 않는다
        // ============================================================================
        // ★ 2026-09-01 실측으로 배운 것: 화면상 2pt 하한의 **월드 값은 실행 환경마다 다르다**
        //   (StickmanAgent.ResolveMinStrokeWorldWidth가 화면 높이 ÷ 직교 크기로 실측한다).
        //   배치 모드 헤드리스 화면(480pt)에서는 0.100유닛이 되어 최소 배율에서 **실제로 구속한다**.
        //   그래도 머리는 커지지 않는다 — 하한이 채움과 링에 **똑같이** 걸리는데 채움의 경로 반경이
        //   더 작기(R/2.2 &lt; R) 때문이다. 그래서 여기서 재는 절대 조건은 "하한에 안 걸린다"가 아니라
        //   **"채움이 머리 바깥 실루엣을 넘지 않는다 + 링과 틈이 없다"** 두 개다
        //   (오프라인 증명은 EditMode HeadFillGeometryTests.FillNeverEscapesTheRingUnderAnyStrokeFloor).
        [UnityTest]
        public IEnumerator 다이얼_전_구간에서_채움이_머리_실루엣을_넘지_않는다()
        {
            yield return LoadScene();

            float[] scales = { StickConfig.MinCharacterScale, 0.5f, 1f, StickConfig.MaxCharacterScale };
            foreach (float s in scales)
            {
                Assert.IsTrue(_agent.ApplyCharacterScale(s, "머리채움 테스트"),
                    $"{LogPrefix} 배율 {s:F2} 적용에 실패했습니다.");
                yield return null;

                Transform head = Head();
                MeasureRing(ChildLine(head, "HeadFill"), out float fillPath, out float fillWidth);
                MeasureRing(ChildLine(head, "HeadOutline"), out float ringPath, out float ringWidth);

                float rootScale = Mathf.Abs(_agent.transform.localScale.y);
                float fillOuter = fillPath * rootScale + fillWidth * 0.5f;
                float fillInner = fillPath * rootScale - fillWidth * 0.5f;
                float ringCenter = ringPath * rootScale;               // 머리 반경 R(= metrics.HeadRadius).
                float ringOuter = ringCenter + ringWidth * 0.5f;       // 머리의 **바깥 실루엣** — 이게 "머리 크기"다.
                float floor = _agent.MinStrokeWorldWidth;
                bool floored = fillWidth > ringCenter * FillWidthPerHeadRadius * 1.001f;

                Debug.Log($"{LogPrefix} 배율 {s:F2} — 채움 [{fillInner:F5}, {fillOuter:F5}], " +
                    $"링 중심선 {ringCenter:F5} / 바깥 {ringOuter:F5}, 폭 채움 {fillWidth:F5} 링 {ringWidth:F5}, " +
                    $"실측 하한 {floor:F5}{(floored ? " (하한이 채움을 밀어 올림)" : "")}.");

                // (i) 절대 조건 — 머리가 커지지 않는다.
                Assert.LessOrEqual(fillOuter, ringOuter + 1e-5f,
                    $"{LogPrefix} 배율 {s:F2}에서 채움 바깥({fillOuter:F5})이 머리 바깥 실루엣({ringOuter:F5})을 " +
                    "넘었습니다 — 머리가 커집니다.");

                // (ii) 절대 조건 — 채움과 링 사이에 빈 고리가 생기지 않는다.
                Assert.GreaterOrEqual(fillOuter, ringCenter - 1e-5f,
                    $"{LogPrefix} 배율 {s:F2}에서 채움 바깥({fillOuter:F5})이 링 중심선({ringCenter:F5})에 " +
                    "못 미칩니다 — 얼굴 가장자리에 빈 고리가 보입니다.");

                // (iii) 중심에 구멍이 없다.
                Assert.LessOrEqual(fillInner, 0f,
                    $"{LogPrefix} 배율 {s:F2}에서 채움 안쪽 가장자리가 {fillInner:F5}(>0)입니다.");

                // (iv) 하한이 구속하지 않는 배율에서는 설계 등식이 **정확히** 성립한다.
                if (!floored)
                {
                    Assert.AreEqual(ringCenter, fillOuter, ringCenter * 0.005f,
                        $"{LogPrefix} 배율 {s:F2}(하한 비구속)에서 채움 바깥({fillOuter:F5})이 " +
                        $"머리 반경({ringCenter:F5})과 다릅니다 — 유도식이 깨졌습니다.");
                }
            }
        }

        // ============================================================================
        // ② 눈이 렌더링에서 완전히 사라졌다
        // ============================================================================
        [UnityTest]
        public IEnumerator 캐릭터_어디에도_눈_오브젝트가_없다()
        {
            yield return LoadScene();

            string left = FindAnywhere(_agent.transform, "LeftEye");
            string right = FindAnywhere(_agent.transform, "RightEye");

            Assert.IsNull(left,
                $"{LogPrefix} 캐릭터 계층에 눈이 남아 있습니다: {left} — " +
                "Editor/SceneBootstrapper.BakeEyes=false인데 프리팹이 다시 구워지지 않았을 수 있습니다.");
            Assert.IsNull(right, $"{LogPrefix} 캐릭터 계층에 눈이 남아 있습니다: {right}");

            // 대조군 — 탐색 도구 자체가 동작한다는 증거(이게 없으면 "무엇을 찾아도 null"인 도구로
            // 조용히 통과할 수 있다).
            Assert.IsNotNull(FindAnywhere(_agent.transform, "HeadOutline"),
                $"{LogPrefix} 탐색 도구가 'HeadOutline'조차 못 찾습니다 — 이 테스트는 아무것도 증명하지 못합니다.");
            Assert.IsNotNull(FindAnywhere(_agent.transform, "HeadFill"),
                $"{LogPrefix} 탐색 도구가 'HeadFill'을 못 찾습니다(대조군 실패).");

            Debug.Log($"{LogPrefix} 눈 없음 확인 — LeftEye/RightEye 부재, HeadFill/HeadOutline 존재.");
        }

        // ============================================================================
        // ③ EyeController는 살아 있고, 눈이 없으면 스스로 조용해진다
        // ============================================================================
        [UnityTest]
        public IEnumerator 눈이_없어도_EyeController가_예외없이_무해하게_동작한다()
        {
            yield return LoadScene();

            EyeController eyes = _agent.Blackboard.GetEyeController();
            Assert.IsNotNull(eyes,
                $"{LogPrefix} GetEyeController()가 null입니다 — 되살리기 경로(EyeController)가 사라졌습니다.");
            Assert.IsFalse(eyes.HasEyes,
                $"{LogPrefix} HasEyes가 참입니다 — 눈이 아직 어딘가에 있습니다.");

            // 전 진입점을 실제로 호출한다. 눈이 없을 때 하나라도 던지면 이 테스트가 잡는다
            // (docs/UX_FLOW.md 38-5-1의 E1~E5 전수).
            Assert.DoesNotThrow(() =>
            {
                eyes.TickLookAt(true, new Vector2(3f, 2f), 0.016f, EyeController.EyeTrackingSettings.Default);
                eyes.TickLookAt(false, Vector2.zero, 0.016f, _agent.Blackboard.BuildEyeTrackingSettings());
                eyes.SetLookDirection(new Vector2(0.8f, -0.3f));
                eyes.SetFacing(-1f);
                eyes.SetFacing(1f);
                eyes.LookForward();
            }, $"{LogPrefix} 눈이 없는 상태에서 EyeController가 예외를 던졌습니다 — " +
               "이 클래스는 눈을 못 찾으면 모든 메서드가 조용히 아무것도 하지 않아야 합니다(null 가드).");

            // 몇 프레임 실제로 돌려 매 프레임 진입점(StickmanBlackboard.TickEyeTracking)도 확인한다.
            for (int i = 0; i < 30; i++) yield return null;

            Debug.Log($"{LogPrefix} EyeController 무해 확인 — HasEyes={eyes.HasEyes}, " +
                $"눈동자오프셋={eyes.CurrentPupilOffset.ToString("F4")}, 시선={eyes.CurrentLookDirection.ToString("F3")}, " +
                $"eyeTrackingEnabled={_config.eyeTrackingEnabled}.");
        }
    }
}
