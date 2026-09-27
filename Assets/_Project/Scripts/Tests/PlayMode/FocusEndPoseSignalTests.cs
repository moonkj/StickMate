using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★★ 잠그는 계약 — <b>집중 세션의 끝 신호는 「조용히」 사라지지 않는다</b>
    /// (2026-09-27, <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §21-1 판정 1).
    ///
    /// ============================================================================
    /// 무엇이 결함이었나 — 없던 것은 신호가 아니라 <b>신호의 보장</b>이다
    /// ============================================================================
    /// 완주 축하는 <b>원래 있었다</b>: <c>CompleteSession</c>이 <c>FocusComplete</c> 포즈를 부르고 그
    /// 상태는 대사와 지속 시간을 가진다. 없던 것은 <b>그것이 실제로 났는지에 대한 보장</b>이다 —
    /// 시작 포즈에는 재시도 창과 실패 로그가 둘 다 있는데 <b>완주·취소는 반환값을 버리고 로그도
    /// 남기지 않았다</b>. 관문은 「Idle/Walk일 것 + 스펙터클 락이 비어 있을 것」이라, 세션이 끝나는
    /// 순간 캐릭터가 드래그당하고 있거나 다른 연출 중이면 <b>50분을 채운 축하가 통째로 사라지고
    /// 사후에 그런 일이 있었는지조차 알 수 없었다.</b>
    ///
    /// ============================================================================
    /// 네 테스트가 <b>서로 다른 방향</b>을 겨눈다 (한쪽만 있으면 죽은 프로브와 구별되지 않는다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>음성 대조</b> — 관문이 열려 있으면 <b>첫 시도에</b> 확정되고 포기 로그가
    ///     <b>한 줄도</b> 없다. 이 「0건」은 <see cref="FocusWatchDirector.LogTag"/> 적중을 같은 실행에서
    ///     함께 세어 <b>계기가 살아 있음</b>을 증명한 뒤에만 의미가 있다(부재 단언은 계기가 죽어도
    ///     조용히 초록이 된다).</item>
    ///   <item><b>양성 대조</b> — 관문이 창 내내 닫혀 있으면 확정되지 않고 <b>끝내 로그가 남는다</b>.
    ///     그리고 세션이 끝난 직후에는 <b>아직</b> 포기 로그가 없다 — 그것이 「재시도가 돌고 있다」와
    ///     「즉시 포기했다」를 가르는 계기다.</item>
    ///   <item><b>재시도 본체</b> — 창 안에서 관문이 열리면 그 순간 포즈가 난다. 여기가 「재시도가
    ///     실제로 매 프레임 돈다」의 유일한 직접 증거다.</item>
    ///   <item><b>취소는 다르다</b> — 반환값 확인과 로그까지만이고 <b>재시도가 없다</b>. 그리고
    ///     <b>축하 포즈를 붙이지 않는다</b>(완주하지 않은 것을 축하하면 원칙 1이 깨진다). 이 「없음」도
    ///     같은 테스트 안에서 <b>관문이 열린 경우</b>와 대조해 못박는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 예산과 상수 — <b>숫자를 베끼지 않는다</b>
    /// ============================================================================
    /// <para>재시도 창은 <see cref="FocusWatchDirector.StartPoseRetryWindowSeconds"/>에서, 포즈 지속은
    /// <see cref="StickConfig.pomodoroCompletePoseHoldSeconds"/>에서, 세션 길이는 디렉터가 스스로 정한
    /// <see cref="FocusWatchDirector.SessionDurationSeconds"/>에서 <b>유도</b>한다. 로그 니들도 프로덕션
    /// 상수를 참조한다 — 문자열을 베끼면 문구가 바뀌는 날 이 파일만 조용히 낡는다(CLAUDE.md).</para>
    ///
    /// <para>시간 예산은 전부 <b>벽시계(초)</b>다. 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로
    /// 돌아서 프레임 수 예산은 실제로 밀리초가 된다(CLAUDE.md). 대신
    /// <see cref="Time.timeScale"/>을 올려 <b>세션 시간</b>을 압축한다 —
    /// <c>FocusSessionPhaseEventTests</c>가 세운 어법 그대로이고, <c>Time.deltaTime</c>은 timeScale이
    /// 곱해진 값이라 재시도 창도 같은 배율로 압축된다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 창 열거·좌표계·네이티브 호출에 의존하지 않는다.</para>
    /// </summary>
    public sealed class FocusEndPoseSignalTests
    {
        private const string LogPrefix = "[집중끝포즈]";

        /// <summary>세션 시간 압축 배율. 하한 세션 하나가 벽시계 몇 초에 끝난다.</summary>
        private const float TimeCompression = 12f;

        /// <summary>씬 부팅 직후 캐릭터는 낙하 중이다 — 접지/배회가 안정될 때까지의 상한(벽시계 초).</summary>
        private const float SettleTimeoutSeconds = 12f;

        /// <summary>세션 하나가 끝나기를 기다리는 <b>벽시계</b> 상한(초).</summary>
        private const float SessionWallClockBudget = 40f;

        /// <summary>이 테스트가 시작하는 세션 길이(분). 디렉터의 자체 하한에 걸리므로 실제 길이는
        /// <see cref="FocusWatchDirector.SessionDurationSeconds"/>에서 되읽는다 — 하한 숫자를 베끼지 않는다.</summary>
        private const float SessionMinutes = 1f;

        /// <summary>완주가 임박했다고 보는 남은 시간(<b>세션</b> 초). 이 시점에 관문을 조작한다.
        /// 압축 배율 때문에 벽시계로는 이 값의 1/배율이라 노출 창이 짧다.</summary>
        private const float EndgameRemainingSeconds = 3f;

        /// <summary>재시도 창이 다 지나기를 기다리는 <b>벽시계</b> 상한(초).
        /// ★ 프로덕션 상수에서 유도한다 — 창이 바뀌면 이 예산이 저절로 따라간다.
        /// <c>dt</c>가 timeScale로 압축되므로 창은 벽시계로 <c>창/배율</c>만큼 걸린다(여유 4배 + 3초).</summary>
        private static float RetryWallClockBudget =>
            FocusWatchDirector.StartPoseRetryWindowSeconds / TimeCompression * 4f + 3f;

        private StickmanAgent _agent;
        private FocusWatchDirector _director;

        /// <summary>테스트가 직접 쥐는 관문 차단용 락 소유자. 프로덕션 코드가 아니라 <b>이 테스트</b>가
        /// 주인이라, 해제 책임도 이 파일에 있다(TearDown).</summary>
        private object _blocker;

        private float _savedTimeScale = -1f;

        // ── 로그 계기. 니들은 전부 프로덕션 상수다(문자열 복사 0).
        private int _tagHits;
        private int _giveUpHits;
        private int _cancelSkipHits;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(condition)) return;
            if (condition.IndexOf(FocusWatchDirector.LogTag, StringComparison.Ordinal) >= 0) _tagHits++;
            if (condition.IndexOf(FocusWatchDirector.CompletePoseGiveUpLogNeedle, StringComparison.Ordinal) >= 0)
                _giveUpHits++;
            if (condition.IndexOf(FocusWatchDirector.CancelPoseSkipLogNeedle, StringComparison.Ordinal) >= 0)
                _cancelSkipHits++;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= OnLog;

            // 전역 정적 상태(락)·진행 중 세션·timeScale은 씬 생명주기와 무관하게 살아남는다 —
            // 여기서 안 걷으면 뒤이어 도는 다른 테스트가 "왜 내 연출이 안 뜨지"로 빨개진다.
            if (_director != null && _director.IsSessionActive) _director.StopFocusSession();
            if (_blocker != null)
            {
                SpectacleEventLock.Release(_blocker);
                _blocker = null;
            }
            if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            if (_savedTimeScale > 0f) Time.timeScale = _savedTimeScale;
            _savedTimeScale = -1f;

            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null &&
                _agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }

            _agent = null;
            _director = null;
            _tagHits = 0;
            _giveUpHits = 0;
            _cancelSkipHits = 0;
            yield return null;
        }

        private IEnumerator LoadSceneAndSettle()
        {
            if (SpectacleEventLock.IsActive) SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = UnityEngine.Object.FindFirstObjectByType<StickmanAgent>();
            _director = UnityEngine.Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            Assert.IsNotNull(_director, $"{LogPrefix} 씬에 FocusWatchDirector가 없습니다.");
            if (_director.IsSessionActive) _director.StopFocusSession();

            double t0 = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - t0 < SettleTimeoutSeconds)
            {
                StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
                if ((id == StickmanStateId.Idle || id == StickmanStateId.Walk) && !SpectacleEventLock.IsActive)
                {
                    // 압축은 <b>안정된 뒤</b>에 건다 — 낙하·착지 연출을 12배로 돌리면 접지 판정이 흔들린다.
                    _savedTimeScale = Time.timeScale;
                    Time.timeScale = TimeCompression;

                    _tagHits = 0;
                    _giveUpHits = 0;
                    _cancelSkipHits = 0;
                    Application.logMessageReceived += OnLog;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail($"{LogPrefix} {SettleTimeoutSeconds:F0}초(벽시계) 안에 캐릭터가 Idle/Walk로 안정되지 " +
                $"않았습니다(지금 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}).");
        }

        private IEnumerator WaitRealSeconds(double seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        /// <summary>세션을 켜고 «완주 임박»까지 흘린다. 여기까지는 네 테스트가 공통이다.</summary>
        private IEnumerator StartSessionAndRunToEndgame()
        {
            Assert.IsFalse(_director.IsSessionActive, $"{LogPrefix} 시작 전인데 세션이 이미 진행 중입니다.");
            _director.StartFocusSession(SessionMinutes);
            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} 세션이 시작되지 않았습니다.");

            // 전제 — 관문 조작에 쓸 여유가 실제로 있는가(세션 길이는 디렉터가 정한 값을 되읽는다).
            Assert.Greater(_director.SessionDurationSeconds, EndgameRemainingSeconds * 2f,
                $"{LogPrefix} 세션 길이 {_director.SessionDurationSeconds:F0}초가 너무 짧아 완주 직전 구간을 " +
                "만들 수 없습니다 — 이 테스트의 전제가 깨졌습니다.");

            double t0 = Time.realtimeSinceStartupAsDouble;
            while (_director.IsSessionActive && _director.RemainingSeconds > EndgameRemainingSeconds)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, SessionWallClockBudget,
                    $"{LogPrefix} {SessionWallClockBudget:F0}초(벽시계) 안에 완주 직전 구간에 닿지 못했습니다 " +
                    $"(남은 {_director.RemainingSeconds:F1}초).");
                yield return null;
            }
            Assert.IsTrue(_director.IsSessionActive,
                $"{LogPrefix} 관문을 조작하기 전에 세션이 이미 끝났습니다 — 이 시나리오를 재현할 수 없습니다.");
        }

        /// <summary>관문을 <b>지금</b> 연다: 남의 락을 반납시키고 상태를 Idle로 되돌린다.
        /// (TearDown이 이미 쓰는 어법과 같다. 자율 연출이 끼어드는 것을 이 한 프레임만큼 좁힌다.)</summary>
        private void OpenGateNow()
        {
            if (SpectacleEventLock.IsActive && !ReferenceEquals(SpectacleEventLock.CurrentOwner, _blocker))
                SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);

            StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
            if (id != StickmanStateId.Idle && id != StickmanStateId.Walk)
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
        }

        /// <summary>포즈 관문이 <b>지금</b> 열려 있는가 — 프로덕션 관문(<c>TryTriggerPoseState</c>)이 보는
        /// 세 가지를 같은 순서로 읽는다: 상태가 Idle/Walk · 스펙터클 락이 비어 있음 · 보존 동결이
        /// <b>신규</b> 획득을 막지 않음.
        /// <para>★ 세 번째를 빼면 안 된다. <see cref="SpectacleEventLock.TryAcquire"/>는 「남이 쥐고 있다」와
        /// 「동결이 막는다」에 <b>똑같이 false</b>를 내므로, 동결을 안 보고 기다리면 «열려 있다»고 읽은
        /// 관문에서 락 선점이 실패한다 — 이 저장소가 반복해 당한 «실패한 측정과 성공한 측정이 똑같이
        /// 생겼다»의 또 다른 형태다.</para></summary>
        private bool IsPoseGateOpenNow()
        {
            StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
            return (id == StickmanStateId.Idle || id == StickmanStateId.Walk)
                && !SpectacleEventLock.IsActive
                && !CharacterPreservationFreeze.BlocksNewSpectacle;
        }

        /// <summary>실패 메시지 전용 진단 — <c>TryAcquire</c>가 거짓을 내는 <b>서로 다른 사유</b>를 갈라
        /// 적는다. 이 문장이 없어서 2026-09-28에 「누가 락을 쥐고 있었는가」를 러너 로그를 뒤져 알아내야
        /// 했다(답은 <b>디렉터 자신의 시작 포즈 재시도</b>였다).</summary>
        private string DescribeGateNow()
            => $"상태 {_agent.Blackboard.Machine.CurrentStateId} · 락 " +
               (SpectacleEventLock.IsActive ? $"{SpectacleEventLock.ActiveKind} 점유" : "비어 있음") +
               $" · 보존동결 {CharacterPreservationFreeze.BlocksNewSpectacle}" +
               $" · 시작포즈확정 {_director.IsStartPoseConfirmed}";

        private IEnumerator WaitForSessionEnd()
        {
            double t0 = Time.realtimeSinceStartupAsDouble;
            while (_director.IsSessionActive)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, SessionWallClockBudget,
                    $"{LogPrefix} 세션이 {SessionWallClockBudget:F0}초(벽시계) 안에 끝나지 않았습니다.");
                yield return null;
            }
            yield return null;   // 완주 프레임의 판정이 끝난 뒤에 읽는다.
        }

        // ================================================================================
        // ① 음성 대조 — 관문이 열려 있으면 첫 시도에 확정되고 포기 로그가 없다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 관문이_열려있으면_완주포즈가_첫시도에_확정되고_포기로그가_없다()
        {
            yield return LoadSceneAndSettle();
            yield return StartSessionAndRunToEndgame();

            OpenGateNow();
            yield return null;
            yield return WaitForSessionEnd();

            Assert.IsTrue(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 관문이 열려 있었는데 완주 포즈가 확정되지 않았습니다 " +
                $"(지금 상태 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}). " +
                "50분을 채운 사용자에게 아무 반응도 없는 그 경우입니다.");

            // ★ 계기 양성 대조 — 이게 없으면 아래 「0건」은 «로그가 안 났다»가 아니라
            //   «구독이 죽었다»와 구별되지 않는다.
            Assert.Greater(_tagHits, 0,
                $"{LogPrefix} 세션이 한 번 돌았는데 「{FocusWatchDirector.LogTag}」 로그가 한 줄도 안 잡혔습니다 — " +
                "로그 계기가 죽었습니다. 아래 포기 로그 0건은 아무것도 증명하지 못합니다.");

            // 창이 다 지나도록 기다려도 포기 로그는 없어야 한다(확정됐으면 재시도가 아예 안 돈다).
            yield return WaitRealSeconds(RetryWallClockBudget);
            Assert.AreEqual(0, _giveUpHits,
                $"{LogPrefix} 포즈가 확정됐는데도 포기 로그가 {_giveUpHits}건 났습니다 — 재시도 창이 확정 뒤에도 " +
                "돌고 있습니다(같은 세션에서 성공과 실패를 동시에 보고합니다).");
            Assert.AreEqual(0, _cancelSkipHits,
                $"{LogPrefix} 완주 경로인데 취소 포즈 로그가 {_cancelSkipHits}건 났습니다 — 두 경로가 섞였습니다.");

            // 축하 포즈는 <b>일시</b> 포즈다 — 지속이 지나면 락을 돌려줘야 한다(상시 표면 0개의 근거).
            float hold = Mathf.Max(0.1f, _agent.Config.pomodoroCompletePoseHoldSeconds);
            yield return WaitRealSeconds(hold / TimeCompression * 4f + 1f);
            Assert.IsFalse(SpectacleEventLock.IsActive,
                $"{LogPrefix} 축하 포즈가 끝난 뒤에도 스펙터클 락이 잡혀 있습니다({SpectacleEventLock.ActiveKind}) — " +
                "주인 없는 락은 이후 모든 연출을 조용히 막습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 첫 시도 확정 · 포기 로그 0건(태그 적중 {_tagHits}건으로 계기 생존 확인) · " +
                "지속 뒤 락 반납.");
        }

        // ================================================================================
        // ② 양성 대조 — 창 내내 막히면 확정되지 않고 끝내 로그가 남는다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 관문이_창내내_막히면_확정되지_않고_끝내_포기로그가_남는다()
        {
            yield return LoadSceneAndSettle();
            yield return StartSessionAndRunToEndgame();

            // 관문의 「락이 비어 있을 것」 조건만 골라 막는다(상태는 Idle/Walk 그대로 둔다 —
            // 어느 조건이 막았는지 구분되게).
            //
            // ★★ 관문을 연 뒤 락을 잡기까지 <b>프레임 경계를 두지 않는다</b>. 그 한 프레임에 디렉터의
            //    포즈 재시도가 같은 관문을 보고 락을 <b>먼저</b> 집어간다 — 2026-09-28에 아래 ④번이
            //    정확히 그렇게 빨개졌다(테스트가 자기 락을 못 잡았다). <b>여기서 yield를 되살리지 마라.</b>
            OpenGateNow();
            _blocker = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, _blocker),
                $"{LogPrefix} 테스트가 락을 잡지 못했습니다 — 이 시나리오를 재현할 수 없습니다({DescribeGateNow()}).");

            yield return WaitForSessionEnd();

            Assert.IsFalse(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 락이 걸려 있는데 완주 포즈가 확정됐다고 보고합니다 — 남의 연출에서 상태 슬롯을 " +
                "빼앗았거나 확정 판정이 «불렀다»만 보고 있습니다.");

            // ★★ 이 단언이 「재시도가 돈다」와 「즉시 포기했다」를 가른다 — 포기 니들은 창이 다 지난
            //    뒤의 줄에만 걸리므로(재시도 시작 줄에는 「지금」이 끼어 걸리지 않는다) 지금은 0이어야 한다.
            Assert.AreEqual(0, _giveUpHits,
                $"{LogPrefix} 세션이 끝난 그 프레임에 벌써 포기 로그가 났습니다 — 재시도 창이 열리지 않았거나 " +
                $"{FocusWatchDirector.StartPoseRetryWindowSeconds:F0}초를 기다리지 않고 포기했습니다.");

            double t0 = Time.realtimeSinceStartupAsDouble;
            while (_giveUpHits == 0)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, RetryWallClockBudget,
                    $"{LogPrefix} {RetryWallClockBudget:F1}초(벽시계) 안에 포기 로그가 나오지 않았습니다 — " +
                    "관문이 끝내 안 열렸는데도 <b>아무 흔적이 없습니다</b>. 이 보강이 막으려던 바로 그 상태입니다.");
                yield return null;
            }

            Assert.AreEqual(1, _giveUpHits,
                $"{LogPrefix} 포기 로그가 {_giveUpHits}건입니다 — 한 세션에 정확히 한 번이어야 합니다(창이 지난 뒤에도 " +
                "재시도가 계속 돌면 로그가 매 프레임 쌓입니다).");
            Assert.IsFalse(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 포기했다고 로그를 남기면서 확정 플래그는 참입니다 — 두 보고가 어긋났습니다.");

            SpectacleEventLock.Release(_blocker);
            _blocker = null;

            // 포기한 뒤에는 관문이 열려도 되살아나지 않는다(10초 지난 「수고했어!」는 원칙 1 위반).
            yield return WaitRealSeconds(RetryWallClockBudget);
            Assert.IsFalse(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 창이 끝난 뒤 락이 풀렸더니 축하 포즈가 되살아났습니다 — 완주에서 파생되지 않은 " +
                "시점의 대사입니다.");
            Assert.AreEqual(1, _giveUpHits, $"{LogPrefix} 포기 로그가 뒤늦게 또 났습니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 확정 없음 · 창 동안 무로그 · 창 끝에 포기 로그 1건 · 이후 부활 없음.");
        }

        // ================================================================================
        // ③ 재시도 본체 — 창 안에서 관문이 열리면 그 순간 포즈가 난다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 창안에서_관문이_열리면_재시도가_완주포즈를_낸다()
        {
            yield return LoadSceneAndSettle();
            yield return StartSessionAndRunToEndgame();

            // ★ ②와 같은 이유로 관문 열기와 락 선점 사이에 yield가 없다(위 ②의 주석 참조).
            OpenGateNow();
            _blocker = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, _blocker),
                $"{LogPrefix} 테스트가 락을 잡지 못했습니다 — 이 시나리오를 재현할 수 없습니다({DescribeGateNow()}).");

            yield return WaitForSessionEnd();
            Assert.IsFalse(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 락이 걸려 있는데 완주 포즈가 확정됐습니다.");

            // 창이 살아 있는 동안 관문을 연다 → 재시도가 그 프레임에 포즈를 낸다.
            SpectacleEventLock.Release(_blocker);
            _blocker = null;

            double t0 = Time.realtimeSinceStartupAsDouble;
            while (!_director.IsCompletePoseConfirmed)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, RetryWallClockBudget,
                    $"{LogPrefix} 락이 풀렸는데도 완주 포즈를 다시 시도하지 않았습니다(재시도 창이 죽었습니다). " +
                    $"포기 로그 {_giveUpHits}건.");
                yield return null;
            }

            Assert.AreEqual(0, _giveUpHits,
                $"{LogPrefix} 재시도가 성공했는데 포기 로그가 {_giveUpHits}건 났습니다 — 성공과 실패를 동시에 " +
                "보고합니다.");
            Assert.Greater(_tagHits, 0,
                $"{LogPrefix} 로그 계기가 죽었습니다 — 위 0건이 아무것도 증명하지 못합니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — 락 해제 후 재시도가 포즈를 냈고 포기 로그는 0건.");
        }

        // ================================================================================
        // ④ 취소 — 반환값 확인 + 로그까지만. 재시도도, 축하 포즈도 없다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 취소는_관문이_막히면_로그만_남기고_재시도하지_않는다()
        {
            yield return LoadSceneAndSettle();

            // ── 대조군(관문 열림): 취소 포즈가 실제로 <b>나는</b> 것을 먼저 보인다. 이게 없으면
            //    아래 «막히면 안 난다»는 «이 관측 채널이 죽었다»와 구별되지 않는다.
            //
            // ★★ 관문 확인 → 세션 시작 → 관문 열기 → 취소를 <b>한 스텝에서</b> 끝낸다(사이에 yield가
            //    없다). 이 대기를 헬퍼 코루틴으로 빼면 «중첩 코루틴이 끝난 뒤 호출자가 같은 프레임에
            //    이어진다»를 가정하게 되는데, 그 가정이 깨지는 한 프레임이 2026-09-28에 이 테스트를
            //    빨갛게 만든 경합이다. 그래서 대기 루프는 <b>일부러 인라인</b>이다.
            double t0 = Time.realtimeSinceStartupAsDouble;
            while (!IsPoseGateOpenNow())
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, SettleTimeoutSeconds,
                    $"{LogPrefix} 대조군을 세우기 전에 포즈 관문이 열리지 않았습니다({DescribeGateNow()}).");
                yield return null;
            }

            _director.StartFocusSession(SessionMinutes);
            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} 세션이 시작되지 않았습니다.");
            // ★ 이 단언은 전제 확인이 아니라 <b>재시도 창을 잠그는</b> 장치다: 확정되면 프로덕션이 재시도
            //   잔량을 0으로 만들므로(<c>_startPoseRetryRemaining</c> 문서) 아래에서 디렉터가 락을 다시
            //   집어갈 수 없다. 확정되지 않은 채 진행하면 열린 재시도 창이 이 테스트와 락을 다툰다.
            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 관문이 열려 있었는데 시작 포즈가 확정되지 않았습니다({DescribeGateNow()}) — " +
                "재시도 창이 열린 채로 남아 아래 락 선점과 경합합니다.");
            OpenGateNow();
            Assert.IsFalse(SpectacleEventLock.IsActive,
                $"{LogPrefix} 대조군인데 락이 잡혀 있습니다({SpectacleEventLock.ActiveKind}) — 관문을 열지 못했습니다.");

            _director.StopFocusSession();
            Assert.IsFalse(_director.IsSessionActive, $"{LogPrefix} 취소했는데 세션이 살아 있습니다.");
            Assert.AreEqual(StickmanStateId.FocusCancelled, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 관문이 열려 있었는데 취소 포즈(팔짱 풀기)가 나지 않았습니다 — 이 관측 채널이 " +
                "죽어 있으면 아래 부재 단언은 의미가 없습니다.");
            Assert.AreEqual(0, _cancelSkipHits,
                $"{LogPrefix} 포즈가 났는데 「생략」 로그가 {_cancelSkipHits}건 났습니다.");

            // ── 관문이 <b>다시 열리기</b>를 기다린다 — 「취소 포즈가 끝났다」로는 부족하다.
            //    ★ 2026-09-28 실측: 옛 조건은 «락이 비고 상태가 FocusCancelled가 아니면 됐다»였는데,
            //    취소 포즈에서 강제 Idle로 빠진 캐릭터가 발판을 잃고 <c>Fall</c>로 떨어져 그 조건을
            //    <b>통과했다</b>. 그래서 다음 세션의 시작 포즈가 관문에 막혀 <b>재시도 창이 열렸고</b>,
            //    그 재시도가 한 프레임 뒤에 락을 집어가 이 테스트가 자기 락을 못 잡았다 — 단언이 틀린
            //    것이 아니라 <b>시나리오를 세우지 못한</b> 것이다. 프로덕션 관문의 세 조건을 그대로
            //    기다리는 것이 정답이다.
            float cancelHold = Mathf.Max(0.1f, _agent.Config.pomodoroCancelPoseHoldSeconds);
            double gateBudget = cancelHold / TimeCompression * 4f + SettleTimeoutSeconds;
            t0 = Time.realtimeSinceStartupAsDouble;
            while (!IsPoseGateOpenNow())
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - t0, gateBudget,
                    $"{LogPrefix} 취소 뒤 {gateBudget:F1}초(벽시계) 안에 포즈 관문이 다시 열리지 않았습니다 " +
                    $"({DescribeGateNow()}).");
                yield return null;
            }

            // ── 시험군(관문 막힘): 로그만 남고 포즈는 나지 않으며, <b>나중에도</b> 나지 않는다.
            //    ★ 여기도 관문 확인부터 락 선점까지 <b>한 스텝</b>이다(yield 없음).
            _director.StartFocusSession(SessionMinutes);
            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 관문이 열려 있었는데 시작 포즈가 확정되지 않았습니다({DescribeGateNow()}) — " +
                "재시도 창이 열린 채 남으면 그 재시도가 아래에서 락을 가로챕니다.");
            OpenGateNow();
            _blocker = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, _blocker),
                $"{LogPrefix} 테스트가 락을 잡지 못했습니다({DescribeGateNow()}).");

            int cancelHitsBefore = _cancelSkipHits;
            _director.StopFocusSession();

            Assert.AreEqual(cancelHitsBefore + 1, _cancelSkipHits,
                $"{LogPrefix} 관문이 막힌 취소가 <b>조용히</b> 끝났습니다 — 반환값을 버리고 있습니다. " +
                "사후에 그런 일이 있었는지 알 수 있는 흔적이 없습니다.");
            Assert.AreNotEqual(StickmanStateId.FocusCancelled, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 락을 무시하고 상태를 빼앗았습니다.");

            // 락을 풀고 재시도 창만큼 기다려도 취소 포즈는 <b>되살아나지 않는다</b>(취소엔 재시도가 없다).
            SpectacleEventLock.Release(_blocker);
            _blocker = null;

            bool sawCancelPose = false;
            double t1 = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - t1 < RetryWallClockBudget)
            {
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.FocusCancelled) sawCancelPose = true;
                yield return null;
            }

            Assert.IsFalse(sawCancelPose,
                $"{LogPrefix} 취소 포즈가 락이 풀린 뒤에 뒤늦게 났습니다 — 취소에 재시도가 붙었습니다. " +
                "몇 초 뒤의 「그래 쉬자」는 이미 지난 행동의 대사입니다(원칙 1).");
            Assert.AreEqual(cancelHitsBefore + 1, _cancelSkipHits,
                $"{LogPrefix} 「생략」 로그가 한 번 더 났습니다 — 취소 경로가 반복 시도하고 있습니다.");
            Assert.IsFalse(_director.IsCompletePoseConfirmed,
                $"{LogPrefix} 취소로 끝난 세션이 <b>완주</b> 포즈를 확정했다고 보고합니다 — 취소에 축하를 " +
                "붙이면 행동-텍스트 싱크가 깨집니다(원칙 1).");
            Assert.AreEqual(0, _giveUpHits,
                $"{LogPrefix} 취소 경로에서 완주 포기 로그가 {_giveUpHits}건 났습니다 — 두 경로가 섞였습니다.");

            Debug.Log($"{LogPrefix} ④ 통과 — 열림: 포즈 O·로그 X / 막힘: 포즈 X·로그 1건 · 이후 부활 없음 · 완주 확정 없음.");
        }
    }
}
