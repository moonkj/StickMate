using UnityEngine;

namespace StickMate.States
{
    /// <summary>
    /// ★ 무릎/팔꿈치의 **각진 모서리를 없애는 렌더링 전용** 컴포넌트(2026-09-01).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 사용자 신고
    /// ============================================================================
    /// "캐릭터들이 이렇게 부드럽게 표현되어야하는데 아직도 고도화가 덜됨"(참고: Alan Becker 스틱맨).
    /// 참고 그림의 팔다리는 허벅지→종아리가 <b>하나의 흐르는 곡선</b>이고 무릎에 각진 모서리가 없다.
    /// 우리는 마디마다 2점 직선 LineRenderer라 관절이 항상 꺾인 각으로 그려졌다.
    ///
    /// <b>두께와 짝을 이룬다</b>: 굵은 획을 각진 관절에 얹으면 안쪽에 잉크가 뭉친다(이 저장소가
    /// 2026-08-28에 겪고 LineWidthScale=0.7로 후퇴한 바로 그 실패). 곡선화가 그 뭉침의 원인을
    /// 제거하므로 두께 상향의 <b>전제 조건</b>이다.
    ///
    /// ============================================================================
    /// 무엇을 바꾸고 무엇을 안 바꾸는가
    /// ============================================================================
    /// <b>바꾸는 것은 LineRenderer의 점 목록뿐이다.</b> Transform / Rigidbody2D / HingeJoint2D /
    /// BoxCollider2D / 포즈 각도 / IK는 한 줄도 건드리지 않는다. 그래서:
    ///   · 발끝·손끝의 <b>월드 좌표가 정확히 그대로다</b> — 곡선은 마디의 시작점(관절)과 끝점을
    ///     움직이지 않고 그 <b>사이</b>만 바꾼다. 접지 판정/보폭/사격 조준이 무영향인 근거다.
    ///   · RAGDOLL도 자동으로 커버된다 — LateUpdate에서 <b>실제 localRotation</b>을 읽으므로
    ///     각도의 출처가 포즈 애니메이터든 물리 솔버든 상관없다.
    ///
    /// ============================================================================
    /// 기하학 — 원호 필렛(fillet), 베지어/Catmull-Rom이 아니다
    /// ============================================================================
    /// 관절 A(고관절/어깨) → B(무릎/팔꿈치) → C(발끝/손끝)의 꺾인 corner를 <b>두 변에 접하는
    /// 원호</b>로 갈아낸다. 각 마디는 자기 Transform 로컬 공간에서만 그리므로 원호를 정확히 절반씩
    /// 나눠 갖는다(아래 대칭성 증명).
    ///
    /// <code>
    ///   t  = 필렛 길이(관절에서 각 변을 따라 되돌아간 거리)
    ///   θ  = 관절 굽힘각(= 아래 마디의 localRotation.z), s = sign(θ), h = |θ|/2
    ///   r  = t / tan(h)                     (두 변에 접하는 원의 반지름)
    ///
    ///   위 마디 로컬 :  Arc(φ) = ( s·r·(1−cos φ),  −(Lu−t) − r·sin φ ),  φ ∈ [0, h]
    ///   아래 마디 로컬:  Arc(φ) = ( s·r·(1−cos φ),  −t       + r·sin φ ),  φ ∈ [0, h]
    /// </code>
    ///
    /// 두 식이 <b>완전히 같은 모양</b>인 것은 우연이 아니다. 아래 마디의 원 중심을 위 마디 좌표계에서
    /// 변환하면 (s·r, −t)가 나오고(수치 검증: 접합 오차 ~1e−17), 그래서 두 반호는 각도 이등분선
    /// 위에서 <b>기울기까지 일치하며</b> 만난다(C1 연속). 즉 이음매가 보이지 않는다.
    ///
    /// <b>왜 Catmull-Rom이 아닌가</b>: A,B,C 세 점을 지나는 Catmull-Rom은 B에서의 접선이 (C−A)/2라
    /// 깊게 접힐수록 |C−A|가 0으로 수렴해 <b>첨점(cusp)이 생긴다</b>. 필렛은 θ→180°에서 r→0으로
    /// 수렴할 뿐이라 퇴화 구간이 없다.
    ///
    /// ============================================================================
    /// ★ 곡률 상한 — "관절이 사라져 흐물거리면 안 된다"
    /// ============================================================================
    /// 필렛은 corner를 <b>깎아내므로</b> 관절 끝점이 안쪽으로 물러난다(sagitta = t·tan(θ/4)).
    /// 너무 많이 물러나면 무릎앉아 착지/활쏘기에서 관절이 녹아버린다. 그래서 물러나는 양을
    /// <b>획 두께 W의 배수</b>로 자른다(<see cref="MaxSagittaPerStrokeWidth"/>) — 상한을 W로 표현하면
    /// 두께를 올려도 "관절이 획 하나 두께만큼만 둥글어진다"는 뜻이 그대로 유지된다.
    ///
    /// 반대쪽 하한도 있다(이쪽은 자동 충족): 안쪽 실루엣이 매끄러우려면 r ≥ W/2 여야 한다
    /// (r이 그보다 작으면 안쪽 가장자리가 자기 자신과 교차해 각진 크리즈가 남는다).
    /// 실제 포즈 각도 전 구간(무릎 4°~126°, 팔꿈치 10°~122°) × <b>다이얼 배율 전 구간</b>
    /// (MinCharacterScale ~ MaxCharacterScale)에서 이 부등식이 성립하는지는
    /// Tests/EditMode/LimbCurveGeometryTests가 <b>프로덕션 상수를 참조해</b> 검사한다.
    /// <b>배율을 훑는 것이 핵심이다</b> — 획에는 화면상 하한(2pt)이 있어서 배율을 내리면 마디만
    /// 짧아지고 획은 그대로다. 2026-09-01 이전에는 프리팹이 구워진 배율 하나만 검사해서,
    /// 다이얼 하단(0.35~0.45)이 규칙을 어기고 있는 것을 아무도 못 봤다.
    ///
    /// ============================================================================
    /// 비용 — 24시간 상주 앱이다
    /// ============================================================================
    ///   · 마디당 점 2개 → <see cref="PointsPerSegment"/>개. 8마디 총 16 → 40점.
    ///   · <b>각도가 안 바뀐 마디는 통째로 건너뛴다</b>(<see cref="RebuildEpsilonDegrees"/>).
    ///     완전 정지 상태에서는 LineRenderer 쓰기가 0회다.
    ///   · 매 프레임 할당 0 — 점 버퍼는 마디마다 한 번 잡아두고 재사용하고,
    ///     각도는 쿼터니언 z/w에서 직접 뽑아 <c>localEulerAngles</c>(변환 비용 + 0~360 랩어라운드)를
    ///     피한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LimbCurveRenderer : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────────────────
        // 튜닝 상수 — StickConfig(직렬화 에셋)가 아니라 여기 public const로 두는 이유:
        // 이 값들은 사용자가 조절할 성질이 아니라 "관절을 어떻게 갈아내는가"라는 <b>기하학 불변식</b>이고,
        // 에디터(SceneBootstrapper의 프리팹 굽기)와 런타임과 테스트가 <b>같은 하나</b>를 봐야 한다.
        // 테스트는 반드시 이 상수를 참조한다(CLAUDE.md: 프로덕션 상수를 테스트에 숫자로 베끼지 않는다).
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>필렛 길이 t를 <b>짧은 쪽 마디 길이</b>의 몇 배로 잡을지. 0.5를 넘으면 두 필렛이
        /// 마디 한가운데서 만나 직선 구간이 사라진다(= 팔다리가 활처럼 휜다).
        ///
        /// <para>★ 2026-09-01 <b>0.35 → 0.42</b>(docs/CHARACTER_FORM_SPEC.md 4-4 M3-2). 왜 올렸나:
        /// 획 두께에는 <b>화면상 하한 2pt</b>가 있어서(StickConfig.MinStrokeScreenPoints) 배율을
        /// 내리면 기하학만 줄고 획은 안 줄어든다 — 배율 0.35에서 실효 획이 다리 1.29배·팔 1.55배로
        /// 부푼다. 그 결과 무릎앉아(126°)/주위살피기(122°)에서 필렛 원호의 곡률 반경이 획 반두께보다
        /// 작아져 <b>안쪽 윤곽이 자기교차</b>했다(여유 0.76배 / 0.86배 = 위반). 0.42면 전 배율·전 자세에서
        /// 여유 ≥ 1.05배다.</para>
        ///
        /// <para>★ <b>이 값만 올려서는 낮은 배율에서 효과가 0이었다</b> — <see cref="SolveFilletLength"/>의
        /// sagitta 캡이 걸리면 t = W/tan(θ/4)가 되어 원래 t와 무관해지기 때문이다. 그 캡이 월드 단위 획을
        /// 로컬 단위 마디와 비교하던 <b>단위 불일치</b>를 같은 라운드에 고쳤다(<see cref="Rebuild"/> 참고).
        /// 둘은 한 쌍이다 — 한쪽만 되돌리지 마라.</para>
        ///
        /// <para>"뼈"는 남는가: 마디의 <b>58~62%가 여전히 직선</b>이다(배율 0.75 다리 위 마디
        /// 9.05pt → 8.22pt). 0.5를 넘기지 않는 한 활이 되지 않는다.</para></summary>
        public const float FilletLengthRatio = 0.42f;

