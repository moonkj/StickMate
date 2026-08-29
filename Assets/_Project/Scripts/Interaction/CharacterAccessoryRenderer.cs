using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

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
    /// 제자리에 떠 있게 된다. 중립 y를 새로 상수로 적으면 또 하나의 이중 정의가 되므로,
    /// <b>Head의 중립 localY == StickmanMetrics.HeadCenterLocalY</b>라는 이미 성립하는 항등식
    /// (프리팹이 headY = totalHeight - headRadius로 굽고 Metrics도 같은 식으로 잰다)을 이용해
    /// 바운스 오프셋을 그 자리에서 역산한다. 상수 추가 0개.
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
    /// 라이벌 복제 방어
    /// ============================================================================
    /// 다른 렌더러들과 같은 이유로 자기 GameObject의 StickmanAgent가 없으면 아무것도 하지 않는다.
    /// 1차 방어는 Editor/SceneBootstrapper.CreateRivalStickman이 이 컴포넌트를 제거하는 것이다
    /// (라이벌은 장비도 성장도 없는 별개 개체다).
    /// </summary>
    public sealed class CharacterAccessoryRenderer : MonoBehaviour
    {
        // ==================== 비율 상수 (전부 머리 반경 / 몸통 길이 배수) ====================
        // 월드유닛 절대값은 하나도 없다 — 클래스 문서 (1) 참고.

        private const int SortingOrder = 6;      // 캐릭터 선(0~3)보다 위, 말풍선보다 아래.
        private const float FadeSeconds = 0.18f; // 랙돌 진입/복귀 시 깜빡임을 없애는 짧은 페이드.

        // 모자(캡) — 머리 반경 R 배수.
        // ★ 육안 검증 1회차(배율 0.75, 머리 반경 화면상 약 16pt)에서 챙 선 0.45R / 관 높이 0.78R은
        //   관 안쪽 여백(0.78R x 16pt ≈ 2.7pt)이 획 두께(약 3.6pt)보다 얇아 **머리 전체가 까맣게
        //   메워진 덩어리**로 보였다. 관을 높이고(1.05R) 챙을 눈 위로 올려(0.62R) 안쪽이 비도록 고쳤다.
        private const float HatBrimLineRatio = 0.62f;   // 챙이 얹히는 높이(머리 중심에서 위로 R x 이 값).
        private const float HatCrownHeightRatio = 1.05f; // 챙 선에서 정수리 방향으로 솟는 높이.
        private const float HatCrownHalfWidthRatio = 0.80f; // 관(crown) 밑변 반폭(= 챙 높이에서의 머리 폭 0.785R에 맞춤).
        private const float HatBrimReachRatio = 1.95f;  // 챙 끝이 머리 중심에서 앞으로 뻗는 거리.
        private const float HatBrimDropRatio = 0.16f;   // 챙 끝이 챙 선보다 아래로 처지는 정도.

        // 선글라스 — 머리 반경 R 배수.
        private const float GlassesCenterRatio = 0.00f;  // 렌즈 중심 높이(머리 중심 기준, + = 위).
        private const float GlassesLensOffsetRatio = 0.44f; // 렌즈 중심의 좌우 거리.
        private const float GlassesLensHalfWidthRatio = 0.32f;
        private const float GlassesLensHalfHeightRatio = 0.19f;
        private const float GlassesTempleReachRatio = 1.02f; // 안경다리가 진행 반대쪽으로 뻗는 x 끝.

        // 나비넥타이 — 머리 반경 R 배수(목은 머리 바로 아래라 R 기준이 자연스럽다).
        private const float BowTieDropRatio = 1.15f;    // 머리 중심에서 아래로 내려간 위치(머리 링 바로 밑, 어깨보다 위 = 목).
        private const float BowTieHalfWidthRatio = 0.68f;
        private const float BowTieHalfHeightRatio = 0.30f;
        private const float BowTieKnotRatio = 0.13f;

        // 망토 — 어깨~고관절 거리(몸통 길이) 배수 + 머리 반경 배수 혼합.
        private const float CapeCollarRiseRatio = 0.10f;   // 어깨보다 살짝 위에서 시작(R 배수).
        private const float CapeCollarFrontRatio = 0.40f;  // 옷깃이 앞쪽으로 나온 폭(R 배수).
        private const float CapeCollarBackRatio = 0.62f;   // 옷깃이 뒤쪽으로 나온 폭(R 배수).
        private const float CapeLengthRatio = 1.35f;       // 몸통 길이(어깨-고관절) 배수. 밑단이 고관절 아래로 내려와야 '망토'로 읽힌다.
        private const float CapeSpreadRatio = 1.35f;       // 자락이 뒤로 벌어지는 폭(R 배수).
        private const float CapeHemWaveRatio = 0.18f;      // 밑단 물결의 깊이(R 배수).

        // 선 두께 — 캐릭터 획과 같은 문법을 유지하기 위해 전신 높이 비율로 잡는다.
        // 분자 0.048은 StressGaugeRenderer가 이미 쓰는 검증된 획 두께다(같은 그림체를 유지).
        private const float StrokeWidthRatio = 0.048f / StickConfig.BaselineCharacterTotalHeight;

        private StickmanAgent _agent;
        private StickmanMetrics _metrics;
        private Transform _headTransform;
        private LineRenderer _headOutline;   // 몸이 지금 보이는지 판정하는 기준(ResolveWantVisible 참고).
        private Material _lineMaterial;

        private GameObject _container;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(8);

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

        /// <summary>획 두께(월드 유닛).</summary>
        public float StrokeWidth => (_metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight) * StrokeWidthRatio;

        /// <summary>모자 챙 선의 로컬 Y(발바닥 기준).</summary>
        public float HatBrimLocalY => HeadCenterY + R * HatBrimLineRatio;

        /// <summary>모자 관(crown) 꼭대기의 로컬 Y.</summary>
        public float HatTopLocalY => HatBrimLocalY + R * HatCrownHeightRatio;

        /// <summary>모자 챙 끝의 로컬 X — <b>부호가 곧 바라보는 방향</b>이다(좌우 반전 회귀 테스트용).</summary>
        public float HatBrimTipLocalX => _facingSign * R * HatBrimReachRatio;

        /// <summary>선글라스 렌즈 중심의 로컬 Y.</summary>
        public float GlassesLocalY => HeadCenterY + R * GlassesCenterRatio;

        /// <summary>안경다리 끝의 로컬 X — 진행 <b>반대쪽</b>이므로 부호가 챙과 반대여야 한다.</summary>
        public float GlassesTempleTipLocalX => -_facingSign * R * GlassesTempleReachRatio;

        /// <summary>나비넥타이 중심의 로컬 Y.</summary>
        public float BowTieLocalY => HeadCenterY - R * BowTieDropRatio;

        /// <summary>망토 옷깃(어깨)의 로컬 Y.</summary>
        public float CapeCollarLocalY => ShoulderY + R * CapeCollarRiseRatio;

        /// <summary>망토 밑단의 로컬 Y.</summary>
        public float CapeHemLocalY => CapeCollarLocalY - TorsoLength * CapeLengthRatio;

        /// <summary>망토 자락이 가장 멀리 뻗은 로컬 X — 진행 <b>반대쪽</b>(뒤로 흩날린다).</summary>
        public float CapeTrailTipLocalX => -_facingSign * R * CapeSpreadRatio;

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
            _headTransform = FindDirectChild("Head");
            if (_headTransform != null)
            {
                for (int i = 0; i < _headTransform.childCount; i++)
                {
                    Transform c = _headTransform.GetChild(i);
                    if (c != null && c.name == "HeadOutline") _headOutline = c.GetComponent<LineRenderer>();
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
            if (_agent == null) return; // 라이벌 복제본 방어(클래스 문서).

            bool wantVisible = ResolveWantVisible();
            float target = wantVisible ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime / Mathf.Max(0.01f, FadeSeconds));

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

            // 몸 바운스 추종(클래스 문서 (3)) — 컨테이너를 통째로 밀면 네 아이템이 함께 따라간다.
            _container.transform.localPosition = new Vector3(0f, ResolveBodyOffsetY(), 0f);

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
                if (id == StickmanStateId.Ragdoll || id == StickmanStateId.ThrowTumble) return false;
            }
            return true;
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

        /// <summary>클래스 문서 (3) — Head의 현재 localY에서 중립(=HeadCenterLocalY)을 빼면 바운스 오프셋.</summary>
        private float ResolveBodyOffsetY()
        {
            if (_headTransform == null || _metrics == null) return 0f;
            return _headTransform.localPosition.y - _metrics.HeadCenterLocalY;
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

        private int ComputeSignature()
        {
            int mask = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsEquipped((EquipmentSlot)i) && EquipmentModel.IsUnlocked((EquipmentSlot)i, _agent.Config))
                {
                    mask |= 1 << i;
                }
            }
            mask |= _facingSign >= 0f ? 1 << 8 : 0;
            // 배율은 실행 중에 바뀌지 않지만(프리팹에 구워짐), 에디터에서 Remeasure를 부르는 경로가
            // 있으므로 치수도 서명에 넣어 조용히 어긋나는 경우를 없앤다.
            mask ^= Mathf.RoundToInt((_metrics != null ? _metrics.TotalHeight : 1f) * 10000f) << 9;
            return mask;
        }

        private void Rebuild()
        {
            if (_container != null) Destroy(_container);
            _lines.Clear();

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("EquipmentAccessories");
            _container.transform.SetParent(transform, false);

            Color ink = ResolveInkColor();
            StickConfig config = _agent != null ? _agent.Config : null;

            if (ShouldDraw(EquipmentSlot.Head, config)) BuildHat(ink);
            if (ShouldDraw(EquipmentSlot.Eyes, config)) BuildGlasses(ink);
            if (ShouldDraw(EquipmentSlot.Neck, config)) BuildBowTie(ink);
            if (ShouldDraw(EquipmentSlot.Shoulders, config)) BuildCape(ink);
        }

        private static bool ShouldDraw(EquipmentSlot slot, StickConfig config)
            => EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot, config);

        // ==================== 도형 (전부 "진행 방향 기준" 좌표 -> 마지막에 facing을 x에 곱한다) ====================

        /// <summary>캡 모자 — 관(둥근 사다리꼴) + 진행 방향으로 뻗는 챙. 챙 때문에 <b>비대칭</b>이다.</summary>
        private void BuildHat(Color ink)
        {
            float r = R;
            float brimY = HatBrimLocalY;
            float halfW = r * HatCrownHalfWidthRatio;
            float topY = HatTopLocalY;

            // 관: 밑변 양 끝에서 위로 올라가 둥근 지붕을 그리는 닫힌 고리.
            var crown = new List<Vector3>(12);
            crown.Add(F(-halfW, brimY));
            crown.Add(F(-halfW * 0.92f, brimY + (topY - brimY) * 0.55f));
            crown.Add(F(-halfW * 0.62f, topY));
            crown.Add(F(0f, topY + r * 0.05f));
            crown.Add(F(halfW * 0.62f, topY));
            crown.Add(F(halfW * 0.92f, brimY + (topY - brimY) * 0.55f));
            crown.Add(F(halfW, brimY));
            AddLine("HatCrown", crown.ToArray(), ink, loop: true);

            // 챙: 챙 선에서 진행 방향으로 뻗어 살짝 처지는 두 겹 선(윗면/아랫면).
            var brim = new[]
            {
                F(-halfW * 0.35f, brimY),
                F(halfW * 0.85f, brimY + r * 0.02f),
                F(r * HatBrimReachRatio, brimY - r * HatBrimDropRatio),
                F(halfW * 0.85f, brimY - r * 0.14f),
                F(-halfW * 0.35f, brimY - r * 0.10f),
            };
            AddLine("HatBrim", brim, ink, loop: true);
        }

        /// <summary>선글라스 — 렌즈 2개 + 브리지 + 진행 반대쪽 안경다리(비대칭).</summary>
        private void BuildGlasses(Color ink)
        {
            float r = R;
            float cy = GlassesLocalY;
            float dx = r * GlassesLensOffsetRatio;
            float hw = r * GlassesLensHalfWidthRatio;
            float hh = r * GlassesLensHalfHeightRatio;

            AddLine("GlassesLensFront", BuildRoundedBox(dx, cy, hw, hh), ink, loop: true);
            AddLine("GlassesLensBack", BuildRoundedBox(-dx, cy, hw, hh), ink, loop: true);
            AddLine("GlassesBridge", new[] { F(-dx + hw, cy + hh * 0.35f), F(dx - hw, cy + hh * 0.35f) }, ink, loop: false);

            // 안경다리는 얼굴 <b>뒤쪽</b>(진행 반대 방향)으로 뻗어 귀로 간다 — 이것이 비대칭 요소다.
            AddLine("GlassesTemple", new[]
            {
                F(-dx - hw, cy + hh * 0.45f),
                F(-r * GlassesTempleReachRatio, cy + hh * 0.15f),
            }, ink, loop: false);
        }

        /// <summary>나비넥타이 — 좌우 대칭(반전해도 같은 그림이어야 정상).</summary>
        private void BuildBowTie(Color ink)
        {
            float r = R;
            float cy = BowTieLocalY;
            float hw = r * BowTieHalfWidthRatio;
            float hh = r * BowTieHalfHeightRatio;
            float knot = r * BowTieKnotRatio;

            AddLine("BowTieWings", new[]
            {
                F(-hw, cy + hh), F(-knot, cy), F(-hw, cy - hh),
                F(-hw, cy + hh),
            }, ink, loop: false);
            AddLine("BowTieWingsRight", new[]
            {
                F(hw, cy + hh), F(knot, cy), F(hw, cy - hh),
                F(hw, cy + hh),
            }, ink, loop: false);
            AddLine("BowTieKnot", BuildRoundedBox(0f, cy, knot, knot * 1.2f), ink, loop: true);
        }

        /// <summary>망토 — 어깨에서 진행 <b>반대쪽</b>으로 흘러내리는 가장 비대칭인 아이템.</summary>
        private void BuildCape(Color ink)
        {
            float r = R;
            float collarY = CapeCollarLocalY;
            float hemY = CapeHemLocalY;
            float front = r * CapeCollarFrontRatio;
            float back = r * CapeCollarBackRatio;
            float trail = r * CapeSpreadRatio;
            float wave = r * CapeHemWaveRatio;

            var outline = new[]
            {
                F(front, collarY),                                   // 앞 옷깃
                F(-back, collarY + r * 0.04f),                       // 뒤 옷깃(살짝 세워진 칼라)
                F(-trail, hemY + (collarY - hemY) * 0.28f),          // 뒤로 벌어지는 자락
                F(-trail * 0.82f, hemY),                             // 밑단 뒤 끝
                F(-trail * 0.34f, hemY + wave),                      // 물결 1
                F(front * 0.35f, hemY - wave * 0.35f),               // 물결 2(앞쪽 밑단)
            };
            AddLine("CapeOutline", outline, ink, loop: true);

            // 접힌 주름 한 줄 — 평면 도형이 아니라 천이라는 것을 읽히게 하는 최소한의 표현.
            AddLine("CapeFold", new[]
            {
                F(-back * 0.35f, collarY - r * 0.10f),
                F(-trail * 0.52f, hemY + (collarY - hemY) * 0.18f),
            }, ink, loop: false);
        }

        // ==================== 유틸 ====================

        /// <summary>진행 방향 기준 좌표 -> 로컬 좌표. <b>x에만</b> facing 부호를 곱한다(클래스 문서 (2)).</summary>
        private Vector3 F(float forwardX, float localY) => new Vector3(forwardX * _facingSign, localY, 0f);

        private Vector3[] BuildRoundedBox(float forwardCx, float cy, float halfW, float halfH)
        {
            // 8각 근사 — 원보다 렌즈/매듭처럼 보이고 점이 적어 가볍다.
            return new[]
            {
                F(forwardCx - halfW, cy - halfH * 0.45f),
                F(forwardCx - halfW * 0.7f, cy + halfH),
                F(forwardCx + halfW * 0.7f, cy + halfH),
                F(forwardCx + halfW, cy + halfH * 0.35f),
                F(forwardCx + halfW * 0.85f, cy - halfH * 0.75f),
                F(forwardCx + halfW * 0.2f, cy - halfH),
                F(forwardCx - halfW * 0.6f, cy - halfH),
            };
        }

        private void AddLine(string name, Vector3[] points, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = StrokeWidth;
            lr.endWidth = StrokeWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            _lines.Add(lr);
        }

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
