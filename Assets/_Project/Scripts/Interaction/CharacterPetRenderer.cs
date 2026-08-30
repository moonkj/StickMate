using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 펫(PET 슬롯) 4종 — 2026-08-30 외부 디자인 핸드오프(docs/UX_FLOW.md 33-6).
    /// 작은 공 / 종이비행기 / 작은 졸라맨 / 커서 친구.
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
    public sealed class CharacterPetRenderer : MonoBehaviour
    {
        // ---- 아이템 자리 / 공용 치수는 Interaction/AppearanceShapeBuilder.cs가 소유한다
        //      (초상화 미리보기가 같은 값을 읽어야 "미리보기"가 성립한다).
        private const int PetBall = AppearanceShapeBuilder.PetBall;
        private const int PetPlane = AppearanceShapeBuilder.PetPlane;
        private const int PetMini = AppearanceShapeBuilder.PetMini;
        private const int PetCursor = AppearanceShapeBuilder.PetCursor;

        // ---- 레이어(33-6). 종이비행기만 반주기마다 4 <-> 10을 오간다.
        private const int SortDefault = 4;
        private const int SortPlaneFront = 10;
        private const int SortCursorFriend = 11;

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

        /// <summary>보이기/숨기기 알파 전환 시간(가출 은신 / 전체화면 감지로 캐릭터가 사라질 때).</summary>
        private const float FadeSeconds = 0.25f;
        private const float StrokeRatio = 0.022f;

        private StickmanAgent _agent;
        private StickmanMetrics _metrics;
        private LineRenderer _headOutline;
        private Material _lineMaterial;

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
        private float _orbitPhase;
        private float _legPhase;
        private Vector2 _cursorVelocity;
        private Vector2 _lastCursor;
        private bool _hasCursor;
        private float _alpha;

        /// <summary>테스트/진단용 — 지금 그려지고 있는 펫 자리(없으면 -1).</summary>
        public int ActivePetItemIndex => _builtItem;

        /// <summary>테스트/진단용 — 펫의 현재 월드 좌표.</summary>
        public Vector2 PetWorldPosition => _position;

        /// <summary>테스트/진단용 — 지금 알파(숨김 페이드 확인).</summary>
        public float Alpha => _alpha;

        /// <summary>테스트/진단용 — 작은 공이 지금까지 굴러온 회전각(도). 스폰 프레임에 수천 도가
        /// 튀지 않는지 확인하는 창구다(R2 m4).</summary>
        public float BallSpinDegrees => _ballAngleDegrees;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _metrics = StickmanMetrics.Find(this);

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
            bool visible = item >= 0 && IsCharacterVisible();

            float target = visible ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime / Mathf.Max(0.01f, FadeSeconds));

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

            _position.y = ResolveGroundY(bb, _position.x, bb.Body.position.y) + radius;
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
            float cx = bb.Body.position.x;
            float cy = bb.Body.position.y + HeadCenterLocalY + r * PlaneCenterAboveHeadInR;

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
        /// ③ 작은 졸라맨 — 미니어처가 따라온다. <b>다리는 실제로 이동 중일 때만 흔든다</b>:
        /// 멈췄는데 다리가 계속 움직이면 그게 바로 행동-그림 불일치(원칙 1의 그림 버전)다.
        /// </summary>
        private void TickMini(float dt)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            float h = Height;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            float previousX = _position.x;

            float targetX = bb.Body.position.x - facing * h * MiniTrailInHeight;
            if (!_hasPosition) { _position = new Vector2(targetX, bb.Body.position.y); _hasPosition = true; }
            _position.x = Mathf.Lerp(_position.x, targetX, 1f - Mathf.Exp(-MiniFollowRate * dt));
            _position.y = ResolveGroundY(bb, _position.x, bb.Body.position.y);
            ClampToScreen(ref _position, h * MiniScale * 0.5f);

            float speed = dt > 0.0001f ? Mathf.Abs(_position.x - previousX) / dt : 0f;
            bool moving = speed > h * MiniMovingSpeedGateInHeight;
            if (moving) _legPhase += dt * Mathf.PI * 2f / MiniLegSwingPeriod;
            float swing = moving ? Mathf.Sin(_legPhase) * MiniLegSwingDegrees : 0f;
            ApplyMiniLegSwing(swing);

            _body.position = new Vector3(_position.x, _position.y, 0f);
            _body.localRotation = Quaternion.identity;
            // 좌우 반전은 자식 회전이 아니라 도형 재구성으로 처리한다(localScale.x = -1 금지 규약).
            _body.localScale = Vector3.one;
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

        /// <summary>발판 표면 Y. 못 찾으면 <b>마지막으로 유효했던 값</b>을 유지한다(33-6-4) —
        /// 창이 사라져 발판을 놓친 순간 펫이 화면 밑으로 떨어지지 않게 한다.</summary>
        private float ResolveGroundY(StickmanBlackboard bb, float x, float probeY)
        {
            if (bb.TryGetGroundSurfaceWorldY(new Vector2(x, probeY), out float surfaceY))
            {
                _lastGroundY = surfaceY;
                _hasGroundY = true;
                return surfaceY;
            }
            return _hasGroundY ? _lastGroundY : probeY;
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
                + (item == PetMini && FacingSign < 0f ? 1 : 0);
            if (_builtItem == item && signature == _builtSignature && _container != null) return;

            // ★ 위치는 유지한 채 그림만 다시 굽는다. Teardown()을 쓰면 _hasPosition이 초기화되어
            //   잉크색을 바꾸거나 방향을 트는 순간 펫이 주인 발밑으로 **순간이동**한다.
            DestroyVisuals();
            _builtItem = item;
            _builtSignature = signature;

            // ★ 2026-08-30 — 펫은 <b>물건</b>이라 자기 색을 갖는다(빨간 공, 종이 비행기).
            //   색표는 Core/ItemCatalog 하나뿐이고 여기서는 그 값을 받아 칠하기만 한다.
            //   "작은 졸라맨"만은 카탈로그가 잉크 표식색을 들고 있어 자동으로 캐릭터 잉크색이 된다.
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
            // 이름 6개는 순서 계약을 사람이 읽을 수 있게 남긴다(다리 2개가 마지막 — ApplyMiniLegSwing).
            _lines = new[]
            {
                MakeLine("MiniHead", parts[0], true, SortDefault, _primary),
                MakeLine("MiniTorso", parts[1], false, SortDefault, _primary),
                MakeLine("MiniArmBack", parts[2], false, SortDefault, _primary),
                MakeLine("MiniArmFront", parts[3], false, SortDefault, _primary),
                MakeLine("MiniLegBack", parts[4], false, SortDefault, _primary),
                MakeLine("MiniLegFront", parts[5], false, SortDefault, _primary),
            };
        }

        /// <summary>다리 2개만 뿌리 기준으로 회전시킨다(도형을 다시 굽지 않는다).</summary>
        private void ApplyMiniLegSwing(float degrees)
        {
            if (_lines == null || _lines.Length < 6) return;
            RotateLimb(_lines[4], degrees);
            RotateLimb(_lines[5], -degrees);
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
            lr.startWidth = Stroke;
            lr.endWidth = Stroke;
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

        /// <summary>그림만 지운다(위치/굴린 각도 같은 진행 상태는 유지).</summary>
        private void DestroyVisuals()
        {
            if (_container != null) Destroy(_container);
            _container = null;
            _body = null;
            _lines = null;
            _builtItem = -1;
            _builtSignature = -1;
        }

        /// <summary>펫이 <b>사라진다</b>(해제/은신/컴포넌트 종료). 다음에 다시 나타날 때는 주인 옆에서 시작한다.</summary>
        private void Teardown()
        {
            DestroyVisuals();
            _hasPosition = false;
            _hasGroundY = false;
            _hasCursor = false;
        }

        // ==================== 치수/재료 ====================

        private float Height => _metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        private float HeadRadius => _metrics != null ? _metrics.HeadRadius : 0.22f;
        private float HeadCenterLocalY => _metrics != null ? _metrics.HeadCenterLocalY : Height - HeadRadius;
        private float Stroke => Height * StrokeRatio;

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
