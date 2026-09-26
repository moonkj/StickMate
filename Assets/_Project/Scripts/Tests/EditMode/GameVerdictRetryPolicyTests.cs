using System.Globalization;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// FC-1 — 게임 판정 조회 실패의 재시도 · 후퇴 규칙 (N-23, 2026-09-26 · dev-platform)
    /// ============================================================================
    /// 설계 정본은 <c>docs/platform/GAME_DETECTION_FAILURE_CACHE.md</c>의 「4-1. ★ FC-1 확장 설계」다.
    /// 착지 전까지 이 갈래를 잠그는 테스트는 <b>0건</b>이었다(그 문서의 「6. 테스트 존재 여부」 —
    /// 실패 경로·캐시 기호를 참조하는 테스트 줄 0, 음성 대조 포함). 그래서 해제 조건에 들어간다.
    ///
    /// <para><b>여기서 재는 것은 순수 규칙뿐이다.</b> 실제 레지스트리·프로세스 조회는 하지 않는다 —
    /// 조회 계층(<c>Platform/Windows/WindowsGameProcessProbe.cs</c>)은 파일 전체가
    /// <c>#if UNITY_STANDALONE_WIN</c> 안이라 이 머신에서는 타입이 존재하지 않는다. 그 배선은
    /// <see cref="GameVerdictFailureCacheAuditTests"/>가 소스 텍스트로 잰다.</para>
    ///
    /// <para><b>시간은 인자로 넣는다.</b> 이 규칙은 시각을 스스로 읽지 않으므로 대기가 없다 —
    /// 벽시계 예산도, 프레임 수 대기도 필요하지 않다(그 둘이 이 저장소에서 반복해 오판을 냈다).
    /// 대신 「한 폴링 뒤」를 <b>아주 작은 증분</b>으로 흉내 내서, 시간 비교로 구현했다면
    /// 통과할 수 없게 만든다.</para>
    /// </summary>
    public class GameVerdictRetryPolicyTests
    {
        private const string LogPrefix = "[게임판정재시도]";

        /// <summary>출하 기본 폴링 간격(초). 숫자를 베끼지 않고 프로덕션 기본값에서 읽는다.</summary>
        private static double DefaultPollIntervalSeconds()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                double interval = config.fullscreenPollInterval;
                Assert.Greater(interval, 0.0,
                    $"{LogPrefix} 폴링 간격 기본값이 0 이하입니다 — 아래 계수 시나리오가 성립하지 않습니다.");
                return interval;
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // ====================================================================
        // 0. 전제 — 시나리오가 성립하는가(비공허성)
        // ====================================================================

        /// <summary>
        /// ★ <see cref="DefaultPollIntervalSeconds"/>는 <b>코드 기본값</b>을 읽는다. 출하 애셋이 그 값을
        /// 덮으면 아래 비용 계수는 실제와 다른 세계를 재게 된다 — CLAUDE.md 거짓 통과 9번이 정확히
        /// 「애셋이 코드 기본값을 덮는데 애셋을 안 고쳐 스위치가 꺼진 채 출하될 뻔」이었다
        /// (test-engineer GAP-9, 2026-09-26).
        /// </summary>
        [Test]
        public void 전제_출하_애셋의_폴링_간격이_코드_기본값과_같다()
        {
            string assetPath = Path.Combine(Application.dataPath, "_Project", "Data",
                "DefaultStickConfig.asset");
            Assert.IsTrue(File.Exists(assetPath),
                $"{LogPrefix} 출하 설정 애셋을 찾지 못했습니다({assetPath}) — 이 대조 없이는 아래 계수가 " +
                "«코드 기본값 세계»의 값일 뿐입니다.");

            const string key = "fullscreenPollInterval:";
            string text = File.ReadAllText(assetPath);
            int at = text.IndexOf(key, System.StringComparison.Ordinal);
            Assert.Greater(at, 0, $"{LogPrefix} 애셋에서 '{key}'를 찾지 못했습니다 — 필드 이름이 바뀌었다면 " +
                "이 대조도 함께 갱신하세요(그대로 두면 조용히 통과합니다).");

            int lineEnd = text.IndexOf('\n', at);
            string raw = (lineEnd < 0 ? text.Substring(at) : text.Substring(at, lineEnd - at))
                .Substring(key.Length).Trim();
            Assert.IsTrue(double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double assetInterval),
                $"{LogPrefix} 애셋의 폴링 간격 «{raw}»를 숫자로 읽지 못했습니다.");

            Assert.AreEqual(DefaultPollIntervalSeconds(), assetInterval,
                $"{LogPrefix} 코드 기본값({DefaultPollIntervalSeconds()})과 출하 애셋({assetInterval})의 " +
                "폴링 간격이 다릅니다 — 아래 비용 계수는 코드 기본값 세계의 값이므로 실제 출하 동작과 " +
                "어긋납니다. 둘 중 하나만 고치는 것이 이 저장소의 거짓 통과 9번입니다.");
        }

        [Test]
        public void 전제_후퇴_문턱과_창이_폴링_간격보다_크다()
        {
            double interval = DefaultPollIntervalSeconds();

            Assert.GreaterOrEqual(GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold, 2,
                $"{LogPrefix} 후퇴 문턱이 1이면 «다음 폴링 1회 재시도»가 아예 없습니다 — " +
                "일시 실패 흡수라는 이 설계의 목적이 사라집니다.");
            Assert.Greater(GameVerdictRetryPolicy.BackoffWindowSeconds, interval,
                $"{LogPrefix} 후퇴 창이 폴링 간격보다 짧으면 후퇴가 아무것도 막지 않습니다.");
        }

        // ====================================================================
        // 1. 확정 판정만 캐시한다 — N-23의 본체
        // ====================================================================

        [Test]
        public void 조회_실패에서_나온_판정은_캐시하지_않는다()
        {
            // 경로를 못 읽었으면 목록이 완전해도 캐시 금지 — 그 false가 30초 굳으면
            // 감지된 게임 위에서 캐릭터가 다시 나온다(원칙 2 조건부 위반).
            Assert.IsFalse(GameVerdictRetryPolicy.ShouldCacheVerdict(false, GameListReadOutcome.Success),
                $"{LogPrefix} 경로 조회 실패에서 나온 판정을 캐시합니다 — 실패가 30초 굳습니다.");

            // 재시도 대상 목록 실패도 캐시 금지 — 판정 캐시만 고치면 목록 캐시가 30초를 붙잡는다.
            Assert.IsFalse(GameVerdictRetryPolicy.ShouldCacheVerdict(true, GameListReadOutcome.Retryable),
                $"{LogPrefix} 재시도 대상 목록 실패에서 나온 판정을 캐시합니다.");

            // 확정 = 경로를 읽었고 목록 상태가 「유지」 계열.
            Assert.IsTrue(GameVerdictRetryPolicy.ShouldCacheVerdict(true, GameListReadOutcome.Success),
                $"{LogPrefix} 확정 판정을 캐시하지 않으면 폴링마다 레지스트리를 다시 훑습니다(24시간 상주 앱).");
            Assert.IsTrue(GameVerdictRetryPolicy.ShouldCacheVerdict(true, GameListReadOutcome.KeyMissing),
                $"{LogPrefix} 게임 바를 쓰지 않는 계정에서 폴링마다 키를 다시 엽니다 — " +
                "키가 없다는 사실은 다시 물어도 같습니다(설계: 30초 유지).");
            Assert.IsTrue(GameVerdictRetryPolicy.ShouldCacheVerdict(true, GameListReadOutcome.SubkeyLimitReached),
                $"{LogPrefix} 하위 키 상한은 결정적이라 재시도 대상이 아닙니다(30초 유지).");
        }

        [Test]
        public void 유지_계열과_재시도_대상이_갈린다()
        {
            Assert.IsTrue(GameVerdictRetryPolicy.HoldsWithoutRetry(GameListReadOutcome.Success));
            Assert.IsTrue(GameVerdictRetryPolicy.HoldsWithoutRetry(GameListReadOutcome.KeyMissing));
            Assert.IsTrue(GameVerdictRetryPolicy.HoldsWithoutRetry(GameListReadOutcome.SubkeyLimitReached));
            Assert.IsFalse(GameVerdictRetryPolicy.HoldsWithoutRetry(GameListReadOutcome.Retryable),
                $"{LogPrefix} 재시도 대상을 «유지»로 보면 그 실패가 30초 묶입니다.");

            // 기본값이 재시도 대상이어야 «한 번도 읽지 않은 상태»에서 첫 폴링이 반드시 조회한다.
            Assert.AreEqual(GameListReadOutcome.Retryable, default(GameListReadOutcome),
                $"{LogPrefix} 열거 기본값이 「유지」 계열이면 첫 폴링이 조회를 건너뛰고 빈 목록으로 " +
                "판정해, 게임을 30초 동안 놓칩니다.");
        }

        /// <summary>
        /// 결과는 <b>두 갈래</b>, 입력 경우는 셋이다. 예전 판은 「성공 코드 + 핸들 0」을 별도 분기로
        /// 뒀는데 다음 줄이 같은 값을 돌려줘서 그 인자가 결과를 한 번도 바꾸지 않았다(죽은 분기 —
        /// 지워도 이 테스트가 초록이었다). 지금은 그 경우도 「rc가 2가 아니다」로 자연히 재시도 대상이
        /// 되므로, 세 입력이 각각 무엇으로 떨어지는지를 그대로 잠근다.
        /// </summary>
        [Test]
        public void 키_열기_결과가_두_갈래로_분류된다()
        {
            Assert.AreEqual(GameListReadOutcome.KeyMissing,
                GameVerdictRetryPolicy.ClassifyKeyOpenResult(
                    GameVerdictRetryPolicy.RegistryKeyMissingResultCode),
                $"{LogPrefix} 「키가 없다」(게임 바 미사용 계정)를 재시도 대상으로 보면 폴링마다 " +
                "키를 다시 엽니다 — 결과는 매번 같습니다.");

            Assert.AreEqual(GameListReadOutcome.Retryable,
                GameVerdictRetryPolicy.ClassifyKeyOpenResult(
                    GameVerdictRetryPolicy.RegistryOpenSuccessResultCode),
                $"{LogPrefix} 성공 코드(핸들이 0인 채 돌아오는 그 조합)를 「키 없음」으로 묶으면, " +
                "문서에 없는 그 조합에서 30초 동안 목록이 빈 채로 굳습니다. 근거가 없으므로 다시 " +
                "물어야 합니다.");

            Assert.AreEqual(GameListReadOutcome.Retryable,
                GameVerdictRetryPolicy.ClassifyKeyOpenResult(5),
                $"{LogPrefix} 키 없음 이외의 오류는 재시도 대상입니다(접근 거부 등).");

            Assert.AreNotEqual(GameVerdictRetryPolicy.RegistryKeyMissingResultCode,
                GameVerdictRetryPolicy.RegistryOpenSuccessResultCode,
                $"{LogPrefix} 성공 코드와 「키 없음」 코드가 같아졌습니다 — 위 두 단언이 같은 입력을 " +
                "두 번 재는 셈이 되어 분류가 공허해집니다.");
        }

        // ====================================================================
        // 7. 예외 경고 접기 — 설계 4-1 항목 4
        // ====================================================================

        /// <summary>
        /// 프로브는 예외 경고를 <b>연속 실패 구간당 1줄</b>로 묶는다(24시간 상주 앱). 그 묶음 장치는
        /// 저장소에 이미 있는 <see cref="RepeatedLogFolder"/>이고, 여기서는 <b>프로브가 쓰는 방식</b>
        /// (키 = 이유 · 주기 요약 없음)이 그 약속을 실제로 지키는지 잰다. 배선 자체는
        /// <see cref="GameVerdictFailureCacheAuditTests"/>가 소스로 본다.
        ///
        /// <para>횟수는 숫자로 베끼지 않고 후퇴 문턱 상수를 쓴다 — 그만큼 연속 실패해야 후퇴가
        /// 걸리므로, 「한 구간」의 길이가 곧 그 상수다.</para>
        /// </summary>
        [Test]
        public void 예외_경고는_같은_이유가_이어지면_한_줄로_접히고_이유가_바뀌면_새_줄이_난다()
        {
            const double noPeriodicSummary = 0.0;      // 프로브가 쓰는 값: 구간당 정확히 1줄
            int runLength = GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold;
            var folder = new RepeatedLogFolder();

            int emitted = 0;
            for (int i = 0; i < runLength; i++)
            {
                if (folder.ShouldEmit("InvalidOperationException", 1234, 0f, i * 1.5, noPeriodicSummary,
                        out int foldedDuringRun))
                {
                    emitted++;
                }
                Assert.AreEqual(0, foldedDuringRun,
                    $"{LogPrefix} 같은 이유가 이어지는 동안 중간 요약이 났습니다 — 주기 요약 없이 " +
                    "구간당 1줄이어야 합니다.");
            }

            Assert.AreEqual(1, emitted,
                $"{LogPrefix} 연속 {runLength}회 같은 이유 실패에 경고가 {emitted}줄 났습니다(기대 1줄). " +
                "지속 예외 계정에서 1시간 로그가 후퇴 없이 폴링 수만큼 쌓입니다.");
            Assert.AreEqual(runLength - 1, folder.PendingRepeats,
                $"{LogPrefix} 접힌 횟수가 보존되지 않았습니다 — 접기가 «횟수를 잃는» 감량이 되면 " +
                "그건 감량이 아니라 실명입니다.");

            // 이유가 바뀌면 새 줄 + 접힌 횟수 방출(묶음이 진단을 죽이지 않는다).
            Assert.IsTrue(folder.ShouldEmit("Win32Exception", 1234, 0f, runLength * 1.5, noPeriodicSummary,
                    out int foldedOnReasonChange),
                $"{LogPrefix} 예외 형이 바뀌었는데 새 줄이 나가지 않았습니다 — 「같은 이유로 계속 " +
                "실패」와 「이유가 바뀜」이 로그에서 구별되지 않습니다.");
            Assert.AreEqual(runLength - 1, foldedOnReasonChange,
                $"{LogPrefix} 이유가 바뀔 때 접혀 있던 횟수를 내보내지 않았습니다.");

            // 대상(pid)이 바뀌어도 새 줄이어야 한다 — 다른 프로세스의 실패는 다른 사실이다.
            Assert.IsTrue(folder.ShouldEmit("Win32Exception", 5678, 0f, (runLength + 1) * 1.5,
                    noPeriodicSummary, out int _),
                $"{LogPrefix} pid가 바뀌었는데 같은 줄로 접혔습니다 — 서로 다른 프로세스의 실패가 " +
                "한 줄로 뭉개집니다.");
        }

        // ====================================================================
        // 2. 재시도는 시간 비교 없이 「다음 폴링」이다
        // ====================================================================

        [Test]
        public void 실패_직후_다음_폴링에_시간_비교_없이_다시_묻는다()
        {
            var state = new GameVerdictRetryState();
            Assert.IsTrue(state.TryBeginAttempt(0.0), $"{LogPrefix} 첫 조회가 막혔습니다.");
            state.NoteFailure(0.0);

            // ★ 이 단언이 「시간 비교 재시도」를 구조적으로 기각한다: 간격이 얼마든 다음 폴링은 열린다.
            Assert.IsTrue(state.TryBeginAttempt(1e-9),
                $"{LogPrefix} 실패 뒤 다음 폴링이 막혔습니다 — 재시도를 시각 비교로 구현하면 폴링 " +
                "간격이 1.5초에 붙는 격자에서 재시도가 한 폴링 밀리고, 두 번째 실패 관측이 " +
                "디바운서를 확정시켜 노출 3.0초가 납니다(설계 정본이 기각한 형태).");
        }

        [Test]
        public void 성공하면_계수가_0으로_돌아간다()
        {
            var state = new GameVerdictRetryState();
            state.NoteFailure(0.0);
            state.NoteFailure(1.0);
            Assert.AreEqual(2, state.ConsecutiveFailures);

            state.NoteSuccess();
            Assert.AreEqual(0, state.ConsecutiveFailures,
                $"{LogPrefix} 성공했는데 연속 실패 계수가 남아 있습니다 — 드문 실패가 쌓여 " +
                "정상 상태에서도 후퇴가 걸립니다.");
            Assert.IsFalse(state.ReachedBackoffThreshold);
        }

        // ====================================================================
        // 3. 후퇴 — 창은 「연속 실패의 첫 시도」부터
        // ====================================================================

        [Test]
        public void 후퇴_창은_연속_실패의_첫_시도부터_잰다()
        {
            double interval = DefaultPollIntervalSeconds();
            var state = new GameVerdictRetryState();

            double firstFailureAt = 0.0;
            double lastFailureAt = 0.0;
            for (int i = 0; i < GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold; i++)
            {
                lastFailureAt = firstFailureAt + i * interval;
                Assert.IsTrue(state.TryBeginAttempt(lastFailureAt),
                    $"{LogPrefix} 문턱에 닿기 전({i + 1}번째)인데 조회가 막혔습니다.");
                state.NoteFailure(lastFailureAt);
            }

            Assert.IsTrue(state.ReachedBackoffThreshold, $"{LogPrefix} 문턱에 닿지 않았습니다.");

            double windowEnd = firstFailureAt + GameVerdictRetryPolicy.BackoffWindowSeconds;
            Assert.IsFalse(state.TryBeginAttempt(windowEnd - interval),
                $"{LogPrefix} 후퇴 창 안인데 조회가 열렸습니다 — 지속 실패에서 폴링마다 조회합니다.");

            // ★ 이 단언이 「마지막 시도 기준 창」을 기각한다. 마지막 시도 기준이면 이 시각은 아직
            //   창 안이라 닫혀 있고, 세 폴링짜리 일시 실패의 노출이 약 30초에서 약 33초로 늘어난다.
            Assert.IsTrue(state.TryBeginAttempt(windowEnd),
                $"{LogPrefix} 첫 시도로부터 후퇴 창이 지났는데 아직 막혀 있습니다 — 창을 마지막 " +
                $"시도부터 재고 있습니다(마지막 시도 {lastFailureAt:F1}초, 첫 시도 {firstFailureAt:F1}초). " +
                "그러면 세 폴링짜리 일시 실패의 노출이 약 3초 더 늘어납니다.");
        }

        [Test]
        public void 후퇴_창이_끝나면_계수가_0으로_초기화된다()
        {
            double interval = DefaultPollIntervalSeconds();
            var state = new GameVerdictRetryState();

            for (int i = 0; i < GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold; i++)
            {
                state.TryBeginAttempt(i * interval);
                state.NoteFailure(i * interval);
            }

            double windowEnd = GameVerdictRetryPolicy.BackoffWindowSeconds;
            Assert.IsTrue(state.TryBeginAttempt(windowEnd), $"{LogPrefix} 창이 끝났는데 막혀 있습니다.");
            Assert.AreEqual(0, state.ConsecutiveFailures,
                $"{LogPrefix} 창이 끝났는데 계수가 0으로 돌아가지 않았습니다.");

            // 초기화가 없으면 이 실패 하나로 다시 문턱을 넘어 «창 뒤에도 1회씩만» 조회하게 되고,
            // 첫 시도 시각이 고정돼 창이 다시 닫히지 않는 쪽으로도 갈 수 있다(설계 정본의 두 계수 표).
            state.NoteFailure(windowEnd);
            Assert.AreEqual(1, state.ConsecutiveFailures,
                $"{LogPrefix} 창 뒤 첫 실패가 «연속 1회»로 세지지 않았습니다 — 계수 초기화가 없습니다.");
            Assert.IsTrue(state.TryBeginAttempt(windowEnd + interval),
                $"{LogPrefix} 창 뒤 첫 실패 다음 폴링이 막혔습니다 — 계수가 초기화되지 않아 " +
                "「다음 폴링 1회 재시도」가 사라졌습니다.");
        }

        // ====================================================================
        // 4. 후퇴 범위 — 경로는 pid별
        // ====================================================================

        [Test]
        public void pid가_바뀌면_후퇴가_초기화된다()
        {
            double interval = DefaultPollIntervalSeconds();
            const ulong failingAppPid = 7;
            const ulong gamePid = 8;

            var state = new GameVerdictRetryState();
            state.ResetIfScopeChanged(failingAppPid);
            for (int i = 0; i < GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold; i++)
            {
                state.TryBeginAttempt(i * interval);
                state.NoteFailure(i * interval);
            }

            double insideWindow = interval * GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold;
            Assert.IsFalse(state.TryBeginAttempt(insideWindow),
                $"{LogPrefix} 같은 pid인데 후퇴 창이 열려 있습니다(전제 실패).");

            Assert.IsTrue(state.ResetIfScopeChanged(gamePid),
                $"{LogPrefix} pid가 바뀌었는데 «바뀜»으로 보지 않습니다.");
            Assert.AreEqual(0, state.ConsecutiveFailures);

            // ★ 이 단언이 「전역 후퇴」를 기각한다. 전역이면 매번 실패하는 비게임 전체화면 앱의
            //   후퇴가 게임 전환 뒤까지 남아, 게임 위에 최대 약 30초 노출이 새로 생긴다.
            Assert.IsTrue(state.TryBeginAttempt(insideWindow),
                $"{LogPrefix} pid가 바뀌었는데 이전 pid의 후퇴 창이 남아 조회가 막힙니다 — " +
                "비게임 전체화면 앱에서 게임으로 전환할 때 게임 위 노출이 새로 생깁니다.");

            Assert.IsFalse(state.ResetIfScopeChanged(gamePid),
                $"{LogPrefix} 같은 pid를 «바뀜»으로 봅니다 — 그러면 후퇴가 매 폴링 초기화되어 " +
                "지속 실패에서 상한이 사라집니다.");
        }

        // ====================================================================
        // 5. 비용 상한 — 창당 K회 (해제 조건 5)
        // ====================================================================

        /// <summary>
        /// 지속 실패(모든 조회가 매번 실패)를 1시간 모사해 <b>창당 조회 수</b>를 잠근다.
        /// 설계가 약속한 비용은 «창당 K회 = 1시간 현행의 3배»이고, 이 테스트가 그 약속을 계수로 고정한다.
        /// <para>호출당 비용은 실측이 없어 미확인이므로 여기서는 <b>횟수만</b> 잰다.</para>
        /// </summary>
        [Test]
        public void 지속_실패에서_조회는_후퇴_창당_K회를_넘지_않는다()
        {
            double interval = DefaultPollIntervalSeconds();
            const double oneHourSeconds = 3600.0;
            var state = new GameVerdictRetryState();

            int attempts = 0;
            int attemptsInCurrentWindow = 0;
            int maxAttemptsInWindow = 0;
            double windowAnchor = double.NegativeInfinity;
            double previousWindowAnchor = double.NegativeInfinity;
            double minWindowSpacing = double.PositiveInfinity;

            int pollCount = (int)(oneHourSeconds / interval);
            for (int i = 0; i < pollCount; i++)
            {
                double now = i * interval;                     // 누적하지 않는다(부동소수 드리프트 방지)
                state.ResetIfScopeChanged(1);                  // 같은 pid가 계속 전경
                if (!state.TryBeginAttempt(now)) continue;

                if (state.ConsecutiveFailures == 0)
                {
                    previousWindowAnchor = windowAnchor;
                    windowAnchor = now;
                    if (!double.IsNegativeInfinity(previousWindowAnchor))
                    {
                        minWindowSpacing = System.Math.Min(minWindowSpacing, windowAnchor - previousWindowAnchor);
                    }
                    if (attemptsInCurrentWindow > maxAttemptsInWindow) maxAttemptsInWindow = attemptsInCurrentWindow;
                    attemptsInCurrentWindow = 0;
                }

                attempts++;
                attemptsInCurrentWindow++;
                state.NoteFailure(now);
            }
            if (attemptsInCurrentWindow > maxAttemptsInWindow) maxAttemptsInWindow = attemptsInCurrentWindow;

            int windowsPerHour = (int)(oneHourSeconds / GameVerdictRetryPolicy.BackoffWindowSeconds);
            int expected = GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold * windowsPerHour;

            Debug.Log($"{LogPrefix} 지속 실패 1시간 — 폴링 {pollCount}회 중 조회 {attempts}회 " +
                $"(창당 최대 {maxAttemptsInWindow}회 · 창 간격 최소 {minWindowSpacing:F1}초). " +
                $"후퇴가 없으면 {pollCount}회, 옛 판(실패도 30초 캐시)은 {windowsPerHour}회다.");

            Assert.AreEqual(GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold, maxAttemptsInWindow,
                $"{LogPrefix} 후퇴 창 하나에서 조회가 {maxAttemptsInWindow}회 났습니다 — " +
                "설계 상한은 창당 K회입니다.");
            Assert.AreEqual(expected, attempts,
                $"{LogPrefix} 지속 실패 1시간 조회 수가 {attempts}회입니다(기대 {expected}회 = " +
                $"옛 판 {windowsPerHour}회의 K배). 이 값이 늘면 24시간 상주 앱의 비용 약속이 깨집니다.");
            Assert.GreaterOrEqual(minWindowSpacing, GameVerdictRetryPolicy.BackoffWindowSeconds,
                $"{LogPrefix} 후퇴 창이 {minWindowSpacing:F1}초 간격으로 열렸습니다 — 창 길이보다 짧습니다.");
        }

        // ====================================================================
        // 6. 값 타입 함정 — 상태가 사라지면 후퇴가 매번 열린다
        // ====================================================================

        private sealed class ReadonlyHolder
        {
            public readonly GameVerdictRetryState State = new GameVerdictRetryState();
        }

        /// <summary>
        /// <c>readonly</c> 필드에 담아도 상태가 남는가. <see cref="WallClockIntervalGate"/>가 같은
        /// 함정으로 값 타입을 버렸다 — 값 타입이면 C#이 호출마다 <b>복사본</b>에 메서드를 불러
        /// 계수가 저장되지 않고 문이 매번 열린다(컴파일 경고도, 테스트 실패도 없다).
        /// </summary>
        [Test]
        public void readonly_필드에_담아도_후퇴_상태가_남는다()
        {
            var holder = new ReadonlyHolder();
            double interval = DefaultPollIntervalSeconds();

            for (int i = 0; i < GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold; i++)
            {
                Assert.IsTrue(holder.State.TryBeginAttempt(i * interval));
                holder.State.NoteFailure(i * interval);
            }

            Assert.AreEqual(GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold,
                holder.State.ConsecutiveFailures,
                $"{LogPrefix} readonly 필드에 담은 상태의 계수가 남지 않았습니다 — 값 타입으로 " +
                "바뀌었다면 후퇴가 영원히 걸리지 않습니다(조용한 결함).");
            Assert.IsFalse(holder.State.TryBeginAttempt(
                interval * GameVerdictRetryPolicy.ConsecutiveFailureBackoffThreshold),
                $"{LogPrefix} readonly 필드에 담으면 후퇴 창이 열립니다 — 값 타입 복사 함정입니다.");
        }
    }
}
