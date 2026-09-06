using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 음악 반응 춤 — <b>화면에 실제로 나오는가</b>(2026-09-06 신설).
    ///
    /// ============================================================================
    /// 이 테스트가 막는 결함은 <b>이미 세 번 났다</b>
    /// ============================================================================
    /// 상태는 정상 전이하고 로그도 대사도 뜨는데 <b>화면에서는 아무 일도 일어나지 않는</b> 결함이
    /// 이 저장소에서 반복됐다: 등반이 «차렷 자세로 평행이동»했고(2026-09-01), 집중 모드가 «대사만
    /// 뜨고 포즈가 없었고»(2026-09-06), 둘 다 원인이 같다 —
    /// <c>StickmanBlackboard.TickPoseRouting</c>의 마지막 줄(<c>ApplyIdlePose</c>)이 매 프레임 중립
    /// 포즈를 덧씌웠다. 춤은 <b>7종 전부</b>가 그 함정 위에 있다.
    ///
    /// <para>그래서 «상태가 Dance가 됐다»를 재지 않는다. <b>관절 각도를 직접 잰다</b> —
    /// 상태 전이는 성공했는데 그림이 없는 실패와 성공이 그것 말고는 구분되지 않는다.</para>
    ///
    /// ============================================================================
    /// 왜 하필 프리샤트카(D6)로 재는가
    /// ============================================================================
    /// 이 검사는 <b>위상에 의존하지 않아야</b> 한다(어느 프레임에 재느냐로 결과가 달라지면 그건
    /// 불안정한 테스트다). D6의 지지 무릎 132°는 루프 내내 <b>붙잡고 있는</b> 값이고, 유지 자세에는
    /// 보간 감쇠 오차가 0이라(지수 감쇠는 정지 목표에 정확히 수렴한다) 중립 4°와 33배 벌어진다.
    /// 다른 6종은 진동 신호가 섞여 있어 표본 시점에 따라 진폭이 흔들린다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(자세·박자 경로에 플랫폼 분기가 없다). 1층 감지만 갈라지는데
    /// 이 테스트는 1층을 타지 않는다 — 에디터에는 프로브가 없어 창이 영원히 닫힌 채이므로, 상태를
    /// 직접 전이시켜 3층만 잰다.</para>
    /// </summary>
    public sealed class DancePoseRoutingTests
    {
        private const string LogPrefix = "[춤포즈-TEST]";

        /// <summary>씬 부팅 직후 캐릭터는 낙하 중이다 — 접지/배회가 안정될 때까지의 상한(벽시계 초).</summary>
        private const float SettleTimeoutSeconds = 12f;

        /// <summary>상태가 Idle/Walk가 된 뒤 <b>자세 자체</b>가 중립으로 수렴할 때까지 더 기다리는 시간(초).
        /// 착지 무릎앉아의 잔상이 남은 순간을 «중립»이라고 재면 기준선이 오염된다
        /// (FocusRingPoseSyncTests가 실제로 그렇게 한 번 빨개졌다).</summary>
        private const float PoseSettleSeconds = 1.5f;

        /// <summary>중립 기준선을 평균 내는 구간(초).</summary>
        private const float BaselineSampleSeconds = 0.5f;

        /// <summary>춤 자세를 관측하는 구간(초). D6 진입 박자 0.84초 + 루프 0.84초를 넉넉히 덮는다.</summary>
        private const float DanceSampleSeconds = 2.5f;

        /// <summary>「중립」으로 인정하는 무릎 굽힘의 상한(도). 출하 <c>idleKneeBendDegrees</c>는 4°다 —
        /// 그보다 훨씬 위에 선을 그어 두고, 여기 걸리면 <b>기준선이 오염됐다</b>는 뜻이라 단언한다(양성 대조).</summary>
        private const float NeutralKneeCeilingDegrees = 20f;

        /// <summary>「완전 스쾃」으로 인정하는 무릎 굽힘의 하한(도). 사양은 132°이고 진입 램프·진폭
        /// 흔들림을 감안해도 이 아래로는 내려오지 않는다. 중립 상한(20°)과 넉넉히 벌어져 있어
        /// 포즈가 덜 수렴해도 오판하지 않는다.</summary>
        private const float SquatKneeFloorDegrees = 70f;

        private StickmanAgent _agent;

        [TearDown]
        public void TearDown()
        {
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null &&
                _agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            _agent = null;
        }

        private IEnumerator LoadSceneAndSettle()
        {
            SpectacleEventLock.Release(SpectacleEventLock.CurrentOwner);
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");

            float elapsed = 0f;
            while (elapsed < SettleTimeoutSeconds)
            {
                StickmanStateId id = _agent.Blackboard.Machine.CurrentStateId;
                if ((id == StickmanStateId.Idle || id == StickmanStateId.Walk) && !SpectacleEventLock.IsActive)
                {
                    // 상태가 안정된 뒤에도 각도는 지수 감쇠라 잔상이 남는다 — 더 기다린다.
                    float settle = 0f;
                    while (settle < PoseSettleSeconds)
                    {
                        settle += Time.deltaTime;
                        yield return null;
                    }
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.Fail($"{LogPrefix} {SettleTimeoutSeconds:F0}초 안에 캐릭터가 Idle/Walk로 안정되지 않았습니다 " +
                $"(지금 {_agent.Blackboard.Machine.CurrentStateId}, 락 {SpectacleEventLock.IsActive}).");
        }

        /// <summary>지금 프레임의 무릎 굽힘 최댓값(도, 절댓값). 부호 규약(KneeBendSign)과 무관하게 재려고
        /// 절댓값을 쓴다 — 이 검사가 묻는 것은 «굽었는가»이지 «어느 쪽으로»가 아니다.</summary>
        private float MaxKneeBendDegrees()
        {
            StickmanPoseAnimator pose = _agent.Blackboard.GetPoseAnimator();
            Assert.IsNotNull(pose, $"{LogPrefix} 포즈 애니메이터를 찾지 못했습니다.");
            pose.GetJointAngles(out float leftKnee, out float rightKnee, out _, out _);
            return Mathf.Max(Mathf.Abs(leftKnee), Mathf.Abs(rightKnee));
        }

        // ================================================================================
        // ① 포즈가 중립으로 덮이지 않는가 — 이 기능의 존폐가 걸린 한 줄
        // ================================================================================

        [UnityTest]
        public IEnumerator 춤_상태에서_포즈가_매프레임_중립으로_덮이지_않는다()
        {
            yield return LoadSceneAndSettle();

            // ── 중립 기준선(양성 대조). 여기서 이미 크면 아래 단언이 무의미하다.
            float baselineSum = 0f;
            int baselineSamples = 0;
            float t = 0f;
            while (t < BaselineSampleSeconds)
            {
                baselineSum += MaxKneeBendDegrees();
                baselineSamples++;
                t += Time.deltaTime;
                yield return null;
            }
            float baseline = baselineSum / Mathf.Max(1, baselineSamples);
            Assert.Less(baseline, NeutralKneeCeilingDegrees,
                $"{LogPrefix} 중립 기준선의 무릎 굽힘이 {baseline:F1}°로 이미 큽니다 — 기준선이 오염됐고 " +
                "아래 단언이 «춤 자세를 잡았다»와 «원래부터 굽어 있었다»를 구분하지 못합니다.");

            // ── 춤(프리샤트카). 진입 대사가 섞이지 않도록 창의 두 번째 에피소드로 둔다.
            _agent.Blackboard.DanceId = DanceIds.Prisyadka;
            _agent.Blackboard.DanceEpisodeIndex = 2;
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Dance);
            Assert.AreEqual(StickmanStateId.Dance, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} Dance로 전이하지 못했습니다 — StickmanAgent의 상태 표에 등록되지 않았다면 " +
                "ChangeState가 BUG-M2 방어 코드를 밟아 연출이 통째로 사라집니다.");

            float peak = 0f;
            t = 0f;
            while (t < DanceSampleSeconds && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Dance)
            {
                peak = Mathf.Max(peak, MaxKneeBendDegrees());
                t += Time.deltaTime;
                yield return null;
            }

            Debug.Log($"{LogPrefix} 무릎 굽힘 — 중립 {baseline:F1}° → 프리샤트카 최대 {peak:F1}° " +
                $"({peak / Mathf.Max(0.01f, baseline):F1}배).");

            Assert.Greater(peak, SquatKneeFloorDegrees,
                $"{LogPrefix} 춤 중인데 무릎이 {peak:F1}°밖에 굽지 않았습니다(사양 132°). " +
                "StickmanBlackboard.TickPoseRouting에 Dance 분기가 없으면 ApplyIdlePose가 매 프레임 " +
                "중립 포즈를 덧씌워 정확히 이 그림이 나옵니다 — 등반이 «차렷 자세로 평행이동»했던 것, " +
                "집중 모드가 «대사만 뜨고 아무 동작도 없었던» 것과 같은 계열의 결함입니다.");
        }

        // ================================================================================
        // ② 대사 — 창의 첫 에피소드에서만, 그리고 상태가 끝나면 즉시 컷
        // ================================================================================

        [UnityTest]
        public IEnumerator 창의_첫_에피소드에서만_진입_대사가_뜨고_상태가_끝나면_즉시_컷된다()
        {
            yield return LoadSceneAndSettle();

            var spoken = new List<DialogueIntent>();
            var expired = new List<DialogueIntent>();
            void OnRequested(DialogueIntent intent) => spoken.Add(intent);
            void OnExpired(DialogueIntent intent) => expired.Add(intent);

            StickmanEventBus.DialogueRequested += OnRequested;
            StickmanEventBus.DialogueExpired += OnExpired;
            try
            {
                // ── 창의 첫 에피소드(n = 1) — 대사가 한 번 뜬다.
                _agent.Blackboard.DanceId = DanceIds.Prisyadka;
                _agent.Blackboard.DanceEpisodeIndex = 1;
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Dance);
                yield return null;

                Assert.AreEqual(1, spoken.Count,
                    $"{LogPrefix} 창의 첫 에피소드에서 진입 대사가 {spoken.Count}건입니다(1건이어야 합니다).");
                Assert.AreEqual(StickmanStateId.Dance, spoken[0].StateId,
                    $"{LogPrefix} 대사가 Dance 상태에서 파생되지 않았습니다 — 원칙 1 위반입니다.");
                Assert.AreEqual(DialogueKind.Narrative, spoken[0].Kind,
                    $"{LogPrefix} 진입 대사의 종류가 Narrative가 아닙니다. Reaction이면 상태가 끝난 뒤에도 " +
                    "최소 노출을 채우고 나가 **휴지 구간으로 샙니다** — 그때의 사실은 «춤추는 중»이 아니라 " +
                    "«서 있는 중»이라 원칙 1 위반이 됩니다.");
                string line = spoken[0].Text;
                Assert.IsNotEmpty(line, $"{LogPrefix} 진입 대사가 빈 문자열입니다.");

                // ── 상태가 끝나면 즉시 컷된다(Narrative의 정의).
                int expiredBefore = expired.Count;
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return null;
                Assert.Greater(expired.Count, expiredBefore,
                    $"{LogPrefix} Dance에서 빠졌는데 대사가 만료되지 않았습니다 — 말풍선이 휴지 구간까지 남습니다.");
                Assert.IsFalse(spoken[0].IsValid,
                    $"{LogPrefix} Dance에서 빠졌는데 대사가 아직 유효합니다.");

                // ── 같은 창의 두 번째 에피소드(n = 2) — 대사가 뜨지 않는다.
                //    (에피소드마다 말하면 3시간 감상에서 같은 문장이 102번 뜬다 — 리더 결정 2026-09-06.)
                spoken.Clear();
                _agent.Blackboard.DanceEpisodeIndex = 2;
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Dance);
                yield return null;
                Assert.AreEqual(0, spoken.Count,
                    $"{LogPrefix} 창의 두 번째 에피소드에서 진입 대사가 {spoken.Count}건 떴습니다(0건이어야 합니다) — " +
                    "같은 문장이 3시간에 102번 뜹니다.");

                // ── 창이 다시 열려 n이 1로 되돌아가면 대사도 다시 뜬다(회귀 방지: 「한 번 뜨면 끝」이 아니다).
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return null;
                spoken.Clear();
                _agent.Blackboard.DanceEpisodeIndex = 1;
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Dance);
                yield return null;
                Assert.AreEqual(1, spoken.Count,
                    $"{LogPrefix} 새 창의 첫 에피소드에서 진입 대사가 다시 뜨지 않았습니다 — " +
                    "피로 카운터가 1로 되돌아가면 대사도 함께 되살아나야 합니다.");
                Assert.AreEqual(line, spoken[0].Text,
                    $"{LogPrefix} 같은 상황인데 대사 문안이 달라졌습니다.");

                Debug.Log($"{LogPrefix} 진입 대사 «{line}» — 창당 1회 게이트와 즉시 컷을 모두 확인했습니다.");
            }
            finally
            {
                StickmanEventBus.DialogueRequested -= OnRequested;
                StickmanEventBus.DialogueExpired -= OnExpired;
            }
        }
    }
}
