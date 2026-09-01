using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// **스톨 귀인 계측**(2026-09-01 프레임스파이크 라운드)을 잠그는 테스트.
    ///
    /// ============================================================================
    /// 이 라운드가 풀어야 했던 문제
    /// ============================================================================
    /// 사용자 실기 로그(릴리즈 20260901c)는 스파이크의 존재까지만 말했다:
    /// <code>
    /// [프레임스파이크] 누적 55회 — 109/116/140/171/276/315/332ms
    ///   백버퍼: 3831x2160(변화 없음). GC: gen0 +0 / gen1 +0.
    /// [프레임시간] 루프 평균 30.98ms p95 89.81ms p99 277.95ms 최대 815.95ms
    /// </code>
    /// 백버퍼 불변 + GC 0으로 스왑체인 재생성과 관리 힙 GC는 배제됐지만, 남은 후보
    /// (네이티브 창 열거 / 파일 IO / 그 밖)를 <b>가를 계측이 하나도 없었다</b>.
    ///
    /// 이 파일은 그 계측이 (a) 올바르게 판정하고 (b) <b>스스로 비싸지 않으며</b>
    /// (c) 나중 리팩터링에 조용히 빠지지 않도록 잠근다. (c)가 특히 중요하다 — 계측 배선은
    /// 기능이 아니라서 삭제돼도 아무 테스트도 깨지지 않는 것이 보통이다.
    /// </summary>
    public class StallAttributionTests
    {
        private static string ScriptsDir => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string ReadScript(params string[] parts)
            => File.ReadAllText(Path.Combine(ScriptsDir, Path.Combine(parts)));

        [SetUp]
        public void SetUp()
        {
            StallAttribution.ResetForTests();
            PlayerLogPolicy.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            StallAttribution.ResetForTests();
            PlayerLogPolicy.ResetForTests();
        }

        // ====================================================================================
        // A. 판정표 — "한 줄로 갈린다"의 본체
        // ====================================================================================

        /// <summary>
        /// 로직 구간이 프레임의 절반도 안 되면 원인은 우리 Update 밖이다.
        /// <b>이 케이스가 이 라운드의 핵심 반례다</b>: [프레임스파이크]가 남기던 문장
        /// ("GC 0 + 백버퍼 그대로면 네이티브 창 열거/파일 IO 쪽이다")은 거짓 이분법이며,
        /// 렌더/프레젠트/OS 합성 대기도 GC를 만들지 않고 백버퍼도 바꾸지 않는다.
        /// </summary>
        [Test]
        public void 로직밖이_대부분이면_렌더프레젠트로_판정한다()
        {
            // 300ms 프레임인데 우리 Update+LateUpdate는 5ms밖에 안 썼다.
            var verdict = StallAttribution.Attribute(frameMs: 300f, logicMs: 5f, enumMs: 0.4f, logMs: 0.2f);
            Assert.AreEqual(StallCulprit.OutsideLogic, verdict,
                "로직 구간이 5/300ms(1.7%)인데도 우리 코드를 범인으로 지목하면 다음 라운드가 " +
                "엉뚱한 곳을 판다. 이 케이스가 정확히 오늘 4번 헛나간 추측 수정의 재발 방지선이다.");
        }

        [Test]
        public void 창열거가_지배적이면_창열거로_판정한다()
        {
            var verdict = StallAttribution.Attribute(frameMs: 200f, logicMs: 190f, enumMs: 180f, logMs: 1f);
            Assert.AreEqual(StallCulprit.WindowEnumeration, verdict);
        }

        [Test]
        public void 로그쓰기가_지배적이면_로그쓰기로_판정한다()
        {
            var verdict = StallAttribution.Attribute(frameMs: 200f, logicMs: 190f, enumMs: 1f, logMs: 180f);
            Assert.AreEqual(StallCulprit.LogWrite, verdict);
        }

        [Test]
        public void 로직_안이지만_열거도_로그도_아니면_기타로직이다()
        {
            var verdict = StallAttribution.Attribute(frameMs: 200f, logicMs: 190f, enumMs: 2f, logMs: 2f);
            Assert.AreEqual(StallCulprit.OtherLogic, verdict,
                "가려짐 솔버(O(n^2))/렌더러 갱신 같은 세 번째 부류가 존재한다 — 이걸 창열거로 " +
                "뭉뚱그리면 구조 변경 라운드가 헛수고한다.");
        }

        [Test]
        public void 지배적_기여자가_없으면_혼합으로_정직하게_보고한다()
        {
            // 로직 60% / 그 밖 40%, 로직 안에서도 셋이 고루 나눠 가진 경우.
            var verdict = StallAttribution.Attribute(frameMs: 100f, logicMs: 60f, enumMs: 20f, logMs: 20f);
            Assert.AreEqual(StallCulprit.Mixed, verdict,
                "확정하지 못했다는 사실을 숨기고 아무 이름이나 붙이면 그게 바로 '추측 수정'이다.");
        }

        [Test]
        public void 프레임시간이_0이면_판정불가다()
        {
            Assert.AreEqual(StallCulprit.Unknown, StallAttribution.Attribute(0f, 0f, 0f, 0f));
        }

        [Test]
        public void 로직시간이_프레임시간을_넘겨도_음수나_100퍼센트초과가_되지_않는다()
        {
            // 측정 시점 차이로 로직 ms가 프레임 ms보다 크게 나올 수 있다(경계 케이스).
            var verdict = StallAttribution.Attribute(frameMs: 100f, logicMs: 140f, enumMs: 130f, logMs: 0f);
            Assert.AreEqual(StallCulprit.WindowEnumeration, verdict,
                "클램프가 없으면 '로직밖'이 음수가 되어 판정이 뒤집힌다.");
        }

        // ====================================================================================
        // B. 태그 히스토그램 — "어떤 로그가 제일 많이 찍히는가"를 앱이 스스로 센다
        // ====================================================================================

        [Test]
        public void 태그별로_세고_상위_3개를_보고한다()
        {
            for (int i = 0; i < 10; i++) StallAttribution.CountTag("[유휴동작] 주위 살피기 재생 — 상태=Idle");
            for (int i = 0; i < 5; i++) StallAttribution.CountTag("[말풍선] 제거 — 정상 종료");
            for (int i = 0; i < 3; i++) StallAttribution.CountTag("[발판변경] 1 -> 2");
            StallAttribution.CountTag("[벽타기] 진입");

            string top = StallAttribution.TopTags();
            StringAssert.Contains("[유휴동작] x10", top);
            StringAssert.Contains("[말풍선] x5", top);
            StringAssert.Contains("[발판변경] x3", top);
            StringAssert.DoesNotContain("[벽타기]", top, "상위 3개만 보고해야 한 줄에 들어간다.");
        }

        [Test]
        public void 태그가_비슷해도_서로_섞이지_않는다()
        {
            StallAttribution.CountTag("[벽타기] 진입");
            StallAttribution.CountTag("[벽타기] 완료");
            StallAttribution.CountTag("[뛰어내리기] 결정");

            string top = StallAttribution.TopTags();
            StringAssert.Contains("[벽타기] x2", top);
            StringAssert.Contains("[뛰어내리기] x1", top);
        }

        [Test]
        public void 태그가_없는_줄도_잃지_않고_따로_센다()
        {
            StallAttribution.CountTag("Unloading 3 unused Assets");
            StallAttribution.CountTag(null);
            StallAttribution.CountTag(string.Empty);

            StringAssert.Contains("3", StallAttribution.TopTags(),
                "Unity 엔진 자신이 찍는 줄(Unloading 등)도 파일 IO 비용을 낸다 — 세지 않으면 " +
                "'로그가 적다'는 잘못된 결론이 나온다.");
        }

        [Test]
        public void 여는_대괄호로_시작해도_닫히지_않으면_태그로_보지_않는다()
        {
            StallAttribution.CountTag("[닫히지 않은 태그 — 아주 긴 문장이 계속 이어지는데 닫는 괄호가 없다");
            StringAssert.Contains("태그 없", StallAttribution.TopTags(),
                "닫는 괄호를 문장 끝까지 찾으면 긴 로그마다 O(n) 스캔이 되어 계측이 비싸진다.");
        }

        // ====================================================================================
        // C. 계측이 증상을 만들지 않는다 — 오늘 이미 겪은 사고의 재발 방지
        // ====================================================================================

        /// <summary>
        /// 직전 z-order 라운드에서 "진단 장치가 초당 10회 전체 창을 열거해 증상을 키운" 사고가 있었다.
        /// 이 계측 코드에는 <b>OS 창 조회가 한 줄도 없어야 한다.</b>
        /// </summary>
        [Test]
        public void 계측코드_자체에는_OS_창조회가_없다()
        {
            foreach (string file in new[] { "StallAttribution.cs", "StallAttributionProbe.cs", "PlayerLogPolicy.cs" })
            {
                // ★ 2026-09-01 정정: 예전에는 파일 <b>원문</b>을 스캔해서 <b>문서 주석에 적힌 API 이름</b>까지
                // 위반으로 잡았다(실제로 이 테스트가 그 이유 하나로 빨갛게 남아 있었다 — Tasklist.md 기록).
                // 그건 거짓 양성이고, 더 나쁘게는 "고치려면 문서에서 정확한 API 이름을 지워야 한다"는
                // 압력을 만든다. 이 테스트가 지키려는 것은 <b>실행되는 코드</b>이므로 주석을 벗기고 본다.
                string src = StripComments(ReadScript("Platform", file));
                foreach (string banned in new[] { "EnumWindows", "DllImport", "GetWindowRect", "DwmGetWindowAttribute",
                                                  "FindObjectsByType", "FindAnyObjectByType", "GetComponentsInChildren" })
                {
                    StringAssert.DoesNotContain(banned, src,
                        $"{file}의 **실행 코드**에 {banned}가 들어갔다 — 진단 장치가 증상을 만드는 그 사고의 재발이다.");
                }
            }
        }

        /// <summary>줄 주석(<c>//</c>, <c>///</c>)과 블록 주석(<c>/* */</c>)을 지운다. 문자열 리터럴 안의
        /// <c>//</c>까지 정밀하게 다루지는 않지만, 이 파일들에는 그런 리터럴이 없고 이 테스트의 목적
        /// (실행 코드에 OS 호출이 있는가)에는 충분하다.</summary>
        private static string StripComments(string src)
        {
            src = System.Text.RegularExpressions.Regex.Replace(src, @"/\*.*?\*/", string.Empty,
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var sb = new System.Text.StringBuilder(src.Length);
            foreach (string line in src.Split('\n'))
            {
                int i = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(i >= 0 ? line.Substring(0, i) : line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 프레임마다 도는 경로에서 <c>Screen</c>/<c>Application</c> 같은 네이티브 프로퍼티를
        /// 무조건 읽으면 안 된다. <c>Application.targetFrameRate</c>는 <b>스파이크 후보일 때만</b>
        /// 읽어야 한다(=100ms 절대 문턱 검사보다 뒤에 있어야 한다).
        /// </summary>
        [Test]
        public void targetFrameRate는_절대문턱_검사_뒤에서만_읽는다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            int guard = src.IndexOf("if (dtMs < SpikeAbsoluteMs) return;", System.StringComparison.Ordinal);
            int read = src.IndexOf("Application.targetFrameRate", System.StringComparison.Ordinal);
            Assert.Greater(guard, 0, "절대 문턱 조기 반환이 사라졌다.");
            Assert.Greater(read, guard,
                "평상시 프레임마다 Application.targetFrameRate를 읽으면 계측 자체가 비용이 된다.");
        }

        // ====================================================================================
        // D. 두 로그가 같은 프레임#으로 짝을 이룬다 — 사용자가 눈으로 대조할 수 있어야 한다
        // ====================================================================================

        /// <summary>
        /// <c>[스톨귀인]</c>은 <c>FramePacing</c>의 <c>[프레임스파이크]</c>와 <b>같은 문턱</b>을 써야
        /// 한 프레임에 두 줄이 함께 나온다. FramePacing.cs는 이번 라운드 소유가 아니라 그쪽에 필드를
        /// 넣을 수 없어 <b>값 동기화</b>로 대신했다 — 그 동기화가 깨지면 두 로그가 어긋나고 대조가
        /// 불가능해지므로 여기서 잠근다.
        /// </summary>
        [Test]
        public void 스파이크_문턱이_FramePacing과_같다()
        {
            string pacing = ReadScript("Platform", "FramePacing.cs");
            StringAssert.Contains("SpikeAbsoluteMs = 100f", pacing);
            StringAssert.Contains("SpikeRelativeFactor = 2.5f", pacing);
            Assert.AreEqual(100f, StallAttribution.SpikeAbsoluteMs, 0.001f);
            Assert.AreEqual(2.5f, StallAttribution.SpikeRelativeFactor, 0.001f);
        }

        // ====================================================================================
        // E. 배선이 조용히 빠지지 않게 잠근다
        // ====================================================================================

        /// <summary>
        /// 발판 폴링 경로의 스톱워치 배선. 이게 사라지면 다음 실기 로그에서 창열거 항목이 영원히
        /// 0.0ms로 나와 <b>후보 A가 거짓으로 무죄가 된다.</b>
        ///
        /// <para>★ 2026-09-02 정정 — 예전 이 문서에는 "<c>FootholdPoller</c>가 <b>유일한 호출자</b>라
        /// 거기 스톱워치 하나면 앱 전체가 빠짐없이 잡힌다"고 적혀 있었다. <b>거짓이었다.</b>
        /// 전체화면 판정 경로가 창 목록을 따로 조회한다 — 아래 H절이 그쪽을 잠근다.</para>
        /// </summary>
        [Test]
        public void 발판폴러가_열거_소요시간을_실측해_보고한다()
        {
            string src = ReadScript("Platform", "FootholdPoller.cs");
            StringAssert.Contains("Stopwatch.GetTimestamp()", src);
            StringAssert.Contains("StallAttribution.RecordWindowEnumeration(", src);

            int start = src.IndexOf("long enumStart = Stopwatch.GetTimestamp();", System.StringComparison.Ordinal);
            int call = src.IndexOf("_service.EnumerateFootholds();", System.StringComparison.Ordinal);
            int stop = src.IndexOf("long enumTicks = Stopwatch.GetTimestamp() - enumStart;", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "열거 직전 타임스탬프가 없다.");
            Assert.Greater(call, start, "타임스탬프가 열거 호출보다 뒤에 있다 — 0ms만 찍힌다.");
            Assert.Greater(stop, call, "종료 타임스탬프가 열거 호출을 감싸지 않는다.");
        }

        /// <summary>
        /// 리더가 지정한 세 측정값이 전부 배선돼 있는가:
        /// (1) 1회 소요 ms (2) 비싼 호출(DWM) 횟수 (3) 전체 열거 창 개수.
        /// </summary>
        [Test]
        public void 리더가_요청한_세_측정값이_전부_배선돼_있다()
        {
            string win32 = ReadScript("Platform", "Windows", "Win32WindowService.cs");
            StringAssert.Contains("IWindowEnumerationCostSource", win32, "(3) 전체 열거 개수 창구가 없다.");
            StringAssert.Contains("LastEnumeratedWindowCount", win32);
            StringAssert.Contains("LastDwmProbeCount", win32, "(2) 비싼 호출 횟수 창구가 없다.");
            StringAssert.Contains("_enumeratedWindowCount++;", win32);

            string poller = ReadScript("Platform", "FootholdPoller.cs");
            StringAssert.Contains("LastEnumeratedWindowCount", poller);
            StringAssert.Contains("LastDwmProbeCount", poller);

            string attribution = ReadScript("Platform", "StallAttribution.cs");
            StringAssert.Contains("[발판열거]", attribution,
                "구조 변경(폴링 -> 이벤트) 라운드의 before/after 기준선 줄이 없다 — 리더 지시 항목이다.");
        }

        /// <summary>
        /// 계측을 넣으면서 <b>열거 동작 자체를 바꾸면 안 된다</b>(리더 지시: 읽기·계측만).
        /// 콜백에 추가된 것이 int 증가뿐인지, 새 OS 호출이나 로그가 끼어들지 않았는지 확인한다.
        /// </summary>
        [Test]
        public void 계측은_열거_동작을_바꾸지_않았다()
        {
            string win32 = ReadScript("Platform", "Windows", "Win32WindowService.cs");
            int cb = win32.IndexOf("private bool OnEnumWindow(", System.StringComparison.Ordinal);
            Assert.Greater(cb, 0);
            int end = win32.IndexOf("private void BuildVisibleTopEdgeFootholds", System.StringComparison.Ordinal);
            Assert.Greater(end, cb);
            string body = win32.Substring(cb, end - cb);

            StringAssert.DoesNotContain("Debug.Log", body,
                "열거 콜백(창 수백 개 x 초당 3.3회)에 로그가 들어가면 계측이 곧 증상이 된다.");
            Assert.AreEqual(1, CountOccurrences(body, "_enumeratedWindowCount++"),
                "콜백당 정확히 1회만 세야 전체 창 수가 맞는다.");
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        // ====================================================================================
        // F. 명백한 낭비 제거 — 스택트레이스 / 상시 로그 양
        // ====================================================================================

        /// <summary>
        /// 릴리즈 빌드에서 Log/Warning의 스택트레이스를 끈다. 그러나
        /// <b>Error/Assert/Exception은 절대 끄지 않는다</b> — 예외 추적을 잃으면 원격 진단이 죽는다.
        /// </summary>
        [Test]
        public void 빌드스크립트가_정보로그_스택만_끄고_예외스택은_남긴다()
        {
            string build = File.ReadAllText(Path.Combine(Application.dataPath, "Editor", "BuildStandalone.cs"));
            StringAssert.Contains("SetStackTraceLogType(LogType.Log, StackTraceLogType.None)", build);
            StringAssert.Contains("SetStackTraceLogType(LogType.Warning, StackTraceLogType.None)", build);
            StringAssert.Contains("SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly)", build);
            StringAssert.Contains("SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly)", build);
            StringAssert.Contains("SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly)", build);

            StringAssert.DoesNotContain("SetStackTraceLogType(LogType.Error, StackTraceLogType.None)", build);
            StringAssert.DoesNotContain("SetStackTraceLogType(LogType.Exception, StackTraceLogType.None)", build);
            StringAssert.DoesNotContain("SetStackTraceLogType(LogType.Assert, StackTraceLogType.None)", build);

            // macOS/Windows **양쪽** 빌드 경로에서 불려야 한다(한쪽만 고치는 사고를 막는다).
            Assert.AreEqual(3, CountOccurrences(build, "ConfigureLogStackTraces"),
                "정의 1회 + macOS 빌드 1회 + Windows 빌드 1회여야 한다.");
        }

        /// <summary>
        /// 런타임에서도 같은 정책이 걸린다(다른 빌드 경로/에디터 UI로 되돌아가도 지켜지게).
        /// </summary>
        [Test]
        public void 런타임_정책도_예외스택을_남긴다()
        {
            string src = ReadScript("Platform", "PlayerLogPolicy.cs");
            StringAssert.Contains("Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly)", src);
            StringAssert.Contains("Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly)", src);
            StringAssert.DoesNotContain("LogType.Exception, StackTraceLogType.None", src);
        }

        [Test]
        public void 프로젝트설정의_스택트레이스가_정보로그만_꺼져있다()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string settings = File.ReadAllText(Path.Combine(root, "ProjectSettings", "ProjectSettings.asset"));
            int at = settings.IndexOf("m_StackTraceTypes:", System.StringComparison.Ordinal);
            Assert.Greater(at, 0);
            string value = settings.Substring(at + "m_StackTraceTypes:".Length).Trim();
            value = value.Split('\n')[0].Trim();

            Assert.AreEqual(48, value.Length, "int32 6칸(각 8자리 hex)이어야 한다.");
            // LogType 열거 순서: Error=0, Assert=1, Warning=2, Log=3, Exception=4 (Unity 공식 문서).
            Assert.AreEqual("01000000", value.Substring(0, 8), "Error 스택은 남겨야 한다.");
            Assert.AreEqual("01000000", value.Substring(8, 8), "Assert 스택은 남겨야 한다.");
            Assert.AreEqual("00000000", value.Substring(16, 8), "Warning 스택을 껐어야 한다.");
            Assert.AreEqual("00000000", value.Substring(24, 8), "Log 스택을 껐어야 한다.");
            Assert.AreEqual("01000000", value.Substring(32, 8), "Exception 스택은 남겨야 한다.");
        }

        [Test]
        public void 상시_동작서술_로그는_verbose_스위치를_따른다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                config.verboseDiagnosticsLogging = false;
                PlayerLogPolicy.Configure(config);
                Assert.IsFalse(PlayerLogPolicy.RoutineNarrationEnabled);

                config.verboseDiagnosticsLogging = true;
                PlayerLogPolicy.Configure(config);
                Assert.IsTrue(PlayerLogPolicy.RoutineNarrationEnabled);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void 설정이_없으면_로그를_잃지_않는다()
        {
            PlayerLogPolicy.Configure(null);
            Assert.IsTrue(PlayerLogPolicy.RoutineNarrationEnabled,
                "설정 배선 전(기동 직후)에 로그를 삼키면 기동 문제를 원격에서 볼 수 없게 된다.");
        }

        /// <summary>
        /// 스위치 검사는 <b>보간 문자열 조립보다 먼저</b> 와야 한다 — 뒤에 두면 꺼져 있어도
        /// 문자열이 만들어져 24시간 상주 앱의 "매 프레임 할당 금지" 컨벤션과 충돌한다.
        /// </summary>
        [Test]
        public void 로그_스위치는_문자열_조립_앞에_있다()
        {
            string src = ReadScript("Interaction", "IdleAmbientMotionRenderer.cs");
            int gate = src.IndexOf("PlayerLogPolicy.RoutineNarrationEnabled", System.StringComparison.Ordinal);
            int build = src.IndexOf("$\"[유휴동작]", System.StringComparison.Ordinal);
            Assert.Greater(gate, 0, "가장 많이 찍히던 로그(실측 2,564줄 중 661줄)의 스위치가 사라졌다.");
            Assert.Greater(build, gate, "스위치가 문자열 조립 뒤에 있으면 꺼도 할당은 그대로다.");
        }
        // ====================================================================================
        // ★ 2026-09-01 2차 라운드 — "기타로직"을 이름으로 쪼개는 구간 계측
        // ====================================================================================
        // 이 라운드가 풀어야 했던 문제: 1차 계측이 창 열거(0.5%)와 로그 IO(0.04%)를 무죄로 확정한 뒤
        // 남은 것이 <b>기타로직</b>(= 총시간 − 창열거 − 로그)이고, 실기 60초 요약이
        // <c>로직구간 최대 684.1ms</c>를 잡았다. "잔차"에는 이름이 없어 다음 라운드도 추측하게 된다.
        // 아래 테스트들은 그 이름표가 (a) 산술적으로 옳고 (b) 나중에 조용히 빠지지 않도록 잠근다.

        [Test]
        public void 구간_이름표가_열거형과_1대1이다()
        {
            foreach (StallSection section in System.Enum.GetValues(typeof(StallSection)))
            {
                if (section == StallSection.Count) continue;
                string name = StallAttribution.SectionName(section);
                Assert.AreNotEqual("알수없음", name,
                    $"{section}에 사람이 읽는 이름이 없다 — 실기 로그가 '구간 3번'이라고 말하면 아무도 못 읽는다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
            }
        }

        /// <summary>
        /// ★ 이 라운드의 핵심 산술. 중첩 구간을 그대로 더하면 <b>같은 시간이 두 번</b> 세어져
        /// "구간 합 &gt; 로직 시간"이 되고, 그러면 요약의 <c>미계측</c>이 음수가 되어 판독 불능이 된다.
        /// 실제 사례: <c>CharacterProgressionDirector.Update</c>(=연출감독)가 <c>CharacterSaveStore.Save</c>
        /// (=세이브)를 부른다. 자식 시간을 부모에서 빼야 두 값이 각자의 몫만 갖는다.
        /// </summary>
        [Test]
        public void 중첩_구간은_자기시간만_세어_이중계산하지_않는다()
        {
            StallAttribution.BeginSection(StallSection.Directors);
            Busy(3);
            StallAttribution.BeginSection(StallSection.Save);
            Busy(12);
            StallAttribution.EndSection();
            Busy(3);
            StallAttribution.EndSection();

            double parent = StallAttribution.WindowSectionMs(StallSection.Directors);
            double child = StallAttribution.WindowSectionMs(StallSection.Save);

            Assert.Greater(child, 0.0, "자식 구간이 0ms로 잡혔다 — 스톱워치 배선이 끊겼다.");
            Assert.Greater(child, parent,
                $"자식(세이브 {child:F2}ms)이 부모(연출감독 {parent:F2}ms)보다 오래 걸리게 만든 시나리오인데 " +
                "부모가 더 크다 = 자식 시간이 부모에도 더해졌다(이중계산).");
            Assert.AreEqual(1, StallAttribution.WindowSectionCount(StallSection.Save));
            Assert.AreEqual(1, StallAttribution.WindowSectionCount(StallSection.Directors));
        }

        [Test]
        public void using_범위는_예외가_나도_스택을_닫는다()
        {
            try
            {
                using (StallAttribution.Section(StallSection.UiWindows))
                {
                    throw new System.InvalidOperationException("의도된 예외");
                }
            }
            catch (System.InvalidOperationException) { /* 여기서 받아 삼킨다 */ }

            // 스택이 닫혔다면 다음 구간이 정상적으로 자기 이름으로 잡힌다(부모로 오염되지 않는다).
            using (StallAttribution.Section(StallSection.Portrait)) { Busy(2); }
            Assert.AreEqual(1, StallAttribution.WindowSectionCount(StallSection.UiWindows),
                "예외로 빠져나간 구간이 닫히지 않았다 — 이후 모든 구간이 그 안에 중첩된 것으로 집계된다.");
            Assert.AreEqual(1, StallAttribution.WindowSectionCount(StallSection.Portrait));
        }

        /// <summary>계측이 <b>스스로 GC 압박을 만들면</b> 24시간 상주 앱에서 그 자체가 증상이 된다.
        /// <c>SectionScope</c>가 값 타입이어야 <c>using</c>이 박싱 없이 Dispose를 부른다.</summary>
        [Test]
        public void 구간_범위는_값타입이라_힙할당이_없다()
        {
            Assert.IsTrue(typeof(StallAttribution.SectionScope).IsValueType,
                "SectionScope가 클래스가 되면 프레임당 40회 힙 할당이 생긴다 — 계측이 증상을 만드는 그 사고다.");
        }

        // ====================================================================================
        // 배선 감사 — 계측은 기능이 아니라서 빠져도 아무 테스트가 깨지지 않는다. 그래서 소스로 잠근다.
        // ====================================================================================

        /// <summary>
        /// ★ <b>세이브는 [스톨귀인]의 "로그" 항목에 잡히지 않는다</b>(Debug.Log가 아니므로).
        /// 그런데 이 경로는 Update 안에서 동기로 fsync + File.Replace를 한다 = 기타로직의 유력 후보.
        /// 이름표가 빠지면 다음 실기 로그가 또 침묵한다.
        /// </summary>
        [Test]
        public void 세이브_경로에_구간_이름표가_붙어_있다()
        {
            string src = ReadScript("Core", "CharacterSaveStore.cs");
            StringAssert.Contains("StallSection.Save", src,
                "CharacterSaveStore.Save()에서 [스톨구간] 계측이 사라졌다 — 동기 파일 IO가 다시 익명이 된다.");
        }

        /// <summary>
        /// ★ Unity 프레임 순서는 [FixedUpdate x K] -&gt; Update -&gt; LateUpdate다. 1차 계측은
        /// Update 시작~LateUpdate 끝만 쟀으므로 <b>물리를 통째로 "로직밖"으로 오분류</b>하고 있었다.
        /// 랙돌을 굴리는 앱에서 이건 결코 작은 구멍이 아니다.
        /// </summary>
        [Test]
        public void 물리_단계도_계측_대상에_들어와_있다()
        {
            string probe = ReadScript("Platform", "StallAttributionProbe.cs");
            StringAssert.Contains("BeginFixedStep", probe);
            StringAssert.Contains("EndFixedStep", probe);
            StringAssert.Contains("BeginLatePhase", probe);
            StringAssert.Contains("EndUpdatePhase", probe);
        }

        /// <summary>
        /// ★ 앞 라운드의 함정 재발 방지. 개별 스파이크 줄은 억제되는데, 실기에서 <b>가장 심한
        /// 프레임이 바로 그 억제된 줄</b>이었다. 그래서 "남은 줄이 전부 판정: 로직밖"이 "로직 안은
        /// 무죄"로 잘못 읽혔다. 최악 프레임 스냅샷은 <b>억제 검사보다 먼저</b> 있어야 한다.
        ///
        /// <para>★ 2026-09-02 R2-1 — 억제 수단이 고정 5초 쿨다운에서
        /// <see cref="StickMate.Platform.SpikeLogBackoff"/>(같은 등급이 이어지면 5→60초로 확대)로
        /// 바뀌었다. <b>불변식은 그대로다</b>: 억제가 무엇이든 스냅샷이 그보다 앞이어야 한다.
        /// 오히려 간격이 최대 60초까지 벌어지므로 이 순서는 <b>전보다 더 중요해졌다</b>.</para>
        /// </summary>
        [Test]
        public void 최악_프레임_스냅샷은_로그_억제보다_먼저_기록된다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            int snapshot = src.IndexOf("if (dtMs > _worstDtMs)", System.StringComparison.Ordinal);
            int suppress = src.IndexOf("_spikeBackoff.ShouldLog(", System.StringComparison.Ordinal);
            Assert.Greater(snapshot, 0, "최악 프레임 스냅샷이 사라졌다.");
            Assert.Greater(suppress, 0, "로그 억제 지점이 사라졌다 — 억제가 없으면 로그가 폭주한다.");
            Assert.Less(snapshot, suppress,
                "억제 뒤에서 스냅샷하면 억제된 프레임(=대개 가장 심한 프레임)의 귀인을 통째로 잃는다 — " +
                "실기 로그에서 실제로 벌어졌던 오독이다.");

            // 억제가 다시 고정 쿨다운으로 되돌아가면 R2-1(가려진 동안 230 B/s)이 재발한다.
            StringAssert.DoesNotContain("_spikeCooldownLeft = SpikeLogCooldownSeconds;", src);
        }

        /// <summary>
        /// 사용자 확정 조건이 "켜놓을수록 렉이 심해짐"(p50은 그대로, p99/최대만 커짐)이므로,
        /// 요약 줄은 <b>추세를 볼 수 있는 값</b>을 반드시 함께 남겨야 한다. 하나라도 빠지면
        /// 두 로그를 나란히 놓고 비교할 수 없다.
        /// </summary>
        [Test]
        public void 요약이_시간에_따른_증가를_볼_수_있게_남긴다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            foreach (string token in new[]
            {
                "_summaryIndex",              // 몇 번째 창인가
                "Time.realtimeSinceStartup",  // 가동 시간
                "SectionLifeMs",              // 구간별 생애 누적
                "SectionLifeMaxMs",           // 구간별 생애 최대
                "GetTotalMemory",             // 관리 힙 증가
                "CollectionCount",            // GC 세대별
                "_fontAtlasRebuildsTotal",    // 폰트 아틀라스 재구성 누적
            })
            {
                StringAssert.Contains(token, src,
                    $"{token}이(가) 요약에서 빠졌다 — '3분째 vs 30분째'를 비교할 근거가 사라진다.");
            }
        }

        /// <summary>
        /// ★ 플랫폼 중립 문구 감사(2026-09-01, 병행 라운드 지적). '정밀검사' 항목은 이제 macOS도
        /// 보고한다. 설명이 Windows 전용(<c>DWM 조회</c>)으로 남아 있으면 macOS 로그를 읽는 사람이
        /// "이 값은 나와 무관하다"고 잘못 판단한다.
        /// </summary>
        [Test]
        public void 열거_비용_설명이_플랫폼_중립이다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            StringAssert.DoesNotContain("DWM조회", src,
                "로그 본문이 Windows 전용 용어를 쓰고 있다 — macOS도 같은 값을 보고한다.");
            StringAssert.Contains("macOS", src,
                "'정밀검사'가 두 플랫폼에서 각각 무엇인지 설명이 있어야 한다.");
        }

        // ====================================================================================
        // H. ★★ 2026-09-02 — 원장의 17%가 비어 있던 구멍을 잠근다
        // ====================================================================================
        // 배경: FootholdPoller가 스스로를 "네이티브 창 열거가 일어나는 **유일한** 지점"이라고
        // 단언했지만 거짓이었다. 전체화면 판정(IsFullscreenAppActive)이 창 목록을 **따로** 조회한다.
        //   발판 폴링 0.3초 = 초당 3.33회 / 전체화면 판정 1.5초 = 초당 0.67회
        //   -> 초당 4회 중 **0.67회 = 17%가 계측 밖**이었다.

        /// <summary>ms를 <see cref="System.Diagnostics.Stopwatch"/> 틱으로 — 계측 API가 틱을 받는다.</summary>
        private static long TicksForMs(double ms)
            => (long)(ms * System.Diagnostics.Stopwatch.Frequency / 1000.0);

        /// <summary>
        /// ★★ <b>네거티브 컨트롤.</b> 같은 프레임, 같은 시간인데 <b>장부에 전체화면 경로가 있는지</b>에
        /// 따라 판정이 갈리는 것을 증명한다.
        ///
        /// <para>재현하는 상황: 200ms 블로킹이 전부 전체화면 판정 경로(1.5초 주기)에서 났고, 그 프레임에
        /// 발판 폴링(0.3초 주기)은 마침 돌지 않았다 — 두 주기가 다르므로 <b>실제로 자주 일어난다</b>.</para>
        ///
        /// <list type="bullet">
        ///   <item><b>고치기 전</b>: enumMs에 전체화면 경로가 없다 -> 200ms가 익명 잔차로 흘러
        ///         <c>기타로직(우리 Update 안)</c>. <b>원인이 창 목록 조회인데 원장이 "아니다"라고 말한다.</b></item>
        ///   <item><b>고친 뒤</b>: 같은 200ms가 창열거경로에 잡혀 <c>WindowEnumeration</c>.</item>
        /// </list>
        /// </summary>
        [Test]
        public void 전체화면_판정이_블로킹되면_옛_장부는_기타로직이라고_거짓말한다()
        {
            const float frameMs = 210f;
            const float logicMs = 205f;
            const float fullscreenMs = 200f;
            const float footholdEnumMs = 0f;   // 이 프레임엔 발판 폴링이 돌지 않았다.

            Assert.AreEqual(StallCulprit.OtherLogic,
                StallAttribution.Attribute(frameMs, logicMs, footholdEnumMs, 0f),
                "이게 2026-09-02 이전의 장부다 — 200ms 블로킹 창 목록 조회가 '기타로직'으로 찍혔다. " +
                "이 단언이 깨지면 네거티브 컨트롤이 성립하지 않는다(대조군이 사라진 것).");

            Assert.AreEqual(StallCulprit.WindowEnumeration,
                StallAttribution.Attribute(frameMs, logicMs, footholdEnumMs + fullscreenMs, 0f),
                "전체화면 판정 경로를 창열거경로에 합산하지 않으면 미계측 17%가 그대로 돌아온다.");
        }

        /// <summary>
        /// 위 네거티브 컨트롤의 <b>실행 배선</b>. 소스 문자열이 아니라 <b>장부에 실제로 쌓이는지</b>를 본다.
        /// </summary>
        [Test]
        public void 전체화면_판정_시간이_장부에_실제로_쌓인다()
        {
            Assert.AreEqual(0.0, StallAttribution.CurrentFrameFullscreenProbeMs, 1e-9,
                "ResetForTests가 새 칸을 비우지 않으면 테스트 간에 값이 샌다.");
            Assert.AreEqual(0, StallAttribution.CurrentFrameFullscreenProbeCount);

            StallAttribution.RecordFullscreenProbe(TicksForMs(40.0));
            StallAttribution.RecordFullscreenProbe(TicksForMs(60.0));

            Assert.AreEqual(2, StallAttribution.CurrentFrameFullscreenProbeCount,
                "판정 횟수가 세어지지 않으면 '1.5초 주기가 맞나'를 로그로 확인할 수 없다.");
            Assert.AreEqual(100.0, StallAttribution.CurrentFrameFullscreenProbeMs, 5.0,
                "전체화면 판정 시간이 프레임 장부에 누적되지 않는다 — 계측 창구가 죽어 있다.");

            // 발판 경로와 **섞이지 않아야** 한다. 섞이면 "둘 중 어느 쪽이 느린가"를 다시 못 가른다.
            Assert.AreEqual(0.0, StallAttribution.CurrentFrameFootholdEnumMs, 1e-9);
        }

        /// <summary>OS 왕복 단독 계측이 장부에 쌓이고, 지원하지 않는 CPU 시계(-1)를 0으로 위장하지 않는가.</summary>
        [Test]
        public void OS_창목록_왕복이_따로_장부에_쌓인다()
        {
            StallAttribution.RecordNativeWindowListQuery(TicksForMs(3.0), 1_500_000L); // CPU 1.5ms
            StallAttribution.RecordNativeWindowListQuery(TicksForMs(2.0), -1);          // CPU 미지원

            Assert.AreEqual(2, StallAttribution.CurrentFrameOsListCount);
            Assert.AreEqual(5.0, StallAttribution.CurrentFrameOsListMs, 1.0,
                "OS 왕복만 재는 중첩 타이머가 장부에 쌓이지 않는다 — 'OS가 느린가 우리 후처리가 느린가'를 " +
                "다시 라벨만 보고 추측하게 된다.");

            // 이 값은 발판/전체화면 칸의 **부분집합**이므로 그쪽에 더해지면 안 된다(이중계산).
            Assert.AreEqual(0.0, StallAttribution.CurrentFrameFootholdEnumMs, 1e-9);
            Assert.AreEqual(0.0, StallAttribution.CurrentFrameFullscreenProbeMs, 1e-9);
        }

        /// <summary>
        /// 스파이크 귀인이 <b>두 경로의 합</b>을 쓰는가. 여기가 끊기면 위 네거티브 컨트롤의 "고친 뒤"가
        /// 실기에서 재현되지 않는다(테스트만 초록인 상태).
        /// </summary>
        [Test]
        public void 스파이크_귀인이_전체화면_경로를_창열거에_합산한다()
        {
            string src = ReadScript("Platform", "StallAttribution.cs");
            StringAssert.Contains("float enumMs = (float)(b.EnumMs + b.FullscreenProbeMs);", src,
                "스파이크 귀인이 발판 경로만 보고 있다 — 전체화면 판정의 블로킹이 다시 '기타로직'이 된다.");
        }

        /// <summary>
        /// 전체화면 판정 계측은 <b>플랫폼 중립 데코레이터</b>에 있어야 한다. macOS 파일 안에 넣으면
        /// Windows가 물리적으로 같은 계측을 못 받는다 — <c>FullscreenSuspendPolicy.cs</c>로 이미 겪은
        /// 사고이고, CLAUDE.md가 명시적으로 금지하는 형태다.
        /// </summary>
        [Test]
        public void 전체화면_판정_계측이_플랫폼_중립_한_곳에_배선돼_있다()
        {
            string decorator = ReadScript("Platform", "FallbackPlatformWindowService.cs");
            StringAssert.Contains("StallAttribution.RecordFullscreenProbe(", decorator,
                "데코레이터가 전체화면 판정 시간을 재지 않는다 — Mac/Win 양쪽이 동시에 미계측으로 돌아간다.");

            int start = decorator.IndexOf("long start = System.Diagnostics.Stopwatch.GetTimestamp();",
                System.StringComparison.Ordinal);
            int call = decorator.IndexOf("bool active = _inner.IsFullscreenAppActive();",
                System.StringComparison.Ordinal);
            int record = decorator.IndexOf("StallAttribution.RecordFullscreenProbe(",
                System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "판정 직전 타임스탬프가 없다.");
            Assert.Greater(call, start, "타임스탬프가 판정 호출보다 뒤에 있다 — 0ms만 찍힌다.");
            Assert.Greater(record, call, "기록이 판정 호출을 감싸지 않는다.");

            // 플랫폼 구현체가 각자 재기 시작하면 이중계산 + 한쪽만 고쳐지는 갭이 동시에 생긴다.
            foreach (string plat in new[] { Path.Combine("MacOS", "MacWindowService.cs"),
                                            Path.Combine("Windows", "Win32WindowService.cs") })
            {
                StringAssert.DoesNotContain("RecordFullscreenProbe(", ReadScript("Platform", plat),
                    $"{plat}가 전체화면 판정을 자체 계측한다 — 데코레이터와 이중계산되고, " +
                    "플랫폼마다 복제하면 한쪽만 고쳐지는 그 실패가 반복된다.");
            }
        }

        /// <summary>
        /// macOS의 OS 왕복 중첩 타이머가 <b>모든</b> 창목록 조회 경로에 걸려 있는가.
        /// 원시 <c>CopyOnScreenWindowList()</c>를 직접 부르는 곳이 하나라도 남으면 그 왕복은
        /// 다시 원장에서 사라진다 — 그게 이번 라운드가 고친 결함의 형태 그 자체다.
        /// </summary>
        [Test]
        public void macOS_창목록_왕복은_전부_중첩_타이머를_거친다()
        {
            string mac = ReadScript("Platform", "MacOS", "MacWindowService.cs");
            StringAssert.Contains("StallAttribution.RecordNativeWindowListQuery(", mac,
                "OS 왕복 단독 계측이 없다 — 'OS가 느린가 우리 후처리가 느린가'를 다시 못 가른다.");

            // 주석을 걷어내고 **실행 코드**만 본다(결함을 설명하는 주석 자체가 구현으로 오인되던 함정).
            string exec = StripComments(mac);
            int declared = CountOccurrences(exec, "CopyOnScreenWindowList()");
            int measured = CountOccurrences(exec, "CopyOnScreenWindowListMeasured(");
            // 남아야 하는 원시 호출은 딱 둘: 정의 한 줄 + 계측 래퍼 안의 실제 호출 한 줄.
            Assert.AreEqual(2, declared,
                $"계측을 우회하는 원시 CopyOnScreenWindowList() 호출이 {declared - 2}개 남아 있다 — " +
                "그 왕복은 원장에 잡히지 않는다.");
            Assert.GreaterOrEqual(measured, 4,
                "계측판 호출부(정의 1 + 발판열거/전체화면판정/자기창조회 3)가 모자란다.");

            // ★ 2026-09-02 2차 — **부모 구간이 없는** 세 번째 호출자(TryGetSelfWindowRect ->
            //   DetectDesktopDpiScale / ReportOverlayRectNow)가 부모 있는 칸에 섞이면
            //   프레임 단위로 자식이 부모보다 커져 '우리 후처리'가 음수가 된다
            //   (실측: 창열거경로 12.3ms인데 그중 OS창목록 15.0ms/2회 -> −2.7ms).
            //   그 호출자만 insideEnumerationPass: false여야 한다.
            Assert.AreEqual(1, CountOccurrences(exec, "CopyOnScreenWindowListMeasured(insideEnumerationPass: false)"),
                "부모 없는 창목록 조회(TryGetSelfWindowRect)가 부모 있는 칸에 섞여 있거나 둘 이상으로 늘었다 — " +
                "그러면 '발판열거 + 전체화면판정 − OS창목록 = 우리 후처리' 뺄셈이 음수가 된다.");

            int selfRect = exec.IndexOf("private bool TryGetSelfWindowRect(", System.StringComparison.Ordinal);
            Assert.Greater(selfRect, 0, "TryGetSelfWindowRect가 사라졌다 — 이 검사의 대상이 없다.");
            int orphanCall = exec.IndexOf("CopyOnScreenWindowListMeasured(insideEnumerationPass: false)",
                System.StringComparison.Ordinal);
            Assert.Greater(orphanCall, selfRect,
                "부모 없음 표시가 TryGetSelfWindowRect 밖에 붙어 있다 — 다른 경로가 잘못 표시됐다.");
        }

        /// <summary>
        /// ★★ Windows도 <b>같은 계측기</b>를 거치는가(2026-09-02 — 사용자 상시 지시 "맥에 적용한 사항
        /// 윈도우에도 모두 적용").
        ///
        /// <para>이 배선이 없던 동안 Windows 로그는 <c>★OS창목록 평균 0.00ms, 0회 -> 우리 후처리
        /// &lt;발판열거 전액&gt;</c>으로 찍혔다. <b>이번 라운드가 없애려던 것과 정확히 같은 오도</b>이고,
        /// 하필 Windows가 크로스 프로세스 DWM 때문에 왕복이 더 비싼 플랫폼이다.</para>
        ///
        /// <para>구조가 다르다는 사실 자체는 갭이 아니다(macOS는 왕복 1회, Windows는 콜백 N회) —
        /// 그래서 "같은 이름의 함수가 있는가"가 아니라 <b>같은 원장 창구를 부르는가</b>와
        /// <b>스레드 CPU를 -1로 정직하게 다루는가</b>를 본다.</para>
        /// </summary>
        [Test]
        public void Windows_창열거도_같은_계측기를_거친다()
        {
            string exec = StripComments(ReadScript("Platform", "Windows", "Win32WindowService.cs"));

            StringAssert.Contains("StallAttribution.RecordNativeWindowListQuery(", exec,
                "Windows 창 열거가 원장에 한 번도 잡히지 않는다 — 로그가 'OS창목록 0.00ms/0회'라고 말하고 " +
                "DWM 왕복이 원인일 때조차 '전부 우리 후처리'로 오도한다.");

            // 열거 호출은 반드시 계측판을 거친다. 원시 호출이 남으면 그 왕복이 원장에서 사라진다.
            Assert.AreEqual(2, CountOccurrences(exec, "EnumWindowsMeasured()"),
                "계측판(정의 1 + 호출 1)이 아니다 — 열거는 패스당 1회이고 그 1회가 계측을 거쳐야 한다.");
            Assert.AreEqual(1, CountOccurrences(exec, "EnumWindows(_enumWindowsCallback"),
                "계측을 우회하는 원시 열거 호출이 남아 있다(남아야 하는 것은 계측판 안의 1회뿐).");

            // 스레드 CPU 시간 — macOS는 clock_gettime, Windows는 GetThreadTimes. 없으면 -1로 정직하게.
            StringAssert.Contains("GetThreadTimes", exec,
                "Windows에 스레드 CPU 시계 대응물이 없다 — '벽시계 >> CPU면 블로킹'이라는 판별을 " +
                "Windows에서만 할 수 없게 된다.");
            StringAssert.Contains("ThreadCpuNanoseconds()", exec,
                "CPU 시간을 재는 헬퍼가 없다 — 계측 창구에 -1만 넘기고 있을 가능성이 높다.");
            StringAssert.Contains("_threadCpuClockState = 2", exec,
                "조회 실패를 영구히 끄는 상태 기계가 없다 — macOS판과 같은 규약(-1, 0으로 위장 금지)이어야 한다.");
        }

        /// <summary>
        /// ★ 낡은 주석 재발 방지. "여기가 <b>유일한</b> 지점"이라는 <b>거짓 단언</b>이 조사를 한 라운드
        /// 통째로 잡아먹었다. 같은 문장이 돌아오면 여기서 막는다.
        /// </summary>
        [Test]
        public void 발판폴러가_스스로를_유일한_창열거_지점이라고_주장하지_않는다()
        {
            string poller = ReadScript("Platform", "FootholdPoller.cs");
            StringAssert.DoesNotContain("네이티브 창 열거가 일어나는 **유일한** 지점", poller,
                "거짓 단언이 되살아났다 — 전체화면 판정 경로가 창 목록을 따로 조회한다.");
            StringAssert.DoesNotContain("창 열거는 이 클래스가 **유일한 호출자**", poller,
                "같은 거짓 단언의 다른 문장이 되살아났다.");
            StringAssert.Contains("IsFullscreenAppActive()", poller,
                "다른 조회 경로가 존재한다는 사실이 이 파일에 적혀 있어야 한다 — 다음 사람이 " +
                "같은 함정에 빠지지 않게 하는 것이 이 주석의 유일한 목적이다.");
        }

        /// <summary>
        /// macOS의 창당 문자열 할당 제거(초당 60개 ≈ 250MB/일 gen0 쓰레기)가 되돌아가지 않게 잠근다.
        /// <see cref="StickMate.Platform.MacOS"/>의 <c>IsOwnAppWindow</c>는 <b>레이어 필터보다 먼저</b>,
        /// <b>모든 창</b>에 대해 돈다 — 여기서 <c>string</c>을 만들면 24시간 상주 앱에서 그대로 누적된다.
        /// </summary>
        [Test]
        public void macOS_자기앱_판정은_창마다_문자열을_만들지_않는다()
        {
            string exec = StripComments(ReadScript("Platform", "MacOS", "MacWindowService.cs"));

            int body = exec.IndexOf("private bool IsOwnAppWindow(", System.StringComparison.Ordinal);
            Assert.Greater(body, 0, "IsOwnAppWindow가 사라졌다 — 이 테스트의 대상이 없다.");
            int end = exec.IndexOf('}', exec.IndexOf('{', body));
            string fn = exec.Substring(body, end - body);
            StringAssert.DoesNotContain("TryGetString(", fn,
                "IsOwnAppWindow가 다시 창마다 string을 만든다 — 18창 x 3.33패스/초 = 초당 60개다. " +
                "UTF-8 바이트 비교(OwnerNameEqualsCurrentProcess)로 돌려놓으세요.");

            StringAssert.Contains("_currentProcessNameUtf8", exec,
                "프로세스 이름 UTF-8 1회 캐시가 사라졌다.");
            StringAssert.Contains("_ownerNameBuffer[want] == 0", exec,
                "NUL 종단 확인이 빠지면 우리 이름을 접두사로 갖는 남의 앱까지 '우리 앱'으로 통과한다.");
        }

        /// <summary>테스트가 시간을 쓰게 만드는 최소 바쁜 대기(ms). Thread.Sleep은 정밀도가 낮아
        /// 3ms와 12ms를 구분하지 못하는 환경이 있어 쓰지 않는다.</summary>
        private static void Busy(double milliseconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalMilliseconds < milliseconds) { }
        }

    }
}
