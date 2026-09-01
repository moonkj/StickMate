using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 우상단 기어(Interaction/InfoGearIconWidget.cs) 회귀 테스트 — 2026-08-30
    /// 사용자 요청("클릭하면 큰기어와 작은기어가 맞물려 움직이면서").
    ///
    /// ============================================================================
    /// ★★ 2026-09-01 — 맞물림 단언 2건을 <b>Ignore로 내렸다</b>. 지우지 않았다.
    /// ============================================================================
    /// P0-3(톱니가 어떤 배경에서도 보이게)을 구현하려면 잉크 뒤에 <b>역상 헤일로</b>를 깔아야 하고,
    /// 그 헤일로는 획의 2.2배(3.74pt)를 먹는다. 그런데 옛 작은 기어의 이 골은 <b>1.68pt</b>뿐이라
    /// (이 높이 − 획 = −0.02pt: 톱니가 물리적으로 없었다) 헤일로를 한 겹도 넣을 수 없었다.
    /// 두 기어를 유지한 채 헤일로를 넣으려면 묶음을 2.82배(bbox 약 102pt)로 키워야 한다 —
    /// 화면 구석 아이콘으로 성립하지 않는다. 그래서 <b>단일 기어</b>가 됐다.
    /// (근거 전문: docs/UI_SURFACE_SPEC.md §5.1~§5.2 + InfoGearIconWidget 형태 상수 블록)
    ///
    /// <b>왜 파일을 안 지웠나</b>: 위 두 단언은 <b>사용자가 직접 요청한 연출</b>을 잠그고 있었다.
    /// 삭제하면 "그런 요청이 있었다"는 사실이 저장소에서 사라진다. CLAUDE.md의 관례대로
    /// <c>Assert.Ignore</c>로 남겨 러너에 "건너뜀"으로 계속 보이게 한다 — 두 기어를 되살리기로
    /// 결정하면 이 두 메서드의 Ignore 한 줄만 지우면 그대로 다시 잠긴다.
    ///
    /// 지금도 <b>살아서 잠그는 것</b>: 회전 연출이 죽지 않았는가, 그리고 비침해 원칙(히트 사각형이
    /// 아이콘만 덮고 화면 대부분은 밖인가).
    /// </summary>
    public sealed class InfoGearMeshingTests
    {
        private InfoGearIconWidget _gear;

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 InfoGearIconWidget 개수가 {found.Length}개입니다 — 1개여야 합니다(2개면 중복 배치).");
            _gear = found[0];
        }

        [Test]
        public void GearRatioAndCenterDistanceFollowRealGearGeometry()
        {
            Assert.Ignore("2026-09-01 P0-3: 역상 헤일로(획 × 2.2 = 3.74pt)가 옛 작은 기어의 이 골(1.68pt)에 " +
                          "물리적으로 들어가지 않아 단일 기어로 바꿨다. 두 기어를 되살리면 이 Ignore 한 줄만 " +
                          "지우면 된다(클래스 문서 참고).");
        }

        [UnityTest]
        public IEnumerator TwoGearsSpinInOppositeDirectionsAtTheToothRatio()
        {
            Assert.Ignore("2026-09-01 P0-3: 작은 기어가 없어졌다(위 메서드의 사유와 같다). " +
                          "회전 연출 자체가 살아 있는지는 아래 SpinAnimationStillTurnsTheGear가 잠근다.");
            yield break;
        }

        /// <summary>맞물림은 사라졌지만 <b>"클릭하면 기어가 회전한다"</b>는 원래 사용자 요청
        /// (2026-08-29 "클릭하면 기어가 회전하면서")의 핵심은 그대로다 — 그것만 따로 잠근다.</summary>
        [UnityTest]
        public IEnumerator SpinAnimationStillTurnsTheGear()
        {
            yield return LoadSceneAndResolve();

            _gear.StartSpinForTests();
            yield return null;
            yield return null;

            float start = _gear.GearAngleDegrees;

            // ★ 프레임 수가 아니라 <b>벽시계</b>로 기다린다(CLAUDE.md: 배치모드 PlayMode는 2,000fps를
            //   넘겨서 "N프레임"이 0.01초밖에 안 되는 경우가 있다).
            float deadline = Time.realtimeSinceStartup + 0.10f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            float delta = Mathf.Abs(Mathf.DeltaAngle(start, _gear.GearAngleDegrees));
            Assert.Greater(delta, 0.5f,
                $"기어가 거의 돌지 않았습니다(Δ={delta:F2}도) — 회전 연출이 죽어 있습니다.");
        }

        [UnityTest]
        public IEnumerator HitRectCoversTheGearAndNothingElse()
        {
            yield return LoadSceneAndResolve();
            yield return null;

            Rect rect = _gear.IconScreenRect;
            Assert.Greater(rect.width, 0f, "히트 사각형이 아직 계산되지 않았습니다.");

            // 기어 중심은 당연히 안에 있어야 하고,
            Assert.IsTrue(rect.Contains(_gear.IconScreenCenter), "기어 중심이 히트 사각형 밖입니다.");

            // 화면 대부분은 밖이어야 한다(비침해 원칙 — 이 작은 영역만 클릭관통이 풀린다).
            float screenArea = Screen.width * (float)Screen.height;
            float rectArea = rect.width * rect.height;
            Assert.Less(rectArea / screenArea, 0.02f,
                $"히트 사각형이 화면의 {rectArea / screenArea * 100f:F1}%를 덮습니다 — " +
                "이 영역만큼 클릭관통이 풀리므로 필요한 최소 크기여야 합니다.");

            Assert.IsFalse(rect.Contains(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)),
                "화면 한가운데가 히트 사각형 안입니다 — 비침해 원칙 위반입니다.");
            Assert.IsFalse(rect.Contains(new Vector2(10f, 10f)), "화면 좌하단이 히트 사각형 안입니다.");
        }
    }
}
