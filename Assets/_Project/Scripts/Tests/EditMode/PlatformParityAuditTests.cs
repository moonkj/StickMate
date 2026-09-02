using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// Windows/macOS 패리티 자동 감사 (2026-09-01 신설)
    /// ============================================================================
    /// 사용자의 <b>상시 요구사항</b>: "수정한 모든 것들은 윈도우 버전도 동일하게 수정되어야 함".
    /// 그런데 개발은 macOS 머신에서만 이뤄지고 Windows 빌드는 이 환경에서 <b>실행조차 할 수 없다</b>.
    /// 그래서 Windows 쪽 누락은 컴파일 에러로도, 실측으로도 드러나지 않고 <b>조용히</b> 쌓인다.
    /// 실제로 이 프로젝트는 같은 사고를 이미 세 번 겪었다:
    ///   · <c>VisibleTopEdgeSolver</c>  — 가려짐 필터를 macOS에만 고쳐 Windows에서 버그가 그대로 생존
    ///   · <c>WindowsFootholdFilter</c> — macOS의 알파 필터에 대응물이 없어 몇 주 지연
    ///   · <c>FullscreenVerdictDebouncer</c> — 2026-08-31 밤에 macOS에만 배선(이 파일이 잡아낸 건)
    /// 매번 사람이 눈으로 대조하면 또 뒤처진다. 그래서 <b>기계가</b> 대조한다.
    ///
    /// <para><b>왜 리플렉션이 아니라 소스 텍스트 스캔인가</b>: <c>Win32WindowService.cs</c>와
    /// <c>WindowsOverlayStateEnforcer.cs</c>는 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이라
    /// macOS 에디터에서는 <b>타입이 존재하지 않는다</b>. 리플렉션으로는 영원히 검사할 수 없다.
    /// 기존 <c>DisplayTopologyRefitTests</c>/<c>UserAssetImmutabilityAuditTests</c>가 쓰는 것과 같은
    /// 정적 스캔 방식을 따른다.</para>
    ///
    /// <para><b>이 테스트의 한계(정직하게)</b>: 텍스트가 있다고 실제로 동작한다는 보증은 아니다.
    /// "한쪽에만 있다"는 <b>구조적 비대칭</b>만 잡는다 — 그게 지금까지 실제로 반복된 실패 모드다.
    /// 실동작 확인은 사용자 Windows 머신에서 별도로 해야 한다.</para>
        ///
        /// ============================================================================
        /// ★★ 2026-09-02 — 항목의 <b>성격</b>을 이름으로 구분한다
        /// ============================================================================
        /// 이 파일이 커지면서 실제로 생긴 문제: <b>전부 <c>Assert.Ignore</c>로 뭉치면 목록이 뜻을
        /// 잃는다.</b> 러너에 "건너뜀 N건"이라고만 뜨면 그중 무엇이 진짜 코드 갭이고, 무엇이 일부러
        /// 다르게 둔 <b>결정</b>이며, 무엇이 오히려 <b>macOS가 뒤처진</b> 항목인지 아무도 구분하지
        /// 못한다. 그래서 접두사로 성격을 드러내고, 그 규칙을 <c>감사_대장_모든_항목이_분류표에_들어_있다</c>가
        /// 기계적으로 지킨다(사람의 성실함에 기대지 않는다):
        /// <list type="table">
        ///  <item><term>미해결_</term><description>진짜 코드 갭. 고칠 코드가 있다. → 건너뜀</description></item>
        ///  <item><term>실기미확인_</term><description>코드는 닫혔고 Windows 하드웨어만 남았다. → 건너뜀</description></item>
        ///  <item><term>결정_</term><description>의도된 차이. <b>되돌리면 실패한다.</b> → 정식 검사</description></item>
        ///  <item><term>역방향_</term><description>macOS가 뒤처진 쪽. Windows 구현을 보호한다. → 정식 검사</description></item>
        ///  <item><term>해당없음_</term><description>반대 플랫폼에 그 문제가 구조적으로 없다. → 정식 검사</description></item>
        ///  <item><term>갭추적_</term><description>갭은 열렸으나 다른 테스트가 이미 러너에 띄운다(중복 스킵 금지). → 정식 검사</description></item>
        /// </list>
        ///
        /// <para><b>사유는 반드시 오늘 날짜를 달고 갱신한다.</b> 2026-09-02에 이 파일의 네 항목 중
        /// <b>셋</b>이 "사용자 지시로 보류(윈도우는 일단 미루고 맥만)"를 근거로 들고 있었는데, 그 지시는
        /// 같은 밤 <b>"맥에 적용한 사항 윈도우에도 모두 적용"으로 뒤집혔다</b>. 낡은 사유는 거짓말이고,
        /// 거짓말하는 감사는 없는 감사보다 나쁘다. 대장 검사가 날짜 없는 사유를 실패로 잡는다.</para>
    /// </summary>
    public class PlatformParityAuditTests
    {
        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string MacWindowServicePath =>
            Path.Combine(PlatformRoot, "MacOS", "MacWindowService.cs");

        private static string WinWindowServicePath =>
            Path.Combine(PlatformRoot, "Windows", "Win32WindowService.cs");

        private static string MacEnforcerPath =>
            Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs");

        private static string WinEnforcerPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs");

        // ==================== 상단 예약 띠(메뉴바 / 상단 도킹 작업표시줄) ====================

        /// <summary>
        /// 상단 예약 띠(macOS 메뉴바 / Windows 상단 도킹 작업표시줄) 두께 조회 —
        /// <c>Platform/IReservedTopBarService</c> + <c>SurfaceSafeAreaPolicy</c>.
        ///
        /// <para><b>★ 2026-09-02 04:32 — 이 항목은 닫혔다. Ignore를 걷고 정식 검사로 승격한다.</b>
        /// 같은 밤 Windows 라운드가 <c>Win32WindowService</c>에
        /// <c>TryGetReservedTopInsetPoints</c>(= <c>GetMonitorInfo</c>의 <c>rcWork.Top − rcMonitor.Top</c>)를
        /// 넣고 <b>기반 목록에 인터페이스까지 달았다</b>. 그 전까지 이 자리는 "Windows 미해결"이었다.</para>
        ///
        /// ============================================================================
        /// ★ 승격할 때 반드시 물어야 하는 것 — "이 초록은 공허하지 않은가"
        /// ============================================================================
        /// 같은 밤 <c>하단막대_회피_정책</c>이 <c>StringAssert.Contains</c>가 <b>XML 주석에만</b> 걸려
        /// Windows 단언이 <b>한 번도 구현을 검사한 적이 없었다</b>는 것으로 드러났다. 이 항목은 그
        /// 함정이 특히 위험하다 — <c>Win32WindowService</c>의 <b>주석 자체가</b> 며칠 전부터
        /// "IReservedTopBarService 구현이 이미 같은 호출에서 rcWork/rcMonitor를 읽고 있다"고
        /// <b>적어 두고 있었다</b>. 단순 문자열 검사였다면 인터페이스를 달기 <b>전부터</b> 초록이었다.
        /// 그래서 세 겹으로 묻는다:
        /// <list type="number">
        ///   <item><b>기반 목록에 있는가</b> — <c>class X :</c> 와 여는 중괄호 <b>사이</b>에서만 찾는다.
        ///         주석·XML·본문 어디에 이름이 있어도 세지 않는다. 인터페이스를 달지 않으면
        ///         <c>ReservedTopBarProbe.Resolve</c>의 <c>inner is IReservedTopBarService</c>가
        ///         <b>조용히 null</b>이 되어 인셋이 0으로 떨어진다 — "메서드는 있는데 아무도 못 부른다"가
        ///         이 항목의 정확한 실패 모양이고, 컴파일도 테스트도 그것을 알려 주지 않는다.</item>
        ///   <item><b>메서드가 실제로 있는가</b> — 찾을 이름을 <b>인터페이스에서 리플렉션으로 뽑는다</b>.
        ///         문자열을 베끼면 계약이 바뀐 날 검사만 낡는다(CLAUDE.md: 상수를 베끼지 않는다).</item>
        ///   <item><b>정책은 중립 위치에 있는가</b> — <c>SurfaceSafeAreaPolicy</c>는 OS 호출 0줄이어야
        ///         한다. 갈라진 것은 <b>사실 조회</b>뿐이라는 것이 이 설계의 전제다.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 상단_예약띠_조회가_양_플랫폼에_모두_배선되어_있다()
        {
            // ---- (1) 정책과 계약은 중립 위치(Platform/ 바로 아래)에 있어야 한다 ----
            string policy = Path.Combine(PlatformRoot, "SurfaceSafeAreaPolicy.cs");
            string contract = Path.Combine(PlatformRoot, "IReservedTopBarService.cs");

            Assert.IsTrue(File.Exists(policy),
                "SurfaceSafeAreaPolicy가 Platform/ 중립 위치에 없습니다 — 정책이 플랫폼 폴더 안으로 " +
                "들어가면 반대편 플랫폼이 물리적으로 호출할 수 없습니다(FullscreenSuspendPolicy 사고).");
            Assert.IsTrue(File.Exists(contract),
                "IReservedTopBarService가 Platform/ 중립 위치에 없습니다.");

            StringAssert.DoesNotContain("UNITY_STANDALONE_", StripLineComments(ReadSource(policy)),
                "정책 파일에 플랫폼 분기가 들어왔습니다 — 이 파일은 순수 산술이어야 하고, 그래야 " +
                "양쪽 플랫폼이 같은 규칙을 씁니다.");

            // ---- (2) 찾을 메서드 이름은 계약에서 뽑는다(베끼지 않는다) ----
            System.Reflection.MethodInfo[] contractMethods = typeof(IReservedTopBarService).GetMethods();
            Assert.AreEqual(1, contractMethods.Length,
                "IReservedTopBarService의 메서드 수가 1이 아닙니다 — 이 검사는 '그 하나'를 기준으로 " +
                "양 플랫폼을 대조합니다. 계약이 늘었다면 아래 대조도 함께 늘리세요.");
            string probeMethod = contractMethods[0].Name;

            // ---- (3) 양 플랫폼이 **기반 목록에** 달고 **메서드를 실제로** 갖고 있는가 ----
            string macProbe = Path.Combine(PlatformRoot, "MacOS", "MacReservedTopBarService.cs");
            Assert.IsTrue(File.Exists(macProbe),
                "macOS 상단 인셋 조회가 사라졌습니다 — 팝오버가 다시 메뉴바를 덮습니다(절대 불변 원칙 2).");
            AssertDeclaresInterface(macProbe, "MacReservedTopBarService",
                nameof(IReservedTopBarService), probeMethod);

            AssertDeclaresInterface(WinWindowServicePath, "Win32WindowService",
                nameof(IReservedTopBarService), probeMethod);

            // ---- (4) 소비 배선: 두 경로 모두 중립 프로브 한 곳을 지난다 ----
            string probeSource = StripLineComments(
                ReadSource(Path.Combine(PlatformRoot, "ReservedTopBarProbe.cs")));
            StringAssert.Contains("is IReservedTopBarService", probeSource,
                "ReservedTopBarProbe가 '플랫폼 서비스가 직접 구현한 경우'를 잡지 않습니다 — " +
                "Windows는 Win32WindowService가 직접 인터페이스를 달았으므로 이 분기가 유일한 경로입니다.");
            StringAssert.Contains("MacReservedTopBarService.TryCreate(", probeSource,
                "macOS 조립 경로가 사라졌습니다 — MacWindowService는 이 인터페이스를 직접 달지 않고 " +
                "별도 어댑터로 조립합니다. 이 줄이 없으면 macOS 인셋이 조용히 0이 됩니다.");
        }

        /// <summary>
        /// ★ 이 감사의 <b>핵심 장치</b> — "인터페이스 이름이 그 파일 어딘가에 있다"가 아니라
        /// <b>기반 목록(class X : ... {)</b> 안에 있는지를 본다.
        ///
        /// <para>왜 이렇게까지 하는가: 이 저장소에서 <b>실제로</b> 벌어진 거짓 초록 두 건이 모두
        /// "결함을 설명하는 주석이 구현으로 오인된" 형태였다(<see cref="StripLineComments"/> 문서와
        /// <c>하단막대_회피_정책</c> 문서). 인터페이스 이름은 특히 주석에 자주 등장한다 —
        /// "이건 아직 IXxx를 달지 않았다"는 문장이 그 자체로 검사를 통과시킨다.</para>
        /// </summary>
        private static void AssertDeclaresInterface(
            string path, string className, string interfaceName, string requiredMethodName)
        {
            string src = StripLineComments(ReadSource(path));

            Assert.IsTrue(TryGetBaseList(src, className, out string baseList),
                $"{Path.GetFileName(path)}에서 '{className}' 선언(또는 그 기반 목록)을 찾지 못했습니다 — " +
                "감사 앵커가 낡았습니다. 클래스 이름이 바뀌었다면 이 검사도 함께 갱신하세요. " +
                "그대로 두면 '못 찾았다'가 조용한 초록이 됩니다.");

            StringAssert.Contains(interfaceName, baseList,
                $"{Path.GetFileName(path)}의 {className}이 {interfaceName}을 **기반 목록에 달지 않았습니다**. " +
                "메서드만 있고 인터페이스가 없으면 소비 측의 'is/as' 판정이 조용히 null이 되어, " +
                "기능이 있는 것처럼 보이면서 실제로는 한 번도 호출되지 않습니다(컴파일도 통과합니다).\n" +
                $"찾은 기반 목록: {baseList.Replace("\n", " ").Trim()}");

            StringAssert.Contains(requiredMethodName + "(", src,
                $"{Path.GetFileName(path)}에 계약 메서드 {requiredMethodName}()가 없습니다 — " +
                "인터페이스만 달고 본문이 없으면 컴파일이 막지만, 이름이 바뀌면 이 검사가 먼저 알려 줍니다.");
        }

        /// <summary>
        /// <c>class X</c> 선언과 여는 중괄호 <b>사이</b> 문자열(= 기반 목록)만 잘라 낸다.
        /// 기반 목록이 아예 없으면(콜론이 없으면) false — "아무 인터페이스도 안 달았다"는 뜻이다.
        /// </summary>
        private static bool TryGetBaseList(string source, string className, out string baseList)
        {
            baseList = string.Empty;
            int at = source.IndexOf("class " + className, StringComparison.Ordinal);
            if (at < 0) return false;

            int brace = source.IndexOf('{', at);
            if (brace < 0) return false;

            string head = source.Substring(at, brace - at);
            int colon = head.IndexOf(':');
            if (colon < 0) return true;                 // 선언은 찾았고 기반 목록만 비었다.

            baseList = head.Substring(colon + 1);
            return true;
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>기반 목록 추출기가 실제로 구분하는가.</b>
        /// <para>위 검사는 "달려 있다"를 단언하므로, 추출기가 파일 전체를 돌려주기만 해도 초록이다.
        /// 그래서 <b>주석·본문에만</b> 이름이 있는 미끼와 <b>진짜로 단</b> 선언을 둘 다 이 파일 안에
        /// 박제해 판정이 갈리는지 본다(비교 대상 양쪽을 다 얼린다).</para>
        /// <para>미끼의 마지막 줄은 <b>문자열 리터럴</b>이다 — <see cref="StripLineComments"/>로는
        /// 지워지지 않으므로, 통과의 근거가 "주석을 지웠다"가 아니라 "<b>구간을 제한했다</b>"임을
        /// 증명한다. 이것이 <c>하단막대_회피_정책</c>이 놓쳤던 바로 그 층이다.</para>
        /// </summary>
        [Test]
        public void 기반목록_추출기는_주석과_본문의_인터페이스_이름을_세지_않는다()
        {
            const string decoy =
                "    /// <summary>이 클래스는 IReservedTopBarService를 곧 달 예정이다.</summary>\n" +
                "    public sealed class Decoy :\n" +
                "        IPlatformWindowService\n" +
                "    {\n" +
                "        // IReservedTopBarService 구현이 이미 같은 호출에서 rcWork를 읽고 있다.\n" +
                "        public bool TryGetReservedTopInsetPoints(out float p) { p = 0f; return false; }\n" +
                "        private void Log() { Debug.Log(\"IReservedTopBarService 미구현\"); }\n" +
                "    }\n";

            Assert.IsTrue(TryGetBaseList(StripLineComments(decoy), "Decoy", out string decoyList),
                "미끼 선언조차 찾지 못했습니다 — 추출기가 눈이 멀었습니다.");
            StringAssert.DoesNotContain("IReservedTopBarService", decoyList,
                "주석/본문에만 있는 인터페이스 이름을 '달았다'로 셉니다 — 이 상태라면 위 승격은 " +
                "거짓 초록입니다(2026-09-02 하단막대 항목이 정확히 이 모양으로 무력했습니다).");

            const string real =
                "    public sealed class Real :\n" +
                "        IPlatformWindowService,\n" +
                "        IReservedTopBarService\n" +
                "    {\n" +
                "    }\n";
            Assert.IsTrue(TryGetBaseList(StripLineComments(real), "Real", out string realList),
                "진짜 선언을 찾지 못했습니다.");
            StringAssert.Contains("IReservedTopBarService", realList,
                "진짜로 단 인터페이스를 못 봅니다 — 추출기가 너무 좁습니다(오탐이 아니라 미탐).");

            const string bare = "    public sealed class Bare\n    {\n    }\n";
            Assert.IsTrue(TryGetBaseList(StripLineComments(bare), "Bare", out string bareList),
                "기반 목록이 없는 선언에서 false를 돌려주면 '못 찾았다'와 구분되지 않습니다.");
            Assert.IsEmpty(bareList.Trim(),
                "기반 목록이 없는데 무언가를 돌려줍니다 — 그 문자열에 우연히 이름이 들어가면 거짓 초록입니다.");
        }

        /// <summary>
        /// ★ 상단은 양쪽 다 닫혔지만 <b>하단</b>은 아직 판단이 안 났다 — 잊히지 않게 러너에 띄워 둔다.
        /// </summary>
        [Test]
        public void 미해결_하단_예약띠를_Windows에서도_강제할지_판단되지_않았다()
        {
            string neutral = StripLineComments(
                ReadSource(Path.Combine(PlatformRoot, "SurfaceSafeAreaPolicy.cs")));

            // 정책이 하단까지 강제하기 시작하면(= 아래쪽 한계에 인셋이 들어가면) 자동 승격시킨다.
            if (neutral.Contains("bottomInset"))
            {
                Assert.Pass("하단 인셋이 정책에 들어왔습니다 — 이 테스트를 정식 검사로 승격하고 " +
                    "macOS Dock 발판(Core/DockGeometry)과 충돌하지 않는지 반드시 함께 확인하세요.");
            }

            Assert.Ignore("【미해결 · 판정 대기(코드가 아니라 결정이 막혀 있다)】 사유 갱신 2026-09-02 04:5x\n" +
                "항목: 화면 **하단** 예약 띠를 표면 배치에서 강제할 것인가.\n" +
                "macOS: 일부러 강제하지 않는다 — Dock은 자동 숨김이 흔하고, 이 앱은 그 위를 " +
                "의도적으로 캐릭터 발판으로 쓴다(Core/DockGeometry). 창이 Dock을 덮는 것은 " +
                "macOS의 모든 앱이 하는 표준 동작이기도 하다.\n" +
                "Windows: 사정이 다르다 — 작업표시줄은 가로 전체를 점유하고, " +
                "'작업표시줄에 걸쳐서 돌아다닌다'는 실제 사용자 신고 이력이 있다(2026-08-31). " +
                "하단도 강제해야 할 수 있다.\n" +
                "★ 지금 막혀 있는 것은 코드가 아니라 **판정**이다. 조회는 이미 양쪽 다 있다 " +
                "(IReservedBottomBarService). 실기 확인이 필요하다.\n" +
                "★ 2026-09-02 갱신 — 우선순위가 올라갔다: 사용자 지시가 '맥에 적용한 사항 윈도우에도 " +
                "모두 적용'으로 바뀌었다. 다만 이 항목은 '맥에 적용한 것을 윈도우에 옮기는' 종류가 " +
                "아니다 — macOS는 하단을 **일부러** 강제하지 않으므로, 그대로 옮기면 Windows에서 " +
                "신고된 '작업표시줄에 걸쳐서 돌아다닌다'가 그대로 남는다. 즉 이 항목만은 " +
                "**같게 만드는 것이 정답이 아닐 수 있다**(위 결정_/역방향_ 항목들과 같은 성격).\n" +
                "★ 상단과의 대조: 상단은 2026-09-02 04:32에 양쪽 다 닫혔다(위 " +
                "상단_예약띠_조회가_양_플랫폼에_모두_배선되어_있다). 조회 경로가 같은 " +
                "GetMonitorInfo 한 번이므로 하단도 코드는 이미 있다.\n" +
                "실기 검증 필요 — 사용자 Windows 머신: 작업표시줄 위에 캐릭터를 올리고 " +
                "팝오버/정보창을 열어 하단 막대를 덮는지 본다.");
        }

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path), $"플랫폼 소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// 주석 줄을 걷어낸 소스. <b>"실제로 부르는가"를 물을 때만</b> 쓴다.
        ///
        /// 필요한 이유(이 파일을 만들면서 실제로 밟은 함정): Win32WindowService의 XML 문서에
        /// "macOS는 <c>FullscreenGameCategory.IsGameCategory</c>로 게임만 거른다(Windows는 아직
        /// 없다)"고 <b>결함을 설명하는 문장</b>을 적어 두었더니, 단순 문자열 검사가 그것을 구현으로
        /// 오인해 "이미 고쳐졌다"고 통과해버렸다. 결함을 정직하게 적을수록 감사가 눈머는 셈이라
        /// 판정에서는 주석을 반드시 뺀다.
        /// </summary>
        private static string StripLineComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;   // /* */ 블록 본문
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>두 플랫폼 파일 각각에 같은 조각이 들어 있는지 한 번에 확인한다.</summary>
        private static void AssertBothContain(string macPath, string winPath, string needle, string why)
        {
            foreach (string path in new[] { macPath, winPath })
            {
                StringAssert.Contains(needle, ReadSource(path),
                    $"{Path.GetFileName(path)}에 \"{needle}\"이(가) 없습니다 — {why}\n" +
                    "이 테스트는 '한쪽 플랫폼만 고치고 넘어간' 상태를 잡기 위한 것입니다. " +
                    "두 파일을 같은 라운드에 함께 고치세요(CLAUDE.md: 수정은 Windows에도 동일하게).");
            }
        }

        // ====================================================================
        // 1. 전체화면 자동 숨김(절대 불변 원칙 2)
        // ====================================================================

        /// <summary>
        /// 전체화면 판정의 <b>깜빡임 디바운스</b>가 두 플랫폼 모두에 걸려 있어야 한다.
        ///
        /// 2026-08-31 밤에 macOS에만 들어갔고 Windows는 원시 판정을 그대로 썼다. 그 상태에서
        /// Windows 사용자는 작업표시줄 자동 숨김/알트탭/게임 해상도 전환 순간마다 캐릭터가
        /// Suspend↔Resume을 반복하며 깜빡인다(프레임 등급도 함께 요동친다).
        /// </summary>
        [Test]
        public void 전체화면_판정_디바운스가_양_플랫폼에_모두_배선되어_있다()
        {
            AssertBothContain(MacWindowServicePath, WinWindowServicePath,
                "FullscreenVerdictDebouncer",
                "전체화면 판정 깜빡임(flapping)을 흡수하는 공용 디바운서가 배선되지 않았습니다. " +
                "그러면 그 플랫폼에서만 캐릭터가 숨었다 나타났다를 반복합니다.");

            AssertBothContain(MacWindowServicePath, WinWindowServicePath,
                "FullscreenVerdictHoldSeconds",
                "디바운스 유지 시간 상수가 없습니다 — 한쪽만 값이 다르면 같은 상황에서 두 플랫폼이 " +
                "다르게 행동합니다.");
        }

        /// <summary>
        /// 전체화면 판정 규칙 파일이 <b>플랫폼 중립 위치</b>에 있어야 한다.
        ///
        /// 이 파일은 원래 <c>Platform/MacOS/FullscreenSuspendPolicy.cs</c>(네임스페이스
        /// <c>StickMate.Platform.MacOS</c>)였다. 그 자리에 있는 한 Windows 구현은 같은 규칙을
        /// <b>부를 수조차 없다</b> — 정확히 그래서 디바운스가 한쪽에만 걸렸다. 위치를 되돌리는
        /// 리팩터링이 들어오면 여기서 막는다.
        /// </summary>
        [Test]
        public void 전체화면_정책은_플랫폼_중립_위치에_있다()
        {
            string neutral = Path.Combine(PlatformRoot, "FullscreenSuspendPolicy.cs");
            string macOnly = Path.Combine(PlatformRoot, "MacOS", "FullscreenSuspendPolicy.cs");

            Assert.IsTrue(File.Exists(neutral),
                "FullscreenSuspendPolicy.cs가 Platform/ 바로 아래에 없습니다. 이 파일은 두 플랫폼이 " +
                "함께 쓰는 순수 규칙이므로 플랫폼 폴더에 두면 반대쪽이 참조할 수 없습니다.");
            Assert.IsFalse(File.Exists(macOnly),
                "FullscreenSuspendPolicy.cs가 다시 Platform/MacOS/ 로 들어갔습니다 — " +
                "그 순간 Windows는 같은 규칙을 부를 수 없게 되고, 2026-08-31의 누락이 그대로 재발합니다.");

            StringAssert.Contains("namespace StickMate.Platform\n", ReadSource(neutral).Replace("\r\n", "\n"),
                "FullscreenSuspendPolicy.cs의 네임스페이스가 StickMate.Platform이 아닙니다 — " +
                "하위 네임스페이스로 내리면 반대쪽 플랫폼에서 using 없이 참조되지 않습니다.");
        }

        // ====================================================================
        // 2. 전역 단축키 — 키 하나가 한쪽에만 들어가는 사고 방지
        // ====================================================================

        /// <summary>
        /// <see cref="GlobalKey"/>에 새 키를 추가하면 <b>두 플랫폼 모두</b>가 그 키를 매핑해야 한다.
        ///
        /// 이 열거형은 "물어볼 수 있는 키"의 전부다. 한쪽 구현의 switch에 한 줄이 빠지면 컴파일은
        /// 그대로 되고, 그 플랫폼에서만 <c>TryGetKeyPressed</c>가 조용히 false를 돌려준다 =
        /// <b>단축키가 아무 로그 없이 죽는다</b>(2026-09-01 설정창 키를 넣고 다시 <c>,</c>→<c>P</c>로
        /// 옮기는 동안 두 번 실제로 경계했던 위험 — IGlobalKeyStateService.cs의 <c>P</c> 문서 참고).
        ///
        /// 검사 방식: 각 열거값 이름이 두 소스에 문자열로 등장하는지 본다. macOS는
        /// <c>case GlobalKey.X: code = kVK_ANSI_X</c>, Windows는 <c>case GlobalKey.X: letter = 'X'</c>
        /// 처럼 형태가 다르므로 <c>GlobalKey.이름</c>이라는 공통 조각만 겨냥한다.
        /// </summary>
        [Test]
        public void 모든_전역키가_양_플랫폼_구현에_매핑되어_있다()
        {
            string mac = ReadSource(MacWindowServicePath);
            string win = ReadSource(WinWindowServicePath);

            var missing = new List<string>();
            foreach (GlobalKey key in (GlobalKey[])Enum.GetValues(typeof(GlobalKey)))
            {
                // ★ 콜론까지 포함해야 한다. 지금은 없어진 "GlobalKey.Comma"가 "GlobalKey.Command"의
                //   접두사였고, 콜론이 없으면 Command만 있어도 Comma가 통과했다(이 테스트 자신의 오탐).
                //   접두사 관계는 언제든 다시 생기므로 규칙은 남긴다.
                string token = "GlobalKey." + key + ":";
                bool inMac = mac.Contains(token);
                bool inWin = win.Contains(token);
                if (inMac && inWin) continue;

                missing.Add($"  · {key} — macOS {(inMac ? "있음" : "★없음")} / Windows {(inWin ? "있음" : "★없음")}");
            }

            if (missing.Count == 0) return;

            Assert.Fail("전역 단축키 매핑이 한쪽 플랫폼에만 있습니다(그 플랫폼에서 조용히 false만 " +
                "돌아옵니다 — 사용자에게는 '단축키가 안 먹는다'로 보이고 로그도 남지 않습니다):\n" +
                string.Join("\n", missing) +
                "\nmacOS는 MacWindowService.TryGetKeyPressed의 표에, Windows는 " +
                "Win32WindowService.TryGetKeyPressed의 switch에 한 줄씩 추가하세요.");
        }

        // ====================================================================
        // 3. 프레임 페이싱 — 등급 판정 입력이 양쪽에서 똑같이 공급되는가
        // ====================================================================

        /// <summary>
        /// 두 Enforcer 모두 같은 자리에서 <c>FramePacing.ApplyOnce</c> + <c>FramePacing.Tick</c>을
        /// 불러야 한다. 특히 <c>ResolveCharacterIdle</c>은 2026-09-01부터 <b>Away 등급 판정에도</b>
        /// 들어가므로(무입력만으로 Away를 주면 구경 중인 사용자 앞에서 걷기가 15fps로 끊긴다),
        /// 한쪽에서 이 인자가 끊기면 그 플랫폼은 절감이 아니라 <b>비용</b> 쪽으로 실패한다.
        /// </summary>
        [Test]
        public void 프레임페이싱_배선이_양_플랫폼_Enforcer에_동일하게_있다()
        {
            AssertBothContain(MacEnforcerPath, WinEnforcerPath,
                "FramePacing.ApplyOnce(",
                "프레임 페이싱 초기화가 없습니다 — 그 플랫폼은 24시간 상주 절감이 통째로 꺼집니다.");

            AssertBothContain(MacEnforcerPath, WinEnforcerPath,
                "FramePacing.Tick(FramePacing.ResolveCharacterIdle(",
                "캐릭터 정지 신호가 프레임 등급 판정에 공급되지 않습니다 — Calm/Away 두 등급이 " +
                "모두 성립하지 않아 화면이 꺼지지 않는 한 계속 60fps로 돕니다.");
        }

        /// <summary>
        /// UI 조작 중 60fps 홀드(<c>FramePacing.HoldActiveForInteraction</c>)는 플랫폼 중립
        /// <c>FramePacing</c> 한 곳에만 있어야 한다 — 플랫폼별로 복제되면 한쪽만 고쳐진다.
        /// </summary>
        [Test]
        public void UI_홀드는_플랫폼_중립_한_곳에만_구현되어_있다()
        {
            string neutral = ReadSource(Path.Combine(PlatformRoot, "FramePacing.cs"));
            StringAssert.Contains("HoldActiveForInteraction", neutral,
                "FramePacing에 UI 조작 홀드 진입점이 없습니다.");

            foreach (string path in new[] { MacWindowServicePath, WinWindowServicePath })
            {
                Assert.IsFalse(ReadSource(path).Contains("_interactionHoldUntil"),
                    $"{Path.GetFileName(path)}가 UI 홀드 상태를 자체 보관합니다 — 플랫폼마다 복제하면 " +
                    "한쪽만 고쳐지는 바로 그 실패가 반복됩니다. FramePacing 한 곳에만 두세요.");
            }
        }

        // ====================================================================
        // 3-1. 투명화 재적용 경로 (2026-09-02 추가 — 1px 래칫 라운드)
        // ====================================================================

        /// <summary>
        /// 재적용 루프의 <b>투명화 처리 방식 결정</b>은 플랫폼 중립
        /// <c>OverlayStateReapplyPolicy</c> 한 곳에만 있어야 한다.
        ///
        /// <para><b>이 항목이 생긴 이유(새 플랫폼 분기)</b>: Windows에서만
        /// <c>_controller.isTransparent</c> 재대입이 네이티브 <c>SetBorderless</c>의 폭 ±1 흔들기
        /// (<c>SetWindowPos</c> 4회)를 유발해 1px 래칫 + 스왑체인 재생성을 만들었다.
        /// 그래서 Windows만 "OS 실측 후 필요할 때만 부르는" 분기를 갖게 됐다.</para>
        ///
        /// <para><b>macOS는 일부러 갈라 두었다(갭이 아니다)</b>: Swift 원본
        /// <c>LibUniWinC.swift · _setWindowBorderless</c>는 <c>window.styleMask = [.borderless]</c>
        /// 한 줄이라 프레임을 건드리지 않고, <c>window.styleMask != [.borderless]</c> 동등성 가드까지
        /// 이미 걸려 있다. 같은 수술을 하면 얻는 것 없이 실측 튜닝이 끝난 경로에 위험만 넣는다.
        /// 그 판단 근거를 macOS Enforcer 주석에 남겼는지까지 여기서 확인한다 — 근거가 사라지면
        /// 다음 사람이 "한쪽만 고쳐졌다"고 오판한다.</para>
        /// </summary>
        [Test]
        public void 투명화_재적용_정책은_플랫폼_중립_위치에_있다()
        {
            string policyPath = Path.Combine(PlatformRoot, "OverlayStateReapplyPolicy.cs");
            Assert.IsTrue(File.Exists(policyPath),
                "OverlayStateReapplyPolicy.cs가 없습니다 — 정책이 다시 플랫폼 폴더로 내려갔다면 " +
                "다른 플랫폼이 물리적으로 호출할 수 없습니다(FullscreenSuspendPolicy 사고와 같은 형태).");

            string policy = StripLineComments(ReadSource(policyPath));
            StringAssert.DoesNotContain("UNITY_STANDALONE_", policy,
                "플랫폼 중립이어야 할 정책에 플랫폼 조건부 컴파일이 들어왔습니다.");
            StringAssert.DoesNotContain("DllImport", policy,
                "정책에 P/Invoke가 들어왔습니다 — 그러면 이 머신의 EditMode가 규칙을 실행할 수 없습니다.");

            // 두 Enforcer 모두 공용 재적용 상수를 참조한다(값이 갈라지면 패리티가 말뿐이 된다).
            AssertBothContain(MacEnforcerPath, WinEnforcerPath,
                "OverlayStateReapplyPolicy.ReapplyAttempts",
                "재적용 횟수 상수를 그 플랫폼이 자체 리터럴로 들고 있습니다.");

            // Windows만 갖는 분기 — 있어야 한다.
            string win = StripLineComments(ReadSource(WinEnforcerPath));
            StringAssert.Contains("OverlayStateReapplyPolicy.DecideTransparencyReapply(", win,
                "Windows Enforcer가 투명화 재적용 방식을 정책에 묻지 않습니다 — 무조건 재대입으로 " +
                "돌아갔다면 1px 래칫과 스왑체인 재생성 4회/회차가 되살아납니다.");

            // macOS는 갈라 둔 것이 맞다 — 다만 그 사유가 코드 옆에 남아 있어야 한다.
            string macRaw = ReadSource(MacEnforcerPath);
            StringAssert.Contains("_setWindowBorderless", macRaw,
                "macOS Enforcer에 '왜 같은 수술을 하지 않는가'의 근거(Swift 원본 실측)가 없습니다. " +
                "근거 없는 비대칭은 다음 라운드에서 '한쪽만 고쳐진 갭'으로 오판됩니다.");
        }

        // ====================================================================
        // 3-2. 오버레이 창 기하 — 적합 규칙 / 창 장식 / 진동 가드 (2026-09-01 추가)
        // ====================================================================

        /// <summary>
        /// 창 기하 적합 규칙(<c>OverlayBoundsFitPolicy</c>)을 <b>양쪽 Enforcer가 실제로 호출</b>하는가.
        ///
        /// <para>이번 라운드의 출발 가설은 "이 정책을 호출하는 곳은 Windows 하나뿐"이었는데
        /// <b>사실이 아니었다</b>(macOS Enforcer도 이미 호출하고 있었다). 그 오판이 조사를 엉뚱한
        /// 방향으로 보냈으므로, 앞으로는 사람이 눈으로 확인하지 않고 이 테스트가 대답하게 한다.</para>
        /// </summary>
        [Test]
        public void 창기하_적합_규칙을_양_플랫폼_Enforcer가_모두_부른다()
        {
            foreach (string call in new[]
            {
                "OverlayBoundsFitPolicy.ShouldSetResolution(",
                // ★ 크기 재대입은 **수명 상한이 붙은** 변형을 써야 한다(2026-09-01). 상한 없는
                //   ShouldResize를 직접 부르면 Screen.SetResolution만 조여진 비대칭으로 되돌아간다.
                "OverlayBoundsFitPolicy.ShouldResizeWithinBudget(",
                "OverlayBoundsFitPolicy.ShouldMove(",
            })
            {
                foreach (string path in new[] { MacEnforcerPath, WinEnforcerPath })
                {
                    StringAssert.Contains(call, StripLineComments(ReadSource(path)),
                        $"{Path.GetFileName(path)}가 \"{call}\"을 부르지 않습니다 — 그 플랫폼에는 " +
                        "불감대/호출 상한이 존재하지 않는 것과 같고, 창 기하 재적용이 무제한이 됩니다. " +
                        "(재생성 호출은 두 종류다: Screen.SetResolution과 창 크기 재대입. " +
                        "둘 다 OS 표면 재생성이므로 둘 다 수명 상한 안에 있어야 한다.)");
                }
            }
        }

        /// <summary>
        /// 창 기하 A↔B <b>진동</b> 가드가 양쪽 Enforcer에 배선돼 있는가.
        ///
        /// <para>불감대(<c>OverlayBoundsFitPolicy</c>)는 <b>1px 래칫</b>만 막는다. 값이 두 값 사이를
        /// 오가면 둘 다 불감대 밖이라 "불일치"가 매번 참이고 재적용이 영원히 계속된다. 2026-09-01
        /// 맥 실기에서 오버레이 창 사각형이 <c>(0,0,1512,982)</c> ↔ <c>(0,33,1512,1010)</c>로 교대한
        /// 것이 그 모양이다. 규칙은 플랫폼 중립 <c>OverlayGeometryOscillationGuard</c>에 있고,
        /// <b>양쪽이 실제로 부르는지</b>를 여기서 잠근다.</para>
        /// </summary>
        [Test]
        public void 창기하_진동_가드가_양_플랫폼_Enforcer에_모두_배선되어_있다()
        {
            AssertBothContain(MacEnforcerPath, WinEnforcerPath,
                "new OverlayGeometryOscillationGuard()",
                "진동 가드가 없습니다 — 창 기하가 두 값 사이를 오가면 그 플랫폼은 재적용을 " +
                "영원히 반복하고, 재적용 한 번이 곧 OS 표면 재생성(수백 ms 정지) 한 번입니다.");

            foreach (string path in new[] { MacEnforcerPath, WinEnforcerPath })
            {
                string src = StripLineComments(ReadSource(path));
                StringAssert.Contains(".Observe(", src,
                    $"{Path.GetFileName(path)}가 가드를 만들기만 하고 관측하지 않습니다.");
                StringAssert.Contains("IsOscillating", src,
                    $"{Path.GetFileName(path)}가 진동 확정 뒤에도 재적용/재무장을 계속합니다 — " +
                    "확정 자체가 아무것도 멈추지 못하면 가드가 아닙니다.");
            }
        }

        /// <summary>
        /// 오버레이 창 사각형을 <c>ScreenCoordinateConverter</c>에 보고할 때, 양 플랫폼 모두
        /// <b>OS의 frame 사각형이 아니라 콘텐츠(시각) 사각형</b>을 쓰는가.
        ///
        /// <para>같은 결함, 다른 OS 메커니즘이라 <b>수단은 다르고 성질은 같다</b>:</para>
        /// <list type="bullet">
        ///   <item>Windows — <c>TryGetVisualWindowRect</c>(DWM 확장 프레임). 이미 있었다.</item>
        ///   <item>macOS — <c>OverlayContentRectPolicy.TryStripTopDecoration</c>(타이틀바 28pt 제거).
        ///         2026-09-01까지 <b>없었다</b>: 창이 보더리스에서 빠지는 순간 원점이 28pt 위로,
        ///         높이가 28pt 크게 보고되어 발판/커서 판정이 통째로 어긋났다.</item>
        /// </list>
        /// <para>Windows 쪽 주석이 이 인과("보더리스가 아직 적용되지 않은 기동 직후 몇 프레임에는
        /// GetWindowRect가 보이지 않는 테두리를 포함해…")를 <b>이미 정확히</b> 적고 있었는데도
        /// macOS에는 대응물이 없었다 — CLAUDE.md가 경고하는 그 실패 모드 그대로다.</para>
        /// </summary>
        [Test]
        public void 오버레이_사각형_보고가_양_플랫폼_모두_창장식을_걷어낸다()
        {
            StringAssert.Contains("OverlayContentRectPolicy.TryStripTopDecoration(",
                StripLineComments(ReadSource(MacWindowServicePath)),
                "MacWindowService가 kCGWindowBounds(frame)를 그대로 보고합니다 — 창에 타이틀바가 " +
                "붙는 순간 좌표계가 28pt 어긋납니다.");

            StringAssert.Contains("TryGetVisualWindowRect(",
                StripLineComments(ReadSource(WinWindowServicePath)),
                "Win32WindowService가 GetWindowRect 원본을 그대로 보고합니다 — 같은 부류의 " +
                "어긋남이 Windows에서 재발합니다.");
        }

        /// <summary>
        /// <b>자기 창 판정</b>(= 오버레이 원점/배율의 출처)은 양 플랫폼 모두 <b>프로세스 ID</b>로만
        /// 해야 한다. 같은 앱의 <b>두 번째 인스턴스</b>는 창 소유자 <b>이름</b>이 정확히 같으므로,
        /// 이름을 판정에 쓰면 남의 프로세스 창이 우리 좌표계를 덮어쓴다.
        ///
        /// <para>2026-09-01 macOS에서 실제로 그랬다(<c>IsSelfWindow</c>의 이름 폴백). Windows는
        /// 원래부터 PID 단독이었고 오버레이 원점 출처도 자기 프로세스의 <c>_overlayHwnd</c>뿐이라
        /// 같은 결함이 없었다 — 이번에는 <b>macOS가 뒤처진 쪽</b>이었다.</para>
        /// </summary>
        [Test]
        public void 자기창_판정은_양_플랫폼_모두_PID로_한다()
        {
            string mac = StripLineComments(ReadSource(MacWindowServicePath));
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            StringAssert.Contains("IsSelfProcessWindow(", mac,
                "macOS의 좌표계 출처 판정이 PID 단독 함수로 분리돼 있지 않습니다 — 같은 이름의 " +
                "두 번째 인스턴스 창이 오버레이 원점/배율을 덮어씁니다.");
            Assert.IsFalse(mac.Contains("private bool IsSelfWindow("),
                "이름 폴백을 포함한 옛 판정이 남아 있습니다.");

            StringAssert.Contains("pid == _currentProcessId", win,
                "Windows의 자기 창 제외가 PID 비교가 아닙니다 — 이름/제목 기반으로 바뀌면 macOS가 " +
                "겪은 것과 같은 사고가 그대로 생깁니다.");
        }

        // ====================================================================
        // 4. 발판 열거 — 가려짐/필터 계산의 공용 본체
        // ====================================================================

        /// <summary>
        /// 가려짐(오클루전) 계산 본체는 두 플랫폼이 <b>같은 클래스</b>를 써야 한다.
        /// 이 규칙이 깨진 것이 <c>VisibleTopEdgeSolver</c>를 뽑아내게 만든 원래 사고다.
        /// </summary>
        [Test]
        public void 가려짐_계산은_양_플랫폼이_같은_공용_클래스를_쓴다()
        {
            AssertBothContain(MacWindowServicePath, WinWindowServicePath,
                "VisibleTopEdgeSolver",
                "가려짐 계산을 플랫폼 안에서 자체 구현하고 있습니다 — 한쪽만 고치면 반대쪽에서 " +
                "같은 버그가 그대로 살아남습니다.");
        }

        /// <summary>
        /// 발판 진단(원본 창 수 / 완전히 가려진 창 수 / 사유별 집계)은 두 플랫폼 모두 있어야 한다.
        /// 이게 한쪽에만 있으면 "Windows에서만 캐릭터가 허공에 선다"류 신고를 원격에서 특정할 수 없다.
        /// </summary>
        [Test]
        public void 발판_진단_채널이_양_플랫폼에_모두_있다()
        {
            AssertBothContain(MacWindowServicePath, WinWindowServicePath,
                "AppendWindowDiagnostics",
                "발판 진단 출력이 없습니다 — 그 플랫폼의 발판 문제는 원격 진단이 불가능해집니다.");

            AssertBothContain(MacWindowServicePath, WinWindowServicePath,
                "LastFullyOccludedWindowCount",
                "'완전히 가려져 제외된 창 수' 카운터가 없습니다 — 발판이 0개가 된 이유를 구분할 수 없습니다.");
        }

        // ====================================================================
        // 5. 하단 예약 막대(macOS Dock / Windows 작업표시줄) 회피
        // ====================================================================

        /// <summary>
        /// 하단 막대 회피 정책은 <b>두 플랫폼의 막대를 모두</b> 본다.
        ///
        /// <para>★ 2026-09-01 — 이 항목의 <b>검사 대상이 바뀌었다</b>. 원래는 좌하단 호버 패널
        /// (<c>Interaction/CornerHoverPanel.cs</c>)을 직접 읽어 "macOS Dock만 피하고 Windows
        /// 작업표시줄은 안 피한다"는 갭을 잡던 항목이었는데, 그 패널이 사용자 요청으로 <b>삭제</b>됐다.
        /// 표면이 사라졌다고 항목을 조용히 지우면 CLAUDE.md가 금지한 "잊히는 갭"이 되므로,
        /// 같은 사실을 담고 있는 <b>플랫폼 중립 정책</b>(<see cref="BottomSafetyNetPolicy"/>)으로
        /// 대상을 옮겨 계속 감시한다. 이쪽이 원래 있어야 할 자리이기도 하다 — 정책은
        /// <c>Platform/</c>에 있고 플랫폼 전용 코드는 사실 조회만 한다(<c>FullscreenSuspendPolicy</c>
        /// 사고의 교훈).</para>
        ///
        /// <para>★★ 2026-09-01 밤 (디버거) — 위 "대상을 옮겼다"가 <b>절반만 옮겨졌다.</b>
        /// 검사 대상 경로만 <c>BottomSafetyNetPolicy.cs</c>로 바꾸고 단언 두 줄을 그대로 뒀는데,
        /// 그 파일은 <b>일부러 순수 함수</b>다 — <c>hasDock/dockLeftOsX/dockRightOsX</c>와
        /// <c>hasScreenBounds/screenLeft/Right/Bottom</c>을 <b>인자로 받는다</b>(그 파일의
        /// "왜 별도 파일인가 (2) 순수 함수라 테스트가 잡을 수 있다" 문단이 그 이유다).
        /// 서비스 인터페이스 이름이 거기 있으면 오히려 설계 위반이다. 그래서:
        /// <list type="bullet">
        ///  <item><c>IDockMetricsService</c> 단언은 <b>실패</b>했다(2026-09-01 20:41 기준선부터 빨간불).</item>
        ///  <item><c>IReservedBottomBarService</c> 단언은 <b>XML 주석에만</b> 그 이름이 있어서
        ///        통과했다 — 이 파일이 다른 곳에서 <see cref="StripLineComments"/>로 막고 있는
        ///        바로 그 <b>거짓 초록</b>이다("결함을 설명하는 주석이 구현으로 오인된다").</item>
        /// </list>
        /// 원인은 플랫폼 갭이 아니라 <b>검사 대상 오지정</b>이었다. 사실 조회를 실제로 하는 곳은
        /// 플랫폼 중립 데코레이터 <c>FallbackPlatformWindowService</c>이고, 거기서 두 서비스를
        /// 받아 정책에 넘긴다. 그래서 이 검사를 <b>두 층</b>으로 나눈다:
        /// (1) 사실 조회 — 데코레이터가 양 플랫폼 서비스를 모두 소비하는가,
        /// (2) 판정 — 정책이 중립 위치에 있고 실제로 호출되는가.
        /// 두 층 모두 주석을 걷어낸 뒤에 본다.</para>
        /// </summary>
        [Test]
        public void 하단막대_회피_정책이_양_플랫폼_막대를_모두_본다()
        {
            // ---- (1) 사실 조회: 양 플랫폼의 하단 막대를 **둘 다** 받아오는가 ----
            // 이 데코레이터는 플랫폼 중립이고 두 서비스를 `inner as ...`로 받는다. 한쪽을 빼면
            // 그 플랫폼의 막대가 조용히 무시된다(= 예전 CornerHoverPanel 갭과 같은 형태).
            string factsPath = Path.Combine(PlatformRoot, "FallbackPlatformWindowService.cs");
            string facts = StripLineComments(ReadSource(factsPath));

            StringAssert.Contains("IDockMetricsService", facts,
                "macOS Dock 실측 경로가 없습니다 — Dock 가로 구간을 모르면 안전망에 구멍을 못 뚫어 " +
                "캐릭터가 Dock 아래를 걸어다닙니다(2026-08-29 신고 \"독과 겹쳐서 걸음\").");
            StringAssert.Contains("IReservedBottomBarService", facts,
                "Windows 작업표시줄 실측 경로가 없습니다 — 하단 막대의 정확한 사각형은 " +
                "Win32WindowService가 IReservedBottomBarService로 이미 실측해 내놓고 있습니다.");

            // ---- (2) 판정: 중립 위치에 있고, 실제로 호출되는가 ----
            // 정책이 플랫폼 전용 폴더로 내려가면 반대쪽 플랫폼이 **물리적으로** 부를 수 없다.
            string policyPath = Path.Combine(PlatformRoot, "BottomSafetyNetPolicy.cs");
            Assert.IsTrue(File.Exists(policyPath),
                "하단 막대 정책이 Platform/ 중립 위치에서 사라졌습니다 — FullscreenSuspendPolicy가 " +
                "Platform/MacOS/ 안에 있어 Windows가 못 부르던 그 사고와 같은 형태입니다.");

            // 존재만으로는 부족하다. 아무도 안 부르는 정책은 없는 것과 같다.
            StringAssert.Contains("BottomSafetyNetPolicy.Resolve(", facts,
                "정책 파일은 있는데 아무도 호출하지 않습니다 — 안전망이 예전처럼 화면 밖/막대 뒤로 " +
                "삐져나가도 이 정책은 한 번도 실행되지 않습니다.");

            // 정책은 **순수 함수**여야 한다: 사실은 인자로 받고 서비스를 직접 잡지 않는다.
            // (이 단언이 위 두 줄과 반대 방향인 것이 핵심이다 — 예전 판은 여기서 서비스 이름을
            //  요구하다가 실패했다.)
            string policy = StripLineComments(ReadSource(policyPath));
            StringAssert.DoesNotContain("IDockMetricsService", policy,
                "정책이 플랫폼 서비스를 직접 잡고 있습니다 — 그러면 EditMode에서 '모니터 밖 2pt 조각' " +
                "같은 회귀를 재현할 수 없게 되어(순수 함수가 아니게 되어) 실기에서만 드러납니다.");
            StringAssert.DoesNotContain("IReservedBottomBarService", policy,
                "정책이 플랫폼 서비스를 직접 잡고 있습니다 — 위와 같은 이유로 금지입니다.");
        }

        // ====================================================================
        // 6. 아직 남은 패리티 결함 — 실패시키지 않고 '무시(Ignored)'로 눈에 띄게 남긴다
        // ====================================================================

        /// <summary>
        /// <b>전체화면 자동 숨김은 "게임"에만 걸려야 한다</b> — 양 플랫폼 모두.
        ///
        /// <para>이 테스트는 2026-09-01까지 <c>Assert.Ignore</c>로 "알려진 패리티 결함"으로만 떠 있었다.
        /// macOS는 2026-08-31에 <c>LSApplicationCategoryType</c> 필터를 넣어 고쳤는데, <b>정작 사용자가
        /// 신고한 Windows는 기하 판정만 남아</b> 전체화면 Excel/PowerPoint/브라우저에서도 캐릭터가
        /// 계속 사라졌다. 신고 원문(2026-08-31): "엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가
        /// 없어져버림". 같은 날 Windows 구현(<c>WindowsGameProcessProbe</c> + 공용
        /// <c>WindowsGameExecutablePolicy</c>)이 들어오면서 정식 검사로 승격했다.</para>
        ///
        /// <para>검사는 세 겹이다: (1) 각 플랫폼이 게임 여부를 <b>실제로 조회</b>하는가,
        /// (2) 판정 <b>규칙</b>은 플랫폼 중립 파일에 있는가, (3) Windows가 기하 판정 결과를
        /// <b>그대로 돌려주지 않는가</b>(그게 정확히 이 버그의 형태였다).</para>
        /// </summary>
        [Test]
        public void 전체화면_숨김은_양_플랫폼_모두_게임일_때만_건다()
        {
            // 주석을 걷어낸 뒤 "실제 호출"만 본다(위 StripLineComments 문서 — 결함을 설명하는 주석
            // 자체가 구현으로 오인되던 함정).
            string mac = StripLineComments(ReadSource(MacWindowServicePath));
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            StringAssert.Contains("FullscreenGameCategory.IsGameCategory(", mac,
                "macOS가 전경 앱의 카테고리를 확인하지 않고 기하만으로 숨깁니다 — " +
                "전체화면 키노트/브라우저에서 캐릭터가 사라집니다(2026-08-31 신고 버그).");

            StringAssert.Contains("IsGameProcess(", win,
                "Windows가 전경 프로세스의 게임 여부를 확인하지 않고 기하만으로 숨깁니다 — " +
                "전체화면 엑셀/PPT/브라우저에서 캐릭터가 사라집니다(2026-08-31 사용자 신고 원문: " +
                "\"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림\").");

            // 규칙은 두 플랫폼 모두 플랫폼 중립 파일에 있어야 한다 — Windows 폴더 안에 규칙을 복제하면
            // macOS 개발 머신에서 영원히 검증되지 않는다(이 프로젝트가 이미 세 번 겪은 실패 구조).
            string policy = ReadSource(Path.Combine(PlatformRoot, "FullscreenSuspendPolicy.cs"));
            StringAssert.Contains("class FullscreenGameCategory", policy,
                "macOS 게임 판정 규칙이 플랫폼 중립 파일에 없습니다.");
            StringAssert.Contains("class WindowsGameExecutablePolicy", policy,
                "Windows 게임 판정 규칙이 플랫폼 중립 파일에 없습니다.");

            bool windowsCallsSharedRule = false;
            foreach (string f in Directory.GetFiles(Path.Combine(PlatformRoot, "Windows"), "*.cs"))
            {
                if (StripLineComments(File.ReadAllText(f))
                    .Contains("WindowsGameExecutablePolicy.IsRegisteredGameExecutable("))
                {
                    windowsCallsSharedRule = true;
                    break;
                }
            }
            Assert.IsTrue(windowsCallsSharedRule,
                "Platform/Windows/ 어디에서도 공용 순수 규칙(WindowsGameExecutablePolicy)을 부르지 " +
                "않습니다 — Windows 안에서 자체 판정을 하고 있다면 EditMode 테스트가 그 규칙을 " +
                "검증할 수 없습니다.");

            // 이 버그의 정확한 형태: 기하 판정을 그대로 반환하는 코드. 되살아나면 여기서 막는다.
            Assert.IsFalse(win.Contains("return match;"),
                "Win32WindowService가 기하 판정(match)을 그대로 전체화면 판정으로 돌려줍니다 — " +
                "그것이 2026-08-31 사용자 신고 버그의 정확한 형태입니다. 기하 일치 이후 " +
                "'게임인가'를 한 번 더 물어야 합니다.");
        }

        /// <summary>
        /// <b>미해결 갭</b>: macOS는 <c>MacSpaceBehaviorNative</c>로 "모든 Space에 따라붙기"를 걸어
        /// 타 앱 전체화면 위에서도 캐릭터가 남게 했다. Windows의 대응 개념은 <b>가상 데스크톱</b>인데
        /// 대응물이 없어, Windows 사용자가 데스크톱 2로 전환하면 캐릭터가 데스크톱 1에 남는다.
        ///
        /// <para><b>★ 2026-09-02 사유 갱신.</b> 이 항목의 예전 사유는 "리더 배정 대기"였고 그것은
        /// 지금도 맞다. 바뀐 것은 <b>우선순위와 성격</b>이다 — 사용자 지시가
        /// "맥에 적용한 사항 윈도우에도 모두 적용"으로 바뀌었으므로 더 이상 "나중에" 항목이 아니고,
        /// 동시에 이것은 <b>한 줄 추가로 닫히는 항목이 아니다</b>(아래 Ignore 본문의 정책 갈래 참고).</para>
        /// </summary>
        [Test]
        public void 미해결_Windows에는_가상데스크톱_동행_배선이_없다()
        {
            string winDir = Path.Combine(PlatformRoot, "Windows");
            string[] winFiles = Directory.GetFiles(winDir, "*.cs");

            // ★ 비공허성 잠금: 폴더가 비거나 이름이 바뀌면 아래 루프가 **아무 파일도 안 보고** 지나가고,
            //   그 상태의 Ignore는 "확인했다"가 아니라 "아무것도 안 봤다"다.
            Assert.Greater(winFiles.Length, 0,
                $"Platform/Windows/에서 .cs를 한 개도 읽지 못했습니다({winDir}) — 스캔이 공허합니다(거짓 초록).");

            foreach (string f in winFiles)
            {
                if (StripLineComments(File.ReadAllText(f)).Contains("IVirtualDesktopManager"))
                {
                    Assert.Pass($"{Path.GetFileName(f)}에 가상 데스크톱 배선이 들어왔습니다 — " +
                        "이 테스트를 정식 검사로 승격하세요(기반 목록/실호출까지 보는 " +
                        "AssertDeclaresInterface 형태를 쓸 것 — 이름만 보면 주석에 걸립니다).");
                }
            }

            Assert.Ignore("【미해결 갭 · 배정 대기】 사유 갱신 2026-09-02 04:36 (재확인 완료)\n" +
                "macOS: 해결됨 — MacSpaceBehaviorNative(.canJoinAllSpaces + .stationary + accessory 등급).\n" +
                $"Windows: 미해결 — Platform/Windows/ {winFiles.Length}개 파일 어디에도 " +
                "IVirtualDesktopManager 참조가 없다(주석 제외 후 0건).\n" +
                "★ 보류 사유가 바뀌었다: 예전에 이 파일의 다른 항목들이 근거로 삼던 사용자 지시 " +
                "'윈도우는 일단 미루고 맥만'(2026-09-01)은 2026-09-02 '맥에 적용한 사항 윈도우에도 " +
                "모두 적용'으로 뒤집혔다. 이 항목의 사유는 원래부터 '리더 배정 대기'였으므로 사유 자체는 " +
                "유효하지만 우선순위가 올라갔다.\n" +
                "★ 이것은 '한 줄 추가' 항목이 아니다 — 먼저 필요한 것은 코드가 아니라 정책 판단이다:\n" +
                "  (a) 소속만 확인한다: IVirtualDesktopManager::IsWindowOnCurrentVirtualDesktop은 공개 COM이다. " +
                "남의 데스크톱에 있는 동안 스스로 숨으면 비침해 원칙 2와 같은 방향이고 공개 API만으로 된다. " +
                "다만 macOS의 .canJoinAllSpaces와 '결과'가 다르다(따라붙지 않고 사라진다).\n" +
                "  (b) 모든 데스크톱에 고정한다: 이쪽이 macOS와 같은 결과지만 비공개 API가 필요하고 " +
                "OS 업데이트마다 깨진다. 원칙 2(비침해)와도 긴장 관계다.\n" +
                "리더 판단 대상. 실기 검증 필요 — 사용자 Windows 머신에서 Win+Ctrl+←/→로 데스크톱을 " +
                "전환하며 캐릭터가 따라오는지 본다.");
        }

        // ============================================================================
        // C4 — 데스크톱 표시(Show Desktop) 면제 (2026-09-01 · macOS 해결 / Windows 미배정)
        // ============================================================================
        // 사용자 신고: "바탕화면을 클릭하거나 F11을 누르면 캐릭터·펫·톱니가 통째로 사라진다."
        // 대조 실험으로 확정된 원인: OS가 우리 창을 화면 밖으로 **밀어냈다**(사라진 게 아니다).
        // 두 플랫폼에 같은 개념의 구멍이 있고, 사용자 지시("윈도우는 미루고 맥만")에 따라 이번
        // 라운드는 macOS만 고쳤다. Windows 쪽은 아래에서 Ignore로 계속 눈에 띄게 남긴다.

        private static string MacSpaceBehaviorPath =>
            Path.Combine(PlatformRoot, "MacOS", "MacSpaceBehaviorNative.cs");

        /// <summary>
        /// macOS 쪽 <b>정식 검사</b>: 오버레이 창의 <c>collectionBehavior</c>에 <c>.stationary</c>가
        /// 반드시 들어 있어야 한다.
        ///
        /// <para>실측 근거(대조 실험, 재현 2회): 우리와 동일 조건(accessory, borderless, level=101)의 창을
        /// 띄우고 F11을 토글하면 <c>0x101</c>(stationary 없음)은 화면 밖으로 밀려나고
        /// <c>0x111</c>(stationary 있음)은 미동도 없다. 그 밀림이 그대로 오버레이 원점 오염
        /// (<c>origin=(0,-937)</c>)으로 이어져 발판 좌표계 전체가 한 화면만큼 어긋났다.</para>
        ///
        /// <para>함께 잠그는 것: <c>.managed</c>/<c>.transient</c>는 <c>.stationary</c>와 상호 배타
        /// 그룹이라 <b>반드시 꺼야</b> 한다. 켜진 채로 남으면 어느 쪽이 이기는지가 미정의다 — 이 파일이
        /// 아니라 그 소스 자신의 주석이 예전부터 세워 둔 규칙이다.</para>
        /// </summary>
        [Test]
        public void 데스크톱표시_면제가_macOS_창플래그에_실제로_걸려_있다()
        {
            string mac = StripLineComments(ReadSource(MacSpaceBehaviorPath));

            StringAssert.Contains("NSWindowCollectionBehaviorStationary", mac,
                "오버레이 창에 .stationary 비트가 없습니다 — 데스크톱 표시(F11)/Exposé에서 macOS가 " +
                "우리 창을 화면 밖으로 밀어내고, 그 좌표가 그대로 보고되어 캐릭터가 사라진 것처럼 보입니다.");

            // 켜는 쪽(Required)에 실제로 들어갔는지. 비트를 선언만 하고 안 쓰면 아무 일도 일어나지 않는다.
            int required = mac.IndexOf("RequiredBehavior", System.StringComparison.Ordinal);
            Assert.Greater(required, 0, "RequiredBehavior 정의를 찾지 못했습니다.");
            int forbidden = mac.IndexOf("ForbiddenBehavior", System.StringComparison.Ordinal);
            Assert.Greater(forbidden, required, "ForbiddenBehavior 정의를 찾지 못했습니다.");
            string requiredBlock = mac.Substring(required, forbidden - required);
            StringAssert.Contains("NSWindowCollectionBehaviorStationary", requiredBlock,
                "비트를 선언만 하고 RequiredBehavior에 넣지 않았습니다 — 창에는 아무것도 걸리지 않습니다.");

            string forbiddenBlock = mac.Substring(forbidden);
            StringAssert.Contains("NSWindowCollectionBehaviorTransient", forbiddenBlock,
                ".transient를 끄지 않았습니다 — .stationary와 상호 배타 그룹이라 둘이 함께 켜지면 " +
                "동작이 미정의가 됩니다(그 소스 자신의 주석이 세운 규칙).");
            StringAssert.Contains("NSWindowCollectionBehaviorManaged", forbiddenBlock,
                ".managed를 끄지 않았습니다 — 위와 같은 이유입니다.");
        }

        /// <summary>
        /// <b>미해결 갭</b>: Windows에는 macOS <c>.stationary</c>에 대응하는 "데스크톱 표시 면제"가
        /// 없고, 오히려 <b>더 나쁘다</b>.
        ///
        /// <para>구조: <c>Win32WindowService.CaptureOverlayOrigin()</c>이 자기 창에 대해
        /// <c>IsIconic</c>을 확인하지 않는다. 같은 파일이 "최소화된 창은 <c>(-32000,-32000)</c>을
        /// 돌려준다"고 스스로 적어 두고 그 필터를 <b>남의 창에만</b> 적용한다
        /// (<c>WindowsFootholdRejection.Minimized</c>). Win+D가 우리 창을 최소화하면 그 값이
        /// <b>안정된 값</b>으로 들어오고, 플랫폼 중립 코드의 연속 확인
        /// (<c>ScreenCoordinateConverter.OffDesktopConfirmReports</c>)이 2회 만에 받아들인다.
        /// macOS와 달리 <b>폭까지 오염되어 <c>AutoDpiScale</c>도 함께 깨진다.</b></para>
        ///
        /// <para><b>★ 2026-09-02 사유 갱신 — 보류 사유가 사라졌다.</b> 예전 사유는 기술적 판단이 아니라
        /// 사용자 지시("윈도우는 일단 미루고 맥만 중점적으로 고쳐줘", 2026-09-01)였는데, 그 지시가
        /// 같은 밤 "맥에 적용한 사항 윈도우에도 모두 적용"으로 <b>뒤집혔다</b>. 낡은 사유를 그대로
        /// 두면 그 문장 자체가 거짓말이 되므로 갱신한다 — 지금 이것은 <b>보류가 아니라 배정 대기</b>다.</para>
        /// </summary>
        [Test]
        public void 미해결_Windows에는_데스크톱표시_최소화_면제가_없다()
        {
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            // ★ 앵커 잠금: 이름이 바뀌면 아래 승격 조건이 **영원히 성립하지 않아**, 갭이 고쳐져도
            //   러너는 계속 "건너뜀"만 보여 준다. 그건 이 감사가 막으려는 상태 그 자체다.
            int start = win.IndexOf("private void CaptureOverlayOrigin()", System.StringComparison.Ordinal);
            Assert.Greater(start, 0,
                "감사 앵커가 낡았습니다 — Win32WindowService에서 CaptureOverlayOrigin() 선언을 찾지 " +
                "못했습니다. 이름이 바뀌었다면 여기도 함께 갱신하세요. 그대로 두면 이 항목은 " +
                "'고쳐져도 영원히 건너뜀'이 됩니다.");

            int end = win.IndexOf("private ", start + 1, System.StringComparison.Ordinal);
            string body = end > start ? win.Substring(start, end - start) : win.Substring(start);
            if (body.Contains("IsIconic("))
            {
                Assert.Pass("CaptureOverlayOrigin()에 자기 창 최소화 검사가 들어왔습니다 — " +
                    "이 테스트를 정식 검사로 승격하세요.");
            }

            Assert.Ignore("【미해결 갭 · 보류 사유 소멸 → 배정 대기】 사유 갱신 2026-09-02 04:36 (재확인 완료)\n" +
                "항목: 데스크톱 표시(Show Desktop) / Exposé 면제.\n" +
                "macOS: 해결됨 — MacSpaceBehaviorNative의 collectionBehavior에 .stationary(0x10)를 " +
                "추가하고 상호 배타 비트(.managed/.transient)를 껐다. 목표 0x111. 위 " +
                "데스크톱표시_면제가_macOS_창플래그에_실제로_걸려_있다()가 그 사실을 잠근다.\n" +
                "Windows: 미해결 — CaptureOverlayOrigin() 본문에 IsIconic(_overlayHwnd) 검사가 " +
                "여전히 없다(오늘 본문 스캔으로 재확인). 같은 파일이 '최소화 창은 (-32000,-32000)을 " +
                "돌려준다'고 적어 두고 그 필터를 남의 창에만 적용한다. macOS와 달리 폭까지 오염되어 " +
                "AutoDpiScale도 함께 깨진다.\n" +
                "★ 보류 사유가 사라졌다: 근거였던 사용자 지시 '윈도우는 일단 미루고 맥만'(2026-09-01)이 " +
                "2026-09-02 '맥에 적용한 사항 윈도우에도 모두 적용'으로 뒤집혔다. 지금은 배정 대기다.\n" +
                "처방 후보(작다): CaptureOverlayOrigin() 진입부에서 IsIconic(_overlayHwnd)이면 즉시 " +
                "return(= 직전 유효 원점/배율 유지). P/Invoke 선언은 같은 파일에 이미 있어 새 선언이 " +
                "0줄이다.\n" +
                "실기 검증 필요 — 사용자 Windows 머신에서 Win+D 후 다시 Win+D.");
        }

        // ============================================================================
        // C4 — ★ 획 하한(2pt)의 월드 환산이 <b>DPI에 의존</b>한다 (2026-09-01 신설 · 미해결)
        // ============================================================================

        /// <summary>
        /// <b>구조는 닫혔고 Windows 실측만 남았다</b>(2026-09-02 사유 분리) — 화면상 최소 획 두께
        /// (<c>StickConfig.MinStrokeScreenPoints</c> = 2pt)를 <b>월드 유닛으로 바꾸는 환산</b>이
        /// 화면 DPI/카메라 크기에 의존하고, 그 결과가 <b>기하학 안전 한계를 좌우</b>한다.
        ///
        /// <para><b>★ 왜 이름을 바꿨나(옛 이름: 미해결_획_하한의_월드_환산이_Windows_DPI에서_검증되지_않았다).</b>
        /// 옛 이름은 <b>구조 갭과 실측 부재를 한 단어로 뭉뚱그렸다</b>. 둘은 성격이 다르고 닫히는 방법도
        /// 다르다 — 구조는 코드로 닫고 실측은 하드웨어로 닫는다. 하나로 두면 절반이 닫혔다는 사실이
        /// 목록에서 사라진다. 그래서 <c>실기미확인_</c> 접두사로 옮겨 "코드로는 더 할 일이 없다"를
        /// 이름 자체가 말하게 한다.</para>
        ///
        /// <para><b>[닫힌 절반 — 구조]</b> 진단 환산이 <c>Platform/MacOS/MacOverlayStateEnforcer</c>
        /// 안에 있었고(= Windows가 물리적으로 같은 숫자를 낼 수 없었다), 거기서 <c>× lossyScale.x</c>를
        /// 곱하는 오류까지 함께 있어 로그가 "하한 2pt 미달"이라는 <b>정반대 결론</b>을 냈다. 둘 다
        /// 플랫폼 중립 <c>Platform/StrokeWidthDiagnostics</c>로 옮겨 고쳤다. 아래 단언들이 그 사실을
        /// <b>양방향으로</b> 얼린다(플랫폼 폴더에 "없다" + 중립 위치에 "있다").</para>
        ///
        /// <para><b>[열린 절반 — 실측]</b> <c>StickmanAgent.ResolveMinStrokeWorldWidth()</c>는 카메라
        /// 직교 크기와 <b>화면 높이(포인트)</b>로 환산한다. Windows 표시 배율 100% / 125% / 150%에서
        /// "포인트"의 정의가 달라지면 같은 2pt가 다른 월드 폭이 되고, 규칙 B(안쪽 오프셋 자기교차 금지)
        /// 위반이 시작되는 <b>임계 배율</b>(macOS Retina 실측: 다리 0.451 / 팔 0.398)도 함께 움직인다.
        /// macOS는 Retina 2배가 사실상 고정이라 이 축이 없다. <b>코드 갭이 아니라 하드웨어가 없다</b>가
        /// 정확한 상태다.</para>
        /// </summary>
        [Test]
        public void 실기미확인_획_하한의_월드_환산이_Windows_DPI에서_실측되지_않았다()
        {
            // ---- [닫힌 절반 ①] 판정 로직이 플랫폼 전용 폴더로 이사하면 즉시 실패시킨다 ----
            string macRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform", "MacOS");
            Assert.IsTrue(Directory.Exists(macRoot),
                $"macOS 전용 폴더를 찾지 못했습니다({macRoot}) — 폴더가 이름을 바꾸면 아래 스캔이 " +
                "**아무 파일도 안 보고 통과**합니다(거짓 초록). 경로를 갱신하세요.");

            string[] macFiles = Directory.GetFiles(macRoot, "*.cs", SearchOption.AllDirectories);
            // ★ 비공허성 잠금: 스캐너가 눈이 멀어도 "없다"는 단언은 초록이다. 실제로 파일을 봤는지 먼저 박는다.
            Assert.Greater(macFiles.Length, 0,
                "macOS 전용 .cs를 한 개도 읽지 못했습니다 — 스캔이 공허합니다(거짓 초록).");

            foreach (string file in macFiles)
            {
                string src = StripLineComments(File.ReadAllText(file));
                Assert.IsFalse(src.Contains("MinStrokeScreenPoints"),
                    $"획 하한 환산이 {Path.GetFileName(file)}(macOS 전용)로 옮겨갔습니다 — " +
                    "Windows가 물리적으로 호출할 수 없는 자리입니다(FullscreenSuspendPolicy 사고와 같은 형태). " +
                    "정책은 플랫폼 중립 위치에 두고 플랫폼 코드는 사실 조회만 하세요.");
            }

            // ---- [닫힌 절반 ②] "없다"만 두면 누가 지워도 초록이다. 옮겨간 자리를 함께 얼린다 ----
            Type neutral = typeof(StrokeWidthDiagnostics);
            Assert.AreEqual("StickMate.Platform", neutral.Namespace,
                "획 두께 계측기가 플랫폼 중립 네임스페이스를 벗어났습니다 — " +
                "Platform.MacOS/Platform.Windows로 들어가면 같은 갭이 되돌아옵니다.");

            string neutralPath = Path.Combine(Application.dataPath,
                "_Project", "Scripts", "Platform", "StrokeWidthDiagnostics.cs");
            Assert.IsTrue(File.Exists(neutralPath),
                $"플랫폼 중립 계측기 파일이 없습니다({neutralPath}) — 네임스페이스만 중립이고 " +
                "파일이 플랫폼 폴더 안에 있으면 같은 사고가 반복됩니다.");

            string neutralSrc = StripLineComments(File.ReadAllText(neutralPath));
            StringAssert.Contains("MinStrokeScreenPoints", neutralSrc,
                "중립 계측기가 하한 상수를 더 이상 참조하지 않습니다 — 하한 판정이 어딘가로 흩어졌습니다.");

            // ★ 2026-09-02 M6 — 하한이 <b>둘</b>이 됐다(낱선 2.00pt / 채운 도형의 경계선 1.00pt).
            //   계측기가 하나만 나르면 최소값 하나를 하한 하나와 비교하게 되고, 정상적으로 1.18pt인
            //   채움 경계선이 "★ 하한 미달 — 결함"으로 찍힌다. 그 줄은 macOS/Windows가 <b>같은 함수</b>로
            //   만들므로 오진도 양쪽에서 똑같이 난다 — 즉 이것은 플랫폼 갭이 아니라 <b>공유 계측 갭</b>이고,
            //   그래서 여기(패리티 감사)가 잠글 자리가 맞다.
            StringAssert.Contains("MinFillOutlineScreenPoints", neutralSrc,
                "중립 계측기가 채움 경계선 하한을 나르지 않습니다 — Windows 사용자가 보낸 [렌더품질] 줄이 " +
                "정상적으로 얇은 채움 경계선을 결함으로 신고하게 됩니다(그리고 그 신고를 받은 사람이 " +
                "멀쩡한 코드를 고치려고 한 라운드를 씁니다).");
            StringAssert.Contains("FillOutlineStroke", neutralSrc,
                "중립 계측기가 선의 <b>역할</b>을 묻지 않습니다 — 하한이 둘인데 통이 하나면 " +
                "어느 하한과 비교해야 하는지 알 수 없습니다.");

            // macOS 감시자는 '사실 조회 + 출력'만 한다 = 중립 계측기를 부른다.
            string enforcer = StripLineComments(File.ReadAllText(Path.Combine(macRoot, "MacOverlayStateEnforcer.cs")));
            StringAssert.Contains("StrokeWidthDiagnostics", enforcer,
                "MacOverlayStateEnforcer가 중립 계측기를 부르지 않습니다 — 환산을 다시 인라인했을 가능성이 큽니다.");
            Assert.IsFalse(enforcer.Contains("lossyScale"),
                "MacOverlayStateEnforcer가 다시 lossyScale을 곱하고 있습니다 — " +
                "LineRenderer.startWidth는 월드 유닛이라 Transform 스케일을 따라가지 않습니다" +
                "(2026-08-30 실측 / 2026-09-01 배율 0.60 실기 캡처로 재확인).");

            // ---- [닫힌 절반 ③] Windows 쪽도 같은 중립 계측기를 부르는가 ----
            // ★ 이 줄이 2026-09-02에 새로 들어갔다. 앞의 세 단언은 전부 macOS만 봤다 —
            //   "중립으로 옮겼다"의 절반(반대편이 실제로 그것을 쓰는가)이 검사되지 않고 있었다.
            string winEnforcer = StripLineComments(ReadSource(WinEnforcerPath));
            StringAssert.Contains("StrokeWidthDiagnostics", winEnforcer,
                "WindowsOverlayStateEnforcer가 중립 계측기를 부르지 않습니다 — 중립 위치로 옮긴 " +
                "목적의 절반(양쪽이 같은 숫자를 낸다)이 성립하지 않고, 아래 '실측만 남았다'는 서술도 " +
                "거짓이 됩니다(측정할 코드가 그 플랫폼에 없으니까).");

            Assert.Ignore("【실기 미확인 · 구조는 닫힘 / 하드웨어만 남음】 사유 분리 2026-09-02\n" +
                "★ 이 항목은 2026-09-01까지 '미해결 갭'이었지만 지금은 **절반만 열려 있다.** 위 단언들이 " +
                "닫힌 절반을 잠근다 — 진단 환산이 Platform/MacOS/MacOverlayStateEnforcer 안에 있던 것을 " +
                "Platform/StrokeWidthDiagnostics(중립)로 옮겼고, 거기 있던 `× lossyScale.x` 오류도 함께 " +
                "고쳤으며, 양 플랫폼 Enforcer가 모두 그 중립 계측기를 부른다.\n" +
                "[구조] 닫힘 — 코드로 더 할 일이 없다. 판정 로직은 플랫폼 중립이다" +
                "(States/LimbCurveRenderer, Core/StickConfig). 이 항목을 '윈도우 코드 갭'으로 배정하면 " +
                "배정받은 사람이 고칠 것을 찾지 못한다.\n" +
                "[실측] 열림 — 남은 것은 하드웨어다. macOS는 Retina 실측 기준 규칙 B 위반 임계 배율이 " +
                "다리 0.451 / 팔 0.398이고 전 배율 여유 >= 1.05배다. Windows는 표시 배율 100%/125%/150%에서 " +
                "같은 2pt가 다른 월드 폭이 되므로 그 임계 배율이 달라질 수 있는데, 이 개발 머신에는 " +
                "그 화면이 없다.\n" +
                "★ 함께 닫히는 항목: 아래 착지 티어/매달리기 두 항목도 같은 축(Windows에서 1pt가 몇 " +
                "월드 유닛인가)에 걸려 있다. 실측 한 번이 세 항목을 함께 닫는다.\n" +
                "승격 절차: Windows 3종 배율에서 MinStrokeWorldWidth를 실측 -> 그 값으로 " +
                "LimbCurveGeometryTests의 배율 스윕 재실행 -> 여유 > 1.0 확인 후 이 테스트를 정식 검사로 교체.");
        }

        // ============================================================================
        // C3 — 단축키 표기(2026-09-01 <b>해결 · 정식 검사로 승격</b>)
        // ============================================================================

        /// <summary>
        /// 사용자에게 보여주는 단축키 문자열이 <b>macOS 글리프로 하드코딩</b>되어 있었다.
        /// Windows의 실제 조합은 <c>Ctrl+Alt+Win+X</c>다(<c>Win32WindowService.TryGetKeyPressed</c>가
        /// <c>GlobalKey.Command</c>를 <c>VK_LWIN</c>/<c>VK_RWIN</c>으로 읽는다). 즉 Windows 사용자에게는
        /// <b>존재하지 않는 조합</b>이 안내되고 있었다.
        ///
        /// <para>고침은 <c>Core/ShortcutLabel</c> 하나로 모았고, 이 검사는 그 규칙을 잠근다:
        /// <b>런타임 소스의 문자열 리터럴 안에 macOS 글리프가 있으면 안 된다</b>(단일 정의처만 예외).</para>
        ///
        /// <para><b>왜 주석은 세지 않고 리터럴만 세는가</b>: 이 저장소의 문서 주석은 단축키를 자주
        /// 인용한다(<c>AppControlDirector</c>만 6곳). 그것들은 화면에 나가지 않으므로 결함이 아니고,
        /// 오히려 "왜 이 표기인가"를 남기는 좋은 기록이다. 반대로 <c>StripLineComments</c>는 줄 <b>끝</b>에
        /// 붙은 주석을 못 걷어내므로(<c>Settings, // ⌃⌥⌘,</c>) 줄 단위 스캔은 오탐을 낸다.
        /// 그래서 여기서는 문자열 리터럴만 정확히 골라낸다.</para>
        /// </summary>
        [Test]
        public void 단축키_표기가_플랫폼별_단일_정의처를_거친다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string singleSource = Path.Combine(root, "Core", "ShortcutLabel.cs");
            Assert.IsTrue(File.Exists(singleSource),
                "단축키 표기의 단일 정의처(Core/ShortcutLabel.cs)가 없습니다 — " +
                "지우면 각 소비자가 다시 자기 파일에 글리프를 적게 되고, 이 감사는 그것을 막으려고 있습니다.");

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;      // 테스트는 사용자 화면이 아니다
                if (Path.GetFullPath(file) == Path.GetFullPath(singleSource)) continue;

                string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!ContainsGlyphInStringLiteral(lines[i], MacGlyphs)) continue;
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {lines[i].Trim()}");
                }
            }

            Assert.IsEmpty(offenders,
                $"macOS 조합키 글리프가 문자열 리터럴에 직접 적혀 있습니다({offenders.Count}곳) — " +
                "Windows 빌드에서는 존재하지 않는 조합이 사용자에게 안내됩니다.\n  - " +
                string.Join("\n  - ", offenders) +
                "\nCore/ShortcutLabel.Chord(\"X\")로 바꾸십시오.");
        }

        /// <summary>
        /// ★ 위 검사의 <b>공허함 방지</b>. 글리프를 전부 지우고 단축키 안내 자체를 없애도 위 검사는
        /// 초록이다. 그래서 "단일 정의처를 <b>실제로 쓰고 있는가</b>"를 함께 본다.
        /// </summary>
        [Test]
        public void 단축키를_안내하는_화면들이_단일_정의처를_실제로_부른다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var consumers = new[]
            {
                Path.Combine(root, "Core", "ItemCatalog.cs"),
                Path.Combine(root, "Interaction", "CharacterInfoWindow.cs"),
                Path.Combine(root, "Interaction", "SettingsWindow.cs"),
            };

            foreach (string path in consumers)
            {
                StringAssert.Contains("ShortcutLabel.Chord(", StripLineComments(ReadSource(path)),
                    $"{Path.GetFileName(path)}이 단축키 표기를 단일 정의처에서 받지 않습니다 — " +
                    "이 파일들은 사용자에게 조합을 알려 주는 자리라, 표기가 빠지면 " +
                    "'무엇을 눌러야 하는지'가 화면에서 사라집니다.");
            }
        }

        /// <summary>
        /// Windows 표기가 <b>실제 키 매핑과 같은 말</b>을 하는가. 표기와 구현이 각자 움직이면
        /// 안내는 그럴듯한데 눌리지 않는 조합이 된다 — 이 파일이 존재하는 이유 그대로다.
        /// </summary>
        [Test]
        public void Windows_표기가_Win32의_실제_조합키_매핑과_일치한다()
        {
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            StringAssert.Contains("VK_LWIN", win,
                "Win32WindowService가 Command를 Windows 키로 읽지 않습니다 — " +
                "그렇다면 ShortcutLabel의 'Win' 표기가 거짓말이 됩니다.");
            StringAssert.Contains("VK_CONTROL", win, "Control -> Ctrl 매핑이 사라졌습니다.");
            StringAssert.Contains("VK_MENU", win, "Option -> Alt 매핑이 사라졌습니다(VK_MENU = Alt).");

            foreach (string token in new[] { "Ctrl", "Alt", "Win" })
            {
                StringAssert.Contains(token, ShortcutLabel.WindowsModifiers,
                    $"Windows 표기에 '{token}'이 없습니다 — 위 매핑과 다른 말을 하고 있습니다.");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>스캐너가 실제로 볼 수 있는가.</b>
        /// <para>위 검사는 "글리프가 없다"를 단언하므로, 스캐너가 눈이 멀어도 초록이다. 그래서 여기서
        /// <b>옛 코드의 실제 모양</b>과 <b>지금 코드의 실제 모양</b>을 둘 다 이 파일 안에 박제해
        /// 판정이 갈리는지 본다(비교 대상 양쪽을 다 얼린다 — 2026-09-01 방울/펜던트 라운드의 교훈).</para>
        /// <para>박제한 옛 줄은 <c>ItemCatalog.cs</c>가 2026-09-01 이전에 실제로 갖고 있던 형태이고,
        /// 주석 줄은 <c>AppControlDirector.cs</c>가 <b>지금도</b> 갖고 있는 형태다(줄 <b>끝</b> 주석 —
        /// 옛 줄 단위 스캔이 오탐을 내던 바로 그 모양).</para>
        /// </summary>
        [Test]
        public void 컨트롤_글리프_스캐너는_리터럴만_잡고_주석은_넘긴다()
        {
            const string oldLiteral =
                "            ItemCatalogEntry.ForAction(\"action.archery\", \"활쏘기\", \"⌃⌥⌘A\",";
            const string trailingComment =
                "            Settings,           // ★ 2026-09-01 신설 — 설정창(⌃⌥⌘P)";
            const string docComment =
                "        /// 설정창 토글(전역 단축키 ⌃⌥⌘P, Preferences). 주 진입점은 <b>정보창 헤더의 [설정]</b>이고";
            const string fixedLiteral =
                "            ItemCatalogEntry.ForAction(\"action.archery\", \"활쏘기\", ShortcutLabel.Chord(\"A\"),";

            Assert.IsTrue(ContainsGlyphInStringLiteral(oldLiteral, MacGlyphs),
                "스캐너가 옛 하드코딩 줄을 못 잡습니다 — 그렇다면 위 검사의 초록불은 아무 뜻도 없습니다.");
            Assert.IsFalse(ContainsGlyphInStringLiteral(trailingComment, MacGlyphs),
                "줄 끝 주석을 위반으로 셉니다 — 주석의 단축키 인용은 화면에 나가지 않으므로 결함이 아닙니다. " +
                "여기서 오탐이 나면 다음 사람은 검사를 끄거나 주석에서 사실을 지웁니다.");
            Assert.IsFalse(ContainsGlyphInStringLiteral(docComment, MacGlyphs),
                "문서 주석을 위반으로 셉니다.");
            Assert.IsFalse(ContainsGlyphInStringLiteral(fixedLiteral, MacGlyphs),
                "고쳐진 줄을 아직 위반으로 셉니다 — 스캐너가 리터럴이 아니라 줄 전체를 보고 있습니다.");
        }


        /// <summary>
        /// 한 메서드의 <b>본문만</b> 잘라 낸다(주석이 이미 걷힌 소스 기준).
        ///
        /// <para><b>왜 파일 전체가 아니라 본문인가</b>: 같은 파일이 여러 관심사를 담고 있어서,
        /// 파일 전체 검사는 <b>엉뚱한 곳의 흔적으로 통과</b>한다. 실제 예 — <c>Win32WindowService</c>는
        /// 전체화면 판정과 상단/하단 예약 띠 조회가 <b>둘 다</b> <c>rcMonitor</c>를 읽는다. 파일 전체에서
        /// "rcMonitor.Left"를 찾으면 <b>전체화면 판정이 통째로 사라져도</b> 초록이다.</para>
        ///
        /// <para>끝 경계는 "줄 시작이 8칸 들여쓰기 + 닫는 중괄호"다. 이 저장소의 클래스 멤버는 모두
        /// 8칸이고 그 안쪽 블록은 12칸 이상이라, 중괄호 세기 없이도 안전하다(문자열 안의
        /// <c>{}</c>·보간 표현식에 속지 않는다는 것이 이 방식의 이점이다).</para>
        /// </summary>
        private static string MethodBody(string source, string signature, string label)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"감사 앵커가 낡았습니다 — {label}의 선언(\"{signature}\")을 찾지 못했습니다. " +
                "이름이나 서명이 바뀌었다면 이 감사도 함께 갱신하세요. 그대로 두면 '못 찾았다'가 " +
                "조용한 초록이 되거나, 파일 전체 검사로 되돌아가 공허해집니다.");

            int end = source.IndexOf("\n        }", at, StringComparison.Ordinal);
            Assert.Greater(end, at,
                $"{label}의 본문 끝(8칸 들여쓰기 닫는 중괄호)을 찾지 못했습니다 — 들여쓰기 규약이 " +
                "바뀌었다면 이 추출기도 함께 갱신하세요.");

            return source.Substring(at, end - at);
        }

        // ============================================================================
        // ★★ 2026-09-02 심야 — 그날 밤 갈라진 것들. 성격을 이름으로 구분한다
        // ============================================================================
        // 전부 Assert.Ignore로 뭉치면 목록이 뜻을 잃는다. 접두사 규칙은 이 파일 맨 위 문서와
        // 아래 감사_대장 테스트가 **기계적으로** 지킨다.
        //   결정_      = 의도된 차이. 되돌리면 실패한다.
        //   역방향_    = macOS가 뒤처진 쪽. Windows 구현을 보호하고, 고쳐지면 스스로 알린다.
        //   해당없음_  = 반대 플랫폼에 그 문제가 구조적으로 존재하지 않는다.
        //   갭추적_    = 갭은 열려 있으나 다른 테스트가 이미 러너에 띄우고 있다(중복 스킵 금지).
        //   실기미확인_= 코드로는 더 할 게 없고 Windows 하드웨어만 남았다. -> Ignore
        //   미해결_    = 진짜 코드 갭. -> Ignore

        /// <summary>
        /// <b>[결정 A] 전체화면 기하 "상단 여백 관용"은 macOS만 켠다.</b>
        ///
        /// <para>macOS 네이티브 전체화면 창은 디스플레이 사각형과 <b>같지 않다</b>(상단 33pt가 시스템
        /// 스트립에 남는다). 그래서 macOS는 관용 있는 <c>FullscreenGeometry.CoversDisplay</c>를 쓴다.
        /// Windows는 <b>일부러 따라가지 않는다</b> — "OS가 화면 위쪽에 항상 남겨두는 띠"라는 개념이
        /// 없고 오히려 <b>상단 도킹 작업표시줄이 흔하다</b>. 관용을 그대로 켜면 그 환경에서 <b>최대화한
        /// 업무 창이 전부 전체화면 게임으로 오판</b>되어, 원칙 2의 반대편(멀쩡히 일하는데 캐릭터가
        /// 사라진다)을 깬다. 2026-08-31 사용자 신고가 정확히 그 방향이었다.</para>
        ///
        /// <para><b>왜 Ignore가 아닌가</b>: 이건 "아직 못 고친 것"이 아니라 <b>고치면 안 되는 것</b>이다.
        /// Ignore로 두면 언젠가 누군가 "패리티 맞추자"며 관용을 켜고, 그 순간 사용자 신고가 재발한다.
        /// 그래서 <b>정식 검사</b>로 잠근다 — 되돌리는 쪽이 빨간불이 되게.</para>
        ///
        /// <para><b>★ 이 결정이 취향이 아니라 필요임을 산술로 못 박는다</b>(아래 (4)):
        /// 상단 48px 작업표시줄 + 2560x1440 모니터에서 최대화한 창을 두 규칙에 각각 물어보면
        /// 관용 규칙은 "덮는다"(=오판), 정확일치 규칙은 "아니다"(=정답)라고 답한다. 이 두 줄이
        /// 갈리지 않으면 위 서술 전체가 근거를 잃으므로 여기서 함께 얼린다.</para>
        /// </summary>
        [Test]
        public void 결정_전체화면_기하_관용은_macOS만_켠다()
        {
            string mac = StripLineComments(ReadSource(MacWindowServicePath));
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            // (1) macOS는 관용 있는 쪽을 **실제로** 부른다.
            StringAssert.Contains("FullscreenGeometry.CoversDisplay(", mac,
                "macOS가 관용 규칙을 부르지 않습니다 — 네이티브 전체화면(상단 33pt가 남는 형태)에서 " +
                "기하 판정이 영원히 false가 되고, 절대 불변 원칙 2가 그 경로에서 통째로 죽습니다.");

            // (2) Windows는 부르지 않는다. ★ 이 한 줄이 '결정'의 본체다.
            Assert.IsFalse(win.Contains("FullscreenGeometry.CoversDisplay("),
                "Win32WindowService가 상단 여백 관용 규칙을 부르기 시작했습니다. 이것은 패리티 개선이 " +
                "아니라 **회귀**입니다 — 상단 도킹 작업표시줄 환경에서 최대화한 업무 창이 전부 " +
                "전체화면 게임으로 오판되어 캐릭터가 사라집니다(2026-08-31 신고의 반대편). " +
                "정말 켜야 한다면 실기 검증 결과를 근거로 이 테스트를 먼저 고치세요.");

            // (3) 대신 네 변 정확일치를 **실제로** 본다(= '판정을 아예 안 한다'와 구분).
            //     ★ 파일 전체가 아니라 **그 메서드 본문 안**에서 찾는다. 같은 파일의 상단/하단 예약 띠
            //     조회도 rcMonitor를 읽으므로, 파일 전체 검사는 전체화면 판정이 통째로 사라져도 초록이다.
            string winBody = MethodBody(win, "private bool EvaluateFullscreen(",
                "Win32WindowService.EvaluateFullscreen");
            foreach (string edge in new[] { "rcMonitor.Left", "rcMonitor.Top", "rcMonitor.Right", "rcMonitor.Bottom" })
            {
                StringAssert.Contains(edge, winBody,
                    $"Windows 전체화면 판정 본문에서 {edge} 비교가 사라졌습니다 — 관용을 끈 자리에 " +
                    "정확일치도 없으면 판정 자체가 없는 것이고, 그건 결정이 아니라 결함입니다.");
            }

            // (4) ★ 결정의 근거를 산술로 얼린다 — 상단 48px 작업표시줄 + 최대화한 업무 창.
            const double monW = 2560.0, monH = 1440.0, topBar = 48.0;
            bool tolerantSaysCovered = FullscreenGeometry.CoversDisplay(
                0.0, topBar, monW, monH - topBar, 0.0, 0.0, monW, monH, FullscreenGeometry.Epsilon);
            bool exactSaysCovered = FullscreenGeometry.MatchesExactly(
                0.0, topBar, monW, monH - topBar, 0.0, 0.0, monW, monH, FullscreenGeometry.Epsilon);

            Assert.Less(topBar, monH * FullscreenGeometry.MenuBarStripFraction,
                "이 시나리오의 작업표시줄이 관용 상한보다 두꺼워졌습니다 — 그러면 아래 두 줄이 " +
                "우연히 갈리는 것이고 논증이 성립하지 않습니다. 시나리오나 상한을 다시 보세요.");
            Assert.IsTrue(tolerantSaysCovered,
                "관용 규칙이 '상단 띠만 남은 최대화 창'을 덮는다고 보지 않습니다 — 그렇다면 " +
                "'Windows에서 관용을 켜면 위험하다'는 이 결정의 전제가 깨진 것이므로, 결정을 " +
                "다시 판단해야 합니다(이 테스트를 지우지 말고 근거를 갱신하세요).");
            Assert.IsFalse(exactSaysCovered,
                "정확일치 규칙마저 최대화한 업무 창을 전체화면으로 봅니다 — Windows 경로가 " +
                "안전하지 않습니다. 이 경우 결정 자체가 무의미하므로 즉시 리더에게 올리세요.");
        }

        /// <summary>
        /// <b>[해당없음 B] 전체화면 판정의 "투명 보조 창 알파 거부권"은 Windows 경로에 존재할 수 없다.</b>
        ///
        /// <para>macOS는 네이티브 전체화면 창마다 알파 0짜리 "자동 숨김 타이틀바 컨테이너"를 함께
        /// 만든다. 그것이 layer 0이면서 z-order상 본 창보다 앞이라, 알파 필터가 없으면 그 창이 먼저
        /// 잡혀 기하 불일치로 <c>return false</c> — 본 창은 <b>영원히 검사되지 않는다</b>. 그래서
        /// macOS는 발판 경로와 <b>같은 상수</b>(<c>MinWindowAlpha</c>)로 거부권을 건다.</para>
        ///
        /// <para><b>Windows는 창 목록을 훑지 않는다</b> — <c>GetForegroundWindow()</c> 단일 조회다.
        /// 전경 창은 정의상 알파 0짜리 보조 창이 아니므로 <b>그 문제가 존재할 경로가 없다</b>.
        /// 갭이 아니라 "해당 없음"이며, 그래서 Ignore가 아니라 정식 검사로 <b>양쪽의 구조</b>를
        /// 얼린다: 훗날 Windows가 창을 열거하도록 바뀌면 이 검사가 먼저 빨간불이 되어 "이제는
        /// 해당된다"고 알려 준다.</para>
        /// </summary>
        [Test]
        public void 해당없음_전체화면_보조창_알파거부권은_Windows_경로에_없다()
        {
            string mac = StripLineComments(ReadSource(MacWindowServicePath));
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            // macOS: 전체화면 평가 본문 **안**에 알파 거부권이 있는가(파일 어딘가가 아니라).
            string macBody = MethodBody(mac, "private bool EvaluateFullscreen(",
                "MacWindowService.EvaluateFullscreen");
            StringAssert.Contains("MinWindowAlpha", macBody,
                "macOS 전체화면 판정에서 투명 보조 창 거부권이 사라졌습니다 — 네이티브 전체화면 " +
                "게임에서 알파 0짜리 타이틀바 컨테이너가 먼저 잡혀 판정이 영원히 false가 됩니다" +
                "(2026-09-02에 이 한 줄이 없어서 원칙 2가 그 경로에서 통째로 죽어 있었습니다).");

            // Windows: 전경 창 **단일 조회**인가. 열거로 바뀌면 같은 문제가 새로 생긴다.
            string winBody = MethodBody(win, "private bool EvaluateFullscreen(",
                "Win32WindowService.EvaluateFullscreen");
            StringAssert.Contains("GetForegroundWindow()", winBody,
                "Windows 전체화면 판정이 전경 창 단일 조회가 아닙니다.");
            Assert.IsFalse(winBody.Contains("EnumWindows("),
                "Windows 전체화면 판정이 창을 **열거하기 시작했습니다** — 그 순간 macOS가 겪은 " +
                "'투명 보조 창이 먼저 잡힌다' 문제가 이쪽에도 생깁니다. 이제는 '해당 없음'이 아니므로 " +
                "알파(또는 그에 준하는 가시성) 거부권을 함께 넣고 이 검사를 정식 패리티 검사로 " +
                "바꾸세요. Windows의 대응 수단은 DWM 클로킹(DWMWA_CLOAKED)과 WS_EX_LAYERED 알파입니다.");

            // 두 본문이 같은 결론 계약을 지키는가: '게임인가'를 한 번 더 묻고 끝난다.
            StringAssert.Contains("IsGameProcess(", winBody,
                "Windows가 기하만으로 숨깁니다 — 전체화면 엑셀/PPT에서 캐릭터가 사라집니다.");
            StringAssert.Contains("FullscreenGameCategory.IsGameCategory(", macBody,
                "macOS가 기하만으로 숨깁니다 — 전체화면 키노트/브라우저에서 캐릭터가 사라집니다.");
        }

        /// <summary>
        /// <b>[역방향 C] 보조 모니터 전체화면 감지 — 이번엔 macOS가 뒤처진 쪽이다.</b>
        ///
        /// <para><c>Win32WindowService</c>는 <c>MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST)</c>로
        /// <b>전경 창이 실제로 놓인 모니터</b>를 기준으로 판정한다. <c>MacWindowService</c>는
        /// <c>CGDisplayBounds(CGMainDisplayID())</c> — <b>메인 디스플레이만</b> 본다. 그래서 보조
        /// 모니터에서 게임을 전체화면으로 띄우면 macOS는 그것을 전체화면으로 인식하지 못하고
        /// 캐릭터가 그대로 남는다(원칙 2 미적용).</para>
        ///
        /// <para><b>왜 Ignore가 아닌가</b>: 이 항목을 "미해결 Windows 갭" 더미에 넣으면 방향이
        /// 거꾸로 기록된다. 지금 필요한 일은 <b>Windows를 고치는 것이 아니라 Windows를 지키는 것</b>
        /// 이다 — "패리티를 맞추자"며 Windows를 메인 모니터 조회로 내리는 변경이 들어오면 두
        /// 플랫폼이 함께 나빠진다. 그래서 나은 쪽을 정식 검사로 <b>얼리고</b>, 못한 쪽은 고쳐지는
        /// 순간 <c>Assert.Pass</c>로 스스로 알리게 한다.</para>
        /// </summary>
        [Test]
        public void 역방향_보조모니터_전체화면_감지는_Windows가_낫다()
        {
            string win = StripLineComments(ReadSource(WinWindowServicePath));
            string mac = StripLineComments(ReadSource(MacWindowServicePath));

            // (1) 나은 쪽(Windows)을 얼린다 — 여기서 후퇴하면 두 플랫폼이 함께 나빠진다.
            string winBody = MethodBody(win, "private bool EvaluateFullscreen(",
                "Win32WindowService.EvaluateFullscreen");
            StringAssert.Contains("MonitorFromWindow(fg", winBody,
                "Windows 전체화면 판정이 더 이상 '전경 창이 놓인 모니터'를 묻지 않습니다 — " +
                "이것은 패리티 맞추기가 아니라 회귀입니다(보조 모니터의 전체화면 게임을 놓칩니다). " +
                "macOS를 올려서 맞추세요, Windows를 내려서 맞추지 마세요.");

            // (2) 이 갭이 '정책'이 아니라 '사실 조회' 갭임을 못 박는다:
            //     중립 규칙은 이미 디스플레이 사각형을 **인자로** 받는다 = macOS를 고치는 데
            //     정책 변경이 0줄이고, 어느 디스플레이를 넘기느냐만 바꾸면 된다.
            Assert.AreEqual(9, typeof(FullscreenGeometry)
                    .GetMethod(nameof(FullscreenGeometry.CoversDisplay)).GetParameters().Length,
                "CoversDisplay의 인자 구성이 바뀌었습니다 — 이 항목의 '정책은 이미 준비돼 있고 " +
                "사실 조회만 바꾸면 된다'는 진단이 낡았을 수 있으니 함께 갱신하세요.");

            // (3) 못한 쪽(macOS). 고쳐지면 스스로 알린다 — 실패로 막지 않는다.
            string macBody = MethodBody(mac, "private bool EvaluateFullscreen(",
                "MacWindowService.EvaluateFullscreen");
            if (!macBody.Contains("CGMainDisplayID()"))
            {
                Assert.Pass("macOS 전체화면 판정이 더 이상 메인 디스플레이만 보지 않습니다 — " +
                    "역방향 갭이 닫혔습니다. 이 항목을 '양 플랫폼이 창이 놓인 디스플레이를 본다'는 " +
                    "정식 패리티 검사로 바꾸세요.");
            }

            Assert.IsTrue(macBody.Contains("CGDisplayBounds("),
                "macOS가 CGMainDisplayID를 쓰면서 CGDisplayBounds를 부르지 않습니다 — " +
                "이 항목의 진단(메인 디스플레이 사각형과 비교한다)이 더 이상 사실이 아닙니다. " +
                "감사가 낡은 사실을 말하고 있으니 지금 다시 읽고 갱신하세요.");
        }

        // ============================================================================
        // ★ Windows 하단 막대 낙차 — D/E 두 항목이 공유하는 **가정**
        // ============================================================================

        /// <summary>
        /// ★ <b>실측이 아니라 가정</b>이다. 이름에 <c>Assumed</c>를 박아 두는 이유가 그것이다.
        /// Windows 11 기본 작업표시줄 두께 48물리px(표시 배율 100%).
        /// <para>이 값을 <c>StickConfig</c>나 <c>DockGeometry</c>에 상수로 넣지 <b>않는다</b> —
        /// 프로덕션이 가정을 사실처럼 들고 있게 되고, 그러면 실측이 들어와도 아무도 갱신하지 않는다.
        /// 가정은 그것을 <b>가정이라고 말하는 유일한 자리</b>인 감사 안에만 둔다.</para>
        /// </summary>
        private const float AssumedWindows11TaskbarThicknessPoints = 48f;

        /// <summary>
        /// 위 가정에서 유도한 "작업표시줄 상단 → 바닥 안전망 상단" 낙차(월드 유닛).
        /// <para><b>여기에는 가정이 두 개 겹쳐 있다</b>(둘 다 Windows 실기에서만 확인 가능):
        /// <list type="number">
        ///   <item>작업표시줄 두께가 48pt다.</item>
        ///   <item>pt→월드 환산이 이 개발 머신과 같다 — <c>DockGeometry.ReferenceWorldUnitsPerPoint</c>는
        ///         화면 높이 982pt / 직교 12에서 나온 값이다. <b>이 두 번째 가정은 획 하한 항목과
        ///         정확히 같은 축</b>이라, Windows 실측 한 번이 세 항목을 함께 닫는다.</item>
        /// </list></para>
        /// <para>낙차 식 자체는 프로덕션과 같은 것을 쓴다(<c>DockGeometry.DockDropPoints</c>가
        /// 하는 것과 같은 뺄셈: 두께 − <c>BottomSafetyNetInsetPoints</c>).</para>
        /// </summary>
        private static float AssumedWindowsTaskbarDropWorldUnits()
            => (AssumedWindows11TaskbarThicknessPoints - NullPlatformWindowService.BottomSafetyNetInsetPoints)
               * DockGeometry.ReferenceWorldUnitsPerPoint;

        /// <summary>배포되는 설정 에셋(코드 기본값이 아니라 <b>실제로 나가는 값</b>)을 읽는다.</summary>
        private static StickConfig LoadDeployedConfig()
        {
            const string path = "Assets/_Project/Data/DefaultStickConfig.asset";
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<StickConfig>(path);
            Assert.IsNotNull(config,
                $"배포 설정 에셋을 찾지 못했습니다: {path} — 아래 유도는 코드 기본값이 아니라 " +
                "실제로 출하되는 값으로 해야 의미가 있습니다.");
            return config;
        }

        /// <summary>
        /// <b>[갭 D] 착지 티어 교차 배율이 Windows 작업표시줄에서 실측되지 않았다.</b>
        ///
        /// <para>착지 무게감 티어는 <b>낙차 하나</b>로 갈린다. 낙차는 하단 막대 두께에서 나오는데
        /// 그 두께가 플랫폼마다 다르므로, "몇 배율부터 무릎을 굽히는가"라는 교차 배율도 갈린다:
        /// macOS Dock <b>0.8180</b>(<c>StickConfig.DockKneelCriticalScale</c>, 이 머신 tilesize 49) /
        /// Windows 작업표시줄 <b>0.4883</b>(가정에서 나온 역산값).</para>
        ///
        /// <para><b>★ 이 항목이 말하는 진짜 사실</b>: 출하 기본 배율 0.75에서 <b>두 플랫폼의 착지
        /// 티어가 다르다</b>. macOS는 0.75 ≤ 0.8180이라 T1(무릎앉아)이고, Windows는 0.75 &gt; 0.4883이라
        /// T0.5(가벼운 흡수)다. 같은 앱, 같은 설정, 같은 동작인데 <b>발이 바닥에 닿는 그림이 다르다</b>.
        /// 이건 버그가 아니라 "낙차가 다르면 티어도 다르다"는 설계의 정직한 귀결이고
        /// (<c>landingReactionThresholdHeights</c>의 Tooltip이 이미 그렇게 적고 있다), 그래서
        /// <b>고칠 것인지부터 결정해야 한다</b>. 다만 그 결정의 근거인 0.977유닛이 아직 <b>역산값</b>이다.</para>
        ///
        /// <para>아래 (1)은 <b>덤으로 얻는 이득</b>이다 — <c>DockKneelCriticalScale</c>은 지금 어떤
        /// 테스트도 재유도하지 않는다(<c>DockLandingSilhouetteTests</c>는 아직 없다). 여기서 배포
        /// 설정값으로 다시 계산해 상수와 대조하므로, 램프 상수가 바뀌면 이 자리가 먼저 알려 준다.</para>
        /// </summary>
        [Test]
        public void 실기미확인_착지티어_교차배율이_Windows_작업표시줄에서_실측되지_않았다()
        {
            StickConfig config = LoadDeployedConfig();

            // (1) macOS 쪽을 **다시 유도**해 배포 상수와 맞는지 확인한다(숫자를 베끼지 않는다).
            float kneelThresholdAtScaleOne =
                config.ResolveLandingReactionThreshold(StickConfig.BaselineCharacterTotalHeight);
            Assert.Greater(kneelThresholdAtScaleOne, 0f,
                "T1(무릎앉아) 문턱이 0 이하입니다 — 아래 나눗셈이 전부 무의미해집니다.");

            float macCross = DockGeometry.ReferenceDockDropWorldUnits / kneelThresholdAtScaleOne;
            Assert.AreEqual(StickConfig.DockKneelCriticalScale, macCross, 0.0005f,
                $"macOS Dock의 T1 교차 배율을 배포 설정값에서 다시 유도한 값({macCross:F4})이 " +
                $"StickConfig.DockKneelCriticalScale({StickConfig.DockKneelCriticalScale:F4})과 다릅니다. " +
                "둘 중 하나가 낡았습니다 — 램프 상수(landingReactionThresholdHeights)나 Dock 낙차가 " +
                "바뀌었는데 상수만 그대로일 가능성이 큽니다.");

            // (2) Windows 쪽은 **가정**에서 나온다.
            float winDrop = AssumedWindowsTaskbarDropWorldUnits();
            float winCross = winDrop / kneelThresholdAtScaleOne;
            Assert.Greater(winCross, 0f, "Windows 교차 배율이 0 이하입니다 — 가정 상수를 확인하세요.");

            // (3) 출하 기본 배율에서 두 플랫폼의 티어가 **실제로 갈리는가**를 못 박는다.
            //     (이 두 줄이 아래 Ignore 본문의 유일한 근거다. 갈리지 않게 되면 여기서 먼저 깨진다.)
            Assert.LessOrEqual(config.characterScale, macCross,
                $"출하 기본 배율({config.characterScale:F2})이 macOS T1 교차 배율({macCross:F4})을 " +
                "넘었습니다 — 이제 macOS도 Dock에서 무릎을 굽히지 않습니다. 아래 서술이 낡았으니 " +
                "함께 갱신하세요(연출 회귀일 수도 있습니다).");
            Assert.Greater(config.characterScale, winCross,
                $"출하 기본 배율({config.characterScale:F2})이 Windows T1 교차 배율({winCross:F4}) " +
                "이하로 내려왔습니다 — 두 플랫폼의 티어 차이가 사라졌다는 뜻이라 반가운 소식이지만, " +
                "아래 서술이 낡았으니 갱신하세요.");

            // (4) 그래도 Windows가 '완전 무반응(T0)'은 아님을 확인한다 — 갭의 크기를 정직하게 재기 위해.
            float softAbsorbAtShipping = config.ResolveLandingSoftAbsorbThreshold(
                StickConfig.BaselineCharacterTotalHeight * config.characterScale);
            Assert.Greater(winDrop, softAbsorbAtShipping,
                $"가정 낙차({winDrop:F4})가 출하 배율의 T0.5 문턱({softAbsorbAtShipping:F4})에도 못 " +
                "미칩니다 — 그렇다면 Windows 작업표시줄에서 내려올 때 **아무 반응도 없다**는 뜻이고, " +
                "이 항목은 '티어가 다르다'가 아니라 '반응이 없다'로 다시 써야 합니다.");

            Assert.Ignore("【실기 미확인 · 낙차가 역산값】 신설 2026-09-02\n" +
                $"macOS: T1(무릎앉아) 교차 배율 {macCross:F4} — Dock 낙차 " +
                $"{DockGeometry.ReferenceDockDropWorldUnits:F4}유닛(이 머신 tilesize=49) 실측 기반. " +
                "배포 상수 StickConfig.DockKneelCriticalScale과 방금 재유도로 일치 확인.\n" +
                $"Windows: T1 교차 배율 {winCross:F4} — 작업표시줄 낙차 {winDrop:F4}유닛. " +
                "★ 이 낙차는 **실측이 아니라 역산**이다: (가정 두께 " +
                $"{AssumedWindows11TaskbarThicknessPoints:F0}pt − 바닥 안전망 인셋 " +
                $"{NullPlatformWindowService.BottomSafetyNetInsetPoints:F0}pt) x 이 머신의 pt→월드 환산.\n" +
                $"★ 지금 상태의 귀결: 출하 기본 배율 {config.characterScale:F2}에서 macOS는 T1(무릎앉아), " +
                "Windows는 T0.5(가벼운 흡수)다. 같은 앱·같은 설정인데 하단 막대에서 내려오는 그림이 " +
                "다르다. 설계상 정직한 귀결이지만(티어 축이 낙차 하나이므로) '이대로 둘 것인가'는 " +
                "리더 결정 대상이다.\n" +
                "가정이 두 개다(둘 다 Windows 실기에서만 확인 가능): (1) 작업표시줄 두께 48pt, " +
                "(2) pt→월드 환산이 이 개발 머신(982pt/직교12)과 같다. (2)는 획 하한 항목과 같은 축이라 " +
                "실측 한 번이 함께 닫는다.\n" +
                "실기 검증 필요 — 사용자 Windows 머신: 작업표시줄 위에서 캐릭터를 걷게 하고 아래로 " +
                "내려오게 한 뒤, 로그의 [무릎앉아] 줄에서 '티어=' 값과 '낙하높이='(월드 유닛)를 읽는다.");
        }

        /// <summary>
        /// <b>[갭 E] 매달리기(LedgeHang)가 Windows 작업표시줄에서는 어떤 배율에서도 나오지 않는다.</b>
        ///
        /// <para>매달리기는 "낙차 ≥ 매달리기 최소 낙차"일 때만 선택된다. 최소 낙차는 배율에 비례해
        /// 줄지만 낙차(막대 두께)는 <b>고정</b>이므로, 경계 배율 = 낙차 / (배율 1에서의 최소 낙차)다.
        /// macOS Dock은 0.4493(<c>StickConfig.DockHopDownCriticalScale</c>)이라 슬라이더 구간
        /// [0.35, 1.0] <b>안에</b> 들어온다 — 사용자가 배율을 아주 작게 하면 Dock에서 매달리는 그림을
        /// 실제로 볼 수 있다. Windows 작업표시줄은 낙차가 절반 이하라 경계 배율이 약 0.268이고,
        /// 이는 <c>StickConfig.MinCharacterScale</c>(0.35) <b>아래</b>다.</para>
        ///
        /// <para><b>안전은 검산된다</b>(아래 (1)(2)) — 매달리기 밴드에 못 들어가면 항상 뛰어내리기
        /// 밴드이고, 낙차가 뛰어내리기 하한보다 크므로 "내려갈 길이 하나도 없어 막대 위에 갇힌다"는
        /// 사고는 일어나지 않는다. <b>남는 것은 연출 결손</b>이다: Windows 사용자는 이 앱의 모션
        /// 하나(매달려 내려가기)를 <b>실기에서 한 번도 볼 수 없다</b>. 슬라이더를 끝까지 내려도 안 나온다.</para>
        /// </summary>
        [Test]
        public void 실기미확인_매달리기가_Windows_작업표시줄에서는_어떤_배율에서도_안_나온다()
        {
            StickConfig config = LoadDeployedConfig();

            // 배율 1.0에서의 매달리기 최소 낙차를 **배포 상수에서 역산**한다(숫자를 베끼지 않는다):
            //   DockHopDownCriticalScale = ReferenceDockDropWorldUnits / hangMinAtScaleOne
            Assert.Greater(StickConfig.DockHopDownCriticalScale, 0f,
                "DockHopDownCriticalScale이 0 이하입니다 — 아래 역산이 성립하지 않습니다.");
            float hangMinAtScaleOne =
                DockGeometry.ReferenceDockDropWorldUnits / StickConfig.DockHopDownCriticalScale;

            float winDrop = AssumedWindowsTaskbarDropWorldUnits();
            float winHangCross = winDrop / hangMinAtScaleOne;

            // (1) 안전 방향: 매달리기 밴드에 못 들어가면 **항상** 뛰어내리기 밴드다.
            Assert.Less(winHangCross, StickConfig.MinCharacterScale,
                $"Windows 매달리기 경계 배율({winHangCross:F4})이 슬라이더 하한 " +
                $"({StickConfig.MinCharacterScale:F2}) 위로 올라왔습니다 — 좋은 소식일 수 있지만 " +
                "아래 서술이 낡았으니 갱신하세요(그 구간에서 매달리기가 실제로 나옵니다).");

            // (2) 뛰어내리기 밴드가 비어 있지 않다 = '막대 위에 갇힌다'가 아니다.
            Assert.Greater(winDrop, config.hopDownMinDropHeight,
                $"가정 낙차({winDrop:F4})가 뛰어내리기 하한({config.hopDownMinDropHeight:F2}) 이하입니다 — " +
                "그러면 매달리지도 뛰어내리지도 못해 캐릭터가 작업표시줄 위에 갇힙니다. " +
                "이 항목은 '연출 결손'이 아니라 **정지 버그**로 다시 분류해야 합니다.");

            // (3) 대조군: macOS는 슬라이더 구간 안에서 매달리기가 실제로 나온다.
            Assert.Greater(StickConfig.DockHopDownCriticalScale, StickConfig.MinCharacterScale,
                "macOS Dock의 매달리기 경계 배율마저 슬라이더 하한 아래로 내려갔습니다 — " +
                "그러면 두 플랫폼 모두 매달리기를 하단 막대에서는 볼 수 없게 됩니다. " +
                "이 항목의 '한쪽에서만 안 보인다'는 서술이 통째로 낡았으니 다시 쓰세요.");

            Assert.Ignore("【실기 미확인 · 안전은 검산됨 / 연출 결손이 남음】 신설 2026-09-02\n" +
                $"배율 1.0의 매달리기 최소 낙차 = {hangMinAtScaleOne:F4}유닛(배포 상수에서 역산).\n" +
                $"macOS: 경계 배율 {StickConfig.DockHopDownCriticalScale:F4} — 슬라이더 구간 " +
                $"[{StickConfig.MinCharacterScale:F2}, {StickConfig.MaxCharacterScale:F2}] 안이라 " +
                "작게 만들면 Dock에서 매달리는 그림이 실제로 나온다.\n" +
                $"Windows: 경계 배율 {winHangCross:F4} — 슬라이더 하한 " +
                $"{StickConfig.MinCharacterScale:F2}보다 낮다. 즉 **어떤 배율에서도** 작업표시줄에서 " +
                "매달리기가 선택되지 않는다.\n" +
                "★ 안전은 검산됐다: 항상 뛰어내리기 밴드이고 낙차가 뛰어내리기 하한보다 크므로 " +
                "'막대 위에 갇힘'은 발생하지 않는다(위 (1)(2)).\n" +
                "★ 남는 것은 연출 결손이다: Windows 사용자는 이 앱의 모션 하나를 실기에서 한 번도 " +
                "볼 수 없다. 이걸 고칠지는 기능 결정이지 버그 수정이 아니다 — 후보는 " +
                "(a) 그대로 둔다(가장 정직: 낙차가 실제로 얕다), " +
                "(b) 매달리기 최소 낙차를 낮춘다(다른 모든 발판의 거동이 함께 바뀐다 — 위험), " +
                "(c) 얕은 단차용 '접힌 팔 매달림' 자세를 새로 만든다(StickmanBlackboard의 " +
                "LedgeHangMinDropDepth 문서가 이미 지목해 둔 길, 신규 곡선 필요).\n" +
                "★ 이 판단의 근거인 낙차는 D 항목과 같은 **역산값**이다 — 실측이 먼저다.\n" +
                "실기 검증 필요 — 사용자 Windows 머신: 배율 슬라이더를 최소까지 내리고 작업표시줄 " +
                "가장자리에서 내려오게 한 뒤, 로그에 [매달리기] 상태 진입이 한 번이라도 찍히는지 본다.");
        }

        /// <summary>
        /// <b>[갭추적 F] 만화 텍스트 <u>속공간</u>은 배율 1(Windows 100% · 비Retina)에서 아직 검증
        /// 운용점에 못 미친다.</b>
        ///
        /// <para>★ 2026-09-02 <b>갭의 내용이 바뀌었다</b>(UX_FLOW §44-1/§44-2). 종전 갭은
        /// <b>C1(분리막 1물리픽셀)</b>이었고 그것은 <b>해소됐다</b> — 링이 서브픽셀로 무너지는 배율에서는
        /// 같은 예산 전부를 한 대각선 그림자에 실어 막이 1물리픽셀을 넘긴다
        /// (<see cref="DialogueBubbleRenderer.UseOutlineRing"/>). 남은 갭은 <b>C2(속공간 개방)</b>이며,
        /// 예산이 <b>총 소모량</b>으로 정의돼 있어 분기를 어떻게 바꿔도 나아지지 않는다.</para>
        ///
        /// <para><b>왜 여기서 Ignore를 또 걸지 않는가</b>: 이 갭은 이미 러너에 떠 있다
        /// (<c>ComicFontFloorOutlineRingTests.배율1에서는_속공간이_검증_운용점에_못_미친다_보류</c>).
        /// 같은 것을 여기서 한 번 더 건너뛰면 <b>목록만 두 배가 되고 뜻은 그대로다</b>. 그래서 이 자리는
        /// <b>플랫폼 관점의 사실</b>만 정식으로 잠근다: ① 갭이 아직 살아 있는가(숫자로),
        /// ② 검증된 캔버스 배율이 Retina 쪽뿐인가, ③ 추적자가 사라지지 않았는가.</para>
        ///
        /// <para>③이 핵심이다. 추적 테스트가 삭제되면 이 갭은 <b>어디에도 안 뜬 채</b> 사라진다 —
        /// 이 항목이 그것을 막는 유일한 장치다.</para>
        /// </summary>
        [Test]
        public void 갭추적_만화텍스트_속공간은_배율1에서_아직_검증운용점에_못미친다()
        {
            const float nonRetinaCanvasScale = 1f;   // Windows 100% 표시 배율 = 논리 1pt가 물리 1px.

            int floor = DialogueBubbleRenderer.ResolveMinComicFontSize(nonRetinaCanvasScale);
            int font = DialogueBubbleRenderer.SnapPointsNotBelow(floor, floor, nonRetinaCanvasScale);
            float counter = DialogueBubbleRenderer.RemainingCounterPointsFor(font) * nonRetinaCanvasScale;

            int verifiedFloor = DialogueBubbleRenderer.ResolveMinComicFontSize(
                DialogueBubbleRenderer.VerifiedCanvasScale);
            float verified = DialogueBubbleRenderer.RemainingCounterPointsFor(verifiedFloor)
                             * DialogueBubbleRenderer.VerifiedCanvasScale;

            // ★ C1은 해소됐다 — 갭 항목이 아니라 <실단언>으로 지킨다. 되돌아가면 여기서 즉시 빨개진다.
            float membrane = DialogueBubbleRenderer.MembranePointsFor(font, nonRetinaCanvasScale)
                             * nonRetinaCanvasScale;
            Assert.GreaterOrEqual(membrane, DialogueBubbleRenderer.OutlineRingMinPhysicalPixels - 1e-4f,
                $"배율 1의 분리막이 {membrane:F3}물리픽셀로 되돌아갔습니다 — 2026-09-02에 닫은 C1 갭이 " +
                "다시 열렸습니다(그림자 분기가 빠졌거나 하한 스냅이 하한을 다시 깹니다).");

            // ① 갭(C2)이 아직 살아 있는가. 닫혔으면 두 파일을 함께 정리하라고 알린다.
            if (counter + 1e-4f >= verified)
            {
                Assert.Pass($"배율 1의 남는 속공간({counter:F3}물리px)이 검증 운용점" +
                    $"({verified:F3}물리px)을 채웁니다 — 갭이 닫혔습니다. 이 항목과 " +
                    "ComicFontFloorOutlineRingTests의 보류 항목을 **함께** 정리하세요" +
                    "(한쪽만 지우면 다른 쪽이 유령으로 남습니다).");
            }

            // ② 검증된 캔버스 배율이 Retina 쪽뿐이라는 사실 = 이 갭이 '플랫폼' 항목인 이유.
            Assert.AreNotEqual(nonRetinaCanvasScale, DialogueBubbleRenderer.VerifiedCanvasScale,
                "검증된 캔버스 배율이 1이 됐습니다 — 그렇다면 이 항목은 더 이상 플랫폼 항목이 " +
                "아닙니다(비Retina에서도 캡처로 판정했다는 뜻). 분류를 갱신하세요.");

            // ③ 추적자가 살아 있는가. ★ 이 항목이 존재하는 진짜 이유다.
            string tracker = Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Tests", "EditMode", "ComicFontFloorOutlineRingTests.cs");
            Assert.IsTrue(File.Exists(tracker),
                $"이 갭을 러너에 띄우던 테스트 파일이 사라졌습니다({tracker}) — 지금 이 갭은 " +
                "**아무 데도 안 보입니다**. 여기서 Ignore를 다시 걸든지, 그 파일을 되살리세요.");
            StringAssert.Contains("배율1에서는_속공간이_검증_운용점에_못_미친다_보류", ReadSource(tracker),
                "추적 테스트의 보류 항목이 사라졌습니다 — 갭이 닫혀서 지운 것이라면 위 ①이 " +
                "Assert.Pass로 먼저 알려 줬을 것입니다. ①이 통과하지 못했다는 것은 갭이 아직 " +
                "살아 있는데 감시만 사라졌다는 뜻입니다.");
        }

        /// <summary>
        /// <b>[갭 G] 탭 배지·캡션 폭이 Windows 한글 폰트 폴백에서 검증되지 않았다.</b>
        ///
        /// <para>설정창 탭바는 라벨 폭을 <b>글자 수 x 고정 pt</b>로 <b>근사</b>한다
        /// (<c>SettingsWindow.TabLabelCharWidth</c>). 이 근사는 macOS 내장 폰트의 한글 자간에서
        /// 실측한 값이다. Windows의 한글 폴백(맑은 고딕/굴림 계열)은 전각 자간이 달라 같은 글자 수라도
        /// 렌더 폭이 달라진다. 라벨 상자는 <c>MiddleCenter</c>라 넘친 폭이 <b>양쪽으로</b> 삐져나오고,
        /// 오른쪽으로 삐져나온 글자는 그 탭의 "준비 중" 배지를 <b>침범</b>한다.</para>
        ///
        /// <para><b>여기서 실제로 계산하는 것</b>: 침범이 시작되는 <b>자간 팽창 배수</b>다.
        /// 가장 긴 라벨을 기준으로 <c>1 + 2 x 라벨-배지 간격 / (글자 수 x 근사 폭)</c>. 이 숫자가
        /// 있어야 사용자가 Windows에서 "얼마나 넓어졌는가"를 재서 <b>판정</b>할 수 있다 —
        /// "달라 보인다"가 아니라 "1.18배를 넘었다/아니다"로.</para>
        ///
        /// <para><b>왜 실단언이 아니라 Ignore인가</b>: 이 개발 머신에는 Windows 폰트 스택이 없다.
        /// 근사 자체는 <c>UiChrome.Ellipsize</c>(실제 <c>preferredWidth</c> 측정)라는 적응형 대안이
        /// 이미 있으므로 <b>코드 갭이라기보다 배선 선택</b>이고, 어느 쪽이 맞는지는 실기가 정한다.</para>
        /// </summary>
        [Test]
        public void 실기미확인_탭배지_캡션폭이_Windows_한글폴백에서_검증되지_않았다()
        {
            const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            Type tab = typeof(SettingsWindow);

            // ★ 이 항목의 앵커는 **다른 라운드가 소유한 파일**의 private 필드다. 이름이 바뀌면
            //   실패가 아니라 '앵커 낡음'으로 띄운다 — 남의 라운드를 내 빨간불로 막지 않는다.
            // ★ 논리곱은 반드시 비단축(&=)이다. ||로 묶으면 앞이 실패한 순간 뒤의 out 인자가
            //   호출되지 않아 "할당되지 않은 지역 변수" 컴파일 에러가 난다.
            bool anchorsOk = TryReadFloat(tab, "TabLabelCharWidth", Any, out float charWidth);
            anchorsOk &= TryReadFloat(tab, "TabBadgeGap", Any, out float badgeGap);
            anchorsOk &= TryReadFloat(tab, "TabBadgeWidth", Any, out float badgeWidth);
            anchorsOk &= TryReadFloat(tab, "TabPadX", Any, out float padX);
            string[] names = tab.GetField("TabNames", Any)?.GetValue(null) as string[];

            if (!anchorsOk || names == null || names.Length == 0)
            {
                Assert.Ignore("【실기 미확인 · 감사 앵커 낡음】 신설 2026-09-02\n" +
                    "SettingsWindow의 탭바 치수 필드(TabLabelCharWidth / TabBadgeGap / TabBadgeWidth / " +
                    "TabPadX / TabNames) 중 일부를 리플렉션으로 찾지 못했습니다. 이름이 바뀌었으면 " +
                    "이 감사도 함께 갱신하세요 — 그때까지 아래 계산은 하지 않습니다(틀린 숫자를 " +
                    "내는 것보다 못 냈다고 말하는 편이 낫습니다).");
            }

            Assert.Greater(names.Length, 0, "탭 이름 배열이 비었습니다 — 계산이 공허합니다.");
            Assert.Greater(charWidth, 0f, "탭 라벨 글자 폭 근사가 0 이하입니다.");

            // 가장 긴 라벨이 배지를 침범하기 시작하는 자간 팽창 배수.
            //   라벨 상자 폭 = 글자수 x charWidth, MiddleCenter -> 한쪽 초과분 = (k-1) x 폭 / 2
            //   그 초과분이 라벨-배지 간격을 넘으면 침범 -> k > 1 + 2 x gap / 폭
            float worstTolerance = float.MaxValue;
            string worstName = string.Empty;
            foreach (string name in names)
            {
                float boxWidth = name.Length * charWidth;
                if (boxWidth <= 0f) continue;
                float tolerance = 1f + (2f * badgeGap / boxWidth);
                if (tolerance >= worstTolerance) continue;
                worstTolerance = tolerance;
                worstName = name;
            }
            Assert.Less(worstTolerance, float.MaxValue, "여유 배수를 한 번도 계산하지 못했습니다(공허).");
            Assert.Greater(worstTolerance, 1f,
                "자간이 조금도 넓어지지 않아도 배지를 침범합니다 — macOS 기준선 자체가 이미 " +
                "빠듯하다는 뜻이라 Windows 이전에 여기서 먼저 고쳐야 합니다.");

            // 배지 자신이 다음 탭으로 넘어가기 시작하는 배수(오른쪽 여백 = TabPadX).
            float badgeTolerance = badgeWidth > 0f ? 1f + (padX / badgeWidth) : float.NaN;

            // 적응형 대안이 실재하는가 — "고칠 방법이 없다"와 "안 쓰고 있다"를 구분한다.
            Assert.IsNotNull(typeof(UiChrome).GetMethod(nameof(UiChrome.Ellipsize)),
                "UiChrome.Ellipsize(실제 preferredWidth 측정)가 사라졌습니다 — 그러면 이 갭의 " +
                "처방 후보('근사 대신 실측을 쓴다')가 함께 사라진 것이므로 서술을 다시 쓰세요.");

            Assert.Ignore("【실기 미확인 · macOS 폰트 메트릭 기준 근사】 신설 2026-09-02\n" +
                $"모델: 탭 라벨 폭 = 글자 수 x {charWidth:F1}pt(고정 근사), 배지 폭 {badgeWidth:F0}pt, " +
                $"라벨-배지 간격 {badgeGap:F0}pt, 탭 좌우 여백 {padX:F0}pt.\n" +
                $"★ 침범 시작 배수: 가장 긴 라벨 '{worstName}'({worstName.Length}글자) 기준 " +
                $"**{worstTolerance:F3}배**. 즉 Windows 한글 폴백의 평균 자간이 macOS 대비 " +
                $"{(worstTolerance - 1f) * 100f:F1}% 이상 넓으면 그 라벨이 자기 탭의 '준비 중' 배지를 " +
                "침범한다.\n" +
                $"참고 — 배지가 다음 탭으로 넘어가기 시작하는 배수는 {badgeTolerance:F3}배(더 여유롭다). " +
                "즉 먼저 깨지는 곳은 배지가 아니라 **라벨↔배지 간격**이다.\n" +
                "macOS: 검증됨(내장 폰트 실측에서 나온 근사).\n" +
                "Windows: 미검증 — 이 개발 머신에 Windows 폰트 스택이 없다.\n" +
                "처방 후보: 근사 대신 UiChrome.Ellipsize와 같은 실측(Text.preferredWidth) 경로로 " +
                "탭 폭을 잡는다. 이미 있는 함수라 신규 알고리즘이 0줄이고, 대신 탭바 생성 시 " +
                "폰트 측정이 탭 수만큼 늘어난다(생성 1회이므로 상주 비용 0).\n" +
                "실기 검증 필요 — 사용자 Windows 머신: 설정창(Ctrl+Alt+Win+P)을 열고 '접근성 · 성능' " +
                "탭의 라벨 끝과 '준비 중' 배지가 붙었는지, 또는 배지가 옆 탭 글자와 겹쳤는지 본다.");
        }

        /// <summary>정적 <c>float</c> 필드(const/readonly 무관)를 이름으로 읽는다. 없으면 false.</summary>
        private static bool TryReadFloat(Type type, string fieldName, BindingFlags flags, out float value)
        {
            value = 0f;
            FieldInfo field = type.GetField(fieldName, flags);
            if (field == null || field.FieldType != typeof(float)) return false;
            object raw = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
            if (!(raw is float f)) return false;
            value = f;
            return true;
        }

        /// <summary>
        /// <b>[함께 적용됨 H] 텍스트 대비는 플랫폼 분기 없이 한 벌이다.</b>
        ///
        /// <para>이 항목은 갭이 아니다 — 색과 대비 계산이 전부 <c>Interaction/UiChrome</c> 한 곳의
        /// 순수 산술이라 <b>두 플랫폼이 비트 단위로 같은 값을 쓴다</b>. 그래서 Ignore로 러너를
        /// 채우지 않고, 대신 그 "한 벌"이 깨지는 순간을 잡는 정식 검사로 둔다.</para>
        ///
        /// <para><b>값 자체는 여기서 다시 검사하지 않는다</b> — <c>UiInkHierarchyTests</c>가 네거티브
        /// 컨트롤(폐기된 잉크가 실제로 AA에 미달하는가, 대비식이 알려진 양 끝점을 내는가)까지 갖춰
        /// 이미 잠그고 있다. 같은 단언을 복제하면 한쪽만 갱신되는 그 실패가 다시 생긴다. 여기서는
        /// <b>플랫폼 관점</b>의 두 가지만 본다: 분기가 없는가, 그리고 그 잠금장치가 살아 있는가.</para>
        ///
        /// <para>남은 것은 <b>실기 캡처 대조</b>뿐이고 그건 색이 아니라 <b>래스터화</b>의 문제다
        /// (Windows의 그레이스케일 AA vs macOS의 서브픽셀/감마) — 성격상 위 G 항목과 같은 축이라
        /// "Windows 머신에서만 판정 가능한 항목" 목록에 함께 올린다.</para>
        /// </summary>
        [Test]
        public void 텍스트_대비_색은_플랫폼_분기_없이_한_벌이다()
        {
            string chromePath = Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", "UiChrome.cs");
            string chrome = StripLineComments(ReadSource(chromePath));

            foreach (string branch in new[] { "UNITY_STANDALONE_", "Application.platform", "SystemInfo.operatingSystem" })
            {
                Assert.IsFalse(chrome.Contains(branch),
                    $"UiChrome에 플랫폼 분기(\"{branch}\")가 들어왔습니다 — 색이 플랫폼마다 갈리는 " +
                    "순간 대비표는 한쪽에서만 참이 되고, 검증은 macOS에서만 돌아갑니다. " +
                    "정말 필요하다면 값이 아니라 **측정 경로**를 갈라야 합니다.");
            }

            // 대비 계산기와 하한이 실재하는가(= 위 '분기 없음'이 공허하지 않은가).
            Assert.IsNotNull(typeof(UiChrome).GetMethod(nameof(UiChrome.ContrastRatio)),
                "UiChrome.ContrastRatio가 사라졌습니다 — 대비 규칙이 어딘가로 흩어졌다는 뜻입니다.");
            Assert.Greater(UiChrome.MinTextContrast, UiChrome.MinNonTextContrast,
                "본문 글자 하한이 아이콘 하한보다 낮거나 같습니다 — WCAG 1.4.3(4.5:1)과 " +
                "1.4.11(3:1)의 관계가 뒤집혔습니다.");

            // 잠금장치가 살아 있는가. 이 파일이 값 검사를 복제하지 않는 근거 그 자체다.
            string tracker = Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Tests", "EditMode", "UiInkHierarchyTests.cs");
            Assert.IsTrue(File.Exists(tracker),
                $"대비 값을 잠그던 테스트가 사라졌습니다({tracker}) — 이 파일은 그 존재를 전제로 " +
                "값 검사를 생략하고 있으므로, 지금 대비는 **아무도 검사하지 않습니다**.");
            StringAssert.Contains("UiChrome.MinTextContrast", ReadSource(tracker),
                "잠금장치가 더 이상 본문 대비 하한을 참조하지 않습니다 — 숫자를 베껴 갔을 " +
                "가능성이 큽니다(CLAUDE.md: 테스트에 프로덕션 상수를 숫자로 베끼지 않는다).");
        }

        // ============================================================================
        // ★★ 구조 규약을 일반 검사로 — "정책은 중립, 플랫폼은 사실 조회" (2026-09-02 신설)
        // ============================================================================
        // CLAUDE.md: "정책 판정 로직은 플랫폼 중립 위치(Platform/)에 두고, 플랫폼 전용 코드는
        // '사실 조회'만 담당한다. 정책이 Platform/MacOS/ 안에 있으면 Windows가 물리적으로 호출할
        // 수 없다(실제 사고: FullscreenSuspendPolicy.cs)."
        //
        // 이 규약은 지금까지 **항목마다 손으로** 검사돼 왔다. 오늘까지 같은 형태가 세 번 나왔다:
        //   (1) FullscreenSuspendPolicy.cs 가 Platform/MacOS/ 안에 있었다.
        //   (2) 획 하한 환산이 MacOverlayStateEnforcer 안에 있었다(감사가 잡았다).
        //   (3) 그 환산의 `× lossyScale.x` 오류가 같은 자리에 함께 숨어 있었다.
        // 세 번 나왔으면 일반 검사로 만들 값이 있다. 아래가 그 시도이고, **무엇을 못 가르는지도
        // 함께 적는다** — 억지로 만든 검사는 오탐으로 무시당하고, 그게 이 저장소가 여러 번 당한 일이다.
        //
        // ────────────────────────────────────────────────────────────────────────────
        // ★ 판정: "판정 로직"을 일반적으로는 못 가른다. 세 갈래만 가른다
        // ────────────────────────────────────────────────────────────────────────────
        // 이상적인 검사는 "이 메서드는 OS를 안 부르는데 문턱 비교를 한다 = 정책이다"일 것이다.
        // 실제로 재 봤다: 플랫폼 두 폴더의 소스에서 **숫자 문턱 비교는 117곳**이다(2026-09-02 측정).
        // 그 대부분은 정책이 아니라 OS 사실이다 — 재시도 횟수, 창 스타일 비트, 폴링 간격, 퇴화
        // 사각형 방어, HRESULT 비교. 이것들을 정책이라고 부르기 시작하면 감사는 117건의 오탐을 내고,
        // 다음 사람은 검사를 끄거나 (더 나쁘게) 주석에서 사실을 지운다. 그래서 **이 축은 포기한다.**
        //
        // 대신 기계적으로 <b>거짓 양성이 0</b>인 세 갈래만 잠근다. 셋 다 실제 사고에서 유도됐다:
        //   R1 파일 이름 — Platform/MacOS|Windows 안에 `*Policy.cs`가 있으면 실패. (사고 1을 직접 잡는다)
        //   R2 타입 재정의 — 중립 정책 타입 이름을 플랫폼 폴더가 class/struct로 다시 선언하면 실패.
        //   R3 도메인 상수 유출 — 플랫폼 폴더의 **실행 코드**가 캐릭터 도메인 튜닝(StickConfig 등)을
        //      참조하면 실패. (사고 2를 직접 잡는다 — 그 코드는 StickConfig.MinStrokeScreenPoints를
        //      플랫폼 폴더 안에서 읽고 있었다)
        //
        // ★ R1이 사고 2를 못 잡았을 것이라는 점을 정직하게 적는다(그 파일 이름에는 Policy가 없다).
        //   R3이 그것을 잡는다. 반대로 R3은 사고 1을 못 잡는다(그 정책은 도메인 상수를 안 썼다).
        //   그래서 셋이 함께 있어야 하고, 그래도 **못 잡는 형태가 남는다**: 공유 상수도 안 쓰고
        //   이름에 Policy도 없는 **순수 인라인 판정**(예: `if (topInset <= h * 0.05)`를 플랫폼 파일
        //   안에 직접 적는 것). 그건 위에서 포기한 축이고, 지금은 사람이 리뷰로 잡는 수밖에 없다.

        /// <summary>R3이 감시하는 <b>캐릭터 도메인</b> 진입점. 플랫폼 코드가 이것들을 읽으면 그 순간
        /// 판정이 플랫폼 폴더 안으로 들어온 것이다(사실 조회는 OS만 묻는다).</summary>
        private static readonly string[] DomainTuningTokens =
        {
            "StickConfig.", "StickmanMetrics.", "DockGeometry.", "StickmanBlackboard.",
        };

        /// <summary>R1/R2가 "정책 타입"으로 보는 접미사.</summary>
        private static readonly string[] PolicyTypeSuffixes = { "Policy", "Geometry", "Solver", "Guard" };

        [Test]
        public void 구조규약_판정로직은_플랫폼_전용_폴더에_있으면_안된다()
        {
            string[] platformDirs =
            {
                Path.Combine(PlatformRoot, "MacOS"),
                Path.Combine(PlatformRoot, "Windows"),
            };

            var offenders = new List<string>();
            int scannedFiles = 0;

            // ---- R2 준비: 중립 위치(Platform/ 바로 아래)의 정책 타입 이름을 모은다 ----
            var neutralPolicyTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(PlatformRoot, "*.cs", SearchOption.TopDirectoryOnly))
            {
                foreach (string name in DeclaredTypeNames(StripLineComments(File.ReadAllText(file))))
                {
                    if (LooksLikePolicyName(name)) neutralPolicyTypes.Add(name);
                }
            }
            Assert.Greater(neutralPolicyTypes.Count, 0,
                "Platform/ 중립 위치에서 정책 타입을 하나도 찾지 못했습니다 — R2 스캔이 공허합니다. " +
                "(FullscreenSuspendPolicy / OverlayBoundsFitPolicy / BottomSafetyNetPolicy 등이 " +
                "거기 있어야 정상입니다.)");

            foreach (string dir in platformDirs)
            {
                Assert.IsTrue(Directory.Exists(dir),
                    $"플랫폼 폴더를 찾지 못했습니다({dir}) — 폴더 이름이 바뀌면 아래 스캔이 " +
                    "아무 파일도 안 보고 통과합니다(거짓 초록).");

                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    scannedFiles++;
                    string shown = Path.GetFileName(dir) + "/" + Path.GetFileName(file);

                    // ---- R1: 정책 파일이 플랫폼 폴더 안에 있는가 ----
                    if (IsPolicyFileMisplaced(file))
                    {
                        offenders.Add($"  · [R1] {shown} — 파일 이름이 정책을 뜻합니다. " +
                            "반대편 플랫폼이 물리적으로 호출할 수 없는 자리입니다(FullscreenSuspendPolicy 사고).");
                    }

                    string code = StripLineComments(File.ReadAllText(file));

                    // ---- R2: 중립 정책 타입을 여기서 다시 선언하는가 ----
                    foreach (string declared in DeclaredTypeNames(code))
                    {
                        if (!neutralPolicyTypes.Contains(declared)) continue;
                        offenders.Add($"  · [R2] {shown} — 중립 정책 타입 '{declared}'을 여기서 다시 " +
                            "선언합니다. 두 벌이 되는 순간 반드시 한쪽만 고쳐집니다.");
                    }

                    // ---- R3: 캐릭터 도메인 튜닝을 실행 코드에서 읽는가 ----
                    string[] lines = code.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string token = FindDomainTokenInCode(lines[i]);
                        if (token == null) continue;
                        offenders.Add($"  · [R3] {shown} — 실행 코드가 '{token}'을 읽습니다: " +
                            $"{lines[i].Trim()}\n        플랫폼 코드는 OS 사실만 조회해야 합니다. " +
                            "캐릭터 도메인 값을 여기서 읽으면 그 계산은 반대편 플랫폼이 재현할 수 " +
                            "없습니다(2026-09-01 획 하한 환산 사고의 정확한 형태).");
                    }
                }
            }

            // ★ 비공허성 잠금 — "위반 없음"이 "아무것도 안 봤음"과 구분되어야 한다.
            Assert.Greater(scannedFiles, 0,
                "플랫폼 전용 .cs를 한 개도 읽지 못했습니다 — 스캔이 공허합니다(거짓 초록).");

            Assert.IsEmpty(offenders,
                $"플랫폼 전용 폴더에 **판정 로직**이 들어왔습니다({offenders.Count}건, " +
                $"{scannedFiles}개 파일 스캔):\n" + string.Join("\n", offenders) +
                "\n\n규약(CLAUDE.md): 정책 판정 로직은 Platform/ 중립 위치에 두고, 플랫폼 전용 코드는 " +
                "사실 조회만 한다. 이 감사가 못 잡는 형태도 있다 — 공유 상수도 안 쓰고 이름에 Policy도 " +
                "없는 순수 인라인 판정. 그건 사람이 리뷰에서 잡아야 한다(이 테스트 위 주석 참고).");
        }

        /// <summary>R1: 플랫폼 전용 폴더 안의 <c>*Policy.cs</c>.</summary>
        private static bool IsPolicyFileMisplaced(string path)
        {
            string norm = path.Replace('\\', '/');
            bool inPlatformFolder = norm.Contains("/Platform/MacOS/") || norm.Contains("/Platform/Windows/");
            if (!inPlatformFolder) return false;
            return Path.GetFileNameWithoutExtension(norm).EndsWith("Policy", StringComparison.Ordinal);
        }

        private static bool LooksLikePolicyName(string typeName)
        {
            foreach (string suffix in PolicyTypeSuffixes)
            {
                if (typeName.EndsWith(suffix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary><c>class</c>/<c>struct</c>로 <b>선언된</b> 타입 이름들(주석은 이미 걷힌 소스 기준).</summary>
        private static IEnumerable<string> DeclaredTypeNames(string code)
        {
            foreach (string keyword in new[] { "class ", "struct " })
            {
                int at = 0;
                while (true)
                {
                    at = code.IndexOf(keyword, at, StringComparison.Ordinal);
                    if (at < 0) break;

                    // 앞 글자가 식별자면 'subclass ' 같은 부분일치다. 앞이 공백/줄바꿈일 때만 센다.
                    bool boundaryOk = at == 0 || !(char.IsLetterOrDigit(code[at - 1]) || code[at - 1] == '_');
                    at += keyword.Length;
                    if (!boundaryOk) continue;

                    int end = at;
                    while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_')) end++;
                    if (end > at) yield return code.Substring(at, end - at);
                }
            }
        }

        /// <summary>
        /// R3 스캐너 — 한 줄의 <b>실행 코드</b>에서 도메인 튜닝 토큰을 찾는다. 없으면 null.
        ///
        /// <para><b>문자열 리터럴 안은 세지 않는다.</b> 이 규칙이 없으면 실제로 오탐이 난다:
        /// <c>MacWindowService</c>가 <c>Debug.LogWarning("... StickConfig.dockFootholdWidthFraction
        /// 고정 추정으로 폴백합니다.")</c> 처럼 <b>사용자에게 보여 줄 문장 안에</b> 상수 이름을
        /// 적는다(2026-09-02 측정: 이 규칙 없이는 2건 오탐, 있으면 0건).</para>
        ///
        /// <para><b>식별자 경계도 본다.</b> <c>DefaultStickConfig.asset</c>은 <c>StickConfig.</c>를
        /// 부분 문자열로 포함한다 — 앞 글자가 식별자면 세지 않는다.</para>
        /// </summary>
        private static string FindDomainTokenInCode(string line)
        {
            string code = BlankStringLiterals(line);
            foreach (string token in DomainTuningTokens)
            {
                int at = 0;
                while (true)
                {
                    at = code.IndexOf(token, at, StringComparison.Ordinal);
                    if (at < 0) break;
                    bool boundaryOk = at == 0 || !(char.IsLetterOrDigit(code[at - 1]) || code[at - 1] == '_');
                    at += token.Length;
                    if (boundaryOk) return token;
                }
            }
            return null;
        }

        /// <summary>
        /// 문자열 리터럴의 <b>내용</b>을 공백으로 지우고 줄 끝 주석을 자른다.
        /// <see cref="ContainsGlyphInStringLiteral"/>의 정확한 반대편이며, 같은 상태 기계를 쓴다.
        /// </summary>
        private static string BlankStringLiterals(string line)
        {
            var sb = new StringBuilder(line.Length);
            bool inString = false, verbatim = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (!inString)
                {
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;   // 줄 끝 주석
                    sb.Append(c);
                    if (c == '"')
                    {
                        inString = true;
                        verbatim = i > 0 && line[i - 1] == '@';
                    }
                    continue;
                }

                if (!verbatim && c == '\\') { sb.Append("  "); i++; continue; }         // 이스케이프
                if (c == '"')
                {
                    if (verbatim && i + 1 < line.Length && line[i + 1] == '"') { sb.Append("  "); i++; continue; }
                    inString = false;
                    sb.Append('"');
                    continue;
                }
                sb.Append(' ');
            }
            return sb.ToString();
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>구조 규약 스캐너 셋이 실제로 구분하는가.</b>
        /// <para>위 검사는 전부 "없다"를 단언하므로 스캐너가 눈이 멀어도 초록이다. 게다가 오늘
        /// R1은 위반 0건이라 <b>한 번도 물지 않은 채</b> 통과한다. 그래서 위반형과 합법형을 둘 다
        /// 이 파일 안에 박제해 판정이 갈리는지 본다(비교 대상 양쪽을 다 얼린다).</para>
        /// </summary>
        [Test]
        public void 구조규약_스캐너는_위반과_합법을_실제로_구분한다()
        {
            // ---- R1 ----
            Assert.IsTrue(IsPolicyFileMisplaced("/x/Assets/_Project/Scripts/Platform/MacOS/FooPolicy.cs"),
                "플랫폼 폴더 안의 *Policy.cs를 못 잡습니다 — FullscreenSuspendPolicy 사고가 그대로 재발합니다.");
            Assert.IsTrue(IsPolicyFileMisplaced("/x/Assets/_Project/Scripts/Platform/Windows/BarPolicy.cs"),
                "Windows 쪽만 못 잡습니다 — 규칙이 한쪽에만 걸리면 그게 바로 이 파일이 막으려는 것입니다.");
            Assert.IsFalse(IsPolicyFileMisplaced("/x/Assets/_Project/Scripts/Platform/FooPolicy.cs"),
                "중립 위치의 정책 파일을 위반으로 셉니다 — 정책은 **거기 있어야** 합니다(오탐).");
            Assert.IsFalse(IsPolicyFileMisplaced("/x/Assets/_Project/Scripts/Platform/Windows/WindowsGameProcessProbe.cs"),
                "사실 조회 파일을 위반으로 셉니다(오탐).");

            // ---- R2 ----
            var declared = new List<string>(DeclaredTypeNames(
                "public static class FullscreenGeometry\n{\n}\npublic struct RECT\n{\n}\n"));
            CollectionAssert.Contains(declared, "FullscreenGeometry", "class 선언을 못 읽습니다.");
            CollectionAssert.Contains(declared, "RECT", "struct 선언을 못 읽습니다.");
            // 주석 제거는 **호출부의 몫**이다(DeclaredTypeNames 자신은 주석을 모른다). 그 조합이
            // 실제로 작동하는지를 본다 — 프로덕션 호출부가 하는 것과 똑같은 순서로.
            CollectionAssert.DoesNotContain(
                new List<string>(DeclaredTypeNames(StripLineComments("// class GhostPolicy\n"))),
                "GhostPolicy",
                "주석 속 선언이 타입으로 셉니다 — R2가 '중립 정책을 여기서 다시 선언한다'는 " +
                "거짓 위반을 만들어 냅니다(오탐 한 건이면 다음 사람은 검사를 끕니다).");
            CollectionAssert.Contains(
                new List<string>(DeclaredTypeNames(StripLineComments("    public sealed class RealPolicy\n"))),
                "RealPolicy",
                "주석을 걷어낸 뒤의 진짜 선언을 못 읽습니다 — 미탐입니다.");
            Assert.IsTrue(LooksLikePolicyName("BottomSafetyNetPolicy"), "Policy 접미사를 못 봅니다.");
            Assert.IsFalse(LooksLikePolicyName("Win32WindowService"), "서비스를 정책으로 셉니다(오탐).");

            // ---- R3 ----
            Assert.AreEqual("StickConfig.", FindDomainTokenInCode(
                "            float w = StickConfig.MinStrokeScreenPoints * scale;"),
                "실행 코드의 도메인 상수 참조를 못 잡습니다 — 2026-09-01 획 하한 환산 사고가 " +
                "그대로 되풀이됩니다.");
            Assert.IsNull(FindDomainTokenInCode(
                "                    \"StickConfig.dockFootholdWidthFraction 고정 추정으로 폴백합니다.\");"),
                "문자열 리터럴 안의 상수 이름을 위반으로 셉니다 — MacWindowService가 지금 실제로 " +
                "갖고 있는 줄입니다. 여기서 오탐이 나면 다음 사람은 검사를 끄거나 로그에서 사실을 지웁니다.");
            Assert.IsNull(FindDomainTokenInCode(
                "                    $\"... DefaultStickConfig.asset의 verboseDiagnosticsLogging 체크\");"),
                "식별자 경계를 안 봅니다 — 'DefaultStickConfig.'가 'StickConfig.'로 잘못 걸립니다.");
            Assert.IsNull(FindDomainTokenInCode(
                "        // StickConfig.verboseDiagnosticsLogging 스위치로 옮긴다"),
                "주석 줄을 위반으로 셉니다(오탐).");
            Assert.AreEqual("DockGeometry.", FindDomainTokenInCode(
                "            float drop = DockGeometry.ReferenceDockDropWorldUnits;   // 실측"),
                "줄 끝 주석이 붙은 실행 코드를 놓칩니다.");

            // BlankStringLiterals 자체도 얼린다(위 판정의 근거이므로).
            StringAssert.DoesNotContain("secret", BlankStringLiterals("var s = \"secret\";"),
                "문자열 내용을 지우지 못합니다.");
            StringAssert.Contains("var s =", BlankStringLiterals("var s = \"secret\";"),
                "문자열 밖의 코드까지 지웁니다 — 그러면 모든 검사가 눈이 멉니다.");
        }

        // ============================================================================
        // ★★ 감사 대장 — "갭"과 "결정"과 "역방향"을 뭉개지 못하게 하는 자물쇠 (2026-09-02 신설)
        // ============================================================================
        // 이 파일의 항목이 늘어나면서 실제로 생긴 문제: **전부 Assert.Ignore로 뭉치면 목록이 뜻을
        // 잃는다.** 러너에 "건너뜀 8건"이라고만 뜨면 그중 무엇이 진짜 갭이고 무엇이 의도된 차이인지
        // 아무도 모르고, 결국 "언젠가 다 고칠 것들"이라는 하나의 흐릿한 덩어리가 된다.
        //
        // 그래서 성격을 **이름**으로 드러내고, 그 규칙을 이 테스트가 기계적으로 지킨다.
        // 사람의 성실함에 기대지 않는다 — 그건 이 저장소가 여러 번 실패한 방식이다.

        /// <summary>갭 분류 접두사와 그 뜻. 이름 자체가 러너 목록에서 성격을 말하게 한다.</summary>
        private static readonly (string Prefix, string Meaning, bool MustBeIgnored)[] LedgerCategories =
        {
            ("미해결_",     "진짜 코드 갭 — 고칠 코드가 있다",                       true),
            ("실기미확인_", "코드는 닫혔고 Windows 하드웨어만 남았다",                true),
            ("결정_",       "의도된 차이 — 되돌리면 실패한다",                        false),
            ("역방향_",     "macOS가 뒤처진 쪽 — Windows 구현을 보호한다",            false),
            ("해당없음_",   "반대 플랫폼에 그 문제가 구조적으로 존재하지 않는다",     false),
            ("갭추적_",     "갭은 열렸으나 다른 테스트가 이미 러너에 띄운다",         false),
        };

        private static string SelfSourcePath => Path.Combine(Application.dataPath,
            "_Project", "Scripts", "Tests", "EditMode", "PlatformParityAuditTests.cs");

        /// <summary>
        /// ★ 대장 검사 — <b>접두사와 <c>Ignore</c> 사용이 서로를 배신하지 못하게</b> 양방향으로 잠근다.
        ///
        /// <list type="bullet">
        ///  <item><b>갭 이름인데 건너뛰지 않는다</b> → 이름만 남고 감시가 사라진 것이다. 누군가
        ///        "고쳤다"고 생각하며 Ignore를 지웠는데 검사는 안 넣은, 가장 조용한 실패다.</item>
        ///  <item><b>건너뛰는데 갭 이름이 아니다</b> → 목록에서 성격을 읽을 수 없다. 결정이 갭으로
        ///        보이면 누군가 "패리티 맞추자"며 되돌린다.</item>
        ///  <item><b>사유에 날짜가 없다</b> → 낡은 사유는 거짓말이다(2026-09-02, 실제로 이 파일의
        ///        네 항목 중 셋이 이미 뒤집힌 사용자 지시를 근거로 들고 있었다).</item>
        /// </list>
        ///
        /// <para><b>왜 이 테스트 자신은 걸리지 않는가</b>: 찾는 토큰을 소스에 통째로 적지 않고
        /// 두 조각으로 이어 붙인다. 그래서 이 메서드 본문에는 그 토큰이 <b>문자 그대로 존재하지
        /// 않는다</b>. 자기 자신을 예외 목록에 넣는 것보다 정직하다 — 예외 목록은 언젠가 커진다.</para>
        /// </summary>
        [Test]
        public void 감사_대장_모든_항목이_분류표에_들어_있다()
        {
            string ignoreToken = "Assert" + ".Ignore(";      // ← 이 파일에 문자 그대로 적지 않는다.

            Assert.IsTrue(File.Exists(SelfSourcePath),
                $"감사 자신의 소스를 찾지 못했습니다({SelfSourcePath}) — 파일이 옮겨졌다면 " +
                "SelfSourcePath를 갱신하세요. 그대로 두면 이 대장은 아무것도 검사하지 않습니다.");

            string source = File.ReadAllText(SelfSourcePath).Replace("\r\n", "\n");
            const string marker = "\n        public void ";

            var names = new List<string>();
            var bodies = new List<string>();
            int at = 0;
            while (true)
            {
                at = source.IndexOf(marker, at, StringComparison.Ordinal);
                if (at < 0) break;

                int nameStart = at + marker.Length;
                int paren = source.IndexOf('(', nameStart);
                if (paren < 0) break;

                int end = source.IndexOf("\n        }", nameStart, StringComparison.Ordinal);
                names.Add(source.Substring(nameStart, paren - nameStart));
                bodies.Add(end > nameStart ? source.Substring(nameStart, end - nameStart)
                                           : source.Substring(nameStart));
                at = nameStart;
            }

            // ★ 비공허성 잠금 ①: 테스트를 하나도 못 읽었으면 아래 전부가 공허하다.
            Assert.Greater(names.Count, 0,
                "이 파일에서 테스트 메서드를 한 개도 읽지 못했습니다 — 들여쓰기/서명 형식이 " +
                "바뀌었을 수 있습니다. 대장이 눈이 먼 상태로 초록입니다(거짓 초록).");

            // ★ 비공허성 잠금 ②: [Test] 개수와 맞아야 한다. 하나라도 어긋나면 스캐너가 놓친 것이 있다.
            int attributeCount = CountOccurrences(source, "\n        [Test]");
            Assert.AreEqual(attributeCount, names.Count,
                $"[Test] 특성은 {attributeCount}개인데 읽어 낸 테스트 메서드는 {names.Count}개입니다 — " +
                "대장이 일부 항목을 못 보고 있습니다. 못 본 항목은 어떤 분류 규칙도 적용받지 않으므로 " +
                "조용히 규칙 밖으로 빠져나갑니다.");

            var report = new StringBuilder();
            report.Append("[패리티 감사 대장] ").Append(names.Count).Append("개 항목\n");
            var problems = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                string body = bodies[i];
                bool skips = body.Contains(ignoreToken);

                string prefix = null;
                bool mustSkip = false;
                foreach ((string p, string _, bool must) in LedgerCategories)
                {
                    if (!name.StartsWith(p, StringComparison.Ordinal)) continue;
                    prefix = p;
                    mustSkip = must;
                    break;
                }

                string label = prefix ?? "(일반 검사)";
                counts.TryGetValue(label, out int n);
                counts[label] = n + 1;
                report.Append("  ").Append(skips ? "건너뜀 " : "검사   ").Append(label).Append('\t')
                      .Append(name).Append('\n');

                if (mustSkip && !skips)
                {
                    problems.Add($"  · {name}\n      갭 접두사 '{prefix}'인데 러너에 '건너뜀'으로 뜨지 " +
                        "않습니다. 갭이 닫혔다면 이름에서 접두사를 떼고 정식 검사로 바꾸세요. " +
                        "이름만 남기면 목록은 갭이라고 말하는데 러너는 초록이라, 아무도 이 갭을 " +
                        "다시 보지 않습니다.");
                }

                if (!mustSkip && skips)
                {
                    problems.Add($"  · {name}\n      '{label}' 항목인데 러너를 건너뜁니다. " +
                        "건너뛰는 항목은 반드시 갭 접두사(미해결_ / 실기미확인_)를 달아야 목록에서 " +
                        "성격이 읽힙니다 — 결정과 갭이 같은 회색으로 보이면 언젠가 결정 쪽이 " +
                        "'패리티 맞추기'로 되돌려집니다.");
                }

                if (skips && !body.Contains("2026-09"))
                {
                    problems.Add($"  · {name}\n      사유에 갱신 날짜(2026-09...)가 없습니다. " +
                        "낡은 사유는 거짓말입니다 — 2026-09-02에 이 파일의 세 항목이 이미 뒤집힌 " +
                        "사용자 지시를 근거로 들고 있었습니다.");
                }
            }

            foreach (KeyValuePair<string, int> kv in counts)
            {
                report.Append("  합계 ").Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
            }
            Debug.Log(report.ToString());

            Assert.IsEmpty(problems,
                $"패리티 감사 대장의 분류 규칙이 깨졌습니다({problems.Count}건):\n" +
                string.Join("\n", problems) + "\n\n분류표:\n" + DescribeLedgerCategories());
        }

        private static string DescribeLedgerCategories()
        {
            var sb = new StringBuilder();
            foreach ((string prefix, string meaning, bool mustSkip) in LedgerCategories)
            {
                sb.Append("  ").Append(prefix).Append('\t')
                  .Append(mustSkip ? "[건너뜀]" : "[정식 검사]").Append(' ')
                  .Append(meaning).Append('\n');
            }
            return sb.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, at = 0;
            while (true)
            {
                at = haystack.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0) return count;
                count++;
                at += needle.Length;
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>대장의 분류기가 실제로 판정을 가르는가.</b>
        /// <para>위 대장은 "문제 없음"을 단언하므로 분류기가 아무것도 안 해도 초록이다. 그래서
        /// 위반 두 형태와 합법 두 형태를 이 자리에 박제한다.</para>
        /// </summary>
        [Test]
        public void 감사_대장_분류기가_위반과_합법을_실제로_구분한다()
        {
            string ignoreToken = "Assert" + ".Ignore(";

            // 분류표에 실제로 두 종류(건너뜀 강제 / 정식 검사)가 다 있어야 규칙 자체가 성립한다.
            bool hasSkipKind = false, hasCheckKind = false;
            foreach ((string _, string __, bool mustSkip) in LedgerCategories)
            {
                if (mustSkip) hasSkipKind = true; else hasCheckKind = true;
            }
            Assert.IsTrue(hasSkipKind && hasCheckKind,
                "분류표가 한 종류로만 이뤄져 있습니다 — 그러면 대장의 양방향 검사 중 한쪽이 " +
                "영원히 발동하지 않습니다(반쪽짜리 자물쇠).");

            // 토큰 조립이 실제로 같은 문자열을 만드는가(이 트릭이 깨지면 대장이 통째로 눈이 먼다).
            Assert.AreEqual(14, ignoreToken.Length,
                "토큰 조립 결과가 예상 길이와 다릅니다 — 대장이 찾는 문자열이 실제 호출 형태와 " +
                "달라졌을 수 있습니다.");
            Assert.IsTrue(("            " + ignoreToken + "\"사유\");").Contains(ignoreToken),
                "조립한 토큰이 실제 호출 줄에 걸리지 않습니다 — 그렇다면 모든 '건너뜀' 판정이 " +
                "false가 되어 대장은 초록인 채로 아무것도 못 봅니다.");

            // 접두사 판정이 접두사와 부분일치를 구분하는가.
            Assert.IsTrue("미해결_Windows에는_가상데스크톱_동행_배선이_없다"
                    .StartsWith("미해결_", StringComparison.Ordinal),
                "갭 접두사를 못 읽습니다.");
            Assert.IsFalse("전체화면_판정_디바운스가_양_플랫폼에_모두_배선되어_있다"
                    .StartsWith("미해결_", StringComparison.Ordinal),
                "일반 검사를 갭으로 셉니다(오탐).");

            // 개수 세기 도구가 실제로 세는가.
            Assert.AreEqual(2, CountOccurrences("a\n        [Test]\nb\n        [Test]\n", "\n        [Test]"),
                "[Test] 개수 세기가 틀립니다 — 위 비공허성 잠금 ②가 무의미해집니다.");
            Assert.AreEqual(0, CountOccurrences("public void X()", "\n        [Test]"),
                "없는 것을 셉니다.");
        }

        /// <summary>macOS 조합키 글리프(Control·Option·Command). 스캔 대상이자 <b>이 감사의 내용</b>이다.</summary>
        private static readonly char[] MacGlyphs = { '\u2303', '\u2325', '\u2318' };

        /// <summary>
        /// 한 줄에서 <b>문자열 리터럴 안</b>에 그 글자가 있는가. 주석/식별자는 세지 않는다.
        /// <para>줄 단위로 도는 이유는 실패 메시지에 줄 번호를 실어 주기 위해서다. 여러 줄에 걸친
        /// 문자열은 이 저장소에 없다(전부 <c>" + "</c> 이어 붙이기다).</para>
        /// </summary>
        private static bool ContainsGlyphInStringLiteral(string line, char[] glyphs)
        {
            bool inString = false, verbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (!inString)
                {
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') return false;   // 줄 끝 주석
                    if (c == '"')
                    {
                        inString = true;
                        verbatim = i > 0 && line[i - 1] == '@';
                    }
                    continue;
                }

                if (!verbatim && c == '\\') { i++; continue; }                                  // 이스케이프
                if (c == '"')
                {
                    if (verbatim && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                    inString = false;
                    continue;
                }
                for (int g = 0; g < glyphs.Length; g++)
                {
                    if (c == glyphs[g]) return true;
                }
            }
            return false;
        }
    }
}
