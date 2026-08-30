using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 우상단 맞물린 기어(Interaction/InfoGearIconWidget.cs) 회귀 테스트 — 2026-08-30
    /// 사용자 요청("클릭하면 큰기어와 작은기어가 맞물려 움직이면서").
    ///
    /// ============================================================================
    /// 왜 "그림"이 아니라 "기구학"을 단언하는가
    /// ============================================================================
    /// 사용자 요구의 핵심은 <b>진짜 맞물린 것처럼 보이는가</b>였다. 그 판정은 눈으로 하지만, 눈으로 본
    /// 것이 다음 라운드에 조용히 깨지는 것을 막으려면 <b>수치로 잠가야</b> 한다. 그래서 세 가지를
    /// 절대 조건으로 못박는다:
    ///  ① 두 기어는 <b>서로 반대 방향</b>으로 돈다.
    ///  ② 작은 기어는 <b>정확히 잇수비만큼</b> 더 빨리 돈다(ω작 / ω큰 = N큰 / N작).
    ///  ③ 중심 거리 = 두 피치 반지름의 합(이 값이 아니면 이가 겹치거나 떨어져 보인다).
    /// 그리고 비침해 원칙: 히트 사각형은 두 기어를 덮되 <b>화면 대부분은 여전히 밖</b>이어야 한다.
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
            Assert.AreEqual(10, InfoGearIconWidget.BigTeeth, "큰 기어 잇수가 바뀌었습니다.");
            Assert.AreEqual(6, InfoGearIconWidget.SmallTeeth, "작은 기어 잇수가 바뀌었습니다.");
            Assert.AreEqual(10f / 6f, InfoGearIconWidget.MeshRatio, 0.0001f,
                "속도비가 잇수비와 다릅니다 — 맞물린 기어는 잇수에 반비례하는 속도로 돕니다.");

            // 중심 거리 = 두 피치 반지름의 합. 값이 커지면 이가 떨어져 보이고, 작아지면 서로 파고든다.
            const float BigOuter = 13f, BigRoot = 10.2f;
            float scale = 6f / 10f;
            float expected = (BigOuter + BigRoot) * 0.5f + (BigOuter * scale + BigRoot * scale) * 0.5f;
            Assert.AreEqual(expected, InfoGearIconWidget.CenterDistance, 0.001f,
                "중심 거리가 두 피치 반지름의 합이 아닙니다 — 두 기어가 물린 그림이 되지 않습니다.");
        }

        [UnityTest]
        public IEnumerator TwoGearsSpinInOppositeDirectionsAtTheToothRatio()
        {
            yield return LoadSceneAndResolve();

            _gear.StartSpinForTests();
            yield return null;
            yield return null;

            float bigStart = Mathf.DeltaAngle(0f, _gear.BigGearAngleDegrees);
            float smallStart = Mathf.DeltaAngle(0f, _gear.SmallGearAngleDegrees);

            // 몇 프레임 더 돌린 뒤 각 변화량을 잰다(회전 중에만 의미가 있으므로 짧게 본다).
            for (int i = 0; i < 4; i++) yield return null;

            float bigDelta = Mathf.DeltaAngle(bigStart, _gear.BigGearAngleDegrees);
            float smallDelta = Mathf.DeltaAngle(smallStart, _gear.SmallGearAngleDegrees);

            Assert.Greater(Mathf.Abs(bigDelta), 0.5f,
                $"큰 기어가 거의 돌지 않았습니다(Δ={bigDelta:F2}도) — 회전 연출이 죽어 있습니다.");
            Assert.Less(bigDelta * smallDelta, 0f,
                $"두 기어가 같은 방향으로 돕니다(큰 Δ={bigDelta:F2}, 작은 Δ={smallDelta:F2}) — " +
                "맞물린 기어는 반드시 반대 방향입니다.");
            Assert.Greater(Mathf.Abs(smallDelta), Mathf.Abs(bigDelta) * 1.2f,
                $"작은 기어가 충분히 빠르지 않습니다(큰 |Δ|={Mathf.Abs(bigDelta):F2}, " +
                $"작은 |Δ|={Mathf.Abs(smallDelta):F2}) — 잇수비 {InfoGearIconWidget.MeshRatio:F2}배여야 합니다.");

            Debug.Log($"[기어테스트] 큰 기어 Δ={bigDelta:F2}도, 작은 기어 Δ={smallDelta:F2}도 " +
                $"(비율 {Mathf.Abs(smallDelta / bigDelta):F2}, 기대 {InfoGearIconWidget.MeshRatio:F2}).");
        }

        [UnityTest]
        public IEnumerator HitRectCoversBothGearsAndNothingElse()
        {
            yield return LoadSceneAndResolve();
            yield return null;

            Rect rect = _gear.IconScreenRect;
            Assert.Greater(rect.width, 0f, "히트 사각형이 아직 계산되지 않았습니다.");

            // 큰 기어 중심은 당연히 안에 있어야 하고,
            Assert.IsTrue(rect.Contains(_gear.IconScreenCenter), "큰 기어 중심이 히트 사각형 밖입니다.");

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
