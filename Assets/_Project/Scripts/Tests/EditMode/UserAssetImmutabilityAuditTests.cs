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

        /// <summary>
        /// 알려진 유일한 예외: Win32WindowService.cs가 자기 자신의 오버레이 창(_overlayHwnd)을
        /// 항상-최상단(topmost)으로 유지하기 위해 SetWindowPos를 호출하는 것 — 단, SWP_NOMOVE|SWP_NOSIZE
        /// 플래그로 Z-order만 바꾸고 좌표/크기는 절대 건드리지 않는다(위치를 쓰는 인자는 전부 0으로
        /// 고정, 실제 좌표 변경은 이 플래그가 무시함). Win32WindowService.cs 187~188행 주석 참고.
        /// P/Invoke 시그니처 선언(extern) 자체는 함수를 "호출"하는 게 아니므로 별도로 허용한다.
        /// 이 두 조건(extern 선언 / _overlayHwnd + SWP_NOMOVE + SWP_NOSIZE 동시 존재) 중 하나도
        /// 만족하지 않는 SetWindowPos( 라인이 그 파일에 나타나면 — 예: 타 윈도우 핸들을 인자로 넘기거나
        /// 좌표를 실제로 바꾸는 호출이 추가되면 — 이 예외는 더 이상 적용되지 않고 그대로 실패 처리된다.
        /// </summary>
        private static bool IsSafeSelfOverlaySetWindowPosUsage(string line)
        {
            if (line.Contains("extern")) return true; // DllImport 시그니처 선언, 실제 호출이 아님.
            return line.Contains("_overlayHwnd")
                && line.Contains("SWP_NOMOVE")
                && line.Contains("SWP_NOSIZE");
        }

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
                    "27-7 체크리스트가 명시적으로 0건을 요구). Win32WindowService.cs의 자기 오버레이 " +
                    "Z-order 조정 1건만 화이트리스트로 허용하며, 그마저도 라인 단위로 재검증한다.",
                ExceptionsByFileName = new Dictionary<string, Func<string, bool>>
                {
                    ["Win32WindowService.cs"] = IsSafeSelfOverlaySetWindowPosUsage,
                },
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
        };

        [Test]
        public void 금지된_API_호출_패턴이_소스코드_어디에도_없다()
        {
            var files = CollectScannedSourceFiles();
            var violations = new List<string>();

            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string[] lines = File.ReadAllLines(filePath);

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
        public void SetWindowPos_화이트리스트_예외가_실제로_검증되고_있다()
        {
            // 화이트리스트 엔트리가 정작 아무 매치도 없어 사실상 테스트되지 않는 죽은 코드로 방치되는
            // 것을 막는 가드. Win32WindowService.cs 리팩터링으로 SetWindowPos 호출이 사라진다면(좋은
            // 신호), 위 ExceptionsByFileName 엔트리와 이 테스트를 함께 제거할 것.
            var files = CollectScannedSourceFiles();
            var win32File = files.FirstOrDefault(p => Path.GetFileName(p) == "Win32WindowService.cs");
            Assert.IsNotNull(win32File, "Win32WindowService.cs를 찾을 수 없습니다(사전 조건 확인).");

            int matchCount = File.ReadAllLines(win32File).Count(l => l.Contains("SetWindowPos("));
            Assert.Greater(matchCount, 0,
                "Win32WindowService.cs 안에 SetWindowPos( 호출이 하나도 없어 화이트리스트 예외 로직이 " +
                "실제로는 아무것도 검증하지 않고 있습니다.");
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
