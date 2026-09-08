using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 실측 회귀 — <b>[오늘 할일] 행 글자는 상자 안에서 멈추고, 취소선은 1pt 선이다.</b>
    /// (docs/UX_WIDGETS.md <b>R6-0-2</b> · <b>UW-6-3</b>)
    ///
    /// ============================================================================
    /// 왜 소스 감사만으로는 부족한가
    /// ============================================================================
    /// <c>TodoBoardRowOverflowAuditTests</c>(EditMode)는 «처방이 걸려 있는가»를 소스로 잰다.
    /// 그것으로는 <b>잉크가 실제로 어디서 멈추는가</b>를 알 수 없다 — 이 결함의 본체가 정확히
    /// «상자는 162로 잡혀 있는데 그림은 상자를 안 지킨다»였다. 그래서 여기서는 폰트가 직접 잰
    /// <c>preferredWidth</c>와 실제 <c>RectTransform</c> 화면 사각형만 본다.
    ///
    /// ============================================================================
    /// 이 테스트가 스스로를 의심하는 지점
    /// ============================================================================
    /// <list type="bullet">
    /// <item><b>양성 대조</b>: 짧은 항목은 <b>한 글자도 잘리지 않는다</b>. 이게 없으면
    ///   "전부 말줄임" 같은 과잉 절단도 초록으로 통과한다.</item>
    /// <item><b>표본이 실제로 넘치는가</b>: 60자 항목이 잘렸다는 사실 자체(길이 감소)로 확인한다.
    ///   상자에 들어가는 짧은 글자를 넣고 "안 넘쳤다"고 말하는 거짓 초록을 막는다.</item>
    /// <item><b>글자 수 상한을 손으로 베끼지 않는다</b>:
    ///   <see cref="TodoBoardPopover.InputCharacterLimit"/>에서 읽는다.</item>
    /// </list>
    /// </summary>
    public sealed class TodoBoardRowOverflowTests
    {
        private const string LogPrefix = "[할일행넘침-TEST]";

        /// <summary>상자에 넉넉히 들어가는 짧은 항목(양성 대조용).</summary>
        private const string ShortItem = "장보기";

        /// <summary>결합 취소선 U+0336 — 옛 방식의 흔적. 라벨 문자열에 <b>한 글자도</b> 없어야 한다.</summary>
        private const char CombiningLongStrokeOverlay = '̶';

        private TodoBoardPopover _popover;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFile()
        {
            // 이 픽스처는 제품 경로(AddForTests → AddFromInput → CharacterSaveStore.Save)를 그대로 탄다.
            // 격리가 꺼진 채 돌면 개발자의 진짜 저장 파일에 테스트 항목이 남는다(절대 불변 원칙 3).
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다.");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            TodoListModel.ResetForTesting();
        }

        private IEnumerator LoadSceneAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _popover = Object.FindFirstObjectByType<TodoBoardPopover>();
            Assert.IsNotNull(_popover, $"{LogPrefix} 씬에 TodoBoardPopover가 없습니다.");

            TodoListModel.ResetForTesting();

            var anchor = new Rect(Screen.width * 0.5f - 22f, Screen.height * 0.5f - 22f, 44f, 44f);
            _popover.Open(anchor, "행 넘침 테스트");
            Assert.IsTrue(_popover.IsOpen, $"{LogPrefix} 팝오버가 열리지 않았습니다.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_popover != null && _popover.IsOpen) _popover.Close("테스트 정리");
            _popover = null;
            TodoListModel.ResetForTesting();
            yield return null;
        }

        /// <summary>입력 상한만큼의 한글 항목. 상한을 <b>숫자로 베끼지 않는다</b>.</summary>
        private static string MaxLengthItem()
        {
            const string unit = "장보기목록정리하고저녁준비하기";
            var sb = new StringBuilder(TodoBoardPopover.InputCharacterLimit);
            while (sb.Length < TodoBoardPopover.InputCharacterLimit)
                sb.Append(unit, 0, Mathf.Min(unit.Length, TodoBoardPopover.InputCharacterLimit - sb.Length));
            return sb.ToString();
        }

        // ====================================================================
        // ① 말줄임 — 잉크가 상자에서 멈춘다
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LongItemIsEllipsizedAndNeverPaintsOverTheDeleteButtonOrOutsideThePanel()
        {
            yield return LoadSceneAndOpen();

            string source = MaxLengthItem();
            _popover.AddForTests(source);
            yield return null;

            Assert.AreEqual(1, _popover.VisibleRowCount, $"{LogPrefix} 항목이 목록에 들어가지 않았습니다.");

            string drawn = _popover.RowLabelText(0);
            float ink = _popover.RowLabelInkWidth(0);
            float box = TodoBoardPopover.RowLabelWidth;

            // (a) 표본이 실제로 상자를 넘치는 길이였는가 — 이 단언이 없으면 짧은 글자를 넣고
            //     "안 넘쳤다"고 말하는 거짓 초록이 된다.
            Assert.Less(drawn.Length, source.Length,
                $"{LogPrefix} {source.Length}자 항목이 한 글자도 잘리지 않았습니다(그려진 {drawn.Length}자). " +
                "말줄임이 안 걸렸거나, 표본이 상자를 넘치지 않습니다 — 어느 쪽이든 이 실행의 판정은 무효입니다.");
            Assert.IsTrue(drawn.EndsWith(UiChrome.Ellipsis, System.StringComparison.Ordinal),
                $"{LogPrefix} 잘렸는데 말줄임표가 없습니다: \"{drawn}\" — 잘린 사실을 숨기면 " +
                "사용자는 자기가 적은 문장이 그게 전부인 줄 압니다.");

            // (b) 잉크가 상자 안에서 멈춘다.
            Assert.LessOrEqual(ink, box + 0.5f,
                $"{LogPrefix} 잉크 폭 {ink:F1}pt가 라벨 상자 {box:F1}pt를 넘었습니다.");

            // (c) 화면 좌표로 다시 잰다(같은 계기로 두 번 재지 않는다).
            //     pt→px 환산은 상자의 실측 사각형에서 뽑는다 — 캔버스 배율을 손으로 적지 않는다.
            Rect labelRect = _popover.RowLabelScreenRect(0);
            Rect deleteRect = _popover.RowDeleteScreenRect(0);
            Rect panelRect = _popover.PanelScreenRect;
            Assert.Greater(labelRect.width, 1f, $"{LogPrefix} 라벨 상자 사각형이 비었습니다.");
            Assert.Greater(deleteRect.width, 1f, $"{LogPrefix} [✕] 사각형이 비었습니다.");

            float pxPerPoint = labelRect.width / box;
            float inkRightPx = labelRect.xMin + ink * pxPerPoint;

            Assert.LessOrEqual(inkRightPx, deleteRect.xMin + 0.5f,
                $"{LogPrefix} 글자의 오른쪽 끝({inkRightPx:F1}px)이 [✕] 버튼 왼쪽 끝({deleteRect.xMin:F1}px)을 " +
                "넘었습니다 — 17자 항목이 삭제 버튼을 덮던 그 결함입니다(R6-0-2 가).");
            Assert.LessOrEqual(inkRightPx, panelRect.xMax + 0.5f,
                $"{LogPrefix} 글자가 패널 오른쪽 끝({panelRect.xMax:F1}px) 밖 {inkRightPx - panelRect.xMax:F1}px까지 " +
                "흘렀습니다 — 클릭관통 차단막 <b>바깥</b>의 바탕화면에 글자를 그리는 상태입니다(비침해 원칙 2).");

            Debug.Log($"{LogPrefix} ① 통과 — {source.Length}자 → 그려진 {drawn.Length}자, " +
                $"잉크 {ink:F1}pt ≤ 상자 {box:F1}pt, 잉크 오른끝 {inkRightPx:F1}px < [✕] {deleteRect.xMin:F1}px.");
        }

        /// <summary>★ 양성 대조 — 상자에 들어가는 항목은 <b>손대지 않고</b> 그대로 그린다.
        /// 이것이 없으면 "무조건 자른다"도 ①을 통과한다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ShortItemIsLeftExactlyAsTyped()
        {
            yield return LoadSceneAndOpen();

            _popover.AddForTests(ShortItem);
            yield return null;

            Assert.AreEqual(ShortItem, _popover.RowLabelText(0),
                $"{LogPrefix} 상자에 들어가는 짧은 항목까지 손댔습니다 — 말줄임은 넘칠 때만 개입해야 합니다.");
            Assert.LessOrEqual(_popover.RowLabelInkWidth(0), TodoBoardPopover.RowLabelWidth,
                $"{LogPrefix} 짧은 항목이 상자를 넘습니다 — 표본 선정이 잘못됐습니다.");
        }

        // ====================================================================
        // ② 취소선 — 글리프가 아니라 선
        // ====================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CompletingAnItemDrawsAOnePointLineAndNeverTouchesTheText()
        {
            yield return LoadSceneAndOpen();

            _popover.AddForTests(ShortItem);
            yield return null;

            // 음성 대조 — 미완료 항목에는 선이 없다.
            Assert.IsFalse(_popover.RowStrikeActive(0),
                $"{LogPrefix} 미완료인데 취소선이 켜져 있습니다.");

            Rect labelRect = _popover.RowLabelScreenRect(0);
            float inkBefore = _popover.RowLabelInkWidth(0);

            // 제품 경로 그대로 — 행을 클릭해 완료로 만든다.
            _popover.FeedClickForTests(_popover.RowScreenRect(0).center);
            yield return null;

            Assert.IsTrue(_popover.RowStrikeActive(0),
                $"{LogPrefix} 완료로 바꿨는데 취소선이 켜지지 않았습니다.");

            // (a) 글자에는 손대지 않는다 — 옛 방식은 글자마다 U+0336을 끼워 길이를 2배로 만들었다.
            string drawn = _popover.RowLabelText(0);
            Assert.AreEqual(ShortItem, drawn,
                $"{LogPrefix} 완료 표시가 <b>글자를 바꿨습니다</b>: \"{drawn}\" — " +
                "취소선은 그리기의 문제이지 문자열의 문제가 아닙니다(UW-6-3).");
            Assert.Less(drawn.IndexOf(CombiningLongStrokeOverlay), 0,
                $"{LogPrefix} 라벨에 U+0336 결합문자가 남아 있습니다 — Windows 폰트 폴백 미확인 위험이 " +
                "그대로입니다(R6-11 #3).");

            // (b) 선은 1pt 두께이고, 글자 폭만큼만 긋는다.
            Vector2 strike = _popover.RowStrikeSizePoints(0);
            Assert.AreEqual(1f, strike.y, 0.001f,
                $"{LogPrefix} 취소선 두께가 {strike.y}pt입니다 — 포스트잇(정본)과 같은 1pt여야 합니다.");
            Assert.AreEqual(inkBefore, strike.x, 0.5f,
                $"{LogPrefix} 취소선 폭 {strike.x:F1}pt가 글자 잉크 폭 {inkBefore:F1}pt와 다릅니다.");
            Assert.LessOrEqual(strike.x, TodoBoardPopover.RowLabelWidth + 0.5f,
                $"{LogPrefix} 취소선이 라벨 상자를 넘었습니다.");

            // (c) 같은 줄에 놓인다 — 라벨 상자의 세로 중심과 선의 세로 중심이 같아야 한다.
            Rect strikeRect = _popover.RowStrikeScreenRect(0);
            Assert.AreEqual(labelRect.center.y, strikeRect.center.y, 1f,
                $"{LogPrefix} 취소선이 글자와 다른 줄에 있습니다(라벨 중심 {labelRect.center.y:F1}px, " +
                $"선 중심 {strikeRect.center.y:F1}px).");
            Assert.AreEqual(labelRect.xMin, strikeRect.xMin, 1f,
                $"{LogPrefix} 취소선의 왼쪽 시작이 글자와 어긋났습니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 취소선 {strike.x:F1}×{strike.y:F0}pt, 글자 원본 유지, U+0336 0건.");
        }

        /// <summary>긴 항목을 완료하면 <b>말줄임된 잉크</b>만큼만 긋는다 — 두 처방이 서로를 안다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator StrikeOnAnEllipsizedItemStopsAtTheBoxToo()
        {
            yield return LoadSceneAndOpen();

            _popover.AddForTests(MaxLengthItem());
            yield return null;

            _popover.FeedClickForTests(_popover.RowScreenRect(0).center);
            yield return null;

            Assert.IsTrue(_popover.RowStrikeActive(0), $"{LogPrefix} 취소선이 켜지지 않았습니다.");

            float strikeWidth = _popover.RowStrikeSizePoints(0).x;
            Assert.LessOrEqual(strikeWidth, TodoBoardPopover.RowLabelWidth + 0.5f,
                $"{LogPrefix} 60자 항목의 취소선이 {strikeWidth:F1}pt로 라벨 상자 " +
                $"{TodoBoardPopover.RowLabelWidth:F1}pt를 넘었습니다 — 글자는 잘렸는데 선만 흘러나갑니다.");
            Assert.Greater(strikeWidth, TodoBoardPopover.RowLabelWidth * 0.5f,
                $"{LogPrefix} 취소선이 {strikeWidth:F1}pt뿐입니다 — 폭 측정이 말줄임 이전/이후 중 " +
                "엉뚱한 시점을 보고 있습니다.");
        }
    }
}
