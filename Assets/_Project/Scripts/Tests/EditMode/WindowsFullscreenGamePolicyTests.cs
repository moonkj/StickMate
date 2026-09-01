using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// Windows "전체화면 게임일 때만 숨긴다" 규칙 검증 (2026-09-01)
    /// ============================================================================
    /// 사용자 신고(2026-08-31 원문): "맥os는 모르겠는데 엑셀같은 프로그램 전체화면에서 엑셀 클릭하면
    /// 캐릭터가 없어져버림 화면 뒤로 넘어가는 거 같음."
    ///
    /// macOS는 그날 <c>LSApplicationCategoryType</c> 필터로 고쳤지만 <b>정작 신고 대상인 Windows는
    /// 기하 판정만 남아</b> 있었다(2026-09-01 패리티 감사에서 발각). CLAUDE.md 절대 불변 원칙 2의
    /// 문구는 "전체화면 <b>게임</b> 감지 시 자동 숨김"이므로, Windows도 게임일 때만 숨긴다.
    ///
    /// <para>이 테스트가 검증하는 것은 <b>규칙(순수 함수)</b>뿐이다 — 실제 레지스트리/프로세스 조회는
    /// 하지 않는다. 그래서 macOS 개발 머신의 EditMode에서 그대로 돌아간다(macOS 쪽
    /// <c>FullscreenGameCategory</c> 테스트와 같은 설계). 실제 조회 계층
    /// (<c>Platform/Windows/WindowsGameProcessProbe.cs</c>)은 파일 전체가
    /// <c>#if UNITY_STANDALONE_WIN</c> 안이라 여기서는 타입이 존재하지 않으므로, 그 파일에 대해서는
    /// 아래 "원칙 3" 절에서 소스 텍스트 정적 스캔으로 검사한다.</para>
    /// </summary>
    public class WindowsFullscreenGamePolicyTests
    {
        // ====================================================================
        // 1. 보수적 기본값 — "모르면 게임이 아니다"
        //    (이 계약이 깨지면 신고된 버그가 그대로 재발한다)
        // ====================================================================

        [Test]
        public void 실행파일_경로를_모르면_게임이_아니다()
        {
            var registered = new List<string> { @"C:\Games\Doom\doom.exe" };

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(null, registered),
                "전경 프로세스의 실행 파일 경로 조회에 실패했는데 게임으로 단정하면, 조회가 막히는 " +
                "모든 상황(보호된 프로세스 등)에서 업무 중 캐릭터가 사라진다.");
            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable("", registered));
            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable("   ", registered));
        }

        [Test]
        public void 게임_목록_조회에_실패하면_게임이_아니다()
        {
            // 목록 null = 레지스트리 키 자체가 없음(게임 바를 한 번도 쓰지 않은 계정).
            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Games\Doom\doom.exe", null),
                "게임 목록을 못 읽었다고 전체화면 앱을 게임으로 추정하면 안 된다 — " +
                "그 추정의 대가가 '업무 중 캐릭터 실종'이다.");

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Games\Doom\doom.exe", new List<string>()),
                "빈 목록은 '아무것도 게임이 아니다'로 해석되어야 한다.");
        }

        [Test]
        public void 전체화면_엑셀은_게임이_아니다()
        {
            // 사용자가 실제로 신고한 바로 그 상황의 회귀 테스트.
            var registered = new List<string>
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Portal\portal.exe",
                @"D:\Games\Elden Ring\Game\eldenring.exe",
            };

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE", registered),
                "전체화면 엑셀에서 캐릭터가 사라지던 버그(2026-08-31 사용자 신고)의 회귀 테스트다.");

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE", registered),
                "PowerPoint 슬라이드쇼도 전체화면 팝업 창이지만 게임이 아니다.");

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Program Files\Google\Chrome\Application\chrome.exe", registered),
                "브라우저 F11 / 동영상 전체화면도 게임이 아니다.");
        }

        [Test]
        public void 게임바에_등록된_실행파일은_게임이다()
        {
            var registered = new List<string>
            {
                @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE_NOT_REALLY",
                @"D:\Games\Elden Ring\Game\eldenring.exe",
            };

            Assert.IsTrue(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"D:\Games\Elden Ring\Game\eldenring.exe", registered),
                "게임 바(HKCU\\System\\GameConfigStore)에 게임으로 등록된 실행 파일이 전경 전체화면이면 " +
                "원칙 2대로 숨겨야 한다.");
        }

        [Test]
        public void 목록에_섞인_null이나_빈_항목이_판정을_망가뜨리지_않는다()
        {
            // 레지스트리 값이 비어 있거나 읽기에 실패한 항목이 섞여 들어올 수 있다.
            var registered = new List<string> { null, "", "   ", @"D:\Games\a\a.exe" };

            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Windows\explorer.exe", registered));
            Assert.IsTrue(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"D:\Games\a\a.exe", registered));

            // 빈 전경 경로가 빈 목록 항목과 "같다"고 판정되면 explorer.exe 조회 실패만으로 숨는다.
            Assert.IsFalse(WindowsGameExecutablePolicy.PathEquals("", ""),
                "빈 경로끼리는 같다고 보면 안 된다 — 조회 실패 두 건이 우연히 일치해 게임으로 " +
                "판정되는 최악의 경로가 열린다.");
        }

        // ====================================================================
        // 2. 경로 대조 규칙 — Windows 경로의 실제 표기 흔들림을 흡수하는가
        // ====================================================================

        [Test]
        public void 경로_대조는_대소문자를_구분하지_않는다()
        {
            // NTFS는 대소문자를 구분하지 않는다. 레지스트리에는 게임 바가 본 표기가, 커널에서는
            // QueryFullProcessImageName의 표기가 나와 철자 케이스가 어긋나는 일이 흔하다.
            Assert.IsTrue(WindowsGameExecutablePolicy.PathEquals(
                @"C:\Games\Doom\DOOM.exe", @"c:\games\doom\doom.EXE"));
        }

        [Test]
        public void 경로_대조는_슬래시와_역슬래시를_같게_본다()
        {
            Assert.IsTrue(WindowsGameExecutablePolicy.PathEquals(
                @"C:\Games\Doom\doom.exe", "C:/Games/Doom/doom.exe"));
        }

        [Test]
        public void 경로_대조는_따옴표와_공백과_종단NUL을_무시한다()
        {
            // REG_SZ 값에는 종단 NUL이 딸려오고, 경로를 따옴표로 감싸 저장해 둔 항목도 있다.
            Assert.IsTrue(WindowsGameExecutablePolicy.PathEquals(
                @"C:\Games\Doom\doom.exe", "\"C:\\Games\\Doom\\doom.exe\"\0"));
            Assert.IsTrue(WindowsGameExecutablePolicy.PathEquals(
                @"C:\Games\Doom\doom.exe", "  C:\\Games\\Doom\\doom.exe  "));
        }

        [Test]
        public void 다른_경로는_같다고_보지_않는다()
        {
            Assert.IsFalse(WindowsGameExecutablePolicy.PathEquals(
                @"C:\Games\Doom\doom.exe", @"C:\Games\Doom2\doom.exe"));
            Assert.IsFalse(WindowsGameExecutablePolicy.PathEquals(null, @"C:\a.exe"));
            Assert.IsFalse(WindowsGameExecutablePolicy.PathEquals(@"C:\a.exe", null));
        }

        [Test]
        public void 파일_이름만_같은_다른_경로는_게임이_아니다()
        {
            // 의도적 설계: 이름 대조는 "설치 경로가 바뀐 게임"을 구제해 주지만 launcher.exe 같은 흔한
            // 이름에서 업무 앱을 게임으로 오인시킨다. 그 오탐은 위험한 방향(숨김)이라 넣지 않았다.
            var registered = new List<string> { @"D:\Games\SomeGame\launcher.exe" };
            Assert.IsFalse(WindowsGameExecutablePolicy.IsRegisteredGameExecutable(
                @"C:\Program Files\Work\launcher.exe", registered),
                "파일 이름만 같은 다른 실행 파일을 게임으로 보면, 흔한 이름을 쓰는 업무 앱에서 " +
                "캐릭터가 사라진다(이번에 고친 버그와 같은 계열의 오탐).");
        }

        // ====================================================================
        // 3. 절대 불변 원칙 3 — 레지스트리는 '읽기만' 한다
        //    (Windows 조회 계층은 macOS 에디터에서 타입이 없으므로 소스 텍스트로 검사)
        // ====================================================================

        private static string WindowsPlatformDir => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "Windows");

        /// <summary>
        /// 주석 줄을 걷어낸 소스. <b>"실제로 쓰는가"를 물을 때만</b> 쓴다.
        ///
        /// 필요한 이유(PlatformParityAuditTests가 이미 밟은 함정과 같다): 조회 계층의 문서에
        /// "쓰기 계열(RegSetValueEx / RegCreateKeyEx ...)은 선언조차 하지 않는다"고 <b>금지 사실을
        /// 정직하게 적어 두면</b> 단순 문자열 스캔이 그 문장을 위반으로 오인한다. 정직하게 적을수록
        /// 감사가 빨개지는 셈이라, 판정에서는 주석을 반드시 뺀다.
        /// </summary>
        private static string[] StripCommentLines(string[] lines)
        {
            var kept = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) { kept.Add(""); continue; }
                if (t.StartsWith("*", StringComparison.Ordinal)) { kept.Add(""); continue; }
                kept.Add(line);
            }
            return kept.ToArray();
        }

        // ★ 2026-09-01 이관 — 여기 있던 `Windows_구현에는_레지스트리_쓰기_API가_한_건도_없다`는
        //   Tests/EditMode/UserAssetImmutabilityAuditTests.cs로 옮겼다(리더 결정, C1 라운드 1번).
        //   이유: 원칙 3(유저 자산 불변)의 <b>단일 관문</b>은 그 파일이고, 여기 두면 스캔 범위가
        //   Platform/Windows/ 폴더에 갇힌다. 레지스트리를 만지는 코드가 언젠가 그 밖에 생기면
        //   (예: 로그인 자동 실행 등록) 이 자리의 스캔은 그것을 영영 못 본다 — 원칙은 폴더가 아니라
        //   저장소 전체에 걸어야 한다. 옮긴 쪽은 주석을 걷어낸 스캔 + 네거티브 컨트롤까지 함께 갖췄다.

        [Test]
        public void 게임_판정_프로브는_최소_권한만_요청한다()
        {
            string probe = Path.Combine(WindowsPlatformDir, "WindowsGameProcessProbe.cs");
            Assert.IsTrue(File.Exists(probe),
                $"Windows 게임 판정 조회 계층이 없습니다: {probe}");
            string src = string.Join("\n", StripCommentLines(File.ReadAllLines(probe)));

            StringAssert.Contains("KEY_READ", src,
                "레지스트리를 KEY_READ 이외의 권한으로 여는 순간 원칙 3의 보장이 무너집니다.");
            StringAssert.Contains("PROCESS_QUERY_LIMITED_INFORMATION", src,
                "프로세스 핸들은 조회 전용 최소 권한으로만 열어야 합니다.");

            // 레지스트리 권한 플래그(KEY_WRITE / KEY_ALL_ACCESS / KEY_SET_VALUE)는 여기서 빠졌다 —
            // UserAssetImmutabilityAuditTests가 <b>저장소 전체</b>에서 같은 것을 본다. 두 곳에 적어 두면
            // 한쪽만 늘어나고, 그때 어느 쪽이 규칙인지 아무도 모르게 된다(이 저장소의 이중 정의 계열 실패).
            foreach (string bad in new[] { "PROCESS_ALL_ACCESS", "PROCESS_VM_WRITE", "PROCESS_VM_OPERATION",
                "PROCESS_TERMINATE", "WriteProcessMemory" })
            {
                Assert.IsFalse(src.Contains(bad),
                    $"WindowsGameProcessProbe.cs가 '{bad}'를 요청합니다 — 이 파일은 '게임인가'라는 " +
                    "사실을 읽기만 해야 하고, 그 이상의 권한은 원칙 3의 표면적을 넓힙니다.");
            }
        }

        [Test]
        public void 게임_판정_규칙은_플랫폼_중립_파일에_있다()
        {
            // Windows 전용 파일 안에 규칙을 복제해 두면 macOS 개발 머신에서 영원히 검증할 수 없다
            // (이 프로젝트가 VisibleTopEdgeSolver / WindowsFootholdFilter / FullscreenVerdictDebouncer
            //  에서 이미 세 번 겪은 실패 구조).
            string probe = string.Join("\n", StripCommentLines(File.ReadAllLines(
                Path.Combine(WindowsPlatformDir, "WindowsGameProcessProbe.cs"))));
            StringAssert.Contains("WindowsGameExecutablePolicy.IsRegisteredGameExecutable(", probe,
                "Windows 조회 계층이 공용 순수 규칙을 부르지 않고 자체 판정을 하고 있습니다 — " +
                "그 순간 이 파일의 나머지 테스트가 전부 헛돕니다.");

            string policy = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform",
                "FullscreenSuspendPolicy.cs");
            Assert.IsTrue(File.Exists(policy), "플랫폼 중립 정책 파일이 없습니다.");
            StringAssert.Contains("class WindowsGameExecutablePolicy", File.ReadAllText(policy),
                "게임 판정 규칙이 플랫폼 중립 파일 밖으로 나갔습니다.");
        }
    }
}
