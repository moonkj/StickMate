using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 톱니 클릭 -> <b>부채꼴 버튼 3개</b>(Interaction/GearRadialMenuWidget.cs) 회귀 테스트 —
    /// 2026-08-30 사용자 요청("기어메뉴를 클릭했을때 집중모드 버튼 캐릭터 버튼 오늘 할일 버튼 3가지가
    /// 촤르륵 원버튼 3개가 나오고 각 버튼을 클릭했을때 세부 메뉴로 들어가도록").
    ///
    /// ============================================================================
    /// 무엇을 절대 조건으로 잠그는가
    /// ============================================================================
    ///  ① 짧은 클릭 -> 회전 -> <b>3개가 순차로</b> 펼쳐진다(스태거가 실제로 있는지까지 확인 —
    ///     동시에 튀어나오면 "촤르륵"이 아니다).
    ///  ② 기어 재클릭 = 접기(토글), 부채꼴 바깥 클릭 = 접기.
    ///  ③ <b>[캐릭터] 버튼을 실제 입력 경로로 눌렀을 때</b> CharacterInfoWindow가 열린다.
    ///  ④ <b>네거티브 컨트롤</b>: 버튼에서 벗어난 곳(부채꼴 반지름 밖)을 눌러 떼면 아무 버튼도 발동하지
    ///     않는다. 그리고 <b>펼침 애니메이션이 시작되기 전(진행도 0)</b>에는 그 자리를 눌러도 발동하지
    ///     않는다 — 안 보이는 버튼이 클릭을 먹으면 "누른 적 없는 것이 눌린다".
    ///  ⑤ 눌렀다가 <b>버튼 밖으로 끌고 나가 떼면</b> 취소된다(모든 OS의 버튼 관례).
    ///  ⑥ 톱니를 화면 <b>어느 사분면으로 옮겨도</b> 세 버튼이 전부 화면 안에 남는다. 이 항목이 이번
    ///     라운드에서 가장 깨지기 쉬운 곳이다(톱니는 사용자가 어디로든 옮길 수 있다).
    ///  ⑦ 클릭관통 차단 사각형(InteractiveScreenRect)이 펼쳐진 버튼까지 덮는다 — 안 덮으면 버튼을
    ///     눌러도 그 클릭이 밑의 앱으로 새어 나간다. 접히면 원래 크기로 돌아온다(비침해).
    ///
    /// 입력 주입은 InfoGearDragTests와 완전히 같은 관례다 — 테스트 전용 분기를 만들지 않고 실제 입력이
    /// 지나가는 같은 함수(ProcessPointer)에 버튼 상태와 커서를 먹인다.
    /// </summary>
    public sealed class InfoGearRadialMenuTests
    {
        private InfoGearIconWidget _gear;
        private CharacterInfoWindow _window;
        private string _backup;
        private bool _hadFile;

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 InfoGearIconWidget 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            _gear = found[0];
            _window = _gear.GetComponent<CharacterInfoWindow>();
            yield return null;
        }

        /// <summary>실제 사용자와 같은 순서: 짧게 눌렀다 뗀 뒤 회전이 끝나고 부채꼴이 다 펼쳐질 때까지 기다린다.</summary>
        private IEnumerator OpenMenuByShortClick()
        {
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.MenuReadySeconds + 0.25f);
            Assert.IsTrue(_gear.IsMenuExpanded, "짧은 클릭 후에도 부채꼴이 펼쳐지지 않았습니다.");
        }

        private void ClickAt(Vector2 screen)
        {
            _gear.FeedPointerForTests(true, screen);
            _gear.FeedPointerForTests(false, screen);
        }

        // ==================== ① 순차로 펼쳐진다 ====================

        [UnityTest]
        public IEnumerator ShortClickExpandsThreeButtonsInSequence()
        {
            yield return LoadSceneAndResolve();

            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);

            // ★ 32-9 (B): 회전이 끝나기를 기다리지 않는다. 클릭한 그 프레임에 이미 펼치기 시작해야 한다 —
            //   520ms 동안 아무 변화가 없으면 사용자가 다시 눌러 메뉴가 깜빡이는 실패 모드가 생긴다.
            Assert.IsTrue(_gear.IsMenuExpanded,
                "클릭한 프레임에 부채꼴이 펼쳐지기 시작하지 않았습니다 — 회전이 끝난 뒤에 펼치면 안 됩니다.");
            Assert.Less(_gear.MenuButtonProgress(GearMenuButton.FocusMode), 0.2f,
                "펼침이 시작되자마자 이미 다 펼쳐졌습니다 — 애니메이션이 없습니다.");
            Assert.IsTrue(_gear.IsSpinning, "부채꼴은 펼쳐졌는데 기어 회전이 시작되지 않았습니다(동시 진행이어야 합니다).");

            // 한 프레임만 찍어보면 그 순간이 마침 "이미 다 펼쳐진 뒤"일 수 있어 플레이키하다.
            // 그래서 펼치는 내내 매 프레임 관찰하며 (가) 순서가 뒤집히지 않는지 항상 확인하고,
            // (나) 앞 버튼이 뒤 버튼보다 앞서 있는 순간을 한 번이라도 봤는지를 남긴다.
            bool sawStagger = false;
            float bestGap = 0f;
            float deadline = Time.realtimeSinceStartup + InfoGearIconWidget.MenuReadySeconds + 0.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                float a = _gear.MenuButtonProgress(GearMenuButton.FocusMode);
                float b = _gear.MenuButtonProgress(GearMenuButton.Character);
                float c = _gear.MenuButtonProgress(GearMenuButton.Todo);
                Assert.GreaterOrEqual(a + 0.001f, b, $"펼침 순서가 뒤집혔습니다({a:F2} < {b:F2}).");
                Assert.GreaterOrEqual(b + 0.001f, c, $"펼침 순서가 뒤집혔습니다({b:F2} < {c:F2}).");
                if (a - c > bestGap) bestGap = a - c;
                if (a > c) sawStagger = true;
                yield return null;
            }

            Assert.IsTrue(sawStagger,
                "세 버튼이 항상 같은 진행도였습니다 — 순차 등장(촤르륵)이 아니라 동시 등장입니다.");
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.AreEqual(1f, _gear.MenuButtonProgress((GearMenuButton)i), 0.001f,
                    $"{i}번 버튼이 끝까지 펼쳐지지 않았습니다.");
            }

            Debug.Log($"[부채꼴테스트] 순차 펼침 확인 — 첫/마지막 버튼 진행도 최대 격차 {bestGap:F2}, 최종 전부 1.00.");
        }

        // ==================== ② 토글 닫기 / 바깥 클릭 닫기 ====================

        [UnityTest]
        public IEnumerator ClickingGearAgainCollapsesAndOutsideClickCollapses()
        {
            yield return LoadSceneAndResolve();
            yield return OpenMenuByShortClick();

            ClickAt(_gear.IconScreenCenter);
            Assert.IsFalse(_gear.IsMenuExpanded, "기어를 다시 눌렀는데 접히지 않았습니다(토글 실패).");

            yield return OpenMenuByShortClick();

            // 부채꼴에서 확실히 먼 곳(화면 반대편 모서리 근처).
            Vector2 far = new Vector2(
                _gear.IconScreenCenter.x > Screen.width * 0.5f ? 12f : Screen.width - 12f,
                _gear.IconScreenCenter.y > Screen.height * 0.5f ? 12f : Screen.height - 12f);
            _gear.FeedPointerForTests(true, far);
            Assert.IsFalse(_gear.IsMenuExpanded, "부채꼴 바깥을 눌렀는데 접히지 않았습니다.");
            _gear.FeedPointerForTests(false, far);
            yield return null;
        }

        // ==================== ③ [캐릭터] 버튼이 실제로 창을 연다 ====================

        [UnityTest]
        public IEnumerator CharacterButtonOpensCharacterInfoWindow()
        {
            yield return LoadSceneAndResolve();
            Assert.IsFalse(_window.IsOpen, "테스트 시작 시점에 이미 창이 열려 있습니다.");

            yield return OpenMenuByShortClick();

            ClickAt(_gear.MenuButtonScreenCenter(GearMenuButton.Character));
            yield return null;

            Assert.IsTrue(_window.IsOpen, "[캐릭터] 버튼을 눌렀는데 CharacterInfoWindow가 열리지 않았습니다.");
            Assert.IsFalse(_gear.IsMenuExpanded, "버튼을 선택했는데 부채꼴이 그대로 남아 있습니다.");

            _window.Close("테스트 정리");
            Debug.Log("[부채꼴테스트] [캐릭터] 버튼 -> 정보창 열림 확인.");
        }

        // ==================== ④ 네거티브 컨트롤 ====================

        [UnityTest]
        public IEnumerator ClickingBesideAButtonDoesNothingAndInvisibleButtonsIgnoreClicks()
        {
            yield return LoadSceneAndResolve();
            yield return OpenMenuByShortClick();

            // (가) 버튼 중심에서 반지름의 3배만큼 벗어난 자리 — 판정 원 밖이다.
            Vector2 c = _gear.MenuButtonScreenCenter(GearMenuButton.Character);
            Vector2 gear = _gear.IconScreenCenter;
            ClickAt(gear + (c - gear) * 2f);   // 같은 방향으로 두 배 = 부채꼴 반지름의 두 배 지점.
            Assert.IsFalse(_window.IsOpen, "버튼 옆(판정 원 밖)을 눌렀는데 캐릭터 창이 열렸습니다.");

            // (나) 아직 나타나지도 않은 버튼: 펼침이 시작된 <b>바로 그 프레임</b>(진행도 0)에 누른다.
            // 회전과 동시 진행이므로 클릭 직후가 곧 그 순간이다.
            yield return LoadSceneAndResolve();
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            Assert.IsTrue(_gear.IsMenuExpanded, "준비 조건 실패 — 부채꼴이 펼쳐지지 않았습니다.");

            Vector2 charSeat = _gear.MenuButtonScreenCenter(GearMenuButton.Character);
            Assert.Less(_gear.MenuButtonProgress(GearMenuButton.Character), 0.5f,
                "준비 조건 실패 — 이 시점에 [캐릭터] 버튼이 이미 절반 이상 펼쳐졌습니다.");
            ClickAt(charSeat);
            yield return null;
            Assert.IsFalse(_window.IsOpen,
                "아직 보이지도 않는 버튼 자리를 눌렀는데 창이 열렸습니다 — 안 보이는 버튼이 클릭을 먹었습니다.");
        }

        // ==================== ⑤ 버튼 밖에서 떼면 취소 ====================

        [UnityTest]
        public IEnumerator PressingAButtonAndReleasingOutsideCancels()
        {
            yield return LoadSceneAndResolve();
            yield return OpenMenuByShortClick();

            Vector2 c = _gear.MenuButtonScreenCenter(GearMenuButton.Character);
            _gear.FeedPointerForTests(true, c);
            // 톱니 중심은 어떤 버튼의 판정 원에도 들지 않는 자리다(부채꼴 반지름만큼 떨어져 있다).
            _gear.FeedPointerForTests(false, _gear.IconScreenCenter);
            yield return null;

            Assert.IsFalse(_window.IsOpen, "버튼 밖에서 뗐는데 발동했습니다 — 취소되어야 합니다.");
            Assert.IsTrue(_gear.IsMenuExpanded, "취소인데 부채꼴까지 접혔습니다.");
        }

        // ==================== ⑥ 어느 사분면에서도 화면 안 ====================

        [UnityTest]
        public IEnumerator FanStaysOnScreenFromEveryCorner()
        {
            yield return LoadSceneAndResolve();

            var corners = new[]
            {
                new Vector2(6f, 6f),                                    // 좌하단
                new Vector2(Screen.width - 6f, 6f),                     // 우하단
                new Vector2(6f, Screen.height - 6f),                    // 좌상단
                new Vector2(Screen.width - 6f, Screen.height - 6f),     // 우상단
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), // 정중앙
            };

            for (int k = 0; k < corners.Length; k++)
            {
                // 톱니를 그 모서리로 끌어다 놓는다(거리 임계로 즉시 드래그되는 실제 경로 그대로).
                _gear.FeedPointerForTests(true, _gear.IconScreenCenter);
                _gear.FeedPointerForTests(true, corners[k]);
                _gear.FeedPointerForTests(false, corners[k]);
                yield return null;

                yield return OpenMenuByShortClick();

                for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
                {
                    Vector2 bc = _gear.MenuButtonScreenCenter((GearMenuButton)i);
                    Assert.GreaterOrEqual(bc.x, 0f, $"모서리 {k}: {i}번 버튼이 화면 왼쪽 밖({bc}).");
                    Assert.GreaterOrEqual(bc.y, 0f, $"모서리 {k}: {i}번 버튼이 화면 아래쪽 밖({bc}).");
                    Assert.LessOrEqual(bc.x, Screen.width, $"모서리 {k}: {i}번 버튼이 화면 오른쪽 밖({bc}).");
                    Assert.LessOrEqual(bc.y, Screen.height, $"모서리 {k}: {i}번 버튼이 화면 위쪽 밖({bc}).");
                    AssertNoOverlap($"모서리 {k}");
                }

                Rect r = _gear.InteractiveScreenRect;
                Assert.GreaterOrEqual(r.xMin, -0.5f, $"모서리 {k}: 판정 사각형이 화면 왼쪽 밖({r}).");
                Assert.GreaterOrEqual(r.yMin, -0.5f, $"모서리 {k}: 판정 사각형이 화면 아래쪽 밖({r}).");
                Assert.LessOrEqual(r.xMax, Screen.width + 0.5f, $"모서리 {k}: 판정 사각형이 화면 오른쪽 밖({r}).");
                Assert.LessOrEqual(r.yMax, Screen.height + 0.5f, $"모서리 {k}: 판정 사각형이 화면 위쪽 밖({r}).");

                ClickAt(_gear.IconScreenCenter);   // 다음 모서리로 가기 전에 접는다.
                yield return null;
            }

            Debug.Log("[부채꼴테스트] 5개 위치(4모서리 + 정중앙) 전부 화면 안 확인.");
        }

        // ==================== ⑧ 기하 확정치 (32-1) ====================

        /// <summary>기준각은 사분면 부호가 아니라 (화면 중심 − 기어 중심)의 실제 각도를 45도 단위로
        /// 스냅한 값이어야 한다 — 부호 방식이면 위쪽 한가운데에서 아래로 곧게 못 펼친다(32-9 (C)).</summary>
        [Test]
        public void 기준각은_화면_중심_방향을_45도로_스냅한다()
        {
            Assert.AreEqual(225f, GearRadialMenuWidget.Snap45(new Vector2(-1f, -1f)), 0.01f, "우상단 -> 좌하");
            Assert.AreEqual(45f, GearRadialMenuWidget.Snap45(new Vector2(1f, 1f)), 0.01f, "좌하단 -> 우상");
            Assert.AreEqual(270f, GearRadialMenuWidget.Snap45(new Vector2(0f, -1f)), 0.01f,
                "화면 위쪽 한가운데인데 아래로 곧게(270도) 펼치지 않았습니다 — 사분면 부호 방식의 결함입니다.");
            Assert.AreEqual(180f, GearRadialMenuWidget.Snap45(new Vector2(-1f, 0.1f)), 0.01f, "오른쪽 한가운데 -> 왼쪽");
            Assert.AreEqual(0f, GearRadialMenuWidget.Snap45(new Vector2(1f, -0.1f)), 0.01f, "왼쪽 한가운데 -> 오른쪽");
        }

        /// <summary>
        /// 히트 원이 절대 겹치지 않는다 — 겹치면 "먼저 검사되는 버튼이 이긴다"가 되어 <b>보이는 것과
        /// 눌리는 것이 달라진다</b>. 이 불변식은 어떤 폴백(회전/세로 일렬/축소)이 걸려도 성립해야 하므로
        /// 지금 지름 기준으로 잰다. 이어서 <b>공간이 넉넉하면</b> 확정 기하(Ø44 / R62 / 60도 -> 중심
        /// 거리 62pt)가 실제로 선택되는지도 함께 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator AdjacentButtonsNeverOverlap()
        {
            yield return LoadSceneAndResolve();
            yield return OpenMenuByShortClick();
            AssertNoOverlap("기본 위치(우상단)");

            // 화면 한가운데로 옮기면 어떤 배치 해상도에서도 부채꼴 원안이 그대로 들어간다.
            ClickAt(_gear.IconScreenCenter);
            yield return new WaitForSecondsRealtime(0.2f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _gear.FeedPointerForTests(true, _gear.IconScreenCenter);
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return null;
            yield return OpenMenuByShortClick();

            AssertNoOverlap("화면 중앙");
            Assert.AreEqual(GearRadialMenuWidget.ButtonDiameterPoints, FanWidget().ButtonDiameter, 0.01f,
                "공간이 넉넉한데 축소 폴백(Ø36)이 걸렸습니다 — 확정 기하가 먼저 성립해야 합니다.");
            float spacing = FanWidget().MinimumCenterSpacingPoints();
            Assert.AreEqual(2f * GearRadialMenuWidget.OrbitRadiusPoints
                    * Mathf.Sin(GearRadialMenuWidget.ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad),
                spacing, 0.5f,
                $"공간이 넉넉한데 부채꼴 원안이 아닙니다(중심 거리 {spacing:F1}pt) — 폴백이 잘못 걸렸습니다.");

            ClickAt(_gear.IconScreenCenter);
            yield return null;
        }

        private void AssertNoOverlap(string context)
        {
            float spacing = FanWidget().MinimumCenterSpacingPoints();
            float minimum = FanWidget().ButtonDiameter + GearRadialMenuWidget.HitPaddingPoints * 2f;
            Assert.GreaterOrEqual(spacing, minimum,
                $"{context}: 인접 버튼 중심 거리 {spacing:F1}pt < 판정 지름 {minimum:F1}pt — " +
                "판정 원이 겹쳐 '보이는 것과 눌리는 것'이 달라집니다.");
        }

        // ==================== ⑨ [집중 모드] 팝오버 — 90초 데모가 아니라 고른 길이 ====================

        [UnityTest]
        public IEnumerator FocusPopoverStartsTheChosenDurationNotTheNinetySecondDemo()
        {
            yield return LoadSceneAndResolve();
            var focus = _gear.GetComponent<FocusSessionPopover>();
            var director = _gear.GetComponent<FocusWatchDirector>();
            Assert.IsNotNull(focus, "씬에 FocusSessionPopover가 없습니다.");
            Assert.IsNotNull(director, "씬에 FocusWatchDirector가 없습니다.");
            if (director.IsSessionActive) director.StopFocusSession();

            yield return OpenMenuByShortClick();
            ClickAt(_gear.MenuButtonScreenCenter(GearMenuButton.FocusMode));
            yield return null;

            Assert.IsTrue(focus.IsOpen, "[집중 모드] 버튼을 눌렀는데 팝오버가 열리지 않았습니다.");
            Assert.AreEqual((int)GearMenuButton.FocusMode, FanWidget().AnchoredButton,
                "팝오버가 열렸는데 그 버튼이 활성 상태로 남지 않았습니다(32-3).");
            yield return new WaitForSecondsRealtime(0.25f);

            // "25분"(인덱스 1)을 실제 클릭 경로로 고르고 [시작].
            focus.FeedClickForTests(focus.DurationChipScreenRect(1).center);
            Assert.AreEqual(25f, focus.SelectedMinutes, 0.01f, "25분을 골랐는데 다른 값이 선택됐습니다.");
            focus.FeedClickForTests(focus.StartButtonScreenRect.center);
            yield return null;

            Assert.IsTrue(director.IsSessionActive, "[시작]을 눌렀는데 세션이 시작되지 않았습니다.");
            Assert.AreEqual(1500f, director.SessionDurationSeconds, 0.5f,
                $"25분을 골랐는데 세션 길이가 {director.SessionDurationSeconds:F0}초입니다 — " +
                "ForceTriggerNow(90초 데모)를 부르면 화면의 숫자가 거짓이 됩니다(원칙 1 위반).");

            // 링과 라벨이 같은 스냅샷에서 나오는가.
            yield return new WaitForSecondsRealtime(0.35f);
            float fill = focus.RingFillAmount;
            string label = focus.TimeLabel;
            float labelSeconds = ParseMmSs(label);
            Assert.AreEqual(director.RemainingSeconds / director.SessionDurationSeconds, fill, 0.05f,
                $"링 채움({fill:F3})이 실제 남은 비율과 다릅니다.");
            Assert.AreEqual(fill * director.SessionDurationSeconds, labelSeconds, 20f,
                $"링({fill:F3})과 라벨({label})이 서로 다른 값을 가리킵니다.");

            director.StopFocusSession();
            focus.Close("테스트 정리");
            Debug.Log($"[부채꼴테스트] 집중 팝오버 25분 확인 — 세션 {director.SessionDurationSeconds:F0}초, 링 {fill:F2}, 라벨 {label}.");
        }

        private static float ParseMmSs(string text)
        {
            string[] parts = text.Split(':');
            if (parts.Length != 2) return -1f;
            return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
        }

        // ==================== ⑩ [오늘 할일] 팝오버 — 적은 것이 실제로 남는다 ====================

        [UnityTest]
        public IEnumerator TodoPopoverAddsAndTogglesThroughTheRealPath()
        {
            TodoListModel.ResetForTesting();
            yield return LoadSceneAndResolve();
            var todo = _gear.GetComponent<TodoBoardPopover>();
            Assert.IsNotNull(todo, "씬에 TodoBoardPopover가 없습니다.");

            yield return OpenMenuByShortClick();
            ClickAt(_gear.MenuButtonScreenCenter(GearMenuButton.Todo));
            yield return null;
            Assert.IsTrue(todo.IsOpen, "[오늘 할일] 버튼을 눌렀는데 팝오버가 열리지 않았습니다.");
            yield return new WaitForSecondsRealtime(0.25f);

            todo.AddForTests("보고서 초안");
            yield return null;

            Assert.AreEqual(1, TodoListModel.UncompletedCount, "추가했는데 목록에 들어가지 않았습니다.");
            Assert.AreEqual(1, todo.VisibleRowCount, "목록에 있는데 화면에 행이 그려지지 않았습니다.");

            int id = todo.RowItemId(0);
            Assert.Greater(id, 0, "행이 항목을 물고 있지 않습니다.");

            // 행 아무 데나 클릭 = 완료 토글(포스트잇과 같은 관례).
            todo.FeedClickForTests(todo.RowScreenRect(0).center);
            yield return null;
            Assert.AreEqual(0, TodoListModel.UncompletedCount, "행을 눌렀는데 완료 토글이 되지 않았습니다.");

            // 삭제는 즉시가 아니라 인라인 확인 3초 — 첫 클릭으로 지워지면 안 된다(네거티브 컨트롤).
            todo.FeedClickForTests(todo.RowScreenRect(0).center);   // 다시 미완료로.
            yield return null;
            todo.FeedClickForTests(todo.RowDeleteScreenRect(0).center);
            yield return null;
            Assert.AreEqual(id, todo.PendingDeleteId, "[✕]를 눌렀는데 삭제 확인이 뜨지 않았습니다.");
            Assert.AreEqual(1, TodoListModel.ActiveItems.Count, "확인도 없이 바로 삭제됐습니다.");

            todo.FeedClickForTests(todo.RowConfirmYesScreenRect(0).center);
            yield return null;
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count, "[삭제]를 눌렀는데 지워지지 않았습니다.");

            todo.Close("테스트 정리");
            TodoListModel.ResetForTesting();
        }

        // ==================== ⑪ 닫히면 차단막이 전부 꺼진다 (비침해) ====================

        [UnityTest]
        public IEnumerator AllClickBlockersAreDisabledWhenNothingIsOpen()
        {
            yield return LoadSceneAndResolve();
            yield return new WaitForSecondsRealtime(0.2f);

            AssertPopoverBlockersDisabled("아무것도 열지 않은 상태");

            yield return OpenMenuByShortClick();
            ClickAt(_gear.MenuButtonScreenCenter(GearMenuButton.Todo));
            yield return new WaitForSecondsRealtime(0.3f);

            ClickAt(_gear.IconScreenCenter);   // 기어 클릭 = 완전 종료.
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(_gear.IsMenuExpanded, "기어를 눌렀는데 부채꼴이 남아 있습니다.");
            AssertPopoverBlockersDisabled("닫은 뒤");
            Assert.AreEqual(_gear.IconScreenRect.width, _gear.InteractiveScreenRect.width, 0.5f,
                "닫혔는데 차단막이 톱니보다 넓게 남아 있습니다(비침해 위반).");
        }

        private void AssertPopoverBlockersDisabled(string context)
        {
            var blockers = Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
            for (int i = 0; i < blockers.Length; i++)
            {
                string name = blockers[i].gameObject.name;
                if (!name.Contains("Popover") && !name.Contains("Blocker")) continue;
                if (name.Contains("FocusSessionPopover") || name.Contains("TodoBoardPopover"))
                {
                    Assert.IsFalse(blockers[i].enabled,
                        $"{context}: 팝오버 차단막 {name}이(가) 켜진 채 남아 그 화면 영역의 클릭관통이 해제됩니다.");
                }
            }
        }

        private GearRadialMenuWidget FanWidget() => _gear.GetComponent<GearRadialMenuWidget>();

        // ==================== ⑦ 클릭관통 차단 영역이 버튼까지 덮는다 ====================

        [UnityTest]
        public IEnumerator InteractiveRectCoversButtonsOnlyWhileExpanded()
        {
            yield return LoadSceneAndResolve();

            Rect closed = _gear.InteractiveScreenRect;
            Assert.AreEqual(_gear.IconScreenRect.width, closed.width, 0.5f,
                "접혀 있는데 판정 사각형이 톱니보다 넓습니다 — 그만큼 애먼 클릭을 가로챕니다(비침해 위반).");

            yield return OpenMenuByShortClick();

            Rect open = _gear.InteractiveScreenRect;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.IsTrue(open.Contains(_gear.MenuButtonScreenCenter((GearMenuButton)i)),
                    $"{i}번 버튼이 클릭관통 차단 사각형 밖입니다 — 눌러도 클릭이 밑의 앱으로 샙니다.");
            }
            Assert.Greater(open.width * open.height, closed.width * closed.height,
                "펼쳤는데 판정 사각형이 커지지 않았습니다.");

            ClickAt(_gear.IconScreenCenter);
            yield return new WaitForSecondsRealtime(GearRadialMenuWidget.CollapseUserSeconds + 0.15f);

            Rect again = _gear.InteractiveScreenRect;
            Assert.AreEqual(closed.width, again.width, 0.5f, "접었는데 판정 사각형이 원래 크기로 돌아오지 않았습니다.");
            Assert.AreEqual(closed.height, again.height, 0.5f, "접었는데 판정 사각형 높이가 원래대로 돌아오지 않았습니다.");
        }
    }
}
