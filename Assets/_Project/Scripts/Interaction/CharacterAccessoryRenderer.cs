using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 장비 액세서리 시각 레이어 — 2026-08-29 사용자 요청("캐릭터 장비 착용").
    /// Core/EquipmentModel.cs에서 <b>착용 중인 슬롯만</b> 캐릭터와 완전히 같은 문법(LineRenderer 선화,
    /// 머티리얼은 캐릭터 것을 빌려 씀)으로 그린다.
    ///
    /// ============================================================================
    /// (1) 배율 연동 — 모든 치수는 <see cref="StickmanMetrics"/>에서 파생된다
    /// ============================================================================
    /// 이 프로젝트가 이미 여러 번 겪은 실패("로직은 도는데 그림이 몸에서 떨어져 나간다")를 반복하지
    /// 않기 위해, 이 파일에는 <b>월드유닛 절대 상수가 하나도 없다</b>. 모자 크기도 망토 길이도 전부
    /// 머리 반경(<see cref="StickmanMetrics.HeadRadius"/>) 또는 어깨~고관절 거리의 배수다. 그래서
    /// StickConfig.characterScale이 1.0 -> 0.75 -> 0.5로 바뀌어도 액세서리만 뒤에 남지 않는다
    /// (회귀 테스트: Tests/PlayMode/CharacterAccessoryScaleTests.cs가 세 배율을 절대 조건으로 잠근다).
    ///
    /// ============================================================================
    /// (2) 좌우 반전 — 비대칭 아이템은 반드시 진행 방향을 따라간다
    /// ============================================================================
    /// 이번 세션에 "비대칭 요소가 좌우 반전 시 안 뒤집혀 관절이 이상하게 꺾이는" 사고가 2번 있었다
    /// (무릎앉아 착지의 해부학적 제한, 활 든 손 방향). 여기서 비대칭인 것은 <b>모자 챙 / 안경다리 /
    /// 망토</b> 셋이다. 그래서 도형을 "진행 방향 기준 좌표계"(+x = 바라보는 쪽)로 정의하고 마지막에
    /// facing 부호를 x에만 곱한다 — States/EyeController.Mirror()와 정확히 같은 관례다
    /// (localScale.x = -1 뒤집기를 쓰지 않는 이유도 같다: 그러면 LineRenderer의 선 두께/캡까지
    ///  뒤집혀 미세하게 다른 그림이 되고, 자식 좌표계가 왼손계가 되어 이후 계산이 전부 헷갈린다).
    ///
    /// ============================================================================
    /// (3) 몸 바운스 추종 — Head 오프셋에서 역산한다
    /// ============================================================================
    /// States/StickmanPoseAnimator.SetBodyOffset()이 매 프레임 "Torso"와 "Head"의 localPosition.y를
    /// 중립에서 밀어 몸 전체를 오르내리게 한다. 액세서리가 이걸 따라가지 않으면 걸을 때마다 모자만
    /// 제자리에 떠 있게 된다. 중립을 새로 상수로 적으면 또 하나의 이중 정의가 되므로, <b>Awake에서
    /// 머리의 프리팹 중립 위치를 한 번 재어</b> 그 자리에서 바운스 오프셋을 역산한다(상수 추가 0개 —
    /// 포즈 애니메이터는 첫 Update에서야 생기므로 Awake 시점의 값이 곧 프리팹 중립이다).
    /// <para>★ 2026-09-01까지는 세로 중립을 <c>StickmanMetrics.HeadCenterLocalY</c> 항등식으로 대신했다.
    /// (3-2)의 회전이 들어오면 그 항등식의 어떤 편차든 sin(각도)만큼 가로로 새어 나오므로, 가정 대신
    /// 실측으로 바꿨다. 항등식은 이제 머리를 못 찾은 리그의 폴백으로만 남는다.</para>
    ///
    /// ============================================================================
    /// (3-1) 머리 좌우 추종 — 2026-08-30 사용자 신고("머리만 움직이고 모자는 가만히 있음")
    /// ============================================================================
    /// 유휴 앰비언트 "주위 살피기"(States/StickmanPoseAnimator.ApplyIdleAmbientPose)는 <b>머리만</b>
    /// 좌우로 민다(SetBodyOffset의 headOffsetX). (3)의 바운스 역산은 y만 봤기 때문에 머리가 옆으로
    /// 움직이는 동안 모자가 제자리에 남아 목이 어긋나 보였다. 컨테이너를 통째로 미는 것으로는
    /// 고칠 수 없다 — 넥타이/망토는 <b>어깨선</b>에서 유도되므로 함께 밀면 그쪽이 어긋난다.
    /// 그래서 머리에 붙는 자리(HEAD/EYES/HAIR)만 담는 자식 하나를 두고 거기에만 오프셋을 준다.
    ///
    /// ============================================================================
    /// (3-2) 몸통 <b>회전</b> 추종 — 2026-09-01 상체 기울임(SetBodyLean) 라운드
    /// ============================================================================
    /// (3)/(3-1)은 <b>위치</b>만 따라갔다. 그런데 States/StickmanPoseAnimator.SetBodyLean이 도입되면서
    /// 몸통·머리·어깨가 <b>엉덩이를 축으로 회전</b>하게 됐다 — 위치만 따라가면 모자가 완벽히 수평으로
    /// 뜬 채 그 아래 머리만 기울어지는 그림이 된다(리더가 착수 전에 지목한 교차 레이어 결함).
    ///
    /// 고치는 방법은 하나뿐이다: <b>컨테이너를 같은 피벗(엉덩이)으로 같은 각도만큼 돌린다.</b>
    /// 그러면 모자(머리 부착)·망토·넥타이(어깨 유도)가 전부 한 번에 따라온다 — 도형 32종을 한 줄도
    /// 고치지 않는다. 각도는 <b>새로 계산하지 않고</b> Torso Transform의 localRotation을 그대로 읽는다:
    /// 그것이 포즈가 실제로 적용한 값이고, 같은 값을 두 곳에서 계산하면 언젠가 한쪽만 바뀐다
    /// (이 프로젝트가 여러 번 겪은 실패 유형 — 클래스 문서 (3)의 "상수 추가 0개"와 같은 원칙).
    ///
    /// 회전이 붙으면서 (3)/(3-1)의 역산도 함께 갱신됐다: 머리의 현재 localPosition에서 빼야 할 중립은
    /// 이제 "프리팹 중립"이 아니라 <b>"기울임이 적용된 중립"</b>(= 엉덩이 피벗 회전 뒤의 위치)이다.
    /// 그렇게 하지 않으면 기울임이 만든 머리 이동을 바운스/좌우 오프셋으로 오해해 <b>두 번</b> 적용한다.
    ///
    /// ============================================================================
    /// (4) 랙돌 중에는 <b>숨긴다</b> — 그리고 그 이유
    /// ============================================================================
    /// RAGDOLL/ThrowTumble은 전신을 물리에 위임하는 상태라 머리·몸통이 루트에서 독립적으로 굴러간다.
    /// 이 렌더러는 루트 로컬 좌표로 그리므로, 그대로 두면 모자와 망토만 캐릭터가 원래 서 있던 자리에
    /// 남아 <b>몸에서 분리되어</b> 보인다. 랙돌 각 부위를 따라가게 만들려면 부위별 앵커 추적이
    /// 필요한데, 그건 이 라운드의 예산을 넘고(랙돌은 몇 초짜리 과도 상태다) 무엇보다 "머리에서 벗겨진
    /// 모자"는 물리적으로 그럴듯하지도 않다(모자는 관성으로 날아가야 자연스럽지, 몸을 따라 붙어
    /// 다니면 그것대로 어색하다). 그래서 <b>가장 단순하고 정직한 선택</b>인 "랙돌 동안 숨김"을 택했다.
    /// 페이드로 사라졌다 나타나므로 깜빡임처럼 보이지 않는다(리더가 명시적으로 허용한 선택지).
    ///
    /// ============================================================================
    /// 복제 방어
    /// ============================================================================
    /// 다른 렌더러들과 같은 이유로 자기 GameObject의 StickmanAgent가 없으면 아무것도 하지 않는다.
    /// 이 프리팹의 사본이 씬에 생기면 그 사본에는 이 컴포넌트를 배치하지 않는 것이 1차 방어다
    /// (장비/성장은 플레이어 하나에만 붙는 전역 상태다).
    /// </summary>
    public sealed class CharacterAccessoryRenderer : MonoBehaviour, ICharacterInkExtentProvider,
        ICharacterVisualSource
    {
        // ==================== 비율 상수 (전부 머리 반경 / 몸통 길이 배수) ====================
        // 월드유닛 절대값은 하나도 없다 — 클래스 문서 (1) 참고.
        //
        // ★ 2026-08-30(R2 m2 갱신): 도형 비율은 이제 이 파일에 없다 — 전부
        //   Interaction/AccessoryShapeBuilder.cs로 이관했고, 정보창 초상화를 그리는
        //   Interaction/CharacterPortraitStage.cs가 바로 그 비율을 읽어 미니어처를 만든다.
        //   챙 길이 같은 튜닝값은 AccessoryShapeBuilder 한 곳만 고치면 몸과 초상화가 함께 따라온다.

        // ★ 2026-08-30: 단일 sortingOrder 상수를 폐기했다. 33-2-0의 레이어 재배치표(망토 2 / 머리 6 /
        //   넥타이 7 / 안경 8 / 모자 9)를 표현하려면 선마다 값이 달라야 하고, 그 값은 도형이
        //   스스로 선언한다(AccessoryShapeBuilder.Shape.SortingOrder). 기본값은 AddLine의 기본 인자에
        //   AccessoryShapeBuilder.SortDefault(=6)로 남아 있어 이 인자를 넘기지 않는 호출부는 무변경이다.
        private const float FadeSeconds = 0.18f; // 랙돌 진입/복귀 시 깜빡임을 없애는 짧은 페이드.

        // ★ 2026-08-30: 액세서리 도형 비율/점 좌표는 전부 Interaction/AccessoryShapeBuilder.cs로
        //   옮겼다. 정보창 초상화(CharacterPortraitStage)가 같은 모자·망토를 한 벌 더 그리게 되면서,
        //   도형 정의가 두 곳에 있으면 "망토를 고쳤는데 초상화만 옛 모양"이 되기 때문이다.
        //   이 파일은 이제 "언제/어디에 그릴지"만 책임진다.

        // 선 두께 — 캐릭터 획과 같은 문법을 유지하기 위해 전신 높이 비율로 잡는다.
        // 분자 0.048은 StressGaugeRenderer가 이미 쓰는 검증된 획 두께다(같은 그림체를 유지).
        // ★ 2026-09-01 — 0.048은 여기·초상화·획 예산 세 곳에 각자 적혀 있었다(한 곳만 고쳐지는 사고의
        //   전형). AccessoryShapeBuilder를 단일 정의처로 삼고 값은 그대로 둔다(거동 변화 0).
        private const float StrokeWidthRatio = AccessoryShapeBuilder.StrokeWidthRatio;

        // HemSway(33-2-5 (A)) — 33절이 준 값 그대로. 진폭은 머리 반경 배수이므로 배율을 자동으로 따라간다.
        private const float SwayPeriodSeconds = 0.62f;
        private const float SwayAmplitudeRatio = 0.16f;
        private const float SwayPointPhaseStep = 0.9f;  // 점마다 위상을 어긋내 천이 접히는 것처럼 보이게 한다.
        private const float SwayBackRatio = 0.7f;
        private const float SwayLiftRatio = 0.4f;

        /// <summary>이 아래의 기류는 "없다"로 본다(2026-08-31). 포물선 정점처럼 속도가 0을 스치는
        /// 구간에서 진폭이 0에 수렴하므로 값 자체는 무엇이든 화면에서 같지만, 이 하한이 있어야
        /// <b>SetPositions/메시 갱신 호출을 건너뛰는</b> 정지 경로로 되돌아간다(24시간 상주 앱).</summary>
        private const float AirFlowEpsilon = 0.0001f;

        /// <summary>
        /// ★ 2026-09-01 (디버거) — 기류가 <b>사라질 때</b> 천이 잦아드는 데 걸리는 시간(초).
        ///
        /// <para><b>새 숫자가 아니다</b> — <see cref="SwayPeriodSeconds"/>(이 파일이 이미 "천이 한 번
        /// 흔들리는 시간"으로 쓰는 값)를 그대로 재사용한다. 천 한 장이 흐트러졌다가 제자리를 찾는 데
        /// 걸리는 시간은 그 천의 흔들림 주기와 같은 크기라는 것이 유일한 근거이며, 여기에 별도의
        /// 튜닝 스칼라를 만들면 "걷는 망토"와 "잦아드는 망토"가 서로 다른 시간축을 갖게 된다.</para>
        ///
        /// <para><b>왜 지수감쇠가 아니라 <see cref="Mathf.MoveTowards"/>인가</b>: 지수감쇠는 <b>영원히
        /// 0에 닿지 않는다</b>. 그러면 "가만히 서 있으면 망토는 정적이다"(Tests/PlayMode/
        /// CapeFallFlutterTests)가 성립하지 않고, 24시간 상주 앱이 영구히 매 프레임 SetPositions +
        /// 메시 갱신을 돌게 된다. 선형 감쇠는 유한 시간에 <b>정확히 0</b>이 되어 그 두 성질을 모두
        /// 지킨다.</para>
        /// </summary>
        private const float AirFlowSettleSeconds = SwayPeriodSeconds;

        /// <summary>요일 확인 주기(초). 33-2-5 (D) 줄무늬 타이가 자정을 넘겼는지만 알면 되므로 넉넉하다.</summary>
        private const float DayPollSeconds = 60f;

        private StickmanAgent _agent;
        private StickmanMetrics _metrics;
        private Transform _headTransform;

        /// <summary>몸통 Transform — 상체 기울임(클래스 문서 (3-2))의 <b>단일 진실</b>. 각도를 여기서
        /// 읽기만 하고 다시 계산하지 않는다. 없으면(구버전 리그/테스트 스텁) 회전은 identity다.</summary>
        private Transform _torsoTransform;

        private LineRenderer _headOutline;   // 몸이 지금 보이는지 판정하는 기준(ResolveWantVisible 참고).
        private Material _lineMaterial;

        private GameObject _container;

        /// <summary>모자/안경/머리카락만 담는 자식. 머리의 좌우 오프셋을 여기에만 준다.</summary>
        private Transform _headGroup;

        /// <summary>머리의 <b>중립</b> 로컬 위치(루트 로컬 단위). 프리팹이 굽어 있는 값이므로 Awake에서
        /// 한 번만 잰다(포즈 애니메이터는 첫 Update에서야 생성되므로 이 시점의 값이 곧 프리팹 중립이다).
        ///
        /// <para>★ 2026-09-01 — 예전에는 x만 재고 y는 <c>HeadCenterLocalY / RootScale</c> <b>항등식</b>으로
        /// 대신했다. 기울임이 없을 때는 세로 편차가 상수로만 남아 보이지 않지만, 기울임이 들어오면
        /// 그 편차가 <b>sin(각도)만큼 가로로 새어 나온다</b>(모자가 머리에서 미끄러진다). 중립을
        /// <b>직접 재면</b> 그 편차가 정의상 0이고, RootScale 나눗셈이 개입하는 경로도 함께 사라진다.
        /// <br/>실측(Tests/PlayMode/BodyLeanAccessoryFollowTests, 기울임 20도): 모자의 머리 로컬 이탈
        /// <b>0.0037 → 0.0018유닛</b>. <i>정직한 단서</i> — 프리팹 유도상 항등식은 루트 스케일 1에서
        /// 정확해야 하므로(SceneBootstrapper: totalHeight = headY + headRadius), 남은 0.0018과 줄어든
        /// 0.0019의 원인을 항등식 하나로 단정하지 않는다. 두 값 모두 머리 반경의 2% 미만이라 육안
        /// 판별 범위 밖이고, 이 교체의 근거는 실측 개선이 아니라 <b>가정을 실측으로 바꾼 것</b>이다.</para></summary>
        private Vector2 _headNeutralLocal;
        private bool _hasHeadNeutral;

        private readonly List<LineRenderer> _lines = new List<LineRenderer>(8);

        /// <summary>채움 면(모자류). 알파/표시 토글은 선과 같은 규칙을 따른다.</summary>
        private readonly List<MeshRenderer> _fills = new List<MeshRenderer>(4);

        /// <summary>직접 만든 메시. <b>반드시 손으로 지운다</b> — GameObject를 Destroy해도 메시는
        /// 남아서 24시간 상주 앱에서 재구성마다 조금씩 샌다.</summary>
        private readonly List<Mesh> _fillMeshes = new List<Mesh>(4);

        /// <summary>재구성 때만 쓰는 도형 조립 버퍼. 매번 새 List를 만들지 않는다(24시간 상주 앱).</summary>
        private readonly List<AccessoryShapeBuilder.Shape> _shapes = new List<AccessoryShapeBuilder.Shape>(16);

        /// <summary>HemSway 대상 선 하나. 원본 점과 작업 버퍼를 함께 들고 있어야
        /// 매 프레임 배열을 새로 만들지 않고 "원본 + 오프셋"을 계산할 수 있다.</summary>
        private sealed class SwayLine
        {
            public LineRenderer Line;

            /// <summary>★ 2026-08-31 — 이 선이 두르고 있는 <b>채움 면</b>의 메시(없으면 null).
            /// <para>이 필드가 없던 것이 사용자 신고 "망토가 고정되어있음"의 절반이었다. 흔들림은
            /// LineRenderer의 점만 옮기고 채움 메시는 재구성 전까지 정적이었으므로, 화면에서 천으로
            /// 보이는 면은 <b>한 번도 움직인 적이 없었다</b>. 테두리만 미끄러지는 그림이라 사용자가
            /// "고정"이라고 말한 것이 정확했다.</para></summary>
            public Mesh Fill;

            public Vector3[] Base;
            public Vector3[] Buffer;
            public int Start;
            public int Count;
        }

        private readonly List<SwayLine> _swayLines = new List<SwayLine>(4);
        private bool _swayApplied;

        // ★ 2026-09-01 (디버거) — 천의 <b>관성</b>. 아래 TickAirFlowInertia() 문서 참고.
        /// <summary>지금 천에 실제로 걸려 있는 기류 세기(0~1). 목표값(ResolveAirFlow01)을 바로 쓰지
        /// 않는다 — 착지 프레임에 목표가 1에서 0으로 뚝 떨어지면 천이 한 프레임에 순간이동한다.</summary>
        private float _airFlow01;

        /// <summary>그 기류가 부는 방향(로컬 단위벡터). 공중이 끝난 뒤 잦아드는 동안에는
        /// <b>마지막 공중 프레임의 방향</b>을 유지한다 — 천은 방향을 잃는 것이 아니라 세기를 잃는다.</summary>
        private Vector2 _airWindLocal;

        /// <summary>관성을 마지막으로 갱신한 프레임. 액세서리가 숨겨져 있던(= LateUpdate가 조기
        /// 반환한) 동안에는 갱신이 끊기므로, 끊긴 뒤 첫 프레임에는 관성 없이 목표로 바로 맞춘다.
        /// 그렇게 하지 않으면 랙돌로 몇 초간 숨었다 돌아온 천이 <b>몇 초 전의 기류</b>로 펄럭인다.</summary>
        private int _airFlowFrame = -1;

        private int _cachedDayOfWeek = -1;
        private float _dayCheckedAt = float.NegativeInfinity;

        private float _facingSign = 1f;
        private float _alpha;
        private bool _built;
        private int _builtSignature = -1;

        // ==================== 실측 검증용 공개 프로퍼티(테스트가 읽는다) ====================
        // RendererScaleRatioTests가 쓰는 것과 같은 관례 — 렌더러가 "지금 어디에 그리려 하는가"를
        // 계산 그대로 노출해, 테스트가 도형을 실제로 만들지 않고도 배치를 단언할 수 있게 한다.

        // 폴백 비율 — StickmanMetrics가 없는 리그(구버전 프리팹/테스트 스텁)에서도 0을 돌려주지 않게.
        // 분자는 배율 1.0 프리팹의 실측치 그대로다(StickmanMetrics의 폴백 상수와 같은 출처).
        private const float FallbackHeadRadiusRatio = 0.22f / StickConfig.BaselineCharacterTotalHeight;
        private const float FallbackShoulderRatio = 1.7646944f / StickConfig.BaselineCharacterTotalHeight;
        private const float FallbackHipRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        private float R => _metrics != null ? _metrics.HeadRadius
            : StickConfig.BaselineCharacterTotalHeight * FallbackHeadRadiusRatio;
        private float HeadCenterY => _metrics != null ? _metrics.HeadCenterLocalY
            : StickConfig.BaselineCharacterTotalHeight - R;
        private float ShoulderY => _metrics != null ? _metrics.ShoulderLocalY
            : StickConfig.BaselineCharacterTotalHeight * FallbackShoulderRatio;
        private float HipY => _metrics != null ? _metrics.HipLocalY
            : StickConfig.BaselineCharacterTotalHeight * FallbackHipRatio;

        /// <summary>획의 <b>비례 두께</b>(월드 유닛). 도형 유도(AccessoryShapeBuilder.Append의 획 반폭
        /// 인자, 모자 커버선)는 이 값을 쓴다 — 배율에 정확히 비례해야 낮은 배율에서 모양이 달라지지
        /// 않는다(Tests/PlayMode/CharacterAccessoryScaleTests가 이 비례를 잠근다).</summary>
        public float StrokeWidth => (_metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight) * StrokeWidthRatio;

        /// <summary>
        /// ★ 실제로 <b>그려지는</b> 두께 — 화면상 최소 두께(<see cref="StickConfig.MinStrokeScreenPoints"/>
        /// = 2pt) 아래로 내려가지 않는다(2026-08-31).
        ///
        /// <para>하한이 빠져 있었다. 순수 비례라 <b>출하 기본 배율 0.75에서도 1.47pt로 미달</b>이었고,
        /// 다이얼 최소값 0.35에서는 0.69pt — 몸 획(4.09pt)의 <b>1/6</b>이었다. 왕관 지그재그·방울·
        /// 외알안경 체인·배낭 끈처럼 얇은 도형이 그 배율에서 안티에일리어싱에 묻혔다.</para>
        ///
        /// <para>왜 <see cref="StrokeWidth"/>와 나누는가: 몸이 쓰는 규칙과 같게 하기 위해서다 —
        /// Core/StickmanAgent.ApplyStrokeWidthsForScale도 <b>구워진 도형은 그대로 두고 LineRenderer
        /// 두께만</b> 하한으로 올린다. 도형 좌표까지 하한을 태우면 낮은 배율에서 모자 커버선이 함께
        /// 움직여 실루엣이 달라진다.</para>
        ///
        /// <para>하한 값 자체는 <see cref="StickmanAgent.MinStrokeWorldWidth"/>가 단일 소스다
        /// (여기 다시 적으면 몸과 액세서리의 하한이 어긋난다).</para>
        /// </summary>
        private float RenderStrokeWidth => Mathf.Max(StrokeWidth, MinStrokeWorld);

        /// <summary>
        /// ★★ <b>채운 도형의 경계선</b>이 실제로 그려지는 두께(2026-09-02 M6,
        /// docs/CHARACTER_FORM_SPEC.md 19절). 위 <see cref="RenderStrokeWidth"/>와 <b>같은 비례
        /// 두께</b>를 쓰되 하한만 절반(<see cref="StickConfig.MinFillOutlineScreenPoints"/> = 1.00pt)이다.
        ///
        /// <para><b>왜 이 선만 다른가</b>: 채움 경계선은 폭 W 펜이 경계에 중심을 두므로 도형이
        /// 안쪽으로 정확히 W/2를 잃는다 — 굵힐수록 <b>자기 색면을 먹는다</b>. 반면 낱선 20개는
        /// 그 선이 그 자리의 유일한 잉크라 얇아지면 요소가 사라진다. 두 집합은 겹치지 않으므로
        /// 각각 다른 하한을 걸면 <b>양쪽을 동시에</b> 얻는다(교환이 아니다).
        /// 배율 0.60에서 색면 생존율 중앙값 22.0% → <b>47.7%</b>, 색면 폭 ≥ 1획인 도형 25/61 → 57/61.</para>
        ///
        /// <para>배율 0.509 이상에서는 이 하한이 <b>아예 물리지 않는다</b> — 사용자 배율 0.60,
        /// 출하 0.75, 다이얼 최대 1.00 전부 순수 비례가 된다.</para>
        /// </summary>
        private float RenderFillOutlineStrokeWidth => Mathf.Max(StrokeWidth, MinFillOutlineWorld);

        /// <summary>이 렌더러가 쓰는 화면상 최소 두께(월드). 에이전트가 없는 사본/스텁에서는
        /// StickConfig의 근사 환산으로 되메운다 — 0을 흘리면 하한이 조용히 사라진다.</summary>
        private float MinStrokeWorld => _agent != null
            ? _agent.MinStrokeWorldWidth
            : StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        /// <summary>채움 경계선 하한(월드). 값의 단일 소스는 <see cref="StickmanAgent.MinFillOutlineWorldWidth"/>다.</summary>
        private float MinFillOutlineWorld => _agent != null
            ? _agent.MinFillOutlineWorldWidth
            : StickConfig.MinFillOutlineScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        /// <summary>지금 치수/방향으로 만든 도형 리그 — 이 렌더러와 초상화가 같은 값을 쓴다.</summary>
        internal AccessoryShapeBuilder.Rig BuildRig()
            => new AccessoryShapeBuilder.Rig(R, HeadCenterY, ShoulderY, HipY, _facingSign);

        // ★ 아래 6개 프로퍼티는 <b>확장 전 기본 아이템 4종</b>(천 모자 / 선글라스 / 나비넥타이 /
        //   짧은 망토)의 기준선이다. 32종으로 늘어난 지금은 "지금 쓴 아이템"이 아니라 "그 카테고리의
        //   0번 아이템"을 말한다 — 이름을 바꾸지 않은 이유는 CharacterAccessoryScaleTests가 이 값들로
        //   배율 1.0/0.75/0.5를 이미 잠그고 있어서다. 신규 12종은 아이템 자리를 인자로 받는
        //   TryMeasureItemBounds()로 측정한다(프로퍼티를 32개 늘어놓지 않는 이유는 그 함수 문서 참고).

        /// <summary>천 모자(0번) 챙 선의 로컬 Y(발바닥 기준).</summary>
        public float HatBrimLocalY => AccessoryShapeBuilder.HatBrimLocalY(BuildRig());

        /// <summary>천 모자(0번) 관(crown) 꼭대기의 로컬 Y.</summary>
        public float HatTopLocalY => AccessoryShapeBuilder.HatTopLocalY(BuildRig());

        /// <summary>천 모자(0번) 챙 끝의 로컬 X — <b>부호가 곧 바라보는 방향</b>이다(좌우 반전 회귀 테스트용).</summary>
        public float HatBrimTipLocalX => _facingSign * R * AccessoryShapeBuilder.HatBrimReachRatio;

        /// <summary>선글라스(0번) 렌즈 중심의 로컬 Y. 안경 4종이 전부 이 기준선을 공유한다.</summary>
        public float GlassesLocalY => AccessoryShapeBuilder.GlassesLocalY(BuildRig());

        /// <summary>안경다리 끝의 로컬 X — 진행 <b>반대쪽</b>이므로 부호가 챙과 반대여야 한다.</summary>
        public float GlassesTempleTipLocalX => -_facingSign * R * AccessoryShapeBuilder.GlassesTempleReachRatio;

        /// <summary>나비넥타이(0번) 중심의 로컬 Y. 넥타이 4종이 전부 이 기준선을 공유한다.</summary>
        public float BowTieLocalY => AccessoryShapeBuilder.BowTieLocalY(BuildRig());

        /// <summary>망토 옷깃(어깨)의 로컬 Y.</summary>
        public float CapeCollarLocalY => AccessoryShapeBuilder.CapeCollarLocalY(BuildRig());

        /// <summary>짧은 망토(0번) 밑단의 로컬 Y.</summary>
        public float CapeHemLocalY => AccessoryShapeBuilder.CapeHemLocalY(BuildRig());

        /// <summary>망토 자락이 가장 멀리 뻗은 로컬 X — 진행 <b>반대쪽</b>(뒤로 흩날린다).</summary>
        public float CapeTrailTipLocalX => -_facingSign * R * AccessoryShapeBuilder.CapeSpreadRatio;

        /// <summary>어깨~고관절 거리(몸통 길이).</summary>
        public float TorsoLength => Mathf.Max(0.0001f, ShoulderY - HipY);

        /// <summary>지금 바라보는 방향 부호(테스트/진단 전용).</summary>
        public float FacingSign => _facingSign;

        /// <summary>테스트 전용 — 씬의 실제 캐릭터 없이도 배치 계산을 검증할 수 있게 한다
        /// (RendererScaleRatioTests가 렌더러를 최소 리그에 붙여 프로퍼티만 읽는 것과 같은 방식).</summary>
        public void SetFacingForTests(float sign) => _facingSign = sign >= 0f ? 1f : -1f;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _metrics = StickmanMetrics.Find(this);
            _torsoTransform = FindDirectChild("Torso");
            _headTransform = FindDirectChild("Head");
            if (_headTransform != null)
            {
                _headNeutralLocal = _headTransform.localPosition;
                _hasHeadNeutral = true;
                for (int i = 0; i < _headTransform.childCount; i++)
                {
                    Transform c = _headTransform.GetChild(i);
                    if (c != null && c.name == StickmanMetrics.HeadRingObjectName) _headOutline = c.GetComponent<LineRenderer>();
                }
            }
        }

        private void OnEnable()
        {
            StickmanEventBus.CharacterEquipmentChanged += OnEquipmentChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.CharacterEquipmentChanged -= OnEquipmentChanged;
        }

        private void OnDestroy()
        {
            if (_container != null) Destroy(_container);
            DestroyFillMeshes();
        }

        private void DestroyFillMeshes()
        {
            for (int i = 0; i < _fillMeshes.Count; i++)
            {
                if (_fillMeshes[i] != null) Destroy(_fillMeshes[i]);
            }
            _fillMeshes.Clear();
        }

        private void OnEquipmentChanged()
        {
            _builtSignature = -1; // 다음 LateUpdate에서 재구성.
        }

        /// <summary>
        /// LateUpdate인 이유: StickmanPoseAnimator가 Update에서 머리/몸통 오프셋을 쓰므로, 그 뒤에
        /// 읽어야 같은 프레임의 값을 따라간다(한 프레임 늦으면 빠르게 걸을 때 모자가 머리에서 떤다).
        /// </summary>
        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Accessory);   // [스톨구간] 계측
            if (_agent == null) return; // 복제본 방어(클래스 문서).

            bool wantVisible = ResolveWantVisible();

            // ★★ 2026-08-31 (원칙 2) — <b>몸이 이미 사라졌다면 페이드는 금지</b>다.
            // 페이드(FadeSeconds)의 목적은 랙돌 진입/복귀에서 모자가 깜빡이지 않게 하는 것이고, 그
            // 근거는 "몸은 그대로 있는데 액세서리만 순간적으로 사라진다"에만 성립한다. 전체화면 감지와
            // 가출 은신은 <b>몸이 그 프레임에 통째로 없어지는</b> 경우라, 여기서 0.18초를 더 끌면
            // 사용자가 방금 켠 전체화면 게임 위에 "몸 없는 모자·망토"가 남는다(실측으로 확인된 원칙 2
            // 위반). StickmanAgent.SetRenderersEnabled(false)가 같은 프레임에 우리 선도 끄지만,
            // 그것만으로는 부족하다 — 이 LateUpdate가 바로 뒤에 돌면서 다시 켜 버리기 때문이다.
            bool bodyHidden = _headOutline != null && !_headOutline.enabled;
            float target = wantVisible ? 1f : 0f;
            _alpha = bodyHidden
                ? 0f
                : Mathf.MoveTowards(_alpha, target, Time.deltaTime / Mathf.Max(0.01f, FadeSeconds));

            if (_alpha <= 0.001f)
            {
                SetLinesEnabled(false);
                if (_container != null && _container.activeSelf) _container.SetActive(false);
                return;
            }

            SyncFacing();
            EnsureBuilt();
            if (_container == null) return;
            if (!_container.activeSelf) _container.SetActive(true);
            SetLinesEnabled(true);

            // ★ 2026-08-31 — 루트 스케일을 컨테이너에서 상쇄한다(아래 "이중 스케일" 문단).
            SyncContainerScale();

            // 몸 바운스 + 상체 기울임 추종(클래스 문서 (3)/(3-2)) — 컨테이너 하나를 엉덩이 피벗으로
            // 돌리고 밀면 네 아이템이 함께 따라간다. localPosition은 <b>부모(루트) 로컬 단위</b>다.
            //
            // 식의 유도: 컨테이너 안의 점 p(월드 단위, 중립 직립 좌표)가 루트 로컬에서
            //   f(p) = 엉덩이 + R·(p/s - 엉덩이) + (0, 바운스)   [s = 루트 배율]
            // 로 가야 하는데, 컨테이너 자식의 변환은 localPosition + R·p/s 이므로
            //   localPosition = 엉덩이 - R·엉덩이 + (0, 바운스)
            // 가 된다(= 피벗 회전 공식 그 자체). 기울임이 없으면 R = identity라 예전 식과 완전히 같다.
            Quaternion bodyRotation = ResolveBodyRotation();
            Vector3 hipLocal = new Vector3(0f, HipY / RootScale, 0f);
            _container.transform.SetLocalPositionAndRotation(
                hipLocal - bodyRotation * hipLocal + new Vector3(0f, ResolveBodyOffsetY(), 0f),
                bodyRotation);

            // 머리 좌우 추종(클래스 문서 (3-1)) — 머리에 붙은 것만 따라간다.
            // 이쪽은 <b>컨테이너 안</b>(월드 스케일 1)이므로 루트 로컬 오프셋에 배율을 곱해 월드로 올린다.
            // (기울어진 컨테이너 안이라 이 오프셋도 함께 기운다. 지금 이 값은 항상 0이고 —
            //  StickConfig.idleAmbientLookHeadShiftRatio — 되살아나더라도 목 획 반폭 이하라 무시 가능하다.)
            if (_headGroup != null)
            {
                _headGroup.localPosition = new Vector3(ResolveHeadOffsetX() * RootScale, 0f, 0f);
            }

            TickHemMotion();
            ApplyAlpha();
        }

        /// <summary>
        /// 랙돌/던져짐 중에는 숨긴다(클래스 문서 (4)). 착용 중인 것이 없어도 숨긴다.
        ///
        /// ★ 그리고 <b>몸이 안 보이면 액세서리도 안 보인다</b>(2026-08-29 실측으로 발견한 교차 레이어 영향).
        /// 이 앱에는 캐릭터를 통째로 숨기는 경로가 최소 두 개 있다 — 가출 은신(States/RunawayState.cs가
        /// StickmanBlackboard.SetCharacterVisible로 호출)과 전체화면 앱 감지 시 자동 숨김
        /// (Core/StickmanAgent.Suspend, 비침해 원칙 2). 둘 다 <c>StickmanAgent</c>가 <b>Awake에서
        /// 캐시한</b> 렌더러 배열만 끄는데, 이 렌더러의 액세서리는 그 뒤에 런타임 생성되므로 그 배열에
        /// 들어 있지 않다. 그대로 두면 캐릭터가 사라진 자리에 <b>모자와 망토만 공중에 남는다</b>
        /// (PlayMode 회귀 테스트 Phase5VisualLayerTests가 실제로 이 상태를 잡아냈다).
        ///
        /// 상태 목록을 늘려 대응하지 않고 <b>머리 링(HeadOutline)이 지금 켜져 있는가</b>를 그대로 따라간다 —
        /// 숨기는 이유가 무엇이든(앞으로 새 경로가 생겨도) 자동으로 함께 숨는 유일한 규칙이기 때문이다.
        /// </summary>
        private bool ResolveWantVisible()
        {
            if (!EquipmentModel.AnyEquipped()) return false;
            if (_headOutline != null && !_headOutline.enabled) return false;

            var machine = _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            if (machine != null)
            {
                StickmanStateId id = machine.CurrentStateId;

                // ★ 2026-08-31 — <b>ThrowTumble을 이 목록에서 뺐다</b>(사용자 신고 "캐릭터 회전할때도
                //   모자착용중인데 모자가 없어짐"의 근본 원인).
                //
                //   위 (4)의 숨김 근거는 <b>랙돌에만</b> 성립한다: 랙돌은 전신을 물리에 위임해
                //   머리·몸통이 루트에서 <b>독립적으로</b> 굴러가므로, 루트 로컬로 그리는 이 렌더러의
                //   모자는 몸에서 떨어져 나간다. 그런데 ThrowTumble은 랙돌이 아니다 —
                //   States/StickmanBlackboard.TickPose()가 이 상태에서 <c>rig.EnterActiveMode()</c>를
                //   지나 <c>SnapRootUpright()</c> <b>직전</b>에 빠져나가고(그 자리의 주석 참고),
                //   회전은 States/ThrowTumbleState가 <b>루트 하나의</b> 시각 회전으로 구동한다.
                //   즉 머리·몸통·액세서리가 <b>같은 강체</b>로 함께 돈다. 숨길 이유가 없었다.
                //
                //   실측(Tests/PlayMode/AccessoryFacingFlipFillTests): 던져서 180도까지 도는 동안
                //   회전 프레임의 <b>78%에서 모자 채움이 0개</b>였다 = 사용자가 본 그림 그대로다.
                //   랙돌은 그대로 숨긴다(그쪽 근거는 여전히 유효하다).
                if (id == StickmanStateId.Ragdoll) return false;
            }
            return true;
        }

        /// <summary>
        /// <see cref="ICharacterInkExtentProvider"/> — 지금 그리고 있는 액세서리 잉크의 최저 월드 Y.
        ///
        /// GETUP 바닥 클리어런스(States/GetupState)가 유일한 호출부다. 왜 이 계산을 액세서리가 직접
        /// 해야 하는가: 실측 스윕에서 <b>망토(CapeOutline) 하나가 8.92pt를 단독 기여</b>한 사이클이
        /// 있었다 — 즉 몸(FK 끝점)만 보고 들어 올리면 망토만 발판 아래로 남는다. 그렇다고 소비자 쪽에
        /// 모자챙/망토자락 좌표를 다시 적으면 32종(+DLC)이 늘어날 때마다 그 목록이 낡는다.
        ///
        /// 계산은 <b>지금 실제로 그리는 정점</b>에서 한다(모양 비율을 다시 유도하지 않는다 —
        /// 도형 빌더와 두 벌이 되면 반드시 어긋난다). LineRenderer.bounds를 쓰지 않는 이유는
        /// Tests/PlayMode/StickmanInkBounds 문서와 같다(Y로 +1.0유닛 부풀려져 있다). 반대로
        /// 채움 면(MeshRenderer)의 bounds는 실제 메시 정점에서 나오므로 그대로 쓴다.
        ///
        /// 안 보이면(페이드 0 / 장비 없음 / RAGDOLL 중 숨김) false — 그리지 않는 잉크는 바닥을 뚫을
        /// 수 없으므로 리프트를 만들면 안 된다(안 그러면 랙돌 중 숨은 망토가 몸을 계속 띄운다).
        /// 알파가 0보다 크기만 하면 포함한다 — 반투명이어도 화면에는 보이고, 회귀 테스트의 잉크
        /// 계측도 알파를 보지 않기 때문이다(두 기준이 어긋나면 테스트만 실패한다).
        /// </summary>
        public bool TryGetLowestInkWorldY(out float worldY)
        {
            worldY = float.PositiveInfinity;
            if (_alpha <= 0.001f || _container == null || !_container.activeSelf) return false;

            bool any = false;
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null || !lr.enabled || !lr.gameObject.activeInHierarchy) continue;
                int count = lr.positionCount;
                for (int q = 0; q < count; q++)
                {
                    Vector3 p = lr.GetPosition(q);
                    float y = (lr.useWorldSpace ? p : lr.transform.TransformPoint(p)).y;
                    if (!any || y < worldY) { worldY = y; any = true; }
                }
            }
            for (int i = 0; i < _fills.Count; i++)
            {
                MeshRenderer mr = _fills[i];
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                float y = mr.bounds.min.y;
                if (!any || y < worldY) { worldY = y; any = true; }
            }

            if (!any) worldY = 0f;
            return any;
        }

        /// <summary>
        /// <see cref="ICharacterVisualSource"/> — 지금 그리고 있는 액세서리 선/채움면을 단일 창구에 신고한다.
        ///
        /// <para>왜 필요한가: StickmanAgent가 Awake에서 캐시한 렌더러 배열에는 이 컨테이너가
        /// <b>영원히 없다</b>(우리는 그 뒤에 만들어진다). 그래서 전체화면 자동 숨김 / 획 두께 하한 /
        /// 화면 여백 계산이 셋 다 액세서리를 못 보고 있었다.</para>
        ///
        /// <para><see cref="CharacterVisualAnchor.BodyAttached"/>인 이유: 컨테이너가 캐릭터 루트의
        /// 자식이라 몸을 그대로 따라다닌다 — 화면 여백(시각 반폭)에 반드시 포함돼야 한다
        /// (긴 망토는 배율 2.00에서 몸보다 0.30유닛 더 튀어나온다).</para>
        /// </summary>
        public void CollectVisuals(CharacterVisualRegistry sink)
        {
            if (sink == null || _container == null || !_container.activeSelf) return;
            sink.AddRange(_lines, CharacterVisualAnchor.BodyAttached);
            sink.AddRange(_fills, CharacterVisualAnchor.BodyAttached);
        }

        /// <summary>
        /// 액세서리 LineRenderer의 <c>enabled</c>를 직접 켜고 끈다.
        /// GameObject를 비활성화하는 것만으로는 부족하다 — 이 앱의 "캐릭터가 지금 보이는가" 판정은
        /// 여러 곳에서 <c>GetComponentsInChildren&lt;LineRenderer&gt;(true).enabled</c>로 이루어지고
        /// (Core/StickmanAgent.SetRenderersEnabled, Tests/PlayMode/Phase5VisualLayerTests), 비활성
        /// GameObject의 컴포넌트도 그 조회에 잡히면서 <c>enabled</c>는 true로 남기 때문이다.
        /// </summary>
        private void SetLinesEnabled(bool enabledState)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr != null && lr.enabled != enabledState) lr.enabled = enabledState;
            }
            for (int i = 0; i < _fills.Count; i++)
            {
                MeshRenderer mr = _fills[i];
                if (mr != null && mr.enabled != enabledState) mr.enabled = enabledState;
            }
        }

        private void SyncFacing()
        {
            var blackboard = _agent.Blackboard;
            if (blackboard == null) return;
            float sign = blackboard.FacingSign >= 0f ? 1f : -1f;
            if (Mathf.Approximately(sign, _facingSign)) return;
            _facingSign = sign;
            _builtSignature = -1; // 좌우가 바뀌면 도형을 다시 굽는다(클래스 문서 (2)).
        }

        /// <summary>클래스 문서 (3) — Head의 현재 localY에서 중립(=HeadCenterLocalY)을 빼면 바운스 오프셋.
        /// <para>★ 2026-08-31 단위 정정: <c>_headTransform.localPosition</c>은 <b>루트 로컬</b> 단위인데
        /// <c>_metrics.HeadCenterLocalY</c>는 이미 루트 스케일이 곱해진 <b>월드</b> 단위다. 루트 스케일이
        /// 1이던 시절에는 두 단위가 같아서 우연히 맞았다 — 다이얼이 붙는 순간 바운스 오프셋이 배율만큼
        /// 틀어지므로 중립값을 로컬로 되돌려 뺀다.</para></summary>
        private float ResolveBodyOffsetY()
        {
            if (_headTransform == null || _metrics == null) return 0f;
            return _headTransform.localPosition.y - LeanedHeadNeutralLocal().y;
        }

        /// <summary>클래스 문서 (3-1) — Head의 현재 localX에서 중립을 뺀 값. 세로(y)와 달리
        /// Metrics에 대응 항등식이 없어(중립 x는 언제나 프리팹 값 그대로다) Awake에서 잰 값을 쓴다.</summary>
        private float ResolveHeadOffsetX()
            => _headTransform != null ? _headTransform.localPosition.x - LeanedHeadNeutralLocal().x : 0f;

        /// <summary>
        /// 지금 상체가 기운 각도(클래스 문서 (3-2)). <b>계산하지 않고 읽는다</b> — 포즈가 Torso에
        /// 실제로 적용한 회전이 유일한 진실이므로, 이 렌더러가 각도 유도식을 한 벌 더 갖지 않는다.
        /// </summary>
        private Quaternion ResolveBodyRotation()
            => _torsoTransform != null ? _torsoTransform.localRotation : Quaternion.identity;

        /// <summary>
        /// "기울임이 적용된 머리 중립 위치"(루트 로컬). 바운스/좌우 오프셋 역산의 기준점이다 —
        /// 프리팹 중립을 그대로 빼면 기울임이 만든 머리 이동까지 바운스로 오해해 두 번 적용된다
        /// (클래스 문서 (3-2)). 기울임이 없으면 결과가 프리팹 중립과 정확히 같다.
        /// </summary>
        private Vector2 LeanedHeadNeutralLocal()
        {
            Vector2 neutral = _hasHeadNeutral
                ? _headNeutralLocal
                : new Vector2(0f, _metrics != null ? _metrics.HeadCenterLocalY / RootScale : 0f);

            Quaternion rot = ResolveBodyRotation();
            if (rot == Quaternion.identity) return neutral;

            var hip = new Vector2(0f, HipY / RootScale);
            return hip + (Vector2)(rot * (neutral - hip));
        }

        /// <summary>착용 조합 + 방향 + 배율이 그대로면 아무것도 하지 않는다(24시간 상주 앱 — 매 프레임
        /// GameObject를 만들고 부수지 않는다. StressGaugeRenderer의 "단계가 바뀐 순간에만 재구성"과 동일).</summary>
        private void EnsureBuilt()
        {
            int signature = ComputeSignature();
            if (_built && signature == _builtSignature) return;

            Rebuild();
            _builtSignature = signature;
            _built = true;
        }

        /// <summary>
        /// ★ 재구성 서명 — 2026-08-30 32종 확장에서 <b>실제 버그를 고친 자리</b>.
        ///
        /// 확장 전에는 "카테고리 비트마스크"였다. 그때는 카테고리당 아이템이 하나뿐이라 그 값이 곧
        /// 착용 상태 전부였지만, 32종이 된 지금은 <b>같은 카테고리 안에서 아이템만 바꾸면</b>
        /// (천 모자 → 왕관) 마스크가 그대로여서 도형이 영영 갱신되지 않는다 —
        /// 화면에는 "착용은 됐다는데 그림이 그대로"로 나타난다.
        /// 그래서 <see cref="EquipmentModel.WornStateSignature"/>(카테고리별 <b>아이템 자리</b>까지 섞는
        /// 정수 1개, 할당 0)로 갈아탔다. 직전 라운드 데이터 모델 코더가 이 자리를 위해 미리 만들어 둔 값이다.
        ///
        /// 잠금 상태도 함께 섞는다: 레벨이 올라 잠긴 아이템이 열리는 순간에도 다시 구워야 한다
        /// (착용 상태는 그대로인데 그릴지 말지가 바뀌는 유일한 경로다).
        /// </summary>
        private int ComputeSignature()
        {
            int hash = EquipmentModel.WornStateSignature;

            int unlockedMask = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsUnlocked((EquipmentSlot)i))
                {
                    unlockedMask |= 1 << i;
                }
            }
            hash = hash * 31 + unlockedMask;
            hash = hash * 31 + (_facingSign >= 0f ? 1 : 0);

            // ★ 2026-08-30 실측으로 발견한 결함: 잉크색(⌃⌥⌘C / 정보창 [외형] 탭)을 바꿔도 액세서리는
            //   예전 색 그대로 남았다. StickmanAgent.ApplyInkColorFromConfig()는 **Awake에서 캐시한**
            //   LineRenderer 배열만 갱신하는데 액세서리 선은 그 뒤에 런타임 생성되기 때문이다
            //   (Tasklist.md의 "캐릭터를 통째로 숨기는 경로" 함정과 정확히 같은 뿌리). 색을 서명에
            //   넣어 색이 바뀐 프레임에 도형을 다시 굽는다 — 색만 갱신하는 별도 경로를 만들지 않는
            //   이유는, 그 경로가 재구성 경로와 어긋나 또 하나의 이중 정의가 되기 때문이다.
            hash = hash * 31 + ResolveInkColor().GetHashCode();

            // ★ 2026-08-31 문서 정정 — 예전 주석은 "배율은 실행 중에 바뀌지 않는다(프리팹에 구워짐)"
            //   였는데 크기 다이얼(UX_FLOW.md 34-3)이 붙은 뒤로 <b>사실이 아니다</b>. 실행 중에 바뀌고,
            //   바뀌면 이 서명 때문에 컨테이너가 Destroy된 뒤 다시 구워진다 — 그 사실을 모른 채
            //   LineRenderer 참조를 캐시해 둔 테스트가 전부 null이 되어 조용히 스킵됐다(거짓 안심).
            hash = hash * 31 + Mathf.RoundToInt((_metrics != null ? _metrics.TotalHeight : 1f) * 10000f);

            // 실제로 그려질 두께 — 화면상 하한에 걸리면 배율이 그대로여도 두께가 달라진다
            // (창 크기/DPI 변화). 서명에 없으면 그 프레임에 다시 굽지 않아 옛 두께가 남는다.
            // ★ 2026-09-02 M6 — 하한이 둘이 되면서 <b>두 두께 모두</b> 서명에 들어가야 한다.
            //   낱선 두께만 넣으면, 낱선은 하한에 걸려 그대로인데 채움 경계선만 달라지는 구간
            //   (배율 0.509 부근)에서 다시 굽지 않아 옛 경계선 두께가 남는다.
            hash = hash * 31 + Mathf.RoundToInt(RenderStrokeWidth * 10000f);
            hash = hash * 31 + Mathf.RoundToInt(RenderFillOutlineStrokeWidth * 10000f);

            // 33-2-5 (D) 줄무늬 타이는 월요일에 조금 느슨해진다 — 자정을 넘기면 다시 구워야 한다.
            hash = hash * 31 + DayOfWeekIndex;
            return hash;
        }

        /// <summary>
        /// 오늘 요일. <see cref="System.DateTime.Now"/>를 매 프레임 부르지 않는다 — 이 앱은 하루 종일
        /// 켜져 있고 요일은 하루에 한 번 바뀐다. <see cref="DayPollSeconds"/>마다 한 번만 확인한다.
        /// </summary>
        private int DayOfWeekIndex
        {
            get
            {
                if (_cachedDayOfWeek < 0 || Time.unscaledTime - _dayCheckedAt >= DayPollSeconds)
                {
                    _cachedDayOfWeek = (int)System.DateTime.Now.DayOfWeek;
                    _dayCheckedAt = Time.unscaledTime;
                }
                return _cachedDayOfWeek;
            }
        }

        private bool IsMonday => DayOfWeekIndex == (int)System.DayOfWeek.Monday;

        /// <summary>지금 쓴 모자가 선언한 커버선(33-4-1). 모자를 안 썼거나 왕관이면 +∞ = 아무것도 안 가린다.</summary>
        private float ResolveHatCoverLocalY(in AccessoryShapeBuilder.Rig rig)
            => ShouldDraw(EquipmentSlot.Head)
                ? AccessoryShapeBuilder.HatCoverLocalY(EquipmentModel.WornIndex(EquipmentSlot.Head), rig)
                : float.PositiveInfinity;

        // ============================================================================
        // ★ 이중 스케일(s²) 제거 — 캐릭터 크기 다이얼 선행조건 (2026-08-31)
        // ============================================================================
        // 이 렌더러가 쓰는 치수(R / HeadCenterY / ShoulderY / HipY / StrokeWidth)는 전부
        // <see cref="StickmanMetrics"/>에서 오는데, 그쪽은 이미 <b>루트 lossyScale이 곱해진 월드 값</b>을
        // 돌려준다. 그런데 그 좌표로 만든 도형을 <b>루트의 자식</b>인 컨테이너에 그리면 배율이 한 번 더
        // 곱해진다 — 실측: 루트 스케일 2.6667에서 모자 꼭대기가 정수리 대비 2.839배(= 1.065 × s)로 떠올랐다.
        // 지금까지 이게 드러나지 않은 이유는 단 하나, <b>루트 스케일이 항상 1이었기</b> 때문이다.
        //
        // 고치는 방법은 둘이었다: (a) 모든 치수를 lossyScale로 나눠 로컬 단위로 바꾼다,
        // (b) 컨테이너의 localScale로 루트 스케일을 <b>상쇄</b>한다. (b)를 택했다 —
        //  · 나눠야 할 지점이 4개 프로퍼티 + StrokeWidth + 32종 도형 빌더에 흩어져 있어 (a)는
        //    빠뜨리기 쉽고, 빠뜨린 곳은 배율 1.0에서 여전히 정상으로 보인다(가장 나쁜 실패 유형).
        //  · LineRenderer의 <b>두께</b>는 Transform 스케일을 따라가지 않으므로(2026-08-30 실측),
        //    (a)로 두께까지 나누면 오히려 두 번 틀린다. (b)는 컨테이너 안의 월드 스케일이 1이라
        //    좌표도 두께도 <b>둘 다 월드 단위 하나</b>로 통일된다.
        //  · AccessoryShapeBuilder(32종 도형 전체)를 한 줄도 고치지 않는다.

        /// <summary>루트의 현재 월드 배율(항상 &gt; 0). 다이얼이 없던 시절에는 늘 1이었다.</summary>
        private float RootScale
        {
            get
            {
                float s = Mathf.Abs(transform.lossyScale.y);
                return s > 0.0001f ? s : 1f;
            }
        }

        /// <summary>컨테이너의 월드 스케일을 1로 유지한다(위 문단). 값이 실제로 달라진 프레임에만
        /// 대입한다 — 24시간 상주 앱이라 매 프레임 Transform을 건드리지 않는다.</summary>
        private void SyncContainerScale()
        {
            if (_container == null) return;
            float inv = 1f / RootScale;
            Vector3 current = _container.transform.localScale;
            if (Mathf.Approximately(current.x, inv) && Mathf.Approximately(current.y, inv)) return;
            _container.transform.localScale = new Vector3(inv, inv, 1f);
        }

        private void Rebuild()
        {
            if (_container != null) Destroy(_container);
            DestroyFillMeshes();
            _lines.Clear();
            _fills.Clear();
            _swayLines.Clear();
            _swayApplied = false;

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("EquipmentAccessories");
            _container.transform.SetParent(transform, false);
            SyncContainerScale();   // 첫 프레임부터 월드 스케일 1(위 "이중 스케일" 문단).

            // ★ 머리에 붙는 것만 따로 담는 자식. 유휴 앰비언트 "주위 살피기"가 머리만 좌우로 미는데
            //   (AccessoryShapeBuilder.IsHeadAttached 문서), 컨테이너 하나로는 그 오프셋을 모자에만
            //   줄 수 없다. 목/등 아이템은 어깨선에서 유도되므로 바깥 컨테이너에 그대로 남는다.
            var headGroup = new GameObject("HeadAttached");
            headGroup.transform.SetParent(_container.transform, false);
            _headGroup = headGroup.transform;

            Color ink = ResolveInkColor();
            AccessoryShapeBuilder.Rig rig = BuildRig();

            // ★ 슬롯별 if 사다리를 쓰지 않는다 — 카테고리가 4개에서 8개가 되면서, 어느 슬롯을 빠뜨려도
            //   컴파일은 통과하고 화면에서만 조용히 사라지는 구조가 됐기 때문이다. 순회로 바꾸면
            //   EquipmentSlot에 값이 하나 더 생기는 순간 자동으로 함께 그려진다(도형만 추가하면 된다).
            _shapes.Clear();
            float cover = ResolveHatCoverLocalY(rig);
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (!ShouldDraw(slot)) continue;

                // 색은 <b>슬롯 단위</b>로 한 번 푼다 — 도형마다 카탈로그를 다시 뒤지지 않기 위해서고,
                // 그래야 "이 아이템의 두 색"이라는 팔레트 규칙이 코드에서도 그대로 보인다.
                int item = EquipmentModel.WornIndex(slot);
                ItemCatalog.ResolveWornPalette(slot, item, ink, out Color primary, out Color secondary);

                int start = _shapes.Count;
                AccessoryShapeBuilder.Append(_shapes, slot, item, rig, cover, StrokeWidth * 0.5f, IsMonday);

                Transform parent = AccessoryShapeBuilder.IsHeadAttached(slot) ? _headGroup : _container.transform;
                for (int k = start; k < _shapes.Count; k++)
                {
                    // k − start = <b>이 아이템 안에서 몇 번째</b>. 채움끼리 겹칠 때의 순서를 정한다
                    // (AccessoryShapeBuilder.FillDepthStep — sortingOrder는 건드리지 않는다).
                    AddShape(_shapes[k], ToneColor(_shapes[k].Tone, primary, secondary), parent, k - start);
                }
            }
        }

        private static bool ShouldDraw(EquipmentSlot slot)
            => EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot);

        // ==================== 유틸 ====================

        /// <summary>도형이 선언한 <b>역할</b>을 실제 색으로 바꾼다. 세 번째 톤(그림자)은 팔레트를
        /// 늘리지 않고 주색에서 유도한다 — AccessoryShapeBuilder.Shade 문서 참고.</summary>
        private static Color ToneColor(byte tone, Color primary, Color secondary)
        {
            if (tone == AccessoryShapeBuilder.Accent) return secondary;
            if (tone == AccessoryShapeBuilder.Shade) return AccessoryShapeBuilder.FillOutlineColor(primary);
            return primary;
        }

        private void AddShape(in AccessoryShapeBuilder.Shape shape, Color color, Transform parent,
            int orderWithinItem)
        {
            // ★ 채움 면 먼저(윤곽선 바로 아래). 2026-08-30 사용자 신고 "모자가 투명해보임" —
            //   선화만으로는 모자 관 안쪽으로 머리 링이 그대로 비친다(AccessoryShapeBuilder.Shape.Filled).
            Color outline = color;
            Mesh fillMesh = null;
            if (shape.Filled)
            {
                fillMesh = AddFill(shape, color, parent, orderWithinItem);
                outline = AccessoryShapeBuilder.FillOutlineColor(color);
            }

            // ★ 2026-09-02 M6 — 이 선이 <b>채움의 경계선인가 낱선인가</b>가 두께를 가른다.
            //   판정 근거는 shape.Filled 하나뿐이고, 그 사실을 선 자신에게 표식으로 붙여
            //   에이전트/진단이 같은 답을 얻게 한다(FillOutlineStroke 문서).
            LineRenderer lr = AddLine(shape.Name, shape.Points, outline, shape.Loop, shape.SortingOrder,
                parent, shape.Filled);
            if (lr == null || !shape.HasSway) return;

            // 흔들 점이 있는 선만 별도 목록에 둔다 — 매 프레임 전체 선을 훑지 않기 위해서다.
            // ★ 채움 메시를 <b>같은 항목에</b> 넣는다(SwayLine.Fill 문서). 목록을 따로 두면 선과 면이
            //   서로 다른 프레임에 갱신될 수 있는 자리가 생기고, 그게 정확히 지금 고치는 버그다.
            var buffer = new Vector3[shape.Points.Length];
            System.Array.Copy(shape.Points, buffer, shape.Points.Length);
            _swayLines.Add(new SwayLine
            {
                Line = lr,
                Fill = fillMesh,
                Base = shape.Points,
                Buffer = buffer,
                Start = shape.SwayStart,
                Count = Mathf.Min(shape.SwayCount, shape.Points.Length - shape.SwayStart),
            });
        }

        /// <param name="sortingOrder">33-2-0의 레이어 재배치표. 기본값은 확장 전 값(6)이라
        /// 이 인자를 넘기지 않는 기존 호출부는 그대로 같은 그림을 얻는다.</param>
        /// <summary>채움 면 하나를 만든다. 재질은 캐릭터 선의 것을 그대로 빌려 쓰고 색은 정점 색으로
        /// 넣는다(AccessoryShapeBuilder.BuildFillMesh 문서). 메시는 <see cref="_fillMeshes"/>가
        /// 들고 있다가 재구성/파괴 때 직접 지운다 — GameObject를 지워도 메시는 남는다.</summary>
        private Mesh AddFill(in AccessoryShapeBuilder.Shape shape, Color color, Transform parent,
            int orderWithinItem)
        {
            Mesh mesh = AccessoryShapeBuilder.BuildFillMesh(shape.Points, color);
            if (mesh == null) return null;

            var go = new GameObject(shape.Name + "Fill");
            go.transform.SetParent(parent != null ? parent : _container.transform, false);
            // 같은 아이템의 채움끼리는 sortingOrder가 동률이라 순서가 미정이다. 로컬 z로 가른다 —
            // 나중에 넣은 것이 위로 온다(AccessoryShapeBuilder.FillDepthStep).
            go.transform.localPosition =
                new Vector3(0f, 0f, orderWithinItem * AccessoryShapeBuilder.FillDepthStep);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _lineMaterial;
            mr.sortingOrder = shape.FillSortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _fillMeshes.Add(mesh);
            _fills.Add(mr);
            return mesh;
        }

        /// <param name="isFillOutline">이 선이 <b>채운 도형의 경계선</b>인가. true면 두께 하한이
        /// 1.00pt로 내려가고(<see cref="RenderFillOutlineStrokeWidth"/>) 선에 표식이 붙는다.
        /// 기본값 false는 낱선(그 선이 그 자리의 유일한 잉크)이다.</param>
        private LineRenderer AddLine(string name, Vector3[] points, Color color, bool loop,
            int sortingOrder = AccessoryShapeBuilder.SortDefault, Transform parent = null,
            bool isFillOutline = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            float width = isFillOutline ? RenderFillOutlineStrokeWidth : RenderStrokeWidth;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = sortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            if (isFillOutline) FillOutlineStroke.Mark(lr);
            _lines.Add(lr);
            return lr;
        }

        // ==================== 밑단 움직임 (docs/UX_FLOW.md 33-2-5 (A) + 2026-08-31 낙하 펄럭임) ====================

        /// <summary>
        /// ★ 자락은 <b>지금 상태가 만든 움직임에서만</b> 흔들린다 — 원칙 1(행동-텍스트 싱크)의 그림 버전.
        ///
        /// <para>모드는 둘이고 가중치의 합이 <b>언제나 1</b>인 교차 페이드로 섞인다 — 그냥 더하면
        /// 던져진 순간에 진폭이 두 배가 되어 천이 몸을 뚫고, 배타로 잘라내면 기류가 0이 되는 프레임에
        /// 보행 스웨이가 진폭 그대로 튀어나온다(★ 2026-09-01에 배타 -> 교차 페이드로 고침):</para>
        /// <list type="bullet">
        ///   <item><b>보행 스웨이</b>(땅) — 카탈로그 문구 셋("걸을 때마다 흔들린다" 등)이 주장하는 동작.
        ///     2026-08-30 확장 전에는 이 렌더러가 완전히 정적이라 문구만 있고 동작이 없었다.</item>
        ///   <item><b>기류 펄럭임</b>(공중, 2026-08-31 신설) — 사용자 신고
        ///     "떨어지거나 할때 망토도 펄럭여야하는데 고정되어있음". 보행 스웨이가
        ///     <c>|velocity.x| / 보행속도</c> 하나로만 구동돼 <b>수직 낙하에서 진폭이 정확히 0</b>이었다
        ///     (그리고 0이면 SetPositions를 건너뛰므로 한 점도 안 움직이는 것이 설계상 보장돼 있었다).</item>
        /// </list>
        ///
        /// <para>도형 전체를 매 프레임 다시 굽지 않는다 — 각 도형이 "흔들리는 점 구간"을 스스로 선언하고
        /// (<see cref="AccessoryShapeBuilder.Shape.SwayStart"/>) 그 점에만 오프셋을 더한다. 오프셋 식은
        /// <see cref="AccessoryShapeBuilder.HemAirOffset"/>(순수 함수)에 있다.</para>
        ///
        /// <para><b>정지 중에는 갱신 호출 자체를 건너뛴다.</b> 24시간 상주 앱에서 헛일을 하지 않기
        /// 위해서이기도 하고, 그 스킵이 곧 "움직일 <b>때만</b> 흔들린다"를 코드로 보장하기 때문이기도
        /// 하다. 다만 멈춘 첫 프레임에는 <b>한 번만</b> 원본으로 되돌린다 — 안 그러면 마지막 흔들린
        /// 모양이 그대로 굳어 "멈췄는데 자락이 뒤로 날린 채"가 된다.
        /// ★ 2026-09-01 — 그 "멈춘 첫 프레임"이 <b>착지 프레임</b>이었던 것이 결함이었다. 지금은
        /// <see cref="TickAirFlowInertia"/>가 기류를 유한 시간에 걸쳐 0으로 데려가므로, 되돌리기는
        /// 천이 실제로 다 잦아든 뒤에만 일어난다(그 시점의 변위는 정의상 0이라 아무것도 튀지 않는다).</para>
        ///
        /// <para>★ 선과 <b>채움 면</b>을 같은 루프에서 함께 갱신한다. 2026-08-31 이전에는 선만 옮기고
        /// 채움 메시는 재구성 전까지 정적이라, 화면에서 천으로 보이는 면이 한 번도 움직이지 않았다
        /// (사용자가 "고정"이라고 신고한 것의 나머지 절반).</para>
        /// </summary>
        private void TickHemMotion()
        {
            if (_swayLines.Count == 0) return;

            // 지금 천에 걸려 있는 기류(관성 포함). 목표값이 아니라 이 값이 그림을 만든다.
            float air01 = TickAirFlowInertia(out Vector2 windLocal);

            // ★ 2026-09-01 — 배타(air ? 0 : walk)를 <b>교차 페이드</b>로 바꿨다. 배타였을 때는
            //   기류가 0이 되는 프레임에 보행 스웨이가 진폭 그대로 <b>튀어나왔다</b>(달리다 착지하면
            //   그 자리에서 천이 한 번 튄다). 가중치의 합이 언제나 1이므로 "둘을 더하면 진폭이 두 배가
            //   된다"(원래 배타로 만든 이유)는 여전히 성립하지 않는다.
            float walk01 = ResolveWalkSpeed01() * (1f - air01);

            if (air01 <= 0f && walk01 <= 0.0001f)
            {
                if (!_swayApplied) return;
                RestoreHemBase();
                _swayApplied = false;
                return;
            }

            float now = Time.time;
            float walkPhase = now * Mathf.PI * 2f / SwayPeriodSeconds;
            float walkAmplitude = R * SwayAmplitudeRatio * walk01;
            float r = R;

            for (int i = 0; i < _swayLines.Count; i++)
            {
                SwayLine s = _swayLines[i];
                if (s.Line == null) continue;

                System.Array.Copy(s.Base, s.Buffer, s.Base.Length);
                for (int k = 0; k < s.Count; k++)
                {
                    int idx = s.Start + k;
                    Vector3 p = s.Buffer[idx];
                    if (air01 > 0f)
                    {
                        Vector2 o = AccessoryShapeBuilder.HemAirOffset(r, windLocal, air01, now, idx);
                        p.x += o.x;
                        p.y += o.y;
                    }
                    if (walkAmplitude > 0f)
                    {
                        float sway = Mathf.Sin(walkPhase + idx * SwayPointPhaseStep) * walkAmplitude;
                        p.x += -_facingSign * sway * SwayBackRatio;  // 뒤로 밀린다
                        p.y += sway * SwayLiftRatio;                 // 살짝 들린다
                    }
                    s.Buffer[idx] = p;
                }

                s.Line.SetPositions(s.Buffer);
                ApplyFillVertices(s.Fill, s.Buffer);
            }
            _swayApplied = true;
        }

        /// <summary>
        /// ★★ 2026-09-01 (디버거) — <b>천에는 관성이 있다.</b> 기류의 <b>목표값</b>
        /// (<see cref="ResolveAirFlow01"/>)을 그대로 그리지 않고, 이 함수가 관성을 태운 <b>실제</b>
        /// 값을 돌려준다.
        ///
        /// ============================================================================
        /// 무엇이 결함이었나 (실측)
        /// ============================================================================
        /// 목표값은 상태로 게이트돼 있다(Fall/Jump/ThrowTumble). <b>착지하는 순간 그 게이트가 닫히면서
        /// 목표가 1에서 0으로 한 프레임에 떨어지고</b>, 그러면 <c>TickHemMotion</c>이 곧바로
        /// <see cref="RestoreHemBase"/>를 불러 밑단을 구워진 원본으로 <b>순간이동</b>시킨다.
        /// 최대 기류에서의 변위는 <c>(HemAirPushRatio 0.85 + HemAirRippleRatio 0.34)·R = 1.19R</c>이므로,
        /// 출하 기본 배율(R=0.22)에서만 해도 <b>한 프레임에 0.26유닛</b>이 튄다. 사용자가 요청한
        /// "떨어질 때 펄럭이는 망토"가 <b>가장 눈에 띄는 순간(발이 닿는 프레임)에 딸깍 끊긴다.</b>
        ///
        /// 실측 근거(PlayMode 진단, 배율 상한 1.5·긴 망토): 착지 연출(LandingCrouch) 0.62초 <b>전
        /// 구간 2720프레임에서 망토 잉크 반폭이 정확히 1.0730유닛 — 소수점 넷째 자리까지 구워진
        /// 도형값 그대로였다.</b> 즉 몸이 이 앱에서 가장 크게 움직이는 그 0.62초 동안 천은 <b>단 한
        /// 점도</b> 움직이지 않았다.
        ///
        /// ============================================================================
        /// 어떻게 고치는가
        /// ============================================================================
        ///  · <b>붙잡을 때는 즉시</b>(목표가 지금보다 세면 그대로 따라간다) — 바람은 천을 곧바로 민다.
        ///    낙하 회귀 테스트가 재는 "낙하 중 최대 이동"이 한 프레임도 늦어지지 않는다.
        ///  · <b>놓을 때는 <see cref="AirFlowSettleSeconds"/>에 걸쳐 선형으로</b> — 유한 시간에 정확히
        ///    0이 되므로 "가만히 서 있으면 정적"과 정지 시 계산 스킵이 모두 그대로 유지된다.
        ///  · 방향은 <b>마지막 공중 프레임의 것을 유지</b>한다. 천은 세기를 잃는 것이지 방향을 바꾸는
        ///    것이 아니고, 지상에서 새 방향을 지어내면 그것이야말로 원칙 1 위반(상태가 만들지 않은
        ///    움직임)이 된다.
        ///
        /// <para>원칙 1과의 관계: 이 잔여 운동은 <b>스스로 생기지 않는다.</b> 반드시 직전에 확정된
        /// 공중 상태가 만든 값에서만 출발하고, 단조 감소해 0에 닿은 뒤에는 어떤 경우에도 다시 커지지
        /// 않는다. "대사(그림)를 먼저 정하고 상태를 끼워 맞춘" 것이 아니라, 상태가 남긴 운동량을
        /// 물리적으로 소진시키는 것이다.</para>
        /// </summary>
        /// <param name="windLocal">지금 천을 미는 방향(로컬 단위벡터). 0이면 기류 없음.</param>
        private float TickAirFlowInertia(out Vector2 windLocal)
        {
            float target = ResolveAirFlow01(out Vector2 targetWind);

            // 갱신이 끊겼던 뒤(숨김/재구성)에는 관성을 태우지 않는다 — 위 _airFlowFrame 문서.
            bool continuous = _airFlowFrame == Time.frameCount - 1;
            _airFlowFrame = Time.frameCount;

            if (target > 0f) _airWindLocal = targetWind;   // 공중이면 방향은 언제나 지금 것.

            if (!continuous || target >= _airFlow01) _airFlow01 = target;
            else _airFlow01 = Mathf.MoveTowards(_airFlow01, target, Time.deltaTime / AirFlowSettleSeconds);

            if (_airFlow01 <= AirFlowEpsilon)
            {
                _airFlow01 = 0f;
                _airWindLocal = Vector2.zero;
            }

            windLocal = _airWindLocal;
            return _airFlow01;
        }

        /// <summary>흔들린 선/면을 <b>구워진 원본</b>으로 한 번에 되돌린다(멈춘 첫 프레임 전용).</summary>
        private void RestoreHemBase()
        {
            for (int i = 0; i < _swayLines.Count; i++)
            {
                SwayLine s = _swayLines[i];
                if (s.Line != null) s.Line.SetPositions(s.Base);
                ApplyFillVertices(s.Fill, s.Base);
            }
        }

        /// <summary>채움 메시의 정점을 지금 점으로 갈아 끼운다.
        /// <para><c>Mesh.RecalculateBounds()</c>를 반드시 함께 부른다 — 정점만 바꾸고 경계를 두면
        /// 카메라가 옛 경계로 컬링을 판정해, 크게 젖혀진 자락이 화면 가장자리에서 통째로 사라진다
        /// (이 프로젝트가 투사체에서 이미 겪은 "화면 밖 컬링" 계열 사고와 같은 뿌리다).</para>
        /// <para>정점 <b>개수</b>는 바뀌지 않으므로 색/삼각형 채널은 그대로 살아 있다.</para></summary>
        private static void ApplyFillVertices(Mesh mesh, Vector3[] points)
        {
            if (mesh == null || points == null || mesh.vertexCount != points.Length) return;
            mesh.vertices = points;
            mesh.RecalculateBounds();
        }

        /// <summary>지금 걷는 속도(0~1). 블랙보드가 없으면 0 — 정지로 본다(테스트 스텁 리그 포함).</summary>
        private float ResolveWalkSpeed01()
        {
            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null) return 0f;
            StickConfig config = blackboard.Config;
            float walk = config != null ? config.ResolveWalkSpeed() : 1f;
            if (walk <= 0.0001f) return 0f;
            return Mathf.Clamp01(Mathf.Abs(blackboard.Body.linearVelocity.x) / walk);
        }

        /// <summary>
        /// 지금 <b>공중</b>인가, 그렇다면 기류가 얼마나 센가(0~1)와 어느 쪽으로 부는가.
        ///
        /// <para><b>(a) 왜 상태를 보는가</b> — 원칙 1. 기류 펄럭임은 "발이 땅에서 떨어져 몸이 통째로
        /// 날아가는 중"이라는 <b>확정된 상태</b>에서만 파생돼야 한다. 땅에서는 발이 박혀 있어 천이
        /// 아래로 늘어지는 것이 맞고, 그쪽 연출은 보행 스웨이가 이미 맡고 있다.
        /// 대상은 <see cref="StickmanStateId.Fall"/> / <see cref="StickmanStateId.Jump"/> /
        /// <see cref="StickmanStateId.ThrowTumble"/> 셋이다. <see cref="StickmanStateId.Ragdoll"/>은
        /// 넣지 않는다 — 그 상태에서는 액세서리를 통째로 숨기므로(ResolveWantVisible) 계산이 낭비다.
        /// <see cref="StickmanStateId.Dragged"/>도 넣지 않는다: 커서에 붙들려 Kinematic으로 끌려다니는
        /// 동안의 속도는 "기류"가 아니라 사용자의 손이고, 커서를 흔들면 진폭이 발작하듯 튄다.</para>
        ///
        /// <para><b>(b) 왜 이 무차원화인가</b> — 세기의 기준을
        /// <see cref="StickConfig.fallPoseFullSpeedHeightsPerSecond"/>(초당 몇 신장)로 쓴다. 새 숫자를
        /// 만들지 않은 것이 핵심이다: 낙하 <b>자세</b>가 최대가 되는 바로 그 속도에서 망토도 최대로
        /// 펄럭인다 = 자세와 천이 <b>같은 한 값</b>에서 나온다. 속도를 절대 유닛/초로 재면 배율을 줄인
        /// 캐릭터일수록 같은 낙하에서 천이 더 세게 날리는 어긋남이 생긴다.</para>
        ///
        /// <para><b>(c) 왜 하강 속도가 아니라 속력인가</b> — 낙하 자세
        /// (<see cref="StickmanBlackboard.ComputeFallPoseIntensity"/>)는 착지에 대비하는 동작이라
        /// <b>하강</b> 성분만 본다. 기류는 방향을 가리지 않는다. 옆으로 세게 던져진(수평) 캐릭터의
        /// 망토가 안 날리면 그게 오히려 고장으로 읽힌다.</para>
        ///
        /// <para><b>(d) 왜 로컬로 내리는가</b> — 던지기 회전(States/ThrowTumbleState)은 <b>루트를 통째로</b>
        /// 돌린다. 이 렌더러의 점은 루트 로컬이므로 월드 방향을 그대로 쓰면 회전하는 몸 위에서 기류만
        /// 고정돼 망토가 몸을 가로질러 도는 그림이 된다. 로컬로 내리면 반대로, 몸이 도는 동안 기류가
        /// 몸 주위를 훑고 지나가는 <b>맞는</b> 그림이 나온다.
        /// <c>Transform.InverseTransformDirection()</c>은 스케일의 영향을 받지 않는다.</para>
        /// </summary>
        /// <param name="windLocal">바람이 <b>가는</b> 방향(로컬, 단위벡터). 0이면 기류 없음.</param>
        private float ResolveAirFlow01(out Vector2 windLocal)
        {
            windLocal = Vector2.zero;

            StickmanBlackboard blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null || blackboard.Machine == null) return 0f;
            if (!IsAirborne(blackboard.Machine.CurrentStateId)) return 0f;

            Vector2 v = blackboard.Body.linearVelocity;
            float speed = v.magnitude;
            if (speed <= 0.0001f) return 0f;

            float height = Mathf.Max(0.0001f, blackboard.CharacterHeightWorld);
            float full = blackboard.Config != null ? blackboard.Config.fallPoseFullSpeedHeightsPerSecond : 7f;
            if (full <= 0.0001f) full = 7f;

            float air01 = Mathf.Clamp01(speed / (height * full));
            if (air01 <= AirFlowEpsilon) return 0f; // 정점 부근 — 사실상 정지, 펄럭임도 없다.

            // 기류는 진행 <b>반대</b> 방향에서 불어와 천을 그쪽으로 민다.
            Vector3 local = transform.InverseTransformDirection(new Vector3(-v.x / speed, -v.y / speed, 0f));
            windLocal = new Vector2(local.x, local.y);
            return air01;
        }

        /// <summary>발이 땅에서 떨어져 몸이 통째로 날아가는 상태인가(위 (a) 문단).</summary>
        // 2026-09-01: GroundLossHang(발판이 사라져 허공에 붙잡힌 유예 구간)도 공중이다.
        // 빠뜨리면 코요테 연출 0.45초 동안 망토만 뻣뻣하게 멈춰 그림이 반쪽이 된다.
        private static bool IsAirborne(StickmanStateId id)
            => id == StickmanStateId.Fall || id == StickmanStateId.Jump || id == StickmanStateId.ThrowTumble
               || id == StickmanStateId.GroundLossHang;

        // ==================== 테스트/진단 훅 ====================

        /// <summary>
        /// 지금 치수·방향으로 <paramref name="slot"/>의 <paramref name="itemIndex"/>번 아이템을 굽고
        /// 그 잉크 사각형(획 두께 제외, 점 좌표만)을 돌려준다. 그릴 것이 없으면 false.
        ///
        /// 32종 각각에 전용 프로퍼티를 만드는 대신 이 훅 하나를 두는 이유: 프로퍼티를 32개 늘어놓으면
        /// 그 자체가 도형 정의의 두 번째 사본이 되고, 아이템이 늘 때마다 렌더러를 함께 고쳐야 한다.
        /// </summary>
        public bool TryMeasureItemBounds(EquipmentSlot slot, int itemIndex, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;
            _shapes.Clear();
            // 커버선 +∞ / 월요일 false — 측정은 결정론적이어야 한다(요일에 따라 테스트가 달라지면 안 된다).
            AccessoryShapeBuilder.Append(_shapes, slot, itemIndex, BuildRig(),
                float.PositiveInfinity, StrokeWidth * 0.5f, mondayLoosened: false);
            if (_shapes.Count == 0) return false;

            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < _shapes.Count; i++)
            {
                Vector3[] pts = _shapes[i].Points;
                for (int p = 0; p < pts.Length; p++)
                {
                    min = Vector2.Min(min, new Vector2(pts[p].x, pts[p].y));
                    max = Vector2.Max(max, new Vector2(pts[p].x, pts[p].y));
                }
            }
            return true;
        }

        /// <summary>이 아이템이 만드는 선의 개수(0이면 도형이 정의되지 않은 자리다).</summary>
        public int ItemLineCount(EquipmentSlot slot, int itemIndex)
        {
            _shapes.Clear();
            AccessoryShapeBuilder.Append(_shapes, slot, itemIndex, BuildRig(),
                float.PositiveInfinity, StrokeWidth * 0.5f, mondayLoosened: false);
            return _shapes.Count;
        }

        /// <summary>33-4-1 회귀용 — 이 모자를 썼을 때 이 머리 모양의 선이 몇 개나 살아남는가.</summary>
        public int HairLineCountUnderHat(int hairItemIndex, int hatItemIndex)
        {
            _shapes.Clear();
            AccessoryShapeBuilder.Rig rig = BuildRig();
            AccessoryShapeBuilder.Append(_shapes, EquipmentSlot.Hair, hairItemIndex, rig,
                AccessoryShapeBuilder.HatCoverLocalY(hatItemIndex, rig), StrokeWidth * 0.5f, false);
            return _shapes.Count;
        }

        /// <summary>
        /// ★ 2026-09-01 커버 규칙이 "생략"에서 "자르기"로 바뀌면서 신설한 회귀 훅.
        /// 선 개수만으로는 더 이상 규칙을 말할 수 없다 — 모자를 써도 <b>옆머리는 남는 것이 옳고</b>,
        /// 대신 <b>커버선 위로 잉크가 한 점도 올라가지 않아야</b> 한다. 그 최고점을 돌려준다.
        /// </summary>
        /// <returns>남은 것이 있으면 true.</returns>
        public bool TryMeasureHairTopUnderHat(int hairItemIndex, int hatItemIndex,
            out float topLocalY, out float hatCoverLocalY)
        {
            _shapes.Clear();
            AccessoryShapeBuilder.Rig rig = BuildRig();
            hatCoverLocalY = AccessoryShapeBuilder.HatCoverLocalY(hatItemIndex, rig);
            AccessoryShapeBuilder.Append(_shapes, EquipmentSlot.Hair, hairItemIndex, rig,
                hatCoverLocalY, StrokeWidth * 0.5f, false);

            topLocalY = float.NegativeInfinity;
            for (int i = 0; i < _shapes.Count; i++)
            {
                Vector3[] pts = _shapes[i].Points;
                for (int k = 0; k < pts.Length; k++) topLocalY = Mathf.Max(topLocalY, pts[k].y);
            }
            return _shapes.Count > 0;
        }

        /// <summary>이 모자가 선언한 커버선(33-4-1). 왕관/미착용은 <see cref="float.PositiveInfinity"/>.</summary>
        public float HatCoverLocalYFor(int hatItemIndex)
            => AccessoryShapeBuilder.HatCoverLocalY(hatItemIndex, BuildRig());

        private void ApplyAlpha()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                if (Mathf.Approximately(c.a, _alpha)) continue;
                c.a = _alpha;
                lr.startColor = c;
                lr.endColor = c;
            }

            // 채움 면의 알파는 <b>정점 색</b>에 들어 있다(머티리얼은 캐릭터 것을 공유하므로 절대 만지지
            // 않는다 — 건드리면 캐릭터 획까지 함께 반투명해진다).
            for (int i = 0; i < _fillMeshes.Count; i++)
            {
                Mesh mesh = _fillMeshes[i];
                if (mesh == null) continue;
                Color[] colors = mesh.colors;
                if (colors == null || colors.Length == 0) continue;
                if (Mathf.Approximately(colors[0].a, _alpha)) continue;
                for (int k = 0; k < colors.Length; k++) colors[k].a = _alpha;
                mesh.colors = colors;
            }
        }

        /// <summary>액세서리도 캐릭터와 <b>같은 잉크색</b>을 쓴다 — 이 앱의 모든 시각 요소가 한 자루
        /// 펜으로 그린 선화라는 문법을 지킨다(잉크색 전환 ⌃⌥⌘C에도 함께 따라간다).</summary>
        private Color ResolveInkColor()
        {
            StickConfig config = _agent != null ? _agent.Config : null;
            return config != null ? config.ResolveInkColor() : Color.black;
        }

        /// <summary>다른 렌더러들과 같은 이유로 캐릭터 LineRenderer의 머티리얼을 빌려 쓴다
        /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }

        private Transform FindDirectChild(string childName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform t = transform.GetChild(i);
                if (t != null && t.name == childName) return t;
            }
            return null;
        }
    }
}
