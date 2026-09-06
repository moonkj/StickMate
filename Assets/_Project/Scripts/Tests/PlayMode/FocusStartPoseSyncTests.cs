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
    /// ★★★ 사용자 신고(2026-09-06): <i>"집중모드 시작시 캐릭터다리쪽에 원이 생김. 집중모드 행동을
    /// 해야하는데 안함"</i>. — <b>앞의 「원」은 같은 날 지시로 기능째 삭제됐다</b>(발밑 타이머 링 제거).
    /// 이 파일에 남은 것은 <b>뒤의 「행동을 안 함」</b> 하나다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 계약 — <b>시작 포즈는 「조용히 생략」되지 않는다</b>
    /// ============================================================================
    /// ★ <b>2026-09-06 개정</b>. 원래 이름은 <c>FocusRingPoseSyncTests</c>였고 «발밑 링이 세션이 아니라
    /// 확정된 행동에서 파생되는가»를 함께 잠갔다. 그날 사용자 지시로 <b>발밑 타이머 링이 삭제</b>돼
    /// (<c>Interaction/FocusWatchRenderer.cs</c> 파일째 제거) 링 쪽 단언은 전부 걷어냈다.
    /// <b>남은 절반이 이 파일의 전부이고, 그 절반이 원래 신고의 본체였다</b> — «집중모드 행동을
    /// 해야하는데 안함».
    ///
    /// <para>캐릭터의 시작 행동(안경+팔짱)은 관문을 통과해야 한다 — Idle/Walk일 것,
    /// <see cref="SpectacleEventLock"/>이 비어 있을 것. 관문이 막히면 포즈가 <b>조용히</b> 생략된다.
    /// 그래서 세 가지를 서로 다른 방법으로 잰다:</para>
    /// <list type="number">
    ///   <item><b>관문이 열려 있으면</b> — 포즈 전이가 확정되고 <b>손끝 Transform이 실제로
    ///     움직인다</b>(포즈 애니메이터의 내부 계산을 한 줄도 참조하지 않고 계층에서 직접 잰다 —
    ///     ParkourClimbPoseTests가 같은 이유로 같은 방식을 쓴다). "상태가 바뀌었다"만 재면 이번 버그의
    ///     <b>나머지 절반</b>(포즈를 그리는 코드가 저장소에 아예 없었다)을 그대로 놓친다.</item>
    ///   <item><b>락에 막히면</b> — 포즈가 확정되지 않는다. 그리고 락이 풀리는 순간 재시도 창이
    ///     포즈를 낸다.</item>
    ///   <item><b>Idle/Walk가 아니면</b> — 같은 형태로 막히고, 상태가 돌아오면 다시 시도한다.
    ///     그동안에도 <b>타이머는 계속 흐른다</b>(docs/UX_WIDGETS.md 369행 "위상 전이를 놓쳐도
    ///     타이머는 영향받지 않아야 한다" — 이 단언이 그 경계를 못박는다).</item>
    /// </list>
    ///
    /// <para><b>프로덕션 상수를 숫자로 베끼지 않는다</b>(CLAUDE.md): 자세 판정의 기준자는 전부 이 캐릭터의
    /// <b>실측 기하</b>(팔 길이·어깨/머리 Transform 위치)이고, 시간 예산은 <see cref="StickConfig"/>에서
    /// 읽는다. 포즈 각도 상수(<c>StickmanPoseAnimator.FocusCross*</c>)는 <b>일부러</b> 참조하지 않는다 —
    /// 그 값을 베끼면 "애니메이터가 자기 상수를 자기가 확인하는" 항상 참인 단언이 된다.</para>
    ///
    /// <para>시간 예산은 전부 <b>벽시계(초)</b>다(CLAUDE.md — 이 저장소의 배치모드 PlayMode는 수천 fps로
    /// 돌아서 프레임 수 예산이 밀리초가 된다).</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 창 열거/좌표계에 의존하지 않는다.</para>
    /// </summary>
    public sealed class FocusStartPoseSyncTests
    {
        private const string LogPrefix = "[집중시작포즈]";

        /// <summary>씬 부팅 직후 캐릭터는 낙하 중이다 — 접지/배회가 안정될 때까지의 상한(벽시계 초).</summary>
        private const float SettleTimeoutSeconds = 12f;

        /// <summary>
        /// 상태가 Idle/Walk가 된 <b>뒤에</b> 자세 자체가 중립으로 수렴할 때까지 더 기다리는 시간(초).
        ///
        /// <para>★ 이 상수가 없어서 이 테스트가 한 번 빨갛게 났다(실측): 씬 부팅 직후 캐릭터는 착지하며
        /// <b>무릎앉아 착지</b> 연출을 지나는데, 상태는 곧바로 Idle이 되지만 팔 각도는 지수 감쇠라
        /// 몇 백 ms 동안 그 브레이스 자세(팔이 앞으로 45도)에 남아 있다. 그 순간을 "중립"이라고 재면
        /// 손끝이 이미 팔 길이의 71% 앞에 나가 있다 — 기준선이 오염된다.</para>
        /// </summary>
        private const float PoseSettleSeconds = 1.5f;

        /// <summary>중립 기준선을 평균 내는 구간(초). <b>평균</b>인 이유: 캐릭터가 Walk로 안정되면 팔이
        /// 앞뒤로 스윙하므로 한 프레임 표본은 스윙 위상에 따라 튄다(부호 있는 평균은 0으로 상쇄된다).</summary>
        private const float BaselineSampleSeconds = 0.5f;

        /// <summary>이 테스트가 시작하는 세션 길이(분). 하한(<c>MinimumSessionSeconds</c> = 60초)에
        /// 걸리므로 실제로는 60초짜리 세션이고, 어느 단언도 세션이 끝나기를 기다리지 않는다.</summary>
        private const float SessionMinutes = 1f;

        // ============================================================================
        // ★ 무엇을 재면 「팔짱」인가 — <b>"손이 앞으로 나갔는가"는 답이 아니다</b>(실측으로 반증됨)
        // ============================================================================
        // 첫 시도는 "손끝이 어깨보다 앞에 있는가"로 쟀고 <b>중립에서도 이미 참</b>이라 무너졌다:
        // 이 캐릭터의 Idle 중립은 팔을 곧게 내린 자세가 아니라 <c>idleArmSpreadDegrees</c>(출하 40°)만큼
        // 벌린 자세라, 2D 측면도에서 그 "벌림"이 곧 <b>앞</b>이다. 실측 손끝은 어깨보다 0.40유닛(팔 길이의
        // 71%) 앞에 있었다.
        //
        // 팔짱을 중립과 갈라놓는 것은 <b>전완의 방향</b>이다:
        //   중립  — 전완이 아래앞으로 처진다(어깨 40° + 팔꿈치 10° = 전완 절대각 50° -> 손이 팔꿈치보다
        //           전완 길이의 0.64배 <b>아래</b>).
        //   팔짱  — 전완이 <b>수평으로</b> 가슴 앞을 가로지른다(절대각 90° 근처 -> 손이 팔꿈치와 거의 같은
        //           높이거나 위).
        // 그래서 지표를 «(손끝 y − 팔꿈치 y) ÷ 전완 길이»로 잡는다. 두 자세의 값이 0.7 이상 벌어져 있어
        // 포즈가 덜 수렴해도 오판하지 않는다.

        /// <summary>「팔짱」으로 인정하는 전완 기울기의 하한(전완 길이 대비 손끝−팔꿈치 높이차).
        /// 완전히 수렴한 팔짱은 +0.10 근처이고 중립은 −0.64 근처다 — 그 사이에서 중립 쪽으로 넉넉히
        /// 떨어진 곳에 선을 긋는다.</summary>
        private const float CrossForearmRiseFloor = -0.15f;

        /// <summary>중립 기준선이 "팔을 내린 자세"라고 인정하는 상한(같은 지표). 이 값보다 커버리면
        /// 기준선이 이미 팔짱과 구분되지 않는다는 뜻이라 아래 단언이 무의미해진다(양성 대조).</summary>
        private const float NeutralForearmRiseCeiling = -0.40f;

        private StickmanAgent _agent;
        private FocusWatchDirector _director;

        [TearDown]
        public void TearDown()
        {
            // 전역 정적 상태(락)와 진행 중인 세션은 씬 생명주기와 무관하게 살아남는다 —
            // 여기서 안 걷으면 뒤이어 도는 다른 테스트가 "왜 내 연출이 안 뜨지"로 빨개진다.
            if (_director != null) _director.StopFocusSession();
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null &&
                _agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
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
        // ① 관문이 열려 있으면 — 포즈가 확정되고 손이 실제로 움직인다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 관문이_열려있으면_시작포즈가_확정되고_손이_실제로_움직인다()
        {
            yield return LoadSceneAndSettle();

            Assert.IsFalse(_director.IsSessionActive, $"{LogPrefix} 시작 전인데 세션이 이미 진행 중입니다.");

            // ── 중립 기준선(양성 대조): 직전 연출(무릎앉아 착지)의 자세가 완전히 풀린 뒤에 잰다.
            float settle = 0f;
            while (settle < PoseSettleSeconds)
            {
                settle += Time.deltaTime;
                yield return null;
            }

            float riseSum = 0f;
            float reachSum = 0f;
            int samples = 0;
            float sampled = 0f;
            float neutralShoulderY = 0f;
            while (sampled < BaselineSampleSeconds)
            {
                // 유휴 앰비언트 "주위 살피기"는 한쪽 팔을 이마에 얹는다 — 그 프레임은 중립이 아니다.
                if (!_agent.Blackboard.IsIdleAmbientMotionActive && TryArmGeometry(out ArmSample s))
                {
                    riseSum += s.ForearmRise;
                    reachSum += s.ArmReach;
                    neutralShoulderY = s.Shoulder.y;
                    samples++;
                }
                sampled += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(samples, 0,
                $"{LogPrefix} 오른팔 Transform(RightArm/RightArmLower)을 한 프레임도 읽지 못했습니다 — " +
                "이 리그에서는 자세를 기하로 잴 수 없습니다.");

            float armReach = reachSum / samples;
            float neutralRise = riseSum / samples;   // 보행 스윙은 부호 있는 평균에서 상쇄된다.
            Assert.Greater(armReach, 0f, $"{LogPrefix} 팔 길이 실측이 0입니다.");
            Debug.Log($"{LogPrefix} 중립 기준선 — {DescribeArm()} / 전완 기울기 {neutralRise:F3}(전완 길이 대비), " +
                $"팔 길이 {armReach:F3}유닛, 표본 {samples}개, 상태 {_agent.Blackboard.Machine.CurrentStateId}.");
            Assert.Less(neutralRise, NeutralForearmRiseCeiling,
                $"{LogPrefix} 중립 자세인데 전완이 이미 수평에 가깝습니다(기울기 {neutralRise:F3}) — " +
                "이 지표가 두 자세를 구분하지 못한다는 뜻이라 아래 단언이 무의미해집니다.");

            _director.StartFocusSession(SessionMinutes);
            yield return null;

            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 관문이 열려 있었는데 시작 포즈가 확정되지 않았습니다.");
            Assert.AreEqual(StickmanStateId.FocusStart, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 상태가 FocusStart가 아닙니다 — 대사만 뜨고 행동이 없는 그 상태입니다.");

            // ── 자세 표본: 상태가 유지되는 동안 매 프레임 팔을 훑는다.
            //    「안경 밀어올리기」 = 손끝이 어깨 위로 올라가 얼굴에 닿는다.
            //    「팔짱」          = 전완이 수평으로 눕는다(손이 팔꿈치와 같은 높이 근처로 온다).
            float budget = Mathf.Max(1f, _agent.Config.pomodoroStartPoseHoldSeconds) * 3f;
            float elapsed = 0f;
            float maxHandY = float.NegativeInfinity;
            float finalForearmRise = float.NaN;
            float faceY = FaceWorldY();
            float shoulderY = neutralShoulderY;

            while (elapsed < budget && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.FocusStart)
            {
                if (TryArmGeometry(out ArmSample s))
                {
                    maxHandY = Mathf.Max(maxHandY, s.Tip.y);
                    // ★ 팔짱은 <b>최댓값이 아니라 마지막 값</b>으로 잰다. 최댓값으로 재면 「안경」 박자에서
                    //   손이 얼굴로 올라가는 도중의 프레임이 그대로 잡혀, 팔짱을 한 줄도 그리지 않아도
                    //   통과한다(실측으로 확인: 최댓값 0.653은 안경 박자의 값이었다). 연출이 끝나는
                    //   순간의 자세가 곧 사용자가 "한 장의 그림"으로 읽는 그 포즈다.
                    finalForearmRise = s.ForearmRise;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(float.IsNaN(finalForearmRise), $"{LogPrefix} 포즈 상태에서 팔을 한 번도 읽지 못했습니다.");

            float halfwayToFace = shoulderY + (faceY - shoulderY) * 0.5f;
            Assert.Greater(maxHandY, halfwayToFace,
                $"{LogPrefix} 「안경 밀어올리기」가 그려지지 않았습니다 — 손끝 최고 높이 {maxHandY:F3}이 " +
                $"어깨({shoulderY:F3})와 얼굴({faceY:F3})의 중간({halfwayToFace:F3})에도 못 미칩니다.");
            Assert.Greater(finalForearmRise, CrossForearmRiseFloor,
                $"{LogPrefix} 「팔짱」이 그려지지 않았습니다 — 연출이 끝나는 순간의 전완 기울기가 " +
                $"{finalForearmRise:F3}으로 중립({neutralRise:F3})에서 거의 움직이지 않았습니다. 상태 전이만 " +
                "되고 포즈를 그리는 코드가 없으면 정확히 이 값이 중립 수준으로 남습니다(이번 신고의 나머지 절반).");

            // 2초짜리 포즈가 끝나도 세션은 계속된다(포즈는 시작의 표현일 뿐 세션의 수명이 아니다).
            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} 포즈가 끝나면서 세션까지 끝났습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 손끝 최고 {maxHandY:F3}(얼굴 {faceY:F3}, 어깨 {shoulderY:F3}), " +
                $"전완 기울기 중립 {neutralRise:F3} -> 팔짱(연출 종료 시점) {finalForearmRise:F3}, 팔 길이 {armReach:F3}.");
        }

        // ================================================================================
        // ② 락에 막히면 포즈가 확정되지 않는다 — 그리고 락이 풀리면 재시도가 포즈를 낸다.
        // ================================================================================

        [UnityTest]
        public IEnumerator 락에_막히면_포즈가_확정되지_않고_락이_풀리면_재시도가_포즈를_낸다()
        {
            yield return LoadSceneAndSettle();

            var blocker = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, blocker),
                $"{LogPrefix} 테스트가 락을 잡지 못했습니다 — 이 시나리오를 재현할 수 없습니다.");

            _director.StartFocusSession(SessionMinutes);
            yield return null;
            yield return null;

            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} 세션이 시작되지 않았습니다.");
            Assert.IsFalse(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 락이 걸려 있는데 시작 포즈가 확정됐다고 보고합니다.");
            Assert.AreNotEqual(StickmanStateId.FocusStart, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 락을 무시하고 상태를 빼앗았습니다.");

            // ★ 타이머 계약(docs/UX_WIDGETS.md 369): 포즈를 놓쳐도 시간은 그대로 흐른다.
            float before = _director.RemainingSeconds;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(_director.RemainingSeconds, before,
                $"{LogPrefix} 포즈가 스킵된 동안 타이머가 멈췄습니다({before:F2}초 그대로) — 포즈를 놓쳐도 " +
                "시간은 그대로 흘러야 합니다.");
            Assert.IsFalse(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 락이 그대로인데 포즈가 뒤늦게 확정됐습니다.");

            // 락 해제 -> 재시도 창이 관문이 열린 것을 보고 포즈를 낸다.
            SpectacleEventLock.Release(blocker);
            float waited = 0f;
            while (waited < 2f && !_director.IsStartPoseConfirmed)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return null;

            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} 락이 풀렸는데도 시작 포즈를 다시 시도하지 않았습니다(재시도 창이 죽었습니다).");
            Assert.AreEqual(StickmanStateId.FocusStart, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 확정됐다고 보고하는데 상태는 FocusStart가 아닙니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 락 점유 중 포즈 미확정 · 타이머 정상 진행, " +
                $"락 해제 후 {waited:F2}초 만에 포즈 확정.");
        }

        // ================================================================================
        // ③ Idle/Walk가 아니면 포즈가 확정되지 않는다 — 상태가 돌아오면 재시도가 포즈를 낸다.
        // ================================================================================

        [UnityTest]
        public IEnumerator IdleWalk가_아니면_포즈가_확정되지_않고_상태가_돌아오면_재시도가_포즈를_낸다()
        {
            yield return LoadSceneAndSettle();

            // 관문의 "Idle/Walk" 조건만 골라 막는다. Attack을 쓰는 이유: 런타임 생산자가 0개라
            // (States/AttackState.cs 클래스 문서) 다른 Director가 이 전이에 반응하지 않는다.
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Attack);
            yield return null;
            Assert.AreEqual(StickmanStateId.Attack, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 상태를 Attack으로 만들지 못했습니다 — 이 시나리오를 재현할 수 없습니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                $"{LogPrefix} 락까지 걸려 버리면 어느 조건이 막았는지 구분되지 않습니다.");

            _director.StartFocusSession(SessionMinutes);
            yield return null;
            yield return null;

            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} 세션이 시작되지 않았습니다.");
            Assert.IsFalse(_director.IsStartPoseConfirmed,
                $"{LogPrefix} Idle/Walk가 아닌데 시작 포즈가 확정됐다고 보고합니다.");
            Assert.AreNotEqual(StickmanStateId.FocusStart, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 관문을 무시하고 상태를 빼앗았습니다.");

            float before = _director.RemainingSeconds;
            yield return new WaitForSeconds(0.4f);
            Assert.Less(_director.RemainingSeconds, before,
                $"{LogPrefix} 포즈가 스킵된 동안 타이머가 멈췄습니다 — 타이머는 위상 전이와 무관해야 합니다.");

            // 상태를 되돌리면(= 관문이 열리면) 재시도가 그 프레임에 포즈를 낸다.
            //
            // ★ <b>이미 확정됐으면 건드리지 않는다</b>(2026-09-06 실측으로 배운 것). Attack은 스스로
            //   만료돼 Idle로 돌아오는 타이머 상태라, 바로 위 0.4초 대기 중에 관문이 저절로 열려
            //   재시도가 <b>먼저</b> 성공해 있을 수 있다. 그때 무조건 Idle로 강제 전이하면 이 테스트가
            //   방금 확인하려던 FocusStart를 <b>자기 손으로 걷어낸다</b>.
            if (!_director.IsStartPoseConfirmed
                && _agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            float waited = 0f;
            while (waited < 2f && !_director.IsStartPoseConfirmed)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return null;

            // ★ 판정은 <see cref="FocusWatchDirector.IsStartPoseConfirmed"/> <b>하나</b>로 한다.
            //   그 값은 디렉터가 «ChangeState를 불렀다»가 아니라 «상태머신이 실제로 그 상태를 들고
            //   있더라»를 확인한 뒤에만 참이 되므로(그 프로퍼티 문서), «포즈가 실제로 났다»의
            //   <b>정본</b>이다. 여기서 CurrentStateId를 한 번 더 보는 것은 2초짜리 포즈가 이미
            //   끝났을 수도 있는 <b>경합</b>을 하나 더 만드는 것뿐이라 일부러 재지 않는다.
            Assert.IsTrue(_director.IsStartPoseConfirmed,
                $"{LogPrefix} Idle로 돌아왔는데도 시작 포즈를 다시 시도하지 않았습니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — Attack 중 포즈 미확정 · 타이머 정상 진행, " +
                $"Idle 복귀 후 {waited:F2}초 만에 포즈 확정.");
        }

        // ================================================================================
        // 기하 헬퍼 — 포즈 애니메이터의 계산을 참조하지 않고 계층에서 직접 잰다.
        // ================================================================================

        /// <summary>한 프레임의 오른팔 실측 — 전부 계층에서 직접 읽은 <b>월드</b> 좌표다.</summary>
        private readonly struct ArmSample
        {
            public readonly Vector3 Shoulder;
            public readonly Vector3 Elbow;
            public readonly Vector3 Tip;

            /// <summary>어깨에서 손끝까지의 직선 거리(= 지금 실제로 뻗은 팔 길이).</summary>
            public readonly float ArmReach;

            /// <summary><b>전완의 기울기</b>: (손끝 y − 팔꿈치 y) ÷ 전완 길이. −1이면 전완이 곧게 아래,
            /// 0이면 정확히 수평, +1이면 곧게 위. 「팔짱」과 「팔을 내린 중립」을 가르는 지표다.</summary>
            public readonly float ForearmRise;

            public ArmSample(Vector3 shoulder, Vector3 elbow, Vector3 tip, float forearmLength)
            {
                Shoulder = shoulder;
                Elbow = elbow;
                Tip = tip;
                ArmReach = Vector3.Distance(shoulder, tip);
                ForearmRise = forearmLength > 0f ? (tip.y - elbow.y) / forearmLength : 0f;
            }
        }

        /// <summary>오른팔의 어깨(위 마디 원점) / 팔꿈치(아래 마디 원점) / 손끝 월드 좌표. 마디의 원점이
        /// 관절이고 선이 로컬 −y로 length만큼 그려지므로(Editor/SceneBootstrapper.CreateLimbSegment)
        /// 끝점의 로컬 좌표는 (0, −length)다. TransformPoint를 쓰므로 루트 스케일까지 자동으로 반영된다
        /// (ParkourClimbPoseTests.TryLimbTipWorld와 같은 방식).</summary>
        private bool TryArmGeometry(out ArmSample sample)
        {
            sample = default;
            Transform root = _agent != null && _agent.Blackboard != null && _agent.Blackboard.Body != null
                ? _agent.Blackboard.Body.transform
                : null;
            if (root == null) return false;

            Transform upper = root.Find("RightArm");
            if (upper == null) return false;
            Transform end = upper.Find("RightArmLower") ?? upper;
            var box = end.GetComponent<BoxCollider2D>();
            float length = box != null ? box.size.y : 0f;
            // 마디 길이는 루트 로컬 유닛이라 월드로 환산해야 위 y 차이와 단위가 맞는다.
            float forearmWorld = length * Mathf.Abs(root.lossyScale.y);

            sample = new ArmSample(upper.position, end.position,
                end.TransformPoint(new Vector3(0f, -length, 0f)), forearmWorld);
            return true;
        }

        /// <summary>지금 오른팔이 실제로 어떤 모양인지(진단 로그 전용). 자세 단언이 빨개졌을 때
        /// "어느 각도가 그런 손끝 좌표를 만들었는가"를 로그만 보고 알 수 있어야 한다.</summary>
        private string DescribeArm()
        {
            Transform root = _agent.Blackboard.Body.transform;
            Transform upper = root.Find("RightArm");
            if (upper == null) return "RightArm 없음";
            Transform lower = upper.Find("RightArmLower");
            var upperBox = upper.GetComponent<BoxCollider2D>();
            var lowerBox = lower != null ? lower.GetComponent<BoxCollider2D>() : null;
            StickmanPoseAnimator pose = _agent.Blackboard.GetPoseAnimator();
            return $"어깨각 {Mathf.DeltaAngle(0f, upper.localEulerAngles.z):F1}도 · 팔꿈치각 " +
                $"{(lower != null ? Mathf.DeltaAngle(0f, lower.localEulerAngles.z).ToString("F1") : "없음")}도 · " +
                $"상완 {(upperBox != null ? upperBox.size.y : 0f):F3} / 전완 " +
                $"{(lowerBox != null ? lowerBox.size.y : 0f):F3} · 방향 {(pose != null ? pose.FacingSign : 0f):F0} · " +
                $"루트회전 {Mathf.DeltaAngle(0f, root.eulerAngles.z):F1}도 · 루트배율 {root.lossyScale.y:F3}";
        }

        /// <summary>얼굴(눈높이)의 월드 Y — 머리 Transform 실측. 못 찾으면 캐릭터 실측 치수로 폴백한다.</summary>
        private float FaceWorldY()
        {
            Transform root = _agent.Blackboard.Body.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == "Head") return child.position.y;
            }
            StickmanMetrics m = _agent.Metrics;
            float local = m != null ? m.HeadCenterLocalY : 0f;
            return root.position.y + local;
        }
    }
}
