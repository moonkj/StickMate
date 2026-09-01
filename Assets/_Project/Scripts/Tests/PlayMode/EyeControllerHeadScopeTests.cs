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
    /// ★ 2026-08-30 R3 m2의 실측 잠금 — <see cref="EyeController"/>가 <b>캐릭터 루트 직속 "Head"의
    /// 직속 자식</b>에서만 눈을 찾는지.
    ///
    /// ============================================================================
    /// 무엇을 막는 테스트인가
    /// ============================================================================
    /// 같은 날 캐릭터의 머리·몸통을 <b>영원히 얼렸던</b> 회귀는 "캐릭터 루트 밑에 붙은 UI 캔버스가
    /// 'Head'라는 이름의 자손을 만들었고, 이름으로 파츠를 찾는 코드가 자손 전체를 훑어 그걸 집었다"였다.
    /// <see cref="EyeController"/> 생성자는 그 수정 직전의 <see cref="StickmanPoseAnimator"/>와
    /// <b>글자 그대로 같은 형태</b>(자손 전체 순회 + break 없음 + 마지막 일치 채택)로 남아 있었고,
    /// <c>_head = _leftEye.parent</c>로 이어지므로 오염되면 눈동자 오프셋이 해석되는 <b>기준 좌표계
    /// 자체</b>가 UI RectTransform으로 넘어간다(눈이 안 움직이는 정도가 아니라 엉뚱한 공간에서 움직인다).
    ///
    /// ============================================================================
    /// 관측 방법 — 미끼(decoy)를 실제로 심는다
    /// ============================================================================
    /// "지금은 LeftEye라는 이름을 쓰는 UI가 없으니 괜찮다"는 검증이 아니다. 캐릭터 루트 <b>밑에</b>
    /// UI 캔버스를 흉내낸 미끼 계층("Decoy/Head/LeftEye","RightEye")을 실제로 만들어 두고, 그 상태에서
    /// <b>새 EyeController를 만들어</b> 진짜 눈을 잡는지 잰다. 미끼는 루트 순서상 진짜 Head보다
    /// <b>뒤에</b>(마지막 일치가 되도록) 붙인다 — 옛 코드였다면 반드시 미끼가 이긴다.
    ///
    /// 절대 조건(플래그가 아니라 실측값):
    ///  ① 미끼가 있어도 <see cref="EyeController.HasEyes"/>가 참이다.
    ///  ② <see cref="EyeController.MeasuredSafePupilOffset"/>이 미끼 없을 때와 <b>완전히 같다</b> —
    ///     이 값은 진짜 머리의 "HeadOutline" 링 반지름에서 유도되므로, 미끼(링이 없다)를 잡았다면
    ///     기본 폴백값으로 바뀌어 반드시 달라진다.
    ///  ③ <see cref="EyeController.GeometryScale"/>이 1.0에서 유의미하게 벗어나 있다(배포 배율 0.75).
    ///     폴백 경로는 정확히 1.0을 남기므로, 이 단언이 "실측에 성공했다"의 증거가 된다.
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — <b>보류(Ignore) 상태다. 삭제하지 않았다.</b>
    /// ============================================================================
    /// 그림체 전환 P1(docs/UX_FLOW.md 38-4/38-5)에서 캐릭터의 눈이 삭제되어
    /// (<c>Editor/SceneBootstrapper.BakeEyes = false</c>) 이 테스트의 <b>관측 대상 자체가 씬에 없다</b>.
    /// 첫 단언 <c>baseline.HasEyes</c>부터 성립하지 않는다.
    ///
    /// 그런데 이 테스트가 잠그던 회귀("이름으로 파츠를 찾을 때 자손 전체를 훑어 UI 미끼를 집는다")는
    /// <b>눈과 무관하게 여전히 유효한 불변식</b>이고, 눈을 되살리는 순간(상수 3개 되돌리기 —
    /// <c>SceneBootstrapper.BakeEyes</c> 문서의 절차) <b>그대로 다시 필요해진다</b>. 그래서 지우지 않고
    /// <c>[Ignore]</c>만 건다. 되살리는 절차의 마지막 단계는 <b>아래 Ignore 한 줄을 지우는 것</b>이다.
    ///
    /// (같은 회귀의 <c>StickmanPoseAnimator</c> 쪽 잠금은 눈과 무관하므로 계속 돌고 있다.)
    /// </summary>
    public sealed class EyeControllerHeadScopeTests
    {
        private const string LogPrefix = "[눈탐색범위-TEST]";

        private GameObject _decoy;

        [TearDown]
        public void RemoveDecoy()
        {
            if (_decoy != null) Object.DestroyImmediate(_decoy);
            _decoy = null;
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Transform ResolveCharacterRoot()
        {
            var agents = Object.FindObjectsByType<StickmanAgent>(FindObjectsSortMode.None);
            Assert.AreEqual(1, agents.Length, $"{LogPrefix} 씬의 StickmanAgent 개수가 {agents.Length}개입니다.");
            Assert.IsNotNull(agents[0].Blackboard, $"{LogPrefix} Blackboard가 없습니다.");
            Assert.IsNotNull(agents[0].Blackboard.Body, $"{LogPrefix} Body가 없습니다.");
            return agents[0].Blackboard.Body.transform;
        }

        /// <summary>UI 캔버스가 캐릭터 루트에 달렸을 때의 계층을 흉내낸다 — 루트 밑에 "Head"라는 이름의
        /// <b>손자</b>가 생기고 그 밑에 "LeftEye"/"RightEye"가 있는 형태. 위치는 진짜 눈과 확연히 다르게
        /// 둔다(잘못 잡으면 값으로 드러나도록).</summary>
        private GameObject BuildDecoy(Transform root)
        {
            var decoyRoot = new GameObject("DecoyUiCanvas");
            decoyRoot.transform.SetParent(root, false);
            decoyRoot.transform.SetAsLastSibling();   // 마지막 일치가 되도록 — 옛 코드였다면 이게 이긴다.

            var head = new GameObject("Head");
            head.transform.SetParent(decoyRoot.transform, false);

            var left = new GameObject("LeftEye");
            left.transform.SetParent(head.transform, false);
            left.transform.localPosition = new Vector3(-12.34f, 56.78f, 0f);

            var right = new GameObject("RightEye");
            right.transform.SetParent(head.transform, false);
            right.transform.localPosition = new Vector3(12.34f, 56.78f, 0f);

            return decoyRoot;
        }

        [UnityTest]
        [Ignore("2026-09-01 그림체 전환 P1 — 캐릭터에서 눈이 삭제되어(SceneBootstrapper.BakeEyes=false) " +
                "관측 대상이 씬에 없다. 눈을 되살리면 이 줄만 지우면 그대로 다시 돈다(클래스 문서 참고).")]
        public IEnumerator DecoyHeadInCharacterHierarchyDoesNotStealTheEyes()
        {
            yield return LoadScene();
            Transform root = ResolveCharacterRoot();

            // 기준선 — 미끼가 없는 상태에서 실제로 실측되는 값.
            var baseline = new EyeController(root);
            Assert.IsTrue(baseline.HasEyes, $"{LogPrefix} 기준선에서 눈을 찾지 못했습니다(프리팹 규약이 바뀌었습니까?).");
            Assert.Greater(baseline.MeasuredSafePupilOffset, 0.0001f,
                $"{LogPrefix} 기준선의 눈동자 상한이 0 이하입니다.");
            Assert.Greater(Mathf.Abs(baseline.GeometryScale - 1f), 0.01f,
                $"{LogPrefix} 기준선 GeometryScale이 1.0({baseline.GeometryScale:F4})입니다 — 실측이 아니라 " +
                "폴백 경로를 탄 것 같습니다(배포 배율은 0.75). 이 값이 1.0이면 아래 미끼 단언이 무의미해집니다.");

            float expectedOffset = baseline.MeasuredSafePupilOffset;
            float expectedScale = baseline.GeometryScale;

            // 미끼를 심고 다시 만든다.
            _decoy = BuildDecoy(root);
            yield return null;

            var withDecoy = new EyeController(root);

            Assert.IsTrue(withDecoy.HasEyes,
                $"{LogPrefix} 미끼가 있을 때 눈을 못 찾았습니다.");
            Assert.AreEqual(expectedOffset, withDecoy.MeasuredSafePupilOffset, 1e-6f,
                $"{LogPrefix} 미끼 계층의 'Head'가 진짜 머리를 밀어냈습니다 — 눈동자 상한이 " +
                $"{expectedOffset:F6} -> {withDecoy.MeasuredSafePupilOffset:F6}으로 바뀌었습니다.");
            Assert.AreEqual(expectedScale, withDecoy.GeometryScale, 1e-6f,
                $"{LogPrefix} 미끼 때문에 머리 기준 배율이 {expectedScale:F4} -> {withDecoy.GeometryScale:F4}로 바뀌었습니다.");

            Debug.Log($"{LogPrefix} 통과 — 루트 자손에 'Head/LeftEye/RightEye' 미끼를 심어도 실측값이 " +
                $"눈동자 상한 {withDecoy.MeasuredSafePupilOffset:F4}유닛 / 머리 배율 {withDecoy.GeometryScale:F4}로 " +
                "그대로 유지됩니다(탐색 범위가 '루트 직속 Head의 직속 자식'으로 좁혀져 있습니다).");
        }
    }
}
