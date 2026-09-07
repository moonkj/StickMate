using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ LFVS(저프레임 벡터 스텝)의 <b>실물</b> — 리더가 재정의한 <b>L-5 「포즈」 반쪽</b>과
    /// 그 위에 서는 <b>L-1</b>(스텝 사이 프레임의 쓰기 = 0).
    /// 정본: <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 5-3·5-5절 /
    /// <c>docs/UX_MOTION_COSTUME_FOCUS.md</c>.
    ///
    /// ============================================================================
    /// 「썼는가」의 <b>정본은 누적 계기</b>다 — 순간값이 아니다
    /// ============================================================================
    /// 표본마다 <see cref="StickmanBlackboard.CostumePoseWriteCount"/>의 <b>증가분</b>으로
    /// 「이 프레임에 썼는가」를 판정한다. 누적값은 <b>단조</b>라 구조적으로 낡을 수 없는 반면,
    /// 순간값(<c>CostumePoseWroteThisFrame</c>)은 <b>포즈 층이 도는 프레임에서만</b> 갱신된다.
    /// 두 값을 <b>둘 다</b> 읽어 어긋남을 세는 것이 이 파일의 두 번째 검사다.
    ///
    /// ============================================================================
    /// 양성과 음성을 <b>같은 실행에서</b>
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>양성</b> — 스텝 번호가 바뀌는 프레임에 <b>쓴다</b>.</item>
    ///   <item><b>음성</b> — 스텝 번호가 그대로인 프레임에는 <b>한 번도 안 쓴다</b>.
    ///     이것이 «보간 없이 즉시 전환»이 만드는 절감의 전부다(설계 5-3절:
    ///     스무딩을 남겨 두면 20° 스텝이 333ms 중 183ms를 이징에 쓰고 그 동안 매 프레임 다시 굽는다).</item>
    /// </list>
    /// </summary>
    public sealed class CostumeFocusLfvsPoseTests
    {
        private const string LogPrefix = "[코스튬포즈-TEST]";

        /// <summary>세션 시간 압축 배율(<c>CostumeFocusPropLifecycleTests</c>와 같은 값·같은 이유).
        /// 프레임 페이싱 절감 문턱은 여기서 재지 않는다.</summary>
        private const float TimeCompression = 4f;

        private float _savedTimeScale = 1f;

        private struct Sample
        {
            public int Step;
            public int SubPhase;
            public bool Flag;
            public int Count;

            /// <summary>★ <b>독립된 두 번째 자</b> — <c>LimbCurveRenderer.LastRebuiltSegmentCount</c>.
            /// 블랙보드의 누적 계기와 <b>다른 곳에서</b> 나오므로, 둘이 같은 말을 한다는 것이
            /// 어느 한쪽이 굳어 있지 않다는 증거가 된다. 이것이 합격선 L-1의 <b>문자 그대로의 계기</b>다.
            /// <para><b>한 프레임 어긋난다</b>: 포즈는 <c>StickmanAgent.Update</c>에서 쓰고 마디 굽기는
            /// <c>LimbCurveRenderer.LateUpdate</c>에서 돈다. 그래서 <b>프레임 대 프레임으로 짝짓지 않고</b>
            /// 구간 합계로만 비교한다 — 정렬 가정에 기대지 않기 위해서다.</para></summary>
            public int Rebuilt;

            /// <summary>이 프레임에 <b>앰비언트 제스처</b>가 돌고 있었는가. 그때는 코스튬 층이
            /// 아예 호출되지 않으므로(<c>!gesturing</c> 가드) 그 프레임의 스텝·순간값은
            /// <b>직전 값이 그대로 남은 것</b>이다 — 표본에서 빼지 않으면 그 잔상이 판정을 오염시킨다.</summary>
            public bool Gesturing;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CostumeFocusRig.RestoreGlobals(_savedTimeScale);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 코스튬_포즈는_스텝이_바뀌는_프레임에만_쓴다()
        {
            yield return CostumeFocusRig.LoadSceneAndSettle();

            StickmanAgent agent = CostumeFocusRig.Agent();
            StickmanBlackboard bb = agent.Blackboard;
            FocusWatchDirector director = CostumeFocusRig.Director();
            CostumePropRenderer prop = CostumeFocusRig.EnsureCostumeProp(agent);

            CostumeDescriptor costume = CostumeFocusRig.WearOfficeCostume(agent.Config);
            Assert.IsNotNull(costume.Keyposes,
                $"{LogPrefix} '{costume.CostumeKey}'에 키포즈 표가 없습니다 — 전용 모션이 없는 코스튬은 " +
                "LFVS를 잴 대상이 아닙니다(그 자체는 정상 상태지만 이 검사는 다른 코스튬이 필요합니다).");
            Assert.IsTrue(costume.Keyposes.IsUsable(out string tableError),
                $"{LogPrefix} 키포즈 표가 실릴 수 없는 상태입니다 — {tableError}");

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            yield return CostumeFocusRig.StartAndReachImmersion(director, prop, TimeCompression);

            Assert.IsTrue(bb.IsCostumeImmersionActive,
                $"{LogPrefix} 프롭은 섰는데 IsCostumeImmersionActive가 거짓입니다 — " +
                "판정이 두 곳으로 갈라졌습니다(P-GHOST-1).");

            // ★ 두 번째 자 — 팔다리를 실제로 다시 굽는 그 컴포넌트.
            LimbCurveRenderer limbs = Object.FindFirstObjectByType<LimbCurveRenderer>();
            Assert.IsNotNull(limbs, $"{LogPrefix} 씬에 LimbCurveRenderer가 없습니다 — L-1을 잴 계기가 없습니다.");
            Assert.Greater(limbs.TrackedLimbCount, 0,
                $"{LogPrefix} LimbCurveRenderer가 추적하는 팔다리가 0개입니다 — 계기가 죽어 있습니다.");

            // ★ 몰입기에 들어선 순간 캐릭터가 아직 «직전 걷기 에피소드»를 마저 걷고 있을 수 있다
            //   (배회 확률 0은 <b>다음</b> 분기부터 적용된다). 코스튬 층은 Idle에서만 도므로
            //   그 구간을 표본에 넣으면 잔상만 읽게 된다 — 먼저 Idle이 되기를 기다린다.
            yield return CostumeFocusRig.WaitForGroundedIdle(agent, 6f);

            // ---- 몰입기 전체를 표본으로 잡는다(진입/절정/이완 세 소구간을 모두 지나가도록) ----
            float immersionWall = CostumeFocusRig.ImmersionWallSeconds(
                director.SessionDurationSeconds, TimeCompression);
            var samples = new List<Sample>(4096);
            float lastElapsed = 0f;
            bool leftIdle = false;
            yield return TestClock.SampleForSeconds(immersionWall * 1.2f, t =>
            {
                lastElapsed = t;
                if (director.CurrentPhase != FocusSessionPhase.Immersion) return false;
                if (bb.Machine == null || bb.Machine.CurrentStateId != StickmanStateId.Idle)
                {
                    leftIdle = true;
                    return false;   // Idle을 벗어나면 코스튬 층이 안 돈다 — 거기서 표본을 끊는다.
                }
                samples.Add(new Sample
                {
                    Step = bb.CostumeStepIndex,
                    SubPhase = bb.CostumeSubPhase,
                    Flag = bb.CostumePoseWroteThisFrame,
                    Count = bb.CostumePoseWriteCount,
                    Gesturing = bb.IsIdleAmbientMotionActive,
                    Rebuilt = limbs.LastRebuiltSegmentCount,
                });
                return true;
            });

            Assert.Greater(samples.Count, 50,
                $"{LogPrefix} 몰입기 표본이 {samples.Count}프레임뿐입니다(벽시계 {lastElapsed:F2}초, " +
                $"몰입기 기대 {immersionWall:F2}초). 표본이 없으면 아래 판정은 전부 공허합니다.");

            // ---- 집계 — 「썼는가」의 정본은 <b>누적 계기의 증가분</b>이다(순간값은 낡을 수 있다) ----
            int wroteFrames = 0, holdFrames = 0, stepChanges = 0, unexplainedWrites = 0;
            int staleFlagFrames = 0, missedFlagFrames = 0, gestureFrames = 0;
            int rebuildFrames = 0, rebuiltSegmentsTotal = 0;
            var subPhasesSeen = new HashSet<int>();

            for (int i = 1; i < samples.Count; i++)
            {
                Sample now = samples[i], prev = samples[i - 1];
                bool wrote = now.Count > prev.Count;

                // 제스처 프레임은 코스튬 층이 아예 안 도는 프레임이다 — 표본에서 뺀다.
                if (now.Gesturing) { gestureFrames++; continue; }
                if (now.Step < 0) continue;   // 아직 한 번도 안 돈 초기 프레임

                subPhasesSeen.Add(now.SubPhase);
                if (wrote) wroteFrames++;
                if (prev.Step >= 0 && !prev.Gesturing && now.Step != prev.Step) stepChanges++;

                // 이 프레임의 쓰기가 «설명되는가». 설명되는 경우는 셋뿐이다:
                //   ① 스텝 번호가 바뀌었다  ② 소구간이 바뀌었다(기울임 편차가 달라진다)
                //   ③ 직전이 제스처/미실행 프레임이었다(포즈 층이 다른 자세로 흘러갔다가 되돌아온다)
                bool changed = prev.Step < 0 || prev.Gesturing
                               || now.Step != prev.Step || now.SubPhase != prev.SubPhase;
                if (wrote && !changed) unexplainedWrites++;
                if (!wrote && !changed) holdFrames++;

                // 순간값 계기가 누적 계기와 같은 말을 하는가(둘이 갈라지면 그 자체가 사건이다).
                if (now.Flag && !wrote) staleFlagFrames++;
                if (!now.Flag && wrote) missedFlagFrames++;

                // ★ L-1의 문자 그대로의 계기 — 이 프레임에 팔다리를 다시 구웠는가.
                if (now.Rebuilt > 0) { rebuildFrames++; rebuiltSegmentsTotal += now.Rebuilt; }
            }

            Debug.Log($"{LogPrefix} 몰입기 표본 {samples.Count}프레임 / 벽시계 {lastElapsed:F2}초 " +
                $"(Idle 이탈로 조기 종료={leftIdle}) — 쓴 프레임 {wroteFrames} · 유지 프레임 {holdFrames} · " +
                $"스텝 전이 {stepChanges} · 소구간 {subPhasesSeen.Count}종 · 제스처 프레임 {gestureFrames} · " +
                $"설명 안 되는 쓰기 {unexplainedWrites} · 순간값 불일치(과잉 {staleFlagFrames} / 누락 {missedFlagFrames}) · " +
                $"★L-1 마디 다시 구운 프레임 {rebuildFrames}(총 {rebuiltSegmentsTotal}마디).");

            // ---- ① 양성: 실제로 쓴다 ----
            Assert.Greater(wroteFrames, 0,
                $"{LogPrefix} 몰입기 {samples.Count}프레임 동안 코스튬 포즈가 <b>한 번도</b> 쓰지 않았습니다 — " +
                "「0이 절감이다」가 아니라 «계기가 안 붙었다»이거나 LFVS가 통째로 안 도는 상태입니다.");
            Assert.GreaterOrEqual(stepChanges, 2,
                $"{LogPrefix} 스텝 전이가 {stepChanges}회뿐입니다 — 표가 순환 재생되지 않았거나 " +
                "작업 구간(듀티)을 한 번도 못 봤습니다.");

            // ---- ② 음성: 스텝 사이 프레임에는 안 쓴다 ----
            Assert.Greater(holdFrames, 0,
                $"{LogPrefix} «스텝이 그대로인데 안 쓴» 프레임이 0개입니다 — 매 프레임 다시 쓰고 있습니다.");
            Assert.AreEqual(0, unexplainedWrites,
                $"{LogPrefix} 스텝도 소구간도 안 바뀌었는데 쓴 프레임이 {unexplainedWrites}개입니다 — " +
                "포즈 스무딩이 되살아났거나(설계 5-3절의 «스냅이 필수다») 조기 반환 조건 " +
                "(HoldsCostumeStepAlready)이 매 프레임 거짓이 됩니다. 그러면 절감이 통째로 사라집니다.");

            // ---- ③ 절감의 크기 — 유지 프레임이 쓴 프레임을 압도해야 LFVS다 ----
            Assert.Greater(holdFrames, wroteFrames,
                $"{LogPrefix} 유지 {holdFrames}프레임 vs 쓴 {wroteFrames}프레임 — " +
                "쓰지 않는 프레임이 더 많아야 «저프레임 스텝»입니다.");

            // ---- ④ 두 계기가 같은 말을 하는가 ----
            Assert.AreEqual(0, missedFlagFrames,
                $"{LogPrefix} 누적 계기는 올랐는데 순간값이 거짓인 프레임이 {missedFlagFrames}개입니다 — " +
                "두 계기가 같은 반환값에서 나오지 않습니다.");
            Assert.AreEqual(0, staleFlagFrames,
                $"{LogPrefix} 순간값이 참인데 누적 계기가 안 오른 프레임이 {staleFlagFrames}개입니다 — " +
                "CostumePoseWroteThisFrame이 <b>낡은 값</b>으로 남아 있습니다(포즈 층이 안 도는 프레임에서 " +
                "초기화되지 않습니다). 그 상태의 순간값은 L-5의 「음성」을 조용히 초록으로 만듭니다.");

            // ---- ⑤ ★ L-1 — <b>다른 자</b>로 같은 것을 잰다: 팔다리를 실제로 다시 굽는 프레임 수 ----
            //   두 계기는 서로 다른 컴포넌트에서 나온다. 한 프레임 어긋나므로 짝짓지 않고 합계로 본다.
            Assert.Greater(rebuildFrames, 0,
                $"{LogPrefix} 몰입기 내내 팔다리를 <b>한 번도</b> 다시 굽지 않았습니다 — " +
                "LimbCurveRenderer 계기가 죽었거나 포즈가 마디 각도를 전혀 안 건드렸습니다. " +
                "그러면 아래 상한은 «절감»이 아니라 «아무 일도 안 일어남»을 재는 것입니다.");
            Assert.LessOrEqual(rebuildFrames, wroteFrames + 2,
                $"{LogPrefix} 코스튬 포즈가 쓴 프레임은 {wroteFrames}개인데 팔다리를 다시 구운 프레임은 " +
                $"{rebuildFrames}개입니다(표본 {samples.Count}프레임). 쓰지 않은 프레임에서도 마디가 다시 " +
                "구워지고 있다는 뜻이고, 그것이 바로 설계 5-3절이 «스냅이 필수다»로 막으려던 상태입니다 " +
                "(포즈 스무딩이 살아 있으면 20° 스텝이 333ms 중 183ms를 이징에 쓰고 그 동안 매 프레임 " +
                "다시 굽습니다). 여유 2프레임은 Update/LateUpdate 한 프레임 어긋남과 구간 경계 몫입니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 양성(쓴 프레임 {wroteFrames}) / 음성(유지 {holdFrames}) / " +
                $"설명 안 되는 쓰기 0 / L-1 다시 굽기 {rebuildFrames}프레임({rebuiltSegmentsTotal}마디). " +
                $"절감비 = 유지 {holdFrames} : 쓰기 {wroteFrames}.");
        }

        /// <summary>
        /// ★ <b>진단 계기의 계약</b> — <c>CostumeStepIndex</c>는 «적용하지 않았으면 −1»이라고
        /// 스스로 적어 뒀고, <c>CostumePoseWroteThisFrame</c>은 «이번 프레임에 실제로 썼는가»의
        /// <b>순간값</b>이라고 적어 뒀다. 두 계약 모두 <b>포즈 층이 안 도는 프레임</b>에서 지켜져야 한다.
        ///
        /// <para><b>왜 결정론적인가</b>: 쓴 프레임을 <b>관측한 그 자리에서</b> 세션을 끊는다.
        /// 그 뒤로 <c>TickCostumeKeypose</c>는 한 번도 불리지 않으므로, 계기가 스스로 내려가지 않으면
        /// 값이 <b>영원히</b> 참으로 남는다. 운(제스처 타이밍)에 기대지 않는다.</para>
        ///
        /// <para>이 검사가 지키는 것은 미래의 측정이다 — 낡은 참/낡은 스텝 번호는
        /// «절감이 되고 있다»는 <b>조용한 초록</b>을 만들어 낸다(CLAUDE.md 「부재 단언은 썩으면 조용히 초록」).</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 포즈_계기는_층이_안_도는_프레임에_낡은_값으로_남지_않는다()
        {
            yield return CostumeFocusRig.LoadSceneAndSettle();

            StickmanAgent agent = CostumeFocusRig.Agent();
            StickmanBlackboard bb = agent.Blackboard;
            FocusWatchDirector director = CostumeFocusRig.Director();
            CostumePropRenderer prop = CostumeFocusRig.EnsureCostumeProp(agent);
            CostumeFocusRig.WearOfficeCostume(agent.Config);

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            yield return CostumeFocusRig.StartAndReachImmersion(director, prop, TimeCompression);
            yield return CostumeFocusRig.WaitForGroundedIdle(agent, 6f);

            float immersionWall = CostumeFocusRig.ImmersionWallSeconds(
                director.SessionDurationSeconds, TimeCompression);

            // 「쓴 프레임」을 만나는 즉시 세션을 끊는다 — 그 순간의 계기 값이 그대로 얼어붙는다.
            int prevCount = bb.CostumePoseWriteCount;
            bool caught = false;
            int stepAtCut = -99;
            yield return TestClock.SampleForSeconds(immersionWall * 1.2f, t =>
            {
                int now = bb.CostumePoseWriteCount;
                bool wrote = now > prevCount;
                prevCount = now;
                if (!wrote)
                {
                    return director.CurrentPhase == FocusSessionPhase.Immersion
                           && bb.Machine != null && bb.Machine.CurrentStateId == StickmanStateId.Idle;
                }

                caught = true;
                stepAtCut = bb.CostumeStepIndex;
                director.StopFocusSession();
                return false;
            });

            Assert.IsTrue(caught,
                $"{LogPrefix} 몰입기 안에서 «쓴 프레임»을 한 번도 못 만났습니다 — 아래 판정의 전제가 없습니다 " +
                "(LFVS가 아예 안 돌았다는 뜻이므로 그 자체가 사건입니다).");

            // 층이 확실히 여러 프레임 돌지 않도록 벽시계로 기다린다(프레임 수로 세지 않는다).
            yield return TestClock.SampleForSeconds(0.4f, _ => { });

            Debug.Log($"{LogPrefix} 세션을 끊은 시점의 스텝 {stepAtCut} → 0.4초 뒤 " +
                $"스텝 {bb.CostumeStepIndex} / 순간값 {bb.CostumePoseWroteThisFrame} / " +
                $"몰입기활성 {bb.IsCostumeImmersionActive} / 구간 {bb.FocusPhase}.");

            Assert.IsFalse(bb.IsCostumeImmersionActive,
                $"{LogPrefix} 세션을 끊었는데 IsCostumeImmersionActive가 참입니다.");
            Assert.IsFalse(bb.CostumePoseWroteThisFrame,
                $"{LogPrefix} 코스튬 포즈 층이 <b>돌지도 않는</b> 프레임에서 " +
                "CostumePoseWroteThisFrame이 여전히 참입니다 — 그 값은 «이번 프레임에 실제로 썼는가»의 " +
                "순간값이라고 선언돼 있습니다. 층이 건너뛰어지는 프레임(제스처 중 · 세션 종료 후 · " +
                "Idle이 아닐 때)에서 초기화되지 않으면, 그 계기를 읽는 어떤 측정도 «썼다»를 과대계상합니다.");
            Assert.AreEqual(-1, bb.CostumeStepIndex,
                $"{LogPrefix} 세션이 끝났는데 CostumeStepIndex가 {bb.CostumeStepIndex}입니다 — " +
                "그 값은 «적용하지 않았으면 −1»이라고 선언돼 있습니다(낡은 스텝 번호는 다음 라운드의 " +
                "진단을 통째로 오도합니다).");

            Debug.Log($"{LogPrefix} ② 통과 — 층이 안 도는 프레임에서 두 계기가 모두 내려갔습니다.");
        }
    }
}
