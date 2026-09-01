using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 부채꼴 <b>최초 1회 안내</b> — 2026-09-01 페르소나(민지) M13.
    ///
    /// ============================================================================
    /// 이건 새 아이디어가 아니라 <b>미지급 부채</b>였다
    /// ============================================================================
    /// <see cref="GearRadialMenuWidget"/> 클래스 문서가 상시 라벨을 지우면서 스스로 적었다:
    /// "지우라는 것은 사용자 지시이므로 지우되 비용은 <b>온보딩 1회 안내</b>(35-2)로 갚는다 —
    /// 아이콘 전용 내비게이션의 정답은 툴팁이 아니라 최초 1회 학습이다." 그런데 그 코드가 없었다
    /// (2026-09-01 grep 0건). 라벨은 지워졌고 대가는 지급되지 않은 채 사용자가 대신 내고 있었다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 두 방향
    /// ============================================================================
    ///  ① 처음 펼치면 <b>뜬다</b>(부채가 실제로 지급된다).
    ///  ② 그 다음부터는 <b>절대 안 뜬다</b>. 이미 아는 사용자에게 또 뜨면 그게 방해다(원칙 2) —
    ///     반복 노출 금지가 이 기능의 나머지 절반이다.
    /// </summary>
    public sealed class GearMenuOnboardingHintTests
    {
        private const string LogPrefix = "[부채꼴온보딩-TEST]";

        private GearRadialMenuWidget _fan;
        private InfoGearIconWidget _gear;
        private bool _seenBefore;

        [OneTimeSetUp]
        public void RememberUserState()
        {
            // 이 컴퓨터의 실제 상태를 되돌려 준다 — 테스트가 사용자의 "이미 봤다"를 지우면 안 된다.
            _seenBefore = GearRadialMenuWidget.OnboardingHintSeen;
        }

        [OneTimeTearDown]
        public void RestoreUserState()
        {
            if (_seenBefore) GearRadialMenuWidget.MarkOnboardingHintSeenForTests();
            else GearRadialMenuWidget.ResetOnboardingHintForTests();
        }

        [UnityTearDown]
        public IEnumerator CollapseFan()
        {
            if (_gear != null) _gear.ClearHoverCursorForTests();
            if (_fan != null && _fan.IsVisible) _fan.Collapse(GearMenuCollapseMode.User, "테스트 정리");
            _fan = null;
            _gear = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fan = Object.FindFirstObjectByType<GearRadialMenuWidget>();
            Assert.IsNotNull(_fan, $"{LogPrefix} 씬에 GearRadialMenuWidget이 없습니다.");

            // 호버의 소유자는 톱니 위젯의 폴링이다. 실제 커서가 우연히 버튼 위에 있으면 안내가 곧바로
            // 물러나므로(그게 옳은 동작이다), 커서를 부채꼴에서 먼 곳에 못 박아 두고 시작한다.
            _gear = Object.FindFirstObjectByType<InfoGearIconWidget>();
            if (_gear != null)
                _gear.FeedHoverCursorForTests(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private IEnumerator ExpandFan()
        {
            _fan.Expand(new Vector2(Screen.width - 60f, Screen.height - 90f));
            Assert.IsTrue(_fan.IsExpanded, $"{LogPrefix} 부채꼴이 펼쳐지지 않았습니다.");
            // 알약 페이드(0.09초)가 끝날 때까지.
            yield return new WaitForSecondsRealtime(GearRadialMenuWidget.HoverLabelFadeSeconds + 0.25f);
        }

        // ==================== ① 처음에는 뜬다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheHintAppearsOnTheVeryFirstExpandAndThenTimesOut()
        {
            GearRadialMenuWidget.ResetOnboardingHintForTests();
            yield return LoadScene();
            yield return ExpandFan();

            Assert.AreEqual(GearRadialMenuWidget.OnboardingHintText, _fan.VisibleOnboardingHint,
                $"{LogPrefix} 처음 펼쳤는데 안내가 뜨지 않았습니다 — 상시 라벨을 지운 대가(36-4가 약속한 " +
                "35-2 온보딩)가 여전히 미지급입니다.");
            Assert.IsTrue(GearRadialMenuWidget.OnboardingHintSeen,
                $"{LogPrefix} 안내는 떴는데 \"봤다\"가 기록되지 않았습니다 — 다음 실행에 또 뜹니다.");
            Assert.IsEmpty(_fan.VisibleHoverLabel,
                $"{LogPrefix} 안내와 호버 이름표가 동시에 보입니다(\"{_fan.VisibleHoverLabel}\") — " +
                "화면에 글자는 한 번에 하나뿐이라는 규칙이 깨졌습니다.");

            // 스스로 물러난다(4.5초). 남아 있으면 그건 안내가 아니라 배너다.
            yield return new WaitForSecondsRealtime(
                GearRadialMenuWidget.OnboardingHintSeconds + GearRadialMenuWidget.HoverLabelFadeSeconds + 0.5f);

            Assert.IsEmpty(_fan.VisibleOnboardingHint,
                $"{LogPrefix} {GearRadialMenuWidget.OnboardingHintSeconds:F1}초가 지났는데 안내가 " +
                "화면에 남아 있습니다.");
        }

        // ==================== ② 두 번째부터는 안 뜬다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheHintNeverComesBackOnceSeen()
        {
            GearRadialMenuWidget.MarkOnboardingHintSeenForTests();
            yield return LoadScene();
            yield return ExpandFan();

            Assert.IsEmpty(_fan.VisibleOnboardingHint,
                $"{LogPrefix} 이미 본 사용자에게 안내가 또 떴습니다(\"{_fan.VisibleOnboardingHint}\") — " +
                "반복 노출은 안내가 아니라 방해입니다(원칙 2).");

            // 접었다 다시 펴도 같다.
            _fan.Collapse(GearMenuCollapseMode.User, "테스트");
            yield return new WaitForSecondsRealtime(GearRadialMenuWidget.CollapseUserSeconds + 0.2f);
            yield return ExpandFan();

            Assert.IsEmpty(_fan.VisibleOnboardingHint,
                $"{LogPrefix} 접었다 다시 펴자 안내가 되살아났습니다.");
        }
    }
}
