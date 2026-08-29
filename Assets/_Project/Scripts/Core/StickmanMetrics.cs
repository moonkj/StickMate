using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 캐릭터 실측 치수의 **단일 조회 경로**(2026-08-29 — 캐릭터 크기 배율 도입 라운드).
    ///
    /// ============================================================================
    /// 왜 필요한가
    /// ============================================================================
    /// 이 프로젝트의 시각 레이어들(말풍선/이모트/게이지/타이머 링 등)은 지금까지 "머리 위 y+2.6",
    /// "어깨 y+1.7" 같은 **절대 상수**로 앵커를 잡아왔다. 그 숫자들은 전부 배율 1.0 프리팹의 치수에서
    /// 손으로 옮겨 적은 것이라, StickConfig.characterScale로 캐릭터가 절반이 되는 순간 전부 틀린 값이
    /// 된다(말풍선이 머리에서 한참 위로 떠버린다). 그렇다고 각 렌더러가 각자 프리팹을 뒤지면 같은
    /// 계산이 열 벌 생기고 그 중 하나가 어긋나는 순간 조용히 깨진다 — 이 프로젝트가 이미 두 번 겪은
    /// 실패 유형이다(BUG-P1-R4-B1 씬 지면 Y 이중 정의, BUG-P1-R5-B2 Dock 구간 이중 계산).
    ///
    /// 그래서 "캐릭터가 지금 실제로 얼마나 큰가"를 묻는 창구를 여기 하나로 못박는다. 렌더러는
    /// 상수를 쓰지 말고 항상 <see cref="AboveHeadWorldY"/> / <see cref="ShoulderWorldY"/> /
    /// <see cref="HeightRatio"/> 같은 비율 API를 거쳐야 한다.
    ///
    /// ============================================================================
    /// 값의 출처 — 굽힌 상수가 아니라 **계층 실측**이다
    /// ============================================================================
    /// Awake()에서 자기 GameObject의 계층을 한 번 훑어 재고, 그 뒤로는 캐시된 로컬 치수 + 매 프레임의
    /// 루트 위치만 조합한다(24시간 상주 앱이라 매 프레임 bounds 합산 같은 것은 하지 않는다).
    ///   · 전신 높이   : 루트의 **비-트리거** CapsuleCollider2D.size.y
    ///                   (Editor/SceneBootstrapper.cs가 정확히 totalHeight로 굽는다. 잡기 영역
    ///                    GrabArea는 isTrigger라 여기서 제외된다 — 그쪽은 위아래 여백이 더 있다.)
    ///   · 머리 반경   : "Head/HeadOutline" 링 LineRenderer의 첫 점 x(= 반지름)
    ///   · 머리 중심 Y : 전신 높이 − 머리 반경 (프리팹이 totalHeight = headY + headRadius로 굽는다)
    ///   · 어깨 / 엉덩이 Y : "LeftArm" / "LeftLeg"의 HingeJoint2D.connectedAnchor.y (없으면 오른쪽)
    /// ★ localPosition을 읽지 않는 이유: StickmanPoseAnimator가 매 프레임 머리/팔다리의 localPosition을
    ///   덮어쓴다(몸 바운스 + 좌우 미러링). 위 세 소스는 포즈가 건드리지 않아 언제 재도 같은 값이다.
    /// 어느 하나라도 못 찾으면 배율 1.0 프리팹의 **비율**로 되메운다(테스트 리그/구버전 프리팹에서도
    /// 절대 0을 돌려주지 않는다 — 0은 "머리가 발밑에 있다"는 뜻이 되어 렌더러를 조용히 망가뜨린다).
    ///
    /// 루트 원점 = **발바닥**이라는 이 프로젝트의 규약(StickmanBlackboard.SenseGround 문서)을 그대로
    /// 따르므로, 모든 로컬 Y는 "발바닥에서 얼마나 위인가"이고 월드 Y는 거기에 루트 위치를 더한 값이다.
    ///
    /// ============================================================================
    /// 사용 예 (렌더러에서)
    /// ============================================================================
    /// <code>
    /// // 머리 꼭대기에서 키의 12%만큼 위 — 배율이 바뀌어도 같은 비율을 유지한다.
    /// float bubbleY = StickmanMetrics.AboveHeadWorldYOf(this, 0.12f);
    /// // 컴포넌트를 직접 들고 있는 편이 매 프레임 경로에서는 더 싸다.
    /// _metrics = StickmanMetrics.Find(this);
    /// float y = _metrics != null ? _metrics.AboveHeadWorldY(0.12f) : transform.position.y + 2.6f;
    /// </code>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StickmanMetrics : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────────────────
        // 배율 1.0 프리팹의 기준 비율 — 폴백 전용(실측에 성공하면 쓰이지 않는다).
        // 출처는 Editor/SceneBootstrapper.cs의 지오메트리 상수다:
        //   전신 2.2746944 / 머리중심 2.0546944 / 머리반경 0.22 / 어깨 1.7646944 / 엉덩이 0.9346944
        // ────────────────────────────────────────────────────────────────────────
        private const float BaselineHeadCenterRatio = 2.0546944f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHeadRadiusRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineShoulderRatio = 1.7646944f / StickConfig.BaselineCharacterTotalHeight;
        private const float BaselineHipRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        private Transform _root;
        private float _totalHeight;
        private float _headCenterLocalY;
        private float _headRadius;
        private float _shoulderLocalY;
        private float _hipLocalY;
        private bool _measured;

        // ────────────────────────────────────────────────────────────────────────
        // 로컬(발바닥 기준) 실측 치수
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>발바닥에서 정수리까지의 전신 높이(월드 유닛). 배율 1.0에서 약 2.275.</summary>
        public float TotalHeight { get { EnsureMeasured(); return _totalHeight; } }

        /// <summary>현재 크기 배율(실측 전신 높이 / 배율 1.0 기준 신장). StickConfig.characterScale을
        /// 읽는 것이 아니라 **실제로 구워진 프리팹**에서 역산하므로, 에셋 값과 프리팹이 어긋나 있으면
        /// 이쪽이 진실이다.</summary>
        public float Scale { get { EnsureMeasured(); return _totalHeight / StickConfig.BaselineCharacterTotalHeight; } }

        /// <summary>머리 링 중심의 로컬 Y(발바닥 기준).</summary>
        public float HeadCenterLocalY { get { EnsureMeasured(); return _headCenterLocalY; } }

        /// <summary>머리 링의 시각 반경(월드 유닛). 배율 1.0에서 0.22.</summary>
        public float HeadRadius { get { EnsureMeasured(); return _headRadius; } }

        /// <summary>정수리(머리 링 위쪽 끝)의 로컬 Y — 정의상 <see cref="TotalHeight"/>와 같다.
        /// 이름으로 의도를 드러내기 위해 따로 노출한다(호출부가 "키"와 "머리 꼭대기"를 헷갈리지 않게).</summary>
        public float HeadTopLocalY { get { EnsureMeasured(); return _totalHeight; } }

        /// <summary>어깨 관절 부착점의 로컬 Y(발바닥 기준). 배율 1.0에서 약 1.765.</summary>
        public float ShoulderLocalY { get { EnsureMeasured(); return _shoulderLocalY; } }

        /// <summary>고관절 부착점의 로컬 Y(발바닥 기준). 배율 1.0에서 약 0.935.</summary>
        public float HipLocalY { get { EnsureMeasured(); return _hipLocalY; } }

        /// <summary>계층 실측에 전부 성공했는지(폴백 비율을 하나도 쓰지 않았는지). 진단 로그 전용.</summary>
        public bool MeasuredFromHierarchy { get; private set; }

        // ────────────────────────────────────────────────────────────────────────
        // 월드 좌표 헬퍼 — 루트가 움직이므로 캐싱하지 말고 필요할 때마다 물어볼 것.
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>발바닥의 월드 Y(= 루트 원점).</summary>
        public float FootWorldY => (_root != null ? _root : transform).position.y;

        /// <summary>정수리의 월드 Y.</summary>
        public float HeadTopWorldY => FootWorldY + TotalHeight;

        /// <summary>머리 링 중심의 월드 Y.</summary>
        public float HeadCenterWorldY => FootWorldY + HeadCenterLocalY;

        /// <summary>어깨의 월드 Y.</summary>
        public float ShoulderWorldY => FootWorldY + ShoulderLocalY;

        /// <summary>고관절의 월드 Y.</summary>
        public float HipWorldY => FootWorldY + HipLocalY;

        /// <summary>머리 링 중심의 월드 좌표(x는 루트 x — RAGDOLL로 머리가 굴러가면 실제 머리와
        /// 달라지므로, 머리 Transform을 직접 따라가야 하는 렌더러는 그쪽을 앵커로 쓸 것).</summary>
        public Vector2 HeadCenterWorld
        {
            get
            {
                Transform t = _root != null ? _root : transform;
                return new Vector2(t.position.x, t.position.y + HeadCenterLocalY);
            }
        }

        /// <summary>키에 대한 비율을 월드 길이로 바꾼다(예: 0.12f -> 키의 12%). 렌더러가 오프셋을
        /// 절대 상수 대신 이 비율로 표현하면 배율이 바뀌어도 저절로 따라온다.</summary>
        public float HeightRatio(float ratio) => TotalHeight * ratio;

        /// <summary>정수리에서 키의 <paramref name="ratioAboveHead"/>배만큼 위의 월드 Y.
        /// 말풍선/이모트/아이콘처럼 "머리 위에 띄우는" 모든 것의 표준 앵커다.</summary>
        public float AboveHeadWorldY(float ratioAboveHead) => HeadTopWorldY + TotalHeight * ratioAboveHead;

        // ────────────────────────────────────────────────────────────────────────
        // 정적 조회 — 컴포넌트가 없어도 호출부가 절대 깨지지 않게 한다.
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="context"/>가 붙은 GameObject(없으면 부모 쪽)에서 이 컴포넌트를 찾는다.
        /// 프리팹이 구버전이거나 테스트 리그라 없으면 null — 호출부는 아래 *Of 헬퍼를 쓰면 null을
        /// 신경 쓰지 않아도 된다.
        /// </summary>
        public static StickmanMetrics Find(Component context)
        {
            if (context == null) return null;
            return context.GetComponentInParent<StickmanMetrics>(true);
        }

        /// <summary>전신 높이 조회(컴포넌트가 없으면 배율 1.0 기준 신장으로 폴백).</summary>
        public static float TotalHeightOf(Component context)
        {
            StickmanMetrics m = Find(context);
            return m != null ? m.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        }

        /// <summary>크기 배율 조회(컴포넌트가 없으면 1.0으로 폴백).</summary>
        public static float ScaleOf(Component context)
        {
            StickmanMetrics m = Find(context);
            return m != null ? m.Scale : 1f;
        }

        /// <summary>"머리 위 키의 n배" 월드 Y 조회. 컴포넌트가 없으면 호출부 Transform을 발바닥으로
        /// 보고 배율 1.0 치수로 계산한다(예전 절대 상수와 같은 결과).</summary>
        public static float AboveHeadWorldYOf(Component context, float ratioAboveHead)
        {
            StickmanMetrics m = Find(context);
            if (m != null) return m.AboveHeadWorldY(ratioAboveHead);
            float h = StickConfig.BaselineCharacterTotalHeight;
            float footY = context != null ? context.transform.position.y : 0f;
            return footY + h + h * ratioAboveHead;
        }

        // ────────────────────────────────────────────────────────────────────────
        // 실측
        // ────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _root = transform;
            Measure();
        }

        /// <summary>프리팹 계층을 런타임에 바꿨을 때(예: 에디터에서 크기 조정 실험) 다시 재게 한다.
        /// 읽는 소스가 전부 포즈 독립이라(위 클래스 문서 ★) 아무 시점에 불러도 같은 값이 나온다.</summary>
        public void Remeasure()
        {
            _measured = false;
            Measure();
        }

        private void EnsureMeasured()
        {
            if (!_measured) Measure();
        }

        private void Measure()
        {
            if (_root == null) _root = transform;
            _measured = true;
            MeasuredFromHierarchy = true;

            // 루트 Transform의 스케일 — SceneBootstrapper가 굽는 프리팹은 항상 1이지만, 누군가
            // 씬에서 루트를 스케일해 두면 콜라이더 size와 자식 localPosition은 그만큼 **월드에서**
            // 커진다. 아래 모든 로컬 치수에 곱해 "월드 유닛"이라는 이 클래스의 계약을 지킨다
            // (StickmanAgent.CharacterTotalHeightWorld가 위임 전에 쓰던 규약과 동일하다).
            float rootScaleY = Mathf.Abs(_root.lossyScale.y);
            if (rootScaleY <= 0.0001f) rootScaleY = 1f;

            // (1) 전신 높이 — 루트의 비-트리거 캡슐(GrabArea 트리거는 위아래 여백이 더 있어 제외한다).
            float height = 0f;
            var capsules = GetComponents<CapsuleCollider2D>();
            for (int i = 0; i < capsules.Length; i++)
            {
                CapsuleCollider2D c = capsules[i];
                if (c == null || c.isTrigger) continue;
                height = Mathf.Max(height, c.size.y);
            }
            height *= rootScaleY;
            if (height <= 0.0001f)
            {
                height = StickConfig.BaselineCharacterTotalHeight;
                MeasuredFromHierarchy = false;
            }
            _totalHeight = height;

            // (2) 머리 중심 / (3) 어깨 / (4) 엉덩이 — 이름으로 찾는다(StickmanPoseAnimator/EyeController와
            // 동일한 컨벤션: 계층 순회 순서에는 의미가 없으므로 이름만이 신뢰할 수 있는 식별자다).
            Transform head = null, leftArm = null, rightArm = null, leftLeg = null, rightLeg = null;
            int childCount = _root.childCount; // 루트 직속 자식만 본다(팔 아래마디 등은 손자라 자동 제외).
            for (int i = 0; i < childCount; i++)
            {
                Transform t = _root.GetChild(i);
                if (t == null) continue;
                switch (t.name)
                {
                    case "Head": head = t; break;
                    case "LeftArm": leftArm = t; break;
                    case "RightArm": rightArm = t; break;
                    case "LeftLeg": leftLeg = t; break;
                    case "RightLeg": rightLeg = t; break;
                }
            }
            // 좌우는 부착 높이가 같으므로(SceneBootstrapper가 같은 attachLocal을 넘긴다) 어느 쪽이든 된다.
            Transform arm = leftArm != null ? leftArm : rightArm;
            Transform leg = leftLeg != null ? leftLeg : rightLeg;

            // ★ 포즈에 흔들리지 않는 소스만 읽는다 — 이 클래스가 돌려주는 것은 "지금 자세의 크기"가
            // 아니라 "이 캐릭터의 규격"이기 때문이다. States/StickmanPoseAnimator.cs는 매 프레임
            // 머리/팔다리의 **localPosition을 직접 덮어쓴다**(몸 바운스 + 좌우 미러링). 그래서
            // localPosition을 읽으면 언제 재느냐에 따라 값이 달라진다. 대신:
            //   · 머리 반경 : "HeadOutline" 링 LineRenderer의 첫 점 x(= 반지름). 렌더러는 포즈가
            //                 건드리지 않는다(회전/이동은 부모 Transform이 받는다).
            //   · 머리 중심 : 전신 높이 − 머리 반경 (프리팹이 totalHeight = headY + headRadius로 굽는다).
            //   · 어깨/엉덩이 : HingeJoint2D.connectedAnchor — 프리팹이 구운 관절 부착점 그 자체이고
            //                 런타임에 아무도 바꾸지 않는다(StickmanPoseAnimator도 이것을 PivotLocal로
            //                 한 번 읽어 캐시할 뿐이다).
            _headRadius = ReadRingRadius(head) * rootScaleY;
            if (_headRadius <= 0.0001f)
            {
                _headRadius = _totalHeight * BaselineHeadRadiusRatio;
                MeasuredFromHierarchy = false;
            }
            _headCenterLocalY = _totalHeight - _headRadius;

            _shoulderLocalY = ReadJointAnchorY(arm) * rootScaleY;
            if (_shoulderLocalY <= 0.0001f)
            {
                _shoulderLocalY = _totalHeight * BaselineShoulderRatio;
                MeasuredFromHierarchy = false;
            }

            _hipLocalY = ReadJointAnchorY(leg) * rootScaleY;
            if (_hipLocalY <= 0.0001f)
            {
                _hipLocalY = _totalHeight * BaselineHipRatio;
                MeasuredFromHierarchy = false;
            }
        }

        /// <summary>머리 링("HeadOutline")의 반지름(로컬). 못 읽으면 0.
        /// SceneBootstrapper.CreateRing이 첫 점을 (반지름, 0, 0)에 찍으므로 그 x가 곧 반지름이다.</summary>
        private static float ReadRingRadius(Transform head)
        {
            if (head == null) return 0f;
            for (int i = 0; i < head.childCount; i++)
            {
                Transform c = head.GetChild(i);
                if (c == null || c.name != "HeadOutline") continue;
                var lr = c.GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount > 0) return Mathf.Abs(lr.GetPosition(0).x);
            }
            return 0f;
        }

        /// <summary>팔다리 위 마디의 관절 부착점 Y(부모=루트 로컬). 못 읽으면 0.</summary>
        private static float ReadJointAnchorY(Transform limbUpper)
        {
            if (limbUpper == null) return 0f;
            var joint = limbUpper.GetComponent<HingeJoint2D>();
            if (joint != null) return joint.connectedAnchor.y;
            return limbUpper.localPosition.y; // 관절이 없는 리그를 위한 최후 폴백.
        }
    }
}
