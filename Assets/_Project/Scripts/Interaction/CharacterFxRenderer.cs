using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 외형 이펙트(FX 슬롯) 4종 — 2026-08-30 외부 디자인 핸드오프(docs/UX_FLOW.md 33-5).
    /// 발자국 / 반짝임 / 먼지 구름, 그리고 "없음"(명시적 OFF).
    ///
    /// ============================================================================
    /// 참고 패턴을 그대로 따른다 — Interaction/LandingDustRenderer.cs
    /// ============================================================================
    /// · 같은 GameObject의 <see cref="StickmanAgent"/>가 없으면 <b>아무것도 하지 않는다</b>.
    ///   이 프리팹이 복제되면 사본도 이 컴포넌트를 함께 갖게 되는데, 씬 전체 폴백을 두면
    ///   사본이 같은 전역 상태를 읽어 이펙트가 두 벌 그려진다(이 프로젝트가 실측으로 겪은 사고).
    /// · 이펙트는 <b>월드 좌표에 고정</b>하고 캐릭터를 따라다니지 않는다(땅에 남는 것이므로).
    /// · <b>콜라이더 0개</b> — 이펙트가 떠 있는 동안에도 그 자리의 다른 앱은 평소대로 클릭된다
    ///   (절대 불변 원칙 2·3).
    /// · <see cref="OnDisable"/>에서 반드시 정리 — 화면에 영구 잔류 금지.
    ///
    /// ============================================================================
    /// ★ 착지 먼지(LandingDustRenderer)와 <b>겸용하지 않는다</b> (33-5절 명시 규칙)
    /// ============================================================================
    /// 착지 먼지는 FX 슬롯과 <b>무관하게 항상 켜져 있는 기본 연출</b>이다(StickConfig.landingDustEnabled).
    /// 여기 "먼지 구름"은 그것과 다른 트리거(달리기/도약)를 쓰는 별개 컴포넌트다. 한 컴포넌트를
    /// 두 소비자가 공유하면 <b>FX를 껐을 때 착지 먼지까지 함께 꺼진다</b> — 유저가 요청하지 않은
    /// 기능이 조용히 사라지는 것이므로 코드를 아끼자고 할 일이 아니다.
    /// 다만 두 그림이 같은 자리에 겹쳐 "먼지가 두 배"로 보이는 것은 막는다:
    /// 착지 먼지가 떠 있는 동안에는 먼지 구름 발동을 <b>억제</b>한다.
    ///
    /// ============================================================================
    /// GameObject를 다시 만들지 않는다
    /// ============================================================================
    /// 하루 종일 켜져 있는 앱이라 발자국 12개 / 반짝임 2개 / 먼지 2회분을 <b>원형 버퍼</b>로 미리
    /// 만들어 두고 재사용한다(수명이 끝나면 비활성이 아니라 알파 0으로 두고 다음 발동에서 되살린다).
    /// </summary>
    public sealed class CharacterFxRenderer : MonoBehaviour
    {
        // ---- 아이템 자리 / 공용 치수는 Interaction/AppearanceShapeBuilder.cs가 소유한다
        //      (초상화 미리보기가 같은 값을 읽어야 "미리보기"가 성립한다).
        private const int FxNone = AppearanceShapeBuilder.FxNone;
        private const int FxFootprint = AppearanceShapeBuilder.FxFootprint;
        private const int FxSparkle = AppearanceShapeBuilder.FxSparkle;
        private const int FxDust = AppearanceShapeBuilder.FxDust;

        // ---- 레이어. 발자국은 땅에 남는 것이므로 캐릭터 획(최솟값 0)보다 확실히 뒤(−2).
        //      33-5절 표는 3으로 적었지만 프리팹 실측상 3은 앞쪽 팔다리(2)보다 앞이라
        //      "캐릭터 뒤"라는 그 표의 목적과 어긋난다(AccessoryShapeBuilder.SortBack 문서와 같은 근거).
        private const int SortFootprint = -2;
        private const int SortAerial = 6;

        // ---- 33-5절이 못박은 수치 ----
        private const float FootprintStrideRatio = 0.30f;    // 신장 배수 = 보폭 1회
        private const float FootprintLifeSeconds = 2.4f;
        private const float FootprintFadeSeconds = 0.8f;
        private const int FootprintCapacity = 12;
        private const float FootprintBackFootRatio = 0.08f;  // 진행 반대쪽 발 위치(신장 배수)

        private const float SparkleIdleArmSeconds = 3.0f;
        private const float SparkleIntervalMin = 6f;
        private const float SparkleIntervalMax = 10f;
        private const float SparkleLifeSeconds = 0.7f;
        /// <summary>머리 <b>중심</b> 위 R 배수(= 정수리에서 R·0.3 위).</summary>
        private const float SparkleHeightInR = 1.3f;
        private const float SparkleSpreadInR = 0.9f;
        private const float SparkleArmInR = AppearanceShapeBuilder.SparkleArmInR;
        private const int SparkleCapacity = 2;

        private const float DustSpeedGate = 0.85f;           // walkSpeed 배수
        private const float DustIntervalSeconds = 0.45f;
        private const float DustLifeSeconds = 0.5f;
        private const float DustBehindInR = 0.6f;
        private const int DustCapacity = 2;

        /// <summary>획 두께 — 착지 먼지와 같은 비율(같은 그림체를 유지한다).</summary>
        private const float StrokeRatio = 0.022f;

        private sealed class Puff
        {
            /// <summary>월드 좌표에 고정되는 자리(발동 순간의 발밑/머리 위). 수명 동안 움직이지 않는다.</summary>
            public Transform Root;

            /// <summary>확산/부양/스케일만 담당하는 자식. Root와 나누지 않으면 드리프트를 계산하는 순간
            /// 월드 고정 좌표를 덮어써 이펙트가 원점으로 순간이동한다(첫 구현에서 실제로 그랬다).</summary>
            public Transform Pivot;

            public LineRenderer[] Lines;
            public float Age;
            public float Life;
            public bool Alive;
            public Vector2 Drift;      // 수명 동안 밀려나는 거리(월드 유닛)
            public float StartScale;
            public float EndScale;
        }

        private StickmanAgent _agent;
        private StickmanMetrics _metrics;
        private LineRenderer _headOutline;
        private Material _lineMaterial;
        private GameObject _container;

        private Puff[] _footprints;
        private int _footprintCursor;
        private float _lastFootprintX;
        private bool _hasFootprint;

        private Puff[] _sparkles;
        private int _sparkleCursor;
        private float _idleSeconds;
        private float _nextSparkleIn;

        private Puff[] _dusts;
        private int _dustCursor;
        private float _dustCooldown;

        private StickmanStateId _lastStateId = StickmanStateId.Idle;
        private LandingDustRenderer _landingDust;

        /// <summary>테스트/진단용 — 지금 살아 있는 이펙트 조각 수.</summary>
        public int LiveEffectCount
        {
            get
            {
                int n = 0;
                n += CountAlive(_footprints);
                n += CountAlive(_sparkles);
                n += CountAlive(_dusts);
                return n;
            }
        }

        /// <summary>테스트/진단용 — 지금 착용 중인 FX 아이템 자리(미착용/잠김이면 -1).</summary>
        public int ActiveFxItemIndex => ResolveActiveItem();

        /// <summary>
        /// 테스트/진단용 — 살아 있는 조각 중 <b>지금 잉크색과 다른 색</b>으로 칠해진 것의 수.
        /// 0이 아니면 조각이 잉크색 전환을 따라오지 못한 것이다(R2 M2 회귀 창구). 플래그가 아니라
        /// 실제 <see cref="LineRenderer.startColor"/>를 읽는다.
        /// </summary>
        public int StaleInkPieceCount
        {
            get
            {
                Color ink = ResolveInk();
                int n = 0;
                n += CountStaleInk(_footprints, ink);
                n += CountStaleInk(_sparkles, ink);
                n += CountStaleInk(_dusts, ink);
                return n;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _metrics = StickmanMetrics.Find(this);
            _landingDust = GetComponent<LandingDustRenderer>();

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

        private void OnDisable() => Teardown();

        private void OnDestroy() => Teardown();

        private void LateUpdate()
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 아무것도 하지 않는다.

            float dt = Time.deltaTime;
            TickLifetimes(dt);

            StickmanStateId state = CurrentState();
            StickmanStateId previous = _lastStateId;
            // ★ 어떤 경로로 빠져나가든 직전 상태는 항상 갱신한다. 조기 반환 안쪽에 두면 FX를 껐다 켜는
            //   사이에 값이 낡아, 켜는 순간 "점프 진입 프레임"으로 오인해 먼지가 한 번 헛 터진다.
            _lastStateId = state;

            if (!CanSpawn())
            {
                // 랙돌/던져짐/가출 은신/전체화면 감지 — 새로 만들지 않는다.
                // 떠 있던 것은 수명대로 페이드되어 사라진다(즉시 지우면 깜빡인다).
                _idleSeconds = 0f;
                return;
            }

            int item = ResolveActiveItem();
            if (item <= FxNone) { _idleSeconds = 0f; return; }

            switch (item)
            {
                case FxFootprint: TickFootprints(state); break;
                case FxSparkle: TickSparkle(state, dt); break;
                case FxDust: TickDust(state, dt, previous); break;
            }
        }

        // ==================== 발동 조건 ====================

        /// <summary>
        /// 새 이펙트를 만들어도 되는가. 상태 목록을 새로 만들지 않고 <b>머리 링이 지금 켜져 있는가</b>를
        /// 따라간다 — 가출 은신이든 전체화면 자동 숨김이든 앞으로 생길 새 경로든, 캐릭터가 사라지면
        /// 자동으로 함께 멈추는 유일한 규칙이기 때문이다(액세서리 렌더러와 같은 규약).
        /// </summary>
        private bool CanSpawn()
        {
            if (_headOutline != null && !_headOutline.enabled) return false;
            StickmanStateId id = CurrentState();
            return id != StickmanStateId.Ragdoll && id != StickmanStateId.ThrowTumble;
        }

        private StickmanStateId CurrentState()
        {
            StickmanBlackboard bb = _agent != null ? _agent.Blackboard : null;
            return bb != null && bb.Machine != null ? bb.Machine.CurrentStateId : StickmanStateId.Idle;
        }

        /// <summary>착용 중이고 지금 레벨에서 보유한 FX 아이템 자리. 없으면 -1.</summary>
        private int ResolveActiveItem()
        {
            if (!EquipmentModel.IsEquipped(EquipmentSlot.Fx)) return -1;
            if (!EquipmentModel.IsUnlocked(EquipmentSlot.Fx)) return -1;
            return EquipmentModel.WornIndex(EquipmentSlot.Fx);
        }

        /// <summary>
        /// 발자국/먼지가 찍힐 바닥의 월드 Y — <b>주인이 지금 딛고 있는 발판</b>의 상단.
        ///
        /// ★ 2026-08-31 — 펫이 최대화된 창 위로 올라가던 버그(Interaction/CharacterPetRenderer.cs
        /// ResolveGroundY 문서)와 <b>같은 원인</b>이 여기에도 있었다. 예전에는
        /// <c>TryGetGroundSurfaceWorldY(= 그 x에서 가장 높은 발판 상단)</c>를 물었기 때문에, 창을 하나만
        /// 최대화해도 캐릭터는 Dock 위를 걷는데 발자국과 먼지는 <b>화면 꼭대기</b>에 찍혔다. 이 프로젝트가
        /// 반복해 온 실패 유형("같은 API 오용을 한 곳만 고치기")이라 펫과 같은 라운드에서 함께 고친다.
        /// </summary>
        private static float ResolveOwnerGroundWorldY(StickmanBlackboard bb)
        {
            long handle = bb.CurrentFootholdHandle;
            if (handle != 0L && bb.TryGetFootholdTopWorldY(handle, out float topY)) return topY;
            // 발판이 아직 확정되지 않았거나(최초 접지 전) 그 창이 사라진 프레임 — 루트가 곧 발바닥이므로
            // 주인의 y가 지면에 가장 가까운 답이다. 어떤 경우에도 "화면 꼭대기"가 나올 수는 없다.
            return bb.Body.position.y;
        }

        private void TickFootprints(StickmanStateId state)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;
            if (state != StickmanStateId.Walk) { _hasFootprint = false; return; }

            float h = Height;
            float x = bb.Body.position.x;
            if (_hasFootprint && Mathf.Abs(x - _lastFootprintX) < h * FootprintStrideRatio) return;

            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            float printX = x - facing * h * FootprintBackFootRatio;
            float surfaceY = ResolveOwnerGroundWorldY(bb);

            _lastFootprintX = x;
            _hasFootprint = true;

            Puff p = Take(ref _footprints, ref _footprintCursor, FootprintCapacity, "Footprint", SortFootprint, 1);
            if (p == null) return;
            BuildDot(p.Lines[0], Stroke * 0.9f);
            p.Root.position = new Vector3(printX, surfaceY, 0f);
            Revive(p, FootprintLifeSeconds, Vector2.zero, 1f, 1f);
        }

        private void TickSparkle(StickmanStateId state, float dt)
        {
            if (state != StickmanStateId.Idle) { _idleSeconds = 0f; _nextSparkleIn = 0f; return; }

            _idleSeconds += dt;
            if (_idleSeconds < SparkleIdleArmSeconds) return;

            if (_nextSparkleIn > 0f)
            {
                _nextSparkleIn -= dt;
                if (_nextSparkleIn > 0f) return;
            }

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            // 머리 중심 위 R·1.3(= 정수리에서 R·0.3 위), 좌우로 ±R·0.9 범위.
            float r = HeadRadius;
            float cx = bb.Body.position.x + Random.Range(-SparkleSpreadInR, SparkleSpreadInR) * r;
            float cy = bb.Body.position.y + HeadCenterLocalY + r * SparkleHeightInR;

            Puff p = Take(ref _sparkles, ref _sparkleCursor, SparkleCapacity, "Sparkle", SortAerial, 2);
            if (p == null) return;
            BuildCross(p.Lines, r * SparkleArmInR);
            p.Root.position = new Vector3(cx, cy, 0f);
            Revive(p, SparkleLifeSeconds, Vector2.zero, 0.2f, 1f);

            _nextSparkleIn = Random.Range(SparkleIntervalMin, SparkleIntervalMax);
        }

        private void TickDust(StickmanStateId state, float dt, StickmanStateId previous)
        {
            if (_dustCooldown > 0f) _dustCooldown -= dt;

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            // 착지 먼지가 떠 있는 동안에는 억제한다(같은 자리에 같은 그림이 두 겹으로 겹치지 않게).
            if (_landingDust != null && _landingDust.IsVisible) return;

            bool jumpedThisFrame = state == StickmanStateId.Jump && previous != StickmanStateId.Jump;
            bool running = false;
            if (state == StickmanStateId.Walk)
            {
                StickConfig config = bb.Config;
                float walk = config != null ? config.ResolveWalkSpeed() : 1f;
                running = walk > 0.0001f && Mathf.Abs(bb.Body.linearVelocity.x) > walk * DustSpeedGate;
            }

            if (!jumpedThisFrame && (!running || _dustCooldown > 0f)) return;

            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            float r = HeadRadius;
            float px = bb.Body.position.x - facing * r * DustBehindInR;
            float surfaceY = ResolveOwnerGroundWorldY(bb);

            Puff p = Take(ref _dusts, ref _dustCursor, DustCapacity, "DustCloud", SortAerial, 2);
            if (p == null) return;
            BuildCrescents(p.Lines, r * 0.5f);
            p.Root.position = new Vector3(px, surfaceY + Stroke, 0f);
            // 뒤로 퍼지며 옅어진다.
            Revive(p, DustLifeSeconds, new Vector2(-facing * r * 0.9f, r * 0.25f), 0.5f, 1.25f);

            if (!jumpedThisFrame) _dustCooldown = DustIntervalSeconds;
        }

        // ==================== 수명 ====================

        private void TickLifetimes(float dt)
        {
            TickGroup(_footprints, dt, footprintFade: true);
            TickGroup(_sparkles, dt, footprintFade: false);
            TickGroup(_dusts, dt, footprintFade: false);
        }

        private void TickGroup(Puff[] group, float dt, bool footprintFade)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                Puff p = group[i];
                if (p == null || !p.Alive) continue;

                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Alive = false;
                    SetGroupAlpha(p.Lines, 0f);
                    continue;
                }

                float t = p.Age / p.Life;
                // 발자국은 "마지막 0.8초만 선형 페이드"(그 전에는 또렷하게 남아 있다).
                // 나머지는 0→1→0 산 모양(반짝임/먼지가 나타났다 사라지는 그림).
                float alpha = footprintFade
                    ? Mathf.Clamp01((p.Life - p.Age) / Mathf.Max(0.01f, FootprintFadeSeconds))
                    : Mathf.Sin(t * Mathf.PI);
                SetGroupAlpha(p.Lines, alpha);

                if (p.Pivot == null) continue;
                float ease = 1f - (1f - t) * (1f - t);
                float scale = footprintFade ? p.EndScale : Mathf.Lerp(p.StartScale, p.EndScale, Mathf.Sin(t * Mathf.PI));
                p.Pivot.localScale = new Vector3(scale, scale, 1f);
                p.Pivot.localPosition = new Vector3(p.Drift.x * ease, p.Drift.y * ease, 0f);
            }
        }

        // ==================== 조각 만들기/재사용 ====================

        private Puff Take(ref Puff[] group, ref int cursor, int capacity, string name, int sortingOrder, int lineCount)
        {
            EnsureContainer();
            if (_container == null) return null;

            if (group == null) group = new Puff[capacity];
            if (group[cursor] == null)
            {
                var holder = new GameObject(name);
                holder.transform.SetParent(_container.transform, false);
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(holder.transform, false);

                var lines = new LineRenderer[lineCount];
                for (int i = 0; i < lineCount; i++) lines[i] = CreateLine(pivot.transform, sortingOrder);
                // 위치는 월드 고정이라 holder를 직접 옮기고, 확산/부양은 Pivot에 준다.
                group[cursor] = new Puff { Root = holder.transform, Pivot = pivot.transform, Lines = lines };
            }

            Puff p = group[cursor];
            cursor = (cursor + 1) % capacity;
            return p;
        }

        private void Revive(Puff p, float life, Vector2 drift, float startScale, float endScale)
        {
            p.Age = 0f;
            p.Life = Mathf.Max(0.01f, life);
            p.Alive = true;
            p.Drift = drift;
            p.StartScale = startScale;
            p.EndScale = endScale;
            if (p.Pivot != null)
            {
                p.Pivot.localPosition = Vector3.zero;
                p.Pivot.localScale = new Vector3(startScale, startScale, 1f);
            }
            // 원형 버퍼가 이 조각을 앱 수명 내내 재사용한다 — 생성 시점 색에 머무르면 잉크색 전환을
            // 영영 못 따라간다(흰 잉크로 바꿔도 검은 발자국이 계속 찍혔다). 되살릴 때마다 다시 칠한다.
            SetGroupInk(p.Lines, ResolveInk());
            SetGroupAlpha(p.Lines, 0f);
        }

        private LineRenderer CreateLine(Transform parent, int sortingOrder)
        {
            var go = new GameObject("Ink");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = ResolveLineMaterial();
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = sortingOrder;
            lr.startWidth = Stroke;
            lr.endWidth = Stroke;
            Color ink = ResolveInk();
            ink.a = 0f;
            lr.startColor = ink;
            lr.endColor = ink;
            lr.positionCount = 0;
            return lr;
        }

        /// <summary>채운 점 하나 — 짧은 선을 굵은 캡으로 그리면 원이 된다(점 도형을 따로 만들지 않는다).
        /// 점 좌표는 Interaction/AppearanceShapeBuilder.cs가 소유한다(초상화 미리보기와 같은 그림).</summary>
        private static void BuildDot(LineRenderer lr, float radius)
        {
            if (lr == null) return;
            lr.loop = false;
            lr.startWidth = radius * 2f;
            lr.endWidth = radius * 2f;
            Vector3[] pts = AppearanceShapeBuilder.DotSegment(radius);
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        /// <summary>4갈래 반짝(십자 2획).</summary>
        private void BuildCross(LineRenderer[] lines, float arm)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null) continue;
                lr.loop = false;
                lr.startWidth = Stroke;
                lr.endWidth = Stroke;
                Vector3[] pts = AppearanceShapeBuilder.SparkleStroke(arm, i);
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
            }
        }

        /// <summary>초승달 2개 — 착지 먼지와 같은 어휘라 "먼지"로 바로 읽힌다.</summary>
        private void BuildCrescents(LineRenderer[] lines, float radius)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null) continue;
                lr.loop = false;
                lr.startWidth = Stroke;
                lr.endWidth = Stroke;
                Vector3[] pts = AppearanceShapeBuilder.DustCrescent(radius, i);
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
            }
        }

        /// <summary>RGB만 갈아끼운다 — 알파는 수명 곡선이 소유하므로 유지한다.</summary>
        private static void SetGroupInk(LineRenderer[] lines, Color ink)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null) continue;
                Color current = lr.startColor;
                if (current.r == ink.r && current.g == ink.g && current.b == ink.b) continue;
                Color next = ink;
                next.a = current.a;
                lr.startColor = next;
                lr.endColor = next;
            }
        }

        private static void SetGroupAlpha(LineRenderer[] lines, float alpha)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer lr = lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                if (Mathf.Approximately(c.a, alpha)) continue;
                c.a = alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        private static int CountStaleInk(Puff[] group, Color ink)
        {
            if (group == null) return 0;
            int n = 0;
            for (int i = 0; i < group.Length; i++)
            {
                Puff p = group[i];
                if (p == null || !p.Alive || p.Lines == null) continue;
                for (int k = 0; k < p.Lines.Length; k++)
                {
                    LineRenderer lr = p.Lines[k];
                    if (lr == null) continue;
                    Color c = lr.startColor;
                    if (Mathf.Approximately(c.r, ink.r) && Mathf.Approximately(c.g, ink.g) &&
                        Mathf.Approximately(c.b, ink.b)) continue;
                    n++;
                    break; // 조각 단위로 센다.
                }
            }
            return n;
        }

        private static int CountAlive(Puff[] group)
        {
            if (group == null) return 0;
            int n = 0;
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null && group[i].Alive) n++;
            }
            return n;
        }

        private void EnsureContainer()
        {
            if (_container != null) return;
            _container = new GameObject("CharacterFx");
            // 월드 고정 — 캐릭터를 따라다니지 않는다(땅에 남는 것이므로).
            _container.transform.SetParent(null, false);
        }

        private void Teardown()
        {
            if (_container != null) Destroy(_container);
            _container = null;
            _footprints = null;
            _sparkles = null;
            _dusts = null;
            _footprintCursor = 0;
            _sparkleCursor = 0;
            _dustCursor = 0;
            _hasFootprint = false;
            _idleSeconds = 0f;
            _nextSparkleIn = 0f;
            _dustCooldown = 0f;
        }

        // ==================== 치수/재료 ====================

        private float Height => _metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        private float HeadRadius => _metrics != null ? _metrics.HeadRadius
            : Height * (AccessoryShapeBuilder.BaselineHeadVisualRadius / StickConfig.BaselineCharacterTotalHeight);
        private float HeadCenterLocalY => _metrics != null ? _metrics.HeadCenterLocalY : Height - HeadRadius;
        private float Stroke => Height * StrokeRatio;

        private Color ResolveInk()
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
