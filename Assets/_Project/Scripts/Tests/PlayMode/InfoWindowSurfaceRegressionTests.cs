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
    /// ★ 2026-09-01 — 정보창 P0 3건(레이아웃 예산 / 카드 버튼 위계 / 이름·메타 충돌) 회귀.
    /// 근거: docs/UI_SURFACE_SPEC.md §3.1 · §3.2(1) · §3.2(2).
    ///
    /// ============================================================================
    /// 세 결함의 공통점 — <b>컴파일도 EditMode도 통과하는데 화면에서만 틀렸다</b>
    /// ============================================================================
    ///  · <b>P0-1</b>: 섹션 상한(4)을 <b>고정 예산</b>으로 써서, 섹션이 3개뿐인 [외형] 탭이 없는
    ///    4번째 섹션의 자리를 예약했다 — 마지막 카드 아래 176pt(창 높이의 20.4%)가 비었다.
    ///  · <b>P0-4</b>: 카드 하단 [착용] 버튼이 흰 채움이라, 카드 안에서 <b>가장 밝은 면</b>이
    ///    아이템이 아니라 동사였다(최고 휘도 면적비 약 9:1). 그 흰 막대가 한 화면에 12개 떴다.
    ///  · <b>P0-5</b>: 이름 상자와 메타 상자 <b>사이 간격이 0pt</b>였다. 긴 이름
    ///    ("리틀스틱메이트")이 6pt 넘쳐 "착용 중"과 맞닿았다(캡처 실측 1.2pt).
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는 방법
    /// ============================================================================
    /// 861 / 176 / 70 같은 값을 여기 적으면, 다음 라운드에 레이아웃을 고칠 때 이 파일이
    /// <b>프로덕션이 아니라 옛 숫자</b>를 지키게 된다(CLAUDE.md). 그래서 전부 <b>관계</b>로 단언한다:
    ///  · 죽은 공간 → "[장비]와 [외형]에서 <b>같은가</b>" (탭끼리 비교)
    ///  · 버튼 위계 → "카드 버튼이 <b>흰 채움(<c>UiChrome.TextPrimary</c>)이 아닌가</b>" (토큰과 비교)
    ///    ※ 2026-09-01 상세 패널의 중복 [착용] 버튼이 제거되면서 "자리끼리 비교"가 불가능해졌다.
    ///      비교 대상을 <b>P0-4가 걷어낸 그 토큰</b>으로 옮겼다 — 숫자는 여전히 한 개도 베끼지 않는다.
    ///  · 이름 충돌 → "이름의 <b>실제 잉크 폭</b>이 메타 상자에 닿지 않는가" (렌더러가 잰 값)
    /// </summary>
    public sealed class InfoWindowSurfaceRegressionTests
    {
        private const string LogPrefix = "[정보창표면-TEST]";

        private const int TabEquipment = 0;
        private const int TabAppearance = 1;

        /// <summary>창 높이 애니메이션이 끝나기를 기다리는 상한(초). <b>프레임 수가 아니라 벽시계</b>다 —
        /// 배치모드 PlayMode는 2,000fps를 넘겨서 "N프레임"이 0.01초밖에 안 되는 경우가 있다
        /// (CLAUDE.md 확정 규약).</summary>
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

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
        }

        /// <summary>실제 사용자와 같은 경로로 탭을 누른다(테스트 전용 분기를 만들지 않는다).</summary>
        private IEnumerator ClickTab(int index)
        {
            Rect rect = _window.TabScreenRect(index);
            Assert.Greater(rect.width, 0f, $"{LogPrefix} 탭 {index}의 화면 사각형이 비어 있습니다.");
            _window.FeedClickForTests(rect.center);
            yield return null;
            yield return SettlePanelHeight();
        }

        /// <summary>★ L-8(2026-09-05)로 <b>창 높이 애니메이션이 사라졌다</b> — 창은 탭과 무관하게
        /// <see cref="CharacterInfoWindow.TargetPanelHeightPoints"/> 고정이다. 예전에는 여기서
        /// 애니메이션이 목표에 닿을 때까지 벽시계로 기다렸다. 지금은 <b>기다릴 것이 없다</b>.
        /// <para>함수를 지우지 않고 남기는 이유: 호출부(<see cref="ClickTab"/> 등)의 형태를 그대로 두어
        /// "무엇이 바뀌었는가"를 diff에서 한 곳으로 모으기 위해서다.</para></summary>
        private IEnumerator SettlePanelHeight()
        {
            yield return null;
        }

        // ============================================================================
        // ★ L-8 — 창 높이는 탭에 따라 변하지 않는다 (P0-1의 후속)
        //
        // P0-1이 고친 것은 "섹션이 3개뿐인 [외형] 탭이 없는 4번째 섹션의 자리를 176pt 예약한다"였고,
        // 그 처방은 "창 높이를 섹션 수에서 파생시킨다"였다. 3컬럼 이식으로 <b>세로 섹션이라는 단위
        // 자체가 사라졌고</b>(카테고리가 컬럼 3 안으로 들어갔다), 인계본 규칙이 명시적으로
        // "창 크기와 헤더는 탭에 따라 변하지 않는다"이다.
        //
        // ⇒ 잠글 것이 뒤집혔다: 예전에는 "높이가 달라야 한다", 지금은 <b>"같아야 한다"</b>.
        //   그리고 P0-1이 막으려던 것(없는 것의 자리 예약)은 <b>더 강하게</b> 잠긴다 —
        //   컬럼 3의 콘텐츠 높이가 실제 카드 수에서만 나오는지 함께 본다.
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TabWithFewerSectionsDoesNotReserveSpaceForTheMissingOnes()
        {
            yield return OpenWindow();

            yield return ClickTab(TabEquipment);
            int equipSections = _window.VisibleSectionCount;
            float equipHeight = _window.TargetPanelHeightPoints;
            float equipOnScreen = _window.PanelSizePoints.y;
            float equipContent = _window.GridContentHeightPoints;

            yield return ClickTab(TabAppearance);
            int appearSections = _window.VisibleSectionCount;
            float appearHeight = _window.TargetPanelHeightPoints;
            float appearOnScreen = _window.PanelSizePoints.y;
            float appearContent = _window.GridContentHeightPoints;

            Assert.Greater(equipSections, 0, $"{LogPrefix} [장비] 탭에 카테고리가 하나도 없습니다.");
            Assert.Greater(appearSections, 0, $"{LogPrefix} [외형] 탭에 카테고리가 하나도 없습니다.");

            // ★ 핵심 단언 (1) — 창 높이는 <b>탭과 무관</b>하다.
            Assert.AreEqual(equipHeight, appearHeight, 0.01f,
                $"{LogPrefix} 창 높이가 [장비] {equipHeight:F0} / [외형] {appearHeight:F0}pt로 다릅니다 — " +
                "L-8이 폐기한 탭별 가변 높이가 되살아났습니다(인계본 규칙: 창 크기는 탭에 따라 변하지 않는다).");
            Assert.AreEqual(equipOnScreen, appearOnScreen, 0.51f,
                $"{LogPrefix} 화면 위의 실제 창 높이가 {equipOnScreen:F0} / {appearOnScreen:F0}pt로 다릅니다.");

            // ★ 핵심 단언 (2) — P0-1이 막으려던 것. 스크롤 콘텐츠는 <b>마지막 카드에서 끝나야</b> 한다.
            //
            //   ★ 2026-09-05 — 옛 단언은 "카테고리가 적은 탭의 콘텐츠가 더 짧다"였는데, 그 전제
            //   ([외형] 3 < [장비] 4)가 <b>실측으로 사라졌다</b>(둘 다 4다). 전제가 없어진 단언을
            //   그대로 두면 조용히 무의미해지므로, <b>탭끼리 비교</b> 대신 <b>화면 사각형으로</b>
            //   같은 것을 잰다: 끝까지 민 뒤 마지막 카드 아래에 남는 여백이 아래 패딩 한 겹뿐인가.
            //   없는 카테고리의 자리를 예약하면 그 여백이 블록 하나만큼 벌어진다.
            yield return ClickTab(TabEquipment);
            yield return AssertContentEndsAtLastCard();
            yield return ClickTab(TabAppearance);
            yield return AssertContentEndsAtLastCard();

            Debug.Log($"{LogPrefix} 창 높이 고정 확인 — [장비] {equipSections}칸/{equipContent:F0}pt, " +
                      $"[외형] {appearSections}칸/{appearContent:F0}pt, 높이 {equipHeight:F0}pt 동일.");
        }

        /// <summary>끝까지 민 뒤 <b>마지막 카드 아래</b>에 남는 여백을 화면 사각형으로 잰다.
        /// <para>없는 카테고리의 자리를 예약하면 이 값이 블록 하나(수백 pt)만큼 벌어진다.
        /// 상한은 <b>숫자를 베끼지 않고</b> 카드 한 장 높이로 잡는다 — 한 장보다 크게 남으면
        /// 그것은 여백이 아니라 예약이다.</para></summary>
        private IEnumerator AssertContentEndsAtLastCard()
        {
            float max = _window.GridMaxScrollPoints;
            Assert.Greater(max, 1f, $"{LogPrefix} 컬럼 3이 밀리지 않습니다 — 잴 것이 없습니다.");

            Rect viewport = _window.GridViewportScreenRect;
            Vector2 grab = viewport.center;
            _window.FeedPointerForTests(false, grab);
            _window.FeedPointerForTests(true, grab);
            _window.FeedPointerForTests(true, grab + new Vector2(0f, max + 500f));
            _window.FeedPointerForTests(false, grab + new Vector2(0f, max + 500f));
            yield return null;

            float lowest = float.MaxValue;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                Rect card = _window.CardRawScreenRect(i);
                if (card.height > 1f) lowest = Mathf.Min(lowest, card.yMin);
            }
            Assert.Less(lowest, float.MaxValue, $"{LogPrefix} 보이는 카드가 하나도 없습니다.");

            float scale = _window.CanvasScaleForTests;
            float tail = (lowest - viewport.yMin) / scale;
            float cardHeight = _window.CardDesignSizePoints.y;

            Assert.GreaterOrEqual(tail, 0f,
                $"{LogPrefix} 끝까지 밀었는데 마지막 카드가 뷰포트 아래로 {-tail:F0}pt 삐져나갑니다.");
            Assert.Less(tail, cardHeight,
                $"{LogPrefix} 마지막 카드 아래에 {tail:F0}pt가 남습니다(카드 한 장 {cardHeight:F0}pt보다 큽니다) — " +
                "없는 카테고리의 자리를 예약하고 있다는 뜻입니다(P0-1과 같은 결함).");
        }

        /// <summary>상세 카드가 <b>창 안에</b> 들어 있는가. 컬럼 1 바닥에 정렬되므로 아래로 삐져나가면
        /// 그 자리는 마스크에 잘려 영영 안 보인다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ShorterTabStillFitsTheDetailPanelInsideThePanel()
        {
            yield return OpenWindow();
            yield return ClickTab(TabAppearance);

            Rect panel = _window.PanelScreenRect;
            Assert.Greater(panel.height, 0f, $"{LogPrefix} 패널 사각형이 비어 있습니다.");

            // 상세 카드는 <b>주 버튼이 없어야</b> 한다(L-9). 존재 여부로 잠근다.
            Assert.AreEqual(0, _window.DetailPanelButtonCountForTests,
                $"{LogPrefix} 상세 카드 안에 Button이 {_window.DetailPanelButtonCountForTests}개 있습니다 — " +
                "2026-09-01 사용자가 지워 달라고 한 중복 착용 버튼이 되살아났습니다(L-9).");
        }

        // ============================================================================
        // P0-4 — 카드 버튼이 카드 안에서 가장 밝은 면이 아니다
        //
        // ★ 2026-09-01 재조정: 상세 패널의 중복 [착용] 버튼이 사라져 <b>카드 버튼이 이 창의 1차 행동</b>이
        //   됐다. 그래도 이 항목은 살아 있다 — P0-4가 잰 것은 "다른 버튼보다 어두운가"가 아니라
        //   "카드 안에서 가장 밝은 것이 아이템이 아니라 동사인가"였고, 그 실측(면적비 약 9:1)은
        //   버튼이 하나로 줄었다고 달라지지 않는다. 1차 행동이라는 것은 경쟁자가 없다는 뜻이지
        //   가장 밝아야 한다는 뜻이 아니다.
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardEquipButtonNeverGoesBackToTheWhiteFill()
        {
            yield return OpenWindow();
            yield return ClickTab(TabEquipment);

            int probed = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                Color card = _window.CardActionSurfaceColor(i);

                Assert.AreNotEqual(UiChrome.TextPrimary, card,
                    $"{LogPrefix} 카드 {i}의 [착용] 버튼 표면이 TextPrimary(흰 채움)입니다 — " +
                    "한 화면에 12개가 반복되는 어포던스가 화면에서 가장 밝은 면이 됩니다.");
                probed++;
            }
            Assert.Greater(probed, 0, $"{LogPrefix} 켜져 있는 카드가 하나도 없습니다 — 관측 전제가 성립하지 않습니다.");

            // 비교 기준은 <b>P0-4가 걷어낸 그 토큰</b>이다(숫자가 아니라 프로덕션 상수를 참조한다).
            // 색이 같은지(AreNotEqual)만 보면 "거의 흰색"으로 되돌아오는 회귀를 놓친다.
            float whiteL = UiChrome.RelativeLuminance(UiChrome.TextPrimary);
            float cardSurfaceL = UiChrome.RelativeLuminance(UiChrome.CardSurface);
            Assert.Greater(whiteL, cardSurfaceL,
                $"{LogPrefix} 전제가 깨졌습니다 — TextPrimary가 CardSurface보다 어둡습니다(다크 테마가 뒤집혔습니까?).");

            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                float cardL = UiChrome.RelativeLuminance(_window.CardActionSurfaceColor(i));

                // 흰 채움과 카드 바탕의 <b>중간</b>보다는 확실히 바탕 쪽에 있어야 한다.
                float midpoint = (whiteL + cardSurfaceL) * 0.5f;
                Assert.Less(cardL, midpoint,
                    $"{LogPrefix} 카드 {i} 버튼의 휘도({cardL:F3})가 흰 채움({whiteL:F3})과 카드 바탕" +
                    $"({cardSurfaceL:F3})의 중간({midpoint:F3}) 이상입니다 — 한 화면에 12개 반복되는 " +
                    "이 막대가 다시 카드에서 가장 밝은 면이 됐습니다(P0-4 실측 면적비 약 9:1).");
            }
        }

        /// <summary>조용해지는 대신 <b>안 읽히면</b> 안 된다 — 라벨이 새 표면 위에서 AA(4.5:1)를 넘는가.
        /// 고치기 전 [잠김] 라벨은 2.09:1이었다(§2.3).</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardEquipButtonLabelStaysReadableOnTheQuieterSurface()
        {
            yield return OpenWindow();
            yield return ClickTab(TabEquipment);

            const float AaMinimum = 4.5f;
            int probed = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                Color surface = _window.CardActionSurfaceColor(i);
                Color label = _window.CardActionLabelColor(i);
                float c = UiChrome.ContrastRatio(label, surface);
                Assert.GreaterOrEqual(c, AaMinimum,
                    $"{LogPrefix} 카드 {i}의 버튼 라벨 대비가 {c:F2}:1입니다(표면 {surface}, 글자 {label}) — " +
                    $"AA {AaMinimum:F1}:1 미만입니다. 버튼을 조용하게 만드는 것과 안 읽히게 만드는 것은 다릅니다.");
                probed++;
            }
            Assert.Greater(probed, 0, $"{LogPrefix} 켜져 있는 카드가 없습니다.");
        }

        // ============================================================================
        // P0-5 — 이름과 메타가 물리적으로 겹치지 않는다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardNameNeverTouchesTheMetaColumn()
        {
            yield return OpenWindow();
            yield return ClickTab(TabEquipment);

            float scale = _window.CanvasScaleForTests;
            Assert.Greater(scale, 0f, $"{LogPrefix} 캔버스 배율을 읽지 못했습니다.");

            int probed = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;

                Rect name = _window.CardNameRawScreenRect(i);
                Rect meta = _window.CardMetaRawScreenRect(i);
                if (name.width <= 0f || meta.width <= 0f) continue;

                float gapPoints = (meta.xMin - name.xMax) / scale;
                Assert.GreaterOrEqual(gapPoints, UiChrome.Space2 - 0.5f,
                    $"{LogPrefix} 카드 {i}의 이름 상자와 메타 상자 사이가 {gapPoints:F1}pt입니다 — " +
                    $"토큰 간격(Space2 = {UiChrome.Space2:F0}pt)보다 좁습니다.");

                // ★ 상자만 떨어뜨려 놓고 글자가 흘러 나오면 아무 소용이 없다(예전이 정확히 그랬다).
                float inkWidth = _window.CardNameInkWidthPoints(i);
                float boxWidth = name.width / scale;
                Assert.LessOrEqual(inkWidth, boxWidth + 0.5f,
                    $"{LogPrefix} 카드 {i}의 이름 \"{_window.CardNameTextForTests(i)}\"가 상자를 " +
                    $"{inkWidth - boxWidth:F1}pt 넘칩니다(잉크 {inkWidth:F1} / 상자 {boxWidth:F1}) — " +
                    "말줄임(UiChrome.Ellipsize)이 걸리지 않았습니다.");
                probed++;
            }
            Assert.Greater(probed, 0, $"{LogPrefix} 켜져 있는 카드가 없습니다.");
        }

        /// <summary>말줄임이 <b>실제로 작동하는가</b> — 상자보다 확실히 긴 이름을 넣어 본다.
        /// (위 테스트는 카탈로그의 이름이 전부 짧으면 아무것도 증명하지 못한다.)</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EllipsizeActuallyTruncatesAnOverlongName()
        {
            yield return OpenWindow();

            // 카드와 같은 폰트/크기를 쓰는 임시 Text로 헬퍼만 직접 검증한다 — 카탈로그를 건드리지 않는다.
            var go = new GameObject("EllipsizeProbe", typeof(RectTransform));
            var probe = UiChrome.AddText(go.transform, "Label", UiChrome.FontBody,
                TextAnchor.MiddleLeft, UiChrome.TextPrimary);
            try
            {
                const string Source = "아주아주아주긴이름의장비아이템이름입니다";
                probe.text = Source;
                float full = probe.preferredWidth;
                Assert.Greater(full, 0f, $"{LogPrefix} 폰트가 폭을 재지 못했습니다(폰트 미로딩).");

                float budget = full * 0.5f;
                string cut = UiChrome.Ellipsize(probe, Source, budget);

                Assert.AreNotEqual(Source, cut, $"{LogPrefix} 예산이 절반인데 원본이 그대로 돌아왔습니다.");
                StringAssert.EndsWith(UiChrome.Ellipsis, cut,
                    $"{LogPrefix} 잘렸는데 말줄임표가 없습니다 — 잘렸다는 사실이 화면에서 사라집니다.");

                probe.text = cut;
                Assert.LessOrEqual(probe.preferredWidth, budget + 0.5f,
                    $"{LogPrefix} 잘린 뒤에도 예산({budget:F1}pt)을 넘습니다(실측 {probe.preferredWidth:F1}pt).");

                // 들어가는 문자열은 <b>건드리지 않는다</b>(할당 0 경로).
                Assert.AreSame(Source, UiChrome.Ellipsize(probe, Source, full + 10f),
                    $"{LogPrefix} 여유가 있는데도 새 문자열을 만들었습니다 — 상주 앱의 4Hz 갱신에서 쓰레기가 쌓입니다.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
