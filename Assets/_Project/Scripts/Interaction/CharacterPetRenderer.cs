using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 펫(PET 슬롯) 6종 — 2026-08-30 외부 디자인 핸드오프(docs/UX_FLOW.md 33-6),
    /// 2026-09-01 <b>풍선/달팽이</b> 추가.
    /// 작은 공 / 종이비행기 / 리틀스틱메이트 / 커서 친구 / 풍선 / 달팽이.
    ///
    /// <para>★ 풍선·달팽이는 카테고리당 +2종 라운드가 <b>카드만 만들고 비워 둔 자리</b>였다("준비 중").
    /// 이 저장소의 확정 규칙("착용했는데 화면이 그대로면 그건 착용이 아니다")에 걸리는 상태였고
    /// 이번 라운드에서 채웠다. 둘 다 기존 4종과 <b>같은 배관</b>(물리 없음 + 지수 감쇠 보간 +
    /// 화면 클램프 + 알파 페이드)을 쓴다 — 새 컴포넌트도 새 상태도 만들지 않았다.</para>
    /// ("리틀스틱메이트"는 2026-08-31 사용자 요청으로 "작은졸라맨"에서 바뀐 표시 이름이다.
    ///  아이템 <b>아이디</b>(look.pet.mini)와 코드상의 자리 이름 PetMini는 그대로다 —
    ///  아이디를 함께 바꾸면 사용자의 저장된 차림이 사라진다.)
    ///
    /// ============================================================================
    /// 공통 계약 (33-6-1) — 지키지 않으면 절대 불변 원칙을 깬다
    /// ============================================================================
    /// · <b>물리 없음.</b> Rigidbody2D/Collider2D를 <b>하나도</b> 만들지 않는다. 위치는 전부 스크립트
    ///   보간이다 — 펫이 떠 있는 자리도 그대로 클릭 관통이다(원칙 2·3 직결).
    /// · 보간은 프로젝트 공통 패턴인 프레임레이트 독립 지수 감쇠: <c>p += (target − p)(1 − e^(−k·dt))</c>.
    /// · 펫은 캐릭터 몸에 붙은 것이 아니라 <b>독립 개체</b>다. 그래서 <b>랙돌/넘어짐 중에도 사라지지
    ///   않는다</b>(주인이 자빠져도 공은 그대로 굴러온다 — 그게 옳다). 단 가출 은신 / 전체화면 감지
    ///   때는 함께 사라진다(원칙 2). 그 판정은 새 상태 목록을 만들지 않고 <b>머리 링이 켜져 있는가</b>를
    ///   따라간다.
    /// · 화면 밖으로 나가지 않게 최종 위치를 카메라 사각형 안으로 clamp한다.
    ///
    /// ============================================================================
    /// 자기 캐릭터가 없으면 아무것도 하지 않는다
    /// ============================================================================
    /// 같은 GameObject의 <see cref="StickmanAgent"/>가 없으면 즉시 손을 뗀다(LandingDustRenderer 규약).
    /// </summary>
    public sealed class CharacterPetRenderer : MonoBehaviour, ICharacterVisualSource
    {
        // ---- 아이템 자리 / 공용 치수는 Interaction/AppearanceShapeBuilder.cs가 소유한다
        //      (초상화 미리보기가 같은 값을 읽어야 "미리보기"가 성립한다).
        private const int PetBall = AppearanceShapeBuilder.PetBall;
        private const int PetPlane = AppearanceShapeBuilder.PetPlane;
        private const int PetMini = AppearanceShapeBuilder.PetMini;
        private const int PetCursor = AppearanceShapeBuilder.PetCursor;
        private const int PetBalloon = AppearanceShapeBuilder.PetBalloon;
        private const int PetSnail = AppearanceShapeBuilder.PetSnail;

        // ---- 레이어(33-6). 종이비행기만 반주기마다 4 <-> 10을 오간다.
        private const int SortDefault = 4;
        private const int SortPlaneFront = 10;
        private const int SortCursorFriend = 11;

        /// <summary>풍선은 머리 위에 떠 있으므로 <b>항상 캐릭터 앞</b>이다(뒤로 가면 머리에 잘린다).
        /// 종이비행기의 "앞" 값과 같은 층을 쓴다 — 새 층을 발명하지 않는다.</summary>
        private const int SortBalloon = SortPlaneFront;

        // ---- 33-6-2가 못박은 궤적 상수 ----
        private const float BallTrailInHeight = 0.55f;
        private const float BallFollowRate = 4.0f;
        private const float BallRadiusInHeight = AppearanceShapeBuilder.BallRadiusInHeight;
        private const int BallSegments = 12;

        private const float PlaneOrbitSeconds = 3.2f;
        private const float PlaneCenterAboveHeadInR = 1.9f;
        private const float PlaneOrbitHalfWidthInR = 1.50f;
        private const float PlaneOrbitHalfHeightInR = 0.45f;
        private const float PlaneWingSpanInR = AppearanceShapeBuilder.PlaneWingSpanInR;

        private const float MiniTrailInHeight = 0.75f;
        private const float MiniFollowRate = 3.0f;
        private const float MiniScale = AppearanceShapeBuilder.MiniScale;
        private const float MiniLegSwingPeriod = 0.5f;
        private const float MiniLegSwingDegrees = 22f;
        private const float MiniMovingSpeedGateInHeight = 0.05f;

        // ---- 리틀스틱메이트 낙하 동기화(2026-08-31 사용자 신고: "높은 곳에서 떨어질 때 작은 졸라맨도
        //      캐릭터와 동일한 형태로 떨어져야 하는데 제대로 동작 안 함").
        //      각도는 전부 <b>마디의 절대 각도</b>(0 = 곧게 아래, 부호 = +x 쪽)이며, 실제로 적용하는 값은
        //      "구워진 마디의 기본 각도"와의 차이다(BuildMini가 그 기본 각도를 실측해 캐시한다).

        /// <summary>낙하 만세 — 팔의 절대 각도. 몸통 규격(StickmanPoseAnimator.ApplyFallPose 문서의
        /// "±152도 = 수직에서 바깥으로 28도")과 같은 어휘를 쓴다.</summary>
        private const float MiniAirArmDegrees = 152f;

        /// <summary>낙하 중 다리를 바깥으로 벌리는 절대 각도(StickConfig.fallPoseLegSpreadDegrees와 같은 층).</summary>
        private const float MiniAirLegSpreadDegrees = 16f;

        /// <summary>던지기 공중 회전 중의 <b>웅크림</b> — 팔/다리를 진행 방향으로 모은다. 회전이 눈에
        /// 읽히려면 실루엣이 작아야 한다(몸통의 ApplyThrowTumblePose와 같은 의도).</summary>
        private const float MiniTumbleArmDegrees = 74f;
        private const float MiniTumbleLegDegrees = 48f;

        /// <summary>웅크렸을 때도 두 마디가 완전히 겹치지 않게 하는 최소 벌림(도).</summary>
        private const float MiniTumbleLimbSpreadDegrees = 9f;

        /// <summary>무릎앉아 착지에서 다리를 바깥으로 벌리는 각도(깊이 1일 때). 마디가 하나뿐이라
        /// 무릎을 접을 수 없으므로 <b>벌려서 낮아진다</b>(스쿼트) — 몸이 내려가는 거리는 이 각도에서
        /// 유도되므로(<see cref="ResolveMiniCrouchDrop"/>) 발이 지면을 뚫거나 뜨지 않는다.</summary>
        private const float MiniCrouchLegSpreadDegrees = 34f;

        /// <summary>무릎앉아에서 팔을 진행 방향 앞으로 내미는 각도(깊이 1일 때) — 균형을 잡는 그림.</summary>
        private const float MiniCrouchArmDegrees = 62f;

        // ★ 자세 가중치의 감쇠 계수는 <b>여기서 새로 정하지 않는다</b>. 몸통이 같은 자세를 만들 때
        //   쓰는 값(StickmanBlackboard.PoseSmoothingRate / LandingCrouchPoseSmoothingRate)을 그대로
        //   빌려 쓴다 — 처음에는 22를 상수로 박았다가 PlayMode 로그로 반증했다: 무릎앉아의 눌림 구간은
        //   지속시간의 18%(얕은 착지에서 약 58ms)뿐인데 rate 22는 63% 수렴에 45ms가 걸려, 미니가
        //   주인보다 눈에 띄게 늦게 앉았다가 늦게 일어선다. 몸통은 바로 그 이유로 무릎앉아에만 더 높은
        //   계수(기본 48)를 쓰고 있었다(StickConfig.landingCrouchPoseSmoothingRate Tooltip). 값을 여기
        //   따로 두면 그 튜닝이 두 곳으로 갈라진다.

        /// <summary>주인의 엉덩이 높이를 실측하지 못했을 때 쓰는 신장 대비 비율.
        /// <see cref="ThrowTumbleState"/>의 같은 이름 상수와 <b>같은 값</b>이어야 한다 — 그 상태가
        /// 회전 보정에 쓴 축과 여기서 되돌리는 축이 다르면 미니가 회전 위상에 맞춰 출렁인다.</summary>
        private const float OwnerFallbackHipRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        // ---- 풍선(2026-09-01). 끈이 묶인 자리를 머리 옆에 두고, 주머니는 그 위에서 둥실거린다.
        //      회전 중심이 <b>묶인 자리</b>라 좌우로 흔들려도 끈이 몸에서 떨어지지 않는다.
        private const float BalloonFollowRate = 3.4f;
        private const float BalloonTetherBehindInR = 0.75f;   // 묶인 자리(머리 중심에서 진행 반대쪽)
        private const float BalloonTetherAboveInR = 0.30f;    // 묶인 자리(머리 중심에서 위)
        private const float BalloonBobSeconds = 2.6f;
        private const float BalloonBobInR = 0.28f;
        private const float BalloonMaxTiltDegrees = 26f;
        /// <summary>기울기 1도당 필요한 "목표에서 뒤처진 거리"의 기준(머리 반경 배수).
        /// 끌려가는 만큼 눕는다 — 속도를 따로 재지 않아도 되는 이유다(지연 자체가 곧 속도의 함수).</summary>
        private const float BalloonTiltReferenceInR = 1.2f;

        // ---- 달팽이(2026-09-01). 땅에 붙어 아주 느리게 따라온다.
        //      "느리다"를 계수 하나로만 표현하면 그냥 <b>덜 반응하는 공</b>이다. 그래서 따라가는
        //      속도 자체를 주기적으로 눌러(기어가는 리듬) 몸이 함께 늘었다 줄게 했다.
        private const float SnailTrailInHeight = 0.95f;
        private const float SnailFollowRate = 0.9f;
        private const float SnailCrawlSeconds = 1.3f;         // 한 번 밀어내는 주기
        private const float SnailCrawlGateFloor = 0.30f;      // 밀지 않는 구간에도 이만큼은 나아간다
        private const float SnailBreathScale = 0.045f;        // 기어갈 때의 몸 신축(균등 배율)
        private const float SnailSizeInR = AppearanceShapeBuilder.SnailSizeInR;
        private const int SnailShellSegments = 14;
        private const int SnailCoreSegments = 8;

        private const float CursorFollowRate = 9.0f;
        private const float CursorLeadSeconds = 0.08f;
        private const float CursorIdleOrbitSeconds = 2.4f;
        private const float CursorSizeInR = AppearanceShapeBuilder.CursorSizeInR;

        /// <summary>커서에서 반드시 떨어져 있어야 하는 최소 거리(OS 포인트). 원칙 2 직결 —
        /// 커서 위에 겹치면 클릭 대상이 가려지고 텍스트 캐럿 위에서는 편집을 방해한다.</summary>
        private const float CursorMinGapPoints = 24f;

        /// <summary>커서가 화면 가장자리 이 거리 안이면 반대쪽으로 붙는다(33-6-2 ④ 규칙 3).</summary>
        private const float CursorEdgeMarginPoints = 24f;

        private const float CursorIdleOrbitRadiusPoints = 6f;

        /// <summary>커서 친구가 프레임 페이싱 홀드를 거는 커서 속도 문턱(포인트/초).
        /// 40pt/s면 "손가락으로 아이콘 하나를 지나치는" 정도이며, 그보다 느리면 이 펫의 이동량이
        /// 프레임당 1픽셀 미만이라 제출을 줄여도 계단이 보이지 않는다.</summary>
        private const float CursorHoldSpeedPoints = 40f;

        /// <summary>보이기/숨기기 알파 전환 시간(가출 은신 / 전체화면 감지로 캐릭터가 사라질 때).</summary>
        private const float FadeSeconds = 0.25f;
        private const float StrokeRatio = 0.022f;

        private StickmanAgent _agent;
        private StickmanMetrics _metrics;
        private LineRenderer _headOutline;
        private Material _lineMaterial;

        /// <summary>몸통 Transform — <b>회전만</b> 읽는다(<see cref="LeanedHeadWorld"/>).
        /// 액세서리 렌더러와 같은 규약: 기울임 각도를 이 파일에서 새로 계산하지 않는다.</summary>
        private Transform _torsoTransform;

        private GameObject _container;
        private LineRenderer[] _lines;
        private Transform _body;      // 그림 전체가 붙는 자리(위치/회전/스케일)
        private int _builtItem = -1;
        private int _builtSignature = -1;

        private Vector2 _position;
        private bool _hasPosition;
        private float _ballAngleDegrees;
        private float _lastGroundY;
        private bool _hasGroundY;

        /// <summary>마지막으로 펫이 실제로 서 있던 발판 핸들. 주인이 공중에 뜬 동안(핸들 0) 펫이
        /// "마지막 발판 위에서 기다리게" 하는 유일한 근거다 — Y값만 캐시하면 그 사이 창이 움직여도
        /// 펫이 옛 높이에 남는다.</summary>
        private long _lastGroundHandle;
        private float _orbitPhase;
        private float _legPhase;

        /// <summary>구워진 미니 마디 4개(팔뒤/팔앞/다리뒤/다리앞)의 <b>기본 각도</b>(도, 0 = 곧게 아래).
        /// BuildMini가 실제 점 좌표에서 실측해 담는다 — 도형을 바꿔도 자세 계산이 저절로 따라온다.</summary>
        private float[] _miniLimbNeutral;

        /// <summary>지금 프레임의 자세 가중치(전부 지수 감쇠로 수렴). 세 값은 상태 하나가 정하므로
        /// 서로 배타적이지만, 전이 순간에는 겹쳐 섞이면서 자세가 이어진다.</summary>
        private float _miniAir01;
        private float _miniTumble01;
        private float _miniCrouch;
        private float _miniSpinDegrees;
        private Vector2 _cursorVelocity;
        private Vector2 _lastCursor;
        private bool _hasCursor;
        private float _alpha;

        /// <summary>테스트/진단용 — 지금 그려지고 있는 펫 자리(없으면 -1).</summary>
        public int ActivePetItemIndex => _builtItem;

        /// <summary>테스트/진단용 — 펫의 현재 월드 좌표.</summary>
        public Vector2 PetWorldPosition => _position;

        /// <summary>
        /// 테스트/진단용 — 머리에 매달리는 펫(종이비행기 궤도 중심 / 풍선 매듭)이 <b>지금</b> 기준으로
        /// 삼는 월드 좌표. 값은 <see cref="LeanedHeadWorld"/> 그 자체이므로 "그리는 값"과 "재는 값"이
        /// 갈라질 수 없다.
        /// </summary>
        public Vector2 HeadAnchorWorldPosition
        {
            get
            {
                StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
                if (bb == null || bb.Body == null) return transform.position;
                return LeanedHeadWorld(bb, HeadAnchorAboveHeadCenter);
            }
        }

        /// <summary>머리 중심에서 <see cref="HeadAnchorWorldPosition"/>까지의 높이(월드 유닛) —
        /// 종이비행기 궤도 중심 기준. 테스트가 "기울이지 않았다면 어디였을지"를
        /// <see cref="StickmanMetrics"/>만으로 계산할 수 있게 열어 둔다.</summary>
        public float HeadAnchorAboveHeadCenter => HeadRadius * PlaneCenterAboveHeadInR;

        /// <summary>테스트/진단용 — 지금 알파(숨김 페이드 확인).</summary>
        public float Alpha => _alpha;

        /// <summary>테스트/진단용 — 작은 공이 지금까지 굴러온 회전각(도). 스폰 프레임에 수천 도가
        /// 튀지 않는지 확인하는 창구다(R2 m4).</summary>
        public float BallSpinDegrees => _ballAngleDegrees;

        /// <summary>테스트/진단용 — 리틀스틱메이트에 지금 적용된 <b>몸통 회전각</b>(도).
        /// 주인의 루트 회전(ThrowTumbleState가 구동)을 그대로 따라간다.</summary>
        public float MiniSpinDegrees => _miniSpinDegrees;

        /// <summary>테스트/진단용 — 리틀스틱메이트의 <b>웅크림</b>(0 = 직립 / 1 = 최대 / 음수 = 반동).
        /// 주인의 <see cref="LandingCrouchState.CurrentCrouchAmount"/>를 지수 감쇠로 따라간다.</summary>
        public float MiniCrouchAmount => _miniCrouch;

        /// <summary>테스트/진단용 — 리틀스틱메이트의 <b>공중 자세 세기</b>(0~1).
        /// 주인의 <see cref="StickmanBlackboard.ComputeFallPoseIntensity"/>를 따라간다.</summary>
        public float MiniAirPostureAmount => _miniAir01;

        /// <summary>테스트/진단용 — 리틀스틱메이트의 <b>던지기 웅크림</b> 가중치(0~1).</summary>
        public float MiniTumblePostureAmount => _miniTumble01;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _metrics = StickmanMetrics.Find(this);
            _torsoTransform = FindDirectChild("Torso");

            Transform head = FindDirectChild("Head");
            if (head != null)
            {
                for (int i = 0; i < head.childCount; i++)
                {
                    Transform c = head.GetChild(i);
                    if (c != null && c.name == "HeadOutline") _headOutline = c.GetComponent<LineRenderer>();
                }
            }
        }

        private void OnEnable()
        {
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
        }

        private void OnDisable()
        {
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            Teardown();
        }

        private void OnDestroy() => Teardown();

        /// <summary>
        /// 트레이/앱 제어 메뉴의 <b>긴급 정지</b>(원칙 4 "모든 방해성 이벤트에는 1초 내 탈출구").
        /// 커서 친구만 반응한다 — 커서 근처에 붙는 <b>유일한</b> 아이템이라 방해성이 다른 셋과 다르다.
        /// 억누르기(숨김 플래그)가 아니라 실제로 <b>벗긴다</b>: 숨김 플래그는 정보창 [외형] 탭에
        /// "착용 중"으로 남아 화면과 UI가 어긋나고, 다음 실행에 이유 없이 되살아난다.
        /// </summary>
        private void OnEmergencyStop()
        {
            if (_agent == null) return;
            if (EquipmentModel.WornIndex(EquipmentSlot.Pet) != PetCursor) return;

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, _agent.Config);
            Debug.Log("[펫] 긴급 정지 — 커서 친구를 해제했습니다(커서 근처에 붙는 유일한 아이템). " +
                      "정보창 [외형] 탭에서 다시 고를 수 있습니다.");
        }

        private void LateUpdate()
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본.

            int item = ResolveActiveItem();
            bool bodyHidden = !IsCharacterVisible();
            bool visible = item >= 0 && !bodyHidden;

            // ★★ 2026-08-31 (원칙 2) — 주인이 그 프레임에 사라졌으면 펫도 그 프레임에 사라진다.
            // 페이드(0.25초)는 "펫만 바뀌는" 경우(아이템 해제/교체)를 위한 것이다. 전체화면 감지와
            // 가출 은신은 <b>주인이 통째로 없어지는</b> 경우라, 여기서 0.25초를 더 끌면 방금 켠
            // 전체화면 게임 위에 주인 없는 공/종이비행기가 떠 있고, 숨바꼭질에서는 펫이 은신처를
            // 그대로 가리킨다(실측 확인된 원칙 2 위반).
            float target = visible ? 1f : 0f;
            _alpha = bodyHidden
                ? 0f
                : Mathf.MoveTowards(_alpha, target, Time.deltaTime / Mathf.Max(0.01f, FadeSeconds));

            if (!visible && _alpha <= 0.001f)
            {
                Teardown();
                return;
            }
            if (!visible)
            {
                ApplyAlpha();
                return;
            }

            EnsureBuilt(item);
            if (_body == null) return;

            float dt = Time.deltaTime;
            switch (item)
            {
                case PetBall: TickBall(dt); break;
                case PetPlane: TickPlane(dt); break;
                case PetMini: TickMini(dt); break;
                case PetCursor: TickCursorFriend(dt); break;
                case PetBalloon: TickBalloon(dt); break;
                case PetSnail: TickSnail(dt); break;
            }
            ApplyAlpha();
        }

        // ==================== 궤적 ====================

        /// <summary>
        /// ① 작은 공 — 굴러서 따라온다. <b>미끄러지지 않는 구름 조건</b> <c>θ −= Δx / r</c>이 핵심이다:
        /// 이게 없으면 원이 아무리 이동해도 정지해 보인다(회전을 읽히게 하는 유일한 요소는 반지름 선).
        /// 캐릭터가 Jump/Fall/Ragdoll 중이면 목표 갱신을 멈추고 <b>마지막 발판 위에서 기다린다</b> —
        /// 공은 날지 않는다.
        /// </summary>
        private void TickBall(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            float h = Height;
            float radius = h * BallRadiusInHeight;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;

            // ★ 스폰 프레임에는 "이전 x"가 없다. 예전에는 _position 초기화 <b>전에</b> previousX를 읽어
            // delta가 0 -> 실제 x(수십 유닛)가 되고 첫 프레임 회전각이 수천 도 튀었다(R2 m4).
            bool hadPosition = _hasPosition;
            bool grounded = IsOwnerGrounded();
            float trailX = bb.Body.position.x - facing * h * BallTrailInHeight;

            if (!_hasPosition)
            {
                float spawnX = grounded ? trailX : bb.Body.position.x;
                _position = new Vector2(spawnX, bb.Body.position.y + radius);
                _hasPosition = true;
            }

            float previousX = _position.x;
            if (grounded)
            {
                _position.x = Mathf.Lerp(_position.x, trailX, 1f - Mathf.Exp(-BallFollowRate * dt));
            }

            _position.y = ResolveGroundY(bb, grounded, bb.Body.position.y) + radius;
            // ★ 2026-08-31 사용자 신고 "창 위에 있을 때 창 범위 안에 있어야 하는데 공중에 떠 있음".
            //   y만 발판에서 가져오고 x는 주인 기준 끌림거리로만 정하면, 주인이 창 가장자리에서 <b>돌아설</b>
            //   때 펫이 창 바깥으로 밀려 나가면서 "창 높이에 떠 있는" 그림이 된다(아래 함수 문서에 유도 전문).
            ClampToOwnerFoothold(bb, grounded, ref _position, radius);
            ClampToScreen(ref _position, radius);

            float delta = _position.x - previousX;
            if (hadPosition && radius > 0.0001f) _ballAngleDegrees -= delta / radius * Mathf.Rad2Deg;

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, _ballAngleDegrees);
            _body.localScale = Vector3.one;
        }

        /// <summary>
        /// ② 종이비행기 — 머리 위를 돈다. <b>반주기마다 sortingOrder를 4 ↔ 10으로 바꾸는 것이 이
        /// 아이템의 핵심</b>이다. 그러지 않으면 "머리를 도는" 것이 아니라 "머리 앞에서 좌우로 왔다갔다"로
        /// 보인다. 궤도는 납작한 타원이라 원근 착시가 생긴다.
        /// </summary>
        private void TickPlane(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            _orbitPhase += dt * Mathf.PI * 2f / PlaneOrbitSeconds;
            float r = HeadRadius;
            // ★ 2026-09-01 — 궤도 중심이 <b>기울임이 반영된</b> 머리 위다(교차 레이어 항목 #22).
            //   중립 머리에 묶여 있던 동안에는 상체가 기울어도 비행기만 제자리를 돌았다.
            Vector2 center = LeanedHeadWorld(bb, r * PlaneCenterAboveHeadInR);
            float cx = center.x;
            float cy = center.y;

            float sin = Mathf.Sin(_orbitPhase);
            float cos = Mathf.Cos(_orbitPhase);
            _position = new Vector2(cx + r * PlaneOrbitHalfWidthInR * cos, cy + r * PlaneOrbitHalfHeightInR * sin);
            ClampToScreen(ref _position, r * PlaneWingSpanInR);

            // 기수각 = 궤도 접선 방향(d/dt).
            float dx = -r * PlaneOrbitHalfWidthInR * sin;
            float dy = r * PlaneOrbitHalfHeightInR * cos;
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, angle);
            _body.localScale = Vector3.one;

            SetSortingOrder(sin < 0f ? SortDefault : SortPlaneFront);
        }

        /// <summary>
        /// ③ 리틀스틱메이트 — 미니어처가 따라온다. <b>다리는 실제로 이동 중일 때만 흔든다</b>:
        /// 멈췄는데 다리가 계속 움직이면 그게 바로 행동-그림 불일치(원칙 1의 그림 버전)다.
        ///
        /// ============================================================================
        /// ★★ 사용자 신고 (2026-08-31) — "높은 곳에서 떨어질 때 작은 졸라맨도 캐릭터와 동일한 형태로
        ///     떨어져야 하는데 제대로 동작 안 함"
        /// ============================================================================
        /// 확정된 원인(추측 아님 — 수정 전 이 함수 본문 12줄이 전부였다):
        ///   · y가 <b>언제나</b> <see cref="ResolveGroundY"/>였다. 주인이 공중이면 이 함수는 "마지막
        ///     발판의 상단"을 돌려주므로, 주인이 20유닛을 떨어지는 동안 미니는 원래 높이에 <b>붙박이</b>로
        ///     남아 x만 따라 미끄러졌다.
        ///   · 몸통 회전이 <c>Quaternion.identity</c> <b>하드코딩</b>이었다. 던지기 공중 회전
        ///     (ThrowTumbleState가 루트의 시각 회전을 직접 구동)이 미니에는 한 도(度)도 전달되지 않았다.
        ///   · <see cref="StickmanStateMachine.CurrentStateId"/>를 이 함수가 <b>한 번도 읽지 않았다</b> —
        ///     즉 Fall/ThrowTumble/LandingCrouch를 구독하는 경로 자체가 없었다(리더 가설 1이 옳았다).
        ///
        /// 수정 후 미니가 주인에게서 가져오는 것은 <b>정확히 네 가지</b>이며 전부 상태에서 파생된다:
        ///   (1) 공중 여부(<see cref="IsOwnerGrounded"/>) -> 높이를 발판이 아니라 <b>주인의 발바닥</b>에서 가져온다.
        ///   (2) 루트 회전각(<c>Body.rotation</c>) -> 몸통 회전. 회전 중심은 발이 아니라 <b>엉덩이</b>다
        ///       (ThrowTumbleState.ApplyRootRotation과 같은 식 — 발을 축으로 돌리면 머리가 원을 그린다).
        ///   (3) 낙하 세기(<see cref="StickmanBlackboard.ComputeFallPoseIntensity"/>) -> 만세 자세의 세기.
        ///   (4) 무릎앉아 깊이(<see cref="LandingCrouchState.CurrentCrouchAmount"/>) -> 스쿼트 깊이.
        /// 넷 다 <b>상태가 확정한 값</b>을 읽기만 한다(불변 원칙 1의 그림 버전 — 그림이 먼저 정해지고
        /// 상태가 따라가는 일이 없다).
        /// </summary>
        private void TickMini(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            float h = Height;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            bool grounded = IsOwnerGrounded();
            Vector2 ownerFoot = OwnerFootWorld(bb);
            float previousX = _position.x;

            float targetX = ownerFoot.x - facing * h * MiniTrailInHeight;
            if (!_hasPosition) { _position = new Vector2(targetX, ownerFoot.y); _hasPosition = true; }

            if (grounded)
            {
                _position.x = Mathf.Lerp(_position.x, targetX, 1f - Mathf.Exp(-MiniFollowRate * dt));
                _position.y = ResolveGroundY(bb, true, ownerFoot.y);
                ClampToOwnerFoothold(bb, true, ref _position, h * MiniScale * 0.5f);
            }
            else
            {
                // ★ 공중에서는 지수 감쇠를 쓰지 않는다. 던지기처럼 수평 속도가 큰 비행에서 rate 3.0의
                //   정상상태 지연은 v/3 유닛(초속 20유닛이면 6.7유닛 ≈ 신장 4개)이라, 미니가 주인과
                //   "같은 형태로" 떨어지는 게 아니라 통째로 뒤에 남았다가 착지 후 끌려오는 그림이 된다.
                //   이륙 프레임에는 이미 수렴해 있어 값이 같으므로 순간이동은 생기지 않는다.
                _position.x = targetX;
                _position.y = ownerFoot.y;
            }
            ClampToScreen(ref _position, h * MiniScale * 0.5f);

            // ---- 보행 스윙: 예전 그대로(접지 중에만). 자세 가중치와 달리 감쇠를 걸지 않는다.
            float speed = dt > 0.0001f ? Mathf.Abs(_position.x - previousX) / dt : 0f;
            bool moving = grounded && speed > h * MiniMovingSpeedGateInHeight;
            if (moving) _legPhase += dt * Mathf.PI * 2f / MiniLegSwingPeriod;
            float swing = moving ? Mathf.Sin(_legPhase) * MiniLegSwingDegrees : 0f;

            TickMiniPosture(bb, dt, grounded);
            ApplyMiniPose(facing, swing);

            float miniH = h * MiniScale;
            float drop = ResolveMiniCrouchDrop(miniH);

            // 회전 중심을 엉덩이로 옮기는 보정(ThrowTumbleState.ApplyRootRotation의 식과 동일):
            //   R(θ)·(0,p) = (−sinθ·p, cosθ·p)  ->  보정 = (sinθ·p, p − cosθ·p)
            float hip = miniH * AppearanceShapeBuilder.MiniHipRatio;
            float rad = _miniSpinDegrees * Mathf.Deg2Rad;
            float pivotX = Mathf.Sin(rad) * hip;
            float pivotY = hip - Mathf.Cos(rad) * hip;

            _body.position = new Vector3(_position.x + pivotX, _position.y - drop + pivotY, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, _miniSpinDegrees);
            // 좌우 반전은 자식 회전이 아니라 도형 재구성으로 처리한다(localScale.x = -1 금지 규약).
            _body.localScale = Vector3.one;
        }

        /// <summary>
        /// 자세 가중치 3종과 몸통 회전각을 <b>주인의 상태에서만</b> 갱신한다. 세 가중치는 상태 하나가
        /// 정하므로 상호 배타지만(전이 순간에만 겹친다), 각각 독립적으로 0으로 수렴하기 때문에 어떤
        /// 전이 조합에서도 자세가 튀지 않는다 — 상태 조합을 열거하지 않아도 되는 것이 이 구조의 요점이다.
        /// </summary>
        private void TickMiniPosture(StickmanBlackboard bb, float dt, bool grounded)
        {
            StickmanStateId id = bb.Machine != null ? bb.Machine.CurrentStateId : StickmanStateId.Idle;
            bool tumbling = id == StickmanStateId.ThrowTumble;

            float airTarget = grounded || tumbling ? 0f : Mathf.Clamp01(bb.ComputeFallPoseIntensity());
            float tumbleTarget = tumbling ? 1f : 0f;
            float crouchTarget = ResolveOwnerCrouchAmount(bb);

            // 공중 자세/텀블링은 몸통의 일반 포즈 계수를, 무릎앉아는 몸통의 무릎앉아 전용 계수를 쓴다
            // (위 주석의 반증 기록 참고 — 두 구간은 요구 반응속도가 다르다).
            float kPose = 1f - Mathf.Exp(-Mathf.Max(1f, bb.PoseSmoothingRate) * dt);
            float kCrouch = 1f - Mathf.Exp(-Mathf.Max(1f, bb.LandingCrouchPoseSmoothingRate) * dt);
            _miniAir01 += (airTarget - _miniAir01) * kPose;
            _miniTumble01 += (tumbleTarget - _miniTumble01) * kPose;
            _miniCrouch += (crouchTarget - _miniCrouch) * kCrouch;

            // 몸통 회전은 <b>보간하지 않는다</b>: 주인의 루트 회전각 그 자체가 이미 상태가 매 프레임
            // 적분한 값이라, 여기서 한 번 더 감쇠를 걸면 초당 수백 도의 회전이 눈에 띄게 뒤처진다.
            // 능동 상태에서 루트는 SnapRootUpright로 0이므로 평소에는 정확히 0이다.
            _miniSpinDegrees = bb.Body != null ? bb.Body.rotation : 0f;
        }

        /// <summary>
        /// 주인이 지금 무릎앉아 중이면 그 <b>진행 곡선 값</b>(0 = 직립 / 1 = 최대 깊이 / 음수 = 반동)을,
        /// 아니면 0을 돌려준다. 값을 새로 계산하지 않고 <b>상태 인스턴스에서 직접 읽는</b> 것이 핵심이다 —
        /// 같은 곡선을 여기서 다시 적으면 그 순간부터 두 개의 진실이 되고, 깊이/지속시간 튜닝이
        /// StickConfig 한 곳에서 끝나지 않는다.
        /// </summary>
        private static float ResolveOwnerCrouchAmount(StickmanBlackboard bb)
        {
            if (bb.Machine == null || bb.Machine.CurrentStateId != StickmanStateId.LandingCrouch) return 0f;
            return bb.Machine.CurrentState is LandingCrouchState crouch ? crouch.CurrentCrouchAmount : 0f;
        }

        /// <summary>
        /// 무릎앉아에서 <b>몸이 내려가는 거리</b>(월드 유닛). 미니는 마디가 하나뿐이라 무릎을 접을 수
        /// 없으므로 다리를 φ만큼 <b>벌려서</b> 낮아진다. 벌린 뒤 엉덩이~발끝의 수직 거리는
        /// <c>키·(MiniHipRatio·cosφ − MiniLegTipXRatio·sinφ)</c>이고(구워진 다리 끝점을 φ만큼 돌린
        /// 결과의 −y 성분), 원래 거리와의 차이가 곧 내려가는 거리다. 이 유도 덕분에 <b>발끝은 어떤
        /// 깊이에서도 정확히 지면에 남는다</b> — 임의의 "웅크림 = 얼마 내리기" 상수를 쓰면 발이 지면을
        /// 뚫거나 뜬다. 반동 구간(φ&lt;0)에서는 값이 음수가 되어 몸이 살짝 솟는다(다리가 곧게 펴진다).
        /// </summary>
        private float ResolveMiniCrouchDrop(float miniHeight)
        {
            float phi = MiniCrouchLegSpreadDegrees * _miniCrouch * Mathf.Deg2Rad;
            float standing = AppearanceShapeBuilder.MiniHipRatio;
            float bent = AppearanceShapeBuilder.MiniHipRatio * Mathf.Cos(phi)
                       - AppearanceShapeBuilder.MiniLegTipXRatio * Mathf.Sin(phi);
            return miniHeight * (standing - bent);
        }

        /// <summary>
        /// 자세 가중치 -> 마디 4개의 회전량(도)을 만들어 적용한다. 모든 항이 "기본 자세로부터의 차이"라
        /// <b>가중치가 전부 0이면 결과가 예전과 정확히 같다</b>(보행 스윙만 남는다) — 스위치를 끄면
        /// 예전 거동이 되는 이 프로젝트의 관례를 코드 구조로 보장한다.
        /// </summary>
        private void ApplyMiniPose(float facing, float swingDegrees)
        {
            if (_lines == null || _lines.Length < 6 || _miniLimbNeutral == null) return;

            for (int i = 0; i < 4; i++)
            {
                float neutral = _miniLimbNeutral[i];
                // 기본 각도의 부호가 곧 그 마디의 <b>바깥 방향</b>이다(몸통 리그의 NeutralSign과 같은 개념).
                float outward = neutral >= 0f ? 1f : -1f;
                bool isLeg = i >= 2;

                float airAbs = outward * (isLeg ? MiniAirLegSpreadDegrees : MiniAirArmDegrees);
                float tumbleAbs = facing * (isLeg ? MiniTumbleLegDegrees : MiniTumbleArmDegrees)
                                + outward * MiniTumbleLimbSpreadDegrees;

                float delta = _miniAir01 * (airAbs - neutral)
                            + _miniTumble01 * (tumbleAbs - neutral)
                            + _miniCrouch * (isLeg ? outward * MiniCrouchLegSpreadDegrees
                                                   : facing * MiniCrouchArmDegrees);

                // 다리에만 보행 스윙을 더한다(뒤/앞이 서로 반대 위상 — 예전 ApplyMiniLegSwing과 동일).
                if (isLeg) delta += i == 2 ? swingDegrees : -swingDegrees;

                RotateLimb(_lines[i + 2], delta);
            }
        }

        /// <summary>
        /// ④ 커서 친구 — 마우스를 따라다닌다. 원칙 2 직결 규칙 4가지를 전부 지킨다:
        /// (1) 커서에서 최소 24pt 이격, (2) 기본은 오른쪽 아래(툴팁 관례와 같은 사분면),
        /// (3) 커서가 화면 가장자리 24pt 안이면 반대쪽으로, (4) 콜라이더 0개.
        /// </summary>
        private void TickCursorFriend(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || !bb.TryGetCursorWorldPosition(out Vector2 cursor)) return;

            float worldPerPoint = ResolveWorldPerPoint();
            float gap = worldPerPoint * CursorMinGapPoints;
            if (_hasCursor && dt > 0.0001f)
            {
                Vector2 raw = (cursor - _lastCursor) / dt;
                _cursorVelocity = Vector2.Lerp(_cursorVelocity, raw, 1f - Mathf.Exp(-8f * dt));
            }
            _lastCursor = cursor;
            _hasCursor = true;

            Vector2 anchor;
            float speed = _cursorVelocity.magnitude;

            // ============================================================================
            // ★ 프레임 페이싱 홀드 (2026-09-01 컴포지터 라운드)
            // ============================================================================
            // 적응형 페이싱의 Still 등급은 "캐릭터 상태 ID가 Idle이면 화면이 거의 안 움직인다"는
            // 전제로 제출을 1/4로 줄인다(Platform/ViewerPresence.cs의 FramePacingTier.Still 문서:
            // 호흡 0.4px, 눈동자 3px). 그 전제가 깨지는 곳이 **여기 하나**다 — 커서 친구는 캐릭터가
            // 서 있어도 커서를 따라 화면을 가로지르므로, 15fps로 그리면 눈에 띄게 뚝뚝 끊긴다.
            //
            // 그래서 이 프로젝트의 확립된 관례(UI 표면이 자기가 활성임을 스스로 신고한다)를 그대로
            // 따른다. 조건을 **속도**에 건 이유: 커서가 멈춰 있으면 이 펫도 반경 6pt짜리 아주 느린
            // 원운동만 하므로(아래 else 분기) Still로 내려가도 잃는 것이 없다. 커서가 실제로
            // 움직이는 동안에만 60fps를 붙잡는다.
            //
            // 다른 펫 5종은 캐릭터를 기준으로 수십 픽셀 안에서 초 단위 주기로만 움직이므로 홀드를
            // 걸지 않는다(그것까지 걸면 펫을 낀 사용자는 절감이 통째로 사라진다).
            if (speed > worldPerPoint * CursorHoldSpeedPoints)
            {
                FramePacing.HoldActiveForInteraction();
            }

            if (speed * CursorLeadSeconds > gap)
            {
                // 진행 반대쪽으로 끌려간다.
                anchor = cursor - _cursorVelocity.normalized * (speed * CursorLeadSeconds);
            }
            else
            {
                // 멈춤 — 기본은 오른쪽 아래 + 아주 작은 원운동("살아 있음"만 읽히게).
                _orbitPhase += dt * Mathf.PI * 2f / CursorIdleOrbitSeconds;
                float orbit = worldPerPoint * CursorIdleOrbitRadiusPoints;
                anchor = cursor + new Vector2(gap, -gap)
                    + new Vector2(Mathf.Cos(_orbitPhase), Mathf.Sin(_orbitPhase)) * orbit;
            }

            anchor = FlipAnchorNearScreenEdge(cursor, anchor, worldPerPoint * CursorEdgeMarginPoints);

            // 규칙 1은 마지막에 한 번 더 강제한다 — 위 계산이 어떤 경로를 타든 24pt는 절대 조건이다.
            Vector2 away = anchor - cursor;
            if (away.sqrMagnitude < gap * gap)
            {
                away = away.sqrMagnitude < 1e-8f ? new Vector2(gap, -gap) : away.normalized * gap;
                anchor = cursor + away;
            }

            if (!_hasPosition) { _position = anchor; _hasPosition = true; }
            _position = Vector2.Lerp(_position, anchor, 1f - Mathf.Exp(-CursorFollowRate * dt));
            ClampToScreen(ref _position, HeadRadius * CursorSizeInR);

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.identity;
            _body.localScale = Vector3.one;
        }

        /// <summary>
        /// ⑤ 풍선(2026-09-01) — 머리 옆에 묶인 끈을 달고 위에서 둥실거린다.
        ///
        /// <para>회전 중심을 <b>묶인 자리</b>로 잡은 것이 이 아이템의 전부다. 주머니를 중심으로 두면
        /// 기울일 때 끈이 몸을 뚫고 지나간다 — 도형(AppearanceShapeBuilder.BalloonString/BalloonBody)이
        /// 원점을 매듭이 아니라 <b>묶인 자리</b>로 정의해 둔 이유가 이것이다.</para>
        ///
        /// <para>기우는 각도는 속도를 새로 재서 만들지 않는다. 지수 감쇠 추종의 <b>지연량</b>
        /// (목표와 현재 위치의 차이)이 이미 속도의 함수이므로 그 값을 그대로 쓴다 — 같은 사실을
        /// 두 번 계산하지 않는다는 이 저장소의 관례 그대로다.</para>
        ///
        /// <para>주인이 랙돌로 자빠져도 사라지지 않는다(33-6-4: 펫은 독립 개체). 다만 기울임 각도는
        /// 랙돌 중에도 Torso가 직립(States/StickmanPoseAnimator.ClearBodyLean)이라 0이다.</para>
        /// </summary>
        private void TickBalloon(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            float r = HeadRadius;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;

            _orbitPhase += dt * Mathf.PI * 2f / BalloonBobSeconds;
            Vector2 head = LeanedHeadWorld(bb, r * BalloonTetherAboveInR);
            var anchor = new Vector2(head.x - facing * r * BalloonTetherBehindInR,
                head.y + Mathf.Sin(_orbitPhase) * r * BalloonBobInR);

            if (!_hasPosition) { _position = anchor; _hasPosition = true; }
            _position = Vector2.Lerp(_position, anchor, 1f - Mathf.Exp(-BalloonFollowRate * dt));

            // 주머니 꼭대기까지가 화면 안에 들어와야 한다 — 끈 길이 + 지름이 곧 이 펫의 세로 크기다.
            float reach = r * (AppearanceShapeBuilder.BalloonStringInR
                + AppearanceShapeBuilder.BalloonRadiusInR * 2f);
            ClampToScreen(ref _position, reach * 0.5f);

            float lag = anchor.x - _position.x;
            float tilt = Mathf.Clamp(lag / (r * BalloonTiltReferenceInR) * BalloonMaxTiltDegrees,
                -BalloonMaxTiltDegrees, BalloonMaxTiltDegrees);

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _body.localScale = Vector3.one;
        }

        /// <summary>
        /// ⑥ 달팽이(2026-09-01) — 땅에 붙어 <b>아주 느리게</b> 따라온다.
        ///
        /// <para>"느리다"를 추종 계수 하나로만 표현하면 화면에서는 그냥 <b>둔한 공</b>으로 읽힌다.
        /// 그래서 나아가는 속도 자체에 주기적인 문(gate)을 걸어 <b>밀었다 쉬었다</b>를 만들고, 같은
        /// 위상으로 몸을 균등하게 늘였다 줄인다(비균등 배율은 LineRenderer 두께를 왜곡하므로 쓰지 않는다).</para>
        ///
        /// <para>높이/가로 범위는 공과 <b>완전히 같은 경로</b>(<see cref="ResolveGroundY"/> +
        /// <see cref="ClampToOwnerFoothold"/>)를 쓴다 — "주인이 지금 딛고 있는 발판"만 본다. 주인이
        /// 공중이면 목표 갱신을 멈추고 마지막 발판 위에서 기다린다(달팽이는 날지 않는다).</para>
        /// </summary>
        private void TickSnail(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            float h = Height;
            float r = HeadRadius;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            bool grounded = IsOwnerGrounded();
            float trailX = bb.Body.position.x - facing * h * SnailTrailInHeight;

            if (!_hasPosition)
            {
                _position = new Vector2(grounded ? trailX : bb.Body.position.x, bb.Body.position.y);
                _hasPosition = true;
            }

            // 기어가는 리듬 — 0..1 사이를 오가는 문을 추종 계수에 곱한다.
            _legPhase += dt * Mathf.PI * 2f / SnailCrawlSeconds;
            float gate = SnailCrawlGateFloor
                + (1f - SnailCrawlGateFloor) * (0.5f + 0.5f * Mathf.Sin(_legPhase));

            if (grounded)
            {
                _position.x = Mathf.Lerp(_position.x, trailX, 1f - Mathf.Exp(-SnailFollowRate * gate * dt));
            }
            _position.y = ResolveGroundY(bb, grounded, bb.Body.position.y);
            ClampToOwnerFoothold(bb, grounded, ref _position, r * SnailSizeInR);
            ClampToScreen(ref _position, r * SnailSizeInR);

            float breath = 1f + Mathf.Sin(_legPhase) * SnailBreathScale;

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.identity;
            // 좌우 반전은 도형 재구성으로 처리한다(localScale.x = -1 금지 규약) — 여기 배율은 균등하다.
            _body.localScale = new Vector3(breath, breath, 1f);
        }

        /// <summary>커서가 화면 가장자리 가까이 있으면 붙는 쪽을 뒤집는다(펫이 화면 밖으로 밀려나지 않게).</summary>
        private Vector2 FlipAnchorNearScreenEdge(Vector2 cursor, Vector2 anchor, float margin)
        {
            if (!TryGetScreenRect(out Rect view)) return anchor;

            Vector2 offset = anchor - cursor;
            if (cursor.x > view.xMax - margin && offset.x > 0f) offset.x = -offset.x;
            if (cursor.x < view.xMin + margin && offset.x < 0f) offset.x = -offset.x;
            if (cursor.y < view.yMin + margin && offset.y < 0f) offset.y = -offset.y;
            if (cursor.y > view.yMax - margin && offset.y > 0f) offset.y = -offset.y;
            return cursor + offset;
        }

        // ==================== 상태/좌표 도우미 ====================

        private int ResolveActiveItem()
        {
            if (!EquipmentModel.IsEquipped(EquipmentSlot.Pet)) return -1;
            if (!EquipmentModel.IsUnlocked(EquipmentSlot.Pet)) return -1;
            return EquipmentModel.WornIndex(EquipmentSlot.Pet);
        }

        /// <summary>가출 은신 / 전체화면 감지 판정. 새 상태 목록을 만들지 않고 머리 링을 따라간다.
        /// <b>랙돌은 여기 없다</b> — 펫은 독립 개체라 주인이 자빠져도 남는다(33-6-4).</summary>
        private bool IsCharacterVisible() => _headOutline == null || _headOutline.enabled;

        private bool IsOwnerGrounded()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Machine == null) return true;
            StickmanStateId id = bb.Machine.CurrentStateId;
            return id != StickmanStateId.Jump && id != StickmanStateId.Fall
                && id != StickmanStateId.Ragdoll && id != StickmanStateId.ThrowTumble;
        }

        /// <summary>
        /// 펫이 서 있을 바닥의 월드 Y — <b>주인이 지금 딛고 있는 그 발판</b>의 상단이다.
        ///
        /// ============================================================================
        /// ★ 사용자 신고 "창을 최대화하면 공은 창 위에 있고 캐릭터는 독 위에 있음"(2026-08-31)
        /// ============================================================================
        /// 예전 구현은 <c>TryGetGroundSurfaceWorldY(= 그 x에서 <b>가장 높은</b> 발판 상단)</c>를 물었다.
        /// 화면을 덮는 창이 하나라도 최대화되면 그 값은 어느 x에서든 <b>화면 꼭대기</b>가 된다. 그래서
        /// 캐릭터가 Dock 위(월드 Y ≈ -10.2)에 서 있는 동안 펫만 최대화된 창 상단(월드 Y ≈ +11.2)으로
        /// 올라가 21유닛 떨어진 채 따라오지 않는 것처럼 보였다. 펫의 x는 정상적으로 주인을 따라가고
        /// 있었다 — <b>어긋난 것은 y 하나뿐</b>이다.
        ///
        /// 이 함수가 예전에 물었어야 할 질문은 "이 x에서 딛을 수 있는 가장 높은 면은?"이 아니라
        /// "<b>주인이 지금 딛고 있는 면은?</b>"이다. 그 답은 발판 핸들에만 있다
        /// (<see cref="StickmanBlackboard.CurrentFootholdHandle"/>). 이 프로젝트에서 같은 API를 같은
        /// 이유로 잘못 쓴 사고가 이미 두 번 있었다 — 드래그 순간이동(2026-08-28,
        /// GroundSensor.TryGetFloorWorldY 문서)과 구조 안전망 순간이동(2026-08-29,
        /// Tests/PlayMode/GroundSnapTeleportTests). "가장 높은 표면"은 <b>표면을 고르는 용도가 아니다.</b>
        ///
        /// 주인이 공중(Jump/Fall/Ragdoll)이면 <see cref="StickmanBlackboard.CurrentFootholdHandle"/>이
        /// 0이 된다. 그때는 마지막으로 함께 서 있던 발판 핸들을 계속 조회한다 — 그 창이 움직이면 펫도
        /// 함께 실려 가고(매 프레임 재조회), 창이 닫혀 발판이 사라지면 마지막 Y를 유지한다(33-6-4:
        /// "발판을 놓친 순간 펫이 화면 밑으로 떨어지지 않게 한다").
        /// </summary>
        private float ResolveGroundY(StickmanBlackboard bb, bool ownerGrounded, float ownerFootY)
        {
            long handle = bb.CurrentFootholdHandle != 0L ? bb.CurrentFootholdHandle : _lastGroundHandle;
            if (handle != 0L && bb.TryGetFootholdTopWorldY(handle, out float topY))
            {
                _lastGroundHandle = handle;
                _lastGroundY = topY;
                _hasGroundY = true;
                return topY;
            }

            // 주인이 <b>서 있는데</b> 발판을 못 찾았다(최초 접지 전 / 그 창이 방금 사라진 프레임).
            // 이때 옛 캐시로 버티면 펫만 없어진 창의 높이에 남는다 — 루트가 곧 발바닥이므로 주인의 y가
            // 지면 그 자체다. 이 분기 덕분에 "주인은 서 있는데 펫은 딴 데 있다"가 한 프레임도 못 생긴다.
            if (ownerGrounded)
            {
                _lastGroundY = ownerFootY;
                _hasGroundY = true;
                return ownerFootY;
            }

            // 주인이 공중이고 마지막 발판도 사라졌다 — 마지막 높이를 유지한다(33-6-4).
            return _hasGroundY ? _lastGroundY : ownerFootY;
        }

        /// <summary>
        /// 주인의 <b>직립 환산 발바닥</b> 월드 좌표 — 펫이 "주인이 서 있는/떠 있는 자리"로 삼는 단 하나의 값.
        ///
        /// <para>왜 <c>Body.position</c>을 그냥 쓰면 안 되는가: 던지기 공중 회전(ThrowTumbleState)은
        /// <b>엉덩이를 축으로</b> 몸을 돌리기 위해 루트 위치에 보정량을 얹는다
        /// (<c>Body.position = 탄도상의_발 + (sinθ·p, p − cosθ·p)</c>, p = 엉덩이 로컬 높이).
        /// 그래서 회전 중에는 <c>Body.position</c>이 발바닥이 아니고, 그대로 쓰면 미니가 주인의 회전
        /// 위상에 맞춰 최대 2p(신장의 약 0.8배)만큼 위아래로 출렁인다.</para>
        ///
        /// <para>여기서는 그 상태의 내부 필드를 들여다보지 않고 <b>일반식</b>으로 되돌린다: 리그의
        /// 엉덩이는 루트의 자식이라 언제나 <c>엉덩이 = Body.position + R(θ)·(0,p)</c>이고, 그 값에서
        /// (0,p)를 빼면 회전과 무관한 발바닥이 나온다. θ=0(= 능동 상태에서 SnapRootUpright가 보장)
        /// 이면 <c>Body.position</c>과 <b>정확히</b> 같으므로 기존 경로의 거동은 한 치도 바뀌지 않는다.</para>
        /// </summary>
        private Vector2 OwnerFootWorld(StickmanBlackboard bb)
        {
            Vector2 p = bb.Body.position;
            float degrees = bb.Body.rotation;
            if (Mathf.Abs(degrees) < 0.001f) return p;

            float hip = _metrics != null && _metrics.HipLocalY > 0.0001f
                ? _metrics.HipLocalY
                : Height * OwnerFallbackHipRatio;
            float rad = degrees * Mathf.Deg2Rad;
            return p + new Vector2(-Mathf.Sin(rad) * hip, Mathf.Cos(rad) * hip - hip);
        }

        /// <summary>
        /// ★★ 사용자 신고 (2026-08-31) — <b>"창 위에 있을 때 창 범위 안에 있어야 하는데 공중에 떠 있음"</b>
        ///
        /// ============================================================================
        /// 확정된 원인 (코드 경로로 확정)
        /// ============================================================================
        /// 펫의 <b>y</b>는 2026-08-31 오전 수정으로 "주인이 지금 딛고 있는 발판의 상단"이 되었지만
        /// (<see cref="ResolveGroundY"/>), <b>x</b>는 그때도 지금도 <c>주인의 x − 끌림거리</c> 하나로만
        /// 정해진다. 즉 <b>발판의 가로 범위를 아무도 보지 않았다.</b>
        ///
        /// 그래서 다음 배치에서 반드시 재현된다 — 주인이 창의 <b>오른쪽 가장자리</b>에서 걸음을 멈추고
        /// (AutoWander는 <c>EdgeStopDistanceWorld</c>에서 선다) 왼쪽으로 <b>돌아서는</b> 순간, facing이
        /// −1이 되어 끌림거리의 부호가 뒤집히므로 펫의 목표 x가 <c>주인 + 끌림거리</c>가 된다. 리틀스틱
        /// 메이트의 끌림거리는 신장의 0.75배라 이 값은 창 오른쪽 끝을 <b>확실히</b> 넘는다. 그 결과
        /// 펫은 "창의 상단 높이"에 있으면서 창 바깥 x에 놓인다 = 화면상 <b>공중에 떠 있는 그림</b>이다.
        /// 주인이 창 왼쪽 끝에서 오른쪽으로 돌아설 때가 그 거울상이다.
        ///
        /// ============================================================================
        /// 수정
        /// ============================================================================
        /// y를 발판에서 가져왔으면 x도 <b>같은 발판</b>에서 제한해야 한다 — 두 축이 서로 다른 근거를 쓰면
        /// 어긋난다(이 프로젝트에서 반복된 실패 유형). 발판의 좌/우 모서리는 이미 단일 창구
        /// <see cref="StickmanBlackboard.TryGetFootholdEdgeWorld"/>가 <b>매 프레임 재조회</b>로 답한다
        /// (LedgeHang이 "창이 옆으로 움직이면 잡은 손도 따라간다"에 쓰는 바로 그 함수라, 유저가 창을
        /// 드래그하는 동안에도 값이 늙지 않는다).
        ///
        /// <para>주인이 공중이면(<paramref name="grounded"/> = false) 아무 것도 하지 않는다 — 그때
        /// 펫은 발판 위가 아니라 주인 옆에서 함께 떨어지는 중이고, 여기서 옛 발판의 폭에 묶으면 낙하가
        /// 대각선으로 휘어진다.</para>
        ///
        /// <para>발판이 펫보다 좁으면(작은 창) 안쪽 여백을 폭의 절반으로 줄여 <b>가운데</b>에 세운다.
        /// 여백을 그대로 두면 좌우 한계가 뒤집혀 Clamp가 미정의 결과를 낸다.</para>
        /// </summary>
        private void ClampToOwnerFoothold(StickmanBlackboard bb, bool grounded, ref Vector2 p, float halfWidth)
        {
            if (!grounded) return;

            long handle = bb.CurrentFootholdHandle != 0L ? bb.CurrentFootholdHandle : _lastGroundHandle;
            if (handle == 0L) return;
            if (!bb.TryGetFootholdEdgeWorld(handle, -1, out _, out float leftX)) return;
            if (!bb.TryGetFootholdEdgeWorld(handle, 1, out _, out float rightX)) return;
            if (rightX <= leftX) return;   // 퇴화한 사각형 — 손대지 않는 편이 안전하다.

            float inset = Mathf.Min(Mathf.Max(0f, halfWidth), (rightX - leftX) * 0.5f);
            p.x = Mathf.Clamp(p.x, leftX + inset, rightX - inset);
        }

        private bool TryGetScreenRect(out Rect rect)
        {
            rect = default;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            if (cam == null || !cam.orthographic) return false;
            float halfY = cam.orthographicSize;
            float halfX = halfY * cam.aspect;
            Vector3 c = cam.transform.position;
            rect = new Rect(c.x - halfX, c.y - halfY, halfX * 2f, halfY * 2f);
            return true;
        }

        private void ClampToScreen(ref Vector2 p, float margin)
        {
            if (!TryGetScreenRect(out Rect view)) return;
            p.x = Mathf.Clamp(p.x, view.xMin + margin, view.xMax - margin);
            p.y = Mathf.Clamp(p.y, view.yMin + margin, view.yMax - margin);
        }

        /// <summary>
        /// OS 1포인트가 몇 월드 유닛인가. <b>DPI 계산을 새로 적지 않는다</b> —
        /// 좌표 변환의 단일 소스는 <see cref="ScreenCoordinateConverter"/>이고(Retina 배율/화면 원점
        /// 뒤집기까지 그 안에 한 벌로 들어 있다), 여기서 손으로 다시 계산하면 그게 곧 두 번째 진실이 된다.
        /// 카메라를 못 구하는 경로(헤드리스/초기화 직후)에서는 프로젝트 기준 상수로 폴백한다.
        /// </summary>
        private float ResolveWorldPerPoint()
        {
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            Camera cam = bb != null ? bb.MainCamera : null;
            if (cam == null || bb.Body == null) return DockGeometry.ReferenceWorldUnitsPerPoint;

            _ = ScreenCoordinateConverter.WorldToOsScreen(cam, bb.Body.position, bb.Config, out float depth);
            Vector3 a = ScreenCoordinateConverter.OsScreenToWorld(cam, Vector2.zero, depth, bb.Config);
            Vector3 b = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(1f, 0f), depth, bb.Config);
            float perPoint = Mathf.Abs(b.x - a.x);
            return perPoint > 1e-6f ? perPoint : DockGeometry.ReferenceWorldUnitsPerPoint;
        }

        // ==================== 도형 ====================

        /// <summary>
        /// 도형을 다시 구울지 판단한다. <b>facing은 미니어처에만</b> 섞는다 — 공/비행기/커서 친구는
        /// 좌우 대칭이거나 진행 방향과 무관해서, 캐릭터가 돌 때마다 GameObject를 부수고 다시 만들면
        /// 그림은 똑같은데 비용만 든다(하루 종일 켜져 있는 앱에서 방향 전환은 수 초마다 일어난다).
        /// </summary>
        /// <summary>지금 펫을 칠하는 두 색(<see cref="EnsureBuilt"/>가 굽기 직전에 한 번 푼다).</summary>
        private Color _primary = Color.black;
        private Color _secondary = Color.black;

        private void EnsureBuilt(int item)
        {
            int signature = item * 397 + (ResolveInk().GetHashCode() & 0xFFFF) * 31
                + Mathf.RoundToInt(Height * 10000f)
                // 실제로 그려질 두께 — 화면상 하한에 걸리면 배율이 그대로여도 달라진다(창 크기/DPI).
                + Mathf.RoundToInt(RenderStroke * 10000f) * 7
                // 비대칭 도형만 방향을 서명에 섞는다 — 공/비행기/커서/풍선은 좌우가 같아서
                // 캐릭터가 돌 때마다 다시 구우면 그림은 똑같은데 비용만 든다(24시간 상주 앱).
                + ((item == PetMini || item == PetSnail) && FacingSign < 0f ? 1 : 0);
            if (_builtItem == item && signature == _builtSignature && _container != null) return;

            // ★ 위치는 유지한 채 그림만 다시 굽는다. Teardown()을 쓰면 _hasPosition이 초기화되어
            //   잉크색을 바꾸거나 방향을 트는 순간 펫이 주인 발밑으로 **순간이동**한다.
            DestroyVisuals();
            _builtItem = item;
            _builtSignature = signature;

            // ★ 2026-08-30 — 펫은 <b>물건</b>이라 자기 색을 갖는다(빨간 공, 종이 비행기).
            //   색표는 Core/ItemCatalog 하나뿐이고 여기서는 그 값을 받아 칠하기만 한다.
            //   "리틀스틱메이트"만은 카탈로그가 잉크 표식색을 들고 있어 자동으로 캐릭터 잉크색이 된다.
            ItemCatalog.ResolveWornPalette(EquipmentSlot.Pet, item, ResolveInk(), out _primary, out _secondary);

            _container = new GameObject("CharacterPet");
            _container.transform.SetParent(null, false);   // 독립 개체 — 캐릭터의 자식이 아니다.
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(_container.transform, false);
            _body = bodyGo.transform;

            switch (item)
            {
                case PetBall: BuildBall(); break;
                case PetPlane: BuildPlane(); break;
                case PetMini: BuildMini(); break;
                case PetCursor: BuildCursorFriend(); break;
                case PetBalloon: BuildBalloon(); break;
                case PetSnail: BuildSnail(); break;
            }
        }

        private void BuildBall()
        {
            float radius = Height * BallRadiusInHeight;
            // 반지름 선이 없으면 원이 아무리 굴러도 정지해 보인다 — 회전을 읽히게 하는 유일한 요소.
            _lines = new[]
            {
                MakeLine("BallRing", AppearanceShapeBuilder.BallRing(radius, BallSegments), true, SortDefault, _primary),
                MakeLine("BallSpoke", AppearanceShapeBuilder.BallSpoke(radius), false, SortDefault, _secondary),
            };
        }

        private void BuildPlane()
        {
            float w = HeadRadius * PlaneWingSpanInR;
            // 외곽 4점 닫힌 선 + 접힘선 3점(icon-paths.json의 종이비행기 실루엣).
            _lines = new[]
            {
                MakeLine("PlaneBody", AppearanceShapeBuilder.PlaneBody(w), true, SortDefault, _primary),
                MakeLine("PlaneFold", AppearanceShapeBuilder.PlaneFold(w), false, SortDefault, _secondary),
            };
        }

        /// <summary>
        /// 5선 미니 졸라맨(머리 원 + 몸통 + 팔 2 + 다리 2). <see cref="StickmanPoseAnimator"/>를
        /// 재사용하지 <b>않는다</b> — 그건 Rigidbody 리그 전용이다. 참고 패턴은
        /// <see cref="CharacterPortraitStage"/>의 미니 피규어(관절 각도만으로 선을 굽는 순수 계산)다.
        /// </summary>
        private void BuildMini()
        {
            Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(Height * MiniScale, FacingSign);
            // 이름 6개는 순서 계약을 사람이 읽을 수 있게 남긴다(팔 2 + 다리 2가 마지막 넷 — ApplyMiniPose).
            _lines = new[]
            {
                MakeLine("MiniHead", parts[0], true, SortDefault, _primary),
                MakeLine("MiniTorso", parts[1], false, SortDefault, _primary),
                MakeLine("MiniArmBack", parts[2], false, SortDefault, _primary),
                MakeLine("MiniArmFront", parts[3], false, SortDefault, _primary),
                MakeLine("MiniLegBack", parts[4], false, SortDefault, _primary),
                MakeLine("MiniLegFront", parts[5], false, SortDefault, _primary),
            };

            // 마디 4개의 기본 각도를 <b>구워진 점에서 실측</b>해 캐시한다. 상수로 다시 적지 않는 이유:
            // 도형(AppearanceShapeBuilder.MiniFigure)이 바뀌면 자세 계산이 조용히 어긋나기 때문이다.
            // 좌우 반전은 도형 재구성으로 처리하므로 facing이 바뀌면 EnsureBuilt가 여기를 다시 지나간다.
            _miniLimbNeutral = new float[4];
            for (int i = 0; i < 4; i++) _miniLimbNeutral[i] = LimbNeutralDegrees(_lines[i + 2]);
        }

        /// <summary>마디의 기본 각도(도, 0 = 곧게 아래, + = +x 쪽). <see cref="MakeLine"/>이 뿌리를
        /// 오브젝트 위치로 옮기고 점을 상대 좌표로 다시 적어 두었으므로, 마지막 점이 곧 뿌리에서 본
        /// 끝점 벡터다.</summary>
        private static float LimbNeutralDegrees(LineRenderer lr)
        {
            if (lr == null || lr.positionCount < 2) return 0f;
            Vector3 tip = lr.GetPosition(lr.positionCount - 1);
            return Mathf.Atan2(tip.x, -tip.y) * Mathf.Rad2Deg;
        }

        /// <summary>뿌리(0번 점)를 축으로 돈다. <see cref="MakeLine"/>이 뿌리를 오브젝트 위치로 옮기고
        /// 점을 상대 좌표로 다시 적어 두었기 때문에 Transform 회전만으로 성립한다(선을 다시 굽지 않는다).</summary>
        private static void RotateLimb(LineRenderer lr, float degrees)
        {
            if (lr == null) return;
            lr.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private void BuildCursorFriend()
        {
            float s = HeadRadius * CursorSizeInR;
            // 화살표 커서 실루엣(닫힌 선 8점) — icon-paths.json의 그 모양.
            _lines = new[]
            {
                MakeLine("CursorFriend", AppearanceShapeBuilder.CursorArrow(s), false, SortCursorFriend, _primary),
            };
        }

        /// <summary>풍선 — 끈(보조색) + 주머니(주색). 원점은 <b>끈이 묶인 자리</b>라
        /// <see cref="_body"/>를 돌리면 매달린 것처럼 흔들린다.</summary>
        private void BuildBalloon()
        {
            float r = HeadRadius;
            _lines = new[]
            {
                MakeLine("BalloonString", AppearanceShapeBuilder.BalloonString(r), false, SortBalloon, _secondary),
                MakeLine("BalloonBody", AppearanceShapeBuilder.BalloonBody(r), true, SortBalloon, _primary),
            };
        }

        /// <summary>달팽이 — 발+더듬이 한 획(주색) / 껍데기 링(주색) / 껍데기 속 점(보조색).
        /// 원점은 <b>땅에 닿는 자리</b>이고, 좌우 반전은 도형을 다시 구워서 처리한다.</summary>
        private void BuildSnail()
        {
            float size = HeadRadius * SnailSizeInR;
            float facing = FacingSign;
            _lines = new[]
            {
                MakeLine("SnailFoot", AppearanceShapeBuilder.SnailFoot(size, facing), false, SortDefault, _primary),
                MakeLine("SnailShell", AppearanceShapeBuilder.SnailShell(size, facing, SnailShellSegments),
                    true, SortDefault, _primary),
                MakeLine("SnailShellCore", AppearanceShapeBuilder.SnailShellCore(size, facing, SnailCoreSegments),
                    true, SortDefault, _secondary),
            };
        }

        private LineRenderer MakeLine(string name, Vector3[] points, bool loop, int sortingOrder, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_body, false);
            // 다리 스윙은 뿌리를 축으로 돈다 — 뿌리를 오브젝트 위치로 옮기고 점을 상대 좌표로 다시 적는다.
            Vector3 pivot = points.Length > 0 ? points[0] : Vector3.zero;
            go.transform.localPosition = pivot;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = ResolveLineMaterial();
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = sortingOrder;
            lr.startWidth = RenderStroke;
            lr.endWidth = RenderStroke;
            lr.loop = loop;
            lr.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) lr.SetPosition(i, points[i] - pivot);
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        private void SetSortingOrder(int order)
        {
            if (_lines == null) return;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null && _lines[i].sortingOrder != order) _lines[i].sortingOrder = order;
            }
        }

        private void ApplyAlpha()
        {
            if (_lines == null) return;
            for (int i = 0; i < _lines.Length; i++)
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

        /// <summary>
        /// <see cref="ICharacterVisualSource"/> — 지금 그리는 펫 선을 단일 창구에 신고한다.
        ///
        /// <para><see cref="CharacterVisualAnchor.Detached"/>인 이유: 펫 컨테이너는 캐릭터의 자식이
        /// <b>아니고</b>(독립 GameObject) 커서 친구는 커서까지 따라간다. 이걸 몸의 시각 반폭에 넣으면
        /// 펫이 화면 끝에 갈 때마다 캐릭터가 "내가 그만큼 넓다"고 오판해 안쪽으로 밀린다.
        /// 숨김/획 두께 하한에는 물론 포함된다.</para>
        /// </summary>
        public void CollectVisuals(CharacterVisualRegistry sink)
        {
            if (sink == null || _container == null) return;
            sink.AddRange(_lines, CharacterVisualAnchor.Detached);
        }

        /// <summary>그림만 지운다(위치/굴린 각도 같은 진행 상태는 유지).</summary>
        private void DestroyVisuals()
        {
            if (_container != null) Destroy(_container);
            _container = null;
            _body = null;
            _lines = null;
            _miniLimbNeutral = null;   // 선과 수명이 같다(다시 구우면 다시 실측한다).
            _builtItem = -1;
            _builtSignature = -1;
        }

        /// <summary>펫이 <b>사라진다</b>(해제/은신/컴포넌트 종료). 다음에 다시 나타날 때는 주인 옆에서 시작한다.</summary>
        private void Teardown()
        {
            DestroyVisuals();
            _hasPosition = false;
            _hasGroundY = false;
            _lastGroundHandle = 0L;
            _hasCursor = false;
            _miniAir01 = 0f;
            _miniTumble01 = 0f;
            _miniCrouch = 0f;
            _miniSpinDegrees = 0f;
        }

        // ==================== 치수/재료 ====================

        private float Height => _metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        private float HeadRadius => _metrics != null ? _metrics.HeadRadius : 0.22f;
        private float HeadCenterLocalY => _metrics != null ? _metrics.HeadCenterLocalY : Height - HeadRadius;

        /// <summary>고관절의 로컬 Y(발바닥 기준) — 상체 기울임의 <b>회전 중심</b>.
        /// 폴백 비율은 <see cref="OwnerFallbackHipRatio"/>와 같은 출처다.</summary>
        private float HipLocalY => _metrics != null && _metrics.HipLocalY > 0.0001f
            ? _metrics.HipLocalY
            : Height * OwnerFallbackHipRatio;

        /// <summary>
        /// ★ 2026-09-01 — <b>기울임이 반영된</b> 머리 기준 월드 좌표(교차 레이어 항목 #22).
        ///
        /// <para>예전 식은 <c>Body.position + (0, HeadCenterLocalY)</c>, 즉 <b>중립(기울지 않은)</b>
        /// 머리였다. States/StickmanPoseAnimator.SetBodyLean이 들어오면서 걷는 동안 머리는 엉덩이를
        /// 축으로 앞으로 나가는데, 종이비행기의 궤도 중심만 제자리에 남았다.</para>
        ///
        /// <para>액세서리 렌더러(클래스 문서 3-2)와 <b>같은 방법</b>이다: 같은 피벗(엉덩이)으로 같은
        /// 각도만큼 돌린다. 각도는 새로 계산하지 않고 Torso의 localRotation을 읽는다 — 포즈가 실제로
        /// 적용한 값이 유일한 진실이다. 기울임이 0이면 결과가 예전 식과 정확히 같다.</para>
        ///
        /// <para><paramref name="extraAboveHead"/>도 함께 돈다: 머리 위에 매달린 것(궤도 중심/풍선
        /// 매듭)은 머리가 기울면 같이 기우는 편이 자연스럽다.</para>
        /// </summary>
        private Vector2 LeanedHeadWorld(StickmanBlackboard bb, float extraAboveHead)
        {
            Vector2 foot = bb.Body.position;
            var neutral = new Vector2(0f, HeadCenterLocalY + extraAboveHead);
            Quaternion rot = _torsoTransform != null ? _torsoTransform.localRotation : Quaternion.identity;
            if (rot == Quaternion.identity) return foot + neutral;

            var hip = new Vector2(0f, HipLocalY);
            return foot + hip + (Vector2)(rot * (neutral - hip));
        }

        /// <summary>펫 획의 <b>비례 두께</b>(월드 유닛). 도형 유도는 이 값을 쓴다 — 배율에 정확히
        /// 비례해야 회귀 테스트(배율 비례 단언)와 그림체가 함께 성립한다.</summary>
        private float Stroke => Height * StrokeRatio;

        /// <summary>
        /// ★ 실제로 <b>그려지는</b> 두께 — 화면상 최소 두께(<see cref="StickConfig.MinStrokeScreenPoints"/>)
        /// 아래로 내려가지 않는다(2026-08-31).
        ///
        /// <para>왜 <see cref="Stroke"/>와 나누는가: 몸이 쓰는 규칙과 같게 하기 위해서다
        /// (Core/StickmanAgent.ApplyStrokeWidthsForScale도 <b>도형은 그대로 두고 LineRenderer 두께만</b>
        /// 하한으로 올린다). 도형 좌표까지 하한을 태우면 낮은 배율에서 펫의 <b>모양</b>이 달라진다.</para>
        ///
        /// <para>하한이 없던 시절의 실측: 출하 기본 배율 0.75에서 1.54pt, 다이얼 최소값 0.35에서
        /// 0.72pt — 하한(2pt)의 1/3이자 몸 획의 1/6이라 방울/눈 같은 작은 획이 안티에일리어싱에
        /// 묻혔다. 하한 값의 단일 소스는 <see cref="StickmanAgent.MinStrokeWorldWidth"/>다.</para>
        /// </summary>
        private float RenderStroke => Mathf.Max(Stroke, MinStrokeWorld);

        private float MinStrokeWorld => _agent != null
            ? _agent.MinStrokeWorldWidth
            : StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        private float FacingSign
        {
            get
            {
                StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
                return bb != null && bb.FacingSign < 0f ? -1f : 1f;
            }
        }

        private Color ResolveInk()
        {
            StickConfig config = _agent != null ? _agent.Config : null;
            return config != null ? config.ResolveInkColor() : Color.black;
        }

        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            _lineMaterial = source != null ? source.sharedMaterial : null;
            return _lineMaterial;
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
