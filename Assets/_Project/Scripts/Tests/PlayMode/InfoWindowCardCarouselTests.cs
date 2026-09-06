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
    /// ★ 카드 <b>세로 격자 스크롤</b> + 카드 하단 [착용] 버튼 회귀.
    ///
    /// <para>★ 2026-09-05 — 인계본 3컬럼 이식(L-1)으로 <b>가로 캐러셀이 폐기</b>되고 컬럼 3 전체가
    /// 세로 스크롤 하나가 됐다. 파일 이름은 그대로 둔다 — 이 저장소에는 <b>파일명으로 소스를 찾는
    /// 감사</b>가 있고, 이름을 바꾸면 그 감사가 눈이 먼다(실제 사고 2건). 잠그는 계약은 같다:
    /// "카테고리의 모든 아이템에 도달할 수 있는가 / 밀다가 옷이 갈아입혀지지 않는가".</para>
    ///
    /// ============================================================================
    /// 왜 PlayMode인가
    /// ============================================================================
    /// 여기서 잡으려는 것은 데이터가 아니라 <b>화면과 손</b>이다:
    ///  (a) 카테고리마다 아이템 수가 달라도 카드가 그 수만큼 <b>실제로</b> 켜지는가
    ///      (좌표는 LayoutCardGrid가 잡고, 그건 캔버스가 살아 있어야 값이 나온다),
    ///  (b) 잡고 밀면 <see cref="ScrollRect"/>의 content가 <b>진짜로</b> 움직이는가,
    ///  (c) 카드 하단 버튼을 누르면 착용이 되고 <b>같은 카테고리의 앞 아이템은 벗겨지는가</b>,
    ///  (d) <b>미는 도중에는 착용되지 않는가</b>(이 버튼을 만든 목적의 절반이 이것이다).
    ///
    /// 입력은 실제 경로(<see cref="CharacterInfoWindow.FeedPointerForTests"/>)로 넣는다 — 테스트 전용
    /// 분기를 만들면 "테스트만 통과하는 코드"가 된다(이 프로젝트의 기존 관례).
    ///
    /// ============================================================================
    /// 세이브 파일 — 스위트 격리에 맡긴다
    /// ============================================================================
    /// 착용은 즉시 저장된다(별도 저장 버튼이 없다). 개발자의 실제 파일은
    /// <c>GlobalPlayModeTestIsolation</c>이 이미 임시 폴더로 경로를 옮겨 두므로 여기서 또 백업하지
    /// 않는다 — 관례를 두 겹으로 쌓으면 어느 쪽이 진짜 보호인지 알 수 없게 된다.
    ///
    /// ============================================================================
    /// 임시 QA 해금 스위치에 기대지 않는다
    /// ============================================================================
    /// <c>EquipmentDebugUnlock</c>은 <b>임시</b>다(언젠가 false로 돌아간다). 그래서 두 번째 모자가
    /// 잠겨 있으면 <b>레벨을 실제로 올려서</b> 연다 — 스위치가 꺼지는 날 이 파일이 조용히 죽지 않는다.
    /// </summary>
    public sealed class InfoWindowCardCarouselTests
    {
        private const string LogPrefix = "[카드격자-TEST]";

        /// <summary>드래그로 밀어 볼 거리(캔버스 포인트). 임계값(4pt)보다 확실히 크다.</summary>
        private const float DragPoints = 120f;

        private CharacterInfoWindow _window;
        private StickConfig _config;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            _config = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            _config = agent != null ? agent.Config : null;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig를 찾지 못했습니다 — 레벨을 올릴 수 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;
            yield return null;   // LayoutCardGrid가 한 번 돌고 캔버스가 갱신될 기회를 준다.
        }

        // ============================================================================
        // (a) 카드 수는 <b>콘텐츠가 정한다</b>
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardsMatchCatalogCountPerCategory()
        {
            yield return OpenWindow();

            // [장비] 탭 0번 섹션 = 첫 장비 계열 카테고리(모자).
            int expected = ItemCatalog.ItemCountIn(EquipmentSlot.Head);
            Assert.Greater(expected, 4,
                $"{LogPrefix} 모자가 {expected}종뿐입니다 — 4종 이하면 격자가 밀릴 일이 없어 이 파일이 " +
                "검증할 것 자체가 사라집니다(카테고리당 6종이 지금의 불변식입니다).");

            int visible = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (_window.CardSectionForTests(i) != 0) continue;
                if (_window.IsCardVisibleForTests(i)) visible++;
            }
            Assert.AreEqual(expected, visible,
                $"{LogPrefix} 모자 카드가 {visible}장 켜져 있습니다 — 카탈로그는 {expected}종이라고 말합니다. " +
                "카드 수를 코드가 상수로 적고 있으면 늘어난 아이템이 예외 없이 사라집니다.");
        }

        // ============================================================================
        // (a-2) ★ 6종이 <b>도달 가능한가</b> + 도달 가능하다는 <b>단서가 화면에 있는가</b>
        //        (2026-09-01 페르소나 M1 — 회귀 방지)
        // ============================================================================
        //
        // 왜 위 (a)만으로 부족했는가: `CardsMatchCatalogCountPerCategory`는 카드가 <b>켜져 있는지</b>만
        // 센다. 카드는 6장 다 켜져 있었지만 뷰포트(592pt)에 정확히 4장이 <b>딱 맞게</b> 들어가고
        // 5·6번째는 통째로 화면 밖이었다 — 잘린 카드가 하나도 없으니 "여기가 끝"으로 읽혔고,
        // 콘텐츠의 1/3이 발견되지 않았다. 개수는 통과하고 발견 가능성만 0인 상태였다.

        /// <summary>
        /// ★ 쉬고 있는(스크롤 0) 격자에는 <b>세로로 잘린 카드가 반드시 있어야 한다</b>.
        /// 이 잘림이 이 창의 "더 있다" 신호다(화살표도 스크롤바도 없다).
        ///
        /// <para>재는 기준은 <b>스크롤 뷰포트</b>이지 화면이 아니다. 배치모드의 좁은 화면에서는
        /// <c>ClampPanelToScreen</c>이 창 자체를 줄여 패널 마스크가 카드를 자르는데, 그건 격자가 아니라
        /// 화면 크기가 만든 잘림이라 이 항목의 증거가 될 수 없다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RestingCarouselShowsAHalfCutCardAsTheOnlyMoreCue()
        {
            yield return OpenWindow();

            Assert.AreEqual(0f, _window.GridScrollPoints, 1e-3f,
                $"{LogPrefix} 전제: 창을 연 직후에는 스크롤이 0이어야 합니다.");

            Rect viewport = _window.GridViewportScreenRect;
            Assert.Greater(viewport.height, 1f, $"{LogPrefix} 격자 뷰포트의 사각형이 비었습니다.");

            int fullyVisible = 0, partlyVisible = 0, hidden = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;

                Rect card = _window.CardRawScreenRect(i);
                Assert.Greater(card.height, 1f, $"{LogPrefix} {i}번 카드의 사각형이 비었습니다.");

                float ratio = InsideViewportFraction(card, viewport);
                if (ratio >= 0.98f) fullyVisible++;
                else if (ratio > 0.02f) partlyVisible++;
                else hidden++;
            }

            Assert.Greater(fullyVisible, 0, $"{LogPrefix} 뷰포트 안에 온전히 들어온 카드가 하나도 없습니다.");
            Assert.Greater(hidden, 0,
                $"{LogPrefix} 뷰포트 밖으로 나간 카드가 0장입니다 — 밀 것이 없다는 뜻이라 이 테스트가 " +
                "지킬 것도 없습니다(카테고리 아이템 수가 줄었는지 확인하세요).");

            // ★ "더 있다"는 <b>단서가 화면에 있어야 한다</b>. 화살표도 스크롤바도 없는 창이라
            //   그 일을 하는 것은 뷰포트 아래 끝에 <b>걸친 것</b> 하나뿐이다.
            //
            //   ★ 2026-09-05 정정 — 가로 캐러셀 시절에는 그 단서가 <b>반쯤 잘린 카드</b>였다. 세로
            //   격자에서는 카테고리당 6종이 3행에 딱 떨어져 첫 블록이 뷰포트에 온전히 들어가고,
            //   대신 <b>다음 카테고리 제목줄</b>이 아래에 걸친다. 잠글 것은 "카드가 반쯤 잘린다"가
            //   아니라 <b>"아래 끝에 무언가가 걸쳐 있다"</b>이다 — 둘 중 하나면 통과한다.
            bool nextBlockPeeks = false;
            for (int s = 1; s < _window.VisibleSectionCount; s++)
            {
                Rect block = _window.CategoryBlockScreenRect(s);
                if (block.height <= 1f) continue;
                if (block.yMax > viewport.yMin && block.yMin < viewport.yMin) { nextBlockPeeks = true; break; }
            }

            Assert.IsTrue(partlyVisible >= 1 || nextBlockPeeks,
                $"{LogPrefix} 뷰포트 아래 끝에 걸친 것이 하나도 없습니다" +
                $"(온전 {fullyVisible} / 걸침 {partlyVisible} / 숨김 {hidden}, 다음 블록 걸침 {nextBlockPeeks}) — " +
                "카드가 뷰포트에 딱 맞아떨어져서 \"이게 전부\"로 읽힙니다. 화살표도 스크롤바도 없는 이 창에서 " +
                "걸침은 장식이 아니라 유일한 발견 단서입니다.");
        }

        /// <summary>
        /// ★ 드래그를 <b>실제로 걸 수 있는</b> 지점 — 뷰포트가 마스크에 잘리고 <b>남은</b> 사각형의 중심.
        ///
        /// <para><b>왜 날 중심이 아닌가</b>(2026-09-02 실측): 배치모드 PlayMode 화면은 640×480이라
        /// <c>ClampPanelToScreen</c>이 창을 줄이는데 <b>내용은 함께 접히지 않는다</b> — 컬럼 3은 폭 1042
        /// 기준 자리에 그대로 있고 <c>Body</c> 마스크가 자른다. 즉 <b>뷰포트의 한가운데가 잘린 쪽</b>이고,
        /// 그 자리는 이 창의 규칙("보이지 않는 것은 눌리지 않는다")대로 <b>정당하게</b> 잡히지 않는다.</para>
        /// </summary>
        private Vector2 GrabPointOnRow(int section)
        {
            Rect raw = _window.GridViewportScreenRect;
            Rect visible = _window.VisibleScreenRectOf(null);
            visible = ClipToPanel(raw);
            Debug.Log($"{LogPrefix} 화면 {Screen.width}×{Screen.height}, 창 {_window.PanelSizePoints.x:F0}×" +
                $"{_window.PanelSizePoints.y:F0}pt, 뷰포트 날 y[{raw.yMin:F0}..{raw.yMax:F0}] / " +
                $"보이는 y[{visible.yMin:F0}..{visible.yMax:F0}] — 잡는 지점 {visible.center}.");

            Assert.Greater(visible.width * visible.height, 1f,
                $"{LogPrefix} 컬럼 3 격자가 화면에 한 조각도 보이지 않습니다(날 폭 {raw.width:F0}pt) — " +
                "잡을 자리가 없으면 사용자도 밀 수 없습니다. 창이 화면보다 넓어 통째로 잘렸는지 확인하세요.");
            return visible.center;
        }

        /// <summary>화면 사각형을 <b>창 사각형</b>으로 자른다 — 배치모드의 좁은 화면에서 마스크가
        /// 실제로 자르는 범위와 같다.</summary>
        private Rect ClipToPanel(Rect raw)
        {
            Rect panel = _window.PanelScreenRect;
            float xMin = Mathf.Max(raw.xMin, panel.xMin);
            float yMin = Mathf.Max(raw.yMin, panel.yMin);
            float xMax = Mathf.Min(raw.xMax, panel.xMax);
            float yMax = Mathf.Min(raw.yMax, panel.yMax);
            if (xMax <= xMin || yMax <= yMin) return new Rect();
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>카드가 <b>스크롤 뷰포트</b> 안에 들어와 있는 세로 비율(0 = 통째로 밖).</summary>
        private static float InsideViewportFraction(Rect card, Rect viewport)
        {
            float overlap = Mathf.Min(card.yMax, viewport.yMax) - Mathf.Max(card.yMin, viewport.yMin);
            return card.height > 0f ? Mathf.Clamp01(overlap / card.height) : 0f;
        }

        /// <summary>★ 카테고리의 <b>마지막</b> 아이템까지 실제로 도달할 수 있는가 — 끝까지 민 뒤에
        /// 그 카드가 온전히 보이고 그 자리의 [착용] 버튼이 <b>눌리는가</b>까지 본다(보이기만 하고
        /// 마스크 밖이라 안 눌리면 도달한 것이 아니다).</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EveryCardInACategoryIsReachableByScrollingToTheEnd()
        {
            yield return OpenWindow();

            // 마지막 <b>카테고리</b>의 마지막 아이템 — 세로 격자에서 가장 멀리 있는 카드다.
            int lastSection = _window.VisibleSectionCount - 1;
            Assert.GreaterOrEqual(lastSection, 0, $"{LogPrefix} 보이는 카테고리가 하나도 없습니다.");
            Assert.IsTrue(_window.TryGetCardSlotForTests(FindCardOfSection(lastSection, 0), out EquipmentSlot lastSlot),
                $"{LogPrefix} 마지막 카테고리의 슬롯을 찾지 못했습니다.");

            int expected = ItemCatalog.ItemCountIn(lastSlot);
            int last = FindCardOfSection(lastSection, expected - 1);
            Assert.GreaterOrEqual(last, 0,
                $"{LogPrefix} 마지막 카테고리의 마지막({expected - 1}번) 카드를 찾지 못했습니다.");

            Rect viewport = _window.GridViewportScreenRect;

            // 전제: 처음에는 마지막 카드가 뷰포트 밖이다(그래서 밀 이유가 있다).
            Assert.Less(InsideViewportFraction(_window.CardRawScreenRect(last), viewport), 0.98f,
                $"{LogPrefix} 밀기도 전에 마지막 카드가 뷰포트 안에 다 들어와 있습니다 — 지킬 것이 없습니다.");

            float max = _window.GridMaxScrollPoints;
            Vector2 grab = GrabPointOnRow(0);
            _window.FeedPointerForTests(false, grab);
            _window.FeedPointerForTests(true, grab);
            _window.FeedPointerForTests(true, grab + new Vector2(0f, max + 500f));   // 끝까지(위로 끈다 = Unity y 증가)
            _window.FeedPointerForTests(false, grab + new Vector2(0f, max + 500f));
            yield return null;

            Assert.AreEqual(max, _window.GridScrollPoints, 0.5f,
                $"{LogPrefix} 끝까지 밀리지 않았습니다(스크롤 한계 {max:F1}pt).");

            float ratio = InsideViewportFraction(_window.CardRawScreenRect(last), _window.GridViewportScreenRect);
            Assert.GreaterOrEqual(ratio, 0.98f,
                $"{LogPrefix} 끝까지 밀었는데 마지막 카드가 뷰포트 안에 {ratio:P0}만 들어옵니다 — " +
                "카테고리의 일부가 어떤 방법으로도 도달 불가라는 뜻입니다.");

            // ★ "눌리는가"까지는 <b>창이 화면에 온전히 들어와 있을 때만</b> 물을 수 있다. 배치모드의 좁은
            //   화면에서는 ClampPanelToScreen이 창을 줄여 패널 마스크가 잘라내는데, 그건 격자가
            //   아니라 화면이 만든 잘림이고 그 경우는 InfoWindowClippedHitTestTests가 따로 잠근다.
            Rect card = _window.CardRawScreenRect(last);
            if (_window.CardVisibleScreenRect(last).width >= card.width - 1f
                && _window.CardVisibleScreenRect(last).height >= card.height - 1f)
            {
                Vector2 button = _window.CardEquipButtonRawScreenRect(last).center;
                Assert.IsTrue(_window.IsCardEquipButtonHittableAt(last, button),
                    $"{LogPrefix} 마지막 카드는 보이는데 [착용] 버튼이 눌리지 않습니다(마스크 밖) — " +
                    "보이는 것과 눌리는 것이 다르면 도달한 것이 아닙니다.");
            }
            else
            {
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 좁아 창이 클램프됐습니다 — " +
                    "[착용] 버튼 히트 판정은 이 환경에서 건너뜁니다(도달 가능성은 위에서 확인됨).");
            }
        }

        // ============================================================================
        // (b) 잡고 밀면 실제로 넘어간다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DraggingRowScrollsContentAndClampsAtBothEnds()
        {
            yield return OpenWindow();

            float max = _window.GridMaxScrollPoints;
            Assert.Greater(max, 1f,
                $"{LogPrefix} 격자가 밀릴 여지가 {max:F1}pt뿐입니다 — 카드가 뷰포트 안에 다 들어온다는 뜻이라 " +
                "드래그를 검증할 수 없습니다(카드 크기/개수 또는 레이아웃이 깨졌을 수 있습니다).");
            Assert.AreEqual(0f, _window.GridScrollPoints, 1e-3f,
                $"{LogPrefix} 창을 연 직후인데 이미 밀려 있습니다.");

            Rect viewport = _window.GridViewportScreenRect;
            Assert.Greater(viewport.height, 1f, $"{LogPrefix} 격자 뷰포트의 화면 사각형이 비었습니다.");

            // ★ 한 제스처는 <b>한 프레임 안에서</b> 끝내야 한다. 프레임을 넘기면 Update()의 실제 전역
            //    폴링이 "버튼 뗌"을 보고해 드래그가 그 자리에서 종료된다(제품에서는 그것이 옳은 동작이다).
            Vector2 grab = GrabPointOnRow(0);
            _window.FeedPointerForTests(false, grab);          // 첫 표본(창을 여는 클릭과의 혼동 방지)
            _window.FeedPointerForTests(true, grab);           // 누름
            _window.FeedPointerForTests(true, grab + new Vector2(0f, DragPoints));

            float pushed = _window.GridScrollPoints;
            Assert.Greater(pushed, 1f,
                $"{LogPrefix} 밀었는데 content가 {pushed:F2}pt에 그대로 있습니다 — 격자가 손을 따라오지 않습니다.");
            Assert.LessOrEqual(pushed, max + 0.01f,
                $"{LogPrefix} content가 스크롤 한계({max:F1}pt)를 넘어갔습니다 — 카드가 빈 자리로 사라집니다.");

            // 끝까지 민다 -> 한계에서 멈춘다.
            _window.FeedPointerForTests(true, grab + new Vector2(0f, max + 500f));
            Assert.AreEqual(max, _window.GridScrollPoints, 0.5f,
                $"{LogPrefix} 끝까지 밀었을 때 한계에서 멈추지 않았습니다(Clamped가 아닙니다).");

            // 반대로 되민다 -> 0에서 멈춘다.
            _window.FeedPointerForTests(true, grab + new Vector2(0f, -(max + 500f)));
            Assert.AreEqual(0f, _window.GridScrollPoints, 0.5f,
                $"{LogPrefix} 되밀었을 때 처음(0)에서 멈추지 않았습니다.");

            _window.FeedPointerForTests(false, grab);
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SwitchingTabResetsCarouselToStart()
        {
            yield return OpenWindow();

            Vector2 grab = GrabPointOnRow(0);
            _window.FeedPointerForTests(false, grab);
            _window.FeedPointerForTests(true, grab);
            _window.FeedPointerForTests(true, grab + new Vector2(0f, DragPoints));
            _window.FeedPointerForTests(false, grab + new Vector2(0f, DragPoints));   // 한 프레임 안에서 끝낸다
            yield return null;
            Assert.Greater(_window.GridScrollPoints, 1f, $"{LogPrefix} 전제: 먼저 밀려 있어야 합니다.");

            // [외형] 탭으로 넘어가면 0번 블록이 다른 카테고리(이펙트 — 2026-09-06 [머리] 은퇴 전에는 머리)를 맡는다.
            _window.FeedClickForTests(TabCenter(1));
            yield return null;
            Assert.AreEqual(0f, _window.GridScrollPoints, 1e-3f,
                $"{LogPrefix} 탭을 바꿔 다른 카테고리가 들어왔는데 스크롤이 남아 있습니다 — " +
                "아이템이 적은 카테고리에서는 빈 자리만 보이게 됩니다.");
        }

        // ============================================================================
        // (c) 카드 하단 버튼 = 착용, 그리고 같은 카테고리는 <b>하나만</b>
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardEquipButtonWearsAndCategoryStaysMutuallyExclusive()
        {
            yield return OpenWindow();

            int first = FindCardOfSection(0, 0);
            int second = FindCardOfSection(0, 1);
            Assert.GreaterOrEqual(first, 0, $"{LogPrefix} 모자 0번 카드를 찾지 못했습니다.");
            Assert.GreaterOrEqual(second, 0, $"{LogPrefix} 모자 1번 카드를 찾지 못했습니다.");
            EnsureOwned(EquipmentSlot.Head, 1);

            EquipmentModel.TryWear(EquipmentSlot.Head, 0, _config);
            yield return null;
            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Head), $"{LogPrefix} 전제 설정 실패.");

            // 1번 카드의 하단 [착용]을 누르고 <b>뗀다</b> — 착용은 뗄 때 확정된다.
            yield return ClickCardEquip(second);

            Assert.AreEqual(1, EquipmentModel.WornIndex(EquipmentSlot.Head),
                $"{LogPrefix} 카드 하단 버튼을 눌렀는데 착용되지 않았습니다.");
            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Head, 0),
                $"{LogPrefix} 같은 카테고리의 앞 모자가 그대로 착용 중입니다 — 카테고리당 하나라는 규칙이 깨졌습니다.");

            // 즉시 저장 — 파일이 새 아이디를 담고 있어야 한다.
            string saved = File.ReadAllText(CharacterSaveStore.FilePath);
            string expectedId = EquipmentModel.ItemId(EquipmentSlot.Head, 1);
            StringAssert.Contains(expectedId, saved,
                $"{LogPrefix} 저장 파일에 새로 착용한 '{expectedId}'가 없습니다 — 다시 켜면 차림이 되돌아갑니다.");

            // 같은 버튼을 한 번 더 누르면 벗는다.
            yield return ClickCardEquip(second);
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(EquipmentSlot.Head),
                $"{LogPrefix} 착용 중인 카드의 버튼([해제])이 벗기지 않았습니다.");
        }

        // ============================================================================
        // (d) 미는 동안에는 착용되지 않는다 — 이 버튼을 만든 목적의 절반
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DraggingFromEquipButtonDoesNotWear()
        {
            yield return OpenWindow();

            int card = FindCardOfSection(0, 1);
            Assert.GreaterOrEqual(card, 0, $"{LogPrefix} 모자 1번 카드를 찾지 못했습니다.");
            EnsureOwned(EquipmentSlot.Head, 1);
            EquipmentModel.TryWear(EquipmentSlot.Head, 0, _config);
            yield return null;

            Vector2 down = _window.CardEquipButtonRawScreenRect(card).center;
            _window.FeedPointerForTests(false, down);
            _window.FeedPointerForTests(true, down);                                   // 버튼 위에서 누름
            _window.FeedPointerForTests(true, down + new Vector2(0f, DragPoints));    // 그대로 민다
            _window.FeedPointerForTests(false, down + new Vector2(0f, DragPoints));   // 뗀다
            yield return null;

            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Head),
                $"{LogPrefix} 카드를 밀었을 뿐인데 옷이 갈아입혀졌습니다 — 착용 버튼을 카드로 내린 목적의 " +
                "절반이 '스크롤하다 실수로 착용되는 것'을 막는 것입니다.");
            Assert.Greater(_window.GridScrollPoints, 1f,
                $"{LogPrefix} 버튼 위에서 시작한 드래그가 격자를 밀지 못했습니다 — 카드 위 어디를 잡아도 밀려야 합니다.");
        }

        // ==================== 도구 ====================

        private IEnumerator ClickCardEquip(int cardIndex)
        {
            // ★ 같은 손잡이를 연달아 누를 때는 <b>중복 방지 창</b>(CharacterInfoWindow.ActionDedupSeconds
            //   = 0.35초)을 반드시 넘겨야 한다. 그 창은 버그가 아니라 설계다 — 한 번의 물리 클릭이
            //   uGUI/콜라이더/전역 폴링 세 경로로 도착하기 때문에 있는 것이고, 사람 손으로 같은 버튼을
            //   0.35초 안에 두 번 누르는 일은 없다. 테스트만 그 속도를 낼 수 있다.
            yield return new WaitForSecondsRealtime(0.4f);

            Vector2 p = _window.CardEquipButtonRawScreenRect(cardIndex).center;
            Assert.IsTrue(_window.IsCardEquipButtonHittableAt(cardIndex, p),
                $"{LogPrefix} {cardIndex}번 카드의 [착용] 버튼이 지금 누를 수 없는 자리에 있습니다(잘렸거나 꺼졌습니다).");

            _window.FeedPointerForTests(false, p);
            _window.FeedPointerForTests(true, p);
            _window.FeedPointerForTests(false, p);
            yield return null;
            yield return null;
        }

        /// <summary>이 아이템이 잠겨 있으면 <b>레벨을 실제로 올려서</b> 연다 — 임시 QA 해금 스위치가
        /// 꺼지는 날에도 이 파일이 그대로 돌게 한다(잠금 규칙 자체는 한 줄도 우회하지 않는다).</summary>
        private void EnsureOwned(EquipmentSlot slot, int item)
        {
            int guard = 0;
            while (!EquipmentModel.IsItemOwned(slot, item) && guard++ < 500)
            {
                CharacterProgressionModel.AddXp(1000f, _config);
            }
            Assert.IsTrue(EquipmentModel.IsItemOwned(slot, item),
                $"{LogPrefix} {slot} {item}번을 레벨을 올려도 열지 못했습니다(요구 레벨={EquipmentModel.RequiredLevel(slot, item)}).");
        }

        private int FindCardOfSection(int section, int item)
        {
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                if (_window.CardSectionForTests(i) == section && _window.CardItemForTests(i) == item) return i;
            }
            return -1;
        }

        /// <summary>탭 라벨의 화면 중심 — 탭 사각형은 창이 스스로 알고 있으므로 반사로 읽는다
        /// (AppearanceTabSectionTests와 같은 관례).</summary>
        private Vector2 TabCenter(int tabIndex)
        {
            var field = typeof(CharacterInfoWindow).GetField("_tabRects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tabs = field != null ? field.GetValue(_window) as RectTransform[] : null;
            Assert.IsNotNull(tabs, $"{LogPrefix} _tabRects를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            var corners = new Vector3[4];
            tabs[tabIndex].GetWorldCorners(corners);
            Vector3 c = (corners[0] + corners[2]) * 0.5f;
            return new Vector2(c.x, c.y);
        }
    }
}
