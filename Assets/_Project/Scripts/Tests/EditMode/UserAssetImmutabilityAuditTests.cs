using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 절대 불변 원칙 3 "유저 자산 불변"(CLAUDE.md, docs/ARCHITECTURE.md 3절) 정적 코드 감사.
    ///
    /// 배경: Phase 4(창 도둑/바탕화면 청소부/그라피티/윈도우 크래시/블랙홀, docs/UX_FLOW.md 27절)는
    /// 전부 "실제로는 아무것도 바꾸지 않으면서 바뀌는 것처럼 보이게" 만드는 게 핵심이라, 원칙 3을
    /// 어길 위험이 가장 큰 영역이다(27-7절 공통 체크리스트 참고). 이 테스트는 Coder의 Phase 4 구현
    /// 완료를 기다리지 않고 지금 바로 작성 가능한 "소스코드 텍스트 정적 스캔" 방식을 쓴다 — 리플렉션이
    /// 아니라 파일 시스템에서 .cs 파일을 직접 읽어 금지 API 호출 패턴을 grep하듯 검사하므로, 이 시점에
    /// 아직 존재하지 않는 Phase 4 파일이 나중에 추가되어도 (파일명을 이 테스트에 하드코딩하지 않았으므로)
    /// 자동으로 스캔 대상에 포함된다. 즉 "3. Phase 4 코드가 이미 존재한다면" 요구사항은 별도 코드 없이
    /// 이 스캔의 디렉터리 전체 탐색 설계 자체로 항상 충족된다.
    ///
    /// 화이트리스트는 최소 1건(Win32WindowService.cs의 SetWindowPos 자기 오버레이 Z-order 조정)만
    /// 두며, 그 예외조차 "정말 자기 오버레이 핸들을, 위치/크기는 바꾸지 않는 플래그로만 호출했는지"를
    /// 라인 단위로 재검증한다(맹목적 파일명 통과가 아님) — 아래 <see cref="ForbiddenPatterns"/> 참고.
    /// </summary>
    public class UserAssetImmutabilityAuditTests
    {
        // ================= 공통: 스캔 대상 소스 파일 수집 =================

        /// <summary>
        /// Assets/_Project/Scripts/ 하위 전체 .cs 파일(Tests 폴더 자신은 제외 — 이 테스트 파일 스스로가
        /// 패턴 문자열을 리터럴로 담고 있어 자기 자신을 스캔하면 오탐이 나기 때문). 개별 파일명을
        /// 하드코딩하지 않고 디렉터리 전체를 훑으므로, Coder가 나중에 추가할 Phase 4 파일
        /// (예: WindowTheft/DesktopCleaner/Graffiti/WindowCrash/BlackHole류 컴포넌트)도 자동 포함된다.
        /// </summary>
        private static List<string> CollectScannedSourceFiles()
        {
            string scriptsRoot = Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts");
            string testsRoot = (Path.Combine(scriptsRoot, "Tests") + Path.DirectorySeparatorChar)
                .Replace('\\', '/');

            return Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal))
                .ToList();
        }

        // ================= 1. 금지 API 블랙리스트 =================

        private sealed class ForbiddenPattern
        {
            public string Needle;
            public string Reason;

            /// <summary>
            /// 파일명(대소문자 구분) → "이 파일에서는 이 패턴이 등장해도 되는 이유가 실제로 그 라인에서
            /// 성립하는지"를 검증하는 함수. 항목이 없으면 그 파일에서도 예외 없이 실패한다.
            /// 검증 함수는 매치된 라인 원문 하나를 받아 true/false만 반환 — "파일명이 맞으니 통과"가
            /// 아니라 "그 라인이 정말 알려진 안전한 형태인지"까지 확인해서, 같은 파일에 나중에 진짜
            /// 위반 호출이 추가돼도 화이트리스트가 그걸 함께 숨겨주지 않게 만든다.
            /// </summary>
            public Dictionary<string, Func<string, bool>> ExceptionsByFileName;
        }

        // ★ 2026-08-30 (윈도우 지원 라운드) — SetWindowPos 화이트리스트가 통째로 사라졌다.
        // 이전까지 유일한 예외는 "Win32WindowService.cs가 자기 오버레이 창의 Z-order만
        // SWP_NOMOVE|SWP_NOSIZE로 바꾸는 1건"이었고, 이 파일에 그 라인을 재검증하는 함수
        // (IsSafeSelfOverlaySetWindowPosUsage)와 "그 예외가 죽은 코드가 아닌지" 확인하는 테스트가
        // 함께 있었다. 이번 라운드에 Windows 오버레이 제어가 통째로 UniWindowController로 옮겨가면서
        // Win32WindowService.cs에서 SetWindowPos 호출 자체가 사라졌고, 그 두 장치도 함께 제거했다
        // (원래 테스트 주석이 "호출이 사라진다면 — 좋은 신호 — 함께 제거할 것"이라고 명시해 둔 대로다).
        // 이제 SetWindowPos(는 프로젝트 어디에서도 예외 없이 금지된다 = 원칙 3의 보장이 더 강해졌다.

        private static readonly List<ForbiddenPattern> ForbiddenPatterns = new List<ForbiddenPattern>
        {
            new ForbiddenPattern
            {
                Needle = "File.Delete(",
                Reason = "유저의 실제 파일을 삭제하는 API — 원칙 3(유저 자산 불변) 정면 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "File.Move(",
                Reason = "유저의 실제 파일을 이동(=경로 변경)시키는 API — 원칙 3 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "Directory.Delete(",
                Reason = "유저의 실제 폴더를 삭제하는 API — 원칙 3 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "SetWindowPos(",
                Reason = "타 윈도우의 위치/크기/Z-order를 바꿀 수 있는 Win32 API(27-1 창 도둑, " +
                    "27-7 체크리스트가 명시적으로 0건을 요구). 2026-08-30부터 화이트리스트 예외가 " +
                    "하나도 없다 — 오버레이 Z-order 제어가 UniWindowController로 옮겨가 " +
                    "Win32WindowService.cs의 마지막 1건이 사라졌기 때문이다(위 주석 참고).",
            },
            new ForbiddenPattern
            {
                Needle = "MoveWindow(",
                Reason = "SetWindowPos와 동급인 또 다른 Win32 창 이동 API — 원칙 3 위반, 화이트리스트 없음.",
            },
            new ForbiddenPattern
            {
                Needle = "DestroyWindow(",
                Reason = "타 윈도우를 강제 종료시키는 Win32 API — 원칙 3/비침해 위반, 화이트리스트 없음.",
            },
            new ForbiddenPattern
            {
                Needle = "CloseWindow(",
                Reason = "타 윈도우를 닫거나 최소화시키는 Win32 API — 원칙 3/비침해 위반, 화이트리스트 없음.",
            },
            new ForbiddenPattern
            {
                Needle = ".Kill(",
                Reason = "프로세스 강제 종료 — 원칙 3 위반. (Process.Start(...) 자체는 종료/파괴 행위가 " +
                    "아니므로 블랙리스트에 넣지 않고, 실제로 파괴적인 .Kill( 호출만 금지한다.)",
            },
            new ForbiddenPattern
            {
                Needle = "TerminateProcess(",
                Reason = "Win32WindowService.cs 자체 문서 주석이 명시적으로 금지 대상으로 지목한 저수준 " +
                    "프로세스 강제 종료 API(원칙 3) — 보강 항목, 현재 코드베이스에 등장하지 않음(0건이어야 정상).",
            },
            new ForbiddenPattern
            {
                Needle = "LVM_SETITEMPOSITION",
                Reason = "Windows 바탕화면 아이콘의 실제 좌표를 '쓰기'로 옮기는 리스트뷰 메시지 상수 " +
                    "(27-2 청소부/27-5 블랙홀이 명시적으로 금지). 읽기 전용 대응인 LVM_GETITEMPOSITION은 " +
                    "허용 — 보강 항목, 현재 코드베이스에 등장하지 않음(0건이어야 정상).",
            },
            new ForbiddenPattern
            {
                Needle = "SPI_SETDESKWALLPAPER",
                Reason = "OS 배경화면을 실제로 변경하는 SystemParametersInfo 플래그(27-3 그라피티가 " +
                    "명시적으로 금지) — 보강 항목, 현재 코드베이스에 등장하지 않음(0건이어야 정상).",
            },

            // ★★ 2026-09-01 (폴링 제거 검토 라운드) 추가 8건.
            //
            // 이 라운드는 "초당 3.3회 전체 창 열거"를 user32 이벤트 훅(SetWinEventHook)으로 바꾸려다
            // **실측으로 중단**됐다(창 열거는 실행 시간의 0.5%, 스톨 귀인 판정은 '로직밖'). 구현은
            // 되돌렸지만 **이 항목들은 남긴다** — 되돌린 이유가 "위험해서"가 아니라 "이득이 작아서"라,
            // 언젠가 같은 방향이 다시 열릴 가능성이 높기 때문이다. 그때 훅과 함께 들어오기 쉬운 것이
            // 아래 API들이다: hwnd를 손에 쥔 코드 옆에 "남의 창을 조작하는" 호출을 붙이는 것은
            // 코드상 매우 자연스럽고, 그 순간 원칙 3이 조용히 무너진다.
            //
            // 지금 전부 0건이며, **0건인 상태를 유지하는 것 자체가 이 항목의 목적**이다.
            // (SetWinEventHook / UnhookWinEvent 자체는 순수 관찰이라 금지 목록에 넣지 않는다 —
            //  금지 대상은 '관찰'이 아니라 '조작'이다.)
            new ForbiddenPattern
            {
                Needle = "SetWindowsHookEx(",
                Reason = "SetWinEventHook과 이름이 비슷하지만 **완전히 다른 물건**이다 — 이쪽은 우리 DLL을 " +
                    "다른 프로세스에 주입하는 훅이라 남의 프로세스 안에서 코드가 돈다. 원칙 3/비침해 " +
                    "정면 위반이며, 관찰만 필요한 이 앱에는 필요가 없다(SetWinEventHook + " +
                    "WINEVENT_OUTOFCONTEXT로 충분하다).",
            },
            new ForbiddenPattern
            {
                Needle = "ShowWindow(",
                Reason = "타 윈도우를 최소화/최대화/숨김/복원시키는 Win32 API — 원칙 3 위반. " +
                    "우리는 최소화 '이벤트를 통보받을' 뿐 최소화를 '시키지' 않는다.",
            },
            new ForbiddenPattern
            {
                Needle = "SetForegroundWindow(",
                Reason = "타 윈도우를 강제로 앞으로 끌어와 포커스를 빼앗는 API — 비침해 원칙 2 위반. " +
                    "EVENT_SYSTEM_FOREGROUND를 '구독'하는 것과 포커스를 '바꾸는' 것은 정반대 행위다.",
            },
            new ForbiddenPattern
            {
                Needle = "BringWindowToTop(",
                Reason = "SetForegroundWindow와 동급인 z-order 강제 변경 API — 원칙 2/3 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "SwitchToThisWindow(",
                Reason = "타 윈도우를 강제 활성화하는 API — 원칙 2/3 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "EndTask(",
                Reason = "타 윈도우/앱을 강제로 끝내는 API — 원칙 3 정면 위반.",
            },
            new ForbiddenPattern
            {
                Needle = "PostMessage(",
                Reason = "WM_CLOSE / WM_SYSCOMMAND(SC_MOVE, SC_MINIMIZE) 한 줄이면 남의 창을 닫거나 " +
                    "옮길 수 있다 — SetWindowPos를 금지하고 이걸 열어 두면 금지가 무의미해진다. " +
                    "우리가 남의 창에 보낼 메시지는 하나도 없다(전부 조회/통보 수신이다).",
            },
            new ForbiddenPattern
            {
                Needle = "AttachThreadInput(",
                Reason = "다른 스레드의 입력 큐에 우리를 붙이는 API. 포커스/커서 조작 우회의 표준 수단이라 " +
                    "원칙 2/3의 표면적을 크게 넓힌다 — 관찰 전용 앱에는 필요가 없다.",
            },

            // ★★★ 2026-09-02 — 작업표시줄 앱바 메시지. **원칙 3의 승인된 예외가 여기 하나 생겼다.**
            //
            // 경위(숨기지 않고 적는다): 사용자 지시 "일단 우리 프로그램을 실행하면 작업표시줄 숨김처리가
            // 되어 있어도 강제로 보이게 해야함". 리더가 원칙 3과의 충돌을 명시하고 선택지 3개를 제시했고,
            // 사용자가 "실행 중에만 + 종료 시 원복"을 택했다. CLAUDE.md 원칙 3에 예외 조항이 있고 상세는
            // docs/TASKBAR_REVEAL.md다.
            //
            // ★ 승인은 **한 메시지에만** 내려졌다. 그래서 나머지 앱바 쓰기 메시지를 여기서 함께 잠근다 —
            //   예외가 하나 열리면 "이왕 앱바를 만지는 김에"가 가장 자연스러운 다음 걸음이고, 그 순간
            //   원칙 3이 조용히 무너진다. 아래 넷은 예외가 **없다**(현재 코드베이스 0건, 0건 유지가 목적).
            new ForbiddenPattern
            {
                Needle = "ABM_SETPOS",
                Reason = "작업표시줄/도킹 툴바의 **위치·크기**를 바꾸는 앱바 메시지 — 남의 창을 옮기는 " +
                    "행위 그 자체다(원칙 3 정면 위반). 승인된 예외는 자동 숨김 비트 하나뿐이다.",
            },
            new ForbiddenPattern
            {
                Needle = "ABM_NEW",
                Reason = "우리를 **도킹 앱바로 등록**해 화면 영역을 영구 예약하는 메시지. 다른 모든 앱의 " +
                    "작업 영역(rcWork)을 줄인다 — 오버레이가 할 일이 아니다(원칙 2 비침해 위반).",
            },
            new ForbiddenPattern
            {
                Needle = "ABM_REMOVE",
                Reason = "ABM_NEW의 짝. 등록이 금지이므로 해제도 등장할 이유가 없다 — 이 이름이 " +
                    "나타났다면 어딘가에서 앱바 등록을 했다는 뜻이다.",
            },
            new ForbiddenPattern
            {
                Needle = "ABM_SETAUTOHIDEBAR",
                Reason = "특정 화면 가장자리의 **자동 숨김 앱바 자리를 우리가 차지**하는 메시지. " +
                    "실제 작업표시줄을 그 자리에서 밀어낼 수 있다 — 이름이 비슷하다고 승인된 " +
                    "ABM_SETSTATE와 같은 물건이 아니다. 예외 없음.",
            },
            new ForbiddenPattern
            {
                Needle = "ABM_SETSTATE",
                Reason = "작업표시줄의 자동 숨김/항상위 **상태를 쓰는** 시스템 전역 설정 변경. " +
                    "원칙 3의 승인된 예외(2026-09-02, 사용자 확정)이며, 허용되는 자리는 " +
                    "Platform/Windows/WindowsReservedBarAutoHideControl.cs **한 파일**뿐이다. " +
                    "그 파일 밖에서 이 이름이 보이면 예외가 번지고 있는 것이다.",
                ExceptionsByFileName = new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
                {
                    { TaskbarStateExceptionFileName, IsApprovedTaskbarAutoHideStateUsage },
                },
            },
        };

        // ============================================================================
        // ★ 승인된 예외 1건의 라인 단위 재검증 (2026-09-02)
        // ============================================================================
        // 파일명만 보고 통과시키지 않는다. 그러면 같은 파일에 나중에 추가되는 진짜 위반까지
        // 화이트리스트가 함께 숨겨 준다(이 파일 위쪽 ForbiddenPattern 문서의 설계 의도).
        // 허용하는 형태는 정확히 둘뿐이다:
        //   (a) 상수 선언   — private const uint ABM_SETSTATE = ...;
        //   (b) 그 한 호출 — SHAppBarMessage(ABM_SETSTATE, ref ...);
        // (a)를 허용하는 이유: Win32 이름을 그대로 쓰지 않고 다른 이름으로 감추면 이 감사가
        // 물 대상 자체가 사라진다 — 이름을 숨기는 것은 예외를 없애는 것이 아니라 감시를 없애는 것이다.

        private const string TaskbarStateExceptionFileName = "WindowsReservedBarAutoHideControl.cs";

        private static bool IsApprovedTaskbarAutoHideStateUsage(string line)
        {
            string t = line.Trim();

            // ★ 접두/접미만 보면 뒤에 무엇이든 이어 붙일 수 있다. 실제로 이 파일의 네거티브 컨트롤이
            //   "SHAppBarMessage(ABM_SETSTATE, ref evil); Kill();"을 통과시키는 것을 잡아냈다(2026-09-02).
            //   그래서 **가운데까지** 본다 — 남는 부분이 식별자 하나여야 한다.
            const string declPrefix = "private const uint ABM_SETSTATE = ";
            if (t.StartsWith(declPrefix, StringComparison.Ordinal) && t.EndsWith(";", StringComparison.Ordinal))
            {
                string value = t.Substring(declPrefix.Length, t.Length - declPrefix.Length - 1);
                return IsHexOrDecimalLiteral(value);
            }

            const string callPrefix = "SHAppBarMessage(ABM_SETSTATE, ref ";
            if (t.StartsWith(callPrefix, StringComparison.Ordinal) && t.EndsWith(");", StringComparison.Ordinal))
            {
                string arg = t.Substring(callPrefix.Length, t.Length - callPrefix.Length - 2);
                return IsIdentifier(arg);
            }

            return false;
        }

        /// <summary>식별자 하나인가(문자/숫자/밑줄만, 비어 있지 않음).</summary>
        private static bool IsIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        /// <summary>16진/10진 리터럴 하나인가. 함수 호출이나 추가 문장이 붙으면 false.</summary>
        private static bool IsHexOrDecimalLiteral(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.EndsWith("U", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 1);
            if (s.Length == 0) return false;
            foreach (char c in s)
            {
                if (!Uri.IsHexDigit(c)) return false;
            }
            return true;
        }

        // ★★ 2026-09-01 — 이 스캔은 이제 **주석을 걷어낸 뒤** 본다(BlankOutCommentLines).
        //
        // 그동안은 원문 그대로 훑었고, 이 파일 아래쪽 레지스트리 스캔의 주석이 그 차이를 이렇게
        // 정당화했다: "위 스캔은 주석을 걷어내지 않는다(그리고 지금까지 그래도 됐다 — 어떤 주석도
        // File.Delete( 같은 형태를 인용하지 않는다)". **그 전제가 오늘 깨졌다.**
        //
        // 같은 날 다른 라운드 두 개가 <b>금지를 지키는 이유를 설명하려고</b> 금지 API를 주석에 인용했다:
        //   Platform/TopmostBandOcclusion.cs        — "밴드 안에서 위로 올라가는 유일한 수단은
        //                                              SetWindowPos(HWND_TOPMOST)이고, 그러면
        //                                              작업표시줄 위에 영구히 올라앉는다"
        //   Platform/Windows/WindowsTopmostWatchdog.cs — 같은 취지
        //
        // 즉 **정직하게 적을수록 감사가 빨개지는** 상태가 됐고, 그러면 다음 사람은 사실을 지운다.
        // 그건 감사가 지키려던 것을 정확히 반대로 만드는 결과다. 그래서 판단 기준(아래쪽 레지스트리
        // 스캔 주석에 이미 적혀 있던 것)을 그대로 적용한다 — **"주석에 인용될 수 있는 이름인가"**.
        // 이제 답은 '그렇다'이므로 이 스캔도 같은 처리를 받는다.
        //
        // 감사의 힘은 줄지 않는다: BlankOutCommentLines는 <b>줄 전체가 주석인 줄</b>만 비우므로,
        // 실제 호출은 그런 줄에 있을 수 없다(있으면 컴파일이 안 된다).
        [Test]
        public void 금지된_API_호출_패턴이_소스코드_어디에도_없다()
        {
            var files = CollectScannedSourceFiles();
            var violations = new List<string>();

            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string[] lines = BlankOutCommentLines(File.ReadAllLines(filePath));

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    foreach (var pattern in ForbiddenPatterns)
                    {
                        if (!line.Contains(pattern.Needle)) continue;

                        bool allowed = pattern.ExceptionsByFileName != null
                            && pattern.ExceptionsByFileName.TryGetValue(fileName, out var verifier)
                            && verifier(line);

                        if (!allowed)
                        {
                            violations.Add(
                                $"{fileName}:{i + 1}: 금지 패턴 '{pattern.Needle}' 발견 — {pattern.Reason}\n" +
                                $"    라인 원문: {line.Trim()}");
                        }
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                "다음 위치에서 금지된 API 호출 패턴이 발견되었습니다 " +
                "(CLAUDE.md / docs/ARCHITECTURE.md 3절 '유저 자산 불변' 원칙 위반 가능성):\n\n" +
                string.Join("\n\n", violations));
        }

        // ============================================================================
        // 1-a. ★ 승인된 예외 1건이 "죽은 화이트리스트"가 되지 않게 (2026-09-02)
        // ============================================================================
        // 이 저장소는 예외가 사라진 뒤에도 화이트리스트만 남아 있던 적이 있다(2026-08-30 SetWindowPos —
        // 그때는 예외 검증 함수와 이 검사를 함께 지웠다). 화이트리스트가 가리키는 자리가 비면
        // "예외 0건"과 "감사가 눈이 멀었다"가 구분되지 않는다.

        [Test]
        public void 승인된_작업표시줄_예외는_정확히_그_한_파일_두_형태로만_존재한다()
        {
            string target = null;
            foreach (string path in CollectScannedSourceFiles())
            {
                if (Path.GetFileName(path) == TaskbarStateExceptionFileName) { target = path; break; }
            }

            Assert.IsNotNull(target,
                $"화이트리스트가 가리키는 파일({TaskbarStateExceptionFileName})이 없습니다. " +
                "예외가 정말 사라졌다면 위 ForbiddenPatterns의 ExceptionsByFileName 항목과 " +
                "IsApprovedTaskbarAutoHideStateUsage를 함께 지우세요 — 그러면 이 이름은 " +
                "예외 없이 금지되고 원칙 3의 보장이 다시 강해집니다(2026-08-30 SetWindowPos와 같은 처리).");

            string[] lines = BlankOutCommentLines(File.ReadAllLines(target));
            var hits = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("ABM_SETSTATE")) hits.Add($"{i + 1}: {lines[i].Trim()}");
            }

            Assert.AreEqual(2, hits.Count,
                "승인된 예외의 등장 횟수가 2(상수 선언 1 + 호출 1)가 아닙니다. 늘었다면 예외가 " +
                "번지고 있고, 줄었다면 이름이 숨겨져 감사가 물 대상을 잃었습니다.\n" +
                string.Join("\n", hits));

            foreach (string hit in hits)
            {
                Assert.IsTrue(IsApprovedTaskbarAutoHideStateUsage(hit.Substring(hit.IndexOf(':') + 1)),
                    $"승인된 형태가 아닌 사용입니다 — {hit}");
            }

            // 네거티브 컨트롤: 검증 함수가 실제로 판정을 가르는가(무조건 true면 위 단언이 공허하다).
            Assert.IsFalse(IsApprovedTaskbarAutoHideStateUsage("SHAppBarMessage(ABM_SETSTATE, ref evil); Kill();"),
                "검증 함수가 아무 줄이나 통과시킵니다 — 화이트리스트가 파일 전체를 열어 준 것과 같습니다.");
            Assert.IsFalse(IsApprovedTaskbarAutoHideStateUsage("int x = ABM_SETSTATE;"),
                "선언/호출이 아닌 사용까지 통과시킵니다.");
            Assert.IsFalse(IsApprovedTaskbarAutoHideStateUsage(
                    "private const uint ABM_SETSTATE = Evil(); DoBad();"),
                "선언 줄 뒤에 문장을 붙이면 통과합니다 — 화이트리스트가 그 파일에 자유 통행권을 줍니다.");
            Assert.IsTrue(IsApprovedTaskbarAutoHideStateUsage("                SHAppBarMessage(ABM_SETSTATE, ref data);"),
                "실제 호출 형태를 통과시키지 못합니다(오탐).");
            Assert.IsTrue(IsApprovedTaskbarAutoHideStateUsage("        private const uint ABM_SETSTATE = 0x0000000A;"),
                "실제 선언 형태를 통과시키지 못합니다 — 감사가 자기 예외를 위반으로 잡습니다(미탐이 아니라 오탐).");
        }

        // ================= 1-b. 레지스트리 쓰기 금지 (2026-09-01 이관) =================
        //
        // ★ 이 스캔은 원래 Tests/EditMode/WindowsFullscreenGamePolicyTests.cs 안에 있었다.
        //   C1(Windows 전체화면 게임 판정) 라운드가 HKCU\System\GameConfigStore를 <b>읽기 전용</b>으로
        //   인용하기 시작하면서 함께 만든 것인데, 그 파일은 <c>Platform/Windows/</c> 폴더만 훑었다.
        //   원칙 3의 단일 관문은 <b>이 파일</b>이므로 여기로 옮기고 범위를 저장소 전체로 넓힌다 —
        //   레지스트리를 만지는 코드가 언젠가 Platform/Windows/ 밖에 생기면(예: 자동 실행 등록,
        //   35-1-9 P3의 "로그인 항목") 옛 스캔은 그것을 영영 못 본다. 원칙은 폴더에 걸지 않는다.
        //
        // <b>왜 위 ForbiddenPatterns에 합치지 않았나</b>: 원래 이유는 "위 스캔은 주석을 걷어내지
        //   않는다"였다. ★ 2026-09-01부터 위 스캔도 주석을 걷어내므로 **그 차이는 사라졌다**
        //   (위 스캔의 해당 주석에 경위가 있다). 그럼에도 두 리스트를 합치지 않는 이유는 남아 있다:
        //   대상 범위가 다르다(이쪽은 '선언조차 없어야 하는' 이름 + 접근 권한 플래그까지 본다).
        //   레지스트리 쓰기 API는 <b>금지 사실을 적은 주석</b>이 실제로 존재한다
        //   (WindowsGameProcessProbe.cs가 "RegSetValueEx / RegCreateKeyEx는 선언조차 하지 않는다"고
        //   써 두었다). 정직하게 적을수록 감사가 빨개지면 다음 사람은 사실을 지운다.
        //   그래서 이 항목만 <b>주석을 제외한 스캔</b>으로 따로 둔다 — 두 리스트의 차이는 취향이 아니라
        //   "주석에 인용될 수 있는 이름인가"라는 기준이다.

        /// <summary>선언조차 없어야 하는 레지스트리 <b>쓰기</b> 계열 API. 선언이 없으면 실수로도 못 부른다.
        /// <para>읽기 계열(RegOpenKeyEx / RegQueryValueEx / RegEnumValue)은 <b>허용</b>이다 — 이 앱이
        /// 게임 바 등록 목록을 인용하는 경로가 그것이고, 인용은 원칙 3이 금지하는 행위가 아니다.</para></summary>
        private static readonly string[] ForbiddenRegistryWriteApis =
        {
            "RegSetValue", "RegCreateKey", "RegDeleteKey", "RegDeleteValue",
            "RegDeleteTree", "RegSaveKey", "RegRestoreKey", "RegLoadKey", "RegReplaceKey",
            "RegSetKeySecurity", "RegUnLoadKey",
        };

        /// <summary>레지스트리를 <b>여는 권한</b> 중 쓰기를 포함하는 것. 쓰기 함수를 안 부르더라도
        /// 이 권한으로 열면 원칙 3의 표면적이 넓어진다(KEY_READ 하나면 충분하다).</summary>
        private static readonly string[] ForbiddenRegistryAccessFlags =
        {
            "KEY_WRITE", "KEY_ALL_ACCESS", "KEY_SET_VALUE", "KEY_CREATE_SUB_KEY", "KEY_CREATE_LINK",
        };

        /// <summary>주석 줄을 빈 줄로 바꾼 사본(줄 번호를 보존해야 실패 메시지가 쓸모 있다).</summary>
        private static string[] BlankOutCommentLines(string[] lines)
        {
            var kept = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                kept[i] = t.StartsWith("//", StringComparison.Ordinal)
                          || t.StartsWith("*", StringComparison.Ordinal)
                    ? string.Empty
                    : lines[i];
            }
            return kept;
        }

        [Test]
        public void 레지스트리_쓰기_API가_저장소_어디에도_없다()
        {
            var violations = new List<string>();
            foreach (string filePath in CollectScannedSourceFiles())
            {
                string[] lines = BlankOutCommentLines(File.ReadAllLines(filePath));
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (string needle in ForbiddenRegistryWriteApis)
                    {
                        if (!lines[i].Contains(needle)) continue;
                        violations.Add($"{Path.GetFileName(filePath)}:{i + 1}: '{needle}' — {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                "레지스트리 쓰기 계열 API가 발견되었습니다(CLAUDE.md 절대 불변 원칙 3: 유저 자산 불변 — " +
                "레지스트리도 <b>읽기 전용</b>만 허용). 게임 바 등록 목록 같은 값은 OS/사용자의 소유물이고 " +
                "이 앱은 인용만 합니다:\n" + string.Join("\n", violations));
        }

        [Test]
        public void 레지스트리를_쓰기_권한으로_여는_코드가_없다()
        {
            var violations = new List<string>();
            foreach (string filePath in CollectScannedSourceFiles())
            {
                string[] lines = BlankOutCommentLines(File.ReadAllLines(filePath));
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (string needle in ForbiddenRegistryAccessFlags)
                    {
                        if (!lines[i].Contains(needle)) continue;
                        violations.Add($"{Path.GetFileName(filePath)}:{i + 1}: '{needle}' — {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                "레지스트리를 KEY_READ 이외의 권한으로 여는 코드가 있습니다 — 쓰기 함수를 부르지 " +
                "않더라도 그 순간 원칙 3의 보장이 '코드를 다 읽어야 아는 것'으로 약해집니다:\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 이 스캔이 <b>실제로 볼 수 있는가</b>, 그리고 <b>주석은 넘기는가</b>.
        /// <para>두 사실을 이 파일 안에 함께 박제한다: (a) 옛 위반 형태(P/Invoke 선언 한 줄)를 주면
        /// 걸린다, (b) 같은 이름이 <b>주석</b>에 있으면 안 걸린다 — 후자가 이 스캔을 여기로 옮기면서
        /// 위 <see cref="ForbiddenPatterns"/>에 합치지 <b>않은</b> 이유 그 자체다.</para>
        /// </summary>
        [Test]
        public void 컨트롤_레지스트리_스캔은_선언을_잡고_주석은_넘긴다()
        {
            string[] sample =
            {
                "        [DllImport(\"advapi32.dll\")]",
                "        private static extern int RegSetValueExW(IntPtr hKey, string name);",
                "        // 쓰기 계열(RegSetValueEx / RegCreateKeyEx ...)은 선언조차 하지 않는다.",
                "        /// <summary>KEY_ALL_ACCESS는 쓰지 않는다 — KEY_READ만 요청한다.</summary>",
                "        private const int KEY_READ = 0x20019;",
            };

            string[] stripped = BlankOutCommentLines(sample);
            Assert.AreEqual(sample.Length, stripped.Length,
                "주석 제거가 줄 수를 바꾸면 실패 메시지의 줄 번호가 거짓말이 됩니다.");

            int declarationHits = 0, commentHits = 0;
            for (int i = 0; i < stripped.Length; i++)
            {
                bool writeApi = false, accessFlag = false;
                foreach (string needle in ForbiddenRegistryWriteApis) writeApi |= stripped[i].Contains(needle);
                foreach (string needle in ForbiddenRegistryAccessFlags) accessFlag |= stripped[i].Contains(needle);
                if (writeApi || accessFlag) declarationHits++;

                foreach (string needle in ForbiddenRegistryWriteApis) if (sample[i].Contains(needle) && stripped[i].Length == 0) commentHits++;
                foreach (string needle in ForbiddenRegistryAccessFlags) if (sample[i].Contains(needle) && stripped[i].Length == 0) commentHits++;
            }

            Assert.AreEqual(1, declarationHits,
                "선언 한 줄만 걸려야 합니다 — 0이면 스캐너가 눈이 먼 것이고, 2 이상이면 주석까지 세고 있습니다.");
            Assert.Greater(commentHits, 0,
                "주석에 든 금지 이름이 하나도 없습니다 — 이 컨트롤이 재현하려는 상황(정직한 금지 주석)이 " +
                "표본에 없으므로 '주석은 넘긴다'가 증명되지 않습니다.");
        }

        // ================= 스캔 자체의 유효성을 보장하는 가드 =================

        [Test]
        public void 정적_스캔이_실제로_충분한_수의_소스파일을_찾아낸다()
        {
            // 이 프로젝트는 현재 Scripts/ 하위(Tests 제외)에 47개 .cs 파일을 갖고 있다(2026-08-27 기준).
            // 경로 계산이 잘못되어 0개/소수만 스캔된 채 "위반 0건"으로 허위 통과하는 것을 막기 위한
            // 최소 하한 가드 — Phase 4 파일이 추가되면 이 수는 늘어나기만 하므로 40으로 넉넉히 잡는다.
            var files = CollectScannedSourceFiles();

            Assert.GreaterOrEqual(files.Count, 40,
                "스캔 대상 파일 수가 비정상적으로 적습니다 — Application.dataPath 기준 경로 계산 오류로 " +
                "정적 감사가 사실상 아무것도 스캔하지 않은 채 허위 통과할 위험을 조기에 잡기 위한 가드입니다. " +
                $"실제 발견 수: {files.Count}");

            Assert.IsTrue(files.Any(p => Path.GetFileName(p) == "Win32WindowService.cs"),
                "알려진 파일(Win32WindowService.cs)이 스캔 목록에 없습니다 — 경로 계산 오류 의심.");
            Assert.IsTrue(files.Any(p => Path.GetFileName(p) == "IPlatformWindowService.cs"),
                "알려진 파일(IPlatformWindowService.cs)이 스캔 목록에 없습니다 — 경로 계산 오류 의심.");

            Assert.IsFalse(files.Any(p => p.Replace('\\', '/').Contains("/Tests/")),
                "테스트 폴더 자신의 .cs 파일이 스캔 대상에 포함되면 안 됩니다 " +
                "(이 테스트 파일 스스로가 패턴 문자열 리터럴을 담고 있어 자기 참조 오탐을 일으키기 때문).");
        }

        [Test]
        public void Win32WindowService에는_SetWindowPos_호출이_하나도_없다()
        {
            // 위 화이트리스트 제거(2026-08-30)를 코드로 잠근다. 이전 버전의 이 테스트는 정반대로
            // "SetWindowPos 호출이 최소 1건 있어야 한다"(화이트리스트가 죽은 코드가 아님을 보증)를
            // 확인했는데, 그 1건이 UniWindowController 전환으로 사라졌으므로 이제는 **0건임**을
            // 지키는 테스트로 뒤집는다. 누군가 다시 자기 창을 SetWindowPos로 직접 조작하는 코드를
            // 넣으면 여기서 먼저 걸린다(위 ForbiddenPatterns 스캔에도 예외 없이 걸린다 — 이중 방어).
            var files = CollectScannedSourceFiles();
            var win32File = files.FirstOrDefault(p => Path.GetFileName(p) == "Win32WindowService.cs");
            Assert.IsNotNull(win32File, "Win32WindowService.cs를 찾을 수 없습니다(사전 조건 확인).");

            int matchCount = File.ReadAllLines(win32File).Count(l => l.Contains("SetWindowPos("));
            Assert.AreEqual(0, matchCount,
                "Win32WindowService.cs에 SetWindowPos( 가 다시 등장했습니다. 오버레이의 항상위/스타일 " +
                "제어는 UniWindowController(isTopmost 등)를 통해서만 해야 합니다 — 직접 Win32 창 조작은 " +
                "원칙 3의 표면적을 다시 넓힙니다.");
        }

        // ================= 2. 읽기 전용 열거 계약 =================

        [Test]
        public void IPlatformWindowService에는_타윈도우를_이동_크기변경_종료_포커스강제하는_메서드가_없다()
        {
            // IPlatformWindowService.cs 자체 문서 주석("절대 금지: 타 윈도우를 이동/크기변경/최소화/
            // 종료/포커스 강제하는 메서드는 이 인터페이스에 추가하지 않는다")을 코드로 고정한다.
            // EnumerateFootholds 같은 읽기 전용 열거와, "우리 오버레이 자신"의 속성 제어
            // (CreateOverlayWindow/SetClickThrough/SetAlwaysOnTop/IsFullscreenAppActive)만 허용된다.
            var forbiddenNameSubstrings = new[]
            {
                "Move", "Resize", "Close", "Destroy", "Delete", "Minimize", "Kill", "Terminate", "Activate", "Focus",
            };

            var methods = typeof(IPlatformWindowService).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(methods.Length > 0, "인터페이스에 멤버가 있어야 이 테스트가 의미가 있다(사전 조건 확인).");

            var offenders = new List<string>();
            foreach (var m in methods)
            {
                foreach (var bad in forbiddenNameSubstrings)
                {
                    if (m.Name.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        offenders.Add($"{m.Name}() — 금지어 '{bad}' 포함");
                    }
                }
            }

            Assert.IsTrue(offenders.Count == 0,
                "IPlatformWindowService는 읽기 전용 열거 + 자기 오버레이 속성 제어만 허용해야 합니다 " +
                "(IPlatformWindowService.cs 문서 주석, CLAUDE.md/ARCHITECTURE.md 3절). 위반 의심 멤버:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void PlatformFoothold의_모든_공개_인스턴스_필드는_readonly다()
        {
            // 열거된 발판(PlatformFoothold)이 소비 측에서 수정 가능한 값이면 "읽기 전용 열거" 계약이
            // 컴파일 타임에 보장되지 않는다 — 필드 하나하나가 실제로 readonly인지 리플렉션으로 고정한다.
            var type = typeof(PlatformFoothold);
            Assert.IsTrue(type.IsValueType,
                "PlatformFoothold는 값 타입(struct)이어야 열거 결과가 참조로 공유되어 원본이 " +
                "몰래 수정되는 경로를 원천 차단할 수 있다.");

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(fields.Length > 0, "검사할 공개 필드가 있어야 이 테스트가 의미가 있다(사전 조건 확인).");

            foreach (var f in fields)
            {
                Assert.IsTrue(f.IsInitOnly,
                    $"PlatformFoothold.{f.Name}은 readonly가 아닙니다 — 열거된 발판 데이터는 소비 측에서 " +
                    "수정할 수 없는 읽기 전용이어야 한다(원칙 3 '읽기 전용 열거').");
            }
        }

    }
}