        /// <summary>관절 끝점이 안쪽으로 물러나도 되는 최대 거리를 <b>획 두께 W의 배수</b>로 지정.
        /// 1.0 = "획 하나 두께까지만". 이 상한이 곡률 상한 그 자체다(클래스 문서 ★ 참고).</summary>
        public const float MaxSagittaPerStrokeWidth = 1.0f;

        /// <summary>반호 하나에 찍는 점 개수(양 끝 포함). 마디의 <see cref="PointsPerSegment"/>를 정한다.
        /// 3이면 최대 굽힘(126°)에서 호 한 변이 31.5°라 현(chord) 오차가 획 두께의 12%로 눈에 띈다.
        /// 4면 21°/오차 1.5%로 떨어져 육안 한계 아래다 — 그 이상은 정점만 늘고 그림이 같다.</summary>
        public const int ArcSamplesPerHalf = 4;

        /// <summary>마디 하나가 쓰는 LineRenderer 점 개수 = 마디 바깥쪽 끝점 1 + 반호 표본.
        /// <b>항상 고정</b>이라 positionCount가 매 프레임 바뀌며 메시를 재할당하는 일이 없다.</summary>
        public const int PointsPerSegment = ArcSamplesPerHalf + 1;

        /// <summary>이 각도(도) 미만으로만 움직인 마디는 다시 굽지 않는다. 24시간 상주 앱이라
        /// "정지 화면에서는 아무 일도 하지 않는다"가 기본값이어야 한다.</summary>
        public const float RebuildEpsilonDegrees = 0.05f;

        /// <summary>납작한 폴리라인에서 <b>관절점의 인덱스</b>. 위 마디의 마지막 점이자 아래 마디의
        /// 첫 점이며 <b>같은 점 하나</b>다(두 반호는 각도 이등분선 위에서 정확히 만난다 — 클래스 문서의
        /// 대칭성 증명). 초상화/테스트가 무릎·팔꿈치를 집을 때 쓰는 공개 계약이다.</summary>
        public const int PolylineJointIndex = PointsPerSegment - 1;

        /// <summary>2분절 마디 하나를 <b>납작한 폴리라인 한 줄</b>로 폈을 때의 점 개수.
        /// 초상화(Interaction/CharacterPortraitStage)가 버퍼를 이 크기로 잡는다.
        ///
        /// <para>★ 2026-09-01 <b>2×PointsPerSegment → 그것보다 1 적게</b>(docs/CHARACTER_FORM_SPEC.md 4-5).
        /// 예전에는 관절점을 <b>두 번</b> 담아 인덱스 4와 5가 같은 좌표였고, 그래서 폴리라인 안에
        /// <b>길이 0인 선분</b>이 하나 있었다. 두꺼운 폴리라인 속의 퇴화 선분은 2026-09-01 "발" 실패와
        /// 같은 계열(코너 조인이 자기교차)이라 구조적으로 제거한다. 관절점은 이제
        /// <see cref="PolylineJointIndex"/>에 <b>한 번만</b> 담긴다.</para></summary>
        public const int PolylinePointCount = PointsPerSegment * 2 - 1;

