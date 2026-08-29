using System.Text;
using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// 캐릭터 전신의 물리 파츠(Rigidbody2D)와 관절(HingeJoint2D)을 런타임에 자식 계층에서 찾아 캐싱하고,
    /// **"능동 모드"와 "RAGDOLL 모드" 사이의 물리 전환을 단독으로 소유**하는 헬퍼.
    ///
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 2026-08-28 근본 재구현 — 왜 능동 상태에서 물리를 완전히 꺼버리는가
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 사용자가 여러 번 "캐릭터가 제대로 구현 안 됨"이라며 보낸 스크린샷은 매번 같은 모습이었다:
    /// 바닥에 쓰러져 누운 채 팔다리가 제멋대로 뻗어 있음. Architect가 프리팹/코드를 직접 조사해 확정한
    /// 근본 원인은 튜닝 문제가 아니라 설계 문제였다.
    ///   (1) Stickman.prefab의 Rigidbody2D 5개가 전부 m_Constraints: 0 — 루트(몸통) 회전이 전혀
    ///       고정돼 있지 않아 몸통이 자유롭게 넘어질 수 있었다. 서 있을 이유가 없었던 것이다.
    ///   (2) 팔다리 4개가 전부 Dynamic — 관절에 매달린 순수 물리 객체라 중력에 그냥 늘어졌다.
    ///   (3) 그 위에서 HingeJoint2D 모터 토크로 버텨보려 했으나, 모터로 중력과 싸우는 것은 근본적으로
    ///       지는 싸움이라 매번 무너졌다.
    /// 즉 "물리 랙돌이 스스로 서 있기를 기대하는" 잘못된 설계였다. docs/ARCHITECTURE.md 0절의 원래
    /// 의도("능동 상태는 IK/모터로 포즈 제어, RAGDOLL만 전신 물리 위임")는 옳았고, 이제 그 경계를
    /// 코드로 강제한다.
    ///
    ///   <see cref="EnterActiveMode"/> : 루트 회전 고정(FreezeRotation) + 팔다리 Kinematic + 관절 비활성
    ///                                   → 물리가 포즈에 개입할 수 있는 통로를 전부 차단한다.
    ///                                     실제 포즈 각도는 StickmanPoseAnimator가 만든다.
    ///   <see cref="EnterRagdoll"/>    : 루트 회전 제약 해제 + 팔다리 Dynamic + 관절 활성
    ///                                   → 전신 물리에 완전 위임(피격/던짐 손맛은 그대로).
    ///
    /// 두 메서드 모두 **멱등**이며 실제 모드가 바뀔 때만 컴포넌트를 건드린다(매 프레임 호출해도 안전).
    /// StickmanBlackboard.TickPose()가 현재 상태 ID를 보고 매 프레임 둘 중 하나를 호출해, 어떤 경로로
    /// 상태가 바뀌든(강제 인터럽트/전체화면 Suspend 취소/테스트의 직접 ChangeState 포함) 물리 모드가
    /// 상태와 어긋난 채 남을 수 없게 만든다 — 각 상태의 Enter/Exit에 흩어놓지 않은 이유다(상태가
    /// 14개가 넘고, 하나라도 빠뜨리면 그 상태에서만 캐릭터가 다시 무너진다).
    ///
    /// 관절을 EnterActiveMode에서 아예 disable하는 이유: 팔다리가 Kinematic이면 관절은 사실상 무력화
    /// 되지만, HingeJoint2D는 여전히 살아 있어 **Dynamic인 루트 쪽**을 잡아당기는 보정력을 만들 수
    /// 있다(관절은 두 바디 중 Dynamic인 쪽을 움직여 구속을 만족시키려 한다). 포즈 제어가 만든 각도를
    /// 물리가 미세하게 흔드는 경로 자체를 없애려고 컴포넌트를 끈다. 다시 켤 때 위치 오차로 튕기지
    /// 않는 것은 StickmanPoseAnimator가 항상 관절 구속식을 정확히 만족시키는 위치에 팔다리를 두기
    /// 때문이다(그 클래스의 "회전 중심(pivot) 보정" 문서 참고).
    ///
    /// FootholdPoller/GroundSensor와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수 C# 클래스로 작성한다.
    /// StickmanBlackboard.GetRagdollRig()가 Body.transform을 루트로 최초 1회만 생성해 캐싱한다(매 프레임
    /// 재탐색 금지 컨벤션 준수).
    /// </summary>
    public sealed class RagdollRig
    {
        // RAGDOLL 진입 시 각속도를 한 번 깎는 비율(BUG-SW-M4, 아래 EnterRagdoll 참고).
        private const float AngularVelocityDampenOnEntry = 0.5f;

        // 관절 각도 부호 규약을 실측으로 판정할 때(DetectJointAngleSign) 표본으로 쓸 수 있는 최소 진입
        // 각도(도). 이보다 작으면 referenceAngle도 0 근처라 부호를 신뢰할 수 없어 건너뛴다.
        private const float MinAngleForSignDetectionDegrees = 2f;

        /// <summary>
        /// ★ 2026-08-29 — 지금 캐릭터가 어느 쪽을 보고 있는지(+1 오른쪽 / -1 왼쪽)를 물어보는 통로.
        /// StickmanBlackboard가 자신의 FacingSign을 그대로 넘겨준다(캐싱하지 않고 매번 물어본다 —
        /// RAGDOLL 진입 시점의 최신 방향이어야 하기 때문). null이면 항상 +1로 취급한다(테스트/폴백).
        /// 왜 필요한지는 <see cref="EnableJointsWithAnatomicalLimits"/>의 "좌우 반전" 문서 참고.
        /// </summary>
        private readonly System.Func<float> _facingSignProvider;

        private readonly Rigidbody2D _root;
        private readonly Rigidbody2D[] _bodies;      // 루트 포함 전신(GetMaxSpeed용).
        private readonly Rigidbody2D[] _limbBodies;  // 루트 제외 — 모드에 따라 bodyType이 바뀌는 대상.
        private readonly HingeJoint2D[] _joints;

        // 프리팹에 저장된 **해부학 기준** 관절 제한(마디를 완전히 편 상태 = localRotation 0도가 기준).
        // 런타임에 joint.limits를 덮어쓰기 때문에 반드시 생성 시점(=아직 한 번도 덮어쓰지 않은 시점)에
        // 원본을 복사해둬야 한다 — 이후에는 이 배열만 진실로 취급한다.
        private readonly JointAngleLimits2D[] _anatomicalLimits;
        private readonly bool[] _jointUsesLimits;

        // RAGDOLL 진입(관절 enable) 시점의 마디 상대 각도와, 그때 Unity가 잡은 referenceAngle.
        private readonly float[] _entryLocalAngles;
        private readonly float[] _entryReferenceAngles;

        private bool _modeInitialized;
        private bool _isRagdollMode;

        // GETUP 루트 회전 보간의 시작각(널브러진 실제 각도).
        private float _getupStartRootAngle;

        /// <summary>지금 전신 물리에 위임된(RAGDOLL) 모드인지. 능동 모드면 false.</summary>
        public bool IsRagdollMode => _isRagdollMode;

        /// <summary>루트(몸통)의 현재 Z 회전각(도). 0에 가까울수록 똑바로 서 있다 — 실측 검증 기준값.</summary>
        public float RootRotationDegrees => _root != null ? _root.rotation : 0f;

        public RagdollRig(Transform root, System.Func<float> facingSignProvider = null)
        {
            _facingSignProvider = facingSignProvider;
            _bodies = root != null ? root.GetComponentsInChildren<Rigidbody2D>(true) : System.Array.Empty<Rigidbody2D>();
            _joints = root != null ? root.GetComponentsInChildren<HingeJoint2D>(true) : System.Array.Empty<HingeJoint2D>();
            _root = root != null ? root.GetComponent<Rigidbody2D>() : null;

            var limbs = new System.Collections.Generic.List<Rigidbody2D>(_bodies.Length);
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] == null || _bodies[i] == _root) continue;
                limbs.Add(_bodies[i]);
            }
            _limbBodies = limbs.ToArray();

            _anatomicalLimits = new JointAngleLimits2D[_joints.Length];
            _jointUsesLimits = new bool[_joints.Length];
            _entryLocalAngles = new float[_joints.Length];
            _entryReferenceAngles = new float[_joints.Length];
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null) continue;
                _anatomicalLimits[i] = _joints[i].limits;
                _jointUsesLimits[i] = _joints[i].useLimits;
            }
        }

        /// <summary>
        /// 능동 모드 진입(Idle/Walk/Jump/Fall/ParkourClimb/Attack/Getup/… 전부): 관절을 끄고 팔다리를
        /// Kinematic으로 되돌린 뒤, 루트 회전을 FreezeRotation으로 고정한다. 이 시점 이후 캐릭터가
        /// 넘어지는 것은 물리적으로 불가능하다 — 회전을 만들 수 있는 주체가 하나도 남지 않기 때문이다.
        ///
        /// 루트 각도를 여기서 0으로 스냅하지는 않는다(GETUP이 "널브러진 각도 -> 직립"을 눈에 보이게
        /// 보간해야 하므로). 스냅은 호출자가 <see cref="SnapRootUpright"/>로 따로 요청한다.
        /// </summary>
        /// <returns>이번 호출로 실제 모드 전환이 일어났으면 true(이미 능동 모드였으면 false).</returns>
        public bool EnterActiveMode()
        {
            if (_modeInitialized && !_isRagdollMode) return false;
            _modeInitialized = true;
            _isRagdollMode = false;

            // 순서 중요: 관절을 먼저 끊고 나서 bodyType을 바꾼다(활성 관절이 붙은 채 Dynamic->Kinematic
            // 전환이 일어나면 그 프레임에 관절이 루트 쪽으로 보정 임펄스를 흘릴 수 있다).
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null) continue;
                _joints[i].useMotor = false;
                _joints[i].enabled = false;
            }

            for (int i = 0; i < _limbBodies.Length; i++)
            {
                Rigidbody2D rb = _limbBodies[i];
                if (rb == null) continue;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_root != null)
            {
                _root.angularVelocity = 0f;
                _root.constraints |= RigidbodyConstraints2D.FreezeRotation;
            }
            return true;
        }

        /// <summary>
        /// 루트를 정확히 직립(회전 0도)으로 스냅한다. FreezeRotation이 걸려 있어도 Rigidbody2D.rotation
        /// 직접 대입은 항상 유효하다(제약은 "물리가 회전시키는 것"만 막는다). GETUP을 제외한 모든 능동
        /// 상태에서 매 프레임 호출해 "루트 회전각 ≈ 0"이라는 이번 재구현의 성공 판정 기준을 코드로 보장한다.
        /// </summary>
        public void SnapRootUpright()
        {
            if (_root == null) return;
            if (Mathf.Abs(Mathf.DeltaAngle(_root.rotation, 0f)) > 0.01f)
            {
                _root.rotation = 0f;
            }
            if (_root.angularVelocity != 0f) _root.angularVelocity = 0f;
        }

        /// <summary>
        /// RAGDOLL 진입: 루트 회전 제약을 풀고 팔다리를 Dynamic으로 되돌린 뒤 관절을 다시 켜 전신을
        /// 순수 물리 낙하물로 전환한다(아키텍처 0절 "RAGDOLL은 전신 물리에 완전 위임"). Rigidbody2D의
        /// simulated는 건드리지 않는다 — 시뮬레이션 정지는 오직 전체화면 Suspend 전용이다.
        ///
        /// BUG-SW-M4 대응(2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md)은 그대로 유지: 진입 시 각속도만
        /// 한 번 절반으로 깎아 초기 회전 관성을 줄인다(선속도는 건드리지 않으므로 "충격에 붕 날아가는"
        /// 손맛은 그대로, 회전만 덜 격렬하게 시작해 damping이 정리할 시간을 번다). 실측상 이 완충이
        /// 없으면 Walk 중 피격 RAGDOLL이 ragdollSettleSpeedThreshold 아래로 안정적으로 내려오지 못해
        /// GETUP 복귀에 실패하는 경우가 있었다.
        /// </summary>
        public void EnterRagdoll()
        {
            EnsureRagdollMode();

            // 각속도 완충은 "RAGDOLL 진입 이벤트"마다 1회 적용한다(모드 전환 여부와 무관) — 이미 RAGDOLL인
            // 상태에서 또 얻어맞아 RagdollState.Enter()가 재실행되는 경우("계속 얻어맞으면 계속 ragdoll")에도
            // 새로 실린 회전 관성을 같은 방식으로 한 번 깎아주기 위해서다. 그래서 이 루프는 매 프레임
            // 호출되는 EnsureRagdollMode()가 아니라 이 메서드에만 있다 — 매 프레임 곱하면 각속도가
            // 순식간에 0이 되어 RAGDOLL이 굴러가지 않는 정반대의 버그가 된다.
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (_bodies[i] == null) continue;
                _bodies[i].angularVelocity *= AngularVelocityDampenOnEntry;
            }
        }

        /// <summary>
        /// RAGDOLL 물리 모드임을 보장하는 멱등 연산(StickmanBlackboard.TickPose()가 매 프레임 호출).
        /// 이미 RAGDOLL 모드면 아무것도 하지 않는다 — 진입 이벤트성 처리(각속도 완충)는 여기 두지 않고
        /// <see cref="EnterRagdoll"/>에만 둔다.
        /// </summary>
        /// <returns>이번 호출로 실제 모드 전환이 일어났으면 true.</returns>
        public bool EnsureRagdollMode()
        {
            if (_modeInitialized && _isRagdollMode) return false;
            _modeInitialized = true;
            _isRagdollMode = true;

            if (_root != null)
            {
                _root.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
            }

            for (int i = 0; i < _limbBodies.Length; i++)
            {
                if (_limbBodies[i] == null) continue;
                _limbBodies[i].bodyType = RigidbodyType2D.Dynamic;
            }

            // 순서 중요: 팔다리가 Dynamic이 된 뒤에 관절을 켠다(Kinematic 상태로 켜면 관절이 루트만
            // 잡아당긴다). StickmanPoseAnimator가 항상 구속식을 만족하는 위치에 팔다리를 두고 있어
            // 이 시점에 위치 오차로 인한 튕김은 발생하지 않는다.
            EnableJointsWithAnatomicalLimits();
            return true;
        }

        /// <summary>
        /// ★ 2026-08-28 — 이월 과제 해소: "RAGDOLL 중 팔꿈치가 관절 제한을 순간 초과하는 현상 —
        /// HingeJoint2D 제한이 enable 시점 자세 기준으로 재해석됨"(Tasklist.md).
        ///
        /// 무엇이 문제였나: HingeJoint2D의 각도 제한은 **절대 각도가 아니라 jointAngle에 대한 제한**이고,
        /// jointAngle은 "관절이 만들어진 순간의 상대 각도(referenceAngle)로부터 얼마나 돌아갔는가"다.
        /// 이 프로젝트는 능동 모드에서 관절을 통째로 disable하고 RAGDOLL에서 다시 enable하므로, 관절은
        /// **매 RAGDOLL 진입마다 새로 만들어지고 그때의 포즈가 referenceAngle로 굳는다.** 그래서 프리팹에
        /// 적어둔 제한(예: 팔꿈치 +3~+100)이 진입 포즈만큼 통째로 밀려 해석됐고, 실측에서 팔꿈치가
        /// -59도(= 반대로 꺾임)까지 가는 것이 관찰됐다.
        ///
        /// 어떻게 고쳤나: 관절을 켠 직후 Unity가 확정한 referenceAngle을 읽어, 프리팹의 해부학 기준
        /// 제한을 그 좌표계로 **다시 환산해** 넣는다. 진입 포즈가 무엇이든 최종 허용 범위는 항상 같은
        /// 해부학적 구간이 된다(무릎은 늘 뒤로만, 팔꿈치는 늘 앞으로만, 둘 다 완전한 일직선 금지).
        ///
        /// 부호 규약을 하드코딩하지 않는 이유: jointAngle이 "자기 바디 - 연결 바디"인지 그 반대인지는
        /// Unity 내부 구현에 달려 있어 문서만으로 확정할 수 없다. 대신 진입 시점에 이미 알고 있는 값
        /// (마디의 localRotation)과 Unity가 답한 referenceAngle의 **부호를 비교**해 규약을 실측 판정한다
        /// (DetectJointAngleSign). 판정이 틀릴 여지를 남기지 않으면서, 엔진 버전이 바뀌어도 자동으로 따라간다.
        /// </summary>
        private void EnableJointsWithAnatomicalLimits()
        {
            // 1차 패스: 진입 자세를 먼저 기록하고 관절을 켠 뒤, Unity가 확정한 referenceAngle을 수집한다.
            for (int i = 0; i < _joints.Length; i++)
            {
                HingeJoint2D joint = _joints[i];
                if (joint == null) continue;
                _entryLocalAngles[i] = Mathf.DeltaAngle(0f, joint.transform.localEulerAngles.z);
                joint.useMotor = false;
                joint.enabled = true;
                _entryReferenceAngles[i] = Mathf.DeltaAngle(0f, joint.referenceAngle);
            }

            float sign = DetectJointAngleSign();

            // ★★ 좌우 반전(2026-08-29, 사용자 신고 "넘어질때도 이상하게 넘어지고")
            // ------------------------------------------------------------------------------
            // 이 캐릭터의 좌우 반전은 Transform.localScale.x = -1이 아니라 **각도 부호 뒤집기**로
            // 구현돼 있다(States/StickmanPoseAnimator.cs: `angleDegrees * _facingSign`). 그런데
            // 프리팹에 적힌 해부학 제한은 좌우 비대칭이면서(무릎 [-100,-3], 팔꿈치 [3,100] — "한쪽으로만
            // 굽는다"를 인코딩한 값이다) **반전되지 않은 채 그대로** 쓰이고 있었다. 그 결과:
            //
            //   · 오른쪽을 볼 때(facing +1): 서 있는 무릎의 로컬각 -4도 -> 해부학 [-100,-3] 안. 정상.
            //   · 왼쪽을 볼 때 (facing -1): 서 있는 무릎의 로컬각 +4도 -> 해부학 [-100,-3] **밖**.
            //
            // 즉 왼쪽을 보고 넘어지면 (1) 진입 순간 무릎/팔꿈치가 제한 밖에 있어 솔버가 7~13도를
            // 즉시 튕겨 넣고(넘어지기 시작하는 첫 프레임의 '픽' 하는 부자연스러운 꺾임), (2) 그 뒤로도
            // 허용 범위가 **거울상 반대쪽**이라 무릎이 앞으로 꺾이고 팔꿈치가 뒤로 꺾인 채 뒹군다.
            // 실측 로그에도 그대로 남아 있었다: "LeftLegLower: 진입각 4.0도, 해부학 [-100,-3] ->
            // 적용 [7,104]" — 적용 범위 [7,104]에 진입값 0이 들어있지 않다(= 진입 순간 위반 상태).
            //
            // 해법: 해부학 제한도 포즈와 **같은 방식으로** 반전한다([min,max] -> [-max,-min]).
            // 이러면 어느 쪽을 보든 진입 자세가 항상 허용 범위 안에 들어오고(튕김 소멸), 굽는 방향도
            // 항상 해부학적으로 옳은 쪽이 된다. 검증법: 아래 [RagdollRig] 로그에서 **적용 범위가 항상
            // 0을 포함**하는지 보면 된다(진입각 기준 jointAngle은 0에서 시작하므로).
            float facing = _facingSignProvider != null ? _facingSignProvider() : 1f;
            bool mirrored = facing < 0f;

            // 2차 패스: 해부학 기준 제한을 이번 진입의 jointAngle 좌표계로 환산해 적용한다.
            //   sign = +1 : jointAngle = (각도 - 진입각)        -> [aMin - 진입각, aMax - 진입각]
            //   sign = -1 : jointAngle = -(각도 - 진입각)       -> [진입각 - aMax, 진입각 - aMin]
            for (int i = 0; i < _joints.Length; i++)
            {
                HingeJoint2D joint = _joints[i];
                if (joint == null || !_jointUsesLimits[i]) continue;

                JointAngleLimits2D anatomical = MirrorIfFacingLeft(_anatomicalLimits[i], mirrored);
                float entry = _entryLocalAngles[i];
                joint.limits = sign >= 0f
                    ? new JointAngleLimits2D { min = anatomical.min - entry, max = anatomical.max - entry }
                    : new JointAngleLimits2D { min = entry - anatomical.max, max = entry - anatomical.min };
                joint.useLimits = true;
            }

            LogJointLimitDiagnostics(sign, mirrored);
        }

        /// <summary>
        /// 해부학 제한을 좌우 반전한다. 포즈가 각도에 -1을 곱해 반전되므로(위 문서), 제한 구간도
        /// 같은 변환을 거쳐야 한다 — [min,max]에 -1을 곱하면 순서가 뒤집히므로 [-max,-min]이다.
        /// </summary>
        private static JointAngleLimits2D MirrorIfFacingLeft(JointAngleLimits2D limits, bool mirrored)
        {
            if (!mirrored) return limits;
            return new JointAngleLimits2D { min = -limits.max, max = -limits.min };
        }

        /// <summary>
        /// jointAngle이 마디의 localRotation과 같은 방향으로 증가하는지(+1) 반대인지(-1)를 실측 판정한다.
        /// 관절이 방금 만들어졌으므로 referenceAngle = (부호) x (진입 시 상대 각도)이고, 진입 각도는
        /// 우리가 이미 알고 있다 — 두 값의 부호만 비교하면 규약이 나온다(크기는 쓰지 않으므로 엔진이
        /// 도/라디안 중 무엇으로 답하든 무관). 규약은 엔진 전역이라 표본 하나로 충분하지만, 각도가 작은
        /// 관절은 오차에 취약하므로 **진입 각도의 절댓값이 가장 큰 관절**을 표본으로 고른다(어깨 40도).
        /// 쓸 만한 표본이 하나도 없으면(전신이 정확히 일직선인 이론상의 경우) +1로 둔다 — 그 경우
        /// 진입각이 0이라 두 공식의 결과 차이도 좌우 대칭 구간에서는 사라진다.
        /// </summary>
        private float DetectJointAngleSign()
        {
            float bestAbsAngle = 0f;
            float sign = 1f;
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null) continue;
                float entry = _entryLocalAngles[i];
                float reference = _entryReferenceAngles[i];
                if (Mathf.Abs(entry) < MinAngleForSignDetectionDegrees) continue;
                if (Mathf.Abs(reference) < 0.0001f) continue;
                if (Mathf.Abs(entry) <= bestAbsAngle) continue;

                bestAbsAngle = Mathf.Abs(entry);
                sign = reference * entry >= 0f ? 1f : -1f;
            }
            return sign;
        }

        /// <summary>
        /// RAGDOLL 진입마다 1회(모드 전환 시에만 호출되므로 에피소드당 한 줄) 관절 제한 환산 결과를 남긴다.
        /// 에이전트는 마우스를 조작해 캐릭터를 던져볼 수 없으므로, 랙돌 자세가 다시 이상해졌다는 신고가
        /// 오면 이 한 줄로 "제한이 실제로 해부학 기준으로 적용됐는지"를 로그만으로 확정할 수 있어야 한다.
        /// </summary>
        private void LogJointLimitDiagnostics(float sign, bool mirrored)
        {
            var sb = new StringBuilder();
            sb.Append("[RagdollRig] RAGDOLL 관절 제한 적용 — jointAngle 부호 규약=").Append(sign >= 0f ? "+1(로컬각과 동일)" : "-1(로컬각과 반대)");
            sb.Append(", 바라보는 방향=").Append(mirrored ? "왼쪽(해부학 제한 좌우반전 적용)" : "오른쪽(반전 없음)");
            for (int i = 0; i < _joints.Length; i++)
            {
                if (_joints[i] == null || !_jointUsesLimits[i]) continue;
                sb.Append(" | ").Append(_joints[i].name)
                  .Append(": 진입각 ").Append(_entryLocalAngles[i].ToString("F1"))
                  .Append("도, 해부학 [").Append(_anatomicalLimits[i].min.ToString("F0")).Append(",").Append(_anatomicalLimits[i].max.ToString("F0"))
                  .Append("] -> 적용 [").Append(_joints[i].limits.min.ToString("F0")).Append(",").Append(_joints[i].limits.max.ToString("F0")).Append("]");
            }
            Debug.Log(sb.ToString());
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

        /// <summary>GETUP 진입: 널브러진 루트 회전각을 보간 시작점으로 캡처한다.</summary>
        public void BeginGetup()
        {
            _getupStartRootAngle = _root != null ? _root.rotation : 0f;
        }

        /// <summary>
        /// GETUP 진행 — progress(0~1)에 따라 루트 회전각을 "널브러진 각도"에서 "직립(0도)"으로 직접
        /// 보간한다. 모터 토크로 몸을 일으키던 예전 방식과 달리 결정론적이라 progress=1이면 반드시
        /// 정확히 0도가 되며, 중간에 다시 쓰러지는 경로 자체가 존재하지 않는다. 팔다리 각도 보간은
        /// StickmanPoseAnimator.TickGetupPose()가 같은 progress로 동시에 수행한다.
        /// </summary>
        public void TickGetupRoot(float progress)
        {
            if (_root == null) return;
            _root.angularVelocity = 0f;
            _root.rotation = Mathf.LerpAngle(_getupStartRootAngle, 0f, Mathf.Clamp01(progress));
        }
    }
}
