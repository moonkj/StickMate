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
    /// ★ 2026-09-02 보관함 레일 2건 — docs/UX_FLOW.md §45-9 / docs/UI_SURFACE_SPEC.md §14.5.
    ///
    /// ============================================================================
    /// 고친 결함 셋
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>지시자가 분수로 읽힌다</b> — <c>$"{page}\n/\n{pages}"</c>의 <b>명시적 개행</b> 때문에
    ///         세로로 쌓였고, 쌓인 <c>1 / 3</c>은 "3 중 1"이 아니라 <b>⅓</b>이다. 폭 부족 줄바꿈이
    ///         아니었다(이 <c>Text</c>는 <c>HorizontalWrapMode.Overflow</c>라 애초에 줄을 바꾸지 않는다).</item>
    ///   <item><b>지시자가 허공에 떴다</b> — 24 × 473pt 상자의 MiddleCenter라 [▲]에서 약 219pt 떨어진
    ///         자리에 글자가 놓였다. 그 사이에는 아무것도 없다(보관함 레일에는 트랙·썸이 없다).</item>
    ///   <item><b>[▲]가 1페이지에서도 [▼]와 픽셀 단위로 동일</b>했고, 눌러도 조용히 아무 일도
    ///         하지 않았다(<c>Mathf.Clamp</c>에 걸려서).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 24 / 8 / 3 같은 값을 여기 적으면 이 파일이 프로덕션이 아니라 옛 숫자를 지키게 된다.
    /// 그래서 전부 <b>관계와 토큰</b>으로 단언한다:
    /// <list type="bullet">
    ///   <item>폭 → <b>폰트가 실제로 잰 잉크 폭</b>(<c>preferredWidth</c>)이 레일 폭
    ///         (<see cref="CharacterInfoWindow.InventoryRailWidthPoints"/>)에 들어가는가.
    ///         설계의 "Arial advance 0.556em" 가정을 <b>실제 폰트로</b> 대체한다.</item>
    ///   <item>자리 → 글자 중심이 [▲]에서 <b>레일 폭 이내</b>에 있는가(= 붙어 있는가).</item>
    ///   <item>칩 → 색이 <see cref="UiChrome.InkIcon"/> 사다리에서 나오는가, 그리고 두 칩이
    ///         <b>실제로 다른가</b>.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 이 파일이 <b>검증하지 못하는 것</b> — 숨기지 않는다
    /// ============================================================================
    /// <b>좌표 클릭</b>이다. 배치모드 PlayMode 화면은 640×480이라 880pt 창이 608pt로 줄고, 우측
    /// 레일이 <c>Body</c> 마스크(x 16..624) 밖(x≈850)으로 <b>통째로 잘린다</b>. 그 자리는 이 창의
    /// "보이지 않는 것은 눌리지 않는다" 규칙에 따라 <b>물리적으로 눌리지 않는다</b> — 첫 실행에서
    /// 좌표 클릭 21번이 전부 무시됐다(거짓 빨강). 그래서 페이지 이동은 그 클릭이 부르는 <b>바로 그
    /// 함수</b>로 부르고, 가드는 <see cref="CharacterInfoWindow.CanScrollInventoryForTests"/>로 따로 본다.
    /// 좌표 클릭은 <b>실기 캡처</b> 몫이다(이 저장소 규칙: 최종 판정은 실제 빌드 캡처로만).
    /// </summary>
    public sealed class InventoryPagerRailTests
    {
        private const string LogPrefix = "[보관함레일-TEST]";

        /// <summary>[보관함] 탭 인덱스. <c>CharacterInfoWindow.Tab</c>이 private이라 창구가 여는 순서를 쓴다.</summary>
        private const int TabInventory = 2;

        private const float SettleTimeoutSeconds = 2.0f;

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenInventory()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;

            Rect tab = _window.TabScreenRect(TabInventory);
            Assert.Greater(tab.width, 0f, $"{LogPrefix} [보관함] 탭의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(tab.center);
            yield return null;
            yield return SettlePanelHeight();

            Assert.Greater(_window.MaxInventoryScrollForTests, 0,
                $"{LogPrefix} 보관함이 한 페이지에 다 들어갑니다(최대 스크롤 0) — " +
                "레일이 할 일이 없어 이 파일은 아무것도 증명하지 못합니다.");
        }

        private IEnumerator SettlePanelHeight()
        {
            float deadline = Time.realtimeSinceStartup + SettleTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Mathf.Abs(_window.AnimatedPanelHeightPoints - _window.TargetPanelHeightPoints) < 0.5f) yield break;
                yield return null;
            }
            Assert.Fail($"{LogPrefix} {SettleTimeoutSeconds:F1}초 안에 창 높이가 목표에 닿지 않았습니다.");
        }

        /// <summary>
        /// 칩을 "누른다" — 다만 <b>클릭 좌표가 아니라 그 클릭이 부르는 함수</b>로 부른다.
        ///
        /// <para>★ 이유(2026-09-02 실측): 배치모드 PlayMode 화면은 <b>640×480</b>이라 880pt 창이
        /// 608pt로 줄고, 우측 레일이 <c>Body</c> 마스크(x 16..624) <b>밖</b>(x≈850)으로 통째로 잘린다.
        /// 그 자리는 <b>물리적으로 눌리지 않는다</b> — 이 창의 "보이지 않는 것은 눌리지 않는다" 규칙이
        /// 정상 작동한 결과다. 첫 실행에서 <see cref="CharacterInfoWindow.FeedClickForTests"/>로 21번을
        /// 눌러도 스크롤이 0이었다(거짓 빨강).</para>
        ///
        /// <para>그래서 이 파일은 <b>겉모습이 판정을 따라오는가</b>를 잠근다. 두 클릭 경로가 그
        /// 판정(<see cref="CharacterInfoWindow.CanScrollInventoryForTests"/>)을 <b>둘 다</b> 보는 것은
        /// 코드 구조가 보장한다(사본이 없다). 실제 좌표 클릭은 <b>실기 캡처</b> 몫으로 남긴다.</para>
        /// </summary>
        private IEnumerator PressChip(int direction)
        {
            _window.ScrollInventoryForTests(direction);
            yield return null;
        }

        // ============================================================================
        // (1) 지시자 — 한 줄이고, 레일에 들어가고, [▲]에 붙어 있다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PageIndicatorIsOneHorizontalLineThatFitsTheRail()
        {
            yield return OpenInventory();

            string text = _window.PageIndicatorTextForTests;
            Assert.IsNotNull(text, $"{LogPrefix} 페이지 지시자를 찾지 못했습니다.");

            Assert.IsFalse(text.Contains("\n"),
                $"{LogPrefix} 지시자가 \"{text.Replace("\n", "\\n")}\"입니다 — 세로로 쌓인 1 / 3은 " +
                "\"3 중 1\"이 아니라 분수 ⅓으로 읽힙니다(45-9-a). 깨진 글자가 아니라 다른 뜻이라 더 나쁩니다.");

            float ink = _window.PageIndicatorInkWidthPoints;
            float rail = _window.InventoryRailWidthPoints;
            Assert.Greater(ink, 0f, $"{LogPrefix} 지시자의 잉크 폭을 읽지 못했습니다(폰트가 재지 못함).");
            Assert.LessOrEqual(ink, rail,
                $"{LogPrefix} 지시자 \"{text}\"의 실제 잉크 폭이 {ink:F2}pt로 레일 {rail:F0}pt를 " +
                $"{ink - rail:F2}pt 넘칩니다 — 그때가 진짜 줄바꿈 문제입니다(설계는 Arial " +
                "advance 0.556em을 가정해 19.46pt로 계산했고, 이 단언이 그 가정을 실제 폰트로 대체합니다).");

            Debug.Log($"{LogPrefix} 지시자 \"{text}\" — 실측 잉크 폭 {ink:F2}pt / 레일 {rail:F0}pt.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PageIndicatorSitsRightUnderTheUpChip()
        {
            yield return OpenInventory();

            float scale = _window.CanvasScaleForTests;
            Assert.Greater(scale, 0f, $"{LogPrefix} 캔버스 배율을 읽지 못했습니다.");

            Rect up = _window.PagerChipRawScreenRect(-1);
            Rect indicator = _window.PageIndicatorRawScreenRect;
            Assert.Greater(up.width, 0f, $"{LogPrefix} [▲] 칩의 화면 사각형이 비어 있습니다.");
            Assert.Greater(indicator.width, 0f, $"{LogPrefix} 지시자의 화면 사각형이 비어 있습니다.");

            // ★ 상자의 <b>윗변</b>이 아니라 <b>중심</b>을 본다. 옛 결함에서도 상자 윗변은 [▲] 바로
            //   아래(8pt)에 있었다 — 문제는 상자가 473pt로 길고 정렬이 MiddleCenter여서 <b>글자가</b>
            //   상자 한가운데(= [▲]에서 219pt)에 놓였다는 것이다. 글자의 세로 위치 = 상자 중심이다.
            float gapPoints = (up.yMin - indicator.center.y) / scale;
            float rail = _window.InventoryRailWidthPoints;

            Assert.Greater(gapPoints, 0f,
                $"{LogPrefix} 지시자 글자가 [▲]보다 위에 있습니다({gapPoints:F1}pt) — 순서가 뒤집혔습니다.");
            Assert.LessOrEqual(gapPoints, rail,
                $"{LogPrefix} 지시자 글자가 [▲] 아래 {gapPoints:F1}pt 떨어진 곳에 있습니다(레일 폭 " +
                $"{rail:F0}pt 이내여야 붙어 있다고 할 수 있습니다). 옛 결함은 219pt였고, 그 사이에는 " +
                "아무것도 없어 숫자가 허공에 떴습니다 — \"[▲]를 누르면 이 숫자가 준다\"는 인과가 안 붙습니다.");

            Debug.Log($"{LogPrefix} 지시자 글자가 [▲] 아래 {gapPoints:F1}pt에 붙어 있습니다.");
        }

        // ============================================================================
        // (2) 페이지 칩 — 겉모습이 "실제로 움직일 수 있는가"에서 나온다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FirstPageShowsADeadUpChipAndALiveDownChip()
        {
            yield return OpenInventory();

            Assert.AreEqual(0, _window.InventoryScrollForTests,
                $"{LogPrefix} 보관함이 첫 페이지에서 시작하지 않았습니다.");

            Color upGlyph = _window.PagerGlyphColorForTests(-1);
            Color downGlyph = _window.PagerGlyphColorForTests(+1);

            Assert.AreNotEqual(downGlyph, upGlyph,
                $"{LogPrefix} 첫 페이지인데 [▲]와 [▼]의 글리프 색이 같습니다({upGlyph}) — " +
                "눌러도 아무 일도 안 하는 칩이 살아 있는 칩과 픽셀 단위로 똑같습니다(45-9-b).");
            Assert.AreEqual(UiChrome.InkIcon(false), upGlyph,
                $"{LogPrefix} 첫 페이지의 [▲]가 아이콘 사다리의 '죽은' 색이 아닙니다.");
            Assert.AreEqual(UiChrome.InkIcon(true), downGlyph,
                $"{LogPrefix} 첫 페이지의 [▼]가 아이콘 사다리의 '산' 색이 아닙니다.");
            Assert.AreNotEqual(_window.PagerOutlineColorForTests(+1), _window.PagerOutlineColorForTests(-1),
                $"{LogPrefix} 두 칩의 테두리 색이 같습니다 — 죽은 칩이 죽어 보이지 않습니다.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DeadUpChipDoesNothingAndLiveDownChipScrolls()
        {
            yield return OpenInventory();

            // 죽은 [▲] — 프로덕션 진입점을 그대로 불러도 아무 일이 없어야 한다.
            yield return PressChip(-1);
            Assert.AreEqual(0, _window.InventoryScrollForTests,
                $"{LogPrefix} 첫 페이지에서 [▲]를 눌렀는데 스크롤이 " +
                $"{_window.InventoryScrollForTests}로 움직였습니다.");

            // 산 [▼] — 실제로 움직여야 한다(대조군: 위 단언이 '레일이 죽어서' 통과한 것이 아님을 보인다).
            yield return PressChip(+1);
            Assert.Greater(_window.InventoryScrollForTests, 0,
                $"{LogPrefix} [▼]를 눌렀는데 스크롤이 그대로입니다 — 레일 자체가 죽었다는 뜻이라 " +
                "위 [▲] 단언도 아무것도 증명하지 못합니다.");

            Assert.AreEqual(UiChrome.InkIcon(true), _window.PagerGlyphColorForTests(-1),
                $"{LogPrefix} 한 페이지 내려왔는데 [▲]가 아직 죽어 있습니다 — 겉모습이 " +
                "스크롤 값을 따라오지 않았습니다.");
        }

        /// <summary>★ 핵심 계약 — <b>겉모습과 클릭 판정이 같은 하나를 본다.</b>
        /// 두 벌로 두면 반드시 한쪽만 갱신되고, 그게 곧 표시-실제 불일치다(45-9-b).
        /// 스크롤 값을 처음/중간/끝으로 옮겨 가며 <b>세 자리 전부</b>에서 확인한다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ChipLookAlwaysMatchesTheScrollVerdict()
        {
            yield return OpenInventory();

            int checkedPositions = 0;
            int guard = 0;
            while (guard++ < 20)
            {
                AssertChipMatchesVerdict(-1);
                AssertChipMatchesVerdict(+1);
                checkedPositions++;

                if (!_window.CanScrollInventoryForTests(+1)) break;
                yield return PressChip(+1);
            }

            Assert.Greater(checkedPositions, 2,
                $"{LogPrefix} 확인한 스크롤 자리가 {checkedPositions}곳뿐입니다 — " +
                "처음/중간/끝을 다 보지 못했으므로 이 초록은 약합니다.");
            Assert.IsFalse(_window.CanScrollInventoryForTests(+1),
                $"{LogPrefix} 끝까지 내려가지 못했습니다.");

            Debug.Log($"{LogPrefix} 스크롤 자리 {checkedPositions}곳에서 겉모습과 판정이 일치했습니다.");
        }

        private void AssertChipMatchesVerdict(int direction)
        {
            bool can = _window.CanScrollInventoryForTests(direction);
            string chip = direction < 0 ? "▲" : "▼";
            Assert.AreEqual(UiChrome.InkIcon(can), _window.PagerGlyphColorForTests(direction),
                $"{LogPrefix} 스크롤 {_window.InventoryScrollForTests}/{_window.MaxInventoryScrollForTests}에서 " +
                $"[{chip}]의 글리프 색이 판정(움직일 수 있다={can})과 어긋납니다.");
            Assert.AreEqual(UiChrome.Flatten(can ? UiChrome.CardBorder : UiChrome.Divider, UiChrome.CardSurface),
                _window.PagerOutlineColorForTests(direction),
                $"{LogPrefix} [{chip}]의 테두리 색이 판정(움직일 수 있다={can})과 어긋납니다. " +
                "합성 바탕은 CardSurface입니다 — 설정창(CardSurfaceMuted)의 결과색을 베끼면 여기서 어긋납니다.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LastPageFlipsWhichChipIsDead()
        {
            yield return OpenInventory();

            int guard = 0;
            while (_window.InventoryScrollForTests < _window.MaxInventoryScrollForTests && guard++ < 20)
            {
                yield return PressChip(+1);
            }

            Assert.AreEqual(_window.MaxInventoryScrollForTests, _window.InventoryScrollForTests,
                $"{LogPrefix} [▼]를 {guard}번 불러도 마지막 페이지에 닿지 못했습니다.");

            Assert.AreEqual(UiChrome.InkIcon(false), _window.PagerGlyphColorForTests(+1),
                $"{LogPrefix} 마지막 페이지인데 [▼]가 아직 살아 보입니다.");
            Assert.AreEqual(UiChrome.InkIcon(true), _window.PagerGlyphColorForTests(-1),
                $"{LogPrefix} 마지막 페이지인데 [▲]가 죽어 보입니다.");

            int before = _window.InventoryScrollForTests;
            yield return PressChip(+1);
            Assert.AreEqual(before, _window.InventoryScrollForTests,
                $"{LogPrefix} 마지막 페이지에서 [▼]를 눌렀는데 스크롤이 움직였습니다.");

            Debug.Log($"{LogPrefix} 마지막 페이지(스크롤 {before} / 상한 {_window.MaxInventoryScrollForTests}) — " +
                $"지시자 \"{_window.PageIndicatorTextForTests}\".");
        }
    }
}
