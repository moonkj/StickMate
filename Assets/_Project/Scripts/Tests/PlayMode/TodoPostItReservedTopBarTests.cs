using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>포스트잇 카드가 macOS 메뉴 막대의 아래 절반을 덮는다</b> — 2026-09-03,
    /// <c>persona-newcomer</c>(민지) 신고 + <c>ux-widgets</c> 실측 일치.
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (실측, 화면 위 끝 기준 pt)
    /// ============================================================================
    /// <code>
    ///   메뉴 막대 : 0 ~ 33pt
    ///   카드 상단 : 16pt  (TodoPostItWidget.PanelInsetPoints 로 굳어 있었다)
    ///   겹침      : 17pt = 띠의 51.5%          ← 민지의 "아래 절반"과 수치가 맞는다
    /// </code>
    /// 16은 <b>화면 여백</b>으로 고른 값이지 "메뉴바를 피한 값"이 아니었다. 톱니가 2026-09-02에
    /// 옛 <c>MarginTopPoints = 58</c>을 버리고 <c>ReservedTopBarProbe</c> → <c>SurfaceSafeAreaPolicy</c>
    /// 로 갈아탄 것과 <b>같은 병 · 같은 처방</b>이다.
    ///
    /// ============================================================================
    /// ★ 이 파일의 진짜 표적은 <b>짝 변경</b>이다 — 배치만 옮기면 옛 버그가 되살아난다
    /// ============================================================================
    /// 카드를 띠 아래로 내리면서 <b>톱니 회피 판정</b>만 옛 상수 16에 남겨 두면, 판정이 배치보다
    /// <b>띠 두께만큼 뒤처진</b> 구간이 생긴다. 그 구간에서 판정은 "안 겹친다"고 말하고 카드는
    /// 가만히 있으며, 실제로는 <b>카드가 톱니를 덮는다</b>(51-9-3 재발).
    ///
    /// <para><b>발산 구간</b> — 이 픽스처는 이 값을 <b>손으로 적지 않고 라이브 실측에서 다시 만든다</b>.
    /// (참고용 검산, 메뉴 막대 33pt · 톱니 히트 반지름 19.82 · 톱니 중심 y 기준)
    /// <code>
    ///   1행(카드 높이 80)  : [16+80+19.82, 49+80+19.82)  = [115.82, 148.82)   폭 33.00 = 띠 두께
    ///   8행(카드 높이 276) : [16+276+19.82, 49+276+19.82) = [311.82, 344.82)   폭 33.00
    ///   Windows 상단 도킹 40pt 에서는 폭이 40으로 더 넓다 : [115.82, 155.82) / [311.82, 351.82)
    /// </code>
    /// 폭이 <b>정확히 띠 두께와 같다</b>는 것이 이 결함의 서명이다.</para>
    ///
    /// ============================================================================
    /// 규율 — 모든 "안 덮는다"에 <b>양성 대조</b>가 붙는다
    /// ============================================================================
    /// "안 덮는다"는 두 세계에서 똑같이 생겼다: ① 처방이 실제로 밀어냈다 ② 애초에 카드가 안 떴거나
    /// 톱니를 못 찾아 아무 일도 없었다. 그래서 각 검사는 <b>처방 전 자리로 되돌린 사각형</b>이
    /// 실제로 겹치는 것을 먼저 보이고, 그 다음에 지금 자리를 잰다.
    ///
    /// <para>★ <b>상수를 숫자로 베끼지 않는다</b>(CLAUDE.md). 카드 여백은
    /// <see cref="TodoPostItWidget.PanelInsetPoints"/>를, 톱니 여유는
    /// <see cref="GearRadialMenuWidget.ScreenMarginPoints"/>를 <b>참조</b>한다. 카드 폭·높이와 톱니
    /// 반지름은 <b>라이브 사각형에서 잰다</b>. 숫자로 적힌 것은 <see cref="MenuBarPoints"/> ·
    /// <see cref="TopDockedTaskbarPoints"/> · <see cref="RightDockedTaskbarNarrowPoints"/> ·
    /// <see cref="RightDockedTaskbarWidePoints"/> · <see cref="GearParkedBottomOffsetPoints"/>뿐인데,
    /// 그것들은 프로덕션 상수가 아니라 <b>OS가 주는 환경 입력 / 이 테스트가 고른 배치</b>다
    /// (주입값 = 관측 대상).</para>
    ///
    /// ============================================================================
    /// ★ 2026-09-03 가로축 승격 — 이 파일의 <c>Assert.Ignore</c> 한 건이 없어졌다
    /// ============================================================================
    /// 아래 <b>④절</b>은 오전까지 <c>작업표시줄_좌우_도킹은_가로축에_같은_문제를_남긴다_미해결()</c>
    /// 이라는 <c>Assert.Ignore</c> 한 줄이었다. 사유는 <i>"IReservedTopBarService에 대응하는 측면 사실
    /// 조회 계약이 없어 이 파일에서는 고칠 수 없다"</i>였는데, 같은 날 <c>dev-platform</c>이
    /// <c>Platform/IReservedScreenEdgeService.cs</c> · <c>ReservedEdgeProbe.cs</c> ·
    /// <c>SurfaceSafeAreaPolicy.ClampRightAnchoredInset</c>을 착지시키면서 <b>그 사유가 낡았다</b>.
    /// 그래서 실단언으로 승격했다. 남아 있는 <c>Assert.Ignore</c>는 ③절의 <b>환경 가드</b> 하나뿐이다.
    /// </summary>
    public sealed class TodoPostItReservedTopBarTests
    {
        private const string LogPrefix = "[포스트잇상단띠-TEST]";

        /// <summary>소프트캡 경고를 보지 않으므로 넉넉히 잡는다(다른 투두 픽스처와 같은 관례).</summary>
        private const int SoftCap = 99;

        /// <summary>macOS 메뉴 막대 두께(pt). <b>환경 입력</b>이다 — 이 개발 머신 실측값이고
        /// 프로덕션 어디에도 이 숫자는 없다(있으면 그것이 결함이다).</summary>
        private const float MenuBarPoints = 33f;

        /// <summary>Windows 작업표시줄을 <b>상단에 도킹</b>했을 때의 두께(pt). 같은 이유로 환경 입력이다.</summary>
        private const float TopDockedTaskbarPoints = 40f;

        /// <summary>Windows 작업표시줄을 <b>우측에 도킹</b>했을 때의 통상 두께(pt) 두 값. 같은 이유로
        /// 환경 입력이다 — 프로덕션 어디에도 이 숫자는 없다. macOS Dock 우측 배치도 같은 축이다.</summary>
        private const float RightDockedTaskbarNarrowPoints = 48f;
        private const float RightDockedTaskbarWidePoints = 62f;

        /// <summary>예약 띠가 없는 환경(메뉴 막대 자동 숨김 / 작업표시줄 하단·좌·우 도킹 / 모바일).
        /// <b>이 값에서 좌표가 변경 전과 같아야 한다</b> — 회귀 없음의 증거다.</summary>
        private const float NoReservedBarPoints = 0f;

        /// <summary>가로축 검사에서 톱니를 <b>화면 아래쪽에 세워 두는</b> 거리(화면 아래 끝에서 pt).
        /// <para>왜 필요한가: 기본 자리의 톱니는 카드와 세로로 겹쳐서 카드를 왼쪽으로 민다(51-9-3 회피).
        /// 그 상태에서는 "카드 우변이 어디 있는가"가 <b>띠 때문인지 톱니 때문인지</b> 갈리지 않는다.
        /// 톱니를 카드 아래로 내려 두면 가로 좌표가 오직 예약 띠 하나로만 정해진다.</para>
        /// <para>이 값도 <b>환경 입력</b>이다(테스트가 고른 배치). 다만 충분히 내려갔는지는 짐작하지 않고
        /// <see cref="AssertGearIsParkedAwayFromCard"/>가 <b>실측한 사각형으로</b> 확인한다.</para></summary>
        private const float GearParkedBottomOffsetPoints = 40f;

        /// <summary>원변 발산 구간 검사에서 톱니를 놓을 <b>세로</b> 위치(창 좌상단 원점 pt).
        /// <para>1행 카드의 세로 범위가 대략 16~96pt이므로 그 <b>한가운데</b>다. 톱니 자체의 세로 클램프
        /// 하한(히트 반지름 ≈ 19.8pt)보다도 충분히 아래여서 클램프에 걸리지 않는다.
        /// 이 값도 <b>테스트가 고른 배치</b>이고, 실제로 세로가 겹치는지는 짐작하지 않고
        /// 그 검사가 <b>실측한 사각형으로</b> 전제 단언한다.</para></summary>
        private const float GearDivergenceCenterYPoints = 48f;

        private TodoPostItWidget _widget;
        private InfoGearIconWidget _gear;

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
        public IEnumerator Cleanup()
        {
            TodoListModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            // ★ 주입한 띠를 반드시 걷는다(다음 픽스처 오염 방지). <b>네 방향 프로브부터</b> 걷는다 —
            //   ReservedEdgeProbe.ResetForTests()는 상단 프로브도 함께 걷지만, 반대는 성립하지 않는다
            //   (Platform/ReservedEdgeProbe.cs 클래스 문서). 상단만 걷으면 이쪽 오버라이드가 살아남는다.
            ReservedEdgeProbe.ResetForTests();
            ReservedTopBarProbe.ResetForTests();
            _widget = null;
            _gear = null;
            yield return null;
        }

        // ==================================================================
        // 준비
        // ==================================================================

        /// <summary>상단 예약 띠를 <paramref name="reservedPoints"/>로 <b>고정</b>하고 할 일
        /// <paramref name="count"/>건이 뜬 상태를 만든다. 띠 주입이 씬 로드보다 먼저다 —
        /// 위젯의 <c>Awake</c>가 최초 배치를 하기 때문이다.
        /// <para>★ <b>상단만 측정된 묶음</b>으로 넣는다(<see cref="ReservedEdgeInsets.TopOnly"/>) —
        /// 좌·우·하단은 값 0이지만 <b>측정 비트가 없다</b>. 그래서 이 픽스처의 세로축 검사들은
        /// 가로축 배선이 생긴 뒤에도 <b>가로에 대해 아무것도 바꾸지 않은 세계</b>에서 돈다.
        /// 여기서 <c>Observed(0,0,0,0)</c>을 쓰면 "좌우를 재 봤더니 0이더라"라는 <b>다른 사실</b>이 되고,
        /// 세로 검사가 조용히 가로 배선까지 통과하게 된다.</para></summary>
        private IEnumerator ShowCardWith(int count, float reservedPoints)
        {
            yield return ShowCardWithEdges(count, ReservedEdgeInsets.TopOnly(reservedPoints), null);
        }

        /// <summary>
        /// 네 변 예약 띠를 <paramref name="edges"/>로 <b>고정</b>하고 카드를 띄운다.
        /// <see cref="ShowCardWith"/>(세로축)와 가로축 검사가 <b>같은 준비 경로 하나</b>를 쓴다.
        /// </summary>
        /// <param name="gearCenterPoints">톱니를 여기(창 좌상단 원점 pt)에 세워 둔다. <c>null</c>이면
        /// 기본 자리(우상단)다. 씬 로드 <b>전에</b> 넣어야 한다 — 위젯이 첫 LateUpdate에 딱 한 번 읽는다.</param>
        private IEnumerator ShowCardWithEdges(int count, ReservedEdgeInsets edges, Vector2? gearCenterPoints)
        {
            UiLayoutModel.ResetForTesting();     // 톱니를 기본 위치(우상단)에서 시작한다.
            if (gearCenterPoints.HasValue) UiLayoutModel.SetGearCenter(gearCenterPoints.Value);
            CharacterSaveStore.Save();

            ReservedEdgeProbe.ResetForTests();   // 상단 프로브도 함께 걷힌다.
            ReservedEdgeProbe.SetInsetsForTests(edges);

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _widget = Object.FindFirstObjectByType<TodoPostItWidget>();
            Assert.IsNotNull(_widget, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다.");

            var gears = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, gears.Length, $"{LogPrefix} 씬의 InfoGearIconWidget 개수가 {gears.Length}개입니다.");
            _gear = gears[0];

            TodoListModel.ResetForTesting();
            for (int i = 0; i < count; i++) TodoListModel.Add($"예약 띠 확인용 {i + 1}", SoftCap);
            yield return null;
            yield return null;

            Assert.IsTrue(_widget.IsCardVisible,
                $"{LogPrefix} 할 일이 {count}건인데 카드가 보이지 않습니다 — 관측 전제가 성립하지 않습니다.");

            // ★ 주입한 값이 실제로 프로브를 통과하는지 확인한다. 여기가 조용히 0/미측정이면 아래 모든
            //   초록이 "띠가 없어서 아무 일도 없었다"와 구분되지 않는다(부재 단언의 조용한 초록 — CLAUDE.md).
            ReservedEdgeInsets live = ReservedEdgeProbe.Insets(null);
            Assert.AreEqual(edges.MeasuredEdges, live.MeasuredEdges,
                $"{LogPrefix} ★ 전제 실패 — 주입한 측정 마스크({edges.MeasuredEdges})가 프로브를 " +
                $"통과하지 못했습니다(실측 {live.MeasuredEdges}). " +
                "「측정된 0」과 「미측정 0」은 값이 같고 마스크만 다르므로, 마스크가 새면 두 세계가 " +
                "구분되지 않은 채 초록이 됩니다.");
            Assert.AreEqual(edges.ToString(), live.ToString(),
                $"{LogPrefix} ★ 전제 실패 — 주입한 네 변({edges})이 프로브를 통과하지 못했습니다(실측 {live}).");

            // 상단 프로브에도 같은 상단 값이 심겼는가(ReservedEdgeProbe.SetInsetsForTests의 계약).
            // 이것이 어긋나면 물리적으로 존재할 수 없는 화면에서 검증하게 된다.
            Assert.AreEqual(edges.TopPoints, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                $"{LogPrefix} ★ 전제 실패 — 상단 프로브가 {ReservedTopBarProbe.TopInsetPoints(null):F3}pt로 " +
                $"네 방향 묶음의 상단({edges.TopPoints}pt)과 갈라졌습니다.");

            AssertPointConversionAgreesWithWidget();
        }

        /// <summary>톱니를 카드 아래(화면 하단)에 세워 둘 좌표 — 창 좌상단 원점 pt.
        /// <para>가로는 <b>화면 한가운데</b>다: 기본 자리(우상단)에 두면 카드와 세로로 겹쳐 카드를 밀고,
        /// 왼쪽 끝에 두면 이번엔 <b>원변 발산 구간</b>에 들어간다. 세로만 내려 두면 두 경우 다 피한다.</para></summary>
        private static Vector2 GearParkedCenterPoints()
            => new Vector2(ToPoints(Screen.width) * 0.5f, ToPoints(Screen.height) - GearParkedBottomOffsetPoints);

        /// <summary>세워 둔 톱니가 정말로 <b>카드 아래</b>에 있는가 — 짐작하지 않고 실측한 사각형으로 확인한다.
        /// <para><b>세로 분리</b>를 본다. 프로덕션의 회피 판정이 "가로도 겹치고 <b>세로도</b> 겹칠 때"만
        /// 카드를 미는데, 세로가 갈라져 있으면 가로가 어떻든 밀지 않기 때문이다. 사각형 겹침
        /// (<c>Rect.Overlaps</c>)으로만 보면 <b>가로가 우연히 갈라진 덕</b>에 통과할 수 있고, 그러면
        /// 화면 폭이 조금 달라지는 날 전제가 조용히 무너진다.</para>
        /// <para>게임 뷰가 너무 작아 이 전제가 깨지면 <b>조용히 건너뛰지 않고 빨갛게</b> 실패한다
        /// (건너뜀은 러너에서 사실상 사라진다 — CLAUDE.md).</para></summary>
        private void AssertGearIsParkedAwayFromCard()
        {
            Rect card = ScreenRectOf(Panel());
            Rect gear = _gear.IconScreenRect;
            float cardBottom = TopDownPoints(card.yMin);      // 화면 위 끝 기준(아래로 자란다).
            float gearTop = TopDownPoints(gear.yMax);
            Assert.Greater(gearTop, cardBottom,
                $"{LogPrefix} ★ 전제 실패 — 세워 둔 톱니 상단이 {gearTop:F2}pt로 카드 하단 " +
                $"{cardBottom:F2}pt보다 위에 있습니다(세로가 겹칩니다). 게임 뷰 높이 " +
                $"{ToPoints(Screen.height):F0}pt가 '카드 높이 + 톱니 지름 + " +
                $"{GearParkedBottomOffsetPoints}pt'보다 작습니다. 이 상태에서는 카드의 가로 좌표가 " +
                "예약 띠 때문인지 톱니 회피 때문인지 갈리지 않습니다 — 더 큰 게임 뷰가 필요합니다.");
            Assert.IsFalse(card.Overlaps(gear),
                $"{LogPrefix} ★ 전제 실패 — 세로는 갈라졌는데 사각형({gear})이 카드({card})와 겹칩니다.");
        }

        /// <summary>화면 오른쪽 끝에서 카드 <b>우변</b>까지의 거리(pt) — 실제 렌더 사각형에서 잰다.
        /// <see cref="TodoPostItWidget.RightInsetPointsForTests"/>(앵커 좌표)와 <b>다른 자</b>다.</summary>
        private float CardRightInsetFromRect() => ToPoints(Screen.width - ScreenRectOf(Panel()).xMax);

        /// <summary>톱니 히트 사각형의 <b>좌변</b>이 화면 오른쪽 끝에서 얼마나 떨어져 있는가(pt) —
        /// 프로덕션 <c>ResolveRightInsetPoints</c>의 <c>gearNear</c>와 같은 계다.</summary>
        private float GearNearPoints() => ToPoints(Screen.width - _gear.IconScreenRect.xMin);

        /// <summary>톱니 히트 사각형의 <b>우변</b>이 화면 오른쪽 끝에서 얼마나 떨어져 있는가(pt) — <c>gearFar</c>.</summary>
        private float GearFarPoints() => ToPoints(Screen.width - _gear.IconScreenRect.xMax);

        /// <summary>이 파일은 픽셀↔포인트 환산을 <c>config = null</c>로 한다(다른 투두 픽스처와 같은 관례).
        /// 위젯은 <c>_agentConfig</c>로 한다. 둘이 갈라지는 유일한 경우는 사람이 <c>desktopDpiScale</c>을
        /// 0이 아닌 값으로 덮어쓴 때인데, 그러면 <b>이 파일의 산수가 조용히 틀린다</b>. 그래서 갈라지면
        /// 재지 않고 즉시 멈춘다 — 조용히 틀리게 두지 않는다.</summary>
        private void AssertPointConversionAgreesWithWidget()
        {
            var scaler = _widget.GetComponentInChildren<CanvasScaler>(true);
            Assert.IsNotNull(scaler, $"{LogPrefix} 포스트잇 캔버스의 CanvasScaler를 찾지 못했습니다.");
            Assert.AreEqual(scaler.scaleFactor, ScreenCoordinateConverter.ResolveCanvasScaleFactor(null), 0.0001f,
                $"{LogPrefix} ★ 전제 실패 — 위젯 캔버스 배율({scaler.scaleFactor})과 이 테스트의 환산 배율이 " +
                "다릅니다(StickConfig.desktopDpiScale 수동 오버라이드). 이 상태로는 pt 비교가 틀립니다.");
        }

        private static Rect ScreenRectOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);   // Overlay 캔버스에서는 월드 좌표가 곧 스크린 픽셀이다.
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static RectTransform FindByName(TodoPostItWidget widget, string name)
        {
            foreach (RectTransform rt in widget.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name == name) return rt;
            }
            return null;
        }

        private RectTransform Panel()
        {
            RectTransform panel = FindByName(_widget, "PostItPanel");
            Assert.IsNotNull(panel, $"{LogPrefix} PostItPanel을 찾지 못했습니다.");
            return panel;
        }

        private static float ToPoints(float unityScreenPixels)
            => ScreenCoordinateConverter.UnityScreenToCanvas(unityScreenPixels, null);

        private static float ToPixels(float points)
            => ScreenCoordinateConverter.CanvasToUnityScreen(points, null);

        /// <summary>Unity 스크린 y(아래에서 위) -> 화면 위 끝 기준 pt(위에서 아래).</summary>
        private static float TopDownPoints(float unityScreenY) => ToPoints(Screen.height - unityScreenY);

        /// <summary>지금 가로로 밀려난 양(픽셀) — 처방 전 자리로 되돌리는 평행이동량.</summary>
        private float PushedPixels()
            => ToPixels(_widget.RightInsetPointsForTests - TodoPostItWidget.PanelInsetPoints);

        // ==================================================================
        // ① 예약 띠 0 — 좌표가 변경 전과 <b>한 픽셀도</b> 다르지 않다 (회귀 없음의 증거)
        // ==================================================================

        /// <summary>
        /// ★ 이 처방의 안전성은 "잘 골랐다"가 아니라 <b>구조</b>에서 온다:
        /// 새 세로 위치는 <c>예약 띠 + PanelInsetPoints</c>이고, 띠가 0이면 그 식이 옛 상수와
        /// <b>같은 값이 아니라 같은 식</b>이 된다. 띠 없는 환경(메뉴 막대 자동 숨김 / 작업표시줄
        /// 하단·좌·우 도킹)은 실제로 흔하다.
        ///
        /// <para><b>양성 대조</b>: 같은 측정이 띠 33pt에서는 <b>실제로 움직인다</b>는 것을 같은
        /// 테스트 안에서 보인다. 안 그러면 "동일하다"가 <b>측정이 죽었다</b>와 구분되지 않는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 예약_띠가_0이면_카드_세로_좌표가_변경_전과_동일하다()
        {
            yield return ShowCardWith(3, NoReservedBarPoints);

            float topPoints = _widget.TopInsetPointsForTests;
            Rect card = ScreenRectOf(Panel());
            float cardTopPointsFromRect = TopDownPoints(card.yMax);

            Debug.Log($"{LogPrefix} 띠 0pt — 세로 인셋 {topPoints:F3}pt " +
                      $"(기준 상수 {TodoPostItWidget.PanelInsetPoints}pt) / 카드 사각형 {card}.");

            // 본 검증 — 앵커 좌표(캔버스 유닛 = pt, 환산이 개입하지 않는다).
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints, topPoints, 0.001f,
                $"{LogPrefix} 예약 띠가 0인데 세로 인셋이 {topPoints:F3}pt입니다 — " +
                $"변경 전({TodoPostItWidget.PanelInsetPoints}pt)과 달라졌습니다. 이 라운드는 띠가 있는 " +
                "환경만 고쳐야 하고, 띠가 없는 사용자에게는 아무 일도 일어나지 않아야 합니다.");

            // 같은 사실을 다른 자(실제 렌더 사각형)로 한 번 더 잰다 — 앵커만 맞고 그림은 다른 경우 방지.
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints, cardTopPointsFromRect, 0.5f,
                $"{LogPrefix} 앵커는 {topPoints:F3}pt인데 실제 사각형 상단은 {cardTopPointsFromRect:F2}pt입니다.");

            // ★ 양성 대조 — 같은 측정이 띠 33pt에서는 움직인다.
            yield return ShowCardWith(3, MenuBarPoints);
            float movedTop = _widget.TopInsetPointsForTests;
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints + MenuBarPoints, movedTop, 0.001f,
                $"{LogPrefix} ★ 양성 대조 실패 — 띠를 {MenuBarPoints}pt로 줬는데 세로 인셋이 " +
                $"{movedTop:F3}pt입니다. 측정이 죽어 있으면 위의 '동일하다'도 아무것도 잠그지 못합니다.");
        }

        // ==================================================================
        // ② 카드가 상단 예약 띠를 덮지 않는다 (33 / 40)
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 카드가_macOS_메뉴_막대를_덮지_않는다()
        {
            yield return AssertCardClearsReservedBar(MenuBarPoints, "macOS 메뉴 막대");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 카드가_Windows_상단_도킹_작업표시줄을_덮지_않는다()
        {
            yield return AssertCardClearsReservedBar(TopDockedTaskbarPoints, "Windows 상단 도킹 작업표시줄");
        }

        /// <summary>(가) 카드가 띠를 안 덮는다. <b>양성 대조</b>는 "옛 자리(16pt)였다면 실제로 덮었다"이다.</summary>
        private IEnumerator AssertCardClearsReservedBar(float reservedPoints, string what)
        {
            yield return ShowCardWith(3, reservedPoints);

            Rect card = ScreenRectOf(Panel());
            float cardTop = TopDownPoints(card.yMax);

            // ★ 양성 대조 — 처방 전 세로 자리(PanelInsetPoints)는 이 띠를 실제로 덮었다.
            float coveredBefore = reservedPoints - TodoPostItWidget.PanelInsetPoints;
            Assert.Greater(coveredBefore, 0f,
                $"{LogPrefix} ★ 양성 대조 실패 — {what} {reservedPoints}pt가 카드 여백 " +
                $"{TodoPostItWidget.PanelInsetPoints}pt보다 얇아 애초에 덮을 것이 없습니다. " +
                "덮지 않던 것을 밀어낸 것이라면 이 검사는 아무것도 잠그지 않습니다.");
            Debug.Log($"{LogPrefix} {what} {reservedPoints}pt — 처방 전 겹침 {coveredBefore:F2}pt " +
                      $"({coveredBefore / reservedPoints * 100f:F1}%) / 지금 카드 상단 {cardTop:F2}pt.");

            // 본 검증 (가) — 카드 상단이 띠 아래에 있고, 여백까지 정확히 지킨다.
            Assert.GreaterOrEqual(cardTop, reservedPoints - 0.5f,
                $"{LogPrefix} 카드 상단이 {cardTop:F2}pt로 {what}(0~{reservedPoints}pt) 안에 있습니다 — " +
                $"{reservedPoints - cardTop:F2}pt를 덮습니다(절대 불변 원칙 2).");
            Assert.AreEqual(reservedPoints + TodoPostItWidget.PanelInsetPoints,
                _widget.TopInsetPointsForTests, 0.001f,
                $"{LogPrefix} 세로 인셋이 '띠 + 여백'({reservedPoints} + " +
                $"{TodoPostItWidget.PanelInsetPoints})과 다릅니다 — 정책 경로를 지나지 않았습니다.");

            // 본 검증 (나) — 기본 톱니 위치에서 톱니도 덮지 않는다.
            Assert.IsFalse(card.Overlaps(_gear.IconScreenRect),
                $"{LogPrefix} 카드({card})가 톱니({_gear.IconScreenRect})를 덮습니다.");
        }

        // ==================================================================
        // ③ ★ 짝 변경 — 발산 구간에서도 카드가 톱니를 덮지 않는다
        // ==================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 발산_구간에서도_카드가_톱니를_덮지_않는다_메뉴_막대()
        {
            yield return AssertNoDivergenceGap(MenuBarPoints, "macOS 메뉴 막대");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 발산_구간에서도_카드가_톱니를_덮지_않는다_상단_도킹()
        {
            yield return AssertNoDivergenceGap(TopDockedTaskbarPoints, "Windows 상단 도킹 작업표시줄");
        }

        /// <summary>
        /// <b>배치와 회피 판정이 같은 한 곳에서 값을 뽑는가</b>를 잠근다.
        ///
        /// <para>구간을 <b>손으로 적지 않는다</b> — 라이브 카드 높이와 라이브 톱니 히트 반지름에서
        /// 그때그때 다시 만든다. 그래야 행 수·톱니 크기가 바뀌어도 이 검사가 따라온다.</para>
        ///
        /// <para><b>양성 대조 2겹</b>: ① 옛 판정식(여기서 독립 재구현)이 이 자리에서 "안 겹친다"고
        /// 말한다 = 옛 코드였다면 카드를 <b>밀지 않았다</b>. ② 안 밀린 자리(기본 인셋)는 톱니와
        /// <b>실제로 겹친다</b>. 둘이 다 참일 때에만 아래 초록이 뜻을 가진다.</para>
        /// </summary>
        private IEnumerator AssertNoDivergenceGap(float reservedPoints, string what)
        {
            yield return ShowCardWith(1, reservedPoints);   // 1행이면 충분하다 — 최소 구성에서도 생긴다.

            Rect panelRect = ScreenRectOf(Panel());
            float panelHeight = ToPoints(panelRect.height);
            float gearRadius = ToPoints(_gear.IconScreenRect.height) * 0.5f;
            float screenHeightPoints = ToPoints(Screen.height);

            // 발산 구간 = [옛 판정이 '안 겹친다'로 넘어가는 톱니 중심 y, 실제로 겹치는 상한)
            //   아래끝은 <b>옛 상수</b>에서, 위끝은 <b>실측 배치</b>에서 나온다. 두 출처가 다르므로
            //   폭이 띠 두께와 같은지가 <b>산수의 항등식이 아니라 측정</b>이 된다 — 배치가 안 내려갔으면
            //   폭이 0이 되어 바로 아래에서 빨갛게 걸린다.
            float liveCardTop = _widget.TopInsetPointsForTests;
            float bandLow = TodoPostItWidget.PanelInsetPoints + panelHeight + gearRadius;
            float bandHigh = liveCardTop + panelHeight + gearRadius;
            float target = (bandLow + bandHigh) * 0.5f;

            Debug.Log($"{LogPrefix} {what} {reservedPoints}pt / 카드 높이 {panelHeight:F2}pt / " +
                      $"톱니 반지름 {gearRadius:F2}pt / 실측 카드 상단 {liveCardTop:F2}pt -> " +
                      $"발산 구간 [{bandLow:F2}, {bandHigh:F2}) 폭 {bandHigh - bandLow:F2}pt, 표적 y {target:F2}pt.");

            Assert.AreEqual(reservedPoints, bandHigh - bandLow, 0.01f,
                $"{LogPrefix} ★ 전제 실패 — 발산 구간 폭이 {bandHigh - bandLow:F2}pt로 띠 두께 " +
                $"{reservedPoints}pt와 다릅니다. 폭이 0이면 배치가 띠 아래로 내려가지 않은 것이고, " +
                "그러면 이 검사가 겨눌 구간 자체가 없습니다(이 결함의 서명은 '폭 == 띠 두께'다).");

            // ★ <b>환경 가드</b>(닫힐 프로덕션 갭이 아니다). 되살리는 장치가 <b>둘</b> 있고 둘 다
            //   이 메서드 안에 있다 — 아무도 스위치를 켜 줄 필요가 없다:
            //   (1) 바로 위의 '폭 == 띠 두께' 단언은 <b>이 가드보다 먼저, 조건 없이</b> 돈다. 즉 화면이
            //       작아 아래 드래그 재현을 못 해도 <b>짝 변경 회귀는 여전히 이 메서드가 빨갛게 잡는다</b>.
            //       이 Ignore가 건너뛰는 것은 "드래그로 실제 자리에 놓아 보는 것" 하나뿐이다.
            //   (2) 게임 뷰가 커지면 조건이 거짓이 되어 <b>Ignore를 지나 실검사로</b> 간다.
            //       필요 높이는 아래 메시지가 그때그때 계산해 찍는다(상수로 굳히지 않는다).
            //   ※ 이 저장소의 배치모드 PlayMode 게임 뷰는 640×480이고(docs/verify/runs 실측 로그의
            //     "화면 폭=640pt"), 필요 높이는 1행 카드에서 156pt 남짓이라 실제로는 (2)가 늘 참이다.
            //     ★ 다만 이 픽스처는 아직 배치모드에서 한 번도 돌지 않았다 — 위 문장은 <b>계산</b>이다.
            if (target + gearRadius >= screenHeightPoints)
            {
                Assert.Ignore($"{LogPrefix} 화면 높이 {screenHeightPoints:F0}pt가 작아 표적 " +
                              $"y {target:F2}pt를 재현할 수 없습니다 — 게임 뷰 높이가 " +
                              $"{target + gearRadius:F0}pt를 넘으면 이 Ignore는 저절로 사라지고 " +
                              "아래 실검사로 갑니다(그때까지도 바로 위의 '폭 == 띠 두께' 단언은 계속 돕니다).");
            }

            // 톱니를 발산 구간 한가운데로 끌어다 놓는다(실제 드래그 경로 — 시간 AND 거리, 41-8 3겹).
            Vector2 start = _gear.IconScreenCenter;
            var goal = new Vector2(start.x, Screen.height - ToPixels(target));
            _gear.FeedPointerForTests(true, start);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);
            _gear.FeedPointerForTests(true, goal);
            Assert.IsTrue(_gear.IsDraggingIcon, $"{LogPrefix} 준비 조건 실패 — 톱니를 끌지 못했습니다.");
            _gear.FeedPointerForTests(false, goal);
            yield return null;
            yield return null;

            Rect gear = _gear.IconScreenRect;
            float gearCenterY = TopDownPoints(gear.center.y);
            Assert.AreEqual(target, gearCenterY, 1.0f,
                $"{LogPrefix} 준비 조건 실패 — 톱니가 표적 {target:F2}pt가 아니라 {gearCenterY:F2}pt에 있습니다.");

            float gearTop = TopDownPoints(gear.yMax);
            float gearBottom = TopDownPoints(gear.yMin);

            // ★ 양성 대조 ① — <b>옛 판정식의 독립 재구현</b>. 프로덕션 함수를 부르지 않는다
            //   (부르면 기대값과 대상이 함께 틀어져 아무것도 못 잰다 — docs/TEAM.md).
            bool oldVerdictOverlapY =
                gearTop < TodoPostItWidget.PanelInsetPoints + panelHeight &&
                gearBottom > TodoPostItWidget.PanelInsetPoints;
            Assert.IsFalse(oldVerdictOverlapY,
                $"{LogPrefix} ★ 양성 대조 실패 — 옛 판정식(상단을 " +
                $"{TodoPostItWidget.PanelInsetPoints}pt로 고정)이 이 자리에서 '겹친다'고 말합니다. " +
                "그러면 옛 코드도 카드를 밀었을 것이고, 이 테스트는 발산 구간을 겨누지 못한 것입니다.");

            // ★ 양성 대조 ② — 안 밀린 자리(기본 가로 인셋)는 톱니와 실제로 겹친다.
            Rect card = ScreenRectOf(Panel());
            Rect cardAtBaseInset = new Rect(card.x + PushedPixels(), card.y, card.width, card.height);
            Assert.IsTrue(cardAtBaseInset.Overlaps(gear),
                $"{LogPrefix} ★ 양성 대조 실패 — 기본 인셋 자리({cardAtBaseInset})가 톱니({gear})와 " +
                "겹치지 않습니다. 겹치지 않는 것을 밀어낸 것이라면 아래 초록은 아무것도 잠그지 않습니다.");

            // 본 검증 — 배치와 판정이 같은 곳에서 나왔다면 카드는 여기서도 비켜 있다.
            Assert.Greater(_widget.RightInsetPointsForTests, TodoPostItWidget.PanelInsetPoints + 0.5f,
                $"{LogPrefix} 발산 구간(톱니 중심 y {gearCenterY:F2}pt)인데 카드가 한 픽셀도 밀리지 " +
                $"않았습니다(가로 인셋 {_widget.RightInsetPointsForTests:F2}pt) — 회피 판정이 " +
                "여전히 옛 상수로 카드 상단을 잡고 있습니다(짝 변경 누락).");
            Assert.IsFalse(card.Overlaps(gear),
                $"{LogPrefix} 발산 구간에서 카드({card})가 톱니({gear})를 덮습니다 — 51-9-3 재발입니다.");

            float gapPoints = ToPoints(gear.xMin - card.xMax);
            Assert.GreaterOrEqual(gapPoints, GearRadialMenuWidget.ScreenMarginPoints - 0.5f,
                $"{LogPrefix} 카드 우변과 톱니 좌변의 여유가 {gapPoints:F2}pt로 " +
                $"설계값 {GearRadialMenuWidget.ScreenMarginPoints}pt에 못 미칩니다.");
        }

        // ==================================================================
        // ④ ★ 가로축 — 작업표시줄/Dock <b>좌·우 도킹</b> (2026-09-03 승격)
        // ==================================================================
        //
        // 이 절은 <b>2026-09-03 오전까지 Assert.Ignore 한 줄</b>이었다. 사유는
        // "IReservedTopBarService에 대응하는 측면 사실 조회 계약이 없어 이 파일에서는 고칠 수 없다"였고,
        // 그날 dev-platform이 그 계약(Platform/IReservedScreenEdgeService.cs · ReservedEdgeProbe.cs ·
        // SurfaceSafeAreaPolicy 가로축)을 착지시키면서 <b>사유가 낡았다</b>. 그래서 실단언으로 승격한다.
        //
        // ★ 짐작 금지가 여기서도 그대로다: 「측정된 0」과 「미측정 0」은 값이 같고 마스크만 다른데,
        //   미측정을 0으로 읽고 배치하면 ReservedEdgeProbe의 "실패는 0이다 / 짐작값으로 메우지 않는다"를
        //   우회하는 것이 된다. 그래서 <b>미측정 케이스를 별도 검사로</b> 잰다.

        /// <summary>
        /// ★★ <b>우측 도킹 작업표시줄을 카드가 덮지 않는다</b> — 그리고 <b>띠가 0이면 한 픽셀도 안 바뀐다</b>.
        ///
        /// <para>세로축의 <c>예약_띠가_0이면_카드_세로_좌표가_변경_전과_동일하다</c> +
        /// <c>카드가_..._덮지_않는다</c> 두 검사를 가로축에서 <b>한 메서드</b>로 대칭시킨 것이다.
        /// 세 값(0 / 48 / 62)을 같은 실행 안에서 재기 때문에 "측정이 죽어서 안 움직인 것"과
        /// "띠가 0이라 안 움직인 것"이 구분된다.</para>
        ///
        /// <para><b>왜 톱니를 아래로 세워 두는가</b>: 기본 자리의 톱니는 카드와 세로로 겹쳐서 카드를
        /// 왼쪽으로 민다(51-9-3 회피). 그 상태의 가로 좌표는 <b>띠와 톱니가 섞인 값</b>이라
        /// 이 검사가 겨누는 축을 흐린다. 톱니가 섞인 쪽은 아래
        /// <see cref="우측_띠_발산_구간에서_배치와_판정이_갈라지지_않는다"/>가 따로 잠근다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator 작업표시줄_좌우_도킹은_가로축에_같은_문제를_남기지_않는다()
        {
            // ---------- (나) 측면 띠 0 — 변경 전과 <b>비트 동일</b> ----------
            yield return ShowCardWithEdges(3,
                ReservedEdgeInsets.Observed(NoReservedBarPoints, NoReservedBarPoints,
                                            NoReservedBarPoints, NoReservedBarPoints),
                GearParkedCenterPoints());
            AssertGearIsParkedAwayFromCard();

            float zeroInset = _widget.RightInsetPointsForTests;
            float zeroInsetFromRect = CardRightInsetFromRect();
            Debug.Log($"{LogPrefix} 측면 띠 0pt(측정됨) — 가로 인셋 {zeroInset:R}pt " +
                      $"(기준 상수 {TodoPostItWidget.PanelInsetPoints:R}pt) / 사각형에서 다시 잰 값 " +
                      $"{zeroInsetFromRect:F3}pt / 차이 {Mathf.Abs(zeroInset - TodoPostItWidget.PanelInsetPoints):E3}.");

            // 허용 오차 <b>0</b>. 이 경로는 정책이 W − ((W − 16 − 110) + 110)을 계산하는데 화면 폭이
            // 2^24보다 훨씬 작은 정수/반정수라 중간값이 전부 정확히 표현된다 — 근사가 아니라 항등이다.
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints, zeroInset, 0f,
                $"{LogPrefix} 측면 예약 띠가 0인데 가로 인셋이 {zeroInset:R}pt입니다 — " +
                $"변경 전({TodoPostItWidget.PanelInsetPoints:R}pt)과 달라졌습니다. 이 라운드는 띠가 있는 " +
                "환경만 고쳐야 하고, 띠가 없는 사용자에게는 아무 일도 일어나지 않아야 합니다.");

            // 같은 사실을 다른 자(실제 렌더 사각형)로 한 번 더 — 앵커만 맞고 그림은 다른 경우 방지.
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints, zeroInsetFromRect, 0.5f,
                $"{LogPrefix} 앵커는 {zeroInset:F3}pt인데 실제 사각형 우변은 {zeroInsetFromRect:F2}pt입니다.");

            // ---------- (가) 48 / 62 — 카드가 띠를 덮지 않는다 ----------
            yield return AssertCardClearsRightReservedBar(RightDockedTaskbarNarrowPoints, zeroInset);
            yield return AssertCardClearsRightReservedBar(RightDockedTaskbarWidePoints, zeroInset);
        }

        /// <summary>(가) 카드가 우측 띠를 안 덮는다. <b>양성 대조</b>는 "띠 0일 때의 <b>실측</b> 자리
        /// (<paramref name="zeroInset"/>)가 이 띠 안에 있었다" — 즉 처방 전에는 실제로 덮었다.
        /// 상수를 다시 적지 않고 <b>같은 실행에서 잰 값</b>을 쓴다.</summary>
        private IEnumerator AssertCardClearsRightReservedBar(float reservedPoints, float zeroInset)
        {
            yield return ShowCardWithEdges(3,
                ReservedEdgeInsets.Observed(NoReservedBarPoints, NoReservedBarPoints,
                                            NoReservedBarPoints, reservedPoints),
                GearParkedCenterPoints());
            AssertGearIsParkedAwayFromCard();

            // ★ 양성 대조 — 처방 전 자리(띠 0에서 실측한 zeroInset)는 이 띠를 실제로 덮었다.
            float coveredBefore = reservedPoints - zeroInset;
            Assert.Greater(coveredBefore, 0f,
                $"{LogPrefix} ★ 양성 대조 실패 — 우측 띠 {reservedPoints}pt가 처방 전 카드 우변 " +
                $"{zeroInset:F2}pt보다 얇아 애초에 덮을 것이 없습니다. 덮지 않던 것을 밀어낸 것이라면 " +
                "이 검사는 아무것도 잠그지 않습니다.");

            float inset = _widget.RightInsetPointsForTests;
            float insetFromRect = CardRightInsetFromRect();
            Debug.Log($"{LogPrefix} 우측 도킹 {reservedPoints}pt — 처방 전 겹침 {coveredBefore:F2}pt " +
                      $"({coveredBefore / reservedPoints * 100f:F1}%) / 지금 가로 인셋 {inset:F3}pt " +
                      $"(사각형 {insetFromRect:F2}pt) / 띠 바깥 여유 {inset - reservedPoints:F2}pt.");

            // 본 검증 ① — 카드 우변이 띠 밖에 있다(실제 렌더 사각형으로 잰다).
            Assert.GreaterOrEqual(insetFromRect, reservedPoints - 0.5f,
                $"{LogPrefix} 카드 우변이 화면 오른쪽 끝에서 {insetFromRect:F2}pt로 우측 도킹 " +
                $"작업표시줄(0~{reservedPoints}pt) 안에 있습니다 — {reservedPoints - insetFromRect:F2}pt를 " +
                "덮습니다(절대 불변 원칙 2).");

            // 본 검증 ② — 값이 '띠 + 여백'과 정확히 같다(정책 경로를 지났다는 증거).
            Assert.AreEqual(reservedPoints + TodoPostItWidget.PanelInsetPoints, inset, 0.001f,
                $"{LogPrefix} 가로 인셋이 '띠 + 여백'({reservedPoints} + " +
                $"{TodoPostItWidget.PanelInsetPoints})과 다릅니다 — 정책 경로를 지나지 않았습니다.");
        }

        /// <summary>
        /// ★★ <b>못 쟀으면 아무것도 바꾸지 않는다</b> — 짐작 금지의 테스트.
        ///
        /// <para><see cref="ReservedEdgeInsets.MeasuredEdges"/>에 좌·우 비트가 없을 때 좌표가
        /// <b>요청값 그대로</b>인지 잰다. 「측정된 0」과 값이 같아서 위 검사만으로는 갈리지 않는다 —
        /// 이 검사가 없으면 "화면 폭에서 빼서 48pt쯤 밀어 두자"는 구현이 <b>위 검사를 전부 통과하면서</b>
        /// 들어올 수 있다(그 구현은 미측정에서도 밀어 버린다).</para>
        ///
        /// <para><b>양성 대조</b>: 같은 측정이 우측 띠 62pt에서는 실제로 움직인다는 것을 같은 테스트
        /// 안에서 보인다. 안 그러면 "요청값 그대로"가 <b>측정이 죽었다</b>와 구분되지 않는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator 측면_예약_띠를_못_쟀으면_카드_가로_좌표는_요청값_그대로다()
        {
            yield return ShowCardWithEdges(3, ReservedEdgeInsets.Unknown, GearParkedCenterPoints());
            AssertGearIsParkedAwayFromCard();

            // 전제 — 정말로 '미측정'인가. (ShowCardWithEdges가 마스크 일치를 이미 확인하지만,
            //        이 검사만은 마스크가 이 검사의 <b>주제</b>이므로 여기서 다시 못 박는다.)
            ReservedEdgeInsets live = ReservedEdgeProbe.Insets(null);
            Assert.IsFalse(live.IsMeasured(ReservedEdge.Right),
                $"{LogPrefix} ★ 전제 실패 — 우변이 '측정됨'으로 들어왔습니다({live}). " +
                "이 검사는 <b>못 쟀을 때</b>를 겨눕니다.");
            Assert.IsFalse(live.IsMeasured(ReservedEdge.Left),
                $"{LogPrefix} ★ 전제 실패 — 좌변이 '측정됨'으로 들어왔습니다({live}).");

            float inset = _widget.RightInsetPointsForTests;
            Debug.Log($"{LogPrefix} 측면 띠 미측정({live}) — 가로 인셋 {inset:R}pt " +
                      $"(요청값 {TodoPostItWidget.PanelInsetPoints:R}pt) / " +
                      $"차이 {Mathf.Abs(inset - TodoPostItWidget.PanelInsetPoints):E3}.");

            // 허용 오차 <b>0</b> — 이 경로는 정책을 아예 부르지 않고 요청값을 그대로 돌려준다.
            Assert.AreEqual(TodoPostItWidget.PanelInsetPoints, inset, 0f,
                $"{LogPrefix} 측면 띠를 <b>못 쟀는데</b> 가로 인셋이 {inset:R}pt입니다 — " +
                $"요청값({TodoPostItWidget.PanelInsetPoints:R}pt)과 다릅니다. 미측정을 0으로 읽어 배치했거나 " +
                "화면 폭에서 짐작해 메운 것입니다. 둘 다 ReservedEdgeProbe의 '실패는 0이다 / 짐작값으로 " +
                "메우지 않는다' 규약 위반입니다.");

            // ★ 양성 대조 — 같은 측정이 '측정된 62pt'에서는 움직인다.
            yield return ShowCardWithEdges(3,
                ReservedEdgeInsets.Observed(NoReservedBarPoints, NoReservedBarPoints,
                                            NoReservedBarPoints, RightDockedTaskbarWidePoints),
                GearParkedCenterPoints());
            float movedInset = _widget.RightInsetPointsForTests;
            Assert.AreEqual(RightDockedTaskbarWidePoints + TodoPostItWidget.PanelInsetPoints, movedInset, 0.001f,
                $"{LogPrefix} ★ 양성 대조 실패 — 우측 띠를 {RightDockedTaskbarWidePoints}pt로 <b>측정</b>해 " +
                $"줬는데 가로 인셋이 {movedInset:F3}pt입니다. 측정이 죽어 있으면 위의 '요청값 그대로'도 " +
                "아무것도 잠그지 못합니다.");
        }

        /// <summary>
        /// ★★ <b>짝 변경</b> — 가로축에서도 배치와 톱니 회피 판정이 <b>같은 한 곳</b>에서 값을 뽑는가.
        ///
        /// ============================================================================
        /// 가로축 발산 구간 — 서명(폭 == 띠 두께)은 세로와 같은데 <b>둘</b>이고 <b>하나는 흡수된다</b>
        /// ============================================================================
        /// 배치만 기준선(띠 + 여백)으로 옮기고 판정을 옛 상수에 남겨 두면 구간이 둘 생긴다.
        /// (우측 띠 R, 기준선 B = R + 여백, 카드 폭 W, 톱니 여유 g. 좌표는 화면 오른쪽 끝 기준 pt.)
        /// <code>
        ///   ① 근변  gearNear ∈ (여백, B]   폭 = R   최종 클램프의 <b>하한이 기준선</b>이라 대부분 흡수된다.
        ///                                          남는 차이는 (B−g, B] 뿐이고 결과는 "필요보다 최대 g만큼
        ///                                          더 왼쪽" = <b>낭비이지 침해가 아니다</b>.
        ///   ② 원변  gearFar  ∈ [여백+W, B+W)  폭 = R   흡수되지 않는다. 옛 판정이 "안 겹친다"고 말해
        ///                                          카드가 기준선에 남고 <b>카드가 톱니를 덮는다</b>.
        /// </code>
        /// <b>이 검사는 ②를 겨눈다</b> — ①은 겨눠도 아무것도 잡지 못한다(클램프가 삼킨다).
        /// 폭이 <b>정확히 띠 두께와 같다</b>는 세로축의 서명이 여기서도 그대로 나온다.
        ///
        /// <para><b>구간을 손으로 적지 않는다</b> — 1단계에서 라이브 카드 폭·톱니 반지름·기준선을 재고,
        /// 2단계에서 그 값으로 만든 자리에 톱니를 놓는다. 드래그가 아니라 <b>저장된 톱니 위치</b>
        /// (<c>UiLayoutModel.SetGearCenter</c> -> 씬 로드)로 놓으므로 시간에 의존하는 준비 절차가 0줄이다.
        /// 두 단계가 <b>같은 띠 값</b>을 쓰므로 기준선은 두 번 다 같고, 2단계가 실제로 표적에 놓였는지는
        /// 아래에서 <b>실측 사각형으로</b> 다시 확인한다.</para>
        ///
        /// <para><b>양성 대조 2겹</b>: ① 옛 판정식(여기서 독립 재구현 — 프로덕션 함수를 부르지 않는다)이
        /// 이 자리에서 "안 겹친다"고 말한다 = 옛 코드였다면 카드를 <b>밀지 않았다</b>.
        /// ② 안 밀린 자리(기준선)는 톱니와 <b>실제로 겹친다</b>. 둘이 다 참일 때에만 아래 초록이 뜻을 가진다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator 우측_띠_발산_구간에서_배치와_판정이_갈라지지_않는다()
        {
            const float band = RightDockedTaskbarWidePoints;   // 62

            // ---------- 1단계 — 기준선·카드 폭·톱니 반지름을 라이브에서 잰다 ----------
            // 톱니를 아래로 세워 두어 이 자리의 가로 인셋이 <b>순수한 기준선</b>이 되게 한다.
            yield return ShowCardWithEdges(1,
                ReservedEdgeInsets.Observed(NoReservedBarPoints, NoReservedBarPoints,
                                            NoReservedBarPoints, band),
                GearParkedCenterPoints());
            AssertGearIsParkedAwayFromCard();

            float baseInset = _widget.RightInsetPointsForTests;
            float panelWidth = ToPoints(ScreenRectOf(Panel()).width);
            float gearRadius = ToPoints(_gear.IconScreenRect.width) * 0.5f;
            float screenWidthPoints = ToPoints(Screen.width);

            // 원변 발산 구간 = [옛 판정이 '안 겹친다'로 넘어가는 톱니 우변, 실제로 겹치는 상한)
            //   아래끝은 <b>옛 상수</b>에서, 위끝은 <b>실측 기준선</b>에서 나온다. 출처가 다르므로
            //   폭이 띠 두께와 같은지가 산수의 항등식이 아니라 <b>측정</b>이 된다.
            float gapLow = TodoPostItWidget.PanelInsetPoints + panelWidth;
            float gapHigh = baseInset + panelWidth;
            float targetFar = (gapLow + gapHigh) * 0.5f;

            Debug.Log($"{LogPrefix} 우측 띠 {band}pt / 기준선 {baseInset:F2}pt / 카드 폭 {panelWidth:F2}pt / " +
                      $"톱니 반지름 {gearRadius:F2}pt -> 원변 발산 구간 [{gapLow:F2}, {gapHigh:F2}) " +
                      $"폭 {gapHigh - gapLow:F2}pt, 표적 gearFar {targetFar:F2}pt.");

            Assert.AreEqual(band, gapHigh - gapLow, 0.01f,
                $"{LogPrefix} ★ 전제 실패 — 원변 발산 구간 폭이 {gapHigh - gapLow:F2}pt로 띠 두께 " +
                $"{band}pt와 다릅니다. 폭이 0이면 배치가 띠 바깥으로 밀리지 않은 것이고, 그러면 이 검사가 " +
                "겨눌 구간 자체가 없습니다(이 결함의 서명은 '폭 == 띠 두께'다).");

            // 전제 — 밀려난 카드가 화면 안에 들어가는가. 안 들어가면 왼쪽 한계가 먼저 걸려
            //        "안 밀렸다"와 "밀렸는데 화면이 좁았다"가 같은 빨강으로 보인다. 미리 갈라 둔다.
            //        (건너뛰지 않는다 — 건너뜀은 러너에서 사실상 사라진다.)
            float needWidth = targetFar + gearRadius * 2f
                              + GearRadialMenuWidget.ScreenMarginPoints + panelWidth;
            Assert.GreaterOrEqual(screenWidthPoints, needWidth,
                $"{LogPrefix} ★ 전제 실패 — 게임 뷰 폭 {screenWidthPoints:F0}pt로는 밀려난 카드가 " +
                $"화면에 못 들어갑니다(필요 {needWidth:F0}pt = 표적 {targetFar:F2} + 톱니 지름 " +
                $"{gearRadius * 2f:F2} + 여유 {GearRadialMenuWidget.ScreenMarginPoints} + 카드 폭 " +
                $"{panelWidth:F2}). 더 넓은 게임 뷰가 필요합니다.");

            // ---------- 2단계 — 톱니를 그 구간 한가운데에 놓고 다시 띄운다 ----------
            // 세로는 카드의 세로 범위 안이어야 회피 판정이 성립한다 — 아래에서 실측으로 확인한다.
            var gearCenter = new Vector2(screenWidthPoints - (targetFar + gearRadius),
                                         GearDivergenceCenterYPoints);
            yield return ShowCardWithEdges(1,
                ReservedEdgeInsets.Observed(NoReservedBarPoints, NoReservedBarPoints,
                                            NoReservedBarPoints, band),
                gearCenter);

            Rect card = ScreenRectOf(Panel());
            Rect gear = _gear.IconScreenRect;
            float panelHeight = ToPoints(card.height);
            float gearFar = GearFarPoints();
            float gearNear = GearNearPoints();
            float gearTop = TopDownPoints(gear.yMax);
            float gearBottom = TopDownPoints(gear.yMin);
            float panelTop = _widget.TopInsetPointsForTests;
            float inset = _widget.RightInsetPointsForTests;

            Debug.Log($"{LogPrefix} 2단계 — 톱니 gearFar {gearFar:F2}pt(표적 {targetFar:F2}) " +
                      $"gearNear {gearNear:F2}pt / 카드 세로 [{panelTop:F2}, {panelTop + panelHeight:F2}]pt / " +
                      $"톱니 세로 [{gearTop:F2}, {gearBottom:F2}]pt -> 가로 인셋 {inset:F2}pt " +
                      $"(기준선 {baseInset:F2}pt).");

            // 전제 ① — 톱니가 정말 그 구간 안에 있는가.
            Assert.AreEqual(targetFar, gearFar, 1.0f,
                $"{LogPrefix} ★ 전제 실패 — 톱니 우변이 표적 {targetFar:F2}pt가 아니라 {gearFar:F2}pt에 " +
                "있습니다(화면 밖으로 클램프됐을 수 있습니다 — 더 넓은 게임 뷰가 필요합니다).");

            // 전제 ② — 세로는 겹친다(가로 판정만 갈리는 상황을 만들어야 한다).
            Assert.IsTrue(gearTop < panelTop + panelHeight && gearBottom > panelTop,
                $"{LogPrefix} ★ 전제 실패 — 톱니 세로 [{gearTop:F2}, {gearBottom:F2}]가 카드 세로 " +
                $"[{panelTop:F2}, {panelTop + panelHeight:F2}] 밖입니다. 세로가 안 겹치면 회피 판정은 " +
                "가로와 무관하게 '안 민다'가 되어 이 검사가 아무것도 잠그지 못합니다.");

            // ★ 양성 대조 ① — <b>옛 판정식의 독립 재구현</b>(프로덕션 함수를 부르지 않는다 — docs/TEAM.md:
            //   기대값을 프로덕션 함수로 만들면 그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다).
            bool oldVerdictOverlapX =
                gearFar < TodoPostItWidget.PanelInsetPoints + panelWidth &&
                gearNear > TodoPostItWidget.PanelInsetPoints;
            Assert.IsFalse(oldVerdictOverlapX,
                $"{LogPrefix} ★ 양성 대조 실패 — 옛 판정식(기준선을 " +
                $"{TodoPostItWidget.PanelInsetPoints}pt로 고정)이 이 자리에서 '겹친다'고 말합니다. " +
                "그러면 옛 코드도 카드를 밀었을 것이고, 이 테스트는 발산 구간을 겨누지 못한 것입니다.");

            // ★ 양성 대조 ② — 안 밀린 자리(기준선)는 톱니와 실제로 겹친다.
            Rect cardAtBaseInset = new Rect(card.x + ToPixels(inset - baseInset), card.y,
                                            card.width, card.height);
            Assert.IsTrue(cardAtBaseInset.Overlaps(gear),
                $"{LogPrefix} ★ 양성 대조 실패 — 기준선 자리({cardAtBaseInset})가 톱니({gear})와 " +
                "겹치지 않습니다. 겹치지 않는 것을 밀어낸 것이라면 아래 초록은 아무것도 잠그지 않습니다.");

            // 본 검증 — 배치와 판정이 같은 곳에서 나왔다면 카드는 여기서 실제로 밀린다.
            Assert.Greater(inset, baseInset + 0.5f,
                $"{LogPrefix} 원변 발산 구간(톱니 우변 {gearFar:F2}pt)인데 카드가 한 픽셀도 밀리지 " +
                $"않았습니다(가로 인셋 {inset:F2}pt = 기준선 {baseInset:F2}pt) — 회피 판정이 여전히 " +
                $"옛 상수 {TodoPostItWidget.PanelInsetPoints}pt로 카드 우변을 잡고 있습니다(짝 변경 누락).");
            Assert.IsFalse(card.Overlaps(gear),
                $"{LogPrefix} 발산 구간에서 카드({card})가 톱니({gear})를 덮습니다 — 51-9-3 재발입니다.");

            float gapPoints = ToPoints(gear.xMin - card.xMax);
            Assert.GreaterOrEqual(gapPoints, GearRadialMenuWidget.ScreenMarginPoints - 0.5f,
                $"{LogPrefix} 카드 우변과 톱니 좌변의 여유가 {gapPoints:F2}pt로 " +
                $"설계값 {GearRadialMenuWidget.ScreenMarginPoints}pt에 못 미칩니다.");

            // 그리고 우측 띠도 여전히 안 덮는다(밀리는 방향이 띠 반대쪽이므로 당연하지만, 못 박아 둔다).
            Assert.GreaterOrEqual(CardRightInsetFromRect(), band - 0.5f,
                $"{LogPrefix} 카드({card})가 우측 띠(0~{band}pt)를 덮습니다.");
        }
    }
}
