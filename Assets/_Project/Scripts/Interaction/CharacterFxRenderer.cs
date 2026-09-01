using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 외형 이펙트(FX 슬롯) 6종 — 2026-08-30 외부 디자인 핸드오프(docs/UX_FLOW.md 33-5),
    /// 2026-09-01 <b>물방울/나뭇잎</b> 추가.
    /// "없음"(명시적 OFF) / 발자국 / 반짝임 / 먼지 구름 / 물방울 / 나뭇잎.
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — 물방울·나뭇잎은 "카드만 있고 화면엔 0픽셀"이던 자리다
    /// ============================================================================
    /// 카테고리당 +2종 라운드가 에셋(카드)만 만들고 연출을 비워 둔 채 설명문에 "준비 중인 자리"라고
    /// 적어 두었다(이 파일이 그 라운드의 소유가 아니었기 때문이다). 이 저장소의 확정 규칙은
    /// <b>"착용했는데 화면이 그대로면 그건 착용이 아니다"</b>이므로 이번 라운드에서 채웠다.
    /// 두 아이템 모두 기존 3종과 <b>같은 배관</b>을 쓴다(원형 버퍼 + 월드 고정 + 수명 곡선) —
    /// 새 컴포넌트도, 새 GameObject 생명주기도 만들지 않는다.
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
    public sealed class CharacterFxRenderer : MonoBehaviour, ICharacterVisualSource
    {
        // ---- 아이템 자리 / 공용 치수는 Interaction/AppearanceShapeBuilder.cs가 소유한다
        //      (초상화 미리보기가 같은 값을 읽어야 "미리보기"가 성립한다).
        private const int FxNone = AppearanceShapeBuilder.FxNone;
        private const int FxFootprint = AppearanceShapeBuilder.FxFootprint;
        private const int FxSparkle = AppearanceShapeBuilder.FxSparkle;
        private const int FxDust = AppearanceShapeBuilder.FxDust;
        private const int FxBubble = AppearanceShapeBuilder.FxBubble;
        private const int FxLeaf = AppearanceShapeBuilder.FxLeaf;

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

        // ★ 2026-09-01 (로드맵 P4) — 무장/대기/수명 상수를 여기서 <b>지웠다</b>.
        //   옛 값(무장 3초 + 대기 6~10초)은 배회 Idle 최대 지속시간(6초)을 모른 채 잡혀 있어
        //   2회차 반짝임이 원리적으로 오지 않았다. 이제 Interaction/SparkleCadence.cs가
        //   <b>Idle 창에서 유도</b>한다 — 배회 시간을 바꿔도 같은 버그가 재발하지 않는다.

        /// <summary>머리 <b>중심</b> 위 R 배수. 갈래가 0.34R -> 0.85R로 커지면서 함께 올렸다:
        /// 옛 값 1.3R 그대로였다면 십자 아래 갈래 끝이 0.45R, 즉 <b>머리 링 안쪽</b>에 박힌다.
        /// 지금은 아래 끝이 정수리(1.0R)보다 0.15R 위다.</summary>
        private const float SparkleHeightInR = 2.0f;
        private const float SparkleSpreadInR = 0.9f;

        /// <summary>수명 앞부분의 크기 배수. 옛 값 0.2는 <b>0.8pt짜리 티끌</b>로 시작한다는 뜻이라
        /// 수명의 앞쪽이 통째로 안 보였다(획 하한 2pt의 40%).</summary>
        private const float SparkleStartScale = 0.55f;
        private const float SparkleArmInR = AppearanceShapeBuilder.SparkleArmInR;
        private const int SparkleCapacity = 2;

        private const float DustSpeedGate = 0.85f;           // walkSpeed 배수
        private const float DustIntervalSeconds = 0.45f;
        private const float DustLifeSeconds = 0.5f;
        private const float DustBehindInR = 0.6f;
        private const int DustCapacity = 2;

        // ---- 물방울(2026-09-01). 걷는 동안 몸 옆에서 방울이 하나씩 떠올라 흩어진다.
        //      발동 조건을 Walk로 잡은 이유: 반짝임(Idle)/먼지(달리기·도약)와 <b>겹치지 않는 창</b>이
        //      필요하다. 세 이펙트가 같은 순간을 노리면 어느 것을 골라도 화면이 비슷해진다.
        private const float BubbleIntervalSeconds = 0.55f;
        private const float BubbleLifeSeconds = 1.5f;
        private const float BubbleRiseInR = 2.6f;            // 수명 동안 떠오르는 높이
        private const float BubbleSideInR = 0.5f;            // 진행 반대쪽으로 벗어나는 거리
        private const float BubbleStartScale = 0.90f;        // 지름 하한(3.0 W)을 이 배율까지 검산했다
        private const float BubbleEndScale = 1.15f;
        private const int BubbleCapacity = 3;

        // ---- 나뭇잎(2026-09-01). 머리 위에서 한 장씩 떨어져 <b>지면에 닿으면 사라진다</b>.
        //      발동은 상태와 무관한 앰비언트다(날씨에 가깝다) — 대신 낙하 <b>도착 높이</b>는
        //      주인이 딛고 있는 발판에서 가져와, 최대화된 창 위를 걷는 동안에도 잎이 허공에 멈추지 않는다.
        private const float LeafIntervalMinSeconds = 1.6f;
        private const float LeafIntervalMaxSeconds = 3.2f;
        private const float LeafLifeSeconds = 2.6f;
        private const float LeafFadeSeconds = 0.7f;          // 마지막 이만큼만 옅어진다(발자국과 같은 규칙)
        private const float LeafSpawnAboveHeadInR = 2.2f;
        private const float LeafSpawnSpreadInR = 1.1f;
        private const float LeafSwayInR = 0.9f;              // 팔랑임 좌우 폭
        private const float LeafSwayCycles = 1.5f;           // 수명 동안 좌우로 오가는 횟수
        private const float LeafSpinDegrees = 210f;          // 수명 동안 도는 각도(부호는 매번 무작위)
        private const int LeafCapacity = 3;

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

            /// <summary>좌우 팔랑임의 진폭(월드 유닛). 0이면 이 항이 통째로 빠진다 —
            /// 기존 3종은 0이라 <b>계산 결과가 예전과 한 톨도 다르지 않다</b>.</summary>
            public float SwayAmplitude;

            /// <summary>수명 동안 좌우로 오가는 횟수.</summary>
            public float SwayCycles;

            /// <summary>수명 동안 도는 각도(도). 0이면 회전을 건드리지 않는다.</summary>
            public float SpinDegrees;
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

        private Puff[] _bubbles;
        private int _bubbleCursor;
        private float _bubbleCooldown;

        private Puff[] _leaves;
        private int _leafCursor;
        private float _leafCooldown;

        private StickmanStateId _lastStateId = StickmanStateId.Idle;
        private LandingDustRenderer _landingDust;

        /// <summary>몸통 Transform — <b>회전만</b> 읽는다(아래 <see cref="LeanedHeadWorld"/>).
        /// 액세서리 렌더러와 같은 규약: 기울임 각도를 이 파일에서 새로 계산하지 않는다.</summary>
        private Transform _torsoTransform;

        /// <summary>테스트/진단용 — 지금 살아 있는 이펙트 조각 수.</summary>
        public int LiveEffectCount
        {
            get
            {
                int n = 0;
                n += CountAlive(_footprints);
                n += CountAlive(_sparkles);
                n += CountAlive(_dusts);
                n += CountAlive(_bubbles);
                n += CountAlive(_leaves);
                return n;
            }
        }

        /// <summary>테스트/진단용 — 지금 착용 중인 FX 아이템 자리(미착용/잠김이면 -1).</summary>
        public int ActiveFxItemIndex => ResolveActiveItem();

        /// <summary>
        /// 테스트/진단용 — 머리에 붙는 이펙트(반짝임·나뭇잎)가 <b>지금</b> 기준으로 삼는 월드 좌표.
        /// 다른 렌더러들이 "지금 어디에 그리려 하는가"를 계산 그대로 노출하는 것과 같은 관례다
        /// (RendererScaleRatioTests가 읽는 배치 프로퍼티들과 같은 성격).
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

        /// <summary>머리 중심에서 <see cref="HeadAnchorWorldPosition"/>까지의 높이(월드 유닛).
        /// 테스트가 "기울이지 않았다면 어디였을지"를 <see cref="StickmanMetrics"/>만으로 계산할 수 있게
        /// 열어 둔다 — 옛 식을 프로덕션 코드에 화석으로 남겨 두지 않기 위해서다.</summary>
        public float HeadAnchorAboveHeadCenter => HeadRadius * SparkleHeightInR;

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
                n += CountStaleInk(_bubbles, ink);
                n += CountStaleInk(_leaves, ink);
                return n;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _metrics = StickmanMetrics.Find(this);
            _landingDust = GetComponent<LandingDustRenderer>();
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

        private void OnDisable() => Teardown();

        private void OnDestroy() => Teardown();

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 아무것도 하지 않는다.

            float dt = Time.deltaTime;
            TickLifetimes(dt);

            // ★★ 2026-08-31 (원칙 2) — 캐릭터가 그 프레임에 사라졌으면 <b>이미 떠 있던 조각도</b>
            // 그 프레임에 사라진다. 예전에는 CanSpawn()이 false가 되어 "새로 만들지 않는다"에서
            // 멈췄고, 이미 떠 있던 발자국/반짝임/먼지는 자기 수명(최대 수 초)대로 계속 그려졌다 —
            // 사용자가 방금 켠 전체화면 게임 위에 주인 없는 반짝임이 남고, 가출 숨바꼭질에서는
            // 발자국이 숨은 자리를 그대로 가리켰다. 수명은 숨은 동안에도 계속 흐르게 둔다(위
            // TickLifetimes) — 그래야 재개했을 때 낡은 발자국이 되살아나지 않는다.
            if (!IsCharacterVisible())
            {
                if (!_hiddenApplied) { SetAllPiecesEnabled(false); _hiddenApplied = true; }
                _lastStateId = CurrentState();
                _idleSeconds = 0f;
                return;
            }
            if (_hiddenApplied) { SetAllPiecesEnabled(true); _hiddenApplied = false; }

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
                case FxBubble: TickBubbles(state, dt); break;
                case FxLeaf: TickLeaves(dt); break;
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
            if (!IsCharacterVisible()) return false;
            StickmanStateId id = CurrentState();
            return id != StickmanStateId.Ragdoll && id != StickmanStateId.ThrowTumble;
        }

        /// <summary>캐릭터가 지금 화면에 있는가. 상태 목록을 새로 만들지 않고 <b>머리 링이 지금 켜져
        /// 있는가</b>를 따라간다 — 가출 은신이든 전체화면 자동 숨김이든 앞으로 생길 새 경로든 자동으로
        /// 함께 따라오는 유일한 규칙이다(액세서리/펫 렌더러와 같은 규약).</summary>
        private bool IsCharacterVisible() => _headOutline == null || _headOutline.enabled;

        /// <summary>숨김을 이미 반영했는가(전이 프레임에만 렌더러를 훑기 위한 래치).</summary>
        private bool _hiddenApplied;

        /// <summary>풀에 있는 모든 조각의 <c>enabled</c>를 한 번에 바꾼다. GameObject 비활성화가 아니라
        /// <c>enabled</c>인 이유는 액세서리와 같다 — 이 앱의 "지금 보이는가" 판정이 전부
        /// <c>Renderer.enabled</c>를 읽는다.</summary>
        private void SetAllPiecesEnabled(bool on)
        {
            SetGroupEnabled(_footprints, on);
            SetGroupEnabled(_sparkles, on);
            SetGroupEnabled(_dusts, on);
            SetGroupEnabled(_bubbles, on);
            SetGroupEnabled(_leaves, on);
        }

        private static void SetGroupEnabled(Puff[] group, bool on)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                Puff p = group[i];
                if (p == null || p.Lines == null) continue;
                for (int k = 0; k < p.Lines.Length; k++)
                {
                    LineRenderer lr = p.Lines[k];
                    if (lr != null && lr.enabled != on) lr.enabled = on;
                }
            }
        }

        /// <summary>
        /// <see cref="ICharacterVisualSource"/> — 지금 살아 있는 이펙트 조각을 단일 창구에 신고한다.
        /// 컨테이너가 캐릭터의 자식이 아니고(월드 고정) 발자국은 지나온 자리에 남으므로
        /// <see cref="CharacterVisualAnchor.Detached"/>다 — 몸의 시각 반폭에 넣으면 안 된다.
        /// </summary>
        public void CollectVisuals(CharacterVisualRegistry sink)
        {
            if (sink == null || _container == null) return;
            CollectGroup(sink, _footprints);
            CollectGroup(sink, _sparkles);
            CollectGroup(sink, _dusts);
            CollectGroup(sink, _bubbles);
            CollectGroup(sink, _leaves);
        }

        private static void CollectGroup(CharacterVisualRegistry sink, Puff[] group)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                Puff p = group[i];
                if (p == null || !p.Alive || p.Lines == null) continue;
                sink.AddRange(p.Lines, CharacterVisualAnchor.Detached);
            }
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

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;

            // ★ 리듬은 <b>배회 Idle 창에서 유도</b>한다(Interaction/SparkleCadence.cs).
            //   여기서 숫자를 직접 잡으면 그 숫자가 다시 배회 시간을 모르게 된다.
            SparkleCadence.Resolve(bb.Config, out float armSeconds, out float lifeSeconds,
                out float intervalMin, out float intervalMax);

            _idleSeconds += dt;
            if (_idleSeconds < armSeconds) return;

            if (_nextSparkleIn > 0f)
            {
                _nextSparkleIn -= dt;
                if (_nextSparkleIn > 0f) return;
            }

            // 머리 중심 위 R·2.0(= 십자 아래 갈래 끝이 정수리보다 R·0.15 위), 좌우로 ±R·0.9 범위.
            // ★ 2026-09-01 — 기준점이 <b>기울임이 반영된</b> 머리다(LeanedHeadWorld 문서 참고).
            float r = HeadRadius;
            Vector2 head = LeanedHeadWorld(bb, r * SparkleHeightInR);
            float cx = head.x + Random.Range(-SparkleSpreadInR, SparkleSpreadInR) * r;
            float cy = head.y;

            Puff p = Take(ref _sparkles, ref _sparkleCursor, SparkleCapacity, "Sparkle", SortAerial, 2);
            if (p == null) return;
            BuildCross(p.Lines, r * SparkleArmInR);
            p.Root.position = new Vector3(cx, cy, 0f);
            Revive(p, lifeSeconds, Vector2.zero, SparkleStartScale, 1f);

            _nextSparkleIn = Random.Range(intervalMin, intervalMax);
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

        /// <summary>
        /// ④ 물방울(2026-09-01) — <b>걷는 동안</b> 몸 옆에서 방울이 하나씩 떠올라 흩어진다.
        ///
        /// <para>반짝임(Idle)/먼지(달리기·도약)와 발동 창을 일부러 갈라 놓았다. 세 이펙트가 같은
        /// 순간을 노리면 어느 것을 골라도 화면이 비슷해져 "골랐다"는 감각이 사라진다.</para>
        ///
        /// <para>발생 높이는 <b>고관절</b>이다 — 상체 기울임의 회전 중심이 바로 거기라, 기울여도
        /// 방울이 몸에서 어긋나지 않는다(그래서 이 함수만은 기울임 보정이 필요 없다).</para>
        /// </summary>
        private void TickBubbles(StickmanStateId state, float dt)
        {
            if (_bubbleCooldown > 0f) _bubbleCooldown -= dt;
            if (state != StickmanStateId.Walk) return;

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;
            if (_bubbleCooldown > 0f) return;
            _bubbleCooldown = BubbleIntervalSeconds;

            float r = HeadRadius;
            float facing = bb.FacingSign >= 0f ? 1f : -1f;
            float radius = r * Random.Range(AppearanceShapeBuilder.BubbleMinRadiusInR,
                AppearanceShapeBuilder.BubbleMaxRadiusInR);

            Puff p = Take(ref _bubbles, ref _bubbleCursor, BubbleCapacity, "Bubble", SortAerial, 1);
            if (p == null) return;
            BuildBubble(p.Lines[0], radius);
            p.Root.position = new Vector3(bb.Body.position.x - facing * r * BubbleSideInR,
                bb.Body.position.y + HipLocalY, 0f);
            // 뒤쪽 위로 떠오르며 커졌다가 사라진다("톡" 터지는 그림).
            Revive(p, BubbleLifeSeconds,
                new Vector2(-facing * r * BubbleSideInR, r * BubbleRiseInR),
                BubbleStartScale, BubbleEndScale);
        }

        /// <summary>
        /// ⑤ 나뭇잎(2026-09-01) — 머리 위에서 한 장씩 팔랑이며 떨어져 <b>지면에 닿으면</b> 사라진다.
        ///
        /// <para>발동이 상태와 무관한 이유: 이건 캐릭터의 <b>행동</b>이 아니라 주변 분위기다(날씨에 가깝다).
        /// 그래서 원칙 1("행동이 확정된 뒤 그 상태에서 파생")의 대상이 아니며, 반대로 상태를 붙이면
        /// "가만히 서 있으면 잎이 안 진다"는 이상한 규칙이 생긴다.</para>
        ///
        /// <para>도착 높이는 <see cref="ResolveOwnerGroundWorldY"/>다 — "그 x에서 가장 높은 면"을 물으면
        /// 창 하나만 최대화해도 잎이 화면 꼭대기에 쌓인다(이 프로젝트가 세 번 겪은 API 오용).</para>
        /// </summary>
        private void TickLeaves(float dt)
        {
            if (_leafCooldown > 0f)
            {
                _leafCooldown -= dt;
                if (_leafCooldown > 0f) return;
            }

            StickmanBlackboard bb = _agent.Blackboard;
            if (bb == null || bb.Body == null) return;
            _leafCooldown = Random.Range(LeafIntervalMinSeconds, LeafIntervalMaxSeconds);

            float r = HeadRadius;
            Vector2 head = LeanedHeadWorld(bb, r * LeafSpawnAboveHeadInR);
            float spawnX = head.x + Random.Range(-LeafSpawnSpreadInR, LeafSpawnSpreadInR) * r;
            float spawnY = head.y;
            float surfaceY = ResolveOwnerGroundWorldY(bb);

            Puff p = Take(ref _leaves, ref _leafCursor, LeafCapacity, "Leaf", SortAerial, 2);
            if (p == null) return;
            BuildLeaf(p.Lines, r * AppearanceShapeBuilder.LeafLengthInR);
            p.Root.position = new Vector3(spawnX, spawnY, 0f);

            // 아래로 내려가는 거리는 "여기서 지면까지"다 — 잎이 땅에 닿는 순간 수명이 끝난다.
            // 지면이 스폰 지점보다 위인 병적인 경우(발판이 머리 위)에는 0으로 눌러 위로 솟지 않게 한다.
            float fall = Mathf.Min(0f, surfaceY + Stroke - spawnY);
            Revive(p, LeafLifeSeconds, new Vector2(0f, fall), 1f, 1f,
                swayAmplitude: r * LeafSwayInR, swayCycles: LeafSwayCycles,
                spinDegrees: Random.value < 0.5f ? -LeafSpinDegrees : LeafSpinDegrees);
        }

        // ==================== 수명 ====================

        private void TickLifetimes(float dt)
        {
            TickGroup(_footprints, dt, FootprintFadeSeconds);
            TickGroup(_sparkles, dt, 0f);
            TickGroup(_dusts, dt, 0f);
            TickGroup(_bubbles, dt, 0f);
            // 떨어지는 잎은 <b>떨어지는 내내 또렷해야</b> 한다 — 발자국과 같은 "마지막에만 페이드".
            TickGroup(_leaves, dt, LeafFadeSeconds);
        }

        /// <param name="lateFadeSeconds">0보다 크면 <b>마지막 이 시간만</b> 선형으로 옅어지고 크기는
        /// 고정된다(발자국·나뭇잎). 0이면 0→1→0 산 모양으로 나타났다 사라진다(반짝임·먼지·물방울).</param>
        private void TickGroup(Puff[] group, float dt, float lateFadeSeconds)
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
                bool lateFade = lateFadeSeconds > 0f;
                // 발자국/나뭇잎은 "마지막 N초만 선형 페이드"(그 전에는 또렷하게 남아 있다).
                // 나머지는 0→1→0 산 모양(반짝임/먼지/물방울이 나타났다 사라지는 그림).
                float alpha = lateFade
                    ? Mathf.Clamp01((p.Life - p.Age) / Mathf.Max(0.01f, lateFadeSeconds))
                    : Mathf.Sin(t * Mathf.PI);
                SetGroupAlpha(p.Lines, alpha);

                if (p.Pivot == null) continue;
                float ease = 1f - (1f - t) * (1f - t);
                float scale = lateFade ? p.EndScale : Mathf.Lerp(p.StartScale, p.EndScale, Mathf.Sin(t * Mathf.PI));
                p.Pivot.localScale = new Vector3(scale, scale, 1f);

                // 팔랑임/회전은 <b>진폭이 0이면 항 자체가 빠진다</b> — 기존 3종의 결과가 예전과 동일하다.
                float sway = p.SwayAmplitude != 0f
                    ? Mathf.Sin(t * Mathf.PI * 2f * p.SwayCycles) * p.SwayAmplitude
                    : 0f;
                p.Pivot.localPosition = new Vector3(p.Drift.x * ease + sway, p.Drift.y * ease, 0f);
                if (p.SpinDegrees != 0f)
                {
                    p.Pivot.localRotation = Quaternion.Euler(0f, 0f, p.SpinDegrees * t);
                }
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

        private void Revive(Puff p, float life, Vector2 drift, float startScale, float endScale,
            float swayAmplitude = 0f, float swayCycles = 0f, float spinDegrees = 0f)
        {
            p.Age = 0f;
            p.Life = Mathf.Max(0.01f, life);
            p.Alive = true;
            p.Drift = drift;
            p.StartScale = startScale;
            p.EndScale = endScale;
            p.SwayAmplitude = swayAmplitude;
            p.SwayCycles = swayCycles;
            p.SpinDegrees = spinDegrees;
            if (p.Pivot != null)
            {
                p.Pivot.localPosition = Vector3.zero;
                p.Pivot.localScale = new Vector3(startScale, startScale, 1f);
                // 원형 버퍼는 회전이 남은 조각을 그대로 되살린다 — 여기서 지우지 않으면 잎이 한 번
                // 돌고 난 각도에서 다음 방울/발자국이 시작한다.
                p.Pivot.localRotation = Quaternion.identity;
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
            lr.startWidth = RenderStroke;
            lr.endWidth = RenderStroke;
            Color ink = ResolveInk();
            ink.a = 0f;
            lr.startColor = ink;
            lr.endColor = ink;
            lr.positionCount = 0;
            return lr;
        }

        /// <summary>채운 점 하나 — 짧은 선을 굵은 캡으로 그리면 원이 된다(점 도형을 따로 만들지 않는다).
        /// 점 좌표는 Interaction/AppearanceShapeBuilder.cs가 소유한다(초상화 미리보기와 같은 그림).</summary>
        private void BuildDot(LineRenderer lr, float radius)
        {
            if (lr == null) return;
            lr.loop = false;
            // 점의 지름도 화면상 하한을 받는다 — 2pt 미만의 점은 안티에일리어싱에 그대로 묻힌다.
            float diameter = Mathf.Max(radius * 2f, MinStrokeWorld);
            lr.startWidth = diameter;
            lr.endWidth = diameter;
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
                lr.startWidth = RenderStroke;
                lr.endWidth = RenderStroke;
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
                lr.startWidth = RenderStroke;
                lr.endWidth = RenderStroke;
                Vector3[] pts = AppearanceShapeBuilder.DustCrescent(radius, i);
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
            }
        }

        /// <summary>물방울 한 알(닫힌 고리). 방울은 <b>속이 보여야</b> 방울이므로 점(굵은 캡)이 아니라
        /// 테두리로 그린다 — 반지름 하한은 <see cref="AppearanceShapeBuilder.BubbleMinRadiusInR"/>가 잡는다.</summary>
        private void BuildBubble(LineRenderer lr, float radius)
        {
            if (lr == null) return;
            lr.loop = true;
            lr.startWidth = RenderStroke;
            lr.endWidth = RenderStroke;
            Vector3[] pts = AppearanceShapeBuilder.BubbleRing(radius, 12);
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        /// <summary>나뭇잎 한 장(잎몸 닫힌 고리 + 잎자루). 잎자루는 잎몸 뒤끝에서 이어지므로
        /// 회전 중심(Pivot)이 어디든 두 조각이 절대 떨어지지 않는다.</summary>
        private void BuildLeaf(LineRenderer[] lines, float length)
        {
            if (lines == null || lines.Length < 2) return;
            SetShape(lines[0], AppearanceShapeBuilder.LeafBlade(length), loop: true);
            SetShape(lines[1], AppearanceShapeBuilder.LeafStem(length), loop: false);
        }

        private void SetShape(LineRenderer lr, Vector3[] pts, bool loop)
        {
            if (lr == null) return;
            lr.loop = loop;
            lr.startWidth = RenderStroke;
            lr.endWidth = RenderStroke;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
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
            _bubbles = null;
            _leaves = null;
            _footprintCursor = 0;
            _sparkleCursor = 0;
            _dustCursor = 0;
            _bubbleCursor = 0;
            _leafCursor = 0;
            _hasFootprint = false;
            _hiddenApplied = false;   // 풀이 통째로 사라졌으므로 래치도 함께 초기화한다.
            _idleSeconds = 0f;
            _nextSparkleIn = 0f;
            _dustCooldown = 0f;
            _bubbleCooldown = 0f;
            _leafCooldown = 0f;
        }

        // ==================== 치수/재료 ====================

        private float Height => _metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        private float HeadRadius => _metrics != null ? _metrics.HeadRadius
            : Height * (AccessoryShapeBuilder.BaselineHeadVisualRadius / StickConfig.BaselineCharacterTotalHeight);
        private float HeadCenterLocalY => _metrics != null ? _metrics.HeadCenterLocalY : Height - HeadRadius;

        /// <summary>고관절의 로컬 Y(발바닥 기준, 월드 유닛). 상체 기울임의 <b>회전 중심</b>이다.</summary>
        private float HipLocalY => _metrics != null ? _metrics.HipLocalY : Height * FallbackHipRatio;

        /// <summary>StickmanMetrics가 없는 리그(테스트 스텁)용 폴백. 분자는 배율 1.0 프리팹의 실측치다.</summary>
        private const float FallbackHipRatio = 0.9346944f / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>
        /// ★ 2026-09-01 — <b>기울임이 반영된</b> 머리 기준 월드 좌표(교차 레이어 항목 #22).
        ///
        /// <para>예전에는 <c>Body.position.y + HeadCenterLocalY</c>였다. 즉 <b>중립(기울지 않은)</b>
        /// 머리다. 그런데 States/StickmanPoseAnimator.SetBodyLean이 들어오면서 걷는 동안 머리는
        /// 엉덩이를 축으로 최대 10도 앞으로 나가는데, 반짝임만 제자리에 남아 <b>머리 뒤통수 위</b>에서
        /// 터졌다(배율 0.75에서 화면상 약 5pt).</para>
        ///
        /// <para>고치는 방법은 액세서리 렌더러(3-2)와 같다: <b>같은 피벗(엉덩이)으로 같은 각도만큼</b>
        /// 돌린다. 각도는 <b>새로 계산하지 않고</b> Torso의 localRotation을 읽는다 — 포즈가 실제로
        /// 적용한 값이 유일한 진실이고, 같은 값을 두 곳에서 계산하면 언젠가 한쪽만 바뀐다.</para>
        ///
        /// <para>기울임이 0이면 결과가 예전 식과 <b>정확히</b> 같다(회전이 identity라 항이 사라진다).</para>
        /// </summary>
        /// <param name="extraAboveHead">머리 중심에서 더 올라가는 높이(월드 유닛). 이 값도 함께 돈다 —
        /// 머리 위에 매달린 것은 머리가 기울면 같이 기우는 것이 자연스럽다.</param>
        private Vector2 LeanedHeadWorld(StickmanBlackboard bb, float extraAboveHead)
        {
            Vector2 foot = bb.Body.position;
            var neutral = new Vector2(0f, HeadCenterLocalY + extraAboveHead);
            Quaternion rot = _torsoTransform != null ? _torsoTransform.localRotation : Quaternion.identity;
            if (rot == Quaternion.identity) return foot + neutral;

            var hip = new Vector2(0f, HipLocalY);
            return foot + hip + (Vector2)(rot * (neutral - hip));
        }

        /// <summary>이펙트 획의 <b>비례 두께</b>(월드 유닛). 조각의 크기/위치 유도는 이 값을 쓴다
        /// (발자국 점 반지름, 지면에서 띄우는 높이) — 배율에 정확히 비례해야 한다.</summary>
        private float Stroke => Height * StrokeRatio;

        /// <summary>
        /// ★ 실제로 <b>그려지는</b> 두께 — 화면상 최소 두께 아래로 내려가지 않는다(2026-08-31).
        /// 몸과 같은 규칙이다(도형은 그대로, LineRenderer 두께만 하한으로 올린다).
        /// 하한이 없던 시절 다이얼 최소값 0.35에서 0.72pt(하한 2pt의 1/3)라 반짝임 십자 획이
        /// 사실상 보이지 않았다. 하한 값의 단일 소스는 <see cref="StickmanAgent.MinStrokeWorldWidth"/>다.
        /// </summary>
        private float RenderStroke => Mathf.Max(Stroke, MinStrokeWorld);

        private float MinStrokeWorld => _agent != null
            ? _agent.MinStrokeWorldWidth
            : StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

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
