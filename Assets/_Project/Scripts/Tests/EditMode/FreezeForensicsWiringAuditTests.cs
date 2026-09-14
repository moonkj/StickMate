using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-14 — 동결 계측 배선과 D1·D2 수정의 <b>양 플랫폼 소스 감사</b>.
    ///
    /// <para><b>왜 소스 스캔인가.</b> <c>WindowsOverlayStateEnforcer.cs</c>는 파일 전체가
    /// <c>#if UNITY_STANDALONE_WIN</c> 안이라 macOS 타깃에서는 타입이 존재하지 않는다(CLAUDE.md
    /// 「활성 빌드 타깃 규칙」). 리플렉션으로는 반쪽만 본다. 두 Enforcer를 <b>같은 검사</b>에 넣어
    /// 한쪽만 배선되는 사고(플랫폼 동시 검토 위반)를 막는다.</para>
    ///
    /// <para><b>니들 규칙(CLAUDE.md).</b> 우리 공개 API는 <c>nameof</c>로 만든다 — 이름이 바뀌면 컴파일이 깨진다.
    /// 불가피한 private 식별자 니들은 <b>존재를 먼저 단언</b>해 썩으면 시끄럽게 빨개지게 한다.
    /// 부재 단언은 같은 니들이 <b>파일 다른 곳에는 실재함</b>을 대조로 붙인다. 주석은 벗기고 본다
    /// (주석 속 언급은 배선이 아니다).</para>
    /// </summary>
    public sealed class FreezeForensicsWiringAuditTests
    {
        private static string PlatformRoot => Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string EnforcerPath(string platform) => platform == "Windows"
            ? Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs")
            : Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs");

        /// <summary>
        /// ★ 4차(verify-change 3차 V5) — 주석·문자열·보간 구멍을 전부 공백으로 지운 <b>코드만</b> 돌려준다(<see cref="SourceTextScanner"/>).
        /// 옛 판은 <c>//</c> 주석만 벗겨 문자열·보간을 남겼고, 같은 파일의 로그 문자열이 배선 니들을 채울 수 있었다(V5가 다른 파일에서 그렇게 살아남았다).
        /// 문자열 내용 자체를 봐야 하는 단언은 <see cref="BodyLiterals"/>로 따로 본다. 줄 구조·위치는 원문과 같다.
        /// </summary>
        private static string ReadCode(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다({path}) — 옮겨졌다면 이 감사도 함께 갱신하세요.");
            return SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(path).Replace("\r\n", "\n"), null, blankInterpolationHoles: true);
        }

        /// <summary>시그니처로 시작하는 멤버 본문 안의 평문 문자열 리터럴들(지운 코드와 원문의 위치가 같다는 성질을 쓴다).</summary>
        private static System.Collections.Generic.List<string> BodyLiterals(string path, string signature)
        {
            string raw = File.ReadAllText(path).Replace("\r\n", "\n");
            string code = SourceTextScanner.BlankCommentsAndStrings(raw, null, blankInterpolationHoles: true);
            string body = Body(code, signature);
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            var literals = new System.Collections.Generic.List<string>();
            SourceTextScanner.BlankCommentsAndStrings(raw.Substring(start, body.Length), literals);
            return literals;
        }

        /// <summary>시그니처부터 다음 멤버 선언(8칸 들여쓰기) 직전까지. 못 찾으면 실패한다(썩은 니들은 빨갛게).</summary>
        private static string Body(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"'{signature}'를 찾지 못했습니다 — 이름이 바뀌었다면 감사를 갱신하세요.");
            int end = code.Length;
            foreach (string boundary in new[] { "\n        private ", "\n        internal ", "\n        public " })
            {
                int at = code.IndexOf(boundary, start + signature.Length, StringComparison.Ordinal);
                if (at >= 0 && at < end) end = at;
            }
            return code.Substring(start, end - start);
        }

        private static int Count(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static readonly string ObserveNeedle =
            nameof(FreezeForensics) + "." + nameof(FreezeForensics.ObserveTopologyTransition) + "(";

        private static string EventNeedle(FreezeForensicsEvent e) => nameof(FreezeForensicsEvent) + "." + e;

        // ------------------------------------------------------------------ 계측 배선

        [TestCase("Windows")]
        [TestCase("macOS")]
        public void 토폴로지_관측은_재무장보다_먼저_원장에_전해진다(string platform)
        {
            string body = Body(ReadCode(EnforcerPath(platform)), "private void TickDisplayTopology()");
            int observe = body.IndexOf(ObserveNeedle, StringComparison.Ordinal);
            int rearm = body.IndexOf("_fullScreenBoundsApplied = false;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(observe, 0, $"{platform}: 토폴로지 관측이 원장에 전해지지 않는다(t0를 못 찍는다).");
            Assert.GreaterOrEqual(rearm, 0, $"{platform}: 재무장 줄 니들이 썩었다 — 감사를 갱신하세요.");
            Assert.Less(observe, rearm, $"{platform}: 원장 기록은 재무장보다 앞이어야 한다.");
            CollectionAssert.Contains(BodyLiterals(EnforcerPath(platform), "private void TickDisplayTopology()"), platform,
                $"{platform}: 원장 줄의 플랫폼 꼬리표가 다르다.");
        }

        [TestCase("Windows", FreezeForensicsEvent.SetResolution, "Screen.SetResolution(")]
        [TestCase("Windows", FreezeForensicsEvent.WindowResize, ".windowSize = monitor.size")]
        [TestCase("Windows", FreezeForensicsEvent.WindowMove, ".windowPosition = monitor.position")]
        [TestCase("macOS", FreezeForensicsEvent.SetResolution, "Screen.SetResolution(")]
        [TestCase("macOS", FreezeForensicsEvent.WindowResize, ".windowSize = monitor.size")]
        [TestCase("macOS", FreezeForensicsEvent.WindowMove, ".windowPosition = monitor.position")]
        public void 표면을_건드리는_호출_직전에_원장_기록이_있다(string platform, FreezeForensicsEvent kind, string call)
        {
            string body = Body(ReadCode(EnforcerPath(platform)), "private void TickFullScreenBounds()");
            int calls = Count(body, call);
            int records = Count(body, EventNeedle(kind));
            Assert.AreEqual(1, calls, $"{platform}: '{call}' 호출이 {calls}개 — 새 호출이 생겼다면 원장 기록도 붙이세요(0이면 니들이 썩었다).");
            Assert.AreEqual(1, records, $"{platform}: {kind} 원장 기록이 {records}개.");
            Assert.Less(body.IndexOf(EventNeedle(kind), StringComparison.Ordinal),
                body.IndexOf(call, StringComparison.Ordinal),
                $"{platform}: {kind} 기록이 호출 <b>뒤</b>에 있다 — 호출 중에 멈추면 줄이 안 남는다.");
        }

        [TestCase("Windows")]
        [TestCase("macOS")]
        public void 투명_재대입_직전에_원장_창구가_있다(string platform)
        {
            string body = Body(ReadCode(EnforcerPath(platform)), "private void Update()");
            const string assign = "isTransparent = DesiredTransparent";
            string record = nameof(FreezeForensics) + "." + nameof(FreezeForensics.RecordTransparencyReassign) + "(";
            Assert.AreEqual(1, Count(body, assign), $"{platform}: 투명 재대입 니들이 1개가 아니다.");
            int r = body.IndexOf(record, StringComparison.Ordinal);
            Assert.GreaterOrEqual(r, 0, $"{platform}: 투명 재대입 원장 창구가 없다.");
            Assert.Less(r, body.IndexOf(assign, StringComparison.Ordinal), $"{platform}: 창구가 대입 뒤에 있다.");
        }

        [Test]
        public void 프레임_경계_탐침이_워치독에_프레임과_로직밖_단계를_발행한다()
        {
            string code = ReadCode(Path.Combine(PlatformRoot, "StallAttributionProbe.cs"));
            StringAssert.Contains(nameof(FreezeWatchdog) + "." + nameof(FreezeWatchdog.PublishMainFrame) + "(", code,
                "프레임 발행이 없으면 워치독은 영원히 무장하지 않는다(= 조용히 죽은 계측).");
            StringAssert.Contains(nameof(MainThreadPhase) + "." + nameof(MainThreadPhase.AfterLogic), code,
                "'로직 밖' 단계가 없으면 정지가 우리 C#인지 렌더·프레젠트인지 못 가른다.");
        }

        [Test]
        public void 기동은_두_데스크톱_플랫폼을_같은_규칙으로_켠다()
        {
            string code = ReadCode(Path.Combine(PlatformRoot, "FreezeWatchdog.cs"));
            StringAssert.Contains(nameof(RuntimePlatform) + "." + nameof(RuntimePlatform.WindowsPlayer), code);
            StringAssert.Contains(nameof(RuntimePlatform) + "." + nameof(RuntimePlatform.OSXPlayer), code);
            StringAssert.Contains(nameof(FreezeForensicsPolicy) + "." + nameof(FreezeForensicsPolicy.ShouldActivate) + "(", code);
        }

        // ------------------------------------------------------------------ D1 · D2

        [TestCase("Windows")]
        [TestCase("macOS")]
        public void D1_OS_모니터_목록_갱신은_벽시계_문을_쓴다(string platform)
        {
            string code = ReadCode(EnforcerPath(platform));
            string body = Body(code, "private bool TryResolveChosenMonitorRect(");
            StringAssert.Contains("." + nameof(WallClockIntervalGate.TryConsume) + "(Time.unscaledTime", body,
                $"{platform}: 갱신 주기가 벽시계 문을 거치지 않는다.");
            StringAssert.Contains(nameof(WallClockIntervalGate) + " ", code, $"{platform}: 문 필드 선언이 없다.");

            const string accumulate = "+= Time.unscaledDeltaTime";
            StringAssert.Contains(accumulate, code, $"{platform}: 대조 니들이 파일 어디에도 없다 — 부재 단언이 무의미해졌다.");
            StringAssert.DoesNotContain(accumulate, body,
                $"{platform}: 모니터 선택 함수 안에 누적 타이머가 되살아났다 — 게이트 뒤에서 불리므로 1초가 15~30초가 된다(D1).");
        }

        [TestCase("Windows")]
        [TestCase("macOS")]
        public void D2_크기_0_검사는_목표_재무장보다_앞선다(string platform)
        {
            string body = Body(ReadCode(EnforcerPath(platform)), "private bool TryResolveChosenMonitorRect(");
            int assign = body.IndexOf("monitor = _libraryRects[libIndex];", StringComparison.Ordinal);
            Assert.GreaterOrEqual(assign, 0, $"{platform}: 목표 사각형 대입 니들이 썩었다.");
            int guard = body.IndexOf("if (monitor.width <= 0f || monitor.height <= 0f) return false;", assign, StringComparison.Ordinal);
            int rearm = body.IndexOf("ReArmFullScreenFitForNewTarget();", assign, StringComparison.Ordinal);
            Assert.GreaterOrEqual(guard, 0, $"{platform}: 대입 뒤 크기 0 검사가 없다.");
            Assert.GreaterOrEqual(rearm, 0, $"{platform}: 재무장 호출 니들이 썩었다.");
            Assert.Less(guard, rearm, $"{platform}: 크기 0인 목표로 재적합 루프를 무장한다(D2).");
        }

        [TestCase("Windows")]
        [TestCase("macOS")]
        public void 표시모니터_로그_중복억제는_중립_규칙을_쓴다(string platform)
        {
            string body = Body(ReadCode(EnforcerPath(platform)), "private void LogChoiceOnce(");
            StringAssert.Contains(nameof(OverlayMonitorChoicePolicy) + "." + nameof(OverlayMonitorChoicePolicy.ShouldLogChoiceChange) + "(", body,
                $"{platform}: 중복 억제가 중립 규칙을 거치지 않는다 — 사유가 붙으면 0.25초마다 같은 줄을 다시 찍던 결함.");
        }

        [Test]
        public void 감사가_읽는_Enforcer가_실제로_토폴로지_감시기를_가진_파일이다()
        {
            // 양성 대조 — 엉뚱한 파일을 읽고 초록이 되는 것을 막는다. (테스트 어셈블리는 UniWindowController
            // 패키지를 참조하지 않으므로 중립 타입 이름으로 대조한다.)
            foreach (string platform in new[] { "Windows", "macOS" })
            {
                StringAssert.Contains(nameof(DisplayTopologyWatcher) + " _topologyWatcher", ReadCode(EnforcerPath(platform)), platform);
            }
        }
    }
}
