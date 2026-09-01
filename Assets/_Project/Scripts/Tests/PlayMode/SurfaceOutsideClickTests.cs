using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>"사용자가 닫기 전에는 안 꺼진다" — 그리고 그 대가를 재는 자</b> (2026-09-02 사용자 신고).
    ///
    /// ============================================================================
    /// 사용자 신고 원문
    /// ============================================================================
    /// <i>"캐릭터창이나 다른 메뉴창들이 떠있을때 바탕화면을 클릭하면 꺼지는데 안꺼지고 사용자가
    /// 닫기전에는 안꺼져야함"</i>
    ///
    /// ============================================================================
    /// ★ 왜 "안 닫힌다"만 재면 안 되는가 — 이게 이 파일의 존재 이유다
    /// ============================================================================
    /// 이 앱은 <b>비침해(절대 불변 원칙 2)</b>를 걸고 있다. 창이 열려 있는 동안 그 패널 사각형에는
    /// 클릭관통 차단막(isTrigger <see cref="BoxCollider2D"/>)이 깔리고, OS 히트테스트
    /// (<c>hitTestType=Raycast</c>)는 <b>커서 아래 Collider2D가 있는지</b>만 보고 클릭을 우리에게 줄지
    /// 밑의 앱에 줄지 결정한다.
    ///
    /// <para>바깥 클릭 탈출구를 없앤 대가로, <b>그 차단막이 사용자가 [✕]를 누를 때까지 남는다</b>.
    /// 종전에는 "탈출 비용 1클릭"이 사실상의 상한이었다. 그래서 남는 유일한 방어선은 하나다:
    /// <b>차단막이 패널 사각형에서 한 픽셀도 넓지 않을 것.</b> 그 선이 무너지면
    /// "안 닫히는 창"이 아니라 <b>"바탕화면 일부를 영구 점거한 앱"</b>이 된다.</para>
    ///
    /// <para><b>★ "닫히지 않는 것"과 "클릭을 먹는 것"은 다른 문제다.</b> 전자는 사용자가 요구한
    /// 것이고, 후자면 원칙 2 위반이다. 이 파일은 그 둘을 <b>따로</b> 잰다.</para>
    ///
    /// ============================================================================
    /// 잠그는 것
    /// ============================================================================
    ///  ① 세 표면(정보창 · 설정창 · 팝오버) 모두 <b>창 밖을 눌러도 열려 있다</b>.
    ///  ② ★ <b>차단막 = 패널 사각형</b>. 콜라이더의 실제 월드 bounds를 화면 좌표로 되돌려
    ///     <see cref="CharacterInfoWindow.PanelScreenRect"/>와 1px 오차로 대조한다.
    ///     ("숫자로 재라" — 패널 밖으로 새는 폭을 <b>픽셀 단위로</b> 남긴다.)
    ///  ③ ★ <b>네거티브 컨트롤 1</b> — 창 밖의 그 좌표에서 차단막이 <b>실제로 비어 있다</b>.
    ///     라이브러리가 쓰는 것과 <b>같은 질의</b>(카메라 레이 → Physics2D)를 우리가 직접 쏴서
    ///     "거기서 걸리는 것이 차단막이 아니다"를 확인한다 = 그 클릭은 밑의 앱으로 간다.
    ///  ④ ★ <b>네거티브 컨트롤 2</b> — [✕]는 여전히 닫고, 닫히면 <b>차단막도 함께 꺼진다</b>.
    ///     이게 없으면 ①~③은 "닫기가 통째로 고장난" 상태에서도 전부 초록이다.
    ///  ⑤ ★ <b>안전망이 무력화되지 않았는가</b> — 바깥을 한 번 클릭한 팝오버도 무입력 자동 닫힘
    ///     (<see cref="PopoverPanel.DefaultIdleAutoCloseSeconds"/> = 180초)으로 <b>여전히</b> 닫힌다.
    ///     <b>정보창/설정창에는 이런 상한이 아예 없다</b>(알고 치르는 대가 — 리더 보고 완료).
    ///
    /// <para><b>상수를 숫자로 베끼지 않는다</b>(CLAUDE.md): 임계는
    /// <see cref="PopoverPanel.IdleAutoCloseSeconds"/>를 <b>참조</b>해서 검증하고, 테스트용으로
    /// 낮춘 값도 프로덕션 세터를 통해 넣는다.</para>
    /// </summary>
    public sealed class SurfaceOutsideClickTests
    {
        private const string LogPrefix = "[바깥클릭-TEST]";

        /// <summary>차단막 사각형과 패널 사각형이 어긋나도 되는 최대 폭(px). 카메라 왕복 변환의
        /// 부동소수 오차만 허용한다 — 1px이면 "레이아웃이 다르다"는 뜻이다.</summary>
        private const float BoundsTolerancePixels = 1f;

        private static readonly Rect AnchorRect = new Rect(400f, 400f, 44f, 44f);

        [TearDown]
        public void RestoreIdleBudget() => PopoverPanel.ResetIdleAutoCloseSecondsForTests();

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ==================== ① + ② + ③ + ④ — 정보창 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator InfoWindowSurvivesOutsideClicksAndItsBlockerNeverLeavesThePanel()
        {
            yield return LoadScene();

            var window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            window.Open("바깥 클릭 검사");
            yield return null;
            yield return null;
            Assume.That(window.IsOpen, Is.True, $"{LogPrefix} 전제: 정보창이 열려야 합니다.");

            // ★ 배치모드의 좁은 화면(640×480)에서는 880×861 창이 화면을 통째로 덮어 "창 밖"이
            //   <b>존재하지 않는다</b>. 첫 작성본은 그 경우 Assert.Ignore로 넘겼는데, 그러면 이 앱에서
            //   가장 큰 표면이 <b>어디에서도 검증되지 않는다</b>(=조용한 구멍). 그래서 프로덕션의
            //   클램프 함수에 배율을 주입해 창을 줄인다 — InfoWindowExclusiveModalTests가 확립한
            //   관례 그대로이고, 줄이는 경로 자체가 프로덕션 코드다.
            Rect panel = window.PanelScreenRect;
            if (!TryFindOutsidePoint(panel, out Vector2 outside))
            {
                Assert.IsNotNull(ClampMethod,
                    $"{LogPrefix} CharacterInfoWindow.ClampPanelToScreen을 찾지 못했습니다 — 이름이 " +
                    "바뀌었습니다. 이 창구가 없으면 좁은 화면에서 이 테스트가 통째로 죽습니다.");
                ClampMethod.Invoke(window, new object[] { ScaleFactorForSmallPanel() });
                yield return null;
                panel = window.PanelScreenRect;
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 좁아 창을 최소 크기로 " +
                    $"줄였습니다 — 줄인 뒤 창 사각형 {panel}.");
            }
            Assert.IsTrue(TryFindOutsidePoint(panel, out outside),
                $"{LogPrefix} 창을 최소 크기({panel})로 줄였는데도 화면({Screen.width}×{Screen.height})에 " +
                "\"창 밖\" 지점이 없습니다 — 그러면 이 테스트가 아무것도 검증하지 못합니다.");

            // ② 차단막 = 패널 사각형.
            AssertBlockerMatchesPanel(window.ClickBlockerWorldBounds, panel, "정보창");

            // ③ 그 바깥 좌표에는 차단막이 없다 = 클릭이 밑의 앱으로 간다.
            AssertBlockerDoesNotCover(window.ClickBlockerWorldBounds, outside, "정보창");

            // ① 눌러도 안 닫힌다.
            window.FeedClickForTests(outside);
            yield return null;
            Assert.IsTrue(window.IsOpen,
                $"{LogPrefix} 창 밖({outside})을 눌렀더니 정보창이 꺼졌습니다 — 사용자 지시는 " +
                "\"사용자가 닫기전에는 안꺼져야함\"입니다.");
            Assert.IsTrue(window.IsClickBlockerEnabled,
                $"{LogPrefix} 창은 열려 있는데 차단막이 꺼졌습니다 — 창 안 클릭이 밑으로 새어 나갑니다.");

            yield return new WaitForSecondsRealtime(0.5f);   // 클릭 중복 억제(0.35초)를 넘긴다.

            // ④ 네거티브 컨트롤 — [✕]는 닫고, 차단막까지 거둔다.
            window.FeedClickForTests(window.CloseButtonScreenRect.center);
            yield return null;
            Assert.IsFalse(window.IsOpen,
                $"{LogPrefix} [✕]를 눌렀는데 정보창이 닫히지 않았습니다 — 바깥 클릭을 없앤 지금 " +
                "이것이 유일한 마우스 탈출구입니다. 이 단언이 없으면 위의 \"열려 있다\"는 공짜 초록입니다.");
            Assert.IsFalse(window.IsClickBlockerEnabled,
                $"{LogPrefix} 창을 닫았는데 차단막이 남았습니다 — 그 화면 조각의 클릭관통이 영영 " +
                "해제된 채 남습니다(원칙 2 위반).");
        }

        // ==================== 설정창 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SettingsWindowSurvivesOutsideClicksAndItsBlockerNeverLeavesThePanel()
        {
            yield return LoadScene();

            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            settings.Open("바깥 클릭 검사");
            yield return null;
            yield return null;
            Assume.That(settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려야 합니다.");

            // ★ 설정창은 720×560 <b>고정</b>이라 줄일 창구가 없다(정보창의 ClampPanelToScreen 같은 것이
            //   없다). 배치모드의 좁은 화면에서는 화면 안에 "창 밖"이 존재하지 않는데, 그렇다고
            //   Assert.Ignore로 넘기면 <b>이 창은 어디에서도 검증되지 않는다</b>. 그래서 화면 밖이더라도
            //   <b>패널 사각형 밖</b>의 좌표를 쓴다 — 프로덕션이 실제로 보는 조건이 정확히
            //   "패널 사각형 안인가"이므로(SettingsWindow.FeedClick) 같은 분기를 그대로 지난다.
            //   차이는 "그 자리가 사용자 눈에 보이는가"뿐이고, 실기 해상도(예: 1512×982)에서는 보인다.
            Rect panel = settings.PanelScreenRect;
            Vector2 outside = OutsidePanelPoint(panel, out bool onScreen);
            Assert.IsFalse(panel.Contains(outside),
                $"{LogPrefix} 고른 좌표 {outside}가 패널({panel}) 안입니다 — 전제가 무너졌습니다.");
            if (!onScreen)
            {
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 설정창({panel})보다 좁아 " +
                    $"화면 밖 좌표 {outside}를 씁니다 — 프로덕션 분기(\"패널 안인가\")는 동일하게 지납니다.");
            }

            AssertBlockerMatchesPanel(settings.ClickBlockerWorldBounds, panel, "설정창");
            AssertBlockerDoesNotCover(settings.ClickBlockerWorldBounds, outside, "설정창");

            settings.FeedClickForTests(outside);
            yield return null;
            Assert.IsTrue(settings.IsOpen,
                $"{LogPrefix} 창 밖({outside})을 눌렀더니 설정창이 꺼졌습니다 — 사용자 지시 위반입니다.");
            Assert.IsTrue(settings.IsClickBlockerEnabled,
                $"{LogPrefix} 창은 열려 있는데 차단막이 꺼졌습니다.");

            yield return new WaitForSecondsRealtime(0.5f);

            settings.FeedClickForTests(settings.CloseButtonScreenRect.center);
            yield return null;
            Assert.IsFalse(settings.IsOpen, $"{LogPrefix} [✕]를 눌렀는데 설정창이 닫히지 않았습니다.");
            Assert.IsFalse(settings.IsClickBlockerEnabled,
                $"{LogPrefix} 설정창을 닫았는데 차단막이 남았습니다(원칙 2 위반).");
        }

        // ==================== 팝오버 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PopoverSurvivesOutsideClicksAndItsBlockerNeverLeavesThePanel()
        {
            yield return LoadScene();

            var popover = Object.FindFirstObjectByType<ActionCommandPopover>();
            Assert.IsNotNull(popover, $"{LogPrefix} 씬에 ActionCommandPopover가 없습니다.");
            popover.Open(AnchorRect, "바깥 클릭 검사");
            yield return new WaitForSecondsRealtime(0.3f);   // 자라나는 애니메이션(0.16초)을 넘긴다.
            Assume.That(popover.IsOpen, Is.True, $"{LogPrefix} 전제: 팝오버가 열려야 합니다.");

            Rect panel = popover.PanelScreenRect;
            Vector2 outside = OutsidePanelPoint(panel, out bool onScreen);
            Assert.IsFalse(panel.Contains(outside),
                $"{LogPrefix} 고른 좌표 {outside}가 팝오버({panel}) 안입니다 — 전제가 무너졌습니다.");
            if (!onScreen)
            {
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 팝오버({panel})보다 좁아 " +
                    $"화면 밖 좌표 {outside}를 씁니다 — 프로덕션 분기는 동일하게 지납니다.");
            }

            AssertBlockerMatchesPanel(popover.ClickBlockerWorldBounds, panel, "행동 명령 팝오버");
            AssertBlockerDoesNotCover(popover.ClickBlockerWorldBounds, outside, "행동 명령 팝오버");

            popover.FeedClickForTests(outside);

            // ★★ 여기서 <c>yield return null</c> 한 프레임만 두고 재면 <b>거짓 초록</b>이다.
            //   <c>Close()</c>는 <c>_open</c>을 즉시 내리지 않고 <see cref="PopoverPanel.ShrinkSeconds"/>
            //   동안 접히는 애니메이션을 돌리므로, 한 프레임 뒤의 <c>IsOpen</c>은 <b>닫기가 실제로
            //   걸렸어도 아직 true</b>다. 2026-09-02 네거티브 컨트롤(바깥 클릭 닫기를 일부러 되살려
            //   빨개지는지 확인)이 정확히 이 함정을 잡아냈다 — 세 표면 중 팝오버만 초록이었다.
            //   그래서 접힘 시간의 3배를 <b>벽시계로</b> 기다린 뒤에 본다(숫자는 프로덕션 상수 참조).
            yield return new WaitForSecondsRealtime(PopoverPanel.ShrinkSeconds * 3f + 0.1f);
            Assert.IsTrue(popover.IsOpen,
                $"{LogPrefix} 바깥({outside})을 눌렀더니 팝오버가 꺼졌습니다 — 사용자 지시 위반입니다.");
            Assert.IsTrue(popover.IsClickBlockerEnabled,
                $"{LogPrefix} 팝오버는 열려 있는데 차단막이 꺼졌습니다 — 창 안 클릭이 밑으로 샙니다.");

            yield return new WaitForSecondsRealtime(0.5f);

            popover.FeedClickForTests(popover.CloseButtonScreenRectForTests.center);
            yield return new WaitForSecondsRealtime(PopoverPanel.ShrinkSeconds * 3f + 0.1f);
            Assert.IsFalse(popover.IsOpen, $"{LogPrefix} [✕]를 눌렀는데 팝오버가 닫히지 않았습니다.");
            Assert.IsFalse(popover.IsClickBlockerEnabled,
                $"{LogPrefix} 팝오버를 닫았는데 차단막이 남았습니다(원칙 2 위반).");
        }

        // ==================== ⑤ 안전망이 이 변경으로 무력화되지 않았는가 ====================

        /// <summary>
        /// ★ 바깥 클릭 탈출구를 없앤 뒤 <b>팝오버에 남은 유일한 자동 상한</b>이 무입력 자동 닫힘이다
        /// (<see cref="PopoverPanel.DefaultIdleAutoCloseSeconds"/>). 자동 닫힘 자체는
        /// <see cref="PopoverIdleAutoCloseTests"/>가 이미 잠근다 — <b>여기서 새로 잠그는 것은
        /// 그 안전망이 "창 밖 클릭" 뒤에도 여전히 작동하는가</b>다.
        ///
        /// <para>왜 이게 새 위험인가: <c>FeedClick</c>은 좌표를 보기 <b>전에</b>
        /// <c>NoteUserActivity()</c>로 무입력 시계를 되돌린다. 예전에는 그 다음 줄이 곧바로 창을
        /// 닫아서 아무 상관이 없었지만, 이제는 <b>닫지 않고 돌아간다</b>. 만약 그 경로가 시계를
        /// 되돌리기만 하고 다시 흐르지 않게 만든다면, 바깥을 한 번 클릭한 팝오버는 <b>영영</b>
        /// 남는다 — 차단막까지 함께.</para>
        ///
        /// <para>★ 임계를 숫자로 베끼지 않는다(CLAUDE.md): 프로덕션 세터로 낮추고 그 값을
        /// <b>참조</b>해서 예산을 잡는다. 대기는 전부 벽시계 기준이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OutsideClickDoesNotWedgeThePopoverOpenForever()
        {
            yield return LoadScene();

            Assert.AreEqual(180f, PopoverPanel.DefaultIdleAutoCloseSeconds, 0.001f,
                $"{LogPrefix} 무입력 자동 닫힘 기본값이 바뀌었습니다 — 바깥 클릭 탈출구를 없앤 지금 " +
                "이것이 팝오버에 남은 유일한 자동 상한입니다(정보창/설정창에는 이런 상한이 아예 " +
                "없습니다). 값을 바꾸려면 원칙 2 재판정이 필요합니다.");

            var popover = Object.FindFirstObjectByType<TodoBoardPopover>();
            Assert.IsNotNull(popover, $"{LogPrefix} 씬에 TodoBoardPopover가 없습니다.");

            PopoverPanel.SetIdleAutoCloseSecondsForTests(0.5f);
            float budget = PopoverPanel.IdleAutoCloseSeconds;
            Assert.Less(budget, PopoverPanel.DefaultIdleAutoCloseSeconds,
                $"{LogPrefix} 테스트용 임계 주입이 먹지 않았습니다 — 아래 대기가 의미를 잃습니다.");

            popover.Open(AnchorRect, "바깥 클릭 뒤 안전망 검사");
            yield return new WaitForSecondsRealtime(0.3f);
            Assume.That(popover.IsOpen, Is.True, $"{LogPrefix} 전제: 팝오버가 열려야 합니다.");

            Rect panel = popover.PanelScreenRect;
            Vector2 outside = OutsidePanelPoint(panel, out _);
            Assert.IsFalse(panel.Contains(outside),
                $"{LogPrefix} 고른 좌표 {outside}가 팝오버({panel}) 안입니다 — 전제가 무너졌습니다.");

            popover.FeedClickForTests(outside);
            // 접힘 애니메이션을 넘겨서 본다 — 한 프레임 뒤의 IsOpen은 닫기가 걸렸어도 아직 true다.
            yield return new WaitForSecondsRealtime(PopoverPanel.ShrinkSeconds * 3f + 0.1f);
            Assert.IsTrue(popover.IsOpen,
                $"{LogPrefix} 전제 실패 — 바깥 클릭이 아직 팝오버를 닫고 있습니다(사용자 지시 위반).");

            // 커서를 한 자리에 못 박는다 = 자리 비움(PlayMode는 진짜 OS 커서를 붙잡을 수 없다).
            popover.FeedIdleCursorForTests(panel.center);

            // ★ 벽시계 예산(프레임 수 기반 대기 금지 — CLAUDE.md). 임계의 6배 + 폴링 여유.
            float deadline = Time.realtimeSinceStartup + budget * 6f + 1f;
            while (Time.realtimeSinceStartup < deadline && popover.IsOpen) yield return null;

            Assert.IsFalse(popover.IsOpen,
                $"{LogPrefix} 바깥을 한 번 클릭한 팝오버가 무입력 {budget:F2}초의 6배를 기다려도 " +
                "닫히지 않습니다 — 바깥 클릭이 안전망을 <b>무력화</b>했습니다. 그러면 팝오버와 " +
                "그 차단막이 밤새 남습니다(원칙 2).");
            Assert.IsFalse(popover.IsClickBlockerEnabled,
                $"{LogPrefix} 무입력으로 닫혔는데 차단막이 남았습니다 — 자동 닫힘의 목적 자체가 " +
                "차단막을 거두는 것입니다.");

            popover.ClearIdleCursorForTests();
        }

        // ==================== 도구 ====================

        /// <summary>좁은 화면에서 정보창을 최소 크기로 줄이는 프로덕션 클램프 함수. 배치모드는 화면
        /// 크기를 바꿀 수 없어, 실제 클램프 경로에 배율을 주입해 같은 계산으로 창을 줄인다
        /// (InfoWindowExclusiveModalTests / InfoWindowClippedHitTestTests와 완전히 같은 관례).</summary>
        private static readonly MethodInfo ClampMethod = typeof(CharacterInfoWindow).GetMethod(
            "ClampPanelToScreen", BindingFlags.Instance | BindingFlags.NonPublic);

        private static float ScaleFactorForSmallPanel()
            => Mathf.Max(0.01f, Mathf.Max(Screen.width, Screen.height) / 300f);

        /// <summary>패널 <b>밖</b>의 한 점. 화면 안에서 찾을 수 있으면 그것을 주고(<paramref name="onScreen"/>
        /// = true), 없으면 화면 밖이더라도 패널 밖인 점을 준다 — <b>Ignore로 넘기지 않는다</b>.
        /// 프로덕션이 보는 조건은 "패널 사각형 안인가" 하나이므로 두 경우 모두 같은 분기를 지난다.</summary>
        private static Vector2 OutsidePanelPoint(Rect panel, out bool onScreen)
        {
            if (TryFindOutsidePoint(panel, out Vector2 visible)) { onScreen = true; return visible; }
            onScreen = false;
            return new Vector2(panel.xMin - 40f, panel.center.y);
        }

        /// <summary>
        /// 패널 <b>밖</b>이면서 <b>화면 안</b>인 한 점을 찾는다.
        ///
        /// <para>★ 오프셋을 20px 같은 <b>고정값으로 박지 않는다</b>. 첫 작성본이 그렇게 했다가
        /// 배치모드(640×480)에서 실패했다 — 최소 크기로 줄인 정보창의 여백이 정확히
        /// <c>ScreenMargin</c>(16pt)이라 20px를 빼면 화면 밖으로 나갔다. 남는 틈의 <b>한가운데</b>를
        /// 고르면 어떤 여백에서도 성립한다(틈이 2px만 있어도 된다).</para>
        /// </summary>
        private static bool TryFindOutsidePoint(Rect panel, out Vector2 point)
        {
            const float MinGap = 2f;
            float cy = Mathf.Clamp(panel.center.y, 1f, Screen.height - 1f);
            float cx = Mathf.Clamp(panel.center.x, 1f, Screen.width - 1f);

            if (panel.xMin >= MinGap) { point = new Vector2(panel.xMin * 0.5f, cy); return true; }
            if (Screen.width - panel.xMax >= MinGap)
            {
                point = new Vector2((panel.xMax + Screen.width) * 0.5f, cy);
                return true;
            }
            if (panel.yMin >= MinGap) { point = new Vector2(cx, panel.yMin * 0.5f); return true; }
            if (Screen.height - panel.yMax >= MinGap)
            {
                point = new Vector2(cx, (panel.yMax + Screen.height) * 0.5f);
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>★ 핵심 실측 — 차단막 콜라이더의 <b>월드</b> bounds를 화면 좌표로 되돌려 패널
        /// 사각형과 대조한다. 어긋난 폭을 픽셀로 남긴다("숫자로 재라").</summary>
        private static void AssertBlockerMatchesPanel(Bounds blocker, Rect panel, string surfaceName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"{LogPrefix} 메인 카메라가 없어 차단막을 화면 좌표로 되돌릴 수 없습니다.");
            Assert.Greater(blocker.size.x, 0f,
                $"{LogPrefix} {surfaceName}의 차단막이 비어 있습니다 — 창이 열려 있는데 차단막이 " +
                "꺼져 있으면 창 안 클릭이 밑의 앱으로 새어 나갑니다.");

            Vector3 bl = cam.WorldToScreenPoint(new Vector3(blocker.min.x, blocker.min.y, 0f));
            Vector3 tr = cam.WorldToScreenPoint(new Vector3(blocker.max.x, blocker.max.y, 0f));
            var blockerScreen = Rect.MinMaxRect(
                Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y),
                Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));

            float leakLeft = panel.xMin - blockerScreen.xMin;
            float leakRight = blockerScreen.xMax - panel.xMax;
            float leakBottom = panel.yMin - blockerScreen.yMin;
            float leakTop = blockerScreen.yMax - panel.yMax;
            float worst = Mathf.Max(Mathf.Max(leakLeft, leakRight), Mathf.Max(leakBottom, leakTop));

            Debug.Log($"{LogPrefix} {surfaceName} 차단막 실측 — 패널 {panel}, 차단막 {blockerScreen}, " +
                $"바깥으로 새는 폭(좌/우/하/상) = {leakLeft:F2}/{leakRight:F2}/{leakBottom:F2}/{leakTop:F2}px.");

            Assert.LessOrEqual(worst, BoundsTolerancePixels,
                $"{LogPrefix} {surfaceName}의 차단막이 패널 밖으로 최대 {worst:F2}px 새어 나갑니다 " +
                $"(패널 {panel} / 차단막 {blockerScreen}). 2026-09-02부터 이 사각형은 사용자가 [✕]를 " +
                "누를 때까지 남습니다 — 한 픽셀이라도 넓으면 그만큼의 바탕화면을 영구 점거하는 " +
                "것이고, 그것이 절대 불변 원칙 2가 금지하는 침해입니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 창 밖의 그 좌표를 차단막이 <b>덮지 않는다</b>. 덮지 않으면
        /// OS 히트테스트(<c>hitTestType=Raycast</c>)가 그 클릭을 밑의 앱으로 넘긴다 = 우리가 안 먹는다.
        /// <para>"안 닫힌다"만 재고 이걸 안 재면, 창을 안 닫으면서 <b>클릭까지 삼키는</b> 최악의
        /// 조합이 초록으로 통과한다.</para></summary>
        private static void AssertBlockerDoesNotCover(Bounds blocker, Vector2 screenPoint, string surfaceName)
        {
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"{LogPrefix} 메인 카메라가 없습니다.");

            Vector3 world = cam.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(cam.transform.position.z)));
            var flat = new Vector3(world.x, world.y, blocker.center.z);

            Assert.IsFalse(blocker.Contains(flat),
                $"{LogPrefix} {surfaceName}의 차단막이 창 <b>밖</b> 좌표 {screenPoint}(월드 {flat})까지 " +
                "덮고 있습니다 — 그 클릭은 밑의 앱에 도달하지 못하고 우리가 삼킵니다. " +
                "\"안 닫히는 것\"과 \"클릭을 뺏는 것\"은 다른 문제이고, 후자는 원칙 2 위반입니다.");

            // ★ 여기서 한 번 더, <b>라이브러리가 실제로 쓰는 질의</b>로 확인한다. 위 bounds 대조는
            //   "우리가 계산한 사각형"을 보지만, OS 히트테스트는 <c>Physics2D</c>가 뭘 잡는지만 본다
            //   (MacOverlayStateEnforcer의 히트테스트 감시 프로브와 <b>같은 질의</b>다). 둘이 어긋나면
            //   화면에서만 맞고 실제로는 클릭을 먹는 상태가 된다.
            //   ★ "아무것도 안 잡힌다"까지는 요구하지 않는다 — 그 자리에 캐릭터나 톱니가 서 있을 수
            //     있고 그건 이 라운드가 만든 것이 아니다. 요구하는 것은 <b>창/팝오버의 차단막이
            //     거기 없다</b>는 것 하나다.
            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(
                cam.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f)));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i].collider;
                if (hit == null) continue;
                Assert.IsFalse(hit.gameObject.name.EndsWith("Blocker"),
                    $"{LogPrefix} 창 밖 좌표 {screenPoint}에서 OS 히트테스트와 같은 질의가 차단막 " +
                    $"[{hit.gameObject.name}]을 잡았습니다 — 그 클릭은 밑의 앱으로 가지 못합니다(원칙 2 위반).");
            }
        }
    }
}
