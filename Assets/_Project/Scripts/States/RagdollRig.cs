using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// Active Ragdoll(아키텍처 0절)의 물리 파츠(Rigidbody2D)와 관절(HingeJoint2D)을 런타임에 자식
    /// 계층에서 찾아 캐싱하고, RAGDOLL(모터 해제, 전신 물리 위임)과 GETUP(널브러진 포즈 -> 직립 포즈
    /// IK/모터 보간) 두 상태가 공유해서 쓰는 헬퍼.
    ///
    /// 왜 여기서 GetComponentsInChildren로 탐색하는가: 실제 캐릭터 프리팹(몸통/머리/양팔/양다리 등
    /// 최소 파츠 구성 + Joint2D 배선)은 Phase 2 범위 밖이다(씬/프리팹 작업). 이 클래스는 Phase 1의
    /// StickmanAgent.Suspend()/Resume()가 이미 쓰고 있는 것과 동일한 패턴
    /// (GetComponentsInChildren&lt;Rigidbody2D&gt;(true))을 재사용해, 프리팹이 나중에 어떤 파츠 구성으로
    /// 만들어지든(몸통+머리+양팔+양다리 등) 코드 수정 없이 그대로 동작하게 한다. 관절은 HingeJoint2D를
    /// 기준으로 잡는다(아키텍처 0절 "Rigidbody2D + Joint2D(HingeJoint2D 또는 유사)").
    ///
    /// FootholdPoller/GroundSensor와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수 C# 클래스로 작성한다.
    /// StickmanBlackboard.GetRagdollRig()가 Body.transform을 루트로 최초 1회만 생성해 캐싱한다(매 프레임
    /// 재탐색 금지 컨벤션 준수).
    /// </summary>
    public sealed class RagdollRig
    {
        private readonly Rigidbody2D[] _bodies;
        private readonly HingeJoint2D[] _joints;
        private readonly float[] _getupStartAngles;

        public RagdollRig(Transform root)
        {
            _bodies = root != null ? root.GetComponentsInChildren<Rigidbody2D>(true) : System.Array.Empty<Rigidbody2D>();
            _joints = root != null ? root.GetComponentsInChildren<HingeJoint2D>(true) : System.Array.Empty<HingeJoint2D>();
            _getupStartAngles = new float[_joints.Length];
        }

        /// <summary>RAGDOLL 진입: 모든 관절의 모터를 꺼 전신을 순수 물리 낙하물로 전환한다
        /// (아키텍처 0절 "능동 상태는 모터/IK, RAGDOLL은 전신 물리에 완전 위임"). Rigidbody2D 자체는
        /// StickmanAgent.Suspend()와 달리 건드리지 않는다 — Ragdoll은 물리 시뮬레이션이 계속 돌아야
        /// 의미가 있는 상태이기 때문이다(시뮬레이션 정지는 오직 전체화면 Suspend 전용).</summary>
        public void EnterRagdoll()
        {
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null) continue;
                _joints[i].useMotor = false;
            }

            // BUG-SW-M4 대응(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md) — 이동
            // 중(Walk) 피격 시 걷기 관성이 HingeJoint2D를 통해 팔다리에 이미 실려 있는 채로 RAGDOLL에
            // 진입하면, Rigidbody2D의 damping만으로는 GetMaxSpeed()가 ragdollSettleSpeedThreshold
            // 이하로 안정적으로 내려가기까지 시간이 오래 걸리거나(실측 8회 중 2회, 전부 Walk 피격에서
            // 15초 관찰 안에 정착 실패) 사실상 걸리지 않는 경우가 있었다. 여기서 각속도만 한 번 절반으로
            // 깎아 초기 회전 관성을 즉시 줄인다 — 선속도(linearVelocity)는 건드리지 않으므로 "충격에
            // 붕 날아가는" 손맛은 그대로 유지되고, 회전만 처음부터 덜 격렬하게 시작해 damping이 나머지를
            // 정리할 시간을 벌어준다.
            const float angularVelocityDampenOnEntry = 0.5f;
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] == null) continue;
                _bodies[i].angularVelocity *= angularVelocityDampenOnEntry;
            }
        }

        /// <summary>
        /// 전신 파츠 중 가장 빠른 속도(최댓값, 평균 아님). RagdollState의 Getup 전이 판정
        /// (StickConfig.ragdollSettleSpeedThreshold)에 사용 — 평균을 쓰면 사지 하나가 계속 요동쳐도
        /// 다른 파츠들에 희석되어 "이미 다 멈췄다"고 오판(너무 이른 기상)할 수 있어 최댓값을 쓴다.
        /// </summary>
        public float GetMaxSpeed()
        {
            float max = 0f;
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] == null) continue;
                float sp = _bodies[i].linearVelocity.magnitude;
                if (sp > max) max = sp;
            }
            return max;
        }

        /// <summary>GETUP 진입: 지금 널브러진 각 관절의 각도를 보간 시작점으로 캡처한다.</summary>
        public void BeginGetup()
        {
            for (int i = 0; i < _joints.Length; i++)
            {
                _getupStartAngles[i] = _joints[i] != null ? _joints[i].jointAngle : 0f;
            }
        }

        /// <summary>
        /// GETUP 진행 — progress(0~1)에 따라 각 관절을 "널브러진 시작 각도"에서 "직립(0도)"으로
        /// 비례 제어(모터)를 통해 보간한다. 실제 프리팹의 관절 배치/목표 각도 튜닝은 Phase 2 범위 밖
        /// (씬/프리팹 작업)이며, 여기서는 "GetupState._getupProgress가 실제로 물리 모터를 구동한다"는
        /// 메커니즘 자체를 보장한다.
        /// </summary>
        public void TickGetup(float progress, float motorGainDegPerSec, float maxMotorTorque)
        {
            for (int i = 0; i < _joints.Length; i++)
            {
                HingeJoint2D j = _joints[i];
                if (j == null) continue;

                float targetAngle = Mathf.LerpAngle(_getupStartAngles[i], 0f, progress);
                float angleError = Mathf.DeltaAngle(j.jointAngle, targetAngle);

                JointMotor2D motor = j.motor;
                motor.motorSpeed = angleError * motorGainDegPerSec;
                motor.maxMotorTorque = maxMotorTorque;
                j.motor = motor;
                j.useMotor = true;
            }
        }
    }
}
