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
    /// ★★ <b>포스트잇이 톱니를 덮는다</b> — 2026-09-02, docs/UX_FLOW.md 51-9.
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (실측, 화면 우상단 기준 pt)
    /// ============================================================================
    /// <code>
    ///   톱니 히트 : x 10.18 ~ 49.82 / y 38.18 ~ 77.82
    ///   포스트잇  : x 16.00 ~ 236.00 / y 16.00 ~ (16 + 높이)
    ///   x 겹침 = 16.00 ~ 49.82 = 33.82pt = 톱니 히트 폭(39.64)의 85.3%
    /// </code>
    /// 세로도 겹친다 — 겹치려면 카드 높이가 22.18pt를 넘어야 하는데 <b>1행 최소 구성</b>
    /// (행 28 + 패딩 12)에서 이미 넘는다.
    ///
    /// <b>그리고 z를 뒤집는 선택지는 <u>존재하지 않는다</u></b>: 카드·부채꼴·캐릭터창은
    /// <c>ScreenSpaceOverlay</c> 캔버스이고 <b>톱니만 월드 <c>LineRenderer</c></b>라
    /// 둘의 <c>sortingOrder</c>는 애초에 비교되지 않는다. 톱니의 40을 30001로 올려도 아무 일도
    /// 일어나지 않는다 — <b>비켜 세우는 것 말고 방법이 없다.</b>
    ///
    /// ============================================================================
    /// ★ 함께 사라지는 것 — 이중 발동 구역 <b>47.0 pt²</b> (51-9-4, 지금 출하 중이던 결함)
    /// ============================================================================
    /// <c>x 24.00~49.82 / y 38.18~40.00</c>에서 카드의 <b>[숨기기] 칩</b>(uGUI <c>Button</c>)과
    /// <b>톱니 히트 사각형</b>(전역 폴링 + <c>BoxCollider2D</c>)이 겹쳐, 그 좁은 띠를 클릭하면
    /// <b>카드가 숨겨지는 동시에 부채꼴이 펼쳐졌다</b>. 첫 실행에는 할 일이 0건이라 카드가 안 떠서
    /// 아무도 못 봤고, <b>할 일을 하나 넣는 순간</b> 생겼다.
    ///
    /// ============================================================================
    /// 이 파일의 규율 — 모든 "안 겹친다"에 <b>양성 대조</b>가 붙는다
    /// ============================================================================
    /// "겹치지 않는다"는 <b>두 세계에서 똑같이 생겼다</b>: ① 처방이 실제로 밀어냈다(원하는 것)
    /// ② 애초에 카드가 안 떴거나 톱니를 못 찾아 아무 일도 없었다. 그래서 각 검사는 <b>처방 전
    /// 자리로 되돌린 사각형</b>(라이브 값에서 평행이동으로 복원한다)이 <b>실제로 겹치는 것</b>을
    /// 먼저 보이고, 그 다음에 지금 자리가 안 겹치는 것을 잰다.
    /// </summary>
    public sealed class TodoPostItGearAvoidanceTests
    {
        private const string LogPrefix = "[포스트잇톱니회피-TEST]";

        /// <summary>소프트캡 경고를 보지 않으므로 넉넉히 잡는다(TodoPostItExpansionTests와 같은 관례).</summary>
        private const int SoftCap = 99;

        /// <summary>처방 전 카드의 가로 인셋(pt). <b>역사 기록</b>이다 — 이 값으로 되돌린 사각형이
        /// 실제로 톱니와 겹치는지가 이 파일의 양성 대조다.</summary>
        private const float BaseInsetPoints = 16f;

        private TodoPostItWidget _widget;
        private InfoGearIconWidget _gear;

        /// <summary>
        /// ★★ 2026-09-02 <c>test-engineer</c> — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다.
        /// 걷어냈다. 되살리지 마라. (<c>FullscreenPanelRetreatTests</c>가 같은 날 먼저 걷어낸 것과
        /// <b>같은 코드</b>가 8개 픽스처에 남아 있었다.)
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 정당화는 <i>"저장 파일이 실제 앱의
        /// 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에 <c>GlobalPlayModeTestIsolation</c>이
        /// 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처가 남긴 오염이 있다"</b>는 뜻이다. 옛 TearDown은
        /// 그 오염을 <b>다시 써서 되살렸고</b>, 같은 코드가 여러 픽스처에 있었으므로 오염이 스위트
        /// 전체를 타고 <b>세탁</b>됐다 — 어떤 정리도 그 다음 픽스처의 복원 한 줄에 무효화됐다.
        /// 2026-09-02 실측이 그 결과다: <c>c1-play</c>가 씬 로드 430회 중 "없음 161 → 불러옴 278"로
        /// 도중에 뒤집혔고 <c>스틱메이트 Lv.127</c>이 로그에 505회 찍혔다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓴다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다.</para>
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            TodoListModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            ReservedTopBarProbe.ResetForTests();
            _widget = null;
            _gear = null;
            yield return null;
        }

        // ==================================================================
        // 준비
        // ==================================================================

        /// <summary>할 일 <paramref name="count"/>건을 넣고 카드가 보이는 상태를 만든다.</summary>
        private IEnumerator ShowCardWith(int count)
        {
            UiLayoutModel.ResetForTesting();     // 톱니를 기본 위치(우상단)에서 시작한다.
            CharacterSaveStore.Save();

            // ★ 상단 예약 띠를 <b>0으로 고정</b>한다 — 이 파일의 판정을 환경에 흔들리지 않게 하려는 것이고,
            //   그래도 정직하다: 51-9-3의 처방은 <b>가로만</b> 만지므로 띠 두께는 결론에 관여하지 않는다.
            //   다만 <b>양성 대조</b>는 관여한다 — 띠가 두꺼우면 톱니가 그만큼 내려가 [숨기기] 칩과
            //   세로로 안 만날 수 있고, 그러면 "겹쳤던 것을 떼어냈다"를 보일 수 없다. 띠가 없는 환경
            //   (메뉴바 자동 숨김 / Windows 하단 도킹)은 실제로 흔하고, 그 환경에서 세로 겹침이 <b>가장 크다</b>.
            ReservedTopBarProbe.SetInsetPointsForTests(0f);

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _widget = Object.FindFirstObjectByType<TodoPostItWidget>();
            Assert.IsNotNull(_widget, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다.");

            var gears = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, gears.Length, $"{LogPrefix} 씬의 InfoGearIconWidget 개수가 {gears.Length}개입니다.");
            _gear = gears[0];

            TodoListModel.ResetForTesting();
            for (int i = 0; i < count; i++) TodoListModel.Add($"톱니 회피 확인용 {i + 1}", SoftCap);
            yield return null;
            yield return null;

            Assert.IsTrue(_widget.IsCardVisible,
                $"{LogPrefix} 할 일이 {count}건인데 카드가 보이지 않습니다 — 관측 전제가 성립하지 않습니다.");
        }

        private static Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);   // Overlay 캔버스에서는 월드 좌표가 곧 스크린 픽셀이다.
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static RectTransform FindByName(TodoPostItWidget widget, string name)
        {
            var all = widget.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rt in all)
            {
                if (rt.name == name) return rt;
            }
            return null;
        }

        /// <summary>처방 <b>전</b>(가로 인셋 16) 자리로 되돌린 사각형 — 지금 사각형을 오른쪽으로
        /// 밀어낸 만큼 되돌린다. 라이브 값에서 평행이동으로 복원하므로 pt↔px 환산을 다시 하지 않는다.</summary>
        private Rect RestoredToBaseInset(Rect now, float pushedPixels) =>
            new Rect(now.x + pushedPixels, now.y, now.width, now.height);

        /// <summary>지금 밀려난 양(픽셀). 인셋 차이(pt)를 화면 픽셀로 환산한다.</summary>
        private float PushedPixels()
        {
            float pushedPoints = _widget.RightInsetPointsForTests - BaseInsetPoints;
            return ScreenCoordinateConverter.CanvasToUnityScreen(pushedPoints, null);
        }

        // ==================================================================
        // ① 카드 본체가 톱니를 비킨다
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 카드가_톱니_히트_사각형을_비켜_앉는다()
        {
            yield return ShowCardWith(3);

            RectTransform panel = FindByName(_widget, "PostItPanel");
            Assert.IsNotNull(panel, $"{LogPrefix} PostItPanel을 찾지 못했습니다.");

            Rect gear = _gear.IconScreenRect;
            Rect card = ScreenRectOf(panel);
            float pushed = PushedPixels();

            Debug.Log($"{LogPrefix} 인셋 {_widget.RightInsetPointsForTests:F2}pt(기본 {BaseInsetPoints}) — " +
                      $"카드 {card} / 톱니 {gear} / 밀려난 양 {pushed:F1}px.");

            // ★ 양성 대조 — 처방 전 자리에서는 <b>실제로 겹쳤다</b>. 이게 거짓이면 아래 초록은
            //   "애초에 겹칠 일이 없었다"와 구분되지 않는다.
            Assert.Greater(pushed, 0f,
                $"{LogPrefix} ★ 양성 대조 실패 — 카드가 한 픽셀도 밀리지 않았습니다(인셋 " +
                $"{_widget.RightInsetPointsForTests:F2}pt). 톱니 파생이 배선되지 않았거나 겹침 판정이 안 걸립니다.");
            Rect before = RestoredToBaseInset(card, pushed);
            Assert.IsTrue(before.Overlaps(gear),
                $"{LogPrefix} ★ 양성 대조 실패 — 처방 전 자리({before})가 톱니({gear})와 겹치지 않습니다. " +
                "겹치지 않는 것을 밀어낸 것이라면 이 검사는 아무것도 잠그지 않습니다.");

            // 본 검증.
            Assert.IsFalse(card.Overlaps(gear),
                $"{LogPrefix} 카드({card})가 여전히 톱니 히트 사각형({gear})을 덮습니다 — " +
                "z를 뒤집는 방법은 <b>존재하지 않으므로</b>(캔버스 Overlay vs 월드 LineRenderer) " +
                "비키는 것이 유일한 수단입니다.");

            // 여유가 우연이 아니라 설계값(부채꼴 화면 여백)만큼인가.
            float gapPoints = ScreenCoordinateConverter.UnityScreenToCanvas(
                gear.xMin - card.xMax, null);
            Assert.GreaterOrEqual(gapPoints, GearRadialMenuWidget.ScreenMarginPoints - 0.5f,
                $"{LogPrefix} 카드 우변과 톱니 좌변의 여유가 {gapPoints:F2}pt로 " +
                $"설계값 {GearRadialMenuWidget.ScreenMarginPoints}pt에 못 미칩니다.");
        }

        // ==================================================================
        // ② 이중 발동 구역 47.0pt² — [숨기기] 칩이 톱니와 겹치지 않는다
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 숨기기_칩과_톱니의_이중_발동_구역이_사라진다()
        {
            yield return ShowCardWith(1);   // ★ 1건이면 충분하다 — 최소 구성에서도 겹쳤던 것이 요점이다.

            RectTransform chip = FindByName(_widget, "HideButton");
            Assert.IsNotNull(chip, $"{LogPrefix} [숨기기] 칩을 찾지 못했습니다.");

            Rect gear = _gear.IconScreenRect;
            Rect chipRect = ScreenRectOf(chip);
            float pushed = PushedPixels();

            // 양성 대조 — 처방 전 칩 자리는 톱니와 겹쳤다(그 띠를 누르면 두 곳이 함께 발동했다).
            Rect chipBefore = RestoredToBaseInset(chipRect, pushed);
            Assert.IsTrue(chipBefore.Overlaps(gear),
                $"{LogPrefix} ★ 양성 대조 실패 — 처방 전 칩({chipBefore})이 톱니({gear})와 겹치지 않습니다. " +
                "51-9-4가 실측한 47.0pt² 이중 발동 구역이 재현되지 않으면 아래 초록은 무의미합니다.");

            Rect overlapBefore = Rect.MinMaxRect(
                Mathf.Max(chipBefore.xMin, gear.xMin), Mathf.Max(chipBefore.yMin, gear.yMin),
                Mathf.Min(chipBefore.xMax, gear.xMax), Mathf.Min(chipBefore.yMax, gear.yMax));
            Debug.Log($"{LogPrefix} 처방 전 이중 발동 구역 {overlapBefore.width:F1}x{overlapBefore.height:F1}px " +
                      $"— 지금 칩 {chipRect} / 톱니 {gear}.");

            // 본 검증.
            Assert.IsFalse(chipRect.Overlaps(gear),
                $"{LogPrefix} [숨기기] 칩({chipRect})과 톱니({gear})가 여전히 겹칩니다 — " +
                "그 띠를 한 번 클릭하면 <b>카드가 숨겨지는 동시에 부채꼴이 펼쳐집니다</b>.");
        }

        // ==================================================================
        // ③ 네거티브 컨트롤 — 톱니를 치우면 카드는 원래 자리로 돌아온다
        // ==================================================================

        /// <summary>
        /// ★ <b>상수 −58이 아니라 파생식인 이유</b>를 잠근다. 톱니는 드래그로 옮길 수 있고,
        /// 상수로 박으면 톱니를 화면 왼쪽으로 옮긴 사용자는 <b>아무 이유 없이 42pt를 잃는다.</b>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 톱니를_치우면_카드는_원래_인셋으로_돌아온다()
        {
            yield return ShowCardWith(3);

            Assert.Greater(_widget.RightInsetPointsForTests, BaseInsetPoints + 0.5f,
                $"{LogPrefix} 준비 조건 실패 — 기본 위치에서 카드가 밀려 있지 않습니다.");

            // 톱니를 화면 한가운데로 끌어다 놓는다(실제 드래그 경로 — 시간 AND 거리, 41-8 3겹).
            Vector2 start = _gear.IconScreenCenter;
            Vector2 target = new Vector2(Screen.width * 0.4f, Screen.height * 0.45f);
            _gear.FeedPointerForTests(true, start);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);
            _gear.FeedPointerForTests(true, target);
            Assert.IsTrue(_gear.IsDraggingIcon, $"{LogPrefix} 준비 조건 실패 — 톱니를 끌지 못했습니다.");
            _gear.FeedPointerForTests(false, target);
            yield return null;
            yield return null;

            Assert.AreEqual(BaseInsetPoints, _widget.RightInsetPointsForTests, 0.5f,
                $"{LogPrefix} 톱니를 화면 한가운데로 치웠는데 카드가 여전히 " +
                $"{_widget.RightInsetPointsForTests:F2}pt만큼 밀려 있습니다 — 파생식이 아니라 상수로 굳었습니다.");

            RectTransform panel = FindByName(_widget, "PostItPanel");
            Assert.IsFalse(ScreenRectOf(panel).Overlaps(_gear.IconScreenRect),
                $"{LogPrefix} 되돌아온 자리가 톱니와 겹칩니다 — 되돌리기 조건이 너무 헐겁습니다.");

            Debug.Log($"{LogPrefix} 톱니 이동 후 인셋 {_widget.RightInsetPointsForTests:F2}pt로 복귀.");
        }
    }
}
