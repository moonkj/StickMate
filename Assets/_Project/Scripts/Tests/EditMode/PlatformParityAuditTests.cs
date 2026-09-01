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
        /// <b>단축키가 아무 로그 없이 죽는다</b>(2026-09-01 <c>GlobalKey.Comma</c> 추가 때 실제로
        /// 경계했던 위험 — IGlobalKeyStateService.cs의 Comma 문서 참고).
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
                // ★ 콜론까지 포함해야 한다. "GlobalKey.Comma"는 "GlobalKey.Command"의 접두사라,
                //   콜론이 없으면 Command만 있어도 Comma가 통과해버린다(이 테스트 자신의 오탐).
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
        /// 좌하단 호버 패널은 <b>두 플랫폼의 하단 막대를 모두</b> 피해야 한다.
        /// macOS Dock만 피하던 시절 Windows에서는 패널이 작업표시줄에 파묻혔다(2026-09-01 감사).
        /// </summary>
        [Test]
        public void 좌하단_패널이_양_플랫폼_하단막대를_모두_피한다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", "CornerHoverPanel.cs");
            string src = ReadSource(path);

            StringAssert.Contains("IDockMetricsService", src,
                "macOS Dock 회피 경로가 없습니다.");
            StringAssert.Contains("IReservedBottomBarService", src,
                "Windows 작업표시줄 회피 경로가 없습니다 — 좌하단 감지 영역이 작업표시줄에 파묻혀 " +
                "손잡이를 집을 수 없게 됩니다(하단 막대의 정확한 사각형은 Win32WindowService가 " +
                "IReservedBottomBarService로 이미 실측해 내놓고 있습니다).");
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
                "            Settings,           // ★ 2026-09-01 신설 — 설정창(⌃⌥⌘,)";
            const string docComment =
                "        /// 설정창 토글(전역 단축키 ⌃⌥⌘,). 주 진입점은 정보창 헤더의 [설정]이고";
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
