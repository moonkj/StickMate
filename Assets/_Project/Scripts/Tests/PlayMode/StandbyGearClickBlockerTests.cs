using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>P0 회귀 — 「대기 톱니」가 없는 동안 클릭 차단막이 화면을 가로지르지 않는가</b> (2026-09-05).
    ///
    /// ============================================================================
    /// 무엇이 터졌었나 (test-engineer 실측)
    /// ============================================================================
    /// 톱니가 「평상시 숨김」이 되면서 <c>IconScreenRect</c>는 <b>넓이 0</b>이 됐지만
    /// <b>위치는 화면 우상단 그대로</b>다. 차단막은 <c>Union(톱니 사각형, 부채꼴 사각형)</c>인데
    /// <b>경계상자</b> 합집합은 넓이가 0이어도 그 점의 <b>위치</b>를 함께 삼킨다.
    /// ⇒ 캐릭터 위에서 연 부채꼴과 화면 우상단을 잇는 <b>보이지 않는 띠</b>가 차단막이 됐다.
    /// 실측 640×480에서 정당 면적의 <b>2.31배</b>, 화면의 13.3%를 초과로 흡수. <b>원칙 2 정면 위반</b>이고,
    /// 화면에 아무것도 안 보이므로 사용자는 원인을 절대 못 찾는다.
    ///
    /// ============================================================================
    /// 이 픽스처가 다른 톱니 픽스처와 다른 점 — <b>게이트 우회를 스스로 끈다</b>
    /// ============================================================================
    /// <c>GlobalPlayModeTestIsolation</c>이 이 어셈블리 전체에 대기 톱니 게이트를 우회해 둔다
    /// (그 픽스처들은 「톱니가 보인다」를 전제로 쓰였고 그 전제는 그들의 주제가 아니다).
    /// <b>여기서는 그 우회가 곧 버그를 감춘다</b> — 우회 중에는 <c>IconScreenRect</c>가 넓이를 가지므로
    /// 문제의 「넓이 0」 경로를 아예 타지 않는다. 그래서 이 픽스처만 <b>우회를 끄고</b> 재고,
    /// 끝나면 <b>반드시 되돌린다</b>(정적이라 다음 픽스처로 샌다).
    /// </summary>
    public sealed class StandbyGearClickBlockerTests
    {
        private InfoGearIconWidget _gear;
        private GearRadialMenuWidget _menu;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFile()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다.");
        }

        [SetUp]
        public void TurnGateBackOn()
        {
            // ★ 이 픽스처는 <b>진짜 게이트</b>를 재야 한다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [TearDown]
        public void RestoreSuiteWideBypass()
        {
            // 어셈블리 공용 상태로 되돌린다 — 이 줄이 없으면 뒤따르는 톱니 픽스처가 전부 무너진다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 InfoGearIconWidget 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            _gear = found[0];
            _menu = _gear.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(_menu, "부채꼴 위젯을 찾지 못했습니다.");
            yield return null;
        }

        /// <summary>부채꼴이 실제로 자리를 잡을 때까지 <b>벽시계</b>로 기다린다(프레임 수 대기 금지).</summary>
        private static IEnumerator WaitWallClock(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest]
        public IEnumerator 평상시에는_톱니가_서_있지_않는다()
        {
            yield return LoadSceneAndResolve();
            yield return WaitWallClock(0.2f);

            Assert.IsFalse(_gear.IsStandbyGearVisible,
                $"평상시에 대기 톱니가 서 있습니다(사유={_gear.StandbyGearReason}) — " +
                "2026-09-05 사용자 지시는 «스크류 모양을 없애고 캐릭터 우클릭으로»였습니다.");
            Assert.IsFalse(_gear.IsIconVisible, "판정은 «숨김»인데 그림이 켜져 있습니다 — 배치 경로가 갈라졌습니다.");

            // ★ 양성 대조 — 게이트가 «항상 false»라서 통과한 것이 아님을 같은 실행 안에서 보인다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
            yield return WaitWallClock(0.2f);
            Assert.IsTrue(_gear.IsStandbyGearVisible, "우회를 켰는데도 톱니가 서지 않습니다 — 측정기가 죽었습니다.");
            Assert.IsTrue(_gear.IsIconVisible);
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [UnityTest]
        public IEnumerator 톱니가_없을_때_차단막은_부채꼴만_덮는다()
        {
            yield return LoadSceneAndResolve();
            yield return WaitWallClock(0.2f);

            Assume.That(_gear.IsStandbyGearVisible, Is.False,
                "이 검사는 「대기 톱니가 없는 동안」을 재는 것입니다.");

            // 부채꼴을 <b>화면 반대편</b>(우상단 톱니 자리에서 먼 곳)에 연다 — 그래야 옛 버그의
            // «둘을 잇는 띠»가 실제로 커진다. 좌하단을 고른 이유가 그것이다.
            Vector2 farAnchor = new Vector2(Screen.width * 0.2f, Screen.height * 0.2f);
            _menu.Expand(farAnchor, GearMenuAnchorSource.Character, GearRadialMenuWidget.FanUpBiasPoints);
            yield return WaitWallClock(GearRadialMenuWidget.ExpandTotalSeconds + 0.25f);

            Assert.IsTrue(_menu.IsVisible, "부채꼴이 펼쳐지지 않았습니다 — 아래 측정이 무의미합니다.");

            Rect blocker = _gear.InteractiveScreenRect;
            Rect fan = _menu.UnionScreenRect;

            // ★ 측정 전제 — 부채꼴 사각형이 실제로 넓이를 갖는다.
            Assert.Greater(fan.width * fan.height, 1f, "부채꼴 사각형이 비었습니다 — 측정 전제가 깨졌습니다.");

            // 본 단언: 차단막 = 부채꼴 사각형. 옛 버그에서는 여기가 우상단까지 늘어났다.
            Assert.AreEqual(fan.xMin, blocker.xMin, 0.5f, "차단막 왼쪽이 부채꼴과 다릅니다.");
            Assert.AreEqual(fan.yMin, blocker.yMin, 0.5f, "차단막 아래쪽이 부채꼴과 다릅니다.");
            Assert.AreEqual(fan.xMax, blocker.xMax, 0.5f,
                "★ 차단막 오른쪽이 부채꼴을 넘어섭니다 — 넓이 0인 톱니 사각형의 <b>위치</b>가 " +
                "합집합에 샜습니다(P0). 화면 우상단까지 이어지는 보이지 않는 클릭 흡수 띠입니다.");
            Assert.AreEqual(fan.yMax, blocker.yMax, 0.5f,
                "★ 차단막 위쪽이 부채꼴을 넘어섭니다 — 같은 P0입니다.");

            // ★ 음성 대조 — 옛 식(무조건 Union)이었다면 이 화면에서 실제로 <b>더 컸을</b> 것이다.
            //   그 차이가 0이면 이 테스트는 아무것도 구분하지 못한 것이므로 함께 단언한다.
            Rect wouldHaveBeen = Rect.MinMaxRect(
                Mathf.Min(fan.xMin, _gear.IconScreenCenter.x), Mathf.Min(fan.yMin, _gear.IconScreenCenter.y),
                Mathf.Max(fan.xMax, _gear.IconScreenCenter.x), Mathf.Max(fan.yMax, _gear.IconScreenCenter.y));
            Assert.Greater(wouldHaveBeen.width * wouldHaveBeen.height, fan.width * fan.height * 1.2f,
                "옛 식과 새 식의 면적 차이가 20% 미만입니다 — 이 배치에서는 버그가 드러나지 않으므로 " +
                "앵커를 톱니에서 더 먼 곳으로 옮겨 다시 재세요(측정이 무효입니다).");

            _menu.Collapse(GearMenuCollapseMode.User, "테스트 정리");
            yield return WaitWallClock(GearRadialMenuWidget.CollapseUserSeconds + 0.2f);
        }

        [UnityTest]
        public IEnumerator 부채꼴이_접히면_차단막이_꺼진다()
        {
            yield return LoadSceneAndResolve();
            yield return WaitWallClock(0.2f);

            Assume.That(_gear.IsStandbyGearVisible, Is.False);

            _menu.Expand(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                GearMenuAnchorSource.Character, GearRadialMenuWidget.FanUpBiasPoints);
            yield return WaitWallClock(GearRadialMenuWidget.ExpandTotalSeconds + 0.25f);
            Assert.IsTrue(_gear.IsClickBlockerEnabled, "부채꼴이 떠 있는데 차단막이 꺼져 있습니다 — 버튼 클릭이 밑으로 샙니다.");

            _menu.Collapse(GearMenuCollapseMode.User, "테스트 — 사용자가 닫았다");
            yield return WaitWallClock(GearRadialMenuWidget.CollapseUserSeconds + 0.3f);

            Assert.IsFalse(_gear.IsClickBlockerEnabled,
                "★ 부채꼴도 톱니도 없는데 차단막이 켜져 있습니다 — 화면 한복판에 영구 클릭 흡수 구역이 " +
                "남습니다(원칙 2 정면 위반).");
        }
    }
}
