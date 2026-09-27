using NUnit.Framework;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 커서 조회 <b>원인 코드</b>와 <b>엣지 로그 상한</b>의 계약 잠금(2026-09-27).
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>0이 성공이 아니다.</b> 열거형 기본값이 <c>Ok</c>가 되는 순간, 값을 채우지 않은
    ///     필드가 「커서를 읽었다」고 주장한다. 그 사고는 화면에 안 보인다.</item>
    ///   <item><b>상한이 실제로 무는가.</b> 상태 보유자가 값 타입이면 <c>readonly</c> 필드에 담긴
    ///     순간 호출마다 복사본에 써서 상한이 매번 리셋되는데, <b>컴파일 경고도 테스트 실패도
    ///     없다</b>. 그래서 「여러 번 불러도 한 줄」을 실행으로 잠근다.</item>
    ///   <item><b>접힌 것이 사라지지 않는가.</b> 로그를 줄이는 것이 「눈을 감는 것」이 되면 안 된다.</item>
    /// </list>
    ///
    /// <para>상한값을 <b>숫자로 베끼지 않고</b> <see cref="CursorReadFailureEdgeLog.MaxLinesPerSite"/>를
    /// 참조한다(CLAUDE.md — 테스트에 프로덕션 상수를 하드코딩하지 않는다). 상한을 올리는 라운드에서
    /// 이 단언들이 조용히 통과하지 않게 하려는 것이다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 순수 C#만 부르고 씬·에셋·파일을 건드리지 않는다.</para>
    /// </summary>
    public sealed class CursorReadFailureEdgeLogTests
    {
        private const string Tag = "[커서원인]";

        // ====================================================================
        // (1) 0이 성공이 아니다
        // ====================================================================

        [Test]
        public void 원인코드_기본값은_성공이_아니다()
        {
            Assert.AreNotEqual(CursorReadOutcome.Ok, default(CursorReadOutcome),
                $"{Tag} 열거형 기본값(0)이 «읽었다»가 되면, 값을 한 번도 채우지 않은 자리가 성공을 " +
                "주장합니다 — 조용히 틀리는 방향입니다.");

            // 양성 대조 — 이 단언이 «아무 값이나 다르다»로 통과하는 것이 아님을 같은 자리에서 보인다.
            Assert.AreEqual(CursorReadOutcome.NoProvider, default(CursorReadOutcome),
                $"{Tag} 기본값은 «제공자 배선 없음»이어야 합니다(그것이 배선 전의 실제 사실입니다).");
        }

        [Test]
        public void 성공은_한_줄도_내지_않는다()
        {
            var log = new CursorReadFailureEdgeLog();

            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(log.ShouldEmit(CursorReadOutcome.Ok),
                    $"{Tag} 성공 관측이 로그를 냈습니다(i={i}) — 정상 동작이 로그를 만들면 상주 앱에서 " +
                    "그것이 곧 파일 쓰기입니다.");
            }

            Assert.AreEqual(0, log.EmittedLines, $"{Tag} 성공만 넘겼는데 줄이 나갔습니다.");
            Assert.AreEqual(0, log.SuppressedFailures, $"{Tag} 성공은 «접힌 실패»로도 세지 않아야 합니다.");
        }

        // ====================================================================
        // (2) 상한이 실제로 문다 — 값 타입 함정의 실행 잠금
        // ====================================================================

        [Test]
        public void 같은_원인이_반복되면_첫_줄만_나가고_나머지는_접힌다()
        {
            var log = new CursorReadFailureEdgeLog();

            // 양성 대조: 첫 실패는 반드시 나간다(이것이 false면 아래 «한 줄» 단언은 공허하다).
            Assert.IsTrue(log.ShouldEmit(CursorReadOutcome.ProviderDeclined),
                $"{Tag} 첫 실패가 로그를 내지 않았습니다 — 이 장치는 아무것도 잡을 수 없습니다.");

            const int repeats = 20;
            for (int i = 0; i < repeats; i++)
            {
                Assert.IsFalse(log.ShouldEmit(CursorReadOutcome.ProviderDeclined),
                    $"{Tag} 같은 원인이 반복되는데 또 찍었습니다(i={i}) — 하룻밤이면 수천 줄입니다.");
            }

            Assert.AreEqual(CursorReadFailureEdgeLog.MaxLinesPerSite, log.EmittedLines,
                $"{Tag} 자리당 상한을 넘겼습니다.");
            Assert.AreEqual(repeats, log.SuppressedFailures,
                $"{Tag} 접힌 실패 수가 관측 수와 다릅니다 — 줄을 줄이면서 «세는 능력»을 잃었습니다.");
            Assert.IsTrue(log.ReachedCap, $"{Tag} 상한에 도달했는데 ReachedCap이 거짓입니다.");
        }

        [Test]
        public void 원인이_바뀌어도_그리고_실패와_성공을_왕복해도_상한을_넘지_않는다()
        {
            var log = new CursorReadFailureEdgeLog();

            var causes = new[]
            {
                CursorReadOutcome.NoProvider,
                CursorReadOutcome.ProviderDeclined,
                CursorReadOutcome.NoCamera,
                CursorReadOutcome.NoBody,
            };

            int emitted = 0;
            foreach (CursorReadOutcome cause in causes)
            {
                if (log.ShouldEmit(cause)) emitted++;
                if (log.ShouldEmit(CursorReadOutcome.Ok)) emitted++;   // 성공으로 되돌아갔다 = 엣지 재무장
                if (log.ShouldEmit(cause)) emitted++;
            }

            Assert.AreEqual(CursorReadFailureEdgeLog.MaxLinesPerSite, emitted,
                $"{Tag} 원인이 바뀌고 성공을 왕복하는 동안 상한을 넘겨 찍었습니다 — 상태가 저장되지 " +
                "않는 형태(값 타입 복사본)에서 정확히 이렇게 됩니다.");
            Assert.AreEqual(CursorReadFailureEdgeLog.MaxLinesPerSite, log.EmittedLines,
                $"{Tag} 자기 계수와 호출자 계수가 다릅니다.");
        }

        [Test]
        public void 상한을_readonly_필드에_담아도_닫힌다()
        {
            // 값 타입이었다면 이 형태에서 «매 호출 새 복사본»이 되어 상한이 무의미해진다.
            // (Platform/WallClockIntervalGate가 같은 이유로 struct를 버렸고, 그쪽 테스트와 같은 어법이다.)
            var holder = new Holder();

            Assert.IsTrue(holder.Note(CursorReadOutcome.NoProvider),
                $"{Tag} readonly 필드에 담은 첫 실패가 로그를 내지 않았습니다(양성 대조 실패).");
            for (int i = 0; i < 10; i++)
            {
                Assert.IsFalse(holder.Note(CursorReadOutcome.NoProvider),
                    $"{Tag} readonly 필드에 담으니 상한이 매 호출 열렸습니다(i={i}).");
            }

            Assert.AreEqual(CursorReadFailureEdgeLog.MaxLinesPerSite, holder.Emitted,
                $"{Tag} readonly 필드 경로에서 상한이 지켜지지 않았습니다.");
        }

        private sealed class Holder
        {
            private readonly CursorReadFailureEdgeLog _log = new CursorReadFailureEdgeLog();

            public int Emitted => _log.EmittedLines;

            public bool Note(CursorReadOutcome outcome) => _log.ShouldEmit(outcome);
        }
    }
}
