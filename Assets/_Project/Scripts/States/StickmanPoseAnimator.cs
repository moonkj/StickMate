using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// 능동 상태(Idle/Walk/Jump/Fall/ParkourClimb/Attack/Dragged/…)에서 캐릭터의 팔다리 포즈를
    /// **물리를 전혀 쓰지 않고** transform.localRotation으로 직접 만드는 순수 절차적 애니메이터.
    ///
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 왜 물리(HingeJoint2D 모터) 기반 능동 포즈를 완전히 폐기했는가 (2026-08-28, Architect 결정)
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 이전 구현은 루트 회전이 고정돼 있지 않고(프리팹 m_Constraints: 0) 팔다리가 전부 Dynamic인 채로
    /// HingeJoint2D 모터 토크로 중력과 싸워 "서 있는 포즈"를 유지하려 했다. 결과는 사용자가 여러 번
    /// 스크린샷으로 보고한 그대로 — 바닥에 쓰러져 누운 채 팔다리가 제멋대로 뻗어 있었다. 이것은 튜닝으로
    /// 고칠 수 있는 문제가 아니라 설계 오류다: 몸통 회전이 자유로운 랙돌은 스스로 서 있을 이유가 없고,
    /// 모터 토크로 중력과 싸우는 것은 근본적으로 지는 싸움이다. 이제 경계를 코드로 강제한다:
    ///
    ///   능동 상태 : 루트 회전 고정(FreezeRotation) + 전 마디 Kinematic + 이 클래스가 각도 직접 세팅
    ///               → 100% 예측 가능. 절대 무너지지 않는다. 목표 각도가 곧 실제 각도다.
    ///   RAGDOLL   : 루트 회전 제약 해제 + 전 마디 Dynamic + 관절 활성화 → 전신 물리에 완전 위임.
    ///
    /// 물리 모드 전환은 이 클래스가 아니라 <see cref="RagdollRig"/>가 단독 소유한다. 이 클래스는 오직
    /// "각도"만 책임진다 — 두 클래스가 같은 컴포넌트를 서로 모르게 건드리는 경쟁 상태를 만들지 않기
    /// 위한 의도적 역할 분리다.
    ///
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 2분절(무릎/팔꿈치) 구조 — 2026-08-28 사용자 "손이랑 다리가 다 그냥 막대기 같음" 대응
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 직전까지 팔다리는 각각 **곧은 선 하나**였고 부착점 한 곳에서만 회전했다. 그러면 보간을 아무리
    /// 부드럽게 하고 몸 바운스를 넣어도 "막대기가 흔들리는" 것 이상이 될 수 없다 — 사람이 걷는 게
    /// 자연스러워 보이는 결정적 이유는 **무릎과 팔꿈치가 접히기 때문**이고, 사용자가 제시한 레퍼런스
    /// (Alan Becker 계열 스틱맨 애니메이션)도 확실히 무릎이 접힌다. 그래서 각 팔다리를 2마디로
    /// 재구성했다(리더 결정):
    ///
    ///   위 마디(대퇴/상완)     : 부착점(엉덩이/어깨)이 transform 원점, 선은 (0,0) -> (0,-lenUpper)
    ///   아래 마디(정강이/전완) : **위 마디의 자식**, 원점이 무릎/팔꿈치 지점 (0,-lenUpper),
    ///                            선은 (0,0) -> (0,-lenLower)
    ///
    /// 계층 부모-자식이므로 위 마디를 돌리면 아래 마디가 딸려오고, 아래 마디를 추가로 돌리면 관절이
    /// 접힌다. 각 마디의 localRotation은 **부모 기준 상대 각도**이므로 아래 마디의 각도가 곧 "무릎이
    /// 몇 도 접혔는가"이다.
    ///
    /// 접힘 방향은 한쪽으로만 고정한다(사람 관절은 뒤로 안 꺾인다):
    ///   무릎   <see cref="KneeBendSign"/> = -1 (정강이가 뒤로 접힘, 오른쪽을 보고 선 기준)
    ///   팔꿈치 <see cref="ElbowBendSign"/> = +1 (전완이 앞으로 접힘)
    /// 보행 중 굽힘량은 <c>Max(0, sin(...))</c>으로 항상 0 이상이라, 부호를 곱한 결과가 절대 반대
    /// 방향으로 넘어가지 않는다 — 무릎/팔꿈치가 뒤로 꺾이는 경우의 수가 구조적으로 존재하지 않는다.
    /// 여기에 Idle에서도 완전히 펴지 않고 살짝 굽혀둔다(무릎 몇 도, 팔꿈치 10도 안팎) — 완전히 곧은
    /// 상태가 바로 "막대기" 느낌의 원인이다.
    ///
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// 회전 중심(pivot) / 각도 부호 규약
    /// ─────────────────────────────────────────────────────────────────────────────────────────
    /// transform.localRotation은 항상 그 transform의 원점을 중심으로 회전한다. 그래서 각 마디의 원점을
    /// 그 마디의 관절 위치(부착점 / 무릎 / 팔꿈치)에 두는 것이 이 클래스의 전제조건이며,
    /// SceneBootstrapper.CreateLimb가 정확히 그렇게 만든다. 그래도 <see cref="ApplyAngle"/>은 위치를
    /// 매번 다시 써준다: <c>localPosition = connectedAnchor - (rotation * anchor)</c> (= HingeJoint2D
    /// 구속식 그 자체). 현재 기하학에서는 anchor=(0,0)이라 상수로 축약되지만, RAGDOLL 도중 물리가 마디를
    /// 끌고 다닌 뒤 복귀할 때 위치를 관절로 확실히 되돌려주고, 나중에 기하학이 바뀌어도 시각이 조용히
    /// 깨지지 않게 한다.
    ///
    /// 마디 로컬 +y가 관절 쪽(위), -y가 끝(손/발) 쪽이다. Z축 양의 회전(반시계)은 끝을 +x(화면
    /// 오른쪽)로 보낸다. 따라서 "바깥쪽으로 벌리기"는 왼쪽 = 음수각, 오른쪽 = 양수각.
    ///
    /// RagdollRig/AutoWanderController/FootholdPoller와 같은 컨벤션을 따라 MonoBehaviour가 아닌 순수
    /// C# 클래스이며, StickmanBlackboard.GetPoseAnimator()가 최초 1회만 생성해 캐싱한다(매 프레임 재탐색
    /// 금지 컨벤션 준수).
    /// </summary>
    public sealed class StickmanPoseAnimator
    {
        /// <summary>무릎이 접히는 방향(오른쪽을 보고 선 기준 뒤쪽). 사람 무릎은 이 반대로 꺾이지 않는다.</summary>
        private const float KneeBendSign = -1f;

        /// <summary>팔꿈치가 접히는 방향(앞쪽). 무릎과 반대다.</summary>
        private const float ElbowBendSign = 1f;

        /// <summary>
        /// 보행 사이클에서 무릎 굽힘이 최대가 되는 위상 오프셋(라디안). 다리를 앞으로 스윙해 발을
        /// 들어올릴 때 접히고 딛고 있을 때 펴지는 타이밍을 만든다.
        /// </summary>
        private const float KneeBendPhaseOffset = Mathf.PI * 0.5f;

        /// <summary>팔꿈치 굽힘 변동의 위상 오프셋(라디안). 무릎과 같은 성격.</summary>
        private const float ElbowBendPhaseOffset = Mathf.PI * 0.5f;

        /// <summary>
        /// Walk 중 팔이 유지하는 최소 벌림(중립 팔 각도 대비 비율). 0이면 사인파가 0을 지나는 순간 팔이
        /// 몸통 선과 완전히 겹쳐 사라져 보인다(사용자 "팔이 아예 안 보인다"와 같은 종류의 문제) —
        /// 걷는 동안에도 항상 이만큼은 바깥으로 벌린 채 앞뒤로 흔든다.
        /// </summary>
        private const float ArmWalkBaseRatio = 0.45f;

        /// <summary>
        /// 팔의 보간 계수를 다리 대비 얼마로 낮출지(follow-through). 사지가 전부 같은 타이밍에 딱딱
        /// 맞아떨어지면 로봇처럼 보이므로(사용자 "너무 뻣뻣하게 움직임"), 팔이 다리보다 살짝 늦게
        /// 따라오게 해 자연스러운 시차를 만든다.
        /// </summary>
        private const float ArmSmoothingRatio = 0.55f;

        /// <summary>아래 마디(정강이/전완)의 보간 계수 배율. 위 마디보다 더 늦게 따라와야 관절 연쇄가
        /// 채찍처럼 이어지는 느낌이 난다.</summary>
        private const float LowerSegmentSmoothingRatio = 0.75f;

        /// <summary>한 마디(대퇴/정강이/상완/전완)의 절차적 제어에 필요한 것 전부. 매 프레임 재탐색 금지.</summary>
        private sealed class Segment
        {
            public Transform Transform;
            public Vector2 PivotLocal;     // 부모 로컬 공간에서의 관절 위치(HingeJoint2D.connectedAnchor).
            public Vector2 AnchorLocal;    // 이 마디 로컬 공간에서의 관절 위치(HingeJoint2D.anchor, 현재 항상 0).
            public bool FollowsBodyOffset; // 루트 직속(위 마디)만 true — 몸 바운스 오프셋을 여기에 더한다.
            public float CurrentAngle;     // 지수 감쇠 보간의 상태값 = 지금 실제로 적용돼 있는 각도(도).
            public float GetupStartAngle;  // GETUP 보간 시작각(널브러진 실제 각도) 캡처값.
        }

        /// <summary>팔 또는 다리 하나 = 위 마디 + 아래 마디.</summary>
        private sealed class Limb
        {
            public Segment Upper;
            public Segment Lower;
            public float NeutralSign;  // 바깥쪽 방향 부호(왼쪽 -1 / 오른쪽 +1).
            public float PhaseOffset;  // 보행 사이클 위상(라디안). 왼다리 0, 오른다리 π, 팔은 같은 쪽 다리의 반대.
            public bool IsLeg;
        }

        private readonly Limb _leftLeg;
        private readonly Limb _rightLeg;
        private readonly Limb _leftArm;
        private readonly Limb _rightArm;
        private readonly Limb[] _limbs;
        private readonly Segment[] _segments;

        // Walk 사인파의 누적 위상(라디안). 시간×주파수가 아니라 **위상을 적분**한다: 걷는 속도가 바뀌면
        // 주파수가 바뀌는데, 시간에 주파수를 곱하는 방식은 그 순간 위상이 통째로 점프해 다리가 툭 튄다.
        private float _phase;

        // 보행 주파수 산출에 쓰는 수평 속도의 스무딩 값 — 속도가 급변해도 주파수가 튀지 않게 한다.
        private float _smoothedSpeed;

        // Idle 미세 호흡 모션의 자체 타이머(초). Walk 위상과 독립이라 상태를 오가도 이어진다.
        private float _idleTime;

        // 시각 전용 상하 오프셋(월드 유닛). 걷기 바운스/Idle 호흡이 여기에 쓴다. **Rigidbody2D.position은
        // 절대 건드리지 않는다** — 접지 판정(GroundSensor/SnapToGround)이 루트의 물리 위치를 발 높이로
        // 쓰기 때문에 그걸 흔들면 접지 로직이 깨진다(리더 명시 지시). 순수하게 보이는 것만 흔든다.
        private float _bodyOffsetY;

        // 바라보는 방향(+1 = 오른쪽, -1 = 왼쪽). 2026-08-28 사용자 "이상하게 뒤로 걸어" 대응:
        // 캐릭터가 왼쪽으로 이동하는데 다리 스윙/무릎 접힘 방향은 오른쪽을 향한 채 고정돼 있어
        // 문워크처럼 보였다. 이 부호를 최종 각도에 곱해 포즈 전체를 좌우 대칭으로 뒤집는다.
        //
        // 왜 Transform.localScale.x = -1(스프라이트 flip)이 아니라 각도 부호인가: 이 캐릭터의 모든
        // 시각 요소가 x=0 축 위에 있고(부착점/몸통/머리 전부 x=0) 좌우 차이를 만드는 것은 오직 각도뿐이라,
        // 각도를 뒤집으면 스케일 뒤집기와 **시각적으로 완전히 동일한 결과**가 나온다. 그러면서 음수
        // 스케일이 Rigidbody2D/Collider2D/HingeJoint2D 계산에 끼치는 영향(2D 물리에서 음수 스케일은
        // 콜라이더 뒤집힘/조인트 앵커 오차의 흔한 원인)을 원천적으로 피할 수 있다 — 리더가 경고한
        // "물리 루트의 스케일을 뒤집으면 콜라이더/조인트 계산이 꼬인다"를 시각 전용 부모를 새로 만드는
        // 대신 이 방식으로 해결했다. 눈동자 오프셋도 States/EyeController.SetFacing()이 같은 부호로
        // 함께 뒤집는다.
        private float _facingSign = 1f;

        // 몸통/머리 시각 오브젝트와 그 중립 로컬 위치(상하 오프셋의 기준점).
        private readonly Transform _torso;
        private readonly Transform _head;
        private readonly Vector3 _torsoNeutral;
        private readonly Vector3 _headNeutral;

        public bool HasLimbs => _limbs.Length > 0;

        public StickmanPoseAnimator(Transform root)
        {
            if (root == null)
            {
                _limbs = System.Array.Empty<Limb>();
                _segments = System.Array.Empty<Segment>();
                return;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            _leftLeg = BuildLimb(all, "LeftLeg", sign: -1f, phase: 0f, isLeg: true);
            _rightLeg = BuildLimb(all, "RightLeg", sign: 1f, phase: Mathf.PI, isLeg: true);
            // 팔은 같은 쪽 다리와 반대 위상(실제 보행에서 왼팔은 왼다리와 반대로 나간다).
            _leftArm = BuildLimb(all, "LeftArm", sign: -1f, phase: Mathf.PI, isLeg: false);
            _rightArm = BuildLimb(all, "RightArm", sign: 1f, phase: 0f, isLeg: false);

            var limbs = new System.Collections.Generic.List<Limb>(4);
            var segments = new System.Collections.Generic.List<Segment>(8);
            AddLimb(limbs, segments, _leftLeg);
            AddLimb(limbs, segments, _rightLeg);
            AddLimb(limbs, segments, _leftArm);
            AddLimb(limbs, segments, _rightArm);
            _limbs = limbs.ToArray();
            _segments = segments.ToArray();

            // 몸통/머리는 시각 전용 오브젝트(Rigidbody2D 없음)라 관절로 찾을 수 없다 — 이름으로 찾는다.
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].name == "Torso") _torso = all[i];
                else if (all[i].name == "Head") _head = all[i];
            }
            if (_torso != null) _torsoNeutral = _torso.localPosition;
            if (_head != null) _headNeutral = _head.localPosition;
        }

        private static void AddLimb(System.Collections.Generic.List<Limb> limbs,
            System.Collections.Generic.List<Segment> segments, Limb limb)
        {
            if (limb == null) return;
            limbs.Add(limb);
            segments.Add(limb.Upper);
            if (limb.Lower != null) segments.Add(limb.Lower);
        }

        /// <summary>
        /// 이름으로 위 마디를 찾고(프리팹 계층의 "LeftLeg"/"RightLeg"/"LeftArm"/"RightArm"), 그 자식에서
        /// 같은 이름 + "Lower"인 아래 마디를 찾는다. 배열 순회 순서에는 좌우/상하 의미가 없으므로 이름이
        /// 유일하게 신뢰할 수 있는 식별자다(RagdollRig도 같은 이유로 순회 순서에 의존하지 않는다).
        /// </summary>
        private static Limb BuildLimb(Transform[] all, string upperName, float sign, float phase, bool isLeg)
        {
            Transform upperTransform = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == upperName) { upperTransform = all[i]; break; }
            }
            if (upperTransform == null) return null;

            Transform lowerTransform = null;
            string lowerName = upperName + "Lower";
            for (int i = 0; i < upperTransform.childCount; i++)
            {
                Transform child = upperTransform.GetChild(i);
                if (child != null && child.name == lowerName) { lowerTransform = child; break; }
            }

            return new Limb
            {
                Upper = BuildSegment(upperTransform, followsBodyOffset: true),
                Lower = lowerTransform != null ? BuildSegment(lowerTransform, followsBodyOffset: false) : null,
                NeutralSign = sign,
                PhaseOffset = phase,
                IsLeg = isLeg,
            };
        }

        private static Segment BuildSegment(Transform t, bool followsBodyOffset)
        {
            var joint = t.GetComponent<HingeJoint2D>();
            return new Segment
            {
                Transform = t,
                // 관절이 있으면 그 배선값을 그대로 쓴다(프리팹 배치가 바뀌어도 자동 추종). 없으면 현재
                // 로컬 위치를 피벗으로 삼아 최소한 위치가 흐트러지지 않게 한다.
                PivotLocal = joint != null ? joint.connectedAnchor : (Vector2)t.localPosition,
                AnchorLocal = joint != null ? joint.anchor : Vector2.zero,
                FollowsBodyOffset = followsBodyOffset,
            };
        }

        /// <summary>
        /// 바라보는 방향 설정(+1 오른쪽 / -1 왼쪽). 이동 방향이 뚜렷할 때만 갱신하도록 호출부
        /// (StickmanBlackboard.TickPose)가 불감대를 적용한다 — 매 프레임 0 근처에서 부호가 떨리면
        /// 캐릭터가 좌우로 깜빡인다.
        /// </summary>
        public void SetFacing(float sign)
        {
            _facingSign = sign >= 0f ? 1f : -1f;
        }

        /// <summary>실측 검증/디버그용 — 현재 바라보는 방향 부호.</summary>
        public float FacingSign => _facingSign;

        /// <summary>Walk 진입 시 위상/속도 리셋 — 매번 같은 자세(중립)에서 걷기 사이클을 시작한다.</summary>
        public void ResetWalkPhase()
        {
            _phase = 0f;
            _smoothedSpeed = 0f; // 0에서 시작해 실제 속도로 차오르며 보폭 주파수가 자연스럽게 올라간다.
        }

        /// <summary>
        /// 스무딩 상태값을 현재 Transform의 실제 각도로 동기화한다. RAGDOLL(물리가 마디를 마음대로 굴린
        /// 구간) -> 능동 모드로 전환된 직후에 호출해야, 이후 보간이 "물리가 만들어 놓은 실제 자세"에서
        /// 시작해 부드럽게 이어진다(동기화하지 않으면 랙돌 이전의 낡은 각도에서 갑자기 튄다).
        /// </summary>
        public void SyncFromTransform()
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].CurrentAngle = NormalizeAngle(_segments[i].Transform.localEulerAngles.z);
            }
        }

        /// <summary>
        /// Idle(및 Walk/Getup을 제외한 모든 능동 상태)의 기본 포즈. 무릎/팔꿈치를 완전히 펴지 않고 살짝
        /// 굽혀두는 것이 핵심이다 — 완전히 곧은 상태가 "막대기" 느낌의 원인이다(사용자 지적).
        /// 목표각은 항상 같지만 즉시 대입하지 않고 지수 감쇠로 접근하므로, Walk에서 빠져나온 직후에도
        /// 다리가 중립으로 툭 되돌아가지 않고 서서히 모인다.
        /// </summary>
        public void ApplyIdlePose(float deltaTime, in PoseSettings settings, float smoothingRate)
        {
            // 완전 정지는 "얼어붙은 것"처럼 보인다 — 아주 느린 호흡 같은 미세 움직임을 항상 넣는다.
            _idleTime += deltaTime;
            float breath = Mathf.Sin(_idleTime * settings.BreathFrequencyHz * Mathf.PI * 2f);
            SetBodyOffset(breath * settings.BreathAmplitude);

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float upper = NeutralUpperAngle(limb, settings);
                if (!limb.IsLeg) upper += limb.NeutralSign * breath * settings.BreathArmDegrees;
                ApplyLimb(limb, upper, NeutralLowerAngle(limb, settings), deltaTime, smoothingRate);
            }
        }

        /// <summary>보간 없이 즉시 중립 포즈로 스냅(첫 프레임 초기화 전용 — StickmanAgent.Awake()).</summary>
        public void ApplyIdlePoseImmediate(in PoseSettings settings)
        {
            SetBodyOffset(0f);
            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                SetSegmentImmediate(limb.Upper, NeutralUpperAngle(limb, settings));
                SetSegmentImmediate(limb.Lower, NeutralLowerAngle(limb, settings));
            }
        }

        /// <summary>
        /// Walk 포즈 — 엉덩이/어깨는 위상차 180도의 사인파로 앞뒤로 흔들고, 무릎/팔꿈치는 항상 한 방향
        /// (사람 관절과 같게)으로만 접힌다. 무릎은 다리를 앞으로 스윙해 발을 들어올릴 때 접히고 딛고
        /// 있을 때 펴진다. 팔꿈치는 상시 기본 굽힘 위에 약간의 변동만 얹는다.
        ///
        /// 부드러움 장치: (1) 위상을 적분해 속도 변화에 위상이 점프하지 않게 하고, (2) 주파수 입력이 되는
        /// 수평 속도 자체를 스무딩하며, (3) 최종 각도를 지수 감쇠로 추종하고, (4) 팔은 다리보다, 아래
        /// 마디는 위 마디보다 각각 더 느슨한 계수를 써 관절 연쇄에 자연스러운 시차를 만든다. 전부
        /// 프레임레이트 독립이다.
        /// </summary>
        public void TickWalkPose(float deltaTime, float horizontalSpeedAbs, in PoseSettings settings,
            float frequencyPerSpeed, float legSwingDegrees, float armSwingRatio,
            float smoothingRate, float speedSmoothingRate, float bounceAmplitude)
        {
            _smoothedSpeed = Damp(_smoothedSpeed, horizontalSpeedAbs, speedSmoothingRate, deltaTime);
            _phase += 2f * Mathf.PI * frequencyPerSpeed * _smoothedSpeed * deltaTime;
            if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f; // 부동소수 정밀도 유지(각도 결과는 동일).

            // 상하 바운스 — 다리가 가장 벌어진 순간(|sin|=1)에 몸이 가장 낮고 다리가 모인 순간(sin=0)에
            // 가장 높으므로, |sin|은 보행 사인파의 2배 주파수가 된다. 평균 0 근처가 되도록 0.5를 뺀다.
            SetBodyOffset((0.5f - Mathf.Abs(Mathf.Sin(_phase))) * bounceAmplitude);

            float armBase = settings.ArmSpreadDegrees * ArmWalkBaseRatio;

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float t = _phase + limb.PhaseOffset;
                float swing = Mathf.Sin(t);

                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    upper = swing * legSwingDegrees;
                    // Max(0, ...)라 굽힘량이 절대 음수가 되지 않고 거기에 고정 부호를 곱하므로, 무릎이
                    // 반대로(뒤로) 꺾이는 경우의 수가 구조적으로 존재하지 않는다.
                    lower = KneeBendSign * (settings.IdleKneeBendDegrees
                        + settings.WalkKneeBendDegrees * Mathf.Max(0f, Mathf.Sin(t + KneeBendPhaseOffset)));
                }
                else
                {
                    upper = limb.NeutralSign * armBase + swing * legSwingDegrees * armSwingRatio;
                    lower = ElbowBendSign * (settings.IdleElbowBendDegrees
                        + settings.WalkElbowBendDegrees * Mathf.Max(0f, Mathf.Sin(t + ElbowBendPhaseOffset)));
                }

                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>
        /// GETUP 진입 — 지금 널브러진 각 마디의 실제 localRotation.z를 보간 시작점으로 캡처한다.
        /// 호출 시점에는 이미 RagdollRig.EnterActiveMode()가 마디를 Kinematic으로 되돌린 뒤여야 한다.
        /// </summary>
        public void CaptureGetupStartPose()
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].GetupStartAngle = NormalizeAngle(_segments[i].Transform.localEulerAngles.z);
                _segments[i].CurrentAngle = _segments[i].GetupStartAngle;
            }
        }

        /// <summary>
        /// GETUP 진행 — progress(0~1)에 따라 "널브러진 실제 각도"에서 "Idle 중립 각도"로 직접 보간한다.
        /// progress 자체가 이미 시간에 대한 결정론적 보간이므로 여기서 다시 지수 감쇠를 걸지 않는다
        /// (걸면 progress=1에서도 목표에 도달하지 못해 반쯤 일어난 자세로 남는다).
        /// </summary>
        public void TickGetupPose(float progress, in PoseSettings settings)
        {
            SetBodyOffset(0f); // 기상 중에는 바운스/호흡을 걸지 않는다(연출이 겹쳐 보이지 않도록).
            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                LerpSegmentToTarget(limb.Upper, NeutralUpperAngle(limb, settings), progress);
                LerpSegmentToTarget(limb.Lower, NeutralLowerAngle(limb, settings), progress);
            }
        }

        /// <summary>실측 검증/디버그용 — 위 마디(엉덩이/어깨) 각도.</summary>
        public void GetUpperAngles(out float leftLeg, out float rightLeg, out float leftArm, out float rightArm)
        {
            leftLeg = AngleOf(_leftLeg != null ? _leftLeg.Upper : null);
            rightLeg = AngleOf(_rightLeg != null ? _rightLeg.Upper : null);
            leftArm = AngleOf(_leftArm != null ? _leftArm.Upper : null);
            rightArm = AngleOf(_rightArm != null ? _rightArm.Upper : null);
        }

        /// <summary>실측 검증/디버그용 — 아래 마디(무릎/팔꿈치)의 부모 기준 상대 각도. 무릎은 항상
        /// 음수, 팔꿈치는 항상 양수여야 한다(사람 관절처럼 한 방향으로만 접힘).</summary>
        public void GetJointAngles(out float leftKnee, out float rightKnee, out float leftElbow, out float rightElbow)
        {
            leftKnee = AngleOf(_leftLeg != null ? _leftLeg.Lower : null);
            rightKnee = AngleOf(_rightLeg != null ? _rightLeg.Lower : null);
            leftElbow = AngleOf(_leftArm != null ? _leftArm.Lower : null);
            rightElbow = AngleOf(_rightArm != null ? _rightArm.Lower : null);
        }

        private static float AngleOf(Segment segment)
        {
            return segment != null && segment.Transform != null
                ? NormalizeAngle(segment.Transform.localEulerAngles.z)
                : 0f;
        }

        private static float NeutralUpperAngle(Limb limb, in PoseSettings settings)
        {
            return limb.NeutralSign * (limb.IsLeg ? settings.LegSpreadDegrees : settings.ArmSpreadDegrees);
        }

        private static float NeutralLowerAngle(Limb limb, in PoseSettings settings)
        {
            return limb.IsLeg
                ? KneeBendSign * settings.IdleKneeBendDegrees
                : ElbowBendSign * settings.IdleElbowBendDegrees;
        }

        private void ApplyLimb(Limb limb, float upperAngle, float lowerAngle, float deltaTime, float baseRate)
        {
            float rate = limb.IsLeg ? baseRate : baseRate * ArmSmoothingRatio;
            SmoothTo(limb.Upper, upperAngle, deltaTime, rate);
            SmoothTo(limb.Lower, lowerAngle, deltaTime, rate * LowerSegmentSmoothingRatio);
        }

        /// <summary>
        /// 목표각으로 지수 감쇠 접근. 계수 t = 1 - exp(-rate*dt)는 **프레임레이트 독립적**이다 —
        /// 단순한 Lerp(a, b, rate*dt)는 같은 rate라도 fps에 따라 결과가 달라져(30fps와 120fps에서 다른
        /// 속도로 수렴) 리더가 명시적으로 금지한 형태다. rate가 0 이하면 즉시 대입으로 폴백한다.
        /// </summary>
        private void SmoothTo(Segment segment, float targetAngle, float deltaTime, float rate)
        {
            if (segment == null) return;
            segment.CurrentAngle = rate > 0f
                ? Mathf.LerpAngle(segment.CurrentAngle, targetAngle, 1f - Mathf.Exp(-rate * deltaTime))
                : targetAngle;
            ApplyAngle(segment, segment.CurrentAngle);
        }

        private void SetSegmentImmediate(Segment segment, float angle)
        {
            if (segment == null) return;
            segment.CurrentAngle = angle;
            ApplyAngle(segment, angle);
        }

        private void LerpSegmentToTarget(Segment segment, float targetAngle, float progress)
        {
            if (segment == null) return;
            segment.CurrentAngle = Mathf.LerpAngle(segment.GetupStartAngle, targetAngle, progress);
            ApplyAngle(segment, segment.CurrentAngle);
        }

        /// <summary>
        /// 실제 적용 지점 — 회전과 함께 위치도 보정해 관절 지점이 회전 중심이 되게 한다(클래스 문서
        /// "회전 중심(pivot)" 참고). 루트 직속인 위 마디에는 시각 전용 몸 오프셋(_bodyOffsetY)을 더한다
        /// (아래 마디는 위 마디의 자식이라 자동으로 따라온다). Kinematic Rigidbody2D의 Transform에
        /// 직접 쓰므로 Unity가 다음 물리 스텝 시작 시 바디 위치를 여기에 맞춰 동기화한다.
        /// </summary>
        private void ApplyAngle(Segment segment, float angleDegrees)
        {
            if (segment == null || segment.Transform == null) return;
            // 바라보는 방향은 여기(최종 적용 지점)에서만 곱한다 — CurrentAngle은 "방향 중립" 공간에
            // 유지되므로, 좌우가 뒤집히는 순간에도 지수 감쇠 보간 상태가 깨지지 않는다.
            Quaternion rotation = Quaternion.Euler(0f, 0f, angleDegrees * _facingSign);
            segment.Transform.localRotation = rotation;

            Vector2 offset = rotation * segment.AnchorLocal;
            float bodyOffset = segment.FollowsBodyOffset ? _bodyOffsetY : 0f;
            Vector3 current = segment.Transform.localPosition;
            // 피벗 X도 함께 미러링한다(현재 기하학에서는 모든 부착점이 x=0이라 결과가 같지만, 나중에
            // 좌우 비대칭 배치가 생겨도 시각이 조용히 깨지지 않게).
            segment.Transform.localPosition = new Vector3((segment.PivotLocal.x - offset.x) * _facingSign,
                segment.PivotLocal.y - offset.y + bodyOffset, current.z);
        }

        /// <summary>
        /// 시각 전용 상하 오프셋 적용 — 몸통/머리 Transform의 로컬 Y를 중립에서 이만큼 밀고, 팔다리
        /// 위 마디의 부착점에도 같은 값을 더한다(ApplyAngle 참고). 그래야 몸이 통째로 오르내리고
        /// 팔다리가 몸에서 떨어지지 않는다. Rigidbody2D.position은 건드리지 않으므로 접지 판정에는
        /// 아무 영향이 없다.
        /// </summary>
        private void SetBodyOffset(float offsetY)
        {
            _bodyOffsetY = offsetY;
            if (_torso != null) _torso.localPosition = new Vector3(_torsoNeutral.x, _torsoNeutral.y + offsetY, _torsoNeutral.z);
            if (_head != null) _head.localPosition = new Vector3(_headNeutral.x, _headNeutral.y + offsetY, _headNeutral.z);
        }

        /// <summary>스칼라용 지수 감쇠(위 SmoothTo와 같은 공식) — 보행 주파수 입력 속도 스무딩에 사용.</summary>
        private static float Damp(float current, float target, float rate, float deltaTime)
        {
            if (rate <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * deltaTime));
        }

        private static float NormalizeAngle(float degrees)
        {
            return Mathf.Repeat(degrees + 180f, 360f) - 180f;
        }

        /// <summary>
        /// 포즈 계산에 필요한 각도 설정 묶음. 인자 개수가 늘어 호출부가 읽기 어려워지는 것을 막기 위한
        /// 단순 값 묶음이며, StickmanBlackboard가 StickConfig에서 구성해 넘긴다(readonly struct + in
        /// 파라미터라 매 프레임 호출 경로에서도 힙 할당/복사 비용이 없다).
        /// </summary>
        public readonly struct PoseSettings
        {
            public readonly float LegSpreadDegrees;
            public readonly float ArmSpreadDegrees;
            public readonly float IdleKneeBendDegrees;
            public readonly float IdleElbowBendDegrees;
            public readonly float WalkKneeBendDegrees;
            public readonly float WalkElbowBendDegrees;
            public readonly float BreathAmplitude;
            public readonly float BreathFrequencyHz;
            public readonly float BreathArmDegrees;

            public PoseSettings(float legSpread, float armSpread, float idleKnee, float idleElbow,
                float walkKnee, float walkElbow, float breathAmplitude, float breathFrequencyHz, float breathArmDegrees)
            {
                LegSpreadDegrees = legSpread;
                ArmSpreadDegrees = armSpread;
                IdleKneeBendDegrees = idleKnee;
                IdleElbowBendDegrees = idleElbow;
                WalkKneeBendDegrees = walkKnee;
                WalkElbowBendDegrees = walkElbow;
                BreathAmplitude = breathAmplitude;
                BreathFrequencyHz = breathFrequencyHz;
                BreathArmDegrees = breathArmDegrees;
            }
        }
    }
}
