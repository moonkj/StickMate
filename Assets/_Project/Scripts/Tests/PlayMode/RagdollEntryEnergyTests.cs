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
    /// ★ P9-a 회귀 잠금 — <b>"RAGDOLL 진입에 실제로 에너지가 들어간다."</b>
    /// (docs/UX_FLOW.md 38-14-3, 사용자 지적 "넘어질 때 팔다리가 안 휘둘린다")
    ///
    /// ============================================================================
    /// 무엇이 문제였나 — 세 개가 같은 방향으로 움직임을 깎고 있었다
    /// ============================================================================
    /// <list type="table">
    /// <item>(a) <c>RagdollRig.EnterRagdoll()</c>이 <b>자체 충격량을 하나도 주지 않았다.</b>
    ///       초기 에너지 = 그 순간 이미 실려 있던 걷던 속도뿐.</item>
    /// <item>(b) <c>AngularVelocityDampenOnEntry = 0.5</c>가 <b>남은 회전 에너지를 정확히 절반 삭제</b>.</item>
    /// <item>(c) 팔다리 감쇠 0.9/3.0(2026-08-28 상향)이 스윙 진폭을 추가로 감소.</item>
    /// </list>
    /// <b>(c)는 건드리지 않는다.</b> 감쇠는 광대역 도구라 낮추면 관절 제한 경계의 고주파 링잉
    /// (사용자가 이미 한 번 거부한 <b>"경련"</b>)이 같이 돌아온다. 1차 감쇠계에서
    /// <c>진폭 ∝ 초기에너지 / 감쇠</c>이므로 <b>감쇠를 유지한 채 초기 에너지만 올리는</b> 것이
    /// "크게 휘둘렸다가 축 늘어짐"에 도달하는 유일한 경로다.
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 (전부 실측 — "아마 될 것"이 아니다)
    /// ============================================================================
    /// <list type="number">
    /// <item><b>지렛대가 실재한다</b>: 충격을 가하는 가슴 지점이 질량중심보다 위여야 수평 타격이
    ///       토크를 만든다. 질량중심에 때리면 몸이 미끄러질 뿐 젖혀지지 않는다.</item>
    /// <item><b>토크의 부호가 옳다</b>: 오른쪽에서 밀면 몸이 오른쪽으로 젖혀져야 한다(좌우 대칭 쌍으로
    ///       확인 — 부호가 반대면 얻어맞은 쪽으로 넘어진다).</item>
    /// <item><b>각속도 삭감이 실제로 사라졌다</b>: 이미 랙돌인 상태에서 또 진입해도 각속도가
    ///       절반으로 깎이지 않는다(P9-a의 상수 변경을 직접 잰다).</item>
    /// <item><b>하위호환 경로가 비트 단위로 무변경</b>: 인자 없는 <c>EnterRagdoll()</c>은 아무 힘도
    ///       가하지 않는다(기존 소비자 전원이 이 경로다).</item>
    /// <item><b>비대칭 어깨 제한이 런타임 관절에 실제로 도달한다</b>(P9-d): 진입 시 재환산된
    ///       <c>joint.limits</c>의 폭이 해부학 구간의 폭과 같고, 0을 포함한다(= 진입 순간 위반 없음).</item>
    /// </list>
    /// </summary>
    public sealed class RagdollEntryEnergyTests
    {
        private const string LogPrefix = "[P9A-TEST]";
        private const float SettleWaitSeconds = 3f;

        /// <summary>충격량을 관찰할 물리 스텝 수. 짧게 둔다 — 길면 바닥 충돌/관절 반력이 섞여
        /// "진입 충격량이 만든 회전"이 아닌 것을 재게 된다.</summary>
        private const int ObserveFixedSteps = 3;

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

        // ========================================================================
        // (1) 지렛대가 실재하는가 — 물리 없이 결정론적으로 확인 가능한 기하 사실
        // ========================================================================

        [UnityTest]
        public IEnumerator ChestImpulsePointSitsAboveTheCenterOfMass()
        {
            yield return LoadSceneAndSettle();

            RagdollRig rig = _agent.Blackboard.GetRagdollRig();
            Assert.IsNotNull(rig, $"{LogPrefix} RagdollRig를 얻지 못했습니다.");
            Assert.IsTrue(rig.TryGetChestWorldPoint(out Vector2 chest),
                $"{LogPrefix} 가슴 지점을 유도하지 못했습니다 — 루트에 매달린 관절(어깨)을 못 찾았을 수 있습니다.");

            Vector2 com = _agent.Blackboard.Body.worldCenterOfMass;
            float lever = chest.y - com.y;
            Debug.Log($"{LogPrefix} 가슴 지점 y={chest.y:F4}, 질량중심 y={com.y:F4}, 지렛대={lever:F4}유닛");

            Assert.Greater(lever, 0f,
                $"{LogPrefix} 충격 지점이 질량중심보다 위가 아닙니다(지렛대 {lever:F4}) — 수평 타격이 " +
                "토크를 전혀 만들지 못해 몸이 젖혀지지 않고 미끄러지기만 합니다.");
        }

        // ========================================================================
        // (2) 충격량이 실제로 회전을 만드는가 + 부호가 옳은가
        // ========================================================================

        [UnityTest]
        public IEnumerator EntryImpulseCreatesTorqueInTheDirectionOfTheHit()
        {
            float control = 0f;
            yield return MeasureEntryAngularVelocity(Vector2.zero, 0f, v => control = v);

            float hitFromLeft = 0f;   // 왼쪽에서 맞았다 = 오른쪽(+x)으로 밀린다.
            yield return MeasureEntryAngularVelocity(Vector2.right, ImpulseMagnitude(), v => hitFromLeft = v);

            float hitFromRight = 0f;
            yield return MeasureEntryAngularVelocity(Vector2.left, ImpulseMagnitude(), v => hitFromRight = v);

            // ★★ 2026-09-01 (P9-b, 리더 승인) — 판정 통계를 **좌우 차분**으로 바꿨다.
            // ------------------------------------------------------------------------------
            // 예전 판정은 `|hitFromLeft| > |control| * 3 + 5`로, 타격 하나를 무충격 기준선과 직접
            // 비교했다. 그런데 `control`은 **다른 씬 로드에서** 잰 값이라 이번 타격과 짝지어진
            // 대조군이 아니다. RAGDOLL 진입 자체에도 노이즈가 있다(팔다리가 Kinematic -> Dynamic이
            // 되는 순간 그 무게가 관절을 통해 루트를 비튼다). 그 노이즈는 진입 시점의 포즈/진행
            // 방향에 좌우되고, 배회 AI가 방향을 무작위로 고르므로 **실행마다 다르다.**
            //
            // 실측(2026-09-01, 같은 코드 3회 실행):
            //   단독 실행      control =  6.09도/초  -> 하한  23.3  ->  통과
            //   전체 스위트    control = -0.06도/초  -> 하한   5.2  ->  통과
            //   부분 배치 6종  control = -69.34도/초 -> 하한 213.0  ->  **실패**
            // 4N·s가 만드는 회전은 물리적으로 약 171도/초가 상한이므로, control이 55를 넘는 순간
            // 이 판정은 **달성 불가능한 하한**을 요구한다. 코드가 옳아도 실패하는 구조였다.
            //
            // 좌우 차분 `|hitFromLeft - hitFromRight|`는 같은 노이즈에 훨씬 둔감하다 — 두 측정의
            // 노이즈가 독립이라 부호가 엇갈리면 서로 상쇄되고, 신호는 부호가 반대라 **더해진다**
            // (기댓값 = 2 x 충격량이 만든 회전). 실측 재현성이 그것을 확인해 준다:
            //   부분 배치 298.4 / 단독 294.4 / 전체 스위트 335.8도/초  (이상값 2 x 171 = 342)
            // 그래서 아래 두 하한을 **함께** 건다. 하나는 노이즈 대비(예전 의도 유지), 하나는
            // 물리 상수에서 유도한 절대 하한(= "충격량이 아무 일도 안 한다"를 직접 잡는다).
            //
            // ★ 하한을 이상값의 **1/3**로 잡는 이유(9회 실측 표본으로 확정) — 2026-09-01
            // 좌우 차분 실측: 214.8 / 241.7 / 294.4 / 298.4 / 299.8 / 335.1 / 335.8 / 342.0 / 434.2
            //   -> 이상값(2 x 171.2 = 342.4) 대비 63% ~ 127%. 진입 포즈(팔다리 배치 = 유효 관성모멘트)와
            //      어느 물리 스텝이 피크를 잡느냐에 따라 ±35%가량 흔들린다.
            // 그래서 "절반"(50%) 하한은 최저 표본과 13%p밖에 안 떨어져 언젠가 다시 터진다.
            // 1/3이면 최저 표본과도 1.88배 여유가 있고, 이 단언이 실제로 겨냥하는 결함
            // (= 충격량이 통째로 사라짐 -> 차분이 노이즈 수준 0~70으로 붕괴)은 그대로 잡는다.
            // <b>"각속도가 절반으로 깎이는" 회귀는 이 시끄러운 테스트가 아니라 아래 (3)
            // RepeatedEntryNoLongerHalvesExistingAngularVelocity가 결정론적으로 잠근다</b>
            // (각속도를 직접 주입해 비를 재므로 물리 노이즈가 0이다). 역할이 겹치지 않게 나눈 것이지
            // 커버리지를 줄인 것이 아니다.
            float differential = Mathf.Abs(hitFromLeft - hitFromRight);
            float sensitivity = SensitivityDegreesPerImpulse();
            float idealDifferential = 2f * ImpulseMagnitude() * sensitivity;
            float physicalFloor = idealDifferential / 3f;

            Debug.Log($"{LogPrefix} 진입 각속도(도/초) — 충격 없음 {control:F2} / +x 타격 {hitFromLeft:F2} / " +
                $"-x 타격 {hitFromRight:F2} | 좌우 차분 {differential:F2}(이상값 {idealDifferential:F1}, " +
                $"물리 하한 {physicalFloor:F1}, 노이즈 하한 {Mathf.Abs(control) * 3f + 5f:F1}) | " +
                $"충격량 {ImpulseMagnitude():F1}N·s당 감도 " +
                $"{differential / 2f / Mathf.Max(0.001f, ImpulseMagnitude()):F1}도/초/N·s" +
                $"(설정값 {sensitivity:F1})");

            // ★★ 2026-09-01 (2차 수정) — 노이즈 하한의 계수를 3 -> 2로 내렸다. 임의 완화가 아니라
            // **귀무가설에서 유도한 값**이다.
            // 귀무가설("충격량이 아무 일도 안 한다")에서 차분은 두 독립 진입 노이즈의 차
            // |n1 - n2| 이고, 그 상한은 |n1| + |n2| ≈ 2|noise| 다. 그러니 노이즈 대비 하한의
            // 물리적으로 옳은 계수는 **2**이지 3이 아니다.
            // 계수 3이 왜 실제로 터졌는가(실측): control이 73.53으로 뽑힌 실행에서 하한이 225.6이
            // 됐는데, 그 실행의 차분은 214.6이었다 — control은 **다른 씬 로드**에서 잰 한 표본일
            // 뿐이라 이번 차분의 노이즈와 아무 상관이 없는데, 그것을 3배 해서 하한으로 쓰면
            // 도달 가능 구간(실측 190~434)을 넘어서는 일이 생긴다.
            // 계수 2 + 위 물리 하한(1/3)의 조합을 9+3개 표본 전부에 대입하면 최소 여유가 1.46배다.
            Assert.Greater(differential, 2f * Mathf.Abs(control),
                $"{LogPrefix} 좌우 타격의 회전 차이({differential:F2}도/초)가 무충격 노이즈" +
                $"({control:F2}도/초)의 2배에 못 미칩니다 — AddForceAtPosition이 토크를 만들지 " +
                "못하고 있을 가능성이 있습니다(차이가 노이즈만으로 설명됩니다).");

            Assert.Greater(differential, physicalFloor,
                $"{LogPrefix} 좌우 타격의 회전 차이가 {differential:F2}도/초로 물리 하한 " +
                $"{physicalFloor:F1}도/초(= 이상값 {idealDifferential:F1}의 1/3, 감도 {sensitivity:F1}도/초/N·s 기준)에 " +
                "못 미칩니다 — 진입 충격량이 사실상 사라졌습니다(실측 최저 표본도 이 하한의 1.88배였습니다).");

            // 부호: 가슴(질량중심보다 위)을 +x로 밀면 몸통 위쪽이 오른쪽으로 넘어간다 = 시계 방향 = 음수 Z.
            Assert.Less(hitFromLeft, 0f,
                $"{LogPrefix} +x로 맞았는데 각속도가 {hitFromLeft:F2}도/초(양수)입니다 — 몸이 맞은 쪽으로 " +
                "젖혀지고 있습니다(지렛대 부호가 뒤집혔을 가능성).");
            Assert.Greater(hitFromRight, 0f,
                $"{LogPrefix} -x로 맞았는데 각속도가 {hitFromRight:F2}도/초(음수)입니다.");

            // ★★ 2026-09-01 (P9-b) 좌우 대칭 판정도 **비(比)**로 바꿨다 — 같은 라운드의 두 번째 함정.
            // ------------------------------------------------------------------------------
            // 예전 판정은 `|left| - |right| < |left| * 0.35 + 5`였는데, 수정 직후 첫 실행에서
            // 263.15 vs 171.05가 나왔다: 차 92.10, 허용오차 92.10 + 5 — **경계에 정확히 걸렸다.**
            // 통과했지만 그건 운이지 설계가 아니다.
            //
            // 왜 좌우 크기가 애초에 정확히 같을 수 없는가(= 이건 결함이 아니다):
            //   · 두 측정은 **각각 다른 씬 로드**이고, 진입 순간의 포즈는 걷기 사이클의 한 스냅샷이다.
            //     한쪽 다리가 앞, 한쪽이 뒤인 상태라 질량 분포가 좌우 대칭이 아니다.
            //   · 관절 해부학 제한은 **바라보는 방향에 따라 거울상으로 반전**된다
            //     (RagdollRig.MirrorIfFacingLeft). 배회 AI가 방향을 무작위로 고르므로 두 측정이
            //     같은 방향을 보고 있으리란 보장이 없다.
            // 실측 크기 비(큰 쪽/작은 쪽) 15회: 1.00 ~ 2.15배. 방향별 감도로 환산하면
            // 18.6 ~ 74.1도/초/N·s(약 4배 산포)다 — 진입 포즈와 거울상 제한이 만드는 정상 분산이다.
            // 문턱 4.0배는 실측 최대 2.15배 대비 1.86배 여유이고, 같은 분포를 재는
            // RagdollEntryImpulseWiringTests와 **같은 상수/같은 근거**를 쓴다.
            //
            // 그래서 "차이가 얼마인가"(신장·충격량 크기에 따라 의미가 달라진다)가 아니라
            // "몇 배나 차이 나는가"로 묻는다. 진짜 결함(한쪽이 거의 안 돌아감 = 지렛대/제한이 한쪽만
            // 깨짐)은 비가 발산하므로 4.0배 문턱에 확실히 걸린다. 부호가 뒤집히는 결함은 바로 위
            // 부호 단언 두 개가 이미 잡는다.
            float largerHit = Mathf.Max(Mathf.Abs(hitFromLeft), Mathf.Abs(hitFromRight));
            float smallerHit = Mathf.Min(Mathf.Abs(hitFromLeft), Mathf.Abs(hitFromRight));
            float asymmetryRatio = largerHit / Mathf.Max(0.001f, smallerHit);
            Debug.Log($"{LogPrefix} 좌우 크기 비 {asymmetryRatio:F2}배 (허용 4.00배 미만, 실측 정상 범위 1.00~2.15)");

            Assert.Less(asymmetryRatio, 4f,
                $"{LogPrefix} 좌우 타격의 회전 크기가 {asymmetryRatio:F2}배 차이 납니다" +
                $"({hitFromLeft:F2} vs {hitFromRight:F2}) — 진입 포즈/거울상 제한으로 설명되는 " +
                "정상 범위(15회 실측 최대 2.15배)를 크게 벗어났습니다. 한쪽 방향에서만 지렛대나 관절 제한이 " +
                "깨졌을 가능성이 있습니다.");
        }

        // ========================================================================
        // (3) 각속도 절반 삭감이 실제로 사라졌는가 (P9-a의 상수 변경을 직접 잰다)
        // ========================================================================

        [UnityTest]
        public IEnumerator RepeatedEntryNoLongerHalvesExistingAngularVelocity()
        {
            yield return LoadSceneAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RagdollRig rig = bb.GetRagdollRig();

            // 먼저 랙돌 모드로 들어가 루트 회전 제약을 푼다(FreezeRotation이 걸린 채로는 각속도를
            // 넣어도 물리가 즉시 지운다).
            bb.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            yield return null;

            const float Seeded = 123.4f;
            bb.Body.angularVelocity = Seeded;

            // "계속 얻어맞으면 계속 ragdoll" 경로 — 이미 랙돌인데 또 진입 이벤트가 온다.
            rig.EnterRagdoll();
            float after = bb.Body.angularVelocity;

            Debug.Log($"{LogPrefix} 재진입 각속도 — 주입 {Seeded:F1}도/초 -> 진입 후 {after:F1}도/초 " +
                $"(비 {after / Seeded:F3}. 1.000이면 삭감 없음, 0.500이면 예전 거동)");

            Assert.AreEqual(Seeded, after, 0.01f,
                $"{LogPrefix} RAGDOLL 재진입이 각속도를 {after / Seeded:F3}배로 깎았습니다 — " +
                "AngularVelocityDampenOnEntry가 1이 아닙니다(P9-a 되돌아감).");

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        // ========================================================================
        // (4) 하위호환 — 인자 없는 경로는 아무 힘도 가하지 않는다
        // ========================================================================

        [UnityTest]
        public IEnumerator ParameterlessEntryAppliesNoImpulseAtAll()
        {
            yield return LoadSceneAndSettle();

            RagdollRig rig = _agent.Blackboard.GetRagdollRig();
            rig.EnterRagdoll();
            Assert.AreEqual(0f, rig.LastEntryImpulse, 1e-6f,
                $"{LogPrefix} 인자 없는 EnterRagdoll()이 충격량을 가했습니다 — 기존 소비자의 거동이 바뀝니다.");

            // 방향 길이가 0이거나 충격량이 0/음수면 새 오버로드도 아무것도 하지 않아야 한다.
            rig.EnterRagdoll(Vector2.zero, 50f);
            Assert.AreEqual(0f, rig.LastEntryImpulse, 1e-6f, $"{LogPrefix} 방향이 0인데 힘을 가했습니다.");

            rig.EnterRagdoll(Vector2.right, 0f);
            Assert.AreEqual(0f, rig.LastEntryImpulse, 1e-6f, $"{LogPrefix} 충격량이 0인데 힘을 가했습니다.");

            rig.EnterRagdoll(Vector2.right, -10f);
            Assert.AreEqual(0f, rig.LastEntryImpulse, 1e-6f, $"{LogPrefix} 충격량이 음수인데 힘을 가했습니다.");

            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        // ========================================================================
        // (5) P9-d — 비대칭 어깨 제한이 런타임 관절까지 실제로 도달하는가
        // ========================================================================

        [UnityTest]
        public IEnumerator AsymmetricShoulderLimitsSurviveTheEntryTimeRemapping()
        {
            yield return LoadSceneAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            bb.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            yield return null;
            yield return new WaitForFixedUpdate();

            // 진입 시 RagdollRig가 referenceAngle 기준으로 제한을 다시 환산한다. 어떤 진입 자세든
            // **허용 범위의 폭**은 해부학 구간의 폭과 같아야 하고(환산은 평행이동일 뿐이다),
            // 진입 자세를 원점으로 하는 jointAngle 좌표계이므로 **0을 포함**해야 한다.
            AssertJointRange(bb, "LeftArm", expectedWidth: 210f);   // 60 + 150
            AssertJointRange(bb, "RightArm", expectedWidth: 210f);
            AssertJointRange(bb, "LeftLeg", expectedWidth: 130f);   // 65 + 65 (네거티브 컨트롤 — 다리는 대칭 유지)
            AssertJointRange(bb, "RightLeg", expectedWidth: 130f);

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        private static void AssertJointRange(StickmanBlackboard bb, string limbName, float expectedWidth)
        {
            Transform limb = bb.Body.transform.Find(limbName);
            Assert.IsNotNull(limb, $"{LogPrefix} 프리팹에서 {limbName}을(를) 찾지 못했습니다.");
            var joint = limb.GetComponent<HingeJoint2D>();
            Assert.IsNotNull(joint, $"{LogPrefix} {limbName}에 HingeJoint2D가 없습니다.");
            Assert.IsTrue(joint.enabled, $"{LogPrefix} {limbName}의 관절이 RAGDOLL인데도 꺼져 있습니다.");

            JointAngleLimits2D applied = joint.limits;
            float width = applied.max - applied.min;
            Debug.Log($"{LogPrefix} {limbName} 적용 제한 [{applied.min:F1}, {applied.max:F1}] 폭={width:F1}도 " +
                $"(기대 폭 {expectedWidth:F0}도)");

            Assert.AreEqual(expectedWidth, width, 0.5f,
                $"{LogPrefix} {limbName}의 허용 폭이 {width:F1}도입니다(기대 {expectedWidth:F0}도) — " +
                "프리팹의 해부학 구간이 런타임 환산에서 잘렸거나 프리팹 값이 코드와 어긋났습니다.");
            Assert.LessOrEqual(applied.min, 0f,
                $"{LogPrefix} {limbName}의 적용 범위가 0을 포함하지 않습니다([{applied.min:F1},{applied.max:F1}]) — " +
                "진입 순간 이미 제한 위반이라 솔버가 마디를 튕겨 넣습니다(2026-08-29의 '픽 하는 꺾임').");
            Assert.GreaterOrEqual(applied.max, 0f,
                $"{LogPrefix} {limbName}의 적용 범위가 0을 포함하지 않습니다([{applied.min:F1},{applied.max:F1}]).");
        }

        // ========================================================================
        // 헬퍼
        // ========================================================================

        /// <summary>
        /// 측정용 충격량. <b>실측(2026-09-01)으로 정한 값이다</b> — 루트 질량 1, 지렛대 0.218유닛에서
        /// 관성모멘트가 약 0.28이라 <b>1N·s당 약 45도/초</b>가 실린다. 그래서 게임의 랙돌 임계값
        /// 5배(40N·s)를 그대로 넣으면 진입 각속도가 <b>초당 5회전</b>이 되어 "얻어맞아 넘어짐"이 아니라
        /// 팽이가 된다. 여기서는 넘어지는 회전으로 그럴듯한 자리수(약 180도/초)를 쓴다.
        ///
        /// ★ 이 수치는 <b>리더 보고 대상</b>이었다: 나중에 누가 이 오버로드에 생산자를 배선할 때
        /// <c>LastImpactMagnitude</c>를 그대로 넘기면 안 된다는 뜻이기 때문이다(별도 환산 계수 필요).
        /// <b>2026-09-01 P9-b에서 그 배선이 실제로 이루어졌다</b> —
        /// <c>RagdollImpactResolver.ResolveEntryImpulse()</c>가 환산과 상한 클램프를 맡고,
        /// 감도는 <c>StickConfig.ragdollEntryAngularSensitivityPerImpulse</c>로 노출됐다(실측 42.8).
        /// </summary>
        private float ImpulseMagnitude()
        {
            float threshold = _agent.Blackboard.Config != null ? _agent.Blackboard.Config.ragdollForceThreshold : 8f;
            return threshold * 0.5f;
        }

        /// <summary>
        /// 리그 실측 감도(가슴 지점에 1N·s당 루트 각속도 도/초). P9-b가 이 값을 StickConfig에 노출하기
        /// 전에는 이 테스트 안에 숫자로 적혀 있었는데, 그러면 캐릭터 질량/지렛대가 바뀔 때 <b>제품 코드와
        /// 테스트가 각자 다른 상수를 들고</b> 어긋난다. 이제 둘 다 같은 필드 하나를 읽는다.
        /// </summary>
        private float SensitivityDegreesPerImpulse()
            => _agent.Blackboard.Config != null
                ? _agent.Blackboard.Config.ragdollEntryAngularSensitivityPerImpulse
                : 42.8f;

        /// <summary>
        /// RAGDOLL로 강제 전이시키고 그 직후 충격량을 실은 뒤, 첫 몇 물리 스텝 동안의 루트 각속도
        /// (절댓값이 가장 큰 값, 부호 포함)를 돌려준다.
        ///
        /// ★ 측정마다 <b>씬을 다시 로드한다.</b> 처음에는 Idle로 되돌리고 이어서 재기 시작했는데,
        /// 앞 측정에서 날아간 몸이 아직 공중에 있고 루트가 돌아가 있어 <b>가슴 지점이 질량중심보다
        /// 아래</b>가 되는 상태에서 다음 측정이 시작됐다(실측 로그: 지렛대 −1.269유닛). 그러면 토크
        /// 부호가 뒤집혀 측정 자체가 무의미해진다 — 초기 조건을 코드로 보장한다.
        /// </summary>
        private IEnumerator MeasureEntryAngularVelocity(Vector2 direction, float impulse,
            System.Action<float> report)
        {
            yield return LoadSceneAndSettle();

            StickmanBlackboard bb = _agent.Blackboard;
            RagdollRig rig = bb.GetRagdollRig();

            Assert.IsTrue(rig.TryGetChestWorldPoint(out Vector2 chest), $"{LogPrefix} 가슴 지점 유도 실패.");
            float lever = chest.y - bb.Body.worldCenterOfMass.y;
            Assert.Greater(lever, 0f,
                $"{LogPrefix} 측정 시작 시점의 지렛대가 {lever:F3}유닛(≤0)입니다 — 초기 조건이 오염됐습니다.");

            bb.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            rig.EnterRagdoll(direction, impulse);

            float extreme = 0f;
            for (int i = 0; i < ObserveFixedSteps; i++)
            {
                yield return new WaitForFixedUpdate();
                float w = bb.Body.angularVelocity;
                if (Mathf.Abs(w) > Mathf.Abs(extreme)) extreme = w;
            }
            report(extreme);
        }
    }
}
