using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
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
        /// ★ 2026-09-02 신설 분기 — <c>Platform/IReservedTopBarService</c> + <c>SurfaceSafeAreaPolicy</c>.
        ///
        /// <para><b>정책은 이미 플랫폼 중립이다</b>(<c>SurfaceSafeAreaPolicy</c> = OS 호출 0줄). 갈라진
        /// 것은 <b>사실 조회</b>뿐이며, 그것이 이 감사가 존재하는 이유다 —
        /// <c>FullscreenSuspendPolicy</c> 사고(정책이 <c>Platform/MacOS/</c> 안에 있어 Windows가
        /// 물리적으로 호출할 수 없었다)를 되풀이하지 않았는지 여기서 확인한다.</para>
        ///
        /// <para>① macOS 구현이 실제로 있는가 ② 정책이 중립 위치에 있는가 — 둘은 <b>정식 검사</b>다.
        /// ③ Windows 사실 조회가 없다는 사실만 <c>Assert.Ignore</c>로 러너에 계속 띄운다.</para>
        /// </summary>
        [Test]
        public void 미해결_Windows에는_상단_예약띠_조회가_없다()
        {
            string policy = Path.Combine(PlatformRoot, "SurfaceSafeAreaPolicy.cs");
            string contract = Path.Combine(PlatformRoot, "IReservedTopBarService.cs");

            // ① 정책과 계약은 <b>중립 위치</b>(Platform/ 바로 아래)에 있어야 한다.
            Assert.IsTrue(File.Exists(policy),
                "SurfaceSafeAreaPolicy가 Platform/ 중립 위치에 없습니다 — 정책이 플랫폼 폴더 안으로 " +
                "들어가면 반대편 플랫폼이 물리적으로 호출할 수 없습니다(FullscreenSuspendPolicy 사고).");
            Assert.IsTrue(File.Exists(contract),
                "IReservedTopBarService가 Platform/ 중립 위치에 없습니다.");

            string policySource = StripLineComments(ReadSource(policy));
            StringAssert.DoesNotContain("UNITY_STANDALONE_", policySource,
                "정책 파일에 플랫폼 분기가 들어왔습니다 — 이 파일은 순수 산술이어야 하고, 그래야 " +
                "양쪽 플랫폼이 같은 규칙을 씁니다.");

            // ② macOS 사실 조회가 실제로 있는가.
            string macProbe = Path.Combine(PlatformRoot, "MacOS", "MacReservedTopBarService.cs");
            Assert.IsTrue(File.Exists(macProbe),
                "macOS 상단 인셋 조회가 사라졌습니다 — 팝오버가 다시 메뉴바를 덮습니다(원칙 2).");

            // ③ ★ 2026-09-02 승격 — 사용자 지시가 "맥에 적용한 사항 윈도우에도 모두 적용"으로 바뀌면서
            //    Win32WindowService가 이 인터페이스를 구현했다. 이제 <b>정식 검사</b>다.
            //    (이 자리는 하루 전만 해도 Assert.Ignore였다. Ignore로 러너에 계속 띄워 둔 것이
            //     실제로 다음 라운드에 배정되어 닫혔다 — 그게 Ignore를 쓰는 이유다.)
            string win = StripLineComments(ReadSource(WinWindowServicePath));
            Assert.IsTrue(win.Contains("IReservedTopBarService"),
                "Win32WindowService가 IReservedTopBarService를 더 이상 구현하지 않습니다 — " +
                "Windows에서 상단 인셋이 0으로 떨어지고, 상단 도킹 작업표시줄 아래에 팝오버가 " +
                "다시 겹칩니다. 필요한 값은 GetMonitorInfo의 rcWork.Top − rcMonitor.Top 한 줄이며 " +
                "IReservedBottomBarService 구현이 이미 같은 호출에서 rcWork/rcMonitor를 읽습니다.");
            Assert.IsTrue(win.Contains("TryGetReservedTopInsetPoints"),
                "Windows 구현에 TryGetReservedTopInsetPoints 본문이 없습니다 — 인터페이스 이름만 " +
                "달려 있고 조회가 비어 있으면 그건 구현이 아니라 서명입니다.");

            // ④ 양쪽이 <b>같은 중립 정책</b>을 부르는가 — 갈라지면 그 순간 규칙이 두 벌이 된다.
            string macProbeSource = StripLineComments(ReadSource(macProbe));
            StringAssert.DoesNotContain("ClampCenterY", macProbeSource,
                "macOS 사실 조회 안에 클램프 산술이 들어왔습니다 — 정책이 플랫폼 폴더로 새면 " +
                "Windows가 물리적으로 호출할 수 없습니다(FullscreenSuspendPolicy 사고).");
            StringAssert.DoesNotContain("ClampCenterY", win,
                "Windows 사실 조회 안에 클램프 산술이 들어왔습니다 — 같은 이유로 금지입니다.");
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

            Assert.Ignore("【알려진 미판단 항목 · 리더 배정 대기】\n" +
                "항목: 화면 <b>하단</b> 예약 띠를 표면 배치에서 강제할 것인가.\n" +
                "macOS: 일부러 강제하지 않는다 — Dock은 자동 숨김이 흔하고, 이 앱은 그 위를 " +
                "의도적으로 캐릭터 발판으로 쓴다(Core/DockGeometry). 창이 Dock을 덮는 것은 " +
                "macOS의 모든 앱이 하는 표준 동작이기도 하다.\n" +
                "Windows: 사정이 다르다 — 작업표시줄은 가로 전체를 점유하고, " +
                "'작업표시줄에 걸쳐서 돌아다닌다'는 실제 사용자 신고 이력이 있다(2026-08-31). " +
                "하단도 강제해야 할 수 있다.\n" +
                "★ 지금 막혀 있는 것은 코드가 아니라 <b>판정</b>이다. 조회는 이미 양쪽 다 있다 " +
                "(IReservedBottomBarService). 실기 확인이 필요하다.");
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
        /// <b>알려진 미해결 항목</b>: macOS는 <c>MacSpaceBehaviorNative</c>로 "모든 Space에 따라붙기"를
        /// 걸어 타 앱 전체화면 위에서도 캐릭터가 남게 했다. Windows의 대응 개념은 <b>가상 데스크톱</b>인데
        /// 대응물이 없어, Windows 사용자가 데스크톱 2로 전환하면 캐릭터가 데스크톱 1에 남는다.
        /// </summary>
        [Test]
        public void 미해결_Windows에는_가상데스크톱_동행_배선이_없다()
        {
            string winDir = Path.Combine(PlatformRoot, "Windows");
            foreach (string f in Directory.GetFiles(winDir, "*.cs"))
            {
                if (StripLineComments(File.ReadAllText(f)).Contains("IVirtualDesktopManager"))
                {
                    Assert.Pass($"{Path.GetFileName(f)}에 가상 데스크톱 배선이 들어왔습니다 — " +
                        "이 테스트를 정식 검사로 승격하세요.");
                }
            }

            Assert.Ignore("【알려진 패리티 결함 · 리더 배정 대기】\n" +
                "macOS: MacSpaceBehaviorNative(.canJoinAllSpaces + accessory 등급)로 모든 Space에 따라붙는다.\n" +
                "Windows: 대응물 없음 — 가상 데스크톱을 전환하면 캐릭터가 원래 데스크톱에 남는다.\n" +
                "후보 경로: IVirtualDesktopManager(공개 COM)로 소속 확인, 핀 고정은 비공개 API라 " +
                "정책 판단이 먼저 필요. 실기 검증 필요 — 사용자 Windows 머신에서 확인해야 함.");
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
        /// <b>알려진 미해결 항목(사용자 지시로 이번 라운드 보류)</b>: Windows에는 macOS <c>.stationary</c>에
        /// 대응하는 "데스크톱 표시 면제"가 없고, 오히려 <b>더 나쁘다</b>.
        ///
        /// <para>구조: <c>Win32WindowService.CaptureOverlayOrigin()</c>이 자기 창에 대해
        /// <c>IsIconic</c>을 확인하지 않는다. 같은 파일이 "최소화된 창은 <c>(-32000,-32000)</c>을
        /// 돌려준다"고 스스로 적어 두고 그 필터를 <b>남의 창에만</b> 적용한다. Win+D가 우리 창을
        /// 최소화하면 그 값이 <b>안정된 값</b>으로 들어오고, 플랫폼 중립 코드의 연속 확인
        /// (<c>ScreenCoordinateConverter.OffDesktopConfirmReports</c>)이 2회 만에 받아들인다.
        /// macOS와 달리 <b>폭까지 오염되어 <c>AutoDpiScale</c>도 함께 깨진다.</b></para>
        ///
        /// <para>이번 라운드에 고치지 않은 이유는 기술적 판단이 아니라 <b>사용자 지시</b>다
        /// ("윈도우는 일단 미루고 맥만 중점적으로 고쳐줘", 2026-09-01). 그래서 실패가 아니라
        /// Ignore로 러너에 "건너뜀"으로 계속 띄운다 — 잊히지 않게 하는 것이 목적이다.</para>
        /// </summary>
        [Test]
        public void 미해결_Windows에는_데스크톱표시_최소화_면제가_없다()
        {
            string win = StripLineComments(ReadSource(WinWindowServicePath));

            // CaptureOverlayOrigin 본문에 자기 창 최소화 검사가 들어오면 자동 승격시킨다.
            int start = win.IndexOf("private void CaptureOverlayOrigin()", System.StringComparison.Ordinal);
            if (start >= 0)
            {
                int end = win.IndexOf("private ", start + 1, System.StringComparison.Ordinal);
                string body = end > start ? win.Substring(start, end - start) : win.Substring(start);
                if (body.Contains("IsIconic("))
                {
                    Assert.Pass("CaptureOverlayOrigin()에 자기 창 최소화 검사가 들어왔습니다 — " +
                        "이 테스트를 정식 검사로 승격하세요.");
                }
            }

            Assert.Ignore("【알려진 패리티 결함 · 사용자 지시로 이번 라운드 보류】\n" +
                "항목: 데스크톱 표시(Show Desktop) / Exposé 면제.\n" +
                "macOS: 해결됨 — MacSpaceBehaviorNative의 collectionBehavior에 .stationary(0x10)를 " +
                "추가하고 상호 배타 비트(.managed/.transient)를 껐다. 목표 0x111.\n" +
                "Windows: 미해결 — Win32WindowService.CaptureOverlayOrigin()에 IsIconic(_overlayHwnd) " +
                "검사가 없다. 같은 파일이 '최소화 창은 (-32000,-32000)을 돌려준다'고 적어 두고 그 필터를 " +
                "남의 창에만 적용한다. Win+D로 우리 창이 최소화되면 그 값이 안정적으로 들어오고 " +
                "ScreenCoordinateConverter.OffDesktopConfirmReports(2회, 플랫폼 중립)가 받아들인다. " +
                "macOS와 달리 폭까지 오염되어 AutoDpiScale도 함께 깨진다.\n" +
                "보류 사유: 사용자 지시 '윈도우는 일단 미루고 맥만 중점적으로 고쳐줘'(2026-09-01).\n" +
                "처방 후보: CaptureOverlayOrigin() 진입부에서 IsIconic(_overlayHwnd)이면 즉시 return " +
                "(= 직전 유효 원점/배율 유지). 실기 검증 필요 — 사용자 Windows 머신에서 Win+D로 확인.");
        }

        // ============================================================================
        // C4 — ★ 획 하한(2pt)의 월드 환산이 <b>DPI에 의존</b>한다 (2026-09-01 신설 · 미해결)
        // ============================================================================

        /// <summary>
        /// <b>알려진 미해결 항목(사용자 지시로 이번 라운드 보류)</b>: 화면상 최소 획 두께
        /// (<c>StickConfig.MinStrokeScreenPoints</c> = 2pt)를 <b>월드 유닛으로 바꾸는 환산</b>이
        /// 화면 DPI/카메라 크기에 의존하는데, 그 환산 결과가 <b>기하학 안전 한계를 좌우</b>한다.
        ///
        /// <para><b>왜 문제인가.</b> 2026-09-01 라운드에서 팔다리 필렛이 배율 0.45 미만에서
        /// "규칙 B"(안쪽 오프셋 자기교차 금지)를 어기고 있는 것을 잡았다. 원인은 배율을 내리면
        /// 마디만 짧아지고 <b>획은 하한에서 멈춘다</b>는 것이었고, 그래서 위반이 시작되는
        /// <b>임계 배율</b>이 존재한다 — macOS Retina 실측 기준 다리 <b>0.451</b> / 팔 <b>0.398</b>
        /// (docs/CHARACTER_FORM_SPEC.md 4-2). 이 임계값은 "2pt가 몇 월드 유닛인가"에서 나온다.</para>
        ///
        /// <para><b>Windows에서 달라지는 이유.</b> <c>StickmanAgent.ResolveMinStrokeWorldWidth()</c>는
        /// 카메라 직교 크기와 <b>화면 높이(포인트)</b>로 환산한다. Windows의 표시 배율
        /// 100% / 125% / 150%에서 "포인트"의 정의가 달라지면 같은 2pt가 다른 월드 폭이 되고,
        /// 위 임계 배율도 함께 움직인다. macOS는 Retina 2배가 사실상 고정이라 이 축이 없다.</para>
        ///
        /// <para><b>지금 상태.</b> 판정 로직 자체는 <b>플랫폼 중립</b>이다
        /// (<c>States/LimbCurveRenderer</c> / <c>Core/StickConfig</c>) — 즉 <c>Platform/MacOS/</c>에
        /// 정책이 숨어 있는 종류의 갭은 아니다. 검증만 macOS 수치로 했다. 그래서 실패가 아니라
        /// Ignore로 러너에 "건너뜀"으로 계속 띄운다.</para>
        ///
        /// <para><b>승격 조건.</b> Windows 표시 배율 3종에서 <c>MinStrokeWorldWidth</c>를 실측하고,
        /// 그 값으로 <c>LimbCurveGeometryTests</c>의 배율 스윕을 한 번 더 돌려 여유가 1.0을 넘는지
        /// 확인하면 이 자리를 정식 검사로 바꾼다.</para>
        /// </summary>
        [Test]
        public void 미해결_획_하한의_월드_환산이_Windows_DPI에서_검증되지_않았다()
        {
            // 판정 로직이 플랫폼 전용 폴더로 이사하면(= 진짜 패리티 갭이 되면) 즉시 실패시킨다.
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

            // ============================================================================
            // ★ 2026-09-01 — 구조 절반은 **닫혔다**. 그 사실 자체를 여기서 잠근다.
            // ============================================================================
            // 경위: MacOverlayStateEnforcer의 [렌더품질] 진단이 획 두께를 재면서
            //   (a) 월드->물리픽셀 환산 (b) `× lossyScale.x`(오류) 를 macOS 전용 파일 안에서 하고 있었다.
            //   (b)는 로그 숫자만 루트 스케일만큼 줄여 "하한 2pt 미달"이라는 정반대 결론을 만들었고,
            //   (a)는 Windows가 같은 숫자를 낼 수 없게 만들었다. 둘 다 Platform/StrokeWidthDiagnostics로
            //   옮겨 고쳤다. 위 "없다" 스캔만 두면 **누가 지워도 초록**이므로, 옮겨간 자리가 실제로
            //   존재하고 플랫폼 중립인지를 함께 단언한다(있다/없다 양쪽을 다 얼린다).
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

            // macOS 감시자는 '사실 조회 + 출력'만 한다 = 중립 계측기를 부른다.
            string enforcer = StripLineComments(File.ReadAllText(Path.Combine(macRoot, "MacOverlayStateEnforcer.cs")));
            StringAssert.Contains("StrokeWidthDiagnostics", enforcer,
                "MacOverlayStateEnforcer가 중립 계측기를 부르지 않습니다 — 환산을 다시 인라인했을 가능성이 큽니다.");
            Assert.IsFalse(enforcer.Contains("lossyScale"),
                "MacOverlayStateEnforcer가 다시 lossyScale을 곱하고 있습니다 — " +
                "LineRenderer.startWidth는 월드 유닛이라 Transform 스케일을 따라가지 않습니다" +
                "(2026-08-30 실측 / 2026-09-01 배율 0.60 실기 캡처로 재확인).");

            Assert.Ignore("【알려진 미검증 항목 · 사용자 지시로 이번 라운드 보류】\n" +
                "★ 2026-09-01 — 이 항목의 **구조 절반은 닫혔다**(위 단언들이 그것을 잠근다): " +
                "진단 환산이 Platform/MacOS/MacOverlayStateEnforcer 안에 있던 것을 " +
                "Platform/StrokeWidthDiagnostics(중립)로 옮겼고, 거기 있던 `× lossyScale.x` 오류도 함께 고쳤다. " +
                "남은 것은 코드 구조가 아니라 **Windows 실측이 없다**는 사실 하나뿐이다.\n" +
                "항목: 화면상 획 하한(StickConfig.MinStrokeScreenPoints = 2pt)의 월드 환산이 DPI 의존.\n" +
                "구조: 판정 로직은 플랫폼 중립이다(States/LimbCurveRenderer, Core/StickConfig). " +
                "환산만 StickmanAgent.ResolveMinStrokeWorldWidth()가 카메라 직교 크기 + 화면 높이(pt)로 한다.\n" +
                "macOS: 검증됨 — Retina 실측 기준 규칙 B 위반 임계 배율은 다리 0.451 / 팔 0.398이고, " +
                "이번 라운드의 수정(단위 정합 + FilletLengthRatio 0.42)으로 전 배율 여유 ≥ 1.05배가 됐다.\n" +
                "Windows: 미검증 — 표시 배율 100%/125%/150%에서 같은 2pt가 다른 월드 폭이 되므로 " +
                "위 임계 배율이 달라질 수 있다. 코드 갭이 아니라 '실측이 없다'가 정확한 상태다.\n" +
                "보류 사유: 사용자 지시 '윈도우는 일단 미루고 맥만 중점적으로 고쳐줘'(2026-09-01).\n" +
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
