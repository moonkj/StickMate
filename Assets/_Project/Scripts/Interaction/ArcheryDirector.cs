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
    /// 부채꼴 ④[행동] 창의 [활쏘기]로만</b> 발동한다. 자동 추첨 코드는 남겨두되(값을 올리면 즉시 살아난다)
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
    /// 캐릭터가 화면 끝에 서 있으면 정면에 과녁을 놓을 자리가 없다. 그때는 이 우선순위로 처리한다:
    /// (1) 정면에 자리가 있으면 정면, (2) 없으면 반대편으로 미러링, (3) 양쪽 다 안 되면
    /// <b>조용히 발동을 포기</b>한다. (2026-09-02까지는 격파 미니게임이 같은 규칙을 공유했고,
    /// 거기에만 있던 "빠듯하면 클램프해서라도 그린다" 네 번째 갈래를 활쏘기가 <b>일부러 쓰지
    /// 않았다</b>. 그 이유는 지금도 유효하다: 활쏘기는 과녁만이 아니라
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

        /// <summary>화면 가장자리에서 남겨둘 최소 여백(월드 유닛, 약 4pt).</summary>
        private const float ScreenEdgePadWorld = 0.10f;

        /// <summary>자리 계산에서 "같은 점"으로 볼 부동소수 오차(월드 유닛, 약 0.04pt).
        /// <see cref="ResolvePlacement"/>의 경계 처리에만 쓴다.</summary>
        private const float EdgeEpsilon = 0.001f;

        /// <summary>
        /// ★ <b>가용성 조회는 난수를 소비하지 않는다</b>(2026-09-02). <see cref="GetAvailability"/>는
        /// "자리가 있는가"라는 <b>결정론적</b> 질문인데, 행동 명령창이 0.25초마다 폴링하므로 예전에는
        /// <c>Random.value</c>를 초당 4회 먹었다. 그러면 시드 고정 테스트를 원리적으로 만들 수 없다
        /// (폴링 횟수가 프레임레이트에 좌우된다).
        ///
        /// <para><b>판정이 안 뒤집히는 근거(코드로 확인함, 설계자 주장에 기대지 않았다)</b>:
        /// <see cref="ResolvePlacement"/>가 <see cref="Placement.None"/>을 내는 갈래는 네 개인데
        /// 앞의 세 개(<c>hi &lt;= lo</c> / 여백 뒤집힘 / <c>span &lt; minDistance</c>)는 roll을 아예
        /// 읽지 않는다. roll이 닿는 곳은 <c>slotHi &lt; slotLo</c> 가드 하나뿐이고, 거기서도
        /// <c>distance ≤ span</c>이 수학적으로 보장되므로(밴드 상·하한이 둘 다 span 이하) 실수 연산에서는
        /// 항상 <c>slotLo ≤ slotHi</c>다. float 오차(월드 좌표 규모 ~40유닛에서 약 4e-6)는
        /// <see cref="EdgeEpsilon"/> 0.001에 통째로 삼켜져 한 점으로 합쳐지고 <c>None</c>이 되지 않는다.
        /// → <b>어떤 고정값을 넣어도 <c>Ok</c>가 같다.</b>
        /// Tests/EditMode/ArcheryTargetDistanceTests가 폭·발위치·비율 스윕으로 이 동치를 전수 확인한다.</para>
        ///
        /// <para>★ 2026-09-06 <b>방향 추첨값도 같은 고정값을 쓴다</b>. 근거가 하나 더 단단하다:
        /// 좌우 여유는 <b>항상 정확히 같으므로</b>(<c>spanRight − spanLeft ≡ 0</c>) 방향이 결정하는 것은
        /// "설 자리"뿐이고, 두 방향이 다 막히는 경우에만 <c>None</c>인데 그건 방향 추첨이 아니라
        /// 사거리(<c>roll</c>)와 구간 폭이 정한다. 즉 <b>dirRoll은 Ok를 물리적으로 못 바꾼다.</b></para>
        /// </summary>
        private const float AvailabilityProbeRoll = 0.5f;

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

            // ★★★ 2026-09-03 — <b>캐릭터가 안 보이면 캐릭터가 하는 일도 못 시킨다</b>(원칙 1).
            //   근거·대상·비대상은 Core/HiddenCharacterCommandGate.cs 한 곳에 있다.
            //   가드 값은 <c>IsSuspended</c>다 — <c>HidesScreenSurfaces</c>로 바꾸면 사용자 명시
            //   숨김에서 안 막히고, 그게 정확히 이 줄이 고치는 결함이다.
            if (HiddenCharacterCommandGate.BlocksNow(_player)) return HiddenCharacterCommandGate.WhileHidden;

            if (SpectacleEventLock.IsActive)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(SpectacleEventLock.ActiveKind));

            StickmanStateId current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
                return CommandAvailability.Blocked(StickMateDisplayNames.BusyText(current));

            // ★ 고정 roll — 위 AvailabilityProbeRoll 문서 참고. 난수도, 문자열도 소비하지 않는다.
            //   방향 추첨값도 같은 고정값을 준다: 좌우 여유가 항상 정확히 같으므로(ResolvePlacement의
            //   span 계산 주석) 방향은 Ok 판정에 물리적으로 영향을 줄 수 없다.
            if (!TryResolvePlacement(out _, out _, out _, out _, AvailabilityProbeRoll, AvailabilityProbeRoll))
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

            // 사거리와 **좌우 방향**을 매번 추첨하므로 여기서 한 번 더 부른다 — 위 판정은 "자리가
            // 있는가"(결정론적, 고정 roll)이고 이 호출은 "이번에 쓸 좌표"(추첨)다. 두 추첨값 모두 Ok를
            // 못 바꾸므로(AvailabilityProbeRoll 문서) 두 판단이 어긋날 수 없다.
            if (!TryResolvePlacement(out Placement placed, out Vector2 target, out float groundY,
                    out string kindLabel, Random.value, Random.value))
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — {NoPlacementReason}(좌표 재계산 단계).");
                return false;
            }

            Begin(placed, target, groundY, kindLabel, reason);
            return true;
        }

        private void Begin(in Placement placed, Vector2 target, float groundY, string kindLabel, string reason)
        {
            if (!SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, this)) return;

            float standX = placed.StandX;
            float facing = placed.Facing;

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

            // ★ 사거리 로그는 **여기**에 있다(2026-09-02 이전에는 TryResolvePlacement 안에 있었다).
            //   왜 옮겼나: 그 자리에서는 행동 명령창의 0.25초 폴링(GetAvailability)마다 찍혀
            //   "메뉴를 열어둔 것만으로 3분에 최대 720줄"이 쌓였고, 게다가 **그 줄의 대부분이 실제로는
            //   버려진 추첨값**이었다 — 계기가 거짓말을 하고 있었다. 이제 한 사이클당 정확히 한 줄이고,
            //   전부 화면에 실제로 나간 값이다.
            //   조건(예전의 `상한 미달일 때만`)도 없앴다. 그 조건은 넓은 발판 표본을 잘라내 로그 분석에
            //   절단 편향을 넣는다.
            // ★ 화면 폭 대비 비율을 함께 찍는다 — 2026-09-06 신고("너무 가깝다")가 신장 배수로는
            //   보이지 않고 **화면 대비**로만 보이는 결함이었기 때문이다. 실기 판정은 이 %로 한다.
            float screenWidth = _player.Blackboard != null &&
                _player.Blackboard.TryGetWalkableScreenBoundsWorld(out float sl, out float sr)
                ? Mathf.Max(0.0001f, sr - sl) : 0f;
            // ★ 라벨은 «무엇이 하한을 실제로 잡고 있는가»를 말해야 한다. 화면 비례 바닥이 요청한 값과
            //   실제로 적용된 값이 다르면(= 붕괴 클램프에 눌렸으면) 그 사실을 같이 찍는다 —
            //   그게 없으면 "처방이 안 걸렸다"를 로그에서 구분할 수 없다(실기 14회 전부가 이 경우였다).
            float requestedScreenFloor = screenWidth * MinTargetDistanceScreenFraction;
            string floorLabel;
            switch (placed.BandLoSource)
            {
                case FloorSource.Screen:
                    floorLabel = $"★화면비례 {MinTargetDistanceScreenFraction:P0}×화면폭";
                    break;
                case FloorSource.Span:
                    floorLabel = $"폭비례 {MinDistanceSpanFraction:F2}×기준상한" +
                        (screenWidth > 0f && requestedScreenFloor > placed.BandLo + EdgeEpsilon
                            ? $" (화면비례 요청 {requestedScreenFloor:F2}유닛은 밴드 붕괴 클램프 " +
                              $"{MinDistanceSpanFraction:F2}×상한에 눌려 **적용 안 됨** — 발판이 좁다)"
                            : string.Empty);
                    break;
                default:
                    floorLabel = $"절대 {MinDistanceRatio:F2}H";
                    break;
            }
            Debug.Log($"[활쏘기] 사거리 추첨 {placed.Distance:F2}유닛 " +
                $"(밴드 {placed.BandLo:F2}~{placed.BandHi:F2}유닛, 구간 폭이 허용한 최대 " +
                $"{placed.MaxAvailableDistance:F2}유닛, {kindLabel}). " +
                $"밴드 하한 근거={floorLabel}={placed.BandLo / Mathf.Max(0.0001f, Height):F2}H" +
                (screenWidth > 0f
                    ? $". 화면 폭 대비 = 사거리 {placed.Distance / screenWidth:P1} / 밴드 " +
                      $"{placed.BandLo / screenWidth:P1}~{placed.BandHi / screenWidth:P1}"
                    : string.Empty) +
                $". 방향={(facing > 0f ? "오른쪽" : "왼쪽")}(매 발동 추첨).");
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
            if (!TryResolvePlacement(out Placement placed, out Vector2 target, out float groundY,
                    out string kindLabel, Random.value, Random.value)) return;

            Begin(placed, target, groundY, kindLabel, "자동 추첨");
        }

        // ============================================================================
        // 과녁 자리 선정
        // ============================================================================

        private float Height => _player != null ? _player.Blackboard.CharacterHeightWorld
                                                : StickConfig.BaselineCharacterTotalHeight;

        private float TargetRadius => Height * RadiusRatio;

        private float RadiusRatio => _config != null ? Mathf.Clamp(_config.archeryTargetRadiusRatio, 0.05f, 0.9f) : 0.40f;

        /// <summary>
        /// <b>발동 가부를 가르는 절대 하한</b>(신장 배수). 이보다 가까우면 궁수와 과녁이 한 덩어리로
        /// 붙어 "쏜다"가 아니라 "옆에 서 있다"로 읽히므로 차라리 발동하지 않는다.
        ///
        /// <para>★ 2026-09-02: 더 이상 <b>랜덤 밴드의 하한이 아니다</b>. 실제 밴드 하한은
        /// <see cref="MinDistanceSpanFraction"/>이 발판 폭에서 유도하고, 이 값은 그 아래를 받치는
        /// 바닥이자 <b>포기 판정 전용</b>이다. 둘을 분리한 이유: 이 값을 올려 "너무 가깝다"를 고치면
        /// 좁은 발판(macOS 바닥 안전망 조각 227pt 등)에서 활쏘기가 통째로 사라진다.</para>
        /// </summary>
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
        /// ★ 랜덤 사거리 밴드의 하한을 <b>그 발판이 허용하는 최대 사거리에 비례</b>시키는 비율
        /// (2026-09-02 사용자 신고 대응: "활쏘기가 너무 가까이 과녁이 생기는 경향이 있어 최소거리를
        /// 좀더 늘려야 할거 같아 <b>창위에서는 창길이에 따라 변해야겠지만</b>").
        ///
        /// <para>상한 0.9 클램프는 밴드가 한 점으로 붕괴해 "거리는 항상 랜덤"(2026-08-31 사용자 명시)이
        /// 죽는 것을 막는다. 0이면 2026-09-02 이전 동작으로 정확히 되돌아간다(킬 스위치).</para>
        /// </summary>
        private float MinDistanceSpanFraction => _config != null
            ? Mathf.Clamp(_config.archeryMinDistanceSpanFraction, 0f, MaxMinDistanceSpanFraction)
            : 0.55f;

        /// <summary>밴드 붕괴 방어선 — 이 이상이면 하한이 상한에 붙어 사거리가 사실상 고정값이 된다.</summary>
        internal const float MaxMinDistanceSpanFraction = 0.9f;

        /// <summary>
        /// ★ 랜덤 사거리 밴드의 <b>상한</b>을 발판 폭에 비례시키는 비율(g). 2026-09-06 사용자 재신고
        /// ("활쏘기 과녁이 캐릭터와 너무 가까운 위치에 생김")의 <b>선결 조건</b>이다 — 하한은 언제나
        /// <see cref="MinDistanceSpanFraction"/> × 상한 이하로 클램프되므로,
        /// <b>상한이 절대치인 한 하한도 절대치</b>이고 화면 대비 사거리는 배율에 끌려다닌다.
        /// </summary>
        private float MaxDistanceSpanFraction => _config != null
            ? Mathf.Clamp(_config.archeryMaxDistanceSpanFraction, 0f, MaxMaxDistanceSpanFraction)
            : 0.45f;

        /// <summary>
        /// ★ "무조건 화면 끝" 재발 방지 <b>구조 상수</b>(2026-08-31 신고). 방향 규칙상 캐릭터 앞에
        /// 남는 여유는 항상 <c>0.5 × 발판폭 + 0.875H</c> 이상이므로, 폭 비례 상한이 발판 폭의 절반을
        /// 넘지 않으면 <b>어떤 추첨·어떤 위치에서도</b> 과녁이 구간 끝에 못박히지 않는다.
        /// <see cref="MaxMinDistanceSpanFraction"/>과 같은 성격이라 설정이 아니라 코드에 박는다.
        /// </summary>
        internal const float MaxMaxDistanceSpanFraction = 0.5f;

        /// <summary>폭 비례 상한의 절대 천장(Ucap). <b>공간이 아니라 연출이 정한다</b> —
        /// 이 위로는 앞 화살이 착탄하기 전에 다음 화살이 떠나 "한 발 = 한 박자"가 깨진다
        /// (근거는 StickConfig.archeryMaxDistanceHardCapRatio 툴팁). 기준 상한보다 낮게 설정되면
        /// 무시한다 — 천장이 바닥을 뚫는 조합에서 밴드가 뒤집히지 않게.</summary>
        private float MaxDistanceHardCapRatio => _config != null
            ? Mathf.Max(MaxDistanceRatio, _config.archeryMaxDistanceHardCapRatio)
            : 13.4f;

        /// <summary>
        /// ★★ 최소 사거리의 <b>화면 폭 비율</b> 바닥(s). 2026-09-06 사용자 재신고의 직접 처방이다.
        ///
        /// <para>기존 하한이 전부 <b>신장 배수</b>였다는 것이 이 신고의 물리적 실체다 —
        /// 캐릭터를 작게 쓰는 사용자일수록 과녁이 <b>화면상</b> 가까워진다. 같은 실효 하한 "3.63H"가
        /// 1512pt 화면에서 <b>배율 0.75에 16.8%W(253pt), 0.45에 10.1%W(152pt), 0.35에 7.8%W(118pt)</b>로
        /// 미끄러진다. 신장 배수로만 보면 셋 다 같은 3.63H라 <b>세 번의 신고 동안 아무도 못 봤다.</b>
        /// 화면 비율로 바닥을 깔면 하한이 배율과 무관해진다(1512pt 화면에서 333pt = 22%).</para>
        /// </summary>
        private float MinTargetDistanceScreenFraction => _config != null
            ? Mathf.Clamp(_config.archeryMinTargetDistanceScreenFraction, 0f, MaxMinTargetDistanceScreenFraction)
            : 0.22f;

        /// <summary>화면 비례 바닥의 설정 상한. 이 위는 <see cref="ResolvePlacement"/>의 밴드 붕괴
        /// 클램프(<c>s ≤ f × 밴드상한</c>)에 통째로 먹혀 구조적으로 무효라, 설정창에서 올려 봐야
        /// 아무 일도 일어나지 않는 구간을 잘라 둔다.</summary>
        internal const float MaxMinTargetDistanceScreenFraction = 0.30f;

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
        ///   3) 2026-09-02 "활쏘기가 <b>너무 가까이</b> 과녁이 생기는 경향이 있어 최소거리를 좀더 늘려야
        ///      할거 같아 <b>창위에서는 창길이에 따라 변해야겠지만</b>" → 밴드 하한을 발판 폭에 비례시켰다
        ///      (<c>archeryMinDistanceSpanFraction</c>).
        ///   4) ★★ 2026-09-06 <b>같은 신고가 세 번째로 올라왔다</b> — "활쏘기 과녁이 캐릭터와 너무
        ///      가까운 위치에 생김 / 캐릭터로부터 최소 거리 확보(<b>화면 폭 비율 기준</b>) /
        ///      <b>거리와 좌우 방향 둘 다 매번 랜덤</b>".
        ///      <para>(3)이 왜 안 먹혔는지가 이번 라운드의 진단이다: 하한은 <c>f × 밴드상한</c>인데
        ///      <b>상한이 신장 배수(절대치)</b>라 하한도 절대치였고, 그래서 <b>캐릭터를 작게 쓰는
        ///      사용자일수록 화면상 가까워졌다</b> — 하한 3.63H가 <b>출하 배율 0.75에서 화면 폭의
        ///      16.8%, 0.45에서 10.1%, 0.35에서 7.8%</b>로 미끄러진다.
        ///      신장 배수로만 보면 3.63H는 "충분히 멀다"로 읽혀서 아무도 못 봤다.</para>
        ///
        /// 지금 규칙:
        ///   · 밴드 상한 = min(구간이 허용하는 최대, max(6.6H, min(g×구간폭, Ucap 13.4H))).
        ///     폭 비례 항 g가 넓은 발판에서 상한을 열고, Ucap이 <b>연출</b>(한 발 = 한 박자)로 천장을 친다.
        ///   · 밴드 하한 = max(2.6H, f×min(상한,6.6H), <b>min(s×화면폭, f×상한)</b>).
        ///     ★ 세 번째 항이 2026-09-06 처방이다 — 바닥이 <b>화면 폭</b>에서 오므로 배율과 무관하게
        ///     "화면의 22%"가 보장되고, 좁은 창에서는 <c>f×상한</c> 클램프가 자동으로 낮춘다
        ///     (= 사용자가 09-02에 양보절로 말한 "창길이에 따라 변한다").
        ///   · 사거리 d = Lerp(하한, 상한, 난수) — 매 발동 추첨, 균등.
        ///   · ★ <b>좌우 방향도 매 발동 추첨</b>이다. 후보는 "여분의 도보가 필요 없는 방향"뿐이라
        ///     과녁이 구간 끝에 못박히지 않는다(2026-08-31 방어선 유지). 한쪽만 가능하면 그쪽,
        ///     둘 다 빠듯하면 <b>덜 걷는 쪽</b>으로 폴백한다(예전 "넓게 남은 쪽" 규칙과 같은 결과).
        ///   · 구간(발판 ∩ 걸어다닐 수 있는 화면 범위)이 좁아 밴드 상한을 못 채우면 <b>들어가는 만큼만</b>
        ///     줄인다. <b>절대 하한 2.6H조차 안 나오면</b> 조용히 발동을 포기한다(코앞에 억지로
        ///     놓지 않는다 — 그러면 포물선이 직선처럼 보인다). ★ 포기를 가르는 것은 <b>2.6H 하나뿐</b>이며
        ///     f·g·s·Ucap은 그 판정을 읽지 않는다 = <b>이번 변경으로 포기 빈도는 한 건도 안 는다.</b>
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
        private bool TryResolvePlacement(out Placement placed, out Vector2 target, out float groundY,
            out string kindLabel, float roll01, float dirRoll01)
        {
            placed = Placement.None;
            target = default;
            groundY = 0f;
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

            // ★★ 2026-09-06 — 최소 사거리의 바닥 하나가 **화면 폭**에서 온다(사용자 재신고 처방).
            //    기준은 발판(창) 폭이 아니라 **걸어다닐 수 있는 화면 폭**이다: 좁은 창 위에 서 있어도
            //    "화면에서 이만큼은 떨어져 보여야 한다"는 요구가 창 크기에 좌우되면 안 되기 때문이다.
            //    (창이 좁아 그만큼을 못 쓰는 경우는 ResolvePlacement 안의 f × 밴드상한 클램프가
            //     자동으로 낮춰 준다 — 그쪽이 사용자가 말한 "창길이에 따라 변한다"이다.)
            float screenFloor = Mathf.Max(0f, screenRight - screenLeft) * MinTargetDistanceScreenFraction;

            // ★ 난수는 호출자가 준다. 발동 경로는 Random.value, 가용성 폴링은 고정
            //   AvailabilityProbeRoll이다(그 상수 문서에 판정 불변 근거가 있다). 나머지 계산은 전부
            //   순수 함수라 EditMode에서 시드를 바꿔가며 분포를 직접 검사할 수 있다
            //   (Tests/EditMode/ArcheryTargetDistanceTests.cs).
            // ★ 로그는 여기서 찍지 않는다 — 폴링 경로가 초당 4줄을 쏟아내고 그 값들은 전부 버려진다.
            //   실제로 쓰인 값만 Begin()이 한 줄 남긴다.
            placed = ResolvePlacement(foot.x, lo, hi,
                height * CharacterEdgeInsetRatio,
                radius + height * TargetEdgeInsetRatio,
                height * BackStepRatio,
                minDistance, maxDistance, MinDistanceSpanFraction,
                MaxDistanceSpanFraction, height * MaxDistanceHardCapRatio, screenFloor,
                roll01, dirRoll01);
            if (!placed.Ok) return false;

            float targetX = placed.TargetX;

            float centerY = groundY + TargetCenterHeight(height, radius);
            Camera cam = blackboard.MainCamera;
            if (cam != null && cam.orthographic)
            {
                float topY = cam.transform.position.y + cam.orthographicSize - ScreenEdgePadWorld;
                if (centerY + radius > topY) { placed = Placement.None; return false; }
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

            /// <summary>이번 추첨에 실제로 쓰인 밴드의 하한(월드 유닛). 절대 하한과 폭 비례 하한 중
            /// 큰 쪽이다 — 어느 쪽이 걸렸는지는 절대 하한과 비교하면 알 수 있다(로그/테스트용).</summary>
            public readonly float BandLo;

            /// <summary>이번 추첨에 실제로 쓰인 밴드의 상한(월드 유닛) = min(절대 상한, 구간 최대).</summary>
            public readonly float BandHi;

            /// <summary>밴드 하한을 실제로 결정한 바닥이 셋 중 무엇인가 — 로그/테스트 전용.
            /// 이것이 없으면 "하한이 왜 이 값인가"를 실기 로그에서 되짚을 수 없다.</summary>
            public readonly FloorSource BandLoSource;

            public Placement(bool ok, float standX, float targetX, float facing, float distance,
                float maxAvailable, float bandLo, float bandHi, FloorSource bandLoSource)
            {
                Ok = ok;
                StandX = standX;
                TargetX = targetX;
                Facing = facing;
                Distance = distance;
                MaxAvailableDistance = maxAvailable;
                BandLo = bandLo;
                BandHi = bandHi;
                BandLoSource = bandLoSource;
            }

            public static readonly Placement None =
                new Placement(false, 0f, 0f, 1f, 0f, 0f, 0f, 0f, FloorSource.Absolute);
        }

        /// <summary>밴드 하한을 결정한 바닥. 셋은 서로 배타가 아니라 <b>최댓값 경쟁</b>이며, 이긴 것이
        /// 여기 담긴다.</summary>
        public enum FloorSource
        {
            /// <summary><c>archeryMinTargetDistanceRatio</c> × 신장 — 발동 가부를 가르는 절대 바닥.</summary>
            Absolute = 0,
            /// <summary><c>archeryMinDistanceSpanFraction</c> × min(밴드상한, 기준상한) — 발판 폭 비례.</summary>
            Span = 1,
            /// <summary>★ <c>archeryMinTargetDistanceScreenFraction</c> × 화면 폭 — 2026-09-06 신설.</summary>
            Screen = 2,
        }

        /// <summary>
        /// ★ 사거리 추첨 + 자리 계산의 <b>순수 함수 본체</b>(2026-08-31 신고 "무조건 화면 끝" 수정의 핵심).
        /// MonoBehaviour/씬/카메라에 전혀 의존하지 않으므로 EditMode에서 수천 번 표본을 뽑아
        /// <b>분포 자체</b>를 검사할 수 있다 — "돌아갈 것 같다"가 아니라 통계로 잠근다.
        ///
        /// 계약:
        ///   · 밴드 상한 = min(구간이 허용하는 최대,
        ///     max(<paramref name="maxDistance"/>,
        ///         min(<paramref name="maxDistanceSpanFraction"/> × 구간폭, <paramref name="maxDistanceHardCap"/>))).
        ///   · 밴드 하한 = max(<paramref name="minDistance"/>,
        ///     <paramref name="minDistanceSpanFraction"/> × min(밴드 상한, <paramref name="maxDistance"/>),
        ///     min(<paramref name="minDistanceScreenFloor"/>,
        ///         <paramref name="minDistanceSpanFraction"/> × 밴드 상한)).
        ///     반환 사거리는 항상 그 안이고 <paramref name="roll01"/>에 대해 <b>선형</b>이므로
        ///     균등 난수를 넣으면 사거리도 균등 분포다(한쪽 극단으로 쏠리지 않는다).
        ///   · <b>밴드는 절대 붕괴하지 않는다</b>: 하한 ≤ <paramref name="minDistanceSpanFraction"/> × 상한이
        ///     구조적으로 성립하므로 밴드 폭이 언제나 상한의 (1 − f) 이상 남는다.
        ///   · 캐릭터와 과녁은 둘 다 구간 안에 있고, 각자의 여백(<paramref name="charInset"/>,
        ///     <paramref name="targetInset"/>)을 지킨다.
        ///   · <b>좌우 방향은 <paramref name="dirRoll01"/>로 추첨</b>하되, 후보는 "여분의 도보가 필요 없는
        ///     방향"(= 과녁이 구간 끝에 못박히지 않는 방향)뿐이다. 둘 다 자격이 없으면 덜 걷는 쪽.
        ///   · <b><paramref name="dirRoll01"/>은 <see cref="Placement.Ok"/>를 바꾸지 못한다</b> —
        ///     좌우 여유가 항상 정확히 같기 때문이다(본문 span 주석의 항등식).
        ///   · 최소 사거리조차 안 나오면 <see cref="Placement.None"/>.
        ///     ★ 그 판정은 <paramref name="minDistance"/>만 읽는다 —
        ///     f·g·s·Ucap 어느 것도 <b>포기 빈도를 바꾸지 않는다</b>.
        /// </summary>
        /// <param name="footX">캐릭터 현재 발바닥 월드 X.</param>
        /// <param name="lo">쓸 수 있는 구간의 왼쪽 끝(월드 X).</param>
        /// <param name="hi">쓸 수 있는 구간의 오른쪽 끝(월드 X).</param>
        /// <param name="charInset">캐릭터가 구간 끝에서 남겨야 할 여백.</param>
        /// <param name="targetInset">과녁이 구간 끝에서 남겨야 할 여백(반지름 포함).</param>
        /// <param name="backStep">쏘기 전에 과녁 반대쪽으로 물러서는 거리(월드 유닛). 0이면 제자리.</param>
        /// <param name="minDistance">발동 가부를 가르는 <b>절대</b> 하한(월드 유닛). 밴드 하한의 바닥이기도 하다.</param>
        /// <param name="maxDistance">랜덤 사거리 밴드의 <b>기준</b> 상한 U0(월드 유닛).</param>
        /// <param name="minDistanceSpanFraction">밴드 하한을 밴드 상한에 비례시키는 비율(0~1).
        /// 0이면 2026-09-02 이전 동작과 <b>비트 단위로</b> 같다.</param>
        /// <param name="maxDistanceSpanFraction">밴드 상한을 구간 폭에 비례시키는 비율 g
        /// (0~<see cref="MaxMaxDistanceSpanFraction"/>). <b>0이면 2026-09-06 이전과 비트 단위로 같다.</b></param>
        /// <param name="maxDistanceHardCap">위 폭 비례 상한의 절대 천장 Ucap(월드 유닛).
        /// <paramref name="maxDistance"/>보다 낮으면 무시한다.</param>
        /// <param name="minDistanceScreenFloor">최소 사거리의 <b>화면 폭 비례</b> 바닥(월드 유닛).
        /// 코드가 <c>f × 밴드상한</c>으로 다시 클램프하므로 밴드를 붕괴시키지 못한다.
        /// <b>0이면 2026-09-06 이전과 비트 단위로 같다.</b></param>
        /// <param name="roll01">0~1 난수. 프로덕션은 Random.value(가용성 조회는 고정
        /// <see cref="AvailabilityProbeRoll"/>), 테스트는 시드 난수를 넣는다.</param>
        /// <param name="dirRoll01">0~1 난수 — <b>좌우 방향</b> 추첨용(&lt; 0.5면 오른쪽).
        /// 두 방향이 다 자격이 있을 때만 쓰인다.</param>
        public static Placement ResolvePlacement(float footX, float lo, float hi,
            float charInset, float targetInset, float backStep, float minDistance, float maxDistance,
            float minDistanceSpanFraction, float maxDistanceSpanFraction, float maxDistanceHardCap,
            float minDistanceScreenFloor, float roll01, float dirRoll01)
        {
            if (!(hi > lo)) return Placement.None;

            float standLo = lo + charInset;
            float standHi = hi - charInset;
            float targetLo = lo + targetInset;
            float targetHi = hi - targetInset;
            if (standHi < standLo || targetHi < targetLo) return Placement.None;

            minDistance = Mathf.Max(0.0001f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);

            // ★ 2026-09-06 실측 정정 — <b>좌우 여유는 언제나 정확히 같다.</b>
            //   spanRight − spanLeft = (targetHi − standLo) − (standHi − targetLo)
            //                        = (−targetInset − charInset) − (−charInset − targetInset) = 0.
            //   즉 예전 코드의 "그쪽이 좁으면 반대편으로 미러링" 갈래는 **한 번도 판정을 바꾼 적이
            //   없는 죽은 가지**였다(같은 값을 다시 비교했다). 여기서 한 번만 계산해 부동소수
            //   비대칭까지 없앤다 — 방향 추첨이 좌우 대칭이라는 사실이 아래 로직의 전제다.
            float span = targetHi - standLo;
            if (span < minDistance) return Placement.None;

            // ============================================================================
            // 밴드 상한 — 폭 비례(g) + 절대 천장(Ucap)
            // ============================================================================
            // 2026-09-06 사용자 재신고("과녁이 캐릭터와 너무 가까운 위치에 생김")의 **선결 조건**이다.
            // 하한은 아래에서 언제나 f × 상한 이하로 클램프되므로, 상한이 절대치(6.6H)로 묶여 있는 한
            // 하한도 절대치이고 화면 대비 사거리가 배율에 끌려다닌다(출하 배율 0.75에서 상한 6.6H가 화면의 30.5%, 배율 0.45에서 18.3%).
            // g ≤ 0.5는 "무조건 화면 끝"(2026-08-31) 재발 방지 구조 상수다 — MaxMaxDistanceSpanFraction 문서.
            float g = Mathf.Clamp(maxDistanceSpanFraction, 0f, MaxMaxDistanceSpanFraction);
            float hardCap = Mathf.Max(maxDistance, maxDistanceHardCap); // Ucap이 기준 상한 아래면 무시.
            float bandHi = Mathf.Min(span, Mathf.Max(maxDistance, Mathf.Min(g * span, hardCap)));

            // ============================================================================
            // 밴드 하한 — 바닥 세 개의 최댓값
            // ============================================================================
            // (1) minDistance      절대 바닥. **발동 가부를 가르는 유일한 값**이기도 하다.
            // (2) f × min(상한,U0) 2026-09-02 폭 비례 하한. 기준 상한 U0로 잘라 두는 이유:
            //     이 하한이 지키는 것은 "활끝과 과녁 사이 빈 공간 ≥ 과녁 지름 2.5배"라는 **절대 기하**
            //     기준이지 폭 비율이 아니다. 상한이 열렸다고 함께 커질 이유가 없다.
            // (3) s × 화면폭       ★ 2026-09-06 신설. 화면 비례 바닥.
            //
            // ★ (3)을 f × bandHi로 클램프하는 것이 이 라운드의 안전장치 전부다:
            //   · 밴드가 붕괴하지 않는다 — bandLo ≤ f × bandHi ≤ 0.9 × bandHi이므로 밴드 폭이 언제나
            //     상한의 (1−f) 이상 남는다("거리는 항상 랜덤", 2026-08-31 사용자 명시).
            //   · 좁은 창에서 저절로 내려간다 — 상한이 폭에 눌리면 이 바닥도 함께 눌린다.
            //     사용자가 2026-09-02에 양보절로 말한 "창위에서는 창길이에 따라 변해야겠지만"이 그것이다.
            //   · **포기 빈도 변화 = 정확히 0** — 세 바닥이 전부 bandHi 이하이므로 밴드가 뒤집히지 않고,
            //     발동 가부는 위 `span < minDistance` 하나뿐이며 그 판정은 f·g·s·Ucap을 안 읽는다.
            float f = Mathf.Clamp01(minDistanceSpanFraction);
            float spanFloor = f * Mathf.Min(bandHi, maxDistance);
            float screenFloor = Mathf.Min(Mathf.Max(0f, minDistanceScreenFloor), f * bandHi);
            float bandLo = Mathf.Max(minDistance, Mathf.Max(spanFloor, screenFloor));

            // ★ 2026-09-06 실기에서 잡은 **보고 결함**: 처음에는 동률(screenFloor == spanFloor)을
            //   Screen으로 찍었는데, 그 동률은 대부분 «화면 비례 바닥이 f×상한에 눌려 폭 비례 바닥과
            //   같은 값이 된 것»이다. 즉 화면 비례가 이긴 게 아니라 **아무 일도 안 한 것**인데
            //   로그는 "★화면비례"라고 자랑했다. 실기 14회 전부가 그 상태였고 하마터면
            //   "처방이 걸렸다"로 읽을 뻔했다. 동률은 Span으로 찍는다 — <b>엄격히 클 때만</b> Screen이다.
            FloorSource floorSource = bandLo <= minDistance + EdgeEpsilon ? FloorSource.Absolute
                : screenFloor > spanFloor + EdgeEpsilon ? FloorSource.Screen
                : FloorSource.Span;

            float distance = Mathf.Lerp(bandLo, bandHi, Mathf.Clamp01(roll01));

            // ============================================================================
            // ★★ 좌우 방향 추첨 (2026-09-06 사용자 요구: "좌우 방향도 매번 랜덤")
            // ============================================================================
            // 예전 규칙은 "구간이 더 넓게 남은 쪽"이라 **캐릭터 위치의 순수 함수**였다 — 지금 x를 알면
            // 방향이 100% 정해졌고, 그게 "매번 같은 자리에 생긴다"의 절반이었다.
            //
            // ★★ 이 블록이 지키는 불변식은 하나다(EditMode 전수 확인, 6688/6688):
            //    <b>추첨은 두 방향의 접근 도보 비용이 «동률»일 때만 일어난다.</b>
            //    그래서 방향을 무작위로 골라도 **도보가 단 한 걸음도 늘지 않는다**
            //    (실측: 최대 도보 3.4209H로 구 규칙과 소수점까지 같다 = 2026-08-31 "화면 끝으로
            //     행진" 재발 없음, archeryApproachTimeoutSeconds 12초의 26%).
            //    동률이 되는 갈래는 둘뿐이다:
            //      (가) 양쪽 다 «여분의 도보 없음»(도보 = backStep) — 넓은 발판의 일반적인 경우
            //      (나) 양쪽 다 빠듯한데 비용이 같음 — 좁은 발판 정중앙. 어느 쪽을 골라도 같으므로
            //           예전처럼 한쪽으로 고정할 이유가 없다.
            //    ★ (가)에서 과녁은 구간 끝 여백을 **침범하지 않는다**(slotHi가 targetHi로 막는다).
            //      다만 «닿는» 경계 한 점은 존재한다 — footX − backStep == targetHi − distance인
            //      단일 지점. 못박힘 비율은 구 규칙 8.122% vs 신 규칙 8.156%로 실질 동일하고,
            //      그 8%는 전부 **좁은 발판에서 이미 있던 성질**이지 방향 추첨이 만든 것이 아니다.
            bool okRight = TryResolveStand(1f, footX, distance, backStep, standLo, standHi, targetLo, targetHi,
                out float standRight, out float travelRight);
            bool okLeft = TryResolveStand(-1f, footX, distance, backStep, standLo, standHi, targetLo, targetHi,
                out float standLeft, out float travelLeft);
            if (!okRight && !okLeft) return Placement.None;

            float freeTravel = Mathf.Max(0f, backStep) + EdgeEpsilon;
            bool freeRight = okRight && travelRight <= freeTravel;
            bool freeLeft = okLeft && travelLeft <= freeTravel;

            bool toRight;
            if (freeRight && freeLeft) toRight = dirRoll01 < 0.5f;          // ★ 여기가 추첨이다
            else if (freeRight) toRight = true;
            else if (freeLeft) toRight = false;
            else if (!okLeft) toRight = true;                                // 폴백: 되는 쪽
            else if (!okRight) toRight = false;
            else if (Mathf.Abs(travelRight - travelLeft) <= EdgeEpsilon) toRight = dirRoll01 < 0.5f;
            else toRight = travelRight < travelLeft;                         // 폴백: 덜 걷는 쪽

            float facing = toRight ? 1f : -1f;
            float standX = toRight ? standRight : standLeft;
            float targetX = standX + facing * distance;
            return new Placement(true, standX, targetX, facing, Mathf.Abs(targetX - standX), span,
                bandLo, bandHi, floorSource);
        }

        /// <summary>
        /// 한 방향에 대해 <b>서야 할 자리</b>와 그때 <b>걸어야 하는 거리</b>를 구한다.
        /// 제약 = 캐릭터도 구간 안 + (캐릭터 X + facing×사거리)도 과녁 여백 안. 자리가 남아 있으면
        /// 과녁 반대쪽으로 한 걸음 물러서고(<see cref="BackStepRatio"/>), 없으면 그만큼 덜 물러선다.
        /// </summary>
        /// <returns>그 방향으로 배치가 성립하는가. false면 <paramref name="standX"/>는 의미가 없다.</returns>
        private static bool TryResolveStand(float facing, float footX, float distance, float backStep,
            float standLo, float standHi, float targetLo, float targetHi,
            out float standX, out float travel)
        {
            standX = 0f;
            travel = float.MaxValue;

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
                if (slotLo - slotHi > EdgeEpsilon) return false;
                float pinned = (slotLo + slotHi) * 0.5f;
                slotLo = pinned;
                slotHi = pinned;
            }

            standX = Mathf.Clamp(footX - facing * Mathf.Max(0f, backStep), slotLo, slotHi);
            travel = Mathf.Abs(standX - footX);
            return true;
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
        ///
        /// ============================================================================
        /// ★ 알려진 계통 편향 — <b>실현 사거리가 언제나 뽑은 값보다 짧다</b> (2026-09-02 실측, 미조치)
        /// ============================================================================
        /// <para>여기서 정한 사거리는 <b>이상적인 서는 자리</b> 기준이다. 실제로는 States/ArcheryState의
        /// 도착 판정이 <c>|dx| ≤ ArriveToleranceRatio(0.12) × 신장</c>이라 그만큼 덜 걸어도 "도착"인데,
        /// 서는 자리가 <b>과녁 반대쪽</b>으로 물러선 지점(<see cref="BackStepRatio"/>)이므로
        /// <b>덜 걷는 방향이 언제나 과녁 쪽</b>이다 — 오차가 한쪽으로만 쌓인다.</para>
        ///
        /// <para>실기 4/4회 예외 없이 <b>−0.13유닛 = −0.095H</b>(배율 0.60에서 −5.3pt, 1.00에서 −8.9pt).
        /// 즉 <b>명목 하한 2.60H의 실현값은 2.505H로 약 4% 낙관적</b>이다.</para>
        ///
        /// <para><b>왜 이번 라운드에 안 고치는가</b>: 폭 비례 하한(<see cref="MinDistanceSpanFraction"/>)이
        /// 이 편향을 흡수한다 — 넓은 발판 하한 3.63H의 실현값 3.535H도 판정 기준(활끝~과녁 빈 공간이
        /// 과녁 지름 2.5배)을 여유 있게 넘는다(3.11배). 고치려면 <c>ArriveToleranceRatio</c>를 줄이는 게
        /// 아니라 <b>도착 지점에서 과녁 x를 다시 유도</b>해야 하는데(과녁은 Begin()에서 이미 고정된다)
        /// 비용 대비 효과가 낮다. <b>사실만 기록해 둔다 — 다음에 이 숫자로 놀라지 말라고.</b></para>
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

            // ★ 2026-09-02 — WindowTheftDirector와 같은 보강. IsForcedInterrupt만으로는 발판 상실
            // Fall(강제 인터럽트가 아니다)을 걸러내지 못해 "쏘다 굴러떨어짐"이 완료로 기록됐다.
            bool abnormal = evt.IsAbnormalExit;
            RaiseOverlay(evt.IsForcedInterrupt || abnormal
                ? SpectacleOverlayPhase.Cancelled
                : SpectacleOverlayPhase.Completed);
            if (!abnormal) _cooldownRemaining = _config != null ? _config.archeryCooldownSeconds : 600f;
            SpectacleEventLock.Release(this);

            Debug.Log($"[활쏘기] 종료 — {evt.To}(으)로 전이(강제인터럽트={evt.IsForcedInterrupt}, " +
                $"비정상이탈={abnormal}). 과녁/화살 오버레이를 걷고, " +
                (abnormal
                    ? "비정상 이탈이라 쿨다운을 걸지 않습니다"
                    : $"다음 **자율** 발동까지 {_cooldownRemaining:F0}초 쿨다운을 겁니다") +
                $"(자율 발동 확률은 기본 0이라 실제로는 단축키 {ShortcutLabel.Chord("A")} 또는 " +
                "기어 아이콘 → 부채꼴 ④[행동] → [활쏘기]로만 다시 볼 수 있습니다).");
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
