using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>「안 보이는 것을 볼 방법이 있는가」</b> — 결함 W-1 회귀(2026-09-08).
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼나 — 축 하나가 통째로 비어 있었다
    /// ============================================================================
    /// 이 창은 <b>「보이지 않는 것은 눌리지 않는다」</b>를 <c>InfoWindowClippedHitTestTests</c>로 이미
    /// 잠가 뒀다. 그 파일이 지키는 것은 *"안 보이는데 눌리지는 않는다"*까지이고,
    /// <b>*"안 보이는 것을 볼 방법이 있는가"*는 아무도 안 재고 있었다.</b>
    ///
    /// <para>그 사이로 <b>출하 중인 결함</b>이 지나갔다(<c>ux-designer</c> §14-6이 잡았다).
    /// <c>Body</c>는 앵커 스트레치라 창을 줄이면 함께 줄어드는데, 그 안의 <b>컬럼 루트 ·
    /// <c>Col2Viewport</c> · 컬럼 3 페이지</b>는 전부 컴파일 타임 상수 736으로 고정이었다.
    /// 스크롤 범위는 «콘텐츠 − <b>뷰포트</b>»이므로, <b>Body가 줄어도 스크롤 범위가 늘지 않았다</b>:</para>
    /// <code>
    /// Windows 1366×768 @100%  → Body 670 : 세트 패널 2행이 통째로 안 보이는데 최대 스크롤 0
    /// Windows 1920×1080 @150% → Body 622 : 세트 블록이 4pt만 보인다(제목도 안 읽힌다)
    /// </code>
    /// <b>스크롤은 붙어 있었다. 그런데 닿지 못했다.</b> 그리고 소스 주석이 *"스크롤이 그 구멍을
    /// 닫는다"*라고 적고 있어서 <b>다들 안심하고 있었다</b> — 이 저장소가 반복해 당한
    /// *"죽은 프로브가 산 프로브와 똑같이 생겼다"*의 UI판이다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 「도달성」 하나다
    /// ============================================================================
    ///  ① <b>구조 불변식</b> — 스크롤 뷰포트의 높이가 <b>Body의 실제 높이</b>와 같다.
    ///     기대값을 «패널 − 헤더»라는 <b>프로덕션과 같은 식</b>으로 만들지 않는다 —
    ///     <b>Body 사각형 자체를 재서</b> 비교한다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    ///  ② <b>산술</b> — 창을 줄이면 최대 스크롤이 <b>줄어든 만큼 정확히 늘어난다</b>
    ///     (콘텐츠 높이·열 수가 그대로임을 같은 테스트에서 확인한 뒤에 비교한다).
    ///  ③ <b>행동</b> — 줄어든 창에서 <b>마지막 카드의 [착용] 버튼</b>이 스크롤 0에서는 통째로
    ///     잘려 있고, <b>실제로 밀어 보면</b>(FeedPointerForTests — 진짜 입력 경로) 온전히 드러난다.
    ///  ④ <b>설계 크기에서는 아무것도 안 바뀐다</b> — 컬럼 2 최대 스크롤이 <b>0</b>이다
    ///     (「아래 여백은 뷰포트가 남긴 만큼만」 규칙이 4pt 가짜 스크롤을 없앴다).
    ///
    /// ★★ <b>이 파일에 조건부 건너뜀은 없다 — 네 건 전부 조건 없이 돈다.</b>
    /// 초판에는 «격자 모양이 바뀌면» · «컬럼 2가 접혀 있으면» 두 곳에 방어적 건너뜀이 있었고,
    /// 둘 다 <b>가로 화면에서는 도달하지 않는</b> 가지였다. 그런데 도달하지 않는 건너뜀도
    /// <b>「조용한 초록」의 씨앗</b>이다 — 언젠가 조건이 참이 되는 날 이 파일은 아무 소리 없이
    /// 러너에서 사라지고, 그 «건너뜀»은 «통과»와 <b>같은 색</b>으로 지나간다. 그래서 걷어냈다:
    /// 격자 모양 쪽은 <b>모양 변화까지 품는 항등식</b>으로 바꿨고(②), 컬럼 2 쪽은
    /// <b>시끄럽게 실패</b>하도록 바꿨다(④ — 거기서 접혀 있다면 «못 잰다»가 아니라 «클램프가 깨졌다»다).
    ///
    /// <para>★ <b>처방을 빼면 빨개지는가</b>(음성 대조): ①은 옛 코드에서 뷰포트 736 vs Body 616으로
    /// 즉시 빨갛고, ②는 «늘어난 양 0 ≠ 줄어든 양 120»으로 빨갛고, ③은 스크롤 끝에서도 마지막 카드가
    /// 절반만 보여 빨갛다. <b>세 개가 서로 다른 방식으로 같은 결함을 겨눈다.</b></para>
    ///
    /// ============================================================================
    /// 미확인으로 남긴 것 (추측으로 메우지 않는다)
    /// ============================================================================
    ///  · <b>코스튬 3행이 켜진 상태의 런타임 검증</b>. 그 상태를 만들려면 <c>office</c> 4부위를
    ///    실제로 입혀야 하고, 그 리그(<c>CostumeFocusRig</c>)는 이 라운드의 소유가
    ///    아니다. 3행판의 세로 예산은 <c>CharacterStatColumnLayoutTests</c>(C1/C2)와
    ///    <c>CostumeProgressReadoutTests</c>가 <b>상수 산술</b>로 잠근다.
    ///  · 실기 캡처. 배치 러너의 화면에서 <b>산술로</b> 재현한 것이고 실제 Windows 1366×768에서
    ///    눈으로 본 것이 아니다.
    /// </summary>
    public sealed class InfoWindowShrunkPanelReachTests
    {
        private const string LogPrefix = "[도달성-TEST]";

        /// <summary>실제 클램프 경로에 <b>스케일 팩터를 주입</b>한다 — 배치 실행에서 화면 크기를 바꿀
        /// 수단이 없다. <c>InfoWindowClippedHitTestTests</c>가 쓰는 것과 <b>같은 주입 지점</b>이다.</summary>
        private static readonly MethodInfo ClampMethod = typeof(CharacterInfoWindow).GetMethod(
            "ClampPanelToScreen", BindingFlags.Instance | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            UiLayoutModel.ResetForTesting();
        }

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            _window = null;
            yield return null;
        }

        // ====================================================================
        // ① 구조 불변식 — 뷰포트가 Body를 따라간다
        // ====================================================================

        /// <summary>
        /// ★ <b>스크롤 뷰포트의 높이 = Body의 실제 높이.</b> 설계 크기에서도, 줄어든 창에서도.
        ///
        /// <para>★ 기대값의 출처가 요점이다 — <b>Body 사각형을 직접 재서</b> 쓴다. 프로덕션이
        /// 「패널 − 헤더」로 계산하므로, 여기서도 그 식을 쓰면 <b>둘이 같이 틀려도 초록</b>이다.
        /// Body는 앵커 스트레치라 패널을 따라가는 <b>독립된 증인</b>이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 스크롤_뷰포트_높이가_본문_높이를_따라간다()
        {
            yield return LoadSceneAndOpenWindow();

            AssertViewportsFollowBody("설계 크기", ScaleFactorForDesignSize());
            AssertViewportsFollowBody("줄어든 창", ScaleFactorForShortScreen());
            // 되돌려도 따라온다 — 「한 번 줄면 못 돌아온다」도 결함이다.
            AssertViewportsFollowBody("되돌린 뒤", ScaleFactorForDesignSize());

            yield return null;
        }

        private void AssertViewportsFollowBody(string label, float scaleFactor)
        {
            Clamp(scaleFactor);

            float scale = Mathf.Max(0.0001f, _window.CanvasScaleForTests);
            float body = _window.BodyScreenRect.height / scale;
            Assert.Greater(body, 0f,
                $"{LogPrefix} [{label}] Body 사각형을 재지 못했습니다 — 이 측정의 기준이 없습니다.");

            float col3 = _window.GridViewportScreenRect.height / scale;
            Assert.AreEqual(body, col3, 1f,
                $"{LogPrefix} [{label}] 컬럼 3 뷰포트가 본문을 따라가지 않습니다 — 뷰포트 {col3}pt vs " +
                $"본문 {body}pt.\n" +
                "  스크롤 범위는 «콘텐츠 − 뷰포트»입니다. 뷰포트가 상수에 얼어붙으면 창이 줄어도 " +
                "범위가 늘지 않고, 잘려 나간 아래쪽에 <b>도달할 방법이 사라집니다</b>(결함 W-1).");

            // 컬럼 2는 좁은 창에서 접힌다(가로 강등 사다리) — 떠 있을 때만 잰다.
            float col2 = _window.Column2ViewportHeightForTests;
            if (_window.Column2ViewportScreenRect.width > 0f)
            {
                Assert.AreEqual(body, col2, 1f,
                    $"{LogPrefix} [{label}] 컬럼 2 뷰포트가 본문을 따라가지 않습니다 — 뷰포트 {col2}pt vs " +
                    $"본문 {body}pt (같은 결함, 같은 원인).");
            }
        }

        // ====================================================================
        // ② 산술 — 줄어든 만큼 스크롤 범위가 늘어난다
        // ====================================================================

        /// <summary>
        /// ★ 창을 줄이면 컬럼 3의 최대 스크롤이 <b>Body가 줄어든 만큼 정확히</b> 늘어난다.
        /// <para>비교가 성립하려면 <b>콘텐츠가 그대로여야</b> 한다(열 수가 2 → 1로 바뀌면 카드 총
        /// 높이가 달라진다). 그래서 열 수와 콘텐츠 높이를 <b>먼저 대조</b>하고, 어긋나면
        /// 「이 화면에서는 이 산술을 못 잰다」로 정직하게 건너뛴다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 창이_줄어든_만큼_컬럼3_스크롤_범위가_늘어난다()
        {
            yield return LoadSceneAndOpenWindow();

            Clamp(ScaleFactorForDesignSize());
            float scale = Mathf.Max(0.0001f, _window.CanvasScaleForTests);
            float tallBody = _window.BodyScreenRect.height / scale;
            float tallMax = _window.GridMaxScrollPoints;
            float tallContent = _window.GridContentHeightPoints;
            int tallColumns = MeasuredCardColumns();

            Clamp(ScaleFactorForShortScreen());
            float shortBody = _window.BodyScreenRect.height / scale;
            float shortMax = _window.GridMaxScrollPoints;
            float shortContent = _window.GridContentHeightPoints;
            int shortColumns = MeasuredCardColumns();

            // ---- 전제 (전부 <b>단언</b>이다 — 건너뛰지 않는다) ----
            Assert.Less(shortBody, tallBody - 1f,
                $"{LogPrefix} 창이 실제로 줄지 않았습니다 — 본문 {tallBody} → {shortBody}pt. " +
                "이 배치 화면에서는 클램프를 만들 수 없어 아래 판정이 공허합니다.");
            Assert.Greater(tallColumns, 0,
                $"{LogPrefix} 첫 카테고리에서 카드를 한 장도 못 셌습니다 — 열 수 측정기가 죽었습니다.");
            Assert.Greater(tallMax, 0f,
                $"{LogPrefix} 설계 크기에서 컬럼 3이 이미 다 들어갑니다(최대 스크롤 0) — " +
                $"콘텐츠 {tallContent}pt ≤ 본문 {tallBody}pt. 그러면 «넘치는 격자»라는 전제가 없어 " +
                "아래 항등식이 0 = 0으로 공허해집니다.");
            Assert.Greater(shortMax, 0f,
                $"{LogPrefix} 줄어든 창에서도 최대 스크롤이 0입니다 — 결함 W-1의 직접 증상입니다.");

            // ---- 본 판정 ----
            // ★ 2026-09-08 — 예전 판은 «격자 모양이 바뀌면 Assert.Ignore»였다. <b>걷어냈다.</b>
            //   화면비에 따라 컬럼 1·2가 접히면 격자 폭이 달라지는데, 그때 조용히 건너뛰면
            //   <b>이 저장소가 가장 자주 당한 형태</b>가 된다(건너뜀은 초록과 같은 색으로 지나간다).
            //   ⇒ 조건을 없애는 대신 <b>모양 변화까지 품는 항등식</b>으로 바꿨다:
            //
            //       max = 콘텐츠 − 뷰포트,  그리고 우리가 잠그려는 것은 «뷰포트 = 본문»이므로
            //       shortMax − tallMax = (shortContent − tallContent) + (tallBody − shortBody)
            //
            //   콘텐츠가 그대로면 오른쪽 첫 항이 0이 되어 예전 식과 <b>같은 값</b>이고,
            //   격자가 바뀌어도 <b>여전히 성립한다</b>. 결함 W-1이 되돌아오면 뷰포트가 736에 얼어붙어
            //   «(tallBody − shortBody)»만큼 어긋난다 — 그 항이 바로 이 테스트가 겨누는 것이다.
            float expectedGrowth = (shortContent - tallContent) + (tallBody - shortBody);
            Assert.AreEqual(expectedGrowth, shortMax - tallMax, 1.5f,
                $"{LogPrefix} 창을 {tallBody - shortBody}pt 줄였는데 최대 스크롤은 " +
                $"{tallMax} → {shortMax}pt로 {shortMax - tallMax}pt만 늘었습니다(기대 {expectedGrowth}pt).\n" +
                $"  격자: 열 {tallColumns} → {shortColumns}, 콘텐츠 {tallContent} → {shortContent}pt.\n" +
                "  ★ 어긋난 양이 «본문이 줄어든 양»과 같으면 결함 W-1이 되돌아온 것입니다 — " +
                "뷰포트가 상수에 얼어붙어 잘려 나간 아래쪽에 닿을 수 없습니다.");

            Debug.Log($"{LogPrefix} 산술 통과 — 본문 {tallBody} → {shortBody}pt, " +
                      $"최대 스크롤 {tallMax} → {shortMax}pt(콘텐츠 {tallContent} → {shortContent}pt, " +
                      $"열 {tallColumns} → {shortColumns}).");
            yield return null;
        }

        // ====================================================================
        // ③ 행동 — 실제로 밀어서 마지막 카드에 닿는다
        // ====================================================================

        /// <summary>
        /// ★★ <b>이 파일의 본체.</b> 줄어든 창에서 <b>가장 아래 카드의 [착용] 버튼</b>이
        /// 스크롤 0에서는 통째로 잘려 있고, <b>실제 입력으로 밀면</b> 온전히 드러난다.
        ///
        /// <para>스크롤 위치를 옮기는 테스트 창구를 <b>만들지 않았다</b> — 만들면 «실제로 밀린다»를
        /// 못 재게 된다. <c>FeedPointerForTests</c>(누름/이동/뗌)로 진짜 경로를 탄다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 줄어든_창에서_마지막_카드에_스크롤로_도달한다()
        {
            yield return LoadSceneAndOpenWindow();

            // ---- 양성 대조: 설계 크기에서는 밀면 마지막 카드가 온전히 드러난다 ----
            Clamp(ScaleFactorForDesignSize());
            int probeTall = LowestCardWithActionButton();
            Assert.GreaterOrEqual(probeTall, 0,
                $"{LogPrefix} [착용] 버튼이 살아 있는 카드를 하나도 찾지 못했습니다 — 관측 전제가 없습니다.");
            DragGridToEnd();
            Assert.Greater(_window.CardEquipButtonVisibleFraction(probeTall), 0.99f,
                $"{LogPrefix} 설계 크기에서 끝까지 밀었는데도 마지막 카드의 [착용] 버튼이 잘려 있습니다 — " +
                "이 양성 대조가 깨지면 아래 판정 전부 무효입니다(측정기가 죽었습니다).");

            // ---- 본 판정: 줄어든 창 ----
            Clamp(ScaleFactorForShortScreen());
            int probe = LowestCardWithActionButton();
            Assert.GreaterOrEqual(probe, 0,
                $"{LogPrefix} 줄어든 창에서 [착용] 버튼이 살아 있는 카드를 찾지 못했습니다.");

            // 스크롤을 맨 위로 되돌린다(위 양성 대조가 밀어 둔 자리를 그대로 쓰면 전제가 흐려진다).
            DragGridToTop();
            Assert.AreEqual(0f, _window.GridScrollPoints, 0.5f,
                $"{LogPrefix} 격자를 맨 위로 되돌리지 못했습니다 — 아래 «처음에는 안 보인다»의 전제가 깨집니다.");

            float hidden = _window.CardEquipButtonVisibleFraction(probe);
            Assert.Less(hidden, 0.01f,
                $"{LogPrefix} 줄어든 창의 맨 위에서 마지막 카드의 [착용] 버튼이 이미 {hidden:P0} 보입니다 — " +
                "이 화면에서는 «가려짐»이 만들어지지 않아 아래 판정이 공허합니다.");

            float max = _window.GridMaxScrollPoints;
            Assert.Greater(max, 0f,
                $"{LogPrefix} ★ 줄어든 창에서 최대 스크롤이 0입니다 — <b>안 보이는 것을 볼 방법이 없습니다</b>. " +
                "결함 W-1입니다(뷰포트가 Body를 따라가지 않습니다).");

            DragGridToEnd();

            Assert.AreEqual(max, _window.GridScrollPoints, 1f,
                $"{LogPrefix} 끝까지 밀리지 않았습니다 — 지금 {_window.GridScrollPoints}pt / 최대 {max}pt.");
            Assert.Greater(_window.CardEquipButtonVisibleFraction(probe), 0.99f,
                $"{LogPrefix} ★ 끝까지 밀었는데도 마지막 카드의 [착용] 버튼이 " +
                $"{_window.CardEquipButtonVisibleFraction(probe):P0}만 보입니다.\n" +
                "  스크롤은 «있는데» 닿지 못하는 상태입니다 — 결함 W-1의 정확한 증상이고, " +
                "그 화면에서는 아이템을 갈아입을 수단 자체가 사라집니다.");

            Debug.Log($"{LogPrefix} 도달성 통과({probe}번 카드) — 줄어든 창에서 " +
                      $"{max}pt 밀어 [착용] 버튼이 온전히 드러났습니다.");
            yield return null;
        }

        // ====================================================================
        // ④ 설계 크기에서는 아무것도 안 바뀐다
        // ====================================================================

        /// <summary>
        /// ★ 설계 크기에서 컬럼 2는 <b>밀리지 않는다</b>(최대 스크롤 0 · 콘텐츠 높이 = 뷰포트 높이).
        ///
        /// <para>이것이 「아래 여백은 뷰포트가 남긴 만큼만」 규칙의 계약면이다. 이 값이 4pt만 돼도
        /// 컬럼 2가 <b>잡히는 면</b>이 되고(«밀리는데 새로 드러나는 것이 없는» 가짜 어포던스),
        /// 민 직후 0.20초 동안 <b>컬럼 3 카드 버튼 클릭 1건이 삼켜진다</b>(§14-3).</para>
        ///
        /// <para><b>미확인</b>: 이 테스트는 오늘의 기본 상태(세트 패널 2행)를 잰다. 코스튬 3행이
        /// 켜진 상태를 배치에서 만들려면 <c>office</c> 4부위 리그(<c>CostumeFocusRig</c>)가 필요하고
        /// 그 파일은 이 라운드의 소유가 아니다 — 3행판은 EditMode의 C1/C2가 <b>상수 산술</b>로 잠근다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설계_크기에서_컬럼2는_밀리지_않는다()
        {
            yield return LoadSceneAndOpenWindow();

            Clamp(ScaleFactorForDesignSize());

            // ★ 2026-09-08 — 예전 판은 여기서 «컬럼 2가 접혀 있으면 Assert.Ignore»였다. <b>걷어냈다.</b>
            //   설계 크기 클램프(가용 세로 1200pt)는 가로도 함께 넉넉해지므로, 가로가 세로의
            //   0.9배만 돼도 창은 설계 폭에 닿고 컬럼 2가 뜬다 — 가로 화면이면 <b>언제나</b> 성립한다.
            //   즉 그 건너뜀은 실제로는 도달하지 않으면서 «조용한 초록»의 씨앗만 남기고 있었다.
            //   여기서 접혀 있다면 그건 «못 잰다»가 아니라 <b>클램프가 깨졌다</b>는 신호다 — 시끄럽게 실패한다.
            Assert.Greater(_window.Column2ViewportScreenRect.width, 0f,
                $"{LogPrefix} 설계 크기 클램프인데 컬럼 2가 접혀 있습니다(창 {_window.PanelSizePoints}pt / " +
                $"화면 {Screen.width}×{Screen.height}). 가로 강등 사다리가 설계 폭에서 걸렸다는 뜻이고, " +
                "그러면 이 창은 어떤 화면에서도 3컬럼으로 서지 못합니다.");

            float viewport = _window.Column2ViewportHeightForTests;
            float content = _window.Column2ContentHeightForTests;

            Assert.AreEqual(viewport, content, 0.5f,
                $"{LogPrefix} 설계 크기인데 컬럼 2 콘텐츠({content}pt)가 뷰포트({viewport}pt)와 다릅니다 — " +
                "잉크가 넘쳤거나, 아래 여백이 뷰포트 밖으로 삐져나와 «가짜 스크롤»을 만들고 있습니다.");

            Assert.AreEqual(0f, _window.Column2MaxScrollForTests, 0.5f,
                $"{LogPrefix} 설계 크기인데 컬럼 2가 {_window.Column2MaxScrollForTests}pt 밀립니다 — " +
                "밀어도 새로 드러나는 것이 없는 가짜 어포던스이고, 민 직후 0.20초 동안 컬럼 3의 " +
                "카드 버튼 클릭이 삼켜집니다.");

            Assert.Greater(_window.SetPanelVisibleFractionForTests, 0.99f,
                $"{LogPrefix} 설계 크기인데 세트 패널이 " +
                $"{_window.SetPanelVisibleFractionForTests:P0}만 보입니다 — 컬럼 2의 마지막 블록이 잘렸습니다.");

            yield return null;
        }

        // ==================== 도구 ====================

        /// <summary>
        /// 첫 카테고리의 카드 <b>화면 사각형</b>에서 열 수를 직접 센다(같은 y에 몇 장이 서 있는가).
        ///
        /// <para>★ <b><c>CharacterInfoWindow.CardGridColumns</c>를 일부러 쓰지 않는다 — 그 창구는
        /// 죽어 있다.</b> 그것은 런타임 값 <c>_gridColumns</c>가 아니라 <b>상수 <c>CardColumns</c>(2)</b>를
        /// 돌려주므로 창을 아무리 좁혀도 <b>항상 2</b>다. 자기 문서에는 *"컬럼 폭이 바뀌면 이 값이
        /// 따라와야 한다"*라고 적혀 있는데 구조적으로 따라오지 않는다 — 이 저장소가 반복해 당한
        /// «죽은 프로브가 산 프로브와 똑같이 생겼다»의 또 한 건이다. <b>리더에게 보고했다</b>
        /// (고치면 다른 팀 파일 <c>InfoWindowCardRowEdgeTests</c>의 단언이 함께 움직이므로
        /// 이 라운드가 임의로 손대지 않는다).</para>
        /// </summary>
        private int MeasuredCardColumns()
        {
            float top = float.MinValue;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i) || _window.CardSectionForTests(i) != 0) continue;
                Rect r = _window.CardRawScreenRect(i);
                if (r.width > 0f && r.yMax > top) top = r.yMax;
            }
            if (top <= float.MinValue) return 0;

            int columns = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i) || _window.CardSectionForTests(i) != 0) continue;
                Rect r = _window.CardRawScreenRect(i);
                if (r.width > 0f && Mathf.Abs(r.yMax - top) < 1f) columns++;
            }
            return columns;
        }

        /// <summary>가장 <b>아래</b>에 있는 카드(= 콘텐츠 맨 끝) 중 [착용] 버튼이 살아 있는 것.
        /// <b>인덱스를 상수로 적지 않는다</b> — 섹션 높이나 카드 배치를 바꾸면 지정된 번호가 조용히
        /// 무의미해지고, 그러면 이 파일이 아무것도 지키지 않게 된다.</summary>
        private int LowestCardWithActionButton()
        {
            int best = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (!_window.IsCardVisibleForTests(i)) continue;
                Rect action = _window.CardEquipButtonRawScreenRect(i);
                if (action.width <= 0f || action.height <= 0f) continue;
                if (action.yMin >= lowest) continue;
                lowest = action.yMin;
                best = i;
            }
            return best;
        }

        /// <summary>격자를 <b>끝까지</b> 민다 — 최대 스크롤보다 넉넉히 민 뒤 클램프에 맡긴다.</summary>
        private void DragGridToEnd() => DragGrid(_window.GridMaxScrollPoints + 200f);

        /// <summary>격자를 <b>맨 위로</b> 되돌린다(반대 방향으로 같은 양).</summary>
        private void DragGridToTop() => DragGrid(-(_window.GridMaxScrollPoints + 200f));

        /// <summary>
        /// 진짜 입력 경로로 격자를 민다. <paramref name="points"/>가 양수면 <b>아래쪽이 드러난다</b>.
        /// <para>부호 유도는 프로덕션의 <c>DragGridTo</c> 문단과 같다: content 피벗이 (0,1)이라
        /// <c>anchoredPosition.y</c>가 커질수록 아래가 드러나고, 직접 조작이므로 커서를 위로 끌면
        /// 콘텐츠도 위로 간다 ⇒ 화면 y를 <b>+</b> 방향으로 옮긴다.</para>
        /// </summary>
        private void DragGrid(float points)
        {
            Rect viewport = _window.GridViewportScreenRect;
            Assert.Greater(viewport.width, 0f,
                $"{LogPrefix} 컬럼 3 뷰포트의 화면 사각형이 비었습니다 — 밀 자리를 찾을 수 없습니다.");

            float scale = Mathf.Max(0.0001f, _window.CanvasScaleForTests);
            var start = new Vector2(viewport.center.x, viewport.center.y);
            var end = new Vector2(start.x, start.y + points * scale);

            _window.FeedPointerForTests(false, start);   // 첫 표본은 버려진다(창을 여는 클릭 방지 장치).
            _window.FeedPointerForTests(true, start);    // 누름 = 잡기
            _window.FeedPointerForTests(true, end);      // 이동 = 밀기
            _window.FeedPointerForTests(false, end);     // 뗌
        }

        private void Clamp(float scaleFactor)
        {
            Assert.IsNotNull(ClampMethod,
                $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다. " +
                "이 파일의 모든 측정이 무효입니다.");
            ClampMethod.Invoke(_window, new object[] { scaleFactor });
        }

        /// <summary>창이 설계 크기를 <b>다 쓸 만큼</b> 넉넉한 화면을 흉내내는 배율.
        /// 화면비와 무관하게 성립한다(가로·세로 둘 다 넉넉해진다).</summary>
        private static float ScaleFactorForDesignSize() => Mathf.Max(0.01f, Screen.height / 1200f);

        /// <summary>본문이 <b>확실히 짧아지는</b> 배율 — <b>가용 세로 600pt</b>짜리 화면을 흉내낸다.
        /// 창은 600pt로 클램프되고(설계 802에서 202pt 감소) 본문은 534pt가 된다. 컬럼 2의 잉크는
        /// 700pt이므로 <b>세트 패널이 확실히 잘린다</b>.
        /// <para>★ 16:9에서는 가로가 1042 상한에 걸려 <b>컬럼 2가 그대로 떠 있다</b> — 결함 W-1이
        /// 신고된 바로 그 형태(가로는 넉넉한데 세로만 모자란 노트북 해상도)가 여기서 재현된다.
        /// 더 좁은 화면비에서는 컬럼 2가 접히므로, 컬럼 2를 재는 단언은 «떠 있을 때만» 돈다.</para>
        /// <para>여백 숫자를 베끼지 않는다 — <see cref="UiWindowDrag.ScreenMarginPoints"/>를 참조한다
        /// (CLAUDE.md 하드코딩 금지). 600은 «흉내낼 가용 세로»라 프로덕션 상수가 아니다.</para></summary>
        private static float ScaleFactorForShortScreen()
            => Mathf.Max(0.01f, Screen.height / (600f + UiWindowDrag.ScreenMarginPoints * 2f));

        private IEnumerator LoadSceneAndOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에서 CharacterInfoWindow를 찾지 못했습니다.");

            _window.Open("도달성 회귀 테스트");
            yield return null;
            yield return null;
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");

            // ★ 여기부터는 <b>프레임을 넘기지 않는다</b> — Update가 매 프레임 실제 화면 크기로 다시
            //   클램프하므로, 주입한 크기는 그 프레임 안에서만 유효하다(레이아웃 그룹이 없어
            //   코너는 즉시 갱신된다 — InfoWindowClippedHitTestTests가 세운 관례와 같다).
        }
    }
}
