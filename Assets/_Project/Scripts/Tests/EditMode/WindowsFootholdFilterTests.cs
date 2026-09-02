using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Platform;
using StickMate.Platform.Windows;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 이월 결함 2건 회귀 잠금(2026-08-31, coder).
    ///
    /// ============================================================================
    /// 결함 1 (Major, 이월) — Windows에 macOS 알파 필터의 대응물이 없었다
    /// ============================================================================
    /// macOS <c>MacWindowService</c>는 <c>kCGWindowAlpha &lt; 0.05</c>인 창을 발판 후보에서 뺀다.
    /// <c>Win32WindowService</c>에는 그것이 없었다(<c>WS_EX_LAYERED</c>/<c>GetLayeredWindowAttributes</c>
    /// 전수 검색 0건). 그런데 같은 날 도입된 가려짐 필터는 <b>"발판이 되는 자격"과 "가릴 자격"을 같게</b>
    /// 취급한다(<see cref="VisibleTopEdgeSolver"/>). 그래서 눈에 보이지도 않는 <b>전체화면 투명 창</b>
    /// (스트리밍/접근성/보안 툴의 HUD) 하나가 z-order 앞에 있으면 그 아래 <b>멀쩡한 발판이 전부
    /// 사라져</b> 캐릭터가 영원히 낙하한다. 가려짐 수정 <b>전에는 없던</b> 위험이다.
    ///
    /// 이 파일이 잠그는 것:
    ///  W1   레이어드가 아닌 평범한 창은 항상 불투명 = 발판/가림 자격 유지(과잉 제거 방지).
    ///  W2   LWA_ALPHA로 거의 투명하게 설정된 창은 후보에서 빠진다.
    ///  W3   LWA_ALPHA=255(완전 불투명)인 레이어드 창은 <b>빠지지 않는다</b>.
    ///  W4   컬러키(LWA_COLORKEY)만 쓰는 창은 전체 알파가 불투명이므로 빠지지 않는다.
    ///  W5   픽셀별 알파(UpdateLayeredWindow → 조회 실패) + 클릭관통 = 순수 HUD → 빠진다.
    ///  W6   픽셀별 알파인데 클릭관통이 아니면 <b>빠지지 않는다</b>(조회 실패로 멀쩡한 창을 지우지 않는다).
    ///  W7   문턱 경계값(0.05)이 macOS와 정확히 같다.
    ///  W8   너무 작은 창 / 모든 모니터 밖 창 — macOS 필터와 1:1.
    ///  W9   ★핵심★ 전체화면 투명 창이 있어도 그 아래 발판이 살아남는다(솔버 결합 실측).
    ///  W9n  (네거티브) 같은 배치에서 <b>수정 전처럼</b> 그 창을 솔버에 넣으면 발판이 전멸한다
    ///       = W9가 항상 참인 단언이 아니라 진짜 결함을 잡고 있다는 증거.
    ///
    /// ============================================================================
    /// ★★ 2026-09-02 — <c>WS_EX_TRANSPARENT</c> 무조건 배제 승격(리더 승인)
    /// ============================================================================
    /// 승격 전 <see cref="WindowsFootholdFilter.ResolveWindowAlpha"/>는 클릭 관통 비트를
    /// <b>네 번째 갈래(레이어드 + 조회 실패) 안에서만</b> 봤다. 그래서 <b>클릭 관통인데도 발판이
    /// 되는 조합이 셋</b> 남아 있었고, 이 파일에는 그 셋을 겨냥한 케이스가 <b>0건</b>이었다
    /// (W5·W9는 <c>LAYERED | TRANSPARENT</c> + 조회 실패 조합만 쓴다). 네 갈래 표를 그대로 채운다:
    ///  W10  <c>TRANSPARENT</c> 단독(레이어드 아님) — 승격 전 1.0이었다.
    ///  W11  <c>LAYERED|TRANSPARENT</c> + <c>LWA_ALPHA</c>=255 — 승격 전 1.0이었다.
    ///  W12  <c>LAYERED|TRANSPARENT</c> + 컬러키만 — 승격 전 1.0이었다.
    ///  W13  ★양성 대조★ 위 셋이 <b>승격 전 규칙에서는 실제로 발판이 되고 남의 발판까지 지웠다</b>는
    ///       것을 독립 재현으로 먼저 보이고, 그 다음 승격 후 그러지 못하는 것을 보인다.
    ///       (독립 재현은 프로덕션 함수를 부르지 않는다 — 부르면 대조가 스스로 무너진다.)
    ///  W14  ★과잉 제거 방지★ 클릭 관통이 <b>아닌</b> 창의 네 갈래는 한 칸도 바뀌지 않았다.
    ///
    /// ============================================================================
    /// 결함 2 (Minor, 이월) — DWMWA_EXTENDED_FRAME_BOUNDS 미적용
    /// ============================================================================
    /// <c>GetWindowRect</c>는 Windows 10/11에서 DWM의 <b>보이지 않는 리사이즈 테두리</b>(좌/우/하 약
    /// 7px)를 포함한다. 그래서 발판이 눈에 보이는 창보다 넓고, 창 좌표를 겨냥하는 연출(인질극 닫기버튼
    /// 조준 / 로프 앵커)이 일제히 ~7px 어긋난다.
    ///  D1   ~7px 오차가 <b>실제로 결과를 바꾼다</b>는 실측(왜 이 Minor를 고칠 가치가 있는가의 근거).
    ///  D2~D5 소스 정적 감사 — P/Invoke 시그니처/상수/폴백/호출부가 실제로 그렇게 되어 있는지.
    ///        (Win32 P/Invoke는 <c>#if UNITY_STANDALONE_WIN</c> 안이라 macOS EditMode에서 실행할 수
    ///         없다. 이 프로젝트가 이미 쓰는 소스 스캔 방식 —
    ///         <c>UserAssetImmutabilityAuditTests</c> — 을 그대로 재사용한다.)
    /// </summary>
    public sealed class WindowsFootholdFilterTests
    {
        private const string LogPrefix = "[윈도우발판필터-TEST]";

        /// <summary>Win32WindowService/MacWindowService가 쓰는 것과 같은 값.</summary>
        private const float MinVisibleWidth = 24f;

        private static readonly Rect NoVirtualScreen = default;

        // ============================================================================
        // 결함 1 — 알파 판정 (순수 함수. OS 호출 0건이라 macOS 개발 환경에서 그대로 실측된다)
        // ============================================================================

        [Test]
        public void W1_레이어드가_아닌_평범한_창은_항상_불투명이다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(0, false, 0u, 0);
            Assert.AreEqual(1f, alpha, 0.0001f,
                $"{LogPrefix} WS_EX_LAYERED가 없는 창에는 '전체 알파'라는 개념 자체가 없다. " +
                "여기서 0이 나오면 데스크톱의 거의 모든 창이 발판에서 사라진다.");
        }

        [Test]
        public void W2_LWA_ALPHA로_거의_투명하게_설정된_창은_후보에서_빠진다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered, true, WindowsFootholdFilter.LwaAlpha, 5);

            Assert.Less(alpha, WindowsFootholdFilter.MinWindowAlpha, $"{LogPrefix} 알파 5/255는 문턱 미만이어야 한다.");
            Assert.AreEqual(WindowsFootholdRejection.TransparentAlpha,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(0f, 0f, 1920f, 1080f), alpha, false, NoVirtualScreen),
                $"{LogPrefix} macOS의 kCGWindowAlpha<0.05 필터와 같은 판정이 나와야 한다.");
        }

        [Test]
        public void W3_완전히_불투명한_레이어드_창은_빠지지_않는다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered, true, WindowsFootholdFilter.LwaAlpha, 255);

            Assert.AreEqual(1f, alpha, 0.0001f);
            Assert.AreEqual(WindowsFootholdRejection.None,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(0f, 0f, 800f, 600f), alpha, false, NoVirtualScreen),
                $"{LogPrefix} 레이어드라는 이유만으로 창을 지우면 과잉 제거다(WS_EX_LAYERED는 " +
                "요즘 애니메이션/둥근모서리 용도로도 널리 쓰인다).");
        }

        /// <summary>
        /// ★ 2026-09-02 — <b>이 테스트를 지우지 마라.</b> 같은 날 <c>WS_EX_TRANSPARENT</c>가 무조건
        /// 탈락으로 승격되면서 <see cref="W12_클릭관통이면_컬러키만_쓰는_레이어드_창도_빠진다"/>가
        /// "컬러키 창인데 0"을 단언하게 됐다. 두 테스트를 나란히 보면 <b>모순처럼 보이지만 모순이
        /// 아니다</b> — 갈리는 축은 컬러키가 아니라 <b>클릭 관통 비트</b>다:
        /// <list type="bullet">
        ///  <item>클릭 관통 <b>없는</b> 컬러키 창 = 사용자가 실제로 클릭해 쓰는 창 → 불투명(이 테스트).</item>
        ///  <item>클릭 관통 <b>있는</b> 컬러키 창 = 만질 수 없는 오버레이 → 0(W12).</item>
        /// </list>
        /// 이 테스트가 잠그는 원래 함정(<c>LWA_ALPHA</c> 비트가 없을 때 <c>pbAlpha</c>를 그대로 읽어
        /// 컬러키 창을 통째로 지우는 것)은 승격과 <b>무관하게 그대로 살아 있다</b>. "일관성"을 이유로
        /// 이 단언을 W12에 맞춰 바꾸면 그 함정이 조용히 되살아난다.
        /// </summary>
        [Test]
        public void W4_컬러키만_쓰는_레이어드_창은_전체_알파가_불투명이다()
        {
            const uint LwaColorKey = 0x00000001;
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered, true, LwaColorKey, 0);

            Assert.AreEqual(1f, alpha, 0.0001f,
                $"{LogPrefix} LWA_ALPHA 비트가 없으면 pbAlpha는 의미 없는 값이다. " +
                "그걸 그대로 알파로 읽으면(0) 컬러키 창이 통째로 사라진다 — 흔한 함정.");
        }

        /// <summary>
        /// ★ 2026-09-02 주석 — 이 조합의 <b>답</b>은 승격 전후가 같지만 <b>답이 나오는 자리</b>가 바뀌었다.
        /// 승격 전에는 네 번째 갈래(조회 실패 + 클릭 관통)가 0을 냈고, 지금은 그보다 앞선 무조건
        /// 게이트가 낸다. 그래서 이 테스트는 더 이상 "네 번째 갈래가 살아 있는가"를 재지 못한다 —
        /// 그 역할은 <see cref="W14_클릭관통이_아니면_네_갈래_판정이_한_칸도_바뀌지_않았다"/>가 맡는다.
        /// 여기 남기는 이유는 이 조합이 <b>이월 Major가 지목한 실제 시나리오</b>이기 때문이다.
        /// </summary>
        [Test]
        public void W5_픽셀별_알파에_클릭관통까지_켜진_창은_순수_HUD로_보고_뺀다()
        {
            // UpdateLayeredWindow를 쓰는 창은 GetLayeredWindowAttributes가 실패한다(문서화된 동작).
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent,
                layeredAttributesQuerySucceeded: false, layeredFlags: 0u, layeredAlpha: 0);

            Assert.AreEqual(0f, alpha, 0.0001f,
                $"{LogPrefix} 레이어드 + 클릭관통 = 사용자가 만질 수조차 없는 오버레이. " +
                "이월 Major가 지목한 '전체화면 투명 창'이 정확히 이 조합이며, 우리 앱 자신의 " +
                "오버레이도 이 조합이다.");
        }

        [Test]
        public void W6_픽셀별_알파여도_클릭관통이_아니면_지우지_않는다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered,
                layeredAttributesQuerySucceeded: false, layeredFlags: 0u, layeredAlpha: 0);

            Assert.AreEqual(1f, alpha, 0.0001f,
                $"{LogPrefix} 조회 실패를 이유로 멀쩡한 창을 발판에서 지우지 않는다 " +
                "(IsCloaked와 같은 보수 원칙). 여기서 0을 돌려주면 픽셀별 알파를 쓰는 평범한 앱 창이 " +
                "전부 발판에서 사라진다 = 이 수정이 원래 버그보다 나쁜 버그가 된다.");
        }

        [Test]
        public void W7_알파_문턱은_macOS와_정확히_같은_005다()
        {
            Assert.AreEqual(0.05f, WindowsFootholdFilter.MinWindowAlpha, 0.00001f,
                $"{LogPrefix} 두 플랫폼의 문턱이 갈리면 '한쪽에서만 재현되는' 발판 버그가 다시 태어난다.");

            var big = new Rect(0f, 0f, 1920f, 1080f);
            Assert.AreEqual(WindowsFootholdRejection.TransparentAlpha,
                WindowsFootholdFilter.ClassifyGeometry(big, 0.049f, false, NoVirtualScreen));
            Assert.AreEqual(WindowsFootholdRejection.None,
                WindowsFootholdFilter.ClassifyGeometry(big, 0.051f, false, NoVirtualScreen));
        }

        [Test]
        public void W8_너무_작은_창과_모든_모니터_밖_창은_macOS와_같은_기준으로_빠진다()
        {
            Assert.AreEqual(60f, WindowsFootholdFilter.MinWindowWidth, 0.0001f);
            Assert.AreEqual(40f, WindowsFootholdFilter.MinWindowHeight, 0.0001f);

            Assert.AreEqual(WindowsFootholdRejection.TooSmall,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(10f, 10f, 59f, 400f), 1f, false, NoVirtualScreen));
            Assert.AreEqual(WindowsFootholdRejection.TooSmall,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(10f, 10f, 400f, 39f), 1f, false, NoVirtualScreen));

            var virtualScreen = new Rect(0f, 0f, 1920f, 1080f);
            Assert.AreEqual(WindowsFootholdRejection.OffVirtualScreen,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(-32000f, -32000f, 800f, 600f), 1f, true, virtualScreen),
                $"{LogPrefix} 최소화 유령 좌표(-32000,-32000)가 IsIconic을 빠져나가도 여기서 걸린다.");

            // 보조 모니터(가상 화면 안, 주 모니터 밖)는 절대 빠지면 안 된다 — 멀티모니터 발판 유실 방지.
            var multiMonitorVirtual = new Rect(-1920f, 0f, 3840f, 1080f);
            Assert.AreEqual(WindowsFootholdRejection.None,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(-1500f, 200f, 800f, 600f), 1f, true, multiMonitorVirtual),
                $"{LogPrefix} 가상 화면은 '주 모니터'가 아니라 '모든 모니터의 외접 사각형'이어야 한다.");

            // 조회 실패(hasVirtualScreen=false)면 이 검사 자체를 건너뛴다.
            Assert.AreEqual(WindowsFootholdRejection.None,
                WindowsFootholdFilter.ClassifyGeometry(new Rect(-32000f, -32000f, 800f, 600f), 1f, false, NoVirtualScreen));
        }

        // ============================================================================
        // W9 — ★핵심★ 필터 + 솔버 결합 실측 (이 결함이 실제로 일으키는 결과를 그대로 재현)
        //
        //   전체화면 투명 HUD(z0): 화면 전체를 덮는다. 사용자 눈에는 아무것도 없다.
        //   실제 앱 창(z1):        상단선 y=500, x 100~900. 사용자가 지금 보고 있는 창.
        // ============================================================================
        private static readonly Rect TransparentFullscreenHud = new Rect(0f, 0f, 1920f, 1080f);
        private static readonly Rect RealAppWindow = new Rect(100f, 500f, 800f, 400f);

        [Test]
        public void W9_전체화면_투명창이_있어도_그_아래_발판은_살아남는다()
        {
            // 필터가 HUD를 먼저 걸러낸다 = 솔버에 아예 들어가지 않는다.
            float hudAlpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent, false, 0u, 0);
            Assert.AreEqual(WindowsFootholdRejection.TransparentAlpha,
                WindowsFootholdFilter.ClassifyGeometry(TransparentFullscreenHud, hudAlpha, false, NoVirtualScreen),
                $"{LogPrefix} HUD가 후보 단계에서 걸러지지 않으면 아래 W9 단언은 의미가 없다.");

            var solver = new VisibleTopEdgeSolver();
            solver.Begin();
            solver.AddWindow(RealAppWindow);   // 필터를 통과한 창만 들어간다
            solver.Solve(MinVisibleWidth, false, default);

            Assert.AreEqual(1, solver.SegmentCount,
                $"{LogPrefix} 눈에 보이지도 않는 HUD 때문에 실제 창의 발판이 사라지면 캐릭터가 영원히 낙하한다.");
            Assert.AreEqual(RealAppWindow.width, solver.GetVisibleWidth(0), 0.001f);
        }

        [Test]
        public void W9n_네거티브_투명창을_솔버에_넣으면_아래_발판이_전멸한다()
        {
            var solver = new VisibleTopEdgeSolver();
            solver.Begin();
            solver.AddWindow(TransparentFullscreenHud); // 수정 전 동작: 알파를 모르니 그냥 넣는다
            solver.AddWindow(RealAppWindow);
            solver.Solve(MinVisibleWidth, false, default);

            Assert.AreEqual(0f, solver.GetVisibleWidth(1), 0.001f,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 투명창을 넣어도 발판이 살아남는다면 W9는 " +
                "항상 참인 공허한 단언이고, 알파 필터는 아무 결함도 막고 있지 않다는 뜻이다.");

            for (int s = 0; s < solver.SegmentCount; s++)
            {
                Assert.AreNotEqual(1, solver.GetSegmentWindowIndex(s),
                    $"{LogPrefix} 네거티브 배치에서는 실제 창이 조각을 하나도 내지 못해야 한다.");
            }
        }

        // ============================================================================
        // ★★ 2026-09-02 — WS_EX_TRANSPARENT 무조건 배제 승격 (리더 승인)
        //
        // 승격 전 네 갈래 표에서 클릭 관통 비트는 **4번 갈래 안에서만** 읽혔다. 아래 세 조합은
        // 클릭 관통인데도 알파 1.0을 받아 (a) 발판이 되고 (b) 남의 발판을 지울 자격까지 가졌다.
        // 이 파일에는 그 셋을 겨냥한 케이스가 승격 전까지 **0건**이었다.
        // ============================================================================

        /// <summary>LWA_COLORKEY — dwFlags에 이 비트만 있으면 pbAlpha는 의미 없는 값이다.</summary>
        private const uint LwaColorKeyOnly = 0x00000001;

        /// <summary>
        /// ★ <b>승격 전(2026-09-01까지) 규칙의 독립 재현.</b> 프로덕션 함수를 부르지 않는다 —
        /// 부르면 승격이 기대값에도 그대로 반영되어 <b>대조가 스스로 무너진다</b>
        /// (TEAM.md: "기대값을 프로덕션 함수로 만들지 마라. 그 함수가 틀어지면 기대값도 함께 틀어져
        /// 아무것도 못 잰다"). 상수는 프로덕션 것을 참조한다 — 베끼면 안 되는 것은 <b>로직</b>이지
        /// Win32 ABI 상수가 아니고, 오히려 상수를 베끼면 값이 바뀐 날 이 재현만 낡는다.
        /// </summary>
        private static float LegacyResolveWindowAlpha(int exStyle, bool queryOk, uint flags, byte alphaByte)
        {
            if ((exStyle & WindowsFootholdFilter.WsExLayered) == 0) return 1f;
            if (queryOk) return (flags & WindowsFootholdFilter.LwaAlpha) != 0 ? alphaByte / 255f : 1f;
            return (exStyle & WindowsFootholdFilter.WsExTransparent) != 0 ? 0f : 1f;
        }

        /// <summary>승격이 답을 <b>바꾸는</b> 정확히 그 세 조합. 이름이 실패 메시지에 그대로 실린다.</summary>
        private static readonly (string Name, int ExStyle, bool QueryOk, uint Flags, byte Alpha)[]
            ClickThroughCombos =
        {
            ("TRANSPARENT 단독(레이어드 아님)",
                WindowsFootholdFilter.WsExTransparent,
                false, 0u, (byte)0),
            ("LAYERED|TRANSPARENT + LWA_ALPHA=255(완전 불투명)",
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent,
                true, WindowsFootholdFilter.LwaAlpha, (byte)255),
            ("LAYERED|TRANSPARENT + 컬러키만",
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent,
                true, LwaColorKeyOnly, (byte)0),
        };

        /// <summary>
        /// 승격이 답을 <b>바꾸면 안 되는</b> 조합 전수(네 갈래 표에서 클릭 관통 비트가 꺼진 칸 전부).
        /// <para>기대 알파는 Win32가 정의한 의미(pbAlpha 0~255가 알파 0~1)에서 직접 온다. 이것은
        /// 프로덕션 상수가 아니라 OS ABI라 여기 적어도 "기준과 대상이 같이 움직이는" 문제가 없다.</para>
        /// </summary>
        private static readonly (string Name, int ExStyle, bool QueryOk, uint Flags, byte Alpha, float Expected)[]
            NonClickThroughCombos =
        {
            ("1번 갈래 — 레이어드 아님",
                0, false, 0u, (byte)0, 1f),
            ("2번 갈래 — LWA_ALPHA=255",
                WindowsFootholdFilter.WsExLayered, true, WindowsFootholdFilter.LwaAlpha, (byte)255, 1f),
            ("2번 갈래 — LWA_ALPHA=5(거의 투명)",
                WindowsFootholdFilter.WsExLayered, true, WindowsFootholdFilter.LwaAlpha, (byte)5, 5f / 255f),
            ("3번 갈래 — 컬러키만",
                WindowsFootholdFilter.WsExLayered, true, LwaColorKeyOnly, (byte)0, 1f),
            ("4번 갈래 — 픽셀별 알파(조회 실패), 클릭관통 아님",
                WindowsFootholdFilter.WsExLayered, false, 0u, (byte)0, 1f),
        };

        [Test]
        public void W10_클릭관통_단독창은_레이어드가_아니어도_후보에서_빠진다()
        {
            // WS_EX_TRANSPARENT의 값 자체를 못 박는다 — 이 상수가 틀리면 아래 모든 단언이 엉뚱한
            // 비트를 검사하면서 초록으로 통과한다(값 출처: winuser.h).
            Assert.AreEqual(0x00000020, WindowsFootholdFilter.WsExTransparent,
                $"{LogPrefix} WS_EX_TRANSPARENT는 0x20이다. 값이 틀리면 이 필터는 아무 창도 못 잡는다.");

            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExTransparent,
                layeredAttributesQuerySucceeded: false, layeredFlags: 0u, layeredAlpha: 0);

            Assert.AreEqual(0f, alpha, 0.0001f,
                $"{LogPrefix} WS_EX_LAYERED가 없어도 WS_EX_TRANSPARENT 단독이면 클릭이 그대로 " +
                "통과한다 = 사용자가 이 창을 만질 수 없다. 만질 수 없는 창은 사용자가 쓰는 창이 " +
                "아니므로 발판도 아니고 남의 발판을 지울 자격도 없다.");

            Assert.AreEqual(WindowsFootholdRejection.TransparentAlpha,
                WindowsFootholdFilter.ClassifyGeometry(TransparentFullscreenHud, alpha, false, NoVirtualScreen),
                $"{LogPrefix} 알파 판정이 0이어도 기하 분류가 그것을 탈락으로 옮기지 못하면 " +
                "열거 경로에서는 아무 일도 일어나지 않는다.");
        }

        [Test]
        public void W11_클릭관통이면_LWA_ALPHA가_255여도_빠진다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent,
                layeredAttributesQuerySucceeded: true,
                layeredFlags: WindowsFootholdFilter.LwaAlpha, layeredAlpha: 255);

            Assert.AreEqual(0f, alpha, 0.0001f,
                $"{LogPrefix} 승격 게이트가 2번 갈래보다 **앞**에 있어야 한다. 뒤로 내려가면 " +
                "이 조합이 알파 1.0을 받아 다시 발판이 된다 — 순서가 곧 판정이다.");
        }

        [Test]
        public void W12_클릭관통이면_컬러키만_쓰는_레이어드_창도_빠진다()
        {
            float alpha = WindowsFootholdFilter.ResolveWindowAlpha(
                WindowsFootholdFilter.WsExLayered | WindowsFootholdFilter.WsExTransparent,
                layeredAttributesQuerySucceeded: true, layeredFlags: LwaColorKeyOnly, layeredAlpha: 0);

            Assert.AreEqual(0f, alpha, 0.0001f,
                $"{LogPrefix} W4와 모순처럼 보이지만 갈리는 축은 컬러키가 아니라 클릭 관통이다 " +
                "(W4 문서 참고). 클릭 관통 컬러키 창은 화면 특정 색만 비치는 오버레이이고 " +
                "사용자가 만질 수 없다.");
        }

        [Test]
        public void W13_양성대조_승격_전_규칙에서는_그_세_조합이_전부_발판이_됐다()
        {
            // ---- (가) 교정 먼저. 독립 재현이 '승격을 뺀 나머지'에서 프로덕션과 같은 답을 내는가? ----
            //      이 교정이 깨지면 아래 (나)(다)는 "그냥 서로 다른 함수 둘"을 비교한 것일 뿐이라
            //      아무것도 증명하지 못한다(TEAM.md 공통 처방: 알려진 값으로 먼저 교정한다).
            foreach ((string name, int ex, bool ok, uint flags, byte a, float _) in NonClickThroughCombos)
            {
                Assert.AreEqual(
                    WindowsFootholdFilter.ResolveWindowAlpha(ex, ok, flags, a),
                    LegacyResolveWindowAlpha(ex, ok, flags, a), 0.0001f,
                    $"{LogPrefix} 교정 실패 [{name}] — 승격 전 규칙의 독립 재현이 클릭 관통과 " +
                    "무관한 칸에서 프로덕션과 다른 답을 낸다. 재현이 틀렸다는 뜻이므로 " +
                    "이 테스트의 대조 결과를 전부 폐기해야 한다.");
            }

            // ---- (나) 양성 대조 — 승격 전에는 세 조합이 전부 '불투명 = 채택'이었다 ----
            foreach ((string name, int ex, bool ok, uint flags, byte a) in ClickThroughCombos)
            {
                float legacy = LegacyResolveWindowAlpha(ex, ok, flags, a);
                Assert.AreEqual(1f, legacy, 0.0001f,
                    $"{LogPrefix} 양성 대조 실패 [{name}] — 승격 전 규칙에서도 이 조합이 이미 " +
                    "탈락했다면 이번 승격은 아무것도 닫지 않은 것이고, 아래 '승격 후 0' 단언은 " +
                    "항상 참인 공허한 단언이다.");
                Assert.AreEqual(WindowsFootholdRejection.None,
                    WindowsFootholdFilter.ClassifyGeometry(TransparentFullscreenHud, legacy, false, NoVirtualScreen),
                    $"{LogPrefix} 양성 대조 실패 [{name}] — 승격 전 이 창이 실제로 후보를 " +
                    "'통과'했다는 것까지 보여야 대조가 성립한다.");

                float now = WindowsFootholdFilter.ResolveWindowAlpha(ex, ok, flags, a);
                Assert.AreEqual(0f, now, 0.0001f,
                    $"{LogPrefix} 승격 후에도 [{name}]이 불투명으로 남았다.");
                Assert.AreEqual(WindowsFootholdRejection.TransparentAlpha,
                    WindowsFootholdFilter.ClassifyGeometry(TransparentFullscreenHud, now, false, NoVirtualScreen),
                    $"{LogPrefix} [{name}]의 알파는 0인데 기하 분류가 채택으로 남긴다.");
            }

            // ---- (다) 결과 실측 — 그 통과가 실제로 무엇을 일으켰는가(솔버 결합) ----
            var before = new VisibleTopEdgeSolver();
            before.Begin();
            before.AddWindow(TransparentFullscreenHud);   // 승격 전: 후보를 통과했으므로 솔버에 들어간다
            before.AddWindow(RealAppWindow);
            before.Solve(MinVisibleWidth, false, default);
            Assert.AreEqual(0f, before.GetVisibleWidth(1), 0.001f,
                $"{LogPrefix} 양성 대조 실패 — 클릭 관통 전체화면 창을 솔버에 넣어도 아래 발판이 " +
                "살아남는다면, 이 승격이 막고 있다는 사고가 애초에 일어나지 않는다는 뜻이다.");

            var after = new VisibleTopEdgeSolver();
            after.Begin();
            after.AddWindow(RealAppWindow);               // 승격 후: HUD는 필터에서 이미 탈락해 안 들어온다
            after.Solve(MinVisibleWidth, false, default);
            Assert.AreEqual(1, after.SegmentCount,
                $"{LogPrefix} 승격 후에는 실제 앱 창의 발판이 남아야 한다.");
            Assert.AreEqual(RealAppWindow.width, after.GetVisibleWidth(0), 0.001f,
                $"{LogPrefix} 승격 후 발판 폭이 창 폭과 같아야 한다(가리는 창이 없으므로).");
        }

        [Test]
        public void W14_클릭관통이_아니면_네_갈래_판정이_한_칸도_바뀌지_않았다()
        {
            // ★ 과잉 제거 방지. 승격이 클릭 관통 축에서만 작동하고 나머지 축을 건드리지 않았음을
            //   전수로 잠근다. 여기서 하나라도 0이 되면 데스크톱의 멀쩡한 창들이 발판에서 사라진다
            //   = 이 수정이 원래 버그보다 나쁜 버그가 된다(W6이 경고하는 바로 그 형태).
            Assert.AreEqual(5, NonClickThroughCombos.Length,
                $"{LogPrefix} 네 갈래 표의 칸이 줄었다 — 빈 목록을 도는 foreach는 초록인 채로 " +
                "아무것도 재지 않는다(거짓 통과 #5).");

            foreach ((string name, int ex, bool ok, uint flags, byte a, float expected) in NonClickThroughCombos)
            {
                Assert.AreEqual(0, ex & WindowsFootholdFilter.WsExTransparent,
                    $"{LogPrefix} [{name}]에 클릭 관통 비트가 섞여 있다 — 이 표는 '관통이 아닌' " +
                    "축만 담아야 대조가 성립한다.");

                float alpha = WindowsFootholdFilter.ResolveWindowAlpha(ex, ok, flags, a);
                Assert.AreEqual(expected, alpha, 0.0001f,
                    $"{LogPrefix} [{name}]의 알파가 승격 때문에 바뀌었다. 승격은 클릭 관통 창만 " +
                    "닫아야 한다.");
            }
        }

        // ============================================================================
        // 결함 2 — DWMWA_EXTENDED_FRAME_BOUNDS
        // ============================================================================

        [Test]
        public void D1_보이지_않는_7px_테두리는_가려짐_판정을_실제로_바꾼다()
        {
            // 앞 창의 "진짜" 오른쪽 끝이 x=500. GetWindowRect는 여기에 보이지 않는 테두리 7px을 더해
            // x=507까지 있다고 보고한다. 뒤 창의 보이는 상단선은 그 차이만큼 짧아진다.
            const float InvisibleBorder = 7f;
            var frontVisual = new Rect(100f, 100f, 400f, 600f);              // x 100~500
            var frontAsReportedByGetWindowRect = new Rect(100f - InvisibleBorder, 100f,
                400f + InvisibleBorder * 2f, 600f + InvisibleBorder);        // x 93~507
            var back = new Rect(200f, 300f, 700f, 400f);                     // 상단선 y=300, x 200~900

            float visualWidth = SolveVisibleWidthOfSecondWindow(frontVisual, back);
            float inflatedWidth = SolveVisibleWidthOfSecondWindow(frontAsReportedByGetWindowRect, back);

            Assert.AreEqual(400f, visualWidth, 0.001f, $"{LogPrefix} 시각적 경계 기준: x 500~900이 남아야 한다.");
            Assert.AreEqual(393f, inflatedWidth, 0.001f, $"{LogPrefix} GetWindowRect 기준: x 507~900만 남는다.");
            Assert.AreEqual(InvisibleBorder, visualWidth - inflatedWidth, 0.001f,
                $"{LogPrefix} 차이가 0이면 이 Minor를 고칠 이유가 없다는 뜻이므로 이 테스트가 무의미해진다.");
        }

        private static float SolveVisibleWidthOfSecondWindow(Rect front, Rect back)
        {
            var solver = new VisibleTopEdgeSolver();
            solver.Begin();
            solver.AddWindow(front);
            solver.AddWindow(back);
            solver.Solve(MinVisibleWidth, false, default);
            return solver.GetVisibleWidth(1);
        }

        // ---- 소스 정적 감사 (Win32 P/Invoke는 macOS EditMode에서 실행 불가하므로 소스로 잠근다) ----

        private static string ReadWin32Source()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "Windows",
                "Win32WindowService.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} Win32WindowService.cs를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void D2_DWMWA_EXTENDED_FRAME_BOUNDS_상수와_RECT_오버로드가_선언되어_있다()
        {
            string src = ReadWin32Source();

            StringAssert.Contains("private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;", src,
                $"{LogPrefix} 값이 9가 아니면 DWM이 전혀 다른 속성을 다른 크기로 써서 스택이 깨진다.");
            StringAssert.Contains(
                "private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);",
                src,
                $"{LogPrefix} EXTENDED_FRAME_BOUNDS는 RECT를 돌려준다 — int 오버로드로 부르면 " +
                "4바이트만 받아 좌표가 통째로 쓰레기가 된다.");
            StringAssert.Contains("Marshal.SizeOf<RECT>()", src,
                $"{LogPrefix} cbAttribute는 리터럴이 아니라 실제 구조체 크기여야 한다.");
        }

        [Test]
        public void D3_시각적_경계_조회는_실패시_GetWindowRect로_폴백한다()
        {
            string src = ReadWin32Source();
            int helperStart = src.IndexOf("private static bool TryGetVisualWindowRect", System.StringComparison.Ordinal);
            Assert.Greater(helperStart, 0, $"{LogPrefix} TryGetVisualWindowRect 헬퍼가 사라졌다.");

            string helper = src.Substring(helperStart, System.Math.Min(1400, src.Length - helperStart));
            StringAssert.Contains("DWMWA_EXTENDED_FRAME_BOUNDS", helper);
            StringAssert.Contains("GetWindowRect(hWnd, out RECT raw)", helper,
                $"{LogPrefix} DWM 합성이 꺼진 환경/지원하지 않는 창에서 조회가 실패하면 창 열거가 통째로 " +
                "죽는다. 반드시 GetWindowRect 폴백이 있어야 한다(조회 실패로 멀쩡한 창을 지우지 않는다).");
        }

        [Test]
        public void D4_발판_열거는_GetWindowRect를_직접_쓰지_않는다()
        {
            string src = ReadWin32Source();
            int start = src.IndexOf("private bool OnEnumWindow", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, $"{LogPrefix} OnEnumWindow를 찾지 못했다.");
            int end = src.IndexOf("public IReadOnlyList<PlatformFoothold> EnumerateFootholds", start,
                System.StringComparison.Ordinal);
            Assert.Greater(end, start);

            string body = src.Substring(start, end - start);
            StringAssert.Contains("TryGetVisualWindowRect", body);
            Assert.IsFalse(body.Contains("GetWindowRect("),
                $"{LogPrefix} 발판 사각형이 다시 GetWindowRect로 돌아가면 보이지 않는 테두리 ~7px이 " +
                "발판/가림/조준 좌표에 그대로 되살아난다(이월 Minor 재발).");
            StringAssert.Contains("ClassifyGeometry", body,
                $"{LogPrefix} 알파/크기 필터가 열거 경로에서 빠지면 이월 Major가 그대로 재발한다.");
        }

        [Test]
        public void D5_알파는_읽기만_하고_쓰기_API는_들어오지_않았다()
        {
            string src = ReadWin32Source();
            StringAssert.Contains("GetLayeredWindowAttributes", src);
            Assert.IsFalse(src.Contains("SetLayeredWindowAttributes("),
                $"{LogPrefix} 남의 창 투명도를 바꾸는 API다 — 원칙 3(유저 자산 불변) 정면 위반. " +
                "이 필터를 넣으면서 짝이 되는 쓰기 API가 딸려 들어오지 않았는지 잠근다.");
        }
    }
}
