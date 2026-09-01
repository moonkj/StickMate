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
    /// ★ 부채꼴 <b>호버 이름표</b> 회귀 — 2026-08-31 사용자 지시:
    /// <i>"기어메뉴에서 4가지중 마우스로 선택되고있는 메뉴만 텍스트로 어떤 메뉴인지 이름이 보여야함"</i>.
    ///
    /// 이 요구는 앞선 지시("버튼 메뉴들의 텍스트는 전부삭제")와 짝을 이룬다: <b>상시 이름표는 없고,
    /// 커서가 올라간 하나만 이름이 보인다.</b> 그래서 이 테스트가 잠그는 것은 두 방향 모두다.
    ///  ① 아무 데도 안 올렸을 때 — 이름이 <b>하나도</b> 안 보인다(옛 라벨 알약의 부활 방지).
    ///  ② 한 버튼에 올렸을 때 — <b>그 버튼의 이름만</b> 보인다.
    ///  ③ 이름표가 <b>배치와 클릭관통 차단 영역에 개입하지 않는다</b>(36-3 기하 + 원칙 2 보호).
    /// </summary>
    public sealed class GearMenuHoverLabelTests
    {
        private InfoGearIconWidget _gear;
        private GearRadialMenuWidget _fan;
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

        [TearDown]
        public void ClearInjectedCursor()
        {
            if (_gear != null) _gear.ClearHoverCursorForTests();
        }

        private IEnumerator LoadSceneAndOpenFan()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _gear = Object.FindFirstObjectByType<InfoGearIconWidget>();
            Assert.IsNotNull(_gear, "씬에 InfoGearIconWidget이 없습니다.");
            _fan = _gear.GetComponent<GearRadialMenuWidget>();
            Assert.IsNotNull(_fan, "톱니와 같은 GameObject에 GearRadialMenuWidget이 없습니다.");

            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.MenuReadySeconds + 0.25f);
            Assert.IsTrue(_gear.IsMenuExpanded, "짧은 클릭 후에도 부채꼴이 펼쳐지지 않았습니다.");
        }

        /// <summary>
        /// ★ 커서를 <b>그 버튼 위에 실제로 놓는다</b>. <c>GearRadialMenuWidget.SetHover</c>를 직접 부르지
        /// 않는 이유: 호버의 소유자는 <see cref="InfoGearIconWidget"/>의 폴링이라, 밖에서 SetHover를
        /// 부르면 다음 프레임에 폴링이 덮어쓴다(첫 작성본이 정확히 그래서 전부 빈 이름표를 봤다).
        /// 커서를 주입하면 히트테스트 → 호버 → 이름표가 전부 <b>실제 코드 경로</b>로 흐른다.
        /// </summary>
        private IEnumerator HoverButton(int index)
        {
            _gear.FeedHoverCursorForTests(_gear.MenuButtonScreenCenter((GearMenuButton)index));
            yield return SettleHover();
        }

        /// <summary>커서를 어느 버튼에서도 멀리 떨어진 곳(부채꼴 밖)으로 옮긴다.</summary>
        private IEnumerator HoverNothing()
        {
            _gear.FeedHoverCursorForTests(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return SettleHover();
        }

        /// <summary>호버가 반영되고 페이드(0.09초)가 끝날 때까지 기다린다.</summary>
        private IEnumerator SettleHover()
        {
            yield return new WaitForSecondsRealtime(GearRadialMenuWidget.HoverLabelFadeSeconds + 0.25f);
        }

        // ==================== ① 아무것도 안 올렸으면 이름이 없다 ====================

        /// <summary>★ "텍스트 전부 삭제" 쪽 요구 — 커서가 버튼 밖이면 화면에 이름이 하나도 없어야 한다.</summary>
        [UnityTest]
        public IEnumerator NoNameIsShownWhileTheCursorIsOffEveryButton()
        {
            yield return LoadSceneAndOpenFan();

            yield return HoverNothing();

            Assert.IsEmpty(_fan.VisibleHoverLabel,
                $"커서가 버튼 밖인데 이름표 \"{_fan.VisibleHoverLabel}\"가 보입니다 — " +
                "상시 이름표는 2026-08-31 지시로 폐지됐습니다(옛 라벨 알약이 되살아났는지 확인하세요).");
        }

        // ==================== ② 올린 하나만 이름이 보인다 ====================

        /// <summary>
        /// ★ 핵심 요구 — 네 버튼 각각에 커서를 올렸을 때, 화면의 이름표가 <b>그 버튼의 이름</b>이어야
        /// 한다. 이름표 인스턴스가 하나뿐이므로 "둘이 동시에 보이는" 상태는 구조적으로 불가능하고,
        /// 여기서는 <b>올린 것과 보이는 것이 일치하는가</b>를 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator OnlyTheHoveredButtonShowsItsName()
        {
            yield return LoadSceneAndOpenFan();

            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                yield return HoverButton(i);

                string expected = GearRadialMenuWidget.NameOf(i);
                Assert.IsNotEmpty(expected, $"{i}번 버튼에 이름이 정의돼 있지 않습니다.");
                Assert.AreEqual(expected, _fan.VisibleHoverLabel,
                    $"{i}번 버튼에 커서를 올렸는데 이름표가 \"{_fan.VisibleHoverLabel}\"입니다 — " +
                    $"\"{expected}\"여야 합니다.");
            }
        }

        /// <summary>네 이름이 서로 달라야 한다 — 같은 이름이 둘이면 이름표가 있으나 마나다.</summary>
        [Test]
        public void EveryEntryPointHasADistinctName()
        {
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.IsNotEmpty(GearRadialMenuWidget.NameOf(i), $"{i}번 버튼 이름이 비어 있습니다.");
                for (int j = i + 1; j < GearRadialMenuWidget.ButtonCount; j++)
                {
                    Assert.AreNotEqual(GearRadialMenuWidget.NameOf(i), GearRadialMenuWidget.NameOf(j),
                        $"{i}번과 {j}번 버튼의 이름이 같습니다 — 이름표가 구별에 쓸모가 없어집니다.");
                }
            }
            Assert.AreEqual("행동", GearRadialMenuWidget.NameOf((int)GearMenuButton.Action),
                "④ 신규 진입점의 이름이 [행동]이 아닙니다.");
        }

        /// <summary>커서가 벗어나면 이름이 사라진다 — 남아 있으면 그때부터는 거짓말이다.</summary>
        [UnityTest]
        public IEnumerator TheNameDisappearsWhenTheCursorLeaves()
        {
            yield return LoadSceneAndOpenFan();

            yield return HoverButton((int)GearMenuButton.Todo);
            Assume.That(_fan.VisibleHoverLabel, Is.EqualTo(GearRadialMenuWidget.NameOf((int)GearMenuButton.Todo)),
                "사전 조건: 호버 이름표가 떠 있어야 합니다.");

            yield return HoverNothing();
            Assert.IsEmpty(_fan.VisibleHoverLabel,
                "커서가 벗어났는데 이름표가 남아 있습니다 — 어느 버튼의 이름인지 알 수 없는 글자가 됩니다.");
        }

        /// <summary>부채꼴이 접히면 이름표도 함께 사라진다(떠 있는 글자만 남는 사고 방지).</summary>
        [UnityTest]
        public IEnumerator TheNameIsGoneAfterTheFanCollapses()
        {
            yield return LoadSceneAndOpenFan();

            yield return HoverButton((int)GearMenuButton.FocusMode);
            Assume.That(_fan.VisibleHoverLabel, Is.Not.Empty, "사전 조건: 호버 이름표가 떠 있어야 합니다.");

            _fan.Collapse(GearMenuCollapseMode.User, "PlayMode 테스트");
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.IsFalse(_fan.IsVisible, "접힘이 끝나지 않았습니다.");
            Assert.IsEmpty(_fan.VisibleHoverLabel, "부채꼴이 접혔는데 이름표만 화면에 남았습니다.");
        }

        // ==================== ③ 이름표는 기하/비침해에 개입하지 않는다 ====================

        /// <summary>
        /// ★★ 36-3 보호 — 이름표가 떠도 <b>버튼 위치와 클램프 상자가 변하지 않아야</b> 한다.
        /// 예전 라벨 알약이 상자 계산에 끼어들어 기본 위치에서 평행이동 35.5pt를 만들었고, 36-3의 전수
        /// 계산은 "상자가 56×56 정사각"이라는 전제 위에 서 있다. 이름표가 그 전제를 건드리면 근거 전체가
        /// 조용히 무너진다.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHoverLabelChangesNeitherLayoutNorClickBlocking()
        {
            yield return LoadSceneAndOpenFan();

            yield return HoverNothing();

            var centersBefore = new Vector2[GearRadialMenuWidget.ButtonCount];
            var boxesBefore = new Rect[GearRadialMenuWidget.ButtonCount];
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                centersBefore[i] = _fan.ButtonScreenCenter(i);
                boxesBefore[i] = _fan.ClampBoxPoints(i);
            }
            Rect unionBefore = _fan.UnionScreenRect;

            yield return HoverButton((int)GearMenuButton.Character);
            Assume.That(_fan.VisibleHoverLabel, Is.Not.Empty, "사전 조건: 호버 이름표가 떠 있어야 합니다.");

            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Assert.AreEqual(centersBefore[i].x, _fan.ButtonScreenCenter(i).x, 0.5f,
                    $"이름표가 뜨자 {i}번 버튼이 가로로 움직였습니다 — 이름표는 배치에 참여하면 안 됩니다.");
                Assert.AreEqual(centersBefore[i].y, _fan.ButtonScreenCenter(i).y, 0.5f,
                    $"이름표가 뜨자 {i}번 버튼이 세로로 움직였습니다.");

                Rect box = _fan.ClampBoxPoints(i);
                Assert.AreEqual(boxesBefore[i].width, box.width, 0.01f,
                    $"이름표가 뜨자 {i}번 버튼의 클램프 상자 폭이 변했습니다 — 36-3의 56×56 전제가 깨집니다.");
                Assert.AreEqual(boxesBefore[i].height, box.height, 0.01f,
                    $"이름표가 뜨자 {i}번 버튼의 클램프 상자 높이가 변했습니다.");
            }

            // ★ 원칙 2 — 이름표는 누를 수 있는 물건이 아니므로 클릭관통 차단 영역을 넓히면 안 된다.
            Assert.AreEqual(unionBefore.width, _fan.UnionScreenRect.width, 0.5f,
                "이름표가 뜨자 클릭관통 차단 영역이 넓어졌습니다 — 누를 수도 없는 글자 때문에 밑의 앱이 " +
                "클릭을 잃습니다(비침해 원칙 2).");
            Assert.AreEqual(unionBefore.height, _fan.UnionScreenRect.height, 0.5f,
                "이름표가 뜨자 클릭관통 차단 영역이 세로로 넓어졌습니다.");
        }

        /// <summary>
        /// ★ 이름표가 <b>형제 버튼을 덮지 않는다</b> — 2026-09-01 페르소나(소은) #1 회귀.
        ///
        /// <para>알약이 원 <b>아래</b> 39pt에 놓이던 시절, 이웃 버튼 중심은 57.5pt밖에 안 떨어져 있어
        /// 4개 중 3개에서 알약이 옆 원을 물었다. 알약 바탕이 불투명이라 덮인 부분은 통째로 사라지는데
        /// 클릭 판정은 그대로 남는다 — 이 프로젝트가 <b>최악</b>이라고 부르는 "안 보이는데 눌리는" 상태의
        /// 거울상이다. 그래서 겹침 자체를 없앴다(반지름 방향 배치).</para>
        ///
        /// <para>원 대 사각형의 <b>실제 거리</b>로 잰다 — 버튼의 정사각 판정 상자로 재면 원 밖의 모서리
        /// 때문에 그리지도 않은 겹침을 잡아낸다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheHoverLabelNeverCoversAnotherButton()
        {
            yield return LoadSceneAndOpenFan();

            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                yield return HoverButton(i);

                Rect label = _fan.HoverLabelScreenRect;
                Assert.Greater(label.width, 1f, $"{i}번 버튼의 이름표 사각형이 비어 있습니다.");

                for (int j = 0; j < GearRadialMenuWidget.ButtonCount; j++)
                {
                    if (j == i) continue;
                    Rect circle = _fan.ButtonScreenRect(j);
                    float radius = circle.width * 0.5f;
                    Vector2 c = circle.center;

                    float dx = Mathf.Max(Mathf.Max(label.xMin - c.x, 0f), c.x - label.xMax);
                    float dy = Mathf.Max(Mathf.Max(label.yMin - c.y, 0f), c.y - label.yMax);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    Assert.Greater(distance, radius,
                        $"[{GearRadialMenuWidget.NameOf(i)}] 이름표가 {j}번 버튼" +
                        $"([{GearRadialMenuWidget.NameOf(j)}])을 {radius - distance:F1}px 덮습니다 — " +
                        "불투명 알약이 형제 버튼을 가리면 사용자는 자기가 무엇을 누르는지 보지 못한 채 " +
                        "누르게 됩니다(이름표는 클릭 판정에 들어가지도 않습니다).");
                }
            }
        }

        /// <summary>이름표는 화면 안에 있어야 한다 — 화면 밖이면 그 버튼만 이름을 못 읽는다.</summary>
        [UnityTest]
        public IEnumerator TheHoverLabelStaysOnScreenForEveryButton()
        {
            yield return LoadSceneAndOpenFan();

            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                yield return HoverButton(i);

                Rect label = _fan.HoverLabelScreenRect;
                Assert.Greater(label.width, 1f, $"{i}번 버튼의 이름표 사각형이 비어 있습니다.");
                Assert.GreaterOrEqual(label.xMin, -0.5f, $"{i}번 이름표가 화면 왼쪽 밖입니다({label}).");
                Assert.GreaterOrEqual(label.yMin, -0.5f, $"{i}번 이름표가 화면 아래쪽 밖입니다({label}).");
                Assert.LessOrEqual(label.xMax, Screen.width + 0.5f, $"{i}번 이름표가 화면 오른쪽 밖입니다({label}).");
                Assert.LessOrEqual(label.yMax, Screen.height + 0.5f, $"{i}번 이름표가 화면 위쪽 밖입니다({label}).");
            }
        }
    }
}