        // ========================================================================
        // ★ 발(foot) — 넣었다가 <b>같은 날 되돌렸다</b>(2026-09-01)
        // ========================================================================
        // 사용자 지시: <b>"이럴바엔 그냥 다시 다리를 원래대로 돌리는게 맞음. 발을 넣으면서 이상해짐"</b>
        // (그 직전 신고: "다리가 이상해졌어"). 그래서 발 코드는 전부 제거했다.
        // <b>곡선화와 획 두께는 그대로 유지</b>한다 — 사용자가 문제 삼은 것은 발뿐이다.
        //
        // 다시 시도할 사람을 위해 <b>측정값과 실패 원인 가설을 여기 남긴다</b>. 이걸 지우면
        // 다음 사람이 같은 실측을 처음부터 다시 하고 같은 함정에 다시 빠진다.
        //
        // ── (A) 참고 이미지(@alanbecker) 재실측 — 이 값들은 유효하다
        //    직전 라운드의 스펙 "획 두께의 1.2배 / 앞으로 1.5획"은 <b>둘 다 재현되지 않았다</b>:
        //      · 두께: 발목 EDT가 T=37→45로 오르는 것은 맞지만, 그건 발이 두꺼워서가 아니라
        //        정강이 획과 발 획이 ㄴ자로 만나는 <b>corner에서 내접원이 커지기 때문</b>이다.
        //        발끝 쪽에서 획을 가로질러 재면 39~40px = 정강이(37~38)와 사실상 같다.
        //        → 발 획 두께 = 다리 획 두께 <b>1.0배</b>.
        //      · 길이: "가로 런 53~58px"은 <b>정강이 폭까지 포함한 전체 폭</b>이었다. 발끝 둥근 캡의
        //        중심을 원 피팅으로 구하면 발목에서 16.5px(빨강)/20.2px(초록) = <b>획의 0.43~0.53배</b>.
        //        검산: 정강이 반폭 19 + 발 16.5 + 발끝 캡 19.5 = 55 ✓ (측정 55).
        //      → 정지 측면 자세 기준 확정값: <b>두께 1.0배 / 길이 0.5획</b>.
        //
        // ── (B) 방향 규칙 — 뿌리(고관절)에서 발목까지의 <b>가로 오프셋</b>
        //    참고 이미지 3장이 전부 "앞다리 발은 앞, 뒷다리 발은 뒤"(heel-strike / toe-off)라
        //    facing 기준으로는 설명되지 않는다. 오프셋을 다리 길이의 0.12배로 나눠 −1~+1로 자르고
        //    (bias), 발목 각도 = 90° × bias 로 썼다.
        //
        // ── (C) ★ 왜 실패했나 — 확정 아님, 가설 두 개
        //    (C1) <b>동작 중 길이가 변한다</b>. bias→0(디딤 중간)에서 발이 정강이의 직선 연장이 되어
        //         지면을 파고들기에, 발 길이에 |bias|를 곱해 접히게 했다. 침투는 0.498획 → 0.210획으로
        //         줄었지만, 대신 <b>걸음마다 발이 100%↔1%로 두 번 커졌다 작아진다</b>(실측 시뮬레이션).
        //         화면에서는 "발이 생겼다 사라졌다"로 읽힌다. 리더가 지목한 가설이다.
        //    (C2) <b>이 저장소의 '규칙 1'(최단 선분 ≥ 획 두께)을 발이 위반했다</b>.
        //         두꺼운 폴리라인이 각 th로 꺾일 때 안쪽 오프셋이 자기교차하지 않으려면 양쪽 선분이
        //         최소 r·tan(th/2)(r = 획 반폭) 이상이어야 한다. 발은 길이 = r·|bias|,
        //         꺾임 = 90°·|bias| 라 필요량이 r·tan(45°·|bias|)이고, <b>|bias|=1에서 여유가 정확히 0</b>이다.
        //         그런데 |bias|=1이 바로 <b>서 있을 때(다리 벌림 12°)와 걷기의 대부분</b>이다.
        //         즉 발은 거의 항상 <b>자기교차 경계</b>에서 그려졌다 — LineRenderer가 그 지점에서
        //         발목에 핀치/노치를 낼 수 있다. 펫 마디에는 이미 같은 규칙을 강제하는 테스트가
        //         있었는데(선분이_획보다_짧지_않다) 본체 팔다리에는 없어서 통과해 버렸다.
        //
        // ── (D) 다시 넣는다면
        //    · 길이를 |bias|로 줄이지 말 것(C1). 대신 발목 각도를 정강이 기준으로 <b>제한</b>해
        //      지면 침투를 막는 쪽을 검토할 것.
        //    · 또는 발을 <b>같은 폴리라인에 붙이지 말고 독립 LineRenderer 2점 선</b>으로 그릴 것 —
        //      코너 조인이 아예 없어져 (C2)가 구조적으로 사라진다(발목은 둥근 캡 두 개가 겹칠 뿐이고,
        //      그게 참고 이미지의 모양이기도 하다). 대신 다리마다 GameObject가 하나씩 는다.
        //    · 본체 팔다리에도 "최단 선분 ≥ 획" 테스트를 먼저 깔 것.
        //    · 검증은 <b>반드시 실제 빌드 캡처</b>로 할 것. 이번에 나는 오프라인 렌더러로 확인했는데
        //      그 렌더러는 모든 점에 둥근 캡을 찍어 (C2)의 코너 퇴화를 <b>가려 버렸다</b>.

