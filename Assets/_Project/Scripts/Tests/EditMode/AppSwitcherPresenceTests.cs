using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-03 (dev-platform) — <b>앱 전환 표면 제외</b>의 규칙·배선·경계를 잠근다.
    ///
    /// <para>사용자 위임 판정 ③ "(가) 완전히 뺀다": 시작 시 우리 창에 <c>WS_EX_TOOLWINDOW</c>를
    /// 영구 부여해 macOS <c>NSApplicationActivationPolicyAccessory</c>와 대칭으로 만든다.
    /// 실행부는 <c>Platform/Windows/WindowsToolWindowStyleControl.cs</c>이고, 그 파일은 이 개발
    /// 머신의 활성 타깃(macOS)에서 <b>컴파일되지 않는다</b> — 그래서 이 파일은
    /// <b>(A) 순수 규칙은 실행해서</b>, <b>(B) 배선은 소스 텍스트로</b> 두 갈래로 검사한다
    /// (<c>PlatformParityAuditTests</c>가 세운 방식 그대로).</para>
    ///
    /// <para><b>이 파일이 증명하지 않는 것(정직하게)</b>: Windows 실기에서 실제로 Alt+Tab에서
    /// 사라지는가는 여기서 확인할 수 없다. 이 머신에 Windows가 없다. 잠그는 것은
    /// <b>규칙이 옳은가</b>와 <b>배선이 끊기지 않았는가</b>뿐이다.</para>
    /// </summary>
    public sealed class AppSwitcherPresenceTests
    {
        private const string LogPrefix = "[전환기제외-TEST]";

        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string PolicyPath =>
            Path.Combine(PlatformRoot, "AppSwitcherPresencePolicy.cs");

        private static string WinControlPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsToolWindowStyleControl.cs");

        private static string WinRemoverPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsTaskbarButtonRemover.cs");

        private static string WinEnforcerPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs");

        private static string MacEnforcerPath =>
            Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs");

        private static string MacNativePath =>
            Path.Combine(PlatformRoot, "MacOS", "MacSpaceBehaviorNative.cs");

        // ====================================================================
        // (A) 순수 규칙 — 실행해서 검사한다
        // ====================================================================

        /// <summary>
        /// ★ 비트 값 자체를 못박는다. <b>이것은 "프로덕션 상수를 베낀 것"이 아니다</b> —
        /// <c>0x00000080</c>은 Win32 SDK가 정한 <b>바깥의 사실</b>이고, 우리 상수가 그 사실과
        /// 일치하는지가 이 단언의 내용이다(CLAUDE.md: 기대값은 프로덕션 함수가 아니라 외부
        /// 근거에서 와야 한다). 누가 <c>0x00000008</c>(=<c>WS_EX_TOPMOST</c>)로 오타를 내면
        /// 클릭 관통 창이 <b>항상위 비트를 얻는</b> 전혀 다른 사고가 되는데, 그 오타는
        /// 이 머신에서 컴파일도 실행도 되지 않으므로 <b>여기 말고는 잡힐 곳이 없다</b>.
        /// </summary>
        [Test]
        public void A1_전환기_제외_비트는_Win32_SDK의_WS_EX_TOOLWINDOW다()
        {
            Assert.AreEqual(0x00000080L, AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                $"{LogPrefix} WS_EX_TOOLWINDOW의 SDK 값과 다릅니다. 이 상수는 우리 자기 창의 " +
                "GWL_EXSTYLE에 그대로 OR 되므로, 값이 틀리면 <엉뚱한 스타일 비트가 켜진다>.");

            // 이웃한 비트들과 실제로 다른 값인지까지 본다(오타는 대개 한 자리 차이로 난다).
            Assert.AreNotEqual(0x00000008L, AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                $"{LogPrefix} WS_EX_TOPMOST(0x8)와 같아졌습니다.");
            Assert.AreNotEqual(0x00000020L, AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                $"{LogPrefix} WS_EX_TRANSPARENT(0x20)와 같아졌습니다 — 클릭 관통 비트입니다.");
            Assert.AreNotEqual(0x00080000L, AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                $"{LogPrefix} WS_EX_LAYERED(0x80000)와 같아졌습니다.");
        }

        /// <summary>
        /// ★ <b>이 라운드에서 가장 무거운 단언</b> — 되쓰는 값이 <b>다른 비트를 하나도 잃지
        /// 않는가</b>. 우리 창에는 <c>WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST</c>가
        /// 동시에 서 있고, 그중 <c>WS_EX_TRANSPARENT</c> 하나만 떨어져도 <b>절대 불변 원칙 2
        /// (클릭 관통)</b>가 그 자리에서 깨진다. 절대값 대입으로 바꾸는 리팩터가 가장 위험하다.
        /// </summary>
        [Test]
        public void A2_되쓰는_값은_다른_확장스타일_비트를_하나도_잃지_않는다()
        {
            // 출하 형상에 가까운 값: LAYERED(0x80000) | TOPMOST(0x8) | TRANSPARENT(0x20)
            const long shipping = 0x00080000L | 0x00000008L | 0x00000020L;

            long next = AppSwitcherPresencePolicy.ComposeToolWindowExStyle(shipping);

            Assert.AreEqual(shipping, next & shipping,
                $"{LogPrefix} 기존 비트가 사라졌습니다 — 전=0x{shipping:X}, 후=0x{next:X}. " +
                "클릭 관통/항상위/레이어드 중 하나라도 떨어지면 원칙 2가 즉시 깨집니다.");
            Assert.IsTrue(AppSwitcherPresencePolicy.IsOutOfAppSwitcher(next),
                $"{LogPrefix} 정작 목표 비트가 서지 않았습니다.");
            Assert.AreEqual(shipping | AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit, next,
                $"{LogPrefix} 목표 비트 하나 말고 다른 것도 바뀌었습니다.");

            // 멱등: 이미 서 있는 값에 다시 걸어도 아무것도 안 바뀐다.
            Assert.AreEqual(next, AppSwitcherPresencePolicy.ComposeToolWindowExStyle(next),
                $"{LogPrefix} 멱등하지 않습니다 — 2초마다 도는 감시가 매번 값을 바꾸게 됩니다.");

            // 0(=읽기 실패 관례)이라도 산술 자체는 안전해야 한다. 쓸지 말지는 아래 A3가 정한다.
            Assert.AreEqual(AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit,
                AppSwitcherPresencePolicy.ComposeToolWindowExStyle(0L),
                $"{LogPrefix} 0 입력에서 예상 밖의 값이 나왔습니다.");
        }

        /// <summary>
        /// 판정 3갈래. <b>모를 때는 아무것도 쓰지 않는다</b>가 이 정책의 뼈대다 —
        /// 못 읽은 값을 근거로 되쓰면 다른 비트를 통째로 날린다.
        /// </summary>
        [Test]
        public void A3_스타일을_못_읽으면_아무것도_쓰지_않는다()
        {
            Assert.AreEqual(AppSwitcherStyleVerdict.Unknown,
                AppSwitcherPresencePolicy.DecideToolWindowApply(false, 0x00080028L),
                $"{LogPrefix} 읽기 실패인데 쓰기로 갔습니다.");

            // GetWindowLong 계열은 실패를 0으로 알린다 — 성공 플래그가 true여도 0은 '모른다'다.
            Assert.AreEqual(AppSwitcherStyleVerdict.Unknown,
                AppSwitcherPresencePolicy.DecideToolWindowApply(true, 0L),
                $"{LogPrefix} exStyle 0을 실제 값으로 믿었습니다 — WindowsLayeredHybridResolver / " +
                "WindowsWindowStyleProbe와 같은 판정이어야 합니다.");

            Assert.AreEqual(AppSwitcherStyleVerdict.Apply,
                AppSwitcherPresencePolicy.DecideToolWindowApply(true, 0x00080028L),
                $"{LogPrefix} 비트가 없는데 켜지 않습니다.");

            Assert.AreEqual(AppSwitcherStyleVerdict.AlreadyOut,
                AppSwitcherPresencePolicy.DecideToolWindowApply(
                    true, 0x00080028L | AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit),
                $"{LogPrefix} 이미 서 있는데 또 씁니다 — 24시간 상주 앱에서 불필요한 쓰기입니다.");

            // 되읽기 검증도 같은 규칙을 따른다.
            Assert.IsFalse(AppSwitcherPresencePolicy.VerifyApplied(false, 0x000800A8L),
                $"{LogPrefix} 되읽기에 실패했는데 성공으로 셌습니다.");
            Assert.IsFalse(AppSwitcherPresencePolicy.VerifyApplied(true, 0L),
                $"{LogPrefix} 되읽기 0(=실패)을 성공으로 셌습니다.");
            Assert.IsFalse(AppSwitcherPresencePolicy.VerifyApplied(true, 0x00080028L),
                $"{LogPrefix} 비트가 안 섰는데 성공으로 셌습니다 — 쓰기 반환값을 믿지 않는 " +
                "장치가 무의미해집니다.");
            Assert.IsTrue(AppSwitcherPresencePolicy.VerifyApplied(true, 0x000800A8L),
                $"{LogPrefix} 실제로 선 값을 실패로 셌습니다.");
        }

        /// <summary>
        /// ★★ <b>이 라운드가 과장 보고를 하지 못하게 막는 단언.</b>
        ///
        /// <para>스타일 한 번 얹기는 두 표면에 <b>정반대로</b> 작용한다. Alt+Tab은 전환기가
        /// 호출될 때 스타일로 거르므로 즉시 듣고, 작업표시줄 버튼은 셸이 <b>창을 보일 때</b>
        /// 만들어 둔 것이라 스타일 변경을 통보받지 못한다(Microsoft 문서의 처방: <c>SW_HIDE</c>
        /// → 스타일 변경 → 다시 보이기). 이 둘을 한 낱말로 뭉치는 순간 <b>"작업표시줄 버튼이
        /// 사라졌다"</b>는 확인되지 않은 주장이 보고서에 들어간다 — 이 저장소가 가장 자주 당한
        /// 사고의 형태다.</para>
        /// </summary>
        [Test]
        public void A4_작업표시줄_버튼은_스타일만으로_사라지지_않는다는_사실이_코드에_박혀_있다()
        {
            Assert.IsTrue(
                AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(AppSwitcherSurface.AltTabList),
                $"{LogPrefix} Alt+Tab이 '즉시 듣지 않는다'로 바뀌었습니다 — 그렇다면 이 라운드가 " +
                "얻는 것이 하나도 없고, 근거(전환기는 호출 시점에 스타일로 거른다)부터 다시 재야 합니다.");

            Assert.IsFalse(
                AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(AppSwitcherSurface.TaskbarButton),
                $"{LogPrefix} ★ 작업표시줄 버튼이 '즉시 사라진다'로 바뀌었습니다. 그렇게 바꾸려면 " +
                "실기 확인이 <먼저>여야 합니다(Windows 머신에서 ⌃⌥⌘K 없이 실행 → 작업표시줄 관찰). " +
                "코드만 고치면 이 저장소는 확인되지 않은 주장을 사실로 굳히게 됩니다.");

            // 두 표면이 실제로 서로 다른 답을 낸다 — 함수가 상수를 돌려주는 껍데기가 아님을 잠근다.
            Assert.AreNotEqual(
                AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(AppSwitcherSurface.AltTabList),
                AppSwitcherPresencePolicy.TakesEffectOnAlreadyShownWindow(AppSwitcherSurface.TaskbarButton),
                $"{LogPrefix} 두 표면이 같은 답을 냅니다 — 이 열거형의 존재 이유가 사라졌습니다.");
        }

        /// <summary>정책은 <b>플랫폼 중립</b>이어야 한다. 이 파일이 <c>Platform/MacOS/</c>나
        /// <c>Platform/Windows/</c>로 들어가면 반대편이 물리적으로 호출할 수 없다
        /// (실제 사고: <c>FullscreenSuspendPolicy</c>).</summary>
        [Test]
        public void A5_정책은_중립_위치에_있고_OS를_직접_부르지_않는다()
        {
            Assert.IsTrue(File.Exists(PolicyPath),
                $"{LogPrefix} AppSwitcherPresencePolicy가 Platform/ 바로 아래에 없습니다: {PolicyPath}");

            string src = StripCommentLines(File.ReadAllText(PolicyPath));
            StringAssert.DoesNotContain("UNITY_STANDALONE_", src,
                $"{LogPrefix} 판정에 플랫폼 분기가 들어왔습니다 — 그러면 Windows가 없는 이 머신의 " +
                "EditMode가 규칙을 실행해 검증할 수 없습니다(A1~A4가 통째로 죽습니다).");
            StringAssert.DoesNotContain("DllImport", src,
                $"{LogPrefix} 판정이 OS를 직접 부릅니다.");
            StringAssert.DoesNotContain("UnityEngine", src,
                $"{LogPrefix} 판정이 UnityEngine에 의존합니다 — 순수 함수로 남겨 두세요.");
        }

        /// <summary>
        /// ★ <b>상한이 없으면 24시간 상주 앱에서 COM 호출이 영원히 반복된다.</b>
        /// 그 규칙이 <c>Platform/Windows/</c> 안에 있으면 이 머신은 실행해 볼 방법이 없으므로
        /// 중립 정책에 두었고, 여기서 <b>실제로 실행해서</b> 잠근다.
        /// </summary>
        [Test]
        public void A6_작업표시줄_버튼_제거는_반드시_멈춘다()
        {
            const int max = 3;

            Assert.IsTrue(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, 0, max),
                $"{LogPrefix} 첫 시도조차 하지 않습니다 — 기능이 통째로 죽은 것입니다.");
            Assert.IsTrue(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, max - 1, max),
                $"{LogPrefix} 마지막 한 번을 남기고 멈춥니다(off-by-one).");
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, max, max),
                $"{LogPrefix} ★ 상한에 도달했는데 계속 시도합니다 — 24시간 상주 앱에서 COM 호출이 " +
                "영원히 반복됩니다.");
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, max + 9, max),
                $"{LogPrefix} 상한을 넘긴 뒤에도 시도합니다.");

            // 실패는 조용히 — 한 번 못 쓴다고 확인되면 다시 두드리지 않는다(리더 지시 1).
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, true, 0, max),
                $"{LogPrefix} COM이 못 쓴다고 확인됐는데 계속 재시도합니다 — 실패 로그가 도배됩니다.");

            // 되돌릴 문 — 환경변수 옵트아웃(리더 지시 2).
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(true, false, 0, max),
                $"{LogPrefix} 사용자가 껐는데도 시도합니다 — 재빌드 없는 탈출구가 사라집니다.");

            // 상한 0/음수 = 기능 전체 off. 경계에서 무한루프가 열리지 않아야 한다.
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, 0, 0),
                $"{LogPrefix} 상한 0인데 시도합니다.");
            Assert.IsFalse(AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(false, false, 0, -1),
                $"{LogPrefix} 상한이 음수인데 시도합니다.");
        }

        // ====================================================================
        // (B) 배선과 경계 — 소스 텍스트로 검사한다(반대 타깃이라 타입이 없다)
        // ====================================================================

        /// <summary>
        /// 실행부가 있고, <b>쓰기가 정확히 한 종류</b>이며, 대상이 <b>우리 자신의 창</b>인가.
        /// </summary>
        [Test]
        public void B1_Windows_실행부는_자기창_확장스타일_한_종류만_쓴다()
        {
            Assert.IsTrue(File.Exists(WinControlPath),
                $"{LogPrefix} WindowsToolWindowStyleControl이 없습니다: {WinControlPath}");

            string code = StripCommentLines(File.ReadAllText(WinControlPath));

            // 파일 전체가 #if 안이어야 macOS 타깃 컴파일에 끼지 않는다.
            StringAssert.StartsWith("#if UNITY_STANDALONE_WIN", code.TrimStart(),
                $"{LogPrefix} 파일 첫 줄이 #if UNITY_STANDALONE_WIN이 아닙니다 — macOS 타깃 빌드가 깨집니다.");

            // 쓰기는 SetWindowLongPtr 계열 하나뿐이고, 인덱스는 GWL_EXSTYLE 하나뿐이다.
            // ★★ 2026-09-03 — 이 단언은 <b>뒤집혔다.</b> 초안은 여기에 쓰기 진입점을 직접 두었고,
            //   LayeredHybridPolicyTests.자기창_스타일_쓰기_API는_해소기_한_파일에만_있다가
            //   러너에서 잡아냈다(연속 빨강 2회). 저장소 규약은 <b>자기 창 스타일 쓰기 API를
            //   WindowsLayeredHybridResolver 한 파일에 가두는 것</b>이고, 그 규약이 생긴 이유가
            //   바로 그 해소기가 검증 실패로 영구 비활성된 사고다.
            foreach (string writeApi in new[] { "SetWindowLongPtrW", "SetWindowLongW", "SetLayeredWindowAttributes(" })
            {
                Assert.AreEqual(0, CountOccurrences(code, writeApi),
                    $"{LogPrefix} 자기 창 스타일 <쓰기> API '{writeApi}'가 이 파일에 있습니다. " +
                    "규약상 그것은 WindowsLayeredHybridResolver 한 파일에만 존재해야 합니다 — " +
                    "여러 곳에서 쓰면 누가 무엇을 되돌렸는지 아무도 모릅니다.");
            }

            // 읽기는 여기 있어도 된다. 32비트 플레이어에는 GetWindowLongPtrW가 아예 없으므로
            // (별칭이 아니다) 분기가 살아 있어야 한다.
            StringAssert.Contains("EntryPoint = \"GetWindowLongPtrW\"", code,
                $"{LogPrefix} 64비트 읽기 진입점이 사라졌습니다.");
            StringAssert.Contains("EntryPoint = \"GetWindowLongW\"", code,
                $"{LogPrefix} 32비트 읽기 진입점이 사라졌습니다 — 그 빌드에서 " +
                "EntryPointNotFoundException으로 조용히 실패합니다.");
            StringAssert.Contains("IntPtr.Size == 8", code,
                $"{LogPrefix} 32/64비트 분기 자체가 사라졌습니다.");

            // 쓰기는 반드시 <대행>으로 간다. 이 줄이 없으면 비트가 영원히 서지 않는다.
            StringAssert.Contains("WindowsLayeredHybridResolver.TryAddExStyleBits(", code,
                $"{LogPrefix} 쓰기 대행 호출이 없습니다 — 규약을 지키면서 비트를 켜는 유일한 경로입니다.");
            StringAssert.Contains("private const int GwlExStyle = -20;", code,
                $"{LogPrefix} GWL_EXSTYLE(-20) 인덱스 선언이 사라졌거나 값이 바뀌었습니다.");

            // ★ '건드리지 않는 쪽'은 **식별자와 인덱스 값**으로 본다. 이름을 산문/로그에서
            //   금지하면 안 된다 — 정직하게 적을수록 감사가 빨개지면 다음 사람이 사실을 지운다
            //   (UserAssetImmutabilityAuditTests가 같은 이유로 판단 기준을 바꾼 전례가 있다).
            //   여기서 막는 것은 "GWL_STYLE(-16)을 실제로 쓰는 코드"뿐이다. 그쪽은 라이브러리의
            //   SetBorderless가 절대값(WS_VISIBLE|WS_POPUP)으로 되쓰는 인덱스라, 우리가 같이
            //   쓰면 서로의 값을 덮어쓴다.
            Assert.AreEqual(0, CountOccurrences(code, "GwlStyle"),
                $"{LogPrefix} GWL_STYLE 인덱스 식별자가 이 파일에 생겼습니다.");
            Assert.AreEqual(0, CountOccurrences(code, "= -16"),
                $"{LogPrefix} GWL_STYLE(-16) 인덱스 값이 이 파일에 생겼습니다 — 라이브러리의 " +
                "SetBorderless가 절대값으로 되쓰는 인덱스라 충돌합니다.");

            // 네거티브 컨트롤 — 위 두 '0건'이 <스캐너가 눈이 먼 결과>가 아님을 같은 자리에서 보인다.
            Assert.AreEqual(1, CountOccurrences(code, "GwlExStyle = -20"),
                $"{LogPrefix} 양성 대조 실패 — 같은 스캐너로 <있어야 하는 것>도 못 찾았습니다. " +
                "위의 0건은 무효입니다.");

            // 비트 값 사본을 두지 않는다(단일 출처는 중립 정책).
            Assert.AreEqual(0, CountOccurrences(code, "0x00000080"),
                $"{LogPrefix} 비트 값을 이 파일에 복사했습니다 — 단일 출처는 " +
                "AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit입니다(두 곳이 갈라지면 " +
                "이 머신에서는 컴파일도 안 되므로 아무도 못 봅니다).");
            StringAssert.Contains("AppSwitcherPresencePolicy.ComposeToolWindowExStyle(", code,
                $"{LogPrefix} 되쓸 값을 중립 정책이 만들지 않습니다 — 비트 보존 규칙(A2)이 " +
                "검사하는 대상과 실제 실행 경로가 갈라집니다.");
            StringAssert.Contains("AppSwitcherPresencePolicy.DecideToolWindowApply(", code,
                $"{LogPrefix} 판정을 중립 정책에 묻지 않습니다.");
            StringAssert.Contains("AppSwitcherPresencePolicy.VerifyApplied(", code,
                $"{LogPrefix} 되읽기 검증을 중립 정책에 묻지 않습니다.");

            // 대상 창은 우리 것 — 핸들 출처가 단 하나여야 한다.
            StringAssert.Contains("UniWinCNativeHandle.Resolve()", code,
                $"{LogPrefix} 핸들 출처가 UniWinCNativeHandle이 아닙니다 — 남의 창에 쓰지 않는다는 " +
                "보증이 이 한 줄에 걸려 있습니다(절대 불변 원칙 3).");
            Assert.AreEqual(0, CountOccurrences(code, "EnumWindows"),
                $"{LogPrefix} 창을 열거합니다 — 이 파일은 자기 창 하나만 다뤄야 합니다.");
            Assert.AreEqual(0, CountOccurrences(code, "FindWindow"),
                $"{LogPrefix} 남의 창을 찾습니다.");
        }

        /// <summary>
        /// ★ <b>승인된 예외와의 경계.</b> 작업표시줄 <b>자동 숨김 해제</b>(원칙 3의 승인된 예외
        /// 1건)는 셸의 <b>앱바</b>를 건드리는 일이고, 이 라운드는 <b>우리 창</b>의 스타일 비트다.
        /// 두 일이 한 낱말로 뭉치면 승인 범위가 조용히 넓어진다 —
        /// <c>UserAssetImmutabilityAuditTests</c>가 앱바 쓰기 메시지 5종을 금지하고 승인된 1건만
        /// <b>파일 1개 · 형태 2개</b>로 재검증하는데, 그 자물쇠가 이 새 파일에도 걸려 있어야 한다.
        /// </summary>
        [Test]
        public void B2_이_라운드는_앱바_예외를_한_글자도_넓히지_않는다()
        {
            string[] appbarNeedles =
            {
                "SHAppBarMessage", "ABM_SETSTATE", "ABM_SETPOS", "ABM_NEW",
                "ABM_REMOVE", "ABM_SETAUTOHIDEBAR",
            };

            var hits = new List<string>();
            foreach (string path in new[] { PolicyPath, WinControlPath, WinRemoverPath })
            {
                string code = StripCommentLines(File.ReadAllText(path));
                foreach (string needle in appbarNeedles)
                {
                    if (code.Contains(needle)) hits.Add($"{Path.GetFileName(path)}: {needle}");
                }
            }

            Assert.IsEmpty(hits,
                $"{LogPrefix} 이 라운드의 파일이 앱바 메시지를 씁니다({string.Join(", ", hits)}). " +
                "승인된 예외는 작업표시줄 <자동 숨김 비트 하나>뿐이고 그 자리는 " +
                "WindowsReservedBarAutoHideControl.cs 한 파일입니다(docs/TASKBAR_REVEAL.md).");

            // ★ 양성 대조 — 이 니들 목록이 <실제로 무언가를 찾을 수 있는> 상태인가.
            //   승인된 예외 파일에서는 반드시 잡혀야 한다. 여기서 0이 나오면 위의 "0건"은
            //   깨끗한 것이 아니라 <스캐너가 눈이 먼 것>이다(이 저장소 거짓 통과 4번째 형태).
            string approved = Path.Combine(PlatformRoot, "Windows", "WindowsReservedBarAutoHideControl.cs");
            Assert.IsTrue(File.Exists(approved),
                $"{LogPrefix} 승인된 예외 파일을 찾지 못했습니다 — 양성 대조를 세울 수 없습니다: {approved}");
            string approvedCode = StripCommentLines(File.ReadAllText(approved));
            Assert.IsTrue(approvedCode.Contains("SHAppBarMessage") && approvedCode.Contains("ABM_SETSTATE"),
                $"{LogPrefix} 양성 대조 실패 — 승인된 예외 파일에서도 앱바 이름을 못 찾았습니다. " +
                "위의 '0건'은 무효입니다(스캐너나 주석 제거기가 죽었습니다).");
        }

        /// <summary>
        /// ★ <b>기각된 대안이 몰래 들어오지 않았는가.</b> 리더 판정은 "(나) 숨김 중에만 토글은
        /// 기각" — <c>ShowWindow(SW_HIDE/SW_SHOW)</c> 왕복이 <c>WindowsOverlayStateEnforcer</c> /
        /// <c>WindowsTopmostWatchdog</c>의 topmost·layered·클릭관통 재적용과 충돌하기 때문이다.
        /// <c>ShowWindow(</c>는 <c>UserAssetImmutabilityAuditTests</c>의 금지 목록에도 있다.
        /// </summary>
        [Test]
        public void B3_기각된_ShowWindow_왕복이_들어오지_않았다()
        {
            foreach (string path in new[] { PolicyPath, WinControlPath, WinRemoverPath, WinEnforcerPath })
            {
                string code = StripCommentLines(File.ReadAllText(path));
                Assert.AreEqual(0, CountOccurrences(code, "ShowWindow("),
                    $"{LogPrefix} {Path.GetFileName(path)}에 ShowWindow 호출이 들어왔습니다. " +
                    "리더 판정으로 기각된 경로이고, 감사 금지 목록에도 있습니다. 되살리려면 " +
                    "UserAssetImmutabilityAuditTests의 화이트리스트를 여는 <별도 승인>이 먼저입니다.");
                Assert.AreEqual(0, CountOccurrences(code, "SW_HIDE"),
                    $"{LogPrefix} {Path.GetFileName(path)}에 SW_HIDE 상수가 들어왔습니다.");
            }
        }

        /// <summary>
        /// 배선 — <b>구현은 있는데 아무도 안 부른다</b>가 이 저장소가 반복해 겪은 조용한 실패다.
        /// 부착 1회 + 수명 감시 두 지점이 모두 살아 있어야 한다.
        /// </summary>
        [Test]
        public void B4_Windows_Enforcer가_부착시_1회와_수명감시_두_지점에서_부른다()
        {
            string code = StripCommentLines(File.ReadAllText(WinEnforcerPath));

            StringAssert.Contains("new WindowsToolWindowStyleControl()", code,
                $"{LogPrefix} 실행부를 만들지 않습니다 — 파일만 있고 한 번도 실행되지 않는 상태입니다.");
            StringAssert.Contains("_toolWindowStyle.ApplyOnce();", code,
                $"{LogPrefix} 창 부착 시 1회 호출이 없습니다.");
            StringAssert.Contains("_toolWindowStyle.Tick(", code,
                $"{LogPrefix} 수명 감시 호출이 없습니다 — 라이브러리가 확장 스타일을 되쓰는 " +
                "경로가 여럿이라 '한 번 걸고 끝'은 확인되지 않은 가정입니다.");

            // ★ COM 경로도 같은 루프에서 돌아야 한다(리더 판정 (b)). 상한 안에서 스스로 멈춘다.
            StringAssert.Contains("WindowsTaskbarButtonRemover.Tick(", code,
                $"{LogPrefix} 작업표시줄 버튼 제거가 배선되지 않았습니다 — 파일만 있고 한 번도 " +
                "실행되지 않는 상태입니다(이 저장소가 반복해 겪은 조용한 실패).");

            // 부착 1회 호출은 반드시 '부착 감지' 블록 안에 있어야 한다(= 카메라 배경 처리 직후).
            int attach = code.IndexOf("ApplyTransparentSafeCameraBackground();", StringComparison.Ordinal);
            int applyOnce = code.IndexOf("_toolWindowStyle.ApplyOnce();", StringComparison.Ordinal);
            int tick = code.IndexOf("_toolWindowStyle.Tick(", StringComparison.Ordinal);
            Assert.Greater(attach, -1, $"{LogPrefix} 부착 감지 블록을 못 찾았습니다.");
            Assert.Greater(applyOnce, attach,
                $"{LogPrefix} 1회 호출이 부착 감지보다 앞에 있습니다 — 창이 아직 없을 때 " +
                "핸들을 잡으려 들게 됩니다.");
            Assert.Greater(tick, applyOnce,
                $"{LogPrefix} 수명 감시가 부착 블록보다 앞입니다.");

            // 재적용 상한(ReapplyAttempts) return 보다 **위**여야 앱 수명 내내 돈다.
            int bail = code.IndexOf("if (_appliedCount >= ReapplyAttempts) return;", StringComparison.Ordinal);
            Assert.Greater(bail, -1, $"{LogPrefix} 재적용 상한 return을 못 찾았습니다.");
            Assert.Less(tick, bail,
                $"{LogPrefix} 수명 감시가 재적용 상한 return **아래**에 있습니다 — 기동 2.5초 뒤 " +
                "영원히 돌지 않습니다. WindowsTopmostWatchdog이 정확히 이 실수로 " +
                "'엑셀 클릭하면 캐릭터가 창 뒤로 넘어감'을 3번 재발시켰습니다.");
        }

        /// <summary>
        /// ★ <b>두 플랫폼이 같은 자리에서 같은 목적을 이루는가.</b> 이번 변경으로 처음 대칭이
        /// 됐다 — macOS는 부착 시점에 activation policy를 accessory로 내리고, Windows는 같은
        /// 시점에 <c>WS_EX_TOOLWINDOW</c>를 얹는다.
        /// </summary>
        [Test]
        public void B5_macOS와_Windows가_같은_부착_시점에_전환기에서_빠진다()
        {
            string mac = StripCommentLines(File.ReadAllText(MacEnforcerPath));
            StringAssert.Contains("ApplyAccessoryActivationPolicyOnce();", mac,
                $"{LogPrefix} macOS 쪽 대응물이 사라졌습니다 — 그러면 이번 Windows 변경은 " +
                "대칭이 아니라 <Windows만 다른> 상태가 됩니다.");

            int macAttach = mac.IndexOf("ApplyTransparentSafeCameraBackground();", StringComparison.Ordinal);
            int macApply = mac.IndexOf("ApplyAccessoryActivationPolicyOnce();", StringComparison.Ordinal);
            Assert.Greater(macAttach, -1, $"{LogPrefix} macOS 부착 블록을 못 찾았습니다.");
            Assert.Greater(macApply, macAttach,
                $"{LogPrefix} macOS 쪽 호출 자리가 부착 블록 밖으로 나갔습니다 — 두 플랫폼의 " +
                "'같은 자리'라는 성질이 깨집니다.");

            // 기전 이름은 중립 정책의 상수에서 가져온다(문자열을 베끼지 않는다 — CLAUDE.md).
            string macNative = StripCommentLines(File.ReadAllText(MacNativePath));
            StringAssert.Contains(AppSwitcherPresencePolicy.MacOsMechanismName, macNative,
                $"{LogPrefix} macOS 네이티브에 {AppSwitcherPresencePolicy.MacOsMechanismName}가 " +
                "없습니다 — 중립 정책이 적어 둔 '대응 기전'이 실재하지 않습니다(문서만 남은 대칭).");
        }

        /// <summary>
        /// ★★ <b>COM 경로가 할 수 있는 일이 정확히 하나인가.</b>
        ///
        /// <para><c>ITaskbarList</c>의 vtable에는 <c>ActivateTab</c>이 들어 있고, 그것은 <b>남의 창을
        /// 활성화</b>할 수 있어 절대 불변 원칙 2/3에 정면으로 걸린다. 슬롯을 지울 수는 없다(ABI가
        /// 깨진다). 그래서 <b>이름을 지워</b> 부를 수 없게 만들었고, 그 상태를 여기서 다시 센다 —
        /// "hwnd를 손에 쥔 코드 옆에 남의 창을 조작하는 호출을 붙이는 것은 매우 자연스럽고,
        /// 그 순간 원칙 3이 조용히 무너진다"(UserAssetImmutabilityAuditTests).</para>
        /// </summary>
        [Test]
        public void B6_COM_경로는_DeleteTab_하나만_할_수_있다()
        {
            Assert.IsTrue(File.Exists(WinRemoverPath),
                $"{LogPrefix} WindowsTaskbarButtonRemover가 없습니다: {WinRemoverPath}");

            string code = StripCommentLines(File.ReadAllText(WinRemoverPath));

            StringAssert.StartsWith("#if UNITY_STANDALONE_WIN", code.TrimStart(),
                $"{LogPrefix} 파일 첫 줄이 #if UNITY_STANDALONE_WIN이 아닙니다 — macOS 타깃 빌드가 깨집니다.");

            // ---- (1) 위험한 슬롯 이름이 코드에 없어야 한다 ----
            foreach (string forbidden in new[] { "ActivateTab", "SetActiveAlt", "AddTab" })
            {
                Assert.AreEqual(0, CountOccurrences(code, forbidden),
                    $"{LogPrefix} '{forbidden}' 이름이 코드에 생겼습니다. vtable 슬롯은 " +
                    "ReservedSlotN으로 이름을 지워 두는 것이 이 파일의 안전 설계입니다 — " +
                    "특히 ActivateTab은 남의 창을 활성화할 수 있습니다(원칙 2/3).");
            }

            // ---- (2) 실제 호출은 DeleteTab 하나뿐 ----
            Assert.AreEqual(1, CountOccurrences(code, "_list.DeleteTab("),
                $"{LogPrefix} DeleteTab 호출 수가 1이 아닙니다 — 이 파일의 유일한 능력이어야 합니다.");
            Assert.AreEqual(1, CountOccurrences(code, "list.HrInit();"),
                $"{LogPrefix} HrInit 호출이 1회가 아닙니다 — COM 규약상 첫 호출이어야 하고, " +
                "여러 번 부를 이유가 없습니다(캐시된 인터페이스를 재사용하므로 1회면 충분합니다).");
            Assert.AreEqual(0, CountOccurrences(code, "_list.ReservedSlot"),
                $"{LogPrefix} 자리표시자 슬롯을 실제로 부릅니다 — 그 슬롯들은 순서를 맞추기 " +
                "위해서만 존재합니다.");

            // ---- (3) GUID 두 개가 서로 다르고 문서 값과 같은가 ----
            //     끝 한 자리(344 vs 342)만 다르다. 바꿔 적으면 개체 생성이 <조용히> 실패하고,
            //     이 머신에서는 실행으로 확인할 방법이 없다 — 그래서 여기서 잠근다.
            const string clsid = "56FDF344-FD6D-11D0-958A-006097C9A090";   // CLSID_TaskbarList (문서 값)
            const string iid = "56FDF342-FD6D-11D0-958A-006097C9A090";     // IID_ITaskbarList  (문서 값)
            Assert.AreNotEqual(clsid, iid,
                $"{LogPrefix} (자기 점검) 이 테스트가 두 GUID를 같은 값으로 들고 있습니다 — " +
                "그러면 아래 두 단언이 같은 것을 두 번 확인하는 껍데기가 됩니다.");

            Assert.AreEqual(1, CountOccurrences(code, "TaskbarListClsid = \"" + clsid + "\""),
                $"{LogPrefix} CLSID_TaskbarList 상수가 문서 값이 아니거나 1회가 아닙니다. " +
                "IID와 끝 한 자리(344 vs 342)만 달라서 바꿔 적기 쉽고, 바꿔 적으면 개체 생성이 " +
                "<조용히> 실패합니다 — 이 머신에서는 실행으로 확인할 방법이 없습니다.");
            Assert.AreEqual(1, CountOccurrences(code, "TaskbarListIid = \"" + iid + "\""),
                $"{LogPrefix} IID_ITaskbarList 상수가 문서 값이 아니거나 1회가 아닙니다.");

            // ---- (4) 실패는 조용히 / 되돌릴 문 ----
            StringAssert.Contains("catch (Exception", code,
                $"{LogPrefix} 예외를 삼키지 않습니다 — COM이 없는 런타임에서 부팅이 깨집니다(리더 지시 1).");
            StringAssert.Contains("Application.quitting", code,
                $"{LogPrefix} 종료 시 COM 해제 훅이 없습니다(리더 지시 2).");
            StringAssert.Contains("Marshal.ReleaseComObject", code,
                $"{LogPrefix} COM 참조를 해제하지 않습니다.");
            StringAssert.Contains("STICKMATE_KEEP_TASKBAR_BUTTON", code,
                $"{LogPrefix} 재빌드 없이 끌 수 있는 환경변수가 없습니다 — 실기에서 무언가 " +
                "이상할 때 돌아갈 길이 사라집니다(WindowsLayeredHybridResolver의 관례).");

            // ---- (5) 상한 판정은 중립 정책이 내린다 ----
            StringAssert.Contains("AppSwitcherPresencePolicy.ShouldAttemptTaskbarButtonRemoval(", code,
                $"{LogPrefix} 상한 판정이 플랫폼 코드 안으로 들어갔습니다 — 그러면 A6이 " +
                "검사하는 규칙과 실제 실행 경로가 갈라집니다.");

            // ---- (6) 창 상태를 건드리지 않는다(이 경로를 택한 이유 그 자체) ----
            foreach (string forbidden in new[] { "ShowWindow(", "SetWindowPos(", "SetWindowLong", "SW_HIDE" })
            {
                Assert.AreEqual(0, CountOccurrences(code, forbidden),
                    $"{LogPrefix} '{forbidden}'가 들어왔습니다 — 이 경로를 택한 <유일한> 이유가 " +
                    "'창 상태를 한 비트도 안 건드린다'입니다. 건드리는 순간 기각된 (a)와 같아지고, " +
                    "재적용 감시자들과 다투게 됩니다.");
            }

            // ---- (7) 양성 대조 — 스캐너가 이 파일을 실제로 읽고 있는가 ----
            Assert.Greater(CountOccurrences(code, "ITaskbarList"), 0,
                $"{LogPrefix} 양성 대조 실패 — 같은 스캐너로 <있어야 하는 것>도 못 찾았습니다. " +
                "위의 0건은 전부 무효입니다.");
        }

        /// <summary>
        /// ★★ <b>대행 창구가 "켜기만" 하는가.</b> 이것이 초안보다 안전해진 이유의 전부다.
        ///
        /// <para>임의 값을 대입하는 창구였다면 호출자가 실수로 <c>WS_EX_TRANSPARENT</c>를 지워
        /// <b>클릭 관통(절대 불변 원칙 2)</b>을 그 자리에서 깨뜨릴 수 있다. 비트를 <b>끄는</b>
        /// 능력은 해소기 안의 원래 경로에만 남아 있어야 하고, 그쪽에는 대조군·실험군·되돌림이
        /// 붙어 있다.</para>
        ///
        /// <para>이 검사가 소스 텍스트인 이유: <c>Platform/Windows/</c>는 이 머신의 활성 타깃에서
        /// <b>컴파일되지 않으므로</b> 함수를 불러 볼 수 없다. 같은 이유로 이 규약 위반을 잡은 것도
        /// 타입 감사가 아니라 <b>소스를 읽는 감사</b>였다.</para>
        /// </summary>
        [Test]
        public void B7_스타일_쓰기_대행창구는_비트를_끄지_못한다()
        {
            string resolverPath = Path.Combine(PlatformRoot, "Windows", "WindowsLayeredHybridResolver.cs");
            Assert.IsTrue(File.Exists(resolverPath),
                $"{LogPrefix} 쓰기 대행이 사는 파일이 없습니다: {resolverPath}");

            string code = StripCommentLines(File.ReadAllText(resolverPath));
            const string signature = "internal static bool TryAddExStyleBits(";
            int at = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(at, -1,
                $"{LogPrefix} 쓰기 대행 창구가 사라졌습니다 — 그러면 앱 전환기 제외 비트가 " +
                "영원히 서지 않습니다(그리고 그 실패는 조용합니다).");

            // 함수 본문만 잘라 낸다 — 8칸 들여쓰기 닫는 중괄호가 메서드의 끝이다
            // (PlatformParityAuditTests의 대장 스캐너와 같은 규약).
            int bodyEnd = code.IndexOf("\n        }", at, StringComparison.Ordinal);
            Assert.Greater(bodyEnd, at,
                $"{LogPrefix} 대행 창구의 본문 끝을 찾지 못했습니다 — 들여쓰기 규약이 바뀌었다면 " +
                "이 검사는 <파일 전체>를 보게 되어 엉뚱한 곳에서 빨개집니다.");
            string body = code.Substring(at, bodyEnd - at);

            StringAssert.Contains("ex | bitsToAdd", body,
                $"{LogPrefix} OR로 켜지 않습니다 — 다른 비트가 보존된다는 보증이 사라집니다.");
            Assert.AreEqual(0, CountOccurrences(body, "& ~"),
                $"{LogPrefix} ★ 대행 창구가 비트를 <끌> 수 있게 됐습니다. 그 순간 호출자가 실수로 " +
                "WS_EX_TRANSPARENT를 지워 클릭 관통(원칙 2)을 깨뜨릴 수 있습니다 — 끄는 능력은 " +
                "대조군/되돌림이 붙은 해소기 본래 경로에만 있어야 합니다.");

            // 네거티브 컨트롤 — 같은 스캐너가 "끄는 코드"를 실제로 볼 수 있는가.
            //   해소기 <전체>에는 끄는 경로(SetLayered의 되돌림)가 살아 있어야 한다. 여기서 0이면
            //   위의 0건은 '깨끗함'이 아니라 '스캐너가 눈이 먼 것'이다.
            Assert.Greater(CountOccurrences(code, "& ~"), 0,
                $"{LogPrefix} 양성 대조 실패 — 해소기 전체에서 비트를 끄는 코드를 하나도 찾지 " +
                "못했습니다. 위의 0건은 무효입니다(그리고 되돌림 경로가 사라졌다면 그 자체가 사고입니다).");
        }

        // ====================================================================
        // (C) 네거티브 컨트롤 — 위 스캐너들이 <실제로 볼 수 있는가>
        // ====================================================================

        /// <summary>
        /// ★ 위 B1~B5는 전부 "있다/없다"를 단언한다. 스캐너가 눈이 멀면 <b>없다 쪽이 조용히
        /// 전부 초록</b>이 된다(이 저장소 거짓 통과의 대표 형태). 그래서 주석 제거기와 카운터가
        /// 실제로 일을 하는지 여기서 박제한다.
        /// </summary>
        [Test]
        public void C1_주석제거기와_카운터가_실제로_구분한다()
        {
            const string sample =
                "        // ShowWindow(hwnd, SW_HIDE);\n" +
                "        /// <c>ShowWindow(</c> 금지\n" +
                "         * ShowWindow(\n" +
                "        int x = 1;   // ShowWindow( 꼬리 주석은 남는다\n";

            string stripped = StripCommentLines(sample);

            Assert.AreEqual(1, CountOccurrences(stripped, "ShowWindow("),
                $"{LogPrefix} 주석 제거기가 <줄 전체가 주석인 줄>만 비우는 규약을 벗어났습니다. " +
                "UserAssetImmutabilityAuditTests.BlankOutCommentLines와 같은 규약이어야 " +
                "두 감사가 같은 것을 봅니다.");

            Assert.AreEqual(0, CountOccurrences("아무것도 없다", "ShowWindow("),
                $"{LogPrefix} 없는 것을 셉니다(오탐).");
            Assert.AreEqual(2, CountOccurrences("aXbXc", "X"),
                $"{LogPrefix} 개수 세기가 틀립니다.");

            // 실제 파일에서도 볼 수 있는가 — 경로가 틀리면 위 검사들이 통째로 무의미해진다.
            Assert.IsTrue(File.Exists(WinControlPath), $"{LogPrefix} 경로 오타: {WinControlPath}");
            Assert.IsTrue(File.Exists(WinEnforcerPath), $"{LogPrefix} 경로 오타: {WinEnforcerPath}");
            Assert.IsTrue(File.Exists(MacEnforcerPath), $"{LogPrefix} 경로 오타: {MacEnforcerPath}");
            Assert.IsTrue(File.Exists(MacNativePath), $"{LogPrefix} 경로 오타: {MacNativePath}");
            StringAssert.Contains("WindowsToolWindowStyleControl",
                File.ReadAllText(WinControlPath),
                $"{LogPrefix} 실행부 파일을 읽었는데 클래스 이름이 없습니다 — 엉뚱한 파일입니다.");
        }

        // ====================================================================
        // 도구
        // ====================================================================

        /// <summary><b>줄 전체가 주석인 줄</b>만 비운다 —
        /// <c>UserAssetImmutabilityAuditTests.BlankOutCommentLines</c>와 <b>같은 규약</b>이다.
        /// 두 감사가 다른 규약을 쓰면 같은 파일에 대해 서로 다른 판정을 낸다.</summary>
        private static string StripCommentLines(string source)
        {
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)
                    || t.StartsWith("*", StringComparison.Ordinal))
                {
                    lines[i] = string.Empty;
                }
            }
            return string.Join("\n", lines);
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
    }
}
