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

        // ─────────────────────────────────────────────────────────────────────────────────────
        // 명시적 키프레임 보행 사이클 (2026-08-28 리더 결정 — 사인파 방식 폐기)
        // ─────────────────────────────────────────────────────────────────────────────────────
        // 왜 사인파를 버렸는가: 여러 라운드 동안 사용자가 계속 "움직임이 어색하다"고 지적했고, 원인은
        // 튜닝이 아니라 곡선의 형태 자체였다. **실제 사람의 걷기는 사인파가 아니다.**
        //   (1) 걷기는 "디딤(stance)"과 "흔듦(swing)" 두 국면이 비대칭인데 사인파는 완벽히 대칭이다.
        //   (2) 무릎은 스윙 중에 크게(45~50도) 접혔다가 착지 직전에 펴지는데, 사인파로는 이 타이밍이
        //       절대 나오지 않는다 — 사인파는 최대 굽힘이 항상 사이클의 고정된 대칭 지점에 온다.
        //   (3) 사용자가 제시한 레퍼런스(Alan Becker 계열)는 애니메이터가 포즈를 하나하나 잡은
        //       키프레임 애니메이션이다.
        // 그래서 8개 키포즈를 표로 정의하고 그 사이를 보간하는 고전적 방식으로 교체했다. 아래 각도는
        // 리더가 지정한 값 그대로다.
        //
        // 부호 규약(리더 지정): 엉덩이(대퇴)는 +가 **앞쪽(진행 방향)**, 무릎은 +가 **뒤로 접힘**
        // (사람 무릎이 접히는 방향, 절대 음수가 되면 안 됨). 이 표의 값을 실제 Z 회전으로 옮길 때
        // 무릎에는 KneeBendSign(-1)을, 팔꿈치에는 ElbowBendSign(+1)을 곱한다.
        //
        // 이 표를 StickConfig가 아니라 여기 상수로 두는 이유: 이건 튜닝 스칼라가 아니라 "애니메이션
        // 에셋"에 가까운 16개 값의 묶음이고, 서로 정합성(디딤/흔듦 국면 구분)을 가져야 의미가 있다.
        // 개별 값을 따로 만지면 보행이 깨지므로 표 전체를 하나의 단위로 다룬다.

        /// <summary>왼다리 기준 8키 엉덩이 각도(도, + = 앞). 오른다리는 위상 0.5 오프셋.</summary>
        private static readonly float[] LegHipKeys = { 25f, 12f, 0f, -15f, -25f, -12f, 0f, 15f };

        /// <summary>왼다리 기준 8키 무릎 굽힘(도, 항상 0 이상 = 뒤로 접힘). 스윙 국면(t=0.625~0.75)에서
        /// 45~50도로 크게 접히고 디딤 국면(t=0~0.375)에서는 5~20도로 거의 펴진다 — 이 **비대칭**이
        /// 자연스러움의 핵심이다.</summary>
        private static readonly float[] LegKneeKeys = { 5f, 20f, 5f, 5f, 10f, 45f, 50f, 25f };

        /// <summary>4키 어깨 각도(도, + = 앞). 팔은 같은 쪽 다리와 반대 위상(t+0.5)으로 샘플링한다.</summary>
        private static readonly float[] ArmShoulderKeys = { 18f, 0f, -18f, 0f };

        /// <summary>4키 팔꿈치 굽힘(도). 항상 15~25도로 굽어 있어 절대 완전히 펴지지 않는다.</summary>
        private static readonly float[] ArmElbowKeys = { 15f, 20f, 25f, 20f };

        // 몸통 상하 바운스는 더 이상 손으로 적은 8키 표(BounceKeys)가 아니다 — 2026-08-28 실측으로
        // 그 표의 **위상이 기하학과 반대**임이 드러났기 때문이다(아래 ComputeFootGroundingOffset 문서에
        // 측정치와 유도가 있다). 이제는 지금 실제로 적용돼 있는 다리 각도에서 "낮은 쪽 발이 지면에
        // 정확히 닿으려면 몸이 얼마나 오르내려야 하는가"를 매 프레임 계산해 쓴다.

        /// <summary>팔은 같은 쪽 다리와 반대 위상 — 다리 위상에 이만큼 더해서 팔 키를 샘플링한다.</summary>
        private const float ArmPhaseOffset = 0.5f;

        /// <summary>실제 이동 속도를 평균 내는 측정 창(초). 물리 고정 스텝(50Hz) 5회분이라 렌더 프레임
        /// 단위의 톱니가 확실히 상쇄되면서도, 속도 변화에 대한 반응이 눈에 띄게 늦어지지 않는다.</summary>
        private const float SpeedWindowSeconds = 0.1f;

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
            public float Length;           // 이 마디의 길이(월드 유닛) — 보폭 계산/발끝 좌표 산출용.
        }

        /// <summary>팔 또는 다리 하나 = 위 마디 + 아래 마디.</summary>
        private sealed class Limb
        {
            public Segment Upper;
            public Segment Lower;
            public float NeutralSign;  // 바깥쪽 방향 부호(왼쪽 -1 / 오른쪽 +1).
            public float PhaseOffset;  // 보행 사이클 위상(0~1). 왼다리 0, 오른다리 0.5. 팔은 ArmPhaseOffset을 더 더한다.
            public bool IsLeg;
        }

        private readonly Limb _leftLeg;
        private readonly Limb _rightLeg;
        private readonly Limb _leftArm;
        private readonly Limb _rightArm;
        private readonly Limb[] _limbs;
        private readonly Segment[] _segments;

        // 보행 사이클의 누적 위상(0~1, 1이 한 사이클). 시간×주파수가 아니라 **위상을 적분**한다: 걷는
        // 속도가 바뀌면 주파수가 바뀌는데, 시간에 주파수를 곱하는 방식은 그 순간 위상이 통째로 점프해
        // 다리가 툭 튄다.
        private float _phase01;

        /// <summary>
        /// 한 보행 사이클당 몸이 실제로 전진해야 하는 거리(월드 유닛). 키프레임 표의 디딤 국면
        /// (t=0 Contact -> t=0.5 Toe-off)에서 발이 몸 기준으로 앞에서 뒤로 이동하는 거리(= 한 걸음)를
        /// 다리 길이와 각도로부터 계산하고, 한 사이클에 양다리가 한 걸음씩 딛으므로 2배 한다.
        ///
        /// **왜 이게 중요한가(발 미끄러짐)**: 예전에는 StickConfig의 임의 계수
        /// (walkCycleFrequencyPerSpeed)를 속도에 곱해 주파수를 냈는데, 그 값이 실제 보폭과 무관해서
        /// 디딤발이 바닥에서 미끄러졌다(문워크). 이제는 주파수를 **실제 이동 속도에서 역산**한다:
        ///     사이클 주파수(Hz) = 수평 이동 속도 / 이 거리
        /// 이러면 발이 지면에 붙어 있는 것처럼 보인다. 다리 길이나 키프레임 각도를 바꿔도 자동으로
        /// 다시 맞는다(하드코딩된 계수가 하나도 없다).
        ///
        /// 매 Walk 틱마다 다시 계산한다(sin 4번) — StickConfig.walkPoseAmplitudeScale로 포즈 진폭을
        /// 바꾸면 보폭도 함께 바뀌어야 발이 계속 붙어 있기 때문이다. 마지막 값은 실측 로그용으로 남긴다.
        /// </summary>
        private float _distancePerCycle;

        // 다리 마디 길이 캐시(<b>루트 로컬 유닛</b>) — 보폭 계산 입력. 프리팹 지오메트리에서 1회만 읽는다.
        // Transform 스케일은 여기 반영되지 않는다(BoxCollider2D.size는 스케일을 곱해 저장되지 않는다).
        // 월드 유닛이 필요한 곳은 ComputeDistancePerCycle처럼 RootScaleX/Y를 곱해서 쓴다.
        private readonly float _legUpperLength;
        private readonly float _legLowerLength;

        // 보행 주파수 산출에 쓰는 수평 속도의 스무딩 값 — 속도가 급변해도 주파수가 튀지 않게 한다.
        private float _smoothedSpeed;

        // Idle 미세 호흡 모션의 자체 타이머(초). Walk 위상과 독립이라 상태를 오가도 이어진다.
        private float _idleTime;

        // 매달리기(LedgeHang) 흔들림 위상의 자체 타이머(초) — 매달릴 때마다 0에서 시작한다.
        private float _hangTime;

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

        // 루트(물리 몸통) Transform — 보행 사이클 주파수 산출에 쓰는 **실제** 수평 이동량 측정용.
        // 이 클래스는 루트를 절대 움직이지 않는다(읽기 전용 사용).
        private readonly Transform _root;
        private float _prevRootX;
        private bool _hasPrevRootX;
        private float _speedWindowDistance; // 측정 창에 누적된 이동 거리(월드 유닛).
        private float _speedWindowTime;     // 측정 창에 누적된 시간(초).
        private float _measuredSpeed;       // 마지막으로 확정된 측정 속도(유닛/초).

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

            _root = root;

            _leftLeg = BuildLimb(root, "LeftLeg", sign: -1f, phase: 0f, isLeg: true);
            _rightLeg = BuildLimb(root, "RightLeg", sign: 1f, phase: 0.5f, isLeg: true);
            // 팔의 PhaseOffset은 "같은 쪽 다리"와 같게 두고, 다리와의 반대 위상은 샘플링 시점에
            // ArmPhaseOffset(0.5)을 더해 만든다 — 왼팔은 왼다리의 반대로 나간다.
            _leftArm = BuildLimb(root, "LeftArm", sign: -1f, phase: 0f, isLeg: false);
            _rightArm = BuildLimb(root, "RightArm", sign: 1f, phase: 0.5f, isLeg: false);

            var limbs = new System.Collections.Generic.List<Limb>(4);
            var segments = new System.Collections.Generic.List<Segment>(8);
            AddLimb(limbs, segments, _leftLeg);
            AddLimb(limbs, segments, _rightLeg);
            AddLimb(limbs, segments, _leftArm);
            AddLimb(limbs, segments, _rightArm);
            _limbs = limbs.ToArray();
            _segments = segments.ToArray();

            // 몸통/머리는 시각 전용 오브젝트(Rigidbody2D 없음)라 관절로 찾을 수 없다 — 이름으로 찾는다.
            //
            // ★★ 2026-08-30 회귀 수정 — **루트 직속 자식만** 본다(예전에는 GetComponentsInChildren로
            // 얻은 자손 전체를 훑고 마지막 일치를 채택했다).
            //
            // 실측으로 확인한 사고: 같은 캐릭터 루트에 붙는 UI 위젯이 Awake에서 자기 Canvas를
            // `SetParent(transform)` 으로 달고, 그 안에 "미니 스틱맨" 아이콘의 머리 원을 **"Head"라는
            // 이름의 자손**으로 만든다. 그러면 이 루프가 캐릭터의 머리 대신 그 UI 원을 잡아
            // <b>캐릭터의 머리와 몸통이 영원히 움직이지 않게 된다</b>(무릎앉아 착지에서 머리 하강이
            // 정확히 0.000유닛이 되어 발견됐다 — 팔다리는 관절로 찾으므로 멀쩡했고, 그래서 "포즈는
            // 되는데 머리만 안 내려가는" 진단하기 어려운 형태였다).
            //
            // 이름 전역 탐색은 "다른 레이어가 우연히 같은 이름을 쓰면 조용히 깨지는" 구조다.
            // 캐릭터의 Torso/Head는 프리팹 규약상 **항상 루트 직속**이므로(Editor/SceneBootstrapper의
            // CreateLineSegmentVisual/CreateHeadAnchor가 root.transform을 부모로 만든다) 탐색 범위를
            // 직속으로 좁히면 어떤 UI가 어떤 이름으로 자식을 만들든 구조적으로 영향을 받지 않는다.
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;
                if (_torso == null && child.name == "Torso") _torso = child;
                else if (_head == null && child.name == "Head") _head = child;
            }
            if (_torso != null) _torsoNeutral = _torso.localPosition;
            if (_head != null) _headNeutral = _head.localPosition;

            Limb refLeg = _leftLeg ?? _rightLeg;
            if (refLeg != null)
            {
                _legUpperLength = refLeg.Upper != null ? refLeg.Upper.Length : 0f;
                _legLowerLength = refLeg.Lower != null ? refLeg.Lower.Length : 0f;
            }
            _distancePerCycle = ComputeDistancePerCycle(1f);
        }

        /// <summary>
        /// 보폭(한 걸음) × 2 = 한 사이클 이동 거리(<b>월드 유닛</b>). <see cref="_distancePerCycle"/> 문서 참고.
        ///
        /// ★★ 2026-08-31 BUG-WALK-B2 (문워크) — 마지막에 <see cref="RootScaleX"/>를 곱하는 것이 핵심이다.
        /// 입력인 <see cref="_legUpperLength"/>/<see cref="_legLowerLength"/>는 BoxCollider2D.size에서
        /// 읽은 <b>로컬</b> 길이라 Transform 스케일이 빠져 있는데, 이 값을 나누는 쪽
        /// (<see cref="TickWalkPose"/>의 _smoothedSpeed)은 루트의 <b>월드</b> X 이동량 실측이다.
        /// 단위가 다른 둘을 나누면 루트 localScale이 1이 아닌 순간 사이클 주파수가 통째로 어긋난다:
        ///   · 배율 0.35(사용자 저장값, 루트 localScale 0.4667) -> 분모가 2.14배 과대 -> 주파수가
        ///     2.14배 느려 디딤발이 몸에 끌려간다 = 문워크(실측 미끄러짐 비율 0.54, 상한 0.30).
        ///   · 배율 2.00 -> 반대로 주파수가 2.67배 빨라 발이 앞뒤로 종종거린다.
        /// 프리팹이 0.75로 구워져 있어 기본 다이얼(0.75)에서만 localScale이 정확히 1이라 어긋남이 0이었고,
        /// 그래서 기본 배율 테스트를 통과한 채 살아 있었다(<see cref="HangHandReachAboveRoot"/>와 같은 계열).
        ///
        /// StickConfig.ResolveWalkSpeed()의 "속도를 배율에 비례시키면 주파수가 배율과 무관해진다"는 설계는
        /// <b>보폭도 배율에 비례할 때만</b> 성립한다 — 이 곱셈이 그 전제를 실제로 참으로 만든다.
        /// </summary>
        private float ComputeDistancePerCycle(float amplitudeScale)
        {
            if (_legUpperLength <= 0f || _legLowerLength <= 0f) return 0f;

            // 디딤 국면의 시작(Contact, 키 0)과 끝(Toe-off, 키 4)에서의 발끝 수평 위치 차이 = 한 걸음.
            // 진폭 배율을 곱한 **실제로 적용되는 각도**로 계산해야 보폭과 애니메이션이 어긋나지 않는다.
            float contact = FootHorizontalOffset(LegHipKeys[0] * amplitudeScale, LegKneeKeys[0] * amplitudeScale,
                _legUpperLength, _legLowerLength);
            float toeOff = FootHorizontalOffset(LegHipKeys[4] * amplitudeScale, LegKneeKeys[4] * amplitudeScale,
                _legUpperLength, _legLowerLength);
            return Mathf.Abs(contact - toeOff) * 2f * RootScaleX;
        }

        /// <summary>
        /// 엉덩이 각도와 무릎 굽힘이 주어졌을 때 발끝이 엉덩이로부터 얼마나 앞(+)/뒤(-)에 있는지.
        /// 정강이의 절대 각도는 (엉덩이각 - 무릎굽힘) — 무릎이 뒤로 접히면 그만큼 앞쪽 각도가 줄어든다.
        /// </summary>
        private static float FootHorizontalOffset(float hipDegrees, float kneeDegrees, float upper, float lower)
        {
            return upper * Mathf.Sin(hipDegrees * Mathf.Deg2Rad)
                 + lower * Mathf.Sin((hipDegrees - kneeDegrees) * Mathf.Deg2Rad);
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
        ///
        /// ★★ 2026-08-30 — Torso/Head와 같은 이유로 **루트 직속 자식만** 본다(Editor/SceneBootstrapper의
        /// CreateLimb(root.transform, ...)이 항상 루트 직속으로 만든다). Torso/Head 회귀(위 생성자 주석
        /// 참고 — 캐릭터 루트에 붙는 UI 위젯이 "Head"라는 이름의 UI 자손을 만들어 포즈를 얼렸던 사고)와
        /// 같은 계열의 잠재 위험이 여기도 있었다 — 팔다리 이름과 우연히 같은 이름을 쓰는 자손이 어딘가에
        /// 생기면 이 전역 탐색이 그걸 집어 조용히 깨진다. 직속으로 좁혀 구조적으로 차단한다.
        /// </summary>
        private static Limb BuildLimb(Transform root, string upperName, float sign, float phase, bool isLeg)
        {
            Transform upperTransform = null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == upperName) { upperTransform = child; break; }
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
            var box = t.GetComponent<BoxCollider2D>();
            return new Segment
            {
                Transform = t,
                // 관절이 있으면 그 배선값을 그대로 쓴다(프리팹 배치가 바뀌어도 자동 추종). 없으면 현재
                // 로컬 위치를 피벗으로 삼아 최소한 위치가 흐트러지지 않게 한다.
                PivotLocal = joint != null ? joint.connectedAnchor : (Vector2)t.localPosition,
                AnchorLocal = joint != null ? joint.anchor : Vector2.zero,
                FollowsBodyOffset = followsBodyOffset,
                // 마디 길이는 BoxCollider2D.size.y와 정확히 같게 만들어져 있다
                // (Editor/SceneBootstrapper.CreateLimbSegment) — 하드코딩 대신 그 값을 읽는다.
                Length = box != null ? box.size.y : 0f,
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
            _phase01 = 0f;
            _smoothedSpeed = -1f;  // 미초기화 표식 — 첫 틱에서 실제 속도로 즉시 채운다(TickWalkPose 참고).
            _hasPrevRootX = false; // 이전 위치가 없으면 첫 틱은 호출부가 넘긴 명령 속도를 쓴다.
            _speedWindowDistance = 0f;
            _speedWindowTime = 0f;
            _measuredSpeed = -1f;
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

        /// ============================================================================
        /// ★ 유휴 앰비언트 동작 (docs/UX_FLOW.md 26-3 "살아있는 느낌", 2026-08-30 배선)
        /// ============================================================================
        /// Idle 중립 포즈 **위에 얹는** 짧은 변주다. 새 상태가 아니라 이 메서드 하나이며, 호출부
        /// (StickmanBlackboard.TickPose)가 Idle 분기에서 ApplyIdlePose 대신 이것을 부를 뿐이다 —
        /// 그래서 이동 의도가 생기거나 발판을 잃으면 상태 전이가 알아서 연출을 끊는다(별도 취소 배관 없음).
        ///
        /// 트리거는 <see cref="StickmanEventBus.WanderAmbientMotionRequested"/>이고, 그 이벤트의 발행
        /// 조건은 States/AutoWanderController.cs가 이미 갖고 있던 것 그대로다(새 확률을 하나도 더하지
        /// 않았다 — 사용자가 "요청하지 않은 연출"에 반복적으로 민감했으므로 상위 신호의 빈도를 그대로
        /// 물려받는 것이 규칙이다).
        ///
        /// 왜 두 동작이 하필 이 모양인가(캐릭터가 화면상 약 60pt로 아주 작다는 실측 제약):
        ///   · LookAround(주위 살피기) — <b>한쪽 팔을 이마에 얹어 멀리 보는 손차양 자세</b> + 머리가
        ///     좌우로 한 번 왕복한다. 처음에는 "머리만 좌우로 움직이기"를 검토했으나, 머리 반경이
        ///     화면상 약 6pt라 머리만으로는 사실상 보이지 않는다. 팔 길이는 신장의 약 1/3이라
        ///     팔 실루엣이 바뀌는 것이 이 크기에서 유일하게 확실히 읽히는 신호다.
        ///     각도는 손 끝이 어깨 기준 (앞 0.20, 위 0.95)·팔길이에 오도록 역산한 값이다(어깨 107도 /
        ///     팔꿈치 122도) — 그 지점이 곧 이마 높이다.
        ///   · SitAndYawn(기지개) — 두 팔을 머리 위로 뻗고(매달리기와 같은 180∓spread 규약) 무릎을
        ///     펴며 몸이 살짝 솟는다. 하품/기지개의 가장 큰 시각 신호가 "만세"이므로 그것만 쓴다.
        ///
        /// 진행 곡선: env = smoothstep(sin(pi*p)). 양 끝에서 정확히 0이라 <b>시작과 끝이 항상 중립</b>이고
        /// (도중에 끊겨도 튀지 않는다), 가운데에 평평한 구간이 생겨 자세가 한 장의 그림으로 남는다
        /// (무릎앉아 착지의 hold 구간과 같은 관행).
        ///
        /// <param name="progress01">이번 동작의 진행도 0~1. 호출부가 시간을 센다(포즈 계산은 무상태).</param>
        public void ApplyIdleAmbientPose(float deltaTime, in PoseSettings settings, float smoothingRate,
            in IdleAmbientPoseSettings ambient, StickMate.Core.WanderAmbientMotion motion, float progress01)
        {
            _idleTime += deltaTime;
            float breath = Mathf.Sin(_idleTime * settings.BreathFrequencyHz * Mathf.PI * 2f);

            float raw = Mathf.Sin(Mathf.Clamp01(progress01) * Mathf.PI);
            float env = raw * raw * (3f - 2f * raw); // smoothstep — 양 끝 0, 가운데에 평평한 hold.

            bool stretching = motion == StickMate.Core.WanderAmbientMotion.SitAndYawn;

            float headShift = stretching
                ? 0f
                // 좌 -> 우 한 번 왕복. env를 곱해 양 끝이 정확히 중립이다.
                : Mathf.Sin(Mathf.Clamp01(progress01) * Mathf.PI * 2f) * env * ambient.LookHeadShiftDistance;
            float rise = stretching ? ambient.StretchRiseDistance * env : 0f;
            SetBodyOffset(breath * settings.BreathAmplitude + rise, headShift);

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float upper = NeutralUpperAngle(limb, settings);
                float lower = NeutralLowerAngle(limb, settings);
                if (!limb.IsLeg) upper += limb.NeutralSign * breath * settings.BreathArmDegrees;

                if (stretching)
                {
                    if (limb.IsLeg)
                    {
                        // 무릎을 펴며 몸이 솟는다(굽힘을 줄이는 방향이라 부호가 뒤집힐 일이 없다).
                        lower *= 1f - Mathf.Clamp01(ambient.StretchKneeStraighten01) * env;
                    }
                    else
                    {
                        upper = Mathf.LerpAngle(upper,
                            HangArmUpperAngle(limb.NeutralSign, ambient.StretchArmSpreadDegrees), env);
                        lower = Mathf.LerpAngle(lower,
                            ElbowBendSign * Mathf.Max(0f, ambient.StretchElbowDegrees), env);
                    }
                }
                else if (!limb.IsLeg && limb.NeutralSign > 0f)
                {
                    // 한쪽 팔만 올린다 — 두 팔을 다 올리면 기지개와 실루엣이 구분되지 않는다.
                    upper = Mathf.LerpAngle(upper, ambient.LookArmDegrees, env);
                    lower = Mathf.LerpAngle(lower, ElbowBendSign * Mathf.Max(0f, ambient.LookElbowDegrees), env);
                }

                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>
        /// ★ 매달리기 포즈(UX_FLOW.md 4절 "매달리기(HANG)": "팔이 완전히 펴진 채 대롱대롱, 미세한
        /// 흔들림") — 사용자 요청 "내려갈때도 매달려서 내려가는형태로"의 시각적 실체.
        ///
        /// 각도 규약은 이 클래스 전체와 같다: 마디 로컬 -y가 끝(손/발)이고 각도 0이 "곧게 아래".
        /// 따라서 **팔을 위로 뻗는다 = 어깨 각도 180도 근처**다(0이 아니라 180이라는 점이 Idle/Walk와
        /// 다른 유일한 핵심이며, 나머지는 전부 기존 경로를 그대로 탄다).
        ///   - 팔: 180 ∓ spread (좌우로 살짝 벌려 몸통 선과 겹치지 않게) + 팔꿈치 약간 굽힘.
        ///   - 다리: 거의 수직으로 모으고(spread 작게) 무릎만 조금 접어 축 늘어진 느낌.
        ///   - 흔들림: 사지 전체에 같은 위상의 사인파를 더해 몸이 통째로 좌우로 대롱거리게 한다
        ///     (마디별로 다른 위상을 주면 "흔들린다"가 아니라 "허우적댄다"로 보인다).
        /// 몸 오프셋은 0으로 되돌린다 — 매달린 몸의 상하 위치는 포즈가 아니라 LedgeHangState가 루트
        /// 위치로 직접 만든다(호흡/바운스 오프셋이 남아 있으면 손이 모서리에서 미세하게 떨어져 보인다).
        /// </summary>
        public void ApplyLedgeHangPose(float deltaTime, in LedgeHangPoseSettings settings, float smoothingRate)
        {
            SetBodyOffset(0f);

            _hangTime += deltaTime;
            float sway = Mathf.Sin(_hangTime * settings.SwayFrequencyHz * Mathf.PI * 2f) * settings.SwayAmplitudeDegrees;

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    // 다리는 아래로 늘어진다(각도 0 = 곧게 아래) + 흔들림이 그대로 실린다.
                    upper = limb.NeutralSign * settings.LegSpreadDegrees + sway;
                    lower = KneeBendSign * Mathf.Max(0f, settings.KneeBendDegrees);
                }
                else
                {
                    // 팔은 위로 뻗어 모서리를 잡는다(각도 180 = 곧게 위). 흔들림은 다리와 **반대 부호**로
                    // 넣는다 — 손이 모서리에 고정된 채 몸이 흔들리면, 몸에서 본 팔은 반대로 기운다.
                    upper = HangArmUpperAngle(limb.NeutralSign, settings.ArmSpreadDegrees) - sway;
                    lower = ElbowBendSign * Mathf.Max(0f, settings.ElbowBendDegrees);
                }
                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>매달릴 때 팔을 위로 뻗는 어깨 각도. 180이 정확히 수직 위이고, 부호에 따라 바깥으로
        /// 벌린다(왼팔 sign=-1 -> 180+spread, 오른팔 sign=+1 -> 180-spread).</summary>
        private static float HangArmUpperAngle(float neutralSign, float spreadDegrees)
        {
            return 180f - neutralSign * spreadDegrees;
        }

        /// <summary>매달리기 진입 시 흔들림 위상을 0에서 시작시킨다(LedgeHangState.Enter가 호출).</summary>
        public void ResetHangPhase() => _hangTime = 0f;

        /// <summary>
        /// ★ 매달린 자세에서 **손끝이 루트(=발 원점)보다 얼마나 위에 있는가**(월드 유닛).
        /// LedgeHangState가 "손이 모서리에 정확히 닿는" 루트 Y를 계산하는 데 쓴다:
        ///     매달린 루트 Y = 모서리 상단 Y − 이 값
        /// 하드코딩 상수가 아니라 **프리팹의 실제 어깨 부착 높이와 팔 마디 길이**에서 유도하므로,
        /// 어깨 위치(목 길이)나 팔 길이를 바꿔도 손이 모서리에서 떨어지거나 파묻히지 않는다.
        /// 계산은 ApplyAngle이 실제로 쓰는 것과 같은 회전식이다(각도 θ의 마디 끝 방향 = (sinθ, −cosθ)).
        /// 좌우 대칭이라 오른팔 하나만 계산하면 충분하다(팔이 없으면 0).
        ///
        /// ★★ 2026-08-31 BUG-LH-B1 (사용자 신고 "제대로 경계면에서 매달리는게 아니고 좀 밑에서 매달림")
        /// ── 반드시 **루트 스케일을 곱해** 월드 유닛으로 내보내야 한다.
        /// <see cref="Segment.PivotLocal"/>(= HingeJoint2D.connectedAnchor)와 <see cref="Segment.Length"/>
        /// (= BoxCollider2D.size.y)는 둘 다 **루트 로컬 유닛**이다. Transform 스케일은 이 두 값에
        /// 반영되지 않는다(콜라이더 size/조인트 anchor는 스케일을 곱해 저장되지 않는다). 반면 호출부인
        /// LedgeHangState는 이 값을 그대로 **월드 Y에서 빼서**(모서리 월드Y − 이 값) 루트를 배치한다.
        /// 그래서 루트 localScale이 1이 아닌 순간 그 차이가 통째로 어긋남이 된다:
        ///   · 실측(2026-08-31, 640x480 PlayMode) 배율 0.35(= 사용자 저장값, 루트 localScale 0.4667)
        ///     -> 손끝이 경계면보다 **1.0013유닛 아래**(캐릭터 키보다 더 아래에 매달렸다).
        ///   · 배율 2.00(루트 localScale 2.6667) -> 손끝이 경계면보다 3.1429유닛 **위**.
        /// 프리팹이 0.75로 구워져 있어 기본 다이얼(0.75)에서만 localScale = 1이라 어긋남이 0이었고,
        /// 그래서 이 버그가 기본값 테스트를 통과한 채 살아 있었다.
        ///
        /// 같은 함정을 이미 <see cref="StickMate.Core.StickmanMetrics"/>.Measure()가 rootScaleY를 곱해
        /// 막고 있다("아래 모든 로컬 치수에 곱해 '월드 유닛'이라는 이 클래스의 계약을 지킨다"). 여기도
        /// 정확히 같은 규약을 따른다 — 로컬 유닛을 월드 유닛이라 부르지 않는다.
        /// </summary>
        public float HangHandReachAboveRoot(in LedgeHangPoseSettings settings)
        {
            Limb arm = _rightArm ?? _leftArm;
            if (arm == null || arm.Upper == null) return 0f;

            float sign = arm.NeutralSign;
            float upperAngle = HangArmUpperAngle(sign, settings.ArmSpreadDegrees);
            float lowerAngle = upperAngle + ElbowBendSign * Mathf.Max(0f, settings.ElbowBendDegrees);

            float y = arm.Upper.PivotLocal.y;
            y += arm.Upper.Length * -Mathf.Cos(upperAngle * Mathf.Deg2Rad);
            if (arm.Lower != null) y += arm.Lower.Length * -Mathf.Cos(lowerAngle * Mathf.Deg2Rad);
            return y * RootScaleY;
        }

        /// <summary>루트의 월드 배율(X). 가로 길이(보폭)를 월드 유닛으로 바꿀 때 곱한다 — 세로를 다루는
        /// <see cref="RootScaleY"/>와 같은 규약이며, 배율은 균일하지만 축이 다른 값을 섞지 않으려고
        /// 가로는 가로 배율로 환산한다. 좌우 반전은 스케일이 아니라 각도 부호(_facingSign)로 하므로
        /// 이 값은 음수가 될 일이 없지만, 외부에서 뒤집어도 보폭이 음수가 되지 않도록 절댓값을 쓴다.</summary>
        private float RootScaleX
        {
            get
            {
                if (_root == null) return 1f;
                float s = Mathf.Abs(_root.lossyScale.x);
                return (s > 0.0001f && !float.IsNaN(s)) ? s : 1f;
            }
        }

        /// <summary>루트의 월드 배율(Y). 로컬 지오메트리(PivotLocal / Length)를 월드 길이로 바꿀 때
        /// 곱한다. 0/음수/NaN은 1로 막는다 — 배율이 잘못 들어와도 "매달림 높이 0"(= 발판 위에 서 있는
        /// 것처럼 보임) 같은 조용한 파손을 만들지 않기 위해서다.</summary>
        private float RootScaleY
        {
            get
            {
                if (_root == null) return 1f;
                float s = Mathf.Abs(_root.lossyScale.y);
                return (s > 0.0001f && !float.IsNaN(s)) ? s : 1f;
            }
        }

        /// <summary>
        /// ★ 낙하 중 공중 자세(2026-08-29, 사용자 요청 "떨어질때 관절이 이상하게 꺾이면서 넘어지는데").
        ///
        /// 왜 필요한가: StickmanBlackboard.TickPose()는 상태 ID로 포즈를 고르는데 Fall에 해당하는
        /// 분기가 없어 지금까지 낙하 중에도 <see cref="ApplyIdlePose"/>(직립 중립)가 적용되고 있었다 —
        /// 팔을 살짝 벌리고 다리를 곧게 편 채 막대기가 그대로 내려오는 그림이다.
        ///
        /// 자세의 형태(Alan Becker 계열 졸라맨 레퍼런스): **팔은 위/바깥으로, 다리는 살짝 접힘.**
        /// 사람이 떨어질 때 팔이 위로 뜨는 이유는 공기 저항이 아니라 몸통이 팔보다 먼저 가속되기
        /// 때문이라, 이것이 낙하를 알리는 가장 큰 신호다. 각도 규약은 이 클래스 전체와 같다:
        /// 마디 로컬 −y가 끝(손/발), 각도 0이 "곧게 아래", 끝 방향은 (sinθ, −cosθ)다. 따라서
        /// **팔을 위-바깥으로 뻗는다 = 어깨 각도 ±152도**(부호는 그 팔의 바깥 방향 NeutralSign)이며,
        /// 180이면 정확히 수직 위, 152면 수직에서 바깥으로 28도 벌어진 만세 자세다.
        ///
        /// <paramref name="intensity01"/>는 "지금 얼마나 빠르게 떨어지고 있는가"(0~1)다 — 호출부가
        /// 하강 속도를 신장으로 나눈 무차원 값에서 만든다(StickmanBlackboard.ComputeFallPoseIntensity).
        /// 이 값으로 Idle 중립 포즈와 낙하 자세를 섞으므로, 막 떨어지기 시작한 순간에는 자세가 거의
        /// 변하지 않다가 최고 속도에 가까워질수록 만세 자세가 완성된다(리더 지시 "낙하 속도/시간에 따라
        /// 자세가 점진적으로 변하면 더 좋다"). 한 계단 내려서는 정도의 짧은 낙하에서는 사실상 자세가
        /// 바뀌지 않는다는 부수 효과도 여기서 나온다.
        ///
        /// 몸 오프셋은 0으로 되돌린다 — 공중에는 "발이 닿는 지면"이 없으므로 보행/착지에서 쓰는
        /// 접지 보정(<see cref="ComputeFootGroundingOffset"/>)이 의미를 갖지 않는다.
        /// </summary>
        public void ApplyFallPose(float deltaTime, in PoseSettings idle, in FallPoseSettings fall,
            float smoothingRate, float intensity01)
        {
            SetBodyOffset(0f);

            float t = Mathf.Clamp01(intensity01);
            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    // 다리는 좌우로 벌리면서(NeutralSign) 공통으로 앞쪽으로 살짝 들어올린다(HipDegrees).
                    upper = limb.NeutralSign * fall.LegSpreadDegrees + fall.HipDegrees;
                    lower = KneeBendSign * Mathf.Max(0f, fall.KneeBendDegrees);
                }
                else
                {
                    // 만세 — 부호가 곧 그 팔의 바깥 방향이므로 좌우 대칭이 자동으로 보장된다.
                    upper = limb.NeutralSign * fall.ArmRaiseDegrees;
                    lower = ElbowBendSign * Mathf.Max(0f, fall.ElbowBendDegrees);
                }

                upper = Mathf.LerpAngle(NeutralUpperAngle(limb, idle), upper, t);
                lower = Mathf.LerpAngle(NeutralLowerAngle(limb, idle), lower, t);
                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>
        /// ★ 붙잡힌 채 발버둥치는 자세(2026-08-29, 사용자 요청 "마우스로 캐릭을 잡았을때 막 벗어날려는듯이
        /// 몸부림 치게끔 만들어줘"). States/DragThrowState.cs가 드래그 중 매 프레임 호출한다.
        ///
        /// 형태: 두 다리를 **서로 반대 위상**으로 차고(허우적), 팔은 다리와 **다른 주파수**로 휘젓는다.
        /// 주파수 비를 정수배가 아닌 값(<see cref="StruggleArmFrequencyRatio"/>)으로 둔 것이 핵심이다 —
        /// 팔다리가 같은 주기로 딱딱 맞으면 발버둥이 아니라 행진처럼 보인다. 무릎/팔꿈치는 사인파의
        /// **절반 위상**으로 접었다 폈다 하되 0 미만으로는 절대 가지 않는다(사람 관절 불변식).
        ///
        /// <paramref name="intensity01"/>는 지금 이 순간의 몸부림 세기(0=Idle 중립, 1=최대)이며,
        /// 호출부가 "세게 몸부림 → 잠깐 지침" 리듬과 시간에 따른 지침을 곱해 만든다
        /// (DragThrowState.EvaluateStruggleEnvelope). 이 함수는 그 값을 받아 Idle 중립 포즈와 섞기만
        /// 한다 — 즉 세기가 0이면 결과가 Idle과 정확히 같아, 스위치를 꺼도 자세가 튀지 않는다.
        ///
        /// <paramref name="phaseTime"/>은 몸부림 전용 누적 시간(초)이다. Idle 호흡/보행 위상과 독립인
        /// 이유는 잡을 때마다 같은 자세에서 시작하는 편이 예측 가능하기 때문이다(ResetWalkPhase와 같은 관례).
        ///
        /// 몸 오프셋은 0으로 되돌린다 — 잡혀 매달린 몸에는 "발이 닿는 지면"이 없다. 몸통의 비틀림은
        /// 여기가 아니라 상태가 루트의 시각 회전으로 만든다(팔다리 각도와 루트 회전은 서로 다른 층이다).
        /// ★ 루트 **위치**는 이 함수도 상태도 절대 흔들지 않는다 — 드래그 추종이 "커서에 딱 붙는다"는
        /// 이전 라운드 수정(dragFollowSmoothTime=0, 즉시 대입)을 무효로 만들기 때문이다.
        /// </summary>
        public void ApplyDragStrugglePose(float deltaTime, in PoseSettings idle, in DragStrugglePoseSettings struggle,
            float smoothingRate, float intensity01, float phaseTime)
        {
            SetBodyOffset(0f);

            float t = Mathf.Clamp01(intensity01);
            float legPhase = phaseTime * struggle.FrequencyHz * Mathf.PI * 2f;
            float armPhase = phaseTime * struggle.FrequencyHz * StruggleArmFrequencyRatio * Mathf.PI * 2f;

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float phase = (limb.IsLeg ? legPhase : armPhase) + limb.PhaseOffset * Mathf.PI * 2f;
                float swing = Mathf.Sin(phase);
                // 굽힘은 스윙보다 1/4주기 늦게(코사인) 최대가 되고, 0~1 범위라 절대 반대로 꺾이지 않는다.
                float bend01 = 0.5f - 0.5f * Mathf.Cos(phase * 2f);

                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    upper = limb.NeutralSign * idle.LegSpreadDegrees + swing * struggle.HipDegrees;
                    lower = KneeBendSign * (idle.IdleKneeBendDegrees + bend01 * struggle.KneeDegrees);
                }
                else
                {
                    upper = limb.NeutralSign * idle.ArmSpreadDegrees + swing * struggle.ArmDegrees;
                    lower = ElbowBendSign * (idle.IdleElbowBendDegrees + bend01 * struggle.ElbowDegrees);
                }

                upper = Mathf.LerpAngle(NeutralUpperAngle(limb, idle), upper, t);
                lower = Mathf.LerpAngle(NeutralLowerAngle(limb, idle), lower, t);
                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>발버둥에서 팔이 다리보다 몇 배 빠른가. **정수배가 아닌 값**이라는 것이 요점이다 —
        /// 정수배면 두 주기가 계속 같은 지점에서 만나 규칙적인 루프로 보인다. 튜닝 스칼라가 아니라
        /// 자연스러움을 만드는 구조라 StickConfig가 아니라 여기 상수로 둔다(보행 키프레임 표와 같은 기준).</summary>
        private const float StruggleArmFrequencyRatio = 1.37f;

        /// <summary>
        /// ★ 던져진 뒤 공중 회전(텀블링) 자세(2026-08-29, 사용자 요청 "던져도 공중에서 회전하면서
        /// 무릎앉아 착지할수있게 해줘"). States/ThrowTumbleState.cs가 매 프레임 호출한다.
        ///
        /// <see cref="ApplyFallPose"/>와 형태가 정반대인 것이 핵심이다: 낙하는 **펼친다**(팔을 위/바깥,
        /// 다리는 살짝만 접힘), 회전은 **웅크린다**(팔로 몸을 감싸고 다리를 크게 접어 올림). 사람이
        /// 공중제비를 돌 때 몸을 모으는 이유는 회전 관성을 줄이기 위해서이고, 시각적으로도 팔다리가
        /// 펴져 있으면 회전이 아니라 "기울어진 채 날아간다"로 읽힌다. 그래서 같은 함수에 플래그를
        /// 더하지 않고 별도 자세로 둔다(리더 지시 "ApplyFallPose를 참고하되 별도 자세로").
        ///
        /// 각도 규약은 이 클래스 전체와 같다(마디 로컬 −y가 끝, 각도 0이 곧게 아래, 하위 마디 각도는
        /// 부모 기준 로컬). 좌우는 <see cref="Limb.NeutralSign"/>으로 아주 조금만 벌려 깊이감을 준다 —
        /// 완전히 겹치면 팔다리가 두 개가 아니라 하나로 보인다.
        ///
        /// <paramref name="tuck01"/>은 "얼마나 웅크렸는가"(0=Idle 중립, 1=완전히 웅크림)이며 호출부가
        /// 회전 국면에서는 1, 착지 정렬 국면에서는 <c>StickConfig.throwTumbleLandingTuck01</c>로 낮춰
        /// 넘긴다 — 그래야 착지 직전에 몸이 펴지면서 무릎앉아로 자연스럽게 이어진다.
        ///
        /// 몸 오프셋은 0으로 되돌린다 — 공중에는 발이 닿는 지면이 없어 접지 보정이 의미를 갖지 않는다
        /// (<see cref="ApplyFallPose"/>와 같은 이유).
        ///
        /// ★ 이 함수는 **루트의 회전에 일절 관여하지 않는다.** 몸 전체의 회전은 상태가 루트의 시각
        /// 회전을 직접 구동한다(아키텍처 0절 — 회전을 물리에 맡기면 그것이 곧 "관절이 이상하게 꺾이는"
        /// 그림이다). 여기서는 어디까지나 사지의 로컬 각도만 만든다.
        /// </summary>
        public void ApplyThrowTumblePose(float deltaTime, in PoseSettings idle, in ThrowTumblePoseSettings tumble,
            float smoothingRate, float tuck01)
        {
            SetBodyOffset(0f);

            float t = Mathf.Clamp01(tuck01);
            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    upper = tumble.HipDegrees + limb.NeutralSign * tumble.LimbSpreadDegrees;
                    lower = KneeBendSign * Mathf.Max(0f, tumble.KneeBendDegrees);
                }
                else
                {
                    upper = tumble.ArmDegrees + limb.NeutralSign * tumble.LimbSpreadDegrees;
                    lower = ElbowBendSign * Mathf.Max(0f, tumble.ElbowBendDegrees);
                }

                upper = Mathf.LerpAngle(NeutralUpperAngle(limb, idle), upper, t);
                lower = Mathf.LerpAngle(NeutralLowerAngle(limb, idle), lower, t);
                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>
        /// ★★ 무릎앉아 착지 포즈(2026-08-29, 사용자 요청의 핵심 — "떨어질때 무릎앉아 형태로 멋지게
        /// 착지해야지"). States/LandingCrouchState.cs가 매 프레임 자기 진행 곡선의 값을
        /// <paramref name="amount"/>로 넘겨 호출한다.
        ///
        /// ============================================================================
        /// 앉는 "깊이"를 왜 거리 값으로 두지 않았는가 (배율 대응의 핵심)
        /// ============================================================================
        /// 몸이 얼마나 내려앉는지는 별도의 설정값이 아니라 **무릎/엉덩이 각도에서 유도**된다.
        /// <see cref="ComputeFootGroundingOffset"/>이 "지금 이 다리 각도에서 발이 지면에 정확히 닿으려면
        /// 몸이 얼마나 오르내려야 하는가"를 실제 마디 길이로 역산해 주기 때문이다. 덕분에
        ///   · 캐릭터 배율(StickConfig.characterScale)이 바뀌어도 발이 뜨거나 지면을 파고들지 않고,
        ///   · 각도만 만지면 깊이가 따라오므로 "각도와 깊이가 서로 어긋나는" 상태 자체가 존재할 수 없다.
        /// 각도는 크기와 무관한 양이므로 StickConfig에 절대값으로 두는 것이 맞다(리더 지시).
        ///
        /// ============================================================================
        /// 좌우 비대칭 = "멋지게"의 실질적 내용
        /// ============================================================================
        /// 두 다리를 같은 각도로 굽히면 그냥 쪼그려 앉은 그림이다. 바깥 방향 부호가 +인 쪽(오른쪽
        /// 마디)을 **앞**, −인 쪽을 **뒤**로 고정해서
        ///   · 앞다리: 엉덩이를 크게 앞으로 + 무릎을 깊게 접어 앞발로 바닥을 디디고,
        ///   · 뒷다리: 허벅지를 거의 수직으로 세우고 무릎을 얕게 접어 **무릎이 바닥에 닿을 듯 말 듯**,
        ///   · 앞팔: 손이 바닥 쪽으로 내려가 몸을 받치고(3점 착지),
        ///   · 뒷팔: 뒤로 크게 젖혀 균형을 잡는다.
        /// 최종 적용 시점(<see cref="ApplyAngle"/>)에서 _facingSign이 곱해지므로, 캐릭터가 왼쪽을 보고
        /// 있으면 이 "앞/뒤"가 통째로 좌우 반전되어 항상 진행 방향이 앞이 된다.
        ///
        /// ============================================================================
        /// amount가 음수일 수 있다 — 눌렸다가 펴지는 반동
        /// ============================================================================
        /// 0 = 직립 중립, 1 = 최대 깊이. 일어서는 구간의 끝에서 호출부가 잠깐 **음수**를 넘기며, 그때는
        /// 중립보다 더 편 자세(다리 완전 직립 + 팔을 바깥으로 더 벌림)로 섞어 몸이 살짝 위로 솟았다가
        /// 가라앉게 한다. Mathf.LerpAngle은 t를 0~1로 clamp하므로 음수를 그대로 넘겨 외삽할 수 없어,
        /// 양수/음수 두 갈래를 명시적으로 나눠 섞는다. 무릎 굽힘은 어느 갈래에서도 0 미만이 될 수 없다
        /// (사람 무릎은 뒤로 꺾이지 않는다 — 이 클래스 전체의 불변식).
        /// </summary>
        public void ApplyLandingCrouchPose(float deltaTime, in PoseSettings idle,
            in LandingCrouchPoseSettings crouch, float smoothingRate, float amount)
        {
            float down = Mathf.Clamp01(amount);
            float up = Mathf.Clamp01(-amount);

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                bool front = limb.NeutralSign >= 0f;
                float upper = NeutralUpperAngle(limb, idle);
                float lower = NeutralLowerAngle(limb, idle);

                if (limb.IsLeg)
                {
                    float deepHip = front ? crouch.FrontHipDegrees : crouch.RearHipDegrees;
                    float deepKnee = KneeBendSign * Mathf.Max(0f, front ? crouch.FrontKneeDegrees : crouch.RearKneeDegrees);
                    upper = Mathf.LerpAngle(upper, deepHip, down);
                    lower = Mathf.LerpAngle(lower, deepKnee, down);
                    if (up > 0f)
                    {
                        // 완전 직립(엉덩이 0 / 무릎 0)이 이 리그에서 가능한 가장 "편" 자세다 —
                        // 그때 발끝이 가장 깊이 내려가므로 접지 보정이 몸을 중립보다 위로 올린다.
                        upper = Mathf.LerpAngle(upper, 0f, up);
                        lower = Mathf.LerpAngle(lower, 0f, up);
                    }
                }
                else
                {
                    float deepShoulder = front ? crouch.FrontArmDegrees : crouch.RearArmDegrees;
                    float deepElbow = ElbowBendSign * Mathf.Max(0f, front ? crouch.FrontElbowDegrees : crouch.RearElbowDegrees);
                    upper = Mathf.LerpAngle(upper, deepShoulder, down);
                    lower = Mathf.LerpAngle(lower, deepElbow, down);
                    if (up > 0f)
                    {
                        upper = Mathf.LerpAngle(upper, limb.NeutralSign * (idle.ArmSpreadDegrees + ReboundArmSpreadDegrees), up);
                        lower = Mathf.LerpAngle(lower, ElbowBendSign * idle.IdleElbowBendDegrees * ReboundElbowRatio, up);
                    }
                }

                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }

            // ★ 몸 높이는 **이번 프레임에 실제로 적용된 각도**에서 계산한다 — 걷기(TickWalkPose)가
            // 각도 적용 **전에** 직전 프레임 각도로 계산하는 것과 일부러 다르다.
            //
            // 왜 다른가: 걷기의 접지 보정은 사이클에 걸쳐 완만히 변해 한 프레임 지연이 눈에 띄지 않지만,
            // 무릎앉아의 "눌림" 구간은 0.1~0.2초 안에 몸을 신장의 16~20%만큼 내린다. 그 구간에서 한
            // 프레임 지연은 곧 발이 지면에서 그만큼 뜨는 것이고, 낮은 fps일수록 커진다. 각도를 먼저
            // 확정하고 그 각도로 몸 높이를 정한 뒤 위 마디의 부착점만 새 오프셋으로 다시 적용하면
            // (아래 두 줄) 그 지연이 원리적으로 0이 된다 — 이 상태에서는 "발이 지면에 붙어 있다"가
            // 근사가 아니라 항등식이다.
            SetBodyOffset(ComputeFootGroundingOffset());
            ReapplyCurrentAngles();
        }

        /// <summary>
        /// ★★ 활 쏘는 자세(2026-08-29, 사용자 요청 "활을 들고 화살을 쏘는" 동작의 몸 쪽 절반).
        /// States/ArcheryState.cs가 매 프레임 자기 진행도를 넘겨 호출한다. 활/화살/과녁 그림은
        /// Interaction/ArcheryRenderer.cs가 그리고, 이 메서드는 **팔다리 각도만** 책임진다.
        ///
        /// ============================================================================
        /// 왜 각도를 "상대 굽힘"이 아니라 "절대 각도"로 받는가
        /// ============================================================================
        /// 이 클래스의 다른 포즈들은 아래 마디를 "무릎/팔꿈치가 몇 도 접혔는가"(상대)로 받는다. 활
        /// 자세만 다르게 절대 각도(<see cref="ArcheryPoseSettings.BowForearmDegrees"/> 등)를 받는 이유는
        /// 실제 만작 자세의 기하 때문이다: 시위를 당긴 손은 뺨 근처에 오는데, 어깨~손 거리(신장의
        /// 약 12%)가 상완+전완 길이(신장의 약 42%)보다 훨씬 짧아 팔이 <b>거의 완전히 접힌다</b>.
        /// 그때의 상대 굽힘은 200도 근처의 값이 되어 설정 파일에 적어도 사람이 읽을 수 없다.
        /// 반면 "위 팔은 뒤로 -100도, 전완은 앞위로 +100도"는 그림이 그대로 떠오른다.
        /// 실제 적용 시에는 (절대 전완각 - 어깨각)으로 상대 각도를 만들어 기존 경로에 그대로 넘긴다.
        ///
        /// ============================================================================
        /// 두 개의 진행도
        /// ============================================================================
        /// <paramref name="draw01"/> 0=중립, 1=완전히 당김. Idle 중립 포즈와 만작 자세를 섞으므로
        /// 당기는 동안 자세가 연속적으로 변한다.
        /// <paramref name="recoil01"/> 0=반동 없음, 1=발사 직후. 당기는 팔만 뒤로 튕겨 나가며 펴진다
        /// (follow-through). draw01은 발사 순간 0으로 떨어지므로 두 값이 동시에 큰 일은 없다.
        ///
        /// 몸 오프셋: 당기는 힘에 몸이 살짝 가라앉는다. 루트 회전은 능동 상태에서 고정이라
        /// (아키텍처 0절) 몸통을 뒤로 기울일 수 없으므로, 시각 전용 상하 오프셋으로 대신한다.
        /// </summary>
        public void ApplyArcheryPose(float deltaTime, in PoseSettings idle, in ArcheryPoseSettings archery,
            float smoothingRate, float ready01, float draw01, float recoil01)
        {
            float ready = Mathf.Clamp01(ready01);
            float draw = Mathf.Clamp01(draw01);
            float recoil = Mathf.Clamp01(recoil01);

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                bool front = limb.NeutralSign >= 0f;
                float upper = NeutralUpperAngle(limb, idle);
                float lower = NeutralLowerAngle(limb, idle);

                if (limb.IsLeg)
                {
                    // 스탠스는 당김과 함께 자리를 잡는다(발을 앞뒤로 벌리고 무릎을 살짝 굽혀 버틴다).
                    float hip = front ? archery.FrontHipDegrees : archery.RearHipDegrees;
                    float knee = KneeBendSign * Mathf.Max(0f, archery.KneeBendDegrees);
                    // 다리는 발사 후에도 스탠스를 유지해야 한다 — draw01은 발사 순간 0이 되므로
                    // 그것만 쓰면 쏘자마자 다리가 중립으로 돌아가 자세가 무너진다. 반동 구간에는
                    // recoil을 함께 보아 스탠스를 붙잡아 둔다.
                    // 스탠스는 **활을 들고 있는 내내** 유지된다(ready). draw만 쓰면 발사 직후 다리가
                    // 중립으로 돌아가 자세가 매 발마다 무너진다.
                    upper = Mathf.LerpAngle(upper, hip, ready);
                    lower = Mathf.LerpAngle(lower, knee, ready);
                }
                else if (front)
                {
                    // 활을 든 팔 — 정면 위로 곧게 뻗어 **활쏘기 상태 내내** 그대로 든다(ready).
                    //
                    // ★ 2026-08-29 육안 검증에서 잡은 실수: 원래 이 팔도 draw/recoil로 섞었더니
                    // 당기지 않는 구간(과녁 등장·발사 후 회복)마다 팔이 중립으로 내려가, 활이
                    // 캐릭터 옆구리에 비스듬히 걸린 것처럼 보였다(사용자 "활이 이상하다"의 큰 몫).
                    // 실제 궁수도 활을 든 팔은 발사 후에도 그대로 둔다 — follow-through의 절반이다.
                    upper = Mathf.LerpAngle(upper, archery.BowArmDegrees, ready);
                    lower = Mathf.LerpAngle(lower,
                        archery.BowForearmDegrees - archery.BowArmDegrees, ready);
                }
                else
                {
                    // 시위를 당기는 팔 — 완전히 접혀 손이 뺨 근처로 온다.
                    float drawUpper = archery.DrawUpperDegrees;
                    float drawLower = archery.DrawForearmDegrees - archery.DrawUpperDegrees;
                    upper = Mathf.LerpAngle(upper, drawUpper, draw);
                    lower = Mathf.LerpAngle(lower, drawLower, draw);

                    if (recoil > 0f)
                    {
                        // 발사 — 어깨가 더 열리며 뒤로 빠지고 팔이 펴진다.
                        float openUpper = drawUpper + archery.RecoilOpenDegrees;
                        float straight = Mathf.Clamp01(archery.RecoilStraighten01);
                        float openLower = Mathf.LerpAngle(drawLower, ElbowBendSign * idle.IdleElbowBendDegrees, straight);
                        upper = Mathf.LerpAngle(upper, openUpper, recoil);
                        lower = Mathf.LerpAngle(lower, openLower, recoil);
                    }
                }

                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }

            // 몸이 살짝 가라앉는다 — 무릎앉아와 달리 접지 역산이 아니라 고정 오프셋이다(다리 각도를
            // 거의 바꾸지 않는 자세라 접지 보정을 쓰면 값이 0에 가까워 아무 일도 일어나지 않는다).
            SetBodyOffset(-Mathf.Max(0f, archery.BodySinkDistance) * Mathf.Max(draw, recoil) * ready);
            ReapplyCurrentAngles();
        }

        /// <summary>
        /// 실측/렌더링용 — 두 손 끝의 월드 좌표(아래 마디 끝 = 로컬 (0,-Length)).
        /// <see cref="GetFootWorldPositions"/>와 완전히 같은 계산이며, 활을 든 손 위치가 필요한
        /// Interaction/ArcheryRenderer.cs가 이 창구만 쓴다(렌더러가 계층을 직접 뒤져 같은 계산을
        /// 한 벌 더 갖는 것을 막는다 — 이 프로젝트가 이미 두 번 겪은 "같은 값의 두 번째 계산원" 함정).
        /// 팔이 없으면 Vector2.zero.
        /// </summary>
        public void GetHandWorldPositions(out Vector2 left, out Vector2 right)
        {
            left = FootWorldPosition(_leftArm);
            right = FootWorldPosition(_rightArm);
        }

        /// <summary>지금 보간 상태(Segment.CurrentAngle)를 각도 변경 없이 다시 적용한다. 각도 자체는
        /// 그대로이므로 회전은 바뀌지 않고, <see cref="ApplyAngle"/>이 함께 계산하는 **부착점 위치**만
        /// 최신 몸 오프셋(_bodyOffsetY)으로 갱신된다. 마디 4~8개짜리 루프라 비용은 무시할 수준이다.</summary>
        private void ReapplyCurrentAngles()
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                ApplyAngle(_segments[i], _segments[i].CurrentAngle);
            }
        }

        /// <summary>일어서는 반동에서 팔을 중립보다 얼마나 더 바깥으로 벌릴지(도). 튜닝 스칼라가 아니라
        /// **자세의 형태**라 StickConfig가 아니라 여기 상수로 둔다(보행 키프레임 표와 같은 판단 기준 —
        /// 이 값 하나만 따로 만지면 반동의 의미가 깨진다). 반동의 크기 자체는
        /// StickConfig.landingCrouchReboundAmount가 정한다.</summary>
        private const float ReboundArmSpreadDegrees = 46f;

        /// <summary>일어서는 반동에서 팔꿈치를 중립 대비 얼마나 펼지(비율). 위 상수와 같은 성격.</summary>
        private const float ReboundElbowRatio = 0.35f;

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
        /// Walk 포즈 — 8개 키포즈 표(<see cref="LegHipKeys"/> 등)를 Catmull-Rom 스플라인으로 보간해
        /// 만든다. 선형 보간을 쓰지 않는 이유는 키포즈마다 각도의 기울기가 꺾여 딱딱해 보이기 때문이고,
        /// SmoothStep을 쓰지 않는 이유는 매 키포즈에서 속도가 0이 되어 "찍고 멈추는" 느낌이 나기
        /// 때문이다 — Catmull-Rom은 키를 정확히 지나면서 속도가 연속이라 이 용도에 맞다.
        ///
        /// 주파수는 임의 계수가 아니라 **실제 이동 속도에서 역산**한다(<see cref="_distancePerCycle"/>
        /// 문서 참고) — 이게 디딤발이 바닥에서 미끄러지는 문워크를 막는 핵심이다.
        ///
        /// amplitudeScale(StickConfig.walkPoseAmplitudeScale)은 표의 모든 관절 각도에 곱해지는 전체
        /// 진폭 배율이고, strideScale(StickConfig.walkStrideScale)은 계산된 사이클 이동 거리에 곱하는
        /// 보정 계수다. 진폭을 바꾸면 보폭도 같이 다시 계산되므로 어느 배율에서도 발이 붙어 있다.
        ///
        /// 부드러움 장치: (1) 위상을 적분해 속도 변화에 위상이 점프하지 않게 하고, (2) 주파수 입력이 되는
        /// 수평 속도 자체를 스무딩하며, (3) 최종 각도를 지수 감쇠로 추종하고, (4) 팔은 다리보다, 아래
        /// 마디는 위 마디보다 각각 더 느슨한 계수를 써 관절 연쇄에 자연스러운 시차를 만든다. 전부
        /// 프레임레이트 독립이다.
        /// </summary>
        public void TickWalkPose(float deltaTime, float horizontalSpeedAbs, in PoseSettings settings,
            float smoothingRate, float speedSmoothingRate, float groundingBlend,
            float amplitudeScale, float strideScale)
        {
            // 주파수 입력 속도 — 호출부가 넘긴 **명령** 속도(walkSpeed)가 아니라 루트가 실제로 이동한
            // 거리에서 측정한다. 벽/발판 경계에 막혀 명령대로 못 나아가는 순간에도 다리가 헛돌지 않게
            // 하려는 것이고, 마찰로 실제 속도가 명령보다 낮은 것(실측 2.26 vs 명령 2.5)도 자동으로
            // 반영된다("사이클 주파수 = 실제 수평 이동 속도 / 보폭" — 리더 지시의 '실제').
            //
            // **반드시 창(window) 단위로 평균 내야 한다**: 물리는 고정 스텝(50Hz)인데 렌더는 그보다
            // 빨라서, 어떤 Update 프레임의 이동량은 0이고 다음 프레임은 한 스텝치가 통째로 들어온다.
            // 프레임 단위 순간속도에 상한을 씌우면 큰 값만 잘리고 0은 그대로 남아 **평균이 통째로
            // 내려간다** — 실측에서 이 때문에 사이클이 1.35Hz여야 할 구간에서 0.94Hz로 돌아 디딤발이
            // 계속 미끄러졌다(로그 phase 증가율로 확인). 창에 거리와 시간을 각각 누적해 나누면 그
            // 톱니가 정확히 상쇄된다. 상한은 순간적인 위치 점프(스냅/텔레포트) 방어용으로만 남긴다.
            if (_root != null)
            {
                float rootX = _root.position.x;
                if (_hasPrevRootX)
                {
                    _speedWindowDistance += Mathf.Abs(rootX - _prevRootX);
                    _speedWindowTime += deltaTime;
                    if (_speedWindowTime >= SpeedWindowSeconds)
                    {
                        _measuredSpeed = Mathf.Min(_speedWindowDistance / _speedWindowTime, horizontalSpeedAbs * 3f);
                        _speedWindowDistance = 0f;
                        _speedWindowTime = 0f;
                    }
                }
                _prevRootX = rootX;
                _hasPrevRootX = true;
            }
            // 아직 한 창도 못 채웠으면 호출부가 넘긴 명령 속도를 그대로 쓴다(걷기 시작 직후 0.1초).
            float measuredSpeed = _measuredSpeed >= 0f ? _measuredSpeed : horizontalSpeedAbs;

            // 첫 Walk 틱에서는 스무딩 없이 실제 속도로 시작한다(_smoothedSpeed 음수 = 미초기화 표식).
            // 0에서 서서히 차오르게 두면 걷기 시작 직후 다리가 실제 이동 속도보다 느리게 놀아 그 구간만
            // 발이 미끄러진다 — 보폭 역산으로 문워크를 없애는 이번 구현에서는 그 자체가 결함이다.
            // 걷는 도중의 속도 변화에는 계속 스무딩이 걸린다(주파수가 튀지 않게).
            _smoothedSpeed = _smoothedSpeed < 0f
                ? measuredSpeed
                : Damp(_smoothedSpeed, measuredSpeed, speedSmoothingRate, deltaTime);

            if (amplitudeScale <= 0f) amplitudeScale = 1f;

            // 사이클 주파수(Hz) = 이동 속도 / 한 사이클 이동 거리. 보폭을 계산할 수 없는 이례적 상황
            // (콜라이더 누락 등)에서만 안전한 고정값으로 폴백한다.
            _distancePerCycle = ComputeDistancePerCycle(amplitudeScale) * Mathf.Max(0.1f, strideScale);
            float cyclesPerSecond = _distancePerCycle > 0.0001f
                ? _smoothedSpeed / _distancePerCycle
                : _smoothedSpeed * 0.6f;
            _phase01 = Mathf.Repeat(_phase01 + cyclesPerSecond * deltaTime, 1f);

            // 상하 바운스 — 손으로 적은 곡선이 아니라 다리 기하학에서 유도한다(아래 메서드 문서 참고).
            // 각도 적용 **전에** 계산하므로 입력은 직전 프레임의 실제 각도다(1프레임 = 사이클의 1% 미만
            // 지연이라 눈에 띄지 않고, 대신 ApplyAngle이 이 값을 그대로 쓸 수 있어 배선이 단순해진다).
            SetBodyOffset(ComputeFootGroundingOffset() * Mathf.Clamp01(groundingBlend));

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                float t = _phase01 + limb.PhaseOffset;

                float upper;
                float lower;
                if (limb.IsLeg)
                {
                    upper = SampleCyclic(LegHipKeys, t) * amplitudeScale;
                    // 스플라인은 키 사이에서 살짝 오버슈트할 수 있다. 무릎이 음수(뒤로 꺾임)가 되는 것은
                    // 절대 허용하지 않으므로 0에서 자른다 — 이 한 줄로 "사람 관절처럼 한 방향으로만
                    // 접힌다"는 불변식이 스플라인 오버슈트와 무관하게 유지된다.
                    lower = KneeBendSign * Mathf.Max(0f, SampleCyclic(LegKneeKeys, t) * amplitudeScale);
                }
                else
                {
                    // 팔은 같은 쪽 다리와 반대 위상(리더 지정 t+0.5).
                    float ta = t + ArmPhaseOffset;
                    upper = SampleCyclic(ArmShoulderKeys, ta) * amplitudeScale;
                    lower = ElbowBendSign * Mathf.Max(0f, SampleCyclic(ArmElbowKeys, ta) * amplitudeScale);
                }

                ApplyLimb(limb, upper, lower, deltaTime, smoothingRate);
            }
        }

        /// <summary>
        /// ★ 2026-08-28 실측 대응 — 걷는 동안 몸통을 상하로 얼마나 움직여야 **낮은 쪽 발이 지면에
        /// 정확히 닿는가**(월드 유닛, 시각 전용 오프셋).
        ///
        /// 왜 손으로 적은 바운스 곡선을 버렸는가(측정 결과): 예전 8키 표(BounceKeys, 진폭
        /// walkBounceAmplitude=0.025)를 이 계산과 대조해보니 **위상이 서로 반대**였다. 사람이 걸을 때
        /// 엉덩이는 다리가 몸 아래 수직으로 지날 때(t=0.25) 가장 높고 두 발이 앞뒤로 벌어졌을 때
        /// (t≈0.44) 가장 낮은데, 옛 표는 t=0.125에서 최저(-1)를 찍었다. 그 결과 실제 프리팹 치수에서
        /// 디딤발이 지면을 최대 **0.025유닛 파고들고**(t≈0.12) 반대로 최대 **0.070유닛 떠 있었다**
        /// (t≈0.44). 즉 "땅에 닿아 있어야 할 발"이 계속 지면을 들락거렸다 — 보폭/주파수 역산이 정확한데도
        /// 걸음이 어색해 보이던 원인 중 하나다.
        ///
        /// 계산: 루트 원점이 곧 지면(이 프로젝트의 접지 규약)이고 발끝의 루트 기준 높이는
        ///   엉덩이 부착 높이(PivotLocal.y) − (U·cos(엉덩이각) + L·cos(엉덩이각 − 무릎굽힘))
        /// 이므로, 두 다리 중 **더 깊이 내려간 쪽**(= 지금 땅에 닿아 있는 발)의 값을 0으로 만드는
        /// 오프셋은 그대로 (그 깊이 − 부착 높이)다. 두 다리의 최댓값을 쓰므로 발이 바뀌는 순간에도
        /// 연속이고(두 연속 함수의 max), 어떤 발도 지면 아래로 내려가지 않는다.
        ///
        /// 입력이 "목표 각도"가 아니라 **지금 실제로 적용돼 있는 각도**(Segment.CurrentAngle)라는 점이
        /// 중요하다 — 지수 감쇠 보간 때문에 실제 각도는 표보다 진폭이 조금 작은데, 표 기준으로 계산하면
        /// 그 차이만큼 다시 어긋난다. 좌우 반전(_facingSign)은 각도 부호만 뒤집으므로 cos에 영향이 없다.
        /// </summary>
        private float ComputeFootGroundingOffset()
        {
            float deepest = float.NegativeInfinity;
            float pivotY = 0f;
            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                if (!limb.IsLeg || limb.Upper == null) continue;

                float hip = limb.Upper.CurrentAngle;
                // 아래 마디의 CurrentAngle은 KneeBendSign이 곱해진 값이라, 굽힘량으로 되돌린다.
                float knee = limb.Lower != null ? limb.Lower.CurrentAngle * KneeBendSign : 0f;
                float drop = limb.Upper.Length * Mathf.Cos(hip * Mathf.Deg2Rad)
                           + (limb.Lower != null ? limb.Lower.Length * Mathf.Cos((hip - knee) * Mathf.Deg2Rad) : 0f);
                if (drop > deepest)
                {
                    deepest = drop;
                    pivotY = limb.Upper.PivotLocal.y;
                }
            }
            return deepest > float.NegativeInfinity ? deepest - pivotY : 0f;
        }

        /// <summary>
        /// 순환 키프레임 배열을 위상(0~1)에서 Catmull-Rom 스플라인으로 샘플링한다. 배열 전체가 한
        /// 사이클이며 끝과 처음이 이어지므로(순환) 이음매에서도 각도가 매끄럽게 연결된다.
        /// </summary>
        private static float SampleCyclic(float[] keys, float phase01)
        {
            int n = keys.Length;
            if (n == 0) return 0f;
            if (n == 1) return keys[0];

            float x = Mathf.Repeat(phase01, 1f) * n;
            int i1 = Mathf.FloorToInt(x) % n;
            float u = x - Mathf.Floor(x);
            int i0 = (i1 - 1 + n) % n;
            int i2 = (i1 + 1) % n;
            int i3 = (i1 + 2) % n;
            return CatmullRom(keys[i0], keys[i1], keys[i2], keys[i3], u);
        }

        /// <summary>Catmull-Rom 스플라인 1구간. p1~p2 사이를 u(0~1)로 보간하며 키를 정확히 지난다.</summary>
        private static float CatmullRom(float p0, float p1, float p2, float p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
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

        /// <summary>실측 검증/디버그용 — 두 발 끝의 월드 좌표(아래 마디 끝 = 로컬 (0,-Length)).
        /// 발 미끄러짐(문워크) 확인에 쓴다: 디딤 국면 동안 디딤발의 월드 X가 거의 고정이어야 한다.</summary>
        public void GetFootWorldPositions(out Vector2 left, out Vector2 right)
        {
            left = FootWorldPosition(_leftLeg);
            right = FootWorldPosition(_rightLeg);
        }

        /// <summary>
        /// ★ 지금 이 포즈에서 <b>몸이 그리는 잉크의 가장 낮은 월드 Y</b>(2026-08-31, GETUP 바닥 관통).
        ///
        /// 왜 필요한가: 이 프로젝트의 접지 규약은 "루트 원점 = 발바닥"이라 <b>서 있을 때만</b> 참이다.
        /// 누워 있는 몸(RAGDOLL 직후의 GETUP 첫 프레임)에 그 규약을 그대로 강제하면 회전한 몸의
        /// 반대편 파츠가 기하학적으로 발판 아래로 갈 수밖에 없다. 그 깊이를 알아야 "필요한 만큼만"
        /// 들어 올릴 수 있다(States/GetupState 참고).
        ///
        /// 왜 렌더러 정점 전수가 아니라 이 점들인가: 팔다리는 마디 2개짜리 꺾은선이라
        /// <b>관절 3점(부착점/무릎·팔꿈치/끝)</b>이 그 선분의 극값을 전부 지배하고, 몸통은 엉덩이~어깨
        /// 사이라 다리·팔 부착점에 이미 포함되며, 머리는 원이라 중심에서 반지름만큼 아래가 항상 최저다
        /// (회전과 무관 — 그래서 정수리를 따로 볼 필요가 없다). 즉 이 16점이 몸 전체의 하한을 정확히
        /// 준다. 획 두께의 절반(약 1.25pt)은 일부러 빼지 않는다 — 안전망 여백(8pt) 안이고, 빼면
        /// 서 있는 자세에서도 리프트가 남아 GETUP -> Idle 전이에 그만큼의 단차가 생긴다.
        ///
        /// 액세서리(모자/망토)는 여기 없다 — 그쪽은 자기 정점을 아는 부품이 스스로 신고한다
        /// (Core/ICharacterInkExtentProvider).
        /// </summary>
        /// <param name="headRadiusWorld">머리 링의 실측 반지름(월드 유닛). 0 이하면 머리를 건너뛴다.</param>
        public bool TryGetLowestBodyInkWorldY(float headRadiusWorld, out float worldY)
        {
            worldY = float.PositiveInfinity;
            bool any = false;

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                if (limb == null) continue;
                if (limb.Upper != null && limb.Upper.Transform != null)
                {
                    float y = limb.Upper.Transform.position.y;   // 엉덩이/어깨 부착점(= 몸통 양 끝).
                    if (!any || y < worldY) { worldY = y; any = true; }
                }
                if (limb.Lower == null || limb.Lower.Transform == null) continue;
                Transform lower = limb.Lower.Transform;
                float knee = lower.position.y;                    // 무릎/팔꿈치.
                if (!any || knee < worldY) { worldY = knee; any = true; }
                float tip = lower.TransformPoint(new Vector3(0f, -limb.Lower.Length, 0f)).y; // 발끝/손끝.
                if (tip < worldY) worldY = tip;
            }

            if (_head != null && headRadiusWorld > 0f)
            {
                float y = _head.position.y - headRadiusWorld;
                if (!any || y < worldY) { worldY = y; any = true; }
            }

            if (!any) worldY = 0f;
            return any;
        }

        /// <summary>실측 검증/디버그용 — 현재 보행 사이클 위상(0~1)과 한 사이클 이동 거리.</summary>
        public float WalkPhase01 => _phase01;
        public float DistancePerCycle => _distancePerCycle;

        private static Vector2 FootWorldPosition(Limb limb)
        {
            if (limb == null || limb.Lower == null || limb.Lower.Transform == null) return Vector2.zero;
            return limb.Lower.Transform.TransformPoint(new Vector3(0f, -limb.Lower.Length, 0f));
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
        private void SetBodyOffset(float offsetY) => SetBodyOffset(offsetY, 0f);

        /// <param name="headOffsetX">머리만 좌우로 미는 시각 전용 오프셋(월드 유닛) — 유휴 앰비언트
        /// "주위 살피기"에서만 0이 아니다. 인자 없는 오버로드가 항상 0을 넣으므로, 다른 포즈 경로로
        /// 넘어가는 순간(Walk/Fall/Ragdoll 등 전부 SetBodyOffset을 부른다) 자동으로 원복된다 —
        /// 연출이 중간에 끊겨도 머리가 옆으로 밀린 채 굳는 경우가 구조적으로 없다.</param>
        private void SetBodyOffset(float offsetY, float headOffsetX)
        {
            _bodyOffsetY = offsetY;
            if (_torso != null) _torso.localPosition = new Vector3(_torsoNeutral.x, _torsoNeutral.y + offsetY, _torsoNeutral.z);
            if (_head != null) _head.localPosition = new Vector3(_headNeutral.x + headOffsetX, _headNeutral.y + offsetY, _headNeutral.z);
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
            public readonly float BreathAmplitude;
            public readonly float BreathFrequencyHz;
            public readonly float BreathArmDegrees;

            public PoseSettings(float legSpread, float armSpread, float idleKnee, float idleElbow,
                float breathAmplitude, float breathFrequencyHz, float breathArmDegrees)
            {
                LegSpreadDegrees = legSpread;
                ArmSpreadDegrees = armSpread;
                IdleKneeBendDegrees = idleKnee;
                IdleElbowBendDegrees = idleElbow;
                BreathAmplitude = breathAmplitude;
                BreathFrequencyHz = breathFrequencyHz;
                BreathArmDegrees = breathArmDegrees;
            }
        }

        /// <summary>
        /// 매달리기 포즈 각도 묶음(위 PoseSettings와 같은 성격·같은 컨벤션 — readonly struct + in 파라미터).
        /// StickmanBlackboard.BuildLedgeHangPoseSettings()가 StickConfig에서 구성해 넘긴다.
        /// </summary>
        public readonly struct LedgeHangPoseSettings
        {
            public readonly float ArmSpreadDegrees;
            public readonly float ElbowBendDegrees;
            public readonly float LegSpreadDegrees;
            public readonly float KneeBendDegrees;
            public readonly float SwayAmplitudeDegrees;
            public readonly float SwayFrequencyHz;

            public LedgeHangPoseSettings(float armSpread, float elbowBend, float legSpread, float kneeBend,
                float swayAmplitude, float swayFrequencyHz)
            {
                ArmSpreadDegrees = armSpread;
                ElbowBendDegrees = elbowBend;
                LegSpreadDegrees = legSpread;
                KneeBendDegrees = kneeBend;
                SwayAmplitudeDegrees = swayAmplitude;
                SwayFrequencyHz = swayFrequencyHz;
            }
        }

        /// <summary>
        /// 낙하 중 공중 자세 각도 묶음(<see cref="ApplyFallPose"/>). 위 두 구조체와 같은 성격·같은
        /// 컨벤션(readonly struct + in 파라미터 — 매 프레임 경로라 힙 할당/복사 비용이 없다).
        /// StickmanBlackboard.BuildFallPoseSettings()가 StickConfig에서 구성해 넘긴다.
        /// </summary>
        public readonly struct FallPoseSettings
        {
            public readonly float ArmRaiseDegrees;
            public readonly float ElbowBendDegrees;
            public readonly float LegSpreadDegrees;
            public readonly float HipDegrees;
            public readonly float KneeBendDegrees;

            public FallPoseSettings(float armRaise, float elbowBend, float legSpread, float hip, float kneeBend)
            {
                ArmRaiseDegrees = armRaise;
                ElbowBendDegrees = elbowBend;
                LegSpreadDegrees = legSpread;
                HipDegrees = hip;
                KneeBendDegrees = kneeBend;
            }
        }

        /// <summary>
        /// 발버둥 자세 각도 묶음(<see cref="ApplyDragStrugglePose"/>). 위 구조체들과 같은 성격·같은
        /// 컨벤션. StickmanBlackboard.BuildDragStrugglePoseSettings()가 StickConfig에서 구성해 넘긴다.
        /// </summary>
        public readonly struct DragStrugglePoseSettings
        {
            public readonly float FrequencyHz;
            public readonly float HipDegrees;
            public readonly float KneeDegrees;
            public readonly float ArmDegrees;
            public readonly float ElbowDegrees;

            public DragStrugglePoseSettings(float frequencyHz, float hip, float knee, float arm, float elbow)
            {
                FrequencyHz = frequencyHz;
                HipDegrees = hip;
                KneeDegrees = knee;
                ArmDegrees = arm;
                ElbowDegrees = elbow;
            }
        }

        /// <summary>
        /// 공중 회전(텀블링) 자세 각도 묶음(<see cref="ApplyThrowTumblePose"/>). 위 구조체들과 같은
        /// 성격·같은 컨벤션(readonly struct + in 파라미터 — 매 프레임 경로라 힙 할당이 없다).
        /// StickmanBlackboard.BuildThrowTumblePoseSettings()가 StickConfig에서 구성해 넘긴다.
        /// </summary>
        public readonly struct ThrowTumblePoseSettings
        {
            public readonly float HipDegrees;
            public readonly float KneeBendDegrees;
            public readonly float ArmDegrees;
            public readonly float ElbowBendDegrees;
            public readonly float LimbSpreadDegrees;

            public ThrowTumblePoseSettings(float hip, float kneeBend, float arm, float elbowBend, float limbSpread)
            {
                HipDegrees = hip;
                KneeBendDegrees = kneeBend;
                ArmDegrees = arm;
                ElbowBendDegrees = elbowBend;
                LimbSpreadDegrees = limbSpread;
            }
        }

        /// <summary>
        /// 무릎앉아 착지 포즈의 **최대 깊이 각도** 묶음(<see cref="ApplyLandingCrouchPose"/>).
        /// 여기 담긴 값은 전부 "amount=1일 때의 각도"이며, 중간 깊이는 Idle 중립과의 보간으로 만들어진다.
        /// StickmanBlackboard.BuildLandingCrouchPoseSettings()가 StickConfig에서 구성해 넘긴다.
        /// </summary>
        public readonly struct LandingCrouchPoseSettings
        {
            public readonly float FrontHipDegrees;
            public readonly float FrontKneeDegrees;
            public readonly float RearHipDegrees;
            public readonly float RearKneeDegrees;
            public readonly float FrontArmDegrees;
            public readonly float FrontElbowDegrees;
            public readonly float RearArmDegrees;
            public readonly float RearElbowDegrees;

            public LandingCrouchPoseSettings(float frontHip, float frontKnee, float rearHip, float rearKnee,
                float frontArm, float frontElbow, float rearArm, float rearElbow)
            {
                FrontHipDegrees = frontHip;
                FrontKneeDegrees = frontKnee;
                RearHipDegrees = rearHip;
                RearKneeDegrees = rearKnee;
                FrontArmDegrees = frontArm;
                FrontElbowDegrees = frontElbow;
                RearArmDegrees = rearArm;
                RearElbowDegrees = rearElbow;
            }
        }

        /// <summary>
        /// 활 쏘는 자세 각도 묶음(<see cref="ApplyArcheryPose"/>). 위 구조체들과 같은 성격·같은
        /// 컨벤션(readonly struct + in 파라미터 — 매 프레임 경로라 힙 할당/복사 비용이 없다).
        /// StickmanBlackboard.BuildArcheryPoseSettings()가 StickConfig에서 구성해 넘긴다.
        ///
        /// ★ 팔 각도는 <b>절대 각도</b>다(다른 구조체의 "상대 굽힘"과 다르다) — 그 이유는
        /// <see cref="ApplyArcheryPose"/> 문서 참고. BodySinkDistance만 월드 유닛이며, 호출부가
        /// 신장 비율에서 이미 곱해 넘긴다(각도는 크기 무관, 거리는 신장 비례 — 리더 지시).
        /// </summary>
        public readonly struct ArcheryPoseSettings
        {
            public readonly float BowArmDegrees;
            public readonly float BowForearmDegrees;
            public readonly float DrawUpperDegrees;
            public readonly float DrawForearmDegrees;
            public readonly float RecoilOpenDegrees;
            public readonly float RecoilStraighten01;
            public readonly float FrontHipDegrees;
            public readonly float RearHipDegrees;
            public readonly float KneeBendDegrees;
            public readonly float BodySinkDistance;

            public ArcheryPoseSettings(float bowArm, float bowForearm, float drawUpper, float drawForearm,
                float recoilOpen, float recoilStraighten01, float frontHip, float rearHip, float kneeBend,
                float bodySinkDistance)
            {
                BowArmDegrees = bowArm;
                BowForearmDegrees = bowForearm;
                DrawUpperDegrees = drawUpper;
                DrawForearmDegrees = drawForearm;
                RecoilOpenDegrees = recoilOpen;
                RecoilStraighten01 = recoilStraighten01;
                FrontHipDegrees = frontHip;
                RearHipDegrees = rearHip;
                KneeBendDegrees = kneeBend;
                BodySinkDistance = bodySinkDistance;
            }
        }

        /// <summary>
        /// 유휴 앰비언트 동작(26-3) 각도/거리 묶음 — <see cref="ApplyIdleAmbientPose"/> 참고.
        /// 각도는 크기와 무관하므로 절대값이고, 거리 성분(<see cref="LookHeadShiftDistance"/> /
        /// <see cref="StretchRiseDistance"/>)만 호출부가 신장을 곱해 월드 유닛으로 넘긴다
        /// (ArcheryPoseSettings.BodySinkDistance와 같은 관례 — 배율 대응의 단일 창구는 StickmanMetrics다).
        /// </summary>
        public readonly struct IdleAmbientPoseSettings
        {
            public readonly float LookArmDegrees;
            public readonly float LookElbowDegrees;
            public readonly float LookHeadShiftDistance;
            public readonly float StretchArmSpreadDegrees;
            public readonly float StretchElbowDegrees;
            public readonly float StretchKneeStraighten01;
            public readonly float StretchRiseDistance;

            public IdleAmbientPoseSettings(float lookArm, float lookElbow, float lookHeadShiftDistance,
                float stretchArmSpread, float stretchElbow, float stretchKneeStraighten01, float stretchRiseDistance)
            {
                LookArmDegrees = lookArm;
                LookElbowDegrees = lookElbow;
                LookHeadShiftDistance = lookHeadShiftDistance;
                StretchArmSpreadDegrees = stretchArmSpread;
                StretchElbowDegrees = stretchElbow;
                StretchKneeStraighten01 = stretchKneeStraighten01;
                StretchRiseDistance = stretchRiseDistance;
            }
        }
    }
}
