using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>설정창 · 집중 모드 팝오버를 끌어서 옮기고, 그 자리가 남는가</b> — 2026-09-07 사용자 요청
    /// PART1-1: <i>"모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)이 마우스로 끌어도 움직이지 않음 —
    /// 전부 드래그 이동 가능해야 함"</i> · <i>"이동한 위치는 창별로 저장되어 재시작 후에도 유지"</i>.
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (라운드 착수 시점 실측)
    /// ============================================================================
    /// 창 3종 중 <b>정보창에만</b> 헤더 드래그가 있었고(설정창·팝오버에는 코드가 0줄), 그 한 벌조차
    /// 「옮긴 자리는 기억하지 않는다」였다. 즉 셋 다 사용자 요구를 만족하지 못했고, 그 이유가 서로
    /// 달랐다 — <b>이 파일은 그 셋이 다시 갈라지는 것을 막는다</b>.
    /// (정보창 쪽은 <see cref="InfoWindowExclusiveModalTests"/>가 이미 잰다. 여기는 나머지 둘이다.)
    ///
    /// ============================================================================
    /// ★ 배치 화면(640×480)의 함정 — 이 파일이 무엇을 잴 수 있고 무엇을 못 재는가
    /// ============================================================================
    /// 설정창은 <b>720×560 고정</b>이라(정보창처럼 줄지 않는다) 배치 화면에서는 가로 여유가 0이고
    /// 세로도 클램프가 한 자리로 못박는다. 그 상태에서 «옮겨졌는가»를 좌표로 재면
    /// <b>"복원됐다"와 "아무 일도 안 일어났다"가 똑같이 생긴다</b> — 이 저장소가 반복해 당한 형태다.
    /// 그래서 축을 나눈다:
    ///  · <b>설정창</b> — 손잡이 판정([✕]는 손잡이가 아니다) + <b>모델·디스크에 남는가</b>.
    ///  · <b>집중 팝오버</b>(244×252, 이 화면에서 여유가 있다) — <b>실제로 움직이는가</b> +
    ///    <b>다른 앵커로 다시 열어도 그 자리에 있는가</b>. 앵커를 바꿔서 여는 것이 요점이다:
    ///    "저장된 자리를 썼다"와 "우연히 같은 앵커였다"를 가른다.
    /// 두 창은 <b>같은 기구</b>(<see cref="UiWindowDrag"/>)를 쓰므로, 여유가 있는 쪽에서 «움직인다»를
    /// 증명하면 그 기구 자체는 증명된다. 설정창 고유분은 배선과 손잡이뿐이다.
    ///
    /// 입력 주입 관례는 <see cref="InfoWindowExclusiveModalTests"/>와 같다 — 테스트 전용 분기를
    /// 만들지 않고 실제 입력이 지나가는 같은 함수에 버튼 상태와 커서를 먹인다.
    /// </summary>
    public sealed class WindowDragPersistenceTests
    {
        private const string LogPrefix = "[창이동-TEST]";

        /// <summary>부채꼴 버튼 자리를 흉내내는 앵커. 두 개를 <b>서로 다른 자리</b>로 두는 것이
        /// 「저장된 자리를 썼는가」를 가르는 장치다(아래 팝오버 테스트 참고).</summary>
        private static Rect AnchorA => new Rect(Screen.width * 0.25f - 16f, Screen.height * 0.25f - 16f, 32f, 32f);

        private static Rect AnchorB => new Rect(Screen.width * 0.75f - 16f, Screen.height * 0.75f - 16f, 32f, 32f);

        private SettingsWindow _settings;
        private FocusSessionPopover _focus;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_focus != null && _focus.IsOpen) _focus.Close("테스트 정리");
            _settings = null;
            _focus = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ====================================================================================
        // ① 설정창 — 헤더가 손잡이다. [✕]는 아니다.
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SettingsHeaderIsTheHandleAndCloseChipIsNot()
        {
            yield return LoadScene();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");

            _settings.Open("테스트 — 손잡이");
            yield return null;
            yield return null;
            Assume.That(_settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려야 합니다.");

            Rect header = _settings.HeaderScreenRect;
            Assert.Greater(header.width * header.height, 0f,
                $"{LogPrefix} 헤더 사각형이 비어 있습니다 — 손잡이 자체를 못 찾았습니다.");

            Rect[] blocked = _settings.HeaderNonDragRectsForTests();
            Assert.Greater(blocked.Length, 0,
                $"{LogPrefix} 손잡이에서 빼는 목록이 <b>비었습니다</b> — 그러면 [✕] 위에서도 창이 끌립니다. " +
                "빈 목록은 아래 «[✕]는 손잡이가 아니다» 단언을 조용히 공허하게 만듭니다(거짓 통과 #5).");

            Vector2 grab = FindGrabPoint(header, blocked);

            // ---- 손잡이: 잡힌다 ----
            _settings.FeedPointerForTests(false, grab);   // 첫 표본(Open이 버리는 것) 소모.
            _settings.FeedPointerForTests(true, grab);
            Assert.IsTrue(_settings.IsDraggingWindow,
                $"{LogPrefix} 헤더 빈 자리({grab})를 눌렀는데 창을 잡지 않았습니다 — 끌어도 안 움직입니다.");
            _settings.FeedPointerForTests(false, grab);
            Assert.IsFalse(_settings.IsDraggingWindow, $"{LogPrefix} 버튼을 뗐는데 잡은 상태가 남았습니다.");

            // ---- [✕]: 손잡이가 아니고, 눌리면 창이 닫힌다(버튼이 여전히 동작한다) ----
            Vector2 closePoint = _settings.CloseButtonScreenRect.center;
            Assert.IsTrue(header.Contains(closePoint),
                $"{LogPrefix} 전제 실패 — [✕]가 헤더 안이 아닙니다. 이 대조는 «헤더 안인데도 손잡이가 " +
                "아니다»를 재는 것이라 [✕]가 헤더 밖이면 아무것도 증명하지 못합니다.");

            _settings.FeedPointerForTests(true, closePoint);
            Assert.IsFalse(_settings.IsDraggingWindow,
                $"{LogPrefix} [✕] 위에서 창이 잡혔습니다 — 닫으려다 창을 끌게 됩니다.");
            yield return null;
            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} [✕]를 눌렀는데 설정창이 닫히지 않았습니다 — 드래그가 버튼 클릭을 삼켰습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 헤더 빈 자리는 손잡이, [✕]는 아니고 여전히 닫힙니다.");
        }

        // ====================================================================================
        // ② 설정창 — 문턱을 넘긴 이동만 «자리»로 남는다
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SettingsDragPersistsOnlyPastTheThreshold()
        {
            yield return LoadScene();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");

            _settings.Open("테스트 — 문턱");
            yield return null;
            yield return null;

            Vector2 grab = FindGrabPoint(_settings.HeaderScreenRect, _settings.HeaderNonDragRectsForTests());
            float threshold = UiWindowDrag.MoveThresholdPoints;
            Assert.Greater(threshold, 0f,
                $"{LogPrefix} 이동 임계가 0입니다 — 아래 «절반만 밀었다»가 «안 밀었다»와 같아집니다.");

            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 전제 실패 — 시작 시점에 이미 «옮긴 적 있음»입니다([SetUp] 격리가 깨졌습니다).");

            // ---- 본 검증: 임계의 절반만 밀고 뗀다 → 아무것도 남지 않는다 ----
            _settings.FeedPointerForTests(false, grab);
            _settings.FeedPointerForTests(true, grab);
            _settings.FeedPointerForTests(true, grab + new Vector2(threshold * 0.5f, 0f));
            _settings.FeedPointerForTests(false, grab + new Vector2(threshold * 0.5f, 0f));
            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 문턱({threshold:F1}pt)을 안 넘은 누름이 창 위치를 <b>영구히</b> 저장했습니다 — " +
                "톱니가 2026-09-02에 당한 사고(스치듯 지나간 클릭 하나가 아이콘을 영구히 옮김)와 같은 형태입니다.");

            // ---- 양성 대조: 같은 경로가 문턱을 넘으면 실제로 남는다 ----
            _settings.FeedPointerForTests(true, grab);
            _settings.FeedPointerForTests(true, grab + new Vector2(threshold * 6f, -threshold * 4f));
            _settings.FeedPointerForTests(false, grab + new Vector2(threshold * 6f, -threshold * 4f));
            Assert.IsTrue(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 양성 대조 실패 — 문턱의 6배를 밀었는데도 «옮긴 적 있음»이 서지 않습니다. " +
                "위 IsFalse는 «임계가 옳다»가 아니라 <b>«설정창 드래그가 통째로 죽었다»</b>를 보고 있었습니다.");

            // ---- 디스크까지 내려갔는가 — <b>다른 방법으로</b> 다시 잰다 ----
            // 위 단언은 <c>UiLayoutModel</c>(메모리)을 봤다. 여기서는 저장 파일 <b>원문</b>을 읽는다.
            // (같은 함정에 같이 빠지지 않게: Save가 안 써도 메모리 값은 그대로 남아 초록이 된다.)
            // ★ 씬이 살아 있는 동안 <c>Load()</c>를 부르지 않는 이유: 그건 진행도·장비까지 통째로
            //   되살려 <b>돌고 있는 씬의 상태를 흔든다</b>. 왕복 자체는 EditMode의
            //   <c>UiLayoutPersistenceTests</c>가 이미 잰다 — 여기서 확인할 것은 «디스크에 닿았는가»다.
            Vector2 saved = UiLayoutModel.WindowOffsetPoints(UiWindowId.Settings);
            string path = CharacterSaveStore.FilePath;
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 저장 파일이 없습니다({path}) — 드래그 확정이 디스크를 안 두드렸습니다.");
            string json = File.ReadAllText(path);
            Match m = Regex.Match(json, @"""settingsWindowPositionSaved""\s*:\s*(true|false)");
            Assert.IsTrue(m.Success,
                $"{LogPrefix} 저장 파일에 settingsWindowPositionSaved 키가 없습니다 — 스캐너가 죽었거나 스키마가 깨졌습니다.");
            Assert.AreEqual("true", m.Groups[1].Value,
                $"{LogPrefix} 옮긴 자리가 메모리({saved})에만 남고 파일에는 false로 적혔습니다 — 재시작하면 사라집니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 문턱 미만은 안 남고, 넘기면 {saved}가 디스크까지 갑니다.");
        }

        // ====================================================================================
        // ③ 집중 팝오버 — 창 전체가 손잡이. 실제로 움직이고, 앵커를 바꿔 열어도 그 자리다.
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator FocusPopoverBodyDragsAndBeatsTheAnchorOnReopen()
        {
            yield return LoadScene();
            _focus = Object.FindFirstObjectByType<FocusSessionPopover>();
            Assert.IsNotNull(_focus, $"{LogPrefix} 씬에 FocusSessionPopover가 없습니다.");

            Assert.IsTrue(_focus.IsWindowDraggableForTests,
                $"{LogPrefix} 집중 팝오버에 드래그가 꺼져 있습니다 — 사용자 요청 «집중모드 타이머»가 미반영입니다.");
            Assert.Greater(_focus.DragExcludedControlCountForTests, 0,
                $"{LogPrefix} 손잡이에서 빼는 컨트롤이 0개입니다 — 창 전체가 손잡이가 되어 " +
                "시간 칩도 [시작]도 눌리지 않습니다(빈 목록은 조용히 초록이 된다 — 거짓 통과 #5).");

            _focus.Open(AnchorA, "테스트 — 드래그");
            yield return new WaitForSecondsRealtime(0.3f);   // 자라나는 애니메이션(0.16초)을 넘긴다.
            Assume.That(_focus.IsOpen, Is.True, $"{LogPrefix} 전제: 팝오버가 열려야 합니다.");

            // ---- 전제 ①: 아직 «옮긴 적 없음»이고 앵커 배치를 쓰고 있다 ----
            Assert.IsFalse(_focus.HasUserPlacedCenter,
                $"{LogPrefix} 전제 실패 — 열자마자 «사용자가 옮긴 자리»를 쓰고 있습니다.");

            // ---- 전제 ②: 버튼은 여전히 눌린다(드래그가 클릭을 삼키지 않는다) ----
            float before = _focus.SelectedMinutes;
            Rect chip = _focus.DurationChipScreenRect(0);
            Assert.Greater(chip.width * chip.height, 0f, $"{LogPrefix} 시간 칩 사각형이 비어 있습니다.");
            _focus.FeedClickForTests(chip.center);
            Assert.AreNotEqual(before, _focus.SelectedMinutes,
                $"{LogPrefix} 시간 칩을 눌렀는데 값이 안 바뀌었습니다({before} → {_focus.SelectedMinutes}) — " +
                "«창 전체가 손잡이»가 버튼 클릭까지 먹었습니다.");
            Assert.IsFalse(_focus.IsDraggingWindow,
                $"{LogPrefix} 시간 칩 위에서 창이 잡혔습니다 — 칩을 누르다 창이 끌려갑니다.");

            // ---- 본 검증: 제목 줄(버튼이 아니다)을 잡고 끈다 ----
            Assert.IsNotNull(_focus.TitleTextForTests, $"{LogPrefix} 제목 글자를 찾지 못했습니다.");
            Vector2 grab = PopoverPanel.ScreenRectOf(_focus.TitleTextForTests.rectTransform).center;
            Assert.IsTrue(_focus.PanelScreenRect.Contains(grab),
                $"{LogPrefix} 제목 중심({grab})이 패널({_focus.PanelScreenRect}) 밖입니다 — 전제가 무너졌습니다.");

            Rect panelBefore = _focus.PanelScreenRect;
            float step = UiWindowDrag.MoveThresholdPoints * 5f;

            _focus.FeedPointerForTests(false, grab);
            _focus.FeedPointerForTests(true, grab);
            Assert.IsTrue(_focus.IsDraggingWindow,
                $"{LogPrefix} 제목 줄(버튼이 아닌 자리)을 눌렀는데 창을 잡지 않았습니다.");

            _focus.FeedPointerForTests(true, grab + new Vector2(step, 0f));
            Rect panelAfter = _focus.PanelScreenRect;
            Assert.Greater(panelAfter.center.x, panelBefore.center.x + 0.5f,
                $"{LogPrefix} 오른쪽으로 {step:F1}pt 끌었는데 창이 안 움직였습니다" +
                $"({panelBefore.center} → {panelAfter.center}). 화면 폭 {Screen.width}px, 창 폭 {panelAfter.width}px.");
            AssertPanelInsideScreen(_focus.PanelScreenRect, "끄는 중");

            // 화면 밖으로 끌어내도 나가지 않는다 — [✕]가 화면 밖으로 나가면 닫을 수 없는 창이 된다.
            _focus.FeedPointerForTests(true, grab + new Vector2(100000f, 100000f));
            AssertPanelInsideScreen(_focus.PanelScreenRect, "화면 밖으로 끌었을 때");

            _focus.FeedPointerForTests(false, grab + new Vector2(100000f, 100000f));
            Assert.IsFalse(_focus.IsDraggingWindow, $"{LogPrefix} 버튼을 뗐는데 잡은 상태가 남았습니다.");
            Assert.IsTrue(UiLayoutModel.HasWindowOffset(UiWindowId.FocusSession),
                $"{LogPrefix} 팝오버를 옮겼는데 «옮긴 적 있음»이 서지 않았습니다.");

            Vector2 stored = UiLayoutModel.WindowOffsetPoints(UiWindowId.FocusSession);
            Rect placed = _focus.PanelScreenRect;

            // ---- 다시 열 때: <b>다른 앵커</b>로 연다. 앵커를 따라가면 실패다 ----
            _focus.Close("테스트 — 재개 확인");
            yield return new WaitForSecondsRealtime(PopoverPanel.ShrinkSeconds * 3f + 0.1f);
            Assume.That(_focus.IsOpen, Is.False, $"{LogPrefix} 전제: 팝오버가 닫혀야 합니다.");

            Assert.AreNotEqual(AnchorA.center, AnchorB.center,
                $"{LogPrefix} 두 앵커가 같은 자리입니다 — 아래 판정이 «저장된 자리»와 «앵커»를 가르지 못합니다.");
            _focus.Open(AnchorB, "테스트 — 재개 확인(다른 앵커)");
            yield return null;

            Assert.IsTrue(_focus.HasUserPlacedCenter,
                $"{LogPrefix} 다시 열었는데 앵커 배치로 돌아갔습니다 — «재시작 후에도 유지»가 깨졌습니다.");
            Assert.AreEqual(stored.x, _focus.UserCenterPointsForTests.x, 0.5f,
                $"{LogPrefix} 다시 연 자리의 x가 저장값과 다릅니다(저장 {stored}, 지금 {_focus.UserCenterPointsForTests}).");
            Assert.AreEqual(stored.y, _focus.UserCenterPointsForTests.y, 0.5f,
                $"{LogPrefix} 다시 연 자리의 y가 저장값과 다릅니다(저장 {stored}, 지금 {_focus.UserCenterPointsForTests}).");
            Assert.AreEqual(placed.center.x, _focus.PanelScreenRect.center.x, 1f,
                $"{LogPrefix} 화면 위 자리가 옮겨 둔 곳과 다릅니다 — 저장은 됐는데 화면에 반영되지 않았습니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 창 몸통으로 끌리고, 화면을 벗어나지 않고, " +
                $"앵커를 {AnchorA.center} → {AnchorB.center}로 바꿔 열어도 옮긴 자리({stored})에 뜹니다.");
        }

        // ====================================================================================
        // 도구
        // ====================================================================================

        /// <summary>손잡이 안에서 <b>실제로 끌 수 있는</b> 지점 — 막힌 사각형을 전부 피한 자리.
        /// <para>좌표를 손으로 적으면 헤더에 컨트롤이 하나 늘어날 때마다 이 테스트가 조용히 엉뚱한
        /// 곳을 누르게 된다(정보창 쪽에서 실제로 한 번 그렇게 깨졌다).</para></summary>
        private static Vector2 FindGrabPoint(Rect handle, Rect[] blocked)
        {
            float y = handle.center.y;
            for (float t = 0.02f; t < 1f; t += 0.01f)
            {
                var p = new Vector2(Mathf.Lerp(handle.xMin, handle.xMax, t), y);
                bool hit = false;
                for (int i = 0; i < blocked.Length && !hit; i++) hit = blocked[i].Contains(p);
                if (!hit) return p;
            }

            Assert.Fail($"{LogPrefix} 손잡이({handle}) 전체가 막힌 사각형으로 덮여 있습니다 — " +
                "끌 수 있는 자리가 한 점도 없습니다.");
            return handle.center;
        }

        private static void AssertPanelInsideScreen(Rect panel, string when)
        {
            // 화면이 창보다 좁으면 «전부 안»이 물리적으로 불가능하다 — 그때는 판정을 건너뛰지 않고
            // 그 사실을 로그로 남긴다(조용히 통과시키면 클램프가 죽어도 초록이다).
            if (panel.width > Screen.width || panel.height > Screen.height)
            {
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 창({panel.size})보다 작아 " +
                    $"«전부 화면 안»을 잴 수 없습니다({when}) — 클램프는 상단 우선 규칙으로 동작합니다.");
                return;
            }
            Assert.GreaterOrEqual(panel.xMin, -0.5f, $"{LogPrefix} 창이 화면 왼쪽으로 나갔습니다({when}, {panel}).");
            Assert.GreaterOrEqual(panel.yMin, -0.5f, $"{LogPrefix} 창이 화면 아래로 나갔습니다({when}, {panel}).");
            Assert.LessOrEqual(panel.xMax, Screen.width + 0.5f, $"{LogPrefix} 창이 화면 오른쪽으로 나갔습니다({when}, {panel}).");
            Assert.LessOrEqual(panel.yMax, Screen.height + 0.5f, $"{LogPrefix} 창이 화면 위로 나갔습니다({when}, {panel}).");
        }
    }
}