        /// <summary>이 반각(라디안) 아래에서는 r = t/tan(h)가 발산하므로 직선 보간으로 폴백한다.
        /// 0.25°에서도 r은 유한하고(약 마디 길이의 230배) 그림은 직선과 구분되지 않는다.</summary>
        private const float MinHalfAngleRadians = 0.00436f; // 0.25°

        /// <summary>계층 탐색 이름 규약 — Core/StickmanMetrics, States/StickmanPoseAnimator와 동일하다
        /// (프리팹은 Editor/SceneBootstrapper.CreateLimb이 이 이름으로 굽는다).</summary>
        private static readonly string[] LimbNames = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };

        /// <summary>팔다리 하나에 필요한 캐시 전부. 매 프레임 재탐색/재할당 금지.</summary>
        private sealed class Limb
        {
            public Transform LowerTransform;   // 굽힘각의 유일한 출처(= 이 Transform의 localRotation.z).
            public LineRenderer UpperLine;
            public LineRenderer LowerLine;
            public float UpperLength;          // 관절에서 관절까지(로컬 유닛, 프리팹 굽기 시점 값).
            public float LowerLength;          // 관절에서 끝점까지.
            public Vector3[] UpperPoints;      // 재사용 버퍼(길이 PointsPerSegment).
            public Vector3[] LowerPoints;
            public float LastAngleDegrees;
            public float LastWidth;
            public bool Primed;                // 첫 프레임에는 무조건 한 번 굽는다.
        }

        private Limb[] _limbs;

        /// <summary>실측 진단용 — 이번 프레임에 실제로 다시 구운 마디 수(0이면 LineRenderer 쓰기 없음).</summary>
        public int LastRebuiltSegmentCount { get; private set; }

        /// <summary>계층 실측에 성공한 팔다리 수(정상은 4). 테스트/진단 전용.</summary>
        public int TrackedLimbCount => _limbs != null ? _limbs.Length : 0;

        private void Awake() => Initialize();

        private void LateUpdate() => Rebuild(force: false);

        /// <summary>
        /// 에디터에서 프리팹을 구울 때 호출한다(Awake/LateUpdate가 돌지 않는 시점).
        /// <b>런타임과 완전히 같은 코드 경로</b>를 쓰므로 프리팹에 저장된 곡선과 첫 프레임의 곡선이
        /// 구조적으로 어긋날 수 없다 — 같은 수식을 두 벌 적지 않는 것이 목적이다.
        /// </summary>
        public void BakeEditorPreview()
        {
            Initialize();
            Rebuild(force: true);
        }

        /// <summary>LateUpdate를 기다리지 않고 <b>지금</b> 한 번 갱신한다(LateUpdate와 완전히 같은 경로:
        /// 각도가 안 바뀐 마디는 그대로 건너뛴다). 순간이동/배율 변경처럼 같은 프레임 안에 그림이
        /// 맞아야 하는 호출부와 테스트가 쓴다.</summary>
        public void RefreshNow() => Rebuild(force: false);

        private void Initialize()
        {
            if (_limbs != null) return;

            var found = new System.Collections.Generic.List<Limb>(LimbNames.Length);
            for (int i = 0; i < LimbNames.Length; i++)
            {
                Limb limb = BuildLimb(LimbNames[i]);
                if (limb != null) found.Add(limb);
            }
            _limbs = found.ToArray();

            if (_limbs.Length != LimbNames.Length)
            {
                // 조용히 직선으로 남는 것이 크래시보다 낫지만, 원인 없이 "곡선이 안 나온다"로
                // 헤매지 않도록 흔적은 남긴다(테스트 리그/구버전 프리팹에서 정상적으로 발생 가능).
                Debug.LogWarning($"[곡선] 팔다리 {_limbs.Length}/{LimbNames.Length}개만 찾았습니다 — " +
                    "못 찾은 마디는 기존처럼 직선으로 그려집니다.");
            }
        }

        /// <summary>
        /// 이름으로 위 마디를 찾고 그 자식에서 "<i>이름</i>Lower"를 찾는다. 길이는 <b>구워진
        /// LineRenderer의 마지막 점 y</b>에서 읽는다 — 마디 길이의 원본 정의가 그 선이기 때문이다
        /// (BoxCollider2D.size.y도 같은 값이지만, 그쪽은 물리 형상이라 시각과 어긋날 여지가 있다).
        /// </summary>
        private Limb BuildLimb(string name)
        {
            Transform upper = null;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                // 루트 직속만 본다 — 다른 레이어가 같은 이름의 자손을 만들어도 영향받지 않는다
                // (StickmanPoseAnimator가 2026-08-30에 겪은 "UI가 Head라는 자식을 만들어 머리가
                //  영원히 안 움직인" 사고와 같은 계열의 예방).
                if (c != null && c.name == name) { upper = c; break; }
            }
            if (upper == null) return null;

            Transform lower = null;
            string lowerName = name + "Lower";
            for (int i = 0; i < upper.childCount; i++)
            {
                Transform c = upper.GetChild(i);
                if (c != null && c.name == lowerName) { lower = c; break; }
            }
            if (lower == null) return null;

            var upperLine = upper.GetComponent<LineRenderer>();
            var lowerLine = lower.GetComponent<LineRenderer>();
            if (upperLine == null || lowerLine == null) return null;

            float upperLength = ReadSegmentLength(upperLine);
            float lowerLength = ReadSegmentLength(lowerLine);
            if (upperLength <= 0.0001f || lowerLength <= 0.0001f) return null;

            return new Limb
            {
                LowerTransform = lower,
                UpperLine = upperLine,
                LowerLine = lowerLine,
                UpperLength = upperLength,
                LowerLength = lowerLength,
                UpperPoints = new Vector3[PointsPerSegment],
                LowerPoints = new Vector3[PointsPerSegment],
                LastAngleDegrees = float.NaN,
                LastWidth = float.NaN,
                Primed = false,
            };
        }

        /// <summary>
        /// 마디 길이 = 선의 <b>마디 끝점</b>의 |y|. 이미 곡선으로 구워져 있어도(재실행) 마디 끝은
        /// 같은 자리라 같은 값이 나온다 — 멱등이다.
        /// <para>★ 인덱스를 <see cref="PointsPerSegment"/>−1로 <b>자르는</b> 이유: 2026-09-01에
        /// 잠깐 들어갔다 빠진 "발"이 마디 끝 <i>뒤에</i> 점을 하나 더 붙였고, 그렇게 구워진 프리팹이
        /// 아직 남아 있을 수 있다. 자르지 않으면 그 발끝을 마디 끝으로 잘못 읽어 <b>다리가 최대
        /// 획의 절반만큼 길어진다</b>. 이 한 줄이 있으면 옛/새 프리팹 어느 쪽으로도 안전하다.</para>
        /// </summary>
        private static float ReadSegmentLength(LineRenderer lr)
        {
            int n = lr.positionCount;
            if (n < 2) return 0f;
            int endIndex = Mathf.Min(n - 1, PointsPerSegment - 1);
            return Mathf.Abs(lr.GetPosition(endIndex).y);
        }

        private void Rebuild(bool force)
        {
            LastRebuiltSegmentCount = 0;
            if (_limbs == null) return;

            for (int i = 0; i < _limbs.Length; i++)
            {
                Limb limb = _limbs[i];
                if (limb.LowerTransform == null || limb.UpperLine == null || limb.LowerLine == null) continue;

                float angle = SignedZDegrees(limb.LowerTransform.localRotation);
                float width = LocalStrokeWidth(limb);

                if (!force && limb.Primed
                    && Mathf.Abs(Mathf.DeltaAngle(limb.LastAngleDegrees, angle)) < RebuildEpsilonDegrees
                    && Mathf.Approximately(limb.LastWidth, width))
                {
                    continue;
                }

                BuildCurve(limb, angle, width);
                limb.LastAngleDegrees = angle;
                limb.LastWidth = width;
                limb.Primed = true;
                LastRebuiltSegmentCount += 2;
            }
        }

        /// <summary>
        /// ★ 획 두께를 <b>마디 로컬 단위</b>로 환산한다(2026-09-01 단위 불일치 수정,
        /// docs/CHARACTER_FORM_SPEC.md 4-3).
        ///
        /// <para><b>버그의 정체.</b> <see cref="LineRenderer.startWidth"/>는 <b>월드 유닛</b>이고
        /// Transform 스케일을 따라가지 않는다(Core/StickmanAgent.ApplyStrokeWidthsForScale이 배율마다
        /// 값을 직접 다시 대입하는 이유가 그것이다). 반면 마디 길이
        /// (<see cref="Limb.UpperLength"/>/<see cref="Limb.LowerLength"/>)는 프리팹 <b>로컬</b> 값이고,
        /// 캐릭터 배율은 루트 <c>localScale</c>로 들어온다. 그래서 예전 코드가
        /// <c>SolveFilletLength(로컬 길이, …, 월드 획)</c>을 부르던 순간부터 sagitta 캡이
        /// <b>서로 다른 단위 두 개를 비교</b>하고 있었다.</para>
        ///
        /// <para><b>증상.</b> 배율 0.35(루트 스케일 0.4667)에서 캡이 실제로 허용하는 sagitta가
        /// 의도 1.00 W의 <b>0.47배</b>라 필렛이 과하게 조여 곡률 반경 r이 작아졌고, 배율 1.00에서는
        /// 반대로 1.33 W까지 허용해 관절이 의도보다 33% 뭉툭했다. 배율 0.75(루트 스케일 1.0)에서만
        /// 우연히 정확했다 — <b>프리팹이 구워지는 그 배율이라 아무도 못 봤다</b>.</para>
        ///
        /// <para>lossyScale을 <b>아래 마디</b>에서 읽는 이유: 굽힘각과 두 마디 길이가 전부 그
        /// Transform의 로컬 공간에서 정의되기 때문이다. 0/NaN은 씬 구성 실수(스케일 0)일 뿐이므로
        /// 나누지 않고 1로 폴백한다 — 0으로 나눠 NaN 좌표를 LineRenderer에 넣는 것보다
        /// "곡선이 조금 뭉툭하다"가 낫다.</para>
        /// </summary>
        private static float LocalStrokeWidth(Limb limb)
        {
            float world = limb.LowerLine.startWidth;
            float scale = Mathf.Abs(limb.LowerTransform.lossyScale.y);
            if (scale <= 0.0001f || float.IsNaN(scale)) return world;
            return world / scale;
        }

        /// <summary>
        /// Z축 회전만 쓰는 리그에서 쿼터니언 → 부호 있는 각도(도). <c>localEulerAngles</c>는 내부
        /// 변환 비용이 있고 0~360으로 랩어라운드해 "−4°"가 "356°"로 튀는데, 이 값은 매 프레임
        /// 델타 비교에 쓰이므로 그 튐이 곧 불필요한 재굽기다.
        /// </summary>
        private static float SignedZDegrees(Quaternion q)
        {
            return 2f * Mathf.Atan2(q.z, q.w) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 클래스 문서의 두 수식을 그대로 채운다. <b>양 끝점(관절과 마디 끝)은 절대 움직이지 않는다</b> —
        /// 위 마디의 첫 점은 (0,0), 아래 마디의 마지막 점은 (0, −Ll)로 고정이다.
        /// </summary>
        private static void BuildCurve(Limb limb, float angleDegrees, float width)
        {
            float t = SolveFilletLength(limb.UpperLength, limb.LowerLength, angleDegrees, width);
            FillArcs(limb.UpperLength, limb.LowerLength, angleDegrees, t,
                limb.UpperPoints, 0, limb.LowerPoints, 0);
            Apply(limb, limb.UpperPoints, limb.LowerPoints);
        }

        // ========================================================================
        // ★ 공유 기하학 — 실제 캐릭터와 초상화가 <b>같은 이 두 함수</b>를 부른다
        //
        // 왜 컴포넌트를 그대로 재사용하지 못하는가: 이 클래스는 "위 마디 Transform + 그 자식인 아래 마디
        // Transform, 각자 LineRenderer 하나씩"이라는 <b>계층</b>에서만 동작한다. 초상화의 미니 피규어는
        // 마디 하나를 <b>납작한 폴리라인 한 줄</b>로 그리고 Transform이 없다(Interaction/
        // CharacterPortraitStage.DrawLimb). 그래서 <b>수식만</b> 아래 두 static으로 빼서 공유한다 —
        // 곡선을 두 번 구현하면 다음 라운드에 반드시 다시 갈라진다(이번 라운드가 고친 그 결함).
        // ========================================================================

        /// <summary>필렛 길이 t — (1) 짧은 쪽 마디 기준 비율, (2) 곡률 상한(sagitta ≤ W 배수)으로 자른다.
        ///
        /// <para>★ <b>단위 계약</b>: 세 길이 인자(<paramref name="upperLength"/>,
        /// <paramref name="lowerLength"/>, <paramref name="width"/>)는 <b>반드시 같은 단위</b>여야 한다.
        /// 이 함수는 단위를 모른다 — (2)의 캡이 길이와 두께를 직접 비교하기 때문이다.
        /// 호출부가 맞춰 넣어야 한다:
        /// <list type="bullet">
        /// <item>실제 캐릭터: 마디 길이가 로컬이므로 획도 로컬로 환산해 넘긴다
        ///       (<see cref="LocalStrokeWidth"/> — 2026-09-01에 여기서 버그가 났다).</item>
        /// <item>초상화/펫: 도형과 획을 같은 프레임에서 만들므로 그대로 넘기면 된다.</item>
        /// </list></para></summary>
        public static float SolveFilletLength(float upperLength, float lowerLength,
            float angleDegrees, float width)
        {
            float halfAngle = 0.5f * Mathf.Abs(angleDegrees) * Mathf.Deg2Rad;
            float t = FilletLengthRatio * Mathf.Min(upperLength, lowerLength);
            if (halfAngle > MinHalfAngleRadians && width > 0f)
            {
                float sagitta = t * Mathf.Tan(0.5f * halfAngle); // = t·tan(θ/4)
                float maxSagitta = MaxSagittaPerStrokeWidth * width;
                // sagitta > maxSagitta 이면 sagitta > 0 이므로 나눗셈이 안전하다(0으로 나눌 수 없다).
                if (sagitta > maxSagitta) t *= maxSagitta / sagitta;
            }
            return t;
        }

        /// <summary>두 반호를 각자의 마디 로컬 좌표계로 채운다. 오프셋을 받는 이유는 초상화/펫이
        /// <b>같은 배열의 앞뒤</b>에 담기 때문이다(버퍼를 두 개 잡지 않기 위해).
        /// <para>★ 두 구간은 관절점 한 칸을 <b>겹쳐 쓴다</b>(<see cref="PolylineJointIndex"/>). 아래 마디를
        /// 나중에 쓰므로 그 칸에는 아래 마디 좌표계의 값이 남고, 호출부가 그것을 위 마디 좌표계로
        /// 변환하면 위 마디가 쓴 값과 같은 자리로 간다. 겹치지 않게 담으면 길이 0인 선분이 생긴다.</para></summary>
        public static void FillArcs(float upperLength, float lowerLength, float angleDegrees, float filletLength,
            Vector3[] upper, int upperOffset, Vector3[] lower, int lowerOffset)
        {
            float halfAngle = 0.5f * Mathf.Abs(angleDegrees) * Mathf.Deg2Rad;
            float sign = angleDegrees >= 0f ? 1f : -1f;
            float t = filletLength;

            // 위 마디: [관절(0,0)] + 반호 표본(φ = 0 → h). 반호의 첫 점이 곧 직선 구간의 끝(0, −(Lu−t)).
            upper[upperOffset] = Vector3.zero;
            // 아래 마디: 반호 표본(φ = h → 0) + [마디 끝(0, −Ll)]. 반호의 마지막 점이 (0, −t).
            lower[lowerOffset + PointsPerSegment - 1] = new Vector3(0f, -lowerLength, 0f);

            float upperStraight = upperLength - t;

            if (halfAngle <= MinHalfAngleRadians)
            {
                // 사실상 곧게 편 상태 — r이 발산하므로 원호 대신 직선을 균등 분할한다.
                // (극한값이 이 직선과 일치한다: r·sin φ → t·φ/h, r·(1−cos φ) → 0.
                //  즉 아래 마디의 y는 −t + t·u 로 수렴한다 — 부호를 −t·u 로 적으면 점 순서가
                //  뒤집혀 마디 끝이 관절 위로 되접힌다. 실제로 한 번 그렇게 적어 수치 검증에서 잡혔다.)
                for (int k = 0; k < ArcSamplesPerHalf; k++)
                {
                    float u = k / (float)(ArcSamplesPerHalf - 1);
                    upper[upperOffset + 1 + k] = new Vector3(0f, -(upperStraight + t * u), 0f);
                    lower[lowerOffset + ArcSamplesPerHalf - 1 - k] = new Vector3(0f, -t + t * u, 0f);
                }
                return;
            }

            float radius = t / Mathf.Tan(halfAngle);

            for (int k = 0; k < ArcSamplesPerHalf; k++)
            {
                float phi = halfAngle * (k / (float)(ArcSamplesPerHalf - 1));
                float x = sign * radius * (1f - Mathf.Cos(phi));
                float y = radius * Mathf.Sin(phi);

                upper[upperOffset + 1 + k] = new Vector3(x, -upperStraight - y, 0f);
                // 아래 마디는 φ가 큰 쪽(관절)이 먼저 그려지므로 인덱스를 뒤집어 담는다.
                lower[lowerOffset + ArcSamplesPerHalf - 1 - k] = new Vector3(x, -t + y, 0f);
            }
        }

        /// <summary>
        /// 2분절 마디 하나를 <b>납작한 폴리라인 한 줄</b>로 굽는다 — Transform 계층이 없는 소비자
        /// (정보창 초상화의 미니 피규어)를 위한 진입점이다.
        ///
        /// <para>좌표계는 <b>위 마디 로컬</b>이다: 뿌리(고관절/어깨)가 원점이고 위 마디는 −Y 방향으로
        /// 뻗는다. 호출부는 벌림 각도만큼 돌려서 뿌리에 갖다 놓으면 된다.</para>
        ///
        /// <para>아래 마디의 점들은 여기서 위 마디 좌표계로 옮겨진다(무릎 = (0, −Lu)를 중심으로
        /// <paramref name="angleDegrees"/>만큼 회전). 실제 캐릭터에서는 <b>Transform 계층이</b>
        /// 정확히 같은 변환을 하므로, 두 경로가 만드는 그림은 같은 수식의 같은 결과다.</para>
        /// </summary>
        /// <returns>채워 넣은 점 개수(= <see cref="PolylinePointCount"/>). 버퍼가 모자라면 0.</returns>
        public static int BuildLimbPolyline(float upperLength, float lowerLength, float angleDegrees,
            float strokeWidth, Vector3[] destination)
        {
            const int needed = PolylinePointCount;
            if (destination == null || destination.Length < needed) return 0;

            float t = SolveFilletLength(upperLength, lowerLength, angleDegrees, strokeWidth);
            // ★ 아래 마디를 PolylineJointIndex에서 시작해 담는다 — 관절점이 위 마디의 마지막 점과
            //   같은 자리이므로 그 칸을 <b>겹쳐 쓴다</b>. 예전에는 PointsPerSegment에서 시작해
            //   같은 점을 두 번 담았고, 그 결과 길이 0인 선분이 하나 생겼다(4-5).
            FillArcs(upperLength, lowerLength, angleDegrees, t, destination, 0, destination, PolylineJointIndex);

            // 아래 마디 절반을 위 마디 좌표계로: p' = (0, −Lu) + Rz(θ)·p.
            // 관절점(PolylineJointIndex)도 함께 변환한다 — 아래 마디가 마지막에 쓴 값이고,
            // 변환하면 위 마디가 쓴 값과 정확히 같은 자리로 간다(클래스 문서의 접합 오차 ~1e−17).
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int i = PolylineJointIndex; i < needed; i++)
            {
                Vector3 p = destination[i];
                destination[i] = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos - upperLength, 0f);
            }
            return needed;
        }

        // ========================================================================
        // ★ 뿌리→끝점이 <b>고정</b>인 소비자를 위한 진입점 (2026-09-01 — 리틀스틱메이트)
        // ========================================================================
        //
        // <b>왜 오버로드가 필요한가.</b> 위의 BuildLimbPolyline은 "마디 길이 두 개와 굽힘각"을 받아
        // <b>끝점이 각도에 따라 움직이는</b> 그림을 만든다(사람 다리가 그렇다 — 무릎을 굽히면 발이
        // 올라온다). 실제 캐릭터는 그래도 되는데, 발이 물리 리그에 붙어 있어 그 이동이 곧 사실이기
        // 때문이다. <b>펫은 반대다</b>: 펫의 다리 끝은 항상 발바닥(y=0)이어야 하고
        // (AppearanceShapeBuilder.MiniHipRatio 문서 — 접지/무릎앉아 내림 거리가 그 점에 얹혀 있다),
        // 뿌리는 CharacterPetRenderer.MakeLine이 스윙 회전축으로 쓴다. 즉 <b>양 끝점이 계약</b>이다.
        //
        // 그래서 이 오버로드는 순서를 뒤집는다: 끝점을 먼저 고정하고 <b>마디 길이를 역산</b>한다.
        //   1) 굽힘각 θ에서 단위 마디(ru, rl)가 만드는 뿌리→끝 거리 d = √(ru² + rl² + 2·ru·rl·cos θ)
        //   2) 실제 현 길이 C에 맞추는 배율 k = C / d  →  Lu = k·ru, Ll = k·rl
        //   3) 그렇게 만든 폴리라인의 끝점 방향(β)과 실제 끝점 방향(γ)의 차이만큼 뿌리를 축으로 회전
        // 결과적으로 <b>양 끝점은 각도와 무관하게 정확히 그대로</b>이고, 바뀌는 것은 그 사이 모양뿐이다.
        // (같은 성질을 AppearanceShapeBuilder.Limb의 활도 갖고 있었다 — 그 계약을 그대로 물려받는다.)

        /// <summary>
        /// 뿌리와 끝점을 <b>정확히 고정한 채</b> 2분절 마디를 굽는다. 수식은 위
        /// <see cref="BuildLimbPolyline"/>과 <b>같은 하나</b>다(같은 <see cref="SolveFilletLength"/> /
        /// <see cref="FillArcs"/>를 부른다) — 펫과 주인이 "같은 시각 언어"를 쓴다는 말이 문자 그대로
        /// 같은 수식이라는 뜻이 되게 하기 위해서다.
        /// </summary>
        /// <param name="root">뿌리(고관절/어깨). <c>destination[0]</c>이 정확히 이 값이 된다.</param>
        /// <param name="tip">끝점(발끝/손끝). 마지막 점이 정확히 이 값이 된다.</param>
        /// <param name="bendDegrees">관절 굽힘각(도). 부호가 굽는 쪽(무릎은 뒤, 팔꿈치는 앞)이다.</param>
        /// <param name="upperFraction">위 마디가 가져가는 비율(0~1). 주인의 마디 비를 옮겨 쓴다.</param>
        /// <param name="strokeWidth">획 두께 — <b>좌표와 같은 단위</b>여야 한다(SolveFilletLength 단위 계약).</param>
        /// <returns>채워 넣은 점 개수(= <see cref="PolylinePointCount"/>). 버퍼가 모자라면 0.</returns>
        public static int BuildLimbPolylineBetween(Vector3 root, Vector3 tip, float bendDegrees,
            float upperFraction, float strokeWidth, Vector3[] destination)
        {
            const int needed = PolylinePointCount;
            if (destination == null || destination.Length < needed) return 0;

            float dx = tip.x - root.x, dy = tip.y - root.y;
            float chord = Mathf.Sqrt(dx * dx + dy * dy);
            if (chord < 1e-6f) return 0;

            float ru = Mathf.Clamp(upperFraction, 0.05f, 0.95f);
            float rl = 1f - ru;

            // (1)(2) 굽힌 상태의 끝점 거리가 현과 같아지도록 마디 길이를 역산한다.
            float rad = bendDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float bentSpan = Mathf.Sqrt(Mathf.Max(1e-12f, ru * ru + rl * rl + 2f * ru * rl * cos));
            float k = chord / bentSpan;
            float upperLength = k * ru;
            float lowerLength = k * rl;

            int count = BuildLimbPolyline(upperLength, lowerLength, bendDegrees, strokeWidth, destination);
            if (count <= 0) return 0;

            // (3) 끝점이 실제 끝점을 향하도록 뿌리를 축으로 돌린다. 각도 규약은 이 저장소의 다른
            //     마디 코드와 같다: 0 = 곧게 아래(−Y), + = +X 쪽(= CCW).
            Vector3 built = destination[count - 1];
            float beta = Mathf.Atan2(built.x, -built.y);
            float gamma = Mathf.Atan2(dx, -dy);
            float turn = gamma - beta;
            float tc = Mathf.Cos(turn), ts = Mathf.Sin(turn);

            for (int i = 0; i < count; i++)
            {
                Vector3 q = destination[i];
                destination[i] = new Vector3(root.x + q.x * tc - q.y * ts,
                                             root.y + q.x * ts + q.y * tc, 0f);
            }
            // 양 끝점은 계약이다 — 삼각함수 왕복으로 쌓인 부동소수 오차(~1e−7)까지 지운다.
            destination[0] = new Vector3(root.x, root.y, 0f);
            destination[count - 1] = new Vector3(tip.x, tip.y, 0f);
            return count;
        }

        // ========================================================================
        // ★ 안전 굽힘각 상한 — "규칙 B"를 프로덕션이 스스로 지키게 한다
        // ========================================================================
        //
        // 두께 W(반폭 ρ)인 폴리라인이 연속한 꼭짓점에서 <b>전부 같은 쪽으로</b> Δφ씩 꺾이면(원호 표본이
        // 정확히 그 경우다) 안쪽 오프셋 선이 양 끝에서 동시에 깎이므로, 각 선분이
        // <c>W·tan(Δφ/2)</c> 이상이어야 자기교차하지 않는다. 원호에서 선분 = 2r·sin(Δφ/2)이므로
        // 이 규칙은 <c>r ≥ ρ / cos(Δφ/2)</c>와 <b>같은 부등식</b>이다
        // (docs/CHARACTER_FORM_SPEC.md 4-1 규칙 B ⟺ 규칙 C를 1.7% 조인 것).
        //
        // 이 상한이 배율에 따라 <b>움직인다</b>는 것이 핵심이다: 획에는 화면상 하한(2pt)이 있어서
        // 배율을 내리면 마디만 짧아지고 획은 그대로다. 그래서 상한을 상수로 적으면 반드시 틀린다.

        /// <summary>이 굽힘각에서 관절 안쪽 윤곽이 자기교차하지 않는가(규칙 B).
        /// 길이 세 인자는 <see cref="SolveFilletLength"/>와 <b>같은 단위 계약</b>을 따른다.</summary>
        public static bool IsBendGeometrySafe(float upperLength, float lowerLength,
            float angleDegrees, float strokeWidth)
        {
            float half = 0.5f * Mathf.Abs(angleDegrees) * Mathf.Deg2Rad;
            if (half <= MinHalfAngleRadians || strokeWidth <= 0f) return true;   // 사실상 직선

            float t = SolveFilletLength(upperLength, lowerLength, angleDegrees, strokeWidth);
            float radius = t / Mathf.Tan(half);
            // 반호 하나를 (ArcSamplesPerHalf−1)개 변으로 나누므로 표본 하나의 꺾임각.
            float deltaPhi = half / (ArcSamplesPerHalf - 1);
            return radius * Mathf.Cos(0.5f * deltaPhi) >= 0.5f * strokeWidth;
        }

        /// <summary>
        /// <see cref="IsBendGeometrySafe"/>를 만족하는 <b>최대 굽힘각</b>(도). 이 마디 규격에서
        /// 안전하게 접을 수 있는 한계이며, 획/마디 길이 비에서 나오므로 <b>배율마다 달라진다</b>.
        ///
        /// <para>닫힌 해가 없어 이분 탐색한다(고정 24회 ≈ 1e−5도). <b>매 프레임 부르지 마라</b> —
        /// 도형을 다시 구울 때 한 번 구해 캐시하는 용도다
        /// (Interaction/CharacterPetRenderer.PrepareMiniLimbs가 그렇게 쓴다).</para>
        ///
        /// <para><see cref="IsBendGeometrySafe"/>는 각도에 대해 단조 감소한다(θ가 커지면
        /// r = t/tan(θ/2)도 cos(Δφ/2)도 함께 작아진다). 그래서 이분 탐색이 성립하고,
        /// 돌려주는 값은 <b>언제나 안전한 쪽</b>(lo)이다.</para>
        /// </summary>
        public static float MaxSafeBendDegrees(float upperLength, float lowerLength, float strokeWidth)
        {
            const float ceiling = 179f;
            if (IsBendGeometrySafe(upperLength, lowerLength, ceiling, strokeWidth)) return ceiling;

            float lo = 0f, hi = ceiling;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (IsBendGeometrySafe(upperLength, lowerLength, mid, strokeWidth)) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        private static void Apply(Limb limb, Vector3[] up, Vector3[] lo)
        {
            // positionCount는 항상 같은 값이라 두 번째 호출부터는 재할당이 일어나지 않는다.
            // ★ 발이 붙어 있던 프리팹(2026-09-01 21:20 구움)을 만나면 여기서 5개로 다시 줄어든다.
            if (limb.UpperLine.positionCount != PointsPerSegment) limb.UpperLine.positionCount = PointsPerSegment;
            if (limb.LowerLine.positionCount != PointsPerSegment) limb.LowerLine.positionCount = PointsPerSegment;
            limb.UpperLine.SetPositions(up);
            limb.LowerLine.SetPositions(lo);
        }
    }
}
