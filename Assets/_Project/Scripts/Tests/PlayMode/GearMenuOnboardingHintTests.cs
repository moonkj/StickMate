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
    ///  ③ <b>안내가 떠 있는 동안에는 자동 접힘이 돌지 않는다</b>(2026-09-03 추가).
    ///     ①과 ②만 잠그면 "떴다"와 "읽을 수 있었다"가 갈라진 채로 초록이 된다 — 실제로 그랬다.
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

        // ==================== ③ 안내가 떠 있는 동안에는 접히지 않는다 ====================

        /// <summary>
        /// ★ 2026-09-03 — <b>안내를 읽는 동안 6초 카운트다운이 돌고 있었다.</b>
        ///
        /// <para>고치기 전의 시계 두 개는 <c>GearRadialMenuWidget.Expand()</c> <b>같은 함수 안에서</b>
        /// 같은 순간에 출발했다(<c>_idleTimer = 0f</c> 바로 뒤가 <c>TryStartOnboardingHint()</c>다).
        /// 톱니를 막 누른 사람의 커서는 <b>톱니 위</b>에 있는데 톱니는 어떤 클램프 상자에도 없으므로
        /// <c>KeepAlive()</c>가 한 번도 불리지 않는다 — 즉 "커서를 올리면 이름이 보여요"를 읽는 시간이
        /// 통째로 접힘 예산에서 빠져나갔다. 그리고 "봤다"는 <b>뜨는 순간</b> 디스크에 적히므로
        /// 놓치면 이 컴퓨터에서 <b>영원히 다시 안 뜬다</b>.</para>
        ///
        /// <para><b>이 테스트가 무엇으로 갈라내는가</b> — 벽시계 두 관문이다(프레임 수 기준 금지:
        /// 이 저장소의 배치모드 PlayMode는 2,000fps 이상이라 프레임 예산은 0.01초가 될 수 있다).</para>
        /// <list type="table">
        ///   <item><term>관문 A</term><description>
        ///     <c>ExpandTotalSeconds + AutoCollapseIdleSeconds</c> = <b>고치기 전의 접힘 시각</b>을
        ///     지나서도 부채꼴이 살아 있는가. 고치기 전 코드는 여기서 이미 사라져 있으므로
        ///     <b>이 관문은 수정 없이는 통과할 수 없다</b>.</description></item>
        ///   <item><term>관문 B</term><description>
        ///     <c>OnboardingHintSeconds + AutoCollapseIdleSeconds</c>를 지나면 <b>결국 접힌다</b>.
        ///     멈춘 것이지 <b>끈 것이 아니다</b> — 이 관문이 없으면 "영원히 안 접힘"이 초록이 된다.
        ///   </description></item>
        /// </list>
        ///
        /// <para>★ <b>거짓 통과 대조 2건을 같은 테스트 안에 박아 둔다</b>:
        /// (가) 안내가 실제로 화면에 보이는가 — 안 보이면 이 테스트는 아무것도 안 재고 초록이 된다.
        /// (나) 세워 둔 커서가 <c>ContainsCursor</c> 밖인가 — 안이면 <c>KeepAlive</c>가 매 프레임 돌아
        /// 관문 A가 <b>수정과 무관하게</b> 통과한다(그건 안내 정지가 아니라 커서를 잰 것이다).</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheFanDoesNotAutoCollapseWhileTheFirstRunHintIsOnScreen()
        {
            // 관문 A가 고치기 전/후를 구분하려면 "안내가 펼침보다 오래 남아 있어야" 한다.
            // 상수가 뒤집히면 이 테스트는 조용히 무의미해지므로 그 전에 시끄럽게 깨뜨린다.
            Assert.Less(GearRadialMenuWidget.ExpandTotalSeconds, GearRadialMenuWidget.OnboardingHintSeconds,
                $"{LogPrefix} 펼침({GearRadialMenuWidget.ExpandTotalSeconds:F3}초)이 " +
                $"안내({GearRadialMenuWidget.OnboardingHintSeconds:F2}초)보다 길어졌습니다 — " +
                "관문 A가 더 이상 수정 전/후를 구분하지 못합니다. 이 테스트를 다시 설계해야 합니다.");

            GearRadialMenuWidget.ResetOnboardingHintForTests();
            yield return LoadScene();

            // 부채꼴은 우상단에 편다. 커서는 그 반대편 구석에 세워 둔다 — 자동 접힘을 재려면
            // KeepAlive가 한 번도 불리지 않아야 한다(아래 대조 (나)가 그것을 확인한다).
            // 톱니가 없으면 실제 OS 커서가 그대로 쓰이고, 그러면 이 테스트는 "커서를 세웠다"는
            // 전제 자체를 잃는다 — 조용히 넘어가지 않고 여기서 멈춘다.
            Assert.IsNotNull(_gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없어 커서를 세울 수 없습니다 — " +
                "실제 OS 커서가 부채꼴 위에 있으면 KeepAlive가 돌아 이 테스트가 거짓 통과합니다.");
            var parked = new Vector2(4f, 4f);
            _gear.FeedHoverCursorForTests(parked);

            float t0 = Time.realtimeSinceStartup;
            _fan.Expand(new Vector2(Screen.width - 60f, Screen.height - 90f));
            Assert.IsTrue(_fan.IsExpanded, $"{LogPrefix} 부채꼴이 펼쳐지지 않았습니다.");

            // ---- 대조 (가): 안내가 실제로 떠 있어야 이 테스트가 무언가를 재는 것이 된다 ----
            yield return WaitUntilElapsed(t0, GearRadialMenuWidget.HoverLabelFadeSeconds + 0.25f);
            Assert.AreEqual(GearRadialMenuWidget.OnboardingHintText, _fan.VisibleOnboardingHint,
                $"{LogPrefix} 안내가 안 떠 있습니다 — 이 테스트는 \"안내 중 접힘 금지\"를 재는데 " +
                "잴 대상이 없으면 아래 관문이 전부 거짓 통과가 됩니다.");

            // ---- 대조 (나): 커서가 클램프 상자 밖이어야 자동 접힘 시계가 실제로 돈다 ----
            Assert.IsFalse(_fan.ContainsCursor(parked),
                $"{LogPrefix} 세워 둔 커서{parked}가 부채꼴 클램프 상자 안입니다 — KeepAlive가 매 프레임 " +
                "돌아 관문 A가 수정과 무관하게 통과합니다(안내 정지가 아니라 커서를 잰 것이 됩니다).");

            // ==================== 관문 A — 고치기 전이면 여기서 이미 사라져 있다 ====================
            float unfixedCollapseAt =
                GearRadialMenuWidget.ExpandTotalSeconds + GearRadialMenuWidget.AutoCollapseIdleSeconds;
            yield return WaitUntilElapsed(t0,
                unfixedCollapseAt + GearRadialMenuWidget.CollapseAutoSeconds + 0.25f);

            Assert.IsTrue(_fan.IsExpanded,
                $"{LogPrefix} 안내가 떠 있는 동안 부채꼴이 자동으로 접혔습니다" +
                $"(펼침 {GearRadialMenuWidget.ExpandTotalSeconds:F3} + 무반응 " +
                $"{GearRadialMenuWidget.AutoCollapseIdleSeconds:F1}초 = " +
                $"{unfixedCollapseAt:F2}초). 앱이 \"{GearRadialMenuWidget.OnboardingHintText}\"라고 " +
                "말해 놓고 그 문장을 읽는 동안 카운트다운을 멈추지 않은 것이고, \"봤다\"는 이미 " +
                "디스크에 적혔으므로 이 사용자는 안내를 영원히 다시 못 봅니다.");

            // ==================== 관문 B — 멈춘 것이지 끈 것이 아니다 ====================
            float heldCollapseAt =
                GearRadialMenuWidget.OnboardingHintSeconds + GearRadialMenuWidget.AutoCollapseIdleSeconds;
            yield return WaitUntilElapsed(t0,
                heldCollapseAt + GearRadialMenuWidget.CollapseAutoSeconds + 1.5f);

            Assert.IsFalse(_fan.IsVisible,
                $"{LogPrefix} 안내가 끝난 뒤에도 부채꼴이 접히지 않았습니다 — 안내 " +
                $"{GearRadialMenuWidget.OnboardingHintSeconds:F1}초가 지나면 무반응 " +
                $"{GearRadialMenuWidget.AutoCollapseIdleSeconds:F1}초 시계가 정상으로 돌아와야 합니다" +
                $"(합계 {heldCollapseAt:F1}초). 자동 접힘을 멈춘 것이 아니라 꺼 버린 상태입니다.");
        }

        /// <summary>★ <b>벽시계</b> 대기 — 프레임 수로 세지 않는다. 이 저장소의 배치모드 PlayMode는
        /// 2,000fps 이상으로 돌아 "180프레임"이 실제로 0.01~0.08초였던 사고가 있다(CLAUDE.md).</summary>
        private static IEnumerator WaitUntilElapsed(float startRealtime, float seconds)
        {
            while (Time.realtimeSinceStartup - startRealtime < seconds) yield return null;
        }
    }
}
