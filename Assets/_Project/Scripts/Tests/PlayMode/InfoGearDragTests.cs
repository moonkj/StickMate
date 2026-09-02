using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

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
    ///  ② <b>길게 누르고 그 상태로 끌면</b>(임계 <see cref="InfoGearIconWidget.DragLongPressSeconds"/>초
    ///     <b>그리고</b> <see cref="InfoGearIconWidget.DragMoveThreshold"/>pt) 드래그로 바뀌고,
    ///     그 뒤 떼도 <b>캐릭터 창이 열리지 않는다</b> — 이 요구에서 가장 흔한 실패가 "옮기려고 눌렀는데
    ///     창부터 뜬다"이므로 창이 안 열린다는 사실 자체를 단언한다.
    ///  ③ ★ <b>둘 중 하나만으로는 드래그가 아니다</b>(2026-09-02, docs/UX_FLOW.md 41-8 3겹).
    ///     옛 판정은 <b>OR</b>였고 실제 로그에 <c>[톱니] 길게 누름 감지(0.02초 / 16.5pt 이동)</c>가 찍혔다 —
    ///     <b>스치듯 지나간 클릭 하나가 톱니를 영구히 옮겼다</b>(뗀 즉시 세이브 기록).
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

        /// <summary>
        /// ★★ 2026-09-02 — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다. 걷어냈다. 되살리지 마라.
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 그 정당화는 이 클래스가 적어 둔
        /// <i>"저장 파일은 실행 중인 실제 앱의 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에
        /// <c>GlobalPlayModeTestIsolation</c>이 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.
        /// 주석은 갱신되지 않았고 코드는 <b>목적 없이</b> 살아남았다.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처나 앞선 실행이 남긴 오염이 있다"</b>는 뜻이다.
        /// 옛 TearDown은 그 오염을 <b>다시 써서 되살렸다</b> — 뒤따르는 어떤 정리도 무효화하는 형태였고,
        /// 픽스처마다 같은 코드가 있어 오염이 스위트 전체를 타고 <b>세탁</b>됐다.</para>
        ///
        /// <para>실행 사이의 이월은 별도 원인이었다 — 리디렉션 폴더를 아무도 비우지 않았다. 그쪽은
        /// <c>GlobalPlayModeTestIsolation.PurgeIsolatedDirectories</c>가 막는다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓰게 된다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다 —
        /// 백업/복원이 하던 안전 역할은 이 한 줄이 <b>더 정직하게</b> 대신한다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>격리 폴더를 다음 픽스처에 <b>넘기지 않는다</b> — 이 픽스처가 만든 저장 파일을 지운다.
        /// 옛 <c>RestoreRealSaveFile</c>이 하던 "다시 쓰기"의 정확한 반대다(위 문단 참고).</summary>
        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
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

        /// <summary>
        /// ★ <b>실제 사용자가 톱니를 옮기는 그 제스처</b> — 누르고, <b>기다리고</b>, 끌고, 뗀다.
        ///
        /// <para>2026-09-02 이전에는 "누르고 곧바로 멀리 끌기"만으로 드래그가 됐다(OR 판정).
        /// 지금은 <b>시간과 거리를 둘 다</b> 채워야 하므로 대기가 제스처의 일부다 —
        /// 벽시계로 기다린다(프레임 수 기반 대기 금지, CLAUDE.md).</para>
        /// </summary>
        private IEnumerator DragGearTo(Vector2 target)
        {
            Vector2 start = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, start);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);

            // 시간만 채운 표본 — 아직 드래그가 아니어야 한다(내장 네거티브 컨트롤).
            _gear.FeedPointerForTests(true, start);
            Assert.IsFalse(_gear.IsDraggingIcon,
                "움직이지 않고 시간만 채웠는데 드래그가 됐습니다 — AND 판정이 아니라 OR로 되돌아갔습니다.");

            _gear.FeedPointerForTests(true, target);   // 이제 거리까지 채운다.
            Assert.IsTrue(_gear.IsDraggingIcon, "시간과 거리를 둘 다 채웠는데 드래그로 전환되지 않았습니다.");

            _gear.FeedPointerForTests(false, target);
            yield return null;
        }

        // ==================== ① 짧게 클릭 (네거티브 컨트롤) ====================

        /// <summary>2026-08-30 부채꼴 메뉴 라운드로 <b>짧은 클릭의 결과물</b>이 바뀌었다(창 열기 ->
        /// 부채꼴 펼치기). 이 테스트가 잠그는 것은 그대로다: 짧은 클릭은 드래그가 아니고, 회전이 돌고,
        /// <b>아이콘이 움직이지 않는다</b>. 창이 열리는 경로는 [캐릭터] 버튼으로 옮겨가
        /// InfoGearRadialMenuTests가 잠근다.</summary>
        [UnityTest]
        public IEnumerator ShortClickStillSpinsAndDoesNotMoveIcon()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            Assert.IsFalse(_gear.HasCustomPosition, "테스트 시작 시점에 이미 옮겨진 상태입니다.");

            _gear.FeedPointerForTests(true, start);
            _gear.FeedPointerForTests(false, start);   // 시간/거리 임계를 둘 다 못 넘긴 순수 클릭.

            Assert.IsFalse(_gear.IsDraggingIcon, "짧은 클릭이 드래그로 처리됐습니다.");
            Assert.IsTrue(_gear.IsSpinning, "짧은 클릭인데 회전이 시작되지 않았습니다 — 기존 동작이 깨졌습니다.");
            Assert.IsFalse(_gear.HasCustomPosition, "짧은 클릭인데 위치가 옮겨졌습니다.");

            yield return new WaitForSecondsRealtime(0.9f);   // 회전 0.52초 + 펼침 0.30초(동시) + 여유.
            Assert.IsTrue(_gear.IsMenuExpanded, "회전이 끝났는데 부채꼴 메뉴가 펼쳐지지 않았습니다.");
            Assert.IsFalse(_window != null && _window.IsOpen,
                "짧은 클릭만으로 캐릭터 창이 열렸습니다 — 이제 창은 [캐릭터] 버튼을 눌러야 열립니다.");
            Assert.AreEqual(start.x, _gear.IconScreenCenter.x, 1f, "짧은 클릭 후 아이콘이 가로로 움직였습니다.");
            Assert.AreEqual(start.y, _gear.IconScreenCenter.y, 1f, "짧은 클릭 후 아이콘이 세로로 움직였습니다.");

            Debug.Log($"[톱니드래그테스트] 짧은 클릭 유지 확인 — 중심 {start} 그대로, 부채꼴 펼침.");
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

            // ★ 임계 시간을 넘겨도 <b>움직이지 않았으면 아직 드래그가 아니다</b>(41-8 3겹 AND).
            //   예전에는 여기서 드래그로 전환됐고, 그 자리가 "사용자가 고른 위치"로 저장됐다 —
            //   화면상 아무 일도 일어나지 않으므로 눈으로는 절대 안 보이는 사고였다.
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds * 0.7f + 0.05f);
            _gear.FeedPointerForTests(true, start);
            Assert.IsFalse(_gear.IsDraggingIcon,
                $"{InfoGearIconWidget.DragLongPressSeconds:F2}초를 넘겼지만 <b>커서가 한 픽셀도 안 움직였는데</b> " +
                "드래그로 전환됐습니다 — 시간만으로 드래그가 되면 '누르고 있다 뗐을 뿐인데 위치가 저장되는' " +
                "옛 사고가 그대로 돌아옵니다.");
            Assert.IsFalse(_gear.HasCustomPosition,
                "움직이지 않았는데 '사용자가 옮긴 위치'가 섰습니다 — 뗄 때 그대로 세이브에 기록됩니다.");

            // 시간을 채운 상태에서 끌면 그때 드래그가 된다. 아이콘과 히트 사각형이 함께 따라온다.
            Vector2 target = SafeInsideTarget();
            _gear.FeedPointerForTests(true, target);
            Assert.IsTrue(_gear.IsDraggingIcon,
                "시간과 거리를 둘 다 채웠는데 드래그로 전환되지 않았습니다.");
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
            Assert.IsFalse(_gear.IsMenuExpanded,
                "드래그였는데 부채꼴 메뉴가 펼쳐졌습니다 — 옮기려고 눌렀는데 메뉴가 뜨는 실패입니다.");
            Assert.AreEqual(target.x, _gear.IconScreenCenter.x, 2f, "떼고 나서 아이콘이 제자리에 고정되지 않았습니다.");

            Debug.Log($"[톱니드래그테스트] 길게 누름 -> 드래그 -> 고정 확인 — {start} -> {target}, 창 열림 없음.");
        }

        // ==================== ③ 거리만으로는 드래그가 아니다 (41-8 3겹) ====================

        /// <summary>
        /// ★★ <b>이 라운드의 본 검증</b> — 옛 <b>OR</b> 판정이 만든 사고를 실제 입력 경로로 재현한다.
        ///
        /// <para>옛 로그 원문: <c>[톱니] 길게 누름 감지(0.02초 / 16.5pt 이동)</c>.
        /// <b>0.02초는 길게 누른 것이 아니다</b> — "길게 누름"이라는 이름 자체가 계약인데 깨져 있었고,
        /// 결과는 <b>뗀 즉시 세이브에 기록</b>됐다. 되돌리는 문(설정창 [처음 자리로])이 필요해진
        /// <b>발생원</b>이 그 한 줄이다.</para>
        ///
        /// <para><b>양성 대조는 아래 순수 함수 테스트</b>(<see cref="양성대조_옛_OR_판정은_같은_표본을_드래그로_인정한다"/>)가
        /// 맡는다. 여기서 "안 끌렸다"만 보면 <b>입력이 애초에 톱니에 닿지 않은 것</b>과 구분되지 않으므로,
        /// 같은 제스처를 시간만 채워 이어서 태워 <b>드래그가 실제로 가능한 상태였음</b>을 증명한다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator FlickWithoutHoldingIsAClickNotADrag()
        {
            yield return LoadSceneAndResolve();

            Vector2 start = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, start);

            // 시간은 거의 흐르지 않았는데 임계 거리를 한참 넘겼다 = 사고 로그의 그 제스처.
            // 40px는 어떤 DPI 배율에서도 임계 6pt를 확실히 넘는다(Retina 2x에서도 20pt).
            Vector2 moved = start + new Vector2(-40f, -40f);
            _gear.FeedPointerForTests(true, moved);

            Assert.IsFalse(_gear.IsDraggingIcon,
                $"누른 지 {InfoGearIconWidget.DragLongPressSeconds:F2}초도 안 됐는데 끌었다는 이유만으로 " +
                "드래그가 됐습니다 — 스치듯 지나간 클릭이 톱니를 <b>영구히</b> 옮기는 옛 결함입니다.");
            Assert.IsFalse(_gear.HasCustomPosition,
                "드래그가 아닌데 '사용자가 옮긴 위치'가 섰습니다 — 뗄 때 그대로 세이브로 내려갑니다.");
            Assert.AreEqual(start.x, _gear.IconScreenCenter.x, 1f, "드래그가 아닌데 아이콘이 가로로 움직였습니다.");
            Assert.AreEqual(start.y, _gear.IconScreenCenter.y, 1f, "드래그가 아닌데 아이콘이 세로로 움직였습니다.");

            // ★ 프로브가 살아 있는가 — 같은 누름을 유지한 채 시간만 채우면 그때는 드래그가 되어야 한다.
            //   이게 없으면 위의 "안 됐다"는 '입력이 안 닿았다'와 똑같이 생겼다.
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);
            _gear.FeedPointerForTests(true, moved + new Vector2(-40f, 0f));
            Assert.IsTrue(_gear.IsDraggingIcon,
                "★ 프로브 사망 — 시간과 거리를 둘 다 채웠는데도 드래그가 안 됩니다. " +
                "위의 '스치듯 누르면 안 끌린다'는 아무것도 증명하지 않습니다(입력이 톱니에 닿지 않은 것과 같습니다).");

            _gear.FeedPointerForTests(false, moved + new Vector2(-40f, 0f));
            Assert.IsFalse(_gear.IsSpinning, "끌었다 뗐는데 클릭으로 처리됐습니다.");
            yield return null;
        }

        /// <summary>
        /// ★ <b>양성 대조 — 옛 판정식은 같은 표본을 드래그로 인정한다.</b>
        ///
        /// <para>실제 사고 로그의 표본 <b>(0.02초, 16.5pt)</b> 하나를 두 식에 그대로 먹인다.
        /// 옛 식은 <b>여기 테스트 안에 독립적으로</b> 적는다 — 프로덕션에서 가져오면 "생성기와 검사기가
        /// 같이 틀리는" 형태가 되고, 애초에 그 식은 이제 프로덕션에 없다. 옛 임계(0.4초 / 4pt)는
        /// <b>사고 당시의 실측 기록</b>이지 현행 상수가 아니므로 숫자로 적는 것이 맞다.</para>
        ///
        /// <para>이 대조가 없으면 위 <see cref="FlickWithoutHoldingIsAClickNotADrag"/>의 초록은
        /// "표본이 애초에 무해했다"와 구분되지 않는다.</para>
        /// </summary>
        [Test]
        public void 양성대조_옛_OR_판정은_같은_표본을_드래그로_인정한다()
        {
            const float incidentHeldSeconds = 0.02f;   // 로그 원문: "0.02초"
            const float incidentMovedPoints = 16.5f;   // 로그 원문: "16.5pt 이동"

            // 옛 식(재현): held >= 0.4 || moved >= 4 — 프로덕션을 참조하지 않는다.
            bool oldRule = incidentHeldSeconds >= 0.4f || incidentMovedPoints >= 4f;
            Assert.IsTrue(oldRule,
                "옛 OR 판정 재현이 이 표본을 드래그로 인정하지 않았습니다 — 재현이 틀렸다면 " +
                "아래 '지금은 아니다'가 무엇과 비교되는지 알 수 없습니다.");

            Assert.IsFalse(InfoGearIconWidget.ShouldBeginDrag(incidentHeldSeconds, incidentMovedPoints),
                $"지금 판정도 (0.02초 / 16.5pt)를 드래그로 인정합니다 — OR가 남아 있습니다. " +
                $"임계는 {InfoGearIconWidget.DragLongPressSeconds:F2}초 <b>그리고</b> " +
                $"{InfoGearIconWidget.DragMoveThreshold:F0}pt여야 합니다.");

            // 경계 네 칸 — AND의 진리표를 그대로 잠근다(한 칸만 맞고 나머지가 틀리는 구현을 잡는다).
            float t = InfoGearIconWidget.DragLongPressSeconds;
            float d = InfoGearIconWidget.DragMoveThreshold;
            Assert.IsFalse(InfoGearIconWidget.ShouldBeginDrag(t - 0.01f, d - 0.1f), "둘 다 미달인데 드래그입니다.");
            Assert.IsFalse(InfoGearIconWidget.ShouldBeginDrag(t + 1f, d - 0.1f), "시간만 채웠는데 드래그입니다.");
            Assert.IsFalse(InfoGearIconWidget.ShouldBeginDrag(t - 0.01f, d + 100f), "거리만 채웠는데 드래그입니다.");
            Assert.IsTrue(InfoGearIconWidget.ShouldBeginDrag(t, d), "둘 다 임계에 정확히 닿았는데 드래그가 아닙니다.");

            // 거리 임계는 4 -> 6으로 올랐다(20Hz 관측에서 6pt = 120pt/s — 멈추려는 손의 표류는 통과 못 한다).
            Assert.GreaterOrEqual(InfoGearIconWidget.DragMoveThreshold, 6f,
                "거리 임계가 6pt 미만으로 되돌아갔습니다(41-8 3겹).");
        }

        // ==================== ⑤ 저장 -> 씬 재로드 후에도 그 자리 ====================

        [UnityTest]
        public IEnumerator DroppedPositionSurvivesSceneReload()
        {
            yield return LoadSceneAndResolve();

            Vector2 target = SafeInsideTarget();
            yield return DragGearTo(target);

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

            // 화면 오른쪽/위로 한참 밖까지 끌어본다(시간 임계를 먼저 채운다 — 41-8 3겹 AND).
            _gear.FeedPointerForTests(true, start);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);
            _gear.FeedPointerForTests(true, new Vector2(Screen.width + 600f, Screen.height + 600f));
            Assert.IsTrue(_gear.IsDraggingIcon, "준비 조건 실패 — 드래그로 전환되지 않아 클램프를 태울 수 없습니다.");
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

        /// <summary>
        /// ★ 2026-09-02 — <b>저장된 위치가 OS 상단 예약 띠(macOS 메뉴바 / Windows 상단 도킹 작업표시줄)
        /// 안에 들어 있을 때 복원되면서 밖으로 끌려 나오는가.</b>
        ///
        /// <para>왜 이 한 건이 따로 필요한가: 톱니는 팝오버와 달리 <b>드래그한 자리가 저장된다.</b>
        /// 팝오버가 남의 띠를 덮는 것은 열려 있는 동안뿐이지만, 톱니는 재부팅해도 그 자리에 남아
        /// <b>24시간 상주 입력 강탈</b>이 된다. 그래서 "클램프가 걸리는가"만으로는 부족하고
        /// <b>"저장된 나쁜 위치가 복원 경로에서도 걸리는가"</b>를 따로 물어야 한다 —
        /// 이미 저장된 나쁜 위치가 그대로 복원되면 클램프는 무의미하다.</para>
        ///
        /// <para>판정을 <b>상대 비교</b>로 한 이유: 인셋 0으로 같은 저장값을 복원한 결과와 비교하면
        /// pt ↔ 픽셀 환산(Retina 배율)을 테스트가 다시 계산할 필요가 없다. 그리고 인셋 0 쪽이
        /// <b>내장 네거티브 컨트롤</b>이 된다 — 두 결과가 같으면 인셋이 배선되지 않은 것이다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SavedPositionInsideTheReservedTopBarIsPulledOutOnStartup()
        {
            // 창 좌상단 원점에서 y가 아주 작다 = 히트 사각형이 예약 띠 안에 통째로 들어가 있다.
            Vector2 savedInsideTopBar = new Vector2(Screen.width * 0.5f, 1f);

            ReservedTopBarProbe.ResetForTests();
            try
            {
                // ① 인셋 0(= 예약 띠를 못 물은 환경) — 이 라운드 이전과 같은 배치. 기준선이자 컨트롤.
                ReservedTopBarProbe.SetInsetPointsForTests(0f);
                UiLayoutModel.ResetForTesting();
                UiLayoutModel.SetGearCenter(savedInsideTopBar);
                Assert.IsTrue(CharacterSaveStore.Save(), "준비 단계 저장에 실패했습니다(인셋 0).");
                yield return LoadSceneAndResolve();
                yield return null;
                AssertRectFullyOnScreen("인셋 0으로 복원했을 때");
                float noInsetTop = _gear.IconScreenRect.yMax;

                // ② 예약 띠가 있는 환경 — 같은 저장값인데 톱니가 띠 아래로 밀려나야 한다.
                ReservedTopBarProbe.SetInsetPointsForTests(TestTopInsetPoints);
                UiLayoutModel.ResetForTesting();
                UiLayoutModel.SetGearCenter(savedInsideTopBar);
                Assert.IsTrue(CharacterSaveStore.Save(), "준비 단계 저장에 실패했습니다(인셋 주입).");
                yield return LoadSceneAndResolve();
                yield return null;
                AssertRectFullyOnScreen("예약 띠가 있는 채로 복원했을 때");
                float insetTop = _gear.IconScreenRect.yMax;

                Debug.Log($"[톱니드래그테스트] 예약 띠 복원 클램프 — 윗변 {noInsetTop:F1}px(인셋 0) " +
                          $"→ {insetTop:F1}px(인셋 {TestTopInsetPoints}pt), 밀려난 양 {noInsetTop - insetTop:F1}px.");

                Assert.Less(insetTop, noInsetTop - 1f,
                    "저장된 위치가 OS 상단 예약 띠 안이었는데 복원 뒤에도 그 자리에 남았습니다 " +
                    $"(윗변 {insetTop:F1}px vs 인셋 0일 때 {noInsetTop:F1}px). 톱니의 자리는 저장되므로 " +
                    "이 실패는 <재부팅해도 계속되는 메뉴바 입력 강탈>이 됩니다 — 팝오버와 달리 " +
                    "열려 있는 동안만의 문제가 아닙니다.");
            }
            finally
            {
                ReservedTopBarProbe.ResetForTests();
            }
        }

        /// <summary>테스트가 주입하는 상단 예약 띠 두께(OS 포인트). 프로덕션 상수가 아니라 <b>입력</b>이다 —
        /// 실제 메뉴바 두께가 몇이든 클램프가 그 값을 따라가는지만 본다.</summary>
        private const float TestTopInsetPoints = 33f;

        // ================================================================================
        // ⑦ 기본 세로 위치는 <b>상수가 아니라 OS 보고값</b>에서 나온다 (41-1 ③ / 41-8 1겹, X2)
        // ================================================================================

        /// <summary>
        /// ★ 옛 <c>MarginTopPoints = 58f</c> 하드코딩 폐기의 회귀 잠금.
        ///
        /// <para><b>판정을 차이로 한다</b>: 예약 띠 두께만 바꾼 두 실행의 기본 y가 <b>정확히 그 차이만큼</b>
        /// 벌어지는지 본다. 절대값을 단언하면 여백·반지름 상수를 테스트가 다시 계산해야 하고, 그러면
        /// 프로덕션과 <b>같은 산수를 두 벌</b> 갖게 되어 둘이 함께 틀릴 수 있다(이 저장소 거짓 통과 10번째 형태).
        /// 차이는 어느 상수에도 의존하지 않는다 — <b>하드코딩이면 차이가 0</b>이고, 그것만이 이 검사의 관심사다.</para>
        ///
        /// <para>그리고 <b>옛 상수 58이 어느 쪽에서도 재현되지 않는다</b>는 것을 함께 단언한다.
        /// 58은 "메뉴바가 최대 약 38pt겠지"라는 짐작이었고, 짐작이 아니라 사실에서 나오는지가 요점이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 기본_세로_위치는_상단_예약_띠_보고값을_따라간다()
        {
            const float OldHardcodedMarginTop = 58f;   // 폐기된 상수 — 역사 기록이지 현행 값이 아니다.

            ReservedTopBarProbe.ResetForTests();
            try
            {
                // ① 예약 띠가 없는 환경(자동 숨김 / 하단 도킹 작업표시줄).
                ReservedTopBarProbe.SetInsetPointsForTests(0f);
                yield return LoadSceneAndResolve();
                yield return null;
                Assert.IsFalse(_gear.HasCustomPosition, "기본 위치를 재야 하는데 이미 옮겨진 상태입니다.");
                float yNoInset = _gear.IconCenterPoints.y;

                // ② 같은 화면, 예약 띠만 있는 환경.
                ReservedTopBarProbe.SetInsetPointsForTests(TestTopInsetPoints);
                yield return LoadSceneAndResolve();
                yield return null;
                float yWithInset = _gear.IconCenterPoints.y;

                Debug.Log($"[톱니드래그테스트] 기본 세로 위치 — 인셋 0에서 {yNoInset:F2}pt, " +
                          $"인셋 {TestTopInsetPoints}pt에서 {yWithInset:F2}pt (차이 {yWithInset - yNoInset:F2}pt).");

                Assert.AreEqual(TestTopInsetPoints, yWithInset - yNoInset, 0.5f,
                    $"기본 세로 위치가 예약 띠를 따라가지 않았습니다({yNoInset:F2} → {yWithInset:F2}). " +
                    "차이가 0이면 아직 상수가 박혀 있는 것입니다 — 그 상수는 Windows 상단 도킹 작업표시줄 " +
                    "앞에서 근거가 없습니다(41-13).");

                Assert.Less(yNoInset, OldHardcodedMarginTop,
                    $"예약 띠가 없는데도 옛 상수({OldHardcodedMarginTop})만큼 내려가 있습니다 — 낭비입니다.");
                Assert.Greater(yWithInset, OldHardcodedMarginTop,
                    $"예약 띠 {TestTopInsetPoints}pt 환경에서 기본 위치가 옛 상수({OldHardcodedMarginTop}) 위쪽입니다 — " +
                    "여백이 사라졌습니다.");

                // 그리고 실제로 그 띠를 안 문다(픽셀에서 직접 잰다 — 테스트가 pt↔px 환산을 다시 하지 않게).
                float insetPx = ScreenCoordinateConverter.CanvasToUnityScreen(TestTopInsetPoints, null);
                Assert.LessOrEqual(_gear.IconScreenRect.yMax, Screen.height - insetPx + 0.5f,
                    $"기본 위치의 히트 사각형이 예약 띠를 덮습니다({_gear.IconScreenRect}).");
            }
            finally
            {
                ReservedTopBarProbe.ResetForTests();
            }
        }

        // ================================================================================
        // ⑧ 온보딩 대여 — 「집 좌표」 창구와 소유권 이양 (docs/UX_FLOW.md 51-4 / 51-6-1)
        // ================================================================================

        /// <summary>
        /// ★ <b><see cref="InfoGearIconWidget.HomeCenterPoints"/>는 대여에 흔들리지 않는다.</b>
        ///
        /// <para>온보딩은 톱니를 캐릭터 위에서 <b>제자리로 날려 보내며</b> "여기가 원래 자리"를 가르친다.
        /// 그 비행의 목적지를 <see cref="InfoGearIconWidget.IconCenterPoints"/>로 읽으면
        /// <b>목적지 = 현재 위치</b>가 되어 궤적 길이가 0이다 — 톱니가 제자리 비행을 하고 아무도
        /// 아무것도 못 배운다. 그래서 대여와 무관한 읽기 전용 창구가 따로 필요하다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 집_좌표는_온보딩_대여_중에도_원래_자리를_가리킨다()
        {
            yield return LoadSceneAndResolve();

            Vector2 home = _gear.HomeCenterPoints;
            Assert.AreEqual(home.x, _gear.IconCenterPoints.x, 0.5f, "대여 전인데 집 좌표와 현재 좌표가 다릅니다.");
            Assert.AreEqual(home.y, _gear.IconCenterPoints.y, 0.5f, "대여 전인데 집 좌표와 현재 좌표가 다릅니다.");

            // 온보딩이 톱니를 캐릭터 쪽(화면 아래 왼쪽)으로 데려간다.
            Vector2 borrowed = new Vector2(home.x * 0.3f, home.y + 200f);
            _gear.BeginOnboardingPlacement(borrowed, "테스트: 온보딩 대여");
            yield return null;
            yield return null;

            // 양성 대조 — 대여가 실제로 톱니를 옮겼는가(안 옮겼으면 아래 판정은 아무것도 못 잰다).
            Assert.Greater((_gear.IconCenterPoints - home).magnitude, 20f,
                "대여가 톱니를 옮기지 못했습니다 — '집 좌표는 안 흔들린다'가 무의미해집니다.");

            Assert.AreEqual(home.x, _gear.HomeCenterPoints.x, 0.5f,
                "★ 대여 중에 집 좌표가 대여 좌표를 따라갔습니다 — 귀환 비행의 목적지가 현재 위치가 되어 " +
                "궤적 길이가 0이 됩니다(제자리 비행).");
            Assert.AreEqual(home.y, _gear.HomeCenterPoints.y, 0.5f, "★ 대여 중에 집 좌표의 세로가 흔들렸습니다.");
            Assert.IsFalse(UiLayoutModel.HasGearCenter, "집 좌표를 읽는 것만으로 저장 계약이 섰습니다 — 읽기 전용이어야 합니다.");

            _gear.EndOnboardingPlacement("테스트: 반납");
            yield return null;
            Assert.AreEqual(home.y, _gear.IconCenterPoints.y, 1f, "반납 뒤 집으로 돌아오지 않았습니다.");

            Debug.Log($"[톱니드래그테스트] 집 좌표 확인 — 대여({borrowed}) 중에도 집은 ({home.x:F1}, {home.y:F1})pt.");
        }

        /// <summary>
        /// ★★ <b>대여 중 드래그는 막는 것이 아니라 넘긴다</b> (docs/UX_FLOW.md 51-6-1).
        ///
        /// <para>온보딩 ⑦단계는 <i>"길게 누르면 옮길 수 있어요"</i>를 <b>가르치는</b> 단계다.
        /// 그런데 옛 구현은 대여 중 드래그 전환을 <b>막았다</b> — 배운 대로 해 본 사용자에게
        /// <b>그 순간에만</b> 안 먹었다(행동-텍스트 싱크의 정확한 반례).</para>
        ///
        /// <para>★ 그래도 P0 계약은 그대로다: 저장되는 좌표는 <b>사용자가 끌어다 놓은 자리</b>이지
        /// 대여 좌표가 아니다. 이 테스트는 그 둘을 <b>멀리 떨어뜨려</b> 놓고 어느 쪽이 저장됐는지 잰다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 대여_중_진짜_드래그는_소유권을_넘겨받고_대여_좌표는_저장되지_않는다()
        {
            yield return LoadSceneAndResolve();
            Assert.IsFalse(UiLayoutModel.HasGearCenter, "신규 사용자 상태로 시작하지 못했습니다.");

            // 온보딩이 톱니를 화면 왼쪽 아래로 데려간다.
            Vector2 home = _gear.HomeCenterPoints;
            Vector2 borrowed = new Vector2(home.x * 0.25f, home.y + 240f);
            _gear.BeginOnboardingPlacement(borrowed, "테스트: 온보딩 대여");
            yield return null;
            yield return null;
            Assert.IsTrue(_gear.IsOnboardingPositionOwned, "대여가 서지 않았습니다.");

            // 사용자가 <b>보이는 그 톱니</b>를 눌러 배운 대로 끌어 옮긴다.
            Vector2 press = _gear.IconScreenCenter;
            Vector2 drop = SafeInsideTarget();
            _gear.FeedPointerForTests(true, press);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);
            _gear.FeedPointerForTests(true, drop);

            Assert.IsTrue(_gear.IsDraggingIcon,
                "★ 대여 중이라는 이유로 드래그가 막혔습니다 — ⑦이 가르친 그대로 했는데 그 순간에만 안 먹습니다.");
            Assert.IsFalse(_gear.IsOnboardingPositionOwned,
                "드래그가 시작됐는데 대여가 그대로입니다 — 소유권이 넘어오지 않았습니다.");

            _gear.FeedPointerForTests(false, drop);
            yield return null;

            Assert.IsTrue(_gear.HasCustomPosition, "끌어다 놓았는데 '옮긴 적 없음' 상태입니다.");
            Assert.IsTrue(UiLayoutModel.HasGearCenter, "끌어다 놓았는데 저장 계약이 서지 않았습니다.");

            Vector2 saved = UiLayoutModel.GearCenterPoints;
            Debug.Log($"[톱니드래그테스트] 대여 이양 — 대여({borrowed.x:F0}, {borrowed.y:F0}) / " +
                      $"저장({saved.x:F0}, {saved.y:F0}) / 지금({_gear.IconCenterPoints.x:F0}, {_gear.IconCenterPoints.y:F0}).");

            Assert.AreEqual(_gear.IconCenterPoints.x, saved.x, 1f, "저장된 가로가 지금 자리와 다릅니다.");
            Assert.AreEqual(_gear.IconCenterPoints.y, saved.y, 1f, "저장된 세로가 지금 자리와 다릅니다.");
            Assert.Greater((saved - borrowed).magnitude, 20f,
                "★ 저장된 좌표가 <b>대여 좌표</b>입니다 — 온보딩이 만든 자리가 '사용자가 고른 값'으로 " +
                "세이브에 앉았습니다. 저장에는 그것이 온보딩 자리였다는 표시가 남지 않아 사후 복구가 불가능합니다.");
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
