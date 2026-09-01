using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ P9-b 회귀 잠금 ②(실측 파트) — <b>"배관에 물이 실제로 흐른다."</b>
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// P9-a가 <c>RagdollRig.EnterRagdoll(방향, 충격량)</c>을 만들었지만 <b>아무도 호출하지 않았다</b>.
    /// <c>RagdollState.Enter()</c>는 여전히 무인자 버전(충격량 0)을 썼고, 그래서 "얻어맞으면 팔다리가
    /// 크게 튕긴다"는 요청이 화면에는 전혀 나타나지 않았다. P9-b가 그 배선을 잇는다.
    ///
    /// 배선의 유일한 함정은 <b>단위</b>다. 랙돌 판정에 쓰는 충격량(<c>ragdollForceThreshold</c>와 비교하는
    /// 그 값)을 그대로 넘기면 실측 감도 42.8도/초/N·s에서 <b>초당 5바퀴</b>가 나온다. 그래서
    /// <c>RagdollImpactResolver.ResolveEntryImpulse()</c>가 사이에 들어간다.
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 — EditMode와 역할이 다르다
    /// ============================================================================
    /// Tests/EditMode/RagdollEntryImpulseConversionTests가 <b>"설계한 대로 계산되는가"</b>를 물리 없이
    /// 잠근다. 여기서는 그 계산이 <b>현실과 맞는지</b>를 씬에서 잰다:
    /// <list type="number">
    /// <item><b>실제 충격 이벤트</b>(생산자와 똑같은 경로)의 진입 각속도가 설계 구간 안에 있는가.</item>
    /// <item><b>상한 클램프가 실물에서도 동작</b>하는가 — 임계값 5배에서 초당 5바퀴가 안 나오는가.</item>
    /// <item><b>좌우 부호</b>가 옳은가 — 오른쪽으로 밀면 오른쪽으로 젖혀지는가.</item>
    /// <item><b>방향을 모르는 경로는 무변경</b>인가 — 크기만 아는 통지/직접 전이는 힘을 받지 않는가
    ///       (방향 스냅샷이 소비형이 아니면 <b>지난 타격의 방향으로 유령 충격량</b>이 실린다).</item>
    /// <item><b>정착이 여전히 되는가</b> — 새로 실린 에너지 때문에 GETUP에 못 가면 안 된다.</item>
    /// </list>
    /// </summary>
    public sealed class RagdollEntryImpulseWiringTests
    {
        private const string LogPrefix = "[P9B-TEST]";
        private const float SettleWaitSeconds = 3f;

        /// <summary>충격량을 관찰할 물리 스텝 수. RagdollEntryEnergyTests와 같은 이유로 짧게 둔다 —
        /// 길면 바닥 충돌/관절 반력이 섞여 "진입 충격량이 만든 회전"이 아닌 것을 재게 된다.</summary>
        private const int ObserveFixedSteps = 3;

        // 설계 구간(리더 지시). ★ 이 구간을 **정확히** 잠그는 것은 EditMode
        // (RagdollEntryImpulseConversionTests)다 — 거기는 순수 함수라 오차가 0이다.
        private const float DesignMinDegreesPerSecond = 90f;
        private const float DesignMaxDegreesPerSecond = 400f;

        // ★★ 2026-09-01 (2차) 실측 산포를 반영한 허용 배수. 처음엔 ±35%로 뒀는데, 같은 리그를
        // 반복 측정해 보니 그보다 훨씬 넓었다.
        // RagdollEntryEnergyTests의 9회 실측(같은 4N·s를 같은 방식으로 측정):
        //   방향별 각속도 82.1 ~ 263.2도/초 -> 환산 감도 20.5 ~ 65.8도/초/N·s (설정값 42.8의 0.48~1.54배)
        // 원인은 오차가 아니라 **진입 포즈**다. 걷기 사이클의 어느 순간에 진입하느냐에 따라 팔다리
        // 배치(= 유효 관성모멘트)가 달라지고, 바라보는 방향에 따라 관절 해부학 제한이 거울상으로
        // 반전된다(RagdollRig.MirrorIfFacingLeft). 배회 AI가 방향을 무작위로 고르므로 이 분산은
        // 없앨 수 없고, 없애려 들면 테스트가 게임과 다른 조건을 재게 된다.
        //
        // 그래서 역할을 이렇게 나눈다:
        //   · **정밀 잠금**은 아래 MeasureRealImpact의 `LastEntryImpulse == ResolveEntryImpulse` 단언이
        //     맡는다 — 이건 물리를 거치지 않아 오차 0.1%로 배선을 확정한다.
        //   · 각속도 단언은 "감도 상수가 여전히 현실의 자릿수인가"만 본다(아래 배수).
        private const float SensitivityLowFactor = 0.33f;   // 실측 최저 0.48배 대비 1.45배 여유
        private const float SensitivityHighFactor = 2.0f;   // 실측 최고 1.54배 대비 1.30배 여유

        /// <summary>
        /// "절대 나오면 안 되는" 값 — 변환 없이 넘겼을 때의 실측 예측치는 약 1712도/초다.
        /// 상한 400 x 실측 최고 배수 1.54 = 616까지는 정상 산포이므로 그 위, 그리고 1712보다는
        /// 확실히 아래인 900으로 둔다(회귀는 1712이므로 1.9배 여유로 확실히 걸린다).
        /// </summary>
        private const float RunawayDegreesPerSecond = 900f;

        private StickmanAgent _agent;

        private IEnumerator LoadSceneAndSettle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            yield return new WaitForSeconds(SettleWaitSeconds);
        }

        private float Threshold => _agent.Blackboard.Config != null ? _agent.Blackboard.Config.ragdollForceThreshold : 8f;

        // ========================================================================
        // (1) 실제 충격 이벤트가 설계 구간 안의 회전으로 들어오는가
        // ========================================================================

        [UnityTest]
        public IEnumerator 임계값_바로위_충격은_은은한_회전으로_진입한다()
        {
            // 지금 존재하는 가장 약한 랙돌: 긴 망토를 밟고 넘어짐(임계값 1.02배).
            float measured = 0f, predicted = 0f, appliedImpulse = 0f;
            yield return MeasureRealImpact(1.02f, Vector2.right,
                (m, p, imp) => { measured = m; predicted = p; appliedImpulse = imp; });

            Debug.Log($"{LogPrefix} [약] 원본 {Threshold * 1.02f:F2}N·s -> 진입 충격량 {appliedImpulse:F3}N·s -> " +
                $"측정 {Mathf.Abs(measured):F1}도/초 (예측 {predicted:F1}도/초, 오차 " +
                $"{(predicted > 0f ? Mathf.Abs(measured) / predicted : 0f):F2}배)");

            Assert.Greater(appliedImpulse, 0f,
                $"{LogPrefix} 진입 충격량이 0입니다 — RagdollState.Enter()가 여전히 무인자 EnterRagdoll()을 " +
                "쓰고 있거나, 방향 스냅샷이 전달되지 않았습니다(배관에 물이 안 흐릅니다).");
            AssertWithinDesignRange(Mathf.Abs(measured), predicted, "임계값 1.02배");
            // 강약 구분: 최약 충격이 상한(400)의 산포 하한(400 x 0.33 = 132)을 크게 넘으면 강약이
            // 뭉갠 것이다. 실측 최약은 101.9도/초였고 그 산포 상한은 102 x 2.0 = 204이므로 250으로 둔다.
            Assert.Less(Mathf.Abs(measured), 250f,
                $"{LogPrefix} 가장 약한 충격이 {Mathf.Abs(measured):F1}도/초입니다 — 강약 구분이 사라졌습니다.");
        }

        [UnityTest]
        public IEnumerator 임계값_5배_충격도_상한클램프로_초당_1회전_수준에_머문다()
        {
            float measured = 0f, predicted = 0f, appliedImpulse = 0f;
            yield return MeasureRealImpact(5f, Vector2.right,
                (m, p, imp) => { measured = m; predicted = p; appliedImpulse = imp; });

            float naive = Threshold * 5f * SensitivityOf(_agent.Blackboard.Config);
            Debug.Log($"{LogPrefix} [강/네거티브컨트롤] 원본 {Threshold * 5f:F2}N·s -> 진입 충격량 " +
                $"{appliedImpulse:F3}N·s -> 측정 {Mathf.Abs(measured):F1}도/초 " +
                $"({Mathf.Abs(measured) / 360f:F2}회전/초). 변환 없이 넘겼다면 약 {naive:F0}도/초" +
                $"({naive / 360f:F1}회전/초)였을 것.");

            AssertWithinDesignRange(Mathf.Abs(measured), predicted, "임계값 5배");
            Assert.Less(Mathf.Abs(measured), RunawayDegreesPerSecond,
                $"{LogPrefix} 임계값 5배 충격의 진입 각속도가 {Mathf.Abs(measured):F0}도/초" +
                $"({Mathf.Abs(measured) / 360f:F1}회전/초)입니다 — 상한 클램프가 동작하지 않아 " +
                "LastImpactMagnitude가 그대로 흘러들어간 것으로 보입니다(P9-b가 막으려던 바로 그 회귀).");
        }

        // ========================================================================
        // (2) 좌우 부호
        // ========================================================================

        [UnityTest]
        public IEnumerator 밀린_방향으로_젖혀져야_한다_좌우_부호검증()
        {
            float pushedRight = 0f;
            yield return MeasureRealImpact(2f, Vector2.right, (m, p, imp) => pushedRight = m);

            float pushedLeft = 0f;
            yield return MeasureRealImpact(2f, Vector2.left, (m, p, imp) => pushedLeft = m);

            Debug.Log($"{LogPrefix} 부호 — +x로 밀림 {pushedRight:F1}도/초 / -x로 밀림 {pushedLeft:F1}도/초");

            // 가슴(질량중심보다 위)을 +x로 밀면 몸통 위쪽이 오른쪽으로 넘어간다 = 시계 방향 = 음수 Z.
            Assert.Less(pushedRight, 0f,
                $"{LogPrefix} +x로 밀렸는데 각속도가 {pushedRight:F1}도/초(양수)입니다 — 맞은 쪽으로 젖혀지고 있습니다.");
            Assert.Greater(pushedLeft, 0f,
                $"{LogPrefix} -x로 밀렸는데 각속도가 {pushedLeft:F1}도/초(음수)입니다.");
            // ★★ 2026-09-01 (2차) — 크기 대칭은 **비(比)**로만 묻는다. 처음엔 절대차 판정
            // (`|차| < |한쪽| * 0.35 + 5`)을 RagdollEntryEnergyTests에서 그대로 베껴 왔는데, 반복
            // 실행에서 −147.4 vs +317.0(비 2.15배)이 나와 실패했다. 그 판정이 왜 틀렸는지는
            // 그 파일에 적어 둔 것과 같은 이유다: <b>두 측정은 서로 다른 씬 로드의 독립 시행</b>이라
            // 짝지어진 좌우 쌍이 아니다. 진입 포즈(걷기 사이클 위상)와 거울상 관절 제한 때문에
            // 방향별 실측 감도가 18.6 ~ 74.1도/초/N·s(약 4배)로 흩어진다.
            //
            // 그래서 좌우 **부호**가 진짜 불변식이고(위 두 단언 — 전 실행에서 한 번도 흔들린 적 없다),
            // 크기는 "한쪽이 사실상 죽지 않았는가"만 본다. 한쪽 지렛대/관절 제한이 깨지면 그쪽이
            // 0에 수렴해 비가 발산하므로 4배 문턱에 확실히 걸린다(실측 최대 2.15배 대비 1.86배 여유).
            float largerPush = Mathf.Max(Mathf.Abs(pushedRight), Mathf.Abs(pushedLeft));
            float smallerPush = Mathf.Min(Mathf.Abs(pushedRight), Mathf.Abs(pushedLeft));
            float pushRatio = largerPush / Mathf.Max(0.001f, smallerPush);
            Debug.Log($"{LogPrefix} 좌우 크기 비 {pushRatio:F2}배 (허용 4.00배 미만, 실측 정상 범위 1.00~2.15)");

            Assert.Less(pushRatio, 4f,
                $"{LogPrefix} 좌우 타격의 회전 크기가 {pushRatio:F2}배 차이 납니다" +
                $"({pushedRight:F1} vs {pushedLeft:F1}) — 진입 포즈/거울상 제한으로 설명되는 산포를 " +
                "벗어났습니다. 한쪽 방향에서만 지렛대나 관절 제한이 깨졌을 가능성이 있습니다.");
        }

        // ========================================================================
        // (3) 방향을 모르는 경로는 무변경 — 유령 충격량 방지
        // ========================================================================

        [UnityTest]
        public IEnumerator 크기만_아는_통지는_여전히_충격량을_가하지_않는다()
        {
            yield return LoadSceneAndSettle();
            RagdollRig rig = _agent.Blackboard.GetRagdollRig();

            _agent.ReportExternalImpact(Threshold * 5f);   // 방향 없는 기존 시그니처.
            yield return null;

            Assert.AreEqual(StickmanStateId.Ragdoll, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} 임계값 5배 통지인데 RAGDOLL로 전이하지 않았습니다(기존 계약 파손).");
            Assert.AreEqual(0f, rig.LastEntryImpulse, 1e-6f,
                $"{LogPrefix} 방향을 모르는 경로가 충격량 {rig.LastEntryImpulse:F3}N·s를 가했습니다 — " +
                "방향을 추정해 때리고 있습니다(설계상 무방향 경로는 P9-a 이전과 비트 단위로 같아야 합니다).");
        }

        [UnityTest]
        public IEnumerator 앞선_타격의_방향이_다음_진입에_재사용되면_안_된다()
        {
            yield return LoadSceneAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;
            RagdollRig rig = bb.GetRagdollRig();

            // 1차: 방향을 아는 강한 타격.
            RagdollImpactResolver.TryApplyImpact(bb, Threshold * 5f, Vector2.right);
            float first = rig.LastEntryImpulse;
            Assert.Greater(first, 0f, $"{LogPrefix} 1차 방향성 타격이 충격량을 가하지 못했습니다.");
            yield return new WaitForFixedUpdate();

            // 2차: 방향을 모르는 강제 전이(원인 불명 인터럽트/테스트의 직접 ChangeState).
            bb.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            float second = rig.LastEntryImpulse;

            Debug.Log($"{LogPrefix} 유령 충격량 검사 — 1차(방향 있음) {first:F3}N·s / 2차(방향 없음) {second:F3}N·s");
            Assert.AreEqual(0f, second, 1e-6f,
                $"{LogPrefix} 방향 없는 두 번째 진입이 {second:F3}N·s를 가했습니다 — " +
                "StickmanBlackboard.LastImpactDirection이 소비되지 않아 지난 타격의 방향이 재사용되고 있습니다.");
        }

        // ========================================================================
        // (4) 정착 — 새로 실린 에너지가 GETUP 복귀를 막지 않는가
        // ========================================================================

        [UnityTest]
        public IEnumerator 방향성_최대충격을_받아도_유한시간에_GETUP으로_복귀한다()
        {
            const float MaxObserveSeconds = 15f;
            const float SampleInterval = 0.25f;

            yield return LoadSceneAndSettle();
            StickmanBlackboard bb = _agent.Blackboard;

            StickmanStateId before = bb.Machine.CurrentStateId;
            Assert.IsTrue(before == StickmanStateId.Idle || before == StickmanStateId.Walk,
                $"{LogPrefix} 충격 전 능동 상태여야 합니다. 실제={before}");

            // 상한에 걸리는 최대 세기 + 방향 있음 = 이번 라운드가 새로 만든 가장 가혹한 조건.
            RagdollImpactResolver.TryApplyImpact(bb, Threshold * 5f, Vector2.right);
            Assert.AreEqual(StickmanStateId.Ragdoll, bb.Machine.CurrentStateId,
                $"{LogPrefix} 방향성 충격 직후 RAGDOLL이어야 합니다.");
            Assert.Greater(bb.GetRagdollRig().LastEntryImpulse, 0f,
                $"{LogPrefix} 이 테스트는 '충격량이 실린 상태에서도 정착하는가'를 봅니다 — 충격량이 0이면 무의미합니다.");

            bool sawGetup = false, recovered = false;
            float elapsed = 0f;
            while (elapsed < MaxObserveSeconds)
            {
                yield return new WaitForSeconds(SampleInterval);
                elapsed += SampleInterval;
                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.Getup) sawGetup = true;
                if (state == StickmanStateId.Idle || state == StickmanStateId.Walk) { recovered = true; break; }
            }

            Debug.Log($"{LogPrefix} 정착 — sawGetup={sawGetup}, recovered={recovered}, elapsed={elapsed:F2}s, " +
                $"finalState={bb.Machine.CurrentStateId}");
            Assert.IsTrue(sawGetup,
                $"{LogPrefix} 진입 충격량을 받은 RAGDOLL이 GETUP을 거치지 못했습니다 — 새로 실린 에너지가 " +
                "ragdollSettleSpeedThreshold 아래로 내려가지 못하고 있습니다(충격량이 과합니다).");
            Assert.IsTrue(recovered,
                $"{LogPrefix} {MaxObserveSeconds}초 안에 Idle/Walk로 복귀하지 못했습니다.");
        }

        // ========================================================================
        // 헬퍼
        // ========================================================================

        private static float SensitivityOf(StickConfig config)
            => config != null ? config.ragdollEntryAngularSensitivityPerImpulse : 42.8f;

        private void AssertWithinDesignRange(float measuredAbs, float predicted, string label)
        {
            Assert.GreaterOrEqual(measuredAbs, DesignMinDegreesPerSecond * SensitivityLowFactor,
                $"{LogPrefix} {label} 충격의 실측 진입 각속도가 {measuredAbs:F1}도/초입니다 — 설계 하한 " +
                $"{DesignMinDegreesPerSecond}도/초에 실측 산포 최저 배수({SensitivityLowFactor:F2})를 " +
                "적용한 값에도 못 미칩니다. 진입 충격량이 전달되지 않고 있을 가능성이 큽니다.");
            Assert.LessOrEqual(measuredAbs, DesignMaxDegreesPerSecond * SensitivityHighFactor,
                $"{LogPrefix} {label} 충격의 실측 진입 각속도가 {measuredAbs:F1}도/초로 설계 상한 " +
                $"{DesignMaxDegreesPerSecond}도/초에 산포 최고 배수({SensitivityHighFactor:F2})를 적용한 " +
                "값을 넘었습니다 — 상한 클램프를 의심하세요.");
            Assert.GreaterOrEqual(measuredAbs, predicted * SensitivityLowFactor,
                $"{LogPrefix} {label}: 예측 {predicted:F1}도/초 대비 실측 {measuredAbs:F1}도/초가 너무 작습니다 " +
                $"(비 {measuredAbs / Mathf.Max(0.001f, predicted):F2}, 허용 하한 {SensitivityLowFactor:F2}) — " +
                $"StickConfig.ragdollEntryAngularSensitivityPerImpulse" +
                $"({SensitivityOf(_agent.Blackboard.Config):F1}도/초/N·s)가 현재 리그와 맞지 않습니다. " +
                "캐릭터 질량/지렛대가 바뀌었다면 그 상수를 다시 재야 합니다.");
            Assert.LessOrEqual(measuredAbs, predicted * SensitivityHighFactor,
                $"{LogPrefix} {label}: 예측 {predicted:F1}도/초 대비 실측 {measuredAbs:F1}도/초가 너무 큽니다 " +
                $"(비 {measuredAbs / Mathf.Max(0.001f, predicted):F2}, 허용 상한 {SensitivityHighFactor:F2}).");
        }

        /// <summary>
        /// <b>생산자와 완전히 같은 경로</b>로 충격 이벤트를 일으키고(=<c>RagdollImpactResolver.TryApplyImpact</c>
        /// -> ChangeState -> <c>RagdollState.Enter</c> -> <c>RagdollRig.EnterRagdoll(방향, 충격량)</c>),
        /// 첫 몇 물리 스텝 동안의 루트 각속도(절댓값이 가장 큰 값, 부호 포함)를 돌려준다.
        ///
        /// ★ 측정마다 씬을 다시 로드한다 — RagdollEntryEnergyTests와 같은 이유다(앞 측정에서 날아간 몸이
        /// 아직 공중에 있으면 가슴 지점이 질량중심보다 아래가 되어 토크 부호가 뒤집힌다).
        /// </summary>
        /// <param name="thresholdRatio">임계값의 몇 배로 때릴 것인가(= 생산자들이 실제로 넘기는 단위).</param>
        /// <param name="hitDirection">캐릭터가 밀려나는 방향.</param>
        /// <param name="report">(측정 각속도, 예측 각속도, 실제 가해진 충격량 N·s)</param>
        private IEnumerator MeasureRealImpact(float thresholdRatio, Vector2 hitDirection,
            System.Action<float, float, float> report)
        {
            yield return LoadSceneAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RagdollRig rig = bb.GetRagdollRig();

            Assert.IsTrue(rig.TryGetChestWorldPoint(out Vector2 chest), $"{LogPrefix} 가슴 지점 유도 실패.");
            float lever = chest.y - bb.Body.worldCenterOfMass.y;
            Assert.Greater(lever, 0f,
                $"{LogPrefix} 측정 시작 시점의 지렛대가 {lever:F3}유닛(≤0)입니다 — 초기 조건이 오염됐습니다.");

            float raw = Threshold * thresholdRatio;
            float expectedImpulse = RagdollImpactResolver.ResolveEntryImpulse(bb.Config, raw);
            float predicted = expectedImpulse * SensitivityOf(bb.Config);

            bool went = RagdollImpactResolver.TryApplyImpact(bb, raw, hitDirection);
            Assert.IsTrue(went, $"{LogPrefix} 임계값 {thresholdRatio:F2}배 충격이 RAGDOLL 전이를 만들지 못했습니다.");
            Assert.AreEqual(expectedImpulse, rig.LastEntryImpulse, expectedImpulse * 0.001f + 1e-4f,
                $"{LogPrefix} RagdollRig에 실제로 실린 충격량({rig.LastEntryImpulse:F4}N·s)이 " +
                $"ResolveEntryImpulse의 결과({expectedImpulse:F4}N·s)와 다릅니다 — 배선 도중 다른 값이 " +
                "끼어들었거나 변환을 건너뛰었습니다.");

            float extreme = 0f;
            for (int i = 0; i < ObserveFixedSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                float w = bb.Body.angularVelocity;
                if (Mathf.Abs(w) > Mathf.Abs(extreme)) extreme = w;
            }
            report(extreme, predicted, rig.LastEntryImpulse);
        }
    }
}
