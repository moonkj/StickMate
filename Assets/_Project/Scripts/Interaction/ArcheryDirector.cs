using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 활쏘기 연출(2026-08-29 사용자 요청: "과녁이 생성되고 3번정도 포물선을 그리는 활을 쏘는 행동")의
    /// <b>트리거 / 과녁 자리 선정 / 취소 감시</b> 담당. 3발의 타이밍과 포즈는 States/ArcheryState.cs가,
    /// 실제로 보이는 과녁·활·화살은 Interaction/ArcheryRenderer.cs가 맡는다
    /// (GraffitiDirector / TimedSpectacleState / GraffitiRenderer의 3분할과 정확히 같은 구조).
    ///
    /// ============================================================================
    /// 자율 발동은 기본 0이다 (리더 지시)
    /// ============================================================================
    /// 이 프로젝트 사용자는 요청하지 않은 연출이 뜨는 것에 반복적으로 불만을 표했고, 직전 라운드에
    /// 구경거리 연출 전부가 기본 OFF로 내려갔다(StickConfig의 *Chance 필드들이 전부 0). 활쏘기도
    /// 같다 — <see cref="StickConfig.archeryChance"/> 기본값 0이라 <b>단축키(Ctrl+Opt+Cmd+A)와
    /// 캐릭터 우클릭 메뉴로만</b> 발동한다. 자동 추첨 코드는 남겨두되(값을 올리면 즉시 살아난다)
    /// 기본 경로가 아니다.
    ///
    /// ============================================================================
    /// 절대 원칙 3(유저 자산 불변) — 이 클래스가 하지 않는 일
    /// ============================================================================
    /// 과녁은 <b>순수하게 그려지는 오버레이</b>다. 이 파일에는 창/파일/아이콘을 조작하는 API가 하나도
    /// 없고, 읽는 것이라고는 캐릭터 자신의 좌표와 카메라의 가시 범위뿐이다. 렌더러도 콜라이더를
    /// 단 하나도 만들지 않으므로(ArcheryRenderer 문서 참고) 과녁이 떠 있는 동안에도 그 자리의 다른
    /// 앱은 평소처럼 클릭된다 — 비침해 원칙 2 유지.
    ///
    /// ============================================================================
    /// 화면 밖으로 나가면 안 된다 (리더 지시)
    /// ============================================================================
    /// 캐릭터가 화면 끝에 서 있으면 정면에 과녁을 놓을 자리가 없다. 그때는 BattleMinigameRenderer의
    /// 배치 규칙과 <b>같은 우선순위</b>로 처리한다: (1) 정면에 자리가 있으면 정면, (2) 없으면
    /// 반대편으로 미러링, (3) 양쪽 다 안 되면 <b>조용히 발동을 포기</b>한다. 격파 미니게임과 달리
    /// "빠듯하면 클램프해서라도 그린다" 갈래가 없는 이유는, 활쏘기는 과녁만이 아니라
    /// <b>캐릭터에서 과녁까지의 궤적 전체</b>가 보여야 의미가 있어서 반쯤 잘리면 연출이 성립하지
    /// 않기 때문이다.
    /// </summary>
    public sealed class ArcheryDirector : MonoBehaviour
    {
        [SerializeField] private StickmanAgent _player;
        [SerializeField] private StickConfig _config;

        /// <summary>캐릭터 클릭 판정기. 활쏘기 <b>중에도</b> 클릭은 계속 받아야 하고, 클릭이 들어오면
        /// 즉시 연출을 걷는다(2026-08-29 사용자 신고 "활을 쏘는동안은 캐릭터가 클릭이 안됨. 클릭을
        /// 하면 과녁이랑 활이 없어져야지"). 새 입력 경로를 만들지 않고 기존 히트박스 이벤트를 그대로
        /// 쓴다 — StressGaugeDirector/RunawayDirector가 같은 방식으로 클릭 사실만 구독한다.</summary>
        [SerializeField] private StickmanClickHitbox _hitbox;

        /// <summary>화면 가장자리에서 남겨둘 최소 여백(월드 유닛, 약 4pt) — BattleMinigameRenderer와 같은 값.</summary>
        private const float ScreenEdgePadWorld = 0.10f;

        /// <summary>자리 계산에서 "같은 점"으로 볼 부동소수 오차(월드 유닛, 약 0.04pt).
        /// <see cref="ResolvePlacement"/>의 경계 처리에만 쓴다.</summary>
        private const float EdgeEpsilon = 0.001f;

        private float _checkTimer;
        private float _cooldownRemaining;
        private bool _active;
        private Vector2 _targetWorld;
        private float _groundWorldY;
        private float _facing = 1f;

        /// <summary>진단/테스트용 — 지금 활쏘기 사이클이 진행 중인지.</summary>
        public bool IsActive => _active;

        /// <summary>진단/테스트용 — 이번(또는 마지막) 사이클의 과녁 중심 월드 좌표.</summary>
        public Vector2 LastTargetWorld => _targetWorld;

        /// <summary>진단/테스트용 — 다음 <b>자율</b> 발동까지 남은 쿨다운(초). 사용자 신고
        /// "다른행동은 아예안하고 계속 활만쏨"의 회귀 잠금 지점이다: 한 사이클이 끝나면 이 값이
        /// 반드시 0보다 커야 하고(=곧바로 재진입할 수 없음), 그 사이 캐릭터는 평소처럼 배회한다.
        /// 수동 발동(단축키/메뉴)은 의도적으로 이 값을 무시한다.</summary>
        public float CooldownRemaining => _cooldownRemaining;

        private void OnEnable()
        {
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested += OnEmergencyStop;
            if (_hitbox != null) _hitbox.MouseDown += OnCharacterClicked;
        }

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.GlobalEmergencyStopRequested -= OnEmergencyStop;
            if (_hitbox != null) _hitbox.MouseDown -= OnCharacterClicked;
            ReleaseOwnedLock();
        }

        /// <summary>
        /// 활쏘기 중 캐릭터를 클릭하면 <b>즉시</b> 연출을 접는다(사용자 요구). 강제 인터럽트로 Idle에
        /// 전이시키면 그 전이를 <see cref="OnStateTransitioned"/>가 받아 Cancelled 오버레이 이벤트를
        /// 발행하고 락을 반납하므로, 정리 경로가 전체화면 감지/긴급정지와 완전히 같아진다
        /// (GraffitiDirector.CancelDrawing과 같은 관례 — 정리 코드를 두 벌 만들지 않는다).
        ///
        /// 활쏘기가 아닐 때는 아무것도 하지 않는다 — 드래그&던지기 등 다른 소비자의 클릭을 가로채지 않는다.
        /// </summary>
        private void OnCharacterClicked()
        {
            if (_player == null || !_active) return;
            if (_player.Blackboard.Machine.CurrentStateId != StickmanStateId.Archery) return;

            Debug.Log("[활쏘기] 캐릭터 클릭 — 연출을 즉시 중단하고 과녁/활/화살을 걷습니다.");
            _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
        }

        private void ReleaseOwnedLock()
        {
            if (_active)
            {
                _active = false;
                RaiseOverlay(SpectacleOverlayPhase.Cancelled);
            }
            SpectacleEventLock.ReleaseIfOwned(this, _player != null ? _player.Blackboard?.Machine : null,
                StickmanStateId.Archery);
        }

        /// <summary>과녁 자리를 못 찾았을 때 사용자에게 보여줄 한 줄(36-7 표와 1:1). 상수라 폴링해도
        /// 문자열이 새로 생기지 않는다.</summary>
        public const string NoPlacementReason = "과녁 놓을 자리가 없어요";

        /// <summary>
        /// ★ 지금 활쏘기를 시킬 수 있는가 — <b>회색 처리와 실제 실행이 함께 쓰는 단 하나의 판정</b>
        /// (docs/UX_FLOW.md 36-7 절대 규칙). <see cref="ForceTriggerNow"/>가 내부에서 이것을 호출하므로
        /// 두 판단이 어긋날 방법이 구조적으로 없다.
        /// </summary>
        public CommandAvailability GetAvailability()
        {
            if (_player == null || _config == null || _player.Blackboard == null || _player.Blackboard.Machine == null)
                return CommandAvailability.Missing;

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            if (!TryResolvePlacement(out _, out _, out _, out _, out _))
                return CommandAvailability.Blocked(NoPlacementReason);

            return CommandAvailability.Ready;
        }

        /// <summary>
        /// 활쏘기 강제 발동(전역 단축키 Ctrl+Opt+Cmd+A / 행동 명령창 [활쏘기]). GraffitiDirector.
        /// ForceTriggerNow와 같은 관례로 <b>확률/쿨다운만</b> 건너뛴다 — 상호배제 락, Idle/Walk 진입
        /// 조건, 그리고 "과녁 자리가 화면 안에 없으면 발동하지 않는다"는 규칙은 하나도 완화하지 않는다.
        /// </summary>
        /// <returns>실제로 시작했는가. 기존 단축키 호출부는 반환값을 무시하면 되므로 하위 호환이다.</returns>
        public bool ForceTriggerNow(string reason)
        {
            CommandAvailability availability = GetAvailability();
            if (!availability.IsReady)
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — {availability.Reason}. " +
                    "자리 조건은 두 가지다: (1) 궤적 전체가 화면 안, (2) 과녁이 **지금 딛고 있는 발판(창)의 " +
                    "가로 범위 안**(밖이면 허공에 뜬다). 조건은 강제 경로에서도 완화하지 않는다.");
                return false;
            }

            // 사거리를 매번 추첨하므로 여기서 한 번 더 부른다 — 위 판정은 "자리가 있는가"(결정론적)이고
            // 이 호출은 "이번에 쓸 좌표"(추첨)다. 같은 프레임이라 가능/불가 판정이 뒤집히지 않는다.
            if (!TryResolvePlacement(out float standX, out Vector2 target, out float groundY,
                    out float facing, out string kindLabel))
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — {NoPlacementReason}(좌표 재계산 단계).");
                return false;
            }

            Begin(standX, target, groundY, facing, kindLabel, reason);
            return true;
        }

        private void Begin(float standX, Vector2 target, float groundY, float facing, string kindLabel, string reason)
        {
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this)) return;

            _checkTimer = 0f;
            _cooldownRemaining = 0f;
            _targetWorld = target;
            _groundWorldY = groundY;
            _facing = facing;
            _active = true;

            // 상태 전이보다 오버레이 이벤트를 **먼저** 발행한다 — ArcheryState.Enter()가 블랙보드에서
            // 과녁 좌표를 읽어 시나리오(도달점)를 계산하므로, 그 값이 그 전에 확정돼 있어야 한다.
            var blackboard = _player.Blackboard;
            blackboard.ArcheryTargetWorld = target;
            blackboard.ArcheryGroundWorldY = groundY;
            blackboard.ArcheryFacingSign = facing;
            blackboard.ArcheryStandWorldX = standX;
            // ★ 과녁을 향해 **몸을 돌린다**(2026-08-29 사용자 신고 "활을 이상하게 들고있음"의 근본 원인).
            // 화면 끝에서 과녁을 반대편으로 미러링할 때 몸을 돌리지 않으면, 캐릭터가 과녁을 등진 채
            // 활을 등 뒤로 들고 쏘는 그림이 된다. 게다가 활쏘기 중에는 방향이 고정되므로(FacingLocked)
            // 배회 AI가 나중에 고쳐줄 수도 없다. 미러링이 아닐 때도 호출해 두는 편이 안전하다(멱등).
            blackboard.SetFacingSign(facing);
            // ★ Started(과녁 등장)는 여기서 발행하지 않는다 — 사용자 요구 순서가 "이동 -> 과녁 생성 ->
            // 발사"라서, 과녁이 보여야 하는 시점을 아는 것은 이동이 끝났음을 아는 ArcheryState뿐이다
            // (States/ArcheryState.BeginIntro가 발행한다). 종료(Completed/Cancelled)는 생애주기를 아는
            // 이 Director가 계속 담당한다.
            blackboard.Machine.ChangeState(StickmanStateId.Archery);

            Debug.Log($"[활쏘기] 발동({reason}) — 배치 기준: {kindLabel}. " +
                $"캐릭터는 x={blackboard.Body.position.x:F2}에서 x={standX:F2}까지 **걸어간 뒤** " +
                $"{(facing > 0f ? "오른" : "왼")}쪽 {Mathf.Abs(target.x - standX):F2}유닛 앞의 과녁 " +
                $"{target.ToString("F2")}(반지름 {TargetRadius:F2})을 쏩니다. 신장 {Height:F2}유닛 기준. " +
                "실제 창/파일/아이콘은 1픽셀도 건드리지 않는 순수 오버레이입니다.");
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Directors);   // [스톨구간] 계측
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            if (_player == null || _config == null) return;
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.Archery) return;
            TickAutoTrigger();
        }

        /// <summary>
        /// 자동 추첨 경로. <see cref="StickConfig.archeryChance"/> 기본값이 0이라 <b>기본 설정에서는
        /// 절대 발동하지 않는다</b>(클래스 문서 참고) — 값을 올리면 다른 스펙터클과 같은 방식으로 살아난다.
        /// </summary>
        private void TickAutoTrigger()
        {
            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk) { _checkTimer = 0f; return; }

            _checkTimer += Time.deltaTime;
            float interval = Mathf.Max(1f, _config.archeryCheckInterval);
            if (_checkTimer < interval) return;
            _checkTimer = 0f;

            if (_cooldownRemaining > 0f) return;
            if (SpectacleEventLock.IsActive) return;
            if (Random.value >= _config.archeryChance) return;
            if (!TryResolvePlacement(out float standX, out Vector2 target, out float groundY,
                    out float facing, out string kindLabel)) return;

            Begin(standX, target, groundY, facing, kindLabel, "자동 추첨");
        }

        // ============================================================================
        // 과녁 자리 선정
        // ============================================================================

        private float Height => _player != null ? _player.Blackboard.CharacterHeightWorld
                                                : StickConfig.BaselineCharacterTotalHeight;

        private float TargetRadius => Height * RadiusRatio;

        private float RadiusRatio => _config != null ? Mathf.Clamp(_config.archeryTargetRadiusRatio, 0.05f, 0.9f) : 0.40f;

        /// <summary>화면이 좁을 때까지 줄여도 되는 최소 사거리(신장 배수). 이보다 가까우면 "쏘는" 것이
        /// 아니라 "찌르는" 것처럼 보이므로 차라리 발동하지 않는다. = 랜덤 사거리 밴드의 <b>하한</b>.</summary>
        private float MinDistanceRatio => _config != null ? Mathf.Max(0.5f, _config.archeryMinTargetDistanceRatio) : 2.6f;

        /// <summary>랜덤 사거리 밴드의 <b>상한</b>(신장 배수). 2026-08-31 사용자 신고 "무조건 과녁이
        /// 화면 끝에만 생김 ... 거리는 항상 랜덤으로 변경되어야" 대응으로 신설됐다. 이 상한이 없으면
        /// (=구간 전체를 쓰면) 넓은 바탕화면에서 과녁이 매번 화면 맨 끝에 붙는다.</summary>
        private float MaxDistanceRatio
        {
            get
            {
                float min = MinDistanceRatio;
                float max = _config != null ? _config.archeryMaxTargetDistanceRatio : 6.6f;
                return Mathf.Max(min * 1.05f, max); // 상한이 하한 아래로 뒤집히면 랜덤이 사라진다.
            }
        }

        /// <summary>
        /// ★★ 배치 결정 — <b>2026-08-31 사용자 재정의로 규칙이 한 번 더 바뀌었다.</b>
        ///
        /// 이력(둘 다 같은 사용자 신고에서 나왔다, 뒤엣것이 앞엣것을 덮어쓴다):
        ///   1) 2026-08-29 "과녁과 캐릭터 사이가 너무 가까운데서 행동을 함. ... 화면 전체 길이의 절반
        ///      이상 떨어진 곳만큼 캐릭터가 이동한 다음 과녁을 생성" → 캐릭터를 구간 한쪽 끝, 과녁을
        ///      <b>반대쪽 끝</b>에 고정 배치했다. 랜덤이 하나도 없는 결정론적 최대 거리 배치였다.
        ///   2) 2026-08-31 "활쏘기 시키면 <b>무조건 과녁이 화면 끝에만 생김</b>. 적당히 먼 거리만 되도
        ///      되는데 <b>물론 거리는 항상 랜덤으로 변경</b>되어야 하지만" → (1)의 부작용을 그대로
        ///      지적한 것이다. 그래서 지금은 <b>사거리를 매번 추첨</b>한다.
        ///
        /// 지금 규칙(단순하다):
        ///   · 사거리 d = Random(신장×archeryMinTargetDistanceRatio, 신장×archeryMaxTargetDistanceRatio).
        ///     상한이 <b>구간 폭과 무관한 절대 밴드</b>라는 점이 이번 수정의 핵심이다 — 화면/창이
        ///     아무리 넓어도 과녁이 그 끝까지 밀려나지 않는다.
        ///   · 구간(발판 ∩ 걸어다닐 수 있는 화면 범위)이 좁아 밴드 상한을 못 채우면 <b>들어가는 만큼만</b>
        ///     줄인다. 하한(최소 사거리)조차 안 나오면 <b>조용히 발동을 포기</b>한다(코앞에 억지로
        ///     놓지 않는다 — 그러면 포물선이 직선처럼 보인다).
        ///   · 서는 자리는 "지금 위치에서 가장 가까운 유효 지점"이다. 예전처럼 무조건 구간 끝까지
        ///     걸어가지 않는다 — 거리를 랜덤으로 뽑는 이상 끝까지 갈 이유가 사라졌고, 매번 화면
        ///     가장자리로 행진하는 그림 자체가 신고 문구("무조건 ... 화면 끝")의 절반이었다.
        ///
        /// 구간은 예전과 같이 <b>딛고 있는 발판의 실측 좌우 경계</b>(GroundSensor의
        /// CurrentFoothold*WorldX — 추정하지 않는다)와 <b>걸어다닐 수 있는 화면 범위</b>
        /// (StickmanBlackboard.TryGetWalkableScreenBoundsWorld — 화면 끝 클램프와 같은 유일한
        /// 생산자)의 교집합이다. 바닥에서는 안전망 발판이 화면 전체를 덮으므로 사실상 화면 범위가 된다.
        ///
        /// 캐릭터가 서야 할 자리(<paramref name="standX"/>)만 여기서 정하고, 거기까지 <b>실제로 걸어가는</b>
        /// 것은 States/ArcheryState.cs의 Approach 페이즈가 한다(순간이동하지 않는다 — 사용자 명시).
        /// </summary>
        private bool TryResolvePlacement(out float standX, out Vector2 target, out float groundY,
            out float facing, out string kindLabel)
        {
            standX = 0f;
            target = default;
            groundY = 0f;
            facing = 1f;
            kindLabel = "?";

            StickmanBlackboard blackboard = _player.Blackboard;
            if (blackboard == null || blackboard.Body == null) return false;

            Vector2 foot = blackboard.Body.position; // 이 프로젝트 규약: 루트 원점 = 발바닥.
            groundY = foot.y;

            float height = Height;
            float radius = height * RadiusRatio;

            GroundSensor.GroundInfo ground = blackboard.SenseGround();
            if (!ground.Grounded) return false; // 공중에서는 시작하지 않는다.

            if (!blackboard.TryGetWalkableScreenBoundsWorld(out float screenLeft, out float screenRight))
            {
                Camera fallbackCam = blackboard.MainCamera;
                if (fallbackCam == null || !fallbackCam.orthographic) return false;
                float half = fallbackCam.orthographicSize * fallbackCam.aspect;
                screenLeft = fallbackCam.transform.position.x - half + ScreenEdgePadWorld;
                screenRight = fallbackCam.transform.position.x + half - ScreenEdgePadWorld;
            }

            // 구간 = 딛고 있는 발판 ∩ 걸어다닐 수 있는 화면 범위. 창 위든 바닥이든 같은 식이며,
            // 바닥에서는 안전망 발판이 화면 전체를 덮으므로 사실상 화면 범위가 된다.
            float lo = Mathf.Max(ground.CurrentFootholdLeftWorldX, screenLeft);
            float hi = Mathf.Min(ground.CurrentFootholdRightWorldX, screenRight);

            bool onWindow = IsRealWindowFoothold(ground.GroundedFootholdHandle);
            kindLabel = onWindow ? "창/Dock 발판" : "바탕화면";

            float minDistance = height * MinDistanceRatio;
            float maxDistance = height * MaxDistanceRatio;

            // ★ 추첨은 여기 한 번뿐이다(Random.value). 나머지 계산은 전부 순수 함수라 EditMode에서
            //   시드를 바꿔가며 분포를 직접 검사할 수 있다(Tests/EditMode/ArcheryTargetDistanceTests.cs).
            Placement placed = ResolvePlacement(foot.x, lo, hi,
                height * CharacterEdgeInsetRatio,
                radius + height * TargetEdgeInsetRatio,
                height * BackStepRatio,
                minDistance, maxDistance, Random.value);
            if (!placed.Ok) return false;

            standX = placed.StandX;
            facing = placed.Facing;
            float targetX = placed.TargetX;

            if (placed.Distance + 0.001f < maxDistance)
            {
                // 좁은 화면/발판 — 밴드 상한을 못 채운 경우. 포기가 아니라 들어가는 만큼으로 타협한다.
                Debug.Log($"[활쏘기] 사거리 추첨 {placed.Distance:F2}유닛 " +
                    $"(밴드 {minDistance:F2}~{maxDistance:F2}유닛, 구간 폭이 허용한 최대 " +
                    $"{placed.MaxAvailableDistance:F2}유닛, {kindLabel}).");
            }

            float centerY = groundY + TargetCenterHeight(height, radius);
            Camera cam = blackboard.MainCamera;
            if (cam != null && cam.orthographic)
            {
                float topY = cam.transform.position.y + cam.orthographicSize - ScreenEdgePadWorld;
                if (centerY + radius > topY) return false;
            }

            target = new Vector2(targetX, centerY);
            return true;
        }

        /// <summary>
        /// 배치 결과. <see cref="Ok"/>가 false면 "이번엔 놓을 자리가 없다"는 뜻이고 나머지 값은 의미가 없다.
        /// </summary>
        public readonly struct Placement
        {
            public readonly bool Ok;
            /// <summary>캐릭터가 서야 할 월드 X(발바닥 기준).</summary>
            public readonly float StandX;
            /// <summary>과녁 중심의 월드 X.</summary>
            public readonly float TargetX;
            /// <summary>+1이면 오른쪽, -1이면 왼쪽을 향해 쏜다.</summary>
            public readonly float Facing;
            /// <summary>실제로 확정된 사거리(항상 |TargetX - StandX|).</summary>
            public readonly float Distance;
            /// <summary>이 구간에서 물리적으로 가능한 최대 사거리 — 진단/로그용.</summary>
            public readonly float MaxAvailableDistance;

            public Placement(bool ok, float standX, float targetX, float facing, float distance, float maxAvailable)
            {
                Ok = ok;
                StandX = standX;
                TargetX = targetX;
                Facing = facing;
                Distance = distance;
                MaxAvailableDistance = maxAvailable;
            }

            public static readonly Placement None = new Placement(false, 0f, 0f, 1f, 0f, 0f);
        }

        /// <summary>
        /// ★ 사거리 추첨 + 자리 계산의 <b>순수 함수 본체</b>(2026-08-31 신고 "무조건 화면 끝" 수정의 핵심).
        /// MonoBehaviour/씬/카메라에 전혀 의존하지 않으므로 EditMode에서 수천 번 표본을 뽑아
        /// <b>분포 자체</b>를 검사할 수 있다 — "돌아갈 것 같다"가 아니라 통계로 잠근다.
        ///
        /// 계약:
        ///   · 반환 사거리는 항상 [<paramref name="minDistance"/>, min(<paramref name="maxDistance"/>,
        ///     구간이 허용하는 최대)] 안이다. <paramref name="roll01"/>에 대해 <b>선형</b>이므로
        ///     균등 난수를 넣으면 사거리도 균등 분포다(한쪽 극단으로 쏠리지 않는다).
        ///   · 캐릭터와 과녁은 둘 다 구간 안에 있고, 각자의 여백(<paramref name="charInset"/>,
        ///     <paramref name="targetInset"/>)을 지킨다.
        ///   · 최소 사거리조차 안 나오면 <see cref="Placement.None"/>.
        /// </summary>
        /// <param name="footX">캐릭터 현재 발바닥 월드 X.</param>
        /// <param name="lo">쓸 수 있는 구간의 왼쪽 끝(월드 X).</param>
        /// <param name="hi">쓸 수 있는 구간의 오른쪽 끝(월드 X).</param>
        /// <param name="charInset">캐릭터가 구간 끝에서 남겨야 할 여백.</param>
        /// <param name="targetInset">과녁이 구간 끝에서 남겨야 할 여백(반지름 포함).</param>
        /// <param name="backStep">쏘기 전에 과녁 반대쪽으로 물러서는 거리(월드 유닛). 0이면 제자리.</param>
        /// <param name="minDistance">랜덤 사거리 밴드의 하한(월드 유닛).</param>
        /// <param name="maxDistance">랜덤 사거리 밴드의 상한(월드 유닛).</param>
        /// <param name="roll01">0~1 난수. 프로덕션은 Random.value, 테스트는 시드 난수를 넣는다.</param>
        public static Placement ResolvePlacement(float footX, float lo, float hi,
            float charInset, float targetInset, float backStep, float minDistance, float maxDistance, float roll01)
        {
            if (!(hi > lo)) return Placement.None;

            float standLo = lo + charInset;
            float standHi = hi - charInset;
            float targetLo = lo + targetInset;
            float targetHi = hi - targetInset;
            if (standHi < standLo || targetHi < targetLo) return Placement.None;

            minDistance = Mathf.Max(0.0001f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);

            // 오른쪽/왼쪽 각각 물리적으로 가능한 최대 사거리. 예전 코드는 이 값을 **그대로** 썼다
            // (=항상 최대) — 그게 사용자가 본 "무조건 화면 끝"이다.
            float spanRight = targetHi - standLo;
            float spanLeft = standHi - targetLo;

            // 방향: 구간이 더 넓게 남은 쪽(=지금 위치에서 먼 쪽 끝)을 향해 쏜다. 예전과 같은 규칙이라
            // "화면 밖으로 나가지 않는다"는 성질이 유지된다.
            float facing = Mathf.Abs(footX - lo) <= Mathf.Abs(hi - footX) ? 1f : -1f;
            float span = facing > 0f ? spanRight : spanLeft;
            if (span < minDistance)
            {
                // 그쪽이 좁으면 반대편으로 미러링한 뒤에도 안 되면 포기(기존 우선순위 유지).
                facing = -facing;
                span = facing > 0f ? spanRight : spanLeft;
                if (span < minDistance) return Placement.None;
            }

            float bandHi = Mathf.Min(maxDistance, span);
            float distance = Mathf.Lerp(minDistance, bandHi, Mathf.Clamp01(roll01));

            // 서는 자리: 지금 위치에서 가장 가까운 유효 지점(불필요한 도보 없음).
            // 제약 = 캐릭터도 구간 안 + (캐릭터 X + facing×사거리)도 과녁 여백 안.
            float slotLo, slotHi;
            if (facing > 0f)
            {
                slotLo = standLo;
                slotHi = Mathf.Min(standHi, targetHi - distance);
            }
            else
            {
                slotLo = Mathf.Max(standLo, targetLo + distance);
                slotHi = standHi;
            }
            if (slotHi < slotLo)
            {
                // ★ 부동소수 방어(EditMode 실측으로 잡힌 결함). 사거리가 구간 최대(span)와 같아지는
                // 추첨(roll≈1)에서는 수학적으로 slotLo == slotHi여야 하는데, float 연산
                // (Mathf.Lerp(a,b,1f) = a+(b-a)가 b와 정확히 같지 않다)에서 1e-7 단위로 뒤집힌다.
                // 그대로 두면 "좁은 창에서 가장 먼 사거리를 뽑았을 때만" 활쏘기가 조용히 취소되는
                // 재현 난이도 최상급 버그가 된다. 오차 범위 안이면 두 값을 한 점으로 합친다.
                if (slotLo - slotHi > EdgeEpsilon) return Placement.None;
                float pinned = (slotLo + slotHi) * 0.5f;
                slotLo = pinned;
                slotHi = pinned;
            }

            // 자리가 남아 있으면 과녁 반대쪽으로 <b>한 걸음 물러선다</b>. 자리가 없으면 그만큼 덜
            // 물러선다(클램프). 예전처럼 구간 끝까지 행진하지는 않는다 — BackStepRatio 문서 참고.
            float desiredStand = footX - facing * Mathf.Max(0f, backStep);
            float standX = Mathf.Clamp(desiredStand, slotLo, slotHi);
            float targetX = standX + facing * distance;
            return new Placement(true, standX, targetX, facing, Mathf.Abs(targetX - standX), span);
        }

        /// <summary>
        /// 지금 딛고 있는 발판이 <b>진짜 창(Dock 포함)</b>인지, 화면 최하단 바닥 안전망(합성 발판)인지.
        /// 안전망 핸들 상수는 Platform/FallbackPlatformWindowService.cs가 소유한다 — 여기서 숫자를
        /// 다시 적지 않는다(같은 값을 두 곳에서 정의해 어긋난 전례가 이 프로젝트에 두 번 있다).
        /// </summary>
        public static bool IsRealWindowFoothold(long handle)
        {
            if (handle == FallbackPlatformWindowService.SyntheticFootholdHandle) return false;
            if (handle == FallbackPlatformWindowService.SyntheticFootholdHandleRight) return false;
            return handle != 0L; // Dock(-2)과 실제 창(양수 핸들)은 둘 다 "창"으로 취급한다(사용자 명시).
        }

        /// <summary>캐릭터가 발판 끝에서 안쪽으로 남겨두는 여유(신장 배수). 끝에 딱 붙으면 배회 AI의
        /// 경계 판정에 걸려 뛰어내리거나 되돌아선다.</summary>
        internal const float CharacterEdgeInsetRatio = 0.35f;

        /// <summary>과녁이 발판 끝에서 안쪽으로 남겨두는 여유(반지름에 더해지는 신장 배수).</summary>
        internal const float TargetEdgeInsetRatio = 0.20f;

        /// <summary>
        /// 쏘기 전에 과녁 반대쪽으로 <b>물러서는</b> 거리(신장 배수).
        ///
        /// 왜 있는가: 사용자가 확정한 연출 순서는 "<b>이동</b> -> 과녁 생성 -> 발사"다
        /// (States/ArcheryState의 Approach 페이즈, PlayMode 테스트가 '발동 직후에는 과녁이 보이면 안
        /// 된다'로 잠가 놨다). 2026-08-31 수정으로 사거리를 추첨하게 되면서 "구간 끝까지 행진"은
        /// 없앴는데, 그러면 이동이 <b>0</b>이 되어 그 순서가 통째로 사라진다. 그래서 행진 대신
        /// <b>한 걸음(신장 1배 ≈ 0.7초)</b>만 물러선다 — 활 쏘기 전에 거리를 재는 자연스러운 동작이면서,
        /// 매번 화면 가장자리로 걸어가는 옛 그림(신고 문구의 나머지 절반)도 되돌아오지 않는다.
        /// 뒤에 자리가 없으면 그만큼만 물러선다(사거리는 물러선 결과와 무관하게 이미 확정돼 있다).
        /// </summary>
        internal const float BackStepRatio = 1.0f;

        /// <summary>
        /// 과녁 중심의 로컬 높이(발바닥 기준). <b>반지름에서 유도</b>되며 별도 설정값이 아니다:
        /// 과녁 꼭대기(centerY + radius)가 정확히 캐릭터 정수리 높이가 되도록 잡는다. 그래서
        /// 배율을 어떻게 바꿔도 "과녁은 캐릭터와 같은 키"라는 관계가 유지되고, 화면 세로 판정이
        /// 캐릭터 자신의 판정과 같아진다(둘이 따로 놀 경우의 수가 없다).
        /// </summary>
        public static float TargetCenterHeight(float characterHeight, float radius) => characterHeight - radius;

        // ============================================================================
        // 생애주기
        // ============================================================================

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (evt.From != StickmanStateId.Archery) return;
            if (!_active) return;
            _active = false;
            RaiseOverlay(evt.IsForcedInterrupt ? SpectacleOverlayPhase.Cancelled : SpectacleOverlayPhase.Completed);
            _cooldownRemaining = _config != null ? _config.archeryCooldownSeconds : 600f;
            SpectacleEventLock.Release(this);

            Debug.Log($"[활쏘기] 종료 — {evt.To}(으)로 전이(강제인터럽트={evt.IsForcedInterrupt}). " +
                $"과녁/화살 오버레이를 걷고, 다음 **자율** 발동까지 {_cooldownRemaining:F0}초 쿨다운을 겁니다" +
                $"(자율 발동 확률은 기본 0이라 실제로는 단축키 Ctrl+Opt+Cmd+A / 우클릭 메뉴로만 다시 볼 수 있습니다).");
        }

        private void OnEmergencyStop()
        {
            if (SpectacleEventLock.CurrentOwner != (object)this) return;
            if (_player == null) return;
            if (_player.Blackboard.Machine.CurrentStateId == StickmanStateId.Archery)
            {
                _player.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
        }

        private void RaiseOverlay(SpectacleOverlayPhase phase)
            => StickmanEventBus.RaiseArcheryOverlayChanged(_targetWorld, _groundWorldY, _facing, phase);
    }
}
