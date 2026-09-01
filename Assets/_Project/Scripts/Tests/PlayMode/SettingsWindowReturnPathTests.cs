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
    /// ★ 설정창의 <b>돌아갈 문</b>과 <b>준비 중인 탭의 표시</b> — 2026-09-01 페르소나(민지) M8 / M7.
    ///
    /// ============================================================================
    /// M8 — 배타 규칙은 옳았고, 없던 것은 되돌아오는 길이었다
    /// ============================================================================
    /// 설정창의 <b>유일한 마우스 진입점</b>은 캐릭터 정보창 헤더의 [설정] 칩이다. 그런데 설정창이
    /// 열리면서 그 정보창을 닫아 버리고(배타 모달 — 그 자체는 옳다) 설정창을 닫아도 돌아오지 않아,
    /// "장비 구경 → [설정] → [✕] → <b>빈 바탕화면</b>"이라는 막다른 길이 있었다. 시트를 걷으면
    /// 그 밑에 있던 창이 그대로 있어야 한다.
    ///
    /// 이 파일이 <b>진짜 클릭 좌표</b>로 확인하는 이유: 복귀를 <c>Open()/Close()</c> 직접 호출로만
    /// 검증하면 "칩을 눌렀을 때 그 경로를 타는가"가 빠진다 — 실제 사용자는 칩을 누른다.
    ///
    /// ============================================================================
    /// M7 — 회색은 탭을 <b>누르기 전에</b> 보여야 한다
    /// ============================================================================
    /// 미구현 3탭의 회색 처리가 탭 <b>안쪽 행</b>에만 있어서, 눌러 봐야 빈 탭인 걸 알 수 있었다.
    /// 첫 방문에서 5탭 중 3탭이 헛걸음이다. 색 상수를 테스트가 다시 적지 않고
    /// <see cref="SettingsWindow.TabLabelColor"/>로 <b>서로 다른가</b>만 본다 — 팔레트가 바뀌어도
    /// 이 테스트가 지키려는 사실("구분된다")은 그대로다.
    /// </summary>
    public sealed class SettingsWindowReturnPathTests
    {
        private const string LogPrefix = "[설정창복귀-TEST]";

        private CharacterInfoWindow _info;
        private SettingsWindow _settings;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다 — " +
                "SceneBootstrapper.EnsurePrefabComponents가 프리팹에 붙였는지 확인하세요.");
        }

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_info != null && _info.IsOpen) _info.Close("테스트 정리");
            _settings = null;
            _info = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        // ==================== M8 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingSettingsReopensTheInfoWindowItReplaced()
        {
            yield return LoadScene();

            _info.Toggle("테스트");
            yield return null;
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 전제: 정보창이 열려 있어야 합니다.");

            Rect chip = _info.SettingsChipScreenRect;
            Assert.Greater(chip.width, 1f, $"{LogPrefix} 헤더의 [설정] 칩 사각형이 비었습니다.");
            _info.FeedClickForTests(chip.center);
            yield return null;

            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} [설정] 칩을 눌렀는데 설정창이 열리지 않았습니다.");
            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 전제: 설정창은 배타 모달이라 정보창을 닫습니다(이 규칙 자체는 바꾸지 않았습니다).");

            _settings.FeedClickForTests(_settings.CloseButtonScreenRect.center);
            yield return null;

            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} [✕]를 눌렀는데 설정창이 닫히지 않았습니다.");
            Assert.IsTrue(_info.IsOpen,
                $"{LogPrefix} 설정창을 닫았는데 보던 캐릭터/장비창이 돌아오지 않았습니다 — " +
                "이 앱에서 설정창의 마우스 진입점은 그 창의 [설정] 칩 하나뿐이라, 돌아오지 않으면 " +
                "톱니 → [캐릭터]를 처음부터 다시 밟아야 하는 막다른 길이 됩니다(M8).");
        }

        /// <summary>바깥 클릭으로 닫아도 같다 — 닫는 방법에 따라 규칙이 달라지면 사용자가 규칙을 세울 수 없다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingSettingsByClickingOutsideAlsoReturns()
        {
            yield return LoadScene();

            _info.Toggle("테스트");
            yield return null;
            _info.FeedClickForTests(_info.SettingsChipScreenRect.center);
            yield return null;
            Assume.That(_settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려 있어야 합니다.");

            // ★ "창 밖"을 좌표로 못 박지 않는다 — 배치모드의 좁은 화면(예: 640×480)에서는 720×560 패널이
            //   화면을 통째로 덮어 <b>바깥이 존재하지 않는다</b>(첫 작성본이 (4,4)를 찍었다가 그 지점이
            //   패널 <b>안쪽</b>이라 실패했다). 패널 사각형에서 실제 바깥 한 점을 구하고, 그런 점이
            //   화면에 없으면 이 환경에서는 검증할 수 없다고 <b>말하고</b> 넘어간다.
            Rect panel = _settings.PanelScreenRect;
            float outsideX = panel.xMin - 20f;
            if (outsideX < 0f) outsideX = panel.xMax + 20f;
            if (outsideX < 0f || outsideX > Screen.width)
            {
                Assert.Ignore($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 창({panel})보다 좁아 " +
                    "\"창 밖\" 지점이 없습니다 — 이 환경에서는 이 경로를 검증할 수 없습니다.");
            }

            _settings.FeedClickForTests(new Vector2(outsideX, Mathf.Clamp(panel.center.y, 1f, Screen.height - 1f)));
            yield return null;

            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} 창 밖을 눌렀는데 설정창이 닫히지 않았습니다.");
            Assert.IsTrue(_info.IsOpen,
                $"{LogPrefix} 창 밖 클릭으로 닫았을 때만 정보창이 돌아오지 않습니다 — 닫는 방법마다 " +
                "규칙이 다르면 그것도 원칙 1의 불일치입니다.");
        }

        /// <summary>★ 반대 방향 — 정보창이 <b>열려 있지 않았다면</b> 설정창을 닫아도 아무것도 열리지
        /// 않아야 한다. 부르지 않은 창이 뜨는 것은 그 자체로 방해다(원칙 2).</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingSettingsOpenedFromHotkeyDoesNotSummonTheInfoWindow()
        {
            yield return LoadScene();

            Assume.That(_info.IsOpen, Is.False, $"{LogPrefix} 전제: 정보창은 닫혀 있어야 합니다.");

            _settings.Open("테스트 — 단축키 경로");
            yield return null;
            Assume.That(_settings.IsOpen, Is.True);

            _settings.Close("테스트 — [✕]");
            yield return null;

            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 열려 있지도 않던 정보창이 설정창을 닫자 튀어나왔습니다 — 복귀는 " +
                "<b>내가 밀어낸 창</b>에만 적용됩니다.");
        }

        // ==================== 소은 #7-b — 페이지 칩은 내용 위에 앉지 않는다 ====================

        /// <summary>
        /// ★ [▲][▼]가 내용 영역 <b>밖</b>에 있어야 한다 — 2026-09-01 페르소나(소은) #7-b / (민지) M12.
        ///
        /// <para>예전 자리는 패널 y 496~520pt인데 내용 뷰포트가 88~526pt였다. 실물에서 [▼]가 "잡담 빈도"의
        /// "100%" 값 라벨을 덮었고, 그 줄이 <c>[−] ▬▬ [+] [▲] [▼] 100%</c>로 읽혀 <b>페이지 스크롤이
        /// 슬라이더 미세 조정처럼</b> 보였다. 행을 하나 더하거나 캡션을 한 줄 붙이는 것만으로 어떤 행이든
        /// 그 밴드에 들어올 수 있으므로, 좌표를 손으로 비교하는 대신 <b>겹치지 않는다</b>를 잠근다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PageChipsNeverSitOnTopOfTheContentArea()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            Rect content = _settings.ContentViewportScreenRect;
            Assert.Greater(content.width, 1f, $"{LogPrefix} 내용 영역 사각형이 비었습니다.");

            foreach (var chip in new[] { ("▲", _settings.PageUpScreenRect), ("▼", _settings.PageDownScreenRect) })
            {
                Assert.Greater(chip.Item2.width, 1f, $"{LogPrefix} [{chip.Item1}] 칩 사각형이 비었습니다.");
                Assert.IsFalse(chip.Item2.Overlaps(content),
                    $"{LogPrefix} [{chip.Item1}] 칩({chip.Item2})이 내용 영역({content}) 위에 앉아 있습니다 — " +
                    "그 아래 어떤 행이 오느냐에 따라 값 라벨이나 토글이 가려지고, 페이지 버튼이 그 행의 " +
                    "일부처럼 읽힙니다(소은 #7-b).");
            }
        }

        // ==================== M7 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UnimplementedTabsAreDimmedInTheTabBarBeforeBeingClicked()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            Assume.That(_settings.ActiveTab, Is.EqualTo(SettingsWindow.Tab.General),
                $"{LogPrefix} 전제: 처음 열면 [일반] 탭입니다.");

            Assert.IsFalse(SettingsWindow.IsTabImplemented(SettingsWindow.Tab.Event),
                $"{LogPrefix} 전제: [이벤트] 탭은 아직 비어 있습니다.");
            Assert.IsTrue(SettingsWindow.IsTabImplemented(SettingsWindow.Tab.Character),
                $"{LogPrefix} 전제: [캐릭터] 탭에는 내용이 있습니다.");

            Color ready = _settings.TabLabelColor(SettingsWindow.Tab.Character);   // 비활성 + 내용 있음
            Color empty = _settings.TabLabelColor(SettingsWindow.Tab.Event);       // 비활성 + 내용 없음

            Assert.AreNotEqual(ready, empty,
                $"{LogPrefix} 준비 중인 탭([이벤트] {empty})과 멀쩡한 탭([캐릭터] {ready})의 탭바 글자색이 " +
                "같습니다 — 눌러 봐야 빈 탭인 걸 알 수 있으면 첫 방문에서 5탭 중 3탭이 헛걸음입니다(M7).");

            float readyLuma = ready.r + ready.g + ready.b;
            float emptyLuma = empty.r + empty.g + empty.b;
            Assert.Less(emptyLuma, readyLuma,
                $"{LogPrefix} 준비 중인 탭이 더 <b>밝습니다</b>(빈 탭 {emptyLuma:F2} >= 멀쩡한 탭 {readyLuma:F2}) — " +
                "회색 처리의 방향이 뒤집혔습니다.");
        }
    }
}
