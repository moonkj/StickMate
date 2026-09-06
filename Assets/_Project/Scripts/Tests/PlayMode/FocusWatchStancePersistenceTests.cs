using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★★ 페르소나(소은) 지적: <i>"집중모드 25분 세션의 99.87%가 평소와 똑같다"</i>.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — <b>시작 2초가 아니라 그 「뒤」</b>
    /// ============================================================================
    /// <see cref="FocusRingPoseSyncTests"/>는 <b>시작 포즈 2초</b>가 실제로 그려지는지를 잰다.
    /// 그 2초는 세션(1,500초)의 <b>0.13%</b>다. 나머지 99.87% 동안 캐릭터는 평소 Idle 중립으로
    /// 돌아가 있었고, 그게 페르소나가 본 그림이다.
    ///
    /// <para>여기서는 <b>그 뒤</b>를 잰다:</para>
    /// <list type="number">
    ///   <item><b>지속</b> — 시작 포즈가 끝나고 Idle로 돌아온 <b>뒤에도</b> 관망 자세가 유지된다.
    ///     벽시계 몇 초 동안 <b>매 프레임</b> 재서, 한 프레임이라도 중립으로 풀리면 실패한다.</item>
    ///   <item><b>네거티브 컨트롤</b> — 세션을 끄면 <b>실제로 풀린다</b>. 이게 없으면 위 단언은
    ///     "무엇을 해도 팔이 그 모양"인 상태와 구분되지 않는다.</item>
    ///   <item><b>비침해</b> — 관망 자세는 <see cref="SpectacleEventLock"/>을 잡지 않는다.
    ///     잡으면 세션 25분 내내 파쿠르·춤·활쏘기가 <b>조용히</b> 막힌다.</item>
    /// </list>
    ///
    /// <para><b>포즈 상수를 베끼지 않는다</b>: 판정 지표는 <c>FocusRingPoseSyncTests</c>가 세운 것과
    /// 같은 <b>실측 기하</b>(전완 기울기 = (손끝 y − 팔꿈치 y) ÷ 전완 길이)이고, 기준선은 같은 씬에서
    /// <b>직접 측정한 중립</b>이다. <c>StickmanPoseAnimator.FocusCross*</c>를 참조하면 "애니메이터가
    /// 자기 상수를 자기가 확인하는" 항상 참인 단언이 된다.</para>
    ///
    /// <para>시간 예산은 전부 <b>벽시계(초)</b>다 — 이 저장소의 배치모드 PlayMode는 수천 fps로 돌아서
    /// 프레임 수 예산이 밀리초가 된다(CLAUDE.md).</para>
    ///
    /// <para><b>플랫폼</b>: 중립. 창 열거/커서에 의존하지 않는다(G3는 커서를 쓰지만 이 파일은 G3의
    /// 발동을 기다리지 않는다 — 관망 자세 자체는 커서와 무관하다).</para>
    /// </summary>
    public sealed class FocusWatchStancePersistenceTests
    {
        private const string LogPrefix = "[관망자세]";

        /// <summary>씬 부팅 직후 캐릭터는 낙하 중이다 — 접지/배회가 안정될 때까지의 상한(벽시계 초).</summary>
        private const float SettleTimeoutSeconds = 12f;

        /// <summary>상태가 Idle이 된 뒤 착지 브레이스 자세가 완전히 풀릴 때까지의 대기(초).
        /// <see cref="FocusRingPoseSyncTests"/>가 실측으로 세운 값과 같은 이유다.</summary>
        private const float PoseSettleSeconds = 1.5f;

        /// <summary>중립 기준선을 평균 내는 구간(초).</summary>
        private const float BaselineSampleSeconds = 0.5f;

        /// <summary>관망 자세가 «유지되는가»를 지켜보는 구간(초). 25분을 다 볼 수는 없으므로,
        /// <b>평소 Idle 사이클(2~6초)보다 확실히 긴</b> 구간을 본다 — 그 안에 Idle 종료/연장/제스처가
        /// 최소 한 번은 들어오므로 "한 사이클만 버티는" 구현은 여기서 걸린다.</summary>
        private const float HoldWatchSeconds = 8f;

        /// <summary>세션 길이(분). 하한(60초)에 걸리므로 실제로는 60초짜리이고, 어느 단언도 만료를
        /// 기다리지 않는다.</summary>
        private const float SessionMinutes = 1f;

        /// <summary>「관망 자세」로 인정하는 전완 기울기의 하한. 중립은 −0.6 근처, 팔짱은 +0.1 근처,
        /// 뒷짐은 그 사이다 — 중립 쪽으로 넉넉히 떨어진 곳에 선을 긋는다
        /// (<see cref="FocusRingPoseSyncTests"/>가 실측으로 세운 −0.15와 같은 자리).</summary>
        private const float StanceForearmRiseFloor = -0.15f;

        /// <summary>중립 기준선이 "팔을 내린 자세"라고 인정하는 상한(양성 대조).</summary>
        private const float NeutralForearmRiseCeiling = -0.40f;

        private StickmanAgent _agent;
        private FocusWatchDirector _director;

        [TearDown]
        public void TearDown()
        {
            if (_director != null) _director.StopFocusSession();
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            _agent = null;
            _director = null;
        }

        private IEnumerator LoadSceneAndSettle()
        {
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            _director = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            Assert.IsNotNull(_director, $"{LogPrefix} 씬에 FocusWatchDirector가 없습니다.");
            Assert.IsNotNull(_agent.Config, $"{LogPrefix} StickmanAgent에 설정이 배선돼 있지 않습니다.");
            Assert.IsTrue(_agent.Config.focusSessionAmbientEnabled,
                $"{LogPrefix} 배포 설정에서 집중 세션 앰비언트가 꺼져 있습니다 — 이 파일의 모든 단언이 " +
                "«기능이 없어서» 통과하거나 실패하게 됩니다(마스터 스위치는 EditMode가 따로 잽니다).");

            float elapsed = 0f;
            while (elapsed < SettleTimeoutSeconds)
            {
                StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
                if ((id == StickmanStateId.Idle || id == StickmanStateId.Walk) && !SpectacleEventLock.IsActive)
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.Fail($"{LogPrefix} {SettleTimeoutSeconds:F0}초 안에 캐릭터가 Idle/Walk로 안정되지 않았습니다 " +
                $"(지금 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}).");
        }

        // ================================================================================
        // ① 시작 포즈가 끝난 «뒤»에도 관망 자세가 유지된다 — 이 라운드의 전부
        // ================================================================================

        [UnityTest]
        public IEnumerator 시작포즈가_끝난_뒤에도_Idle에서_관망_자세가_유지된다()
        {
            yield return LoadSceneAndSettle();

            // ── 중립 기준선(양성 대조). 이 값이 이미 관망 자세와 구분되지 않으면 아래가 무의미하다.
            yield return WaitSeconds(PoseSettleSeconds);
            float neutralRise = 0f;
            int neutralSamples = 0;
            float sampled = 0f;
            while (sampled < BaselineSampleSeconds)
            {
                if (!_agent.Blackboard.IsIdleAmbientMotionActive && TryForearmRise(out float rise))
                {
                    neutralRise += rise;
                    neutralSamples++;
                }
                sampled += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(neutralSamples, 0, $"{LogPrefix} 중립 표본을 한 프레임도 얻지 못했습니다.");
            neutralRise /= neutralSamples;
            Assert.Less(neutralRise, NeutralForearmRiseCeiling,
                $"{LogPrefix} 중립인데 전완이 이미 수평에 가깝습니다({neutralRise:F3}) — 이 지표가 두 자세를 " +
                "구분하지 못한다는 뜻이라 아래 단언이 무의미해집니다.");
            Assert.IsFalse(_agent.Blackboard.IsFocusWatchStanceActive,
                $"{LogPrefix} 세션을 시작하기도 전에 관망 자세가 활성입니다.");

            // ── 세션 시작 → 시작 포즈(2초)가 끝나기를 기다린다.
            _director.StartFocusSession(SessionMinutes);
            yield return null;
            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 시작 포즈가 확정되지 않았습니다 — 이 시나리오를 재현할 수 없습니다.");

            float wait = 0f;
            float startBudget = Mathf.Max(1f, _agent.Config.pomodoroStartPoseHoldSeconds) * 4f;
            while (wait < startBudget &&
                   _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.FocusStart)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.AreNotEqual(StickmanStateId.FocusStart, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 시작 포즈가 {startBudget:F1}초 안에 끝나지 않았습니다.");

            // ── ★ 본론: 여기서부터 «평소와 똑같았던» 구간이다. 매 프레임 잰다.
            float watched = 0f;
            int idleFrames = 0;
            int stanceFrames = 0;
            float worstRise = float.PositiveInfinity;
            float sumRise = 0f;
            int walkFrames = 0;
            bool sawBackHands = false;
            bool sawCrossed = false;
            while (watched < HoldWatchSeconds)
            {
                StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
                if (id == StickmanStateId.Walk) walkFrames++;

                // ★ 제스처 프레임은 뺀다 — G1(자세 고쳐잡기)과 G4(자세 바꾸기)는 <b>일부러</b>
                //   자세를 풀었다 다시 잡는 박자라, 그 구간의 «중립에 가까움»은 결함이 아니라 설계다.
                //   여기서 재려는 것은 «제스처가 없는 평상 구간이 평소와 같은가»이다.
                if (id == StickmanStateId.Idle && _agent.Blackboard.IsFocusWatchStanceActive
                    && !_agent.Blackboard.IsIdleAmbientMotionActive
                    && _agent.Blackboard.FocusWatchStanceSettle01 >= 0.999f
                    && TryForearmRise(out float rise))
                {
                    idleFrames++;
                    stanceFrames++;
                    sumRise += rise;
                    worstRise = Mathf.Min(worstRise, rise);
                    if (_agent.Blackboard.IsFocusWatchStanceBackHands) sawBackHands = true;
                    else sawCrossed = true;
                }
                watched += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(stanceFrames, 0,
                $"{LogPrefix} {HoldWatchSeconds:F0}초 동안 «관망 자세가 완전히 선 Idle 프레임»이 한 번도 " +
                "없었습니다 — 세션 중인데 자세 층이 한 번도 켜지지 않았거나, 캐릭터가 내내 걷고 있었습니다" +
                $"(걷기 프레임 {walkFrames}개). 걷기를 완전히 막는 것도 설계 위반이지만, 한 번도 서지 " +
                "않는 것은 관망 자세가 아무것도 덮지 못한다는 뜻입니다.");

            Assert.Greater(worstRise, StanceForearmRiseFloor,
                $"{LogPrefix} ★ 페르소나가 본 그림입니다 — 관망 자세가 켜졌다고 보고하는 프레임에서 " +
                $"전완 기울기가 {worstRise:F3}까지 떨어졌습니다(중립 {neutralRise:F3}). " +
                "시작 2초만 팔짱을 끼고 그 뒤에는 평소 Idle로 돌아간 상태입니다.");

            Debug.Log($"{LogPrefix} ① 통과 — {HoldWatchSeconds:F0}초 관찰: 관망 자세 프레임 {stanceFrames}개 " +
                $"(걷기 {walkFrames}개), 전완 기울기 평균 {sumRise / Mathf.Max(1, idleFrames):F3} / " +
                $"최악 {worstRise:F3}, 중립 기준선 {neutralRise:F3}. " +
                $"자세 종류: 팔짱 {sawCrossed} / 뒷짐 {sawBackHands}.");
        }

        // ================================================================================
        // ② 네거티브 컨트롤 — 세션을 끄면 실제로 풀린다
        // ================================================================================

        [UnityTest]
        public IEnumerator 세션을_끄면_관망_자세가_풀리고_평소_중립으로_돌아온다()
        {
            yield return LoadSceneAndSettle();
            yield return WaitSeconds(PoseSettleSeconds);

            _director.StartFocusSession(SessionMinutes);
            yield return null;

            // 시작 포즈가 끝나고 관망 자세가 완전히 설 때까지 기다린다.
            float wait = 0f;
            while (wait < 8f && !(_agent.Blackboard.IsFocusWatchStanceActive
                                  && _agent.Blackboard.FocusWatchStanceSettle01 >= 0.999f))
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(_agent.Blackboard.IsFocusWatchStanceActive,
                $"{LogPrefix} 관망 자세가 서지 않아 «푸는» 것을 잴 수 없습니다.");

            // 자세가 각도까지 수렴하도록 잠깐 더 둔다(지수 감쇠). 제스처(G1/G4)는 일부러 자세를
            // 풀었다 잡으므로 그 프레임을 피해서 표본을 잡는다.
            yield return WaitSeconds(0.5f);
            float stanceRise = float.NaN;
            float seek = 0f;
            while (seek < 4f)
            {
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                    && _agent.Blackboard.IsFocusWatchStanceActive
                    && !_agent.Blackboard.IsIdleAmbientMotionActive
                    && TryForearmRise(out float rise))
                {
                    stanceRise = rise;
                    break;
                }
                seek += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(float.IsNaN(stanceRise),
                $"{LogPrefix} 제스처가 없는 관망 자세 프레임을 4초 안에 못 잡았습니다.");
            Assert.Greater(stanceRise, StanceForearmRiseFloor,
                $"{LogPrefix} 세션 중인데 전완이 이미 중립입니다({stanceRise:F3}) — 아래 «풀림» 판정이 " +
                "아무것도 증명하지 못합니다.");

            _director.StopFocusSession();
            yield return null;
            Assert.IsFalse(_agent.Blackboard.IsFocusSessionAmbientActive,
                $"{LogPrefix} 세션을 껐는데 앰비언트가 계속 활성입니다.");

            // 취소 포즈(FocusCancelled)와 지수 감쇠가 팔을 내리는 시간을 준다.
            float budget = Mathf.Max(1f, _agent.Config.pomodoroCancelPoseHoldSeconds) + 2.5f;
            float elapsed = 0f;
            float finalRise = float.NaN;
            while (elapsed < budget)
            {
                if (_agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                    && !_agent.Blackboard.IsIdleAmbientMotionActive
                    && TryForearmRise(out float rise))
                {
                    finalRise = rise;
                    if (rise < NeutralForearmRiseCeiling) break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(float.IsNaN(finalRise),
                $"{LogPrefix} 세션을 끈 뒤 Idle 표본을 한 번도 얻지 못했습니다.");
            Assert.Less(finalRise, NeutralForearmRiseCeiling,
                $"{LogPrefix} ★ 세션이 끝났는데 팔짱이 풀리지 않았습니다(전완 기울기 {finalRise:F3}). " +
                "«끄면 예전 거동»이 코드로 보장되지 않는다는 뜻이고, 사용자에게는 캐릭터가 " +
                "영원히 팔짱을 낀 채로 남습니다.");
            Assert.IsFalse(_agent.Blackboard.IsFocusWatchStanceActive,
                $"{LogPrefix} 세션이 끝났는데 관망 자세 층이 여전히 활성입니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 세션 중 전완 기울기 {stanceRise:F3} → 종료 후 {finalRise:F3} " +
                $"({elapsed:F2}초 만에 중립 복귀).");
        }

        // ================================================================================
        // ③ 비침해 — 관망 자세는 스펙터클 락을 잡지 않는다
        // ================================================================================

        [UnityTest]
        public IEnumerator 관망_자세는_세션_내내_스펙터클_락을_잡지_않는다()
        {
            yield return LoadSceneAndSettle();

            _director.StartFocusSession(SessionMinutes);
            yield return null;

            // 시작 포즈(FocusStart)는 락을 잡는다 — 그건 상태 전이라서 정상이다. 그 상태가 끝난
            // 뒤부터 잰다(관망 자세는 상태 전이가 아니라 Idle 위의 포즈 층이다).
            float wait = 0f;
            while (wait < 8f && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.FocusStart)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            float watched = 0f;
            int lockedFrames = 0;
            string worstKind = "(없음)";
            while (watched < HoldWatchSeconds)
            {
                if (_agent.Blackboard.IsFocusWatchStanceActive && SpectacleEventLock.IsActive)
                {
                    lockedFrames++;
                    worstKind = SpectacleEventLock.ActiveKind.ToString();
                }
                watched += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0, lockedFrames,
                $"{LogPrefix} 관망 자세가 켜진 프레임에서 스펙터클 락이 {lockedFrames}프레임 잡혀 있었습니다" +
                $"(종류 {worstKind}) — 세션 25분 내내 파쿠르·춤·활쏘기가 조용히 막힙니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — {HoldWatchSeconds:F0}초 동안 관망 자세 중 락 점유 0프레임.");
        }

        // ================================================================================
        // 기하 헬퍼 — 포즈 애니메이터의 계산을 참조하지 않고 계층에서 직접 잰다.
        // ================================================================================

        /// <summary><b>전완의 기울기</b>: (손끝 y − 팔꿈치 y) ÷ 전완 길이. −1이면 전완이 곧게 아래,
        /// 0이면 수평, +1이면 곧게 위. 「관망 자세」와 「팔을 내린 중립」을 가르는 지표다
        /// (<see cref="FocusRingPoseSyncTests"/>가 실측으로 고른 것과 같은 지표 — 두 파일이 같은 자를
        /// 쓰는 것이 중요하다. 서로 다른 자를 쓰면 한쪽 초록이 다른 쪽 빨강을 설명하지 못한다).</summary>
        private bool TryForearmRise(out float rise)
        {
            rise = 0f;
            Transform root = _agent != null && _agent.Blackboard != null && _agent.Blackboard.Body != null
                ? _agent.Blackboard.Body.transform
                : null;
            if (root == null) return false;

            Transform upper = root.Find("RightArm");
            if (upper == null) return false;
            Transform end = upper.Find("RightArmLower") ?? upper;
            var box = end.GetComponent<BoxCollider2D>();
            float length = box != null ? box.size.y : 0f;
            float forearmWorld = length * Mathf.Abs(root.lossyScale.y);
            if (forearmWorld <= 0f) return false;

            Vector3 tip = end.TransformPoint(new Vector3(0f, -length, 0f));
            rise = (tip.y - end.position.y) / forearmWorld;
            return true;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
