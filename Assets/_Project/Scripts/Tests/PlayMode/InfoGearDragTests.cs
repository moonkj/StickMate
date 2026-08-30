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
    /// ★ 우상단 톱니를 <b>길게 눌러 옮기기</b>(Interaction/InfoGearIconWidget.cs) 회귀 테스트 —
    /// 2026-08-30 사용자 요청("캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘").
    ///
    /// ============================================================================
    /// 무엇을 절대 조건으로 잠그는가
    /// ============================================================================
    ///  ① <b>짧게 클릭</b>하면 예전처럼 회전이 시작되고 아이콘은 <b>움직이지 않는다</b>(네거티브 컨트롤).
    ///  ② <b>길게 누르면</b>(임계 <see cref="InfoGearIconWidget.DragLongPressSeconds"/>초) 드래그로 바뀌고,
    ///     그 뒤 떼도 <b>캐릭터 창이 열리지 않는다</b> — 이 요구에서 가장 흔한 실패가 "옮기려고 눌렀는데
    ///     창부터 뜬다"이므로 창이 안 열린다는 사실 자체를 단언한다.
    ///  ③ 누른 채 임계 거리 이상 끌면 시간을 채우기 전에도 드래그다(일반 드래그 관례).
    ///  ④ 드래그 중에는 아이콘이 커서를 따라가고, <b>히트 사각형도 함께 따라간다</b>(안 따라가면 다음
    ///     프레임에 "기어 밖"이 되어 드래그가 끊긴다).
    ///  ⑤ 뗀 위치가 저장 파일에 남아 <b>씬을 다시 띄워도</b> 그 자리에 뜬다(= 재시작 유지).
    ///  ⑥ 어디로 끌든 <b>화면 밖으로 나가지 않는다</b>. 저장된 위치가 화면 밖이어도 다음 실행에 복구된다.
    ///
    /// 입력 주입은 Interaction/StickmanClickHitbox.SimulateMouseDownForTests와 같은 관례를 따른다 —
    /// 테스트 전용 분기를 만들지 않고 <b>실제 입력이 지나가는 같은 함수</b>(ProcessPointer)에 버튼 상태와
    /// 커서 좌표를 먹인다. 실제 전역 입력은 합성 입력에 반응하지 않으므로 이 경로가 유일한 수단이다.
    /// 에디터에서는 전역 버튼 서비스가 없어(NullPlatformWindowService) 실제 폴링이 이 주입을 방해하지 않는다.
    ///
    /// 저장 파일은 실행 중인 실제 앱의 것과 같은 경로이므로 전후로 백업/복원한다(EditMode 영속화
    /// 테스트와 같은 관례, 대상은 CharacterSaveStore.FilePath 하나뿐).
    /// </summary>
    public sealed class InfoGearDragTests
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

        /// <summary>매 테스트를 "아직 한 번도 옮긴 적 없는" 상태에서 시작한다. 메모리 값만 지우면
        /// 부족하다 — 씬을 로드하면 CharacterProgressionDirector가 저장 <b>파일</b>을 다시 읽어 앞선
        /// 테스트가 남긴 위치를 되살리기 때문이다(테스트 실행 순서에 의존하지 않게 파일까지 정리한다).</summary>
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

        /// <summary>화면 안쪽의 안전한 목표 지점(Unity 스크린 픽셀) — 화면 크기가 작은 배치 실행에서도
        /// 클램프에 걸리지 않는 위치를 고른다.</summary>
        private static Vector2 SafeInsideTarget()
            => new Vector2(Screen.width * 0.45f, Screen.height * 0.5f);

        // ==================== ① 짧게 클릭 (네거티브 컨트롤) ====================

        [UnityTest]
        public IEnumerator ShortClickStillOpensWindowAndDoesNotMoveIcon()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            Assert.IsFalse(_gear.HasCustomPosition, "테스트 시작 시점에 이미 옮겨진 상태입니다.");

            _gear.FeedPointerForTests(true, start);
            _gear.FeedPointerForTests(false, start);   // 시간/거리 임계를 둘 다 못 넘긴 순수 클릭.

            Assert.IsFalse(_gear.IsDraggingIcon, "짧은 클릭이 드래그로 처리됐습니다.");
            Assert.IsTrue(_gear.IsSpinning, "짧은 클릭인데 회전이 시작되지 않았습니다 — 기존 동작이 깨졌습니다.");
            Assert.IsFalse(_gear.HasCustomPosition, "짧은 클릭인데 위치가 옮겨졌습니다.");

            yield return new WaitForSecondsRealtime(0.9f);   // 회전 0.52초 + 여유.
            Assert.IsTrue(_window != null && _window.IsOpen, "회전이 끝났는데 캐릭터 창이 열리지 않았습니다.");
            Assert.AreEqual(start.x, _gear.IconScreenCenter.x, 1f, "짧은 클릭 후 아이콘이 가로로 움직였습니다.");
            Assert.AreEqual(start.y, _gear.IconScreenCenter.y, 1f, "짧은 클릭 후 아이콘이 세로로 움직였습니다.");

            _window.Close("테스트 정리");
            Debug.Log($"[톱니드래그테스트] 짧은 클릭 유지 확인 — 중심 {start} 그대로, 창 열림.");
        }

        // ==================== ② 길게 누르면 드래그(창이 열리면 안 된다) ====================

        [UnityTest]
        public IEnumerator LongPressTurnsIntoDragAndNeverOpensWindow()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, start);
            Assert.IsFalse(_gear.IsDraggingIcon, "누르자마자 드래그가 됐습니다 — 임계 시간 전에는 아직 클릭 후보여야 합니다.");

            // 임계 시간 직전: 아직 드래그가 아니어야 한다(네거티브 컨트롤 — 임계값이 실제로 지켜지는가).
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds * 0.5f);
            _gear.FeedPointerForTests(true, start);
            Assert.IsFalse(_gear.IsDraggingIcon,
                $"임계({InfoGearIconWidget.DragLongPressSeconds:F2}초)의 절반만 눌렀는데 드래그로 전환됐습니다.");

            // 임계 시간 이후: 드래그여야 한다.
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds * 0.7f + 0.05f);
            _gear.FeedPointerForTests(true, start);
            Assert.IsTrue(_gear.IsDraggingIcon,
                $"{InfoGearIconWidget.DragLongPressSeconds:F2}초 넘게 눌렀는데 드래그로 전환되지 않았습니다.");

            // 커서를 옮기면 아이콘과 히트 사각형이 함께 따라온다.
            Vector2 target = SafeInsideTarget();
            _gear.FeedPointerForTests(true, target);
            yield return null;

            Assert.AreEqual(target.x, _gear.IconScreenCenter.x, 2f, "아이콘이 커서를 가로로 따라오지 않았습니다.");
            Assert.AreEqual(target.y, _gear.IconScreenCenter.y, 2f, "아이콘이 커서를 세로로 따라오지 않았습니다.");
            Assert.IsTrue(_gear.IconScreenRect.Contains(target),
                "히트 사각형이 아이콘을 따라오지 않았습니다 — 다음 프레임에 커서가 '기어 밖'이 되어 드래그가 끊깁니다.");

            _gear.FeedPointerForTests(false, target);
            Assert.IsFalse(_gear.IsDraggingIcon, "버튼을 뗐는데 드래그가 계속됩니다.");
            Assert.IsFalse(_gear.IsSpinning, "드래그였는데 회전(=창 열기 예약)이 시작됐습니다.");

            yield return new WaitForSecondsRealtime(0.9f);
            Assert.IsFalse(_window != null && _window.IsOpen,
                "드래그였는데 캐릭터 창이 열렸습니다 — 클릭과 드래그가 구분되지 않았습니다.");
            Assert.AreEqual(target.x, _gear.IconScreenCenter.x, 2f, "떼고 나서 아이콘이 제자리에 고정되지 않았습니다.");

            Debug.Log($"[톱니드래그테스트] 길게 누름 -> 드래그 -> 고정 확인 — {start} -> {target}, 창 열림 없음.");
        }

        // ==================== ③ 거리 임계로도 드래그가 시작된다 ====================

        [UnityTest]
        public IEnumerator DraggingFarEnoughStartsDragBeforeTheTimeThreshold()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, start);

            // 시간은 거의 흐르지 않았지만 임계 거리를 넘겼다 -> 즉시 드래그(일반 드래그 UX 관례).
            // 40px는 어떤 DPI 배율에서도 임계 4pt를 확실히 넘는다(Retina 2x에서도 20pt).
            Vector2 moved = start + new Vector2(-40f, -40f);
            _gear.FeedPointerForTests(true, moved);

            Assert.IsTrue(_gear.IsDraggingIcon,
                $"임계({InfoGearIconWidget.DragMoveThreshold:F0}pt)의 3배를 끌었는데 드래그로 전환되지 않았습니다.");

            _gear.FeedPointerForTests(false, moved);
            Assert.IsFalse(_gear.IsSpinning, "끌었다 뗐는데 클릭으로 처리됐습니다.");
            yield return null;
        }

        // ==================== ⑤ 저장 -> 씬 재로드 후에도 그 자리 ====================

        [UnityTest]
        public IEnumerator DroppedPositionSurvivesSceneReload()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            Vector2 target = SafeInsideTarget();
            _gear.FeedPointerForTests(true, start);
            _gear.FeedPointerForTests(true, target);      // 거리 임계로 즉시 드래그.
            Assert.IsTrue(_gear.IsDraggingIcon);
            _gear.FeedPointerForTests(false, target);
            yield return null;

            Vector2 savedPoints = _gear.IconCenterPoints;
            Assert.IsTrue(_gear.HasCustomPosition, "뗐는데 '옮긴 적 없음' 상태입니다.");
            Assert.IsTrue(File.Exists(CharacterSaveStore.FilePath), "위치를 확정했는데 저장 파일이 없습니다.");

            // 메모리 값을 지운 뒤 파일에서만 복원되게 한다 — 파일 왕복을 진짜로 검증하기 위해서다.
            UiLayoutModel.ResetForTesting();
            Assert.IsFalse(UiLayoutModel.HasGearCenter);

            yield return LoadSceneAndResolve();           // 씬 재로드 = 재시작과 같은 경로(저장 파일 Load 포함).
            yield return null;

            Assert.IsTrue(_gear.HasCustomPosition,
                "재시작 후 옮긴 위치가 복원되지 않았습니다 — 우상단 기본 위치로 돌아가 버립니다.");
            Assert.AreEqual(savedPoints.x, _gear.IconCenterPoints.x, 0.6f, "복원된 가로 위치가 다릅니다.");
            Assert.AreEqual(savedPoints.y, _gear.IconCenterPoints.y, 0.6f, "복원된 세로 위치가 다릅니다.");

            Debug.Log($"[톱니드래그테스트] 저장/복원 확인 — ({savedPoints.x:F1}, {savedPoints.y:F1})pt가 씬 재로드 후에도 유지됨.");
        }

        // ==================== ⑥ 화면 밖으로 못 나간다 ====================

        [UnityTest]
        public IEnumerator DragCannotPushIconOffScreen()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;

            // 화면 오른쪽/위로 한참 밖까지 끌어본다.
            _gear.FeedPointerForTests(true, start);
            _gear.FeedPointerForTests(true, new Vector2(Screen.width + 600f, Screen.height + 600f));
            yield return null;
            AssertRectFullyOnScreen("오른쪽 위 바깥으로 끌었을 때");

            // 이어서 왼쪽/아래로도.
            _gear.FeedPointerForTests(true, new Vector2(-600f, -600f));
            yield return null;
            AssertRectFullyOnScreen("왼쪽 아래 바깥으로 끌었을 때");

            _gear.FeedPointerForTests(false, new Vector2(-600f, -600f));
            yield return null;
            AssertRectFullyOnScreen("떼고 난 뒤");
        }

        [UnityTest]
        public IEnumerator SavedPositionOutsideTheScreenIsPulledBackOnStartup()
        {
            // 외장 모니터를 떼서 화면이 좁아진 상황과 같다 — 저장 파일에 화면 밖 좌표가 들어 있다.
            // (메모리 값만 바꾸면 씬 로드 시 파일을 다시 읽으면서 덮여버리므로 파일에 써 둔다.)
            UiLayoutModel.SetGearCenter(new Vector2(99999f, 99999f));
            Assert.IsTrue(CharacterSaveStore.Save(), "준비 단계 저장에 실패했습니다.");

            yield return LoadSceneAndResolve();
            yield return null;

            AssertRectFullyOnScreen("화면 밖 좌표가 저장돼 있던 채로 시작했을 때");
            Debug.Log($"[톱니드래그테스트] 화면 밖 저장값 복구 확인 — 사각형 {_gear.IconScreenRect}.");
        }

        private void AssertRectFullyOnScreen(string context)
        {
            Rect r = _gear.IconScreenRect;
            Assert.GreaterOrEqual(r.xMin, -0.5f, $"{context}: 아이콘이 화면 왼쪽 밖으로 나갔습니다({r}).");
            Assert.GreaterOrEqual(r.yMin, -0.5f, $"{context}: 아이콘이 화면 아래쪽 밖으로 나갔습니다({r}).");
            Assert.LessOrEqual(r.xMax, Screen.width + 0.5f, $"{context}: 아이콘이 화면 오른쪽 밖으로 나갔습니다({r}).");
            Assert.LessOrEqual(r.yMax, Screen.height + 0.5f, $"{context}: 아이콘이 화면 위쪽 밖으로 나갔습니다({r}).");
        }
    }
}
