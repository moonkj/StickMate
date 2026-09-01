using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Platform.Windows;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-01 — <b>창 열거의 블로킹 호출 제거</b> 회귀 잠금(coder).
    ///
    /// ============================================================================
    /// 무엇이 문제였나 (실기 로그로 확정. 추측 아님)
    /// ============================================================================
    /// 사용자 Windows 실기 로그(릴리즈 20260901d, 계측 포함):
    /// <code>
    /// [발판진단] 사유별 [IsWindowVisible=false=796, 최소화=20, 제목 없음=19,
    ///            DWM cloaked=3, WS_EX_TOOLWINDOW=2, 우리 자신의 창=1, 알파≈0=1, 완전히 가려짐=4]
    /// [발판열거] 1회 평균 14.09ms / 최대 199.27ms, 94회/30초 (실행 시간의 4.41%)
    /// </code>
    /// 한 번 열거에 846개를 훑는데 <b>1회 최대 199ms</b> — 창 하나당 0.23ms다. 커널의 창 구조체를
    /// 읽는 단순 검사로는 나올 수 없는 값이다.
    ///
    /// <para>범인은 <c>Win32WindowService.ClassifyWindowStyle</c>의 한 줄이었다:
    /// <c>GetWindowTextLength</c>는 대상 창에 <c>WM_GETTEXTLENGTH</c>를 보내고 <b>그 창의 메시지
    /// 루프가 응답할 때까지 블로킹한다</b>. 796개가 <c>IsWindowVisible</c>에서 걸러지고 남은
    /// ~50개가 이 검사까지 오는데, 그중 <b>하나만</b> 바쁜 앱이어도 우리 프레임이 멈췄다.
    /// 극심한 편차(1.36ms ~ 199ms)와 "켜둘수록 심해진다"는 신고가 전부 이것으로 설명된다.
    /// macOS에 같은 증상이 없는 이유도 같다 — <c>CGWindowListCopyWindowInfo</c>는 목록을 한 번에
    /// 스냅샷으로 받아와 창별 왕복이 원리적으로 없다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것
    /// ============================================================================
    /// <list type="bullet">
    /// <item><b>T1~T3</b> — <b>판정이 달라지지 않았다.</b> 이것이 가장 중요하다. 제목 판정이 바뀌면
    ///   발판 후보 집합이 바뀌고, 최악의 경우 <b>사용자 눈에 보이지 않는 창 위에 캐릭터가 선다</b>
    ///   (Win32WindowService 주석이 명시적으로 경고하는 실패 모드).</item>
    /// <item><b>T4~T5</b> — 예산이 <b>프레임 예산에서 유도</b>된다(하드코딩 금지, 저장소 규칙).</item>
    /// <item><b>T6~T8</b> — 블로킹 API가 <b>전수</b> 사라졌고 다시 들어오지 않는다.</item>
    /// <item><b>T9~T11</b> — 필터 <b>순서</b>. 값싼 검사가 앞, 크로스 프로세스 호출이 뒤.
    ///   특히 자기 프로세스 검사가 제목 조회보다 앞이어야 하는 것은 성능이 아니라 <b>안전</b>
    ///   조건이다(폴백 경로의 WndProc 재진입 방지).</item>
    /// <item><b>T12~T14</b> — 폴백/할당0/계측이 살아 있다.</item>
    /// <item><b>T15</b> — (네거티브 컨트롤) 이 파일의 소스 스캐너가 <b>실제로 볼 수 있는가</b>.</item>
    /// </list>
    ///
    /// <para>Win32 P/Invoke는 <c>#if UNITY_STANDALONE_WIN</c> 안이라 이 개발 환경(macOS)에서
    /// 실행할 수 없다. 그래서 <b>판정 규칙</b>은 플랫폼 중립 <see cref="WindowsFootholdFilter"/>에서
    /// 실측하고, <b>배선/순서</b>는 이 저장소가 이미 쓰는 소스 정적 스캔
    /// (<c>UserAssetImmutabilityAuditTests</c> / <c>WindowsFootholdFilterTests</c>의 D2~D5)으로
    /// 잠근다.</para>
    /// </summary>
    public sealed class WindowsTitleProbeBlockingTests
    {
        private const string LogPrefix = "[제목조회블로킹-TEST]";

        // ====================================================================
        // 판정 동치 — "제목 없는 창 걸러내기"가 그대로 유지되는가
        // ====================================================================

        [Test]
        public void T1_캡션이_비면_제목없음_한글자라도_있으면_제목있음()
        {
            Assert.IsFalse(WindowsFootholdFilter.HasWindowTitle(0),
                $"{LogPrefix} 복사된 글자 수 0 = 캡션이 빈 문자열. 이전 GetWindowTextLength==0과 같은 자리다. " +
                "여기서 true가 되면 제목 없는 껍데기 창이 전부 발판이 되어 캐릭터가 허공에 선다.");

            Assert.IsTrue(WindowsFootholdFilter.HasWindowTitle(1),
                $"{LogPrefix} 버퍼가 1글자뿐이어도 캡션이 있으면 1을 돌려받는다 — 그것이 '제목 있음'이다.");

            Assert.IsTrue(WindowsFootholdFilter.HasWindowTitle(64),
                $"{LogPrefix} 긴 제목도 당연히 '제목 있음'이다.");
        }

        [Test]
        public void T2_음수_반환은_제목없음으로_본다()
        {
            // 문서상 음수는 나오지 않지만, 미래에 다른 조회 API로 바뀌어 실패를 음수로 알리는 경우
            // '제목 있음'으로 오독하면 보이지 않는 창이 발판이 된다. 보수적으로 막아 둔다.
            Assert.IsFalse(WindowsFootholdFilter.HasWindowTitle(-1),
                $"{LogPrefix} 조회 실패/음수를 '제목 있음'으로 해석하면 안 된다.");
        }

        [Test]
        public void T3_공백만_있는_제목은_이전과_똑같이_제목있음으로_남는다()
        {
            // 이전 구현은 길이만 봤으므로 " "(공백 1글자)짜리 캡션도 '제목 있음'이었다.
            // 새 구현이 트림을 도입하면 그 창들이 조용히 발판에서 사라진다 = 판정 변경.
            // 이 프로젝트가 요구한 것은 "기능 유지"이므로 트림하지 않는 것이 정답이다.
            Assert.IsTrue(WindowsFootholdFilter.HasWindowTitle(" ".Length),
                $"{LogPrefix} 공백만 있는 제목의 취급이 바뀌면 발판 후보 집합이 달라진다.");

            string filterSource = ReadSource(FilterPath);
            Assert.IsFalse(filterSource.Contains("HasWindowTitle") && filterSource.Contains(".Trim()"),
                $"{LogPrefix} 제목 판정에 Trim이 끼어들면 이전 집합과 달라진다.");
        }

        // ====================================================================
        // 예산은 프레임 예산에서 유도한다 (하드코딩 금지 — 저장소 규칙)
        // ====================================================================

        [Test]
        public void T4_제목조회_예산은_프레임_예산에_비례한다()
        {
            float at60 = WindowsFootholdFilter.DeriveTitleProbeBudgetMs(60);
            float at30 = WindowsFootholdFilter.DeriveTitleProbeBudgetMs(30);

            Assert.AreEqual(1000f / 60f * WindowsFootholdFilter.TitleProbeFrameBudgetShare, at60, 0.0001f,
                $"{LogPrefix} 60fps 예산 16.7ms의 몫이어야 한다.");

            Assert.AreEqual(at60 * 2f, at30, 0.0001f,
                $"{LogPrefix} 목표 fps가 절반이면 프레임 예산이 두 배이므로 경보선도 두 배여야 한다. " +
                "고정 상수라면 저전력 등급(30fps)에서 의미가 달라진다.");

            Assert.Less(at60, 1000f / 60f,
                $"{LogPrefix} 예산이 프레임 전체보다 크면 어떤 스톨도 잡지 못한다.");
        }

        [Test]
        public void T5_프레임_목표를_모르면_60fps로_대체한다()
        {
            // Application.targetFrameRate는 vsync에 위임하면 -1이고, 아직 설정 전이면 0이다.
            // 그 값을 그대로 나누면 0 나눗셈/음수 예산이 나와 경보가 영구히 꺼지거나 도배된다.
            float fallback = WindowsFootholdFilter.DeriveTitleProbeBudgetMs(WindowsFootholdFilter.DefaultTargetFrameRate);

            Assert.AreEqual(fallback, WindowsFootholdFilter.DeriveTitleProbeBudgetMs(-1), 0.0001f,
                $"{LogPrefix} -1(vsync 위임)에서 기본 fps로 떨어져야 한다.");
            Assert.AreEqual(fallback, WindowsFootholdFilter.DeriveTitleProbeBudgetMs(0), 0.0001f,
                $"{LogPrefix} 0에서 0 나눗셈이 나면 예산이 무한대가 되어 경보가 영영 울리지 않는다.");
            Assert.Greater(fallback, 0f, $"{LogPrefix} 예산은 항상 양수여야 한다.");
        }

        // ====================================================================
        // 블로킹 API 전수 제거 — 하나 고치고 다른 게 남으면 증상은 그대로다
        // ====================================================================

        [Test]
        public void T6_열거_경로에_GetWindowTextLength가_한_줄도_없다()
        {
            string[] code = StripComments(File.ReadAllLines(Win32Path));
            var hits = new List<string>();
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i].Contains("GetWindowTextLength")) hits.Add($"{i + 1}: {code[i].Trim()}");
            }

            Assert.IsEmpty(hits,
                $"{LogPrefix} GetWindowTextLength가 돌아왔습니다. 이 함수는 대상 창에 " +
                "WM_GETTEXTLENGTH를 보내고 그 창의 메시지 루프가 응답할 때까지 블로킹합니다 — " +
                "실기에서 [발판열거] 1회 최대 199.27ms의 원인이었습니다. 커널 구조체를 직접 읽는 " +
                "InternalGetWindowText(폴백 GetWindowTextW)를 쓰세요:\n" + string.Join("\n", hits));
        }

        /// <summary>
        /// 크로스 프로세스로 <b>메시지를 보내는</b> 호출 전수 감사.
        ///
        /// <para>안전한 것과 구별해야 한다: <c>GetClassName</c>/<c>GetWindowLong</c>/
        /// <c>IsWindowVisible</c>/<c>GetWindowRect</c>는 커널 구조체 읽기라 대상 프로세스를 깨우지
        /// 않는다. <c>DwmGetWindowAttribute</c>는 크로스 프로세스지만 상대가 <b>DWM 시스템
        /// 서비스</b>라 남의 앱의 응답성에 묶이지 않는다(성질이 다르다 — 그래서 금지가 아니라
        /// '맨 뒤로 미는' 대상이다). 금지 대상은 <b>임의의 앱의 메시지 루프를 기다리는</b> 것뿐이다.</para>
        /// </summary>
        private static readonly string[] BlockingMessageApis =
        {
            "SendMessage", "SendMessageTimeout", "SendMessageCallback", "SendNotifyMessage",
            "SendDlgItemMessage", "PostMessage", "PostThreadMessage", "ReplyMessage",
            "AttachThreadInput", "WaitForInputIdle",
        };

        [Test]
        public void T7_플랫폼_계층_어디에도_크로스프로세스_메시지_전송이_없다()
        {
            var violations = new List<string>();
            foreach (string path in PlatformSources())
            {
                string[] code = StripComments(File.ReadAllLines(path));
                for (int i = 0; i < code.Length; i++)
                {
                    foreach (string api in BlockingMessageApis)
                    {
                        if (!code[i].Contains(api)) continue;
                        violations.Add($"{Path.GetFileName(path)}:{i + 1}: '{api}' — {code[i].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(violations,
                $"{LogPrefix} 남의 창의 메시지 루프를 기다리는 호출이 발견됐습니다. 하나를 고치고 " +
                "다른 하나가 남으면 [발판열거]의 '최대' 값은 그대로입니다. 이 앱이 남의 창에 보낼 " +
                "메시지는 하나도 없습니다 — 전부 조회입니다(원칙 3):\n" + string.Join("\n", violations));
        }

        [Test]
        public void T8_제목조회는_남의_창에_메시지를_보내지_않는_API만_쓴다()
        {
            string src = ReadSource(Win32Path);

            StringAssert.Contains(
                "[DllImport(\"user32.dll\", CharSet = CharSet.Unicode, EntryPoint = \"InternalGetWindowText\")]",
                src, $"{LogPrefix} 주 경로(InternalGetWindowText) 선언이 사라졌습니다.");
            StringAssert.Contains(
                "[DllImport(\"user32.dll\", CharSet = CharSet.Unicode, EntryPoint = \"GetWindowTextW\")]",
                src, $"{LogPrefix} 폴백(GetWindowTextW) 선언이 사라졌습니다 — 문서화되지 않은 export " +
                     "하나에만 의존하면 그것이 없는 환경에서 제목 필터가 통째로 죽습니다.");

            // 두 선언 모두 CharSet.Auto가 아니라 Unicode로 못 박혀 있어야 한다(위 두 단언이 그것을
            // 문자 그대로 요구한다). Auto면 플랫폼에 따라 ANSI 진입점으로 갈 수 있고, 그러면
            // 비ASCII 제목에서 변환 경로가 끼어들어 "제목이 있는가"의 답이 달라질 여지가 생긴다.

            StringAssert.Contains("private const int TitleProbeBufferChars = 2;", src,
                $"{LogPrefix} 버퍼가 커지면 제목이 긴 창에서 복사 비용이 창 수에 비례해 늘어납니다. " +
                "'비었는가'만 알면 되므로 1글자 + 널 종단이면 충분합니다.");
        }

        // ====================================================================
        // 필터 순서 — 값싼 검사가 앞, 크로스 프로세스가 뒤
        // ====================================================================

        [Test]
        public void T9_값싼_필터가_전부_제목조회보다_앞에_있다()
        {
            string body = ReadClassifyWindowStyleBody();

            int visible = body.IndexOf("IsWindowVisible(hWnd)", StringComparison.Ordinal);
            int iconic = body.IndexOf("IsIconic(hWnd)", StringComparison.Ordinal);
            int exStyle = body.IndexOf("GetWindowLong(hWnd, GWL_EXSTYLE)", StringComparison.Ordinal);
            int pid = body.IndexOf("GetWindowThreadProcessId(hWnd", StringComparison.Ordinal);
            int title = body.IndexOf("ProbeHasTitle(hWnd)", StringComparison.Ordinal);

            Assert.Greater(visible, -1, $"{LogPrefix} IsWindowVisible 검사가 사라졌다.");
            Assert.Greater(title, -1, $"{LogPrefix} 제목 검사가 사라졌다 — 제목 없는 껍데기 창이 발판이 된다.");

            Assert.Less(visible, iconic, $"{LogPrefix} 실기에서 796개(전체의 94%)를 걸러내는 검사가 맨 앞이어야 한다.");
            Assert.Less(iconic, exStyle, $"{LogPrefix} 순수 상태 비트 검사끼리의 순서.");
            Assert.Less(exStyle, pid, $"{LogPrefix} 순수 상태 비트 검사끼리의 순서.");
            Assert.Less(pid, title,
                $"{LogPrefix} 제목 조회는 문자열을 버퍼로 복사하므로 순수 비트 검사보다 비싸다 — " +
                "값싼 검사로 최대한 걸러낸 뒤에만 물어야 한다.");
        }

        [Test]
        public void T10_자기_프로세스_검사가_제목조회보다_먼저다()
        {
            // 이건 성능이 아니라 **안전** 조건이다. 폴백 경로(GetWindowTextW)는 타 프로세스 창에는
            // 메시지를 보내지 않지만 **우리 자신의 창**에는 WM_GETTEXT를 보낸다. 그것은 EnumWindows
            // 콜백 한복판에서 우리 WndProc이 재진입한다는 뜻이다. 여기서 먼저 걸러 두면 그 상황
            // 자체가 성립하지 않는다.
            string body = ReadClassifyWindowStyleBody();
            int pid = body.IndexOf("pid == _currentProcessId", StringComparison.Ordinal);
            int title = body.IndexOf("ProbeHasTitle(hWnd)", StringComparison.Ordinal);

            Assert.Greater(pid, -1, $"{LogPrefix} 자기 프로세스 검사가 사라졌다 — 우리 오버레이가 발판이 된다.");
            Assert.Less(pid, title,
                $"{LogPrefix} 순서가 뒤집히면 폴백 경로에서 EnumWindows 콜백 중 우리 WndProc이 " +
                "재진입한다. 성능이 아니라 안전 조건이므로 되돌리지 말 것.");
        }

        [Test]
        public void T11_크로스프로세스_DWM_조회가_맨_뒤에_남아_있다()
        {
            string body = ReadClassifyWindowStyleBody();
            int title = body.IndexOf("ProbeHasTitle(hWnd)", StringComparison.Ordinal);
            int cloaked = body.IndexOf("IsCloaked(hWnd)", StringComparison.Ordinal);

            Assert.Greater(cloaked, -1, $"{LogPrefix} DWM cloaked 검사가 사라졌다 — 종료된 UWP 껍데기 창 위에 캐릭터가 선다.");
            Assert.Less(title, cloaked,
                $"{LogPrefix} IsCloaked는 DWM 프로세스로 가는 크로스 프로세스 호출이라 이 함수에서 " +
                "가장 비싸다. 값싼 검사를 전부 통과한 창만 그 값을 내야 한다.");
        }

        // ====================================================================
        // 폴백 / 할당0 / 계측
        // ====================================================================

        [Test]
        public void T12_폴백_결정은_열거_루프_밖에서_한_번만_한다()
        {
            string src = ReadSource(Win32Path);

            int resolveAt = src.IndexOf("private void ResolveTitleProbeApi", StringComparison.Ordinal);
            Assert.Greater(resolveAt, 0,
                $"{LogPrefix} ResolveTitleProbeApi가 사라졌습니다 — 문서화되지 않은 export가 없는 " +
                "환경에서 제목 필터가 통째로 죽습니다.");

            string resolve = src.Substring(resolveAt, Math.Min(1400, src.Length - resolveAt));
            StringAssert.Contains("EntryPointNotFoundException", resolve,
                $"{LogPrefix} export가 없을 때 나는 예외를 잡지 않으면 첫 열거에서 폴백 없이 터집니다.");

            string probe = ExtractMember(src, "private bool ProbeHasTitle");
            Assert.IsFalse(probe.Contains("try"),
                $"{LogPrefix} 창마다 try/catch를 돌면, export가 없는 환경에서 한 번의 열거가 예외를 " +
                "수백 개 던지는 경로가 됩니다. 존재 확인은 패스 시작 전 1회만 하세요.");

            StringAssert.Contains("ResolveTitleProbeApi();", src,
                $"{LogPrefix} 결정 함수가 열거 진입점에서 호출되지 않으면 영원히 미결정 상태로 " +
                "폴백 경로만 돕니다.");
        }

        [Test]
        public void T13_제목조회_버퍼는_재사용되고_할당이_없다()
        {
            string src = ReadSource(Win32Path);

            StringAssert.Contains("private readonly char[] _titleProbeBuffer = new char[TitleProbeBufferChars];", src,
                $"{LogPrefix} 버퍼는 인스턴스당 1개를 재사용해야 합니다(24시간 상주 앱 — 폴링 경로 할당 0).");

            string probe = ExtractMember(src, "private bool ProbeHasTitle");
            Assert.IsFalse(probe.Contains("new "),
                $"{LogPrefix} 제목 조회는 초당 수백 번 도는 경로입니다. 여기서 할당이 생기면 " +
                "GC 압박이 24시간 누적됩니다:\n" + probe);
            Assert.IsFalse(probe.Contains("StringBuilder") || probe.Contains("ToString()"),
                $"{LogPrefix} 제목 '문자열'을 만들 이유가 없습니다 — 비었는가만 보면 됩니다. " +
                "덤으로 열거한 남의 창 정보를 보관하지 않는다는 이 파일의 원칙도 함께 지켜집니다.");
        }

        [Test]
        public void T14_블로킹에_쓴_시간이_따로_계측되고_진단_로그에_실린다()
        {
            string src = ReadSource(Win32Path);

            StringAssert.Contains("public float LastTitleProbeMs { get; private set; }", src,
                $"{LogPrefix} 제목 조회에만 쓴 시간을 따로 재지 않으면 다음 실기 로그가 이 수정의 " +
                "성패를 증명하지 못합니다.");
            StringAssert.Contains("public int LastTitleProbeCount { get; private set; }", src,
                $"{LogPrefix} 시간만으로는 '한 번이 느렸는가'와 '횟수가 많았는가'를 못 가릅니다.");

            string diagnostics = ExtractMember(src, "public void AppendWindowDiagnostics");
            StringAssert.Contains("LastTitleProbeMs", diagnostics,
                $"{LogPrefix} [발판진단] 줄은 사용자가 그대로 복사해 보내는 물건입니다. 여기에 " +
                "실리지 않으면 원격에서 이 수치를 볼 방법이 없습니다.");
            StringAssert.Contains("LastTitleProbeCount", diagnostics);

            StringAssert.Contains("DeriveTitleProbeBudgetMs", src,
                $"{LogPrefix} 경보 문턱을 프레임 예산에서 유도하지 않고 숫자로 박으면 저전력 등급에서 " +
                "의미가 달라집니다(저장소 규칙: 수치 하드코딩 금지).");
        }

        // ====================================================================
        // 네거티브 컨트롤 — 이 스캐너가 실제로 볼 수 있는가
        // ====================================================================

        [Test]
        public void T15_컨트롤_스캐너는_실제_호출만_잡고_주석과_로그문구는_넘긴다()
        {
            string[] sample =
            {
                // (0)(1) 실제 위반 — 옛 선언과 옛 호출. 반드시 잡혀야 한다.
                "        private static extern int GetWindowTextLength(IntPtr hWnd);",
                "            if (GetWindowTextLength(hWnd) == 0) return WindowsFootholdRejection.NoTitle;",

                // (2)(3) 주석 인용 — 넘겨야 한다.
                "        /// 이전에는 GetWindowTextLength가 여기 있었고 SendMessageTimeout도 검토했다.",
                "        // SendMessage 계열은 이 앱에 단 한 건도 없다.",

                // (4) ★ 이 라운드에서 실제로 걸린 함정: 진단 로그가 옛 범인을 정직하게 인용한다.
                //     넘겨야 한다 — 그러지 않으면 다음 사람은 로그에서 사실을 지운다.
                "            Debug.Log(\"이전 구현 GetWindowTextLength는 SendMessage 계열이었다\");",

                // (5) 무관한 코드.
                "        private bool ProbeHasTitle(IntPtr hWnd) { return true; }",
            };

            string[] stripped = StripComments(sample);
            Assert.AreEqual(sample.Length, stripped.Length,
                $"{LogPrefix} 주석/리터럴 제거가 줄 수를 바꾸면 실패 메시지의 줄 번호가 거짓말이 됩니다.");

            var caught = new List<int>();
            for (int i = 0; i < sample.Length; i++)
            {
                if (stripped[i].Contains("GetWindowTextLength") || stripped[i].Contains("SendMessage"))
                {
                    caught.Add(i);
                }
            }

            CollectionAssert.AreEqual(new[] { 0, 1 }, caught,
                $"{LogPrefix} 잡혀야 하는 것은 선언(0)과 호출(1) 두 줄뿐입니다. " +
                "0건이면 스캐너가 눈이 멀었고(이 파일의 모든 소스 감사가 공허해집니다), " +
                "2·3이 섞이면 주석까지 세는 것이고, 4가 섞이면 로그 문구 때문에 테스트가 " +
                $"빨개진다는 뜻입니다. 실제로 잡힌 줄: [{string.Join(", ", caught)}]");

            Assert.IsTrue(sample[4].Contains("GetWindowTextLength"),
                $"{LogPrefix} 표본 4번에 '정직한 로그 문구' 상황이 실제로 들어 있어야 이 컨트롤이 " +
                "의미가 있습니다(표본이 상황을 재현하지 못하면 통과는 공허합니다).");
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string Win32Path =>
            Path.Combine(PlatformRoot, "Windows", "Win32WindowService.cs");

        private static string FilterPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsFootholdFilter.cs");

        private static IEnumerable<string> PlatformSources() =>
            Directory.GetFiles(PlatformRoot, "*.cs", SearchOption.AllDirectories);

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// <b>실제로 실행되는 코드만</b> 남긴다(줄 번호 보존). 두 가지를 걷어낸다:
        ///
        /// <list type="number">
        /// <item><b>줄 전체가 주석인 줄.</b> <see cref="UserAssetImmutabilityAuditTests"/>와 같은
        ///   규칙·같은 이유다 — 금지 API를 "왜 금지인지" 설명하려고 주석에 인용하는 것까지 막으면
        ///   다음 사람은 설명을 지운다. 실제 호출은 주석 줄에 있을 수 없다(컴파일이 안 된다).</item>
        /// <item><b>문자열/문자 리터럴의 내용.</b> ★ 이 라운드에서 실제로 걸린 함정이다:
        ///   Win32WindowService의 진단 로그가 <b>이전 범인의 이름을 정직하게 인용</b>한다
        ///   ("이전 구현 GetWindowTextLength는 그 반대였고 ..."). 그 문장은 사용자 로그를 읽는
        ///   사람에게 값진 정보인데, 리터럴을 코드로 세면 그것 때문에 테스트가 빨개진다.
        ///   그러면 다음 사람은 로그에서 사실을 지운다 — 감사가 지키려던 것과 정반대 결과다.
        ///   우리가 잠그려는 것은 <b>호출</b>이지 <b>언급</b>이 아니다.</item>
        /// </list>
        ///
        /// <para>여는 따옴표/닫는 따옴표는 남기므로 구문 구조는 보존된다. 여러 줄에 걸친 문자열이나
        /// verbatim(<c>@"..."</c>)의 <c>""</c> 이스케이프는 완벽히 처리하지 않지만, 그 경우 새어 나오는
        /// 것은 <b>문자열 조각</b>이라 금지 API 이름과 일치할 일이 사실상 없다. 반대 방향(진짜 호출을
        /// 놓치는 쪽)의 오류는 구조상 생기지 않는다 — 코드는 리터럴 밖에 있다.</para>
        /// </summary>
        private static string[] StripComments(string[] lines)
        {
            var kept = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                kept[i] = t.StartsWith("//", StringComparison.Ordinal)
                          || t.StartsWith("*", StringComparison.Ordinal)
                    ? string.Empty
                    : BlankLiterals(lines[i]);
            }
            return kept;
        }

        /// <summary>문자열/문자 리터럴의 <b>내용</b>만 비운다(따옴표 자체는 남긴다).</summary>
        private static string BlankLiterals(string line)
        {
            var sb = new System.Text.StringBuilder(line.Length);
            bool inString = false, inChar = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inString || inChar)
                {
                    if (c == '\\' && i + 1 < line.Length) { i++; continue; }   // 이스케이프 쌍 통째로 건너뜀
                    if (inString && c == '"') { inString = false; sb.Append(c); }
                    else if (inChar && c == '\'') { inChar = false; sb.Append(c); }
                    continue;
                }

                if (c == '"') { inString = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>선언 시그니처부터 다음 멤버 선언 전까지를 잘라낸다(대략적이지만 스캔에는 충분하다).</summary>
        private static string ExtractMember(string src, string signature)
        {
            int start = src.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(start, 0, $"{LogPrefix} '{signature}'를 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 테스트도 함께 갱신해야 합니다(조용히 통과하면 안 됩니다).");

            int end = src.IndexOf("\n        /// <summary>", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(src.Length, start + 2000);
            return src.Substring(start, end - start);
        }

        /// <summary>
        /// <c>ClassifyWindowStyle</c>의 <b>본문</b>만 잘라낸다. 주석은 걷어낸 뒤 자르므로,
        /// 문서에 API 이름을 아무리 인용해도 순서 판정에는 영향이 없다.
        /// </summary>
        private static string ReadClassifyWindowStyleBody()
        {
            string[] code = StripComments(File.ReadAllLines(Win32Path));
            string src = string.Join("\n", code);

            int start = src.IndexOf("private WindowsFootholdRejection ClassifyWindowStyle", StringComparison.Ordinal);
            Assert.Greater(start, 0, $"{LogPrefix} ClassifyWindowStyle을 찾지 못했습니다.");

            int end = src.IndexOf("private static float ReadWindowAlpha", start, StringComparison.Ordinal);
            if (end < 0) end = src.IndexOf("private bool ProbeHasTitle", start, StringComparison.Ordinal);
            Assert.Greater(end, start, $"{LogPrefix} ClassifyWindowStyle의 끝을 찾지 못했습니다.");

            return src.Substring(start, end - start);
        }
    }
}
