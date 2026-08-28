using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// Walk 상태 동안 양다리(+여유 시 양팔)의 HingeJoint2D를 서로 반대 위상의 사인파 목표각으로
    /// 구동해 "걷는 것처럼 보이는" 최소한의 절차적 애니메이션을 만든다.
    ///
    /// 왜 필요한가(Architect 진단, 2026-08-28): 이 프로젝트에는 걷기 애니메이션이 애초에 구현된 적이
    /// 없었다 — RagdollRig의 모터 구동 로직은 GETUP(널브러진 자세 -> 직립)에만 쓰였고, Walk 중에는
    /// 캐릭터 전체가 통짜로 옆으로 미끄러지기만 했다(팔다리는 서로 붙어 고정된 채). 사용자가 "보여도
    /// 제대로 안 움직인다"고 지적한 원인이 바로 이것이다. 정교한 IK가 아니라, HingeJoint2D 모터로
    /// 목표각을 사인파로 흔드는 최소 구현으로 충분하다는 것이 이번 라운드 Architect 결정 스코프다.
    ///
    /// 이름으로 관절을 찾는 이유: RagdollRig._joints는 GetComponentsInChildren 순회 순서에만 의존해
    /// "이 관절이 왼다리인지 오른다리인지"를 알 방법이 없다(순서는 계층 구조 순서일 뿐 좌우 의미가
    /// 없음). 여기서는 Stickman.prefab 계층의 실제 GameObject 이름("LeftLeg"/"RightLeg"/"LeftArm"/
    /// "RightArm")으로 명시적으로 찾는다. RagdollRig와 동일하게 생성 시 1회만 GetComponentsInChildren로
    /// 탐색하고 캐싱한다(매 프레임 재탐색 금지 컨벤션 준수).
    ///
    /// RagdollRig/AutoWanderController/FootholdPoller와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수
    /// C# 클래스로 작성한다. StickmanBlackboard.GetWalkCycleAnimator()가 Body.transform을 루트로 최초
    /// 1회만 생성해 캐싱한다.
    ///
    /// RAGDOLL과의 충돌 방지(중요): WalkState가 외력으로 강제 인터럽트되어 Ragdoll로 전이하더라도,
    /// StickmanStateMachine.ChangeState()는 항상 "새 상태 Enter() 이전에 이전 상태 Exit()"을 먼저
    /// 호출한다(무조건 실행, isForcedInterrupt 여부와 무관 — StickmanStateMachine.cs 참고). 따라서
    /// WalkState.Exit()이 이 클래스의 <see cref="StopWalking"/>을 호출해 다리/팔 모터와 각도 제한을
    /// 확실히 원복하면, RagdollState.Enter() -> RagdollRig.EnterRagdoll()이 그 다음에 실행되어 모든
    /// 관절 모터를 다시 한 번 끄는 순서가 항상 보장되고, 그 반대 순서(모터를 끈 뒤 이 애니메이터가
    /// 다시 켜버리는 경쟁 상태)는 발생할 수 없다 — 이 애니메이터의 Tick()은 오직 WalkState.Tick()이
    /// 살아있는 동안에만 호출되기 때문이다.
    ///
    /// 각도 제한/모터 속도 상한(2026-08-28, Architect 실측 지적 대응 — "관절이 다 부러짐" 사고): 최초
    /// 구현은 HingeJoint2D.useLimits를 켜지 않았고(프리팹 기본값 그대로 무제한 회전) 비례 제어 게인도
    /// 높았다(8). 실제 .app 실행에서 사용자가 스크린샷으로 확인한 결과, Walk 진입 시점의 초기 각도
    /// 오차만으로 모터 속도 명령이 순간적으로 매우 커져(오차×게인, 다리는 질량 0.15kg로 관성모멘트가
    /// 극히 작아 그 명령을 거의 그대로 따라감) 다리가 몸통 반대편까지 완전히 감겨버리는 사고가 실측
    /// 확인됐다(Player.log: rightLeg.jointAngle이 -350도 부근에 고착). 이번 수정으로 두 겹 안전장치를
    /// 추가했다: (1) EnterWalking()이 각 관절에 물리적 각도 제한(HingeJoint2D.useLimits=true +
    /// JointAngleLimits2D)을 걸어 해부학적으로 불가능한 각도까지 절대 돌아가지 못하게 하고,
    /// (2) DriveJoint()가 계산된 모터 속도를 항상 maxMotorSpeedDegPerSec로 클램프해 큰 초기 오차가
    /// 있어도 항상 부드럽게 목표를 향해 수렴하도록 강제한다(급발진 방지). StopWalking()은 각도 제한도
    /// useLimits=false로 원복해 RAGDOLL/GETUP의 기존(무제한 자유 회전) 동작에는 전혀 영향을 주지 않는다.
    /// </summary>
    public sealed class WalkCycleAnimator
    {
        private const float ArmSwingRatio = 0.5f;

        private readonly HingeJoint2D _leftLeg;
        private readonly HingeJoint2D _rightLeg;
        private readonly HingeJoint2D _leftArm;
        private readonly HingeJoint2D _rightArm;

        private readonly bool _hasAnyJoint;

        // Walk 진입 시점부터 누적되는 위상 시간(초) — Enter마다 리셋해 매번 같은 자세에서 사이클을 시작한다.
        private float _phaseTime;

        // TEMP DEBUG(2026-08-28 검증 라운드 전용, 제거 예정) — 관절 각도 진동/각도제한 실측용 로그 스로틀.
        private float _debugLogTimer;

        public WalkCycleAnimator(Transform root)
        {
            if (root == null) return;

            HingeJoint2D[] joints = root.GetComponentsInChildren<HingeJoint2D>(true);
            for (int i = 0; i < joints.Length; i++)
            {
                HingeJoint2D j = joints[i];
                if (j == null) continue;
                switch (j.gameObject.name)
                {
                    case "LeftLeg": _leftLeg = j; break;
                    case "RightLeg": _rightLeg = j; break;
                    case "LeftArm": _leftArm = j; break;
                    case "RightArm": _rightArm = j; break;
                }
            }

            _hasAnyJoint = _leftLeg != null || _rightLeg != null || _leftArm != null || _rightArm != null;
        }

        /// <summary>
        /// WalkState.Enter()가 호출 — 위상 타이머를 리셋해 항상 같은 자세(0도)에서 흔들기를 시작하고,
        /// 다리/팔 관절에 물리적 각도 제한(중립 0도 기준 좌우 대칭)을 건다(클래스 문서 "각도 제한/모터
        /// 속도 상한" 참고). Walk를 벗어나면 StopWalking()이 다시 useLimits=false로 원복한다.
        /// </summary>
        public void EnterWalking(float legAngleLimitDegrees, float armAngleLimitDegrees)
        {
            _phaseTime = 0f;
            SetAngleLimit(_leftLeg, legAngleLimitDegrees);
            SetAngleLimit(_rightLeg, legAngleLimitDegrees);
            SetAngleLimit(_leftArm, armAngleLimitDegrees);
            SetAngleLimit(_rightArm, armAngleLimitDegrees);
        }

        /// <summary>
        /// WalkState.Tick()이 매 프레임 호출한다. horizontalSpeedAbs(현재 실제 수평 속도의 절댓값)에
        /// StickConfig.walkCycleFrequencyPerSpeed를 곱해 주파수를 산출하므로, 빠르게 걸을수록 다리도
        /// 빨리 움직인다(정밀한 보행 사이클 동기화가 목적이 아니라 "걷는 것처럼 보이는" 최소 근사치).
        /// maxMotorSpeedDegPerSec: 계산된 모터 속도의 절댓값 상한(급발진 방지, 클래스 문서 참고).
        /// </summary>
        public void Tick(float deltaTime, float horizontalSpeedAbs, float frequencyPerSpeed, float legSwingDegrees,
            float motorGainDegPerSec, float maxMotorTorque, float maxMotorSpeedDegPerSec)
        {
            if (!_hasAnyJoint) return;

            float frequencyHz = frequencyPerSpeed * horizontalSpeedAbs;
            _phaseTime += deltaTime;

            // 왼다리 각도 = A*sin(2*pi*f*t), 오른다리는 위상차 π(반대 방향) = -A*sin(2*pi*f*t).
            float legAngle = Mathf.Sin(_phaseTime * frequencyHz * Mathf.PI * 2f) * legSwingDegrees;

            DriveJoint(_leftLeg, legAngle, motorGainDegPerSec, maxMotorTorque, maxMotorSpeedDegPerSec);
            DriveJoint(_rightLeg, -legAngle, motorGainDegPerSec, maxMotorTorque, maxMotorSpeedDegPerSec);

            // 팔은 필수 구현은 아니지만(Architect 스코프 "여유 되면"), 다리와 반대 위상으로 진폭을 줄여
            // 살짝만 흔들어 자연스러운 보행 반동을 더한다.
            float armAngle = -legAngle * ArmSwingRatio;
            DriveJoint(_leftArm, armAngle, motorGainDegPerSec, maxMotorTorque, maxMotorSpeedDegPerSec);
            DriveJoint(_rightArm, -armAngle, motorGainDegPerSec, maxMotorTorque, maxMotorSpeedDegPerSec);

            // TEMP DEBUG(2026-08-28 검증 라운드 전용, 제거 예정) — 실제 .app 실행 중 다리
            // HingeJoint2D.jointAngle이 시간에 따라 정말로 (각도 제한 범위 안에서 부드럽게) 진동하는지
            // Player.log로 실측 확인하기 위한 임시 로그. 검증 완료 후 이 블록과 _debugLogTimer 필드를
            // 제거한다(Tasklist.md 참고).
            _debugLogTimer += deltaTime;
            if (_debugLogTimer >= 0.5f)
            {
                _debugLogTimer = 0f;
                float leftAngle = _leftLeg != null ? _leftLeg.jointAngle : 0f;
                float rightAngle = _rightLeg != null ? _rightLeg.jointAngle : 0f;
                float leftSpeed = _leftLeg != null ? _leftLeg.motor.motorSpeed : 0f;
                Debug.Log($"[WalkCycleAnimator][TEMP-DEBUG] t={_phaseTime:F2} freqHz={frequencyHz:F2} " +
                    $"targetLegAngle={legAngle:F1} leftLeg.jointAngle={leftAngle:F1} rightLeg.jointAngle={rightAngle:F1} " +
                    $"leftLeg.motorSpeed={leftSpeed:F1}");
            }
        }

        /// <summary>
        /// Walk 이탈 시(Idle/Fall/Jump/Ragdoll 등 어디로든) WalkState.Exit()가 호출 — 모든 다리/팔
        /// 모터를 끄고 목표각 추종을 중단하며, EnterWalking()에서 걸었던 각도 제한도 원복(useLimits=false)
        /// 한다. RAGDOLL로 강제 인터럽트되는 경우에도 이 호출이 먼저 보장된다(클래스 문서 "RAGDOLL과의
        /// 충돌 방지" 참고) — 이후 RagdollRig.EnterRagdoll()이 같은 관절들의 모터를 다시 한번 끄더라도
        /// 안전하게 멱등적이고, 각도 제한이 남아있지 않아 RAGDOLL의 자유 낙하 손맛도 그대로 유지된다.
        /// </summary>
        public void StopWalking()
        {
            SetMotorEnabled(_leftLeg, false);
            SetMotorEnabled(_rightLeg, false);
            SetMotorEnabled(_leftArm, false);
            SetMotorEnabled(_rightArm, false);

            ClearAngleLimit(_leftLeg);
            ClearAngleLimit(_rightLeg);
            ClearAngleLimit(_leftArm);
            ClearAngleLimit(_rightArm);
        }

        private static void DriveJoint(HingeJoint2D joint, float targetAngleDeg, float motorGainDegPerSec,
            float maxMotorTorque, float maxMotorSpeedDegPerSec)
        {
            if (joint == null) return;

            float angleError = Mathf.DeltaAngle(joint.jointAngle, targetAngleDeg);
            float speed = angleError * motorGainDegPerSec;
            speed = Mathf.Clamp(speed, -maxMotorSpeedDegPerSec, maxMotorSpeedDegPerSec);

            JointMotor2D motor = joint.motor;
            motor.motorSpeed = speed;
            motor.maxMotorTorque = maxMotorTorque;
            joint.motor = motor;
            joint.useMotor = true;
        }

        private static void SetMotorEnabled(HingeJoint2D joint, bool enabled)
        {
            if (joint == null) return;
            joint.useMotor = enabled;
        }

        private static void SetAngleLimit(HingeJoint2D joint, float limitDegrees)
        {
            if (joint == null) return;
            joint.limits = new JointAngleLimits2D { min = -limitDegrees, max = limitDegrees };
            joint.useLimits = true;
        }

        private static void ClearAngleLimit(HingeJoint2D joint)
        {
            if (joint == null) return;
            joint.useLimits = false;
        }
    }
}
