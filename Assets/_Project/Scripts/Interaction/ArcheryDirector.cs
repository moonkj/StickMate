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

        /// <summary>
        /// 활쏘기 강제 발동(전역 단축키 Ctrl+Opt+Cmd+A / 캐릭터 우클릭 메뉴). GraffitiDirector.
        /// ForceTriggerNow와 같은 관례로 <b>확률/쿨다운만</b> 건너뛴다 — 상호배제 락, Idle/Walk 진입
        /// 조건, 그리고 "과녁 자리가 화면 안에 없으면 발동하지 않는다"는 규칙은 하나도 완화하지 않는다.
        /// </summary>
        public void ForceTriggerNow(string reason)
        {
            if (_player == null || _config == null)
            {
                Debug.LogWarning($"[활쏘기] 강제 발동 실패({reason}) — 플레이어/설정 배선이 없습니다.");
                return;
            }
            if (SpectacleEventLock.IsActive)
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — 다른 스펙터클({SpectacleEventLock.ActiveKind})이 " +
                          "진행 중입니다(상호배제 락).");
                return;
            }

            var current = _player.Blackboard.Machine.CurrentStateId;
            if (current != StickmanStateId.Idle && current != StickmanStateId.Walk)
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — 지금은 {current} 중입니다(Idle/Walk에서만 시작).");
                return;
            }

            if (!TryResolvePlacement(out float standX, out Vector2 target, out float groundY,
                    out float facing, out string kindLabel))
            {
                Debug.Log($"[활쏘기] 강제 발동 건너뜀({reason}) — 좌우 어느 쪽에도 과녁을 온전히 놓을 " +
                    "자리가 없습니다. 자리 조건은 두 가지다: (1) 궤적 전체가 화면 안, " +
                    "(2) 과녁이 **지금 딛고 있는 발판(창)의 가로 범위 안**(밖이면 허공에 뜬다). " +
                    "딛고 있는 창이 좁으면 억지로 놓지 않고 조용히 포기합니다.");
                return;
            }

            Begin(standX, target, groundY, facing, kindLabel, reason);
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
        /// 아니라 "찌르는" 것처럼 보이므로 차라리 발동하지 않는다.</summary>
        private float MinDistanceRatio => _config != null ? Mathf.Max(0.5f, _config.archeryMinTargetDistanceRatio) : 2.6f;

        /// <summary>
        /// ★★ 배치 결정(2026-08-29 사용자 재정의): "과녁과 캐릭터 사이가 너무 가까운데서 행동을 함.
        /// 최소 바탕화면일경우는 화면 전체 길이의 절반 이상 떨어진곳만큼 캐릭터가 이동한 다음 과녁을
        /// 생성후 쏘고, 창 일 경우 그 창의 전체 길이의 끝으로 이동한 다음 반대쪽 끝쪽에 과녁 생성후
        /// 활쏘기".
        ///
        /// 두 경우는 <b>같은 형태</b>로 환원된다: 쓸 수 있는 가로 구간을 하나 정하고 <b>캐릭터를 한쪽 끝,
        /// 과녁을 반대쪽 끝</b>에 놓는다. 다른 것은 구간을 무엇으로 잡는가와 최소 거리 요구뿐이다.
        ///   · 창/Dock 위   : 구간 = <b>딛고 있는 그 발판의 실측 좌우 경계</b>(GroundSensor가 돌려주는
        ///                     CurrentFoothold*WorldX — 추정하지 않는다). 최소 거리는 신장 배수.
        ///   · 바닥(바탕화면): 구간 = 걸어다닐 수 있는 화면 범위
        ///                     (StickmanBlackboard.TryGetWalkableScreenBoundsWorld — 화면 끝 클램프와
        ///                     같은 유일한 생산자에서 나온다). 최소 거리는 <b>화면 폭의 절반</b>.
        /// 구간이 좁아 최소 거리조차 안 나오면 <b>조용히 발동을 포기</b>한다(허공/코앞에 억지로 놓지 않는다).
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
            float requiredDistance = onWindow
                ? height * MinDistanceRatio
                : Mathf.Max(height * MinDistanceRatio, (screenRight - screenLeft) * 0.5f);
            kindLabel = onWindow ? "창/Dock 발판의 양 끝" : "바탕화면(화면 폭의 절반 이상)";

            float charInset = height * CharacterEdgeInsetRatio;
            float targetInset = radius + height * TargetEdgeInsetRatio;
            if ((hi - targetInset) - (lo + charInset) <= 0f) return false;

            // 캐릭터는 자기가 더 가까운 쪽 끝으로 걸어가고, 과녁은 반대쪽 끝에 선다(걷는 거리가 짧다).
            bool standLeft = Mathf.Abs(foot.x - lo) <= Mathf.Abs(hi - foot.x);
            standX = standLeft ? lo + charInset : hi - charInset;
            float targetX = standLeft ? hi - targetInset : lo + targetInset;
            facing = standLeft ? 1f : -1f;

            float distance = Mathf.Abs(targetX - standX);
            if (distance < height * MinDistanceRatio) return false; // 코앞이면 아예 하지 않는다.
            if (distance + 0.001f < requiredDistance)
            {
                // 좁은 화면/발판 — 리더 승인대로 "확보 가능한 최대 거리"로 타협한다.
                Debug.Log($"[활쏘기] 요구 사거리 {requiredDistance:F2}유닛을 구간 폭이 감당하지 못해 " +
                    $"확보 가능한 최대 {distance:F2}유닛으로 타협합니다({kindLabel}).");
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
